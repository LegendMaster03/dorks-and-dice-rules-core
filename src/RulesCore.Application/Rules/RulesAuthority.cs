using RulesCore.Application.Hosting;

namespace RulesCore.Application.Rules;

public static class RulesAuthority
{
    public const string DorksAndDiceMode = "dorks-and-dice";
    public const string RulesLawyerRole = "Rules Lawyer";
    public const string CampaignDmRole = "DM";

    public static bool CanEditGlobalRules(ToolHostAuthenticationContext context) =>
        string.Equals(context.SiteMode, DorksAndDiceMode, StringComparison.Ordinal)
        && context.HasGlobalRole(RulesLawyerRole);

    public static bool CanAccessCampaignRules(
        ToolHostAuthenticationContext context,
        Guid campaignId) =>
        string.Equals(context.SiteMode, DorksAndDiceMode, StringComparison.Ordinal)
        && context.Campaigns.Any(campaign => campaign.Id == campaignId);

    public static bool CanEditCampaignRules(
        ToolHostAuthenticationContext context,
        Guid campaignId) =>
        CanAccessCampaignRules(context, campaignId)
        && context.HasCampaignRole(campaignId, CampaignDmRole);

    public static bool CanEditScope(
        ToolHostAuthenticationContext context,
        RuleAdjudicationScopeRequest scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.Kind switch
        {
            RuleAdjudicationScopeKinds.Global when scope.CampaignId is null => CanEditGlobalRules(context),
            RuleAdjudicationScopeKinds.Campaign when scope.CampaignId is Guid campaignId
                => CanEditCampaignRules(context, campaignId),
            _ => false
        };
    }
}
