using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Bootstrap;

internal static class BundledSrdSnapshots
{
    public static readonly IReadOnlyList<BundledSrdSnapshotSeed> Definitions =
    [
        new("wotc-srd-ogl", "srd-3e", "srd-3e.json", "SRD3"),
        new("wotc-srd-ogl", "srd-3-5e", "srd-3-5e.json", "SRD35"),
        new("wotc-srd-cc", "srd-5-1", "srd-5-1.json", "SRD51"),
        new("wotc-srd-cc", "srd-5-2-1", "srd-5-2-1.json", "SRD52")
    ];

    /// <summary>
    /// Ensures every bundled SRD has been hydrated through the normalized Source Layer.
    /// Exact representations already processed by the current adapter family are skipped;
    /// deliberate translator/schema replay remains the responsibility of ReprocessAsync.
    /// The legacy importer parameter remains only to preserve the bootstrapper constructor
    /// contract while older callers are migrated and is deliberately not used for SRD persistence.
    /// </summary>
    public static async Task<IReadOnlyList<NormalizedSourceImportResult>> EnsureAsync(
        RulesCoreDbContext dbContext,
        ISourceImportService legacyImporter,
        CancellationToken cancellationToken = default)
    {
        _ = legacyImporter;
        var imported = new List<NormalizedSourceImportResult>(Definitions.Count);
        foreach (var snapshot in Definitions)
        {
            var artifact = await LoadArtifactAsync(snapshot, cancellationToken);
            if (await IsCurrentHydrationAsync(dbContext, snapshot, artifact, cancellationToken))
            {
                continue;
            }

            imported.Add(await ImportSnapshotAsync(
                dbContext,
                snapshot,
                artifact,
                cancellationToken));
        }

        return imported;
    }

