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
public sealed class CanonicalEntityAliasIntegrationTests
{
    [Fact]
    public async Task ConfirmedStrongAliasReusesCanonicalEntityAcrossDifferentSemanticProjections()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var importer = new NormalizedSourceImportService(db);
            var name = $"Cross Format Rule {token}";
            var firstSemantic = JsonSerializer.Serialize(new
            {
                name,
                effect = "Gain a +2 bonus."
            });
            var secondSemantic = JsonSerializer.Serialize(new
            {
                label = name,
                mechanics = new { bonus = 2 }
            });

            var first = await importer.ImportAsync(Request(
                $"alias-first-{token}",
                "fixture-format-a",
                name,
                "native-a",
                firstSemantic,
                firstSemantic));
            var firstBinding = await ReadLatestBindingAsync(db, Assert.Single(first.Entities).EntityId);
            Assert.NotNull(firstBinding);

            const string scheme = "fixture-lineage-b";
            var aliasValue = $"native-b-{token}";
            var secondFingerprint = CanonicalSourceIdentity.SemanticFingerprint(secondSemantic);
            var aliases = new CanonicalEntityAliasStore(db);
            await aliases.RegisterAsync(
                firstBinding.Value.CanonicalEntityId,
                scheme,
                aliasValue,
                secondFingerprint,
                "bootstrap-confirmed-exact-identity",
                1.0);

            var second = await importer.ImportAsync(Request(
                $"alias-second-{token}",
                "fixture-format-b",
                name,
                "native-b",
                JsonSerializer.Serialize(new
                {
                    sourceShape = "B",
                    name,
                    bonus = 2
                }),
                secondSemantic,
                new Dictionary<string, string> { [scheme] = aliasValue }));
            var secondBinding = await ReadLatestBindingAsync(db, Assert.Single(second.Entities).EntityId);
            Assert.NotNull(secondBinding);

