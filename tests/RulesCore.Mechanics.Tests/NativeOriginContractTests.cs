using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Contract coverage uses deliberately different Origin sources and methods.
/// Passing these tests proves expression and structural validation, not that
/// the Phase 2 evaluator or table authorization service is implemented.
/// </summary>
public sealed class NativeOriginContractTests
{
    private static NativeOriginMethodReference Method(string id = "biological") =>
        new("rule.creature-origination", id);

    private static NativeOriginSourceReference Source(string id = "species.human",
        NativeOriginSourceKind kind = NativeOriginSourceKind.Native) => new(kind, id);

    private static NativeOriginCharacteristicIdentity Trait(string label,
        string source = "species.human", string? definition = null,
        NativeOriginSourceKind kind = NativeOriginSourceKind.Native) =>
        new(NativeOriginCharacteristicKind.Trait,
            new(Source(source, kind), "/definition/traits/" + label),
            definition ?? "mechanic." + label);

    private static NativeOriginCharacteristicIdentity Intrinsic(string slot,
        string source = "species.human") =>
        new(NativeOriginCharacteristicKind.Intrinsic,
            new(Source(source), "/definition/" + slot),
            IntrinsicSlotId: slot);

    private static NativeOriginCandidate Candidate(string id,
        NativeOriginCharacteristicIdentity identity, string? contributor = null,
        IReadOnlyList<NativeOriginChoiceRequirement>? choices = null,
        IReadOnlyList<string>? unresolved = null) =>
        new(id, identity, new(NativeOriginEligibility.Eligible, "rule.test-origin"),
            contributor, RequiredChoices: choices, UnresolvedEvidence: unresolved);

    private static NativeOriginManifestation Manifest(string id, string candidate,
        JsonElement? value = null, IReadOnlyList<NativeOriginChoiceSelection>? choices = null) =>
        new(id, candidate, value, Choices: choices);

    private static NativeOriginEffectiveGrant Grant(string id,
        NativeOriginManifestation manifest, NativeOriginCandidate candidate,
        params string[] sourceCandidates) =>
        new(id, manifest.Id, candidate.Identity,
            sourceCandidates.Length == 0 ? [candidate.Id] : sourceCandidates);

    private static NativeOriginContributor Parent(string id, string species) =>
        new(id, new(NativeOriginInputKind.BiologicalParent), Source(species));

    private static NativeOriginProposal Proposal(string id, string method = "biological",
        NativeOriginIntent intent = NativeOriginIntent.Direct,
        IReadOnlyList<NativeOriginContributor>? contributors = null,
        IReadOnlyList<NativeOriginCandidate>? candidates = null,
        IReadOnlyList<NativeOriginManifestation>? manifestations = null) =>
        new(id, intent, Method(method), contributors ?? [], candidates ?? [],
            manifestations ?? []);

