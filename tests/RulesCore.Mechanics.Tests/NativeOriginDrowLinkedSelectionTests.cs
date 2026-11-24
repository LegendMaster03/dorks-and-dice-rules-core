using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// The canonical Drow sensory adaptation is a contributor-scoped selected
/// package, not a rule that all Darkvision requires Sunlight Sensitivity.
/// A verified table decision can waive the package prerequisite.
/// </summary>
public sealed class NativeOriginDrowLinkedSelectionTests
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
        throw new InvalidOperationException("Native catalog is missing.");
    }

    private static NativeOriginContributor Parent(string id) =>
        new(id, new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.Native, "ancestry.elf.drow"));

    private static NativeOriginProposal Proposal(bool twoContributors = false) =>
        new("drow-link", NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            twoContributors ? [Parent("a"), Parent("b")] : [Parent("a")], [], []);

    private static NativeOriginConstructionResult Evaluate(
        NativeOriginProposal proposal, INativeOriginAuthority? authority = null,
        bool auto = false) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, authority is null ? null : new("campaign", "drow-test"),
                AutoManifestDirectDefaults: auto), authority);

    private static NativeTableRuleDecision Decision(string id, string kind,
        string target, object outcome) =>
        new(id, new("dm", "verified-drow"),
            new("campaign", "drow-test"), new(kind, target),
            JsonSerializer.SerializeToElement(outcome));

    private sealed class TestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision d, NativeTableDecisionScope s) =>
            s.Kind == "campaign" && s.Id == "drow-test" &&
            d.Authority.ActorId == "dm" &&
            d.Authority.AuthorizationEvidenceId == "verified-drow";
        public bool AuthorizesCatalogChoice(string catalogId, string optionId,
            string evidenceId) => false;
    }

    private static (NativeOriginProposal Base, NativeOriginCandidate Darkvision,
        NativeOriginCandidate Sensitivity, NativeOriginConstraint Link,
        IReadOnlyList<NativeTableRuleDecision> Permissions)
        SelectedDrow(bool multiple = false)
    {
        var proposal = Proposal(multiple);
        var offers = Evaluate(proposal).EvaluatedProposal;
        var vision = Assert.Single(offers.Candidates, x =>
            x.ContributorId == "a" && x.Identity.IntrinsicSlotId == "darkvisionFeet");
        var drawback = Assert.Single(offers.Candidates, x =>
            x.ContributorId == "a" &&
            x.Identity.MechanicalDefinitionId == "trait.sunlight-sensitivity");
        Assert.Equal(120, vision.OfferedValue!.Value.GetInt32());
        var constraint = Assert.Single(offers.Constraints!, x =>
            x.Subjects.Any(y => y.CandidateId == vision.Id));
        Assert.Equal(NativeOriginRelationshipKind.Requires, constraint.Kind);
        Assert.Equal("ancestry.elf.drow", constraint.NativeRuleReference);
        Assert.Equal(drawback.Id, Assert.Single(constraint.Conditions).CandidateId);

        var foundation = offers.Candidates.Where(x => x.ContributorId == "a" &&
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet")
            .ToArray();
        Assert.Equal(3, foundation.Length);
        var manifestations = foundation.Select((x, i) =>
            new NativeOriginManifestation("physical-" + i, x.Id,
                SelectedValue: x.OfferedValue))
            .Append(new("selected-darkvision", vision.Id,
                SelectedValue: vision.OfferedValue)).ToArray();
        var decisions = new[]
        {
            Decision("permit-darkvision", "candidate", vision.Id,
                new { eligibility = "eligible" }),
            Decision("permit-sensitivity", "candidate", drawback.Id,
                new { eligibility = "eligible" })
        };
        return (proposal with
        {
            Manifestations = manifestations, TableDecisions = decisions
        }, vision, drawback, constraint, decisions);
    }

    [Fact]
    public void SelectingDrowDarkvisionWithoutItsLinkedDrawbackIsIncomplete()
    {
        var (proposal, vision, drawback, link, _) = SelectedDrow();
        var result = Evaluate(proposal, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, result.Status);
        Assert.Contains(result.Issues, x =>
            x.Code == "required-mechanic-missing" && x.Path == "constraints." + link.Id);
        Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.IntrinsicSlotId == "darkvisionFeet");
        Assert.DoesNotContain(result.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == drawback.Identity.MechanicalDefinitionId);
        Assert.Equal("unresolved",
            NativeOriginDarkvisionProjector.ProjectApprovedMixed(Catalog(), result).Status);
        Assert.Contains(result.EvaluatedProposal.Candidates, x => x.Id == vision.Id);
    }

    [Fact]
    public void SameContributorSensitivitySatisfiesRequiredDrowLink()
    {
        var (proposal, vision, drawback, _, _) = SelectedDrow();
        var result = Evaluate(proposal with
        {
            Manifestations = proposal.Manifestations.Concat(
                [new NativeOriginManifestation("selected-sensitivity", drawback.Id)]).ToArray()
        }, new TestAuthority());
        Assert.True(result.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", result.Issues.Select(x => x.Code + ":" + x.Detail)));
        var grants = result.Profile!.Underlying.InherentGrants;
        var sense = Assert.Single(grants, x =>
            x.Identity.IntrinsicSlotId == "darkvisionFeet");
        Assert.Equal(120, sense.EffectiveValue!.Value.GetInt32());
        Assert.Equal(vision.Id, Assert.Single(sense.SourceCandidateIds));
        var projected = NativeOriginDarkvisionProjector.ProjectApprovedMixed(Catalog(), result);
        Assert.Equal("resolved", projected.Status);
        Assert.Equal(120, projected.Darkvision!.RangeFeet);
        Assert.Equal("ancestry.elf.drow", projected.Darkvision.SourceId);
        var sensitivity = Assert.Single(grants, x =>
            x.Identity.MechanicalDefinitionId == "trait.sunlight-sensitivity");
        Assert.Equal("direct-unprotected-sunlight",
            sensitivity.EffectiveValue!.Value.GetProperty("trigger").GetString());
        Assert.Equal("remove-exposure-or-use-effective-protection",
            sensitivity.EffectiveValue.Value.GetProperty("end").GetString());
        Assert.Equal("condition.dazzled",
            sensitivity.EffectiveValue.Value.GetProperty("condition").GetString());
        Assert.Empty(result.Issues);

        var restored = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(result.Profile));
        Assert.Equal(120, Assert.Single(restored!.Underlying.InherentGrants, x =>
            x.Identity.IntrinsicSlotId == "darkvisionFeet").EffectiveValue!.Value.GetInt32());
        Assert.Contains(restored.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == drawback.Identity.MechanicalDefinitionId);
    }

    [Fact]
    public void OtherContributorsSensitivityCannotSatisfyChosenDrowDarkvision()
    {
        var (proposal, _, _, link, _) = SelectedDrow(multiple: true);
        var preview = Evaluate(proposal).EvaluatedProposal;
        var other = Assert.Single(preview.Candidates, x =>
            x.ContributorId == "b" &&
            x.Identity.MechanicalDefinitionId == "trait.sunlight-sensitivity");
        var extra = Decision("allow-other-sensitivity", "candidate", other.Id,
            new { eligibility = "eligible" });
        var result = Evaluate(proposal with
        {
            Manifestations = proposal.Manifestations.Concat(
                [new NativeOriginManifestation("other-sensitivity", other.Id)]).ToArray(),
            TableDecisions = proposal.TableDecisions!.Append(extra).ToArray()
        }, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, result.Status);
        Assert.Contains(result.Issues, x =>
            x.Code == "required-mechanic-missing" && x.Path == "constraints." + link.Id);
        Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.IntrinsicSlotId == "darkvisionFeet");
        Assert.Contains(result.Profile.Underlying.InherentGrants, x =>
            x.SourceCandidateIds.Contains(other.Id));
    }

    [Fact]
    public void VerifiedTableWaiverCanChangeNativeLinkedDefaultWithoutGrantingDrawback()
    {
        var (proposal, vision, drawback, link, permits) = SelectedDrow();
        var waived = Decision("waive-native-link", "constraint", link.Id,
            new { allow = true });
        var result = Evaluate(proposal with
        {
            TableDecisions = permits.Append(waived).ToArray()
        }, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Equal(120, Assert.Single(result.Profile!.Underlying.InherentGrants,
            x => x.Identity.IntrinsicSlotId == "darkvisionFeet")
            .EffectiveValue!.Value.GetInt32());
        Assert.DoesNotContain(result.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == drawback.Identity.MechanicalDefinitionId);
        Assert.DoesNotContain(result.Issues, x => x.Code == "required-mechanic-missing");
        var projected = NativeOriginDarkvisionProjector.ProjectApprovedMixed(Catalog(), result);
        Assert.Equal(120, projected.Darkvision!.RangeFeet);
        // A waiver of the source-local selection requirement does not waive the
        // independent, ruleset-wide range threshold.
        Assert.Equal("condition.dazzled",
            projected.RequiredSunlightSensitivity?.ConditionId);
        Assert.Contains(result.EvaluatedProposal.Candidates, x => x.Id == vision.Id);

        var unverified = Evaluate(result.EvaluatedProposal);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, unverified.Status);
        Assert.DoesNotContain(unverified.Profile?.Underlying.InherentGrants ?? [], x =>
            x.Identity.IntrinsicSlotId == "darkvisionFeet");
    }

    [Fact]
    public void LinkedDrawbackMustSurviveFinalReconciliationUnlessExplicitlyWaived()
    {
        var (proposal, vision, sensitivity, link, permits) = SelectedDrow();
        var offered = Evaluate(proposal).EvaluatedProposal.Candidates;
        var dancing = Assert.Single(offered, x =>
            x.ContributorId == "a" &&
            x.Identity.MechanicalDefinitionId == "trait.dancing-lights");
        // Dancing Lights now has the previously missing native Drow ancestry
        // spellcasting-ability dependency. Select it from the same contributor
        // so this test isolates the intended Darkvision/drawback relationship.
        var spellAbility = Assert.Single(offered, x =>
            x.ContributorId == "a" &&
            x.Identity.IntrinsicSlotId == "spellcastingAbility");
        var drop = Decision("drop-sensitivity", "relationship", "replace-sensitivity",
            new { action = "retain-second" });
        var extra = Decision("permit-dancing", "candidate", dancing.Id,
            new { eligibility = "eligible" });
        var selections = proposal.Manifestations.Concat(
        [
            new NativeOriginManifestation("drawback", sensitivity.Id),
            new NativeOriginManifestation("dancing-lights", dancing.Id),
            new NativeOriginManifestation("drow-spell-ability", spellAbility.Id,
                Choices: [new(spellAbility.RequiredChoices!.Single().Id, ["wisdom"])])
        ]).ToArray();
        var allowAbility = Decision("permit-drow-spell-ability", "candidate",
            spellAbility.Id, new { eligibility = "eligible" });
        var candidatePermissions = permits.Concat([extra, allowAbility, drop]).ToArray();
        var original = proposal with
        {
            Manifestations = selections, TableDecisions = candidatePermissions,
            Relationships =
            [
                new("replace-sensitivity", NativeOriginRelationshipKind.Substitution,
                    [sensitivity.Id, dancing.Id])
            ],
            Resolutions =
            [
                new("replace-sensitivity", NativeOriginResolutionAction.RetainSecond,
                    new(NativeOriginAdjudication.DmAuthorized,
                        TableDecisionId: drop.Id))
            ]
        };
        var rejected = Evaluate(original, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, rejected.Status);
        Assert.Contains(rejected.Issues, x =>
            x.Code == "native-linked-effective-requirement-missing" &&
            x.Path == "constraints." + link.Id);
        Assert.DoesNotContain(rejected.Profile!.Underlying.InherentGrants, x =>
            x.SourceCandidateIds.Contains(vision.Id));
        Assert.DoesNotContain(rejected.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == sensitivity.Identity.MechanicalDefinitionId);
        Assert.Contains(rejected.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == dancing.Identity.MechanicalDefinitionId);

        var overrideDecision = Decision("linked-replacement-waiver",
            "constraint", link.Id, new { allow = true });
        var waived = Evaluate(original with
        {
            TableDecisions = candidatePermissions.Append(overrideDecision).ToArray()
        }, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, waived.Status);
        Assert.Equal(120, Assert.Single(waived.Profile!.Underlying.InherentGrants, x =>
            x.Identity.IntrinsicSlotId == "darkvisionFeet")
            .EffectiveValue!.Value.GetInt32());
        Assert.DoesNotContain(waived.Issues, x =>
            x.Code == "native-linked-effective-requirement-missing");
    }

    [Fact]
    public void LinkedDrowProposalRoundTripsAndReconstructsDeterministically()
    {
        var (proposal, _, drawback, _, _) = SelectedDrow();
        var selected = proposal with
        {
            Manifestations = proposal.Manifestations.Concat(
                [new NativeOriginManifestation("selected-sensitivity", drawback.Id)]).ToArray()
        };
        var original = Evaluate(selected, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, original.Status);

        var persisted = JsonSerializer.Deserialize<NativeOriginProposal>(
            JsonSerializer.Serialize(original.EvaluatedProposal));
        var replay = Evaluate(persisted!, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, replay.Status);
        Assert.DoesNotContain(replay.Issues,
            x => x.Code == "native-linked-constraint-tampered");
        Assert.Equal(original.Profile!.Underlying.InherentGrants.Select(x => x.Id),
            replay.Profile!.Underlying.InherentGrants.Select(x => x.Id));
        Assert.Equal(Assert.Single(original.EvaluatedProposal.Constraints!).Id,
            Assert.Single(replay.EvaluatedProposal.Constraints!).Id);

        var altered = persisted! with
        {
            Manifestations = persisted.Manifestations.Where(x =>
                x.CandidateId != drawback.Id).ToArray()
        };
        var denied = Evaluate(altered, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, denied.Status);
        Assert.Contains(denied.Issues, x =>
            x.Code == "required-mechanic-missing");
        Assert.DoesNotContain(denied.Profile!.Underlying.InherentGrants, x =>
            x.Identity.IntrinsicSlotId == "darkvisionFeet");
    }

    [Fact]
    public void ConventionalDrowSelectionIncludesItsCompleteSensoryAdaptation()
    {
        var selected = new NativeOriginProposal("direct-drow",
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"),
            [], [], [], SelectedAncestryId: "ancestry.elf.drow");
        var preview = Evaluate(selected, auto: true).EvaluatedProposal;
        var spellcasting = Assert.Single(preview.Candidates, x =>
            x.Identity.IntrinsicSlotId == "spellcastingAbility");
        var candidate = Assert.Single(preview.Candidates, x =>
            x.Identity.IntrinsicSlotId == "darkvisionFeet");
        var sunlight = Assert.Single(preview.Candidates, x =>
            x.Identity.MechanicalDefinitionId == "trait.sunlight-sensitivity");
        Assert.Null(candidate.ContributorId);
        Assert.Single(preview.Constraints!, x =>
            x.Subjects.Single().CandidateId == candidate.Id &&
            x.Conditions.Single().CandidateId == sunlight.Id);

        var full = Evaluate(selected with
        {
            Manifestations =
            [
                new("drow-casting", spellcasting.Id,
                    Choices: [new("spellcastingAbility", ["intelligence"])])
            ]
        }, auto: true);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, full.Status);
        Assert.Equal(120, Assert.Single(full.Profile!.Underlying.InherentGrants,
            x => x.Identity.IntrinsicSlotId == "darkvisionFeet")
            .EffectiveValue!.Value.GetInt32());
        Assert.Contains(full.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.sunlight-sensitivity");
        Assert.DoesNotContain(full.Issues, x =>
            x.Code is "required-mechanic-missing" or "native-linked-constraint-tampered");
        var projected = NativeOriginDarkvisionProjector.Project(Catalog(), full);
        Assert.Equal("resolved", projected.Status);
        Assert.Equal(120, projected.Darkvision!.RangeFeet);
        Assert.Equal("ancestry.elf.drow", projected.Darkvision.SourceId);
        Assert.Equal("/darkvisionFeet", projected.Darkvision.OccurrencePath);
        Assert.Equal("unresolved",
            NativeOriginDarkvisionProjector.ProjectApprovedMixed(Catalog(), full).Status);
    }

    [Fact]
    public void CallerCannotRewriteNativeLinkEndpoints()
    {
        var (proposal, _, drawback, link, _) = SelectedDrow();
        var malicious = proposal with
        {
            Constraints = [link with
            {
                Conditions = [new NativeOriginCharacteristicSelector(
                    CandidateId: "made-up-sensitivity")]
            }]
        };
        var denied = Evaluate(malicious, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Invalid, denied.Status);
        Assert.Contains(denied.Issues, x =>
            x.Code == "native-linked-constraint-tampered");
        Assert.Null(denied.Profile);

        // Player-directed drawback selection without Darkvision remains valid.
        var onlyDrawback = Evaluate(Proposal() with
        {
            Manifestations = [new("sensitivity-only", drawback.Id)],
            TableDecisions =
            [
                Decision("permit-sensitivity", "candidate", drawback.Id,
                    new { eligibility = "eligible" })
            ]
        }, new TestAuthority());
        Assert.DoesNotContain(onlyDrawback.Issues, x =>
            x.Code == "required-mechanic-missing");
    }
}
