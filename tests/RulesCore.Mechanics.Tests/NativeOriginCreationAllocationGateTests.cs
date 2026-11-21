using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Creation allocation policies are illustrative test fixtures, not approved
/// numbers or prices. Only the trusted construction request can enable the gate.
/// </summary>
public sealed class NativeOriginCreationAllocationGateTests
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

    private static NativeOriginConstructionResult Evaluate(
        NativeOriginProposal proposal, NativeOriginAllocationPolicy? policy = null,
        bool require = false, INativeOriginAuthority? authority = null) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, 
                TrustedScope: authority is null ? null :
                    new("campaign", "allocation-gate-test"),
                AutoManifestDirectDefaults: true,
                RequireCreationAllocation: require,
                CreationAllocationPolicy: policy), authority);

    private static NativeOriginProposal Direct() =>
        new("direct-elf-budget", NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedSpeciesId: "species.elf");

    private static NativeOriginProposal Mixed(bool twoOrcs = false)
    {
        NativeOriginContributor[] donors = twoOrcs
            ? new[]
            {
                new NativeOriginContributor("first",
                    new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.orc")),
                new NativeOriginContributor("second",
                    new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.orc"))
            }
            : [
                new NativeOriginContributor("elf",
                    new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.elf"))
            ];
        var proposal = new NativeOriginProposal(
            twoOrcs ? "two-orc-budget" : "mixed-elf-budget",
            NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            donors, [], []);
        var offers = Evaluate(proposal).EvaluatedProposal.Candidates;
        var source = twoOrcs ? "first" : "elf";
        var foundation = offers.Where(candidate => candidate.ContributorId == source &&
            candidate.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        Assert.Equal(3, foundation.Length);
        var geneId = twoOrcs ? "trait.powerful-build" : "trait.low-light-vision";
        var genes = offers.Where(candidate =>
            candidate.Identity.MechanicalDefinitionId == geneId)
            .OrderBy(x => x.ContributorId, StringComparer.Ordinal).ToArray();
        Assert.Equal(twoOrcs ? 2 : 1, genes.Length);
        var manifestations = foundation.Append(genes[0])
            .Select((candidate, i) => new NativeOriginManifestation(
                "manifestation-" + i, candidate.Id,
                SelectedValue: candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic
                    ? candidate.OfferedValue : null)).ToArray();
        var transmission = twoOrcs ? new NativeOriginBiologicalTransmissionPlan(
            foundation.Select(x => x.Id).Concat(genes.Select(x => x.Id)).ToArray()) : null;
        return proposal with
        {
            Manifestations = manifestations,
            BiologicalTransmission = transmission
        };
    }

    private static string Key(NativeOriginEffectiveGrant grant) =>
        grant.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic
            ? "intrinsic:" + grant.Identity.IntrinsicSlotId
            : "trait:" + grant.Identity.MechanicalDefinitionId;

    private static NativeOriginAllocationPolicy Policy(
        int allowance, params NativeOriginConstructionResult[] baselines)
    {
        var keys = baselines.SelectMany(x => x.Profile!.Underlying.InherentGrants)
            .Select(Key).Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        return new NativeOriginAllocationPolicy("test-only-not-balanced",
            "rule.creature-origination", "biological", allowance,
            keys.Select(key => new NativeOriginAllocationPrice(
                "priced:" + key, 1, [key])).ToArray());
    }

    [Fact]
    public void RequiredPolicyMissingFailsClosedWithoutChangingLegacyOptOut()
    {
        var proposal = Mixed();
        var baseline = Evaluate(proposal);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, baseline.Status);
        Assert.Null(baseline.CreationAllocation);

        var required = Evaluate(proposal, require: true);
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, required.Status);
        Assert.Equal("not-configured", required.CreationAllocation!.Status);
        Assert.False(required.CreationAllocation.PreviewOnly);
        Assert.Contains(required.Issues, x =>
            x.Code == "creation-allocation-unconfigured");
        Assert.Equal(NativeOriginEventStage.Pending,
            Assert.Single(required.Profile!.History).Stage);
    }

    [Fact]
    public void OnePricePolicyAppliesToBothDirectPresetsAndMixedBiologicalCreation()
    {
        var direct = Evaluate(Direct());
        var mixed = Evaluate(Mixed());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, direct.Status);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, mixed.Status);
        var policy = Policy(100, direct, mixed);

        var directChecked = Evaluate(Direct(), policy);
        var mixedChecked = Evaluate(Mixed(), policy);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, directChecked.Status);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, mixedChecked.Status);
        Assert.Equal(direct.Profile!.Underlying.InherentGrants.Count,
            directChecked.CreationAllocation!.CommittedLoad);
        Assert.Equal(mixed.Profile!.Underlying.InherentGrants.Count,
            mixedChecked.CreationAllocation!.CommittedLoad);
        Assert.Equal("within-allowance", directChecked.CreationAllocation.Status);
        Assert.Equal("within-allowance", mixedChecked.CreationAllocation.Status);
        Assert.False(directChecked.CreationAllocation.PreviewOnly);
        Assert.Empty(mixedChecked.CreationAllocation.Issues);
    }

    [Fact]
    public void ExceedingCreationBudgetBlocksAuthorizationAndLeavesPendingHistory()
    {
        var proposal = Mixed();
        var baseline = Evaluate(proposal);
        var count = baseline.Profile!.Underlying.InherentGrants.Count;
        Assert.True(count > 1);
        var result = Evaluate(proposal, Policy(count - 1, baseline));
        Assert.Equal(NativeOriginConstructionStatus.Conflicting, result.Status);
        Assert.False(result.IsMechanicallyAuthorized);
        Assert.Equal(NativeOriginAdjudication.Conflict,
            result.Profile!.Adjudication.Adjudication);
        Assert.Equal("over-allowance", result.CreationAllocation!.Status);
        Assert.Equal(count, result.CreationAllocation.CommittedLoad);
        Assert.Contains(result.Issues, x => x.Code == "creation-allocation-exceeded");
        Assert.Equal(NativeOriginEventStage.Pending,
            Assert.Single(result.Profile.History).Stage);
    }

    [Fact]
    public void UnpricedTraitFailsClosedEvenWhenAllowanceIsGenerous()
    {
        var proposal = Mixed();
        var baseline = Evaluate(proposal);
        var policy = Policy(100, baseline);
        policy = policy with
        {
            Prices = policy.Prices.Where(x =>
                !x.MemberKeys.Contains("trait:trait.low-light-vision",
                    StringComparer.Ordinal)).ToArray()
        };
        var result = Evaluate(proposal, policy);
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, result.Status);
        Assert.Equal("unresolved", result.CreationAllocation!.Status);
        Assert.Contains(result.CreationAllocation.Issues, x =>
            x.StartsWith("allocation-load-unpriced:trait:trait.low-light-vision:",
                StringComparison.Ordinal));
        Assert.Contains(result.Issues, x =>
            x.Code == "creation-allocation-unresolved");
    }

    [Fact]
    public void LatentGenesDoNotConsumeCreationAllocation()
    {
        var proposal = Mixed(twoOrcs: true);
        var baseline = Evaluate(proposal);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, baseline.Status);
        var genes = baseline.Profile!.Underlying.BiologicalGenes!;
        Assert.Equal(2, genes.Count(x =>
            x.Identity.MechanicalDefinitionId == "trait.powerful-build"));
        var expressed = baseline.Profile.Underlying.InherentGrants;
        Assert.Single(expressed, x =>
            x.Identity.MechanicalDefinitionId == "trait.powerful-build");

        var policy = Policy(expressed.Count, baseline);
        var checkedResult = Evaluate(proposal, policy);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, checkedResult.Status);
        Assert.Equal(expressed.Count,
            checkedResult.CreationAllocation!.CommittedLoad);
        Assert.Equal("within-allowance", checkedResult.CreationAllocation.Status);
        Assert.Equal(2, checkedResult.Profile!.Underlying.BiologicalGenes!.Count(x =>
            x.Identity.MechanicalDefinitionId == "trait.powerful-build"));
    }

    private sealed class VerifiedDm : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) =>
            scope.Kind == "campaign" && scope.Id == "allocation-gate-test" &&
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "valid-allocation-proof";

        public bool AuthorizesCatalogChoice(string catalogId, string optionId,
            string evidenceId) => false;
    }

    private static NativeTableRuleDecision Ruling(string proposalId, object value) =>
        new("allocation-decision", new("dm", "valid-allocation-proof"),
            new("campaign", "allocation-gate-test"),
            new("creation-allocation", proposalId),
            JsonSerializer.SerializeToElement(value));

    [Fact]
    public void OnlyAuthenticatedTableRulingCanWaiveRequiredCreationBudget()
    {
        var proposal = Mixed();
        proposal = proposal with
        {
            TableDecisions = [Ruling(proposal.Id, new { waive = true })]
        };
        var withoutAuthority = Evaluate(proposal, require: true);
        Assert.Equal(NativeOriginConstructionStatus.Unresolved,
            withoutAuthority.Status);
        Assert.Contains(withoutAuthority.Issues, x =>
            x.Code == "creation-allocation-unconfigured");

        var authorized = Evaluate(proposal, require: true, authority: new VerifiedDm());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, authorized.Status);
        Assert.Equal("dm-waived", authorized.CreationAllocation!.Status);
        Assert.Equal(NativeOriginAdjudication.DmAuthorized,
            authorized.Profile!.Adjudication.Adjudication);
        Assert.Equal(NativeOriginEventStage.Completed,
            Assert.Single(authorized.Profile.History).Stage);
    }

    [Fact]
    public void AuthenticatedAllowanceAdjustmentDoesNotChangePriceSchedule()
    {
        var proposal = Mixed();
        var baseline = Evaluate(proposal);
        var count = baseline.Profile!.Underlying.InherentGrants.Count;
        var policy = Policy(count - 1, baseline);
        proposal = proposal with
        {
            TableDecisions = [Ruling(proposal.Id, new { allowance = count })]
        };
        var notTrusted = Evaluate(proposal, policy);
        Assert.Equal(NativeOriginConstructionStatus.Conflicting, notTrusted.Status);
        var trusted = Evaluate(proposal, policy, authority: new VerifiedDm());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, trusted.Status);
        Assert.Equal(count, trusted.CreationAllocation!.Allowance);
        Assert.Equal(count, trusted.CreationAllocation.CommittedLoad);
        Assert.Equal(NativeOriginAdjudication.DmAuthorized,
            trusted.Profile!.Adjudication.Adjudication);
    }

    [Fact]
    public void ExplicitInteractionPricesAddToBaseCostsOnlyWhenBothEffectsExpress()
    {
        var proposal = Mixed();
        var baseline = Evaluate(proposal);
        var grantCount = baseline.Profile!.Underlying.InherentGrants.Count;
        var plain = Policy(grantCount, baseline);
        var additional = plain with
        {
            InteractionPrices = [
                new NativeOriginAllocationInteractionPrice(
                    "elf-sight-and-size-combination", 2,
                    ["trait:trait.low-light-vision", "intrinsic:size"])
            ]
        };
        var blocked = Evaluate(proposal, additional);
        Assert.Equal(NativeOriginConstructionStatus.Conflicting, blocked.Status);
        Assert.Equal(grantCount + 2, blocked.CreationAllocation!.CommittedLoad);
        Assert.Contains(blocked.CreationAllocation.Charges, x =>
            x.PriceId == "elf-sight-and-size-combination" && x.Load == 2);

        var allowed = Evaluate(proposal, additional with
        {
            Allowance = grantCount + 2
        });
        Assert.Equal(NativeOriginConstructionStatus.Authorized, allowed.Status);
        Assert.Equal(grantCount + 2, allowed.CreationAllocation!.CommittedLoad);
    }

    [Fact]
    public void ExistingBaselineDoesNotConsumeANewCreationBudget()
    {
        var original = Evaluate(Mixed());
        var proposal = Mixed() with
        {
            ExistingConfiguration = original.Profile!.Underlying
        };
        var result = Evaluate(proposal, require: true);
        Assert.Null(result.CreationAllocation);
        Assert.DoesNotContain(result.Issues,
            x => x.Code.StartsWith("creation-allocation-", StringComparison.Ordinal));
    }

    [Fact]
    public void PreviewRemainsNonAuthoritativeForDirectSpeciesSelections()
    {
        var direct = Evaluate(Direct());
        var policy = Policy(0, direct);
        Assert.Equal("not-applicable", NativeOriginAllocationPreview
            .Assess(Catalog(), direct, policy).Status);
        Assert.True(NativeOriginAllocationPreview.Assess(Catalog(), direct, policy)
            .PreviewOnly);
        Assert.Equal(NativeOriginConstructionStatus.Conflicting,
            Evaluate(Direct(), policy).Status);
    }
}
