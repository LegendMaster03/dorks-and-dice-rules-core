using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed class SourceEntitySearchService(RulesCoreDbContext dbContext) : ISourceEntitySearchService
{
    private const int MaximumLimit = 200;

    public Task<IReadOnlyList<SourceEntitySummary>> SearchAccessibleAsync(
        string? userId,
        string? entityType = null,
        string? query = null,
        int limit = 100,
        CancellationToken cancellationToken = default) =>
        SearchAccessiblePageAsync(
            userId,
            entityType,
            query,
            limit,
            offset: 0,
            cancellationToken);

    public async Task<IReadOnlyList<SourceEntitySummary>> SearchAccessiblePageAsync(
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
            .Where(value =>
                value.Revisions.Any()
                && (value.SourcePackage.IsPublic
                    || (normalizedUserId != null
                        && value.SourcePackage.UserGrants
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
                || (value.SourceCode != null && EF.Functions.ILike(value.SourceCode, pattern))
                || EF.Functions.ILike(value.NativeKey, pattern)
                || EF.Functions.ILike(value.SourcePackage.DisplayName, pattern));
        }

        var rows = await sourceEntities
            .OrderBy(value => value.Name)
            .ThenBy(value => value.FormatKey)
            .ThenBy(value => value.SourceCode)
            .ThenBy(value => value.Id)
            .Skip(effectiveOffset)
            .Take(effectiveLimit)
            .Select(value => new
            {
                value.Id,
                value.EntityType,
                value.Name,
                SourceCode = value.SourceCode ?? string.Empty,
                value.FormatKey,
                PackageKey = value.SourcePackage.Key,
                PackageDisplayName = value.SourcePackage.DisplayName,
                LatestRevision = value.Revisions
                    .OrderByDescending(candidate => candidate.RevisionNumber)
                    .Select(candidate => new
                    {
                        candidate.RevisionNumber,
                        candidate.Fingerprint,
                        candidate.ImportedAt,
                        OriginIdentity = candidate.SourceRepresentation.OriginIdentity
                    })
                    .First()
            })
            .ToArrayAsync(cancellationToken);

        return rows
            .Select(value =>
            {
                var compatibility = CompatibilityIdentity(
                    value.PackageKey,
                    value.PackageDisplayName,
                    value.FormatKey,
                    value.LatestRevision.OriginIdentity);
                return new SourceEntitySummary(
                    value.Id,
                    value.EntityType,
                    value.Name,
                    value.SourceCode,
                    value.LatestRevision.RevisionNumber,
                    value.LatestRevision.Fingerprint,
                    value.LatestRevision.ImportedAt,
                    value.PackageKey,
                    value.PackageDisplayName,
                    compatibility.WorkKey,
                    compatibility.WorkDisplayName,
                    compatibility.EditionKey,
                    compatibility.EditionDisplayName);
            })
            .ToArray();
    }

    private static SourceCompatibilityIdentity CompatibilityIdentity(
        string packageKey,
        string packageDisplayName,
        string formatKey,
        string originIdentity)
    {
        var prefix = $"admin:{packageKey}:";
        if (originIdentity.StartsWith(prefix, StringComparison.Ordinal))
        {
            var remainder = originIdentity[prefix.Length..];
            var separator = remainder.LastIndexOf(':');
            if (separator > 0 && separator < remainder.Length - 1)
            {
                var workKey = remainder[..separator];
                var editionKey = remainder[(separator + 1)..];
                return new SourceCompatibilityIdentity(
                    workKey,
                    workKey,
                    editionKey,
                    editionKey);
            }
        }

        return new SourceCompatibilityIdentity(
            packageKey,
            packageDisplayName,
            formatKey,
            formatKey);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record SourceCompatibilityIdentity(
        string WorkKey,
        string WorkDisplayName,
        string EditionKey,
        string EditionDisplayName);
}
