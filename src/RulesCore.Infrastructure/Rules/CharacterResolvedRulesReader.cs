using RulesCore.Application.Rules;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Reads complete global or campaign resolved-rules catalogs across the paged public
/// catalog service. Consumer domains should reuse this reader rather than duplicate
/// pagination or scope reconstruction.
/// </summary>
internal sealed class ResolvedRulesSnapshotReader(
    IResolvedRulesCatalogService resolvedRules)
{
    private const int PageSize = 500;

    internal async Task<ResolvedRulesCatalogView> ReadAllGlobalAsync(
        string? userId,
        CancellationToken cancellationToken)
    {
        var all = new List<ResolvedRuleCatalogItemView>();
        ResolvedRulesCatalogView? page = null;
        var offset = 0;
        while (true)
        {
            page = await resolvedRules.GetGlobalPageAsync(
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
            "global",
            null,
            null,
            null,
            0,
            [],
            [],
            []);
        return page with { Rules = all };
    }

    internal async Task<ResolvedRulesCatalogView> ReadAllCampaignAsync(
        Guid campaignId,
        string userId,
        CancellationToken cancellationToken)
    {
        var all = new List<ResolvedRuleCatalogItemView>();
        ResolvedRulesCatalogView? page = null;
        var offset = 0;
        while (true)
        {
            page = await resolvedRules.GetCampaignPageAsync(
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
            "campaign",
            campaignId,
            null,
            null,
            0,
            [],
            [],
            []);
        return page with { Rules = all };
    }
}

/// <summary>
/// Compatibility wrapper for the established Character consumer. New consumer domains
/// should use <see cref="ResolvedRulesSnapshotReader"/> directly.
/// </summary>
internal sealed class CharacterResolvedRulesReader(
    IResolvedRulesCatalogService resolvedRules)
{
    private readonly ResolvedRulesSnapshotReader inner = new(resolvedRules);

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
