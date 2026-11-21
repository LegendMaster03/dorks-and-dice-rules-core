using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// An explicit biological transmission plan is player-selected, separate
/// from manifestation, and never a substitute for source or grant authority.
/// </summary>
public sealed class NativeOriginBiologicalTransmissionPlanTests
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

    private static NativeOriginProposal Proposal() =>
        new("explicit-transmission", NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"), [
                new("human", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.human")),
                new("elf", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.elf"))
            ], [], []);

    private static NativeOriginConstructionResult Evaluate(NativeOriginProposal proposal) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, AutoManifestDirectDefaults: false));

    private static (NativeOriginProposal Proposal, NativeOriginCandidate[] Foundation,
        NativeOriginCandidate Fey) SelectElf()
    {
        var proposal = Proposal();
        var offered = Evaluate(proposal).EvaluatedProposal.Candidates;
        var foundation = offered.Where(x => x.ContributorId == "elf" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        Assert.Equal(3, foundation.Length);
        var fey = Assert.Single(offered, x => x.ContributorId == "elf" &&
            x.Identity.MechanicalDefinitionId == "trait.fey-ancestry");
        var manifestations = foundation.Append(fey).Select((candidate, index) =>
            new NativeOriginManifestation("choice-" + index, candidate.Id,
                SelectedValue: candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic
                    ? candidate.OfferedValue : null)).ToArray();
        return (proposal with { Manifestations = manifestations }, foundation, fey);
    }

    [Fact]
    public void ExplicitTransmissionAndManifestationReconstructAuthorizedOrigin()
    {
        var (proposal, foundation, fey) = SelectElf();
        var selected = foundation.Append(fey).Select(x => x.Id).ToArray();
        proposal = proposal with
        {
            BiologicalTransmission = new(selected)
        };
        var result = Evaluate(proposal);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Empty(result.Issues);
        var combine = result.BiologicalCombination!;
        Assert.True(combine.ExplicitTransmissionPlan);
        var feyGroup = Assert.Single(combine.Characteristics, x =>
            x.CharacteristicId == "trait.fey-ancestry");
        Assert.True(Assert.Single(feyGroup.Offers).ProposedForTransmission);
        Assert.Single(feyGroup.Expressions);
        Assert.All(foundation, candidate =>
            Assert.Contains(combine.Characteristics.SelectMany(x => x.Offers),
                offer => offer.Candidate.Id == candidate.Id &&
                    offer.ProposedForTransmission));
        Assert.Contains(result.Profile!.Underlying.InherentGrants,
            x => x.Identity.MechanicalDefinitionId == "trait.fey-ancestry");

        var saved = JsonSerializer.Deserialize<NativeOriginProposal>(
            JsonSerializer.Serialize(result.EvaluatedProposal));
        Assert.Equal(selected,
            saved!.BiologicalTransmission!.CandidateIds);
    }

    [Fact]
    public void ManifestationWithoutExplicitTransmissionCannotCreateTheGene()
    {
        var (proposal, foundation, fey) = SelectElf();
        proposal = proposal with
        {
            BiologicalTransmission = new(foundation.Select(x => x.Id).ToArray())
        };
        var result = Evaluate(proposal);
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, result.Status);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "biological-expression-without-transmission" &&
            issue.Path == fey.Id);
        Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants,
            grant => grant.Identity.MechanicalDefinitionId == "trait.fey-ancestry");
        var feyOffer = Assert.Single(Assert.Single(
            result.BiologicalCombination!.Characteristics,
            x => x.CharacteristicId == "trait.fey-ancestry").Offers);
        Assert.False(feyOffer.ProposedForTransmission);
        Assert.Single(feyOffer.AttemptedManifestationIds);
        Assert.Empty(feyOffer.EffectiveGrantIds);
    }

    [Fact]
    public void SelectedForTransmissionWithoutExpressionPersistsAsLatentGene()
    {
        var (proposal, foundation, fey) = SelectElf();
        proposal = proposal with
        {
            Manifestations = proposal.Manifestations.Where(x =>
                x.CandidateId != fey.Id).ToArray(),
            BiologicalTransmission = new(foundation.Select(x => x.Id)
                .Append(fey.Id).ToArray())
        };
        var result = Evaluate(proposal);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Empty(result.Issues);
        Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants,
            grant => grant.Identity.MechanicalDefinitionId == "trait.fey-ancestry");
        var stored = Assert.Single(result.Profile.Underlying.BiologicalGenes!, x =>
            x.Identity.MechanicalDefinitionId == "trait.fey-ancestry");
        Assert.Equal(fey.Id, stored.SourceCandidateId);
        var projected = Assert.Single(result.BiologicalCombination!.InheritedInstances!, x =>
            x.Gene.SourceCandidateId == fey.Id);
        Assert.True(projected.IsLatent);
        Assert.Empty(projected.ExpressionGrantIds);
        var roundTrip = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(result.Profile));
        Assert.Equal(stored.Id,
            Assert.Single(roundTrip!.Underlying.BiologicalGenes!, x =>
                x.Identity.MechanicalDefinitionId == "trait.fey-ancestry").Id);
    }

    [Fact]
    public void UnknownCandidateAndDuplicateTransmissionCannotAuthorizeProfile()
    {
        var (proposal, foundation, fey) = SelectElf();
        proposal = proposal with
        {
            BiologicalTransmission = new(foundation.Select(x => x.Id)
                .Append(fey.Id).Append(fey.Id).Append("forged:gene").ToArray())
        };
        var result = Evaluate(proposal);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, result.Status);
        Assert.Null(result.Profile);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "biological-transmission-candidate-duplicate");
        Assert.Contains(result.Issues, issue =>
            issue.Code == "biological-transmission-candidate-unavailable");
    }

    [Fact]
    public void AnExplicitPlanOnOtherOriginationMethodsIsRejected()
    {
        var proposal = Proposal() with
        {
            Method = new("rule.creature-origination", "construct-assembly"),
            BiologicalTransmission = new([])
        };
        var result = Evaluate(proposal);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, result.Status);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "biological-transmission-method-invalid");
    }

    [Fact]
    public void UnspecifiedPlanPreservesExistingIntentionalManifestationBehavior()
    {
        var (proposal, _, _) = SelectElf();
        Assert.Null(proposal.BiologicalTransmission);
        var result = Evaluate(proposal);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.False(result.BiologicalCombination!.ExplicitTransmissionPlan);
        Assert.All(result.BiologicalCombination.Characteristics.SelectMany(x => x.Offers),
            offer => Assert.False(offer.ProposedForTransmission));
    }

    [Fact]
    public void UnverifiedHistoricalContributorCannotBeSelectedForTransmission()
    {
        var parent = new NativeOriginContributor("mixed",
            new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.ManifestedProfile, "parent", "revision"),
            HistoricalSourceIds: ["species.orc"]);
        var proposal = new NativeOriginProposal("unverified", NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [parent], [], [], BiologicalTransmission: new(["historical:trait.powerful-build"]));
        var result = Evaluate(proposal);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, result.Status);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "biological-transmission-candidate-unavailable");
    }
}
