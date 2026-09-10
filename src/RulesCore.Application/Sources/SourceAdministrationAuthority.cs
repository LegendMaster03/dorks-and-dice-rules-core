using RulesCore.Application.Hosting;

namespace RulesCore.Application.Sources;

public static class SourceAdministrationAuthority
{
    public const string DorksAndDiceMode = "dorks-and-dice";
    public const string DeveloperRole = "Dev";

    public static bool CanAdministerSources(ToolHostAuthenticationContext context) =>
        string.Equals(context.SiteMode, DorksAndDiceMode, StringComparison.Ordinal)
        && context.HasGlobalRole(DeveloperRole);
}
