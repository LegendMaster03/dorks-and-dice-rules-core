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
public sealed class SourceRevisionAdoptionIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task CompatibleReviewedUpdateCanCreatePendingDecisionWithoutPublishing()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["reviewer-ticket"] = Context("adoption-reviewer", "dorks-and-dice", ["Rules Lawyer"]),
            ["ungranted-ticket"] = Context("adoption-ungranted", "dorks-and-dice", ["Rules Lawyer"]),
            ["plain-ticket"] = Context("adoption-reviewer", "dorks-and-dice", []),
            ["wrong-mode-ticket"] = Context("adoption-reviewer", "professional", ["Rules Lawyer"])
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var token = Guid.NewGuid().ToString("N")[..10];
        var compatiblePackageKey = $"adopt-compatible-{token}";
        var incompatiblePackageKey = $"adopt-incompatible-{token}";
        var privatePackageKey = $"adopt-private-{token}";
        var packageKeys = new[] { compatiblePackageKey, incompatiblePackageKey, privatePackageKey };
        var conceptIds = new List<Guid>();

        try
        {
            Guid compatibleConceptId;
            Guid incompatibleConceptId;
            Guid privateConceptId;
            string originalPatchJson;
            string originalPatchFingerprint;

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
                var rules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

                var compatibleImport = await importer.Import5eToolsDocumentAsync(SourceRequest(
                    compatiblePackageKey,
                    $"Adoption Compatible {token}",
                    "ADOPTCOMP",
                    isPublic: true,
                    power: 1,
                    entries: ["alpha", "beta"]));
                var compatibleEntityId = compatibleImport.Entities.Single().EntityId;
                var compatibleRevision1 = await LatestRevisionIdAsync(db, compatibleEntityId);
                compatibleConceptId = (await rules.CreateConceptAsync(
                    new CreateRuleConceptRequest(
                        $"skill.adoption-compatible-{token}",
                        "skill",
                        $"Adoption Compatible {token}"),
                    "seed-rules-lawyer")).Value.Id;
                conceptIds.Add(compatibleConceptId);
                await rules.BindSourceEntityAsync(
                    compatibleConceptId,
                    new BindRuleConceptSourceRequest(compatibleEntityId),
                    "seed-rules-lawyer");

                using var mergePatchDocument = JsonDocument.Parse("{\"custom\":true}");
                await rules.SetDecisionAsync(
                    compatibleConceptId,
                    new SetGlobalRuleDecisionRequest(
                        compatibleRevision1,
                        "Carry this exact decision forward.",
                        MergePatch: mergePatchDocument.RootElement.Clone()),
                    "seed-rules-lawyer");
                var originalDecision = await db.GlobalRuleDecisions
                    .AsNoTracking()
                    .SingleAsync(value => value.RuleConceptId == compatibleConceptId);
                originalPatchJson = originalDecision.PatchJson!;
                originalPatchFingerprint = originalDecision.PatchFingerprint!;

                await importer.Import5eToolsDocumentAsync(SourceRequest(
                    compatiblePackageKey,
                    $"Adoption Compatible {token}",
                    "ADOPTCOMP",
                    isPublic: true,
                    power: 2,
                    entries: ["alpha", "beta"]));

                var incompatibleImport = await importer.Import5eToolsDocumentAsync(SourceRequest(
                    incompatiblePackageKey,
                    $"Adoption Incompatible {token}",
                    "ADOPTINC",
                    isPublic: true,
                    power: 1,
                    entries: ["alpha", "beta"]));
                var incompatibleEntityId = incompatibleImport.Entities.Single().EntityId;
                var incompatibleRevision1 = await LatestRevisionIdAsync(db, incompatibleEntityId);
                incompatibleConceptId = (await rules.CreateConceptAsync(
                    new CreateRuleConceptRequest(
                        $"skill.adoption-incompatible-{token}",
                        "skill",
                        $"Adoption Incompatible {token}"),
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
                        "This patch must not be guessed across a breaking source update.",
                        StructuredPatch: structuredPatch),
                    "seed-rules-lawyer");
                await importer.Import5eToolsDocumentAsync(SourceRequest(
                    incompatiblePackageKey,
                    $"Adoption Incompatible {token}",
                    "ADOPTINC",
                    isPublic: true,
                    power: 2,
                    entries: ["alpha"]));

                var privateImport = await importer.Import5eToolsDocumentAsync(SourceRequest(
                    privatePackageKey,
                    $"Adoption Private {token}",
                    "ADOPTPRIVATE",
                    isPublic: false,
                    power: 1,
                    entries: ["secret"]));
                await grants.GrantAsync("adoption-reviewer", privateImport.PackageId);
                var privateEntityId = privateImport.Entities.Single().EntityId;
                var privateRevision1 = await LatestRevisionIdAsync(db, privateEntityId);
                privateConceptId = (await rules.CreateConceptAsync(
                    new CreateRuleConceptRequest(
                        $"skill.adoption-private-{token}",
                        "skill",
                        $"Adoption Private {token}"),
                    "seed-rules-lawyer")).Value.Id;
                conceptIds.Add(privateConceptId);
                await rules.BindSourceEntityAsync(
                    privateConceptId,
                    new BindRuleConceptSourceRequest(privateEntityId),
                    "seed-rules-lawyer");
                await rules.SetDecisionAsync(
                    privateConceptId,
                    new SetGlobalRuleDecisionRequest(privateRevision1, "Restricted adoption."),
                    "seed-rules-lawyer");
                await importer.Import5eToolsDocumentAsync(SourceRequest(
                    privatePackageKey,
                    $"Adoption Private {token}",
                    "ADOPTPRIVATE",
                    isPublic: false,
                    power: 2,
                    entries: ["secret"]));
            }

            SourceRevisionReviewItemView compatibleUpdate;
            SourceRevisionReviewItemView incompatibleUpdate;
            SourceRevisionReviewItemView privateUpdate;
            using (var updatesRequest = HostedRequest(
                       HttpMethod.Get,
                       "/api/global/rules/source-updates",
                       "reviewer-ticket"))
            using (var updatesResponse = await client.SendAsync(updatesRequest))
            {
                Assert.Equal(HttpStatusCode.OK, updatesResponse.StatusCode);
                var updates = (await updatesResponse.Content
                    .ReadFromJsonAsync<IReadOnlyList<SourceRevisionReviewItemView>>())!;
                compatibleUpdate = Assert.Single(updates, value => value.RuleConceptId == compatibleConceptId);
                incompatibleUpdate = Assert.Single(updates, value => value.RuleConceptId == incompatibleConceptId);
                privateUpdate = Assert.Single(updates, value => value.RuleConceptId == privateConceptId);
            }

            var compatibleRequestBody = AdoptionRequest(compatibleUpdate);

            using (var anonymousRequest = new HttpRequestMessage(
                       HttpMethod.Post,
                       $"/api/global/rules/source-updates/{compatibleConceptId}/adopt")
                   {
                       Content = JsonContent.Create(compatibleRequestBody)
                   })
            using (var anonymousResponse = await client.SendAsync(anonymousRequest))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
            }

            using (var plainRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/source-updates/{compatibleConceptId}/adopt",
                       "plain-ticket",
                       compatibleRequestBody))
            using (var plainResponse = await client.SendAsync(plainRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, plainResponse.StatusCode);
            }

            using (var wrongModeRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/source-updates/{compatibleConceptId}/adopt",
                       "wrong-mode-ticket",
                       compatibleRequestBody))
            using (var wrongModeResponse = await client.SendAsync(wrongModeRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, wrongModeResponse.StatusCode);
            }

            var staleFingerprintRequest = compatibleRequestBody with
            {
                ExpectedLatestFingerprint = new string('0', 64)
            };
            using (var staleSourceRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/source-updates/{compatibleConceptId}/adopt",
                       "reviewer-ticket",
                       staleFingerprintRequest))
            using (var staleSourceResponse = await client.SendAsync(staleSourceRequest))
            {
                Assert.Equal(HttpStatusCode.Conflict, staleSourceResponse.StatusCode);
            }

            AdoptedSourceRevisionView adopted;
            using (var adoptRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/source-updates/{compatibleConceptId}/adopt",
                       "reviewer-ticket",
                       compatibleRequestBody))
            using (var adoptResponse = await client.SendAsync(adoptRequest))
            {
                Assert.Equal(HttpStatusCode.OK, adoptResponse.StatusCode);
                Assert.Equal("no-store", adoptResponse.Headers.CacheControl?.ToString());
                adopted = (await adoptResponse.Content.ReadFromJsonAsync<AdoptedSourceRevisionView>())!;
            }

            Assert.Equal(compatibleConceptId, adopted.RuleConceptId);
            Assert.Equal(compatibleUpdate.GlobalRuleDecisionId, adopted.PreviousGlobalRuleDecisionId);
            Assert.NotEqual(adopted.PreviousGlobalRuleDecisionId, adopted.GlobalRuleDecisionId);
            Assert.Equal(compatibleUpdate.LatestSourceEntityRevisionId, adopted.SourceEntityRevisionId);
            Assert.Equal(compatibleUpdate.LatestRevisionNumber, adopted.SourceRevisionNumber);
            Assert.Equal(compatibleUpdate.LatestFingerprint, adopted.SourceFingerprint);
            Assert.True(adopted.RequiresPublication);

            using (var repeatedRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/source-updates/{compatibleConceptId}/adopt",
                       "reviewer-ticket",
                       compatibleRequestBody))
            using (var repeatedResponse = await client.SendAsync(repeatedRequest))
            {
                Assert.Equal(HttpStatusCode.Conflict, repeatedResponse.StatusCode);
            }

            using (var incompatibleRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/source-updates/{incompatibleConceptId}/adopt",
                       "reviewer-ticket",
                       AdoptionRequest(incompatibleUpdate)))
            using (var incompatibleResponse = await client.SendAsync(incompatibleRequest))
            {
                Assert.Equal(HttpStatusCode.Conflict, incompatibleResponse.StatusCode);
                var detail = await incompatibleResponse.Content.ReadAsStringAsync();
                Assert.Contains("can not be carried forward", detail, StringComparison.Ordinal);
            }

            using (var privateRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/source-updates/{privateConceptId}/adopt",
                       "ungranted-ticket",
                       AdoptionRequest(privateUpdate)))
            using (var privateResponse = await client.SendAsync(privateRequest))
            {
                Assert.Equal(HttpStatusCode.NotFound, privateResponse.StatusCode);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var compatibleDecisions = await db.GlobalRuleDecisions
                    .AsNoTracking()
                    .Where(value => value.RuleConceptId == compatibleConceptId)
                    .OrderBy(value => value.DecisionNumber)
                    .ToArrayAsync();
                Assert.Equal(2, compatibleDecisions.Length);
                Assert.Equal(compatibleUpdate.SelectedSourceEntityRevisionId,
                    compatibleDecisions[0].SelectedSourceEntityRevisionId);
                Assert.Equal(compatibleUpdate.LatestSourceEntityRevisionId,
                    compatibleDecisions[1].SelectedSourceEntityRevisionId);
                Assert.Equal(compatibleDecisions[0].DecisionKind, compatibleDecisions[1].DecisionKind);
                Assert.Equal(originalPatchJson, compatibleDecisions[1].PatchJson);
                Assert.Equal(originalPatchFingerprint, compatibleDecisions[1].PatchFingerprint);
                Assert.Equal(compatibleDecisions[0].Note, compatibleDecisions[1].Note);
                Assert.Equal("adoption-reviewer", compatibleDecisions[1].CreatedByUserId);

                Assert.Single(await db.GlobalRuleDecisions
                    .Where(value => value.RuleConceptId == incompatibleConceptId)
                    .ToArrayAsync());
                Assert.Single(await db.GlobalRuleDecisions
                    .Where(value => value.RuleConceptId == privateConceptId)
                    .ToArrayAsync());
                Assert.Equal(0, await db.RulesetRevisionEntries
                    .CountAsync(value => conceptIds.Contains(value.RuleConceptId)));
            }

            using (var updatesAfterRequest = HostedRequest(
                       HttpMethod.Get,
                       "/api/global/rules/source-updates",
                       "reviewer-ticket"))
            using (var updatesAfterResponse = await client.SendAsync(updatesAfterRequest))
            {
                Assert.Equal(HttpStatusCode.OK, updatesAfterResponse.StatusCode);
                var updates = (await updatesAfterResponse.Content
                    .ReadFromJsonAsync<IReadOnlyList<SourceRevisionReviewItemView>>())!;
                Assert.DoesNotContain(updates, value => value.RuleConceptId == compatibleConceptId);
                Assert.Contains(updates, value => value.RuleConceptId == incompatibleConceptId);
                Assert.Contains(updates, value => value.RuleConceptId == privateConceptId);
            }
        }
        finally
        {
            await CleanupAsync(factory, conceptIds, packageKeys);
        }
    }

    private static AdoptLatestSourceRevisionRequest AdoptionRequest(SourceRevisionReviewItemView update) =>
        new(
            update.GlobalRuleDecisionId,
            update.LatestSourceEntityRevisionId,
            update.LatestFingerprint);

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
