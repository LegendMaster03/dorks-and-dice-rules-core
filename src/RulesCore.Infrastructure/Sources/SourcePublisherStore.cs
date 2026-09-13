using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

internal static class SourcePublisherStore
{
    private const string SchemaSql = """
        ALTER TABLE source_edition_metadata
            ADD COLUMN IF NOT EXISTS publisher varchar(300) NULL;
        CREATE INDEX IF NOT EXISTS ix_source_edition_metadata_publisher
            ON source_edition_metadata(publisher);
        """;

    public static async Task EnsureSchemaAsync(
        RulesCoreDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        await SourceFrameworkStore.EnsureSchemaAsync(dbContext, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(SchemaSql, cancellationToken);
    }

    public static async Task<string?> GetPublisherAsync(
        RulesCoreDbContext dbContext,
        Guid sourceEditionId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(dbContext, cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = CreateCommand(dbContext, connection);
            command.CommandText = """
                SELECT publisher
                FROM source_edition_metadata
                WHERE source_edition_id = @edition_id;
                """;
            AddParameter(command, "@edition_id", sourceEditionId);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is null or DBNull ? null : (string)value;
        }
        finally
        {
            if (openedHere && dbContext.Database.CurrentTransaction is null)
            {
                await connection.CloseAsync();
            }
        }
    }

    public static async Task<string?> MergePublisherAsync(
        RulesCoreDbContext dbContext,
        Guid sourceEditionId,
        string? publisher,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(dbContext, cancellationToken);
        var normalized = NormalizePublisher(publisher);
        var existing = await GetPublisherAsync(dbContext, sourceEditionId, cancellationToken);
        if (existing is not null && normalized is not null
            && !string.Equals(existing, normalized, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The source release already records publisher '{existing}', which conflicts with requested value '{normalized}'.");
        }

        var effective = existing ?? normalized;
        if (effective is null)
        {
            return null;
        }

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = CreateCommand(dbContext, connection);
            command.CommandText = """
                UPDATE source_edition_metadata
                SET publisher = @publisher
                WHERE source_edition_id = @edition_id;
                """;
            AddParameter(command, "@publisher", effective);
            AddParameter(command, "@edition_id", sourceEditionId);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return effective;
        }
        finally
        {
            if (openedHere && dbContext.Database.CurrentTransaction is null)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static DbCommand CreateCommand(RulesCoreDbContext dbContext, DbConnection connection)
    {
        var command = connection.CreateCommand();
        var transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        if (transaction is not null)
        {
            command.Transaction = transaction;
        }
        return command;
    }

    private static string? NormalizePublisher(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var normalized = value.Trim();
        if (normalized.Length > 300)
        {
            throw new ArgumentException("Publisher can not exceed 300 characters.", nameof(value));
        }
        return normalized;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
