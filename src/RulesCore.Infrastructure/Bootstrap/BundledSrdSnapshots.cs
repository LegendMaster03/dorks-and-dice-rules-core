using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

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

    public static async Task<IReadOnlyList<SourceImportResult>> EnsureAsync(
        RulesCoreDbContext dbContext,
        ISourceImportService importer,
        CancellationToken cancellationToken = default)
    {
        var imported = new List<SourceImportResult>(Definitions.Count);
        foreach (var snapshot in Definitions)
        {
            var alreadyAvailable = await dbContext.SourceEntities
                .AsNoTracking()
                .AnyAsync(value =>
                    value.SourceEdition.SourceWork.SourcePackage.Key == snapshot.PackageKey
                    && value.SourceEdition.SourceWork.Key == snapshot.WorkKey,
                    cancellationToken);
            if (alreadyAvailable)
            {
                continue;
            }

            var package = RulesCoreBaselineCatalog.SourcePackages.Single(value =>
                string.Equals(value.Key, snapshot.PackageKey, StringComparison.Ordinal));
            var work = package.Works.Single(value =>
                string.Equals(value.Key, snapshot.WorkKey, StringComparison.Ordinal));
            var json = await LoadAsync(snapshot.FileName, cancellationToken);

            imported.Add(await importer.Import5eToolsDocumentAsync(
                new Import5eToolsDocumentRequest(
                    PackageKey: package.Key,
                    PackageDisplayName: package.DisplayName,
                    Provider: package.Provider,
                    License: package.License,
                    IsPublic: package.IsPublic,
                    WorkKey: work.Key,
                    WorkDisplayName: work.DisplayName,
                    EditionKey: work.EditionKey,
                    EditionDisplayName: work.EditionDisplayName,
                    Json: json,
                    GameEdition: work.GameEdition,
                    ReleaseKind: work.ReleaseKind,
                    PublicationDate: work.PublicationDate),
                cancellationToken));
        }

        return imported;
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
    string FileName);
