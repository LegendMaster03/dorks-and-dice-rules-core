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

        var packageKey = NormalizeKey(request.PackageKey);
        var representation = ReadRepresentation(request, packageKey);
        var conflicts = new List<string>();
        var warnings = new List<string>();

        var package = await dbContext.SourcePackages
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Key == packageKey, cancellationToken);
        if (package is not null)
        {
            AddPackageConflicts(package, request, conflicts);
        }

        var existing = package is null
            ? []
            : await dbContext.SourceEntities
                .AsNoTracking()
                .Include(value => value.Revisions)
                .Where(value => value.SourcePackageId == package.Id
                    && value.FormatKey == representation.FormatKey)
                .ToArrayAsync(cancellationToken);
        var existingByNativeKey = existing.ToDictionary(value => value.NativeKey, StringComparer.Ordinal);

        var previewEntities = new List<SourceImportPreviewEntity>(representation.Records.Count);
        foreach (var record in representation.Records)
        {
            var fingerprint = CanonicalJsonFingerprint(record.RawJson);
            if (!existingByNativeKey.TryGetValue(record.NativeKey, out var current))
            {
                previewEntities.Add(new SourceImportPreviewEntity(
                    null,
                    record.EntityType,
                    record.Name,
                    record.SourceCode ?? string.Empty,
                    null,
                    fingerprint,
                    SourceImportPreviewActions.NewEntity));
                continue;
            }

            var latest = current.Revisions.OrderByDescending(value => value.RevisionNumber).FirstOrDefault();
            previewEntities.Add(new SourceImportPreviewEntity(
                current.Id,
                record.EntityType,
                record.Name,
                record.SourceCode ?? string.Empty,
                latest?.RevisionNumber,
                fingerprint,
                latest is not null && string.Equals(latest.Fingerprint, fingerprint, StringComparison.Ordinal)
                    ? SourceImportPreviewActions.Unchanged
                    : SourceImportPreviewActions.NewRevision));
        }

        var gameEdition = NormalizeGameEdition(request)
            ?? representation.Publications?.Select(value => value.GameEdition).FirstOrDefault(value => value is not null);
        var releaseKind = SourceReleaseKinds.NormalizeImportLabel(request.ReleaseKind);
        var publisher = NormalizeOptional(request.Publisher)
            ?? representation.Publications?.Select(value => value.Publisher).FirstOrDefault(value => value is not null);
        var publicationDate = request.PublicationDate
            ?? representation.Publications?.Select(value => value.PublicationDate).FirstOrDefault(value => value.HasValue);

        if (gameEdition is null)
        {
            warnings.Add("No canonical D&D game edition could be identified. The source can still be imported and reconciled later.");
        }

        return new SourceImportPreviewResult(
            packageKey,
            request.PackageDisplayName.Trim(),
            request.Provider.Trim(),
            NormalizeOptional(request.License),
            request.IsPublic,
            NormalizeKey(request.WorkKey),
            request.WorkDisplayName.Trim(),
            NormalizeKey(request.EditionKey),
            request.EditionDisplayName.Trim(),
            gameEdition,
            releaseKind,
            publicationDate,
            conflicts.Count == 0,
            conflicts,
            warnings,
            previewEntities.Count,
            previewEntities.Count(value => value.Action == SourceImportPreviewActions.NewEntity),
            previewEntities.Count(value => value.Action == SourceImportPreviewActions.NewRevision),
            previewEntities.Count(value => value.Action == SourceImportPreviewActions.Unchanged),
            previewEntities,
            publisher);
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
                $"Source import conflicts with existing immutable package metadata: {string.Join(" ", preview.Conflicts)}");
        }

        var representation = ReadRepresentation(request, preview.PackageKey);
        var publications = (representation.Publications ?? [])
            .Select(value => value with
            {
                Publisher = value.Publisher ?? preview.Publisher,
                GameEdition = value.GameEdition ?? preview.GameEdition,
                PublicationDate = value.PublicationDate ?? preview.PublicationDate
            })
            .ToArray();
        representation = representation with { Publications = publications };

        var imported = await new NormalizedSourceImportService(dbContext).ImportAsync(
            new ImportNormalizedSourceRequest(
                preview.PackageKey,
                preview.PackageDisplayName,
                preview.Provider,
                preview.License,
                preview.IsPublic,
                representation),
            cancellationToken);

        if (preview.ReleaseKind is not null)
        {
            var releaseKinds = new CanonicalPublicationReleaseKindService(dbContext);
            foreach (var publication in imported.Publications)
            {
                await releaseKinds.MergeAsync(
                    publication.CanonicalPublicationId,
                    preview.ReleaseKind,
                    cancellationToken);
            }
        }

        return new SourceImportResult(
            imported.PackageId,
            imported.Entities,
            preview.GameEdition,
            preview.ReleaseKind,
            preview.PublicationDate,
            preview.Publisher);
    }

    private static NormalizedSourceRepresentation ReadRepresentation(
        Import5eToolsDocumentRequest request,
        string packageKey)
    {
        byte[] bytes;
        try
        {
            bytes = new UTF8Encoding(false, true).GetBytes(request.Json);
        }
        catch (EncoderFallbackException exception)
        {
            throw new InvalidDataException("The 5e.tools document is not valid UTF-8 text.", exception);
        }

        var artifact = new SourceRepresentationArtifact(
            $"{packageKey}.json",
            bytes,
            $"admin:{packageKey}:{NormalizeKey(request.WorkKey)}:{NormalizeKey(request.EditionKey)}",
            MediaType: "application/json");
        return new FiveEToolsSourceFormatAdapter().TryRead(artifact)
            ?? throw new InvalidDataException("The 5e.tools document did not contain a supported source representation.");
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

    private static void ValidateRequest(Import5eToolsDocumentRequest request)
    {
        Require(request.PackageKey, nameof(request.PackageKey));
        Require(request.PackageDisplayName, nameof(request.PackageDisplayName));
        Require(request.Provider, nameof(request.Provider));
        Require(request.WorkKey, nameof(request.WorkKey));
        Require(request.WorkDisplayName, nameof(request.WorkDisplayName));
        Require(request.EditionKey, nameof(request.EditionKey));
        Require(request.EditionDisplayName, nameof(request.EditionDisplayName));
        if (string.IsNullOrWhiteSpace(request.Json))
        {
            throw new ArgumentException("Source JSON can not be blank.", nameof(request.Json));
        }
    }

    private static void AddPackageConflicts(
        SourcePackage package,
        Import5eToolsDocumentRequest request,
        ICollection<string> conflicts)
    {
        if (!string.Equals(package.DisplayName, request.PackageDisplayName.Trim(), StringComparison.Ordinal))
        {
            conflicts.Add($"Source package '{package.Key}' is already registered as '{package.DisplayName}'.");
        }
        if (!string.Equals(package.Provider, request.Provider.Trim(), StringComparison.Ordinal))
        {
            conflicts.Add($"Source package '{package.Key}' is already registered with provider '{package.Provider}'.");
        }
        if (!string.Equals(package.License, NormalizeOptional(request.License), StringComparison.Ordinal))
        {
            conflicts.Add($"Source package '{package.Key}' is already registered with a different license.");
        }
        if (package.IsPublic != request.IsPublic)
        {
            conflicts.Add($"Source package '{package.Key}' already has a different visibility setting.");
        }
    }

    private static string CanonicalJsonFingerprint(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(writer, document.RootElement);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
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

    private static string NormalizeKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSeparator = false;
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }
                builder.Append(character);
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        var normalized = builder.ToString().Trim('-');
        if (string.IsNullOrEmpty(normalized))
        {
            throw new ArgumentException("A source key must contain at least one letter or number.", nameof(value));
        }
        if (normalized.Length > 200)
        {
            normalized = normalized[..200].TrimEnd('-');
        }
        return normalized;
    }

    private static string Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value can not be blank.", parameterName);
        }
        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

public sealed class SourceGrantService(RulesCoreDbContext dbContext) : ISourceGrantService
{
    public async Task GrantAsync(
        string userId,
        Guid sourcePackageId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeRequiredUserId(userId);
        if (!await dbContext.SourcePackages.AnyAsync(value => value.Id == sourcePackageId, cancellationToken))
        {
            throw new KeyNotFoundException($"Source package '{sourcePackageId}' does not exist.");
        }

        if (await dbContext.UserSourceGrants.AnyAsync(
                value => value.UserId == normalizedUserId && value.SourcePackageId == sourcePackageId,
                cancellationToken))
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
        var normalizedUserId = NormalizeRequiredUserId(userId);
        var grant = await dbContext.UserSourceGrants.SingleOrDefaultAsync(
            value => value.UserId == normalizedUserId && value.SourcePackageId == sourcePackageId,
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
        var normalizedUserId = NormalizeRequiredUserId(userId);
        return await dbContext.SourcePackages.AnyAsync(
            value => value.Id == sourcePackageId
                && (value.IsPublic || value.UserGrants.Any(grant => grant.UserId == normalizedUserId)),
            cancellationToken);
    }

    private static string NormalizeRequiredUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(userId));
        }
        return userId.Trim();
    }
}
