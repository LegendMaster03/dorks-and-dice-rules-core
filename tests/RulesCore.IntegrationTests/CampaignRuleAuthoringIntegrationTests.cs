using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
public sealed class CampaignRuleAuthoringIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task CampaignAuthoringTracksBaselineOverridePublishAndSourceAccessSeparately()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var campaignId = Guid.NewGuid();
        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["dm-ticket"] = Context(
                "campaign-dm",
                "Campaign DM",
                "dorks-and-dice",
                campaignId,
                RulesAuthority.CampaignDmRole),
            ["player-ticket"] = Context(
                "campaign-player",
                "Campaign Player",
                "dorks-and-dice",
                campaignId,
                "Player"),
            ["outsider-ticket"] = Context(
                "outsider",
                "Outsider",
                "dorks-and-dice"),
            ["wrong-mode-dm-ticket"] = Context(
                "campaign-dm",
                "Campaign DM",
                "professional",
                campaignId,
                RulesAuthority.CampaignDmRole)
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var publicPackageKey = $"campaign-authoring-public-{Guid.NewGuid():N}";
        var restrictedPackageKey = $"campaign-authoring-restricted-{Guid.NewGuid():N}";
        var conceptKey = $"skill.arcana.campaign-authoring.{Guid.NewGuid():N}";
        var packageIds = new List<Guid>();

        try
        {
            Guid conceptId;
            Guid publicEntityId;
            Guid publicRevision1Id;
            Guid restrictedEntityId;
            PublishedRulesetRevisionView globalRevision1;

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

                var publicImport = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(publicPackageKey, "Public Campaign Authoring Package", true, "PUB", "one"));
                packageIds.Add(publicImport.PackageId);
                publicEntityId = publicImport.Entities.Single().EntityId;
                publicRevision1Id = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == publicEntityId)
                    .Select(value => value.Id)
                    .SingleAsync();

                var restrictedImport = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(
                        restrictedPackageKey,
                        "Restricted Campaign Authoring Package",
                        false,
                        "RST",
                        "restricted"));
                packageIds.Add(restrictedImport.PackageId);
                restrictedEntityId = restrictedImport.Entities.Single().EntityId;

                conceptId = (await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(conceptKey, "skill", "Arcana"),
                    "rules-lawyer")).Value.Id;
                await globalRules.BindSourceEntityAsync(
                    conceptId,
                    new BindRuleConceptSourceRequest(publicEntityId),
                    "rules-lawyer");
                await globalRules.BindSourceEntityAsync(
                    conceptId,
                    new BindRuleConceptSourceRequest(restrictedEntityId),
                    "rules-lawyer");
                await globalRules.SetDecisionAsync(
                    conceptId,
                    new SetGlobalRuleDecisionRequest(publicRevision1Id, "Campaign authoring baseline one."),
                    "rules-lawyer");
                globalRevision1 = await globalRules.PublishAsync("rules-lawyer");
            }

            var overviewPath = $"/api/campaigns/{campaignId}/rules/authoring";
            var detailPath = $"{overviewPath}/concepts/{conceptId}";

            using (var anonymousResponse = await client.GetAsync(overviewPath))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
            }

            using (var playerRequest = HostedRequest(HttpMethod.Get, overviewPath, "player-ticket"))
            using (var playerResponse = await client.SendAsync(playerRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, playerResponse.StatusCode);
            }

            using (var outsiderRequest = HostedRequest(HttpMethod.Get, overviewPath, "outsider-ticket"))
            using (var outsiderResponse = await client.SendAsync(outsiderRequest))
            {
                Assert.Equal(HttpStatusCode.NotFound, outsiderResponse.StatusCode);
            }

            using (var wrongModeRequest = HostedRequest(HttpMethod.Get, overviewPath, "wrong-mode-dm-ticket"))
            using (var wrongModeResponse = await client.SendAsync(wrongModeRequest))
            {
                Assert.Equal(HttpStatusCode.NotFound, wrongModeResponse.StatusCode);
            }

            using (var emptyRequest = HostedRequest(HttpMethod.Get, overviewPath, "dm-ticket"))
            using (var emptyResponse = await client.SendAsync(emptyRequest))
            {
                Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
                Assert.Equal("no-store", emptyResponse.Headers.CacheControl?.ToString());
                var overview = (await emptyResponse.Content
                    .ReadFromJsonAsync<CampaignRulesAuthoringOverviewView>())!;
                Assert.Null(overview.SelectedBaseline);
                Assert.Null(overview.LatestPublishedRuleset);
                Assert.False(overview.HasUnpublishedBaselineChange);
                Assert.False(overview.NeedsPublication);
                Assert.Empty(overview.Concepts);
            }

            using (var noBaselineDetail = HostedRequest(HttpMethod.Get, detailPath, "dm-ticket"))
            using (var noBaselineDetailResponse = await client.SendAsync(noBaselineDetail))
            {
                Assert.Equal(HttpStatusCode.NotFound, noBaselineDetailResponse.StatusCode);
            }

            CampaignRulesetSelectionView selection1;
            using (var baselineRequest = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/campaigns/{campaignId}/rules/baseline",
                       "dm-ticket",
                       new SelectCampaignRulesetBaselineRequest(globalRevision1.Id)))
            using (var baselineResponse = await client.SendAsync(baselineRequest))
            {
                Assert.Equal(HttpStatusCode.OK, baselineResponse.StatusCode);
                selection1 = (await baselineResponse.Content
                    .ReadFromJsonAsync<CampaignRulesetSelectionView>())!;
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
            }

            using (var overviewRequest = HostedRequest(HttpMethod.Get, overviewPath, "dm-ticket"))
            using (var overviewResponse = await client.SendAsync(overviewRequest))
            {
                Assert.Equal(HttpStatusCode.OK, overviewResponse.StatusCode);
                var overview = (await overviewResponse.Content
                    .ReadFromJsonAsync<CampaignRulesAuthoringOverviewView>())!;
                Assert.Equal(selection1.Id, overview.SelectedBaseline!.Id);
                Assert.Equal(campaignRevision1.Id, overview.LatestPublishedRuleset!.Id);
                Assert.False(overview.HasUnpublishedBaselineChange);
                Assert.False(overview.NeedsPublication);
                Assert.Equal(1, overview.ConceptCount);
                Assert.Equal(0, overview.ConceptsWithOverrides);
                Assert.Equal(0, overview.PendingOverrideCount);
                var concept = Assert.Single(overview.Concepts);
                Assert.Equal(conceptId, concept.Id);
                Assert.Null(concept.LatestCampaignDecisionId);
                Assert.Null(concept.PublishedCampaignDecisionId);
                Assert.False(concept.HasUnpublishedOverrideChange);
            }

            using (var detailRequest = HostedRequest(HttpMethod.Get, detailPath, "dm-ticket"))
            using (var detailResponse = await client.SendAsync(detailRequest))
            {
                Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
                var detail = (await detailResponse.Content
                    .ReadFromJsonAsync<CampaignRuleAuthoringConceptView>())!;
                Assert.Equal(campaignId, detail.CampaignId);
                Assert.Equal(selection1.Id, detail.SelectedBaseline.Id);
                Assert.Equal(conceptId, detail.Concept.Id);
                Assert.Equal(2, detail.Bindings.Count);
                Assert.Single(detail.AccessibleSources);
                Assert.Equal(1, detail.RestrictedBindingCount);
                Assert.Equal(publicRevision1Id, detail.AccessibleSources[0].Revisions.Single().Id);
                Assert.DoesNotContain(
                    detail.AccessibleSources,
                    value => value.SourceEntityId == restrictedEntityId);
                Assert.Null(detail.LatestCampaignDecision);
                Assert.False(detail.HasUnpublishedBaselineChange);
                Assert.False(detail.HasUnpublishedOverrideChange);
            }

            var candidate = new SetCampaignRuleDecisionRequest(
                CampaignRuleDecisionKinds.JsonMergePatch,
                SourceEntityRevisionId: null,
                Note: "Campaign authoring pending override.",
                MergePatch: Parse("""{ "ability": "wis", "campaignMarker": true }"""));

            using (var previewRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/campaigns/{campaignId}/rules/concepts/{conceptId}/preview",
                       "dm-ticket",
                       candidate))
            using (var previewResponse = await client.SendAsync(previewRequest))
            {
                Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
                var preview = (await previewResponse.Content.ReadFromJsonAsync<RulePatchPreviewView>())!;
                Assert.Equal("wis", preview.PreviewDocument.GetProperty("ability").GetString());
                Assert.True(preview.PreviewDocument.GetProperty("campaignMarker").GetBoolean());
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                Assert.Equal(0, await db.CampaignRuleDecisions.CountAsync(
                    value => value.CampaignId == campaignId));
            }

            CampaignRuleDecisionView savedOverride;
            using (var saveRequest = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/campaigns/{campaignId}/rules/concepts/{conceptId}/decision",
                       "dm-ticket",
                       candidate))
            using (var saveResponse = await client.SendAsync(saveRequest))
            {
                Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
                savedOverride = (await saveResponse.Content
                    .ReadFromJsonAsync<CampaignRuleDecisionView>())!;
            }

            using (var pendingRequest = HostedRequest(HttpMethod.Get, overviewPath, "dm-ticket"))
            using (var pendingResponse = await client.SendAsync(pendingRequest))
            {
                var overview = (await pendingResponse.Content
                    .ReadFromJsonAsync<CampaignRulesAuthoringOverviewView>())!;
                Assert.False(overview.HasUnpublishedBaselineChange);
                Assert.True(overview.NeedsPublication);
                Assert.Equal(1, overview.ConceptsWithOverrides);
                Assert.Equal(1, overview.PendingOverrideCount);
                var concept = Assert.Single(overview.Concepts);
                Assert.Equal(savedOverride.Id, concept.LatestCampaignDecisionId);
                Assert.Null(concept.PublishedCampaignDecisionId);
                Assert.True(concept.HasUnpublishedOverrideChange);
            }

            PublishedCampaignRulesetRevisionView campaignRevision2;
            using (var publishOverrideRequest = HostedRequest(
                       HttpMethod.Post,
                       $"/api/campaigns/{campaignId}/rules/publish",
                       "dm-ticket"))
            using (var publishOverrideResponse = await client.SendAsync(publishOverrideRequest))
            {
                Assert.Equal(HttpStatusCode.OK, publishOverrideResponse.StatusCode);
                campaignRevision2 = (await publishOverrideResponse.Content
                    .ReadFromJsonAsync<PublishedCampaignRulesetRevisionView>())!;
                Assert.Equal(campaignRevision1.RevisionNumber + 1, campaignRevision2.RevisionNumber);
            }

            using (var publishedRequest = HostedRequest(HttpMethod.Get, overviewPath, "dm-ticket"))
            using (var publishedResponse = await client.SendAsync(publishedRequest))
            {
                var overview = (await publishedResponse.Content
                    .ReadFromJsonAsync<CampaignRulesAuthoringOverviewView>())!;
                Assert.False(overview.HasUnpublishedBaselineChange);
                Assert.False(overview.NeedsPublication);
                Assert.Equal(0, overview.PendingOverrideCount);
                var concept = Assert.Single(overview.Concepts);
                Assert.Equal(savedOverride.Id, concept.LatestCampaignDecisionId);
                Assert.Equal(savedOverride.Id, concept.PublishedCampaignDecisionId);
                Assert.False(concept.HasUnpublishedOverrideChange);
            }

            Guid publicRevision2Id;
            PublishedRulesetRevisionView globalRevision2;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

                var changed = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(publicPackageKey, "Public Campaign Authoring Package", true, "PUB", "two"));
                Assert.Equal(publicEntityId, changed.Entities.Single().EntityId);
                publicRevision2Id = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == publicEntityId && value.RevisionNumber == 2)
                    .Select(value => value.Id)
                    .SingleAsync();

                await globalRules.SetDecisionAsync(
                    conceptId,
                    new SetGlobalRuleDecisionRequest(publicRevision2Id, "Campaign authoring baseline two."),
                    "rules-lawyer");
                globalRevision2 = await globalRules.PublishAsync("rules-lawyer");
            }

            CampaignRulesetSelectionView selection2;
            using (var migrateRequest = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/campaigns/{campaignId}/rules/baseline",
                       "dm-ticket",
                       new SelectCampaignRulesetBaselineRequest(globalRevision2.Id)))
            using (var migrateResponse = await client.SendAsync(migrateRequest))
            {
                Assert.Equal(HttpStatusCode.OK, migrateResponse.StatusCode);
                selection2 = (await migrateResponse.Content
                    .ReadFromJsonAsync<CampaignRulesetSelectionView>())!;
                Assert.NotEqual(selection1.Id, selection2.Id);
            }

            using (var migratedRequest = HostedRequest(HttpMethod.Get, overviewPath, "dm-ticket"))
            using (var migratedResponse = await client.SendAsync(migratedRequest))
            {
                var overview = (await migratedResponse.Content
                    .ReadFromJsonAsync<CampaignRulesAuthoringOverviewView>())!;
                Assert.Equal(selection2.Id, overview.SelectedBaseline!.Id);
                Assert.Equal(campaignRevision2.Id, overview.LatestPublishedRuleset!.Id);
                Assert.True(overview.HasUnpublishedBaselineChange);
                Assert.True(overview.NeedsPublication);
                Assert.Equal(0, overview.PendingOverrideCount);
                var concept = Assert.Single(overview.Concepts);
                Assert.Equal(savedOverride.Id, concept.LatestCampaignDecisionId);
                Assert.Equal(savedOverride.Id, concept.PublishedCampaignDecisionId);
                Assert.False(concept.HasUnpublishedOverrideChange);
            }

            using (var migratedDetailRequest = HostedRequest(HttpMethod.Get, detailPath, "dm-ticket"))
            using (var migratedDetailResponse = await client.SendAsync(migratedDetailRequest))
            {
                var detail = (await migratedDetailResponse.Content
                    .ReadFromJsonAsync<CampaignRuleAuthoringConceptView>())!;
                Assert.Equal(selection2.Id, detail.SelectedBaseline.Id);
                Assert.Equal(publicRevision2Id, detail.BaselineGlobalDecision.SelectedSourceEntityRevisionId);
                Assert.True(detail.HasUnpublishedBaselineChange);
                Assert.False(detail.HasUnpublishedOverrideChange);
            }

            using (var publishMigrationRequest = HostedRequest(
                       HttpMethod.Post,
                       $"/api/campaigns/{campaignId}/rules/publish",
                       "dm-ticket"))
            using (var publishMigrationResponse = await client.SendAsync(publishMigrationRequest))
            {
                Assert.Equal(HttpStatusCode.OK, publishMigrationResponse.StatusCode);
                var campaignRevision3 = (await publishMigrationResponse.Content
                    .ReadFromJsonAsync<PublishedCampaignRulesetRevisionView>())!;
                Assert.Equal(campaignRevision2.RevisionNumber + 1, campaignRevision3.RevisionNumber);
                Assert.Equal(selection2.Id, campaignRevision3.BaselineSelectionId);
            }

            using (var finalRequest = HostedRequest(HttpMethod.Get, overviewPath, "dm-ticket"))
            using (var finalResponse = await client.SendAsync(finalRequest))
            {
                var overview = (await finalResponse.Content
                    .ReadFromJsonAsync<CampaignRulesAuthoringOverviewView>())!;
                Assert.False(overview.HasUnpublishedBaselineChange);
                Assert.False(overview.NeedsPublication);
                Assert.Equal(0, overview.PendingOverrideCount);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
                await grants.GrantAsync("campaign-dm", packageIds[1]);
            }

            using (var grantedRequest = HostedRequest(HttpMethod.Get, detailPath, "dm-ticket"))
            using (var grantedResponse = await client.SendAsync(grantedRequest))
            {
                var detail = (await grantedResponse.Content
                    .ReadFromJsonAsync<CampaignRuleAuthoringConceptView>())!;
                Assert.Equal(2, detail.AccessibleSources.Count);
                Assert.Equal(0, detail.RestrictedBindingCount);
                Assert.Contains(
                    detail.AccessibleSources,
                    value => value.SourceEntityId == restrictedEntityId
                        && value.PackageDisplayName == "Restricted Campaign Authoring Package");
            }
        }
        finally
        {
            await CleanupAsync(factory, packageIds);
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
                ? [new ToolHostCampaignContext(campaignId.Value, "Authoring Campaign", campaignRole)]
                : []);

    private static Import5eToolsDocumentRequest SourceRequest(
        string packageKey,
        string packageDisplayName,
        bool isPublic,
        string sourceCode,
        string marker) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: packageDisplayName,
            Provider: "integration-test",
            License: "test-only",
            IsPublic: isPublic,
            WorkKey: $"{packageKey}-work",
            WorkDisplayName: $"{packageDisplayName} Work",
            EditionKey: $"{packageKey}-edition",
            EditionDisplayName: $"{packageDisplayName} Edition",
            Json: $$"""
                {
                  "skill": [
                    {
                      "name": "Arcana",
                      "source": "{{sourceCode}}",
                      "ability": "int",
                      "marker": "{{marker}}"
                    }
                  ]
                }
                """);

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static async Task CleanupAsync(
        WebApplicationFactory<Program> factory,
        IReadOnlyCollection<Guid> packageIds)
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
        db.ChangeTracker.Clear();

        if (packageIds.Count > 0)
        {
            await db.SourcePackages
                .Where(value => packageIds.Contains(value.Id))
                .ExecuteDeleteAsync();
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
