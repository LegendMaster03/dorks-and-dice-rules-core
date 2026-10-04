using System.Data;
using Microsoft.EntityFrameworkCore;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Web;

/// <summary>
/// Ensures indexes used by high-volume Rules Core read paths exist even on databases whose
/// startup schema revision predates the performance indexes. Index creation runs after host
/// startup and only one replica performs it, so a large live database does not delay readiness
/// or cause every replica to wait for the same maintenance work.
/// </summary>
internal sealed class RulesCorePerformanceIndexInitializer(
    IServiceScopeFactory scopeFactory,
    ILogger<RulesCorePerformanceIndexInitializer> logger)
    : BackgroundService
{
    private const long PerformanceIndexLockKey = 4921946562870068041L;

    private static readonly string[] IndexStatements =
    [
        """
        CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_rule_concept_source_binding_canonical_entity
            ON rule_concept_source_binding(canonical_entity_id, rule_concept_id);
        """,
        """
        CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_rule_concept_source_binding_source_entity
            ON rule_concept_source_binding(source_entity_id)
            WHERE source_entity_id IS NOT NULL;
        """,
        """
        CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_ruleset_revision_entry_source_revision
            ON ruleset_revision_entry(source_entity_revision_id);
        """,
        """
        CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_campaign_ruleset_revision_entry_source_revision
            ON campaign_ruleset_revision_entry(source_entity_revision_id);
        """,
        """
        CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_source_entity_entity_type
            ON source_entity(entity_type);
        """,
        """
        CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_source_entity_source_code
            ON source_entity(source_code)
            WHERE source_code IS NOT NULL;
        """,
        """
        CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_source_entity_source_code_ci
            ON source_entity((lower(COALESCE(source_code, ''))));
        """,
        """
        CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_source_package_key_ci
            ON source_package((lower(package_key)));
        """,
        """
        CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_rule_concept_key_ci
            ON rule_concept((lower(concept_key)));
        """
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Ensure BackgroundService.StartAsync returns before any database maintenance begins.
        await Task.Yield();

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            await dbContext.Database.OpenConnectionAsync(stoppingToken);
            var lockAcquired = false;
            try
            {
                lockAcquired = await TryAcquireLockAsync(dbContext, stoppingToken);
                if (!lockAcquired)
                {
                    logger.LogInformation(
                        "Another Rules Core replica is already creating performance indexes; skipping local index initialization.");
                    return;
                }

                foreach (var statement in IndexStatements)
                {
                    await dbContext.Database.ExecuteSqlRawAsync(statement, stoppingToken);
                }

                logger.LogInformation("Rules Core performance indexes are available.");
            }
            finally
            {
                try
                {
                    if (lockAcquired)
                    {
                        await dbContext.Database.ExecuteSqlInterpolatedAsync(
                            $"SELECT pg_advisory_unlock({PerformanceIndexLockKey});",
                            CancellationToken.None);
                    }
                }
                finally
                {
                    await dbContext.Database.CloseConnectionAsync();
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Rules Core performance index initialization failed; continuing without blocking service startup.");
        }
    }

    private static async Task<bool> TryAcquireLockAsync(
        RulesCoreDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var command = dbContext.Database.GetDbConnection().CreateCommand();
        await using (command)
        {
            command.CommandText = "SELECT pg_try_advisory_lock(@lock_key);";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@lock_key";
            parameter.DbType = DbType.Int64;
            parameter.Value = PerformanceIndexLockKey;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is bool acquired && acquired;
        }
    }
}
