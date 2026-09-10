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
public sealed class CampaignBaselineDiscoveryIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task DmCanDiscoverAndPreviewBaselineMigrationWithIndependentAuthority()
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

        var packageKey = $"baseline-discovery-{Guid.NewGuid():N}";
        var conceptAKey = $"skill.baseline-a.{Guid.NewGuid():N}";
        var conceptBKey = $"skill.baseline-b.{Guid.NewGuid():N}";
        var conceptCKey = $"skill.baseline-c.{Guid.NewGuid():N}";
        Guid packageId = Guid.Empty;

        try
        {
            Guid conceptAId;
            Guid conceptBId;
            Guid conceptCId;
            PublishedRulesetRevisionView globalRevision1;
            PublishedRulesetRevisionView globalRevision2;

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var campaignRules = scope.ServiceProvider.GetRequiredService<ICampaignRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

                var imported = await importer.Import5eToolsDocumentAsync(SourceRequest(packageKey));
                packageId = imported.PackageId;
                var sourceEntityId = imported.Entities.Single().EntityId;
                var sourceRevisionId = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == sourceEntityId)
                    .Select(value => value.Id)
                    .SingleAsync();

                conceptAId = (await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(conceptAKey, "skill", "Baseline A"),
                    "rules-lawyer")).Value.Id;
                conceptBId = (await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(conceptBKey, "skill", "Baseline B"),
                    "rules-lawyer")).Value.Id;

                foreach (var conceptId in new[] { conceptAId, conceptBId })
                {
                    await globalRules.BindSourceEntityAsync(
                        conceptId,
                        new BindRuleConceptSourceRequest(sourceEntityId),
                        "rules-lawyer");
                    await globalRules.SetDecisionAsync(
                        conceptId,
                        new SetGlobalRuleDecisionRequest(sourceRevisionId, "Initial global baseline."),
                        "rules-lawyer");
                }

                globalRevision1 = await globalRules.PublishAsync("rules-lawyer");
                Assert.Equal(2, globalRevision1.EntryCount);

                await campaignRules.SelectBaselineAsync(
                    campaignId,
                    new SelectCampaignRulesetBaselineRequest(globalRevision1.Id),
                    "campaign-dm");
                await campaignRules.SetDecisionAsync(
                    campaignId,
                    conceptAId,
                    new SetCampaignRuleDecisionRequest(
                        CampaignRuleDecisionKinds.SelectSource,
                        sourceRevisionId,
                        "Campaign-specific decision remains attached across migration."),
                    "campaign-dm");
                var publishedCampaign = await campaignRules.PublishAsync(campaignId, "campaign-dm");
                Assert.Equal(globalRevision1.Id, publishedCampaign.BaselineRulesetRevisionId);

                using var mergePatchDocument = JsonDocument.Parse("""
                    {
                      "globalMarker": "updated"
                    }
                    """);
                await globalRules.SetDecisionAsync(
                    conceptAId,
                    new SetGlobalRuleDecisionRequest(
                        sourceRevisionId,
                        "Updated global implementation.",
                        MergePatch: mergePatchDocument.RootElement.Clone()),
                    "rules-lawyer");

                conceptCId = (await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(conceptCKey, "skill", "Baseline C"),
                    "rules-lawyer")).Value.Id;
                await globalRules.BindSourceEntityAsync(
                    conceptCId,
                    new BindRuleConceptSourceRequest(sourceEntityId),
                    "rules-lawyer");
                await globalRules.SetDecisionAsync(
                    conceptCId,
                    new SetGlobalRuleDecisionRequest(sourceRevisionId, "Added after the first global publication."),
                    "rules-lawyer");

                globalRevision2 = await globalRules.PublishAsync("rules-lawyer");
                Assert.Equal(3, globalRevision2.EntryCount);
                Assert.Equal(globalRevision1.RevisionNumber + 1, globalRevision2.RevisionNumber);
            }

            using (var anonymous = await client.GetAsync(
                       $"/api/campaigns/{campaignId}/rules/baselines"))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            }

            using (var playerRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules/baselines",
                       "player-ticket"))
            using (var playerResponse = await client.SendAsync(playerRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, playerResponse.StatusCode);
            }

            using (var outsiderRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules/baselines",
                       "outsider-ticket"))
            using (var outsiderResponse = await client.SendAsync(outsiderRequest))
            {
                Assert.Equal(HttpStatusCode.NotFound, outsiderResponse.StatusCode);
            }

            using (var wrongModeRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules/baselines",
                       "wrong-mode-dm-ticket"))
            using (var wrongModeResponse = await client.SendAsync(wrongModeRequest))
            {
                Assert.Equal(HttpStatusCode.NotFound, wrongModeResponse.StatusCode);
            }

            IReadOnlyList<CampaignBaselineCandidateSummaryView> candidates;
            using (var candidateRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules/baselines",
                       "dm-ticket"))
            using (var candidateResponse = await client.SendAsync(candidateRequest))
            {
                Assert.Equal(HttpStatusCode.OK, candidateResponse.StatusCode);
                Assert.Equal("no-store", candidateResponse.Headers.CacheControl?.ToString());
                candidates = (await candidateResponse.Content
                    .ReadFromJsonAsync<IReadOnlyList<CampaignBaselineCandidateSummaryView>>())!;
            }

            Assert.Equal(2, candidates.Count);
            var revision1Candidate = candidates.Single(value => value.RulesetRevisionId == globalRevision1.Id);
            var revision2Candidate = candidates.Single(value => value.RulesetRevisionId == globalRevision2.Id);
            Assert.True(revision1Candidate.IsSelectedBaseline);
            Assert.True(revision1Candidate.IsPublishedCampaignBaseline);
            Assert.False(revision2Candidate.IsSelectedBaseline);
            Assert.False(revision2Candidate.IsPublishedCampaignBaseline);

            using (var previewRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules/baselines/{globalRevision2.Id}/preview",
                       "dm-ticket"))
            using (var previewResponse = await client.SendAsync(previewRequest))
            {
                Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
                Assert.Equal("no-store", previewResponse.Headers.CacheControl?.ToString());
                var raw = await previewResponse.Content.ReadAsStringAsync();
                Assert.DoesNotContain("\"document\"", raw, StringComparison.OrdinalIgnoreCase);

                var preview = JsonSerializer.Deserialize<CampaignBaselineMigrationPreviewView>(
                    raw,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                Assert.NotNull(preview);
                Assert.Equal(globalRevision1.Id, preview.CurrentSelectedBaseline?.RulesetRevisionId);
                Assert.Equal(globalRevision1.Id, preview.PublishedCampaignBaseline?.RulesetRevisionId);
                Assert.Equal(globalRevision2.Id, preview.CandidateBaseline.RulesetRevisionId);
                Assert.Equal(1, preview.AddedConceptCount);
                Assert.Equal(0, preview.RemovedConceptCount);
                Assert.Equal(1, preview.ChangedConceptCount);
                Assert.Equal(1, preview.UnchangedConceptCount);
                Assert.Equal(1, preview.OverridesRemainingActiveCount);
                Assert.Equal(0, preview.OverridesBecomingInactiveCount);
                Assert.Equal(0, preview.HistoricalOverridesBecomingActiveCount);

                var changed = preview.Changes.Single(value => value.RuleConceptId == conceptAId);
                Assert.Equal("changed", changed.ChangeKind);
                Assert.Equal("remains-active", changed.OverrideImpact);
                Assert.Equal(CampaignRuleDecisionKinds.SelectSource, changed.LatestCampaignDecisionKind);

                var unchanged = preview.Changes.Single(value => value.RuleConceptId == conceptBId);
                Assert.Equal("unchanged", unchanged.ChangeKind);
                Assert.Null(unchanged.OverrideImpact);

                var added = preview.Changes.Single(value => value.RuleConceptId == conceptCId);
                Assert.Equal("added", added.ChangeKind);
                Assert.Null(added.CurrentGlobalDecisionNumber);
                Assert.NotNull(added.CandidateGlobalDecisionNumber);
            }

            using (var missingPreviewRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules/baselines/{Guid.NewGuid()}/preview",
                       "dm-ticket"))
            using (var missingPreviewResponse = await client.SendAsync(missingPreviewRequest))
            {
                Assert.Equal(HttpStatusCode.NotFound, missingPreviewResponse.StatusCode);
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
                ? [new ToolHostCampaignContext(campaignId.Value, "Baseline Discovery Campaign", campaignRole)]
                : []);

    private static Import5eToolsDocumentRequest SourceRequest(string packageKey) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: "Campaign Baseline Discovery Package",
            Provider: "integration-test",
            License: "test-only",
            IsPublic: true,
            WorkKey: "baseline-work",
            WorkDisplayName: "Baseline Work",
            EditionKey: "baseline-edition",
            EditionDisplayName: "Baseline Edition",
            Json: """
                {
                  "skill": [
                    {
                      "name": "Arcana",
                      "source": "TST",
                      "ability": "int",
                      "globalMarker": "initial"
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
