using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Persists canonical reconciliation issues against immutable source representations.
/// Issue metadata is shared recognition state only; callers still need access to the
/// package-owned current-user registration before it is returned through user-facing APIs.
/// </summary>
public sealed class SourceReconciliationIssueService(RulesCoreDbContext dbContext)
{
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.ExecuteSqlRawAsync(SchemaSql, cancellationToken);

    internal async Task RecordAsync(
        Guid sourceRepresentationId,
        NormalizedSourceReconciliationIssue issue,
        CancellationToken cancellationToken = default)
    {
        if (sourceRepresentationId == Guid.Empty)
        {
            throw new ArgumentException("Source representation ID can not be empty.", nameof(sourceRepresentationId));
        }
        ArgumentNullException.ThrowIfNull(issue);
        var kind = Require(issue.Kind, nameof(issue.Kind), 80);
        var localKey = Require(issue.PublicationLocalKey, nameof(issue.PublicationLocalKey), 500);
        var displayName = Require(issue.PublicationDisplayName, nameof(issue.PublicationDisplayName), 500);
        var message = Require(issue.Message, nameof(issue.Message), 2000);
        var sourceEntityIds = issue.SourceEntityIds
            .Where(value => value != Guid.Empty)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        if (sourceEntityIds.Length == 0)
        {
            throw new ArgumentException("A reconciliation issue must identify at least one source entity.", nameof(issue));
        }

        await EnsureSchemaAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await ResolvePriorLineageIssuesAsync(
                connection,
                sourceRepresentationId,
                localKey,
                now,
                cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO source_reconciliation_issue (
                    source_reconciliation_issue_id,
                    source_representation_id,
                    issue_kind,
                    publication_local_key,
                    publication_display_name,
                    source_entity_ids_json,
                    message,
                    recorded_at,
                    resolved_at)
                VALUES (
                    @id,
                    @representation_id,
                    @issue_kind,
                    @publication_local_key,
                    @publication_display_name,
                    CAST(@source_entity_ids_json AS jsonb),
                    @message,
                    @recorded_at,
                    NULL)
                ON CONFLICT (source_representation_id, issue_kind, publication_local_key)
                DO UPDATE SET
                    publication_display_name = EXCLUDED.publication_display_name,
                    source_entity_ids_json = EXCLUDED.source_entity_ids_json,
                    message = EXCLUDED.message,
                    recorded_at = EXCLUDED.recorded_at,
                    resolved_at = NULL;
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@representation_id", sourceRepresentationId);
            AddParameter(command, "@issue_kind", kind);
            AddParameter(command, "@publication_local_key", localKey);
            AddParameter(command, "@publication_display_name", displayName);
            AddParameter(command, "@source_entity_ids_json", JsonSerializer.Serialize(sourceEntityIds));
            AddParameter(command, "@message", message);
            AddParameter(command, "@recorded_at", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    internal async Task ResolveAsync(
        Guid sourceRepresentationId,
        string publicationLocalKey,
        CancellationToken cancellationToken = default)
    {
        if (sourceRepresentationId == Guid.Empty)
        {
            throw new ArgumentException("Source representation ID can not be empty.", nameof(sourceRepresentationId));
        }
        var localKey = Require(publicationLocalKey, nameof(publicationLocalKey), 500);
        await EnsureSchemaAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                WITH target AS (
                    SELECT source_package_id, origin_identity
                    FROM source_representation
                    WHERE source_representation_id = @representation_id
                )
                UPDATE source_reconciliation_issue issue
                SET resolved_at = @resolved_at
                FROM source_representation representation, target
                WHERE issue.source_representation_id = representation.source_representation_id
                    AND representation.source_package_id = target.source_package_id
                    AND representation.origin_identity = target.origin_identity
                    AND issue.publication_local_key = @publication_local_key
                    AND issue.resolved_at IS NULL;
                """;
            AddParameter(command, "@resolved_at", DateTimeOffset.UtcNow);
            AddParameter(command, "@representation_id", sourceRepresentationId);
            AddParameter(command, "@publication_local_key", localKey);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public async Task<IReadOnlyList<NormalizedSourceReconciliationIssue>?> ListForCurrentUserSourceAsync(
        string currentUserId,
        Guid currentUserSourceId,
        CancellationToken cancellationToken = default)
    {
        var userId = Require(currentUserId, nameof(currentUserId), 200);
        if (currentUserSourceId == Guid.Empty)
        {
            throw new ArgumentException("Current-user source ID can not be empty.", nameof(currentUserSourceId));
        }
        await EnsureSchemaAsync(cancellationToken);
        if (!await TableExistsAsync("current_user_source", cancellationToken)) return null;

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        Guid? packageId;
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT source_package_id
                FROM current_user_source
                WHERE current_user_source_id = @source_id
                    AND user_id = @user_id;
                """;
            AddParameter(command, "@source_id", currentUserSourceId);
            AddParameter(command, "@user_id", userId);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            packageId = value is null or DBNull ? null : (Guid)value;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }

        return packageId.HasValue
            ? await ListForPackageAsync(packageId.Value, cancellationToken)
            : null;
    }

    public async Task<IReadOnlyList<NormalizedSourceReconciliationIssue>?> ListForCurrentUserImportJobAsync(
        string currentUserId,
        Guid importJobId,
        CancellationToken cancellationToken = default)
    {
        var userId = Require(currentUserId, nameof(currentUserId), 200);
        if (importJobId == Guid.Empty)
        {
            throw new ArgumentException("Import job ID can not be empty.", nameof(importJobId));
        }
        await EnsureSchemaAsync(cancellationToken);
        if (!await TableExistsAsync("current_user_source_import_job", cancellationToken)) return null;

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        bool found;
        Guid? currentUserSourceId;
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT current_user_source_id
                FROM current_user_source_import_job
                WHERE current_user_source_import_job_id = @job_id
                    AND user_id = @user_id;
                """;
            AddParameter(command, "@job_id", importJobId);
            AddParameter(command, "@user_id", userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            found = await reader.ReadAsync(cancellationToken);
            currentUserSourceId = found && !reader.IsDBNull(0) ? reader.GetGuid(0) : null;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }

        if (!found) return null;
        if (!currentUserSourceId.HasValue) return [];
        return await ListForCurrentUserSourceAsync(userId, currentUserSourceId.Value, cancellationToken) ?? [];
    }

    private async Task ResolvePriorLineageIssuesAsync(
        DbConnection connection,
        Guid sourceRepresentationId,
        string publicationLocalKey,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH target AS (
                SELECT source_package_id, origin_identity
                FROM source_representation
                WHERE source_representation_id = @representation_id
            )
            UPDATE source_reconciliation_issue issue
            SET resolved_at = @resolved_at
            FROM source_representation representation, target
            WHERE issue.source_representation_id = representation.source_representation_id
                AND representation.source_package_id = target.source_package_id
                AND representation.origin_identity = target.origin_identity
                AND issue.publication_local_key = @publication_local_key
                AND issue.source_representation_id <> @representation_id
                AND issue.resolved_at IS NULL;
            """;
        AddParameter(command, "@resolved_at", resolvedAt);
        AddParameter(command, "@representation_id", sourceRepresentationId);
        AddParameter(command, "@publication_local_key", publicationLocalKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<NormalizedSourceReconciliationIssue>> ListForPackageAsync(
        Guid packageId,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                WITH latest_representation AS (
                    SELECT DISTINCT ON (origin_identity)
                        source_representation_id
                    FROM source_representation
                    WHERE source_package_id = @package_id
                    ORDER BY origin_identity, imported_at DESC, source_representation_id DESC
                )
                SELECT
                    issue.issue_kind,
                    issue.publication_local_key,
                    issue.publication_display_name,
                    issue.source_entity_ids_json::text,
                    issue.message
                FROM source_reconciliation_issue issue
                JOIN latest_representation latest
                    ON latest.source_representation_id = issue.source_representation_id
                WHERE issue.resolved_at IS NULL
                ORDER BY issue.publication_display_name, issue.publication_local_key, issue.issue_kind;
                """;
            AddParameter(command, "@package_id", packageId);
            var results = new List<NormalizedSourceReconciliationIssue>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new NormalizedSourceReconciliationIssue(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    JsonSerializer.Deserialize<Guid[]>(reader.GetString(3)) ?? [],
                    reader.GetString(4)));
            }
            return results;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT to_regclass(@table_name) IS NOT NULL;";
            AddParameter(command, "@table_name", tableName);
            return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static string Require(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value can not be blank.", parameterName);
        }
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not exceed {maxLength} characters.", parameterName);
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

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS source_reconciliation_issue (
            source_reconciliation_issue_id uuid NOT NULL,
            source_representation_id uuid NOT NULL,
            issue_kind varchar(80) NOT NULL,
            publication_local_key varchar(500) NOT NULL,
            publication_display_name varchar(500) NOT NULL,
            source_entity_ids_json jsonb NOT NULL,
            message varchar(2000) NOT NULL,
            recorded_at timestamp with time zone NOT NULL,
            resolved_at timestamp with time zone NULL,
            CONSTRAINT pk_source_reconciliation_issue PRIMARY KEY (source_reconciliation_issue_id),
            CONSTRAINT fk_source_reconciliation_issue_representation FOREIGN KEY (source_representation_id)
                REFERENCES source_representation(source_representation_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_reconciliation_issue_identity
            ON source_reconciliation_issue(source_representation_id, issue_kind, publication_local_key);
        CREATE INDEX IF NOT EXISTS ix_source_reconciliation_issue_active_representation
            ON source_reconciliation_issue(source_representation_id)
            WHERE resolved_at IS NULL;
        """;
}
