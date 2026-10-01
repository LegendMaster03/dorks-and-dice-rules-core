using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Public maintenance entry point for idempotent canonical-history and companion-content
/// reconciliation. The lower-level stores remain implementation details of Rules Core.
/// </summary>
public sealed class CanonicalDataReconciliationService(RulesCoreDbContext dbContext)
{
    public async Task ReconcileExistingCorpusAsync(CancellationToken cancellationToken = default)
    {
        var relationships = new CanonicalEntityRelationshipStore(dbContext);
        await relationships.EnsureSchemaAsync(cancellationToken);

        var companions = new SourceCompanionContentStore(dbContext);
        await companions.EnsureSchemaAsync(cancellationToken);
        await companions.MigrateLegacyStandaloneFluffAsync(cancellationToken);
        await companions.ResolvePendingAsync(null, cancellationToken);
        await new CanonicalReferenceHistoryReconciliationService(dbContext)
            .ReconcileAllAsync(cancellationToken);
    }
}
