using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed class SourceLibraryBrowserService(RulesCoreDbContext dbContext)
{
    private const int MaximumLimit = 200;

    public async Task<IReadOnlyList<SourceLibraryPublicationView>> GetPublicationsAsync(
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeOptional(userId);
        var editions = await AccessibleEditions(normalizedUserId)
            .Include(value => value.Entities)
                .ThenInclude(value => value.Revisions)
            .OrderBy(value => value.SourceWork.DisplayName)
            .ThenBy(value => value.DisplayName)
            .ThenBy(value => value.Id)
            .ToArrayAsync(cancellationToken);

        return editions
            .Where(value => value.Entities.Any(entity => entity.Revisions.Count > 0))
            .Select(ToPublicationView)
            .ToArray();
    }

    public async Task<SourceLibraryPublicationView?> GetPublicationAsync(
        Guid publicationId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeOptional(userId);
        var edition = await AccessibleEditions(normalizedUserId)
            .Include(value => value.Entities)
                .ThenInclude(value => value.Revisions)
            .SingleOrDefaultAsync(value => value.Id == publicationId, cancellationToken);
        return edition is null || !edition.Entities.Any(entity => entity.Revisions.Count > 0)
            ? null
            : ToPublicationView(edition);
    }

    public async Task<IReadOnlyList<SourceLibraryEntitySummaryView>> SearchAsync(
        string? userId,
        Guid? publicationId = null,
        string? entityType = null,
        string? query = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeOptional(userId);
        var normalizedType = NormalizeOptional(entityType);
        var normalizedQuery = NormalizeOptional(query);
        var effectiveLimit = Math.Clamp(limit, 1, MaximumLimit);
        var effectiveOffset = Math.Max(offset, 0);

        var sourceEntities = AccessibleEntities(normalizedUserId);
        if (publicationId.HasValue)
        {
            sourceEntities = sourceEntities.Where(value => value.SourceEditionId == publicationId.Value);
        }
        if (normalizedType is not null)
        {
            var typeLower = normalizedType.ToLower();
            sourceEntities = sourceEntities.Where(value => value.EntityType.ToLower() == typeLower);
        }
        if (normalizedQuery is not null)
        {
            var pattern = $"%{normalizedQuery}%";
            sourceEntities = sourceEntities.Where(value =>
                EF.Functions.ILike(value.Name, pattern)
                || EF.Functions.ILike(value.SourceCode, pattern)
                || EF.Functions.ILike(value.SourceEdition.DisplayName, pattern)
                || EF.Functions.ILike(value.SourceEdition.SourceWork.DisplayName, pattern));
        }

        var entities = await sourceEntities
            .OrderBy(value => value.Name)
            .ThenBy(value => value.SourceEdition.SourceWork.DisplayName)
            .ThenBy(value => value.SourceCode)
            .ThenBy(value => value.Id)
            .Skip(effectiveOffset)
            .Take(effectiveLimit)
            .ToArrayAsync(cancellationToken);

        return entities.Select(ToSummaryView).ToArray();
    }

    public async Task<SourceLibraryEntityView?> GetEntityAsync(
        Guid publicationId,
        Guid entityId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeOptional(userId);
        var entity = await AccessibleEntities(normalizedUserId)
            .SingleOrDefaultAsync(
                value => value.Id == entityId && value.SourceEditionId == publicationId,
                cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var revision = entity.Revisions.OrderByDescending(value => value.RevisionNumber).FirstOrDefault();
        if (revision is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(revision.RawJson);
        var edition = entity.SourceEdition;
        var work = edition.SourceWork;
        var package = work.SourcePackage;
        return new SourceLibraryEntityView(
            entity.Id,
            edition.Id,
            entity.EntityType,
            entity.Name,
            entity.SourceCode,
            revision.RevisionNumber,
            revision.Fingerprint,
            revision.ImportedAt,
            PublicationDisplayName(work, edition),
            edition.DisplayName,
            package.Provider,
            package.License,
            document.RootElement.Clone(),
            SourceLibraryRoutes.ForEntity(edition.Id, entity.EntityType, entity.Id));
    }

    private IQueryable<SourceEdition> AccessibleEditions(string? userId) =>
        dbContext.SourceEditions
            .AsNoTracking()
            .Include(value => value.SourceWork)
                .ThenInclude(value => value.SourcePackage)
            .Where(value =>
                value.SourceWork.SourcePackage.IsPublic
                || (userId != null
                    && value.SourceWork.SourcePackage.UserGrants.Any(grant => grant.UserId == userId)));

    private IQueryable<SourceEntity> AccessibleEntities(string? userId) =>
        dbContext.SourceEntities
            .AsNoTracking()
            .Include(value => value.Revisions)
            .Include(value => value.SourceEdition)
                .ThenInclude(value => value.SourceWork)
                .ThenInclude(value => value.SourcePackage)
            .Where(value =>
                value.Revisions.Any()
                && (value.SourceEdition.SourceWork.SourcePackage.IsPublic
                    || (userId != null
                        && value.SourceEdition.SourceWork.SourcePackage.UserGrants
                            .Any(grant => grant.UserId == userId))));

    private static SourceLibraryPublicationView ToPublicationView(SourceEdition edition)
    {
        var work = edition.SourceWork;
        var package = work.SourcePackage;
        var entities = edition.Entities.Where(value => value.Revisions.Count > 0).ToArray();
        var categories = entities
            .GroupBy(value => value.EntityType, StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => CategoryDisplayName(value.Key), StringComparer.OrdinalIgnoreCase)
            .Select(group => new SourceLibraryCategoryView(
                group.Key,
                CategoryDisplayName(group.Key),
                group.Count(),
                SourceLibraryRoutes.ForCollection(edition.Id, group.Key)))
            .ToArray();
        var latestImportedAt = entities
            .SelectMany(value => value.Revisions)
            .Select(value => (DateTimeOffset?)value.ImportedAt)
            .OrderByDescending(value => value)
            .FirstOrDefault();

        return new SourceLibraryPublicationView(
            edition.Id,
            PublicationDisplayName(work, edition),
            work.DisplayName,
            edition.DisplayName,
            package.Provider,
            package.License,
            package.IsPublic,
            entities.Length,
            latestImportedAt,
            categories,
            SourceLibraryRoutes.ForPublication(edition.Id));
    }

    private static SourceLibraryEntitySummaryView ToSummaryView(SourceEntity entity)
    {
        var revision = entity.Revisions.OrderByDescending(value => value.RevisionNumber).First();
        var edition = entity.SourceEdition;
        var work = edition.SourceWork;
        return new SourceLibraryEntitySummaryView(
            entity.Id,
            edition.Id,
            entity.EntityType,
            entity.Name,
            entity.SourceCode,
            revision.RevisionNumber,
            revision.ImportedAt,
            PublicationDisplayName(work, edition),
            edition.DisplayName,
            SourceLibraryRoutes.ForEntity(edition.Id, entity.EntityType, entity.Id));
    }

    private static string PublicationDisplayName(SourceWork work, SourceEdition edition) =>
        string.Equals(work.DisplayName, edition.DisplayName, StringComparison.OrdinalIgnoreCase)
            ? work.DisplayName
            : string.IsNullOrWhiteSpace(work.DisplayName)
                ? edition.DisplayName
                : work.DisplayName;

    private static string CategoryDisplayName(string entityType) => entityType switch
    {
        "monster" => "Monsters",
        "spell" => "Spells",
        "class" => "Classes",
        "subclass" => "Subclasses",
        "classFeature" => "Class Features",
        "subclassFeature" => "Subclass Features",
        "prestigeClass" => "Prestige Classes",
        "npcClass" => "NPC Classes",
        "race" => "Races",
        "species" => "Species",
        "feat" => "Feats",
        "skill" => "Skills",
        "item" => "Items",
        "power" => "Psionic Powers",
        "domain" => "Domains",
        "divineAbility" => "Divine Abilities",
        "condition" => "Conditions",
        "houseRule" => "House Rules",
        "rule" => "Rules",
        "source-fragment" => "Document Sections",
        _ => Humanize(entityType)
    };

    private static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Other";
        var result = new System.Text.StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (index > 0 && char.IsUpper(character) && !char.IsUpper(value[index - 1])) result.Append(' ');
            result.Append(index == 0 ? char.ToUpperInvariant(character) : character);
        }
        return result.ToString();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
