using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed class SourceEntitySearchService(RulesCoreDbContext dbContext) : ISourceEntitySearchService
{
    private const int MaximumLimit = 200;

    public async Task<IReadOnlyList<SourceEntitySummary>> SearchAccessibleAsync(
        string? userId,
        string? entityType = null,
        string? query = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeOptional(userId);
        var normalizedEntityType = NormalizeOptional(entityType);
        var normalizedQuery = NormalizeOptional(query);
        var effectiveLimit = Math.Clamp(limit, 1, MaximumLimit);
        var effectiveOffset = Math.Max(offset, 0);

        var sourceEntities = dbContext.SourceEntities
            .AsNoTracking()
            .Include(value => value.Revisions)
            .Include(value => value.SourceEdition)
                .ThenInclude(value => value.SourceWork)
                .ThenInclude(value => value.SourcePackage)
            .Where(value =>
                value.Revisions.Any()
                && (value.SourceEdition.SourceWork.SourcePackage.IsPublic
                    || (normalizedUserId != null
                        && value.SourceEdition.SourceWork.SourcePackage.UserGrants
                            .Any(grant => grant.UserId == normalizedUserId))));

        if (normalizedEntityType is not null)
        {
            var entityTypeLower = normalizedEntityType.ToLower();
            sourceEntities = sourceEntities.Where(value => value.EntityType.ToLower() == entityTypeLower);
        }

        if (normalizedQuery is not null)
        {
            var pattern = $"%{normalizedQuery}%";
            sourceEntities = sourceEntities.Where(value =>
                EF.Functions.ILike(value.Name, pattern)
                || EF.Functions.ILike(value.SourceCode, pattern)
                || EF.Functions.ILike(value.SourceEdition.DisplayName, pattern)
                || EF.Functions.ILike(value.SourceEdition.SourceWork.DisplayName, pattern)
                || EF.Functions.ILike(value.SourceEdition.SourceWork.SourcePackage.DisplayName, pattern));
        }

        var entities = await sourceEntities
            .OrderBy(value => value.Name)
            .ThenBy(value => value.SourceEdition.DisplayName)
            .ThenBy(value => value.SourceCode)
            .ThenBy(value => value.Id)
            .Skip(effectiveOffset)
            .Take(effectiveLimit)
            .ToArrayAsync(cancellationToken);

        return entities
            .Select(value =>
            {
                var revision = value.Revisions
                    .OrderByDescending(candidate => candidate.RevisionNumber)
                    .First();
                var edition = value.SourceEdition;
                var work = edition.SourceWork;
                var package = work.SourcePackage;
                return new SourceEntitySummary(
                    value.Id,
                    value.EntityType,
                    value.Name,
                    value.SourceCode,
                    revision.RevisionNumber,
                    revision.Fingerprint,
                    revision.ImportedAt,
                    package.Key,
                    package.DisplayName,
                    work.Key,
                    work.DisplayName,
                    edition.Key,
                    edition.DisplayName);
            })
            .ToArray();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}