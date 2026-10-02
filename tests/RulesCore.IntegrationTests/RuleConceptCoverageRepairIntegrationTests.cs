using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class RuleConceptCoverageRepairIntegrationTests
{
    [Fact]
    public async Task RepairBackfillsConceptForCanonicalHistoryAndResolvedCatalogUsesNewestVariation()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..10];
            var name = $"Coverage Goblin {token}";
            var packageIds = new List<Guid>();
            var canonicalIds = new HashSet<Guid>();
            var conceptIdsBefore = (await db.RuleConcepts
                    .AsNoTracking()
                    .Select(value => value.Id)
                    .ToArrayAsync())
                .ToHashSet();

            try
            {
                var importer = new ReconciledNormalizedSourceImportService(
                    new NormalizedSourceImportService(db),
                    db);
                var old = await importer.ImportAsync(Request(
                    $"coverage-old-{token}",
                    name,
                    "COV3",
                    "3e",
                    new DateOnly(2000, 1, 1),
                    5));
                var current = await importer.ImportAsync(Request(
                    $"coverage-current-{token}",
                    name,
                    "COV5",
                    "5e",
                    new DateOnly(2014, 8, 19),
                    9));
                packageIds.AddRange([old.PackageId, current.PackageId]);

                var oldEntity = Assert.Single(old.Entities);
                var currentEntity = Assert.Single(current.Entities);
                var oldCanonical = await CanonicalEntityIdAsync(db, oldEntity.EntityId);
                var currentCanonical = await CanonicalEntityIdAsync(db, currentEntity.EntityId);
                canonicalIds.UnionWith([oldCanonical, currentCanonical]);

                var before = await new WikiReferenceCatalogService(db).GetGlobalCatalogAsync(
                    userId: null,
                    entityType: "monster",
                    categoryMode: WikiReferenceCategoryModes.AnyVariation,
                    query: name,
                    sourceCode: null,
                    packageKey: null,
                    edition: null,
                    limit: 20,
                    offset: 0);
                var sourceOnly = Assert.Single(before.References);
                Assert.Null(sourceOnly.RuleConceptId);
                Assert.Null(sourceOnly.ConceptKey);
                Assert.Equal("COV5", sourceOnly.EffectiveVariation.SourceCode);

                var repair = new RuleConceptCoverageRepairService(db);
                var result = await repair.RepairAsync("coverage-owner");
                Assert.True(result.BindingsCreated > 0);

                var after = await new WikiReferenceCatalogService(db).GetGlobalCatalogAsync(
                    userId: null,
                    entityType: "monster",
                    categoryMode: WikiReferenceCategoryModes.AnyVariation,
                    query: name,
                    sourceCode: null,
                    packageKey: null,
                    edition: null,
                    limit: 20,
                    offset: 0);
                var conceptBacked = Assert.Single(after.References);
                Assert.NotNull(conceptBacked.RuleConceptId);
                Assert.False(string.IsNullOrWhiteSpace(conceptBacked.ConceptKey));

                var catalog = await new ResolvedRulesCatalogService(db).GetGlobalAsync(
                    userId: null,
                    entityType: "monster",
                    query: name,
                    limit: 20);
                var resolved = Assert.Single(catalog.Rules);
                Assert.Equal(conceptBacked.ConceptKey, resolved.ConceptKey);
                Assert.Equal("COV5", resolved.SourceCode);
                Assert.Equal(currentEntity.EntityId, resolved.SourceEntityId);
                Assert.Equal(RuleResolutionStates.UnresolvedFallback, resolved.EffectiveDecisionKind);

                var second = await repair.RepairAsync("coverage-owner");
                Assert.Equal(0, second.BindingsCreated);
            }
            finally
            {
                await CleanupAsync(db, conceptIdsBefore, packageIds, canonicalIds);
            }
        }
    }

    private static ImportNormalizedSourceRequest Request(
        string packageKey,
        string name,
        string sourceCode,
        string edition,
        DateOnly publicationDate,
        int hitPoints)
    {
        var raw = JsonSerializer.Serialize(new
        {
            name,
            source = sourceCode,
            hp = hitPoints
        });
        var representation = new NormalizedSourceRepresentation(
            "integration-json",
            new SourceRepresentationArtifact(
                $"{packageKey}.json",
                Encoding.UTF8.GetBytes(raw),
                $"integration:{packageKey}"),
            [new NormalizedSourceRecord(
                "monster",
                name,
                sourceCode,
                $"monster|{sourceCode}|{name}|",
                raw,
                PublicationLocalKey: $"source:{sourceCode}")],
            [new NormalizedSourcePublication(
                $"source:{sourceCode}",
                $"Publication {sourceCode}",
                GameEdition: edition,
                PublicationDate: publicationDate)]);
        return new ImportNormalizedSourceRequest(
            packageKey,
            packageKey,
            "integration-test",
            null,
            true,
            representation);
    }

    private static async Task<Guid> CanonicalEntityIdAsync(RulesCoreDbContext db, Guid sourceEntityId)
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
            return (Guid)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Canonical entity binding was missing."));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<RulesCoreDbContext?> OpenDatabaseAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        return db;
    }

    private static async Task CleanupAsync(
        RulesCoreDbContext db,
        IReadOnlySet<Guid> conceptIdsBefore,
        IReadOnlyCollection<Guid> packageIds,
        IReadOnlyCollection<Guid> canonicalIds)
    {
        db.ChangeTracker.Clear();
        var createdConceptIds = await db.RuleConcepts
            .AsNoTracking()
            .Where(value => !conceptIdsBefore.Contains(value.Id))
            .Select(value => value.Id)
            .ToArrayAsync();

        if (createdConceptIds.Length > 0)
        {
            var connection = db.Database.GetDbConnection();
            var openedHere = connection.State != ConnectionState.Open;
            if (openedHere) await connection.OpenAsync();
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    DELETE FROM rule_concept_relationship
                    WHERE from_rule_concept_id = ANY(@concept_ids)
                       OR to_rule_concept_id = ANY(@concept_ids);
                    """;
                AddParameter(command, "@concept_ids", createdConceptIds);
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                if (openedHere) await connection.CloseAsync();
            }

            await db.RuleConceptSourceBindings
                .Where(value => createdConceptIds.Contains(value.RuleConceptId))
                .ExecuteDeleteAsync();
            await db.RuleConcepts
                .Where(value => createdConceptIds.Contains(value.Id))
                .ExecuteDeleteAsync();
        }

        if (packageIds.Count > 0)
        {
            var packages = await db.SourcePackages
                .Where(value => packageIds.Contains(value.Id))
                .ToArrayAsync();
            db.SourcePackages.RemoveRange(packages);
            await db.SaveChangesAsync();
        }

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
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
