using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;

namespace RulesCore.Tests;

public sealed class RuleBrowserContractTests
{
    [Theory]
    [InlineData("monster", "monster.ancient-red-dragon", "/monsters/ancient-red-dragon")]
    [InlineData("spell", "spell.fireball", "/spells/fireball")]
    [InlineData("feat", "feat.alert", "/feats/alert")]
    public void BrowserLinksUseStableRuleIdentity(string entityType, string conceptKey, string expectedPath)
    {
        var link = RuleBrowserRoutes.ForConcept(entityType, conceptKey);

        Assert.Equal("rules-core", link.ToolSlug);
        Assert.Equal(expectedPath, link.ToolRelativePath);
        Assert.Equal(conceptKey, link.RouteIdentity);
    }

    [Fact]
    public void BrowserRouteCanRecoverConceptIdentity()
    {
        var resolved = RuleBrowserRoutes.TryResolveConceptKey(
            "/monsters/ancient-red-dragon",
            out var conceptKey,
            out var entityType);

        Assert.True(resolved);
        Assert.Equal("monster.ancient-red-dragon", conceptKey);
        Assert.Equal("monster", entityType);
    }

    [Fact]
    public void CampaignDmOwnsCampaignAdjudicationAuthority()
    {
        var campaignId = Guid.NewGuid();
        var context = Context(campaignId, RulesAuthority.CampaignDmRole);

        Assert.True(RulesAuthority.CanEditCampaignRules(context, campaignId));
        Assert.True(RulesAuthority.CanEditScope(
            context,
            new RuleAdjudicationScopeRequest(RuleAdjudicationScopeKinds.Campaign, campaignId)));
    }

    [Fact]
    public void CampaignPlayerCanNotAdjudicateCampaignRules()
    {
        var campaignId = Guid.NewGuid();
        var context = Context(campaignId, "Player");

        Assert.False(RulesAuthority.CanEditCampaignRules(context, campaignId));
        Assert.False(RulesAuthority.CanEditScope(
            context,
            new RuleAdjudicationScopeRequest(RuleAdjudicationScopeKinds.Campaign, campaignId)));
    }

    private static ToolHostAuthenticationContext Context(
        Guid campaignId,
        string role) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: RulesAuthority.DorksAndDiceMode,
            User: new ToolHostUserContext("user", "User"),
            GlobalRoles: [],
            Campaigns:
            [
                new ToolHostCampaignContext(
                    campaignId,
                    "Campaign",
                    role)
            ]);
}
