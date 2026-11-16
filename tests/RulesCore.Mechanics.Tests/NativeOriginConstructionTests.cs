using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Exercise the production construction engine on the actual native catalog; synthetic
/// and trusted-test decisions are used only where first-party policies remain open.
/// </summary>
public sealed class NativeOriginConstructionTests
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
        throw new InvalidOperationException("Native first-party catalog is missing.");
    }

    private static NativeOriginProposal Proposal(string id = "origin",
        NativeOriginIntent intent = NativeOriginIntent.Direct,
        string? species = null, string? ancestry = null,
        IReadOnlyList<NativeOriginContributor>? contributors = null,
        IReadOnlyList<NativeOriginManifestation>? manifests = null,
        IReadOnlyList<NativeTableRuleDecision>? decisions = null) =>
        new(id, intent, new("rule.creature-origination", "biological"),
            contributors ?? [], [], manifests ?? [], species, ancestry,
            TableDecisions: decisions);

    private static NativeOriginContributor Contributor(string id, string species) =>
        new(id, new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.Native, species));

    private static NativeOriginConstructionResult Evaluate(
        NativeOriginProposal proposal, INativeOriginAuthority? authority = null,
        bool defaults = true) => new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, authority is null ? null : new("campaign", "campaign-a"),
                AutoManifestDirectDefaults: defaults), authority);

    private static NativeTableRuleDecision Decision(string id, string kind, string target,
        object outcome) => new(id, new("dm", "trusted-auth-evidence"),
            new("campaign", "campaign-a"), new(kind, target),
            JsonSerializer.SerializeToElement(outcome));

    private sealed class TestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision d, NativeTableDecisionScope scope) =>
            d.Authority.ActorId == "dm" && d.Authority.AuthorizationEvidenceId ==
                "trusted-auth-evidence" && scope.Kind == "campaign" && scope.Id == "campaign-a";
        public bool AuthorizesCatalogChoice(string catalogId, string optionId, string evidenceId) =>
            evidenceId == "verified-catalog-proof" && !string.IsNullOrWhiteSpace(catalogId) &&
            !string.IsNullOrWhiteSpace(optionId);
    }

    [Fact]
    public void HumanSourceIsDeconstructedAndOnlyManifestedGrantsAreReconstructed()
    {
        var result = Evaluate(Proposal(species: "species.human"));
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, result.Status);
        Assert.Empty(result.EvaluatedProposal.Contributors);
        Assert.Equal("species.human", result.Profile!.Underlying.SpeciesId);
        Assert.Equal(NativeOriginEventStage.Pending,
            result.Profile.History.Single(x => x.Id == "construction:origin").Stage);
        Assert.Equal(result.Profile.Underlying, result.Profile.Current);
        Assert.Contains(result.EvaluatedProposal.Candidates, x =>
            x.Identity.IntrinsicSlotId == "size" && x.RequiredChoices?.Count == 1);
        Assert.Contains(result.Profile.Current.InherentGrants, x =>
            x.Identity.IntrinsicSlotId == "walkingSpeedFeet" &&
            x.EffectiveValue?.GetInt32() == 30);
        Assert.Contains(result.Profile.Current.InherentGrants, x =>
            x.Identity.Occurrence.OccurrencePath.StartsWith("/traits/", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Profile.Current.InherentGrants, x =>
            x.Identity.Occurrence.OccurrencePath == "/sizeChoice");
        Assert.Contains(result.Issues, x => x.Code == "required-origin-selection-missing");
    }

    [Fact]
    public void HighElfRetainsSpeciesAndAncestryProvenanceAndRequiredChoices()
    {
        var result = Evaluate(Proposal("high", species: "species.elf",
            ancestry: "ancestry.elf.high"));
        Assert.Equal("species.elf", result.Profile!.Underlying.SpeciesId);
        Assert.Equal("ancestry.elf.high", result.Profile.Underlying.AncestryId);
        Assert.Contains(result.EvaluatedProposal.Candidates,
            x => x.Identity.Occurrence.Source.Id == "species.elf");
        Assert.Contains(result.EvaluatedProposal.Candidates,
            x => x.Identity.Occurrence.Source.Id == "ancestry.elf.high" &&
                 x.Identity.IntrinsicSlotId == "spellcastingAbility");
        Assert.Contains(result.Issues, x => x.Severity is NativeOriginIssueSeverity.Incomplete
            or NativeOriginIssueSeverity.Unresolved);
        Assert.DoesNotContain(result.Profile.Current.InherentGrants, x =>
            x.Identity.Occurrence.OccurrencePath == "/spellcastingAbilityChoice");
    }

    [Fact]
    public void DuplicateVerifiedMixedSourcesProjectOneIntrinsicSetAndOneLowLightVision()
    {
        var proposal = Proposal("duplicate-elf", NativeOriginIntent.Mixed, contributors:
            [Contributor("parent-a", "species.elf"), Contributor("parent-b", "species.elf")]);
        var offered = Evaluate(proposal).EvaluatedProposal.Candidates
            .Where(candidate =>
                candidate.Identity.IntrinsicSlotId is
                    "creatureType" or "size" or "walkingSpeedFeet" ||
                candidate.Identity.Occurrence.Source.Id == "species.elf" &&
                candidate.Identity.Occurrence.OccurrencePath == "/traits/0")
            .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(8, offered.Length);

        var decisions = offered.Select((candidate, i) =>
            Decision("allow-" + i, "candidate", candidate.Id,
                new { eligibility = "eligible" })).ToArray();
        var selections = offered.Select((candidate, i) =>
            new NativeOriginManifestation("select-" + i, candidate.Id,
                candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic
                    ? candidate.OfferedValue : null)).ToArray();
        var result = Evaluate(proposal with
        {
            TableDecisions = decisions,
            Manifestations = selections
        }, new TestAuthority(), false);

        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Equal(4, result.Profile!.Current.InherentGrants.Count);
        Assert.All(result.Profile.Current.InherentGrants,
            grant => Assert.Equal(2, grant.SourceCandidateIds.Count));
        var intrinsics = NativeOriginApprovedMixedIntrinsicProjector.Project(result);
        Assert.Equal("resolved", intrinsics.Status);
        Assert.Equal("fey", intrinsics.CreatureType?.Value);
        Assert.Equal("medium", intrinsics.Size?.Value);
        Assert.Equal(30, intrinsics.WalkingSpeedFeet?.Value);
        var vision = NativeOriginVisionProjector.ProjectApprovedMixed(Catalog(), result);
        Assert.Equal("resolved", vision.Status);
        Assert.Equal("rule.low-light-vision", vision.LowLightVision?.RuleId);

        // A result cannot masquerade as legitimate consolidation by attaching
        // an unselected source ID to an otherwise authorized grant.
        var speed = result.Profile.Current.InherentGrants.Single(grant =>
            grant.Identity.IntrinsicSlotId == "walkingSpeedFeet");
        var forged = result.Profile.Underlying with
        {
            InherentGrants = result.Profile.Current.InherentGrants
                .Select(grant => grant.Id == speed.Id
                    ? grant with { SourceCandidateIds = [..grant.SourceCandidateIds,
                        "unselected-candidate"] }
                    : grant).ToArray()
        };
        var tampered = result with
        {
            Profile = result.Profile with { Underlying = forged, Current = forged }
        };
        Assert.Equal("unresolved",
            NativeOriginApprovedMixedIntrinsicProjector.Project(tampered).Status);
        Assert.Equal("unresolved",
            NativeOriginVisionProjector.ProjectApprovedMixed(Catalog(), tampered).Status);
    }

    [Fact]
    public void SameAuthoredOccurrenceThroughTwoContributorsIsNotGrantedTwice()
    {
        var baseProposal = Proposal("shared", NativeOriginIntent.Mixed, contributors:
            [Contributor("a", "species.human"), Contributor("b", "species.human")]);
        var discovered = Evaluate(baseProposal).EvaluatedProposal.Candidates;
        var offered = discovered.Where(x => x.Identity.IntrinsicSlotId == "walkingSpeedFeet")
            .OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
        Assert.Equal(2, offered.Length);
        var decisions = offered.Select((x, index) =>
            Decision("permit-" + index, "candidate", x.Id, new { eligibility = "eligible" })).ToArray();
        var manifests = offered.Select((x, index) =>
            new NativeOriginManifestation("pick-" + index, x.Id,
                JsonSerializer.SerializeToElement(30))).ToArray();
        var result = Evaluate(baseProposal with
        {
            Manifestations = manifests, TableDecisions = decisions
        }, new TestAuthority(), false);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Equal(NativeOriginEventStage.Completed,
            result.Profile!.History.Single(x => x.Id == "construction:shared").Stage);
        var grant = Assert.Single(result.Profile.Underlying.InherentGrants);
        Assert.Equal(2, grant.SourceCandidateIds.Count);
        Assert.Equal(NativeOriginAdjudication.DmAuthorized, grant.Authorization!.Adjudication);
    }

    [Fact]
    public void HumanAndElfCanParticipateWithoutAutomaticCompatibilityOrTraitStacking()
    {
        var result = Evaluate(Proposal("mixed", NativeOriginIntent.Mixed, contributors:
            [Contributor("human", "species.human"), Contributor("elf", "species.elf")]));
        Assert.Contains(result.EvaluatedProposal.Candidates, x =>
            x.Identity.Occurrence.Source.Id == "species.human");
        Assert.Contains(result.EvaluatedProposal.Candidates, x =>
            x.Identity.Occurrence.Source.Id == "species.elf");
        Assert.Empty(result.Profile!.Underlying.InherentGrants);
        Assert.DoesNotContain(result.Issues, x => x.Code.Contains("cross-type",
            StringComparison.Ordinal));
    }

    [Fact]
    public void FiniteAndOpenCatalogChoicesMustBeCompleteAndVerified()
    {
        var baseline = Evaluate(Proposal("choices", species: "species.human"));
        var size = baseline.EvaluatedProposal.Candidates.Single(x =>
            x.Identity.Occurrence.OccurrencePath == "/sizeChoice");
        var skill = baseline.EvaluatedProposal.Candidates.Single(x =>
            x.Identity.Occurrence.OccurrencePath == "/traits/0");
        var requested = Proposal("choices", species: "species.human", manifests:
        [
            new("size", size.Id, Choices:
                [new("size", ["medium"])]),
            new("skill", skill.Id, Choices:
                [new("selection", ["skill.listen"], "verified-catalog-proof")])
        ]);
        var noAuthority = Evaluate(requested);
        Assert.DoesNotContain(noAuthority.Profile!.Underlying.InherentGrants, x =>
            x.Identity == skill.Identity);
        var trusted = Evaluate(requested, new TestAuthority());
        Assert.Contains(trusted.Profile!.Underlying.InherentGrants, x =>
            x.Identity == skill.Identity);
        Assert.Contains(trusted.Profile.Underlying.InherentGrants, x =>
            x.Identity.IntrinsicSlotId == "size" && x.EffectiveValue?.GetString() == "medium");
        Assert.Contains(trusted.Issues, x => x.Code == "required-origin-selection-missing");
    }

    [Fact]
    public void IntrinsicNormalizationRejectsMalformedChoicesWithoutPublishingGrants()
    {
        var origin = Proposal("mixed-malformed", NativeOriginIntent.Mixed,
            contributors: [Contributor("parent-one", "species.human"),
                Contributor("parent-two", "species.elf")]);
        var speed = Evaluate(origin, defaults: false).EvaluatedProposal.Candidates
            .Single(candidate => candidate.ContributorId == "parent-one" &&
                candidate.Identity.IntrinsicSlotId == "walkingSpeedFeet");
        var malformed = new IReadOnlyList<NativeOriginChoiceSelection>?[]
        {
            [new("forged-one", ["x"]), new("forged-two", ["y"])],
            [null!],
            [new("unrecognized", null!)],
            [new("duplicate", ["x"]), new("duplicate", ["x"])],
            [new("conflict", ["x"]), new("conflict", ["y"])]
        };
        foreach (var selections in malformed)
        {
            var proposed = origin with { Manifestations =
                [new NativeOriginManifestation("speed", speed.Id, Choices: selections)] };
            var result = Evaluate(proposed, defaults: false);
            Assert.NotEqual(NativeOriginConstructionStatus.Authorized, result.Status);
            Assert.Contains(result.Issues,
                issue => issue.Severity == NativeOriginIssueSeverity.Invalid);
            Assert.DoesNotContain(result.Profile?.Underlying.InherentGrants ?? [],
                grant => grant.Identity.IntrinsicSlotId == "walkingSpeedFeet");
        }
    }

    [Fact]
    public void ValidSingleFiniteIntrinsicChoiceStillNormalizes()
    {
        var baseline = Evaluate(Proposal("finite", species: "species.human"),
            defaults: false);
        var size = baseline.EvaluatedProposal.Candidates.Single(candidate =>
            candidate.Identity.Occurrence.OccurrencePath == "/sizeChoice");
        var result = Evaluate(Proposal("finite", species: "species.human",
            manifests: [new("size", size.Id,
                Choices: [new("size", ["medium"])])]), defaults: false);
        Assert.Contains(result.Profile!.Underlying.InherentGrants, grant =>
            grant.Identity.IntrinsicSlotId == "size" &&
            grant.EffectiveValue?.GetString() == "medium");
        Assert.DoesNotContain(result.Issues,
            issue => issue.Code == "dependent-choice-invalid");
    }

    [Fact]
    public void NestedContributorsExposeCurrentManifestationButNotHistoricalGrants()
    {
        var parent = Evaluate(Proposal("parent", species: "species.human")).Profile!;
        var inherited = new NativeOriginContributor("parent", new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.ManifestedProfile, "parent"),
            parent.Current, ["species.elf", "ancestry.elf.high"]);
        var result = Evaluate(Proposal("nested", NativeOriginIntent.Mixed, contributors: [inherited]));
        Assert.Equal(parent.Current.InherentGrants.Count,
            result.EvaluatedProposal.Candidates.Count);
        Assert.All(result.EvaluatedProposal.Candidates,
            x => Assert.Equal("parent", x.ContributorId));
        Assert.Empty(result.Profile!.Underlying.InherentGrants);
        Assert.Equal(new[] { "parent" },
            result.Profile.History.Single(x => x.Id == "construction:nested").ContributorIds);
    }

    [Fact]
    public void VerifiedDmOutcomeChangesEffectiveValueWithoutChangingNativeCatalog()
    {
        var prepared = Evaluate(Proposal("variation", species: "species.human"));
        var speed = prepared.EvaluatedProposal.Candidates.Single(x =>
            x.Identity.IntrinsicSlotId == "walkingSpeedFeet");
        var decision = Decision("speed-override", "candidate", speed.Id, new { value = 40 });
        var proposal = Proposal("variation", species: "species.human",
            manifests: [new("speed", speed.Id, JsonSerializer.SerializeToElement(40),
                TableDecisionId: decision.Id)], decisions: [decision]);
        var result = Evaluate(proposal, new TestAuthority());
        Assert.Equal(40, result.Profile!.Underlying.InherentGrants.Single(x =>
            x.Identity.IntrinsicSlotId == "walkingSpeedFeet").EffectiveValue!.Value.GetInt32());
        Assert.Equal("speed-override", result.Profile.Underlying.InherentGrants.Single(x =>
            x.Identity.IntrinsicSlotId == "walkingSpeedFeet").Authorization!.TableDecisionId);
        Assert.Equal(30, Catalog().Search("human", "species").Items.Single(x =>
            x.Id == "species.human").Definition.GetProperty("walkingSpeedFeet").GetInt32());
        var unauthorized = Evaluate(proposal);
        Assert.DoesNotContain(unauthorized.Profile!.Underlying.InherentGrants,
            x => x.Identity.IntrinsicSlotId == "walkingSpeedFeet");
    }

    [Fact]
    public void CustomSourceRequiresAnAuthorizedTableDecision()
    {
        var occurrence = new NativeOriginSourceOccurrence(
            new(NativeOriginSourceKind.Custom, "custom.marshfolk"), "/speed");
        var identity = new NativeOriginCharacteristicIdentity(
            NativeOriginCharacteristicKind.Intrinsic, occurrence,
            "custom.marshfolk#speed", IntrinsicSlotId: "walkingSpeedFeet");
        var candidate = new NativeOriginCandidate("custom-speed", identity,
            new(NativeOriginEligibility.Unreviewed),
            OfferedValue: JsonSerializer.SerializeToElement(25));
        var decision = Decision("custom-permit", "candidate", "custom-speed",
            new { eligibility = "eligible" });
        var proposal = Proposal("custom", NativeOriginIntent.Custom,
            manifests: [new("custom-pick", "custom-speed",
                JsonSerializer.SerializeToElement(25))], decisions: [decision]) with
        {
            Candidates = [candidate]
        };
        var without = Evaluate(proposal);
        Assert.Empty(without.Profile!.Underlying.InherentGrants);
        var withApproval = Evaluate(proposal, new TestAuthority());
        Assert.Single(withApproval.Profile!.Underlying.InherentGrants);
    }

    [Fact]
    public void UnknownNativeCandidateCanNotBeSubmittedAsATrustedGrant()
    {
        var fake = new NativeOriginCandidate("made-up",
            new(NativeOriginCharacteristicKind.Intrinsic,
                new(new(NativeOriginSourceKind.Native, "species.human"), "/fake"),
                IntrinsicSlotId: "creatureType"),
            new(NativeOriginEligibility.Eligible, "species.human"),
            OfferedValue: JsonSerializer.SerializeToElement("celestial"));
        var result = Evaluate(Proposal("spoof", species: "species.human",
            manifests: [new("fake", "made-up", JsonSerializer.SerializeToElement("celestial"))])
            with { Candidates = [fake] });
        Assert.Equal(NativeOriginConstructionStatus.Invalid, result.Status);
        Assert.Contains(result.Issues, x => x.Code == "candidate-not-offered");
    }

    [Fact]
    public void TemporaryFormPreservesUnderlyingAndIndependentGrantsWithoutUnion()
    {
        var baseProfile = Evaluate(Proposal("human", species: "species.human")).Profile!;
        var form = new NativeOriginCurrentConfiguration("species.wolf", null, []);
        var active = new NativeOriginActiveTemporaryTransformation(
            "wolf", "wolf-form", new(NativeOriginSourceKind.Custom, "effect.wolf"),
            "activation-proof");
        var proposal = Proposal("active") with
        {
            ExistingConfiguration = baseProfile.Underlying,
            ExistingEffectiveConfiguration = form,
            ActiveTemporaryTransformation = active
        };
        var result = new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, PersistentInfluences:
                [new("lycan", new(NativeOriginSourceKind.Custom, "effect.wolf"),
                    "acquisition-proof")]));
        Assert.Equal(baseProfile.Underlying, result.Profile!.Underlying);
        Assert.Equal(form, result.Profile.Current);
        Assert.Equal(active, result.Profile.ActiveTemporaryTransformation);
        Assert.Empty(result.Profile.Current.InherentGrants);
        Assert.NotEmpty(result.Profile.Underlying.InherentGrants);
    }
    [Fact]
    public void DragonbornLeafDeconstructsTheCompleteAuthoredAncestryChain()
    {
        var result = Evaluate(Proposal("red-dragonborn", species: "species.dragonborn",
            ancestry: "ancestry.dragonborn.red"));
        var ids = result.EvaluatedProposal.Candidates
            .Select(x => x.Identity.Occurrence.Source.Id).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("species.dragonborn", ids);
        Assert.Contains("ancestry.dragonborn.chromatic", ids);
        Assert.Contains("ancestry.dragonborn.red", ids);
        Assert.DoesNotContain(result.Issues, x => x.Code == "ancestry-parent-mismatch");
        Assert.DoesNotContain(result.Issues, x => x.Code == "origin-source-restricted" &&
            x.Path is "species.dragonborn" or "ancestry.dragonborn.chromatic");
    }

    [Fact]
    public void CrossSourceIntrinsicConflictsDoNotUseIterationOrderAsAPolicy()
    {
        var first = Contributor("human", "species.human");
        var second = Contributor("elf", "species.elf");
        var initial = Proposal("cross-source", NativeOriginIntent.Mixed, contributors: [first, second]);
        var offered = Evaluate(initial).EvaluatedProposal.Candidates
            .Where(x => x.Identity.IntrinsicSlotId == "walkingSpeedFeet")
            .OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
        Assert.Equal(2, offered.Length);
        var decisions = offered.Select((x, i) =>
            Decision("accept-" + i, "candidate", x.Id, new { eligibility = "eligible" })).ToArray();
        var picks = offered.Select((x, i) => new NativeOriginManifestation("pick-" + i,
            x.Id, JsonSerializer.SerializeToElement(30))).ToArray();
        var proposal = initial with { Manifestations = picks, TableDecisions = decisions };
        var forward = Evaluate(proposal, new TestAuthority());
        var reversed = Evaluate(proposal with { Contributors = [second, first] },
            new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, forward.Status);
        Assert.Empty(forward.Profile!.Underlying.InherentGrants);
        Assert.Contains(forward.Issues, x => x.Code == "intrinsic-slot-interaction-unresolved");
        Assert.Equal(JsonSerializer.Serialize(forward.Profile.Underlying),
            JsonSerializer.Serialize(reversed.Profile!.Underlying));
    }

    [Fact]
    public void ScopedDmResolutionCanSelectOneOfTwoIncompatibleIntrinsics()
    {
        var proposal = Proposal("chosen", NativeOriginIntent.Mixed,
            contributors: [Contributor("h", "species.human"), Contributor("e", "species.elf")]);
        var offered = Evaluate(proposal).EvaluatedProposal.Candidates
            .Where(x => x.Identity.IntrinsicSlotId == "walkingSpeedFeet")
            .OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
        var relation = new NativeOriginRelationship("choose-speed",
            NativeOriginRelationshipKind.Exclusion, offered.Select(x => x.Id).ToArray());
        var decisions = offered.Select((x, i) =>
            Decision("accept-" + i, "candidate", x.Id, new { eligibility = "eligible" }))
            .Append(Decision("choose", "relationship", "choose-speed",
                new { action = "retain-first" })).ToArray();
        var result = Evaluate(proposal with
        {
            Manifestations = offered.Select((x, i) =>
                new NativeOriginManifestation("pick-" + i, x.Id,
                    JsonSerializer.SerializeToElement(30))).ToArray(),
            TableDecisions = decisions,
            Relationships = [relation],
            Resolutions = [new NativeOriginResolution("choose-speed",
                NativeOriginResolutionAction.RetainFirst,
                new(NativeOriginAdjudication.DmAuthorized, TableDecisionId: "choose"))]
        }, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Single(result.Profile!.Underlying.InherentGrants);
    }

    [Fact]
    public void VerifiedWholeProfileExclusionSuppressesConflictingGrants()
    {
        var proposal = Proposal("constraint", species: "species.human");
        var source = Evaluate(proposal).EvaluatedProposal;
        var speed = source.Candidates.Single(x =>
            x.Identity.IntrinsicSlotId == "walkingSpeedFeet");
        var ranks = source.Candidates.Single(x =>
            x.Identity.Occurrence.OccurrencePath == "/traits/2");
        var decision = Decision("enforce-exclusion", "constraint", "no-combo",
            new { enforce = true });
        var exclusion = new NativeOriginConstraint("no-combo",
            NativeOriginRelationshipKind.Exclusion,
            [new(CandidateId: speed.Id)], [new(CandidateId: ranks.Id)],
            TableDecisionId: decision.Id);
        var result = Evaluate(proposal with
        {
            TableDecisions = [decision], Constraints = [exclusion]
        }, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Conflicting, result.Status);
        Assert.Contains(result.Issues, x => x.Code == "authored-exclusion");
        Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants,
            x => x.Identity.IntrinsicSlotId == "walkingSpeedFeet" ||
                 x.Identity.Occurrence.OccurrencePath == "/traits/2");
    }

    [Fact]
    public void DmAuthoredProcessRequirementsCountRolesNotHardCodedParents()
    {
        var decision = Decision("material-requirement", "input-requirement", "material",
            new { minimumCount = 1, maximumCount = 2 });
        var requirement = new NativeOriginInputRequirement("material",
            new(NativeOriginInputKind.Material), 1, 2,
            TableDecisionId: decision.Id);
        var incomplete = Evaluate(Proposal("assembly", NativeOriginIntent.Custom,
            decisions: [decision]) with { InputRequirements = [requirement] },
            new TestAuthority());
        Assert.Contains(incomplete.Issues, x => x.Code == "process-input-incomplete");
        var material = new NativeOriginContributor("iron",
            new(NativeOriginInputKind.Material),
            new(NativeOriginSourceKind.Custom, "material.iron"));
        var withMaterial = Evaluate(Proposal("assembly", NativeOriginIntent.Custom,
            contributors: [material], decisions: [decision]) with
        {
            InputRequirements = [requirement]
        }, new TestAuthority());
        Assert.DoesNotContain(withMaterial.Issues, x => x.Code == "process-input-incomplete");
        Assert.Empty(withMaterial.Profile!.Underlying.InherentGrants);
    }

    [Fact]
    public void DirectProposalWithoutSpeciesAncestryOrExistingBaselineIsIncomplete()
    {
        var result = Evaluate(Proposal("missing-source"));
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, result.Status);
        Assert.Contains(result.Issues, x => x.Code == "origin-source-required");
    }

    [Fact]
    public void DragonbornFamilyAndLeafBindingsResolveFromNativeLineage()
    {
        var red = Evaluate(Proposal("red-family", species: "species.dragonborn",
            ancestry: "ancestry.dragonborn.red"));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, red.Status);
        Assert.Contains(red.EvaluatedProposal.Candidates, x =>
            x.Identity.Occurrence.Source.Id == "ancestry.dragonborn.chromatic");
        var breath = red.Profile!.Underlying.InherentGrants.Single(x =>
            x.Identity.Occurrence.Source.Id == "species.dragonborn" &&
            x.Identity.Occurrence.OccurrencePath == "/traits/2");
        Assert.Equal("fire", breath.Parameters!["damageType"].GetString());
        Assert.Equal("line", breath.Parameters["area"].GetProperty("shape").GetString());
        Assert.Equal(30, breath.Parameters["area"].GetProperty("lengthFeet").GetInt32());
        var resistance = red.Profile.Underlying.InherentGrants.Single(x =>
            x.Identity.Occurrence.Source.Id == "species.dragonborn" &&
            x.Identity.Occurrence.OccurrencePath == "/traits/1");
        Assert.Equal("fire", resistance.Parameters!["damageType"].GetString());
        var warding = red.Profile.Underlying.InherentGrants.Single(x =>
            x.Identity.Occurrence.Source.Id == "ancestry.dragonborn.chromatic" &&
            x.Identity.Occurrence.OccurrencePath == "/traits/0");
        Assert.Equal("fire", warding.Parameters!["damageType"].GetString());
        Assert.DoesNotContain(red.Issues, x => x.Code == "unsupported-source-binding");

        var bronze = Evaluate(Proposal("bronze-family", species: "species.dragonborn",
            ancestry: "ancestry.dragonborn.bronze"));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, bronze.Status);
        var bronzeBreath = bronze.Profile!.Underlying.InherentGrants.Single(x =>
            x.Identity.Occurrence.Source.Id == "species.dragonborn" &&
            x.Identity.Occurrence.OccurrencePath == "/traits/2");
        Assert.Equal("lightning", bronzeBreath.Parameters!["damageType"].GetString());
        Assert.Equal("cone", bronzeBreath.Parameters["area"].GetProperty("shape").GetString());
        Assert.DoesNotContain(bronze.Profile.Underlying.InherentGrants, x =>
            x.Identity.Occurrence.Source.Id == "ancestry.dragonborn.chromatic");
        Assert.Contains(bronze.Profile.Underlying.InherentGrants, x =>
            x.Identity.Occurrence.Source.Id == "ancestry.dragonborn.metallic");
    }

    [Fact]
    public void TableOnlyConfigurationOverrideRetainsItsVerifiedAdjudicationEvidence()
    {
        var decision = Decision("custom-profile", "configuration", "unlabeled",
            new { speciesId = "custom.marshfolk" });
        var proposal = Proposal("unlabeled", NativeOriginIntent.Custom,
            manifests: [], decisions: [decision]);
        // An empty proposal is still incomplete. The verified decision must
        // nonetheless remain attributable when the table decides the label.
        var result = Evaluate(proposal, new TestAuthority());
        Assert.Equal("custom.marshfolk", result.Profile!.Underlying.SpeciesId);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, result.Status);
        Assert.DoesNotContain(result.Issues, x => x.Code == "profile-dm-decision-required");
    }

    [Fact]
    public void StandardHumanWithEveryRequiredNativeChoiceIsAuthorized()
    {
        var source = Evaluate(Proposal("complete-human", species: "species.human"))
            .EvaluatedProposal;
        var size = source.Candidates.Single(x =>
            x.Identity.Occurrence.OccurrencePath == "/sizeChoice");
        var skill = source.Candidates.Single(x =>
            x.Identity.Occurrence.OccurrencePath == "/traits/0");
        var feat = source.Candidates.Single(x =>
            x.Identity.Occurrence.OccurrencePath == "/traits/1");
        var result = Evaluate(Proposal("complete-human", species: "species.human",
            manifests:
            [
                new("size", size.Id, Choices: [new("size", ["medium"])]),
                new("skill", skill.Id, Choices: [
                    new("selection", ["skill.listen"], "verified-catalog-proof")]),
                new("feat", feat.Id, Choices: [
                    new("selection", ["feat.example-origin"], "verified-catalog-proof")])
            ]), new TestAuthority());

        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Empty(result.EvaluatedProposal.Contributors);
        Assert.Equal(NativeOriginEventStage.Completed,
            result.Profile!.History.Single().Stage);
        Assert.Equal(6, result.Profile.Underlying.InherentGrants.Count);
        Assert.Equal("medium", result.Profile.Underlying.InherentGrants.Single(x =>
            x.Identity.IntrinsicSlotId == "size").EffectiveValue!.Value.GetString());
        Assert.Contains(result.Profile.Underlying.InherentGrants, x =>
            x.Identity.Occurrence.OccurrencePath == "/traits/2");
    }

    [Fact]
    public void HighElfSpellTraitsUseOneAuthoredSourceAbilityChoice()
    {
        var source = Evaluate(Proposal("complete-high", ancestry: "ancestry.elf.high"))
            .EvaluatedProposal;
        var ability = source.Candidates.Single(x =>
            x.Identity.IntrinsicSlotId == "spellcastingAbility");
        var cantrip = source.Candidates.Single(x =>
            x.Identity.Occurrence.Source.Id == "ancestry.elf.high" &&
            x.Identity.Occurrence.OccurrencePath == "/traits/0");
        var result = Evaluate(Proposal("complete-high", ancestry: "ancestry.elf.high",
            manifests:
            [
                new("ability", ability.Id,
                    Choices: [new("spellcastingAbility", ["intelligence"])]),
                new("cantrip", cantrip.Id,
                    Choices: [new("selection", ["spell.fire-bolt"],
                        "verified-catalog-proof")])
            ]), new TestAuthority());

        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Equal("species.elf", result.Profile!.Underlying.SpeciesId);
        Assert.Equal("ancestry.elf.high", result.Profile.Underlying.AncestryId);
        var inheritedSpellTraits = result.Profile.Underlying.InherentGrants
            .Where(x => x.Identity.Occurrence.Source.Id == "ancestry.elf.high" &&
                x.Identity.Kind == NativeOriginCharacteristicKind.Trait).ToArray();
        Assert.Equal(3, inheritedSpellTraits.Length);
        Assert.All(inheritedSpellTraits, x => Assert.Equal("intelligence",
            x.Parameters!["spellcastingAbility"].GetString()));
        Assert.Contains(result.Profile.Underlying.InherentGrants, x =>
            x.Identity.Occurrence.Source.Id == "species.elf" &&
            x.Identity.Occurrence.OccurrencePath == "/traits/4");
    }

    [Fact]
    public void MissingAuthoredSpellcastingAbilitySuppressesDependentTraits()
    {
        var result = Evaluate(Proposal("incomplete-high", ancestry: "ancestry.elf.high"));
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, result.Status);
        Assert.Contains(result.Issues, x => x.Code == "origin-binding-choice-missing");
        Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.Occurrence.Source.Id == "ancestry.elf.high" &&
            x.Identity.Kind == NativeOriginCharacteristicKind.Trait);
    }

    [Fact]
    public void WoodElfDirectReplacementRequiresAnAuthorizedPolicy()
    {
        var source = Evaluate(Proposal("wood-choice", ancestry: "ancestry.elf.wood"))
            .EvaluatedProposal;
        var walk = source.Candidates.Where(x =>
            x.Identity.IntrinsicSlotId == "walkingSpeedFeet").ToArray();
        var wood = walk.Single(x =>
            x.Identity.Occurrence.Source.Id == "ancestry.elf.wood");
        var elf = walk.Single(x =>
            x.Identity.Occurrence.Source.Id == "species.elf");
        var ability = source.Candidates.Single(x =>
            x.Identity.IntrinsicSlotId == "spellcastingAbility");
        var decision = Decision("wood-speed-decision", "relationship",
            "wood-speed", new { action = "retain-first" });
        var proposal = Proposal("wood-choice", ancestry: "ancestry.elf.wood",
            manifests: [new("ability", ability.Id,
                Choices: [new("spellcastingAbility", ["wisdom"])])],
            decisions: [decision]) with
        {
            Relationships = [new("wood-speed", NativeOriginRelationshipKind.Substitution,
                [wood.Id, elf.Id])],
            Resolutions = [new("wood-speed", NativeOriginResolutionAction.RetainFirst,
                new(NativeOriginAdjudication.DmAuthorized,
                    TableDecisionId: decision.Id))]
        };
        var without = Evaluate(proposal);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, without.Status);
        var resolved = Evaluate(proposal, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, resolved.Status);
        Assert.Equal(35, resolved.Profile!.Underlying.InherentGrants.Single(x =>
            x.Identity.IntrinsicSlotId == "walkingSpeedFeet")
            .EffectiveValue!.Value.GetInt32());
        Assert.Equal("wood-speed-decision",
            resolved.Profile.Adjudication.TableDecisionId);
    }

    [Fact]
    public void MixedHumanElfCanBeAuthorizedWithoutStackingTwoCompleteSources()
    {
        var baseProposal = Proposal("human-elf", NativeOriginIntent.Mixed,
            contributors: [Contributor("human", "species.human"),
                Contributor("elf", "species.elf")]);
        var sources = Evaluate(baseProposal).EvaluatedProposal.Candidates;
        NativeOriginCandidate Select(string source, string path) =>
            sources.Single(x => x.Identity.Occurrence.Source.Id == source &&
                x.Identity.Occurrence.OccurrencePath == path);
        var chosen = new[]
        {
            Select("species.human", "/creatureType"),
            Select("species.elf", "/size"),
            Select("species.human", "/walkingSpeedFeet"),
            Select("species.elf", "/traits/0"),
            Select("species.human", "/traits/2")
        };
        var permits = chosen.Select((x, i) =>
            Decision("candidate-" + i, "candidate", x.Id,
                new { eligibility = "eligible" })).ToList();
        permits.Add(Decision("identity", "configuration", "human-elf",
            new { speciesId = "custom.human-elf" }));
        var manifestations = chosen.Select((x, i) =>
            new NativeOriginManifestation("selected-" + i, x.Id,
                x.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic ?
                    x.OfferedValue : null)).ToArray();

        var result = Evaluate(baseProposal with
        {
            Manifestations = manifestations, TableDecisions = permits
        }, new TestAuthority(), false);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Equal("custom.human-elf", result.Profile!.Underlying.SpeciesId);
        Assert.Equal(5, result.Profile.Underlying.InherentGrants.Count);
        // Direct first-party projection must stay closed to mixed construction.
        Assert.Equal("unresolved", NativeOriginIntrinsicProjector.Project(result).Status);
        var mixedScalars = NativeOriginApprovedMixedIntrinsicProjector.Project(result);
        Assert.Equal("resolved", mixedScalars.Status);
        Assert.Equal("humanoid", mixedScalars.CreatureType?.Value);
        Assert.Equal("medium", mixedScalars.Size?.Value);
        Assert.Equal(30, mixedScalars.WalkingSpeedFeet?.Value);
        Assert.Empty(mixedScalars.UnresolvedSlots);
        var nativeVision = NativeOriginVisionProjector.ProjectApprovedMixed(Catalog(), result);
        Assert.Equal("resolved", nativeVision.Status);
        Assert.Equal("rule.low-light-vision", nativeVision.LowLightVision?.RuleId);
        Assert.Equal("species.elf", nativeVision.LowLightVision?.SourceId);
        Assert.Equal("/traits/0", nativeVision.LowLightVision?.OccurrencePath);
        Assert.Equal("unresolved", NativeOriginVisionProjector.Project(Catalog(), result).Status);
        Assert.Equal("humanoid", result.Profile.Underlying.InherentGrants.Single(x =>
            x.Identity.IntrinsicSlotId == "creatureType").EffectiveValue!.Value.GetString());
        Assert.Equal(NativeOriginEventStage.Completed, result.Profile.History.Single().Stage);
        Assert.All(result.Profile.Underlying.InherentGrants, x =>
            Assert.Equal(NativeOriginAdjudication.DmAuthorized,
                x.Authorization!.Adjudication));
        Assert.DoesNotContain(result.Profile.Underlying.InherentGrants, x =>
            x.Identity.Occurrence.Source.Id == "species.elf" &&
            x.Identity.IntrinsicSlotId == "creatureType");
    }

    [Fact]
    public void MixedElvenAncestriesShareRootWithoutImplicitlyMergingSpellChoices()
    {
        var proposal = Proposal("two-elf-ancestries", NativeOriginIntent.Mixed,
            contributors: [Contributor("high", "ancestry.elf.high"),
                Contributor("wood", "ancestry.elf.wood")]);
        var offers = Evaluate(proposal).EvaluatedProposal.Candidates;
        var root = offers.Where(x =>
            x.Identity.Occurrence.Source.Id == "species.elf" &&
            x.Identity.Occurrence.OccurrencePath == "/traits/3").ToArray();
        Assert.Equal(2, root.Length);
        Assert.Contains(offers, x => x.Identity.Occurrence.Source.Id ==
            "ancestry.elf.high");
        Assert.Contains(offers, x => x.Identity.Occurrence.Source.Id ==
            "ancestry.elf.wood");
        var permits = root.Select((x, i) =>
            Decision("root-" + i, "candidate", x.Id,
                new { eligibility = "eligible" })).ToArray();
        var manifestations = root.Select((x, i) =>
            new NativeOriginManifestation("root-" + i, x.Id)).ToArray();
        var result = Evaluate(proposal with
        {
            TableDecisions = permits, Manifestations = manifestations
        }, new TestAuthority(), false);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var inherited = Assert.Single(result.Profile!.Underlying.InherentGrants);
        Assert.Equal(2, inherited.SourceCandidateIds.Count);
        Assert.DoesNotContain(result.Profile.Underlying.InherentGrants, x =>
            x.Identity.Occurrence.Source.Id is "ancestry.elf.high" or "ancestry.elf.wood");
    }

    [Fact]
    public void NestedMixedContributorOffersOnlyItsCurrentlyManifestedCharacteristics()
    {
        var parentProposal = Proposal("prior-mixed", NativeOriginIntent.Mixed,
            contributors: [Contributor("human", "species.human"),
                Contributor("elf", "species.elf")]);
        var humanSkillRanks = Evaluate(parentProposal).EvaluatedProposal.Candidates.Single(x =>
            x.Identity.Occurrence.Source.Id == "species.human" &&
            x.Identity.Occurrence.OccurrencePath == "/traits/2");
        var parent = Evaluate(parentProposal with
        {
            Manifestations = [new("ranks", humanSkillRanks.Id)],
            TableDecisions = [Decision("permit-ranks", "candidate", humanSkillRanks.Id,
                new { eligibility = "eligible" })]
        }, new TestAuthority(), false);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, parent.Status);
        var contributor = new NativeOriginContributor("previously-mixed",
            new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.ManifestedProfile, parent.Profile!.Id),
            parent.Profile.Current, ["species.human", "species.elf"]);
        var next = Proposal("second-generation", NativeOriginIntent.Mixed,
            contributors: [contributor]);
        var offer = Assert.Single(Evaluate(next).EvaluatedProposal.Candidates);
        Assert.Equal("species.human", offer.Identity.Occurrence.Source.Id);
        Assert.DoesNotContain("species.elf",
            new[] { offer.Identity.Occurrence.Source.Id });
        var result = Evaluate(next with
        {
            Manifestations = [new("inherited-ranks", offer.Id)],
            TableDecisions = [Decision("permit-nested", "candidate", offer.Id,
                new { eligibility = "eligible" })]
        }, new TestAuthority(), false);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Single(result.Profile!.Underlying.InherentGrants);
        Assert.Equal("previously-mixed", result.EvaluatedProposal.Contributors.Single().Id);
    }

    [Fact]
    public void MixedElvenSpellcastingSourcesDoNotSilentlyCombineAbilityChoices()
    {
        var proposal = Proposal("dual-elven-spell", NativeOriginIntent.Mixed,
            contributors: [Contributor("high", "ancestry.elf.high"),
                Contributor("wood", "ancestry.elf.wood")]);
        var offers = Evaluate(proposal).EvaluatedProposal.Candidates;
        var chosen = offers.Where(x =>
            x.Identity.Occurrence.Source.Id is "ancestry.elf.high" or "ancestry.elf.wood" &&
            (x.Identity.IntrinsicSlotId == "spellcastingAbility" ||
             x.Identity.Occurrence.OccurrencePath == "/traits/1" &&
             x.Identity.Occurrence.Source.Id == "ancestry.elf.high" ||
             x.Identity.Occurrence.OccurrencePath == "/traits/0" &&
             x.Identity.Occurrence.Source.Id == "ancestry.elf.wood")).ToArray();
        Assert.Equal(4, chosen.Length);
        var manifests = chosen.Select((c, i) => new NativeOriginManifestation("spells-" + i,
            c.Id, Choices: c.Identity.IntrinsicSlotId == "spellcastingAbility" ?
                [new NativeOriginChoiceSelection("spellcastingAbility",
                    [c.Identity.Occurrence.Source.Id == "ancestry.elf.high" ?
                        "intelligence" : "wisdom"])] : null)).ToArray();
        var permissions = chosen.Select((c, i) => Decision("permission-" + i,
            "candidate", c.Id, new { eligibility = "eligible" })).ToArray();
        var result = Evaluate(proposal with
        {
            Manifestations = manifests, TableDecisions = permissions
        }, new TestAuthority(), false);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Contains(result.Issues, x => x.Code == "intrinsic-slot-interaction-unresolved");
        Assert.Contains(result.Issues, x => x.Code == "origin-binding-choice-missing");
        Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.Occurrence.Source.Id is "ancestry.elf.high" or "ancestry.elf.wood" &&
            x.Identity.Kind == NativeOriginCharacteristicKind.Trait);
    }

    [Fact]
    public void RedBronzeMixPreservesDistinctParameterOffersWithoutStackingBreathWeapon()
    {
        var proposal = Proposal("cross-dragonborn", NativeOriginIntent.Mixed,
            contributors: [Contributor("red", "ancestry.dragonborn.red"),
                Contributor("bronze", "ancestry.dragonborn.bronze")]);
        var breaths = Evaluate(proposal).EvaluatedProposal.Candidates.Where(x =>
            x.Identity.Occurrence.Source.Id == "species.dragonborn" &&
            x.Identity.Occurrence.OccurrencePath == "/traits/2")
            .OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
        Assert.Equal(2, breaths.Length);
        Assert.Equal(new[] { "fire", "lightning" },
            breaths.Select(x => x.OfferedParameters!["damageType"].GetString())
                .OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(new[] { "cone", "line" },
            breaths.Select(x => x.OfferedParameters!["area"]
                .GetProperty("shape").GetString())
                .OrderBy(x => x, StringComparer.Ordinal));

        var result = Evaluate(proposal with
        {
            Manifestations = breaths.Select((x, i) =>
                new NativeOriginManifestation("breath-" + i, x.Id)).ToArray(),
            TableDecisions = breaths.Select((x, i) =>
                Decision("allow-breath-" + i, "candidate", x.Id,
                    new { eligibility = "eligible" })).ToArray()
        }, new TestAuthority(), false);
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, result.Status);
        Assert.Contains(result.Issues, x => x.Code == "same-definition-different-manifestations");
        Assert.Empty(result.Profile!.Underlying.InherentGrants);
    }

    [Fact]
    public void PlayerDirectedRedBronzeRecombinationProducesOneSourceBackedBreathWeapon()
    {
        var original = Proposal("chosen-dragonborn", NativeOriginIntent.Mixed,
            contributors: [Contributor("red", "ancestry.dragonborn.red"),
                Contributor("bronze", "ancestry.dragonborn.bronze")]);
        var offered = Evaluate(original).EvaluatedProposal.Candidates.Where(x =>
            x.Identity.Occurrence.Source.Id == "species.dragonborn" &&
            x.Identity.Occurrence.OccurrencePath == "/traits/2").ToArray();
        var red = offered.Single(x =>
            x.OfferedParameters!["damageType"].GetString() == "fire");
        var bronze = offered.Single(x =>
            x.OfferedParameters!["damageType"].GetString() == "lightning");
        // Choose fire from Red and the complete cone geometry from Bronze.
        // Neither the category nor its dimensions is split into free traits.
        var parameters = new Dictionary<string, JsonElement>
        {
            ["damageType"] = red.OfferedParameters!["damageType"],
            ["area"] = bronze.OfferedParameters!["area"]
        };
        var sourceMap = new Dictionary<string, string>
        {
            ["damageType"] = red.Id,
            ["area"] = bronze.Id
        };
        var decision = Decision("authorize-composition", "relationship", "breath-compose",
            new { action = "replace", resultCandidateId = red.Id,
                parameters, parameterSources = sourceMap });
        var resolution = new NativeOriginResolution("breath-compose",
            NativeOriginResolutionAction.Replace,
            new(NativeOriginAdjudication.DmAuthorized, TableDecisionId: decision.Id),
            red.Id, parameters, sourceMap);
        var selected = original with
        {
            Manifestations =
            [
                new("red-breath", red.Id),
                new("bronze-breath", bronze.Id)
            ],
            TableDecisions =
            [
                Decision("allow-red", "candidate", red.Id, new { eligibility = "eligible" }),
                Decision("allow-bronze", "candidate", bronze.Id, new { eligibility = "eligible" }),
                decision
            ],
            Relationships = [new("breath-compose",
                NativeOriginRelationshipKind.Substitution, [red.Id, bronze.Id])],
            Resolutions = [resolution]
        };

        var result = Evaluate(selected, new TestAuthority(), false);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var breath = Assert.Single(result.Profile!.Underlying.InherentGrants);
        Assert.Equal("breath-weapon", breath.EffectiveValue!.Value.GetProperty("id").GetString());
        Assert.Equal("fire", breath.Parameters!["damageType"].GetString());
        Assert.Equal("cone", breath.Parameters["area"].GetProperty("shape").GetString());
        Assert.Equal(15, breath.Parameters["area"].GetProperty("lengthFeet").GetInt32());
        Assert.Equal(2, breath.SourceCandidateIds.Count);
        Assert.Equal(red.Id, breath.ParameterSources!["damageType"]);
        Assert.Equal(bronze.Id, breath.ParameterSources["area"]);
        Assert.Equal("proficiency-bonus",
            breath.EffectiveValue.Value.GetProperty("uses").GetProperty("value").GetString());
        Assert.Equal(4, breath.EffectiveValue.Value.GetProperty("damage")
            .GetProperty("progression").GetArrayLength());
        Assert.Equal(NativeOriginAdjudication.DmAuthorized, breath.Authorization!.Adjudication);
        Assert.Equal(decision.Id, breath.Authorization.TableDecisionId);

        // The canonical Breath Weapon now authors both recombinable fields.
        // Candidate inheritance eligibility still needs its own authority;
        // the authored relationship only removes the redundant DM ruling
        // about how the *already permitted* characteristic is composed.
        const string canonical = "trait.dragonborn.breath-weapon";
        var authored = selected with
        {
            TableDecisions = selected.TableDecisions!.Where(x =>
                x.Id != decision.Id).ToArray(),
            Relationships = [new("breath-compose",
                NativeOriginRelationshipKind.Substitution, [red.Id, bronze.Id],
                AuthoredRuleReference: canonical)],
            Resolutions = [resolution with
            {
                Basis = new(NativeOriginAdjudication.Supported,
                    NativeRuleReference: canonical)
            }]
        };
        var authoredResult = Evaluate(authored, new TestAuthority(), false);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, authoredResult.Status);
        var composed = Assert.Single(authoredResult.Profile!.Underlying.InherentGrants);
        Assert.Equal("fire", composed.Parameters!["damageType"].GetString());
        Assert.Equal("cone", composed.Parameters["area"].GetProperty("shape").GetString());
        Assert.Equal(NativeOriginAdjudication.Supported, composed.Authorization!.Adjudication);
        Assert.Equal(canonical, composed.Authorization.NativeRuleReference);
        Assert.Equal(2, composed.SourceCandidateIds.Count);
        Assert.Equal(red.Id, composed.ParameterSources!["damageType"]);
        Assert.Equal(bronze.Id, composed.ParameterSources["area"]);

        // Save/load retains the exact composed state and its field-level
        // source evidence; a UI does not need to replay ancestral choices.
        var persisted = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(authoredResult.Profile));
        var restored = Assert.Single(persisted!.Underlying.InherentGrants);
        Assert.Equal("fire", restored.Parameters!["damageType"].GetString());
        Assert.Equal("cone", restored.Parameters["area"].GetProperty("shape").GetString());
        Assert.Equal(red.Id, restored.ParameterSources!["damageType"]);
        Assert.Equal(bronze.Id, restored.ParameterSources["area"]);

        // Authored recombination is tied to a real canonical trait rule,
        // not a caller-supplied relationship label.
        var unrelatedRule = Evaluate(authored with
        {
            Relationships = [new("breath-compose",
                NativeOriginRelationshipKind.Substitution, [red.Id, bronze.Id],
                AuthoredRuleReference: "trait.shared.low-light-vision")],
            Resolutions = [resolution with
            {
                Basis = new(NativeOriginAdjudication.Supported,
                    NativeRuleReference: "trait.shared.low-light-vision")
            }]
        }, new TestAuthority(), false);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, unrelatedRule.Status);
        Assert.True(unrelatedRule.Profile is null ||
            unrelatedRule.Profile.Underlying.InherentGrants.Count == 0);

        // Additional biological contributors can offer the same mechanic
        // without providing another use pool or altering the chosen fields.
        var threeSources = authored with
        {
            Contributors = authored.Contributors.Concat(
                [Contributor("extra-red", "ancestry.dragonborn.red")]).ToArray()
        };
        var third = Evaluate(threeSources).EvaluatedProposal.Candidates.Single(x =>
            x.ContributorId == "extra-red" &&
            x.Identity.Occurrence.Source.Id == "species.dragonborn" &&
            x.Identity.Occurrence.OccurrencePath == "/traits/2");
        var threeChosen = threeSources with
        {
            Manifestations = threeSources.Manifestations.Concat(
                [new NativeOriginManifestation("extra-breath", third.Id)]).ToArray(),
            TableDecisions = threeSources.TableDecisions!.Concat(
                [Decision("allow-extra", "candidate", third.Id,
                    new { eligibility = "eligible" })]).ToArray(),
            Relationships = [new("breath-compose",
                NativeOriginRelationshipKind.Substitution,
                [red.Id, bronze.Id, third.Id], AuthoredRuleReference: canonical)]
        };
        var threeResult = Evaluate(threeChosen, new TestAuthority(), false);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, threeResult.Status);
        var threeBreath = Assert.Single(threeResult.Profile!.Underlying.InherentGrants);
        Assert.Equal(3, threeBreath.SourceCandidateIds.Count);
        Assert.Equal("fire", threeBreath.Parameters!["damageType"].GetString());
        Assert.Equal("cone", threeBreath.Parameters["area"].GetProperty("shape").GetString());
        Assert.Equal("proficiency-bonus", threeBreath.EffectiveValue!.Value
            .GetProperty("uses").GetProperty("value").GetString());

        // A player cannot forge a field's provenance or silently choose a value
        // outside the contributors' exact configured offers.
        var forged = Evaluate(selected with
        {
            Resolutions = [resolution with
            {
                ReplacementParameterSources = new Dictionary<string, string>(sourceMap)
                {
                    ["area"] = red.Id
                }
            }]
        }, new TestAuthority(), false);
        // Invalid proposal evidence and unresolved reconciliation both fail
        // closed; neither is an authorization to construct a playable grant.
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, forged.Status);
        Assert.True(forged.Profile is null ||
            forged.Profile.Underlying.InherentGrants.Count == 0);
        Assert.Contains(forged.Issues, x => x.Code == "relationship-action-unresolved");

        var unauthorized = Evaluate(selected with
        {
            TableDecisions = selected.TableDecisions!.Where(x =>
                x.Id != decision.Id).ToArray()
        }, new TestAuthority(), false);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, unauthorized.Status);
        Assert.True(unauthorized.Profile is null ||
            unauthorized.Profile.Underlying.InherentGrants.Count == 0);
    }

    [Fact]
    public void PlayerSelectedBiologicalNativeOffersConstructMixedOriginWithoutDmDecisions()
    {
        var proposal = Proposal("first-party-human-elf", NativeOriginIntent.Mixed,
            contributors:
            [
                Contributor("human", "species.human"),
                Contributor("elf", "species.elf")
            ]);
        var offers = Evaluate(proposal).EvaluatedProposal.Candidates;
        var type = offers.Single(x => x.ContributorId == "elf" &&
            x.Identity.IntrinsicSlotId == "creatureType");
        var speed = offers.Single(x => x.ContributorId == "elf" &&
            x.Identity.IntrinsicSlotId == "walkingSpeedFeet");
        var humanSize = offers.Single(x => x.ContributorId == "human" &&
            x.Identity.Occurrence.OccurrencePath == "/sizeChoice");
        var sight = offers.Single(x => x.ContributorId == "elf" &&
            x.Identity.MechanicalDefinitionId == "trait.shared.low-light-vision");
        Assert.All([type, speed, humanSize, sight], x =>
        {
            Assert.Equal(NativeOriginEligibility.Eligible, x.SelectionBasis.Eligibility);
            Assert.Equal("rule.creature-origination", x.SelectionBasis.RuleReference);
        });

        var proposalWithChoices = proposal with
        {
            Manifestations =
            [
                new("fey-type", type.Id, SelectedValue: type.OfferedValue),
                new("elf-speed", speed.Id, SelectedValue: speed.OfferedValue),
                new("human-medium", humanSize.Id,
                    Choices: [new("size", ["medium"])]),
                new("elf-sight", sight.Id)
            ]
        };
        var result = Evaluate(proposalWithChoices, defaults: false);
        Assert.True(result.Status == NativeOriginConstructionStatus.Authorized,
            "Actual: " + result.Status + " issues: " + string.Join(" ; ",
                result.Issues.Select(x => x.Code + ":" + x.Path + ":" + x.Detail)));
        Assert.Null(result.Profile!.Underlying.SpeciesId);
        Assert.Null(result.Profile.Underlying.AncestryId);
        Assert.Equal(NativeOriginAdjudication.Supported,
            result.Profile.Adjudication.Adjudication);
        Assert.Equal(4, result.Profile.Underlying.InherentGrants.Count);
        Assert.All(result.Profile.Underlying.InherentGrants, grant =>
        {
            Assert.Equal(NativeOriginAdjudication.Supported,
                grant.Authorization!.Adjudication);
            Assert.Equal("rule.creature-origination",
                grant.Authorization.NativeRuleReference);
        });
        var scalars = NativeOriginApprovedMixedIntrinsicProjector.Project(result);
        Assert.Equal("resolved", scalars.Status);
        Assert.Equal("fey", scalars.CreatureType!.Value);
        Assert.Equal("medium", scalars.Size!.Value);
        Assert.Equal(30, scalars.WalkingSpeedFeet!.Value);
        Assert.Equal("species.human", scalars.Size.SourceId);
        Assert.Equal("unresolved", NativeOriginIntrinsicProjector.Project(result).Status);

        var vision = NativeOriginVisionProjector.ProjectApprovedMixed(Catalog(), result);
        Assert.Equal("resolved", vision.Status);
        Assert.Equal("rule.low-light-vision", vision.LowLightVision!.RuleId);
        Assert.Equal("species.elf", vision.LowLightVision.SourceId);

        var saved = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(result.Profile));
        Assert.Equal(4, saved!.Current.InherentGrants.Count);
        Assert.Equal(NativeOriginEventStage.Completed,
            Assert.Single(saved.History).Stage);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void BiologicalOffersAreNeverAutoManifestedAndRequireCompleteFoundation()
    {
        var proposal = Proposal("incomplete-biological", NativeOriginIntent.Mixed,
            contributors: [Contributor("elf", "species.elf")]);
        var offered = Evaluate(proposal).EvaluatedProposal.Candidates;
        var sight = offered.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.shared.low-light-vision");
        Assert.Equal(NativeOriginEligibility.Eligible, sight.SelectionBasis.Eligibility);
        var blank = Evaluate(proposal);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, blank.Status);
        Assert.Empty(blank.Profile!.Underlying.InherentGrants);

        var incomplete = Evaluate(proposal with
        {
            Manifestations = [new("selected-sight", sight.Id)]
        }, defaults: false);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, incomplete.Status);
        Assert.Contains(incomplete.Issues, x =>
            x.Code == "biological-foundation-incomplete");
        Assert.Single(incomplete.Profile!.Underlying.InherentGrants);
        Assert.Equal(NativeOriginEventStage.Pending,
            Assert.Single(incomplete.Profile.History).Stage);
    }

    [Fact]
    public void BiologicalPolicyLeavesUnmarkedTraitsAndNonparentInputsUnauthorized()
    {
        var proposal = Proposal("biological-boundary", NativeOriginIntent.Mixed,
            contributors: [Contributor("elf", "species.elf")]);
        var offered = Evaluate(proposal).EvaluatedProposal.Candidates;
        var unmarked = offered.Single(x =>
            x.Identity.Occurrence.Source.Id == "species.elf" &&
            x.Identity.Occurrence.OccurrencePath == "/traits/1");
        Assert.Equal(NativeOriginEligibility.Unreviewed,
            unmarked.SelectionBasis.Eligibility);
        var rejected = Evaluate(proposal with
        {
            Manifestations = [new("unsupported-senses", unmarked.Id)]
        }, defaults: false);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, rejected.Status);
        Assert.DoesNotContain(rejected.Profile?.Underlying.InherentGrants ?? [],
            x => x.Identity.Occurrence.OccurrencePath == "/traits/1");

        var material = proposal with
        {
            Contributors = [new NativeOriginContributor("elf",
                new(NativeOriginInputKind.Material),
                new(NativeOriginSourceKind.Native, "species.elf"))]
        };
        var materialResult = Evaluate(material).EvaluatedProposal.Candidates;
        Assert.DoesNotContain(materialResult, x =>
            x.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible);

        var construct = proposal with
        {
            Method = new("rule.creature-origination", "construct-assembly")
        };
        var constructResult = Evaluate(construct).EvaluatedProposal.Candidates;
        Assert.DoesNotContain(constructResult, x =>
            x.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible);

        var restricted = Proposal("restricted-biological", NativeOriginIntent.Mixed,
            contributors: [Contributor("parent", "species.dragonborn")]);
        var restrictedOffers = Evaluate(restricted).EvaluatedProposal.Candidates;
        Assert.DoesNotContain(restrictedOffers, x =>
            x.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible);
    }

    [Fact]
    public void SameBiologicalVisionFromThreeContributorsIsOneNativeGrant()
    {
        var proposal = Proposal("three-elves", NativeOriginIntent.Mixed, contributors:
        [
            Contributor("elf-1", "species.elf"),
            Contributor("elf-2", "species.elf"),
            Contributor("elf-3", "species.elf")
        ]);
        var offers = Evaluate(proposal).EvaluatedProposal.Candidates;
        var foundations = offers.Where(x => x.ContributorId == "elf-1" &&
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet")
            .ToArray();
        var vision = offers.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.shared.low-light-vision")
            .ToArray();
        Assert.Equal(3, foundations.Length);
        Assert.Equal(3, vision.Length);
        var chosen = foundations.Concat(vision).ToArray();
        var result = Evaluate(proposal with
        {
            Manifestations = chosen.Select((x, i) =>
                new NativeOriginManifestation("choice-" + i, x.Id,
                    SelectedValue: x.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic
                        ? x.OfferedValue : null)).ToArray()
        }, defaults: false);
        Assert.True(result.Status == NativeOriginConstructionStatus.Authorized,
            "Actual: " + result.Status + " issues: " + string.Join(" ; ",
                result.Issues.Select(x => x.Code + ":" + x.Path + ":" + x.Detail)));
        Assert.Equal(4, result.Profile!.Current.InherentGrants.Count);
        var sight = Assert.Single(result.Profile.Current.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.shared.low-light-vision");
        Assert.Equal(3, sight.SourceCandidateIds.Count);
        Assert.Equal(NativeOriginAdjudication.Supported, sight.Authorization!.Adjudication);
        Assert.Equal("resolved",
            NativeOriginVisionProjector.ProjectApprovedMixed(Catalog(), result).Status);
    }

    [Fact]
    public void PlayerCanChooseAnyAuthoredRedBronzeBreathCombinationWithoutDmRuling()
    {
        var baseProposal = Proposal("biological-dragonborn", NativeOriginIntent.Mixed,
            contributors:
            [
                Contributor("red", "ancestry.dragonborn.red"),
                Contributor("bronze", "ancestry.dragonborn.bronze")
            ]);
        var offers = Evaluate(baseProposal).EvaluatedProposal.Candidates;
        var foundation = offers.Where(x => x.ContributorId == "red" &&
            x.Identity.Occurrence.Source.Id == "species.dragonborn" &&
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet")
            .ToArray();
        Assert.Equal(3, foundation.Length);
        var breaths = offers.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.dragonborn.breath-weapon")
            .ToArray();
        Assert.Equal(2, breaths.Length);
        var red = breaths.Single(x => x.ContributorId == "red");
        var bronze = breaths.Single(x => x.ContributorId == "bronze");
        Assert.All(breaths, x =>
            Assert.Equal(NativeOriginEligibility.Eligible, x.SelectionBasis.Eligibility));
        var fixedSelections = foundation.Select((x, i) =>
            new NativeOriginManifestation("foundation-" + i, x.Id,
                SelectedValue: x.OfferedValue)).ToArray();

        foreach (var damageSource in breaths)
        foreach (var areaSource in breaths)
        {
            var paramsSelected = new Dictionary<string, JsonElement>
            {
                ["damageType"] = damageSource.OfferedParameters!["damageType"],
                ["area"] = areaSource.OfferedParameters!["area"]
            };
            var fieldSources = new Dictionary<string, string>
            {
                ["damageType"] = damageSource.Id,
                ["area"] = areaSource.Id
            };
            var result = Evaluate(baseProposal with
            {
                Manifestations = fixedSelections.Concat(
                [
                    new("red-breath", red.Id),
                    new("bronze-breath", bronze.Id)
                ]).ToArray(),
                Relationships = [new("one-breath",
                    NativeOriginRelationshipKind.Substitution,
                    [red.Id, bronze.Id],
                    AuthoredRuleReference: "trait.dragonborn.breath-weapon")],
                Resolutions = [new("one-breath", NativeOriginResolutionAction.Replace,
                    new(NativeOriginAdjudication.Supported,
                        NativeRuleReference: "trait.dragonborn.breath-weapon"),
                    red.Id, paramsSelected, fieldSources)]
            }, defaults: false);
            Assert.True(result.Status == NativeOriginConstructionStatus.Authorized,
                "Issues: " + string.Join("; ", result.Issues.Select(x => x.Code)));
            Assert.Equal(4, result.Profile!.Current.InherentGrants.Count);
            var breath = Assert.Single(result.Profile.Current.InherentGrants, x =>
                x.Identity.MechanicalDefinitionId == "trait.dragonborn.breath-weapon");
            Assert.Equal(paramsSelected["damageType"].GetString(),
                breath.Parameters!["damageType"].GetString());
            Assert.Equal(paramsSelected["area"].GetRawText(),
                breath.Parameters["area"].GetRawText());
            Assert.Equal(2, breath.SourceCandidateIds.Count);
            Assert.Equal(damageSource.Id, breath.ParameterSources!["damageType"]);
            Assert.Equal(areaSource.Id, breath.ParameterSources["area"]);
            Assert.Equal("proficiency-bonus",
                breath.EffectiveValue!.Value.GetProperty("uses").GetProperty("value").GetString());
            Assert.DoesNotContain(result.Profile.Current.InherentGrants, x =>
                x.Identity.MechanicalDefinitionId == "trait.dragonborn.draconic-resistance");
            Assert.Equal("resolved",
                NativeOriginApprovedMixedIntrinsicProjector.Project(result).Status);
        }
    }

    [Theory]
    [InlineData("ancestry.goliath.cloud", "trait.goliath.cloud.clouds-jaunt",
        "trait.goliath.cloud.cloud-strike")]
    [InlineData("ancestry.goliath.stone", "trait.goliath.stone.stones-endurance",
        "trait.goliath.stone.stone-strike")]
    [InlineData("ancestry.goliath.storm", "trait.goliath.storm.storms-thunder",
        "trait.goliath.storm.storm-strike")]
    public void AuthoredSharedUseStrikeRequiresSelectedOwnerAndNeverGetsIndependentPool(
        string ancestry, string ownerId, string strikeId)
    {
        var baseProposal = Proposal("shared-use-" + ancestry, NativeOriginIntent.Mixed,
            contributors: [Contributor("source", ancestry)]);
        var offers = Evaluate(baseProposal).EvaluatedProposal.Candidates;
        var owner = Assert.Single(offers, x =>
            x.ContributorId == "source" &&
            x.Identity.MechanicalDefinitionId == ownerId);
        var strike = Assert.Single(offers, x =>
            x.ContributorId == "source" &&
            x.Identity.MechanicalDefinitionId == strikeId);
        var aptitude = Assert.Single(offers, x =>
            x.ContributorId == "source" &&
            x.Identity.MechanicalDefinitionId == "trait.goliath.giant-ancestry-aptitude");
        var foundations = offers.Where(x => x.ContributorId == "source" &&
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet")
            .ToArray();
        Assert.Equal(3, foundations.Length);
        var baseSelections = foundations.Select((x, i) =>
            new NativeOriginManifestation("foundation-" + i, x.Id,
                SelectedValue: x.OfferedValue)).ToArray();
        var decisions = new[]
        {
            Decision("allow-owner", "candidate", owner.Id, new { eligibility = "eligible" }),
            Decision("allow-strike", "candidate", strike.Id, new { eligibility = "eligible" }),
            Decision("allow-aptitude", "candidate", aptitude.Id, new { eligibility = "eligible" })
        };
        var selectedStrike = new NativeOriginManifestation("strike", strike.Id);
        var aptitudeChoice = new NativeOriginManifestation("aptitude", aptitude.Id,
            Choices: [new("abilityChoice", ["constitution"])]);

        var missing = Evaluate(baseProposal with
        {
            Manifestations = baseSelections.Append(aptitudeChoice)
                .Append(selectedStrike).ToArray(),
            TableDecisions = decisions
        }, new TestAuthority(), false);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, missing.Status);
        Assert.Contains(missing.Issues,
            x => x.Code == "origin-shared-resource-owner-missing");
        Assert.DoesNotContain(missing.Profile?.Underlying.InherentGrants ?? [],
            x => x.Identity.MechanicalDefinitionId == strikeId);

        var complete = Evaluate(baseProposal with
        {
            Manifestations = baseSelections.Append(aptitudeChoice).Append(selectedStrike)
                .Append(new("resource-owner", owner.Id)).ToArray(),
            TableDecisions = decisions
        }, new TestAuthority(), false);
        Assert.True(complete.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", complete.Issues.Select(x => x.Code + ":" + x.Detail)));
        var granted = complete.Profile!.Underlying.InherentGrants;
        Assert.Equal(6, granted.Count);
        var parent = Assert.Single(granted, x => x.Identity.MechanicalDefinitionId == ownerId);
        Assert.Equal("constitution", Assert.Single(granted, x =>
            x.Identity.MechanicalDefinitionId == "trait.goliath.giant-ancestry-aptitude")
            .ResolvedChoices!.Single().SelectedOptionIds.Single());
        var dependent = Assert.Single(granted, x => x.Identity.MechanicalDefinitionId == strikeId);
        Assert.Equal(parent.Id, dependent.ResourceOwners!["uses"]);
        Assert.Equal("constitution",
            dependent.Parameters!["giantAncestryAbility"].GetString());
        Assert.Equal(1, dependent.ResourceOwners.Count);
        Assert.True(parent.EffectiveValue!.Value.TryGetProperty("uses", out var ownerPool));
        Assert.Equal("proficiency-bonus", ownerPool.GetProperty("value").GetString());
        Assert.False(dependent.EffectiveValue!.Value.TryGetProperty("uses", out _));
        Assert.Equal(false, dependent.EffectiveValue!.Value
            .GetProperty("extraUsesGranted").GetBoolean());
        Assert.Equal("rule.creature-origination", granted.Single(x =>
            x.Identity.IntrinsicSlotId == "creatureType").Authorization?.NativeRuleReference);
        var restored = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(complete.Profile));
        var restoredDependent = Assert.Single(restored!.Underlying.InherentGrants,
            x => x.Identity.MechanicalDefinitionId == strikeId);
        Assert.Equal(parent.Id, restoredDependent.ResourceOwners!["uses"]);
        Assert.Empty(complete.Issues);

        // Saving a profile does not make an arbitrary resource-owner claim
        // authoritative. Profile validation rechecks the canonical relationship.
        var forgedConfiguration = complete.Profile.Underlying with
        {
            InherentGrants = granted.Select(x =>
                x.Id == dependent.Id
                    ? x with
                    {
                        ResourceOwners = new Dictionary<string, string>
                        {
                            ["uses"] = "grant:not-actually-selected"
                        }
                    }
                    : x).ToArray()
        };
        var forgedProfile = complete.Profile with
        {
            Underlying = forgedConfiguration,
            Current = forgedConfiguration
        };
        var review = new NativeOriginContractValidator(Catalog()).ReviewProfile(
            forgedProfile, complete.EvaluatedProposal);
        Assert.Contains(review.Issues, x => x.Code == "profile-resource-owner-invalid");

        var omittedConfiguration = complete.Profile.Underlying with
        {
            InherentGrants = granted.Select(x => x.Id == dependent.Id
                ? x with { ResourceOwners = null }
                : x).ToArray()
        };
        var omitted = new NativeOriginContractValidator(Catalog()).ReviewProfile(
            complete.Profile with
            {
                Underlying = omittedConfiguration,
                Current = omittedConfiguration
            }, complete.EvaluatedProposal);
        Assert.Contains(omitted.Issues, x => x.Code == "profile-resource-owner-required");
    }

    [Fact]
    public void SharedUseOwnerCannotBeBorrowedFromAnotherBiologicalContributor()
    {
        var baseProposal = Proposal("separate-resource-lineages", NativeOriginIntent.Mixed,
            contributors:
            [
                Contributor("owner-source", "ancestry.goliath.cloud"),
                Contributor("strike-source", "ancestry.goliath.cloud")
            ]);
        var offers = Evaluate(baseProposal).EvaluatedProposal.Candidates;
        var owner = Assert.Single(offers, x =>
            x.ContributorId == "owner-source" &&
            x.Identity.MechanicalDefinitionId == "trait.goliath.cloud.clouds-jaunt");
        var strike = Assert.Single(offers, x =>
            x.ContributorId == "strike-source" &&
            x.Identity.MechanicalDefinitionId == "trait.goliath.cloud.cloud-strike");
        var aptitude = Assert.Single(offers, x =>
            x.ContributorId == "strike-source" &&
            x.Identity.MechanicalDefinitionId == "trait.goliath.giant-ancestry-aptitude");
        var foundations = offers.Where(x => x.ContributorId == "owner-source" &&
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet")
            .ToArray();
        var result = Evaluate(baseProposal with
        {
            Manifestations = foundations.Select((x, i) =>
                new NativeOriginManifestation("foundation-" + i, x.Id,
                    SelectedValue: x.OfferedValue))
                .Append(new("selected-owner", owner.Id))
                .Append(new("source-aptitude", aptitude.Id,
                    Choices: [new("abilityChoice", ["strength"])]))
                .Append(new("selected-strike", strike.Id)).ToArray(),
            TableDecisions =
            [
                Decision("owner-allow", "candidate", owner.Id,
                    new { eligibility = "eligible" }),
                Decision("aptitude-allow", "candidate", aptitude.Id,
                    new { eligibility = "eligible" }),
                Decision("strike-allow", "candidate", strike.Id,
                    new { eligibility = "eligible" })
            ]
        }, new TestAuthority(), false);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Contains(result.Issues, x =>
            x.Code == "origin-shared-resource-owner-missing");
        Assert.DoesNotContain(result.Profile?.Underlying.InherentGrants ?? [],
            x => x.Identity.MechanicalDefinitionId == strike.Identity.MechanicalDefinitionId);
        Assert.Contains(result.Profile!.Underlying.InherentGrants,
            x => x.Identity.MechanicalDefinitionId == owner.Identity.MechanicalDefinitionId);
    }

    [Fact]
    public void CloudyEscapeRetainsItsExplicitIndependentUsesRatherThanSharingJaunt()
    {
        var baseProposal = Proposal("independent-cloud-resource", NativeOriginIntent.Mixed,
            contributors: [Contributor("source", "ancestry.goliath.cloud")]);
        var offered = Evaluate(baseProposal).EvaluatedProposal.Candidates;
        var independent = Assert.Single(offered, x =>
            x.Identity.MechanicalDefinitionId == "trait.goliath.cloud.cloudy-escape");
        var foundations = offered.Where(x =>
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet")
            .ToArray();
        var result = Evaluate(baseProposal with
        {
            Manifestations = foundations.Select((x, i) =>
                new NativeOriginManifestation("foundation-" + i, x.Id,
                    SelectedValue: x.OfferedValue))
                .Append(new("independent", independent.Id)).ToArray(),
            TableDecisions =
            [
                Decision("independent-allowed", "candidate", independent.Id,
                    new { eligibility = "eligible" })
            ]
        }, new TestAuthority(), false);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var grant = Assert.Single(result.Profile!.Underlying.InherentGrants,
            x => x.Identity.MechanicalDefinitionId ==
                "trait.goliath.cloud.cloudy-escape");
        Assert.True(grant.ResourceOwners is null || grant.ResourceOwners.Count == 0);
        Assert.Equal("clouds-jaunt",
            grant.EffectiveValue!.Value.GetProperty("independentUsesFrom").GetString());
        Assert.Equal("proficiency-bonus",
            grant.EffectiveValue!.Value.GetProperty("uses")
                .GetProperty("value").GetString());
    }

    [Theory]
    [InlineData("ancestry.goliath.cloud", "trait.goliath.cloud.cloud-strike",
        "trait.goliath.cloud.clouds-jaunt")]
    [InlineData("ancestry.goliath.fire", "trait.goliath.fire.searing-ignition", null)]
    [InlineData("ancestry.goliath.frost", "trait.goliath.frost.frosts-chill", null)]
    [InlineData("ancestry.goliath.frost", "trait.goliath.frost.frigid-retaliation", null)]
    [InlineData("ancestry.goliath.hill", "trait.goliath.hill.hills-tumble", null)]
    [InlineData("ancestry.goliath.stone", "trait.goliath.stone.stone-strike",
        "trait.goliath.stone.stones-endurance")]
    [InlineData("ancestry.goliath.stone", "trait.goliath.stone.stone-throw", null)]
    [InlineData("ancestry.goliath.storm", "trait.goliath.storm.storm-strike",
        "trait.goliath.storm.storms-thunder")]
    [InlineData("ancestry.goliath.storm", "trait.goliath.storm.maelstrom-aura", null)]
    public void GiantAbilityChoiceBindsIntoEveryAuthoredDependentFeature(
        string ancestry, string featureId, string? usesOwnerId)
    {
        var proposal = Proposal("giant-choice-" + featureId, NativeOriginIntent.Mixed,
            contributors: [Contributor("parent", ancestry)]);
        var offered = Evaluate(proposal).EvaluatedProposal.Candidates;
        var aptitude = Assert.Single(offered, x =>
            x.Identity.MechanicalDefinitionId == "trait.goliath.giant-ancestry-aptitude");
        var requirement = Assert.Single(aptitude.RequiredChoices!);
        Assert.Equal("abilityChoice", requirement.Id);
        Assert.Equal(NativeOriginChoiceTiming.Creation, requirement.Timing);
        Assert.Equal(new[] { "strength", "constitution" }, requirement.FiniteOptions);
        var feature = Assert.Single(offered, x => x.Identity.MechanicalDefinitionId == featureId);
        var owner = usesOwnerId is null ? null : Assert.Single(offered,
            x => x.Identity.MechanicalDefinitionId == usesOwnerId);
        var physical = offered.Where(x =>
            x.ContributorId == "parent" &&
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet")
            .ToArray();
        Assert.Equal(3, physical.Length);

        foreach (var chosenAbility in new[] { "strength", "constitution" })
        {
            var selected = physical.Select((x, i) =>
                new NativeOriginManifestation("physical-" + i, x.Id,
                    SelectedValue: x.OfferedValue)).ToList();
            selected.Add(new("aptitude", aptitude.Id,
                Choices: [new("abilityChoice", [chosenAbility])]));
            selected.Add(new("feature", feature.Id));
            if (owner is not null) selected.Add(new("shared-uses-owner", owner.Id));

            var decisions = new List<NativeTableRuleDecision>
            {
                Decision("aptitude-allowed", "candidate", aptitude.Id,
                    new { eligibility = "eligible" }),
                Decision("feature-allowed", "candidate", feature.Id,
                    new { eligibility = "eligible" })
            };
            if (owner is not null)
                decisions.Add(Decision("owner-allowed", "candidate", owner.Id,
                    new { eligibility = "eligible" }));
            var completed = Evaluate(proposal with
            {
                Manifestations = selected, TableDecisions = decisions
            }, new TestAuthority(), false);
            Assert.True(completed.Status == NativeOriginConstructionStatus.Authorized,
                "Actual: " + completed.Status + "; issues: " +
                string.Join(" | ", completed.Issues.Select(x => x.Code + ":" + x.Detail)));
            var bound = Assert.Single(completed.Profile!.Underlying.InherentGrants,
                x => x.Identity.MechanicalDefinitionId == featureId);
            Assert.Equal(chosenAbility, bound.Parameters!["giantAncestryAbility"].GetString());
            Assert.Equal(aptitude.Id, bound.ParameterSources!["giantAncestryAbility"]);
            if (owner is not null)
                Assert.Equal("grant:shared-uses-owner", bound.ResourceOwners!["uses"]);
            var restored = JsonSerializer.Deserialize<NativeOriginProfile>(
                JsonSerializer.Serialize(completed.Profile));
            var durable = Assert.Single(restored!.Underlying.InherentGrants,
                x => x.Identity.MechanicalDefinitionId == featureId);
            Assert.Equal(chosenAbility, durable.Parameters!["giantAncestryAbility"].GetString());
            Assert.Equal(aptitude.Id, durable.ParameterSources!["giantAncestryAbility"]);

            var validator = new NativeOriginContractValidator(Catalog());
            Assert.DoesNotContain(validator.ReviewProfile(completed.Profile,
                completed.EvaluatedProposal).Issues,
                x => x.Code == "profile-trait-choice-binding-invalid");

            var forgedParameters = completed.Profile.Underlying with
            {
                InherentGrants = completed.Profile.Underlying.InherentGrants
                    .Select(x => x.Id == bound.Id
                        ? x with
                        {
                            Parameters = new Dictionary<string, JsonElement>(
                                x.Parameters ?? new Dictionary<string, JsonElement>())
                            {
                                ["giantAncestryAbility"] =
                                    JsonSerializer.SerializeToElement(
                                        chosenAbility == "strength" ? "constitution" : "strength")
                            }
                        }
                        : x).ToArray()
            };
            Assert.Contains(validator.ReviewProfile(completed.Profile with
            {
                Underlying = forgedParameters, Current = forgedParameters
            }, completed.EvaluatedProposal).Issues,
                x => x.Code == "profile-trait-choice-binding-invalid");

            var forgedEvidence = completed.Profile.Underlying with
            {
                InherentGrants = completed.Profile.Underlying.InherentGrants
                    .Select(x => x.Id == bound.Id
                        ? x with
                        {
                            ParameterSources = new Dictionary<string, string>(
                                x.ParameterSources ?? new Dictionary<string, string>())
                            {
                                ["giantAncestryAbility"] = "candidate:unrelated-parent"
                            }
                        }
                        : x).ToArray()
            };
            Assert.Contains(validator.ReviewProfile(completed.Profile with
            {
                Underlying = forgedEvidence, Current = forgedEvidence
            }, completed.EvaluatedProposal).Issues,
                x => x.Code == "profile-trait-choice-binding-invalid");
        }
    }

    [Fact]
    public void ConsolidatedGiantAptitudeAttributesBoundModifierToActualContributor()
    {
        var original = Proposal("consolidated-giant-aptitude", NativeOriginIntent.Mixed,
            contributors:
            [
                Contributor("a-fire", "ancestry.goliath.fire"),
                Contributor("b-stone", "ancestry.goliath.stone")
            ]);
        var offers = Evaluate(original).EvaluatedProposal.Candidates;
        var fireApt = Assert.Single(offers, x =>
            x.ContributorId == "a-fire" &&
            x.Identity.MechanicalDefinitionId == "trait.goliath.giant-ancestry-aptitude");
        var stoneApt = Assert.Single(offers, x =>
            x.ContributorId == "b-stone" &&
            x.Identity.MechanicalDefinitionId == "trait.goliath.giant-ancestry-aptitude");
        var stoneThrow = Assert.Single(offers, x =>
            x.ContributorId == "b-stone" &&
            x.Identity.MechanicalDefinitionId == "trait.goliath.stone.stone-throw");
        var foundations = offers.Where(x => x.ContributorId == "a-fire" &&
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet")
            .Select((x, i) => new NativeOriginManifestation("foundation-" + i,
                x.Id, SelectedValue: x.OfferedValue)).ToArray();
        var permits = new[]
        {
            Decision("fire-aptitude-permit", "candidate", fireApt.Id,
                new { eligibility = "eligible" }),
            Decision("stone-aptitude-permit", "candidate", stoneApt.Id,
                new { eligibility = "eligible" }),
            Decision("stone-throw-permit", "candidate", stoneThrow.Id,
                new { eligibility = "eligible" })
        };
        var result = Evaluate(original with
        {
            Manifestations = foundations.Concat(
            [
                new("fire-aptitude", fireApt.Id,
                    Choices: [new("abilityChoice", ["strength"])]),
                new("stone-aptitude", stoneApt.Id,
                    Choices: [new("abilityChoice", ["strength"])]),
                new("stone-throw", stoneThrow.Id)
            ]).ToArray(),
            TableDecisions = permits
        }, new TestAuthority(), false);

        Assert.True(result.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", result.Issues.Select(x => x.Code + ":" + x.Detail)));
        var aptitude = Assert.Single(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.goliath.giant-ancestry-aptitude");
        Assert.Equal(2, aptitude.SourceCandidateIds.Count);
        Assert.Contains(fireApt.Id, aptitude.SourceCandidateIds);
        Assert.Contains(stoneApt.Id, aptitude.SourceCandidateIds);
        var thrown = Assert.Single(result.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.goliath.stone.stone-throw");
        Assert.Equal("strength", thrown.Parameters!["giantAncestryAbility"].GetString());
        Assert.Equal(stoneApt.Id, thrown.ParameterSources!["giantAncestryAbility"]);
        Assert.DoesNotContain(new NativeOriginContractValidator(Catalog())
            .ReviewProfile(result.Profile, result.EvaluatedProposal).Issues,
            x => x.Code == "profile-trait-choice-binding-invalid");

        // Different chosen aptitudes for one consolidated source mechanic do
        // not silently pick a winner or become two global ability selections.
        var conflict = Evaluate(original with
        {
            Manifestations = foundations.Concat(
            [
                new("fire-aptitude", fireApt.Id,
                    Choices: [new("abilityChoice", ["constitution"])]),
                new("stone-aptitude", stoneApt.Id,
                    Choices: [new("abilityChoice", ["strength"])]),
                new("stone-throw", stoneThrow.Id)
            ]).ToArray(),
            TableDecisions = permits
        }, new TestAuthority(), false);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, conflict.Status);
        Assert.Contains(conflict.Issues, x =>
            x.Code == "same-definition-different-manifestations");
        Assert.DoesNotContain(conflict.Profile?.Underlying.InherentGrants ?? [], x =>
            x.Identity.MechanicalDefinitionId == "trait.goliath.stone.stone-throw");
    }

    [Fact]
    public void GiantDependentFeatureRequiresAnExplicitValidContributorAptitude()
    {
        var proposal = Proposal("giant-aptitude-required", NativeOriginIntent.Mixed,
            contributors:
            [
                Contributor("aptitude-source", "ancestry.goliath.stone"),
                Contributor("feature-source", "ancestry.goliath.fire")
            ]);
        var offered = Evaluate(proposal).EvaluatedProposal.Candidates;
        var aptitude = Assert.Single(offered, x =>
            x.ContributorId == "aptitude-source" &&
            x.Identity.MechanicalDefinitionId == "trait.goliath.giant-ancestry-aptitude");
        var feature = Assert.Single(offered, x =>
            x.ContributorId == "feature-source" &&
            x.Identity.MechanicalDefinitionId == "trait.goliath.fire.searing-ignition");
        var physical = offered.Where(x =>
            x.ContributorId == "feature-source" &&
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet")
            .ToArray();
        var physicalChoices = physical.Select((x, i) =>
            new NativeOriginManifestation("physical-" + i, x.Id,
                SelectedValue: x.OfferedValue)).ToArray();
        var decisions = new[]
        {
            Decision("aptitude-allowed", "candidate", aptitude.Id,
                new { eligibility = "eligible" }),
            Decision("feature-allowed", "candidate", feature.Id,
                new { eligibility = "eligible" })
        };

        var missing = Evaluate(proposal with
        {
            Manifestations = physicalChoices.Append(new("feature", feature.Id)).ToArray(),
            TableDecisions = decisions
        }, new TestAuthority(), false);
        Assert.Contains(missing.Issues, x => x.Code == "origin-binding-choice-missing");
        Assert.DoesNotContain(missing.Profile?.Underlying.InherentGrants ?? [],
            x => x.Identity.MechanicalDefinitionId == feature.Identity.MechanicalDefinitionId);

        var otherContributor = Evaluate(proposal with
        {
            Manifestations = physicalChoices
                .Append(new("aptitude", aptitude.Id,
                    Choices: [new("abilityChoice", ["strength"])]))
                .Append(new("feature", feature.Id)).ToArray(),
            TableDecisions = decisions
        }, new TestAuthority(), false);
        Assert.Contains(otherContributor.Issues, x =>
            x.Code == "origin-binding-choice-missing");
        Assert.DoesNotContain(otherContributor.Profile?.Underlying.InherentGrants ?? [],
            x => x.Identity.MechanicalDefinitionId == feature.Identity.MechanicalDefinitionId);

        var invalid = Evaluate(proposal with
        {
            Manifestations = physicalChoices.Append(new("aptitude", aptitude.Id,
                    Choices: [new("abilityChoice", ["wisdom"])]))
                .Append(new("feature", feature.Id)).ToArray(),
            TableDecisions = decisions
        }, new TestAuthority(), false);
        Assert.Contains(invalid.Issues, x => x.Code is "choice-selection-invalid" or
            "dependent-choice-invalid");
        Assert.DoesNotContain(invalid.Profile?.Underlying.InherentGrants ?? [],
            x => x.Identity.MechanicalDefinitionId == feature.Identity.MechanicalDefinitionId);
    }

    [Fact]
    public void DirectStoneGoliathUsesOnePlayerSelectedAptitudeAcrossItsAbilities()
    {
        var proposal = Proposal("direct-stone-goliath",
            ancestry: "ancestry.goliath.stone");
        var preview = Evaluate(proposal).EvaluatedProposal;
        var aptitude = Assert.Single(preview.Candidates, x =>
            x.Identity.MechanicalDefinitionId == "trait.goliath.giant-ancestry-aptitude");
        Assert.Equal("species.goliath", aptitude.Identity.Occurrence.Source.Id);
        Assert.Equal(NativeOriginEligibility.Eligible, aptitude.SelectionBasis.Eligibility);

        var complete = Evaluate(proposal with
        {
            Manifestations =
            [
                new("native-aptitude", aptitude.Id,
                    Choices: [new("abilityChoice", ["strength"])])
            ]
        });
        Assert.True(complete.Status == NativeOriginConstructionStatus.Authorized,
            "Actual: " + complete.Status + "; " +
            string.Join(" | ", complete.Issues.Select(x => x.Code + ":" + x.Detail)));
        var grants = complete.Profile!.Underlying.InherentGrants;
        foreach (var name in new[] { "stone-strike", "stone-throw" })
        {
            var feature = Assert.Single(grants, x =>
                x.EffectiveValue?.ValueKind == JsonValueKind.Object &&
                x.EffectiveValue.Value.GetProperty("id").GetString() == name);
            Assert.Equal("strength", feature.Parameters!["giantAncestryAbility"].GetString());
            Assert.Equal(aptitude.Id, feature.ParameterSources!["giantAncestryAbility"]);
        }
        var strike = Assert.Single(grants, x =>
            x.Identity.MechanicalDefinitionId == "trait.goliath.stone.stone-strike");
        Assert.Equal("grant:auto:direct:ancestry.goliath.stone:/traits/0",
            strike.ResourceOwners!["uses"]);
        Assert.DoesNotContain(new NativeOriginContractValidator(Catalog())
            .ReviewProfile(complete.Profile, complete.EvaluatedProposal).Issues,
            x => x.Code == "profile-trait-choice-binding-invalid");
    }

    [Fact]
    public void NestedSpellTraitRetainsItsAlreadyManifestedParameterValue()
    {
        var source = Evaluate(Proposal("caster-parent", ancestry: "ancestry.elf.high"))
            .EvaluatedProposal;
        var ability = source.Candidates.Single(x =>
            x.Identity.IntrinsicSlotId == "spellcastingAbility");
        var cantrip = source.Candidates.Single(x =>
            x.Identity.Occurrence.Source.Id == "ancestry.elf.high" &&
            x.Identity.Occurrence.OccurrencePath == "/traits/0");
        var parent = Evaluate(Proposal("caster-parent", ancestry: "ancestry.elf.high",
            manifests:
            [
                new("ability", ability.Id,
                    Choices: [new("spellcastingAbility", ["intelligence"])]),
                new("cantrip", cantrip.Id,
                    Choices: [new("selection", ["spell.fire-bolt"],
                        "verified-catalog-proof")])
            ]), new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, parent.Status);
        var feature = parent.Profile!.Current.InherentGrants.Single(x =>
            x.Identity.Occurrence.Source.Id == "ancestry.elf.high" &&
            x.Identity.Occurrence.OccurrencePath == "/traits/1");
        Assert.Equal("intelligence", feature.Parameters!["spellcastingAbility"].GetString());

        var inherited = new NativeOriginContributor("parent",
            new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.ManifestedProfile, parent.Profile.Id),
            new("species.elf", "ancestry.elf.high", [feature],
                parent.Profile.Current.ProfileId),
            ["species.human", "species.elf"]);
        var proposal = Proposal("caster-child", NativeOriginIntent.Mixed,
            contributors: [inherited]);
        var offer = Assert.Single(Evaluate(proposal).EvaluatedProposal.Candidates);
        var result = Evaluate(proposal with
        {
            Manifestations = [new("inherited-spell", offer.Id)],
            TableDecisions = [Decision("transmission", "candidate", offer.Id,
                new { eligibility = "eligible" })]
        }, new TestAuthority(), false);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var effective = Assert.Single(result.Profile!.Underlying.InherentGrants);
        Assert.Equal("intelligence", effective.Parameters!["spellcastingAbility"].GetString());
        Assert.Equal("ancestry.elf.high", effective.Identity.Occurrence.Source.Id);
    }

}
