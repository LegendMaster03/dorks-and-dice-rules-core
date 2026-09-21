using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CanonicalSchemaConcurrencyIntegrationTests
{
    private const string ControlledCreatureInitiativeKey = "house.controlled-creature-initiative";

    [Fact]
    public async Task ConcurrentInitializersAcrossSeparateConnectionsAreIdempotent()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var schema = $"rules_core_schema_concurrency_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await CreateSchemaAsync(admin, schema);

        try
        {
            var isolatedConnectionString = $"{connectionString};Search Path={schema}";
            var initializers = Enumerable.Range(0, 4)
                .Select(async _ =>
                {
                    await using var db = CreateDb(isolatedConnectionString);
                    await new RulesCoreSchemaInitializer(db).InitializeAsync();
                })
                .ToArray();

            await Task.WhenAll(initializers);

            await using var verify = CreateDb(isolatedConnectionString);
            Assert.True(await RelationExistsAsync(verify, "rules_core_schema_revision"));
            Assert.Equal(1, await CountSchemaRevisionsAsync(verify));
            Assert.True(await RelationExistsAsync(verify, "canonical_entity"));
            Assert.True(await RelationExistsAsync(verify, "canonical_entity_relationship"));
            Assert.True(await RelationExistsAsync(verify, "source_entity_lineage"));
            Assert.True(await RelationExistsAsync(verify, "global_rule_decision_contribution"));
            Assert.True(await RelationExistsAsync(verify, "rule_concept_relationship"));
            Assert.True(await RelationExistsAsync(verify, "rule_concept_relationship_backfill"));
            Assert.Equal(
                "NO",
                await ColumnNullableAsync(
                    verify,
                    schema,
                    "rule_concept_source_binding",
                    "canonical_entity_id"));
            Assert.True(await IndexExistsAsync(
                verify,
                schema,
                "ux_rule_concept_source_binding_concept_canonical_entity"));
        }
        finally
        {
            await DropSchemaAsync(admin, schema);
        }
    }

    [Fact]
    public async Task ConcurrentVersionsReadsDoNotMutateCanonicalSchemaOrBindings()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var schema = $"rules_core_versions_concurrency_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await CreateSchemaAsync(admin, schema);

        try
        {
            var isolatedConnectionString = $"{connectionString};Search Path={schema}";
            var token = Guid.NewGuid().ToString("N")[..12];
            Guid conceptId;
            Guid sourceEntityId;
            Guid sourceRevisionId;
            Guid bindingId;
            Guid canonicalEntityId;
            Guid? legacySourceEntityId;

            await using (var setup = CreateDb(isolatedConnectionString))
            {
                await new RulesCoreSchemaInitializer(setup).InitializeAsync();

                var importer = new NormalizedSourceImportService(setup);
                var imported = await importer.ImportAsync(CreateRuleSourceRequest(token));
                var source = Assert.Single(imported.Entities);
                sourceEntityId = source.EntityId;
                sourceRevisionId = await setup.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == sourceEntityId)
                    .OrderByDescending(value => value.RevisionNumber)
                    .Select(value => value.Id)
                    .FirstAsync();

                const string actor = "canonical-schema-concurrency-test";
                var rules = new GlobalRulesService(setup);
                var concept = (await rules.CreateConceptAsync(
                    new CreateRuleConceptRequest(
                        ControlledCreatureInitiativeKey,
                        "rule",
                        "Controlled Creature Initiative"),
                    actor)).Value;
                conceptId = concept.Id;

                await rules.BindSourceEntityAsync(
                    concept.Id,
                    new BindRuleConceptSourceRequest(sourceEntityId),
                    actor);
                await rules.SetDecisionAsync(
                    concept.Id,
                    new SetGlobalRuleDecisionRequest(
                        sourceRevisionId,
                        "Canonical schema concurrency fixture."),
                    actor);
                await rules.PublishAsync(actor);

                var binding = await setup.RuleConceptSourceBindings
                    .AsNoTracking()
                    .SingleAsync(value => value.RuleConceptId == concept.Id);
                bindingId = binding.Id;
                canonicalEntityId = binding.CanonicalEntityId;
                legacySourceEntityId = binding.SourceEntityId;
            }

            var reads = Enumerable.Range(0, 8)
                .Select(async _ =>
                {
                    await using var readDb = CreateDb(isolatedConnectionString);
                    var versions = await new GlobalRulesService(readDb)
                        .GetAccessibleVersionsAsync(ControlledCreatureInitiativeKey, null);

                    Assert.NotNull(versions);
                    Assert.Contains(
                        versions.Versions,
                        value => value.SourceEntityId == sourceEntityId
                            && value.SourceEntityRevisionId == sourceRevisionId);
                })
                .ToArray();

            await Task.WhenAll(reads);

            await using (var verify = CreateDb(isolatedConnectionString))
            {
                var bindings = await verify.RuleConceptSourceBindings
                    .AsNoTracking()
                    .Where(value => value.RuleConceptId == conceptId)
                    .ToArrayAsync();
                var binding = Assert.Single(bindings);
                Assert.Equal(bindingId, binding.Id);
                Assert.Equal(canonicalEntityId, binding.CanonicalEntityId);
                Assert.Equal(legacySourceEntityId, binding.SourceEntityId);
            }

            await using (var restart = CreateDb(isolatedConnectionString))
            {
                await new RulesCoreSchemaInitializer(restart).InitializeAsync();
                var versions = await new GlobalRulesService(restart)
                    .GetAccessibleVersionsAsync(ControlledCreatureInitiativeKey, null);

                Assert.NotNull(versions);
                Assert.Contains(
                    versions.Versions,
                    value => value.SourceEntityId == sourceEntityId
                        && value.SourceEntityRevisionId == sourceRevisionId);

                var bindings = await restart.RuleConceptSourceBindings
                    .AsNoTracking()
                    .Where(value => value.RuleConceptId == conceptId)
                    .ToArrayAsync();
                var binding = Assert.Single(bindings);
                Assert.Equal(bindingId, binding.Id);
                Assert.Equal(canonicalEntityId, binding.CanonicalEntityId);
                Assert.Equal(legacySourceEntityId, binding.SourceEntityId);
                Assert.Equal(1, await CountSchemaRevisionsAsync(restart));
            }
        }
        finally
        {
            await DropSchemaAsync(admin, schema);
        }
    }

    private static RulesCoreDbContext CreateDb(string connectionString) =>
        new(
            new DbContextOptionsBuilder<RulesCoreDbContext>()
                .UseNpgsql(connectionString)
                .Options);

    private static ImportNormalizedSourceRequest CreateRuleSourceRequest(string token)
    {
        var rawJson = $$"""
            {
              "name": "Controlled Creature Initiative {{token}}",
              "source": "CCI{{token}}",
              "initiative": "controlled-creature-uses-controller-initiative"
            }
            """;
        var packageKey = $"canonical-schema-concurrency-{token}";
        var localKey = $"controlled-creature-initiative-{token}";

        return new ImportNormalizedSourceRequest(
            packageKey,
            $"Canonical schema concurrency {token}",
            "integration-test",
            License: "test-only",
            IsPublic: true,
            new NormalizedSourceRepresentation(
                FiveEToolsSourceFormatAdapter.Format,
                new SourceRepresentationArtifact(
                    $"controlled-creature-initiative-{token}.json",
                    Encoding.UTF8.GetBytes(rawJson),
                    $"integration:canonical-schema-concurrency:{token}"),
                [new NormalizedSourceRecord(
                    "rule",
                    $"Controlled Creature Initiative {token}",
                    $"CCI{token}",
                    NativeKey: $"rule|CCI{token}|Controlled Creature Initiative {token}",
                    RawJson: rawJson,
                    PublicationLocalKey: localKey)],
                [new NormalizedSourcePublication(
                    localKey,
                    $"Controlled Creature Initiative publication {token}",
                    Publisher: "Integration Test Press",
                    GameEdition: "5.5e",
                    ExternalIdentifiers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["isbn"] = $"test-canonical-schema-{token}"
                    })]));
    }

    private static async Task CreateSchemaAsync(NpgsqlConnection connection, string schema)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE SCHEMA {schema};";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropSchemaAsync(NpgsqlConnection connection, string schema)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP SCHEMA IF EXISTS {schema} CASCADE;";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountSchemaRevisionsAsync(RulesCoreDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM rules_core_schema_revision;";
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<bool> RelationExistsAsync(RulesCoreDbContext db, string relationName)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT to_regclass(@relation_name) IS NOT NULL;";
            AddParameter(command, "@relation_name", relationName);
            return Convert.ToBoolean(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<string?> ColumnNullableAsync(
        RulesCoreDbContext db,
        string schema,
        string table,
        string column)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT is_nullable
                FROM information_schema.columns
                WHERE table_schema = @schema
                    AND table_name = @table
                    AND column_name = @column;
                """;
            AddParameter(command, "@schema", schema);
            AddParameter(command, "@table", table);
            AddParameter(command, "@column", column);
            return (string?)await command.ExecuteScalarAsync();
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<bool> IndexExistsAsync(
        RulesCoreDbContext db,
        string schema,
        string indexName)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_indexes
                    WHERE schemaname = @schema
                        AND indexname = @index_name);
                """;
            AddParameter(command, "@schema", schema);
            AddParameter(command, "@index_name", indexName);
            return Convert.ToBoolean(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<bool> EnsureOpenAsync(DbConnection connection)
    {
        if (connection.State == ConnectionState.Open) return false;
        await connection.OpenAsync();
        return true;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
