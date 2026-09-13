using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed record CanonicalPublisherPropagationResult(
    int UpdatedPublications,
    int ConflictingPublications);

/// <summary>
/// Compatibility facade for callers that still request an explicit publisher reconciliation pass.
/// Publisher, edition, date, and title are canonical publication evidence now; they are not stored
/// on an import-owned SourceEdition. The 5e.tools metadata reconciler performs that evidence merge
/// directly against canonical publications.
/// </summary>
public sealed class CanonicalPublicationPublisherService(RulesCoreDbContext dbContext)
{
    public async Task<CanonicalPublisherPropagationResult> ReconcilePackageAsync(
        Guid sourcePackageId,
        CancellationToken cancellationToken = default)
    {
        if (sourcePackageId == Guid.Empty)
        {
            throw new ArgumentException("Source package ID can not be empty.", nameof(sourcePackageId));
        }

        var result = await new FiveEToolsPublicationMetadataReconciliationService(dbContext)
            .ReconcilePackageAsync(sourcePackageId, cancellationToken);
        return new CanonicalPublisherPropagationResult(
            result.UpdatedPublications,
            result.ConflictingPublications);
    }
}
