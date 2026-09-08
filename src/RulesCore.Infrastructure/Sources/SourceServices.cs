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
    public async Task<SourceImportResult> Import5eToolsDocumentAsync(
        Import5eToolsDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var packageKey = NormalizeKey(request.PackageKey);
        var workKey = NormalizeKey(request.WorkKey);
        var editionKey = NormalizeKey(request.EditionKey);
        var parsedEntities = ParseEntities(request.Json, editionKey);
        if (parsedEntities.Count == 0)
        {
            throw new InvalidDataException("The 5e.tools document did not contain any importable entity arrays.");
        }

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
                $"Source edition '{editionKey}' is already registered with a different display name.");
        }

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
        return new SourceImportResult(package.Id, work.Id, edition.Id, imported);
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
    public async Task<IReadOnlyList<SourcePackageSummary>> GetPublicPackagesAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.SourcePackages
            .AsNoTracking()
            .Where(value => value.IsPublic)
            .OrderBy(value => value.DisplayName)
            .Select(value => new SourcePackageSummary(
                value.Id,
                value.Key,
                value.DisplayName,
                value.Provider,
                value.License))
            .ToArrayAsync(cancellationToken);

    public async Task<SourceEntityView?> GetLatestPublicEntityAsync(
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SourceEntities
            .AsNoTracking()
            .Include(value => value.Revisions)
            .Include(value => value.SourceEdition)
                .ThenInclude(value => value.SourceWork)
                .ThenInclude(value => value.SourcePackage)
            .SingleOrDefaultAsync(
                value => value.Id == entityId
                    && value.SourceEdition.SourceWork.SourcePackage.IsPublic,
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
}