    /// <summary>
    /// Re-runs one checked-in SRD representation through the current adapter, translation,
    /// persistence, and canonical-reconciliation pipeline. This deliberately uses the same
    /// embedded source evidence rather than fetching upstream content. Normalized import
    /// semantics preserve Source Layer identity/history and Rules Layer references; translator
    /// changes can therefore update ContentJson without fabricating a native source revision.
    /// </summary>
    internal static async Task<NormalizedSourceImportResult> ReprocessAsync(
        RulesCoreDbContext dbContext,
        string workKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedWorkKey = workKey?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedWorkKey))
        {
            throw new ArgumentException("Bundled SRD work key is required.", nameof(workKey));
        }

        var snapshot = Definitions.SingleOrDefault(value =>
            string.Equals(value.WorkKey, normalizedWorkKey, StringComparison.Ordinal));
        if (snapshot is null)
        {
            throw new KeyNotFoundException(
                $"Bundled SRD '{normalizedWorkKey}' was not found.");
        }

        var artifact = await LoadArtifactAsync(snapshot, cancellationToken);
        return await ImportSnapshotAsync(
            dbContext,
            snapshot,
            artifact,
            cancellationToken);
    }

    private static async Task<NormalizedSourceImportResult> ImportSnapshotAsync(
        RulesCoreDbContext dbContext,
        BundledSrdSnapshotSeed snapshot,
        SourceRepresentationArtifact artifact,
        CancellationToken cancellationToken)
    {
        var package = RulesCoreBaselineCatalog.SourcePackages.Single(value =>
            string.Equals(value.Key, snapshot.PackageKey, StringComparison.Ordinal));
        var work = package.Works.Single(value =>
            string.Equals(value.Key, snapshot.WorkKey, StringComparison.Ordinal));
        var originIdentity = artifact.OriginIdentity;
        var adapter = snapshot.IsLegacy
            ? (ISourceFormatAdapter)new LegacySrdSourceFormatAdapter()
            : new FiveEToolsSourceFormatAdapter();
        var representation = adapter.TryRead(artifact)
            ?? throw new InvalidDataException(
                $"Bundled SRD snapshot '{snapshot.FileName}' did not produce a normalized source representation.");

        // The former 5e.tools import wrapper filled edition/date evidence supplied by
        // the reviewed bootstrap catalog. Preserve the same canonical evidence while
        // leaving source membership authority in source_package_authority_reference.
        representation = representation with
        {
            Publications = (representation.Publications ?? [])
                .Select(value => value with
                {
                    GameEdition = value.GameEdition ?? work.GameEdition,
                    PublicationDate = value.PublicationDate ?? work.PublicationDate
                })
                .ToArray()
        };

        if (snapshot.IsLegacy)
        {
            await UpgradeLegacySnapshotIdentityAsync(
                dbContext,
                package.Key,
                originIdentity,
                cancellationToken);
        }

        var result = await new NormalizedSourceImportService(dbContext).ImportAsync(
            new ImportNormalizedSourceRequest(
                package.Key,
                package.DisplayName,
                package.Provider,
                package.License,
                package.IsPublic,
                representation),
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(work.ReleaseKind))
        {
            var releaseKinds = new CanonicalPublicationReleaseKindService(dbContext);
            foreach (var publication in result.Publications)
            {
                await releaseKinds.MergeAsync(
                    publication.CanonicalPublicationId,
                    work.ReleaseKind,
                    cancellationToken);
            }
        }

        return result;
    }

    private static async Task<bool> IsCurrentHydrationAsync(
        RulesCoreDbContext dbContext,
        BundledSrdSnapshotSeed snapshot,
        SourceRepresentationArtifact artifact,
        CancellationToken cancellationToken)
    {
        var packageId = await dbContext.SourcePackages
            .AsNoTracking()
            .Where(value => value.Key == snapshot.PackageKey)
            .Select(value => (Guid?)value.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (packageId is null)
        {
            return false;
        }

        var expectedFormat = snapshot.IsLegacy
            ? LegacySrdSourceFormatAdapter.Format
            : FiveEToolsSourceFormatAdapter.Format;
        var contentHash = Convert.ToHexString(SHA256.HashData(artifact.Content)).ToLowerInvariant();
        var hasExactRepresentation = await dbContext.SourceRepresentations
            .AsNoTracking()
            .AnyAsync(value => value.SourcePackageId == packageId.Value
                && value.OriginIdentity == artifact.OriginIdentity
                && value.ContentSha256 == contentHash
                && value.FormatKey == expectedFormat,
                cancellationToken);
        if (!hasExactRepresentation)
        {
            return false;
        }

        // Representation equality alone is not enough while upgrading installations from
        // the pre-normalized bootstrap path. Probe persisted adapter-owned state so an old
        // representation is processed once by this pipeline, while subsequent startups can
        // avoid thousands of no-op per-record PostgreSQL round trips.
        if (snapshot.IsLegacy)
        {
            var entityId = await dbContext.SourceEntities
                .AsNoTracking()
                .Where(value => value.SourcePackageId == packageId.Value
                    && value.SourceCode == snapshot.SourceCode
                    && value.FormatKey == expectedFormat)
                .OrderByDescending(value => value.CreatedAt)
                .Select(value => (Guid?)value.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (entityId is null)
            {
                return false;
            }

            var contentJson = await dbContext.SourceEntityRevisions
                .AsNoTracking()
                .Where(value => value.SourceEntityId == entityId.Value)
                .OrderByDescending(value => value.RevisionNumber)
                .Select(value => value.ContentJson)
                .FirstOrDefaultAsync(cancellationToken);
            return HasCurrentLegacyTranslation(contentJson);
        }

        var nativeIdentityJson = await dbContext.SourceEntities
            .AsNoTracking()
            .Where(value => value.SourcePackageId == packageId.Value
                && value.SourceCode == snapshot.SourceCode
                && value.FormatKey == expectedFormat)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => value.NativeIdentityJson)
            .FirstOrDefaultAsync(cancellationToken);
        return HasCurrentFiveEToolsIdentity(nativeIdentityJson);
    }

    private static bool HasCurrentLegacyTranslation(string? contentJson)
    {
        if (string.IsNullOrWhiteSpace(contentJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(contentJson);
            return document.RootElement.TryGetProperty("_rulesCore", out var extension)
                && extension.ValueKind == JsonValueKind.Object
                && extension.TryGetProperty("context", out var context)
                && context.ValueKind == JsonValueKind.Object
                && context.TryGetProperty("sourceFormat", out var sourceFormat)
                && sourceFormat.ValueKind == JsonValueKind.String
                && string.Equals(
                    sourceFormat.GetString(),
                    LegacySrdSourceFormatAdapter.Format,
                    StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasCurrentFiveEToolsIdentity(string? nativeIdentityJson)
    {
        if (string.IsNullOrWhiteSpace(nativeIdentityJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(nativeIdentityJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("entityType", out var entityType)
                && entityType.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(entityType.GetString());
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task UpgradeLegacySnapshotIdentityAsync(
        RulesCoreDbContext dbContext,
        string packageKey,
        string originIdentity,
        CancellationToken cancellationToken)
    {
        var packageId = await dbContext.SourcePackages
            .AsNoTracking()
            .Where(value => value.Key == packageKey)
            .Select(value => value.Id)
            .SingleAsync(cancellationToken);

        // Existing installations imported the exact same legacy native objects through the
        // pseudo-5e.tools adapter. Re-key only entities linked to this known bootstrap-managed
        // representation. IDs, revisions, canonical bindings, and Rules Layer references stay
        // untouched; the subsequent normalized import can therefore update ContentJson in
        // place without manufacturing a native revision.
        await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
            UPDATE source_entity AS entity
            SET format_key = {{LegacySrdSourceFormatAdapter.Format}}
            FROM source_representation_entity AS link
            INNER JOIN source_representation AS representation
                ON representation.source_representation_id = link.source_representation_id
            WHERE entity.source_entity_id = link.source_entity_id
                AND representation.source_package_id = {{packageId}}
                AND representation.origin_identity = {{originIdentity}}
                AND entity.format_key = {{FiveEToolsSourceFormatAdapter.Format}};

            UPDATE source_representation
            SET format_key = {{LegacySrdSourceFormatAdapter.Format}}
            WHERE source_package_id = {{packageId}}
                AND origin_identity = {{originIdentity}}
                AND format_key = {{FiveEToolsSourceFormatAdapter.Format}};
            """, cancellationToken);
    }

    private static async Task<SourceRepresentationArtifact> LoadArtifactAsync(
        BundledSrdSnapshotSeed snapshot,
        CancellationToken cancellationToken)
    {
        var package = RulesCoreBaselineCatalog.SourcePackages.Single(value =>
            string.Equals(value.Key, snapshot.PackageKey, StringComparison.Ordinal));
        var work = package.Works.Single(value =>
            string.Equals(value.Key, snapshot.WorkKey, StringComparison.Ordinal));
        var originIdentity = $"admin:{package.Key}:{work.Key}:{work.EditionKey}";
        var json = await LoadAsync(snapshot.FileName, cancellationToken);
        byte[] bytes;
        try
        {
            // Match the old hosted/bootstrap byte representation exactly: the previous
            // path read text then encoded UTF-8 without a BOM before normalization.
            bytes = new UTF8Encoding(false, true).GetBytes(json);
        }
        catch (EncoderFallbackException exception)
        {
            throw new InvalidDataException(
                $"Bundled SRD snapshot '{snapshot.FileName}' is not valid UTF-8 text.",
                exception);
        }

        return new SourceRepresentationArtifact(
            snapshot.FileName,
            bytes,
            originIdentity,
            SourceUri: $"embedded://rules-core/{snapshot.FileName}",
            MediaType: "application/json");
    }

    private static async Task<string> LoadAsync(
        string fileName,
        CancellationToken cancellationToken)
    {
        var assembly = typeof(BundledSrdSnapshots).Assembly;
        var resourceName = $"RulesCore.Infrastructure.BundledSources.{fileName}";
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Bundled SRD snapshot resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}

internal sealed record BundledSrdSnapshotSeed(
    string PackageKey,
    string WorkKey,
    string FileName,
    string SourceCode)
{
    public bool IsLegacy =>
        string.Equals(WorkKey, "srd-3e", StringComparison.Ordinal)
        || string.Equals(WorkKey, "srd-3-5e", StringComparison.Ordinal);
}
