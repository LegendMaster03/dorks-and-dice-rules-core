using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Extends normalized imports with companion-content persistence and logical-history
/// reconciliation while leaving the existing mechanical import transaction unchanged.
/// </summary>
public sealed class ReconciledNormalizedSourceImportService(
    NormalizedSourceImportService inner,
    RulesCoreDbContext dbContext) : INormalizedSourceImportService
{
    public async Task<NormalizedSourceImportResult> ImportAsync(
        ImportNormalizedSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Representation);

        NormalizedSourceImportResult result;
        if (request.Representation.Records.Count == 0)
        {
            if (request.Representation.CompanionContents.Count == 0)
            {
                return await inner.ImportAsync(request, cancellationToken);
            }
            result = await StoreCompanionOnlyRepresentationAsync(request, cancellationToken);
        }
        else
        {
            result = await inner.ImportAsync(request, cancellationToken);
        }

        if (request.Representation.CompanionContents.Count != 0)
        {
            var contentSha = Convert.ToHexString(
                SHA256.HashData(request.Representation.Artifact.Content)).ToLowerInvariant();
            var representationId = await dbContext.SourceRepresentations
                .AsNoTracking()
                .Where(value => value.SourcePackageId == result.PackageId
                    && value.OriginIdentity == request.Representation.Artifact.OriginIdentity
                    && value.ContentSha256 == contentSha)
                .Select(value => (Guid?)value.Id)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "The imported source representation could not be found for companion attachment.");
            var companions = new SourceCompanionContentStore(dbContext);
            await companions.PersistAsync(
                result.PackageId,
                representationId,
                request.Representation.CompanionContents,
                cancellationToken);
        }

        if (result.Entities.Count != 0)
        {
            await new SourceCompanionContentStore(dbContext)
                .ResolvePendingAsync(result.PackageId, cancellationToken);
            await new CanonicalReferenceHistoryReconciliationService(dbContext)
                .ReconcileSourceEntitiesAsync(
                    result.Entities.Select(value => value.EntityId).ToArray(),
                    cancellationToken);
        }

        var sourceCodes = result.SourceCodes
            .Concat(request.Representation.CompanionContents.Select(value => value.SourceCode))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new NormalizedSourceImportResult(
            result.PackageId,
            result.Publications,
            result.Entities,
            sourceCodes)
        {
            ReconciliationIssues = result.ReconciliationIssues
        };
    }

    private async Task<NormalizedSourceImportResult> StoreCompanionOnlyRepresentationAsync(
        ImportNormalizedSourceRequest request,
        CancellationToken cancellationToken)
    {
        SourceImportExecutionPolicy.Apply(dbContext);
        var packageKey = NormalizeKey(request.PackageKey);
        var artifact = request.Representation.Artifact;
        if (artifact.Content.Length == 0)
        {
            throw new InvalidDataException("Source representation content can not be empty.");
        }
        var contentSha = Convert.ToHexString(SHA256.HashData(artifact.Content)).ToLowerInvariant();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({packageKey}, 0));",
            cancellationToken);

        var package = await dbContext.SourcePackages
            .SingleOrDefaultAsync(value => value.Key == packageKey, cancellationToken);
        if (package is null)
        {
            package = new SourcePackage
            {
                Id = Guid.NewGuid(),
                Key = packageKey,
                DisplayName = Require(request.PackageDisplayName, 300),
                Provider = Require(request.Provider, 200),
                License = NormalizeOptional(request.License, 300),
                IsPublic = request.IsPublic,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.SourcePackages.Add(package);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            EnsurePackageMatches(package, request);
        }

        if (!await dbContext.SourceContentBlobs.AnyAsync(value => value.Sha256 == contentSha, cancellationToken))
        {
            dbContext.SourceContentBlobs.Add(new SourceContentBlob
            {
                Sha256 = contentSha,
                ContentLength = artifact.Content.LongLength,
                ContentBytes = artifact.Content,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var representation = await dbContext.SourceRepresentations
            .SingleOrDefaultAsync(value => value.SourcePackageId == package.Id
                && value.OriginIdentity == artifact.OriginIdentity
                && value.ContentSha256 == contentSha,
                cancellationToken);
        if (representation is null)
        {
            var previousId = await dbContext.SourceRepresentations
                .Where(value => value.SourcePackageId == package.Id
                    && value.OriginIdentity == artifact.OriginIdentity)
                .OrderByDescending(value => value.ImportedAt)
                .Select(value => (Guid?)value.Id)
                .FirstOrDefaultAsync(cancellationToken);
            representation = new SourceRepresentation
            {
                Id = Guid.NewGuid(),
                SourcePackageId = package.Id,
                PreviousSourceRepresentationId = previousId,
                FormatKey = Require(request.Representation.FormatKey, 80),
                OriginIdentity = Require(artifact.OriginIdentity, 2000),
                FileName = Require(artifact.FileName, 500),
                SourceUri = NormalizeOptional(artifact.SourceUri, 2000),
                MediaType = NormalizeOptional(artifact.MediaType, 200),
                ContentSha256 = contentSha,
                ContentLength = artifact.Content.LongLength,
                MetadataJson = request.Representation.MetadataJson ?? "{}",
                ImportedAt = DateTimeOffset.UtcNow
            };
            dbContext.SourceRepresentations.Add(representation);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new NormalizedSourceImportResult(
            package.Id,
            [],
            [],
            request.Representation.CompanionContents
                .Select(value => value.SourceCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static void EnsurePackageMatches(SourcePackage package, ImportNormalizedSourceRequest request)
    {
        if (!string.Equals(package.DisplayName, Require(request.PackageDisplayName, 300), StringComparison.Ordinal)
            || !string.Equals(package.Provider, Require(request.Provider, 200), StringComparison.Ordinal)
            || !string.Equals(package.License, NormalizeOptional(request.License, 300), StringComparison.Ordinal)
            || package.IsPublic != request.IsPublic)
        {
            throw new InvalidOperationException(
                $"Source package '{package.Key}' already exists with different package metadata.");
        }
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Package key can not be blank.", nameof(value));
        var builder = new StringBuilder(value.Length);
        var pendingSeparator = false;
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length != 0) builder.Append('-');
                builder.Append(character);
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }
        var normalized = builder.ToString().Trim('-');
        if (normalized.Length == 0) throw new ArgumentException("Package key can not be empty after normalization.", nameof(value));
        if (normalized.Length > 200) throw new ArgumentException("Package key can not exceed 200 characters.", nameof(value));
        return normalized;
    }

    private static string Require(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value can not be blank.", nameof(value));
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"Value can not exceed {maxLength} characters.", nameof(value));
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"Value can not exceed {maxLength} characters.", nameof(value));
        return normalized;
    }
}
