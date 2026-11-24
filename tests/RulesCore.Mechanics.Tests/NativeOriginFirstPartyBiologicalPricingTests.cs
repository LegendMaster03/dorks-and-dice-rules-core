using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// First-party numeric policy tests. These are creation-ledger checks, not
/// proof that every native trait already has executable gameplay support.
/// </summary>
public sealed class NativeOriginFirstPartyBiologicalPricingTests
{
    private static (NativeCatalogSnapshot Catalog, string RulesRoot) Native()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var root = Path.Combine(directory.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(root, "first-party-001", "native",
                "rules", "creature-origination.json")))
                return (NativeCatalogLoader.Load(
                    NativeRulesetLayout.Resolve(root, "first-party-001")), root);
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Native ruleset missing.");
    }

    private static NativeOriginProposal DirectElf() =>
        new("direct-elf-first-party", NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedSpeciesId: "species.elf");

    private static NativeOriginConstructionResult EvaluateDirect(
        NativeCatalogSnapshot catalog, int allowance) =>
        new NativeOriginConstructionEngine(catalog).Evaluate(
            new(DirectElf(), RequireCreationAllocation: true,
                CreationAllocationPolicy:
                    NativeOriginFirstPartyBiologicalPricing.Create(allowance)));

    [Fact]
    public void EveryActiveCanonicalBiologicalGeneHasExactlyOneTrialPrice()
    {
        var (_, root) = Native();
        var folder = Path.Combine(root, "first-party-001", "native",
            "origination", "biological", "genes");
        var genes = Directory.EnumerateFiles(folder, "*.json")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var prices = NativeOriginFirstPartyBiologicalPricing.Create().Prices;
        var covered = prices.SelectMany(x => x.MemberKeys)
            .Where(x => x.StartsWith("trait:", StringComparison.Ordinal))
            .Select(x => x["trait:".Length..])
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(97, genes.Length);
        Assert.Equal(genes, covered);
        Assert.Equal(102, prices.Count); // 97 genes + 5 intrinsic slots.
        Assert.Equal("biological-pricing-20261124-v14",
            NativeOriginFirstPartyBiologicalPricing.Revision);
        Assert.Equal(18, NativeOriginFirstPartyBiologicalPricing.TrialAllowance);
    }

    [Fact]
    public void FirstPartyFixedCostsAndSpellProgressionFollowTheApprovedSchedule()
    {
        var policy = NativeOriginFirstPartyBiologicalPricing.Create();
        var byMember = policy.Prices.SelectMany(x =>
            x.MemberKeys.Select(k => (Key: k, Entry: x)))
            .ToDictionary(x => x.Key, x => x.Entry, StringComparer.Ordinal);
        foreach (var key in new[]
        {
            "intrinsic:creatureType", "intrinsic:size",
            "intrinsic:walkingSpeedFeet", "trait:trait.sunlight-sensitivity"
        })
            Assert.Equal(0, byMember[key].Load);

        Assert.Equal(1, byMember["intrinsic:spellcastingAbility"].Load);
        Assert.Equal(1, byMember["trait:trait.skillful"].Load);
        Assert.Equal(6, byMember["trait:trait.adaptable-learning"].Load);
        Assert.Equal(6, byMember["trait:trait.versatile"].Load);
        Assert.Equal(4, byMember["trait:trait.human-determination"].Load);
        Assert.Equal(4, byMember["trait:trait.flames-of-phlegethos"].Load);
        Assert.Equal(4, byMember["trait:trait.chthonic-life-siphon"].Load);
        Assert.Equal(2, byMember["trait:trait.abyssal-fortitude"].Load);
        Assert.Equal(1, byMember["trait:trait.poison-tolerance"].Load);
        Assert.Equal(2, byMember["trait:trait.potent-venom"].Load);
        Assert.Equal(3, byMember["trait:trait.breath-weapon"].Load);
        Assert.Equal(1, byMember["trait:trait.draconic-natural-weapons"].Load);
        Assert.Equal(5, byMember["trait:trait.draconic-wings"].Load);
        Assert.Equal(5, byMember["trait:trait.hills-tumble"].Load);
        Assert.Equal(4, byMember["trait:trait.large-form"].Load);
        Assert.Equal(4, byMember["trait:trait.searing-ignition"].Load);
        Assert.Equal(4, byMember["trait:trait.frigid-retaliation"].Load);
        Assert.Equal(3, byMember["trait:trait.bulwark"].Load);
        Assert.Equal(3, byMember["trait:trait.stone-throw"].Load);
        Assert.Equal(5, byMember["trait:trait.maelstrom-aura"].Load);
        Assert.Equal(3, byMember["trait:trait.spectral-flight"].Load);
        Assert.Equal(3, byMember["trait:trait.clouds-jaunt"].Load);
        Assert.Equal(4, byMember["trait:trait.cloudy-escape"].Load);
        Assert.Equal(2, byMember["trait:trait.arcane-cantrip"].Load);
        Assert.Equal(3, byMember["trait:trait.animal-speech"].Load);
        Assert.Equal(4, byMember["trait:trait.second-chance"].Load);
        Assert.Equal(4, byMember["trait:trait.adrenaline-rush"].Load);
        Assert.Equal(4, byMember["trait:trait.orcish-fury"].Load);
        Assert.Equal(4, byMember["trait:trait.dwarven-fortitude"].Load);
        Assert.Equal(4, byMember["trait:trait.fade-away"].Load);
        Assert.Equal(2, byMember["trait:trait.natural-trickster"].Load);
        var tremor = byMember["trait:trait.stonebound-tremorsense"];
        Assert.False(tremor.AllowUnmatchedConfigurationFallback);
        Assert.Equal(new[] { 3, 4 }, tremor.Configurations!.Select(x => x.Load).ToArray());
        Assert.Equal(3, byMember["trait:trait.level-based-vitality"].Load);
        Assert.Equal(3, byMember["trait:trait.bountiful-luck"].Load);
        Assert.Equal(3, byMember["trait:trait.natural-camouflage"].Load);
        Assert.Equal(1, byMember["trait:trait.trackless-passage"].Load);
        Assert.Equal(1, byMember["trait:trait.dancing-lights"].Load);
        Assert.Equal(1, byMember["trait:trait.fire-bolt"].Load);
        Assert.Equal(2, byMember["trait:trait.detect-magic"].Load);
        Assert.Equal(3, byMember["trait:trait.darkness"].Load);
        Assert.Equal(3, byMember["trait:trait.hellish-rebuke"].Load);
        Assert.Equal(5, byMember["trait:trait.misty-step"].Load);

        for (var level = 0; level <= 9; level++)
            Assert.Equal(level + 1,
                NativeOriginFirstPartyBiologicalPricing.SpellLevelCost(level));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NativeOriginFirstPartyBiologicalPricing.SpellLevelCost(10));
    }

    [Fact]
    public void ConfiguredDarkvisionAndResistancesDoNotHaveAFreeFallback()
    {
        var policy = NativeOriginFirstPartyBiologicalPricing.Create();
        var sight = Assert.Single(policy.Prices,
            x => x.MemberKeys.Contains("intrinsic:darkvisionFeet"));
        Assert.False(sight.AllowUnmatchedConfigurationFallback);
        Assert.Equal(new[] { 3, 4 },
            sight.Configurations!.Select(x => x.Load).ToArray());
        var walk = Assert.Single(policy.Prices,
            x => x.MemberKeys.Contains("intrinsic:walkingSpeedFeet"));
        Assert.False(walk.AllowUnmatchedConfigurationFallback);
        Assert.Equal(new[] { 20, 25, 30, 35, 40, 45 },
            walk.Configurations!.Select(x =>
                int.Parse(x.Id.Split('-')[0])).ToArray());
        Assert.All(walk.Configurations, x => Assert.Equal(0, x.Load));

        foreach (var name in new[]
        {
            "trait.damage-type-resistance", "trait.born-of-flame",
            "trait.born-of-ice"
        })
        {
            var entry = Assert.Single(policy.Prices, x =>
                x.MemberKeys.Contains("trait:" + name));
            Assert.False(entry.AllowUnmatchedConfigurationFallback);
            Assert.Equal(new[] { 2, 3, 4, 5 },
                entry.Configurations!.Select(x => x.Load).ToArray());
        }
        Assert.Null(policy.MultiplicityPremiums);
        Assert.Null(policy.InteractionPrices);
    }

    [Fact]
    public void FreeFoundationAndWholeTraitChargesPassAndEnforceTheBudget()
    {
        var catalog = Native().Catalog;
        var accepted = EvaluateDirect(catalog, 18);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, accepted.Status);
        Assert.Equal("within-allowance", accepted.CreationAllocation!.Status);
        Assert.Equal(7, accepted.CreationAllocation.CommittedLoad);
        Assert.False(accepted.CreationAllocation.PreviewOnly);
        Assert.All(accepted.CreationAllocation.Charges.Where(x =>
            x.CharacteristicKeys.Single().StartsWith("intrinsic:")),
            x => Assert.Equal(0, x.Load));

        var rejected = EvaluateDirect(catalog, 6);
        Assert.Equal(NativeOriginConstructionStatus.Conflicting, rejected.Status);
        Assert.Equal("over-allowance", rejected.CreationAllocation!.Status);
        Assert.Equal(7, rejected.CreationAllocation.CommittedLoad);
        Assert.Contains(rejected.Issues,
            x => x.Code == "creation-allocation-exceeded");
    }

    [Fact]
    public void DirectAndMixedEffectiveLowLightVisionUseTheSamePrice()
    {
        var catalog = Native().Catalog;
        var direct = EvaluateDirect(catalog, 18);
        var baseProposal = new NativeOriginProposal("mixed-elf-first-party",
            NativeOriginIntent.Mixed, new("rule.creature-origination", "biological"),
            [
                new NativeOriginContributor("elf",
                    new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.elf"))
            ], [], []);
        var engine = new NativeOriginConstructionEngine(catalog);
        var offered = engine.Evaluate(new(baseProposal)).EvaluatedProposal.Candidates;
        var selected = offered.Where(x => x.ContributorId == "elf" &&
            (x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet" ||
             x.Identity.MechanicalDefinitionId == "trait.low-light-vision"))
            .ToArray();
        Assert.Equal(4, selected.Length);
        var proposal = baseProposal with
        {
            Manifestations = selected.Select((x, i) =>
                new NativeOriginManifestation("selected-" + i, x.Id,
                    SelectedValue: x.OfferedValue)).ToArray()
        };
        var mixed = engine.Evaluate(new(proposal,
            AutoManifestDirectDefaults: false,
            RequireCreationAllocation: true,
            CreationAllocationPolicy:
                NativeOriginFirstPartyBiologicalPricing.Create()));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, mixed.Status);
        Assert.Equal(2, mixed.CreationAllocation!.CommittedLoad);
        var key = "trait:trait.low-light-vision";
        var directCharge = Assert.Single(direct.CreationAllocation!.Charges,
            x => x.CharacteristicKeys.Contains(key));
        var mixedCharge = Assert.Single(mixed.CreationAllocation.Charges,
            x => x.CharacteristicKeys.Contains(key));
        Assert.Equal(2, directCharge.Load);
        Assert.Equal(directCharge.Load, mixedCharge.Load);
    }

    [Fact]
    public void UnsupportedZeroPriceForAnIndependentBenefitStillFailsClosed()
    {
        var catalog = Native().Catalog;
        var direct = EvaluateDirect(catalog, 18);
        var legitimate = NativeOriginFirstPartyBiologicalPricing.Create();
        var malformed = legitimate with
        {
            Prices = legitimate.Prices.Select(x =>
                x.MemberKeys.Contains("trait:trait.trance")
                    ? x with { Load = 0 } : x).ToArray()
        };
        var result = new NativeOriginConstructionEngine(catalog).Evaluate(
            new(DirectElf(), RequireCreationAllocation: true,
                CreationAllocationPolicy: malformed));
        Assert.Equal(NativeOriginConstructionStatus.Invalid, result.Status);
        Assert.Equal("invalid-policy", result.CreationAllocation!.Status);
        Assert.Contains(result.Issues,
            x => x.Code == "creation-allocation-policy-invalid");
    }

    [Fact]
    public void NativeDrowDarkvisionIncludesItsDerivedSunlightCostExactlyOnce()
    {
        var catalog = Native().Catalog;
        var engine = new NativeOriginConstructionEngine(catalog);
        var proposal = new NativeOriginProposal("drow-first-party",
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedSpeciesId: "species.elf",
            SelectedAncestryId: "ancestry.elf.drow");
        var offered = engine.Evaluate(new(proposal)).EvaluatedProposal.Candidates;
        var ability = Assert.Single(offered,
            x => x.Identity.IntrinsicSlotId == "spellcastingAbility");
        proposal = proposal with
        {
            Manifestations =
            [
                new("drow-ability-choice", ability.Id,
                    Choices: [new(Assert.Single(ability.RequiredChoices!).Id,
                        ["charisma"])])
            ]
        };
        var result = engine.Evaluate(new(proposal, RequireCreationAllocation: true,
            CreationAllocationPolicy:
                NativeOriginFirstPartyBiologicalPricing.Create()));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Equal("within-allowance", result.CreationAllocation!.Status);
        Assert.Equal(18, result.CreationAllocation.CommittedLoad);
        Assert.Contains(result.CreationAllocation.Charges,
            x => x.CharacteristicKeys.Contains("intrinsic:darkvisionFeet") &&
                x.Load == 4 &&
                x.ConfigurationId == "120-feet-with-sunlight-drawback");
        Assert.Contains(result.CreationAllocation.Charges,
            x => x.CharacteristicKeys.Contains(
                "trait:trait.sunlight-sensitivity") && x.Load == 0);
        Assert.Contains(result.CreationAllocation.Charges,
            x => x.CharacteristicKeys.Contains(
                "intrinsic:spellcastingAbility") && x.Load == 1);
    }

    [Fact]
    public void NativeRedDragonbornBindsOneIndependentFiftyPercentResistance()
    {
        var catalog = Native().Catalog;
        var proposal = new NativeOriginProposal("red-dragonborn-first-party",
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedSpeciesId: "species.dragonborn",
            SelectedAncestryId: "ancestry.dragonborn.red");
        var result = new NativeOriginConstructionEngine(catalog).Evaluate(
            new(proposal, RequireCreationAllocation: true,
                CreationAllocationPolicy:
                    NativeOriginFirstPartyBiologicalPricing.Create()));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Equal("within-allowance", result.CreationAllocation!.Status);
        Assert.Contains(result.CreationAllocation.Charges,
            x => x.CharacteristicKeys.Contains(
                "trait:trait.damage-type-resistance") &&
                x.ConfigurationId == "50-percent" && x.Load == 3);
        Assert.Contains(result.CreationAllocation.Charges,
            x => x.CharacteristicKeys.Contains("trait:trait.breath-weapon") &&
                x.Load == 3);
    }

    [Theory]
    [InlineData("ancestry.dragonborn.blue", 16)]
    [InlineData("ancestry.dragonborn.red", 17)]
    [InlineData("ancestry.dragonborn.white", 18)]
    public void OrdinaryDragonbornInheritedGenesArePricedAtCreation(
        string ancestryId, int expectedPoints)
    {
        var catalog = Native().Catalog;
        var proposal = new NativeOriginProposal("ordinary-dragonborn-priced",
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedSpeciesId: "species.dragonborn",
            SelectedAncestryId: ancestryId);
        var result = new NativeOriginConstructionEngine(catalog).Evaluate(
            new(proposal, RequireCreationAllocation: true,
                CreationAllocationPolicy:
                    NativeOriginFirstPartyBiologicalPricing.Create()));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Equal("within-allowance", result.CreationAllocation!.Status);
        Assert.Equal(expectedPoints, result.CreationAllocation.CommittedLoad);
        Assert.Contains(result.CreationAllocation.Charges,
            x => x.CharacteristicKeys.Contains("trait:trait.draconic-natural-weapons")
                && x.Load == 1);
        Assert.Contains(result.CreationAllocation.Charges,
            x => x.CharacteristicKeys.Contains("trait:trait.draconic-wings")
                && x.Load == 5);
    }
}
