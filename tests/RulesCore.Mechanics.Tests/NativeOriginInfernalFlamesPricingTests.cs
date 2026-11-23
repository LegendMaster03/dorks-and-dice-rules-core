using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Approved Infernal Tiefling Flames of Phlegethos biological increment.
/// This tests catalog shape and complete origin allocation, not execution
/// of spell damage dice, attack retaliation or dynamic light in gameplay.
/// </summary>
public sealed class NativeOriginInfernalFlamesPricingTests
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
        var proposal = new NativeOriginProposal("infernal-flames-" + ancestry,
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedAncestryId: "ancestry.tiefling." + ancestry);
        var engine = new NativeOriginConstructionEngine(catalog);
        var candidates = engine.Evaluate(new(proposal,
            AutoManifestDirectDefaults: true)).EvaluatedProposal.Candidates;
        var size = Assert.Single(candidates, x =>
            x.Identity.IntrinsicSlotId == "size");
        var spellAbility = Assert.Single(candidates, x =>
            x.Identity.IntrinsicSlotId == "spellcastingAbility");

        return engine.Evaluate(new(proposal with
        {
            Manifestations =
            [
                new("selected-size", size.Id,
                    Choices: [new(Assert.Single(size.RequiredChoices!).Id, ["medium"])]),
                new("selected-spellcasting-ability", spellAbility.Id,
                    Choices: [new(Assert.Single(spellAbility.RequiredChoices!).Id,
                        ["charisma"])])
            ]
        }, AutoManifestDirectDefaults: true,
            RequireCreationAllocation: true,
            CreationAllocationPolicy:
                NativeOriginFirstPartyBiologicalPricing.Create()));
    }

    [Theory]
    [InlineData("abyssal", 13, false)]
    [InlineData("chthonic", 17, false)]
    [InlineData("infernal", 18, true)]
    public void EachTieflingAncestryHasCurrentPriceAndOnlyInfernalGainsFlames(
        string ancestry, int expectedPoints, bool hasFlames)
    {
        var result = Direct(Catalog(), ancestry);
        Assert.True(result.Status == NativeOriginConstructionStatus.Authorized,
            string.Join(" | ", result.Issues.Select(x => x.Code + ": " + x.Detail)));
        Assert.Equal("within-allowance", result.CreationAllocation!.Status);
        Assert.Equal(expectedPoints, result.CreationAllocation.CommittedLoad);
        Assert.Equal("ancestry.tiefling." + ancestry,
            result.EvaluatedProposal.SelectedAncestryId);

        var grants = result.Profile!.Underlying.InherentGrants;
        Assert.Single(grants, x =>
            x.Identity.MechanicalDefinitionId == "trait.low-light-vision");
        Assert.Single(grants, x =>
            x.Identity.MechanicalDefinitionId == "trait.otherworldly-presence");
        Assert.Single(grants, x =>
            x.Identity.MechanicalDefinitionId == "trait.damage-type-resistance");
        Assert.Equal(hasFlames, grants.Any(x =>
            x.Identity.MechanicalDefinitionId == "trait.flames-of-phlegethos"));

        if (hasFlames)
        {
            var grant = Assert.Single(grants, x =>
                x.Identity.MechanicalDefinitionId == "trait.flames-of-phlegethos");
            Assert.Equal("ancestry.tiefling.infernal",
                grant.Identity.Occurrence.Source.Id);
            Assert.Equal(4, Assert.Single(result.CreationAllocation.Charges,
                x => x.CharacteristicKeys.Contains(
                    "trait:trait.flames-of-phlegethos")).Load);
        }
    }

    [Fact]
    public void FlamesHaveBothOriginalMechanicalBenefitsWithoutAbilityIncrease()
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("trait.flames-of-phlegethos",
            out var entity));
        var effect = entity!.Definition.GetProperty("trait");
        Assert.False(effect.TryGetProperty("abilityScoreIncrease", out _));
        Assert.False(effect.TryGetProperty("uses", out _));
        Assert.False(effect.TryGetProperty("requiredCharacterLevel", out _));

        var damageReroll = effect.GetProperty("fireSpellDamageReroll");
        Assert.Equal("spell-you-cast",
            damageReroll.GetProperty("source").GetString());
        Assert.Equal("fire",
            damageReroll.GetProperty("damageType").GetString());
        Assert.Equal(1,
            damageReroll.GetProperty("triggerDieResult").GetInt32());
        Assert.Equal(1,
            damageReroll.GetProperty("rerollsPerDie").GetInt32());
        Assert.True(damageReroll.GetProperty("useNewResult").GetBoolean());

        var flames = effect.GetProperty("protectiveFlames");
        Assert.Equal("cast-spell-that-deals-fire-damage",
            flames.GetProperty("trigger").GetString());
        Assert.True(flames.GetProperty("optional").GetBoolean());
        Assert.Equal("end-of-your-next-turn",
            flames.GetProperty("endsAt").GetString());
        Assert.Equal(30, flames.GetProperty("brightLightFeet").GetInt32());
        Assert.Equal(30, flames.GetProperty("dimLightAdditionalFeet").GetInt32());
        Assert.False(flames.GetProperty("harmsSelfOrPossessions").GetBoolean());

        var retaliation = flames.GetProperty("retaliation");
        Assert.Equal("creature-within-5-feet-hits-you-with-melee-attack-while-flames-active",
            retaliation.GetProperty("trigger").GetString());
        Assert.Equal(5, retaliation.GetProperty("rangeFeet").GetInt32());
        Assert.Equal("1d4", retaliation.GetProperty("damageDice").GetString());
        Assert.Equal("fire", retaliation.GetProperty("damageType").GetString());
        Assert.Equal("none", retaliation.GetProperty("actionCost").GetString());
    }

    [Fact]
    public void ExistingInfernalResistanceAndSpellCourseStayIntact()
    {
        var catalog = Catalog();
        var result = Direct(catalog, "infernal");
        var grants = result.Profile!.Underlying.InherentGrants;
        var spellIds = new[]
        {
            "trait.otherworldly-presence", "trait.fire-bolt",
            "trait.hellish-rebuke", "trait.darkness"
        };
        foreach (var id in spellIds)
            Assert.Single(grants, x => x.Identity.MechanicalDefinitionId == id);

        var hellish = Assert.Single(grants, x =>
            x.Identity.MechanicalDefinitionId == "trait.hellish-rebuke");
        Assert.Equal(3, hellish.EffectiveValue!.Value
            .GetProperty("level").GetInt32());
        Assert.Equal(2, hellish.EffectiveValue.Value
            .GetProperty("freeCastSpellLevel").GetInt32());
        Assert.Equal(1, hellish.EffectiveValue.Value
            .GetProperty("freeUses").GetInt32());
        Assert.Equal("long-rest", hellish.EffectiveValue.Value
            .GetProperty("recharge").GetString());
    }
}
