using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class ClassTaxonomyNormalizationIntegrationTests
{
    [Fact]
    public async Task FiveEToolsClassRecordsUseReviewedSpecificCategoriesWithoutChangingNativeIdentity()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var packageKey = $"class-taxonomy-5etools-{token}";
            const string json = """
                {
                  "_meta": {
                    "edition": "classic",
                    "sources": [
                      {"json":"UAPrestigeClassesRunMagic","full":"Unearthed Arcana: Prestige Classes and Rune Magic"},
                      {"json":"TCE","full":"Tasha's Cauldron of Everything"},
                      {"json":"PHB","full":"Player's Handbook"}
                    ]
                  },
                  "class": [
                    {"name":"Prestige Class: Rune Scribe","source":"UAPrestigeClassesRunMagic","hd":{"number":1,"faces":8}},
                    {"name":"Expert Sidekick","source":"TCE","isSidekick":true},
                    {"name":"Fighter","source":"PHB","hd":{"number":1,"faces":10}}
                  ]
                }
                """;
            var artifact = new SourceRepresentationArtifact(
                "class-edge-cases.json",
                Encoding.UTF8.GetBytes(json),
                $"admin:class-taxonomy-fixture:{token}",
                "https://raw.githubusercontent.com/5etools-mirror-3/5etools-src/main/data/class/class-sidekick.json",
                "application/json");
            var representation = new FiveEToolsSourceFormatAdapter().TryRead(artifact)
                ?? throw new InvalidOperationException("The 5e.tools fixture was not readable.");

            var runeNative = Assert.Single(representation.Records, value =>
                value.Name == "Prestige Class: Rune Scribe");
            var sidekickNative = Assert.Single(representation.Records, value =>
                value.Name == "Expert Sidekick");
            var fighterNative = Assert.Single(representation.Records, value =>
                value.Name == "Fighter");
            Assert.All(representation.Records, value => Assert.Equal("class", value.EntityType));

            try
            {
                var imported = await new NormalizedSourceImportService(db).ImportAsync(
                    new ImportNormalizedSourceRequest(
                        packageKey,
                        $"Class taxonomy 5e.tools fixture {token}",
                        "integration-test",
                        "test-only",
                        false,
                        representation));

                var rune = Assert.Single(imported.Entities, value =>
                    value.Name == "Prestige Class: Rune Scribe");
                var sidekick = Assert.Single(imported.Entities, value =>
                    value.Name == "Expert Sidekick");
                var fighter = Assert.Single(imported.Entities, value =>
                    value.Name == "Fighter");

                Assert.Equal(RuleConceptEntityTypes.PrestigeClass, rune.EntityType);
                Assert.Equal(RuleConceptEntityTypes.SidekickClass, sidekick.EntityType);
                Assert.Equal(RuleConceptEntityTypes.Class, fighter.EntityType);

                var entities = await db.SourceEntities
                    .AsNoTracking()
                    .Where(value => value.SourcePackage.Key == packageKey)
                    .ToDictionaryAsync(value => value.Name, StringComparer.OrdinalIgnoreCase);
                Assert.Equal(runeNative.NativeKey, entities[rune.Name].NativeKey);
                Assert.Equal(sidekickNative.NativeKey, entities[sidekick.Name].NativeKey);
                Assert.Equal(fighterNative.NativeKey, entities[fighter.Name].NativeKey);
                Assert.StartsWith("class|", entities[rune.Name].NativeKey, StringComparison.Ordinal);
                Assert.StartsWith("class|", entities[sidekick.Name].NativeKey, StringComparison.Ordinal);

                var runeAliases = await ReadBoundCanonicalAliasValuesAsync(db, rune.EntityId);
                var sidekickAliases = await ReadBoundCanonicalAliasValuesAsync(db, sidekick.EntityId);
                var fighterAliases = await ReadBoundCanonicalAliasValuesAsync(db, fighter.EntityId);
                Assert.Contains(
                    runeAliases,
                    value => value.Contains(
                        "rules-core-class-taxonomy-v1:prestigeclass",
                        StringComparison.Ordinal));
                Assert.Contains(
                    sidekickAliases,
                    value => value.Contains(
                        "rules-core-class-taxonomy-v1:sidekickclass",
                        StringComparison.Ordinal));
                Assert.Contains(fighter.NativeKey, fighterAliases);
            }
            finally
            {
                await db.SourcePackages
                    .Where(value => value.Key == packageKey)
                    .ExecuteDeleteAsync();
            }
        }
    }

    [Fact]
    public async Task ReviewedPcGenDragonsClassFileNormalizesToPrestigeClassWithoutLevelHeuristics()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var packageKey = $"class-taxonomy-pcgen-{token}";
            const string dragonsRaw = """
                {
                  "format":"pcgen-data",
                  "kind":"class-record",
                  "path":"data/3e/alderac_entertainment_group/dragons/dragonsclasses.lst",
                  "name":"Air Lord",
                  "entityType":"class",
                  "lines":[],
                  "segments":[
                    {"Index":0,"Tag":"HD","Value":"10","Raw":"HD:10"},
                    {"Index":1,"Tag":"MAXLEVEL","Value":"10","Raw":"MAXLEVEL:10"}
                  ]
                }
                """;
            const string ordinaryRaw = """
                {
                  "format":"pcgen-data",
                  "kind":"class-record",
                  "path":"data/3e/example/ordinaryclasses.lst",
                  "name":"Ten Level Base Class",
                  "entityType":"class",
                  "lines":[],
                  "segments":[
                    {"Index":0,"Tag":"HD","Value":"10","Raw":"HD:10"},
                    {"Index":1,"Tag":"MAXLEVEL","Value":"10","Raw":"MAXLEVEL:10"}
                  ]
                }
                """;
            var artifact = new SourceRepresentationArtifact(
                "dragonsclasses.lst",
                Encoding.UTF8.GetBytes("fixture"),
                $"github-tree:PCGen/pcgen:data/3e:{token}",
                "https://raw.githubusercontent.com/PCGen/pcgen/master/data/3e/alderac_entertainment_group/dragons/dragonsclasses.lst",
                "text/plain");
            var representation = new NormalizedSourceRepresentation(
                PcGenSourceFormatAdapter.Format,
                artifact,
                [
                    new NormalizedSourceRecord(
                        "class",
                        "Air Lord",
                        "Dragons",
                        "pcgen|class|air-lord",
                        dragonsRaw),
                    new NormalizedSourceRecord(
                        "class",
                        "Ten Level Base Class",
                        "TEST",
                        "pcgen|class|ten-level-base-class",
                        ordinaryRaw)
                ]);

            try
            {
                var imported = await new NormalizedSourceImportService(db).ImportAsync(
                    new ImportNormalizedSourceRequest(
                        packageKey,
                        $"Class taxonomy PCGen fixture {token}",
                        "integration-test",
                        "test-only",
                        false,
                        representation));

                var airLord = Assert.Single(imported.Entities, value => value.Name == "Air Lord");
                var ordinary = Assert.Single(imported.Entities, value => value.Name == "Ten Level Base Class");
                Assert.Equal(RuleConceptEntityTypes.PrestigeClass, airLord.EntityType);
                Assert.Equal(RuleConceptEntityTypes.Class, ordinary.EntityType);

                var airLordEntity = await db.SourceEntities
                    .AsNoTracking()
                    .SingleAsync(value => value.Id == airLord.EntityId);
                Assert.Equal("pcgen|class|air-lord", airLordEntity.NativeKey);

                var airLordRevision = await db.SourceEntityRevisions
                    .AsNoTracking()
                    .SingleAsync(value => value.SourceEntityId == airLord.EntityId);
                using var document = JsonDocument.Parse(airLordRevision.ContentJson!);
                Assert.Equal(
                    RuleConceptEntityTypes.PrestigeClass,
                    document.RootElement
                        .GetProperty("_rulesCore")
                        .GetProperty("context")
                        .GetProperty("translatedEntityType")
                        .GetString());
            }
            finally
            {
                await db.SourcePackages
                    .Where(value => value.Key == packageKey)
                    .ExecuteDeleteAsync();
            }
        }
    }

    [Fact]
    public async Task NormalizationBackfillRepairsStaleSidekickClassIdentityWithoutCreatingANativeRevision()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var packageKey = $"class-taxonomy-backfill-{token}";
            var json = """
                {
                  "_meta": {
                    "edition": "classic",
                    "sources": [
                      {"json":"TCE","full":"Tasha's Cauldron of Everything","dateReleased":"2020-11-17"}
                    ]
                  },
                  "class": [
                    {"name":"Expert Sidekick","source":"TCE","isSidekick":true}
                  ]
                }
                """;
            var artifact = new SourceRepresentationArtifact(
                "class-sidekick.json",
                Encoding.UTF8.GetBytes(json),
                $"admin:class-taxonomy-backfill:{token}",
                "https://raw.githubusercontent.com/5etools-mirror-3/5etools-src/main/data/class/class-sidekick.json",
                "application/json");
            var representation = new FiveEToolsSourceFormatAdapter().TryRead(artifact)
                ?? throw new InvalidOperationException("The sidekick fixture was not readable.");

            try
            {
                var importer = new NormalizedSourceImportService(db);
                var imported = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Class taxonomy backfill fixture {token}",
                    "integration-test",
                    "test-only",
                    false,
                    representation));
                var importedEntity = Assert.Single(imported.Entities);
                Assert.Equal(RuleConceptEntityTypes.SidekickClass, importedEntity.EntityType);

                var entity = await db.SourceEntities
                    .SingleAsync(value => value.Id == importedEntity.EntityId);
                var revision = await db.SourceEntityRevisions
                    .SingleAsync(value => value.SourceEntityId == entity.Id);
                var correctedCanonicalId = await ReadBoundCanonicalEntityAsync(db, revision.Id);

                entity.EntityType = RuleConceptEntityTypes.Class;
                revision.NormalizationVersion = SourceNormalizationVersion.Current - 1;
                revision.NormalizationAttemptVersion = SourceNormalizationVersion.Current - 1;
                revision.NormalizationAttemptedAt = null;
                revision.NormalizationError = null;
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();

                var registry = new SourceFormatAdapterRegistry(
                [
                    new FiveEToolsSourceFormatAdapter(),
                    new PcGenSourceFormatAdapter(),
                    new PdfSourceFormatAdapter()
                ]);
                var maintenance = new SourceNormalizationMaintenanceService(db, registry);
                var result = await maintenance.ReconcileAsync(
                    limit: 10,
                    retryFailed: false,
                    packageKey: packageKey);

                Assert.Equal(1, result.AttemptedRevisionCount);
                Assert.Equal(0, result.FailedRevisionCount);
                Assert.Equal(1, result.CanonicalReassociationCount);

                var repairedEntity = await db.SourceEntities
                    .AsNoTracking()
                    .SingleAsync(value => value.Id == importedEntity.EntityId);
                Assert.Equal(RuleConceptEntityTypes.SidekickClass, repairedEntity.EntityType);
                Assert.Equal(
                    1,
                    await db.SourceEntityRevisions.CountAsync(value =>
                        value.SourceEntityId == importedEntity.EntityId));

                var repairedRevision = await db.SourceEntityRevisions
                    .AsNoTracking()
                    .SingleAsync(value => value.SourceEntityId == importedEntity.EntityId);
                Assert.Equal(SourceNormalizationVersion.Current, repairedRevision.NormalizationVersion);
                Assert.Equal(
                    correctedCanonicalId,
                    await ReadBoundCanonicalEntityAsync(db, repairedRevision.Id));
            }
            finally
            {
                await db.SourcePackages
                    .Where(value => value.Key == packageKey)
                    .ExecuteDeleteAsync();
            }
        }
    }

    private static async Task<IReadOnlyList<string>> ReadBoundCanonicalAliasValuesAsync(
        RulesCoreDbContext db,
        Guid entityId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DISTINCT alias.alias_value
                FROM source_entity_revision revision
                JOIN source_entity_occurrence_binding binding
                  ON binding.source_entity_revision_id = revision.source_entity_revision_id
                JOIN canonical_source_occurrence occurrence
                  ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                JOIN canonical_entity_alias alias
                  ON alias.canonical_entity_id = occurrence.canonical_entity_id
                WHERE revision.source_entity_id = @entity_id
                ORDER BY alias.alias_value;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@entity_id";
            parameter.Value = entityId;
            command.Parameters.Add(parameter);

            var values = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                values.Add(reader.GetString(0));
            }
            return values;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<Guid> ReadBoundCanonicalEntityAsync(
        RulesCoreDbContext db,
        Guid revisionId)
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
                WHERE binding.source_entity_revision_id = @revision_id;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@revision_id";
            parameter.Value = revisionId;
            command.Parameters.Add(parameter);
            return (Guid)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("The source revision is not canonically bound."));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<RulesCoreDbContext?> OpenDatabaseAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>()
                .UseNpgsql(connectionString)
                .Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        return db;
    }
}
