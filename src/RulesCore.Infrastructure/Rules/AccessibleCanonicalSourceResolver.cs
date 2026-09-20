using Microsoft.EntityFrameworkCore;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

internal static class AccessibleCanonicalSourceResolver
{
    public static async Task<SourceEntityRevision?> ResolveRevisionAsync(
        RulesCoreDbContext dbContext,
        Guid ruleConceptId,
        Guid snapshotRevisionId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        if (ruleConceptId == Guid.Empty || snapshotRevisionId == Guid.Empty)
        {
            return null;
        }

        var normalizedUserId = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
        await CanonicalRuleBindingStore.EnsureSchemaAsync(dbContext, cancellationToken);

        var snapshot = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Include(value => value.SourceEntity)
                .ThenInclude(value => value.SourcePackage)
                .ThenInclude(value => value.UserGrants)
            .SingleOrDefaultAsync(value => value.Id == snapshotRevisionId, cancellationToken);
        if (snapshot is null)
        {
            return null;
        }

        if (IsAccessible(snapshot.SourceEntity.SourcePackage, normalizedUserId))
        {
            return snapshot;
        }

        var snapshotCanonicalEntityId = await CanonicalRuleBindingStore.GetCanonicalEntityIdForRevisionAsync(
            dbContext,
            snapshot.Id,
            cancellationToken);
        var candidateRevisionIds = await CanonicalRuleBindingStore.GetAccessibleRevisionIdsForCanonicalEntityAsync(
            dbContext,
            snapshotCanonicalEntityId,
            normalizedUserId,
            cancellationToken);
        if (candidateRevisionIds.Count == 0)
        {
            return null;
        }

        var candidates = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Include(value => value.SourceEntity)
                .ThenInclude(value => value.SourcePackage)
                .ThenInclude(value => value.UserGrants)
            .Where(value => candidateRevisionIds.Contains(value.Id))
            .OrderByDescending(value => value.ImportedAt)
            .ThenByDescending(value => value.RevisionNumber)
            .ToArrayAsync(cancellationToken);

        // Canonical identity alone is not permission to substitute different mechanics.
        // A restricted selected revision may fall back to an accessible representation only
        // when that representation is mechanically equivalent to the published snapshot.
        // This keeps source grants independent even when reviewed cross-edition identity
        // intentionally groups multiple mechanical presentations under one concept.
        var snapshotFingerprint = RuleAutoResolutionService.ComputeSemanticFingerprint(
            snapshot.GetMechanicalContentJson());
        return candidates.FirstOrDefault(value =>
            IsAccessible(value.SourceEntity.SourcePackage, normalizedUserId)
            && string.Equals(
                RuleAutoResolutionService.ComputeSemanticFingerprint(
                    value.GetMechanicalContentJson()),
                snapshotFingerprint,
                StringComparison.Ordinal));
    }

    private static bool IsAccessible(SourcePackage package, string? userId) =>
        package.IsPublic
        || (userId is not null && package.UserGrants.Any(grant => grant.UserId == userId));
}
