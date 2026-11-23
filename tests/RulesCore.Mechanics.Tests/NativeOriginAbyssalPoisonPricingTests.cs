using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Approved Abyssal Tiefling Fortitude, Poison Tolerance, and Potent Venom.
/// Direct-Origin tests verify native grant/pricing and read-only HP/passive
/// interpretation; Poison resistance reduction is not yet a combat executor.
/// </summary>
public sealed class NativeOriginAbyssalPoisonPricingTests
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
        var proposal = new NativeOriginProposal("abyssal-poison-" + ancestry,
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedAncestryId: "ancestry.tiefling." + ancestry);
        var engine = new NativeOriginConstructionEngine(catalog);
        var offered = engine.Evaluate(new(proposal,
            AutoManifestDirectDefaults: true)).EvaluatedProposal.Candidates;
        var size = Assert.Single(offered, x =>
            x.Identity.IntrinsicSlotId == "size");
        var spellAbility = Assert.Single(offered, x =>
            x.Identity.IntrinsicSlotId == "spellcastingAbility");

        return engine.Evaluate(new(proposal with
        {
            Manifestations =
            [
                new("size", size.Id, Choices: [
                    new(Assert.Single(size.RequiredChoices!).Id, ["medium"])
                ]),
                new("spellcasting-ability", spellAbility.Id, Choices: [
                    new(Assert.Single(spellAbility.RequiredChoices!).Id,
                        ["charisma"])
                ])
            ]
        }, AutoManifestDirectDefaults: true,
            RequireCreationAllocation: true,
            CreationAllocationPolicy:
                NativeOriginFirstPartyBiologicalPricing.Create()));
    }

    [Theory]
    [InlineData("abyssal", 18, true)]
    [InlineData("chthonic", 17, false)]
    [InlineData("infernal", 18, false)]
    public void AllTieflingAncestriesRespectApprovedAllocationsAndDistinctGrants(
        string ancestry, int expectedPoints, bool hasAbyssalTraits)
    {
        var result = Direct(Catalog(), ancestry);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Equal("within-allowance", result.CreationAllocation!.Status);
        Assert.Equal(expectedPoints, result.CreationAllocation.CommittedLoad);

        var grants = result.Profile!.Underlying.InherentGrants;
        foreach (var id in new[]
        {
            "trait.abyssal-fortitude",
            "trait.poison-tolerance",
            "trait.potent-venom"
        })
        {
            Assert.Equal(hasAbyssalTraits, grants.Any(x =>
                x.Identity.MechanicalDefinitionId == id));
            if (hasAbyssalTraits)
            {
                var grant = Assert.Single(grants, x =>
                    x.Identity.MechanicalDefinitionId == id);
                Assert.Equal("ancestry.tiefling.abyssal",
                    grant.Identity.Occurrence.Source.Id);
            }
        }

        if (hasAbyssalTraits)
        {
            var loads = new Dictionary<string, int>
            {
                ["trait.abyssal-fortitude"] = 2,
                ["trait.poison-tolerance"] = 1,
                ["trait.potent-venom"] = 2
            };
            foreach (var (id, load) in loads)
                Assert.Equal(load, Assert.Single(result.CreationAllocation.Charges,
                    x => x.CharacteristicKeys.Contains("trait:" + id)).Load);
        }
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(5, 2)]
    [InlineData(9, 4)]
    [InlineData(20, 10)]
    public void AbyssalFortitudeProjectsOneTotalHalfLevelHpBonus(
        int level, int expectedBonus)
    {
        var catalog = Catalog();
        var result = Direct(catalog, "abyssal");
        var projected = NativeOriginHitPointProgressionProjector.Project(
            catalog, result, level);
        Assert.Equal("resolved", projected.Status);
        Assert.Equal(expectedBonus, projected.MaximumHitPointBonus);
        Assert.Null(projected.PerLevelBonus);
        Assert.Null(projected.CombinedMaximumHitPoints);
        Assert.Equal("trait.abyssal-fortitude",
            Assert.Single(result.Profile!.Underlying.InherentGrants,
                x => x.Id == projected.GrantId)
                .Identity.MechanicalDefinitionId);
    }

    [Fact]
    public void PoisonToleranceOnlyAddsAdvantageAgainstPoisonedCondition()
    {
        var catalog = Catalog();
        var result = Direct(catalog, "abyssal");
        var view = NativeOriginPassiveEntitlementProjector.Project(catalog, result);
        Assert.Equal("resolved", view.Status);
        var tolerance = Assert.Single(view.Traits, x =>
            x.DefinitionId == "trait.poison-tolerance");
        Assert.Equal(new[] { "condition.poisoned" },
            tolerance.SavingThrows!.Against);
        Assert.Empty(tolerance.SavingThrows.Abilities);
        Assert.Empty(tolerance.SkillBonuses);
        Assert.Null(tolerance.SavingThrows.Scope);
    }

    [Fact]
    public void PotentVenomUsesPercentagePointsAndPreservesImmunity()
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("trait.potent-venom", out var entity));
        var trait = entity!.Definition.GetProperty("trait");
        var reduction = trait.GetProperty("damageResistanceReduction");
        Assert.Equal("poison", reduction.GetProperty("damageType").GetString());
        Assert.Equal("damage-you-deal", reduction.GetProperty("scope").GetString());
        Assert.Equal("per-damage-instance",
            reduction.GetProperty("timing").GetString());
        Assert.Equal(25,
            reduction.GetProperty("reductionPercentagePoints").GetInt32());
        Assert.Equal(0,
            reduction.GetProperty("minimumResistancePercent").GetInt32());
        Assert.Equal(100,
            reduction.GetProperty("preserveImmunityAtOrAbovePercent").GetInt32());
        Assert.False(reduction.GetProperty("stacksWithSelf").GetBoolean());
        Assert.False(reduction.GetProperty("createsVulnerability").GetBoolean());

        // The permitted transformation applies only to nonimmune
        // resistance. This is not a runtime damage resolution test.
        foreach (var (original, effective) in new[]
        {
            (0, 0), (25, 0), (50, 25), (75, 50), (100, 100)
        })
        {
            var value = original >= 100 ? original : Math.Max(0, original - 25);
            Assert.Equal(effective, value);
        }
    }

    [Fact]
    public void ExistingAbyssalPoisonResistanceAndSpellsRemain()
    {
        var result = Direct(Catalog(), "abyssal");
        var grants = result.Profile!.Underlying.InherentGrants;
        foreach (var id in new[]
        {
            "trait.low-light-vision", "trait.otherworldly-presence",
            "trait.damage-type-resistance", "trait.poison-spray",
            "trait.ray-of-sickness", "trait.hold-person"
        })
            Assert.Single(grants, x => x.Identity.MechanicalDefinitionId == id);
        var resistance = Assert.Single(grants, x =>
            x.Identity.MechanicalDefinitionId == "trait.damage-type-resistance");
        Assert.Equal("poison", resistance.Parameters!["damageType"].GetString());
        var ray = Assert.Single(grants, x =>
            x.Identity.MechanicalDefinitionId == "trait.ray-of-sickness");
        Assert.Equal(3, ray.EffectiveValue!.Value
            .GetProperty("level").GetInt32());
        var hold = Assert.Single(grants, x =>
            x.Identity.MechanicalDefinitionId == "trait.hold-person");
        Assert.Equal(5, hold.EffectiveValue!.Value
            .GetProperty("level").GetInt32());
    }
}
