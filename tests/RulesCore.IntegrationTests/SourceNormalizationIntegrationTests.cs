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
public sealed class SourceNormalizationIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task RulesLawyerCanReviewAndAcceptOnlyAccessiblePackageOwnedSources()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore"))) return;

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["rules-lawyer-ticket"] = Context("normalizer", "dorks-and-dice", ["Rules Lawyer"]),
            ["ungranted-ticket"] = Context("ungranted-normalizer", "dorks-and-dice", ["Rules Lawyer"]),
            ["plain-ticket"] = Context("normalizer", "dorks-and-dice", [])
        });
        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var public2014Key = $"normalization-2014-{Guid.NewGuid():N}";
        var public2024Key = $"normalization-2024-{Guid.NewGuid():N}";
        var restrictedKey = $"normalization-private-{Guid.NewGuid():N}";
        var packageKeys = new[] { public2014Key, public2024Key, restrictedKey };
        Guid public2014EntityId = Guid.Empty;
        Guid public2024EntityId = Guid.Empty;
        Guid restrictedEntityId = Guid.Empty;

        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();

                var public2014 = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(public2014Key, "2014", "Arcana", "PHB14", true));
                public2014EntityId = public2014.Entities.Single().EntityId;

                var public2024 = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(public2024Key, "2024", "Arcana", "PHB24", true));
                public2024EntityId = public2024.Entities.Single().EntityId;

                var restricted = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(restrictedKey, "private", "Restricted Lore", "PRIVATE", false));
                restrictedEntityId = restricted.Entities.Single().EntityId;
                await grants.GrantAsync("normalizer", restricted.PackageId);
            }

            using (var anonymous = await client.GetAsync("/api/global/rules/normalization/candidates?q=Arcana"))
                Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

            using (var plainRequest = HostedRequest(HttpMethod.Get,
                       "/api/global/rules/normalization/candidates?q=Arcana", "plain-ticket"))
            using (var plainResponse = await client.SendAsync(plainRequest))
                Assert.Equal(HttpStatusCode.Forbidden, plainResponse.StatusCode);

            using (var inaccessibleRequest = HostedRequest(HttpMethod.Get,
                       "/api/global/rules/normalization/candidates?q=Restricted", "ungranted-ticket"))
            using (var inaccessibleResponse = await client.SendAsync(inaccessibleRequest))
            {
                Assert.Equal(HttpStatusCode.OK, inaccessibleResponse.StatusCode);
                Assert.Empty((await inaccessibleResponse.Content
                    .ReadFromJsonAsync<IReadOnlyList<SourceNormalizationCandidateView>>())!);
            }

            IReadOnlyList<SourceNormalizationCandidateView> arcanaCandidates;
            using (var candidatesRequest = HostedRequest(HttpMethod.Get,
                       "/api/global/rules/normalization/candidates?entityType=skill&q=Arcana", "rules-lawyer-ticket"))
            using (var candidatesResponse = await client.SendAsync(candidatesRequest))
            {
                Assert.Equal(HttpStatusCode.OK, candidatesResponse.StatusCode);
                arcanaCandidates = (await candidatesResponse.Content
                    .ReadFromJsonAsync<IReadOnlyList<SourceNormalizationCandidateView>>())!;
            }
            Assert.Equal(2, arcanaCandidates.Count);
            Assert.All(arcanaCandidates, value => Assert.Equal("skill.arcana", value.SuggestedConceptKey));

            AcceptedSourceNormalizationView firstAccepted;
            using (var request = HostedRequest(HttpMethod.Post,
                       $"/api/global/rules/normalization/entities/{public2014EntityId}/accept", "rules-lawyer-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                firstAccepted = (await response.Content.ReadFromJsonAsync<AcceptedSourceNormalizationView>())!;
                Assert.True(firstAccepted.CreatedConcept);
                Assert.True(firstAccepted.CreatedBinding);
                Assert.Equal("skill.arcana", firstAccepted.Concept.Key);
            }

            using (var request = HostedRequest(HttpMethod.Post,
                       $"/api/global/rules/normalization/entities/{public2024EntityId}/accept", "rules-lawyer-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var accepted = (await response.Content.ReadFromJsonAsync<AcceptedSourceNormalizationView>())!;
                Assert.False(accepted.CreatedConcept);
                Assert.True(accepted.CreatedBinding);
                Assert.Equal(firstAccepted.Concept.Id, accepted.Concept.Id);
            }

            using (var restrictedRequest = HostedRequest(HttpMethod.Get,
                       "/api/global/rules/normalization/candidates?q=Restricted", "rules-lawyer-ticket"))
            using (var restrictedResponse = await client.SendAsync(restrictedRequest))
            {
                Assert.Equal(HttpStatusCode.OK, restrictedResponse.StatusCode);
                var restricted = Assert.Single((await restrictedResponse.Content
                    .ReadFromJsonAsync<IReadOnlyList<SourceNormalizationCandidateView>>())!);
                Assert.Equal(restrictedEntityId, restricted.SourceEntityId);
            }

            await using var verificationScope = factory.Services.CreateAsyncScope();
            var db = verificationScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            Assert.Equal(2, await db.RuleConceptSourceBindings.CountAsync(
                value => value.RuleConceptId == firstAccepted.Concept.Id));
            Assert.True(await db.SourceEntities.AnyAsync(value =>
                value.Id == public2014EntityId && value.SourcePackageId != Guid.Empty));
            Assert.True(await db.SourceEntities.AnyAsync(value =>
                value.Id == public2024EntityId && value.SourcePackageId != Guid.Empty));
        }
        finally
        {
            await CleanupAsync(factory, packageKeys);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(IToolHostAuthenticationClient authenticationClient) =>
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
        string siteMode,
        IReadOnlyList<string> globalRoles) =>
        new(1, "rules-core", siteMode, new ToolHostUserContext(userId, userId), globalRoles, []);

    private static Import5eToolsDocumentRequest SourceRequest(
        string packageKey,
        string editionKey,
        string entityName,
        string sourceCode,
        bool isPublic) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: $"Normalization package {editionKey}",
            Provider: "integration-test",
            License: "test-only",
            IsPublic: isPublic,
            WorkKey: $"normalization-work-{editionKey}",
            WorkDisplayName: $"Normalization work {editionKey}",
            EditionKey: editionKey,
            EditionDisplayName: $"Normalization edition {editionKey}",
            Json: $$"""
                {
                  "skill": [
                    { "name": "{{entityName}}", "source": "{{sourceCode}}", "ability": "int" }
                  ]
                }
                """);

    private static async Task CleanupAsync(
        WebApplicationFactory<Program> factory,
        IReadOnlyCollection<string> packageKeys)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        var packageIds = await db.SourcePackages
            .Where(value => packageKeys.Contains(value.Key))
            .Select(value => value.Id)
            .ToArrayAsync();
        var sourceEntityIds = await db.SourceEntities
            .Where(value => packageIds.Contains(value.SourcePackageId))
            .Select(value => value.Id)
            .ToArrayAsync();
        var conceptIds = await db.RuleConceptSourceBindings
            .Where(value => sourceEntityIds.Contains(value.SourceEntityId))
            .Select(value => value.RuleConceptId)
            .Distinct()
            .ToArrayAsync();

        if (sourceEntityIds.Length > 0)
        {
            await db.RuleConceptSourceBindings
                .Where(value => sourceEntityIds.Contains(value.SourceEntityId))
                .ExecuteDeleteAsync();
        }
        if (conceptIds.Length > 0)
        {
            await db.GlobalRuleDecisions.Where(value => conceptIds.Contains(value.RuleConceptId)).ExecuteDeleteAsync();
            await db.RuleConcepts.Where(value => conceptIds.Contains(value.Id)).ExecuteDeleteAsync();
        }
        if (packageIds.Length > 0)
        {
            var packages = await db.SourcePackages.Where(value => packageIds.Contains(value.Id)).ToArrayAsync();
            db.SourcePackages.RemoveRange(packages);
            await db.SaveChangesAsync();
        }
    }

    private sealed class FakeToolHostAuthenticationClient(
        IReadOnlyDictionary<string, ToolHostAuthenticationContext> contexts) : IToolHostAuthenticationClient
    {
        public Task<ToolHostAuthenticationContext?> RedeemAsync(
            string ticket,
            string introspectionPath,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(introspectionPath, IntrospectionPath, StringComparison.Ordinal))
                return Task.FromResult<ToolHostAuthenticationContext?>(null);
            contexts.TryGetValue(ticket, out var context);
            return Task.FromResult(context);
        }
    }
}
