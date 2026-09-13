using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

public sealed class GlobalSourceDispositionService(RulesCoreDbContext dbContext)
{
    public async Task<IReadOnlyList<GlobalIgnoredSourceView>> GetIgnoredAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    package.source_package_id,
                    package.package_key,
                    package.display_name,
                    package.provider,
                    disposition.reason,
                    disposition.ignored_by_user_id,
                    disposition.ignored_at
                FROM global_source_disposition disposition
                JOIN source_package package
                    ON package.source_package_id = disposition.source_package_id
                WHERE disposition.restored_at IS NULL
                ORDER BY package.display_name, package.package_key;
                """;

            var rows = new List<GlobalIgnoredSourceView>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new GlobalIgnoredSourceView(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetString(5),
                    reader.GetFieldValue<DateTimeOffset>(6)));
            }
            return rows;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<Guid[]> GetIgnoredPackageIdsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT source_package_id
                FROM global_source_disposition
                WHERE restored_at IS NULL
                ORDER BY source_package_id;
                """;
            var ids = new List<Guid>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }
            return ids.ToArray();
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<GlobalIgnoredSourceView?> SetIgnoredAsync(
        Guid sourcePackageId,
        SetGlobalSourceIgnoredRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (sourcePackageId == Guid.Empty)
        {
            throw new ArgumentException("Source package ID can not be empty.", nameof(sourcePackageId));
        }
        ArgumentNullException.ThrowIfNull(request);
        var actor = RequireActor(actorUserId);
        var reason = NormalizeReason(request.Reason);
        await EnsureSchemaAsync(cancellationToken);

        var package = await dbContext.SourcePackages
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == sourcePackageId, cancellationToken);
        if (package is null)
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
            var now = DateTimeOffset.UtcNow;
            if (request.Ignored)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO global_source_disposition (
                        global_source_disposition_id,
                        source_package_id,
                        reason,
                        ignored_by_user_id,
                        ignored_at,
                        restored_by_user_id,
                        restored_at)
                    VALUES (
                        @id,
                        @source_package_id,
                        @reason,
                        @actor,
                        @now,
                        NULL,
                        NULL)
                    ON CONFLICT (source_package_id) DO UPDATE SET
                        reason = EXCLUDED.reason,
                        ignored_by_user_id = EXCLUDED.ignored_by_user_id,
                        ignored_at = EXCLUDED.ignored_at,
                        restored_by_user_id = NULL,
                        restored_at = NULL;
                    """;
                AddParameter(command, "@id", Guid.NewGuid());
                AddParameter(command, "@source_package_id", sourcePackageId);
                AddNullableParameter(command, "@reason", reason);
                AddParameter(command, "@actor", actor);
                AddParameter(command, "@now", now);
                await command.ExecuteNonQueryAsync(cancellationToken);

                return new GlobalIgnoredSourceView(
                    package.Id,
                    package.Key,
                    package.DisplayName,
                    package.Provider,
                    reason,
                    actor,
                    now);
            }

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    UPDATE global_source_disposition
                    SET restored_by_user_id = @actor,
                        restored_at = @now
                    WHERE source_package_id = @source_package_id
                        AND restored_at IS NULL;
                    """;
                AddParameter(command, "@source_package_id", sourcePackageId);
                AddParameter(command, "@actor", actor);
                AddParameter(command, "@now", now);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            return null;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<bool> IsIgnoredAsync(
        Guid sourcePackageId,
        CancellationToken cancellationToken = default)
    {
        return (await GetIgnoredPackageIdsAsync(cancellationToken)).Contains(sourcePackageId);
    }

    public static Task EnsureSchemaAsync(
        RulesCoreDbContext dbContext,
        CancellationToken cancellationToken = default) =>
        dbContext.Database.ExecuteSqlRawAsync(SchemaSql, cancellationToken);

    private Task EnsureSchemaAsync(CancellationToken cancellationToken) =>
        EnsureSchemaAsync(dbContext, cancellationToken);

    private static string RequireActor(string actorUserId)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            throw new ArgumentException("Actor user ID can not be blank.", nameof(actorUserId));
        }
        var actor = actorUserId.Trim();
        if (actor.Length > 200)
        {
            throw new ArgumentException("Actor user ID can not exceed 200 characters.", nameof(actorUserId));
        }
        return actor;
    }

    private static string? NormalizeReason(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var normalized = value.Trim();
        if (normalized.Length > 1000)
        {
            throw new ArgumentException("Ignore reason can not exceed 1000 characters.", nameof(value));
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

    private static void AddNullableParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS global_source_disposition (
            global_source_disposition_id uuid NOT NULL,
            source_package_id uuid NOT NULL,
            reason varchar(1000) NULL,
            ignored_by_user_id varchar(200) NOT NULL,
            ignored_at timestamp with time zone NOT NULL,
            restored_by_user_id varchar(200) NULL,
            restored_at timestamp with time zone NULL,
            CONSTRAINT pk_global_source_disposition PRIMARY KEY (global_source_disposition_id),
            CONSTRAINT fk_global_source_disposition_package FOREIGN KEY (source_package_id)
                REFERENCES source_package(source_package_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_global_source_disposition_package
            ON global_source_disposition(source_package_id);
        CREATE INDEX IF NOT EXISTS ix_global_source_disposition_active
            ON global_source_disposition(source_package_id)
            WHERE restored_at IS NULL;
        """;
}