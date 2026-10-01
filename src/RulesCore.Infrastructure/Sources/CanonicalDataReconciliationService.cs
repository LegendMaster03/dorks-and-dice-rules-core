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
    internal const string StartupBackfillKey = "reference-history-companion-v1";
    private const string StartupLockIdentity = "rules-core-canonical-data-reconciliation-v1";

    public Task ReconcileExistingCorpusAsync(CancellationToken cancellationToken = default) =>
        ReconcileExistingCorpusCoreAsync(cancellationToken);

    public async Task RunStartupBackfillAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartupBackfillSchemaAsync(cancellationToken);

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await SetStartupLockAsync(connection, acquire: true, cancellationToken);
            try
            {
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
        await companions.ResolvePendingAsync(null, cancellationToken);
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
