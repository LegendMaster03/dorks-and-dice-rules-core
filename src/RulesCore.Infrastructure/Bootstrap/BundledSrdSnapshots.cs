using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Bootstrap;

internal static class BundledSrdSnapshots
{
    public static readonly IReadOnlyList<BundledSrdSnapshotSeed> Definitions =
    [
        new("wotc-srd-ogl", "srd-3e", "srd-3e.json"),
        new("wotc-srd-ogl", "srd-3-5e", "srd-3-5e.json"),
        new("wotc-srd-cc", "srd-5-1", "srd-5-1.json"),
        new("wotc-srd-cc", "srd-5-2-1", "srd-5-2-1.json")
    ];

    /// <summary>
    /// Imports every bundled SRD through the normalized Source Layer. The legacy importer
    /// parameter remains only to preserve the bootstrapper constructor contract while older
    /// callers are migrated; it is deliberately not used for SRD persistence.
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
            imported.Add(await ImportSnapshotAsync(dbContext, snapshot, cancellationToken));
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
    internal static Task<NormalizedSourceImportResult> ReprocessAsync(
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

        return ImportSnapshotAsync(dbContext, snapshot, cancellationToken);
    }

    private static async Task<NormalizedSourceImportResult> ImportSnapshotAsync(
        RulesCoreDbContext dbContext,
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

        var artifact = new SourceRepresentationArtifact(
            snapshot.FileName,
            bytes,
            originIdentity,
            SourceUri: $"embedded://rules-core/{snapshot.FileName}",
            MediaType: "application/json");
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
    string FileName)
{
    public bool IsLegacy =>
        string.Equals(WorkKey, "srd-3e", StringComparison.Ordinal)
        || string.Equals(WorkKey, "srd-3-5e", StringComparison.Ordinal);
}
