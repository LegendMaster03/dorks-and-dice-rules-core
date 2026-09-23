using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class DeterministicSourceSchemaIntegrationTests
{
    [Fact]
    public async Task InitializerCreatesCurrentSchemaWithoutServiceSideMigrations()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var schema = $"rules_core_schema_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using (var create = admin.CreateCommand())
        {
            create.CommandText = $"CREATE SCHEMA {schema};";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var isolatedConnectionString = $"{connectionString};Search Path={schema}";
            await using var db = new RulesCoreDbContext(
                new DbContextOptionsBuilder<RulesCoreDbContext>()
                    .UseNpgsql(isolatedConnectionString)
                    .Options);

            await new RulesCoreSchemaInitializer(db).InitializeAsync();

            Assert.True(await RelationExistsAsync(db, "canonical_entity"));
            Assert.True(await RelationExistsAsync(db, "source_revision_rejection"));
            Assert.True(await RelationExistsAsync(db, "rule_mechanical_relationship_ruling"));
            Assert.Equal("YES", await ColumnNullableAsync(
                db,
                schema,
                "canonical_source_occurrence",
                "canonical_entity_id"));
            Assert.Equal("NO", await ColumnNullableAsync(
                db,
                schema,
                "source_entity_occurrence_binding",
                "source_entity_revision_id"));
            Assert.Equal("NO", await ColumnNullableAsync(
                db,
                schema,
                "source_entity_revision",
                "normalization_version"));
            Assert.Equal("NO", await ColumnNullableAsync(
                db,
                schema,
                "source_entity_revision",
                "normalization_attempt_version"));
            Assert.Equal("YES", await ColumnNullableAsync(
                db,
                schema,
                "source_entity_revision",
                "normalization_error"));
            Assert.Equal("NO", await ColumnNullableAsync(
                db,
                schema,
                "rule_concept_source_binding",
                "canonical_entity_id"));
            Assert.Equal("YES", await ColumnNullableAsync(
                db,
                schema,
                "rule_concept_source_binding",
                "source_entity_id"));
            Assert.False(await IndexIsUniqueAsync(
                db,
                schema,
                "source_entity_occurrence_binding",
                "source_entity_id"));
            Assert.True(await IndexExistsAsync(
                db,
                schema,
                "ux_source_entity_occurrence_binding_revision"));
            Assert.True(await IndexExistsAsync(
                db,
                schema,
                "ix_source_entity_revision_normalization"));
            Assert.True(await IndexExistsAsync(
                db,
                schema,
                "ux_rule_concept_source_binding_concept_canonical_entity"));
            Assert.True(await IndexExistsAsync(
                db,
                schema,
                "ux_rule_mechanical_relationship_ruling_number"));
            Assert.True(await IndexExistsAsync(
                db,
                schema,
                "ix_rule_mechanical_relationship_ruling_latest"));
        }
        finally
        {
            await using var drop = admin.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS {schema} CASCADE;";
            await drop.ExecuteNonQueryAsync();
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

    private static async Task<bool> IndexIsUniqueAsync(
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
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_indexes
                    WHERE schemaname = @schema
                        AND tablename = @table
                        AND indexdef ILIKE 'CREATE UNIQUE INDEX%'
                        AND indexdef LIKE '%' || @column || '%');
                """;
            AddParameter(command, "@schema", schema);
            AddParameter(command, "@table", table);
            AddParameter(command, "@column", column);
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
