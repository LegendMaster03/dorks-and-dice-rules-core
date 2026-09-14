using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CanonicalRevisionIdentityIntegrationTests
{
    [Fact]
    public async Task ProvenanceOnlyRevisionRetainsCanonicalEntityAcrossLocatorMove()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var packageKey = $"canonical-lineage-{token}";
            var importer = new NormalizedSourceImportService(db);

            await importer.ImportAsync(Request(
                packageKey,
                token,
                "{\"name\":\"Lineage Rule\",\"source\":\"LIN\",\"page\":10,\"effect\":\"Gain a +2 bonus.\"}",
                "page:10"));
            await importer.ImportAsync(Request(
                packageKey,
                token,
                "{\"name\":\"Lineage Rule\",\"source\":\"LIN\",\"page\":11,\"effect\":\"Gain a +2 bonus.\"}",
                "page:11"));

            var bindings = await ReadBindingsAsync(db, packageKey);
            Assert.Equal(2, bindings.Count);
            Assert.Equal(bindings[0].CanonicalEntityId, bindings[1].CanonicalEntityId);
            Assert.NotEqual(bindings[0].OccurrenceId, bindings[1].OccurrenceId);
            Assert.Equal(bindings[0].SemanticFingerprint, bindings[1].SemanticFingerprint);
            Assert.EndsWith(":source-revision-lineage", bindings[1].MatchKind, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task MechanicallyChangedRevisionDoesNotInheritPriorCanonicalEntity()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var packageKey = $"canonical-revision-{token}";
            var importer = new NormalizedSourceImportService(db);

            await importer.ImportAsync(Request(
                packageKey,
                token,
                "{\"name\":\"Revision Rule\",\"source\":\"REV\",\"page\":20,\"effect\":\"Gain a +2 bonus.\"}",
                "page:20"));
            await importer.ImportAsync(Request(
                packageKey,
                token,
                "{\"name\":\"Revision Rule\",\"source\":\"REV\",\"page\":20,\"effect\":\"Gain a +3 bonus.\"}",
                "page:20"));

            var bindings = await ReadBindingsAsync(db, packageKey);
            Assert.Equal(2, bindings.Count);
            Assert.NotEqual(bindings[0].SemanticFingerprint, bindings[1].SemanticFingerprint);
            Assert.NotEqual(bindings[0].CanonicalEntityId, bindings[1].CanonicalEntityId);
            Assert.NotEqual(bindings[0].OccurrenceId, bindings[1].OccurrenceId);
            Assert.EndsWith(":identity-key", bindings[1].MatchKind, StringComparison.Ordinal);
        }
    }

    private static ImportNormalizedSourceRequest Request(
        string packageKey,
        string token,
        string rawJson,
        string locator)
    {
        var publicationKey = $"publication-{token}";
        return new ImportNormalizedSourceRequest(
            packageKey,
            $"Canonical revision package {token}",
            "integration-test",
            License: "test-only",
            IsPublic: false,
            new NormalizedSourceRepresentation(
                FiveEToolsSourceFormatAdapter.Format,
                new SourceRepresentationArtifact(
                    $"revision-{token}.json",
                    Encoding.UTF8.GetBytes(rawJson),
                    $"integration:canonical-revision:{token}"),
                [new NormalizedSourceRecord(
                    "rule",
                    rawJson.Contains("Lineage Rule", StringComparison.Ordinal) ? "Lineage Rule" : "Revision Rule",
                    rawJson.Contains("\"LIN\"", StringComparison.Ordinal) ? "LIN" : "REV",
                    NativeKey: $"rule|{token}",
                    RawJson: rawJson,
                    LocatorKey: locator,
                    PublicationLocalKey: publicationKey)],
                [new NormalizedSourcePublication(
                    publicationKey,
                    $"Canonical Revision Publication {token}",
                    Publisher: "Integration Test Press",
                    GameEdition: "3.5e",
                    PublicationDate: new DateOnly(2006, 1, 1),
                    ExternalIdentifiers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["isbn"] = $"test-canonical-revision-{token}"
                    })]));
    }

    private static async Task<IReadOnlyList<BindingRow>> ReadBindingsAsync(
        RulesCoreDbContext db,
        string packageKey)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    revision.revision_number,
                    occurrence.canonical_entity_id,
                    binding.canonical_source_occurrence_id,
                    binding.semantic_fingerprint,
                    binding.match_kind
                FROM source_package package
                JOIN source_entity source
                    ON source.source_package_id = package.source_package_id
                JOIN source_entity_revision revision
                    ON revision.source_entity_id = source.source_entity_id
                JOIN source_entity_occurrence_binding binding
                    ON binding.source_entity_revision_id = revision.source_entity_revision_id
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                WHERE package.package_key = @package_key
                ORDER BY revision.revision_number;
                """;
            AddParameter(command, "@package_key", packageKey);
            var rows = new List<BindingRow>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new BindingRow(
                    reader.GetInt32(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.GetString(3),
                    reader.GetString(4)));
            }
            return rows;
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

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record BindingRow(
        int RevisionNumber,
        Guid CanonicalEntityId,
        Guid OccurrenceId,
        string SemanticFingerprint,
        string MatchKind);
}
