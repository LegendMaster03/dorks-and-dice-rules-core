using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
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

        var snapshotCanonicalEntityId = await CanonicalRuleBindingStore.GetCanonicalEntityIdAsync(
            dbContext,
            snapshot.SourceEntityId,
            cancellationToken);
        var accessibleSourceIds = await CanonicalRuleBindingStore.GetAccessibleSourceEntityIdsForConceptAsync(
            dbContext,
            ruleConceptId,
            normalizedUserId,
            cancellationToken);
        if (accessibleSourceIds.Count == 0)
        {
            return null;
        }

        var canonicalBySource = await CanonicalRuleBindingStore.GetCanonicalEntityIdsAsync(
            dbContext,
            accessibleSourceIds,
            cancellationToken);
        var equivalentSourceIds = canonicalBySource
            .Where(value => value.Value == snapshotCanonicalEntityId)
            .Select(value => value.Key)
            .ToArray();
        if (equivalentSourceIds.Length == 0)
        {
            return null;
        }

        var semanticFingerprint = CanonicalSourceIdentity.SemanticFingerprint(snapshot.RawJson);
        var candidates = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Include(value => value.SourceEntity)
                .ThenInclude(value => value.SourcePackage)
                .ThenInclude(value => value.UserGrants)
            .Where(value => equivalentSourceIds.Contains(value.SourceEntityId))
            .OrderByDescending(value => value.RevisionNumber)
            .ThenByDescending(value => value.ImportedAt)
            .ToArrayAsync(cancellationToken);

        return candidates.FirstOrDefault(value =>
            IsAccessible(value.SourceEntity.SourcePackage, normalizedUserId)
            && string.Equals(
                CanonicalSourceIdentity.SemanticFingerprint(value.RawJson),
                semanticFingerprint,
                StringComparison.Ordinal));
    }

    private static bool IsAccessible(SourcePackage package, string? userId) =>
        package.IsPublic
        || (userId is not null && package.UserGrants.Any(grant => grant.UserId == userId));
}
