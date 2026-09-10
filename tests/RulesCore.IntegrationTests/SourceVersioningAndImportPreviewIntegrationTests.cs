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
public sealed class SourceVersioningAndImportPreviewIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task PreviewVersionDetectionLineageAndConsolidationRemainDeliberateAndRevisionSafe()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["dev-ticket"] = Context("source-dev", ["Dev"]),
            ["rules-ticket"] = Context("rules-lawyer", ["Rules Lawyer"]),
            ["plain-ticket"] = Context("plain-user", [])
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var token = Guid.NewGuid().ToString("N")[..10];
        var uaPackageKey = $"preimport-ua-{token}";
        var publishedPackageKey = $"preimport-published-{token}";
        var privatePackageKey = $"preimport-private-{token}";
        var conceptKey = $"feat.arcane-burst-{token}";
        var packageKeys = new[] { uaPackageKey, publishedPackageKey, privatePackageKey };
        var entityIds = new List<Guid>();
        Guid conceptId = Guid.Empty;

        var uaRequest = SourceRequest(
            uaPackageKey,
            "UA Test Source",
            "ua-article",
            "UA experimental article",
            "2017-04",
            "April 2017 release",
            "5e-2014",
            "playtest",
            "Mystic Bolt",
            "UA-X",
            "A creature takes arcane force damage and is pushed.",
            reprintedAs: "Arcane Burst|PUB");

        var publishedRequest = SourceRequest(
            publishedPackageKey,
            "Published Test Source",
            "published-book",
            "Published test book",
            "first-printing",
            "First printing",
            "5e-2024",
            "published",
            "Arcane Burst",
            "PUB",
            "A creature takes arcane force damage and can be pushed.");

        var privateRequest = SourceRequest(
            privatePackageKey,
            "Restricted Test Source",
            "restricted-book",
            "Restricted test book",
            "release-1",
            "Release 1",
            "5.5e",
            "published",
            "Mystic Bolt",
            "PRIVATE",
            "A hidden implementation that must not participate in accessible detection.",
            isPublic: false);

        try
        {
            SourceImportPreviewResult uaPreview;
            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/source-admin/import/preview",
                       "dev-ticket",
                       uaRequest))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                uaPreview = (await response.Content.ReadFromJsonAsync<SourceImportPreviewResult>())!;
            }

            Assert.True(uaPreview.CanImport);
            Assert.Equal("5e", uaPreview.GameEdition);
            Assert.Equal("playtest", uaPreview.ReleaseKind);
            Assert.Equal(1, uaPreview.NewEntityCount);
            Assert.Equal(SourceImportPreviewActions.NewEntity, Assert.Single(uaPreview.Entities).Action);

            using (var plainPreview = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/source-admin/import/preview",
                       "plain-ticket",
                       uaRequest))
            using (var response = await client.SendAsync(plainPreview))
            {
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            SourceImportResult uaImport;
            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/source-admin/import",
                       "dev-ticket",
                       uaRequest))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                uaImport = (await response.Content.ReadFromJsonAsync<SourceImportResult>())!;
            }
            var uaEntity = Assert.Single(uaImport.Entities);
            entityIds.Add(uaEntity.EntityId);
            Assert.Equal("5e", uaImport.GameEdition);

            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/source-admin/import/preview",
                       "dev-ticket",
                       uaRequest))
            using (var response = await client.SendAsync(request))
            {
                var preview = (await response.Content.ReadFromJsonAsync<SourceImportPreviewResult>())!;
                Assert.Equal(SourceImportPreviewActions.Unchanged, Assert.Single(preview.Entities).Action);
            }

            SourceImportResult publishedImport;
            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/source-admin/import",
                       "dev-ticket",
                       publishedRequest))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                publishedImport = (await response.Content.ReadFromJsonAsync<SourceImportResult>())!;
            }
            var publishedEntity = Assert.Single(publishedImport.Entities);
            entityIds.Add(publishedEntity.EntityId);
            Assert.Equal("5.5e", publishedImport.GameEdition);

            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/source-admin/import",
                       "dev-ticket",
                       privateRequest))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var imported = (await response.Content.ReadFromJsonAsync<SourceImportResult>())!;
                entityIds.Add(Assert.Single(imported.Entities).EntityId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var rules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var concept = await rules.CreateConceptAsync(
                    new CreateRuleConceptRequest(conceptKey, "feat", "Arcane Burst"),
                    "seed-rules-lawyer");
                conceptId = concept.Value.Id;
                await rules.BindSourceEntityAsync(
                    conceptId,
                    new BindRuleConceptSourceRequest(publishedEntity.EntityId),
                    "seed-rules-lawyer");
            }

            SourceVersionDetectionView detection;
            using (var request = HostedRequest(
                       HttpMethod.Get,
                       $"/api/global/rules/versioning/entities/{uaEntity.EntityId}/candidates",
                       "rules-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                detection = (await response.Content.ReadFromJsonAsync<SourceVersionDetectionView>())!;
            }

            var publishedCandidate = Assert.Single(
                detection.Candidates.Where(value => value.Candidate.SourceEntityId == publishedEntity.EntityId));
            Assert.True(publishedCandidate.Confidence >= 65);
            Assert.Contains(publishedCandidate.Reasons, value => value.Contains("Explicit source metadata", StringComparison.Ordinal));
            Assert.DoesNotContain(
                detection.Candidates,
                value => value.Candidate.SourceCode == "PRIVATE");

            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/versioning/entities/{uaEntity.EntityId}/bind",
                       "rules-ticket",
                       new BindDetectedSourceVersionRequest(conceptId)))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var binding = (await response.Content.ReadFromJsonAsync<RuleMutationResult<RuleConceptSourceBindingView>>())!;
                Assert.True(binding.Created);
            }

            SourceLineageMutationView lineage;
            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/global/rules/versioning/lineage",
                       "rules-ticket",
                       new CreateSourceLineageRequest(
                           uaEntity.EntityId,
                           publishedEntity.EntityId,
                           SourceLineageKinds.PlaytestOf,
                           "Experimental implementation developed toward the published rule.")))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                lineage = (await response.Content.ReadFromJsonAsync<SourceLineageMutationView>())!;
                Assert.True(lineage.Changed);
            }

            using (var repeat = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/global/rules/versioning/lineage",
                       "rules-ticket",
                       new CreateSourceLineageRequest(
                           uaEntity.EntityId,
                           publishedEntity.EntityId,
                           SourceLineageKinds.PlaytestOf,
                           "Experimental implementation developed toward the published rule.")))
            using (var response = await client.SendAsync(repeat))
            {
                var repeated = (await response.Content.ReadFromJsonAsync<SourceLineageMutationView>())!;
                Assert.False(repeated.Changed);
                Assert.Equal(lineage.Lineage.Id, repeated.Lineage.Id);
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
            Assert.Contains(consolidation.Sources, value => value.GameEdition == "5e" && value.ReleaseKind == "playtest");
            Assert.Contains(consolidation.Sources, value => value.GameEdition == "5.5e" && value.ReleaseKind == "published");

            var publishedRevisionId = consolidation.Sources
                .Single(value => value.SourceEntityId == publishedEntity.EntityId)
                .Revisions.Single().Id;
            var uaRevisionId = consolidation.Sources
                .Single(value => value.SourceEntityId == uaEntity.EntityId)
                .Revisions.Single().Id;

            var decision = new SetGlobalRuleDecisionRequest(
                publishedRevisionId,
                "Consolidated after reviewing the earlier playtest.",
                Contributions:
                [
                    new RuleConsolidationContributionRequest(
                        uaRevisionId,
                        RuleConsolidationContributionKinds.Incorporated,
                        "Reviewed and incorporated the earlier push behavior.")
                ]);

            RuleMutationResult<GlobalRuleDecisionView> firstDecision;
            using (var request = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/global/rules/concepts/{conceptId}/decision",
                       "rules-ticket",
                       decision))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                firstDecision = (await response.Content.ReadFromJsonAsync<RuleMutationResult<GlobalRuleDecisionView>>())!;
                Assert.True(firstDecision.Created);
            }

            using (var request = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/global/rules/concepts/{conceptId}/decision",
                       "rules-ticket",
                       decision))
            using (var response = await client.SendAsync(request))
            {
                var repeated = (await response.Content.ReadFromJsonAsync<RuleMutationResult<GlobalRuleDecisionView>>())!;
                Assert.False(repeated.Created);
                Assert.Equal(firstDecision.Value.Id, repeated.Value.Id);
            }

            var changedProvenance = decision with
            {
                Contributions =
                [
                    new RuleConsolidationContributionRequest(
                        uaRevisionId,
                        RuleConsolidationContributionKinds.Incorporated,
                        "Reviewed the earlier push behavior and deliberately retained its intent.")
                ]
            };
            using (var request = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/global/rules/concepts/{conceptId}/decision",
                       "rules-ticket",
                       changedProvenance))
            using (var response = await client.SendAsync(request))
            {
                var changed = (await response.Content.ReadFromJsonAsync<RuleMutationResult<GlobalRuleDecisionView>>())!;
                Assert.True(changed.Created);
                Assert.Equal(firstDecision.Value.DecisionNumber + 1, changed.Value.DecisionNumber);
            }

            using (var request = HostedRequest(
                       HttpMethod.Get,
                       $"/api/global/rules/concepts/{conceptId}/consolidation",
                       "rules-ticket"))
            using (var response = await client.SendAsync(request))
            {
                var updated = (await response.Content.ReadFromJsonAsync<RuleConsolidationView>())!;
                var contribution = Assert.Single(updated.LatestContributions);
                Assert.Equal(uaRevisionId, contribution.SourceEntityRevisionId);
                Assert.Contains("deliberately retained", contribution.Note);
            }

            var changedUa = uaRequest with
            {
                Json = SourceJson(
                    "Mystic Bolt",
                    "UA-X",
                    "A creature takes arcane force damage, is pushed, and briefly glows.",
                    "Arcane Burst|PUB")
            };
            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/source-admin/import/preview",
                       "dev-ticket",
                       changedUa))
            using (var response = await client.SendAsync(request))
            {
                var preview = (await response.Content.ReadFromJsonAsync<SourceImportPreviewResult>())!;
                var entity = Assert.Single(preview.Entities);
                Assert.Equal(uaEntity.EntityId, entity.EntityId);
                Assert.Equal(SourceImportPreviewActions.NewRevision, entity.Action);
            }

            var conflictingMetadata = uaRequest with { PackageDisplayName = "Different immutable package name" };
            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/source-admin/import/preview",
                       "dev-ticket",
                       conflictingMetadata))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var preview = (await response.Content.ReadFromJsonAsync<SourceImportPreviewResult>())!;
                Assert.False(preview.CanImport);
                Assert.NotEmpty(preview.Conflicts);
            }
        }
        finally
        {
            await CleanupAsync(factory, packageKeys, conceptKey, entityIds);
        }
    }

    private static Import5eToolsDocumentRequest SourceRequest(
        string packageKey,
        string packageName,
        string workKey,
        string workName,
        string releaseKey,
        string releaseName,
        string gameEdition,
        string releaseKind,
        string entityName,
        string sourceCode,
        string text,
        string? reprintedAs = null,
        bool isPublic = true) =>
        new(
            packageKey,
            packageName,
            "integration-test",
            "test-only",
            isPublic,
            workKey,
            workName,
            releaseKey,
            releaseName,
            SourceJson(entityName, sourceCode, text, reprintedAs),
            gameEdition,
            releaseKind,
            new DateOnly(2024, 1, 1));

    private static string SourceJson(
        string entityName,
        string sourceCode,
        string text,
        string? reprintedAs = null)
    {
        var value = new Dictionary<string, object?>
        {
            ["name"] = entityName,
            ["source"] = sourceCode,
            ["entries"] = new[] { text }
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
        IReadOnlyList<string> globalRoles) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: "dorks-and-dice",
            User: new ToolHostUserContext(userId, userId),
            GlobalRoles: globalRoles,
            Campaigns: []);

    private static async Task CleanupAsync(
        WebApplicationFactory<Program> factory,
        IReadOnlyCollection<string> packageKeys,
        string conceptKey,
        IReadOnlyCollection<Guid> sourceEntityIds)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

        foreach (var entityId in sourceEntityIds.Distinct())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM source_entity_lineage WHERE from_source_entity_id = {entityId} OR to_source_entity_id = {entityId}");
        }

        var concept = await db.RuleConcepts.SingleOrDefaultAsync(value => value.Key == conceptKey);
        if (concept is not null)
        {
            db.RuleConcepts.Remove(concept);
            await db.SaveChangesAsync();
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
