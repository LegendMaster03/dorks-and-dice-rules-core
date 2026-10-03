using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Public maintenance entry point for canonical-history and companion-content reconciliation.
/// Explicit maintenance remains rerunnable; startup reconciliation is separately versioned and
/// serialized so multiple Rules Core ingress processes do not repeat the full corpus backfill.
/// </summary>
public sealed class CanonicalDataReconciliationService(RulesCoreDbContext dbContext)
{
    public const string StartupBackfillKey = "reference-history-companion-v2";

    // Keep the original lock identity across backfill versions so old and new ingress processes
    // remain mutually exclusive during a rolling deployment while their durable completion keys
    // can advance independently.
    private const string StartupLockIdentity = "rules-core-canonical-data-reconciliation-v1";

    public Task ReconcileExistingCorpusAsync(CancellationToken cancellationToken = default) =>
        ReconcileExistingCorpusCoreAsync(cancellationToken);

    public async Task<DateTimeOffset?> GetStartupBackfillCompletedAtAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using (var existsCommand = connection.CreateCommand())
            {
                existsCommand.CommandText =
                    "SELECT to_regclass('public.canonical_data_reconciliation_backfill') IS NOT NULL;";
                if (!Convert.ToBoolean(await existsCommand.ExecuteScalarAsync(cancellationToken)))
                {
                    return null;
                }
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT completed_at
                FROM canonical_data_reconciliation_backfill
                WHERE backfill_key = @key;
                """;
            AddParameter(command, "@key", StartupBackfillKey);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value switch
            {
                DateTimeOffset dateTimeOffset => dateTimeOffset,
                DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
                null or DBNull => null,
                _ => throw new InvalidOperationException(
                    "The canonical-data reconciliation completion timestamp has an unexpected database type.")
            };
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public async Task RunStartupBackfillAsync(CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            // Acquire ownership before even creating the marker table. PostgreSQL's
            // CREATE TABLE IF NOT EXISTS can still race at the catalog level when two fresh
            // processes execute it concurrently against a database that has never run this
            // backfill.
            await SetStartupLockAsync(connection, acquire: true, cancellationToken);
            try
            {
                await EnsureStartupBackfillSchemaAsync(cancellationToken);
                if (await HasCompletedStartupBackfillAsync(connection, cancellationToken))
                {
                    return;
                }

                await ReconcileExistingCorpusCoreAsync(cancellationToken);
                await MarkStartupBackfillCompletedAsync(connection, cancellationToken);
            }
            finally
            {
                await SetStartupLockAsync(connection, acquire: false, CancellationToken.None);
            }
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private async Task ReconcileExistingCorpusCoreAsync(CancellationToken cancellationToken)
    {
        var relationships = new CanonicalEntityRelationshipStore(dbContext);
        await relationships.EnsureSchemaAsync(cancellationToken);

        var companions = new SourceCompanionContentStore(dbContext);
        await companions.EnsureSchemaAsync(cancellationToken);
        await new LegacyStandaloneFluffMigrationService(dbContext).MigrateAsync(cancellationToken);
        await new HistoricalCompanionContentReconciliationService(dbContext)
            .ResolvePendingAsync(cancellationToken);
        await new CanonicalReferenceHistoryReconciliationService(dbContext)
            .ReconcileAllAsync(cancellationToken);
    }

    private Task EnsureStartupBackfillSchemaAsync(CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS canonical_data_reconciliation_backfill (
                backfill_key varchar(120) NOT NULL,
                completed_at timestamp with time zone NOT NULL,
                CONSTRAINT pk_canonical_data_reconciliation_backfill PRIMARY KEY (backfill_key));
            """,
            cancellationToken);

    private static async Task<bool> HasCompletedStartupBackfillAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM canonical_data_reconciliation_backfill
                WHERE backfill_key = @key);
            """;
        AddParameter(command, "@key", StartupBackfillKey);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task MarkStartupBackfillCompletedAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO canonical_data_reconciliation_backfill (backfill_key, completed_at)
            VALUES (@key, @completed_at)
            ON CONFLICT (backfill_key) DO NOTHING;
            """;
        AddParameter(command, "@key", StartupBackfillKey);
        AddParameter(command, "@completed_at", DateTimeOffset.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SetStartupLockAsync(
        DbConnection connection,
        bool acquire,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = acquire
            ? "SELECT pg_advisory_lock(hashtextextended(@identity, 0));"
            : "SELECT pg_advisory_unlock(hashtextextended(@identity, 0));";
        AddParameter(command, "@identity", StartupLockIdentity);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
