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
public sealed class SourceRevisionRejectionIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task RejectionDismissesOnlyTheExactDecisionAndRevisionReviewed()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["reviewer-ticket"] = Context("rejection-reviewer", ["Rules Lawyer"]),
            ["plain-ticket"] = Context("plain-user", [])
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"source-rejection-{token}";
        var conceptKey = $"skill.source-rejection-{token}";
        Guid conceptId = Guid.Empty;

        try
        {
            Guid revision1Id;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var rules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

                var first = await importer.Import5eToolsDocumentAsync(SourceRequest(packageKey, token, 1));
                var entityId = Assert.Single(first.Entities).EntityId;
                revision1Id = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == entityId)
                    .Select(value => value.Id)
                    .SingleAsync();

                conceptId = (await rules.CreateConceptAsync(
                    new CreateRuleConceptRequest(conceptKey, "skill", $"Source Rejection {token}"),
                    "seed-rules-lawyer")).Value.Id;
                await rules.BindSourceEntityAsync(
                    conceptId,
                    new BindRuleConceptSourceRequest(entityId),
                    "seed-rules-lawyer");
                await rules.SetDecisionAsync(
                    conceptId,
                    new SetGlobalRuleDecisionRequest(revision1Id, "Initial source decision."),
                    "seed-rules-lawyer");

                await importer.Import5eToolsDocumentAsync(SourceRequest(packageKey, token, 2));
            }

            var firstUpdate = await GetUpdateAsync(client, conceptId);
            var rejectRequest = new RejectLatestSourceRevisionRequest(
                firstUpdate.GlobalRuleDecisionId,
                firstUpdate.LatestSourceEntityRevisionId,
                firstUpdate.LatestFingerprint,
                "Reviewed revision 2 and deliberately retained the current rule source revision.");

            using (var plain = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/source-updates/{conceptId}/reject",
                       "plain-ticket",
                       rejectRequest))
            using (var response = await client.SendAsync(plain))
            {
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            SourceRevisionRejectionView rejection;
            using (var reject = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/source-updates/{conceptId}/reject",
                       "reviewer-ticket",
                       rejectRequest))
            using (var response = await client.SendAsync(reject))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                rejection = (await response.Content.ReadFromJsonAsync<SourceRevisionRejectionView>())!;
                Assert.True(rejection.Created);
                Assert.Equal(firstUpdate.GlobalRuleDecisionId, rejection.GlobalRuleDecisionId);
                Assert.Equal(firstUpdate.LatestSourceEntityRevisionId, rejection.SourceEntityRevisionId);
            }

            using (var repeated = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/source-updates/{conceptId}/reject",
                       "reviewer-ticket",
                       rejectRequest))
            using (var response = await client.SendAsync(repeated))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var result = (await response.Content.ReadFromJsonAsync<SourceRevisionRejectionView>())!;
                Assert.False(result.Created);
                Assert.Equal(rejection.Id, result.Id);
            }

            Assert.Null(await FindUpdateAsync(client, conceptId));
            using (var preview = HostedRequest(
                       HttpMethod.Get,
                       $"/api/global/rules/source-updates/{conceptId}/preview",
                       "reviewer-ticket"))
            using (var response = await client.SendAsync(preview))
            {
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var rules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                await rules.SetDecisionAsync(
                    conceptId,
                    new SetGlobalRuleDecisionRequest(revision1Id, "A newer global decision still deliberately retains revision 1."),
                    "seed-rules-lawyer");
            }

            var secondDecisionUpdate = await GetUpdateAsync(client, conceptId);
            Assert.NotEqual(firstUpdate.GlobalRuleDecisionId, secondDecisionUpdate.GlobalRuleDecisionId);
            Assert.Equal(firstUpdate.LatestSourceEntityRevisionId, secondDecisionUpdate.LatestSourceEntityRevisionId);

            var secondRejectRequest = new RejectLatestSourceRevisionRequest(
                secondDecisionUpdate.GlobalRuleDecisionId,
                secondDecisionUpdate.LatestSourceEntityRevisionId,
                secondDecisionUpdate.LatestFingerprint,
                "Reviewed revision 2 again in the context of the newer global decision.");
            using (var reject = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/source-updates/{conceptId}/reject",
                       "reviewer-ticket",
                       secondRejectRequest))
            using (var response = await client.SendAsync(reject))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
            Assert.Null(await FindUpdateAsync(client, conceptId));

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                await importer.Import5eToolsDocumentAsync(SourceRequest(packageKey, token, 3));
            }

            var thirdRevisionUpdate = await GetUpdateAsync(client, conceptId);
            Assert.Equal(secondDecisionUpdate.GlobalRuleDecisionId, thirdRevisionUpdate.GlobalRuleDecisionId);
            Assert.Equal(3, thirdRevisionUpdate.LatestRevisionNumber);
            Assert.NotEqual(secondDecisionUpdate.LatestSourceEntityRevisionId, thirdRevisionUpdate.LatestSourceEntityRevisionId);

            using (var stale = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/source-updates/{conceptId}/reject",
                       "reviewer-ticket",
                       secondRejectRequest))
            using (var response = await client.SendAsync(stale))
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                Assert.Equal(0, await db.RulesetRevisionEntries.CountAsync(value => value.RuleConceptId == conceptId));
            }
        }
        finally
        {
            await CleanupAsync(factory, conceptId, packageKey);
        }
    }

    private static async Task<SourceRevisionReviewItemView> GetUpdateAsync(HttpClient client, Guid conceptId) =>
        await FindUpdateAsync(client, conceptId)
            ?? throw new Xunit.Sdk.XunitException($"Expected source update for concept {conceptId}.");

    private static async Task<SourceRevisionReviewItemView?> FindUpdateAsync(HttpClient client, Guid conceptId)
    {
        using var request = HostedRequest(HttpMethod.Get, "/api/global/rules/source-updates", "reviewer-ticket");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updates = (await response.Content.ReadFromJsonAsync<IReadOnlyList<SourceRevisionReviewItemView>>())!;
        return updates.SingleOrDefault(value => value.RuleConceptId == conceptId);
    }

    private static Import5eToolsDocumentRequest SourceRequest(string packageKey, string token, int power) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: $"Source Rejection {token}",
            Provider: "integration-test",
            License: "test-only",
            IsPublic: true,
            WorkKey: $"{packageKey}-work",
            WorkDisplayName: $"Source Rejection {token} Work",
            EditionKey: $"{packageKey}-edition",
            EditionDisplayName: $"Source Rejection {token} Edition",
            Json: JsonSerializer.Serialize(new
            {
                skill = new[]
                {
                    new
                    {
                        name = $"Source Rejection {token}",
                        source = "REJECT",
                        power,
                        entries = new[] { "alpha", $"revision-{power}" }
                    }
                }
            }));

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

    private static ToolHostAuthenticationContext Context(string userId, IReadOnlyList<string> globalRoles) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: "dorks-and-dice",
            User: new ToolHostUserContext(userId, userId),
            GlobalRoles: globalRoles,
            Campaigns: []);

    private static async Task CleanupAsync(
        WebApplicationFactory<Program> factory,
        Guid conceptId,
        string packageKey)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        if (conceptId != Guid.Empty)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM source_revision_rejection
                WHERE global_rule_decision_id IN (
                    SELECT global_rule_decision_id
                    FROM global_rule_decision
                    WHERE rule_concept_id = {conceptId});
                """);
            await db.GlobalRuleDecisions
                .Where(value => value.RuleConceptId == conceptId)
                .ExecuteDeleteAsync();
            await db.RuleConceptSourceBindings
                .Where(value => value.RuleConceptId == conceptId)
                .ExecuteDeleteAsync();
            await db.RuleConcepts
                .Where(value => value.Id == conceptId)
                .ExecuteDeleteAsync();
        }

        var package = await db.SourcePackages.SingleOrDefaultAsync(value => value.Key == packageKey);
        if (package is not null)
        {
            db.SourcePackages.Remove(package);
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
