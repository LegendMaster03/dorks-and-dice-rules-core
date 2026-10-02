using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class HistoricalReconciliationPerformanceIntegrationTests
{
    [Fact]
    public async Task RepeatedHistoricalReconciliationDoesNotRewriteUnchangedCompanionRows()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..10];
            var name = $"Historical Companion {token}";
            var sourceCode = $"HC{token}";
            var importer = new NormalizedSourceImportService(db);

            await importer.ImportAsync(TargetRequest(
                $"historical-target-{token}",
                name,
                sourceCode));
            var legacy = await importer.ImportAsync(LegacyFluffRequest(
                $"historical-fluff-{token}",
                name,
                sourceCode));
            var legacyEntityId = Assert.Single(legacy.Entities).EntityId;
            var legacyRevisionId = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == legacyEntityId)
                .Select(value => value.Id)
                .SingleAsync();

            Assert.True(await HasCanonicalBindingAsync(db, legacyRevisionId));

            var reconciliation = new CanonicalDataReconciliationService(db);
            await reconciliation.ReconcileExistingCorpusAsync();

            Assert.False(await HasCanonicalBindingAsync(db, legacyRevisionId));
            var firstAttachmentVersions = await ReadAttachmentVersionsAsync(db, legacy.PackageId);
            var firstContentVersions = await ReadContentVersionsAsync(db, legacy.PackageId);
            Assert.NotEmpty(firstAttachmentVersions);
            Assert.Single(firstContentVersions);

            await reconciliation.ReconcileExistingCorpusAsync();

            Assert.Equal(
                firstAttachmentVersions,
                await ReadAttachmentVersionsAsync(db, legacy.PackageId));
            Assert.Equal(
                firstContentVersions,
                await ReadContentVersionsAsync(db, legacy.PackageId));
            Assert.Equal(1, await CountCompanionsForPackageAsync(db, legacy.PackageId));
        }
    }

    [Fact]
    public async Task InterruptedLegacyMigrationIsRecoveredWithoutRewritingPersistedCompanionContent()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..10];
            var name = $"Historical Restart {token}";
            var sourceCode = $"HR{token}";
            var importer = new NormalizedSourceImportService(db);

            await importer.ImportAsync(TargetRequest(
                $"historical-restart-target-{token}",
                name,
                sourceCode));
            var legacy = await importer.ImportAsync(LegacyFluffRequest(
                $"historical-restart-fluff-{token}",
                name,
                sourceCode));
            var legacyEntityId = Assert.Single(legacy.Entities).EntityId;
            var legacyRevisionId = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == legacyEntityId)
                .Select(value => value.Id)
                .SingleAsync();

            Assert.True(await HasCanonicalBindingAsync(db, legacyRevisionId));

            // Simulate interruption after legacy rows have been converted and their canonical
            // bindings removed, but before the corpus-wide attachment pass has run.
            await new LegacyStandaloneFluffMigrationService(db).MigrateAsync();

            Assert.False(await HasCanonicalBindingAsync(db, legacyRevisionId));
            Assert.Equal(1, await CountCompanionsForPackageAsync(db, legacy.PackageId));
            Assert.Empty(await ReadAttachmentVersionsAsync(db, legacy.PackageId));
            var persistedContentVersions = await ReadContentVersionsAsync(db, legacy.PackageId);
            Assert.Single(persistedContentVersions);

            await new CanonicalDataReconciliationService(db).ReconcileExistingCorpusAsync();

            Assert.NotEmpty(await ReadAttachmentVersionsAsync(db, legacy.PackageId));
            Assert.Equal(
                persistedContentVersions,
                await ReadContentVersionsAsync(db, legacy.PackageId));
            Assert.Equal(1, await CountCompanionsForPackageAsync(db, legacy.PackageId));
        }
    }

    private static ImportNormalizedSourceRequest TargetRequest(
        string packageKey,
        string name,
        string sourceCode)
    {
        var raw = JsonSerializer.Serialize(new { name, source = sourceCode, hp = 7 });
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
                    GameEdition: "5e")]));
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

    private static async Task<IReadOnlyList<string>> ReadAttachmentVersionsAsync(
        RulesCoreDbContext db,
        Guid packageId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT attachment.source_companion_attachment_id::text || ':' || attachment.xmin::text
                FROM source_companion_attachment attachment
                JOIN source_companion_content content
                    ON content.source_companion_content_id = attachment.source_companion_content_id
                WHERE content.source_package_id = @package_id
                ORDER BY attachment.source_companion_attachment_id;
                """;
            AddParameter(command, "@package_id", packageId);
            var values = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) values.Add(reader.GetString(0));
            return values;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<IReadOnlyList<string>> ReadContentVersionsAsync(
        RulesCoreDbContext db,
        Guid packageId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT source_companion_content_id::text || ':' || xmin::text
                FROM source_companion_content
                WHERE source_package_id = @package_id
                ORDER BY source_companion_content_id;
                """;
            AddParameter(command, "@package_id", packageId);
            var values = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) values.Add(reader.GetString(0));
            return values;
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

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
