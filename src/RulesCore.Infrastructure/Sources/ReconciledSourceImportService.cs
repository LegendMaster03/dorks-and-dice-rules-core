using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Applies the same companion-content and reference-history reconciliation to the legacy/admin
/// 5e.tools import surface. Preview semantics remain mechanical-entity oriented.
/// </summary>
public sealed class ReconciledSourceImportService(
    SourceImportService inner,
    RulesCoreDbContext dbContext) : ISourceImportService
{
    public Task<SourceImportPreviewResult> Preview5eToolsDocumentAsync(
        Import5eToolsDocumentRequest request,
        CancellationToken cancellationToken = default) =>
        inner.Preview5eToolsDocumentAsync(request, cancellationToken);

    public async Task<SourceImportResult> Import5eToolsDocumentAsync(
        Import5eToolsDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var packageKey = NormalizeKey(request.PackageKey);
        var bytes = new UTF8Encoding(false, true).GetBytes(request.Json);
        var artifact = new SourceRepresentationArtifact(
            $"{packageKey}.json",
            bytes,
            $"admin:{packageKey}:{NormalizeKey(request.WorkKey)}:{NormalizeKey(request.EditionKey)}",
            MediaType: "application/json");
        var companionRepresentation = new FiveEToolsCompanionSourceFormatAdapter().TryRead(artifact);

        var imported = await inner.Import5eToolsDocumentAsync(request, cancellationToken);
        if (companionRepresentation is { CompanionContents.Count: > 0 })
        {
            var contentSha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var representationId = await dbContext.SourceRepresentations
                .AsNoTracking()
                .Where(value => value.SourcePackageId == imported.PackageId
                    && value.OriginIdentity == artifact.OriginIdentity
                    && value.ContentSha256 == contentSha)
                .Select(value => (Guid?)value.Id)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "The legacy source representation could not be found for companion attachment.");
            var companions = new SourceCompanionContentStore(dbContext);
            await companions.PersistAsync(
                imported.PackageId,
                representationId,
                companionRepresentation.CompanionContents,
                cancellationToken);
            await companions.ResolvePendingAsync(imported.PackageId, cancellationToken);
        }

        if (imported.Entities.Count != 0)
        {
            await new CanonicalReferenceHistoryReconciliationService(dbContext)
                .ReconcileSourceEntitiesAsync(
                    imported.Entities.Select(value => value.EntityId).ToArray(),
                    cancellationToken);
        }
        return imported;
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Key can not be blank.", nameof(value));
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
        if (normalized.Length == 0) throw new ArgumentException("Key can not be empty after normalization.", nameof(value));
        return normalized;
    }
}