            Assert.NotEqual(firstBinding.Value.SemanticFingerprint, secondBinding.Value.SemanticFingerprint);
            Assert.Equal(firstBinding.Value.CanonicalEntityId, secondBinding.Value.CanonicalEntityId);
            Assert.EndsWith(":strong-alias", secondBinding.Value.MatchKind, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task StrongAliasCanNotBeAssignedToTwoCanonicalEntitiesForTheSameSourceProjection()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var importer = new NormalizedSourceImportService(db);
            var first = await importer.ImportAsync(Request(
                $"alias-conflict-a-{token}",
                "fixture-format-a",
                $"Alias Conflict A {token}",
                "native-a",
                "{\"effect\":\"A\"}",
                "{\"effect\":\"A\"}"));
            var second = await importer.ImportAsync(Request(
                $"alias-conflict-b-{token}",
                "fixture-format-b",
                $"Alias Conflict B {token}",
                "native-b",
                "{\"effect\":\"B\"}",
                "{\"effect\":\"B\"}"));
            var firstBinding = await ReadLatestBindingAsync(db, Assert.Single(first.Entities).EntityId);
            var secondBinding = await ReadLatestBindingAsync(db, Assert.Single(second.Entities).EntityId);
            Assert.NotNull(firstBinding);
            Assert.NotNull(secondBinding);
            Assert.NotEqual(firstBinding.Value.CanonicalEntityId, secondBinding.Value.CanonicalEntityId);

            var fingerprint = CanonicalSourceIdentity.SemanticFingerprint("{\"projection\":42}");
            var store = new CanonicalEntityAliasStore(db);
            var scheme = $"conflict-{token}";
            var value = $"native-{token}";
            await store.RegisterAsync(
                firstBinding.Value.CanonicalEntityId,
                scheme,
                value,
                fingerprint,
                "integration-test",
                0.9);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.RegisterAsync(
                    secondBinding.Value.CanonicalEntityId,
                    scheme,
                    value,
                    fingerprint,
                    "integration-test",
                    1.0));
            Assert.Contains("different canonical entity", exception.Message, StringComparison.Ordinal);
            Assert.Equal(
                firstBinding.Value.CanonicalEntityId,
                await store.ResolveAsync(scheme, value, fingerprint));
        }
    }

    [Fact]
    public async Task StrongAliasCanNotCollapseMechanicalRevisionIntoPriorCanonicalEntity()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var importer = new NormalizedSourceImportService(db);
            var packageKey = $"alias-revision-{token}";
            var name = $"Revision Alias Rule {token}";
            var firstSemantic = "{\"effect\":\"Gain a +2 bonus.\"}";
            var secondSemantic = "{\"effect\":\"Gain a +3 bonus.\"}";

            var first = await importer.ImportAsync(Request(
                packageKey,
                "fixture-revision-format",
                name,
                "stable-native-key",
                "{\"effect\":\"Gain a +2 bonus.\",\"page\":1}",
                firstSemantic,
                originIdentity: $"test:{packageKey}"));
            var firstBinding = await ReadLatestBindingAsync(db, Assert.Single(first.Entities).EntityId);
            Assert.NotNull(firstBinding);

            var scheme = $"revision-{token}";
            const string aliasValue = "stable-upstream-key";
            var secondFingerprint = CanonicalSourceIdentity.SemanticFingerprint(secondSemantic);
            await new CanonicalEntityAliasStore(db).RegisterAsync(
                firstBinding.Value.CanonicalEntityId,
                scheme,
                aliasValue,
                secondFingerprint,
                "deliberately-invalid-collapse-fixture",
                1.0);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                importer.ImportAsync(Request(
                    packageKey,
                    "fixture-revision-format",
                    name,
                    "stable-native-key",
                    "{\"effect\":\"Gain a +3 bonus.\",\"page\":1}",
                    secondSemantic,
                    new Dictionary<string, string> { [scheme] = aliasValue },
                    originIdentity: $"test:{packageKey}")));
            Assert.Contains("can not collapse", exception.Message, StringComparison.Ordinal);
        }
    }

    private static ImportNormalizedSourceRequest Request(
        string packageKey,
        string formatKey,
        string name,
        string nativeKey,
        string rawJson,
        string semanticJson,
        IReadOnlyDictionary<string, string>? aliases = null,
        string? originIdentity = null)
    {
        const string publicationKey = "canonical-alias-book";
        return new ImportNormalizedSourceRequest(
            packageKey,
            packageKey,
            "integration-test",
            License: null,
            IsPublic: false,
            new NormalizedSourceRepresentation(
                formatKey,
                new SourceRepresentationArtifact(
                    $"{packageKey}.json",
                    Encoding.UTF8.GetBytes(rawJson),
                    originIdentity ?? $"test:{packageKey}:{Guid.NewGuid():N}"),
                [new NormalizedSourceRecord(
                    "rule",
                    name,
                    SourceCode: "ALIAS",
                    NativeKey: nativeKey,
                    RawJson: rawJson,
                    LocatorKey: "entry:1",
                    PublicationLocalKey: publicationKey,
                    SemanticJson: semanticJson,
                    CanonicalAliases: aliases)],
                [new NormalizedSourcePublication(
                    publicationKey,
                    "Canonical Alias Fixture Book",
                    Publisher: "Example Press",
                    GameEdition: "3.5e",
                    PublicationDate: new DateOnly(2003, 7, 1),
                    ExternalIdentifiers: new Dictionary<string, string>
                    {
                        ["isbn"] = "9781402894626"
                    })]));
    }

    private static async Task<(Guid CanonicalEntityId, string SemanticFingerprint, string MatchKind)?> ReadLatestBindingAsync(
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
                SELECT occurrence.canonical_entity_id, binding.semantic_fingerprint, binding.match_kind
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
                ? (reader.GetGuid(0), reader.GetString(1), reader.GetString(2))
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
