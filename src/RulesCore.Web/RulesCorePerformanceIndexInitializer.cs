using Microsoft.EntityFrameworkCore;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Web;

/// <summary>
/// Ensures indexes used by high-volume Rules Core read paths exist even on databases whose
/// startup schema revision predates the performance indexes. Each statement is idempotent and
/// uses PostgreSQL concurrent index creation so an upgraded replica does not take an exclusive
/// write lock on a large live table while another replica is serving requests.
/// </summary>
internal sealed class RulesCorePerformanceIndexInitializer(IServiceScopeFactory scopeFactory)
    : IHostedService
{
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
        CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_source_entity_occurrence_binding_revision
            ON source_entity_occurrence_binding(source_entity_revision_id)
            WHERE source_entity_revision_id IS NOT NULL;
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
        """
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        foreach (var statement in IndexStatements)
        {
            await dbContext.Database.ExecuteSqlRawAsync(statement, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
