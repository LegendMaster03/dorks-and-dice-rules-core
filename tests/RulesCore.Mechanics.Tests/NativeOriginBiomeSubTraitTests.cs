using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// The biome and animal habitat are configured, source-owned sub-trait values.
/// They do not create separate canonical genes for every environment.
/// </summary>
public sealed class NativeOriginBiomeSubTraitTests
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

    [Theory]
    [InlineData("ancestry.gnome.forest")]
    [InlineData("ancestry.elf.wood")]
    public void WoodlandSourceOffersTwoCanonicalGenesWithOneBiomeSubTrait(
        string sourceId)
    {
        var catalog = Catalog();
        var proposal = new NativeOriginProposal(
            "woodland-" + sourceId, NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [new("parent", new(NativeOriginInputKind.BiologicalParent),
                new(NativeOriginSourceKind.Native, sourceId))],
            [], []);
        var engine = new NativeOriginConstructionEngine(catalog);
        var offers = engine.Evaluate(new(proposal,
            AutoManifestDirectDefaults: false)).EvaluatedProposal.Candidates;
        var keys = new[] { "trait.natural-camouflage", "trait.trackless-passage" };
        foreach (var key in keys)
        {
            var offer = Assert.Single(offers, x =>
                x.Identity.MechanicalDefinitionId == key);
            Assert.Equal(NativeOriginEligibility.Eligible,
                offer.SelectionBasis.Eligibility);
            Assert.Equal("woodland",
                offer.OfferedParameters!["subTrait"]
                    .GetProperty("biome").GetString());
        }
        // The configured biome is not a free, independent characteristic.
        Assert.DoesNotContain(offers, x =>
            x.Identity.IntrinsicSlotId == "environmentSubTrait");
    }

    [Fact]
    public void AnimalSpeechUsesHabitatSubTraitInsteadOfASecondWoodlandGene()
    {
        var catalog = Catalog();
        var source = new NativeOriginProposal(
            "forest-animal-speech", NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedAncestryId: "ancestry.gnome.forest");
        var result = new NativeOriginConstructionEngine(catalog)
            .Evaluate(new(source));
        var speech = Assert.Single(result.EvaluatedProposal.Candidates, x =>
            x.Identity.MechanicalDefinitionId == "trait.animal-speech");
        Assert.Equal("woodland",
            speech.OfferedParameters!["subTrait"]
                .GetProperty("animalHabitat").GetString());
        Assert.True(catalog.TryGetById("trait.animal-speech", out var definition));
        var mechanic = definition!.Definition.GetProperty("trait");
        Assert.Equal("subTrait.animalHabitat", mechanic
            .GetProperty("animalCommunication")
            .GetProperty("habitatParameter").GetString());
        Assert.Equal("proficiency-bonus",
            mechanic.GetProperty("freeUses").GetString());
        Assert.True(mechanic.GetProperty("slotCasting").GetBoolean());
    }

    [Theory]
    [InlineData("ancestry.gnome.forest")]
    [InlineData("ancestry.elf.wood")]
    public void BiomeConfigurationPersistsThroughSelectedMixedGrants(
        string sourceId)
    {
        var catalog = Catalog();
        var proposal = new NativeOriginProposal(
            "mixed-" + sourceId, NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [new("parent", new(NativeOriginInputKind.BiologicalParent),
                new(NativeOriginSourceKind.Native, sourceId))],
            [], []);
        var engine = new NativeOriginConstructionEngine(catalog);
        var offers = engine.Evaluate(new(proposal,
            AutoManifestDirectDefaults: false)).EvaluatedProposal.Candidates;
        var selected = offers.Where(x => x.ContributorId == "parent" &&
            (x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet" ||
             x.Identity.MechanicalDefinitionId is
                "trait.natural-camouflage" or "trait.trackless-passage"))
            .ToArray();
        var profile = engine.Evaluate(new(proposal with
        {
            Manifestations = selected.Select((x, i) =>
                new NativeOriginManifestation("chosen-" + i, x.Id,
                    SelectedValue: x.OfferedValue)).ToArray()
        }, AutoManifestDirectDefaults: false));
        Assert.True(profile.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", profile.Issues.Select(i => i.Code + ":" + i.Path)));
        foreach (var id in new[] { "trait.natural-camouflage", "trait.trackless-passage" })
        {
            var grant = Assert.Single(profile.Profile!.Underlying.InherentGrants,
                x => x.Identity.MechanicalDefinitionId == id);
            Assert.Equal("woodland",
                grant.Parameters!["subTrait"].GetProperty("biome").GetString());
        }
        // The two environmental effects remain distinct genes.
        Assert.Equal(2, profile.Profile!.Underlying.InherentGrants.Count(x =>
            x.Identity.MechanicalDefinitionId is
                "trait.natural-camouflage" or "trait.trackless-passage"));
    }
}
