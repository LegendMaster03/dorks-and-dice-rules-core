using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed class CurrentUserSourceImportJobService(RulesCoreDbContext dbContext)
{
    private static readonly JsonSerializerOptions ProgressJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, byte> InitializedSchemas =
        new(StringComparer.Ordinal);
    private static readonly SemaphoreSlim SchemaInitializationLock = new(1, 1);

    public async Task<CurrentUserSourceImportJobView> QueueWebAddAsync(
        string currentUserId,
        string? url,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId(currentUserId);
        var uri = RequireWebSourceUri(url);
        var normalizedUrl = NormalizeWebOrigin(uri);
        return await QueueAsync(
            userId,
            CurrentUserSourceImportJobOperations.Add,
            WebSourceDisplayName(uri),
            normalizedUrl,
            currentUserSourceId: null,
            cancellationToken);
    }

    public async Task<CurrentUserSourceImportJobView?> QueueRefreshAsync(
        string currentUserId,
        Guid currentUserSourceId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId(currentUserId);
        if (currentUserSourceId == Guid.Empty)
            throw new ArgumentException("Source ID can not be empty.", nameof(currentUserSourceId));

        await EnsureSchemaAsync(cancellationToken);
        var registration = await ReadWebRegistrationAsync(userId, currentUserSourceId, cancellationToken);
        if (registration is null) return null;

        return await QueueAsync(
            userId,
            CurrentUserSourceImportJobOperations.Refresh,
            registration.Value.DisplayName,
            registration.Value.Url,
            currentUserSourceId,
            cancellationToken);
    }

    public async Task<bool> HasActiveImportAsync(
        string currentUserId,
        Guid currentUserSourceId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId(currentUserId);
        if (currentUserSourceId == Guid.Empty)
            throw new ArgumentException("Source ID can not be empty.", nameof(currentUserSourceId));
        await EnsureSchemaAsync(cancellationToken);
        return await dbContext.Database.SqlQueryRaw<bool>(
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM current_user_source_import_job
                    WHERE user_id = {0}
                        AND current_user_source_id = {1}
                        AND status IN ('queued', 'running')) AS "Value"
                """,
                userId,
                currentUserSourceId)
            .SingleAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CurrentUserSourceImportJobView>> ListAsync(
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId(currentUserId);
        await EnsureSchemaAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    current_user_source_import_job_id,
                    operation,
                    source_kind,
                    display_name,
                    source_url,
                    status,
                    current_user_source_id,
                    error_message,
                    progress_stage,
                    progress_current,
                    progress_total,
                    progress_detail,
                    progress_updated_at,
                    created_at,
                    started_at,
                    completed_at
                FROM current_user_source_import_job
                WHERE user_id = @user_id
                    AND dismissed_at IS NULL
                ORDER BY created_at DESC, current_user_source_import_job_id
                LIMIT 20;
                """;
            AddParameter(command, "@user_id", userId);

            var results = new List<CurrentUserSourceImportJobView>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) results.Add(ReadView(reader));
            return results;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public async Task<int> DismissAsync(
        string currentUserId,
        IReadOnlyCollection<Guid>? jobIds,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId(currentUserId);
        if (jobIds is null || jobIds.Count == 0) return 0;
        var ids = jobIds
            .Where(value => value != Guid.Empty)
            .Distinct()
            .Take(100)
            .ToArray();
        if (ids.Length == 0) return 0;

        await EnsureSchemaAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);

        try
        {
            var dismissedCount = 0;
            var dismissedAt = DateTimeOffset.UtcNow;
            foreach (var jobId in ids)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    UPDATE current_user_source_import_job
                    SET dismissed_at = @dismissed_at
                    WHERE current_user_source_import_job_id = @id
                        AND user_id = @user_id
                        AND status IN ('completed', 'failed')
                        AND dismissed_at IS NULL;
                    """;
                AddParameter(command, "@dismissed_at", dismissedAt);
                AddParameter(command, "@id", jobId);
                AddParameter(command, "@user_id", userId);
                dismissedCount += await command.ExecuteNonQueryAsync(cancellationToken);
            }
            return dismissedCount;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public async Task<bool> RequeueRunningJobAsync(
        Guid jobId,
        string detail,
        CancellationToken cancellationToken = default)
    {
        if (jobId == Guid.Empty)
            throw new ArgumentException("Job ID can not be empty.", nameof(jobId));
        var normalizedDetail = Require(detail, nameof(detail), 1000);

        await EnsureSchemaAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE current_user_source_import_job
                SET status = 'queued',
                    started_at = NULL,
                    completed_at = NULL,
                    error_message = NULL,
                    progress_stage = 'queued',
                    progress_current = 0,
                    progress_total = NULL,
                    progress_detail = @detail,
                    progress_updated_at = @updated_at
                WHERE current_user_source_import_job_id = @id
                    AND status = 'running';
                """;
            AddParameter(command, "@detail", normalizedDetail);
            AddParameter(command, "@updated_at", DateTimeOffset.UtcNow);
            AddParameter(command, "@id", jobId);
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public async Task<int> RequeueInterruptedRunningJobsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE current_user_source_import_job
                SET status = 'queued',
                    started_at = NULL,
                    completed_at = NULL,
                    error_message = NULL,
                    progress_stage = 'queued',
                    progress_current = 0,
                    progress_total = NULL,
                    progress_detail = 'Previous Web source import was interrupted; waiting to retry',
                    progress_updated_at = @updated_at
                WHERE status = 'running';
                """;
            AddParameter(command, "@updated_at", DateTimeOffset.UtcNow);
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public async Task<ClaimedCurrentUserSourceImportJob?> ClaimNextAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                WITH next_job AS (
                    SELECT current_user_source_import_job_id
                    FROM current_user_source_import_job
                    WHERE status = 'queued'
                    ORDER BY created_at, current_user_source_import_job_id
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                )
                UPDATE current_user_source_import_job job
                SET status = 'running',
                    started_at = @started_at,
                    completed_at = NULL,
                    error_message = NULL,
                    progress_stage = 'starting',
                    progress_current = NULL,
                    progress_total = NULL,
                    progress_detail = 'Starting Web source import',
                    progress_updated_at = @started_at
                FROM next_job
                WHERE job.current_user_source_import_job_id = next_job.current_user_source_import_job_id
                RETURNING
                    job.current_user_source_import_job_id,
                    job.user_id,
                    job.operation,
                    job.source_url,
                    job.current_user_source_id;
                """;
            AddParameter(command, "@started_at", DateTimeOffset.UtcNow);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            return new ClaimedCurrentUserSourceImportJob(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public async Task UpdateProgressAsync(
        Guid jobId,
        CurrentUserSourceImportProgress progress,
        CancellationToken cancellationToken = default)
    {
        if (jobId == Guid.Empty)
            throw new ArgumentException("Job ID can not be empty.", nameof(jobId));
        ArgumentNullException.ThrowIfNull(progress);
        var stage = Require(progress.Stage, nameof(progress.Stage), 40);
        ValidateProgress(progress);
        var progressJson = JsonSerializer.Serialize(progress, ProgressJsonOptions);

        await EnsureSchemaAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE current_user_source_import_job
                SET progress_stage = @stage,
                    progress_current = @current,
                    progress_total = @total,
                    progress_detail = @detail,
                    progress_updated_at = @updated_at
                WHERE current_user_source_import_job_id = @id
                    AND status = 'running';
                """;
            AddParameter(command, "@stage", stage);
            AddNullableParameter(command, "@current", progress.Current);
            AddNullableParameter(command, "@total", progress.Total);
            AddParameter(command, "@detail", progressJson);
            AddParameter(command, "@updated_at", DateTimeOffset.UtcNow);
            AddParameter(command, "@id", jobId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public Task CompleteAsync(
        Guid jobId,
        Guid currentUserSourceId,
        CancellationToken cancellationToken = default) =>
        SetTerminalStateAsync(
            jobId,
            CurrentUserSourceImportJobStatuses.Completed,
            currentUserSourceId,
            error: null,
            cancellationToken);

    public Task FailAsync(
        Guid jobId,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var message = DescribeFailure(exception);
        if (message.Length > 1000) message = message[..1000];
        return SetTerminalStateAsync(
            jobId,
            CurrentUserSourceImportJobStatuses.Failed,
            currentUserSourceId: null,
            message,
            cancellationToken);
    }

    private async Task<CurrentUserSourceImportJobView> QueueAsync(
        string userId,
        string operation,
        string displayName,
        string url,
        Guid? currentUserSourceId,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        var originKey = Fingerprint(Encoding.UTF8.GetBytes(url));
        var now = DateTimeOffset.UtcNow;
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO current_user_source_import_job (
                    current_user_source_import_job_id,
                    user_id,
                    operation,
                    source_kind,
                    display_name,
                    source_url,
                    origin_key,
                    status,
                    current_user_source_id,
                    progress_stage,
                    progress_current,
                    progress_total,
                    progress_detail,
                    progress_updated_at,
                    created_at)
                VALUES (
                    @id,
                    @user_id,
                    @operation,
                    'web',
                    @display_name,
                    @source_url,
                    @origin_key,
                    'queued',
                    @current_user_source_id,
                    'queued',
                    0,
                    NULL,
                    'Waiting for background importer',
                    @created_at,
                    @created_at)
                ON CONFLICT (user_id, operation, origin_key)
                    WHERE status IN ('queued', 'running')
                DO UPDATE SET
                    display_name = EXCLUDED.display_name,
                    source_url = EXCLUDED.source_url,
                    current_user_source_id = COALESCE(
                        EXCLUDED.current_user_source_id,
                        current_user_source_import_job.current_user_source_id)
                RETURNING
                    current_user_source_import_job_id,
                    operation,
                    source_kind,
                    display_name,
                    source_url,
                    status,
                    current_user_source_id,
                    error_message,
                    progress_stage,
                    progress_current,
                    progress_total,
                    progress_detail,
                    progress_updated_at,
                    created_at,
                    started_at,
                    completed_at;
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@user_id", userId);
            AddParameter(command, "@operation", operation);
            AddParameter(command, "@display_name", displayName);
            AddParameter(command, "@source_url", url);
            AddParameter(command, "@origin_key", originKey);
            AddNullableParameter(command, "@current_user_source_id", currentUserSourceId);
            AddParameter(command, "@created_at", now);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Web source import job was not readable after it was queued.");
            return ReadView(reader);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private async Task SetTerminalStateAsync(
        Guid jobId,
        string status,
        Guid? currentUserSourceId,
        string? error,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE current_user_source_import_job
                SET status = @status,
                    current_user_source_id = COALESCE(@current_user_source_id, current_user_source_id),
                    error_message = @error_message,
                    progress_stage = @status,
                    progress_updated_at = @completed_at,
                    completed_at = @completed_at
                WHERE current_user_source_import_job_id = @id;
                """;
            AddParameter(command, "@status", status);
            AddNullableParameter(command, "@current_user_source_id", currentUserSourceId);
            AddNullableParameter(command, "@error_message", error);
            AddParameter(command, "@completed_at", DateTimeOffset.UtcNow);
            AddParameter(command, "@id", jobId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private async Task<(string DisplayName, string Url)?> ReadWebRegistrationAsync(
        string userId,
        Guid currentUserSourceId,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT display_name, source_url
                FROM current_user_source
                WHERE current_user_source_id = @id
                    AND user_id = @user_id
                    AND source_kind = 'web'
                    AND source_url IS NOT NULL;
                """;
            AddParameter(command, "@id", currentUserSourceId);
            AddParameter(command, "@user_id", userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken)
                ? (reader.GetString(0), reader.GetString(1))
                : null;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var connectionIdentity = dbContext.Database.GetConnectionString()
            ?? connection.ConnectionString
            ?? $"{connection.DataSource}\u001f{connection.Database}";
        var schemaKey = Fingerprint(Encoding.UTF8.GetBytes(connectionIdentity));
        if (InitializedSchemas.ContainsKey(schemaKey)) return;

        await SchemaInitializationLock.WaitAsync(cancellationToken);
        try
        {
            if (InitializedSchemas.ContainsKey(schemaKey)) return;
            await dbContext.Database.ExecuteSqlRawAsync(SchemaSql, cancellationToken);
            InitializedSchemas.TryAdd(schemaKey, 0);
        }
        finally
        {
            SchemaInitializationLock.Release();
        }
    }

    private static CurrentUserSourceImportJobView ReadView(DbDataReader reader)
    {
        var rawProgress = reader.IsDBNull(reader.GetOrdinal("progress_detail"))
            ? null
            : reader.GetString(reader.GetOrdinal("progress_detail"));
        CurrentUserSourceImportProgress? structured = null;
        if (!string.IsNullOrWhiteSpace(rawProgress) && rawProgress.TrimStart().StartsWith('{'))
        {
            try
            {
                structured = JsonSerializer.Deserialize<CurrentUserSourceImportProgress>(
                    rawProgress,
                    ProgressJsonOptions);
            }
            catch (JsonException)
            {
                structured = null;
            }
        }

        return new CurrentUserSourceImportJobView(
            reader.GetGuid(reader.GetOrdinal("current_user_source_import_job_id")),
            reader.GetString(reader.GetOrdinal("operation")),
            reader.GetString(reader.GetOrdinal("source_kind")),
            reader.GetString(reader.GetOrdinal("display_name")),
            reader.IsDBNull(reader.GetOrdinal("source_url")) ? null : reader.GetString(reader.GetOrdinal("source_url")),
            reader.GetString(reader.GetOrdinal("status")),
            reader.IsDBNull(reader.GetOrdinal("current_user_source_id")) ? null : reader.GetGuid(reader.GetOrdinal("current_user_source_id")),
            reader.IsDBNull(reader.GetOrdinal("error_message")) ? null : reader.GetString(reader.GetOrdinal("error_message")),
            reader.IsDBNull(reader.GetOrdinal("progress_stage")) ? null : reader.GetString(reader.GetOrdinal("progress_stage")),
            reader.IsDBNull(reader.GetOrdinal("progress_current")) ? null : reader.GetInt32(reader.GetOrdinal("progress_current")),
            reader.IsDBNull(reader.GetOrdinal("progress_total")) ? null : reader.GetInt32(reader.GetOrdinal("progress_total")),
            structured is null ? rawProgress : structured.Detail,
            reader.IsDBNull(reader.GetOrdinal("progress_updated_at")) ? null : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("progress_updated_at")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
            reader.IsDBNull(reader.GetOrdinal("started_at")) ? null : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("started_at")),
            reader.IsDBNull(reader.GetOrdinal("completed_at")) ? null : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("completed_at")))
        {
            Progress = structured
        };
    }

    private static string DescribeFailure(Exception exception)
    {
        const string transientFailure = "An exception has been raised that is likely due to a transient failure.";
        if (string.Equals(exception.Message?.Trim(), transientFailure, StringComparison.Ordinal)
            && ExceptionChainContainsTimeout(exception))
        {
            return "A database operation timed out while importing this source. The import can be retried.";
        }

        return string.IsNullOrWhiteSpace(exception.Message)
            ? "The Web source import failed."
            : exception.Message.Trim();
    }

    private static bool ExceptionChainContainsTimeout(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is TimeoutException) return true;
        }
        return false;
    }

    private static void ValidateProgress(CurrentUserSourceImportProgress progress)
    {
        var counts = new int?[]
        {
            progress.Current, progress.Total, progress.FilesDiscovered, progress.CompatibleFiles,
            progress.RecordsDiscovered, progress.RecordsTranslated, progress.EntitiesPersisted,
            progress.NewEntities, progress.UnchangedEntities, progress.NewRevisions,
            progress.TranslationOnlyUpdates, progress.PublicationsProcessed, progress.PublicationTotal,
            progress.ReconciliationIssueCount, progress.RepresentationsStored, progress.RepresentationsReused
        };
        if (counts.Any(value => value is < 0))
            throw new ArgumentOutOfRangeException(nameof(progress), "Progress counts can not be negative.");
        if (progress.Current is not null && progress.Total is not null && progress.Current > progress.Total)
            throw new ArgumentException("Progress current can not exceed progress total.", nameof(progress));
        if (progress.PublicationsProcessed is not null
            && progress.PublicationTotal is not null
            && progress.PublicationsProcessed > progress.PublicationTotal)
            throw new ArgumentException("Processed publication count can not exceed publication total.", nameof(progress));
    }

    private static Uri RequireWebSourceUri(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Web source URL must be an absolute HTTPS URL.", nameof(value));
        if (!string.IsNullOrEmpty(uri.UserInfo)
            || string.Equals(uri.DnsSafeHost, "localhost", StringComparison.OrdinalIgnoreCase)
            || uri.AbsoluteUri.Length > 2000)
            throw new ArgumentException("Web source URL is not allowed.", nameof(value));
        return uri;
    }

    private static string NormalizeWebOrigin(Uri uri)
    {
        var builder = new UriBuilder(uri) { Host = uri.Host.ToLowerInvariant(), Fragment = string.Empty };
        return builder.Uri.AbsoluteUri;
    }

    private static string WebSourceDisplayName(Uri uri)
    {
        if (string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2) return $"{segments[0]}/{segments[1]}";
        }
        return uri.Host + uri.AbsolutePath.TrimEnd('/');
    }

    private static string RequireUserId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("User ID can not be blank.", nameof(value));
        var normalized = value.Trim();
        if (normalized.Length > 200) throw new ArgumentException("User ID can not exceed 200 characters.", nameof(value));
        return normalized;
    }

    private static string Require(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value can not be blank.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"Value can not exceed {maxLength} characters.", parameterName);
        return normalized;
    }

    private static string Fingerprint(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

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
        CREATE TABLE IF NOT EXISTS current_user_source_import_job (
            current_user_source_import_job_id uuid NOT NULL,
            user_id varchar(200) NOT NULL,
            operation varchar(20) NOT NULL,
            source_kind varchar(20) NOT NULL,
            display_name varchar(500) NOT NULL,
            source_url varchar(2000) NOT NULL,
            origin_key varchar(64) NOT NULL,
            status varchar(20) NOT NULL,
            current_user_source_id uuid NULL,
            error_message varchar(1000) NULL,
            progress_stage varchar(40) NULL,
            progress_current integer NULL,
            progress_total integer NULL,
            progress_detail text NULL,
            progress_updated_at timestamp with time zone NULL,
            created_at timestamp with time zone NOT NULL,
            started_at timestamp with time zone NULL,
            completed_at timestamp with time zone NULL,
            dismissed_at timestamp with time zone NULL,
            CONSTRAINT pk_current_user_source_import_job PRIMARY KEY (current_user_source_import_job_id),
            CONSTRAINT ck_current_user_source_import_job_operation CHECK (operation IN ('add', 'refresh')),
            CONSTRAINT ck_current_user_source_import_job_kind CHECK (source_kind = 'web'),
            CONSTRAINT ck_current_user_source_import_job_status CHECK (status IN ('queued', 'running', 'completed', 'failed')));
        ALTER TABLE current_user_source_import_job ADD COLUMN IF NOT EXISTS progress_stage varchar(40) NULL;
        ALTER TABLE current_user_source_import_job ADD COLUMN IF NOT EXISTS progress_current integer NULL;
        ALTER TABLE current_user_source_import_job ADD COLUMN IF NOT EXISTS progress_total integer NULL;
        ALTER TABLE current_user_source_import_job ADD COLUMN IF NOT EXISTS progress_detail text NULL;
        ALTER TABLE current_user_source_import_job ALTER COLUMN progress_detail TYPE text;
        ALTER TABLE current_user_source_import_job ADD COLUMN IF NOT EXISTS progress_updated_at timestamp with time zone NULL;
        ALTER TABLE current_user_source_import_job ADD COLUMN IF NOT EXISTS dismissed_at timestamp with time zone NULL;
        CREATE INDEX IF NOT EXISTS ix_current_user_source_import_job_user_created
            ON current_user_source_import_job(user_id, created_at DESC);
        CREATE INDEX IF NOT EXISTS ix_current_user_source_import_job_queue
            ON current_user_source_import_job(created_at)
            WHERE status = 'queued';
        CREATE UNIQUE INDEX IF NOT EXISTS ux_current_user_source_import_job_active_origin
            ON current_user_source_import_job(user_id, operation, origin_key)
            WHERE status IN ('queued', 'running');
        """;
}

public sealed record ClaimedCurrentUserSourceImportJob(
    Guid Id,
    string UserId,
    string Operation,
    string Url,
    Guid? CurrentUserSourceId);