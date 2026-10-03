using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class ClassParentheticalReferenceHistoryIntegrationTests
{
    [Fact]
    public async Task ParentheticalClassNameJoinsBaseHistoryWithoutGeneralizingToOtherCategories()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var classBaseName = $"Ranger {token}";
            var classVariantName = $"{classBaseName} (Revised)";
            var orphanBaseName = $"Mystic {token}";
            var orphanVariantOneName = $"{orphanBaseName} (Alpha)";
            var orphanVariantTwoName = $"{orphanBaseName} (Beta)";
            var itemBaseName = $"Relic {token}";
            var itemVariantName = $"{itemBaseName} (Revised)";
            var basePackageKey = $"parenthetical-base-{token}";
            var variantPackageKey = $"parenthetical-variant-{token}";

            try
            {
                var baseImport = await ImportAsync(
                    db,
                    basePackageKey,
                    token,
                    "base",
                    [
                        Record(RuleConceptEntityTypes.Class, classBaseName, "BASE", "class-base"),
                        Record(RuleConceptEntityTypes.Class, orphanVariantOneName, "BASE", "orphan-one-first"),
                        Record("item", itemBaseName, "BASE", "item-base")
                    ],
                    reconcileHistory: true);
                var baseClass = Assert.Single(baseImport.Entities, value => value.Name == classBaseName);
                var orphanVariantOneFirst = Assert.Single(
                    baseImport.Entities,
                    value => value.Name == orphanVariantOneName);
                var baseItem = Assert.Single(baseImport.Entities, value => value.Name == itemBaseName);

                var variantImport = await ImportAsync(
                    db,
                    variantPackageKey,
                    token,
                    "variant",
                    [
                        Record(RuleConceptEntityTypes.Class, classVariantName, "VARIANT", "class-variant"),
                        Record(RuleConceptEntityTypes.Class, orphanVariantOneName, "VARIANT", "orphan-one-second"),
                        Record(RuleConceptEntityTypes.Class, orphanVariantTwoName, "VARIANT", "orphan-two"),
                        Record("item", itemVariantName, "VARIANT", "item-variant")
                    ],
                    reconcileHistory: true);
                var variantClass = Assert.Single(variantImport.Entities, value => value.Name == classVariantName);
                var orphanVariantOneSecond = Assert.Single(
                    variantImport.Entities,
                    value => value.Name == orphanVariantOneName);
                var orphanVariantTwo = Assert.Single(
                    variantImport.Entities,
                    value => value.Name == orphanVariantTwoName);
                var variantItem = Assert.Single(variantImport.Entities, value => value.Name == itemVariantName);

                var baseClassCanonicalId = await ReadCanonicalEntityIdAsync(db, baseClass.EntityId);
                var variantClassCanonicalId = await ReadCanonicalEntityIdAsync(db, variantClass.EntityId);
                Assert.NotEqual(baseClassCanonicalId, variantClassCanonicalId);
                Assert.True(await HasAutomaticHistoryEdgeAsync(
                    db,
                    baseClassCanonicalId,
                    variantClassCanonicalId));

                var orphanOneFirstCanonicalId = await ReadCanonicalEntityIdAsync(
                    db,
                    orphanVariantOneFirst.EntityId);
                var orphanOneSecondCanonicalId = await ReadCanonicalEntityIdAsync(
                    db,
                    orphanVariantOneSecond.EntityId);
                var orphanTwoCanonicalId = await ReadCanonicalEntityIdAsync(db, orphanVariantTwo.EntityId);
                Assert.NotEqual(orphanOneFirstCanonicalId, orphanOneSecondCanonicalId);
                Assert.True(await HasAutomaticHistoryEdgeAsync(
                    db,
                    orphanOneFirstCanonicalId,
                    orphanOneSecondCanonicalId));
                Assert.False(await HasAutomaticHistoryEdgeAsync(
                    db,
                    orphanOneFirstCanonicalId,
                    orphanTwoCanonicalId));
                Assert.False(await HasAutomaticHistoryEdgeAsync(
                    db,
                    orphanOneSecondCanonicalId,
                    orphanTwoCanonicalId));

                var baseItemCanonicalId = await ReadCanonicalEntityIdAsync(db, baseItem.EntityId);
                var variantItemCanonicalId = await ReadCanonicalEntityIdAsync(db, variantItem.EntityId);
                Assert.NotEqual(baseItemCanonicalId, variantItemCanonicalId);
                Assert.False(await HasAutomaticHistoryEdgeAsync(
                    db,
                    baseItemCanonicalId,
                    variantItemCanonicalId));
            }
            finally
            {
                await DeletePackagesAsync(db, basePackageKey, variantPackageKey);
            }
        }
    }

    [Fact]
    public async Task ExistingParentheticalClassRecordsJoinDuringCorpusReconciliation()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var classBaseName = $"Artificer {token}";
            var classVariantName = $"{classBaseName} (Revisited)";
            var basePackageKey = $"parenthetical-existing-base-{token}";
            var variantPackageKey = $"parenthetical-existing-variant-{token}";

            try
            {
                var baseImport = await ImportAsync(
                    db,
                    basePackageKey,
                    token,
                    "existing-base",
                    [Record(RuleConceptEntityTypes.Class, classBaseName, "BASE", "existing-class-base")],
                    reconcileHistory: false);
                var variantImport = await ImportAsync(
                    db,
                    variantPackageKey,
                    token,
                    "existing-variant",
                    [Record(RuleConceptEntityTypes.Class, classVariantName, "VARIANT", "existing-class-variant")],
                    reconcileHistory: false);

                var baseCanonicalId = await ReadCanonicalEntityIdAsync(
                    db,
                    Assert.Single(baseImport.Entities).EntityId);
                var variantCanonicalId = await ReadCanonicalEntityIdAsync(
                    db,
                    Assert.Single(variantImport.Entities).EntityId);
                Assert.NotEqual(baseCanonicalId, variantCanonicalId);
                Assert.False(await HasAutomaticHistoryEdgeAsync(db, baseCanonicalId, variantCanonicalId));

                await new CanonicalDataReconciliationService(db).ReconcileExistingCorpusAsync();

                Assert.True(await HasAutomaticHistoryEdgeAsync(db, baseCanonicalId, variantCanonicalId));
            }
            finally
            {
                await DeletePackagesAsync(db, basePackageKey, variantPackageKey);
            }
        }
    }

    private static async Task<NormalizedSourceImportResult> ImportAsync(
        RulesCoreDbContext db,
        string packageKey,
        string token,
        string label,
        IReadOnlyList<NormalizedSourceRecord> records,
        bool reconcileHistory)
    {
        var artifact = new SourceRepresentationArtifact(
            $"parenthetical-{label}-{token}.json",
            Encoding.UTF8.GetBytes($"{{\"fixture\":\"{label}-{token}\"}}"),
            $"integration:parenthetical-history:{label}:{token}",
            MediaType: "application/json");
        var representation = new NormalizedSourceRepresentation(
            "integration-json",
            artifact,
            records,
            [
                new NormalizedSourcePublication(
                    "fixture",
                    $"Parenthetical class history {label} {token}",
                    "Integration Test",
                    "5e",
                    label.Contains("base", StringComparison.Ordinal)
                        ? new DateOnly(2020, 1, 1)
                        : new DateOnly(2021, 1, 1))
            ]);
        var request = new ImportNormalizedSourceRequest(
            packageKey,
            $"Parenthetical class history {label} {token}",
            "integration-test",
            "test-only",
            true,
            representation);

        return reconcileHistory
            ? await new ReconciledNormalizedSourceImportService(
                    new NormalizedSourceImportService(db),
                    db)
                .ImportAsync(request)
            : await new NormalizedSourceImportService(db).ImportAsync(request);
    }

    private static NormalizedSourceRecord Record(
        string entityType,
        string name,
        string sourceCode,
        string nativeSuffix)
    {
        var raw = $"{{\"name\":{System.Text.Json.JsonSerializer.Serialize(name)},\"source\":\"{sourceCode}\",\"entries\":[{System.Text.Json.JsonSerializer.Serialize(nativeSuffix)}]}}";
        return new NormalizedSourceRecord(
            entityType,
            name,
            sourceCode,
            $"{entityType}|{sourceCode}|{nativeSuffix}|",
            raw,
            PublicationLocalKey: "fixture");
    }

    private static Task DeletePackagesAsync(
        RulesCoreDbContext db,
        string firstPackageKey,
        string secondPackageKey) =>
        db.SourcePackages
            .Where(value => value.Key == firstPackageKey || value.Key == secondPackageKey)
            .ExecuteDeleteAsync();

    private static async Task<Guid> ReadCanonicalEntityIdAsync(
        RulesCoreDbContext db,
        Guid sourceEntityId)
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

    private static async Task<bool> HasAutomaticHistoryEdgeAsync(
        RulesCoreDbContext db,
        Guid left,
        Guid right)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT EXISTS (
                    SELECT 1
                    FROM canonical_entity_relationship
                    WHERE relationship_kind = 'revision'
                      AND evidence_kind = @evidence_kind
                      AND ((from_canonical_entity_id = @left_id AND to_canonical_entity_id = @right_id)
                        OR (from_canonical_entity_id = @right_id AND to_canonical_entity_id = @left_id)));
                """;
            AddParameter(command, "@evidence_kind", "normalized-name-compatible-category");
            AddParameter(command, "@left_id", left);
            AddParameter(command, "@right_id", right);
            return Convert.ToBoolean(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static async Task<RulesCoreDbContext?> OpenDatabaseAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>()
                .UseNpgsql(connectionString)
                .Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        return db;
    }
}
