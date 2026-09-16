using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Bootstrap;

public sealed record BundledSrdMaintenanceSourceView(
    string WorkKey,
    string DisplayName,
    string PackageKey,
    string PackageDisplayName,
    string EditionKey,
    string EditionDisplayName,
    string? GameEdition,
    string FileName);

public sealed record BundledSrdReprocessView(
    string WorkKey,
    string DisplayName,
    int ProcessedEntityCount,
    int CreatedNativeRevisionCount,
    int PreservedNativeRevisionCount,
    int ReconciliationIssueCount);

/// <summary>
/// Rules Lawyer maintenance surface for bundled SRDs. Reprocessing is intentionally distinct
/// from a hosted-source refresh: it reuses the checked-in source representation and only reruns
/// current Rules Core interpretation/reconciliation code.
/// </summary>
public sealed class BundledSrdMaintenanceService(RulesCoreDbContext dbContext)
{
    public IReadOnlyList<BundledSrdMaintenanceSourceView> List()
    {
        return BundledSrdSnapshots.Definitions
            .Select(snapshot =>
            {
                var package = RulesCoreBaselineCatalog.SourcePackages.Single(value =>
                    string.Equals(value.Key, snapshot.PackageKey, StringComparison.Ordinal));
                var work = package.Works.Single(value =>
                    string.Equals(value.Key, snapshot.WorkKey, StringComparison.Ordinal));
                return new BundledSrdMaintenanceSourceView(
                    snapshot.WorkKey,
                    work.DisplayName,
                    package.Key,
                    package.DisplayName,
                    work.EditionKey,
                    work.EditionDisplayName,
                    work.GameEdition,
                    snapshot.FileName);
            })
            .ToArray();
    }

    public async Task<BundledSrdReprocessView> ReprocessAsync(
        string workKey,
        CancellationToken cancellationToken = default)
    {
        var source = List().SingleOrDefault(value =>
            string.Equals(value.WorkKey, workKey?.Trim(), StringComparison.Ordinal));
        if (source is null)
        {
            throw new KeyNotFoundException($"Bundled SRD '{workKey}' was not found.");
        }

        var result = await BundledSrdSnapshots.ReprocessAsync(
            dbContext,
            source.WorkKey,
            cancellationToken);
        var createdNativeRevisions = result.Entities.Count(value => value.CreatedRevision);

        return new BundledSrdReprocessView(
            source.WorkKey,
            source.DisplayName,
            result.Entities.Count,
            createdNativeRevisions,
            result.Entities.Count - createdNativeRevisions,
            result.ReconciliationIssues.Count);
    }
}
