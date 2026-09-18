using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;

namespace RulesCore.Tests;

public sealed class RuleBrowserContractTests
{
    [Theory]
    [InlineData("monster", "monster.ancient-red-dragon", "/monsters/ancient-red-dragon")]
    [InlineData("spell", "spell.fireball", "/spells/fireball")]
    [InlineData("subclass", "subclass.champion", "/subclasses/champion")]
    [InlineData("prestigeClass", "prestigeclass.archmage", "/prestige-classes/archmage")]
    [InlineData("skill", "skill.arcana", "/skills/arcana")]
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

    [Theory]
    [InlineData("subclass", "/subclasses")]
    [InlineData("prestigeClass", "/prestige-classes")]
    [InlineData("skill", "/skills")]
    [InlineData("maneuver", "/types/maneuver")]
    public void CatalogPathsUseStableFamilyRoutes(string entityType, string expectedPath)
    {
        Assert.Equal(expectedPath, RuleBrowserRoutes.CatalogPath(entityType));
        Assert.Equal(entityType, RuleBrowserRoutes.EntityTypeForCatalogPath(expectedPath));
    }

    [Fact]
    public void DynamicCatalogRouteCanRecoverEscapedEntityType()
    {
        Assert.Equal(
            "third party rule",
            RuleBrowserRoutes.EntityTypeForCatalogPath("/types/third%20party%20rule?scope=global#catalog"));
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

    [Fact]
    public void GlobalAndCampaignAuthorityAreEvaluatedIndependentlyPerScope()
    {
        var firstDmCampaignId = Guid.NewGuid();
        var secondDmCampaignId = Guid.NewGuid();
        var playerCampaignId = Guid.NewGuid();
        var context = new ToolHostAuthenticationContext(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: RulesAuthority.DorksAndDiceMode,
            User: new ToolHostUserContext("mixed-user", "Mixed User"),
            GlobalRoles: [RulesAuthority.RulesLawyerRole],
            Campaigns:
            [
                new ToolHostCampaignContext(firstDmCampaignId, "First DM Campaign", RulesAuthority.CampaignDmRole),
                new ToolHostCampaignContext(secondDmCampaignId, "Second DM Campaign", RulesAuthority.CampaignDmRole),
                new ToolHostCampaignContext(playerCampaignId, "Player Campaign", "Player")
            ]);

        Assert.True(RulesAuthority.CanEditGlobalRules(context));
        Assert.True(RulesAuthority.CanEditCampaignRules(context, firstDmCampaignId));
        Assert.True(RulesAuthority.CanEditCampaignRules(context, secondDmCampaignId));
        Assert.False(RulesAuthority.CanEditCampaignRules(context, playerCampaignId));

        Assert.True(RulesAuthority.CanEditScope(
            context,
            new RuleAdjudicationScopeRequest(RuleAdjudicationScopeKinds.Global, null)));
        Assert.True(RulesAuthority.CanEditScope(
            context,
            new RuleAdjudicationScopeRequest(RuleAdjudicationScopeKinds.Campaign, firstDmCampaignId)));
        Assert.True(RulesAuthority.CanEditScope(
            context,
            new RuleAdjudicationScopeRequest(RuleAdjudicationScopeKinds.Campaign, secondDmCampaignId)));
        Assert.False(RulesAuthority.CanEditScope(
            context,
            new RuleAdjudicationScopeRequest(RuleAdjudicationScopeKinds.Campaign, playerCampaignId)));
    }

    [Fact]
    public void EmptyCampaignMembershipLeavesCampaignAuthorityUnavailable()
    {
        var context = new ToolHostAuthenticationContext(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: RulesAuthority.DorksAndDiceMode,
            User: new ToolHostUserContext("rules-lawyer", "Rules Lawyer"),
            GlobalRoles: [RulesAuthority.RulesLawyerRole],
            Campaigns: []);
        var unknownCampaignId = Guid.NewGuid();

        Assert.True(RulesAuthority.CanEditGlobalRules(context));
        Assert.False(RulesAuthority.CanAccessCampaignRules(context, unknownCampaignId));
        Assert.False(RulesAuthority.CanEditCampaignRules(context, unknownCampaignId));
        Assert.False(RulesAuthority.CanEditScope(
            context,
            new RuleAdjudicationScopeRequest(RuleAdjudicationScopeKinds.Campaign, unknownCampaignId)));
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
