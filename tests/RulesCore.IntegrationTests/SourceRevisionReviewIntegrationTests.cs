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
public sealed class SourceRevisionReviewIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task RulesLawyerCanReviewNewerAccessibleSourceRevisionsWithoutMutatingRules()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["reviewer-ticket"] = Context("source-reviewer", "dorks-and-dice", ["Rules Lawyer"]),
            ["ungranted-ticket"] = Context("source-reviewer-ungranted", "dorks-and-dice", ["Rules Lawyer"]),
            ["plain-ticket"] = Context("source-reviewer", "dorks-and-dice", []),
            ["wrong-mode-ticket"] = Context("source-reviewer", "professional", ["Rules Lawyer"])
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var token = Guid.NewGuid().ToString("N")[..10];
        var publicKey = $"source-review-public-{token}";
        var incompatibleKey = $"source-review-incompatible-{token}";
        var privateKey = $"source-review-private-{token}";
        var packageKeys = new[] { publicKey, incompatibleKey, privateKey };
        var conceptIds = new List<Guid>();

        try
        {
            Guid publicConceptId;
            Guid incompatibleConceptId;
            Guid privateConceptId;
            int originalDecisionCount;

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
                var rules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

                var publicImport = await importer.Import5eToolsDocumentAsync(SourceRequest(
                    publicKey,
                    $"Public Review {token}",
                    "REVIEWPUB",
                    isPublic: true,
                    power: 1,
                    entries: ["alpha", "beta"]));
                var publicEntityId = publicImport.Entities.Single().EntityId;
                var publicRevision1 = await LatestRevisionIdAsync(db, publicEntityId);
                publicConceptId = (await rules.CreateConceptAsync(
                    new CreateRuleConceptRequest($"skill.source-review-public-{token}", "skill", $"Public Review {token}"),
                    "seed-rules-lawyer")).Value.Id;
                conceptIds.Add(publicConceptId);
                await rules.BindSourceEntityAsync(
                    publicConceptId,
                    new BindRuleConceptSourceRequest(publicEntityId),
                    "seed-rules-lawyer");
                await rules.SetDecisionAsync(
                    publicConceptId,
                    new SetGlobalRuleDecisionRequest(publicRevision1, "Review public source update."),
                    "seed-rules-lawyer");
                await importer.Import5eToolsDocumentAsync(SourceRequest(
                    publicKey,
                    $"Public Review {token}",
                    "REVIEWPUB",
                    isPublic: true,
                    power: 2,
                    entries: ["alpha", "beta"]));

                var incompatibleImport = await importer.Import5eToolsDocumentAsync(SourceRequest(
                    incompatibleKey,
                    $"Patch Review {token}",
                    "REVIEWPATCH",
                    isPublic: true,
                    power: 1,
                    entries: ["alpha", "beta"]));
                var incompatibleEntityId = incompatibleImport.Entities.Single().EntityId;
                var incompatibleRevision1 = await LatestRevisionIdAsync(db, incompatibleEntityId);
                incompatibleConceptId = (await rules.CreateConceptAsync(
                    new CreateRuleConceptRequest($"skill.source-review-patch-{token}", "skill", $"Patch Review {token}"),
                    "seed-rules-lawyer")).Value.Id;
                conceptIds.Add(incompatibleConceptId);
                await rules.BindSourceEntityAsync(
                    incompatibleConceptId,
                    new BindRuleConceptSourceRequest(incompatibleEntityId),
                    "seed-rules-lawyer");

                using var beta = JsonDocument.Parse("\"beta\"");
                var structuredPatch = new RuleStructuredPatchRequest(
                    ArrayOperations:
                    [
                        new RuleArrayOperationRequest(
                            RuleArrayOperationKinds.Remove,
                            "/entries",
                            Match: new RuleArrayItemSelectorRequest(null, beta.RootElement.Clone()))
                    ]);
                await rules.SetDecisionAsync(
                    incompatibleConceptId,
                    new SetGlobalRuleDecisionRequest(
                        incompatibleRevision1,
                        "Patch becomes incompatible with the next source revision.",
                        StructuredPatch: structuredPatch),
                    "seed-rules-lawyer");
                await importer.Import5eToolsDocumentAsync(SourceRequest(
                    incompatibleKey,
                    $"Patch Review {token}",
                    "REVIEWPATCH",
                    isPublic: true,
                    power: 2,
                    entries: ["alpha"]));

                var privateImport = await importer.Import5eToolsDocumentAsync(SourceRequest(
                    privateKey,
                    $"Private Review {token}",
                    "REVIEWPRIVATE",
                    isPublic: false,
                    power: 1,
                    entries: ["secret"]));
                await grants.GrantAsync("source-reviewer", privateImport.PackageId);
                var privateEntityId = privateImport.Entities.Single().EntityId;
                var privateRevision1 = await LatestRevisionIdAsync(db, privateEntityId);
                privateConceptId = (await rules.CreateConceptAsync(
                    new CreateRuleConceptRequest($"skill.source-review-private-{token}", "skill", $"Private Review {token}"),
                    "seed-rules-lawyer")).Value.Id;
                conceptIds.Add(privateConceptId);
                await rules.BindSourceEntityAsync(
                    privateConceptId,
                    new BindRuleConceptSourceRequest(privateEntityId),
                    "seed-rules-lawyer");
                await rules.SetDecisionAsync(
                    privateConceptId,
                    new SetGlobalRuleDecisionRequest(privateRevision1, "Restricted source update."),
                    "seed-rules-lawyer");
                await importer.Import5eToolsDocumentAsync(SourceRequest(
                    privateKey,
                    $"Private Review {token}",
                    "REVIEWPRIVATE",
                    isPublic: false,
                    power: 2,
                    entries: ["secret"]));

                originalDecisionCount = await db.GlobalRuleDecisions
                    .CountAsync(value => conceptIds.Contains(value.RuleConceptId));
            }

            using (var anonymous = await client.GetAsync("/api/global/rules/source-updates"))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            }

            using (var plainRequest = HostedRequest(HttpMethod.Get, "/api/global/rules/source-updates", "plain-ticket"))
            using (var plainResponse = await client.SendAsync(plainRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, plainResponse.StatusCode);
            }

            using (var wrongModeRequest = HostedRequest(HttpMethod.Get, "/api/global/rules/source-updates", "wrong-mode-ticket"))
            using (var wrongModeResponse = await client.SendAsync(wrongModeRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, wrongModeResponse.StatusCode);
            }

            IReadOnlyList<SourceRevisionReviewItemView> reviewerUpdates;
            using (var reviewerRequest = HostedRequest(HttpMethod.Get, "/api/global/rules/source-updates", "reviewer-ticket"))
            using (var reviewerResponse = await client.SendAsync(reviewerRequest))
            {
                Assert.Equal(HttpStatusCode.OK, reviewerResponse.StatusCode);
                Assert.Equal("no-store", reviewerResponse.Headers.CacheControl?.ToString());
                reviewerUpdates = (await reviewerResponse.Content
                    .ReadFromJsonAsync<IReadOnlyList<SourceRevisionReviewItemView>>())!;
            }

            Assert.Equal(3, reviewerUpdates.Count(value => conceptIds.Contains(value.RuleConceptId)));
            Assert.All(reviewerUpdates.Where(value => conceptIds.Contains(value.RuleConceptId)), update =>
            {
                Assert.Equal(1, update.SelectedRevisionNumber);
                Assert.Equal(2, update.LatestRevisionNumber);
                Assert.Equal(1, update.NewerRevisionCount);
            });

            using (var ungrantedRequest = HostedRequest(HttpMethod.Get, "/api/global/rules/source-updates", "ungranted-ticket"))
            using (var ungrantedResponse = await client.SendAsync(ungrantedRequest))
            {
                Assert.Equal(HttpStatusCode.OK, ungrantedResponse.StatusCode);
                var updates = (await ungrantedResponse.Content
                    .ReadFromJsonAsync<IReadOnlyList<SourceRevisionReviewItemView>>())!;
                Assert.Contains(updates, value => value.RuleConceptId == publicConceptId);
                Assert.Contains(updates, value => value.RuleConceptId == incompatibleConceptId);
                Assert.DoesNotContain(updates, value => value.RuleConceptId == privateConceptId);
            }

            using (var previewRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/global/rules/source-updates/{publicConceptId}/preview",
                       "reviewer-ticket"))
            using (var previewResponse = await client.SendAsync(previewRequest))
            {
                Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
                var preview = (await previewResponse.Content
                    .ReadFromJsonAsync<SourceRevisionReviewPreviewView>())!;
                Assert.True(preview.PatchCompatible);
                Assert.NotNull(preview.CandidateResolvedDocument);
                Assert.Contains(preview.Changes, change => change.Path == "/power");
            }

            using (var incompatibleRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/global/rules/source-updates/{incompatibleConceptId}/preview",
                       "reviewer-ticket"))
            using (var incompatibleResponse = await client.SendAsync(incompatibleRequest))
            {
                Assert.Equal(HttpStatusCode.OK, incompatibleResponse.StatusCode);
                var preview = (await incompatibleResponse.Content
                    .ReadFromJsonAsync<SourceRevisionReviewPreviewView>())!;
                Assert.False(preview.PatchCompatible);
                Assert.Null(preview.CandidateResolvedDocument);
                Assert.Contains("exactly one matching item", preview.CompatibilityMessage, StringComparison.Ordinal);
            }

            using (var privateRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/global/rules/source-updates/{privateConceptId}/preview",
                       "ungranted-ticket"))
            using (var privateResponse = await client.SendAsync(privateRequest))
            {
                Assert.Equal(HttpStatusCode.NotFound, privateResponse.StatusCode);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                Assert.Equal(originalDecisionCount, await db.GlobalRuleDecisions
                    .CountAsync(value => conceptIds.Contains(value.RuleConceptId)));
                Assert.Equal(0, await db.RulesetRevisionEntries
                    .CountAsync(value => conceptIds.Contains(value.RuleConceptId)));
            }
        }
        finally
        {
            await CleanupAsync(factory, conceptIds, packageKeys);
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

    private static async Task<Guid> LatestRevisionIdAsync(RulesCoreDbContext db, Guid sourceEntityId) =>
        await db.SourceEntityRevisions
            .Where(value => value.SourceEntityId == sourceEntityId)
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => value.Id)
            .FirstAsync();

    private static HttpRequestMessage HostedRequest(HttpMethod method, string path, string ticket)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(ToolHostAuthenticationHeaders.Ticket, ticket);
        request.Headers.Add(ToolHostAuthenticationHeaders.IntrospectionPath, IntrospectionPath);
        return request;
    }

    private static ToolHostAuthenticationContext Context(
        string userId,
        string siteMode,
        IReadOnlyList<string> globalRoles) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: siteMode,
            User: new ToolHostUserContext(userId, userId),
            GlobalRoles: globalRoles,
            Campaigns: []);

    private static Import5eToolsDocumentRequest SourceRequest(
        string packageKey,
        string displayName,
        string sourceCode,
        bool isPublic,
        int power,
        IReadOnlyList<string> entries) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: displayName,
            Provider: "integration-test",
            License: "test-only",
            IsPublic: isPublic,
            WorkKey: $"{packageKey}-work",
            WorkDisplayName: $"{displayName} Work",
            EditionKey: $"{packageKey}-edition",
            EditionDisplayName: $"{displayName} Edition",
            Json: JsonSerializer.Serialize(new
            {
                skill = new[]
                {
                    new
                    {
                        name = displayName,
                        source = sourceCode,
                        power,
                        entries
                    }
                }
            }));

    private static async Task CleanupAsync(
        WebApplicationFactory<Program> factory,
        IReadOnlyCollection<Guid> conceptIds,
        IReadOnlyCollection<string> packageKeys)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

        if (conceptIds.Count > 0)
        {
            await db.GlobalRuleDecisions
                .Where(value => conceptIds.Contains(value.RuleConceptId))
                .ExecuteDeleteAsync();
            await db.RuleConceptSourceBindings
                .Where(value => conceptIds.Contains(value.RuleConceptId))
                .ExecuteDeleteAsync();
            await db.RuleConcepts
                .Where(value => conceptIds.Contains(value.Id))
                .ExecuteDeleteAsync();
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
