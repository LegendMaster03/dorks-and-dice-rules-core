using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Approval-scoped verification for the independently selectable Orc foundation.
/// This tests native biological grants and trial allocation, not runtime attack
/// execution or an unapproved general Ancestry substitution mechanism.
/// </summary>
public sealed class NativeOriginOrcFoundationTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var root = Path.Combine(directory.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(root, "first-party-001", "native",
                "rules", "creature-origination.json")))
                return NativeCatalogLoader.Load(
                    NativeRulesetLayout.Resolve(root, "first-party-001"));
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Native ruleset missing.");
    }

    private static NativeOriginConstructionResult Direct(int allowance)
    {
        var proposal = new NativeOriginProposal("orc-approved-foundation",
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"),
            [], [], [], SelectedSpeciesId: "species.orc");
        return new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, RequireCreationAllocation: true,
                CreationAllocationPolicy:
                    NativeOriginFirstPartyBiologicalPricing.Create(allowance)));
    }

    [Fact]
    public void BaseOrcAloneIsAuthorizedAtSeventeenPointsWithoutAnAncestry()
    {
        var result = Direct(18);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Equal("within-allowance", result.CreationAllocation!.Status);
        Assert.Equal(17, result.CreationAllocation.CommittedLoad);
        Assert.Null(result.EvaluatedProposal.SelectedAncestryId);
        var traits = result.Profile!.Underlying.InherentGrants
            .Where(x => x.Identity.MechanicalDefinitionId is not null)
            .Select(x => x.Identity.MechanicalDefinitionId).ToArray();
        Assert.Equal(7, traits.Length);
        Assert.Contains("trait.orcish-fury", traits);
        Assert.Contains("trait.adrenaline-rush", traits);
        Assert.Contains("trait.relentless-endurance", traits);
        Assert.Contains("trait.low-light-vision", traits);
        Assert.Contains("trait.powerful-build", traits);
        Assert.Contains("trait.savage-attacks", traits);
        Assert.Contains("trait.menacing-presence", traits);
    }

    [Fact]
    public void AllowanceBelowSeventeenRejectsTheCompleteOrcFoundation()
    {
        var result = Direct(16);
        Assert.Equal(NativeOriginConstructionStatus.Conflicting, result.Status);
        Assert.Equal("over-allowance", result.CreationAllocation!.Status);
        Assert.Equal(17, result.CreationAllocation.CommittedLoad);
        Assert.Contains(result.Issues, x => x.Code == "creation-allocation-exceeded");
    }

    [Fact]
    public void OrcishFuryIsOneGeneWithIndependentWeaponBoostAndEnduranceReaction()
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("trait.orcish-fury", out var entity));
        var trait = entity!.Definition.GetProperty("trait");
        var onHit = trait.GetProperty("onWeaponHit");
        Assert.Equal("when-you-hit-with-simple-or-martial-weapon",
            onHit.GetProperty("trigger").GetString());
        Assert.Equal(new[] { "simple", "martial" },
            onHit.GetProperty("eligibleWeaponCategories")
                .EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal(1, onHit.GetProperty("bonusDamageDice")
            .GetProperty("count").GetInt32());
        Assert.Equal("weapon-damage-die", onHit.GetProperty("bonusDamageDice")
            .GetProperty("dieFrom").GetString());
        Assert.Equal(1, onHit.GetProperty("uses").GetProperty("value").GetInt32());
        Assert.Equal("short-or-long-rest",
            onHit.GetProperty("uses").GetProperty("recharge").GetString());

        var reaction = trait.GetProperty("afterTraitUse");
        Assert.Equal("trait.relentless-endurance",
            reaction.GetProperty("triggerTraitId").GetString());
        Assert.Equal("immediately-after-use",
            reaction.GetProperty("triggerTiming").GetString());
        Assert.Equal("reaction",
            reaction.GetProperty("activation").GetProperty("action").GetString());
        Assert.Equal("one-weapon-attack",
            reaction.GetProperty("activation").GetProperty("grantsAction").GetString());
        Assert.True(reaction.GetProperty("requiresManifestedTrait").GetBoolean());
        Assert.False(reaction.GetProperty("extraUsesGranted").GetBoolean());
        Assert.False(trait.TryGetProperty("abilityScoreIncrease", out _));
    }

    [Fact]
    public void ExistingAdrenalineRushMechanicIsUnchangedDespitePriceIncrease()
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("trait.adrenaline-rush", out var entity));
        var rush = entity!.Definition.GetProperty("trait");
        Assert.Equal("bonus-action", rush.GetProperty("activation")
            .GetProperty("action").GetString());
        Assert.Equal("dash", rush.GetProperty("activation")
            .GetProperty("grantsAction").GetString());
        Assert.Equal("proficiency-bonus", rush.GetProperty("temporaryHitPoints")
            .GetProperty("value").GetString());
        Assert.Equal("proficiency-bonus", rush.GetProperty("uses")
            .GetProperty("value").GetString());
        Assert.Equal("short-or-long-rest", rush.GetProperty("uses")
            .GetProperty("recharge").GetString());

        var prices = NativeOriginFirstPartyBiologicalPricing.Create().Prices;
        Assert.Equal(4, Assert.Single(prices, x =>
            x.MemberKeys.Contains("trait:trait.adrenaline-rush")).Load);
        Assert.Equal(4, Assert.Single(prices, x =>
            x.MemberKeys.Contains("trait:trait.orcish-fury")).Load);
    }
}
