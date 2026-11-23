using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Approved Halfling Species luck and Lightfoot specialization. The
/// biome choice is a permanent, source-authored gene configuration, not a
/// gameplay preference or an additional separately inherited characteristic.
/// </summary>
public sealed class NativeOriginHalflingRevisionTests
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

    private static NativeOriginProposal Halfling() =>
        new("halfling-approved-foundation", NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedSpeciesId: "species.halfling");

    private static NativeOriginProposal Lightfoot() =>
        new("halfling-approved-lightfoot", NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedAncestryId: "ancestry.halfling.lightfoot");

    private static NativeOriginConstructionResult Evaluate(
        NativeOriginProposal proposal, bool price = true) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(new(proposal,
            RequireCreationAllocation: price,
            CreationAllocationPolicy: price
                ? NativeOriginFirstPartyBiologicalPricing.Create()
                : null));

    [Fact]
    public void HalflingFoundationHasItsOwnLuckEffectsWithoutStealth()
    {
        var result = Evaluate(Halfling());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Equal(12, result.CreationAllocation!.CommittedLoad);
        var traits = result.Profile!.Underlying.InherentGrants
            .Where(x => x.Identity.MechanicalDefinitionId is not null)
            .Select(x => x.Identity.MechanicalDefinitionId).ToHashSet();

        Assert.Contains("trait.luck-reroll", traits);
        Assert.Contains("trait.brave", traits);
        Assert.Contains("trait.keen-hearing", traits);
        Assert.Contains("trait.larger-creature-passage", traits);
        Assert.Contains("trait.second-chance", traits);
        Assert.Contains("trait.bountiful-luck", traits);
        Assert.DoesNotContain("trait.naturally-stealthy", traits);
        Assert.DoesNotContain("trait.trackless-passage", traits);
    }

    [Fact]
    public void ReactionAndLuckInteractionsAreExplicitWithoutAbilityIncrease()
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("trait.second-chance", out var second));
        Assert.True(catalog.TryGetById("trait.bountiful-luck", out var sharing));
        var counter = second!.Definition.GetProperty("trait");
        Assert.Equal("reaction", counter.GetProperty("activation")
            .GetProperty("action").GetString());
        Assert.Equal("initiative-or-short-or-long-rest", counter
            .GetProperty("uses").GetProperty("recharge").GetString());
        Assert.Equal("new-roll", counter.GetProperty("reroll")
            .GetProperty("useResult").GetString());

        var gift = sharing!.Definition.GetProperty("trait");
        Assert.Equal("reaction", gift.GetProperty("activation")
            .GetProperty("action").GetString());
        Assert.Equal(30, gift.GetProperty("rangeFeet").GetInt32());
        Assert.Equal("trait.luck-reroll", gift.GetProperty("suspendsTrait")
            .GetProperty("id").GetString());
        Assert.Equal("end-of-next-turn", gift.GetProperty("suspendsTrait")
            .GetProperty("until").GetString());
        Assert.False(counter.TryGetProperty("abilityScoreIncrease", out _));
        Assert.False(gift.TryGetProperty("abilityScoreIncrease", out _));
    }

    [Fact]
    public void LightfootRequiresOneAncestralBiomeBeforeFullGrant()
    {
        var absent = Evaluate(Lightfoot(), price: false);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, absent.Status);
        var trackless = Assert.Single(absent.EvaluatedProposal.Candidates, x =>
            x.Identity.MechanicalDefinitionId == "trait.trackless-passage");
        var req = Assert.Single(trackless.RequiredChoices!);
        Assert.Equal("subTrait.biome", req.Id);
        Assert.Equal("subTrait.biome", req.ParameterKey);
        Assert.Contains("woodland", req.FiniteOptions!);
        Assert.Contains("grassland", req.FiniteOptions!);
        Assert.DoesNotContain(absent.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.trackless-passage");

        var selected = Lightfoot() with
        {
            Manifestations =
            [
                new("lightfoot-ancestral-biome", trackless.Id,
                    Choices: [new(req.Id, ["grassland"])])
            ]
        };
        var complete = Evaluate(selected);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, complete.Status);
        Assert.Equal(17, complete.CreationAllocation!.CommittedLoad);
        var traits = complete.Profile!.Underlying.InherentGrants
            .Where(x => x.Identity.MechanicalDefinitionId is not null)
            .Select(x => x.Identity.MechanicalDefinitionId).ToHashSet();
        Assert.Contains("trait.naturally-stealthy", traits);
        Assert.Contains("trait.natural-agility", traits);
        Assert.Contains("trait.trackless-passage", traits);

        var granted = Assert.Single(complete.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.trackless-passage");
        Assert.Equal("grassland", granted.Parameters!["subTrait"]
            .GetProperty("biome").GetString());
        Assert.False(granted.Parameters["subTrait"]
            .TryGetProperty("biomeChoices", out _));
        Assert.Equal("grassland", Assert.Single(granted.ResolvedChoices!)
            .SelectedOptionIds.Single());
    }

    [Theory]
    [InlineData("volcanic")]
    [InlineData("")]
    public void LightfootCannotSelectAnUnofferedBiome(string biome)
    {
        var template = Evaluate(Lightfoot(), price: false);
        var trackless = Assert.Single(template.EvaluatedProposal.Candidates, x =>
            x.Identity.MechanicalDefinitionId == "trait.trackless-passage");
        var req = Assert.Single(trackless.RequiredChoices!);
        var attempt = Evaluate(Lightfoot() with
        {
            Manifestations =
            [
                new("invalid-biome", trackless.Id, Choices: [new(req.Id, [biome])])
            ]
        }, price: false);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, attempt.Status);
        Assert.DoesNotContain(attempt.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.trackless-passage");
    }

    [Fact]
    public void OtherWoodlandTraitsRetainTheirFixedBiomeWithoutASelection()
    {
        var catalog = Catalog();
        foreach (var selectedSource in new[]
            { "ancestry.gnome.forest", "ancestry.elf.wood" })
        {
            var result = new NativeOriginConstructionEngine(catalog).Evaluate(
                new(new("other-woodland", NativeOriginIntent.Direct,
                    new("rule.creature-origination", "biological"), [], [], [],
                    SelectedAncestryId: selectedSource),
                    RequireCreationAllocation: false));
            var offered = Assert.Single(result.EvaluatedProposal.Candidates, x =>
                x.Identity.MechanicalDefinitionId == "trait.trackless-passage");
            Assert.Empty(offered.RequiredChoices ?? []);
            Assert.Equal("woodland", offered.OfferedParameters!["subTrait"]
                .GetProperty("biome").GetString());
        }
    }
}