    private static NativeOriginProfile Profile(NativeOriginProposal proposal,
        IReadOnlyList<NativeOriginEffectiveGrant>? inherited = null,
        IReadOnlyList<NativeOriginEffectiveGrant>? independent = null,
        IReadOnlyList<NativeOriginHistoryEvent>? history = null,
        NativeOriginDecisionBasis? basis = null) =>
        new("profile-" + proposal.Id,
            new(proposal.SelectedSpeciesId, proposal.SelectedAncestryId, inherited ?? []),
            new(proposal.SelectedSpeciesId, proposal.SelectedAncestryId, inherited ?? []),
            independent ?? [], history ?? [],
            basis ?? new(NativeOriginAdjudication.Supported, "rule.test-origin"),
            proposal.Id);

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "rc-origin-contract-" + Guid.NewGuid().ToString("N"));
        public NativeOriginContractValidator Validator { get; }

        public Fixture()
        {
            var package = Path.Combine(_root, "first-party-001");
            Directory.CreateDirectory(Path.Combine(package, "native"));
            File.WriteAllText(Path.Combine(package, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            File.WriteAllText(Path.Combine(package, "native", "origination.json"),
                """
                {"id":"rule.creature-origination","kind":"rule","name":"Origination fixture",
                 "methodDefinitions":[
                    {"id":"biological","name":"Biological"},
                    {"id":"plant-reproduction","name":"Plant reproduction"},
                    {"id":"construct-assembly","name":"Assembly"},
                    {"id":"spawning","name":"Spawning"},
                    {"id":"rite-of-rebirth","name":"Rite","specializes":"transformation",
                     "stage":"post-origination","createsNewIndividual":false,
                     "operation":"replace-complete-species-and-ancestry-configuration"},
                    {"id":"supernatural-reconstruction","name":"Supernatural reconstruction",
                     "specializes":"transformation","stage":"post-origination",
                     "createsNewIndividual":false}
                 ]}
                """);
            var catalog = NativeCatalogLoader.Load(
                NativeRulesetLayout.Resolve(_root, "first-party-001"));
            Validator = new NativeOriginContractValidator(catalog);
        }
        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
    }

    [Fact]
    public void ActualSelectedNativeMethodCatalogIsResolvedWithoutFixedCounts()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        string? root = null;
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "data", "rulesets",
                "first-party-001", "native", "rules", "creature-origination.json");
            if (File.Exists(path)) { root = directory.FullName; break; }
            directory = directory.Parent;
        }
        Assert.NotNull(root);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root!,
            "data", "rulesets", "first-party-001", "native", "rules",
            "creature-origination.json")));
        var snapshot = NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(
            Path.Combine(root!, "data", "rulesets"), "first-party-001"));
        var validator = new NativeOriginContractValidator(snapshot);
        foreach (var method in document.RootElement.GetProperty("methodDefinitions").EnumerateArray())
        {
            var id = method.GetProperty("id").GetString()!;
            Assert.NotNull(validator.FindMethod(Method(id)));
        }
        var reconstruction = validator.FindMethod(Method("supernatural-reconstruction"));
        Assert.Equal("transformation", reconstruction?.Specializes);
        Assert.False(reconstruction?.CreatesNewIndividual);
        Assert.Equal("post-origination", reconstruction?.Stage);
        Assert.Equal("replace-complete-species-and-ancestry-configuration",
            validator.FindMethod(Method("rite-of-rebirth"))?.Operation);
    }

    [Fact]
    public void HumanDirectOriginRequiresNoInventedBiologicalParents()
    {
        using var fixture = new Fixture();
        var candidate = Candidate("human-speed", Intrinsic("movement.walk.speed"));
        var manifest = Manifest("speed-selection", candidate.Id, JsonSerializer.SerializeToElement(30));
        var proposal = Proposal("human", candidates: [candidate], manifestations: [manifest])
            with { SelectedSpeciesId = "species.human" };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProposal(proposal).Status);
        var profile = Profile(proposal, [Grant("speed-grant", manifest, candidate)]);
        Assert.Equal("species.human", profile.Current.SpeciesId);
        Assert.Empty(proposal.Contributors);
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(profile, proposal).Status);
    }

    [Fact]
    public void DirectSpeciesWithAncestryRetainsBothSourceOccurrences()
    {
        using var fixture = new Fixture();
        var root = Candidate("elf-root", Trait("senses", "species.elf"));
        var leaf = Candidate("wood-leaf", Trait("wood-trait", "ancestry.wood-elf"));
        var rootPick = Manifest("root-pick", root.Id);
        var leafPick = Manifest("leaf-pick", leaf.Id);
        var proposal = Proposal("wood", candidates: [root, leaf],
            manifestations: [rootPick, leafPick]) with
            { SelectedSpeciesId = "species.elf", SelectedAncestryId = "ancestry.wood-elf" };
        var profile = Profile(proposal,
            [Grant("root-grant", rootPick, root), Grant("leaf-grant", leafPick, leaf)]);
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProposal(proposal).Status);
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(profile, proposal).Status);
        Assert.NotEqual(profile.Current.InherentGrants[0].Identity.Occurrence.Source.Id,
            profile.Current.InherentGrants[1].Identity.Occurrence.Source.Id);
    }

    [Fact]
    public void MixedSpeciesDoNotReceiveAnAutomaticWinnerForCreatureType()
    {
        using var fixture = new Fixture();
        var human = Candidate("human-type", Intrinsic("creature.type", "species.human"), "human");
        var elf = Candidate("elf-type", Intrinsic("creature.type", "species.elf"), "elf");
        var proposal = Proposal("mixed", intent: NativeOriginIntent.Mixed,
            contributors: [Parent("human", "species.human"), Parent("elf", "species.elf")],
            candidates: [human, elf],
            manifestations: [Manifest("pick-human", human.Id, JsonSerializer.SerializeToElement("humanoid")),
                             Manifest("pick-elf", elf.Id, JsonSerializer.SerializeToElement("fey"))])
            with { Relationships = [new("creature-type-conflict", NativeOriginRelationshipKind.Exclusion,
                        [human.Id, elf.Id])] };
        var report = fixture.Validator.ReviewProposal(proposal);
        Assert.Equal(NativeOriginReviewStatus.Unresolved, report.Status);
        Assert.Contains(report.Issues, i => i.Code == "relationship-awaits-decision");
        Assert.Equal(2, proposal.Contributors.Count);
        Assert.Empty(Profile(proposal).Current.InherentGrants);
    }

    [Fact]
    public void ConstructAssemblyAcceptsArbitraryNonParentInputRoles()
    {
        using var fixture = new Fixture();
        var roles = new[] {
            NativeOriginInputKind.Material, NativeOriginInputKind.Creator,
            NativeOriginInputKind.MagicalSource, NativeOriginInputKind.Animator
        };
        var contributors = roles.Select((role, n) => new NativeOriginContributor(
            "input-" + n, new(role), Source("source." + n))).ToList();
        contributors.Add(new("assembly-plan", new(NativeOriginInputKind.ProcessSpecific, "blueprint"),
            Source("source.blueprint", NativeOriginSourceKind.Custom)));
        var proposal = Proposal("construct", "construct-assembly", NativeOriginIntent.Custom,
            contributors);
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProposal(proposal).Status);
        Assert.Equal(5, proposal.Contributors.Count);
        Assert.Empty(proposal.Candidates);
    }

    [Fact]
    public void PlantAndFiendMethodsSupportDifferentInputRequirements()
    {
        using var fixture = new Fixture();
        var requirement = new NativeOriginInputRequirement("pollinator",
            new(NativeOriginInputKind.ProcessSpecific, "pollination-source"), 0, 3,
            NativeRuleReference: "rule.synthetic-plant-process");
        var plant = Proposal("plant", "plant-reproduction", NativeOriginIntent.Custom,
            contributors: [
                new("source", new(NativeOriginInputKind.PlantSource), Source("plant.spore"))
            ]) with { InputRequirements = [requirement] };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProposal(plant).Status);
        Assert.NotNull(fixture.Validator.FindMethod(Method("biological")));
        Assert.NotNull(fixture.Validator.FindMethod(Method("spawning")));
        var fiend = Proposal("fiend", "spawning", NativeOriginIntent.Custom,
            contributors: [
                new("summoner", new(NativeOriginInputKind.MagicalSource), Source("power.abyss"))
            ]);
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProposal(fiend).Status);
        var malformed = plant with {
            InputRequirements = [requirement with { MaximumCount = -1 }]
        };
        Assert.Equal(NativeOriginReviewStatus.Invalid,
            fixture.Validator.ReviewProposal(malformed).Status);
    }

    [Fact]
    public void NestedMixedContributorTransmitsOnlyExplicitCurrentCandidateEvidence()
    {
        using var fixture = new Fixture();
        var inherited = Candidate("prior", Trait("vision", "species.elf"));
        var priorPick = Manifest("prior-pick", inherited.Id);
        var now = new NativeOriginCurrentConfiguration(null, null,
            [Grant("prior-grant", priorPick, inherited)], "verified-parent-profile");
        var parent = new NativeOriginContributor("parent",
            new(NativeOriginInputKind.BiologicalParent),
            Source("verified-parent-profile", NativeOriginSourceKind.ManifestedProfile),
            now, ["species.elf", "species.human", "ancestry.wood-elf"]);
        var offer = Candidate("actual-offer",
            Trait("vision", "verified-parent-profile",
                kind: NativeOriginSourceKind.ManifestedProfile), parent.Id);
        var proposal = Proposal("nested", intent: NativeOriginIntent.Mixed,
            contributors: [parent], candidates: [offer]);
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProposal(proposal).Status);
        Assert.Single(proposal.Candidates);
        Assert.Equal("verified-parent-profile", offer.Identity.Occurrence.Source.Id);
        Assert.DoesNotContain(proposal.Candidates, c =>
            c.Identity.Occurrence.Source.Id == "ancestry.wood-elf");
    }

    [Fact]
    public void OfferedCandidatesAndUnselectedTraitsDoNotCreateGrants()
    {
        using var fixture = new Fixture();
        var proposal = Proposal("offers", candidates: [
            Candidate("vision-1", Trait("vision", "species.elf")),
            Candidate("vision-2", Trait("vision", "species.human"))
        ]);
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProposal(proposal).Status);
        var profile = Profile(proposal);
        Assert.Empty(profile.Current.InherentGrants);
        Assert.Empty(proposal.Manifestations);
    }

    [Fact]
    public void SharedDefinitionsDoNotAutomaticallyDoubleEffectiveBenefits()
    {
        using var fixture = new Fixture();
        var first = Candidate("offer-a", Trait("amphibious", "species.a", "mechanic.amphibious"));
        var second = Candidate("offer-b", Trait("amphibious", "species.b", "mechanic.amphibious"));
        var pick = Manifest("one-pick", first.Id);
        var proposal = Proposal("duplicate", intent: NativeOriginIntent.Mixed,
            candidates: [first, second], manifestations: [pick]);
        var consolidated = Grant("one-grant", pick, first, first.Id, second.Id);
        var profile = Profile(proposal, [consolidated]);
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(profile, proposal).Status);
        Assert.Single(profile.Current.InherentGrants);
        Assert.Equal(2, consolidated.SourceCandidateIds.Count);
        Assert.NotEqual(first.Identity.Occurrence, second.Identity.Occurrence);
    }

    [Fact]
    public void PersistentDependentChoiceCanBeIncompleteInvalidOrSupplied()
    {
        using var fixture = new Fixture();
        var identity = Trait("skillful");
        var requirement = new NativeOriginChoiceRequirement("skill", identity.Occurrence,
            NativeOriginChoiceTiming.Creation, 1, 1, FiniteOptions: ["skill.listen", "skill.spot"]);
        var candidate = Candidate("skill-candidate", identity, choices: [requirement]);
        var incomplete = Proposal("choice", candidates: [candidate],
            manifestations: [Manifest("pick", candidate.Id)]);
        Assert.Equal(NativeOriginReviewStatus.Incomplete,
            fixture.Validator.ReviewProposal(incomplete).Status);
        var invalid = incomplete with { Manifestations = [
            Manifest("pick", candidate.Id, choices: [
                new("skill", ["skill.listen", "skill.spot"])
            ]) ] };
        Assert.Equal(NativeOriginReviewStatus.Invalid, fixture.Validator.ReviewProposal(invalid).Status);
        var complete = incomplete with { Manifestations = [
            Manifest("pick", candidate.Id, choices: [new("skill", ["skill.listen"])]) ] };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProposal(complete).Status);
    }

    [Fact]
    public void UnknownCatalogAndSymbolicBindingsStayUnresolved()
    {
        using var fixture = new Fixture();
        var choice = new NativeOriginChoiceRequirement("feat", Trait("versatile").Occurrence,
            NativeOriginChoiceTiming.Creation, 1, 1, OptionCatalogId: "catalog.feats");
        var candidate = Candidate("candidate", Trait("versatile"),
            choices: [choice], unresolved: ["source-symbolic-binding"]);
        var proposal = Proposal("symbolic", candidates: [candidate],
            manifestations: [Manifest("pick", candidate.Id, choices: [
                new("feat", ["feat.unknown"])
            ])]);
        var report = fixture.Validator.ReviewProposal(proposal);
        Assert.Equal(NativeOriginReviewStatus.Unresolved, report.Status);
        Assert.Contains(report.Issues, i => i.Code == "choice-catalog-proof-required");
        Assert.Contains(report.Issues, i => i.Code == "unresolved-source-evidence");
    }

    [Fact]
    public void OnUseChoiceIsNotPersistedAsAnOriginChoice()
    {
        using var fixture = new Fixture();
        var choice = new NativeOriginChoiceRequirement("breath-mode", Trait("breath").Occurrence,
            NativeOriginChoiceTiming.OnUse, 1, 1, FiniteOptions: ["line", "cone"]);
        var candidate = Candidate("breath", Trait("breath"), choices: [choice]);
        var proposal = Proposal("on-use", candidates: [candidate],
            manifestations: [Manifest("pick", candidate.Id)]);
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProposal(proposal).Status);
        var invalid = proposal with { Manifestations = [
            Manifest("pick", candidate.Id, choices: [new("breath-mode", ["line"])]) ] };
        Assert.Equal(NativeOriginReviewStatus.Invalid,
            fixture.Validator.ReviewProposal(invalid).Status);
    }

    [Fact]
    public void ScopedDmDecisionCanReplaceRulesNotMerelyWaiveEligibility()
    {
        using var fixture = new Fixture();
        var decision = new NativeTableRuleDecision("ruling",
            new("dm.actor", "campaign-authorization-record"),
            new("campaign", "campaign-one"),
            new("origin.configuration", "character-one"),
            JsonSerializer.SerializeToElement(new
            {
                replaceCreatureType = "dragon", newTrait = "custom.feature"
            }));
        var candidate = Candidate("offer", Trait("custom"));
        var proposal = Proposal("authorized", candidates: [candidate],
            manifestations: [new("selected", candidate.Id, TableDecisionId: decision.Id)]) with {
            TableDecisions = [decision],
            Relationships = [new("custom-resolution", NativeOriginRelationshipKind.Substitution,
                [candidate.Id])],
            Resolutions = [new("custom-resolution", NativeOriginResolutionAction.Replace,
                new(NativeOriginAdjudication.DmAuthorized, TableDecisionId: decision.Id))]
        };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProposal(proposal).Status);
        var dmProfile = Profile(proposal,
            basis: new(NativeOriginAdjudication.DmAuthorized,
                TableDecisionId: decision.Id));
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(dmProfile, proposal).Status);
        Assert.Equal("campaign", decision.Scope.Kind);
        Assert.Equal("custom.feature",
            decision.EffectiveOutcome.GetProperty("newTrait").GetString());
        var noAuthority = proposal with { TableDecisions = [] };
        Assert.Equal(NativeOriginReviewStatus.Invalid,
            fixture.Validator.ReviewProposal(noAuthority).Status);
    }

    [Fact]
    public void RiteReplacesCurrentInherentGrantsButRetainsIndependentAndHistoricalState()
    {
        using var fixture = new Fixture();
        var oldTrait = Candidate("old-offer", Trait("old", "species.human"));
        var oldManifest = Manifest("old-manifest", oldTrait.Id);
        var oldGrant = Grant("old-grant", oldManifest, oldTrait);
        var newTrait = Candidate("new-offer", Trait("new", "species.dragonborn"));
        var newManifest = Manifest("new-manifest", newTrait.Id);
        var newGrant = Grant("new-grant", newManifest, newTrait);
        var independent = new NativeOriginEffectiveGrant("training-grant", "training-manifest",
            Trait("training", "background.scholar"), ["training-offer"]);
        var old = new NativeOriginCurrentConfiguration("species.human", null,
            [oldGrant], "before-rebirth");
        var proposal = Proposal("rebirth", "rite-of-rebirth",
            NativeOriginIntent.Transformation, candidates: [newTrait],
            manifestations: [newManifest]) with { ExistingConfiguration = old };
        var riteEvent = new NativeOriginHistoryEvent("rite-event", Method("rite-of-rebirth"),
            NativeOriginEventStage.Completed, [], true, old, "after-rebirth");
        var newBaseline = new NativeOriginCurrentConfiguration(
            "species.dragonborn", "ancestry.red", [newGrant], "after-rebirth");
        var profile = Profile(proposal, [newGrant], [independent], [riteEvent]) with {
            Underlying = newBaseline, Current = newBaseline
        };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProposal(proposal).Status);
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(profile, proposal).Status);
        Assert.DoesNotContain(profile.Current.InherentGrants, g => g.Id == oldGrant.Id);
        Assert.Single(profile.IndependentGrants);
        Assert.Equal("species.human", riteEvent.PreviousConfiguration!.SpeciesId);
    }

    [Fact]
    public void SupernaturalReconstructionAllowsStagesBeforeReplacement()
    {
        using var fixture = new Fixture();
        var existing = new NativeOriginCurrentConfiguration("species.human", null, [],
            "human-profile");
        var proposal = Proposal("reconstruction", "supernatural-reconstruction",
            NativeOriginIntent.Transformation) with { ExistingConfiguration = existing };
        var progress = new NativeOriginHistoryEvent("early", proposal.Method,
            NativeOriginEventStage.InProgress, ["subject"], false);
        var pending = Profile(proposal, history: [progress]) with
            { Underlying = existing, Current = existing };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(pending).Status);
        var complete = new NativeOriginHistoryEvent("last", proposal.Method,
            NativeOriginEventStage.Completed, ["subject"], true, existing, "zombie-profile");
        var zombieBaseline = new NativeOriginCurrentConfiguration(
            "species.zombie", null, [], "zombie-profile");
        var final = pending with {
            Underlying = zombieBaseline, Current = zombieBaseline,
            History = [progress, complete]
        };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProposal(proposal).Status);
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(final).Status);
        var native = fixture.Validator.FindMethod(proposal.Method);
        Assert.Equal("transformation", native!.Specializes);
        Assert.False(native.CreatesNewIndividual);
    }

    [Fact]
    public void CustomSpeciesAndUnknownMethodRemainRepresentableWithoutHardcodedLogic()
    {
        using var fixture = new Fixture();
        var custom = Candidate("new-offer", Trait("new-mechanic", "homebrew.species.new",
            kind: NativeOriginSourceKind.Custom));
        var proposal = Proposal("homebrew", "homebrew-genesis",
            NativeOriginIntent.Custom, candidates: [custom],
            manifestations: [Manifest("new", custom.Id)]);
        var report = fixture.Validator.ReviewProposal(proposal);
        Assert.Equal(NativeOriginReviewStatus.Unresolved, report.Status);
        Assert.DoesNotContain(report.Issues, i => i.Severity == NativeOriginIssueSeverity.Invalid);
        Assert.Contains(report.Issues, i => i.Code == "method-not-in-native-catalog");
        Assert.Null(Profile(proposal).Current.SpeciesId);
    }

    [Fact]
    public void InvalidReferencesAndDuplicateOccurrenceIdsAreRejected()
    {
        using var fixture = new Fixture();
        var candidate = Candidate("same", Trait("trait"));
        var malformed = Proposal("broken", candidates: [candidate, candidate],
            manifestations: [Manifest("orphan", "missing")]);
        var result = fixture.Validator.ReviewProposal(malformed);
        Assert.Equal(NativeOriginReviewStatus.Invalid, result.Status);
        Assert.Contains(result.Issues, issue => issue.Code == "duplicate-or-missing-id");
        Assert.Contains(result.Issues, issue => issue.Code == "manifestation-candidate-missing");
    }

    [Fact]
    public void DuplicateIntrinsicSlotsRequireAWholeProfileRule()
    {
        using var fixture = new Fixture();
        var first = Candidate("human", Intrinsic("creature.type", "species.human"));
        var second = Candidate("elf", Intrinsic("creature.type", "species.elf"));
        var a = Manifest("a", first.Id, JsonSerializer.SerializeToElement("humanoid"));
        var b = Manifest("b", second.Id, JsonSerializer.SerializeToElement("fey"));
        var proposal = Proposal("intrinsic", candidates: [first, second],
            manifestations: [a, b]);
        var current = Profile(proposal, [Grant("g1", a, first), Grant("g2", b, second)]);
        var review = fixture.Validator.ReviewProfile(current, proposal);
        Assert.Equal(NativeOriginReviewStatus.Unresolved, review.Status);
        Assert.Contains(review.Issues, issue => issue.Code == "intrinsic-slot-policy-unresolved");
    }

    [Fact]
    public void SameConceptButDifferentTraitDefinitionsNeverImplyEquivalence()
    {
        using var fixture = new Fixture();
        var orc = Candidate("orc", Trait("powerful-build", "species.orc",
            "mechanic.orc-powerful-build") with { TraitConceptId = "concept.powerful-build" });
        var goliath = Candidate("goliath", Trait("powerful-build", "species.goliath",
            "mechanic.goliath-powerful-build") with { TraitConceptId = "concept.powerful-build" });
        var proposal = Proposal("distinct-definitions", candidates: [orc, goliath],
            manifestations: [Manifest("orc-pick", orc.Id)]) with {
            Relationships = [new("definition-review", NativeOriginRelationshipKind.Unknown,
                [orc.Id, goliath.Id])]
        };
        Assert.Equal(NativeOriginReviewStatus.Unresolved,
            fixture.Validator.ReviewProposal(proposal).Status);
        Assert.Equal(orc.Identity.TraitConceptId, goliath.Identity.TraitConceptId);
        Assert.NotEqual(orc.Identity.MechanicalDefinitionId,
            goliath.Identity.MechanicalDefinitionId);
    }

    [Fact]
    public void DescendantIntrinsicReplacementDoesNotUseAnArbitraryMaximum()
    {
        using var fixture = new Fixture();
        var elf = Candidate("elf-speed", Intrinsic("movement.walk.speed", "species.elf"));
        var wood = Candidate("wood-speed", Intrinsic("movement.walk.speed", "ancestry.wood-elf"));
        var proposal = Proposal("wood-speed", candidates: [elf, wood],
            manifestations: [Manifest("wood-pick", wood.Id,
                JsonSerializer.SerializeToElement(35))]) with {
            Relationships = [new("speed-replacement", NativeOriginRelationshipKind.Substitution,
                [elf.Id, wood.Id])]
        };
        var review = fixture.Validator.ReviewProposal(proposal);
        Assert.Equal(NativeOriginReviewStatus.Unresolved, review.Status);
        Assert.Contains(review.Issues, i => i.Code == "relationship-awaits-decision");
        Assert.Single(proposal.Manifestations);
    }

    [Fact]
    public void ConflictingUnresolvedAndDmAuthorizedStatusesRemainDistinct()
    {
        using var fixture = new Fixture();
        var proposal = Proposal("adjudication");
        Assert.Equal(NativeOriginReviewStatus.Conflicting,
            fixture.Validator.ReviewProfile(Profile(proposal,
                basis: new(NativeOriginAdjudication.Conflict))).Status);
        Assert.Equal(NativeOriginReviewStatus.Unresolved,
            fixture.Validator.ReviewProfile(Profile(proposal,
                basis: new(NativeOriginAdjudication.Unresolved))).Status);
        var dm = Profile(proposal,
            basis: new(NativeOriginAdjudication.DmAuthorized, TableDecisionId: "ruling"));
        Assert.Equal(NativeOriginReviewStatus.Unresolved,
            fixture.Validator.ReviewProfile(dm).Status);
        // This verifies the claim's shape, not the actor's actual authority.
        Assert.Equal("ruling", dm.Adjudication.TableDecisionId);
    }

    [Fact]
    public void UnsupportedMechanicalIdentityIsUnresolvedNotEquivalentByName()
    {
        using var fixture = new Fixture();
        var identity = Trait("powerful-build", "species.orc") with
            { MechanicalDefinitionId = null, TraitConceptId = "concept.powerful-build" };
        var proposal = Proposal("unmapped", candidates: [Candidate("offer", identity)]);
        var review = fixture.Validator.ReviewProposal(proposal);
        Assert.Equal(NativeOriginReviewStatus.Unresolved, review.Status);
        Assert.Contains(review.Issues, i => i.Code == "mechanical-identity-unresolved");
    }

    [Fact]
    public void WholeProfileConstraintCanExpressDependenciesWithoutExecutingThem()
    {
        using var fixture = new Fixture();
        var wing = Candidate("wing", Trait("flight"));
        var body = Candidate("body", Trait("winged-body"));
        var constraint = new NativeOriginConstraint("wings-need-body",
            NativeOriginRelationshipKind.Requires,
            [new(CandidateId: wing.Id)], [new(CandidateId: body.Id)],
            NativeRuleReference: "rule.flight-forms");
        var proposal = Proposal("constraint", candidates: [wing, body],
            manifestations: [Manifest("wing-pick", wing.Id)]) with
            { Constraints = [constraint] };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProposal(proposal).Status);
        Assert.Single(proposal.Manifestations);
        var unsupported = proposal with
            { Constraints = [constraint with { NativeRuleReference = null }] };
        Assert.Equal(NativeOriginReviewStatus.Unresolved,
            fixture.Validator.ReviewProposal(unsupported).Status);
        var badReference = proposal with { Constraints = [constraint with {
            Conditions = [new NativeOriginCharacteristicSelector(CandidateId: "missing")]
        }] };
        Assert.Equal(NativeOriginReviewStatus.Invalid,
            fixture.Validator.ReviewProposal(badReference).Status);
    }

    [Fact]
    public void GrantsMustMatchTheSelectedManifestationAndDefinition()
    {
        using var fixture = new Fixture();
        var a = Candidate("a", Trait("first"));
        var b = Candidate("b", Trait("second"));
        var pick = Manifest("pick", a.Id);
        var proposal = Proposal("bad-provenance", candidates: [a, b],
            manifestations: [pick]);
        var review = fixture.Validator.ReviewProfile(Profile(proposal, [Grant("wrong", pick, b)]), proposal);
        Assert.Equal(NativeOriginReviewStatus.Invalid, review.Status);
        Assert.Contains(review.Issues, i => i.Code == "grant-selected-offer-mismatch");
        Assert.Contains(review.Issues, i => i.Code == "grant-definition-mismatch");
    }

    [Fact]
    public void ProposalAndProfileRoundTripPreserveSelectionsAndHistory()
    {
        using var fixture = new Fixture();
        var candidate = Candidate("origin-a", Intrinsic("size"));
        var manifest = Manifest("size-selection", candidate.Id,
            JsonSerializer.SerializeToElement("small"));
        var proposal = Proposal("serialize", candidates: [candidate],
            manifestations: [manifest]);
        var previous = new NativeOriginCurrentConfiguration("species.human", null, []);
        var history = new NativeOriginHistoryEvent("history", Method("rite-of-rebirth"),
            NativeOriginEventStage.Completed, [], true, previous, "current");
        var profile = Profile(proposal, [Grant("small-grant", manifest, candidate)],
            history: [history]);
        var proposalCopy = JsonSerializer.Deserialize<NativeOriginProposal>(
            JsonSerializer.Serialize(proposal))!;
        var profileCopy = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(profile))!;
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProposal(proposalCopy).Status);
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(profileCopy, proposalCopy).Status);
        Assert.Equal("small",
            proposalCopy.Manifestations.Single().SelectedValue!.Value.GetString());
        Assert.Equal("species.human",
            profileCopy.History.Single().PreviousConfiguration!.SpeciesId);
        Assert.Equal("origin-a",
            profileCopy.Current.InherentGrants.Single().SourceCandidateIds.Single());
    }
    private static NativeOriginPersistentInfluence Lycanthropy() =>
        new("werewolf-influence", Source("condition.lycanthropy"), "event.acquired-lycanthropy");

    private static NativeOriginActiveTemporaryTransformation WolfActivation() =>
        new("shift-wolf-one", "form.wolf", Source("condition.lycanthropy"),
            "event.shift-wolf-one", "werewolf-influence", ["wolf-form-grant"]);

    private static (NativeOriginProposal Proposal, NativeOriginProfile Profile,
        NativeOriginCurrentConfiguration Human) HumanWithLycanthropy(bool active)
    {
        var human = Candidate("human-sense-offer", Trait("human-sense", "species.human"));
        var selection = Manifest("human-sense-selection", human.Id);
        var proposal = Proposal("human-with-lycanthropy", candidates: [human],
            manifestations: [selection]) with { SelectedSpeciesId = "species.human" };
        var baseline = new NativeOriginCurrentConfiguration("species.human", null,
            [Grant("human-sense-grant", selection, human)], "underlying-human-current");
        var profile = Profile(proposal, baseline.InherentGrants) with
        {
            Underlying = baseline, Current = baseline,
            PersistentInfluences = [Lycanthropy()]
        };
        if (active)
        {
            var wolf = Candidate("wolf-form-offer", Trait("wolf-sense", "form.wolf"));
            var wolfSelection = Manifest("wolf-sense-selection", wolf.Id);
            var effective = new NativeOriginCurrentConfiguration("species.wolf-form", null,
                [Grant("wolf-form-grant", wolfSelection, wolf)], "active-wolf-form");
            profile = profile with
            {
                Current = effective,
                ActiveTemporaryTransformation = WolfActivation()
            };
        }
        return (proposal, profile, baseline);
    }

    [Fact]
    public void InactiveLycanthropyIsPersistentAndLeavesHumanBaselineEffective()
    {
        using var fixture = new Fixture();
        var (proposal, profile, human) = HumanWithLycanthropy(active: false);
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(profile, proposal).Status);
        Assert.Equal("species.human", profile.Underlying.SpeciesId);
        Assert.Equal("species.human", profile.Current.SpeciesId);
        Assert.Null(profile.ActiveTemporaryTransformation);
        Assert.Single(profile.PersistentInfluences!);
        Assert.Single(profile.Current.InherentGrants);
        Assert.Equal("biological", proposal.Method.MethodId);
        Assert.Equal(human.ProfileId, profile.Current.ProfileId);
    }

    [Fact]
    public void WolfFormReplacesEffectiveSpeciesWithoutOverwritingHumanOrStackingTraits()
    {
        using var fixture = new Fixture();
        var (proposal, transformed, _) = HumanWithLycanthropy(active: true);
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(transformed, proposal).Status);
        Assert.Equal("species.human", transformed.Underlying.SpeciesId);
        Assert.Equal("species.wolf-form", transformed.Current.SpeciesId);
        Assert.DoesNotContain(transformed.Current.InherentGrants,
            grant => grant.Id == "human-sense-grant");
        Assert.Single(transformed.Current.InherentGrants);
        Assert.Equal("human-sense-grant", Assert.Single(transformed.Underlying.InherentGrants).Id);
        Assert.Equal("condition.lycanthropy",
            transformed.ActiveTemporaryTransformation!.EffectSource.Id);
        Assert.Equal("event.shift-wolf-one",
            transformed.ActiveTemporaryTransformation.ActivationEvidenceId);
        Assert.Equal("biological", proposal.Method.MethodId);
    }

    [Fact]
    public void ReversionCanRepresentLatestUnderlyingHumanWithoutRunningATransition()
    {
        using var fixture = new Fixture();
        var (proposal, wolf, human) = HumanWithLycanthropy(active: true);
        var reverted = wolf with
        {
            Current = human,
            ActiveTemporaryTransformation = null
        };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(reverted, proposal).Status);
        Assert.Equal("species.human", reverted.Current.SpeciesId);
        Assert.Single(reverted.PersistentInfluences!);
        Assert.Equal("human-sense-grant", Assert.Single(reverted.Current.InherentGrants).Id);
        Assert.DoesNotContain(reverted.Current.InherentGrants,
            grant => grant.Id == "wolf-form-grant");
    }

    [Fact]
    public void UnderlyingRebirthDuringWolfFormDoesNotRestoreSupersededHumanGrants()
    {
        using var fixture = new Fixture();
        var (_, wolf, human) = HumanWithLycanthropy(active: true);
        var elfTrait = Candidate("elf-trait-offer", Trait("elf-sense", "species.elf"));
        var elfSelection = Manifest("elf-selection", elfTrait.Id);
        var elf = new NativeOriginCurrentConfiguration("species.elf", "ancestry.high-elf",
            [Grant("elf-sense-grant", elfSelection, elfTrait)], "updated-authoritative-elf");
        var rite = new NativeOriginHistoryEvent("baseline-rebirth",
            Method("rite-of-rebirth"), NativeOriginEventStage.Completed, [],
            true, human, elf.ProfileId);
        var proposal = Proposal("underlying-rebirth", "rite-of-rebirth",
            NativeOriginIntent.Transformation, candidates: [elfTrait],
            manifestations: [elfSelection]) with
        {
            ExistingConfiguration = human,
            ExistingEffectiveConfiguration = wolf.Current,
            ActiveTemporaryTransformation = wolf.ActiveTemporaryTransformation,
            SelectedSpeciesId = elf.SpeciesId, SelectedAncestryId = elf.AncestryId
        };
        var during = wolf with
        {
            ProposalId = proposal.Id,
            Underlying = elf,
            // The active effective form has not been replaced by this recorded
            // baseline change. Whether it may remain active is a separate rule.
            Current = wolf.Current,
            History = [rite]
        };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProposal(proposal).Status);
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(during, proposal).Status);
        Assert.Equal("species.elf", during.Underlying.SpeciesId);
        Assert.Equal("species.wolf-form", during.Current.SpeciesId);
        Assert.Equal("shift-wolf-one", during.ActiveTemporaryTransformation!.Id);
        Assert.DoesNotContain(during.Underlying.InherentGrants,
            grant => grant.Id == "human-sense-grant");

        var reverted = during with
        {
            Current = during.Underlying,
            ActiveTemporaryTransformation = null
        };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(reverted, proposal).Status);
        Assert.Equal("species.elf", reverted.Current.SpeciesId);
        Assert.Equal("ancestry.high-elf", reverted.Current.AncestryId);
        Assert.Equal("elf-sense-grant", Assert.Single(reverted.Current.InherentGrants).Id);
        Assert.DoesNotContain(reverted.Current.InherentGrants,
            grant => grant.Id == "human-sense-grant");
        Assert.Equal("species.human", rite.PreviousConfiguration!.SpeciesId);
    }

    [Fact]
    public void StaleHumanRestorationIsUnresolvedAfterUnderlyingRebirth()
    {
        using var fixture = new Fixture();
        var (_, wolf, human) = HumanWithLycanthropy(active: true);
        var updated = new NativeOriginCurrentConfiguration("species.elf", null, [],
            "new-authoritative-elf");
        var changedDuringForm = wolf with { Underlying = updated };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(changedDuringForm).Status);

        // Returning to an old snapshot is not an authorized reversion.
        var staleRestore = changedDuringForm with {
            ActiveTemporaryTransformation = null,
            Current = human
        };
        var review = fixture.Validator.ReviewProfile(staleRestore);
        Assert.Equal(NativeOriginReviewStatus.Unresolved, review.Status);
        Assert.Contains(review.Issues,
            issue => issue.Code == "effective-state-without-active-effect");

        var correct = changedDuringForm with {
            ActiveTemporaryTransformation = null,
            Current = changedDuringForm.Underlying
        };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(correct).Status);
    }

    [Fact]
    public void EffectiveAndUnderlyingProfileIdsNeedNotMatchWhenMechanicsDo()
    {
        using var fixture = new Fixture();
        var (proposal, profile, _) = HumanWithLycanthropy(active: false);
        var equivalent = profile with {
            Current = profile.Current with { ProfileId = "effective-human-snapshot" }
        };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(equivalent, proposal).Status);
    }

    [Fact]
    public void FormReplacementDoesNotEraseIndependentGrants()
    {
        using var fixture = new Fixture();
        var (proposal, wolf, _) = HumanWithLycanthropy(active: true);
        var independent = new NativeOriginEffectiveGrant(
            "class-grant", "class-choice", Trait("trained", "class.warlock"),
            ["class-candidate"]);
        var withClass = wolf with { IndependentGrants = [independent] };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(withClass, proposal).Status);
        Assert.Equal("class-grant", Assert.Single(withClass.IndependentGrants).Id);
        Assert.DoesNotContain(withClass.Current.InherentGrants,
            grant => grant.Id == independent.Id);
        var reverted = withClass with {
            Current = withClass.Underlying,
            ActiveTemporaryTransformation = null
        };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(reverted, proposal).Status);
        Assert.Equal("class-grant", Assert.Single(reverted.IndependentGrants).Id);
    }

    [Fact]
    public void TemporaryFormDoesNotUseHistoryReplacementOrObsoleteBaselineSnapshot()
    {
        var (_, wolf, _) = HumanWithLycanthropy(active: true);
        Assert.Empty(wolf.History);
        Assert.Null(typeof(NativeOriginActiveTemporaryTransformation)
            .GetProperty("PreviousConfiguration"));
        Assert.Null(typeof(NativeOriginActiveTemporaryTransformation)
            .GetProperty("RestoreConfiguration"));
        Assert.NotEqual(wolf.Underlying.ProfileId, wolf.Current.ProfileId);
    }

    [Fact]
    public void ActiveFormAndPersistentInfluenceRequireRealReferences()
    {
        using var fixture = new Fixture();
        var (proposal, wolf, _) = HumanWithLycanthropy(active: true);
        var invalid = wolf with
        {
            ActiveTemporaryTransformation = WolfActivation() with
            {
                PersistentInfluenceId = "condition.unknown",
                EffectiveFormGrantIds = ["missing-form-grant"]
            }
        };
        var review = fixture.Validator.ReviewProfile(invalid, proposal);
        Assert.Equal(NativeOriginReviewStatus.Invalid, review.Status);
        Assert.Contains(review.Issues, issue => issue.Code == "active-form-influence-missing");
        Assert.Contains(review.Issues, issue => issue.Code == "active-form-grant-missing");
        var unknownEffect = wolf with
        {
            Current = wolf.Current,
            ActiveTemporaryTransformation = null
        };
        var unresolved = fixture.Validator.ReviewProfile(unknownEffect, proposal);
        Assert.Equal(NativeOriginReviewStatus.Unresolved, unresolved.Status);
        Assert.Contains(unresolved.Issues,
            issue => issue.Code == "effective-state-without-active-effect");

        var invalidProposal = proposal with {
            ActiveTemporaryTransformation = WolfActivation(),
            ExistingConfiguration = wolf.Underlying,
            ExistingEffectiveConfiguration = null
        };
        Assert.Equal(NativeOriginReviewStatus.Invalid,
            fixture.Validator.ReviewProposal(invalidProposal).Status);
    }

    [Fact]
    public void ActiveFormDmAuthorityRequiresExplicitTableDecisionEvidence()
    {
        using var fixture = new Fixture();
        var (proposal, wolf, _) = HumanWithLycanthropy(active: true);
        var custom = wolf with
        {
            ActiveTemporaryTransformation = WolfActivation() with
            {
                Authorization = new(NativeOriginAdjudication.DmAuthorized,
                    TableDecisionId: "ruling.custom-wolf")
            }
        };
        Assert.Equal(NativeOriginReviewStatus.Unresolved,
            fixture.Validator.ReviewProfile(custom, proposal).Status);

        var ruling = new NativeTableRuleDecision("ruling.custom-wolf",
            new("dm.actor", "authorization.record"),
            new("campaign", "campaign.one"),
            new("origin.form", "form.wolf"),
            JsonSerializer.SerializeToElement(new { retainTrait = "feature.custom" }));
        var withDecision = proposal with { TableDecisions = [ruling] };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(custom, withDecision).Status);
    }

    [Fact]
    public void TemporaryFormAndUnderlyingStateSurviveJsonRoundTrip()
    {
        using var fixture = new Fixture();
        var (proposal, wolf, _) = HumanWithLycanthropy(active: true);
        var copy = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(wolf))!;
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(copy, proposal).Status);
        Assert.Equal("species.human", copy.Underlying.SpeciesId);
        Assert.Equal("species.wolf-form", copy.Current.SpeciesId);
        Assert.Equal("condition.lycanthropy",
            Assert.Single(copy.PersistentInfluences!).Source.Id);
        Assert.Equal("form.wolf", copy.ActiveTemporaryTransformation!.FormId);
        Assert.Single(copy.ActiveTemporaryTransformation.EffectiveFormGrantIds!);
    }

    [Fact]
    public void SimultaneousTemporaryFormsPreserveSourcesButDoNotInventPrecedence()
    {
        using var fixture = new Fixture();
        var (proposal, wolf, _) = HumanWithLycanthropy(active: true);
        var polymorph = new NativeOriginActiveTemporaryTransformation(
            "shift-polymorph", "form.eagle", Source("effect.polymorph"),
            "event.external-polymorph");
        var combined = wolf with
        {
            OtherActiveTemporaryTransformations = [polymorph]
        };

        var unresolved = fixture.Validator.ReviewProfile(combined, proposal);
        Assert.Equal(NativeOriginReviewStatus.Unresolved, unresolved.Status);
        Assert.Contains(unresolved.Issues,
            issue => issue.Code == "temporary-form-precedence-unresolved");
        Assert.Equal("species.human", combined.Underlying.SpeciesId);
        Assert.Equal("species.wolf-form", combined.Current.SpeciesId);
        Assert.Equal("effect.polymorph",
            Assert.Single(combined.OtherActiveTemporaryTransformations!).EffectSource.Id);

        var copy = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(combined))!;
        Assert.Equal(NativeOriginReviewStatus.Unresolved,
            fixture.Validator.ReviewProfile(copy, proposal).Status);
        Assert.Equal("event.external-polymorph",
            Assert.Single(copy.OtherActiveTemporaryTransformations!).ActivationEvidenceId);

        // An Origin proposal can also retain both competing effective inputs.
        var inProgressProposal = proposal with
        {
            ExistingConfiguration = wolf.Underlying,
            ExistingEffectiveConfiguration = wolf.Current,
            ActiveTemporaryTransformation = wolf.ActiveTemporaryTransformation,
            OtherActiveTemporaryTransformations = [polymorph]
        };
        var proposalReview = fixture.Validator.ReviewProposal(inProgressProposal);
        Assert.Equal(NativeOriginReviewStatus.Unresolved, proposalReview.Status);
        Assert.Contains(proposalReview.Issues,
            issue => issue.Code == "temporary-form-precedence-unresolved");
    }

    [Fact]
    public void CompetingFormPrecedenceCanBeAuthoredOrDmAuthorized()
    {
        using var fixture = new Fixture();
        var (proposal, wolf, _) = HumanWithLycanthropy(active: true);
        var second = new NativeOriginActiveTemporaryTransformation(
            "shift-alternate", "form.other", Source("effect.external"),
            "event.external");
        var multi = wolf with { OtherActiveTemporaryTransformations = [second] };
        var native = multi with
        {
            TemporaryTransformationPrecedence =
                new(NativeOriginAdjudication.Supported, NativeRuleReference: "rule.form-priority")
        };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(native, proposal).Status);

        var unproven = multi with {
            TemporaryTransformationPrecedence = new(NativeOriginAdjudication.Supported)
        };
        Assert.Equal(NativeOriginReviewStatus.Unresolved,
            fixture.Validator.ReviewProfile(unproven, proposal).Status);

        var conflicting = multi with {
            TemporaryTransformationPrecedence = new(NativeOriginAdjudication.Conflict)
        };
        Assert.Equal(NativeOriginReviewStatus.Conflicting,
            fixture.Validator.ReviewProfile(conflicting, proposal).Status);

        var ruling = new NativeTableRuleDecision("ruling.form-priority",
            new("dm.actor", "campaign-authority"),
            new("campaign", "test"),
            new("origin.temporary-form-precedence", "shift-wolf-one"),
            JsonSerializer.SerializeToElement(new { selectedForm = "form.wolf" }));
        var dm = multi with
        {
            TemporaryTransformationPrecedence = new(NativeOriginAdjudication.DmAuthorized,
                TableDecisionId: ruling.Id)
        };
        Assert.Equal(NativeOriginReviewStatus.Unresolved,
            fixture.Validator.ReviewProfile(dm, proposal).Status);
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(dm, proposal with { TableDecisions = [ruling] }).Status);
    }

    [Fact]
    public void ConcurrentFormIdsMustBeDistinct()
    {
        using var fixture = new Fixture();
        var (proposal, wolf, _) = HumanWithLycanthropy(active: true);
        var invalid = wolf with
        {
            OtherActiveTemporaryTransformations =
                [WolfActivation() with { FormId = "form.other" }]
        };
        var review = fixture.Validator.ReviewProfile(invalid, proposal);
        Assert.Equal(NativeOriginReviewStatus.Invalid, review.Status);
        Assert.Contains(review.Issues,
            issue => issue.Code == "temporary-form-duplicate-id");
    }

    [Fact]
    public void SourcedNonFormModifierMayChangeEffectiveConfiguration()
    {
        using var fixture = new Fixture();
        var (proposal, human, _) = HumanWithLycanthropy(active: false);
        var modifier = new NativeOriginEffectiveGrant(
            "condition-grant", "condition-manifestation", Trait("blessing", "effect.blessing"),
            ["condition-candidate"]);
        var adjusted = human with
        {
            Current = human.Current with {
                InherentGrants = [..human.Current.InherentGrants, modifier]
            },
            EffectiveConfigurationBasis =
                new(NativeOriginAdjudication.Supported, NativeRuleReference: "rule.blessing")
        };
        Assert.Null(adjusted.ActiveTemporaryTransformation);
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(adjusted, proposal).Status);
        Assert.DoesNotContain(adjusted.Underlying.InherentGrants,
            grant => grant.Id == "condition-grant");
        Assert.Equal("effect.blessing",
            adjusted.Current.InherentGrants.Single(x => x.Id == "condition-grant")
                .Identity.Occurrence.Source.Id);
    }

    [Fact]
    public void DmAuthorizedReversionVariationNeedsScopedEvidence()
    {
        using var fixture = new Fixture();
        var (proposal, wolf, _) = HumanWithLycanthropy(active: true);
        var ruling = new NativeTableRuleDecision("ruling.reversion",
            new("dm.actor", "campaign-authority"),
            new("campaign", "test"),
            new("origin.reversion", "character.human-lycanthrope"),
            JsonSerializer.SerializeToElement(new { effectiveSpecies = "species.custom-human" }));
        // The temporary form has ended. The ordinary baseline remains authoritative;
        // any different effective configuration has separately stated authority.
        var changed = wolf with
        {
            Current = wolf.Underlying with { SpeciesId = "species.custom-human" },
            ActiveTemporaryTransformation = null,
            EffectiveConfigurationBasis = new(NativeOriginAdjudication.DmAuthorized,
                TableDecisionId: ruling.Id)
        };
        Assert.Equal(NativeOriginReviewStatus.Unresolved,
            fixture.Validator.ReviewProfile(changed, proposal).Status);
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(changed,
                proposal with { TableDecisions = [ruling] }).Status);
        Assert.Equal("species.human", changed.Underlying.SpeciesId);
        Assert.Equal("species.custom-human", changed.Current.SpeciesId);
        Assert.Equal("ruling.reversion",
            changed.EffectiveConfigurationBasis!.TableDecisionId);
        Assert.Null(changed.ActiveTemporaryTransformation);

        var withProposal = proposal with
        {
            ExistingConfiguration = wolf.Underlying,
            ExistingEffectiveConfiguration = changed.Current,
            ExistingEffectiveConfigurationBasis = changed.EffectiveConfigurationBasis,
            TableDecisions = [ruling]
        };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProposal(withProposal).Status);
    }

    [Fact]
    public void ModifiedEffectiveStateWithoutARealRuleOrRulingIsUnresolved()
    {
        using var fixture = new Fixture();
        var (proposal, human, _) = HumanWithLycanthropy(active: false);
        var changed = human with {
            Current = human.Current with { SpeciesId = "species.other" }
        };
        Assert.Equal(NativeOriginReviewStatus.Unresolved,
            fixture.Validator.ReviewProfile(changed, proposal).Status);
        Assert.Equal(NativeOriginReviewStatus.Unresolved,
            fixture.Validator.ReviewProfile(changed with {
                EffectiveConfigurationBasis =
                    new(NativeOriginAdjudication.Supported)
            }, proposal).Status);
        var proposalWithDifferentEffective = proposal with
        {
            ExistingConfiguration = human.Underlying,
            ExistingEffectiveConfiguration = changed.Current
        };
        Assert.Equal(NativeOriginReviewStatus.Unresolved,
            fixture.Validator.ReviewProposal(proposalWithDifferentEffective).Status);
    }


    [Fact]
    public void IndependentEntitlementPersistsWhenTemporaryFormSuppressesItsUse()
    {
        using var fixture = new Fixture();
        var (proposal, wolf, _) = HumanWithLycanthropy(active: true);
        var classGrant = new NativeOriginEffectiveGrant("class-grant",
            "class-choice", Trait("class-feature", "class.warlock"),
            ["class-offer"]);
        var suppressed = wolf with
        {
            IndependentGrants = [classGrant],
            IndependentGrantApplicability =
            [
                new("class-grant", NativeOriginGrantApplicabilityState.Suppressed,
                    new(NativeOriginAdjudication.Supported,
                        NativeRuleReference: "rule.form-class-compatibility"))
            ]
        };

        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(suppressed, proposal).Status);
        Assert.Equal("class-grant", Assert.Single(suppressed.IndependentGrants).Id);
        Assert.Equal(NativeOriginGrantApplicabilityState.Suppressed,
            Assert.Single(suppressed.IndependentGrantApplicability!).State);

        // These are two representable states, not an implemented transition.
        var reverted = suppressed with
        {
            Current = suppressed.Underlying,
            ActiveTemporaryTransformation = null,
            IndependentGrantApplicability =
            [
                new("class-grant", NativeOriginGrantApplicabilityState.Applicable,
                    new(NativeOriginAdjudication.Supported,
                        NativeRuleReference: "rule.form-class-compatibility"))
            ]
        };
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(reverted, proposal).Status);
        Assert.Equal("class-grant", Assert.Single(reverted.IndependentGrants).Id);

        var copy = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(suppressed))!;
        Assert.Equal(NativeOriginReviewStatus.StructurallyConsistent,
            fixture.Validator.ReviewProfile(copy, proposal).Status);
        Assert.Equal(NativeOriginGrantApplicabilityState.Suppressed,
            Assert.Single(copy.IndependentGrantApplicability!).State);
    }

    [Fact]
    public void IndependentGrantUseCannotBeInventedOrReferToMissingEntitlements()
    {
        using var fixture = new Fixture();
        var (proposal, profile, _) = HumanWithLycanthropy(active: false);
        var absent = profile with
        {
            IndependentGrantApplicability =
                [new("not-earned", NativeOriginGrantApplicabilityState.Applicable)]
        };
        var invalid = fixture.Validator.ReviewProfile(absent, proposal);
        Assert.Equal(NativeOriginReviewStatus.Invalid, invalid.Status);
        Assert.Contains(invalid.Issues,
            x => x.Code == "independent-applicability-reference-invalid");

        var grant = new NativeOriginEffectiveGrant("earned", "class-choice",
            Trait("training", "class.fighter"), ["class-offer"]);
        var withoutEvidence = profile with
        {
            IndependentGrants = [grant],
            IndependentGrantApplicability =
                [new("earned", NativeOriginGrantApplicabilityState.Applicable)]
        };
        Assert.Equal(NativeOriginReviewStatus.Unresolved,
            fixture.Validator.ReviewProfile(withoutEvidence, proposal).Status);

        var unknown = withoutEvidence with
        {
            IndependentGrantApplicability =
                [new("earned", NativeOriginGrantApplicabilityState.Unresolved)]
        };
        Assert.Equal(NativeOriginReviewStatus.Unresolved,
            fixture.Validator.ReviewProfile(unknown, proposal).Status);
        Assert.Contains(fixture.Validator.ReviewProfile(unknown, proposal).Issues,
            x => x.Code == "independent-grant-applicability-unresolved");
    }


}
