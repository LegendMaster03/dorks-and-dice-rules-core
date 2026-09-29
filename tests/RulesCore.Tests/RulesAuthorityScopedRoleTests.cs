using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;

namespace RulesCore.Tests;

public sealed class RulesAuthorityScopedRoleTests
{
    [Fact]
    public void CurrentSitePayloadRequiresScopedRulesLawyerAuthority()
    {
        var context = Context(
            RulesAuthority.DorksAndDiceMode,
            globalRoles: [RulesAuthority.RulesLawyerRole],
            scopedRoles: []);

        Assert.False(RulesAuthority.CanEditGlobalRules(context));
    }

    [Fact]
    public void CurrentSitePayloadAcceptsDorksAndDiceScopedRulesLawyerAuthority()
    {
        var context = Context(
            RulesAuthority.DorksAndDiceMode,
            globalRoles: [],
            scopedRoles: [RulesAuthority.RulesLawyerRole]);

        Assert.True(RulesAuthority.CanEditGlobalRules(context));
    }

    [Fact]
    public void ScopedRulesLawyerDoesNotApplyOutsideDorksAndDiceMode()
    {
        var context = Context(
            "professional",
            globalRoles: [],
            scopedRoles: [RulesAuthority.RulesLawyerRole]);

        Assert.False(RulesAuthority.CanEditGlobalRules(context));
    }

    [Fact]
    public void OlderSitePayloadRetainsGlobalRulesLawyerCompatibility()
    {
        var context = Context(
            RulesAuthority.DorksAndDiceMode,
            globalRoles: [RulesAuthority.RulesLawyerRole],
            scopedRoles: null);

        Assert.True(RulesAuthority.CanEditGlobalRules(context));
    }

    private static ToolHostAuthenticationContext Context(
        string siteMode,
        IReadOnlyList<string> globalRoles,
        IReadOnlyList<string>? scopedRoles) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: siteMode,
            User: new ToolHostUserContext("rules-authority-test", "Rules Authority Test"),
            GlobalRoles: globalRoles,
            Campaigns: [])
        {
            ScopedRoles = scopedRoles
        };
}
