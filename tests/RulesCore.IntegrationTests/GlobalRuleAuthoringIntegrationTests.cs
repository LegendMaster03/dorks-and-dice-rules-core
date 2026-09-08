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
public sealed class GlobalRuleAuthoringIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task AuthoringWorkflowTracksPreviewSavePublishAndSourceAccessSeparately()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["rules-lawyer-ticket"] = Context(
                "rules-lawyer",
                "Rules Lawyer",
                "dorks-and-dice",
                [RulesAuthority.RulesLawyerRole]),
            ["ordinary-ticket"] = Context(
                "ordinary-user",
                "Ordinary User",
                "dorks-and-dice",
                []),
            ["wrong-mode-ticket"] = Context(
                "rules-lawyer",
                "Rules Lawyer",
                "professional",
                [RulesAuthority.RulesLawyerRole])
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var publicPackageKey = $"authoring-public-{Guid.NewGuid():N}";
        var restrictedPackageKey = $"authoring-restricted-{Guid.NewGuid():N}";
        var conceptKey = $"skill.arcana.authoring.{Guid.NewGuid():N}";
        var packageIds = new List<Guid>();

        try
        {
            Guid conceptId;
            Guid publicRevisionId;
            Guid restrictedEntityId;
            GlobalRuleDecisionView initialDecision;
            PublishedRulesetRevisionView initialPublication;

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

                var publicImport = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(publicPackageKey, "Public Authoring Package", true, "PUB", "public"));
                packageIds.Add(publicImport.PackageId);
                var publicEntityId = publicImport.Entities.Single().EntityId;
                publicRevisionId = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == publicEntityId)
                    .Select(value => value.Id)
                    .SingleAsync();

                var restrictedImport = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(
                        restrictedPackageKey,
                        "Restricted Authoring Package",
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

                initialDecision = (await globalRules.SetDecisionAsync(
                    conceptId,
                    new SetGlobalRuleDecisionRequest(publicRevisionId, "Initial authoring decision."),
                    "rules-lawyer")).Value;
                initialPublication = await globalRules.PublishAsync("rules-lawyer");
            }

            using (var anonymousResponse = await client.GetAsync("/api/global/rules/authoring"))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
            }

            using (var ordinaryRequest = HostedRequest(
                       HttpMethod.Get,
                       "/api/global/rules/authoring",
                       "ordinary-ticket"))
            using (var ordinaryResponse = await client.SendAsync(ordinaryRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, ordinaryResponse.StatusCode);
            }

            using (var wrongModeRequest = HostedRequest(
                       HttpMethod.Get,
                       "/api/global/rules/authoring",
                       "wrong-mode-ticket"))
            using (var wrongModeResponse = await client.SendAsync(wrongModeRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, wrongModeResponse.StatusCode);
            }

            using (var overviewRequest = HostedRequest(
                       HttpMethod.Get,
                       "/api/global/rules/authoring",
                       "rules-lawyer-ticket"))
            using (var overviewResponse = await client.SendAsync(overviewRequest))
            {
                Assert.Equal(HttpStatusCode.OK, overviewResponse.StatusCode);
                Assert.Equal("no-store", overviewResponse.Headers.CacheControl?.ToString());
                var overview = (await overviewResponse.Content
                    .ReadFromJsonAsync<GlobalRulesAuthoringOverviewView>())!;
                var concept = overview.Concepts.Single(value => value.Id == conceptId);
                Assert.Equal(2, concept.BindingCount);
                Assert.Equal(initialDecision.Id, concept.LatestDecisionId);
                Assert.Equal(initialDecision.Id, concept.PublishedDecisionId);
                Assert.False(concept.HasUnpublishedChanges);
                Assert.Equal(initialPublication.Id, overview.LatestPublishedRuleset!.Id);
            }

            using (var detailRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/global/rules/authoring/concepts/{conceptId}",
                       "rules-lawyer-ticket"))
            using (var detailResponse = await client.SendAsync(detailRequest))
            {
                Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
                var detail = (await detailResponse.Content
                    .ReadFromJsonAsync<GlobalRuleAuthoringConceptView>())!;
                Assert.Equal(2, detail.Bindings.Count);
                Assert.Single(detail.AccessibleSources);
                Assert.Equal(1, detail.RestrictedBindingCount);
                Assert.Equal(publicRevisionId, detail.AccessibleSources[0].Revisions.Single().Id);
                Assert.DoesNotContain(
                    detail.AccessibleSources,
                    value => value.SourceEntityId == restrictedEntityId);
                Assert.Equal(initialDecision.Id, detail.LatestDecision!.Id);
                Assert.False(detail.HasUnpublishedChanges);
            }

            var candidate = new SetGlobalRuleDecisionRequest(
                publicRevisionId,
                "Pending authoring change.",
                MergePatch: Parse("""{ "ability": "wis", "authoringMarker": true }"""));

            using (var previewRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/concepts/{conceptId}/preview",
                       "rules-lawyer-ticket",
                       candidate))
            using (var previewResponse = await client.SendAsync(previewRequest))
            {
                Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
                var preview = (await previewResponse.Content.ReadFromJsonAsync<RulePatchPreviewView>())!;
                Assert.Equal("wis", preview.PreviewDocument.GetProperty("ability").GetString());
                Assert.True(preview.PreviewDocument.GetProperty("authoringMarker").GetBoolean());
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                Assert.Equal(1, await db.GlobalRuleDecisions.CountAsync(
                    value => value.RuleConceptId == conceptId));
            }

            using (var saveRequest = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/global/rules/concepts/{conceptId}/decision",
                       "rules-lawyer-ticket",
                       candidate))
            using (var saveResponse = await client.SendAsync(saveRequest))
            {
                Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
            }

            using (var pendingRequest = HostedRequest(
                       HttpMethod.Get,
                       "/api/global/rules/authoring",
                       "rules-lawyer-ticket"))
            using (var pendingResponse = await client.SendAsync(pendingRequest))
            {
                var overview = (await pendingResponse.Content
                    .ReadFromJsonAsync<GlobalRulesAuthoringOverviewView>())!;
                var concept = overview.Concepts.Single(value => value.Id == conceptId);
                Assert.True(concept.HasUnpublishedChanges);
                Assert.Equal(1, overview.PendingDecisionCount);
                Assert.NotEqual(concept.LatestDecisionId, concept.PublishedDecisionId);
            }

            using (var publishRequest = HostedRequest(
                       HttpMethod.Post,
                       "/api/global/rules/publish",
                       "rules-lawyer-ticket"))
            using (var publishResponse = await client.SendAsync(publishRequest))
            {
                Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
            }

            using (var publishedRequest = HostedRequest(
                       HttpMethod.Get,
                       "/api/global/rules/authoring",
                       "rules-lawyer-ticket"))
            using (var publishedResponse = await client.SendAsync(publishedRequest))
            {
                var overview = (await publishedResponse.Content
                    .ReadFromJsonAsync<GlobalRulesAuthoringOverviewView>())!;
                var concept = overview.Concepts.Single(value => value.Id == conceptId);
                Assert.False(concept.HasUnpublishedChanges);
                Assert.Equal(0, overview.PendingDecisionCount);
                Assert.Equal(concept.LatestDecisionId, concept.PublishedDecisionId);
                Assert.Equal(initialPublication.RevisionNumber + 1, overview.LatestPublishedRuleset!.RevisionNumber);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
                await grants.GrantAsync("rules-lawyer", packageIds[1]);
            }

            using (var grantedRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/global/rules/authoring/concepts/{conceptId}",
                       "rules-lawyer-ticket"))
            using (var grantedResponse = await client.SendAsync(grantedRequest))
            {
                var detail = (await grantedResponse.Content
                    .ReadFromJsonAsync<GlobalRuleAuthoringConceptView>())!;
                Assert.Equal(2, detail.AccessibleSources.Count);
                Assert.Equal(0, detail.RestrictedBindingCount);
                Assert.Contains(
                    detail.AccessibleSources,
                    value => value.SourceEntityId == restrictedEntityId
                        && value.PackageDisplayName == "Restricted Authoring Package");
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
        IReadOnlyList<string> globalRoles) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: siteMode,
            User: new ToolHostUserContext(userId, displayName),
            GlobalRoles: globalRoles,
            Campaigns: []);

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
