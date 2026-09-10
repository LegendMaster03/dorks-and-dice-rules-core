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
public sealed class PreImportRepresentativeLifecycleIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task RepresentativeCorpusTraversesTheEntirePreImportLifecycle()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var campaignId = Guid.NewGuid();
        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["dev-ticket"] = Context("lifecycle-dev", ["Dev"]),
            ["rules-ticket"] = Context("lifecycle-rules-lawyer", ["Rules Lawyer"]),
            ["dm-ticket"] = Context("lifecycle-dm", [], campaignId, "DM"),
            ["player-ticket"] = Context("lifecycle-player", [], campaignId, "Player")
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var token = Guid.NewGuid().ToString("N")[..10];
        var uaPackageKey = $"lifecycle-ua-{token}";
        var publishedPackageKey = $"lifecycle-published-{token}";
        var conceptKey = $"feat.arcane-burst-lifecycle-{token}";
        var packageKeys = new[] { uaPackageKey, publishedPackageKey };
        var entityIds = new List<Guid>();
        Guid conceptId = Guid.Empty;

        var uaRequest = SourceRequest(
            uaPackageKey,
            "Lifecycle UA Source",
            "lifecycle-ua-work",
            "Lifecycle UA Work",
            "ua-release",
            "UA release",
            "5e-2014",
            "playtest",
            "Mystic Bolt",
            "UA-LIFE",
            ["alpha", "legacy-push"],
            reprintedAs: "Arcane Burst|PUB-LIFE");

        var publishedRequest = SourceRequest(
            publishedPackageKey,
            "Lifecycle Published Source",
            "lifecycle-published-work",
            "Lifecycle Published Work",
            "published-release",
            "Published release",
            "5e-2024",
            "published",
            "Arcane Burst",
            "PUB-LIFE",
            ["alpha", "beta", "gamma"]);

        try
        {
            SourceImportPreviewResult uaPreview;
            using (var request = HostedJsonRequest(HttpMethod.Post, "/api/source-admin/import/preview", "dev-ticket", uaRequest))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                uaPreview = (await response.Content.ReadFromJsonAsync<SourceImportPreviewResult>())!;
            }
            Assert.True(uaPreview.CanImport);
            Assert.Equal("5e", uaPreview.GameEdition);
            Assert.Equal(SourceImportPreviewActions.NewEntity, Assert.Single(uaPreview.Entities).Action);

            SourceImportResult uaImport;
            using (var request = HostedJsonRequest(HttpMethod.Post, "/api/source-admin/import", "dev-ticket", uaRequest))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                uaImport = (await response.Content.ReadFromJsonAsync<SourceImportResult>())!;
            }
            var uaEntity = Assert.Single(uaImport.Entities);
            entityIds.Add(uaEntity.EntityId);

            using (var request = HostedJsonRequest(HttpMethod.Post, "/api/source-admin/import/preview", "dev-ticket", uaRequest))
            using (var response = await client.SendAsync(request))
            {
                var preview = (await response.Content.ReadFromJsonAsync<SourceImportPreviewResult>())!;
                Assert.Equal(SourceImportPreviewActions.Unchanged, Assert.Single(preview.Entities).Action);
            }

            SourceImportResult publishedImport;
            using (var request = HostedJsonRequest(HttpMethod.Post, "/api/source-admin/import", "dev-ticket", publishedRequest))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                publishedImport = (await response.Content.ReadFromJsonAsync<SourceImportResult>())!;
            }
            var publishedEntity = Assert.Single(publishedImport.Entities);
            entityIds.Add(publishedEntity.EntityId);
            Assert.Equal("5.5e", publishedImport.GameEdition);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var rules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                conceptId = (await rules.CreateConceptAsync(
                    new CreateRuleConceptRequest(conceptKey, "feat", "Arcane Burst"),
                    "lifecycle-rules-lawyer")).Value.Id;
                await rules.BindSourceEntityAsync(
                    conceptId,
                    new BindRuleConceptSourceRequest(publishedEntity.EntityId),
                    "lifecycle-rules-lawyer");
            }

            using (var request = HostedRequest(
                       HttpMethod.Get,
                       $"/api/global/rules/versioning/entities/{uaEntity.EntityId}/candidates",
                       "rules-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var detection = (await response.Content.ReadFromJsonAsync<SourceVersionDetectionView>())!;
                Assert.Contains(
                    detection.Candidates,
                    value => value.Candidate.SourceEntityId == publishedEntity.EntityId && value.Confidence >= 65);
            }

            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/versioning/entities/{uaEntity.EntityId}/bind",
                       "rules-ticket",
                       new BindDetectedSourceVersionRequest(conceptId)))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/global/rules/versioning/lineage",
                       "rules-ticket",
                       new CreateSourceLineageRequest(
                           uaEntity.EntityId,
                           publishedEntity.EntityId,
                           SourceLineageKinds.PlaytestOf,
                           "Lifecycle corpus explicitly records playtest-to-published lineage.")))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True((await response.Content.ReadFromJsonAsync<SourceLineageMutationView>())!.Changed);
            }

            RuleConsolidationView consolidation;
            using (var request = HostedRequest(
                       HttpMethod.Get,
                       $"/api/global/rules/concepts/{conceptId}/consolidation",
                       "rules-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                consolidation = (await response.Content.ReadFromJsonAsync<RuleConsolidationView>())!;
            }
            Assert.Equal(2, consolidation.Sources.Count);
            Assert.Single(consolidation.Lineage);

            var publishedRevisionId = consolidation.Sources
                .Single(value => value.SourceEntityId == publishedEntity.EntityId)
                .Revisions.Single().Id;
            var uaRevisionId = consolidation.Sources
                .Single(value => value.SourceEntityId == uaEntity.EntityId)
                .Revisions.Single().Id;

            using var beta = JsonDocument.Parse("\"beta\"");
            var structuredPatch = new RuleStructuredPatchRequest(
                ArrayOperations:
                [
                    new RuleArrayOperationRequest(
                        RuleArrayOperationKinds.Remove,
                        "/entries",
                        Match: new RuleArrayItemSelectorRequest(null, beta.RootElement.Clone()))
                ]);
            var globalDecision = new SetGlobalRuleDecisionRequest(
                publishedRevisionId,
                "Published implementation with the playtest reviewed and one list item deliberately removed.",
                StructuredPatch: structuredPatch,
                Contributions:
                [
                    new RuleConsolidationContributionRequest(
                        uaRevisionId,
                        RuleConsolidationContributionKinds.Incorporated,
                        "The playtest push behavior informed the adjudication.")
                ]);

            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/concepts/{conceptId}/preview",
                       "rules-ticket",
                       globalDecision))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var preview = (await response.Content.ReadFromJsonAsync<RulePatchPreviewView>())!;
                Assert.Equal(RuleDecisionKinds.JsonRulePatch, preview.DecisionKind);
                Assert.DoesNotContain(
                    preview.PreviewDocument.GetProperty("entries").EnumerateArray(),
                    value => value.GetString() == "beta");
            }

            using (var request = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/global/rules/concepts/{conceptId}/decision",
                       "rules-ticket",
                       globalDecision))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True((await response.Content.ReadFromJsonAsync<RuleMutationResult<GlobalRuleDecisionView>>())!.Created);
            }

            PublishedRulesetRevisionView globalPublication;
            using (var request = HostedRequest(HttpMethod.Post, "/api/global/rules/publish", "rules-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                globalPublication = (await response.Content.ReadFromJsonAsync<PublishedRulesetRevisionView>())!;
                Assert.True(globalPublication.CreatedRevision);
            }

            using (var response = await client.GetAsync($"/api/rules/{conceptKey}"))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var resolved = (await response.Content.ReadFromJsonAsync<ResolvedRuleView>())!;
                var entries = resolved.Document.GetProperty("entries").EnumerateArray().Select(value => value.GetString()).ToArray();
                Assert.Equal(["alpha", "gamma"], entries);
                Assert.Equal(RuleDecisionKinds.JsonRulePatch, resolved.DecisionKind);
            }

            using (var request = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/campaigns/{campaignId}/rules/baseline",
                       "dm-ticket",
                       new SelectCampaignRulesetBaselineRequest(globalPublication.Id)))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            using (var request = HostedRequest(
                       HttpMethod.Post,
                       $"/api/campaigns/{campaignId}/rules/publish",
                       "dm-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True((await response.Content.ReadFromJsonAsync<PublishedCampaignRulesetRevisionView>())!.CreatedRevision);
            }

            using var campaignPatchDocument = JsonDocument.Parse("{\"campaignTag\":\"house-rule\"}");
            var campaignDecision = new SetCampaignRuleDecisionRequest(
                CampaignRuleDecisionKinds.JsonMergePatch,
                null,
                "Campaign variation layered on the published global rule.",
                MergePatch: campaignPatchDocument.RootElement.Clone());

            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/campaigns/{campaignId}/rules/concepts/{conceptId}/preview",
                       "dm-ticket",
                       campaignDecision))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var preview = (await response.Content.ReadFromJsonAsync<RulePatchPreviewView>())!;
                Assert.Equal("house-rule", preview.PreviewDocument.GetProperty("campaignTag").GetString());
            }

            using (var request = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/campaigns/{campaignId}/rules/concepts/{conceptId}/decision",
                       "dm-ticket",
                       campaignDecision))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True((await response.Content.ReadFromJsonAsync<CampaignRuleDecisionView>())!.Created);
            }

            using (var request = HostedRequest(
                       HttpMethod.Post,
                       $"/api/campaigns/{campaignId}/rules/publish",
                       "dm-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True((await response.Content.ReadFromJsonAsync<PublishedCampaignRulesetRevisionView>())!.CreatedRevision);
            }

            using (var request = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules/{conceptKey}",
                       "player-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var resolved = (await response.Content.ReadFromJsonAsync<ResolvedCampaignRuleView>())!;
                Assert.Equal("house-rule", resolved.Document.GetProperty("campaignTag").GetString());
                var entries = resolved.Document.GetProperty("entries").EnumerateArray().Select(value => value.GetString()).ToArray();
                Assert.Equal(["alpha", "gamma"], entries);
            }

            var changedPublished = publishedRequest with
            {
                Json = SourceJson("Arcane Burst", "PUB-LIFE", ["alpha", "beta", "gamma", "delta"])
            };
            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/source-admin/import/preview",
                       "dev-ticket",
                       changedPublished))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var preview = (await response.Content.ReadFromJsonAsync<SourceImportPreviewResult>())!;
                var entity = Assert.Single(preview.Entities);
                Assert.Equal(publishedEntity.EntityId, entity.EntityId);
                Assert.Equal(SourceImportPreviewActions.NewRevision, entity.Action);
            }

            using (var request = HostedJsonRequest(HttpMethod.Post, "/api/source-admin/import", "dev-ticket", changedPublished))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(2, Assert.Single((await response.Content.ReadFromJsonAsync<SourceImportResult>())!.Entities).RevisionNumber);
            }

            SourceRevisionReviewPreviewView sourceUpdatePreview;
            using (var request = HostedRequest(
                       HttpMethod.Get,
                       $"/api/global/rules/source-updates/{conceptId}/preview",
                       "rules-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                sourceUpdatePreview = (await response.Content.ReadFromJsonAsync<SourceRevisionReviewPreviewView>())!;
                Assert.True(sourceUpdatePreview.PatchCompatible);
                Assert.Equal(2, sourceUpdatePreview.Update.LatestRevisionNumber);
                Assert.Equal("delta", sourceUpdatePreview.CandidateResolvedDocument!
                    .Value.GetProperty("entries").EnumerateArray().Last().GetString());
                Assert.DoesNotContain(
                    sourceUpdatePreview.CandidateResolvedDocument.Value.GetProperty("entries").EnumerateArray(),
                    value => value.GetString() == "beta");
            }

            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/source-updates/{conceptId}/reject",
                       "rules-ticket",
                       new RejectLatestSourceRevisionRequest(
                           sourceUpdatePreview.Update.GlobalRuleDecisionId,
                           sourceUpdatePreview.Update.LatestSourceEntityRevisionId,
                           sourceUpdatePreview.Update.LatestFingerprint,
                           "Representative lifecycle deliberately retains the published revision after review.")))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True((await response.Content.ReadFromJsonAsync<SourceRevisionRejectionView>())!.Created);
            }

            using (var request = HostedRequest(HttpMethod.Get, "/api/global/rules/source-updates", "rules-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var updates = (await response.Content.ReadFromJsonAsync<IReadOnlyList<SourceRevisionReviewItemView>>())!;
                Assert.DoesNotContain(updates, value => value.RuleConceptId == conceptId);
            }
        }
        finally
        {
            await CleanupAsync(factory, packageKeys, entityIds);
        }
    }

    private static Import5eToolsDocumentRequest SourceRequest(
        string packageKey,
        string packageName,
        string workKey,
        string workName,
        string editionKey,
        string editionName,
        string gameEdition,
        string releaseKind,
        string entityName,
        string sourceCode,
        IReadOnlyList<string> entries,
        string? reprintedAs = null) =>
        new(
            packageKey,
            packageName,
            "integration-test",
            "test-only",
            true,
            workKey,
            workName,
            editionKey,
            editionName,
            SourceJson(entityName, sourceCode, entries, reprintedAs),
            gameEdition,
            releaseKind,
            new DateOnly(2024, 1, 1));

    private static string SourceJson(
        string entityName,
        string sourceCode,
        IReadOnlyList<string> entries,
        string? reprintedAs = null)
    {
        var value = new Dictionary<string, object?>
        {
            ["name"] = entityName,
            ["source"] = sourceCode,
            ["entries"] = entries
        };
        if (reprintedAs is not null)
        {
            value["reprintedAs"] = new[] { reprintedAs };
        }
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["feat"] = new[] { value }
        });
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

    private static HttpRequestMessage HostedJsonRequest<T>(HttpMethod method, string path, string ticket, T body)
    {
        var request = HostedRequest(method, path, ticket);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private static ToolHostAuthenticationContext Context(
        string userId,
        IReadOnlyList<string> globalRoles,
        Guid? campaignId = null,
        string? campaignRole = null) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: "dorks-and-dice",
            User: new ToolHostUserContext(userId, userId),
            GlobalRoles: globalRoles,
            Campaigns: campaignId is not null && campaignRole is not null
                ? [new ToolHostCampaignContext(campaignId.Value, "Lifecycle Campaign", campaignRole)]
                : []);

    private static async Task CleanupAsync(
        WebApplicationFactory<Program> factory,
        IReadOnlyCollection<string> packageKeys,
        IReadOnlyCollection<Guid> sourceEntityIds)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

        await db.CampaignRulesetRevisionEntries.ExecuteDeleteAsync();
        await db.CampaignRulesetRevisions.ExecuteDeleteAsync();
        await db.CampaignRuleDecisions.ExecuteDeleteAsync();
        await db.CampaignRulesetSelections.ExecuteDeleteAsync();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM source_revision_rejection;");
        await db.RulesetRevisionEntries.ExecuteDeleteAsync();
        await db.RulesetRevisions.ExecuteDeleteAsync();
        await db.GlobalRuleDecisions.ExecuteDeleteAsync();
        await db.RuleConceptSourceBindings.ExecuteDeleteAsync();
        await db.RuleConcepts.ExecuteDeleteAsync();

        foreach (var entityId in sourceEntityIds.Distinct())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM source_entity_lineage WHERE from_source_entity_id = {entityId} OR to_source_entity_id = {entityId}");
        }

        var packages = await db.SourcePackages
            .Where(value => packageKeys.Contains(value.Key))
            .ToArrayAsync();
        if (packages.Length > 0)
        {
            db.SourcePackages.RemoveRange(packages);
            await db.SaveChangesAsync();
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
