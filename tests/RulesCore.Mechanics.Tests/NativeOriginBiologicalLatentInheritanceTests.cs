using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Source-neutral multigenerational inheritance: gene presence is distinct from
/// grant expression, and latency is derived without a second capacity quota.
/// </summary>
public sealed class NativeOriginBiologicalLatentInheritanceTests
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
        throw new InvalidOperationException("First-party native ruleset missing.");
    }

    private static NativeOriginConstructionResult Evaluate(
        NativeOriginProposal proposal,
        INativeOriginManifestedContributorAuthority? authority = null) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(new(
            proposal,
            TrustedScope: authority is null ? null : new("campaign", "latent-test"),
            AutoManifestDirectDefaults: false),
            manifestedContributorAuthority: authority);

    private sealed class ParentAuthority(NativeOriginProfile profile)
        : INativeOriginManifestedContributorAuthority
    {
        public NativeOriginVerifiedManifestedContributor? ResolveCurrent(
            NativeOriginSourceReference source,
            NativeTableDecisionScope scope) =>
            scope.Kind == "campaign" && scope.Id == "latent-test" &&
            source.Id == profile.Id && source.Version == "revision-1"
                ? new(profile, "revision-1")
                : null;
    }

    private static NativeOriginContributor Orc(string id) =>
        new(id, new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.Native, "species.orc"));

    private static NativeOriginProposal OrcProposal(int parents = 2) =>
        new("orc-latent-" + parents, NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            Enumerable.Range(0, parents).Select(i => Orc("parent-" + i)).ToArray(),
            [], []);

    private static NativeOriginCandidate[] Foundation(
        IEnumerable<NativeOriginCandidate> offers, string contributorId) =>
        offers.Where(x => x.ContributorId == contributorId &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();

    private static NativeOriginManifestation Express(NativeOriginCandidate candidate,
        int index) =>
        new("express-" + index, candidate.Id,
            SelectedValue: candidate.Identity.Kind ==
                NativeOriginCharacteristicKind.Intrinsic
                ? candidate.OfferedValue : null);

    private static (NativeOriginConstructionResult Result,
        NativeOriginCandidate Latent, NativeOriginCandidate Expressed) Parent()
    {
        var proposal = OrcProposal();
        var offers = Evaluate(proposal).EvaluatedProposal.Candidates;
        var foundation = Foundation(offers, "parent-0");
        Assert.Equal(3, foundation.Length);
        var powers = offers.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.powerful-build")
            .OrderBy(x => x.ContributorId, StringComparer.Ordinal).ToArray();
        Assert.Equal(2, powers.Length);
        var expressed = foundation.Append(powers[1]).ToArray();
        var result = Evaluate(proposal with
        {
            Manifestations = expressed.Select(Express).ToArray(),
            BiologicalTransmission = new(foundation.Select(x => x.Id)
                .Concat(powers.Select(x => x.Id)).ToArray())
        });
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Empty(result.Issues);
        return (result, powers[0], powers[1]);
    }

    [Fact]
    public void TwoInstancesAllowEitherExpressionAndDeriveOtherAsLatent()
    {
        var (result, latent, expressed) = Parent();
        var genes = result.Profile!.Underlying.BiologicalGenes!;
        var powerful = genes.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.powerful-build").ToArray();
        Assert.Equal(2, powerful.Length);
        Assert.Equal(2, powerful.Select(x => x.SourceCandidateId).Distinct().Count());
        Assert.Single(result.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.powerful-build");
        var group = result.BiologicalCombination!.InheritedInstances!
            .Where(x => x.Gene.Identity.MechanicalDefinitionId ==
                "trait.powerful-build").ToArray();
        Assert.Equal(2, group.Length);
        Assert.True(Assert.Single(group, x => x.Gene.SourceCandidateId == latent.Id).IsLatent);
        Assert.False(Assert.Single(group, x => x.Gene.SourceCandidateId == expressed.Id).IsLatent);
        Assert.Equal(1, group.Count(x => x.IsLatent));

        var saved = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(result.Profile))!;
        Assert.Equal(2, saved.Underlying.BiologicalGenes!.Count(x =>
            x.Identity.MechanicalDefinitionId == "trait.powerful-build"));
        Assert.All(saved.Underlying.BiologicalGenes, x =>
            Assert.DoesNotContain(x.Id,
                saved.Underlying.InherentGrants.Select(grant => grant.Id)));
    }

    [Fact]
    public void AVerifiedDescendantCanExpressPreviouslyLatentInstance()
    {
        var (parent, latent, expressed) = Parent();
        var contributor = new NativeOriginContributor("carrier",
            new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.ManifestedProfile,
                parent.Profile!.Id, "revision-1"));
        var proposal = new NativeOriginProposal("third-generation",
            NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [contributor], [], []);
        var fixture = new ParentAuthority(parent.Profile);
        var offers = Evaluate(proposal, fixture).EvaluatedProposal.Candidates;
        var foundation = Foundation(offers, "carrier");
        Assert.Equal(3, foundation.Length);
        var powers = offers.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.powerful-build").ToArray();
        Assert.Equal(2, powers.Length);
        Assert.All(powers, x =>
            Assert.Equal(NativeOriginEligibility.Eligible,
                x.SelectionBasis.Eligibility));
        var carried = Assert.Single(powers, x =>
            x.Id.EndsWith(":gene:gene:" + parent.EvaluatedProposal.Id + ":" + latent.Id,
                StringComparison.Ordinal));
        var other = Assert.Single(powers, x => x.Id != carried.Id);
        var selected = foundation.Append(carried).ToArray();
        var result = Evaluate(proposal with
        {
            Manifestations = selected.Select(Express).ToArray(),
            BiologicalTransmission = new(selected.Select(x => x.Id).ToArray())
        }, fixture);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Empty(result.Issues);
        var active = Assert.Single(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.powerful-build");
        Assert.Equal(carried.Id, Assert.Single(active.SourceCandidateIds));
        Assert.DoesNotContain(result.Profile.Underlying.BiologicalGenes!,
            gene => gene.SourceCandidateId == other.Id);
        var inherited = Assert.Single(result.Profile.Underlying.BiologicalGenes!, x =>
            x.SourceCandidateId == carried.Id);
        Assert.Equal(parent.Profile.Id, inherited.InheritedFrom!.ContributorProfileId);
        Assert.Equal("revision-1", inherited.InheritedFrom.ContributorRevision);
        Assert.False(inherited.InheritedFrom.SourceWasGrant);
        Assert.False(Assert.Single(result.BiologicalCombination!.InheritedInstances!, x =>
            x.Gene.SourceCandidateId == carried.Id).IsLatent);
        Assert.NotEqual(expressed.Id, carried.Id);
    }

    [Fact]
    public void BothInstancesMayRemainLatentWithoutASeparateLatentLimit()
    {
        var proposal = OrcProposal();
        var offers = Evaluate(proposal).EvaluatedProposal.Candidates;
        var foundation = Foundation(offers, "parent-0");
        var powerful = offers.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.powerful-build").ToArray();
        var result = Evaluate(proposal with
        {
            BiologicalTransmission = new(foundation.Select(x => x.Id)
                .Concat(powerful.Select(x => x.Id)).ToArray()),
            Manifestations = foundation.Select(Express).ToArray()
        });
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.powerful-build");
        Assert.Equal(2, result.BiologicalCombination!.InheritedInstances!.Count(x =>
            x.Gene.Identity.MechanicalDefinitionId == "trait.powerful-build" &&
            x.IsLatent));
    }

    [Fact]
    public void ThreeOfOneGeneKindExceedsConfigurableInheritedCapacity()
    {
        var proposal = OrcProposal(3);
        var offers = Evaluate(proposal).EvaluatedProposal.Candidates;
        var foundation = Foundation(offers, "parent-0");
        var powerful = offers.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.powerful-build").ToArray();
        Assert.Equal(3, powerful.Length);
        var selection = foundation.Append(powerful[0]).ToArray();
        var result = Evaluate(proposal with
        {
            Manifestations = selection.Select(Express).ToArray(),
            BiologicalTransmission = new(foundation.Select(x => x.Id)
                .Concat(powerful.Select(x => x.Id)).ToArray())
        });
        Assert.Equal(NativeOriginConstructionStatus.Conflicting, result.Status);
        Assert.Contains(result.Issues, x =>
            x.Code == "biological-inherited-capacity-exceeded" &&
            x.Path.Contains("trait.powerful-build", StringComparison.Ordinal));
    }

    [Fact]
    public void UnverifiedParentCanNotSupplyLatentGeneInstances()
    {
        var (parent, _, _) = Parent();
        var claim = new NativeOriginContributor("carrier",
            new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.ManifestedProfile,
                parent.Profile!.Id, "revision-1"),
            parent.Profile.Underlying);
        var proposal = new NativeOriginProposal("unverified-latency",
            NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [claim], [], []);
        var result = Evaluate(proposal);
        Assert.All(result.EvaluatedProposal.Candidates, x =>
            Assert.NotEqual(NativeOriginEligibility.Eligible,
                x.SelectionBasis.Eligibility));
        Assert.DoesNotContain(result.BiologicalCombination!.Characteristics, x =>
            x.Offers.Any(offer =>
                offer.Candidate.Id.Contains(":gene:", StringComparison.Ordinal)));
    }

    [Fact]
    public void NewlyLatentGeneWithPersistentChoiceRequiresValidatedConfiguration()
    {
        var proposal = new NativeOriginProposal("goliath-choice-latent",
            NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [new("goliath", new(NativeOriginInputKind.BiologicalParent),
                new(NativeOriginSourceKind.Native, "ancestry.goliath.stone"))],
            [], []);
        var candidates = Evaluate(proposal).EvaluatedProposal.Candidates;
        var foundation = Foundation(candidates, "goliath");
        Assert.Equal(3, foundation.Length);
        var aptitude = Assert.Single(candidates, x =>
            x.Identity.MechanicalDefinitionId == "trait.ancestral-aptitude");
        var chosen = foundation.Select(Express).ToArray();
        var list = foundation.Select(x => x.Id).Append(aptitude.Id).ToArray();
        var withoutChoice = Evaluate(proposal with
        {
            Manifestations = chosen,
            BiologicalTransmission = new(list)
        });
        Assert.Contains(withoutChoice.Issues, x =>
            x.Code == "biological-latent-choice-incomplete" &&
            x.Path == aptitude.Id);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, withoutChoice.Status);

        var withChoice = Evaluate(proposal with
        {
            Manifestations = chosen,
            BiologicalTransmission = new(list, [
                new(aptitude.Id, [new("abilityChoice", ["constitution"])])
            ])
        });
        Assert.Equal(NativeOriginConstructionStatus.Authorized, withChoice.Status);
        Assert.Empty(withChoice.Issues);
        var stored = Assert.Single(withChoice.Profile!.Underlying.BiologicalGenes!, x =>
            x.SourceCandidateId == aptitude.Id);
        Assert.Equal("constitution",
            Assert.Single(stored.ResolvedChoices!).SelectedOptionIds.Single());
        Assert.DoesNotContain(withChoice.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.ancestral-aptitude");
    }

    [Fact]
    public void RuleDataControlsCapacityWithoutGeneSpecificDominanceFlags()
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("rule.creature-origination", out var rule));
        var policy = rule!.Definition.GetProperty("biologicalTransmission");
        Assert.Equal(2, policy.GetProperty("maxInheritedInstancesPerKind").GetInt32());
        Assert.Equal(1, policy.GetProperty("maxExpressedInstancesPerKind").GetInt32());
    }
}
