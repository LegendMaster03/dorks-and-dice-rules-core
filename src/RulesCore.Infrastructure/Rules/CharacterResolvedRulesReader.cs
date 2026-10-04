using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Reads complete global or campaign resolved-rules snapshots for internal consumers without
/// walking the paged browser catalog.
/// </summary>
internal sealed class ResolvedRulesSnapshotReader(RulesCoreDbContext dbContext)
{
    private readonly ResolvedRulesSnapshotService snapshots = new(dbContext);

    internal Task<ResolvedRulesCatalogView> ReadAllGlobalAsync(
        string? userId,
        CancellationToken cancellationToken) =>
        snapshots.ReadGlobalAsync(userId, cancellationToken);

    internal Task<ResolvedRulesCatalogView> ReadAllCampaignAsync(
        Guid campaignId,
        string userId,
        CancellationToken cancellationToken) =>
        snapshots.ReadCampaignAsync(campaignId, userId, cancellationToken);
}

/// <summary>
/// Compatibility wrapper for the established Character consumer.
/// </summary>
internal sealed class CharacterResolvedRulesReader(RulesCoreDbContext dbContext)
{
    private readonly ResolvedRulesSnapshotReader inner = new(dbContext);

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
