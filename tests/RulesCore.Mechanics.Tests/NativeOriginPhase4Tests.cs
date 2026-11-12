using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Phase 4 tests use real native Human/Dragonborn content for rebirth and
/// explicitly scoped synthetic fixtures for unfinished Plant, Construct, Fiend
/// and restricted-acquisition rules. Fixtures are not first-party Species.
/// </summary>
public sealed class NativeOriginPhase4Tests
{
    private static readonly NativeTableDecisionScope Scope = new("campaign", "phase4");
    private static readonly NativeOriginMethodReference Biological =
        new("rule.creature-origination", "biological");

    private static NativeCatalogSnapshot Catalog()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var root = Path.Combine(current.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(root, "first-party-001", "native",
                "rules", "creature-origination.json")))
                return NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(root, "first-party-001"));
            current = current.Parent;
        }
        throw new InvalidOperationException("Native selected ruleset not found.");
    }

    private sealed class Authority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision d, NativeTableDecisionScope scope) =>
            d.Authority.ActorId == "dm" &&
            d.Authority.AuthorizationEvidenceId == "verified-phase4" &&
            scope.Kind == Scope.Kind && scope.Id == Scope.Id;
        public bool AuthorizesCatalogChoice(string catalogId, string optionId, string evidenceId) =>
            evidenceId == "verified-catalog-proof" &&
            !string.IsNullOrWhiteSpace(catalogId) && !string.IsNullOrWhiteSpace(optionId);
    }

    private static NativeTableRuleDecision Rule(string id, string kind, string target, object outcome) =>
        new(id, new("dm", "verified-phase4"), Scope, new(kind, target),
            JsonSerializer.SerializeToElement(outcome));

    private static NativeOriginProfile Human()
    {
        var proposal = new NativeOriginProposal("human-before-rebirth",
            NativeOriginIntent.Direct, Biological, [], [], [],
            SelectedSpeciesId: "species.human");
        var engine = new NativeOriginConstructionEngine(Catalog());
        var inspected = engine.Evaluate(new(proposal));
        NativeOriginCandidate Find(string path) => inspected.EvaluatedProposal.Candidates.Single(x =>
            x.Identity.Occurrence.OccurrencePath == path);
        var completed = proposal with
        {
            Manifestations =
            [
                new("size", Find("/sizeChoice").Id, Choices: [new("size", ["medium"])]),
                new("skill", Find("/traits/0").Id, Choices: [
                    new("selection", ["skill.listen"], "verified-catalog-proof")]),
                new("feat", Find("/traits/1").Id, Choices: [
                    new("selection", ["feat.phase4"], "verified-catalog-proof")])
            ]
        };
        var result = engine.Evaluate(new(completed, Scope), new Authority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        return result.Profile!;
    }

    private static NativeOriginProposal Target(string id = "red-rebirth",
        IReadOnlyList<NativeTableRuleDecision>? decisions = null,
        string ancestry = "ancestry.dragonborn.red") =>
        new(id, NativeOriginIntent.Direct,
            new("rule.creature-origination", "rite-of-rebirth"), [], [], [],
            SelectedSpeciesId: "species.dragonborn", SelectedAncestryId: ancestry,
            TableDecisions: decisions);

    private static NativeTableRuleDecision Authorize(string id = "red-rebirth",
        object? additional = null)
    {
        // Test cases needing an altered outcome specify the full authorized
        // replacement record, not a caller-originated partial decision.
        return Rule("rite-auth-" + id, "rite", "rite.draconic-test",
            additional ?? new
            {
                allow = true,
                targetSpeciesId = "species.dragonborn",
                targetAncestryId = "ancestry.dragonborn.red"
            });
    }

    private static NativeOriginRebirthResult Rebirth(NativeOriginProfile original,
        NativeOriginProposal proposal, INativeOriginAuthority? authority = null) =>
        new NativeOriginRebirthEngine(Catalog()).Execute(
            new(original, proposal, "rite.draconic-test", Scope), authority);

    [Theory]
    [InlineData("creature-type.plant", "plant-reproduction")]
    [InlineData("creature-type.construct", "construct-assembly")]
    [InlineData("creature-type.fiend", "spawning")]
    [InlineData("creature-type.fiend", "biological")]
    public void SelectedCreatureTypeMethodsAreAvailableIndependently(
        string type, string method)
    {
        var evaluator = new NativeOriginMethodAvailabilityEvaluator(Catalog());
        var availability = evaluator.Resolve(type,
            new("rule.creature-origination", method));
        Assert.Equal(NativeOriginMethodDefaultStatus.Available, availability.Status);
    }

    [Fact]
    public void AbsenceOfMethodDefaultDoesNotBecomeAProhibition()
    {
        var evaluator = new NativeOriginMethodAvailabilityEvaluator(Catalog());
        Assert.Equal(NativeOriginMethodDefaultStatus.NotGranted,
            evaluator.Resolve("creature-type.fiend",
                new("rule.creature-origination", "construct-assembly")).Status);
        Assert.Equal(NativeOriginMethodDefaultStatus.Unresolved,
            evaluator.Resolve("creature-type.unknown",
                new("rule.creature-origination", "biological")).Status);
    }

    private static NativeOriginConstructionResult SyntheticProcess(
        string id, string method, string creatureType,
        IReadOnlyList<NativeOriginContributor> contributors,
        IReadOnlyList<NativeOriginInputRequirement> requirements,
        bool approveMethod = false)
    {
        var source = new NativeOriginSourceReference(NativeOriginSourceKind.Custom,
            "fixture." + id);
        var candidate = new NativeOriginCandidate("synthetic:" + id,
            new(NativeOriginCharacteristicKind.Intrinsic,
                new(source, "/creatureType"), "fixture." + id + "#type",
                IntrinsicSlotId: "creatureType"),
            new(NativeOriginEligibility.Unreviewed),
            OfferedValue: JsonSerializer.SerializeToElement(creatureType));
        var decisions = new List<NativeTableRuleDecision>
        {
            Rule("process-type-" + id, "process-type", id,
                new { creatureTypeId = "creature-type." + creatureType }),
            Rule("configuration-" + id, "configuration", id,
                new { speciesId = "fixture." + id }),
            Rule("candidate-" + id, "candidate", candidate.Id,
                new { eligibility = "eligible" })
        };
        decisions.AddRange(requirements.Select(x =>
            Rule("require-" + x.Id, "input-requirement", x.Id,
                new { minimumCount = x.MinimumCount, maximumCount = x.MaximumCount })));
        if (approveMethod)
            decisions.Add(Rule("allow-method-" + id, "method", method, new { allow = true }));
        var proposal = new NativeOriginProposal(id, NativeOriginIntent.Custom,
            new("rule.creature-origination", method), contributors, [candidate],
            [new("synthetic-manifest:" + id, candidate.Id)],
            TableDecisions: decisions,
            InputRequirements: requirements,
            ProcessCreatureTypeId: "creature-type." + creatureType);
        return new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, Scope), new Authority());
    }

    private static NativeOriginContributor Input(string id, NativeOriginInputKind role,
        string source, string? processRole = null, bool native = false) =>
        new(id, new(role, processRole),
            new(native ? NativeOriginSourceKind.Native : NativeOriginSourceKind.Custom, source));

    [Fact]
    public void PlantReproductionUsesNonAnimalInputsWithoutGrantingEverySourceTrait()
    {
        var requirements = new NativeOriginInputRequirement[]
        {
            new("spore", new(NativeOriginInputKind.ProcessSpecific, "spore"), 1, 2),
            new("growth-medium", new(NativeOriginInputKind.Material), 1, 1)
        };
        var inputs = new[]
        {
            Input("spore", NativeOriginInputKind.ProcessSpecific, "fixture.spores", "spore"),
            Input("soil", NativeOriginInputKind.Material, "fixture.soil")
        };
        var result = SyntheticProcess("plant", "plant-reproduction", "plant", inputs,
            requirements);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Single(result.Profile!.Underlying.InherentGrants);
        Assert.Equal("fixture.plant", result.Profile.Underlying.SpeciesId);
        var incomplete = SyntheticProcess("plant-missing", "plant-reproduction", "plant",
            [inputs[0]], requirements);
        Assert.Contains(incomplete.Issues, x => x.Code == "process-input-incomplete");
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, incomplete.Status);
    }

    [Fact]
    public void ConstructAssemblyDoesNotInheritCreatorsSpeciesOrMaterialTraits()
    {
        var requirements = new NativeOriginInputRequirement[]
        {
            new("creator", new(NativeOriginInputKind.Creator), 1, 1),
            new("material", new(NativeOriginInputKind.Material), 1, 2),
            new("design", new(NativeOriginInputKind.ProcessSpecific, "blueprint"), 1, 1),
            new("animator", new(NativeOriginInputKind.Animator), 1, 1)
        };
        var inputs = new[]
        {
            Input("creator", NativeOriginInputKind.Creator, "species.human", native: true),
            Input("metal", NativeOriginInputKind.Material, "fixture.iron"),
            Input("design", NativeOriginInputKind.ProcessSpecific, "fixture.blueprint", "blueprint"),
            Input("animator", NativeOriginInputKind.Animator, "fixture.spark")
        };
        var result = SyntheticProcess("construct", "construct-assembly", "construct",
            inputs, requirements);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Single(result.Profile!.Underlying.InherentGrants);
        Assert.DoesNotContain(result.Profile.Underlying.InherentGrants,
            x => x.Identity.Occurrence.Source.Id == "species.human");
        Assert.Equal(4, result.EvaluatedProposal.Contributors.Count);
    }

    [Fact]
    public void FiendSpawningAndBiologicalReproductionRemainSeparateProcesses()
    {
        var spawn = SyntheticProcess("fiend-spawn", "spawning", "fiend",
            [Input("planar", NativeOriginInputKind.MagicalSource, "fixture.portal")],
            [new("planar", new(NativeOriginInputKind.MagicalSource), 1, 1)]);
        var biological = SyntheticProcess("fiend-biological", "biological", "fiend",
            [Input("source", NativeOriginInputKind.BiologicalParent, "fixture.fiend-parent")],
            [new("parent", new(NativeOriginInputKind.BiologicalParent), 1, 1)]);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, spawn.Status);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, biological.Status);
        Assert.DoesNotContain(spawn.EvaluatedProposal.Contributors,
            x => x.Role.Kind == NativeOriginInputKind.BiologicalParent);
        Assert.DoesNotContain(biological.EvaluatedProposal.Contributors,
            x => x.Role.Kind == NativeOriginInputKind.MagicalSource);
        Assert.Single(spawn.Profile!.Underlying.InherentGrants);
    }

    [Fact]
    public void UnverifiedProcessTypeIsNeverTrustedAsAMethodAuthorization()
    {
        var source = Input("spore", NativeOriginInputKind.PlantSource, "fixture.seed");
        var result = SyntheticProcess("unverified-type", "plant-reproduction", "plant",
            [source], []) ;
        var proposal = result.EvaluatedProposal with
        {
            TableDecisions = result.EvaluatedProposal.TableDecisions!
                .Where(d => d.Target.Kind != "process-type").ToArray()
        };
        var rejected = new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, Scope), new Authority());
        Assert.Contains(rejected.Issues, x => x.Code == "process-type-unverified");
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, rejected.Status);
    }

    [Fact]
    public void AuthorizedHumanToRedDragonbornRiteReplacesTypeAndPreservesProvenance()
    {
        var original = Human();
        var independent = new NativeOriginEffectiveGrant("trained-class", "trained-source",
            new(NativeOriginCharacteristicKind.Trait,
                new(new(NativeOriginSourceKind.Custom, "class.training"), "/traits/0"),
                "class.training#trait"), ["class-offer"]);
        original = original with { IndependentGrants = [independent] };
        var result = Rebirth(original, Target(decisions: [Authorize()]), new Authority());
        Assert.True(result.IsMechanicallyAuthorized,
            string.Join("; ", result.Issues.Select(x => x.Code)));
        var next = result.ResultingProfile!;
        Assert.Equal(original.Id, next.Id);
        Assert.Equal("species.dragonborn", next.Underlying.SpeciesId);
        Assert.Equal("ancestry.dragonborn.red", next.Underlying.AncestryId);
        Assert.Contains(next.Underlying.InherentGrants, x =>
            x.Identity.IntrinsicSlotId == "creatureType" &&
            x.EffectiveValue?.GetString() == "dragon");
        Assert.DoesNotContain(next.Underlying.InherentGrants, x =>
            x.Identity.Occurrence.Source.Id == "species.human");
        Assert.Equal(independent, Assert.Single(next.IndependentGrants));
        Assert.Equal(original.History.Count + 1, next.History.Count);
        Assert.Equal(original.History, next.History.Take(original.History.Count));
        var ev = next.History.Last();
        Assert.Equal(NativeOriginEventStage.Completed, ev.Stage);
        Assert.True(ev.ReplacesConfiguration);
        Assert.Equal(original.Underlying, ev.PreviousConfiguration);
        Assert.Equal(next.Underlying, next.Current);
    }

    [Fact]
    public void UnauthorizedAndIncompleteRitesLeaveTheOldProfileUnchanged()
    {
        var old = Human();
        var noRite = Rebirth(old, Target(), new Authority());
        Assert.False(noRite.IsMechanicallyAuthorized);
        Assert.Null(noRite.ResultingProfile);
        Assert.Contains(noRite.Issues, x => x.Code == "rebirth-authorization-unresolved");

        var incompleteTarget = Target("bad-target",
            [Authorize("bad-target")], ancestry: "ancestry.dragonborn.not-present");
        var rejected = Rebirth(old, incompleteTarget, new Authority());
        Assert.False(rejected.IsMechanicallyAuthorized);
        Assert.Null(rejected.ResultingProfile);
        Assert.DoesNotContain(old.History, x => x.Id == "rite:bad-target");
    }

    [Fact]
    public void DmModifiedRiteMayRetainASpecificallyAuthorizedEarlierGrant()
    {
        var old = Human();
        var retained = old.Underlying.InherentGrants.Single(x =>
            x.Identity.Occurrence.OccurrencePath == "/traits/2");
        var rite = Authorize("retain-human-trait", new
        {
            allow = true,
            targetSpeciesId = "species.dragonborn",
            targetAncestryId = "ancestry.dragonborn.red",
            retainPreviousGrantIds = new[] { retained.Id }
        });
        var result = Rebirth(old, Target("retain-human-trait", [rite]), new Authority());
        Assert.True(result.IsMechanicallyAuthorized,
            string.Join("; ", result.Issues.Select(x => x.Code)));
        Assert.Contains(result.ResultingProfile!.Underlying.InherentGrants,
            x => x.Id == retained.Id && x.Identity.Occurrence.Source.Id == "species.human");
        Assert.DoesNotContain(result.ResultingProfile.Underlying.InherentGrants,
            x => x.Identity.Occurrence.Source.Id == "species.human" && x.Id != retained.Id);
    }

    [Fact]
    public void RepeatedRitesReplaceTheMostRecentBaselineNotTheOriginal()
    {
        var first = Rebirth(Human(), Target(decisions: [Authorize()]), new Authority());
        Assert.True(first.IsMechanicallyAuthorized);
        var target = Target("rebirth-bronze",
            [Rule("bronze-auth", "rite", "rite.draconic-test",
                new { allow = true, targetSpeciesId = "species.dragonborn",
                    targetAncestryId = "ancestry.dragonborn.bronze" })],
            ancestry: "ancestry.dragonborn.bronze");
        var second = Rebirth(first.ResultingProfile!, target, new Authority());
        Assert.True(second.IsMechanicallyAuthorized,
            string.Join("; ", second.Issues.Select(x => x.Code)));
        Assert.Equal("ancestry.dragonborn.bronze", second.ResultingProfile!.Underlying.AncestryId);
        Assert.DoesNotContain(second.ResultingProfile.Underlying.InherentGrants, x =>
            x.Identity.Occurrence.Source.Id == "ancestry.dragonborn.red");
        Assert.Equal(2, second.ResultingProfile.History.Count(x =>
            x.ReplacesConfiguration && x.Stage == NativeOriginEventStage.Completed));
        Assert.Equal("ancestry.dragonborn.red",
            second.ResultingProfile.History.Last().PreviousConfiguration!.AncestryId);
    }

    [Fact]
    public void RiteDuringTemporaryFormRequiresExplicitInteractionAuthorization()
    {
        var old = Human();
        var current = new NativeOriginCurrentConfiguration("species.wolf", null, [],
            "origin:wolf-form");
        var form = new NativeOriginActiveTemporaryTransformation("wolf", "wolf-form",
            new(NativeOriginSourceKind.Custom, "fixture.lycanthropy"), "activation");
        old = old with { Current = current, ActiveTemporaryTransformation = form };
        var unresolved = Rebirth(old, Target(decisions: [Authorize()]), new Authority());
        Assert.Null(unresolved.ResultingProfile);
        Assert.Contains(unresolved.Issues, x =>
            x.Code == "rebirth-temporary-interaction-unresolved");
        var decision = Rule("form-interaction", "rite-form-interaction",
            "rite.draconic-test", new
            {
                preserveEffectiveForm = true, effectiveFormId = "wolf-form"
            });
        var resolved = Rebirth(old, Target(decisions: [Authorize(), decision]),
            new Authority());
        Assert.True(resolved.IsMechanicallyAuthorized,
            string.Join("; ", resolved.Issues.Select(x => x.Code)));
        Assert.Equal(current, resolved.ResultingProfile!.Current);
        Assert.Equal("species.dragonborn", resolved.ResultingProfile.Underlying.SpeciesId);
        Assert.Equal(form, resolved.ResultingProfile.ActiveTemporaryTransformation);
    }

    [Fact]
    public void RiteOnlyAncestryCannotBeSelectedThroughOrdinaryCreation()
    {
        var root = Path.Combine(Path.GetTempPath(), "rules-core-p4-" + Guid.NewGuid());
        var native = Path.Combine(root, "first-party-001", "native");
        try
        {
            Directory.CreateDirectory(native);
            File.WriteAllText(Path.Combine(root, "first-party-001", "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/","schemaVersion":1}""");
            File.WriteAllText(Path.Combine(native, "fixture.json"), """
                [
                  {"id":"species.fixture","kind":"species","name":"Fixture Species",
                   "selectable":true,"creatureType":"humanoid","walkingSpeedFeet":30},
                  {"id":"ancestry.fixture.rite","kind":"ancestry","name":"Rite-only Fixture",
                   "parent":"species.fixture","selectable":false,
                   "acquisitionMethods":["rite-of-rebirth"],"size":"medium"},
                  {"id":"rule.creature-origination","kind":"rule","name":"Origination Fixture",
                   "methodDefinitions":[
                     {"id":"biological","name":"Biological"},
                     {"id":"rite-of-rebirth","name":"Rite","stage":"post-origination",
                      "createsNewIndividual":false,
                      "operation":"replace-complete-species-and-ancestry-configuration"}]}
                ]
                """);
            var catalog = NativeCatalogLoader.Load(
                NativeRulesetLayout.Resolve(root, "first-party-001"));
            NativeOriginProposal Select(string method) =>
                new("selection", NativeOriginIntent.Direct,
                    new("rule.creature-origination", method), [], [], [],
                    SelectedSpeciesId: "species.fixture",
                    SelectedAncestryId: "ancestry.fixture.rite");
            var deconstructor = new NativeOriginSourceDeconstructor(catalog);
            var normal = deconstructor.Deconstruct(Select("biological"));
            var rebirth = deconstructor.Deconstruct(Select("rite-of-rebirth"));
            Assert.Contains(normal.Issues, x => x.Code == "origin-source-restricted");
            Assert.DoesNotContain(rebirth.Issues, x => x.Code == "origin-source-restricted");
            Assert.Contains(rebirth.Proposal.Candidates, x =>
                x.Identity.Occurrence.Source.Id == "ancestry.fixture.rite" &&
                x.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
