using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed class SourceImportService(RulesCoreDbContext dbContext) : ISourceImportService
{
    public async Task<SourceImportPreviewResult> Preview5eToolsDocumentAsync(
        Import5eToolsDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        await SourceFrameworkStore.EnsureSchemaAsync(dbContext, cancellationToken);

        var packageKey = NormalizeKey(request.PackageKey);
        var workKey = NormalizeKey(request.WorkKey);
        var editionKey = NormalizeKey(request.EditionKey);
        var parsedEntities = ParseEntities(request.Json, editionKey);
        if (parsedEntities.Count == 0)
        {
            throw new InvalidDataException("The 5e.tools document did not contain any importable entity arrays.");
        }

        var gameEdition = NormalizeGameEdition(request);
        var releaseKind = SourceReleaseKinds.NormalizeImportLabel(request.ReleaseKind);
        var conflicts = new List<string>();
        var warnings = new List<string>();
        if (gameEdition is null)
        {
            warnings.Add(
                "No canonical D&D game edition could be identified. The source can still be imported, but cross-edition detection and provenance will be less informative until edition metadata is recorded.");
        }

        var package = await dbContext.SourcePackages
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Key == packageKey, cancellationToken);
        if (package is not null)
        {
            AddPackageConflicts(package, request, conflicts);
        }

        SourceWork? work = null;
        if (package is not null)
        {
            work = await dbContext.SourceWorks
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    value => value.SourcePackageId == package.Id && value.Key == workKey,
                    cancellationToken);
            if (work is not null
                && !string.Equals(work.DisplayName, request.WorkDisplayName.Trim(), StringComparison.Ordinal))
            {
                conflicts.Add(
                    $"Source work '{workKey}' is already registered as '{work.DisplayName}', not '{request.WorkDisplayName.Trim()}'.");
            }
        }

        SourceEdition? edition = null;
        if (work is not null)
        {
            edition = await dbContext.SourceEditions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    value => value.SourceWorkId == work.Id && value.Key == editionKey,
                    cancellationToken);
            if (edition is not null
                && !string.Equals(edition.DisplayName, request.EditionDisplayName.Trim(), StringComparison.Ordinal))
            {
                conflicts.Add(
                    $"Source release '{editionKey}' is already registered as '{edition.DisplayName}', not '{request.EditionDisplayName.Trim()}'.");
            }
        }

        if (edition is not null)
        {
            var existingMetadata = await SourceFrameworkStore.GetEditionMetadataAsync(
                dbContext,
                edition.Id,
                cancellationToken);
            AddMetadataConflict(existingMetadata?.GameEdition, gameEdition, "game edition", conflicts);
            AddMetadataConflict(existingMetadata?.ReleaseKind, releaseKind, "release kind", conflicts);
            AddMetadataConflict(existingMetadata?.PublicationDate, request.PublicationDate, "publication date", conflicts);

            gameEdition ??= existingMetadata?.GameEdition;
            releaseKind ??= existingMetadata?.ReleaseKind;
        }

        var existingByNaturalKey = new Dictionary<string, SourceEntity>(StringComparer.Ordinal);
        if (edition is not null)
        {
            var entities = await dbContext.SourceEntities
                .AsNoTracking()
                .Include(value => value.Revisions)
                .Where(value => value.SourceEditionId == edition.Id)
                .ToArrayAsync(cancellationToken);
            existingByNaturalKey = entities.ToDictionary(value => value.NaturalKey, StringComparer.Ordinal);
        }

        var previewEntities = new List<SourceImportPreviewEntity>(parsedEntities.Count);
        foreach (var parsed in parsedEntities)
        {
            if (!existingByNaturalKey.TryGetValue(parsed.NaturalKey, out var existing))
            {
                previewEntities.Add(new SourceImportPreviewEntity(
                    EntityId: null,
                    parsed.EntityType,
                    parsed.Name,
                    parsed.SourceCode,
                    CurrentRevisionNumber: null,
                    parsed.Fingerprint,
                    SourceImportPreviewActions.NewEntity));
                continue;
            }

            var latest = existing.Revisions
                .OrderByDescending(value => value.RevisionNumber)
                .FirstOrDefault();
            if (latest is null)
            {
                previewEntities.Add(new SourceImportPreviewEntity(
                    existing.Id,
                    parsed.EntityType,
                    parsed.Name,
                    parsed.SourceCode,
                    CurrentRevisionNumber: null,
                    parsed.Fingerprint,
                    SourceImportPreviewActions.NewRevision));
                continue;
            }

            previewEntities.Add(new SourceImportPreviewEntity(
                existing.Id,
                parsed.EntityType,
                parsed.Name,
                parsed.SourceCode,
                latest.RevisionNumber,
                parsed.Fingerprint,
                string.Equals(latest.Fingerprint, parsed.Fingerprint, StringComparison.Ordinal)
                    ? SourceImportPreviewActions.Unchanged
                    : SourceImportPreviewActions.NewRevision));
        }

        return new SourceImportPreviewResult(
            packageKey,
            request.PackageDisplayName.Trim(),
            request.Provider.Trim(),
            NormalizeOptional(request.License),
            request.IsPublic,
            workKey,
            request.WorkDisplayName.Trim(),
            editionKey,
            request.EditionDisplayName.Trim(),
            gameEdition,
            releaseKind,
            request.PublicationDate,
            CanImport: conflicts.Count == 0,
            conflicts,
            warnings,
            previewEntities.Count,
            previewEntities.Count(value => value.Action == SourceImportPreviewActions.NewEntity),
            previewEntities.Count(value => value.Action == SourceImportPreviewActions.NewRevision),
            previewEntities.Count(value => value.Action == SourceImportPreviewActions.Unchanged),
            previewEntities);
    }

    public async Task<SourceImportResult> Import5eToolsDocumentAsync(
        Import5eToolsDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var preview = await Preview5eToolsDocumentAsync(request, cancellationToken);
        if (!preview.CanImport)
        {
            throw new InvalidOperationException(
                $"Source import conflicts with existing immutable provenance: {string.Join(" ", preview.Conflicts)}");
        }

        var packageKey = preview.PackageKey;
        var workKey = preview.WorkKey;
        var editionKey = preview.EditionKey;
        var parsedEntities = ParseEntities(request.Json, editionKey);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var package = await dbContext.SourcePackages
            .SingleOrDefaultAsync(value => value.Key == packageKey, cancellationToken);
        if (package is null)
        {
            package = new SourcePackage
            {
                Id = Guid.NewGuid(),
                Key = packageKey,
                DisplayName = request.PackageDisplayName.Trim(),
                Provider = request.Provider.Trim(),
                License = NormalizeOptional(request.License),
                IsPublic = request.IsPublic,
                CreatedAt = now
            };
            dbContext.SourcePackages.Add(package);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            EnsurePackageIdentityMatches(package, request);
        }

        var work = await dbContext.SourceWorks
            .SingleOrDefaultAsync(
                value => value.SourcePackageId == package.Id && value.Key == workKey,
                cancellationToken);
        if (work is null)
        {
            work = new SourceWork
            {
                Id = Guid.NewGuid(),
                SourcePackageId = package.Id,
                Key = workKey,
                DisplayName = request.WorkDisplayName.Trim(),
                CreatedAt = now
            };
            dbContext.SourceWorks.Add(work);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (!string.Equals(work.DisplayName, request.WorkDisplayName.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Source work '{workKey}' is already registered with a different display name.");
        }

        var edition = await dbContext.SourceEditions
            .SingleOrDefaultAsync(
                value => value.SourceWorkId == work.Id && value.Key == editionKey,
                cancellationToken);
        if (edition is null)
        {
            edition = new SourceEdition
            {
                Id = Guid.NewGuid(),
                SourceWorkId = work.Id,
                Key = editionKey,
                DisplayName = request.EditionDisplayName.Trim(),
                CreatedAt = now
            };
            dbContext.SourceEditions.Add(edition);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (!string.Equals(edition.DisplayName, request.EditionDisplayName.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Source release '{editionKey}' is already registered with a different display name.");
        }

        var metadata = await SourceFrameworkStore.MergeEditionMetadataAsync(
            dbContext,
            edition.Id,
            preview.GameEdition,
            preview.ReleaseKind,
            preview.PublicationDate,
            cancellationToken);

        var imported = new List<ImportedSourceEntity>(parsedEntities.Count);
        foreach (var parsed in parsedEntities)
        {
            var entity = await dbContext.SourceEntities
                .SingleOrDefaultAsync(
                    value => value.SourceEditionId == edition.Id && value.NaturalKey == parsed.NaturalKey,
                    cancellationToken);

            if (entity is null)
            {
                entity = new SourceEntity
                {
                    Id = Guid.NewGuid(),
                    SourceEditionId = edition.Id,
                    EntityType = parsed.EntityType,
                    Name = parsed.Name,
                    SourceCode = parsed.SourceCode,
                    NaturalKey = parsed.NaturalKey,
                    CreatedAt = now
                };
                dbContext.SourceEntities.Add(entity);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var latestRevision = await dbContext.SourceEntityRevisions
                .Where(value => value.SourceEntityId == entity.Id)
                .OrderByDescending(value => value.RevisionNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestRevision is not null
                && string.Equals(latestRevision.Fingerprint, parsed.Fingerprint, StringComparison.Ordinal))
            {
                imported.Add(new ImportedSourceEntity(
                    entity.Id,
                    entity.EntityType,
                    entity.Name,
                    entity.SourceCode,
                    latestRevision.RevisionNumber,
                    latestRevision.Fingerprint,
                    CreatedRevision: false));
                continue;
            }

            var revision = new SourceEntityRevision
            {
                Id = Guid.NewGuid(),
                SourceEntityId = entity.Id,
                RevisionNumber = (latestRevision?.RevisionNumber ?? 0) + 1,
                Fingerprint = parsed.Fingerprint,
                RawJson = parsed.RawJson,
                ImportedAt = DateTimeOffset.UtcNow
            };
            dbContext.SourceEntityRevisions.Add(revision);
            await dbContext.SaveChangesAsync(cancellationToken);

            imported.Add(new ImportedSourceEntity(
                entity.Id,
                entity.EntityType,
                entity.Name,
                entity.SourceCode,
                revision.RevisionNumber,
                revision.Fingerprint,
                CreatedRevision: true));
        }

        await transaction.CommitAsync(cancellationToken);
        return new SourceImportResult(
            package.Id,
            work.Id,
            edition.Id,
            imported,
            metadata.GameEdition,
            metadata.ReleaseKind,
            metadata.PublicationDate);
    }

    private static IReadOnlyList<ParsedSourceEntity> ParseEntities(string json, string editionKey)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A 5e.tools source document must have a JSON object root.");
        }

        var parsed = new List<ParsedSourceEntity>();
        var naturalKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Name.StartsWith('_') || property.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in property.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException(
                        $"5e.tools entity array '{property.Name}' contained a non-object value.");
                }

                if (!item.TryGetProperty("name", out var nameElement)
                    || nameElement.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(nameElement.GetString()))
                {
                    throw new InvalidDataException(
                        $"5e.tools entity array '{property.Name}' contained an entity without a name.");
                }

                var name = nameElement.GetString()!.Trim();
                var sourceCode = item.TryGetProperty("source", out var sourceElement)
                    && sourceElement.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(sourceElement.GetString())
                        ? sourceElement.GetString()!.Trim()
                        : editionKey;
                var identitySuffix = GetIdentitySuffix(item);
                var naturalKey = NormalizeKey(
                    $"{property.Name}|{sourceCode}|{name}{(identitySuffix is null ? string.Empty : $"|{identitySuffix}")}");

                if (!naturalKeys.Add(naturalKey))
                {
                    throw new InvalidDataException(
                        $"The source document contains duplicate entity identity '{naturalKey}'.");
                }

                var rawJson = item.GetRawText();
                var canonicalJson = Canonicalize(item);
                var fingerprint = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)))
                    .ToLowerInvariant();

                parsed.Add(new ParsedSourceEntity(
                    property.Name,
                    name,
                    sourceCode,
                    naturalKey,
                    fingerprint,
                    rawJson));
            }
        }

        return parsed;
    }

    private static string? NormalizeGameEdition(Import5eToolsDocumentRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.GameEdition))
        {
            return DndEditionCatalog.NormalizeImportLabel(request.GameEdition);
        }

        foreach (var candidate in new[] { request.EditionKey, request.EditionDisplayName })
        {
            if (DndEditionCatalog.TryParse(candidate, out var edition))
            {
                return DndEditionCatalog.GetCanonicalLabel(edition);
            }
        }
        return null;
    }

    private static string? GetIdentitySuffix(JsonElement entity)
    {
        foreach (var key in new[] { "uniqueId", "id" })
        {
            if (!entity.TryGetProperty(key, out var value))
            {
                continue;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String when !string.IsNullOrWhiteSpace(value.GetString()) => value.GetString()!.Trim(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null
            };
        }

        return null;
    }

    private static string Canonicalize(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonical(writer, element);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(value => value.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var child in element.EnumerateArray())
                {
                    WriteCanonical(writer, child);
                }
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static void ValidateRequest(Import5eToolsDocumentRequest request)
    {
        Require(request.PackageKey, nameof(request.PackageKey));
        Require(request.PackageDisplayName, nameof(request.PackageDisplayName));
        Require(request.Provider, nameof(request.Provider));
        Require(request.WorkKey, nameof(request.WorkKey));
        Require(request.WorkDisplayName, nameof(request.WorkDisplayName));
        Require(request.EditionKey, nameof(request.EditionKey));
        Require(request.EditionDisplayName, nameof(request.EditionDisplayName));
        Require(request.Json, nameof(request.Json));
    }

    private static void AddPackageConflicts(
        SourcePackage package,
        Import5eToolsDocumentRequest request,
        ICollection<string> conflicts)
    {
        if (!string.Equals(package.DisplayName, request.PackageDisplayName.Trim(), StringComparison.Ordinal))
        {
            conflicts.Add($"Source package '{package.Key}' already has display name '{package.DisplayName}'.");
        }
        if (!string.Equals(package.Provider, request.Provider.Trim(), StringComparison.Ordinal))
        {
            conflicts.Add($"Source package '{package.Key}' already has provider '{package.Provider}'.");
        }
        if (!string.Equals(package.License, NormalizeOptional(request.License), StringComparison.Ordinal))
        {
            conflicts.Add($"Source package '{package.Key}' already has different license metadata.");
        }
        if (package.IsPublic != request.IsPublic)
        {
            conflicts.Add($"Source package '{package.Key}' already has different public/restricted visibility.");
        }
    }

    private static void AddMetadataConflict(
        string? existing,
        string? requested,
        string label,
        ICollection<string> conflicts)
    {
        if (existing is not null && requested is not null
            && !string.Equals(existing, requested, StringComparison.Ordinal))
        {
            conflicts.Add($"Source release already records {label} '{existing}', not '{requested}'.");
        }
    }

    private static void AddMetadataConflict(
        DateOnly? existing,
        DateOnly? requested,
        string label,
        ICollection<string> conflicts)
    {
        if (existing.HasValue && requested.HasValue && existing.Value != requested.Value)
        {
            conflicts.Add($"Source release already records {label} '{existing:yyyy-MM-dd}', not '{requested:yyyy-MM-dd}'.");
        }
    }

    private static void Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value can not be blank.", parameterName);
        }
    }

    private static string NormalizeKey(string value) => value.Trim().ToLowerInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void EnsurePackageIdentityMatches(
        SourcePackage package,
        Import5eToolsDocumentRequest request)
    {
        if (!string.Equals(package.DisplayName, request.PackageDisplayName.Trim(), StringComparison.Ordinal)
            || !string.Equals(package.Provider, request.Provider.Trim(), StringComparison.Ordinal)
            || !string.Equals(package.License, NormalizeOptional(request.License), StringComparison.Ordinal)
            || package.IsPublic != request.IsPublic)
        {
            throw new InvalidOperationException(
                $"Source package '{package.Key}' is already registered with different immutable metadata.");
        }
    }

    private sealed record ParsedSourceEntity(
        string EntityType,
        string Name,
        string SourceCode,
        string NaturalKey,
        string Fingerprint,
        string RawJson);
}

