using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Real-catalog biological transmission checks. Opt-in annotations only admit
/// an authored offer: the player still chooses all effective characteristics,
/// and unsupported traits remain unavailable without trusted table decisions.
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
    [InlineData("ancestry.dragonborn.bronze", "trait.shared.amphibious")]
    [InlineData("species.elf", "trait.elf.trance")]
    [InlineData("ancestry.goliath.fire", "trait.goliath.altitude-acclimation")]
    [InlineData("species.orc", "trait.orc.relentless-endurance")]
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
    [InlineData("ancestry.dragonborn.red", "trait.dragonborn.draconic-resistance")]
    [InlineData("species.dwarf", "trait.dwarf.dwarven-resilience")]
    [InlineData("species.elf", "trait.elf.fey-ancestry")]
    [InlineData("species.gnome", "trait.gnome.gnomish-cunning")]
    [InlineData("ancestry.goliath.fire", "trait.goliath.powerful-build")]
    [InlineData("species.halfling", "trait.halfling.halfling-luck")]
    [InlineData("species.human", "trait.human.adaptable-learning")]
    [InlineData("species.orc", "trait.orc.savage-attacks")]
    [InlineData("ancestry.tiefling.infernal", "trait.tiefling.infernal.infernal-resistance")]
    public void UnreviewedTraitsFromEveryBaseSpeciesCannotSelfAuthorize(
        string nativeDonor, string traitId)
    {
        var catalog = Catalog();
        var proposal = Proposal(nativeDonor);
        var offered = Assert.Single(Evaluate(catalog, proposal)
            .EvaluatedProposal.Candidates, x =>
                x.Identity.MechanicalDefinitionId == traitId);
        Assert.NotEqual(NativeOriginEligibility.Eligible,
            offered.SelectionBasis.Eligibility);

        var forged = proposal with
        {
            Candidates =
            [
                offered with
                {
                    SelectionBasis = new(NativeOriginEligibility.Eligible,
                        RuleReference: "rule.creature-origination")
                }
            ],
            Manifestations = [new("injected", offered.Id)]
        };
        var denied = Evaluate(catalog, forged);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, denied.Status);
        Assert.DoesNotContain(denied.Profile?.Underlying.InherentGrants ?? [], x =>
            x.Identity.MechanicalDefinitionId == traitId);
    }

    [Fact]
    public void BiologicalTraitAuthoringMustRemainExplicitAndSourceNeutral()
    {
        var catalog = Catalog();
        Assert.Equal(9, catalog.CountByCategory("species"));

        foreach (var id in new[]
        {
            "trait.shared.low-light-vision",
            "trait.dragonborn.breath-weapon",
            "trait.shared.amphibious",
            "trait.elf.trance",
            "trait.goliath.altitude-acclimation",
            "trait.orc.relentless-endurance"
        })
        {
            Assert.True(catalog.TryGetById(id, out var definition), id);
            Assert.Equal("origin-trait", definition!.Category);
            Assert.True(definition.Definition.TryGetProperty("inheritanceMethods",
                out var methods), id);
            Assert.Contains(methods.EnumerateArray(), x =>
                x.ValueKind == JsonValueKind.String &&
                x.GetString() == "biological");
        }
    }
}
