using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Reads complete global or campaign resolved-rules snapshots. Internal Rules Core callers should
/// provide the DbContext to use the bulk snapshot path. The catalog-service overload remains for
/// compatibility with consumers that have not yet migrated from paged catalog traversal.
/// </summary>
internal sealed class ResolvedRulesSnapshotReader
{
    private const int PageSize = 500;
    private readonly ResolvedRulesSnapshotService? snapshots;
    private readonly IResolvedRulesCatalogService? pagedCatalog;

    internal ResolvedRulesSnapshotReader(RulesCoreDbContext dbContext)
    {
        snapshots = new ResolvedRulesSnapshotService(dbContext);
    }

    internal ResolvedRulesSnapshotReader(IResolvedRulesCatalogService resolvedRules)
    {
        pagedCatalog = resolvedRules;
    }

    internal Task<ResolvedRulesCatalogView> ReadAllGlobalAsync(
        string? userId,
        CancellationToken cancellationToken) =>
        snapshots is not null
            ? snapshots.ReadGlobalAsync(userId, cancellationToken)
            : ReadPagedGlobalAsync(userId, cancellationToken);

    internal Task<ResolvedRulesCatalogView> ReadAllCampaignAsync(
        Guid campaignId,
        string userId,
        CancellationToken cancellationToken) =>
        snapshots is not null
            ? snapshots.ReadCampaignAsync(campaignId, userId, cancellationToken)
            : ReadPagedCampaignAsync(campaignId, userId, cancellationToken);

    private async Task<ResolvedRulesCatalogView> ReadPagedGlobalAsync(
        string? userId,
        CancellationToken cancellationToken)
    {
        var all = new List<ResolvedRuleCatalogItemView>();
        ResolvedRulesCatalogView? page = null;
        var offset = 0;
        while (true)
        {
            page = await pagedCatalog!.GetGlobalPageAsync(
                userId,
                limit: PageSize,
                offset: offset,
                cancellationToken: cancellationToken);
            if (all.Count == 0 && page.TotalCount > 0)
            {
                all.Capacity = page.TotalCount;
            }
            all.AddRange(page.Rules);
            if (page.Rules.Count == 0 || all.Count >= page.TotalCount)
            {
                break;
            }
            offset += page.Rules.Count;
        }

        page ??= new ResolvedRulesCatalogView(
            "global", null, null, null, 0, [], [], []);
        return page with { Rules = all };
    }

    private async Task<ResolvedRulesCatalogView> ReadPagedCampaignAsync(
        Guid campaignId,
        string userId,
        CancellationToken cancellationToken)
    {
        var all = new List<ResolvedRuleCatalogItemView>();
        ResolvedRulesCatalogView? page = null;
        var offset = 0;
        while (true)
        {
            page = await pagedCatalog!.GetCampaignPageAsync(
                campaignId,
                userId,
                limit: PageSize,
                offset: offset,
                cancellationToken: cancellationToken);
            if (all.Count == 0 && page.TotalCount > 0)
            {
                all.Capacity = page.TotalCount;
            }
            all.AddRange(page.Rules);
            if (page.Rules.Count == 0 || all.Count >= page.TotalCount)
            {
                break;
            }
            offset += page.Rules.Count;
        }

        page ??= new ResolvedRulesCatalogView(
            "campaign", campaignId, null, null, 0, [], [], []);
        return page with { Rules = all };
    }
}

/// <summary>
/// Compatibility wrapper for the established Character consumer.
/// </summary>
internal sealed class CharacterResolvedRulesReader
{
    private readonly ResolvedRulesSnapshotReader inner;

    internal CharacterResolvedRulesReader(RulesCoreDbContext dbContext)
    {
        inner = new ResolvedRulesSnapshotReader(dbContext);
    }

    internal CharacterResolvedRulesReader(IResolvedRulesCatalogService resolvedRules)
    {
        inner = new ResolvedRulesSnapshotReader(resolvedRules);
    }

    internal Task<ResolvedRulesCatalogView> ReadAllGlobalAsync(
        string? userId,
        CancellationToken cancellationToken) =>
        inner.ReadAllGlobalAsync(userId, cancellationToken);

    internal Task<ResolvedRulesCatalogView> ReadAllCampaignAsync(
        Guid campaignId,
        string userId,
        CancellationToken cancellationToken) =>
        inner.ReadAllCampaignAsync(campaignId, userId, cancellationToken);
}
