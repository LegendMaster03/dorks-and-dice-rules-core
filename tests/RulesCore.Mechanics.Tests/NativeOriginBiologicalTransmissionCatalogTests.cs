using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Real-catalog biological gene transmission checks. Gene eligibility comes from
/// native catalog location; actual grants still require player manifestations,
/// complete authored choices, and the universal construction validations.
/// </summary>
public sealed class NativeOriginBiologicalTransmissionCatalogTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var root = Path.Combine(directory.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(root, "first-party-001", "native",
                "rules", "creature-origination.json")))
                return NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(root, "first-party-001"));
            directory = directory.Parent;
        }
        throw new InvalidOperationException("First-party native catalog is missing.");
    }

    private static NativeOriginProposal Proposal(string donor) =>
        new("inherit-" + donor, NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [new("biological-source", new(NativeOriginInputKind.BiologicalParent),
                new(NativeOriginSourceKind.Native, donor))],
            [], []);

    private static NativeOriginConstructionResult Evaluate(
        NativeCatalogSnapshot catalog, NativeOriginProposal proposal) =>
        new NativeOriginConstructionEngine(catalog).Evaluate(
            new(proposal, AutoManifestDirectDefaults: false));

    [Theory]
    [InlineData("ancestry.dragonborn.bronze", "trait.amphibious")]
    [InlineData("species.elf", "trait.trance")]
    [InlineData("ancestry.goliath.fire", "trait.altitude-acclimation")]
    [InlineData("species.orc", "trait.relentless-endurance")]
    public void PlayerCanSelectIndependentHeritableTraitWithCompletePhysicalOrigin(
        string nativeDonor, string traitId)
    {
        var catalog = Catalog();
        var proposal = Proposal(nativeDonor);
        var offers = Evaluate(catalog, proposal).EvaluatedProposal.Candidates;
        var inheritable = Assert.Single(offers, candidate =>
            candidate.Identity.MechanicalDefinitionId == traitId);
        Assert.Equal(NativeOriginEligibility.Eligible,
            inheritable.SelectionBasis.Eligibility);
        Assert.Equal("rule.creature-origination",
            inheritable.SelectionBasis.RuleReference);

        // Selecting a donor does not automatically select any of its traits.
        var none = Evaluate(catalog, proposal);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, none.Status);
        Assert.Empty(none.Profile!.Underlying.InherentGrants);

        var core = offers.Where(x => x.ContributorId == "biological-source" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        Assert.Equal(3, core.Length);
        var manifestations = core.Select((x, index) =>
            new NativeOriginManifestation("physical-" + index, x.Id,
                SelectedValue: x.OfferedValue))
            .Append(new("player-selected-trait", inheritable.Id))
            .ToArray();
        var outcome = Evaluate(catalog, proposal with { Manifestations = manifestations });

        Assert.True(outcome.Status == NativeOriginConstructionStatus.Authorized,
            "Issues: " + string.Join("; ",
                outcome.Issues.Select(x => x.Code + ":" + x.Path)));
        Assert.Equal(4, outcome.Profile!.Underlying.InherentGrants.Count);
        var actual = Assert.Single(outcome.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == traitId);
        Assert.Equal(inheritable.Identity, actual.Identity);
        Assert.Equal(new[] { inheritable.Id }, actual.SourceCandidateIds);
        Assert.Equal(NativeOriginAdjudication.Supported,
            actual.Authorization!.Adjudication);
        Assert.Equal("rule.creature-origination",
            actual.Authorization.NativeRuleReference);
        Assert.Equal(NativeOriginEventStage.Completed,
            Assert.Single(outcome.Profile.History).Stage);
        Assert.Empty(outcome.Issues);

        var saved = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(outcome.Profile));
        Assert.Contains(saved!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == traitId &&
            x.Authorization?.NativeRuleReference == "rule.creature-origination");
    }

    [Theory]
    [InlineData("ancestry.dragonborn.red", "trait.damage-type-resistance")]
    [InlineData("species.dwarf", "trait.poison-resilience")]
    [InlineData("species.elf", "trait.fey-ancestry")]
    [InlineData("species.gnome", "trait.magical-cunning")]
    [InlineData("ancestry.goliath.fire", "trait.powerful-build")]
    [InlineData("species.halfling", "trait.luck-reroll")]
    [InlineData("species.human", "trait.adaptable-learning")]
    [InlineData("species.orc", "trait.savage-attacks")]
    [InlineData("species.orc", "trait.powerful-build")]
    [InlineData("ancestry.elf.drow", "trait.darkness")]
    [InlineData("ancestry.tiefling.infernal", "trait.darkness")]
    [InlineData("ancestry.tiefling.abyssal", "trait.damage-type-resistance")]
    [InlineData("ancestry.tiefling.chthonic", "trait.damage-type-resistance")]
    [InlineData("ancestry.tiefling.infernal", "trait.damage-type-resistance")]
    public void BiologicalGeneFromEachSpeciesIsAnEligibleOffer(
        string nativeDonor, string geneId)
    {
        var proposal = Proposal(nativeDonor);
        var result = Evaluate(Catalog(), proposal);
        var offered = Assert.Single(result.EvaluatedProposal.Candidates, x =>
            x.Identity.MechanicalDefinitionId == geneId);
        Assert.Equal(NativeOriginEligibility.Eligible,
            offered.SelectionBasis.Eligibility);
        Assert.Equal("rule.creature-origination",
            offered.SelectionBasis.RuleReference);
        // Eligibility is not automatic manifestation.
        Assert.DoesNotContain(result.Profile?.Underlying.InherentGrants ?? [], x =>
            x.Identity.MechanicalDefinitionId == geneId);
    }

    [Fact]
    public void BiologicalGeneDefinitionsAreIdentifiedByCatalogLocation()
    {
        var catalog = Catalog();
        foreach (var id in new[]
        {
            "trait.damage-type-resistance",
            "trait.fey-ancestry",
            "trait.amphibious"
        })
        {
            Assert.True(catalog.TryGetById(id, out var gene), id);
            Assert.Equal("origin-trait", gene!.Category);
            Assert.StartsWith("native/origination/biological/genes/", gene.SourcePath);
        }
    }
}
