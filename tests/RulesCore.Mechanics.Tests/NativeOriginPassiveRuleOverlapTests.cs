using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Actual Elf and Tiefling Low-Light Vision definitions have distinct native
/// trait identities but reference one passive rule. A shared rule reference
/// is overlap evidence, never implicit permission to stack or suppress a trait.
/// </summary>
public sealed class NativeOriginPassiveRuleOverlapTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null)
        {
            var root = Path.Combine(folder.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(root, "first-party-001", "native",
                "rules", "creature-origination.json")))
                return NativeCatalogLoader.Load(
                    NativeRulesetLayout.Resolve(root, "first-party-001"));
            folder = folder.Parent;
        }
        throw new InvalidOperationException("Native ruleset unavailable.");
    }

    private static NativeOriginProposal Base() =>
        new("same-passive-rule", NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [
                new("elf", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.elf")),
                new("tiefling", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "ancestry.tiefling.infernal"))
            ], [], []);

    private static NativeOriginConstructionResult Evaluate(
        NativeOriginProposal proposal, INativeOriginAuthority? authority = null) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, authority is null ? null :
                new("campaign", "passive-test"),
                AutoManifestDirectDefaults: false), authority);

    private sealed class TestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) =>
            scope.Kind == "campaign" && scope.Id == "passive-test" &&
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "passive-rule-proof";
        public bool AuthorizesCatalogChoice(string catalogId,
            string optionId, string evidenceId) => false;
    }

    private static NativeTableRuleDecision Permission(
        string id, string kind, string target, object outcome) =>
        new(id, new("dm", "passive-rule-proof"),
            new("campaign", "passive-test"), new(kind, target),
            JsonSerializer.SerializeToElement(outcome));

    private static (NativeOriginProposal Proposal, NativeOriginCandidate Elf,
        NativeOriginCandidate Tiefling, NativeTableRuleDecision Eligibility) Prepared()
    {
        var initial = Base();
        var available = Evaluate(initial).EvaluatedProposal.Candidates;
        var elf = Assert.Single(available, x =>
            x.ContributorId == "elf" &&
            x.Identity.MechanicalDefinitionId == "trait.low-light-vision");
        var tiefling = Assert.Single(available, x =>
            x.ContributorId == "tiefling" &&
            x.Identity.MechanicalDefinitionId == "trait.low-light-vision");
        Assert.Equal(NativeOriginEligibility.Eligible,
            elf.SelectionBasis.Eligibility);
        // The selectable Infernal Ancestry supplies its inherited Species gene
        // as an offer. Its distinct definition still requires reconciliation.
        Assert.Equal(NativeOriginEligibility.Eligible,
            tiefling.SelectionBasis.Eligibility);
        Assert.NotEqual(elf.Identity.MechanicalDefinitionId,
            tiefling.Identity.MechanicalDefinitionId);
        Assert.Equal("rule.low-light-vision",
            elf.OfferedValue!.Value.GetProperty("rule").GetString());
        Assert.Equal("rule.low-light-vision",
            tiefling.OfferedValue!.Value.GetProperty("rule").GetString());

        var physical = available.Where(x => x.ContributorId == "elf" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        Assert.Equal(3, physical.Length);
        var eligibility = Permission("allow-tiefling", "candidate", tiefling.Id,
            new { eligibility = "eligible" });
        var proposal = initial with
        {
            Manifestations = physical.Select((x, i) =>
                new NativeOriginManifestation("physical-" + i, x.Id,
                    SelectedValue: x.OfferedValue)).Concat([
                new NativeOriginManifestation("elf-vision", elf.Id),
                new NativeOriginManifestation("tiefling-vision", tiefling.Id)
            ]).ToArray(),
            TableDecisions = [eligibility]
        };
        return (proposal, elf, tiefling, eligibility);
    }

    [Fact]
    public void DistinctTraitsReferencingSamePassiveRuleRequireExplicitDisposition()
    {
        var (proposal, _, _, _) = Prepared();
        var result = Evaluate(proposal, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, result.Status);
        Assert.Contains(result.Issues, x =>
            x.Code == "same-passive-native-rule-reconciliation-required" &&
            x.Path == "rule.low-light-vision");
        Assert.DoesNotContain(result.Profile?.Underlying.InherentGrants ?? [],
            x => x.Identity.MechanicalDefinitionId is
                "trait.low-light-vision" or
                "trait.low-light-vision");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ScopedDispositionRetainsOneCompleteSourceDefinition(bool keepElf)
    {
        var (proposal, elf, tiefling, _) = Prepared();
        var action = keepElf ? NativeOriginResolutionAction.RetainFirst :
            NativeOriginResolutionAction.RetainSecond;
        var decision = Permission("retain-one", "relationship", "same-vision",
            new { action = keepElf ? "retain-first" : "retain-second" });
        var selected = proposal with
        {
            TableDecisions = proposal.TableDecisions!.Append(decision).ToArray(),
            Relationships = [new("same-vision",
                NativeOriginRelationshipKind.Equivalent, [elf.Id, tiefling.Id])],
            Resolutions = [new("same-vision", action,
                new(NativeOriginAdjudication.DmAuthorized,
                    TableDecisionId: decision.Id))]
        };
        var result = Evaluate(selected, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var expected = keepElf ? elf : tiefling;
        var other = keepElf ? tiefling : elf;
        var retained = Assert.Single(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == expected.Identity.MechanicalDefinitionId);
        Assert.DoesNotContain(result.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == other.Identity.MechanicalDefinitionId);
        Assert.Equal(expected.Id, Assert.Single(retained.SourceCandidateIds));
        Assert.Equal(expected.OfferedValue!.Value.GetRawText(),
            retained.EffectiveValue!.Value.GetRawText());
        Assert.Equal("resolved", NativeOriginVisionProjector
            .ProjectApprovedMixed(Catalog(), result).Status);
        Assert.Empty(result.Issues);
        var copied = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(result.Profile))!;
        Assert.Equal("resolved", NativeOriginVisionProjector.ProjectApprovedMixed(
            Catalog(), result with { Profile = copied }).Status);
    }

    [Fact]
    public void ChoosingOnlyOnePassiveSourceDoesNotRequireACrossSourceRuling()
    {
        var (proposal, elf, tiefling, _) = Prepared();
        var onlyElf = proposal with
        {
            Manifestations = proposal.Manifestations.Where(x =>
                x.CandidateId != tiefling.Id).ToArray(),
            TableDecisions = []
        };
        var result = Evaluate(onlyElf);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Single(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == elf.Identity.MechanicalDefinitionId);
        Assert.Equal("resolved", NativeOriginVisionProjector
            .ProjectApprovedMixed(Catalog(), result).Status);
    }

    [Fact]
    public void CallerDeclaredNativeDispositionDoesNotAuthorizeSuppression()
    {
        var (proposal, elf, tiefling, _) = Prepared();
        var fabricated = proposal with
        {
            Relationships = [new("claimed-rule", NativeOriginRelationshipKind.Equivalent,
                [elf.Id, tiefling.Id], AuthoredRuleReference: "rule.low-light-vision")],
            Resolutions = [new("claimed-rule", NativeOriginResolutionAction.RetainFirst,
                new(NativeOriginAdjudication.Supported,
                    NativeRuleReference: "rule.low-light-vision"))]
        };
        var result = Evaluate(fabricated, new TestAuthority());
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Contains(result.Issues, x =>
            x.Code == "relationship-disposition-unauthorized");
    }

    [Fact]
    public void ScopedCoexistenceDoesNotImplyThatTwoEqualPassiveEffectsCanBeProjected()
    {
        var (proposal, elf, tiefling, _) = Prepared();
        var decision = Permission("retain-both", "relationship", "same-vision",
            new { action = "retain-both", candidateIds = new[] { elf.Id, tiefling.Id } });
        var selected = proposal with
        {
            TableDecisions = proposal.TableDecisions!.Append(decision).ToArray(),
            Relationships = [new("same-vision",
                NativeOriginRelationshipKind.Equivalent, [elf.Id, tiefling.Id])],
            Resolutions = [new("same-vision", NativeOriginResolutionAction.RetainBoth,
                new(NativeOriginAdjudication.DmAuthorized,
                    TableDecisionId: decision.Id))]
        };
        var result = Evaluate(selected, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Equal(2, result.Profile!.Underlying.InherentGrants.Count(x =>
            x.Identity.MechanicalDefinitionId is
                "trait.low-light-vision" or "trait.low-light-vision"));
        var view = NativeOriginVisionProjector.ProjectApprovedMixed(Catalog(), result);
        Assert.Equal("unresolved", view.Status);
        Assert.Contains("duplicate-low-light-vision", view.UnresolvedReasons);
    }
}
