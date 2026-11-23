using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Approved Goliath biological trial pricing. The six native Giant Ancestries
/// remain alternative choices, and changes to gene load do not alter their
/// canonical effects, resource pools, or character-level unlocks.
/// </summary>
public sealed class NativeOriginGoliathPricingReconciliationTests
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

    private static NativeOriginConstructionResult Direct(
        NativeCatalogSnapshot catalog, string ancestry)
    {
        var engine = new NativeOriginConstructionEngine(catalog);
        var proposal = new NativeOriginProposal("goliath-price-" + ancestry,
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"),
            [], [], [], SelectedAncestryId: "ancestry.goliath." + ancestry);
        var discovered = engine.Evaluate(new(proposal)).EvaluatedProposal;
        var aptitude = Assert.Single(discovered.Candidates, x =>
            x.Identity.MechanicalDefinitionId == "trait.ancestral-aptitude");
        Assert.Equal("species.goliath", aptitude.Identity.Occurrence.Source.Id);
        Assert.Equal(NativeOriginEligibility.Eligible, aptitude.SelectionBasis.Eligibility);
        return engine.Evaluate(new(proposal with
        {
            Manifestations =
            [
                new("selected-giant-aptitude", aptitude.Id,
                    Choices: [new("abilityChoice", ["strength"])])
            ]
        }, RequireCreationAllocation: true,
            CreationAllocationPolicy:
                NativeOriginFirstPartyBiologicalPricing.Create()));
    }

    [Theory]
    [InlineData("cloud", 17, "trait.cloudy-escape", 4)]
    [InlineData("fire", 18, "trait.searing-ignition", 4)]
    [InlineData("frost", 18, "trait.frigid-retaliation", 4)]
    [InlineData("hill", 17, "trait.bulwark", 3)]
    [InlineData("stone", 18, "trait.stone-throw", 3)]
    [InlineData("storm", 17, "trait.maelstrom-aura", 5)]
    public void EveryApprovedGoliathAncestryHasItsOwnBalancedAllocation(
        string ancestry, int expectedPoints, string advanced, int advancedLoad)
    {
        var result = Direct(Catalog(), ancestry);
        Assert.True(result.Status == NativeOriginConstructionStatus.Authorized,
            string.Join(" | ", result.Issues.Select(x => x.Code + ": " + x.Detail)));
        Assert.Equal("within-allowance", result.CreationAllocation!.Status);
        Assert.Equal(expectedPoints, result.CreationAllocation.CommittedLoad);
        Assert.Equal("ancestry.goliath." + ancestry,
            result.EvaluatedProposal.SelectedAncestryId);
        var grants = result.Profile!.Underlying.InherentGrants;
        Assert.Single(grants, x =>
            x.Identity.MechanicalDefinitionId == "trait.large-form");
        Assert.Single(grants, x =>
            x.Identity.MechanicalDefinitionId == "trait.ancestral-aptitude");
        Assert.Single(grants, x =>
            x.Identity.MechanicalDefinitionId == advanced);

        var charges = result.CreationAllocation.Charges;
        Assert.Equal(4, Assert.Single(charges, x =>
            x.CharacteristicKeys.Contains("trait:trait.large-form")).Load);
        Assert.Equal(advancedLoad, Assert.Single(charges, x =>
            x.CharacteristicKeys.Contains("trait:" + advanced)).Load);
    }

    [Fact]
    public void PriceChangesDoNotAlterLevelGatesOrUsageRules()
    {
        var catalog = Catalog();
        foreach (var traitId in new[]
        {
            "trait.searing-ignition", "trait.frigid-retaliation",
            "trait.bulwark", "trait.stone-throw", "trait.maelstrom-aura"
        })
        {
            Assert.True(catalog.TryGetById(traitId, out var advanced));
            Assert.Equal(4, advanced!.Definition.GetProperty("trait")
                .GetProperty("requiredCharacterLevel").GetInt32());
        }
        Assert.True(catalog.TryGetById("trait.large-form", out var largeForm));
        var form = largeForm!.Definition.GetProperty("trait");
        Assert.Equal(5, form.GetProperty("requiredCharacterLevel").GetInt32());
        Assert.Equal(1, form.GetProperty("uses").GetProperty("value").GetInt32());
        Assert.Equal("long-rest",
            form.GetProperty("uses").GetProperty("recharge").GetString());
        Assert.Equal(10,
            form.GetProperty("duration").GetProperty("minutes").GetInt32());
        Assert.Equal(10, form.GetProperty("walkingSpeedBonusFeet").GetInt32());
    }
}
