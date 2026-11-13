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
