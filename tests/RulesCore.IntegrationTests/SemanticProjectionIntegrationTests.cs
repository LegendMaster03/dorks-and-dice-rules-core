using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class SemanticProjectionIntegrationTests
{
    [Fact]
    public async Task SemanticProjectionPreservesRawRevisionsWithoutSplittingCanonicalIdentity()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<RulesCoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new RulesCoreDbContext(options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..12];
        var packageKey = $"semantic-projection-{token}";
        var publicationKey = $"publication-{token}";
        var importer = new NormalizedSourceImportService(db);

        try
        {
            var first = await importer.ImportAsync(Request(
                packageKey,
                publicationKey,
                token,
                "p. 10",
                "line:10"));
            var second = await importer.ImportAsync(Request(
                packageKey,
                publicationKey,
                token,
                "p. 11",
                "line:11"));

            var firstEntity = Assert.Single(first.Entities);
            var secondEntity = Assert.Single(second.Entities);
            Assert.Equal(firstEntity.EntityId, secondEntity.EntityId);
            Assert.Equal(1, firstEntity.RevisionNumber);
            Assert.Equal(2, secondEntity.RevisionNumber);
            Assert.NotEqual(firstEntity.Fingerprint, secondEntity.Fingerprint);

            var rows = await ReadBindingsAsync(db, firstEntity.EntityId);
            Assert.Equal(2, rows.Count);
            Assert.Equal(rows[0].CanonicalEntityId, rows[1].CanonicalEntityId);
            Assert.Equal(rows[0].SemanticFingerprint, rows[1].SemanticFingerprint);

            var revisions = await db.SourceEntityRevisions
                .AsNoTracking()
                .Where(value => value.SourceEntityId == firstEntity.EntityId)
                .OrderBy(value => value.RevisionNumber)
                .Select(value => value.RawJson)
                .ToArrayAsync();
            Assert.Contains("p. 10", revisions[0], StringComparison.Ordinal);
            Assert.Contains("p. 11", revisions[1], StringComparison.Ordinal);
        }
        finally
        {
            var package = await db.SourcePackages.SingleOrDefaultAsync(value => value.Key == packageKey);
            if (package is not null)
            {
                db.SourcePackages.Remove(package);
                await db.SaveChangesAsync();
            }
        }
    }

    private static ImportNormalizedSourceRequest Request(
        string packageKey,
        string publicationKey,
        string token,
        string sourcePage,
        string locator) =>
        new(
            packageKey,
            $"Semantic projection package {token}",
            "integration-test",
            "test-only",
            false,
            new NormalizedSourceRepresentation(
                "projection-test",
                new SourceRepresentationArtifact(
                    $"projection-{token}.json",
                    Encoding.UTF8.GetBytes($"{{\"sourcePage\":\"{sourcePage}\"}}"),
                    $"test:semantic-projection:{token}"),
                [new NormalizedSourceRecord(
                    "rule",
                    "Projected Rule",
                    "PROJ",
                    $"rule|{token}",
                    $"{{\"name\":\"Projected Rule\",\"effect\":\"Gain a +2 bonus.\",\"sourcePage\":\"{sourcePage}\"}}",
                    LocatorKey: locator,
                    PublicationLocalKey: publicationKey,
                    NativeIdentityJson: "{}",
                    SemanticJson: "{\"name\":\"Projected Rule\",\"effect\":\"Gain a +2 bonus.\"}")],
                [new NormalizedSourcePublication(
                    publicationKey,
                    $"Semantic Projection Publication {token}",
                    "Integration Test Press",
                    "3.5e",
                    new DateOnly(2006, 1, 1),
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["isbn"] = $"test-semantic-projection-{token}"
                    })]));

    private static async Task<IReadOnlyList<BindingRow>> ReadBindingsAsync(
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
                SELECT
                    occurrence.canonical_entity_id,
                    binding.semantic_fingerprint
                FROM source_entity_revision revision
                JOIN source_entity_occurrence_binding binding
                    ON binding.source_entity_revision_id = revision.source_entity_revision_id
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                WHERE revision.source_entity_id = @source_entity_id
                ORDER BY revision.revision_number;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            var rows = new List<BindingRow>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new BindingRow(reader.GetGuid(0), reader.GetString(1)));
            }
            return rows;
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

    private sealed record BindingRow(Guid CanonicalEntityId, string SemanticFingerprint);
}
