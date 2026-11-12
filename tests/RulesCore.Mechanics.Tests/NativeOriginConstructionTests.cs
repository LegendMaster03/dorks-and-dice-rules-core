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
        var grant = Assert.Single(result.Profile!.Underlying.InherentGrants);
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
}
