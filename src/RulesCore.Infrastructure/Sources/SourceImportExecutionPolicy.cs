using System.Data;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public static class SourceImportExecutionPolicy
{
    public const int DatabaseCommandTimeoutSeconds = 300;

    // Imports may reuse a globally content-addressed blob while package deduplication removes
    // unreferenced blobs. Keep ordinary source work concurrent through a shared session lock, but
    // make package deduplication exclusive so its orphan cleanup can not delete/reinsert a digest
    // underneath a serializable import snapshot.
    private const string BlobMaintenanceLockIdentity =
        "rules-core-source-content-blob-maintenance-v1";

    private static readonly ConditionalWeakTable<RulesCoreDbContext, LockState> LockStates = new();

    public static void Apply(
        RulesCoreDbContext dbContext,
        [CallerMemberName] string? callerMemberName = null)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        dbContext.Database.SetCommandTimeout(DatabaseCommandTimeoutSeconds);

        var desiredMode = string.Equals(
            callerMemberName,
            "ConsolidateAsync",
            StringComparison.Ordinal)
            ? LockMode.Exclusive
            : LockMode.Shared;
        var state = LockStates.GetValue(dbContext, static _ => new LockState());
        var connection = dbContext.Database.GetDbConnection();

        // A pooled connection reset releases PostgreSQL session advisory locks. If a caller closed
        // the connection explicitly, forget the cached mode and reacquire it on the new session.
        if (connection.State != ConnectionState.Open)
        {
            state.Mode = LockMode.None;
            dbContext.Database.OpenConnection();
        }

        if (state.Mode == LockMode.Exclusive || state.Mode == desiredMode)
        {
            return;
        }

        if (state.Mode == LockMode.Shared && desiredMode == LockMode.Exclusive)
        {
            dbContext.Database.ExecuteSqlRaw($"""
                SELECT pg_advisory_unlock_shared(
                    hashtextextended('{BlobMaintenanceLockIdentity}', 0));
                """);
            state.Mode = LockMode.None;
        }

        dbContext.Database.ExecuteSqlRaw(desiredMode == LockMode.Exclusive
            ? $"SELECT pg_advisory_lock(hashtextextended('{BlobMaintenanceLockIdentity}', 0));"
            : $"SELECT pg_advisory_lock_shared(hashtextextended('{BlobMaintenanceLockIdentity}', 0));");
        state.Mode = desiredMode;
    }

    private sealed class LockState
    {
        public LockMode Mode { get; set; }
    }

    private enum LockMode
    {
        None,
        Shared,
        Exclusive
    }
}