public sealed class SourceCatalogService(RulesCoreDbContext dbContext) : ISourceCatalogService
{
    public async Task<IReadOnlyList<SourcePackageSummary>> GetAccessiblePackagesAsync(
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeUserId(userId);

        return await dbContext.SourcePackages
            .AsNoTracking()
            .Where(value =>
                value.IsPublic
                || (normalizedUserId != null
                    && value.UserGrants.Any(grant => grant.UserId == normalizedUserId)))
            .OrderBy(value => value.DisplayName)
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
            .Include(value => value.SourceEdition)
                .ThenInclude(value => value.SourceWork)
                .ThenInclude(value => value.SourcePackage)
            .SingleOrDefaultAsync(
                value => value.Id == entityId
                    && (value.SourceEdition.SourceWork.SourcePackage.IsPublic
                        || (normalizedUserId != null
                            && value.SourceEdition.SourceWork.SourcePackage.UserGrants
                                .Any(grant => grant.UserId == normalizedUserId))),
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

        return new SourceEntityView(
            entity.Id,
            entity.EntityType,
            entity.Name,
            entity.SourceCode,
            revision.RevisionNumber,
            revision.Fingerprint,
            revision.ImportedAt,
            package.Key,
            package.DisplayName,
            work.Key,
            work.DisplayName,
            edition.Key,
            edition.DisplayName,
            document.RootElement.Clone());
    }

    private static string? NormalizeUserId(string? userId) =>
        string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
}

public sealed class SourceGrantService(RulesCoreDbContext dbContext) : ISourceGrantService
{
    public async Task GrantAsync(
        string userId,
        Guid sourcePackageId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = RequireUserId(userId);
        var packageExists = await dbContext.SourcePackages
            .AnyAsync(value => value.Id == sourcePackageId, cancellationToken);
        if (!packageExists)
        {
            throw new KeyNotFoundException($"Source package '{sourcePackageId}' does not exist.");
        }

        var existing = await dbContext.UserSourceGrants
            .AnyAsync(
                value => value.UserId == normalizedUserId
                    && value.SourcePackageId == sourcePackageId,
                cancellationToken);
        if (existing)
        {
            return;
        }

        dbContext.UserSourceGrants.Add(new UserSourceGrant
        {
            Id = Guid.NewGuid(),
            SourcePackageId = sourcePackageId,
            UserId = normalizedUserId,
            GrantedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RevokeAsync(
        string userId,
        Guid sourcePackageId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = RequireUserId(userId);
        var grant = await dbContext.UserSourceGrants
            .SingleOrDefaultAsync(
                value => value.UserId == normalizedUserId
                    && value.SourcePackageId == sourcePackageId,
                cancellationToken);
        if (grant is null)
        {
            return false;
        }

        dbContext.UserSourceGrants.Remove(grant);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> HasGrantAsync(
        string userId,
        Guid sourcePackageId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = RequireUserId(userId);
        return await dbContext.UserSourceGrants
            .AsNoTracking()
            .AnyAsync(
                value => value.UserId == normalizedUserId
                    && value.SourcePackageId == sourcePackageId,
                cancellationToken);
    }

    private static string RequireUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(userId));
        }

        return userId.Trim();
    }
}
