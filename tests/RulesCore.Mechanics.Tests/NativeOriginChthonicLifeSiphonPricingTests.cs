using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Approved Chthonic Tiefling life-siphon origin grant and trial allocation.
/// The catalog and price test does not simulate damage, restorative resource
/// spending, actual combat turn ordering, or Hit Point change execution.
/// </summary>
public sealed class NativeOriginChthonicLifeSiphonPricingTests
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
        NativeCatalogSnapshot catalog, string ancestry, int allowance = 18)
    {
        var proposal = new NativeOriginProposal("chthonic-siphon-" + ancestry,
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
                        ["wisdom"])])
            ]
        }, AutoManifestDirectDefaults: true,
            RequireCreationAllocation: true,
            CreationAllocationPolicy:
                NativeOriginFirstPartyBiologicalPricing.Create(allowance)));
    }

    [Theory]
    [InlineData("abyssal", 13, false)]
    [InlineData("chthonic", 17, true)]
    [InlineData("infernal", 18, false)]
    public void DirectTieflingAncestryHasApprovedPriceAndSiphonOnlyWhenChthonic(
        string ancestry, int expectedPoints, bool hasSiphon)
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
            x.Identity.MechanicalDefinitionId == "trait.otherworldly-presence");
        Assert.Single(grants, x =>
            x.Identity.MechanicalDefinitionId == "trait.damage-type-resistance");
        Assert.Equal(hasSiphon, grants.Any(x =>
            x.Identity.MechanicalDefinitionId == "trait.chthonic-life-siphon"));
        if (hasSiphon)
        {
            var siphon = Assert.Single(grants, x =>
                x.Identity.MechanicalDefinitionId == "trait.chthonic-life-siphon");
            Assert.Equal("ancestry.tiefling.chthonic",
                siphon.Identity.Occurrence.Source.Id);
            Assert.Equal(4, Assert.Single(result.CreationAllocation.Charges,
                x => x.CharacteristicKeys.Contains(
                    "trait:trait.chthonic-life-siphon")).Load);
        }
    }

    [Fact]
    public void ChthonicSiphonRequiresDeliveredNecroticDamageAndLimitsRecovery()
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("trait.chthonic-life-siphon",
            out var entity));
        var trait = entity!.Definition.GetProperty("trait");
        Assert.True(trait.GetProperty("oncePerTurn").GetBoolean());
        Assert.False(trait.TryGetProperty("abilityScoreIncrease", out _));
        Assert.False(trait.TryGetProperty("requiredCharacterLevel", out _));
        var siphon = trait.GetProperty("lifeSiphon");
        Assert.Equal("deal-necrotic-damage-to-another-creature",
            siphon.GetProperty("trigger").GetString());
        Assert.Equal("necrotic", siphon.GetProperty("damageType").GetString());
        Assert.Equal("necrotic-damage-actually-taken",
            siphon.GetProperty("damageMeasured").GetString());
        Assert.True(siphon.GetProperty("requiresPositiveDamage").GetBoolean());
        var healing = siphon.GetProperty("healing");
        Assert.Equal("self", healing.GetProperty("recipient").GetString());
        Assert.Equal("proficiency-bonus",
            healing.GetProperty("amount").GetString());
        Assert.Equal("necrotic-damage-actually-taken",
            healing.GetProperty("maximum").GetString());
        Assert.True(healing.GetProperty("obeysNormalHitPointMaximum").GetBoolean());

        var uses = trait.GetProperty("uses");
        Assert.Equal("proficiency-bonus", uses.GetProperty("value").GetString());
        Assert.Equal("long-rest", uses.GetProperty("recharge").GetString());
    }

    [Fact]
    public void ExistingChthonicSpellsAndNecroticResistanceRemainUnchanged()
    {
        var catalog = Catalog();
        var result = Direct(catalog, "chthonic");
        var grants = result.Profile!.Underlying.InherentGrants;
        foreach (var id in new[]
        {
            "trait.otherworldly-presence", "trait.chill-touch",
            "trait.false-life", "trait.ray-of-enfeeblement"
        })
            Assert.Single(grants, x => x.Identity.MechanicalDefinitionId == id);

        var resistance = Assert.Single(grants, x =>
            x.Identity.MechanicalDefinitionId == "trait.damage-type-resistance");
        Assert.Equal("necrotic",
            resistance.Parameters!["damageType"].GetString());

        var falseLife = Assert.Single(grants, x =>
            x.Identity.MechanicalDefinitionId == "trait.false-life");
        Assert.Equal(3, falseLife.EffectiveValue!.Value
            .GetProperty("level").GetInt32());
        Assert.Equal(1, falseLife.EffectiveValue.Value
            .GetProperty("freeUses").GetInt32());

        var enfeeble = Assert.Single(grants, x =>
            x.Identity.MechanicalDefinitionId == "trait.ray-of-enfeeblement");
        Assert.Equal(5, enfeeble.EffectiveValue!.Value
            .GetProperty("level").GetInt32());
        Assert.Equal(1, enfeeble.EffectiveValue.Value
            .GetProperty("freeUses").GetInt32());
    }
}
