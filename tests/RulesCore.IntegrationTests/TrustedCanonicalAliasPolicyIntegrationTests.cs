using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class TrustedCanonicalAliasPolicyIntegrationTests
{
    [Fact]
    public async Task OfficialPcGenArtifactCanReuseBootstrapConfirmedAliasWithoutExplicitRecordAlias()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var importer = new NormalizedSourceImportService(db);
            var name = $"Trusted PCGen Rule {token}";
            var firstSemantic = $"{{\"name\":\"{name}\",\"effect\":\"Gain a +2 bonus.\"}}";
            var pcgenSemantic = $"{{\"name\":\"{name}\",\"entityType\":\"feat\",\"segments\":[{{\"Tag\":\"BONUS\",\"Value\":\"2\"}}]}}";

            var seed = await importer.ImportAsync(Request(
                $"trusted-seed-{token}",
                "fixture-seed",
                name,
                "seed-native",
                firstSemantic,
                firstSemantic,
                sourceUri: null));
            var seedBinding = await ReadLatestBindingAsync(db, Assert.Single(seed.Entities).EntityId);
            Assert.NotNull(seedBinding);

            var pcgenNativeKey = $"pcgen|feat|fixture|{token}|1";
            var pcgenFingerprint = CanonicalSourceIdentity.SemanticFingerprint(pcgenSemantic);
            await new CanonicalEntityAliasStore(db).RegisterAsync(
                seedBinding.Value.CanonicalEntityId,
                "pcgen-org-pcgen",
                pcgenNativeKey,
                pcgenFingerprint,
                "bootstrap-confirmed-exact-identity",
                1.0);

            var imported = await importer.ImportAsync(Request(
                $"trusted-pcgen-{token}",
                PcGenSourceFormatAdapter.Format,
                name,
                pcgenNativeKey,
                $"{{\"format\":\"pcgen-data\",\"name\":\"{name}\",\"raw\":\"BONUS:2\"}}",
                pcgenSemantic,
                sourceUri: "https://raw.githubusercontent.com/PCGen/pcgen/master/data/35e/example/example_feats.lst"));
            var importedBinding = await ReadLatestBindingAsync(db, Assert.Single(imported.Entities).EntityId);
            Assert.NotNull(importedBinding);

            Assert.Equal(seedBinding.Value.CanonicalEntityId, importedBinding.Value.CanonicalEntityId);
            Assert.EndsWith(":strong-alias", importedBinding.Value.MatchKind, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task LookalikePcGenArtifactDoesNotReceiveOfficialLineageAlias()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var importer = new NormalizedSourceImportService(db);
            var name = $"Untrusted PCGen Rule {token}";
            var firstSemantic = $"{{\"name\":\"{name}\",\"effect\":\"Gain a +2 bonus.\"}}";
            var pcgenSemantic = $"{{\"name\":\"{name}\",\"entityType\":\"feat\",\"segments\":[{{\"Tag\":\"BONUS\",\"Value\":\"2\"}}]}}";

            var seed = await importer.ImportAsync(Request(
                $"untrusted-seed-{token}",
                "fixture-seed",
                name,
                "seed-native",
                firstSemantic,
                firstSemantic,
                sourceUri: null));
            var seedBinding = await ReadLatestBindingAsync(db, Assert.Single(seed.Entities).EntityId);
            Assert.NotNull(seedBinding);

            var pcgenNativeKey = $"pcgen|feat|fixture|{token}|1";
            var pcgenFingerprint = CanonicalSourceIdentity.SemanticFingerprint(pcgenSemantic);
            await new CanonicalEntityAliasStore(db).RegisterAsync(
                seedBinding.Value.CanonicalEntityId,
                "pcgen-org-pcgen",
                pcgenNativeKey,
                pcgenFingerprint,
                "bootstrap-confirmed-exact-identity",
                1.0);

            var imported = await importer.ImportAsync(Request(
                $"untrusted-pcgen-{token}",
                PcGenSourceFormatAdapter.Format,
                name,
                pcgenNativeKey,
                $"{{\"format\":\"pcgen-data\",\"name\":\"{name}\",\"raw\":\"BONUS:2\"}}",
                pcgenSemantic,
                sourceUri: "https://example.invalid/PCGen/pcgen/example_feats.lst"));
            var importedBinding = await ReadLatestBindingAsync(db, Assert.Single(imported.Entities).EntityId);
            Assert.NotNull(importedBinding);

            Assert.False(importedBinding.Value.MatchKind.EndsWith(":strong-alias", StringComparison.Ordinal));
        }
    }

    private static ImportNormalizedSourceRequest Request(
        string packageKey,
        string formatKey,
        string name,
        string nativeKey,
        string rawJson,
        string semanticJson,
        string? sourceUri)
    {
        const string publicationKey = "trusted-alias-book";
        return new ImportNormalizedSourceRequest(
            packageKey,
            packageKey,
            "integration-test",
            License: null,
            IsPublic: false,
            new NormalizedSourceRepresentation(
                formatKey,
                new SourceRepresentationArtifact(
                    $"{packageKey}.data",
                    Encoding.UTF8.GetBytes(rawJson),
                    $"test:{packageKey}:{Guid.NewGuid():N}",
                    SourceUri: sourceUri,
                    MediaType: "text/plain"),
                [new NormalizedSourceRecord(
                    "feat",
                    name,
                    SourceCode: "TRUST",
                    NativeKey: nativeKey,
                    RawJson: rawJson,
                    LocatorKey: "entry:1",
                    PublicationLocalKey: publicationKey)
                {
                    ContentJson = semanticJson
                }],
                [new NormalizedSourcePublication(
                    publicationKey,
                    "Trusted Alias Fixture Book",
                    Publisher: "Example Press",
                    GameEdition: "3.5e",
                    PublicationDate: new DateOnly(2003, 7, 1),
                    ExternalIdentifiers: new Dictionary<string, string>
                    {
                        ["isbn"] = "9780306406157"
                    })]));
    }

    private static async Task<(Guid CanonicalEntityId, string MatchKind)?> ReadLatestBindingAsync(
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
                SELECT occurrence.canonical_entity_id, binding.match_kind
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
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync()
                ? (reader.GetGuid(0), reader.GetString(1))
                : null;
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
}
