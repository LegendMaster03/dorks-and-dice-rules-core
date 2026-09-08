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

    public static bool CanEditCampaignRules(
        ToolHostAuthenticationContext context,
        Guid campaignId) =>
        context.HasCampaignRole(campaignId, CampaignDmRole);
}
