using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CampaignRulesLayerIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task CampaignPinsGlobalRevisionUntilDmDeliberatelyMigratesAndPublishes()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var campaignId = Guid.NewGuid();
        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["dm-ticket"] = Context("campaign-dm", "Campaign DM", "dorks-and-dice", campaignId, "DM"),
            ["player-ticket"] = Context("campaign-player", "Campaign Player", "dorks-and-dice", campaignId, "Player"),
            ["outsider-ticket"] = Context("outsider", "Outsider", "dorks-and-dice"),
            ["wrong-mode-dm-ticket"] = Context("campaign-dm", "Campaign DM", "professional", campaignId, "DM")
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var packageKey = $"campaign-rules-public-{Guid.NewGuid():N}";
        var conceptKey = $"skill.arcana.campaign.{Guid.NewGuid():N}";
        Guid packageId = Guid.Empty;

        try
        {
            Guid conceptId;
            Guid sourceEntityId;
            Guid sourceRevision1Id;
            PublishedRulesetRevisionView globalRevision1;

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

                var imported = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(packageKey, isPublic: true, versionMarker: "one"));
                packageId = imported.PackageId;
                sourceEntityId = imported.Entities.Single().EntityId;
                sourceRevision1Id = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == sourceEntityId && value.RevisionNumber == 1)
                    .Select(value => value.Id)
                    .SingleAsync();

                var concept = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(conceptKey, "skill", "Arcana"),
                    "rules-lawyer");
                conceptId = concept.Value.Id;
                await globalRules.BindSourceEntityAsync(
                    conceptId,
                    new BindRuleConceptSourceRequest(sourceEntityId),
                    "rules-lawyer");
                await globalRules.SetDecisionAsync(
                    conceptId,
                    new SetGlobalRuleDecisionRequest(sourceRevision1Id, "Initial global implementation."),
                    "rules-lawyer");
                globalRevision1 = await globalRules.PublishAsync("rules-lawyer");
            }

            using (var anonymousBaseline = new HttpRequestMessage(
                       HttpMethod.Put,
                       $"/api/campaigns/{campaignId}/rules/baseline")
                   {
                       Content = JsonContent.Create(new SelectCampaignRulesetBaselineRequest(globalRevision1.Id))
                   })
            using (var anonymousResponse = await client.SendAsync(anonymousBaseline))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
            }

            using (var playerBaseline = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/campaigns/{campaignId}/rules/baseline",
                       "player-ticket",
                       new SelectCampaignRulesetBaselineRequest(globalRevision1.Id)))
            using (var playerResponse = await client.SendAsync(playerBaseline))
            {
                Assert.Equal(HttpStatusCode.Forbidden, playerResponse.StatusCode);
            }

            using (var outsiderBaseline = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/campaigns/{campaignId}/rules/baseline",
                       "outsider-ticket",
                       new SelectCampaignRulesetBaselineRequest(globalRevision1.Id)))
            using (var outsiderResponse = await client.SendAsync(outsiderBaseline))
            {
                Assert.Equal(HttpStatusCode.NotFound, outsiderResponse.StatusCode);
            }

            using (var wrongModeBaseline = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/campaigns/{campaignId}/rules/baseline",
                       "wrong-mode-dm-ticket",
                       new SelectCampaignRulesetBaselineRequest(globalRevision1.Id)))
            using (var wrongModeResponse = await client.SendAsync(wrongModeBaseline))
            {
                Assert.Equal(HttpStatusCode.NotFound, wrongModeResponse.StatusCode);
            }

            CampaignRulesetSelectionView initialSelection;
            using (var baselineRequest = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/campaigns/{campaignId}/rules/baseline",
                       "dm-ticket",
                       new SelectCampaignRulesetBaselineRequest(globalRevision1.Id)))
            using (var baselineResponse = await client.SendAsync(baselineRequest))
            {
                Assert.Equal(HttpStatusCode.OK, baselineResponse.StatusCode);
                initialSelection = (await baselineResponse.Content
                    .ReadFromJsonAsync<CampaignRulesetSelectionView>())!;
                Assert.True(initialSelection.Created);
                Assert.Equal(1, initialSelection.SelectionNumber);
                Assert.Equal(globalRevision1.Id, initialSelection.RulesetRevisionId);
            }

            PublishedCampaignRulesetRevisionView campaignRevision1;
            using (var publishRequest = HostedRequest(
                       HttpMethod.Post,
                       $"/api/campaigns/{campaignId}/rules/publish",
                       "dm-ticket"))
            using (var publishResponse = await client.SendAsync(publishRequest))
            {
                Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
                campaignRevision1 = (await publishResponse.Content
                    .ReadFromJsonAsync<PublishedCampaignRulesetRevisionView>())!;
                Assert.True(campaignRevision1.CreatedRevision);
                Assert.Equal(1, campaignRevision1.RevisionNumber);
                Assert.Equal(globalRevision1.Id, campaignRevision1.BaselineRulesetRevisionId);
            }

            using (var playerRead = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules/{conceptKey}",
                       "player-ticket"))
            using (var playerReadResponse = await client.SendAsync(playerRead))
            {
                Assert.Equal(HttpStatusCode.OK, playerReadResponse.StatusCode);
                var resolved = (await playerReadResponse.Content
                    .ReadFromJsonAsync<ResolvedCampaignRuleView>())!;
                Assert.Equal(1, resolved.CampaignRulesetRevisionNumber);
                Assert.Equal(globalRevision1.Id, resolved.BaselineRulesetRevisionId);
                Assert.Equal(sourceRevision1Id, resolved.SourceEntityRevisionId);
                Assert.Equal(CampaignRuleDecisionKinds.InheritGlobal, resolved.EffectiveDecisionKind);
                Assert.Equal("one", resolved.Document.GetProperty("versionMarker").GetString());
            }

            Guid sourceRevision2Id;
            PublishedRulesetRevisionView globalRevision2;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

                var imported = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(packageKey, isPublic: true, versionMarker: "two"));
                Assert.Equal(2, imported.Entities.Single().RevisionNumber);
                sourceRevision2Id = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == sourceEntityId && value.RevisionNumber == 2)
                    .Select(value => value.Id)
                    .SingleAsync();
                await globalRules.SetDecisionAsync(
                    conceptId,
                    new SetGlobalRuleDecisionRequest(sourceRevision2Id, "Updated global implementation."),
                    "rules-lawyer");
                globalRevision2 = await globalRules.PublishAsync("rules-lawyer");
                Assert.Equal(globalRevision1.RevisionNumber + 1, globalRevision2.RevisionNumber);
            }

            using (var stillPinnedRead = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules/{conceptKey}",
                       "player-ticket"))
            using (var stillPinnedResponse = await client.SendAsync(stillPinnedRead))
            {
                Assert.Equal(HttpStatusCode.OK, stillPinnedResponse.StatusCode);
                var resolved = (await stillPinnedResponse.Content
                    .ReadFromJsonAsync<ResolvedCampaignRuleView>())!;
                Assert.Equal(campaignRevision1.Id, resolved.CampaignRulesetRevisionNumber == 1 ? campaignRevision1.Id : Guid.Empty);
                Assert.Equal(globalRevision1.Id, resolved.BaselineRulesetRevisionId);
                Assert.Equal(sourceRevision1Id, resolved.SourceEntityRevisionId);
                Assert.Equal("one", resolved.Document.GetProperty("versionMarker").GetString());
            }

            using (var migrateRequest = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/campaigns/{campaignId}/rules/baseline",
                       "dm-ticket",
                       new SelectCampaignRulesetBaselineRequest(globalRevision2.Id)))
            using (var migrateResponse = await client.SendAsync(migrateRequest))
            {
                Assert.Equal(HttpStatusCode.OK, migrateResponse.StatusCode);
                var selection = (await migrateResponse.Content
                    .ReadFromJsonAsync<CampaignRulesetSelectionView>())!;
                Assert.True(selection.Created);
                Assert.Equal(2, selection.SelectionNumber);
                Assert.Equal(globalRevision2.Id, selection.RulesetRevisionId);
            }

            using (var beforeRepublishRead = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules/{conceptKey}",
                       "player-ticket"))
            using (var beforeRepublishResponse = await client.SendAsync(beforeRepublishRead))
            {
                Assert.Equal(HttpStatusCode.OK, beforeRepublishResponse.StatusCode);
                var resolved = (await beforeRepublishResponse.Content
                    .ReadFromJsonAsync<ResolvedCampaignRuleView>())!;
                Assert.Equal(globalRevision1.Id, resolved.BaselineRulesetRevisionId);
                Assert.Equal("one", resolved.Document.GetProperty("versionMarker").GetString());
            }

            using (var publishMigrated = HostedRequest(
                       HttpMethod.Post,
                       $"/api/campaigns/{campaignId}/rules/publish",
                       "dm-ticket"))
            using (var publishMigratedResponse = await client.SendAsync(publishMigrated))
            {
                Assert.Equal(HttpStatusCode.OK, publishMigratedResponse.StatusCode);
                var published = (await publishMigratedResponse.Content
                    .ReadFromJsonAsync<PublishedCampaignRulesetRevisionView>())!;
                Assert.True(published.CreatedRevision);
                Assert.Equal(2, published.RevisionNumber);
                Assert.Equal(globalRevision2.Id, published.BaselineRulesetRevisionId);
            }

            using (var migratedRead = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules/{conceptKey}",
                       "player-ticket"))
            using (var migratedResponse = await client.SendAsync(migratedRead))
            {
                Assert.Equal(HttpStatusCode.OK, migratedResponse.StatusCode);
                var resolved = (await migratedResponse.Content
                    .ReadFromJsonAsync<ResolvedCampaignRuleView>())!;
                Assert.Equal(globalRevision2.Id, resolved.BaselineRulesetRevisionId);
                Assert.Equal(sourceRevision2Id, resolved.SourceEntityRevisionId);
                Assert.Equal("two", resolved.Document.GetProperty("versionMarker").GetString());
            }

            using (var overrideRequest = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/campaigns/{campaignId}/rules/concepts/{conceptId}/decision",
                       "dm-ticket",
                       new SetCampaignRuleDecisionRequest(
                           CampaignRuleDecisionKinds.SelectSource,
                           sourceRevision1Id,
                           "Campaign keeps the older implementation.")))
            using (var overrideResponse = await client.SendAsync(overrideRequest))
            {
                Assert.Equal(HttpStatusCode.OK, overrideResponse.StatusCode);
                var decision = (await overrideResponse.Content
                    .ReadFromJsonAsync<CampaignRuleDecisionView>())!;
                Assert.True(decision.Created);
                Assert.Equal(1, decision.DecisionNumber);
                Assert.Equal(CampaignRuleDecisionKinds.SelectSource, decision.DecisionKind);
            }

            PublishedCampaignRulesetRevisionView overridePublication;
            using (var publishOverride = HostedRequest(
                       HttpMethod.Post,
                       $"/api/campaigns/{campaignId}/rules/publish",
                       "dm-ticket"))
            using (var publishOverrideResponse = await client.SendAsync(publishOverride))
            {
                Assert.Equal(HttpStatusCode.OK, publishOverrideResponse.StatusCode);
                overridePublication = (await publishOverrideResponse.Content
                    .ReadFromJsonAsync<PublishedCampaignRulesetRevisionView>())!;
                Assert.True(overridePublication.CreatedRevision);
                Assert.Equal(3, overridePublication.RevisionNumber);
            }

            using (var overriddenRead = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules/{conceptKey}",
                       "player-ticket"))
            using (var overriddenResponse = await client.SendAsync(overriddenRead))
            {
                Assert.Equal(HttpStatusCode.OK, overriddenResponse.StatusCode);
                var resolved = (await overriddenResponse.Content
                    .ReadFromJsonAsync<ResolvedCampaignRuleView>())!;
                Assert.Equal(CampaignRuleDecisionKinds.SelectSource, resolved.EffectiveDecisionKind);
                Assert.Equal(sourceRevision1Id, resolved.SourceEntityRevisionId);
                Assert.Equal("one", resolved.Document.GetProperty("versionMarker").GetString());
            }

            using (var inheritRequest = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/campaigns/{campaignId}/rules/concepts/{conceptId}/decision",
                       "dm-ticket",
                       new SetCampaignRuleDecisionRequest(
                           CampaignRuleDecisionKinds.InheritGlobal,
                           null,
                           "Return to the selected global baseline.")))
            using (var inheritResponse = await client.SendAsync(inheritRequest))
            {
                Assert.Equal(HttpStatusCode.OK, inheritResponse.StatusCode);
                var decision = (await inheritResponse.Content
                    .ReadFromJsonAsync<CampaignRuleDecisionView>())!;
                Assert.True(decision.Created);
                Assert.Equal(2, decision.DecisionNumber);
                Assert.Equal(CampaignRuleDecisionKinds.InheritGlobal, decision.DecisionKind);
            }

            PublishedCampaignRulesetRevisionView inheritedPublication;
            using (var publishInherited = HostedRequest(
                       HttpMethod.Post,
                       $"/api/campaigns/{campaignId}/rules/publish",
                       "dm-ticket"))
            using (var publishInheritedResponse = await client.SendAsync(publishInherited))
            {
                Assert.Equal(HttpStatusCode.OK, publishInheritedResponse.StatusCode);
                inheritedPublication = (await publishInheritedResponse.Content
                    .ReadFromJsonAsync<PublishedCampaignRulesetRevisionView>())!;
                Assert.True(inheritedPublication.CreatedRevision);
                Assert.Equal(4, inheritedPublication.RevisionNumber);
            }

            using (var inheritedRead = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules/{conceptKey}",
                       "player-ticket"))
            using (var inheritedResponse = await client.SendAsync(inheritedRead))
            {
                Assert.Equal(HttpStatusCode.OK, inheritedResponse.StatusCode);
                var resolved = (await inheritedResponse.Content
                    .ReadFromJsonAsync<ResolvedCampaignRuleView>())!;
                Assert.Equal(CampaignRuleDecisionKinds.InheritGlobal, resolved.EffectiveDecisionKind);
                Assert.Equal(sourceRevision2Id, resolved.SourceEntityRevisionId);
                Assert.Equal("two", resolved.Document.GetProperty("versionMarker").GetString());
            }

            using (var repeatPublish = HostedRequest(
                       HttpMethod.Post,
                       $"/api/campaigns/{campaignId}/rules/publish",
                       "dm-ticket"))
            using (var repeatPublishResponse = await client.SendAsync(repeatPublish))
            {
                Assert.Equal(HttpStatusCode.OK, repeatPublishResponse.StatusCode);
                var repeated = (await repeatPublishResponse.Content
                    .ReadFromJsonAsync<PublishedCampaignRulesetRevisionView>())!;
                Assert.False(repeated.CreatedRevision);
                Assert.Equal(inheritedPublication.Id, repeated.Id);
            }
        }
        finally
        {
            await CleanupAsync(factory, packageId);
        }
    }

    [Fact]
    public async Task CampaignMembershipDmAuthorityAndSourceGrantsRemainIndependent()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var campaignId = Guid.NewGuid();
        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["dm-ticket"] = Context("campaign-dm", "Campaign DM", "dorks-and-dice", campaignId, "DM"),
            ["player-ticket"] = Context("granted-player", "Granted Player", "dorks-and-dice", campaignId, "Player"),
            ["outsider-ticket"] = Context("granted-outsider", "Granted Outsider", "dorks-and-dice")
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var packageKey = $"campaign-rules-private-{Guid.NewGuid():N}";
        var conceptKey = $"skill.private.campaign.{Guid.NewGuid():N}";
        Guid packageId = Guid.Empty;

        try
        {
            Guid conceptId;
            PublishedRulesetRevisionView globalRevision;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

                var imported = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(packageKey, isPublic: false, versionMarker: "restricted"));
                packageId = imported.PackageId;
                var sourceEntityId = imported.Entities.Single().EntityId;
                var sourceRevisionId = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == sourceEntityId)
                    .Select(value => value.Id)
                    .SingleAsync();

                var concept = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(conceptKey, "skill", "Restricted Arcana"),
                    "rules-lawyer");
                conceptId = concept.Value.Id;
                await globalRules.BindSourceEntityAsync(
                    conceptId,
                    new BindRuleConceptSourceRequest(sourceEntityId),
                    "rules-lawyer");
                await globalRules.SetDecisionAsync(
                    conceptId,
                    new SetGlobalRuleDecisionRequest(sourceRevisionId, "Restricted global source."),
                    "rules-lawyer");
                globalRevision = await globalRules.PublishAsync("rules-lawyer");

                await grants.GrantAsync("granted-player", packageId);
                await grants.GrantAsync("granted-outsider", packageId);
                Assert.False(await grants.HasGrantAsync("campaign-dm", packageId));
            }

            using (var baselineRequest = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/campaigns/{campaignId}/rules/baseline",
                       "dm-ticket",
                       new SelectCampaignRulesetBaselineRequest(globalRevision.Id)))
            using (var baselineResponse = await client.SendAsync(baselineRequest))
            {
                Assert.Equal(HttpStatusCode.OK, baselineResponse.StatusCode);
            }

            using (var publishRequest = HostedRequest(
                       HttpMethod.Post,
                       $"/api/campaigns/{campaignId}/rules/publish",
                       "dm-ticket"))
            using (var publishResponse = await client.SendAsync(publishRequest))
            {
                Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
            }

            using (var dmRead = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules/{conceptKey}",
                       "dm-ticket"))
            using (var dmReadResponse = await client.SendAsync(dmRead))
            {
                Assert.Equal(HttpStatusCode.NotFound, dmReadResponse.StatusCode);
            }

            using (var playerRead = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules/{conceptKey}",
                       "player-ticket"))
            using (var playerReadResponse = await client.SendAsync(playerRead))
            {
                Assert.Equal(HttpStatusCode.OK, playerReadResponse.StatusCode);
                var resolved = (await playerReadResponse.Content
                    .ReadFromJsonAsync<ResolvedCampaignRuleView>())!;
                Assert.Equal("restricted", resolved.Document.GetProperty("versionMarker").GetString());
            }

            using (var playerMutation = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/campaigns/{campaignId}/rules/baseline",
                       "player-ticket",
                       new SelectCampaignRulesetBaselineRequest(globalRevision.Id)))
            using (var playerMutationResponse = await client.SendAsync(playerMutation))
            {
                Assert.Equal(HttpStatusCode.Forbidden, playerMutationResponse.StatusCode);
            }

            using (var outsiderRead = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules/{conceptKey}",
                       "outsider-ticket"))
            using (var outsiderReadResponse = await client.SendAsync(outsiderRead))
            {
                Assert.Equal(HttpStatusCode.NotFound, outsiderReadResponse.StatusCode);
            }

            using (var anonymousReadResponse = await client.GetAsync(
                       $"/api/campaigns/{campaignId}/rules/{conceptKey}"))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymousReadResponse.StatusCode);
            }
        }
        finally
        {
            await CleanupAsync(factory, packageId);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IToolHostAuthenticationClient authenticationClient) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IToolHostAuthenticationClient>();
                services.AddSingleton(authenticationClient);
            });
        });

    private static HttpRequestMessage HostedRequest(HttpMethod method, string path, string ticket)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(ToolHostAuthenticationHeaders.Ticket, ticket);
        request.Headers.Add(ToolHostAuthenticationHeaders.IntrospectionPath, IntrospectionPath);
        return request;
    }

    private static HttpRequestMessage HostedJsonRequest<T>(
        HttpMethod method,
        string path,
        string ticket,
        T body)
    {
        var request = HostedRequest(method, path, ticket);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private static ToolHostAuthenticationContext Context(
        string userId,
        string displayName,
        string siteMode,
        Guid? campaignId = null,
        string? campaignRole = null) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: siteMode,
            User: new ToolHostUserContext(userId, displayName),
            GlobalRoles: [],
            Campaigns: campaignId is not null && campaignRole is not null
                ? [new ToolHostCampaignContext(campaignId.Value, "Integration Campaign", campaignRole)]
                : []);

    private static Import5eToolsDocumentRequest SourceRequest(
        string packageKey,
        bool isPublic,
        string versionMarker) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: "Campaign Rules Integration Package",
            Provider: "integration-test",
            License: "test-only",
            IsPublic: isPublic,
            WorkKey: "campaign-rules-work",
            WorkDisplayName: "Campaign Rules Work",
            EditionKey: "campaign-rules-edition",
            EditionDisplayName: "Campaign Rules Edition",
            Json: $$"""
                {
                  "skill": [
                    {
                      "name": "Arcana",
                      "source": "TST",
                      "ability": "int",
                      "versionMarker": "{{versionMarker}}"
                    }
                  ]
                }
                """);

    private static async Task CleanupAsync(
        WebApplicationFactory<Program> factory,
        Guid packageId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

        await db.CampaignRulesetRevisionEntries.ExecuteDeleteAsync();
        await db.CampaignRulesetRevisions.ExecuteDeleteAsync();
        await db.CampaignRuleDecisions.ExecuteDeleteAsync();
        await db.CampaignRulesetSelections.ExecuteDeleteAsync();
        await db.RulesetRevisionEntries.ExecuteDeleteAsync();
        await db.RulesetRevisions.ExecuteDeleteAsync();
        await db.GlobalRuleDecisions.ExecuteDeleteAsync();
        await db.RuleConceptSourceBindings.ExecuteDeleteAsync();
        await db.RuleConcepts.ExecuteDeleteAsync();

        if (packageId != Guid.Empty)
        {
            var package = await db.SourcePackages.FindAsync(packageId);
            if (package is not null)
            {
                db.SourcePackages.Remove(package);
                await db.SaveChangesAsync();
            }
        }
    }

    private sealed class FakeToolHostAuthenticationClient(
        IReadOnlyDictionary<string, ToolHostAuthenticationContext> contexts)
        : IToolHostAuthenticationClient
    {
        public Task<ToolHostAuthenticationContext?> RedeemAsync(
            string ticket,
            string introspectionPath,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(introspectionPath, IntrospectionPath, StringComparison.Ordinal))
            {
                return Task.FromResult<ToolHostAuthenticationContext?>(null);
            }

            contexts.TryGetValue(ticket, out var context);
            return Task.FromResult(context);
        }
    }
}
