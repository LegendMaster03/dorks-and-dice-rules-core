using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CanonicalSchemaRestartIntegrationTests
{
    [Fact]
    public async Task ReinitializationPreservesMultipleRevisionOccurrenceBindings()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);
        var initializer = new RulesCoreSchemaInitializer(db);
        await initializer.InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..12];
        var packageKey = $"canonical-restart-{token}";
        try
        {
            var importer = new NormalizedSourceImportService(db);
            var first = await importer.ImportAsync(Request(packageKey, token, 1));
            var entityId = Assert.Single(first.Entities).EntityId;
            var second = await importer.ImportAsync(Request(packageKey, token, 2));
            Assert.Equal(entityId, Assert.Single(second.Entities).EntityId);
            Assert.Equal(2, await db.SourceEntityRevisions.CountAsync(value => value.SourceEntityId == entityId));
            Assert.Equal(2, await CountOccurrenceBindingsAsync(db, entityId));

            await initializer.InitializeAsync();

            Assert.Equal(2, await db.SourceEntityRevisions.CountAsync(value => value.SourceEntityId == entityId));
            Assert.Equal(2, await CountOccurrenceBindingsAsync(db, entityId));
        }
        finally
        {
            db.ChangeTracker.Clear();
            await db.SourcePackages.Where(value => value.Key == packageKey).ExecuteDeleteAsync();
        }
    }

    private static ImportNormalizedSourceRequest Request(string packageKey, string token, int version)
    {
        var rawJson = $$"""
            {
              "name": "Restart Rule {{token}}",
              "source": "RST{{token}}",
              "version": {{version}}
            }
            """;
        var localKey = $"restart-publication-{token}";
        return new ImportNormalizedSourceRequest(
            packageKey,
            $"Restart package {token}",
            "integration-test",
            License: "test-only",
            IsPublic: false,
            new NormalizedSourceRepresentation(
                FiveEToolsSourceFormatAdapter.Format,
                new SourceRepresentationArtifact(
                    $"restart-{token}.json",
                    Encoding.UTF8.GetBytes(rawJson),
                    $"integration:restart:{token}"),
                [new NormalizedSourceRecord(
                    "rule",
                    $"Restart Rule {token}",
                    $"RST{token}",
                    NativeKey: $"rule|RST{token}|Restart Rule {token}",
                    RawJson: rawJson,
                    PublicationLocalKey: localKey)],
                [new NormalizedSourcePublication(
                    localKey,
                    $"Restart Publication {token}",
                    Publisher: "Integration Test Press",
                    GameEdition: "5e",
                    ExternalIdentifiers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["isbn"] = $"test-restart-{token}"
                    })]));
    }

    private static async Task<int> CountOccurrenceBindingsAsync(RulesCoreDbContext db, Guid sourceEntityId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM source_entity_occurrence_binding
                WHERE source_entity_id = @source_entity_id;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
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
}
