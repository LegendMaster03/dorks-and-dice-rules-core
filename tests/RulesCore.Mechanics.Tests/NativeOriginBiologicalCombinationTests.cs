using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeOriginBiologicalCombinationTests
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
        throw new InvalidOperationException("First-party ruleset is missing.");
    }

    private static NativeOriginContributor Parent(string id, string nativeId) =>
        new(id, new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.Native, nativeId));

    private static NativeOriginProposal Biological(params NativeOriginContributor[] parents) =>
        new("biological-combine-test", NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"), parents, [], []);

    private static NativeOriginConstructionResult Evaluate(NativeOriginProposal proposal) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, AutoManifestDirectDefaults: false));

    [Fact]
    public void CombineKeepsContributorSpecificConfigurationsWithoutIssuingGrants()
    {
        var result = Evaluate(Biological(
            Parent("red", "ancestry.dragonborn.red"),
            Parent("bronze", "ancestry.dragonborn.bronze")));
        var pool = Assert.IsType<NativeOriginBiologicalCombination>(
            result.BiologicalCombination);
        Assert.Equal("biological-combine-test", pool.ProposalId);
        var breath = Assert.Single(pool.Characteristics, x =>
            x.Kind == NativeOriginCharacteristicKind.Trait &&
            x.CharacteristicId == "trait.breath-weapon");
        Assert.Equal(2, breath.Offers.Count);
        Assert.Equal(new[] { "bronze", "red" },
            breath.Offers.Select(x => x.Candidate.ContributorId));
        Assert.Equal(2, breath.Offers.Select(x => x.Candidate.Id)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.All(breath.Offers, offer =>
        {
            Assert.Equal(NativeOriginEligibility.Eligible,
                offer.Candidate.SelectionBasis.Eligibility);
            Assert.Equal("rule.creature-origination",
                offer.Candidate.SelectionBasis.RuleReference);
            Assert.Equal("trait.breath-weapon",
                offer.Candidate.Identity.MechanicalDefinitionId);
            Assert.Empty(offer.AttemptedManifestationIds);
            Assert.Empty(offer.EffectiveGrantIds);
        });
        Assert.Empty(breath.Expressions);
        Assert.Equal("Breath Weapon", breath.Presentation!.Name);
        Assert.Contains("Breath Weapon", breath.Presentation.Description!);
        Assert.Equal("Active abilities", breath.Presentation.Category);
        Assert.Equal(new[] { "damageType", "area" }, breath.Presentation.RecombinableParameters);
        // Legacy manifestation-only proposals do not run explicit capacity evaluation.
        Assert.Null(pool.MaxInheritedInstancesPerKind);
        Assert.Null(pool.MaxExpressedInstancesPerKind);
        Assert.Contains(pool.Characteristics, x =>
            x.Kind == NativeOriginCharacteristicKind.Intrinsic &&
            x.CharacteristicId == "creatureType");
        Assert.Empty(result.Profile!.Underlying.InherentGrants);
    }

    [Fact]
    public void ExplicitTransmissionProjectsNativeCapacityAndOneComposedPhenotype()
    {
        var proposal = Biological(Parent("red", "ancestry.dragonborn.red"),
            Parent("bronze", "ancestry.dragonborn.bronze")) with
        { BiologicalTransmission = new([]) };
        var initial = Evaluate(proposal);
        var pool = initial.BiologicalCombination!;
        Assert.Equal(2, pool.MaxInheritedInstancesPerKind);
        Assert.Equal(1, pool.MaxExpressedInstancesPerKind);
        var breath = Assert.Single(pool.Characteristics, g => g.CharacteristicId == "trait.breath-weapon");
        var red = Assert.Single(breath.Offers, o => o.Candidate.ContributorId == "red").Candidate;
        var bronze = Assert.Single(breath.Offers, o => o.Candidate.ContributorId == "bronze").Candidate;
        var foundation = initial.EvaluatedProposal.Candidates.Where(c => c.ContributorId == "red" &&
            c.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        Assert.Equal(3, foundation.Length);
        var parameters = new Dictionary<string, JsonElement>
        {
            ["damageType"] = red.OfferedParameters!["damageType"],
            ["area"] = bronze.OfferedParameters!["area"]
        };
        var sources = new Dictionary<string, string> { ["damageType"] = red.Id, ["area"] = bronze.Id };
        var selected = foundation.Append(red).Append(bronze).ToArray();
        var result = Evaluate(proposal with
        {
            BiologicalTransmission = new(selected.Select(c => c.Id).ToArray()),
            Manifestations = selected.Select((c, i) => new NativeOriginManifestation("selected-" + i, c.Id,
                SelectedValue: c.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic ? c.OfferedValue : null)).ToArray(),
            Relationships = [new("compose-breath", NativeOriginRelationshipKind.Substitution,
                [red.Id, bronze.Id], AuthoredRuleReference: "trait.breath-weapon")],
            Resolutions = [new("compose-breath", NativeOriginResolutionAction.Replace,
                new(NativeOriginAdjudication.Supported, NativeRuleReference: "trait.breath-weapon"),
                red.Id, parameters, sources)]
        });
        Assert.True(result.IsMechanicallyAuthorized,
            string.Join("; ", result.Issues.Select(i => i.Code + ": " + i.Detail)));
        var group = Assert.Single(result.BiologicalCombination!.Characteristics, g => g.CharacteristicId == "trait.breath-weapon");
        var grant = Assert.Single(group.Expressions);
        Assert.Equal("fire", grant.Parameters!["damageType"].GetString());
        Assert.Equal("cone", grant.Parameters["area"].GetProperty("shape").GetString());
        Assert.Equal(2, grant.SourceCandidateIds.Count);
        var genes = result.BiologicalCombination.InheritedInstances!.Where(g =>
            g.Gene.Identity.MechanicalDefinitionId == "trait.breath-weapon").ToArray();
        Assert.Equal(2, genes.Length);
        Assert.Equal("fire", Assert.Single(genes, g => g.Gene.SourceCandidateId == red.Id).Gene.Parameters!["damageType"].GetString());
        Assert.Equal("lightning", Assert.Single(genes, g => g.Gene.SourceCandidateId == bronze.Id).Gene.Parameters!["damageType"].GetString());
    }

    [Fact]
    public void ExpressionReferencesOnlyTheSelectedAndEffectiveGene()
    {
        var proposal = Biological(
            Parent("human", "species.human"),
            Parent("elf", "species.elf"));
        var initial = Evaluate(proposal);
        var pool = initial.BiologicalCombination!;
        var foundation = initial.EvaluatedProposal.Candidates.Where(x =>
            x.ContributorId == "elf" && x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        var fey = Assert.Single(initial.EvaluatedProposal.Candidates, x =>
            x.ContributorId == "elf" &&
            x.Identity.MechanicalDefinitionId == "trait.fey-ancestry");
        var selected = foundation.Append(fey)
            .Select((candidate, i) => new NativeOriginManifestation(
                "selected-" + i, candidate.Id,
                SelectedValue: candidate.Identity.Kind ==
                    NativeOriginCharacteristicKind.Intrinsic
                    ? candidate.OfferedValue : null))
            .ToArray();
        var result = Evaluate(proposal with { Manifestations = selected });
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var feyGroup = Assert.Single(result.BiologicalCombination!.Characteristics,
            x => x.CharacteristicId == "trait.fey-ancestry");
        var offer = Assert.Single(feyGroup.Offers);
        Assert.Single(offer.AttemptedManifestationIds);
        var grant = Assert.Single(feyGroup.Expressions);
        Assert.Equal(new[] { fey.Id }, grant.SourceCandidateIds);
        Assert.Equal(new[] { grant.Id }, offer.EffectiveGrantIds);

        var other = Assert.Single(result.BiologicalCombination.Characteristics,
            x => x.CharacteristicId == "trait.skillful");
        Assert.Empty(other.Expressions);
        Assert.All(other.Offers, x => Assert.Empty(x.AttemptedManifestationIds));

        var saved = JsonSerializer.Deserialize<NativeOriginBiologicalCombination>(
            JsonSerializer.Serialize(result.BiologicalCombination));
        Assert.Equal(grant.Id,
            Assert.Single(saved!.Characteristics, x =>
                x.CharacteristicId == "trait.fey-ancestry").Expressions.Single().Id);
    }

    [Fact]
    public void TwoIdenticalDonorsRemainTwoOffersNotTwoInheritedGrants()
    {
        var result = Evaluate(Biological(
            Parent("first", "species.orc"),
            Parent("second", "species.orc")));
        var powerful = Assert.Single(result.BiologicalCombination!.Characteristics,
            x => x.CharacteristicId == "trait.powerful-build");
        Assert.Equal(2, powerful.Offers.Count);
        Assert.NotEqual(powerful.Offers[0].Candidate.Id, powerful.Offers[1].Candidate.Id);
        Assert.Empty(powerful.Expressions);
    }

    [Fact]
    public void OtherOriginationMethodsHaveNoBiologicalCombineProjection()
    {
        var proposal = Biological(Parent("source", "species.elf")) with
        {
            Method = new("rule.creature-origination", "construct-assembly")
        };
        Assert.Null(Evaluate(proposal).BiologicalCombination);
    }

    [Fact]
    public void UnauthenticatedHistoricalAncestryIsNeverAnEligibleGeneOffer()
    {
        var descendant = new NativeOriginContributor("unverified",
            new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.ManifestedProfile, "parent", "revision"),
            HistoricalSourceIds: ["species.orc", "species.elf"]);
        var result = Evaluate(Biological(descendant));
        Assert.Empty(result.BiologicalCombination!.Characteristics);
    }
}
