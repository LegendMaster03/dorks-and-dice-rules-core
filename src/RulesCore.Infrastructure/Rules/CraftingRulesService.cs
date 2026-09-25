using RulesCore.Application.Rules;

namespace RulesCore.Infrastructure.Rules;

public sealed class CraftingRulesService(
    ICharacterRulesProjectionService characterProjection)
    : ICraftingRulesService
{
    public async Task<CraftingCheckResolutionView> ResolveGlobalManufacturingAsync(
        ManufacturingResolutionRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var projection = await characterProjection.ResolveGlobalAsync(
            request.Character,
            userId,
            cancellationToken);
        return CraftingOutcomeEvaluator.ResolveManufacturing(projection, request);
    }

    public async Task<CraftingCheckResolutionView> ResolveCampaignManufacturingAsync(
        Guid campaignId,
        ManufacturingResolutionRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ValidateCampaign(campaignId, userId);
        ArgumentNullException.ThrowIfNull(request);
        var projection = await characterProjection.ResolveCampaignAsync(
            campaignId,
            request.Character,
            userId.Trim(),
            cancellationToken);
        return CraftingOutcomeEvaluator.ResolveManufacturing(projection, request);
    }

    public async Task<CraftingCheckResolutionView> ResolveGlobalEnchantingAsync(
        EnchantingResolutionRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var projection = await characterProjection.ResolveGlobalAsync(
            request.Character,
            userId,
            cancellationToken);
        return CraftingOutcomeEvaluator.ResolveEnchanting(projection, request);
    }

    public async Task<CraftingCheckResolutionView> ResolveCampaignEnchantingAsync(
        Guid campaignId,
        EnchantingResolutionRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ValidateCampaign(campaignId, userId);
        ArgumentNullException.ThrowIfNull(request);
        var projection = await characterProjection.ResolveCampaignAsync(
            campaignId,
            request.Character,
            userId.Trim(),
            cancellationToken);
        return CraftingOutcomeEvaluator.ResolveEnchanting(projection, request);
    }

    private static void ValidateCampaign(Guid campaignId, string userId)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("Campaign ID can not be empty.", nameof(campaignId));
        }
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(userId));
        }
    }
}
