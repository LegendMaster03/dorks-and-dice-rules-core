using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Approved Dwarf resilience increment. Verifies whole biological trait
/// pricing, directly selectable Species/Ancestry totals and unchanged vitality.
/// Does not assert that Dodge/Hit Die spending already executes in combat.
/// </summary>
public sealed class NativeOriginDwarfFortitudeAllocationTests
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
        NativeCatalogSnapshot catalog, string source, int allowance = 18)
    {
        var proposal = new NativeOriginProposal(
            "dwarf-fortitude:" + source, NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"),
            [], [], [],
            SelectedSpeciesId: source.StartsWith("species.", StringComparison.Ordinal)
                ? source : null,
            SelectedAncestryId: source.StartsWith("ancestry.", StringComparison.Ordinal)
                ? source : null);
        return new NativeOriginConstructionEngine(catalog).Evaluate(
            new(proposal, RequireCreationAllocation: true,
                CreationAllocationPolicy:
                    NativeOriginFirstPartyBiologicalPricing.Create(allowance)));
    }

    [Theory]
    [InlineData("species.dwarf", 17, 4, false)]
    [InlineData("ancestry.dwarf.hill", 18, 5, true)]
    public void SelectableDwarfAndHillDwarfUseTheApprovedAllocation(
        string source, int expectedLoad, int expectedTraitCount, bool hill)
    {
        var result = Direct(Catalog(), source);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Equal("within-allowance", result.CreationAllocation!.Status);
        Assert.Equal(expectedLoad, result.CreationAllocation.CommittedLoad);
        var traits = result.Profile!.Underlying.InherentGrants
            .Where(x => x.Identity.MechanicalDefinitionId is not null)
            .Select(x => x.Identity.MechanicalDefinitionId).ToArray();

        Assert.Equal(expectedTraitCount, traits.Length);
        Assert.Contains("trait.level-based-vitality", traits);
        Assert.Contains("trait.poison-resilience", traits);
        Assert.Contains("trait.stonebound-tremorsense", traits);
        Assert.Contains("trait.dwarven-fortitude", traits);
        Assert.Equal(hill, traits.Contains("trait.stability"));
    }

    [Theory]
    [InlineData("species.dwarf", 16, 17)]
    [InlineData("ancestry.dwarf.hill", 17, 18)]
    public void BudgetBelowApprovedLoadRejectsWholeOrigin(
        string source, int allowance, int committedLoad)
    {
        var result = Direct(Catalog(), source, allowance);
        Assert.Equal(NativeOriginConstructionStatus.Conflicting, result.Status);
        Assert.Equal("over-allowance", result.CreationAllocation!.Status);
        Assert.Equal(committedLoad, result.CreationAllocation.CommittedLoad);
        Assert.Contains(result.Issues, x => x.Code == "creation-allocation-exceeded");
    }

    [Fact]
    public void DwarvenFortitudeSpendsOneExistingHitDieOnlyWhenDodgingInCombat()
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("trait.dwarven-fortitude", out var entity));
        var trait = entity!.Definition.GetProperty("trait");
        Assert.Equal("when-taking-dodge-action-in-combat",
            trait.GetProperty("trigger").GetString());
        var healing = trait.GetProperty("hitDieHealing");
        Assert.Equal(1, healing.GetProperty("spendHitDice").GetInt32());
        Assert.True(healing.GetProperty("requiresAvailableHitDie").GetBoolean());
        Assert.True(healing.GetProperty("rollSpentHitDie").GetBoolean());
        Assert.Equal("constitution",
            healing.GetProperty("addAbilityModifier").GetString());
        Assert.Equal(1, healing.GetProperty("minimumHitPointsRegained").GetInt32());
        Assert.False(healing.GetProperty("extraHitDiceGranted").GetBoolean());
        Assert.False(trait.TryGetProperty("abilityScoreIncrease", out _));
        Assert.False(trait.TryGetProperty("uses", out _));
    }

    [Fact]
    public void VitalityStillAddsOneMaximumHitPointPerCharacterLevel()
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("trait.level-based-vitality", out var entity));
        Assert.Equal(1, entity!.Definition.GetProperty("trait")
            .GetProperty("hitPointsPerCharacterLevel").GetInt32());
        var direct = Direct(catalog, "species.dwarf");
        var projection = NativeOriginHitPointProgressionProjector.Project(
            catalog, direct, 17);
        Assert.Equal("resolved", projection.Status);
        Assert.Equal(1, projection.PerLevelBonus);
        Assert.Equal(17, projection.MaximumHitPointBonus);
        var prices = NativeOriginFirstPartyBiologicalPricing.Create().Prices;
        Assert.Equal(3, Assert.Single(prices, x =>
            x.MemberKeys.Contains("trait:trait.level-based-vitality")).Load);
        Assert.Equal(4, Assert.Single(prices, x =>
            x.MemberKeys.Contains("trait:trait.dwarven-fortitude")).Load);
    }

    [Fact]
    public void HillStabilityHasItsExistingGroundedScope()
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("trait.stability", out var entity));
        var effect = entity!.Definition.GetProperty("trait")
            .GetProperty("conditionalAdvantage");
        Assert.Equal("stable-ground", effect.GetProperty("requires").GetString());
        Assert.Equal(new[] { "ability-checks", "saving-throws" },
            effect.GetProperty("appliesTo").EnumerateArray()
                .Select(x => x.GetString()).ToArray());
        Assert.Equal(new[] { "forced-push", "being-knocked-prone" },
            effect.GetProperty("resists").EnumerateArray()
                .Select(x => x.GetString()).ToArray());
        Assert.False(effect.GetProperty("grantsImmunity").GetBoolean());
    }
}
