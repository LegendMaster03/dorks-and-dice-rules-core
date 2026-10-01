using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
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
public sealed class ReferenceHistoryMergeReadinessIntegrationTests
{
    [Fact]
    public async Task StartupBackfillIsSerializedAndRecordedOnceAcrossIngressContexts()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var first = CreateDb(connectionString);
        await using var second = CreateDb(connectionString);
        await new RulesCoreSchemaInitializer(first).InitializeAsync();
        await EnsureStartupMarkerTableAsync(first);
        await DeleteStartupMarkerAsync(first);

        try
        {
            await Task.WhenAll(
                new CanonicalDataReconciliationService(first).RunStartupBackfillAsync(),
                new CanonicalDataReconciliationService(second).RunStartupBackfillAsync());

            var firstCompletedAt = await ReadStartupMarkerAsync(first);
            Assert.NotNull(firstCompletedAt);
            Assert.Equal(1, await CountStartupMarkersAsync(first));

            await new CanonicalDataReconciliationService(first).RunStartupBackfillAsync();

            Assert.Equal(firstCompletedAt, await ReadStartupMarkerAsync(first));
            Assert.Equal(1, await CountStartupMarkersAsync(first));
        }
        finally
        {
            await DeleteStartupMarkerAsync(first);
        }
    }

    [Fact]
    public async Task UnconvertibleLegacyFluffRevisionKeepsItsCanonicalBinding()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..10];
            var packageKey = $"legacy-unconvertible-{token}";
            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                LegacyFluffRequest(packageKey, $"Legacy Broken {token}", $"LB{token}"));
            var entityId = Assert.Single(imported.Entities).EntityId;
            var revisionId = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == entityId)
                .Select(value => value.Id)
                .SingleAsync();

            await db.SourceEntityRevisions
                .Where(value => value.Id == revisionId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.RawJson, "[]"));

            Assert.True(await HasCanonicalBindingAsync(db, revisionId));
            await new CanonicalDataReconciliationService(db).ReconcileExistingCorpusAsync();
            Assert.True(await HasCanonicalBindingAsync(db, revisionId));
            Assert.Equal(0, await CountCompanionsForPackageAsync(db, imported.PackageId));
        }
    }

    [Fact]
    public async Task AuthoritativeSeparationRepairsTransitiveAutomaticHistoryImmediately()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..10];
            var name = $"Three Way Homonym {token}";
            var importer = new ReconciledNormalizedSourceImportService(
                new NormalizedSourceImportService(db),
                db);

            var first = await importer.ImportAsync(Request(
                $"history-a-{token}", name, $"HA{token}", "3e", 7));
            var second = await importer.ImportAsync(Request(
                $"history-b-{token}", name, $"HB{token}", "3.5e", 9));
            var third = await importer.ImportAsync(Request(
                $"history-c-{token}", name, $"HC{token}", "5e", 12));

            Assert.Single((await CatalogAsync(db, name)).References.Where(value => value.DisplayName == name));

            var secondCanonical = await CanonicalEntityIdAsync(db, Assert.Single(second.Entities).EntityId);
            var thirdCanonical = await CanonicalEntityIdAsync(db, Assert.Single(third.Entities).EntityId);
            await new CanonicalBootstrapReconciliationService(db).RecordAsync(
                new RecordCanonicalBootstrapReconciliationRequest(
                    "5etools-mirror-3-5etools-src",
                    $"history-separation-{token}",
                    CanonicalSourceIdentity.Fingerprint($"history-separation-{token}"),
                    CanonicalBootstrapReconciliationClassifications.SameNameDifferentEntity,
                    secondCanonical,
                    thirdCanonical,
                    "integration-authoritative-separation",
                    1.0,
                    "integration-test",
                    "The second and third fixtures are intentionally distinct homonyms."));

            var separated = await CatalogAsync(db, name);
            Assert.Equal(2, separated.References.Count(value => value.DisplayName == name));
            Assert.NotEmpty(first.Entities);
        }
    }

    [Fact]
    public async Task CompanionOnlyImportsWithIdenticalBytesShareOneContentBlobAcrossPackages()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var firstDb = CreateDb(connectionString);
        await using var secondDb = CreateDb(connectionString);
        await new RulesCoreSchemaInitializer(firstDb).InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..10];
        var artifactBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            fixture = $"shared-companion-{token}"
        }));
        var firstRequest = CompanionOnlyRequest($"companion-race-a-{token}", token, artifactBytes);
        var secondRequest = CompanionOnlyRequest($"companion-race-b-{token}", token, artifactBytes);

        var firstImporter = new ReconciledNormalizedSourceImportService(
            new NormalizedSourceImportService(firstDb),
            firstDb);
        var secondImporter = new ReconciledNormalizedSourceImportService(
            new NormalizedSourceImportService(secondDb),
            secondDb);

        var results = await Task.WhenAll(
            firstImporter.ImportAsync(firstRequest),
            secondImporter.ImportAsync(secondRequest));

        var hash = Convert.ToHexString(SHA256.HashData(artifactBytes)).ToLowerInvariant();
        await using var verify = CreateDb(connectionString);
        Assert.Equal(1, await verify.SourceContentBlobs.CountAsync(value => value.Sha256 == hash));
        Assert.Equal(2, results.Select(value => value.PackageId).Distinct().Count());
    }

    private static ImportNormalizedSourceRequest Request(
        string packageKey,
        string name,
        string sourceCode,
        string edition,
        int hp)
    {
        var raw = JsonSerializer.Serialize(new { name, source = sourceCode, hp });
        return new ImportNormalizedSourceRequest(
            packageKey,
            packageKey,
            "integration-test",
            null,
            true,
            new NormalizedSourceRepresentation(
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
                    GameEdition: edition)]));
    }

    private static ImportNormalizedSourceRequest LegacyFluffRequest(
        string packageKey,
        string name,
        string sourceCode)
    {
        var raw = JsonSerializer.Serialize(new
        {
            name,
            source = sourceCode,
            entries = new[] { "Legacy fluff." }
        });
        return new ImportNormalizedSourceRequest(
            packageKey,
            packageKey,
            "integration-test",
            null,
            true,
            new NormalizedSourceRepresentation(
                FiveEToolsSourceFormatAdapter.Format,
                new SourceRepresentationArtifact(
                    $"{packageKey}.json",
                    Encoding.UTF8.GetBytes(raw),
                    $"integration:{packageKey}"),
                [new NormalizedSourceRecord(
                    "monsterFluff",
                    name,
                    sourceCode,
                    $"monsterFluff|{sourceCode}|{name}|",
                    raw,
                    PublicationLocalKey: $"source:{sourceCode}")],
                [new NormalizedSourcePublication(
                    $"source:{sourceCode}",
                    $"Publication {sourceCode}",
                    GameEdition: "5e")]));
    }

    private static ImportNormalizedSourceRequest CompanionOnlyRequest(
        string packageKey,
        string token,
        byte[] artifactBytes)
    {
        var sourceCode = $"CO{token}";
        var name = $"Companion Only {token}";
        var raw = JsonSerializer.Serialize(new
        {
            name,
            source = sourceCode,
            entries = new[] { "Companion-only fixture." }
        });
        var representation = new NormalizedSourceRepresentation(
            FiveEToolsSourceFormatAdapter.Format,
            new SourceRepresentationArtifact(
                $"{packageKey}.json",
                artifactBytes,
                $"integration:shared-companion:{token}"),
            [],
            [])
        {
            CompanionContents =
            [
                new NormalizedSourceCompanionContent(
                    "monsterFluff",
                    name,
                    sourceCode,
                    $"monsterFluff|{sourceCode}|{name}|",
                    raw,
                    [new NormalizedSourceCompanionTarget(
                        "monster",
                        name,
                        sourceCode,
                        "name-source")])
            ]
        };
        return new ImportNormalizedSourceRequest(
            packageKey,
            packageKey,
            "integration-test",
            null,
            false,
            representation);
    }

    private static Task<WikiReferenceCatalogView> CatalogAsync(RulesCoreDbContext db, string query) =>
        new WikiReferenceCatalogService(db).GetGlobalCatalogAsync(
            userId: null,
            entityType: null,
            categoryMode: WikiReferenceCategoryModes.AnyVariation,
            query,
            sourceCode: null,
            packageKey: null,
            edition: null,
            limit: 100,
            offset: 0);

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
                JOIN source_entity_revision revision
                    ON revision.source_entity_revision_id = binding.source_entity_revision_id
                WHERE binding.source_entity_id = @source_entity_id
                  AND occurrence.canonical_entity_id IS NOT NULL
                ORDER BY revision.revision_number DESC
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

    private static async Task<bool> HasCanonicalBindingAsync(RulesCoreDbContext db, Guid revisionId)
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
                    FROM source_entity_occurrence_binding
                    WHERE source_entity_revision_id = @revision_id);
                """;
            AddParameter(command, "@revision_id", revisionId);
            return Convert.ToBoolean(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<int> CountCompanionsForPackageAsync(RulesCoreDbContext db, Guid packageId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM source_companion_content
                WHERE source_package_id = @package_id;
                """;
            AddParameter(command, "@package_id", packageId);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task EnsureStartupMarkerTableAsync(RulesCoreDbContext db) =>
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS canonical_data_reconciliation_backfill (
                backfill_key varchar(120) NOT NULL,
                completed_at timestamp with time zone NOT NULL,
                CONSTRAINT pk_canonical_data_reconciliation_backfill PRIMARY KEY (backfill_key));
            """);

    private static Task DeleteStartupMarkerAsync(RulesCoreDbContext db) =>
        db.Database.ExecuteSqlRawAsync($"""
            DELETE FROM canonical_data_reconciliation_backfill
            WHERE backfill_key = '{CanonicalDataReconciliationService.StartupBackfillKey}';
            """);

    private static async Task<int> CountStartupMarkersAsync(RulesCoreDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM canonical_data_reconciliation_backfill
                WHERE backfill_key = @key;
                """;
            AddParameter(command, "@key", CanonicalDataReconciliationService.StartupBackfillKey);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<DateTimeOffset?> ReadStartupMarkerAsync(RulesCoreDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT completed_at
                FROM canonical_data_reconciliation_backfill
                WHERE backfill_key = @key;
                """;
            AddParameter(command, "@key", CanonicalDataReconciliationService.StartupBackfillKey);
            var value = await command.ExecuteScalarAsync();
            return value switch
            {
                DateTimeOffset completedAt => completedAt,
                DateTime completedAt => new DateTimeOffset(
                    DateTime.SpecifyKind(completedAt, DateTimeKind.Utc)),
                _ => null
            };
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
        var db = CreateDb(connectionString);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        return db;
    }

    private static RulesCoreDbContext CreateDb(string connectionString) =>
        new(new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
