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
    public async Task RulesLawyerCanReviewAndAcceptAccessibleNormalizationSuggestions()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["rules-lawyer-ticket"] = Context("normalizer", "dorks-and-dice", ["Rules Lawyer"]),
            ["ungranted-rules-lawyer-ticket"] = Context("ungranted-normalizer", "dorks-and-dice", ["Rules Lawyer"]),
            ["plain-ticket"] = Context("normalizer", "dorks-and-dice", []),
            ["wrong-mode-ticket"] = Context("normalizer", "professional", ["Rules Lawyer"])
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var public2014Key = $"normalization-2014-{Guid.NewGuid():N}";
        var public2024Key = $"normalization-2024-{Guid.NewGuid():N}";
        var restrictedKey = $"normalization-private-{Guid.NewGuid():N}";
        var conflictKey = $"normalization-conflict-{Guid.NewGuid():N}";
        var conflictToken = Guid.NewGuid().ToString("N")[..8];
        var conflictName = $"Conflicted {conflictToken}";
        var conflictConceptKey = $"skill.conflicted-{conflictToken}";
        var packageKeys = new[] { public2014Key, public2024Key, restrictedKey, conflictKey };

        Guid public2014EntityId = Guid.Empty;
        Guid public2024EntityId = Guid.Empty;
        Guid restrictedEntityId = Guid.Empty;
        Guid conflictEntityId = Guid.Empty;

        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();

                var public2014 = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(public2014Key, "2014", "Arcana", "PHB14", isPublic: true));
                public2014EntityId = public2014.Entities.Single().EntityId;

                var public2024 = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(public2024Key, "2024", "Arcana", "PHB24", isPublic: true));
                public2024EntityId = public2024.Entities.Single().EntityId;

                var restricted = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(restrictedKey, "private", "Restricted Lore", "PRIVATE", isPublic: false));
                restrictedEntityId = restricted.Entities.Single().EntityId;
                await grants.GrantAsync("normalizer", restricted.PackageId);

                var conflict = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(conflictKey, "conflict", conflictName, "CONFLICT", isPublic: true));
                conflictEntityId = conflict.Entities.Single().EntityId;

                await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(
                        conflictConceptKey,
                        "spell",
                        "Deliberate key collision"),
                    "seed-rules-lawyer");
            }

            using (var anonymous = await client.GetAsync(
                       "/api/global/rules/normalization/candidates?q=Arcana"))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            }

            using (var plainRequest = HostedRequest(
                       HttpMethod.Get,
                       "/api/global/rules/normalization/candidates?q=Arcana",
                       "plain-ticket"))
            using (var plainResponse = await client.SendAsync(plainRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, plainResponse.StatusCode);
            }

            using (var wrongModeRequest = HostedRequest(
                       HttpMethod.Get,
                       "/api/global/rules/normalization/candidates?q=Arcana",
                       "wrong-mode-ticket"))
            using (var wrongModeResponse = await client.SendAsync(wrongModeRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, wrongModeResponse.StatusCode);
            }

            using (var ungrantedRequest = HostedRequest(
                       HttpMethod.Get,
                       "/api/global/rules/normalization/candidates?q=Restricted",
                       "ungranted-rules-lawyer-ticket"))
            using (var ungrantedResponse = await client.SendAsync(ungrantedRequest))
            {
                Assert.Equal(HttpStatusCode.OK, ungrantedResponse.StatusCode);
                var candidates = (await ungrantedResponse.Content
                    .ReadFromJsonAsync<IReadOnlyList<SourceNormalizationCandidateView>>())!;
                Assert.Empty(candidates);
            }

            using (var ungrantedAccept = HostedRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/normalization/entities/{restrictedEntityId}/accept",
                       "ungranted-rules-lawyer-ticket"))
            using (var ungrantedAcceptResponse = await client.SendAsync(ungrantedAccept))
            {
                Assert.Equal(HttpStatusCode.NotFound, ungrantedAcceptResponse.StatusCode);
            }

            IReadOnlyList<SourceNormalizationCandidateView> arcanaCandidates;
            using (var candidatesRequest = HostedRequest(
                       HttpMethod.Get,
                       "/api/global/rules/normalization/candidates?entityType=skill&q=Arcana",
                       "rules-lawyer-ticket"))
            using (var candidatesResponse = await client.SendAsync(candidatesRequest))
            {
                Assert.Equal(HttpStatusCode.OK, candidatesResponse.StatusCode);
                Assert.Equal("no-store", candidatesResponse.Headers.CacheControl?.ToString());
                arcanaCandidates = (await candidatesResponse.Content
                    .ReadFromJsonAsync<IReadOnlyList<SourceNormalizationCandidateView>>())!;
            }

            Assert.Equal(2, arcanaCandidates.Count);
            Assert.All(arcanaCandidates, candidate =>
            {
                Assert.Equal("skill.arcana", candidate.SuggestedConceptKey);
                Assert.Equal(SourceNormalizationSuggestionKinds.NewConcept, candidate.SuggestionKind);
                Assert.Null(candidate.SuggestedConceptId);
            });

            AcceptedSourceNormalizationView firstAccepted;
            using (var acceptRequest = HostedRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/normalization/entities/{public2014EntityId}/accept",
                       "rules-lawyer-ticket"))
            using (var acceptResponse = await client.SendAsync(acceptRequest))
            {
                Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
                firstAccepted = (await acceptResponse.Content
                    .ReadFromJsonAsync<AcceptedSourceNormalizationView>())!;
                Assert.True(firstAccepted.CreatedConcept);
                Assert.True(firstAccepted.CreatedBinding);
                Assert.Equal("skill.arcana", firstAccepted.Concept.Key);
                Assert.Equal("skill", firstAccepted.Concept.EntityType);
                Assert.Equal("Arcana", firstAccepted.Concept.DisplayName);
            }

            using (var repeatRequest = HostedRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/normalization/entities/{public2014EntityId}/accept",
                       "rules-lawyer-ticket"))
            using (var repeatResponse = await client.SendAsync(repeatRequest))
            {
                Assert.Equal(HttpStatusCode.OK, repeatResponse.StatusCode);
                var repeated = (await repeatResponse.Content
                    .ReadFromJsonAsync<AcceptedSourceNormalizationView>())!;
                Assert.False(repeated.CreatedConcept);
                Assert.False(repeated.CreatedBinding);
                Assert.Equal(firstAccepted.Concept.Id, repeated.Concept.Id);
                Assert.Equal(firstAccepted.Binding.Id, repeated.Binding.Id);
            }

            SourceNormalizationCandidateView secondCandidate;
            using (var secondCandidateRequest = HostedRequest(
                       HttpMethod.Get,
                       "/api/global/rules/normalization/candidates?q=Arcana",
                       "rules-lawyer-ticket"))
            using (var secondCandidateResponse = await client.SendAsync(secondCandidateRequest))
            {
                Assert.Equal(HttpStatusCode.OK, secondCandidateResponse.StatusCode);
                var candidates = (await secondCandidateResponse.Content
                    .ReadFromJsonAsync<IReadOnlyList<SourceNormalizationCandidateView>>())!;
                secondCandidate = Assert.Single(candidates);
            }

            Assert.Equal(public2024EntityId, secondCandidate.SourceEntityId);
            Assert.Equal(SourceNormalizationSuggestionKinds.ExistingConcept, secondCandidate.SuggestionKind);
            Assert.Equal(firstAccepted.Concept.Id, secondCandidate.SuggestedConceptId);

            using (var acceptSecondRequest = HostedRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/normalization/entities/{public2024EntityId}/accept",
                       "rules-lawyer-ticket"))
            using (var acceptSecondResponse = await client.SendAsync(acceptSecondRequest))
            {
                Assert.Equal(HttpStatusCode.OK, acceptSecondResponse.StatusCode);
                var accepted = (await acceptSecondResponse.Content
                    .ReadFromJsonAsync<AcceptedSourceNormalizationView>())!;
                Assert.False(accepted.CreatedConcept);
                Assert.True(accepted.CreatedBinding);
                Assert.Equal(firstAccepted.Concept.Id, accepted.Concept.Id);
            }

            using (var restrictedCandidateRequest = HostedRequest(
                       HttpMethod.Get,
                       "/api/global/rules/normalization/candidates?q=Restricted",
                       "rules-lawyer-ticket"))
            using (var restrictedCandidateResponse = await client.SendAsync(restrictedCandidateRequest))
            {
                Assert.Equal(HttpStatusCode.OK, restrictedCandidateResponse.StatusCode);
                var candidates = (await restrictedCandidateResponse.Content
                    .ReadFromJsonAsync<IReadOnlyList<SourceNormalizationCandidateView>>())!;
                var restrictedCandidate = Assert.Single(candidates);
                Assert.Equal(restrictedEntityId, restrictedCandidate.SourceEntityId);
                Assert.Equal("skill.restricted-lore", restrictedCandidate.SuggestedConceptKey);
            }

            using (var conflictRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/global/rules/normalization/candidates?q={conflictToken}",
                       "rules-lawyer-ticket"))
            using (var conflictResponse = await client.SendAsync(conflictRequest))
            {
                Assert.Equal(HttpStatusCode.OK, conflictResponse.StatusCode);
                var candidates = (await conflictResponse.Content
                    .ReadFromJsonAsync<IReadOnlyList<SourceNormalizationCandidateView>>())!;
                var conflictCandidate = Assert.Single(candidates);
                Assert.Equal(conflictEntityId, conflictCandidate.SourceEntityId);
                Assert.Equal(SourceNormalizationSuggestionKinds.Conflict, conflictCandidate.SuggestionKind);
                Assert.Equal(conflictConceptKey, conflictCandidate.SuggestedConceptKey);
            }

            using (var conflictAccept = HostedRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/normalization/entities/{conflictEntityId}/accept",
                       "rules-lawyer-ticket"))
            using (var conflictAcceptResponse = await client.SendAsync(conflictAccept))
            {
                Assert.Equal(HttpStatusCode.Conflict, conflictAcceptResponse.StatusCode);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                Assert.Equal(1, await db.RuleConcepts.CountAsync(value => value.Key == "skill.arcana"));
                Assert.Equal(2, await db.RuleConceptSourceBindings.CountAsync(
                    value => value.RuleConceptId == firstAccepted.Concept.Id));
                Assert.Equal(0, await db.GlobalRuleDecisions.CountAsync(
                    value => value.RuleConceptId == firstAccepted.Concept.Id));
                Assert.Equal(0, await db.RulesetRevisionEntries.CountAsync(
                    value => value.RuleConceptId == firstAccepted.Concept.Id));
            }
        }
        finally
        {
            await CleanupAsync(factory, packageKeys, conflictConceptKey);
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
                    {
                      "name": "{{entityName}}",
                      "source": "{{sourceCode}}",
                      "ability": "int"
                    }
                  ]
                }
                """);

    private static async Task CleanupAsync(
        WebApplicationFactory<Program> factory,
        IReadOnlyCollection<string> packageKeys,
        string conflictConceptKey)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

        var packageIds = await db.SourcePackages
            .Where(value => packageKeys.Contains(value.Key))
            .Select(value => value.Id)
            .ToArrayAsync();
        var sourceEntityIds = await db.SourceEntities
            .Where(value => packageIds.Contains(value.SourceEdition.SourceWork.SourcePackageId))
            .Select(value => value.Id)
            .ToArrayAsync();
        var conceptIds = await db.RuleConceptSourceBindings
            .Where(value => sourceEntityIds.Contains(value.SourceEntityId))
            .Select(value => value.RuleConceptId)
            .Distinct()
            .ToArrayAsync();
        var seededConflictId = await db.RuleConcepts
            .Where(value => value.Key == conflictConceptKey)
            .Select(value => (Guid?)value.Id)
            .SingleOrDefaultAsync();

        if (sourceEntityIds.Length > 0)
        {
            await db.RuleConceptSourceBindings
                .Where(value => sourceEntityIds.Contains(value.SourceEntityId))
                .ExecuteDeleteAsync();
        }

        var removableConceptIds = conceptIds
            .Concat(seededConflictId is null ? [] : [seededConflictId.Value])
            .Distinct()
            .ToArray();
        if (removableConceptIds.Length > 0)
        {
            await db.RuleConcepts
                .Where(value => removableConceptIds.Contains(value.Id))
                .ExecuteDeleteAsync();
        }

        if (packageIds.Length > 0)
        {
            var packages = await db.SourcePackages
                .Where(value => packageIds.Contains(value.Id))
                .ToArrayAsync();
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
