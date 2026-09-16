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
public sealed class CurrentUserSourceRemovalLifecycleIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task RemoveAndReAddReuseSourceIdentityAndPreserveRuleDecision()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore"))) return;

        var token = Guid.NewGuid().ToString("N");
        var userId = $"source-removal-{token}";
        var sourceCode = $"REM{token[..8].ToUpperInvariant()}";
        var entityName = "Arcana";
        var authenticationClient = new FakeToolHostAuthenticationClient(
            new Dictionary<string, ToolHostAuthenticationContext>
            {
                ["user-ticket"] = Context(userId)
            });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var payload = new AddCurrentUserSourceRequest(
            CurrentUserSourceKinds.Upload,
            FileName: "reusable-source.json",
            Json: $$"""
                {
                  "skill": [
                    { "name": "{{entityName}}", "source": "{{sourceCode}}", "ability": "int" }
                  ]
                }
                """);

        CurrentUserSourceView first;
        Guid conceptId = Guid.Empty;
        Guid sourceEntityId = Guid.Empty;
        Guid sourceRevisionId = Guid.Empty;
        Guid sourceRepresentationId = Guid.Empty;
        Guid decisionId = Guid.Empty;
        string? patchFingerprint = null;

        try
        {
            using (var request = HostedJsonRequest("/api/sources/current-user", "user-ticket", payload))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                first = (await response.Content.ReadFromJsonAsync<CurrentUserSourceView>())!;
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var entity = await db.SourceEntities.SingleAsync(value =>
                    value.SourcePackageId == first.SourcePackageId
                    && value.Name == entityName);
                var revision = await db.SourceEntityRevisions.SingleAsync(value =>
                    value.SourceEntityId == entity.Id);
                sourceEntityId = entity.Id;
                sourceRevisionId = revision.Id;
                sourceRepresentationId = revision.SourceRepresentationId;

                Assert.True(await db.UserSourceGrants.AnyAsync(value =>
                    value.UserId == userId
                    && value.SourcePackageId == first.SourcePackageId));

                var rules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var concept = (await rules.CreateConceptAsync(
                    new CreateRuleConceptRequest(
                        $"source-removal-{token}",
                        entity.EntityType,
                        entity.Name),
                    "rules-lawyer-test")).Value;
                conceptId = concept.Id;
                _ = await rules.BindSourceEntityAsync(
                    concept.Id,
                    new BindRuleConceptSourceRequest(entity.Id),
                    "rules-lawyer-test");

                using var patch = JsonDocument.Parse("""
                    {"ability":"wis","retainedByRemoval":true}
                    """);
                var decision = (await rules.SetDecisionAsync(
                    concept.Id,
                    new SetGlobalRuleDecisionRequest(
                        revision.Id,
                        "Removal preservation test.",
                        MergePatch: patch.RootElement.Clone()),
                    "rules-lawyer-test")).Value;
                decisionId = decision.Id;
                patchFingerprint = decision.PatchFingerprint;
                Assert.False(string.IsNullOrWhiteSpace(patchFingerprint));
            }

            using (var request = HostedRequest(
                HttpMethod.Delete,
                $"/api/sources/current-user/{first.Id}",
                "user-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            using (var request = HostedRequest(HttpMethod.Get, "/api/sources/current-user", "user-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var sources = await response.Content.ReadFromJsonAsync<CurrentUserSourceView[]>();
                Assert.DoesNotContain(sources!, value => value.SourcePackageId == first.SourcePackageId);
            }

            using (var request = HostedRequest(
                HttpMethod.Get,
                $"/api/sources/entities?q={Uri.EscapeDataString(sourceCode)}&limit=20",
                "user-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var entities = await response.Content.ReadFromJsonAsync<SourceEntitySummary[]>();
                Assert.DoesNotContain(entities!, value => value.EntityId == sourceEntityId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                Assert.True(await db.SourcePackages.AnyAsync(value => value.Id == first.SourcePackageId));
                Assert.True(await db.SourceEntities.AnyAsync(value => value.Id == sourceEntityId));
                Assert.True(await db.SourceEntityRevisions.AnyAsync(value => value.Id == sourceRevisionId));
                Assert.True(await db.GlobalRuleDecisions.AnyAsync(value =>
                    value.Id == decisionId
                    && value.SelectedSourceEntityRevisionId == sourceRevisionId
                    && value.PatchFingerprint == patchFingerprint));
                Assert.False(await db.UserSourceGrants.AnyAsync(value =>
                    value.UserId == userId
                    && value.SourcePackageId == first.SourcePackageId));
            }

            CurrentUserSourceView second;
            using (var request = HostedJsonRequest("/api/sources/current-user", "user-ticket", payload))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                second = (await response.Content.ReadFromJsonAsync<CurrentUserSourceView>())!;
            }

            Assert.Equal(first.SourcePackageId, second.SourcePackageId);
            Assert.NotEqual(first.Id, second.Id);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var entity = await db.SourceEntities.SingleAsync(value => value.Id == sourceEntityId);
                var revision = await db.SourceEntityRevisions.SingleAsync(value => value.SourceEntityId == sourceEntityId);
                Assert.Equal(sourceRevisionId, revision.Id);
                Assert.Equal(sourceRepresentationId, revision.SourceRepresentationId);
                Assert.Equal(1, await db.SourceEntityRevisions.CountAsync(value => value.SourceEntityId == sourceEntityId));
                Assert.True(await db.UserSourceGrants.AnyAsync(value =>
                    value.UserId == userId
                    && value.SourcePackageId == first.SourcePackageId));
                Assert.True(await db.GlobalRuleDecisions.AnyAsync(value =>
                    value.Id == decisionId
                    && value.PatchFingerprint == patchFingerprint));
                Assert.Equal(entityName, entity.Name);
            }

            using (var request = HostedRequest(
                HttpMethod.Get,
                $"/api/sources/entities?q={Uri.EscapeDataString(sourceCode)}&limit=20",
                "user-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var entities = await response.Content.ReadFromJsonAsync<SourceEntitySummary[]>();
                Assert.Contains(entities!, value => value.EntityId == sourceEntityId);
            }
        }
        finally
        {
            await using var cleanupScope = factory.Services.CreateAsyncScope();
            var db = cleanupScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

            if (conceptId != Guid.Empty)
            {
                var concept = await db.RuleConcepts.SingleOrDefaultAsync(value => value.Id == conceptId);
                if (concept is not null)
                {
                    db.RuleConcepts.Remove(concept);
                    await db.SaveChangesAsync();
                }
            }

            var packageId = await db.SourceEntities
                .Where(value => value.Id == sourceEntityId)
                .Select(value => (Guid?)value.SourcePackageId)
                .SingleOrDefaultAsync();
            if (packageId is not null)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($$"""
                    DELETE FROM current_user_source
                    WHERE user_id = {{userId}}
                        AND source_package_id = {{packageId.Value}};
                    """);
                var package = await db.SourcePackages.SingleOrDefaultAsync(value => value.Id == packageId.Value);
                if (package is not null)
                {
                    db.SourcePackages.Remove(package);
                    await db.SaveChangesAsync();
                }
            }
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

    private static HttpRequestMessage HostedJsonRequest(
        string path,
        string ticket,
        AddCurrentUserSourceRequest request)
    {
        var message = HostedRequest(HttpMethod.Post, path, ticket);
        message.Content = JsonContent.Create(request);
        return message;
    }

    private static HttpRequestMessage HostedRequest(HttpMethod method, string path, string ticket)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(ToolHostAuthenticationHeaders.Ticket, ticket);
        request.Headers.Add(ToolHostAuthenticationHeaders.IntrospectionPath, IntrospectionPath);
        return request;
    }

    private static ToolHostAuthenticationContext Context(string userId) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: "dorks-and-dice",
            User: new ToolHostUserContext(userId, userId),
            GlobalRoles: [],
            Campaigns: []);

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
