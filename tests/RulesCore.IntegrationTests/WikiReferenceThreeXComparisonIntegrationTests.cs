using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class WikiReferenceThreeXComparisonIntegrationTests
{
    [Fact]
    public async Task ThreeEAndThreeFiveERevisionHistoryRemainsDistinctAndComparableThroughWikiContract()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageIds = new List<Guid>();
        var canonicalIds = new HashSet<Guid>();

        try
        {
            Guid threeRevisionId;
            Guid threeFiveRevisionId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

                var three = await ImportFeatAsync(
                    importer,
                    packageIds,
                    $"wiki-three-{token}",
                    $"Legacy Training {token}",
                    "THREE",
                    "3e",
                    new DateOnly(2000, 8, 1),
                    "Dexterity 13");
                var threeFive = await ImportFeatAsync(
                    importer,
                    packageIds,
                    $"wiki-threefive-{token}",
                    $"Legacy Training {token}",
                    "THREEFIVE",
                    "3.5e",
                    new DateOnly(2003, 7, 1),
                    "Dexterity 15");

                var threeCanonical = await GetCanonicalEntityIdAsync(db, three.EntityId);
                var threeFiveCanonical = await GetCanonicalEntityIdAsync(db, threeFive.EntityId);
                canonicalIds.UnionWith([threeCanonical, threeFiveCanonical]);
                if (threeCanonical != threeFiveCanonical)
                {
                    await RelateAsync(db, threeCanonical, threeFiveCanonical, "revision");
                }

                threeRevisionId = await GetRevisionIdAsync(db, three.EntityId);
                threeFiveRevisionId = await GetRevisionIdAsync(db, threeFive.EntityId);
            }

            WikiReferenceItemView reference;
            using (var catalogResponse = await client.GetAsync(
                       $"/api/wiki/references?entityType=feat&categoryMode=any&q={Uri.EscapeDataString($"Legacy Training {token}")}"))
            {
                Assert.Equal(HttpStatusCode.OK, catalogResponse.StatusCode);
                var catalog = (await catalogResponse.Content.ReadFromJsonAsync<WikiReferenceCatalogView>())!;
                reference = Assert.Single(catalog.References);
                Assert.Contains(catalog.EditionFacets, value => value.Value == "3e");
                Assert.Contains(catalog.EditionFacets, value => value.Value == "3.5e");
                Assert.Equal("3.5e", reference.EffectiveEditionKey);
            }

            using (var detailResponse = await client.GetAsync(
                       $"/api/wiki/references/{Uri.EscapeDataString(reference.ReferenceIdentity)}"))
            {
                Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
                var detail = (await detailResponse.Content.ReadFromJsonAsync<WikiReferenceDetailView>())!;
                Assert.Equal(2, detail.Variations.Count);
                Assert.Contains(detail.Variations, value =>
                    value.EditionKey == "3e" && value.SourceEntityRevisionId == threeRevisionId);
                Assert.Contains(detail.Variations, value =>
                    value.EditionKey == "3.5e" && value.SourceEntityRevisionId == threeFiveRevisionId);
            }

            using (var comparisonResponse = await client.PostAsJsonAsync(
                       "/api/wiki/references/comparison",
                       new WikiReferenceComparisonRequest(
                           reference.ReferenceIdentity,
                           threeRevisionId,
                           threeFiveRevisionId)))
            {
                Assert.Equal(HttpStatusCode.OK, comparisonResponse.StatusCode);
                var comparison = await comparisonResponse.Content.ReadFromJsonAsync<RuleSemanticComparisonView>();
                Assert.NotNull(comparison);
                Assert.Equal(threeRevisionId, comparison.LeftSourceEntityRevisionId);
                Assert.Equal(threeFiveRevisionId, comparison.RightSourceEntityRevisionId);
            }
        }
        finally
        {
            await CleanupAsync(factory, packageIds, canonicalIds);
        }
    }

    private static async Task<(Guid PackageId, Guid EntityId)> ImportFeatAsync(
        ISourceImportService importer,
        ICollection<Guid> packageIds,
        string packageKey,
        string name,
        string sourceCode,
        string edition,
        DateOnly publicationDate,
        string prerequisite)
    {
        var request = new Import5eToolsDocumentRequest(
            PackageKey: packageKey,
            PackageDisplayName: $"Package {packageKey}",
            Provider: "integration-test",
            License: "test-only",
            IsPublic: true,
            WorkKey: $"work-{packageKey}",
            WorkDisplayName: $"Work {packageKey}",
            EditionKey: edition,
            EditionDisplayName: edition,
            Json: $$"""
                {
                  "feat": [
                    {
                      "name": "{{name}}",
                      "source": "{{sourceCode}}",
                      "prerequisite": "{{prerequisite}}",
                      "entries": ["{{prerequisite}}"]
                    }
                  ]
                }
                """,
            GameEdition: edition,
            ReleaseKind: "published",
            PublicationDate: publicationDate);
        var imported = await importer.Import5eToolsDocumentAsync(request);
        packageIds.Add(imported.PackageId);
        var entity = Assert.Single(imported.Entities);
        return (imported.PackageId, entity.EntityId);
    }

    private static async Task<Guid> GetRevisionIdAsync(RulesCoreDbContext db, Guid sourceEntityId) =>
        await db.SourceEntityRevisions
            .Where(value => value.SourceEntityId == sourceEntityId)
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => value.Id)
            .FirstAsync();

    private static async Task<Guid> GetCanonicalEntityIdAsync(RulesCoreDbContext db, Guid sourceEntityId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT occurrence.canonical_entity_id
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                  ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                WHERE binding.source_entity_id = @source_entity_id
                  AND occurrence.canonical_entity_id IS NOT NULL
                ORDER BY binding.source_entity_revision_id
                LIMIT 1;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            var value = await command.ExecuteScalarAsync();
            return value is Guid id
                ? id
                : throw new InvalidOperationException("Canonical entity was not created for 3.x comparison fixture.");
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task RelateAsync(
        RulesCoreDbContext db,
        Guid fromId,
        Guid toId,
        string kind)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO canonical_entity_relationship (
                    canonical_entity_relationship_id,
                    from_canonical_entity_id,
                    to_canonical_entity_id,
                    relationship_kind,
                    evidence_kind,
                    confidence,
                    created_at)
                VALUES (@id, @from_id, @to_id, @kind, 'integration-test', 1.0, @created_at)
                ON CONFLICT (from_canonical_entity_id, to_canonical_entity_id, relationship_kind)
                DO NOTHING;
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@from_id", fromId);
            AddParameter(command, "@to_id", toId);
            AddParameter(command, "@kind", kind);
            AddParameter(command, "@created_at", DateTimeOffset.UtcNow);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task CleanupAsync(
        WebApplicationFactory<Program> factory,
        IReadOnlyCollection<Guid> packageIds,
        IReadOnlyCollection<Guid> canonicalIds)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

        if (canonicalIds.Count > 0)
        {
            var connection = db.Database.GetDbConnection();
            var openedHere = connection.State != ConnectionState.Open;
            if (openedHere) await connection.OpenAsync();
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    DELETE FROM canonical_entity_relationship
                    WHERE from_canonical_entity_id = ANY(@canonical_ids)
                       OR to_canonical_entity_id = ANY(@canonical_ids);
                    """;
                AddParameter(command, "@canonical_ids", canonicalIds.ToArray());
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                if (openedHere) await connection.CloseAsync();
            }
        }

        if (packageIds.Count > 0)
        {
            var packages = await db.SourcePackages
                .Where(value => packageIds.Contains(value.Id))
                .ToArrayAsync();
            db.SourcePackages.RemoveRange(packages);
            await db.SaveChangesAsync();
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
