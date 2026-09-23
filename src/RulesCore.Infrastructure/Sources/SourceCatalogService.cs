using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed class SourceCatalogService(RulesCoreDbContext dbContext) : ISourceCatalogService
{
    public async Task<IReadOnlyList<SourcePackageSummary>> GetAccessiblePackagesAsync(
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeUserId(userId);
        return await dbContext.SourcePackages
            .AsNoTracking()
            .Where(value => value.IsPublic
                || (normalizedUserId != null
                    && value.UserGrants.Any(grant => grant.UserId == normalizedUserId)))
            .OrderBy(value => value.DisplayName)
            .ThenBy(value => value.Key)
            .Select(value => new SourcePackageSummary(
                value.Id,
                value.Key,
                value.DisplayName,
                value.Provider,
                value.License,
                value.IsPublic))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<SourceEntityView?> GetLatestAccessibleEntityAsync(
        Guid entityId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeUserId(userId);
        var entity = await dbContext.SourceEntities
            .AsNoTracking()
            .Include(value => value.Revisions)
            .Include(value => value.SourcePackage)
            .SingleOrDefaultAsync(
                value => value.Id == entityId
                    && (value.SourcePackage.IsPublic
                        || (normalizedUserId != null
                            && value.SourcePackage.UserGrants
                                .Any(grant => grant.UserId == normalizedUserId))),
                cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var revision = entity.Revisions
            .OrderByDescending(value => value.RevisionNumber)
            .FirstOrDefault();
        if (revision is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(revision.GetMechanicalContentJson());
        var package = entity.SourcePackage;
        return new SourceEntityView(
            entity.Id,
            entity.EntityType,
            entity.Name,
            entity.SourceCode ?? string.Empty,
            revision.RevisionNumber,
            revision.Fingerprint,
            revision.ImportedAt,
            package.Key,
            package.DisplayName,
            package.Key,
            package.DisplayName,
            entity.FormatKey,
            entity.FormatKey,
            document.RootElement.Clone());
    }

    private static string? NormalizeUserId(string? userId) =>
        string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
}
