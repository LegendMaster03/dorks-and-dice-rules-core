using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Candidate costs are deliberately illustrative. These tests prove accounting
/// mechanics, not a balance decision, a live biological budget, or trait prices.
/// </summary>
public sealed class NativeOriginAllocationPreviewTests
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
        throw new InvalidOperationException("Native catalog missing.");
    }

    private static NativeOriginConstructionResult Evaluate(
        NativeOriginProposal proposal, INativeOriginAuthority? authority = null) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, authority is null ? null : new("campaign", "allocation-test"),
                AutoManifestDirectDefaults: false), authority);

    private static NativeOriginContributor Parent(string id, string source) =>
        new(id, new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.Native, source));

    private static NativeOriginAllocationPolicy Hypothetical(int allowance,
        params NativeOriginAllocationPrice[] additional)
    {
        var foundation = new[]
        {
            new NativeOriginAllocationPrice("creature", 1, ["intrinsic:creatureType"]),
            new NativeOriginAllocationPrice("size", 1, ["intrinsic:size"]),
            new NativeOriginAllocationPrice("walk", 1, ["intrinsic:walkingSpeedFeet"])
        };
        return new("candidate-alpha", "rule.creature-origination", "biological",
            allowance, foundation.Concat(additional).ToArray());
    }

    private static NativeOriginConstructionResult ElfProfile(
        int parents = 1, bool selectAllVisionOffers = false, bool includeTrance = false)
    {
        var proposal = new NativeOriginProposal("elf-preview-" + parents,
            NativeOriginIntent.Mixed, new("rule.creature-origination", "biological"),
            Enumerable.Range(1, parents)
                .Select(i => Parent("elf-" + i, "species.elf")).ToArray(), [], []);
        var offered = Evaluate(proposal).EvaluatedProposal.Candidates;
        var physical = offered.Where(x => x.ContributorId == "elf-1" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        var vision = offered.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.shared.low-light-vision").ToArray();
        var trance = Assert.Single(offered, x => x.ContributorId == "elf-1" &&
            x.Identity.MechanicalDefinitionId == "trait.elf.trance");
        var choices = physical.Select((x, i) =>
            new NativeOriginManifestation("physical-" + i, x.Id,
                SelectedValue: x.OfferedValue)).ToList();
        var chosenVision = selectAllVisionOffers ? vision : vision.Take(1).ToArray();
        choices.AddRange(chosenVision.Select((x, i) =>
            new NativeOriginManifestation("vision-" + i, x.Id)));
        if (includeTrance)
            choices.Add(new("trance", trance.Id));
        var result = Evaluate(proposal with { Manifestations = choices });
        Assert.True(result.Status == NativeOriginConstructionStatus.Authorized,
            "Issues: " + string.Join("; ", result.Issues.Select(x => x.Code)));
        return result;
    }

    [Fact]
    public void NoApprovedAllocationPolicyDoesNotSilentlyTreatAnyTraitAsFree()
    {
        var result = ElfProfile(includeTrance: true);
        var unconfigured = NativeOriginAllocationPreview.Assess(Catalog(), result);
        Assert.Equal("not-configured", unconfigured.Status);
        Assert.Null(unconfigured.CommittedLoad);
        Assert.Equal(5, unconfigured.Components.Count);
        Assert.All(unconfigured.Components, x => Assert.Null(x.AssignedPriceId));
        Assert.Contains("biological-allocation-policy-not-approved",
            unconfigured.Issues);
        Assert.True(unconfigured.PreviewOnly);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
    }

    [Fact]
    public void HypotheticalCostsPriceEveryEffectiveGrantAndDoNotAuthorizeAnything()
    {
        var result = ElfProfile(includeTrance: true);
        var sight = new NativeOriginAllocationPrice("vision", 3,
            ["trait:trait.shared.low-light-vision"]);
        var trance = new NativeOriginAllocationPrice("trance", 2,
            ["trait:trait.elf.trance"]);
        var within = NativeOriginAllocationPreview.Assess(Catalog(), result,
            Hypothetical(8, sight, trance));
        Assert.Equal("within-allowance", within.Status);
        Assert.Equal(8, within.CommittedLoad);
        Assert.Equal(5, within.Charges.Count);
        Assert.All(within.Components, x => Assert.NotNull(x.AssignedPriceId));
        Assert.Empty(within.Issues);
        Assert.Equal("candidate-alpha", within.PolicyRevision);

        var over = NativeOriginAllocationPreview.Assess(Catalog(), result,
            Hypothetical(7, sight, trance));
        Assert.Equal("over-allowance", over.Status);
        Assert.Equal(8, over.CommittedLoad);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);

        var unpriced = NativeOriginAllocationPreview.Assess(Catalog(), result,
            Hypothetical(80, sight));
        Assert.Equal("unresolved", unpriced.Status);
        Assert.Null(unpriced.CommittedLoad);
        Assert.Contains(unpriced.Issues, x =>
            x.Contains("allocation-load-unpriced:trait:trait.elf.trance:",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ThreeDonorsOfOneCanonicalVisionRemainOneBurdenAndOnePool()
    {
        var single = ElfProfile();
        var repeated = ElfProfile(parents: 3, selectAllVisionOffers: true);
        Assert.Equal(4, repeated.Profile!.Underlying.InherentGrants.Count);
        Assert.Equal(3, Assert.Single(repeated.Profile.Underlying.InherentGrants,
            x => x.Identity.MechanicalDefinitionId == "trait.shared.low-light-vision")
            .SourceCandidateIds.Count);
        var policy = Hypothetical(6, new("vision", 3,
            ["trait:trait.shared.low-light-vision"]));
        var one = NativeOriginAllocationPreview.Assess(Catalog(), single, policy);
        var many = NativeOriginAllocationPreview.Assess(Catalog(), repeated, policy);
        Assert.Equal(6, one.CommittedLoad);
        Assert.Equal(one.CommittedLoad, many.CommittedLoad);
        Assert.Equal(one.Charges.Count, many.Charges.Count);
        Assert.Equal(1, many.Charges.Count(x => x.PriceId == "vision"));
    }

    private static NativeTableRuleDecision Permit(string id, string kind, string target,
        object outcome) =>
        new(id, new("dm", "verified-allocation"),
            new("campaign", "allocation-test"), new(kind, target),
            JsonSerializer.SerializeToElement(outcome));

    private sealed class TestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) =>
            scope.Kind == "campaign" && scope.Id == "allocation-test" &&
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "verified-allocation";
        public bool AuthorizesCatalogChoice(string catalogId, string optionId,
            string evidenceId) => false;
    }

    [Fact]
    public void InseparableNativeDrowAdaptationHasOneBundleCharge()
    {
        var proposal = new NativeOriginProposal("drow-allocation",
            NativeOriginIntent.Mixed, new("rule.creature-origination", "biological"),
            [Parent("drow", "ancestry.elf.drow")], [], []);
        var offered = Evaluate(proposal).EvaluatedProposal.Candidates;
        var vision = Assert.Single(offered, x =>
            x.Identity.IntrinsicSlotId == "darkvisionFeet");
        var sensitivity = Assert.Single(offered, x =>
            x.Identity.MechanicalDefinitionId == "trait.elf.drow.sunlight-sensitivity");
        var physical = offered.Where(x => x.ContributorId == "drow" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        var completed = Evaluate(proposal with
        {
            Manifestations = physical.Select((x, i) =>
                new NativeOriginManifestation("physical-" + i, x.Id,
                    SelectedValue: x.OfferedValue)).Concat(
                [
                    new("vision", vision.Id, SelectedValue: vision.OfferedValue),
                    new("sunlight", sensitivity.Id)
                ]).ToArray(),
            TableDecisions =
            [
                Permit("allow-vision", "candidate", vision.Id,
                    new { eligibility = "eligible" }),
                Permit("allow-sunlight", "candidate", sensitivity.Id,
                    new { eligibility = "eligible" })
            ]
        }, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, completed.Status);
        var policy = Hypothetical(7, new("drow-adaptation", 4,
        [
            "intrinsic:darkvisionFeet",
            "trait:trait.elf.drow.sunlight-sensitivity"
        ]));
        var assessment = NativeOriginAllocationPreview.Assess(Catalog(), completed, policy);
        Assert.Equal("within-allowance", assessment.Status);
        Assert.Equal(7, assessment.CommittedLoad);
        var bundle = Assert.Single(assessment.Charges,
            x => x.PriceId == "drow-adaptation");
        Assert.Equal(2, bundle.GrantIds.Count);
        Assert.Equal(4, assessment.Charges.Count);
        Assert.Equal(5, assessment.Components.Count);
        Assert.All(assessment.Components, x => Assert.NotNull(x.AssignedPriceId));
        Assert.Empty(assessment.Issues);
    }

    [Fact]
    public void FutureLevelUnlockIsCommittedAtCharacterCreationNotOnlyAtLevelFour()
    {
        var proposal = new NativeOriginProposal("stone-committed-progression",
            NativeOriginIntent.Mixed, new("rule.creature-origination", "biological"),
            [Parent("stone", "ancestry.goliath.stone")], [], []);
        var offered = Evaluate(proposal).EvaluatedProposal.Candidates;
        var cavernous = Assert.Single(offered, x =>
            x.Identity.MechanicalDefinitionId == "trait.goliath.stone.cavernous-sight");
        var foundation = offered.Where(x => x.Identity.IntrinsicSlotId is
            "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        var completed = Evaluate(proposal with
        {
            Manifestations = foundation.Select((x, i) =>
                new NativeOriginManifestation("foundation-" + i, x.Id,
                    SelectedValue: x.OfferedValue))
                .Append(new("future-vision", cavernous.Id)).ToArray(),
            TableDecisions =
            [
                Permit("allow-cavernous", "candidate", cavernous.Id,
                    new { eligibility = "eligible" })
            ]
        }, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, completed.Status);
        var policy = Hypothetical(8, new("cavernous", 6,
            ["trait:trait.goliath.stone.cavernous-sight"]));
        var preview = NativeOriginAllocationPreview.Assess(Catalog(), completed, policy);
        Assert.Equal("over-allowance", preview.Status);
        Assert.Equal(9, preview.CommittedLoad);
        Assert.Equal(4, Assert.Single(preview.Components,
            x => x.Key == "trait:trait.goliath.stone.cavernous-sight")
            .UnlockLevel);
        Assert.Contains(preview.Charges, x =>
            x.PriceId == "cavernous" && x.Load == 6);
    }

    [Fact]
    public void MalformedOrIncompleteHypotheticalPriceCoverageFailsClosed()
    {
        var result = ElfProfile(includeTrance: true);
        var vision = new NativeOriginAllocationPrice("vision", 3,
            ["trait:trait.shared.low-light-vision"]);
        var duplicate = new NativeOriginAllocationPrice("duplicate", 1,
            ["trait:trait.shared.low-light-vision"]);
        Assert.Equal("invalid-policy", NativeOriginAllocationPreview
            .Assess(Catalog(), result, Hypothetical(12, vision, duplicate)).Status);
        Assert.Equal("invalid-policy", NativeOriginAllocationPreview
            .Assess(Catalog(), result, Hypothetical(12,
                new NativeOriginAllocationPrice("free", 0,
                    ["trait:trait.elf.trance"]))).Status);
        Assert.Equal("invalid-policy", NativeOriginAllocationPreview
            .Assess(Catalog(), result, Hypothetical(-1, vision)).Status);
        Assert.Equal("unresolved", NativeOriginAllocationPreview
            .Assess(Catalog(), result, Hypothetical(100, vision)).Status);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
    }

    [Fact]
    public void AllocationAssessmentNeverAppliesBiologicalBudgetToOtherMethods()
    {
        var selected = new NativeOriginProposal("direct-elf",
            NativeOriginIntent.Direct, new("rule.creature-origination", "biological"),
            [], [], [], SelectedSpeciesId: "species.elf");
        var direct = new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(selected));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, direct.Status);
        var preview = NativeOriginAllocationPreview.Assess(Catalog(), direct,
            Hypothetical(1));
        Assert.Equal("not-applicable", preview.Status);
        Assert.Null(preview.CommittedLoad);
        Assert.True(preview.PreviewOnly);
    }
}
