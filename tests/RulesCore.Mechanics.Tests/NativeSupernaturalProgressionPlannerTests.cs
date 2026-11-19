using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Source-qualified synthetic definitions only. No fixture is registered as
/// a production supernatural rule or gameplay acquisition authority.
/// </summary>
public sealed class NativeSupernaturalProgressionPlannerTests
{
    private sealed class Authority : INativeSupernaturalProgressionAuthority
    {
        public bool DenyDefinition, DenyAcquisition, DenyAdvance, DenyEvent,
            DenyExpression, DenyJournal, DenyStoredInfluence;
        public NativeSupernaturalEligibilityVerdict Eligibility =
            NativeSupernaturalEligibilityVerdict.Satisfied;
        public bool AuthorizesPrimary(string id, NativeCharacterRequest character) =>
            id == "character.test";
        public bool AuthorizesSheet(string id, NativePersistentCharacterSheet sheet) => true;
        public bool AuthorizesProgression(string id, string sheet,
            NativeSheetEarnedProgression item) => true;
        public bool AuthorizesGrant(string id, string sheet,
            NativeSheetEarnedProgression progression, NativeSheetEarnedGrant grant) => true;
        public bool AuthorizesApplicability(string id, string sheet,
            NativeSheetGrantApplicability decision) => true;
        public bool AuthorizesInfluence(string id, NativeCharacterPersistentInfluence item) =>
            !DenyStoredInfluence;
        public bool AuthorizesExpression(string id, NativeCharacterPersistentInfluence item) => true;
        public bool AuthorizesEffect(string id, string influence,
            NativeCharacterInfluenceEffect effect) => true;
        public bool AuthorizesInfluenceProgression(string id, string influence,
            NativeSheetEarnedProgression record) => true;
        public bool AuthorizesInfluenceGrant(string id, string influence,
            NativeSheetEarnedProgression record, NativeSheetEarnedGrant grant) => true;
        public bool AuthorizesInfluenceApplicability(string id, string influence,
            NativeInfluenceGrantApplicability rule) => true;
        public bool AuthorizesJournal(string id, NativeSupernaturalTransitionJournal journal) =>
            !DenyJournal;
        public bool AuthorizesDefinition(string id, NativeSupernaturalDefinition definition,
            NativeOriginDecisionBasis basis) => !DenyDefinition;
        public bool AuthorizesAcquisition(string id, NativeSupernaturalDefinition definition,
            NativeSupernaturalTransitionRequest request) => !DenyAcquisition;
        public bool AuthorizesAdvancement(string id, NativeCharacterPersistentInfluence influence,
            NativeSupernaturalProgressionNode node, NativeSupernaturalTransitionRequest request) =>
            !DenyAdvance;
        public bool AuthorizesExpressionChange(string id, NativeCharacterPersistentInfluence influence,
            NativeSupernaturalTransitionRequest request) => !DenyExpression;
        public bool AuthorizesEventRequirement(string id, string influence, string key,
            string evidence, NativeSupernaturalTransitionRequest request) => !DenyEvent;
        public NativeSupernaturalEligibilityVerdict EvaluateRequirement(string id,
            string influence, string key, NativeSupernaturalTransitionRequest request) => Eligibility;
    }

    private static NativeOriginSourceReference Source(string id = "synthetic.supernatural") =>
        new(NativeOriginSourceKind.Native, id, "synthetic-v1");
    private static NativeOriginSourceOccurrence Occ(string path, string source = "synthetic.supernatural") =>
        new(Source(source), path);
    private static NativeOriginDecisionBasis Basis() =>
        new(NativeOriginAdjudication.Supported, NativeRuleReference: "rule.synthetic.progression");
    private static Dictionary<string, string> Expression(string phase) =>
        new(StringComparer.Ordinal) { ["phase"] = phase };

    private static NativeSupernaturalGrantDefinition Grant(string id,
        IReadOnlyList<NativeSupernaturalGrantUseRule>? uses = null,
        string source = "synthetic.supernatural") =>
        new(id, "capability." + id, Occ("grants." + id, source), UseRules: uses);

    private static NativeSupernaturalProgressionNode Node(string id,
        bool immediate = false, int? level = null, string? prerequisite = null,
        string? exclusive = null, string? eventKey = null,
        IReadOnlyList<NativeSupernaturalFiniteChoice>? choices = null,
        IReadOnlyList<NativeSupernaturalGrantDefinition>? grants = null,
        IReadOnlyList<NativeCharacterInfluenceEffect>? effects = null,
        string source = "synthetic.supernatural",
        string? eligibilityKey = null) =>
        new(id, Occ("nodes." + id, source), OnAcquisition: immediate,
            MinimumCharacterLevel: level,
            RequiredNodeIds: prerequisite is null ? null : [prerequisite],
            ExclusiveGroup: exclusive,
            RequiredEventKeys: eventKey is null ? null : [eventKey],
            RequiredEligibilityKeys: eligibilityKey is null ? null : [eligibilityKey],
            Choices: choices, Grants: grants, Effects: effects);

    private static NativeCharacterInfluenceEffect Effect(string id) =>
        new(id, Occ("effects." + id), "rule.effect", Basis(),
            new Dictionary<string, string>(StringComparer.Ordinal),
            [NativeCharacterSheetResolver.PrimarySheetId],
            NativeInfluenceCharacteristic.AbilityScore,
            NativeInfluenceOperation.Replace, Key: "strength", NumericValue: 15);

    private static NativeSupernaturalDefinition Definition(
        IReadOnlyList<NativeSupernaturalProgressionNode>? nodes = null,
        IReadOnlyList<NativeCharacterInfluenceEffect>? effects = null,
        string source = "synthetic.supernatural") =>
        new("definition." + source, Source(source), Expression("dormant"),
            nodes ?? [], effects);

    private static NativeCharacterState Character(int level = 2) =>
        new("character.test", new NativeCharacterRequest(
            "first-party-001", "synthetic-catalog",
            new Dictionary<string, int> { ["strength"] = 10 },
            [new NativeAdvancement("class.test", level, "level." + level)]),
            []);

    private static NativeSupernaturalTransitionJournal Journal() =>
        new("character.test", 0, [], []);

    private static NativeSupernaturalTransitionRequest Request(
        NativeSupernaturalTransitionKind kind, string eventId, long revision = 0,
        string instance = "influence.one", string? nodeId = null,
        IReadOnlyDictionary<string, string>? choices = null,
        IReadOnlyDictionary<string, string>? eventEvidence = null,
        IReadOnlyDictionary<string, string>? expression = null) =>
        new("character.test", revision, eventId, kind, instance, "receipt." + eventId,
            Basis(), nodeId, choices, eventEvidence, expression);

    private static NativeSupernaturalTransitionPlan Plan(
        NativeCharacterState state, NativeSupernaturalTransitionJournal journal,
        NativeSupernaturalDefinition definition,
        NativeSupernaturalTransitionRequest request, Authority? authority = null) =>
        NativeSupernaturalProgressionPlanner.Plan(state, journal, definition, request,
            authority ?? new Authority());

    private static (NativeCharacterState State, NativeSupernaturalTransitionJournal Journal)
        Committed(NativeSupernaturalTransitionPlan plan)
    {
        Assert.Equal("planned", plan.Status);
        return (Assert.IsType<NativeCharacterState>(plan.ProposedState),
            Assert.IsType<NativeSupernaturalTransitionJournal>(plan.ProposedJournal));
    }

    [Fact]
    public void AcquisitionAwardsOnlyImmediateNodeAndAuthoredPersistentEffect()
    {
        var start = Character();
        var definition = Definition([Node("initial", immediate: true,
            grants: [Grant("initial")]), Node("later", grants: [Grant("later")])],
            [Effect("permanent")]);
        var plan = Plan(start, Journal(), definition,
            Request(NativeSupernaturalTransitionKind.Acquire, "event.acquire"));
        var (state, journal) = Committed(plan);
        Assert.Single(plan.NewProgression);
        Assert.Single(plan.NewGrants);
        Assert.Equal("capability.initial", plan.NewGrants[0].Entitlement.DefinitionId);
        Assert.Single(state.PersistentInfluences!);
        Assert.Single(state.PersistentInfluences![0].Effects);
        Assert.Equal(1, journal.Revision);
        Assert.Equal(10, start.PrimaryMechanics.AbilityScores["strength"]);
        var resolved = NativeCharacterInfluenceResolver.Resolve(state, "@primary", new Authority());
        Assert.Equal("resolved", resolved.Status);
        Assert.Equal(15, resolved.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.DoesNotContain(resolved.Grants, x => x.Entitlement.DefinitionId == "capability.later");
    }

    [Fact]
    public void DormantAcquisitionDoesNotAwardLaterNodeOrAlterPrimary()
    {
        var definition = Definition([Node("latent", grants: [Grant("latent")])]);
        var (state, _) = Committed(Plan(Character(), Journal(), definition,
            Request(NativeSupernaturalTransitionKind.Acquire, "acquire")));
        Assert.Empty(state.PersistentInfluences![0].Progression!);
        Assert.Empty(state.PersistentInfluences![0].Effects);
        Assert.Equal(10, state.PrimaryMechanics.AbilityScores["strength"]);
    }

    [Fact]
    public void EventGateRequiresVerifiedDistinctUnconsumedEvidence()
    {
        var definition = Definition([Node("event1", eventKey: "milestone",
            grants: [Grant("one")]), Node("event2", eventKey: "milestone",
            grants: [Grant("two")])]);
        var (state, ledger) = Committed(Plan(Character(), Journal(), definition,
            Request(NativeSupernaturalTransitionKind.Acquire, "acquire")));
        var request = Request(NativeSupernaturalTransitionKind.Advance, "advance1",
            ledger.Revision, nodeId: "event1",
            eventEvidence: new Dictionary<string, string> { ["milestone"] = "actual-event" });
        Assert.Equal("unresolved", Plan(state, ledger, definition, request,
            new Authority { DenyEvent = true }).Status);
        var (progressed, next) = Committed(Plan(state, ledger, definition, request));
        Assert.Single(next.ConsumedEligibilityEvidenceIds);
        var replay = Request(NativeSupernaturalTransitionKind.Advance, "advance2",
            next.Revision, nodeId: "event2",
            eventEvidence: new Dictionary<string, string> { ["milestone"] = "actual-event" });
        Assert.Equal("invalid", Plan(progressed, next, definition, replay).Status);
    }

    [Fact]
    public void LevelGateUsesVerifiedCharacterLevelWithoutSpendingClassLevels()
    {
        var definition = Definition([Node("high", level: 4, grants: [Grant("high")])]);
        var (state, ledger) = Committed(Plan(Character(2), Journal(), definition,
            Request(NativeSupernaturalTransitionKind.Acquire, "acquire")));
        var advance = Request(NativeSupernaturalTransitionKind.Advance, "level-event",
            ledger.Revision, nodeId: "high");
        Assert.Equal("ineligible", Plan(state, ledger, definition, advance).Status);
        var higher = state with { PrimaryMechanics = Character(4).PrimaryMechanics };
        var (result, _) = Committed(Plan(higher, ledger, definition, advance));
        Assert.Equal(4, result.PrimaryMechanics.Advancements[0].Level);
        Assert.Single(result.PersistentInfluences![0].Progression!);
    }

    [Fact]
    public void BranchingProgressionAndFiniteChoiceAreAuthoritative()
    {
        var options = new[] { new NativeSupernaturalFiniteChoice("aspect", ["red", "blue"]) };
        var definition = Definition([
            Node("left", exclusive: "fork", choices: options, grants: [Grant("left")]),
            Node("right", exclusive: "fork", grants: [Grant("right")])
        ]);
        var (state, ledger) = Committed(Plan(Character(), Journal(), definition,
            Request(NativeSupernaturalTransitionKind.Acquire, "acquire")));
        var bad = Request(NativeSupernaturalTransitionKind.Advance, "bad", ledger.Revision,
            nodeId: "left", choices: new Dictionary<string, string> { ["aspect"] = "invented" });
        Assert.Equal("invalid", Plan(state, ledger, definition, bad).Status);
        var left = Request(NativeSupernaturalTransitionKind.Advance, "left", ledger.Revision,
            nodeId: "left", choices: new Dictionary<string, string> { ["aspect"] = "red" });
        var (progressed, next) = Committed(Plan(state, ledger, definition, left));
        Assert.Equal("red", progressed.PersistentInfluences![0].Progression![0].Choices!["aspect"]);
        Assert.Equal("ineligible", Plan(progressed, next, definition,
            Request(NativeSupernaturalTransitionKind.Advance, "right", next.Revision,
                nodeId: "right")).Status);
        Assert.Equal("invalid", Plan(progressed, next, definition,
            Request(NativeSupernaturalTransitionKind.Advance, "again", next.Revision,
                nodeId: "left", choices: new Dictionary<string, string> { ["aspect"] = "blue" })).Status);
    }

    [Fact]
    public void TwoIndependentAcquisitionsAdvanceWithDifferentSourceOwnership()
    {
        var one = Definition([Node("step", grants: [Grant("first")])]);
        var two = Definition([Node("step", grants: [Grant("second", source: "synthetic.other")],
            source: "synthetic.other")], source: "synthetic.other");
        var (state1, ledger1) = Committed(Plan(Character(), Journal(), one,
            Request(NativeSupernaturalTransitionKind.Acquire, "acquire1")));
        var (state2, ledger2) = Committed(Plan(state1, ledger1, two,
            Request(NativeSupernaturalTransitionKind.Acquire, "acquire2", ledger1.Revision,
                instance: "influence.two")));
        Assert.Equal("invalid", Plan(state2, ledger2, one,
            Request(NativeSupernaturalTransitionKind.Advance, "wrong", ledger2.Revision,
                instance: "influence.two", nodeId: "step")).Status);
        var (state3, _) = Committed(Plan(state2, ledger2, two,
            Request(NativeSupernaturalTransitionKind.Advance, "advance2", ledger2.Revision,
                instance: "influence.two", nodeId: "step")));
        Assert.Empty(state3.PersistentInfluences![0].Progression!);
        Assert.Single(state3.PersistentInfluences![1].Progression!);
    }

    [Fact]
    public void ExpressionSuppressionAndRestorationRetainEarnedProgression()
    {
        var useRules = new[]
        {
            new NativeSupernaturalGrantUseRule(NativeOriginGrantApplicabilityState.Suppressed,
                Basis(), Expression("dormant")),
            new NativeSupernaturalGrantUseRule(NativeOriginGrantApplicabilityState.Applicable,
                Basis(), Expression("active"))
        };
        var definition = Definition([Node("initial", immediate: true,
            grants: [Grant("feature", useRules)])]);
        var (state1, journal1) = Committed(Plan(Character(), Journal(), definition,
            Request(NativeSupernaturalTransitionKind.Acquire, "acquire")));
        var view1 = NativeCharacterInfluenceResolver.Resolve(state1, "@primary", new Authority());
        Assert.Equal(NativeOriginGrantApplicabilityState.Suppressed, Assert.Single(view1.Grants).Applicability);
        var (state2, journal2) = Committed(Plan(state1, journal1, definition,
            Request(NativeSupernaturalTransitionKind.ChangeExpression, "awaken",
                journal1.Revision, expression: Expression("active"))));
        var view2 = NativeCharacterInfluenceResolver.Resolve(state2, "@primary", new Authority());
        Assert.Equal(NativeOriginGrantApplicabilityState.Applicable, Assert.Single(view2.Grants).Applicability);
        var (state3, _) = Committed(Plan(state2, journal2, definition,
            Request(NativeSupernaturalTransitionKind.ChangeExpression, "suppress",
                journal2.Revision, expression: Expression("dormant"))));
        var view3 = NativeCharacterInfluenceResolver.Resolve(state3, "@primary", new Authority());
        Assert.Equal(NativeOriginGrantApplicabilityState.Suppressed, Assert.Single(view3.Grants).Applicability);
        Assert.Equal(state1.PersistentInfluences![0].Progression![0].Id,
            state3.PersistentInfluences![0].Progression![0].Id);
    }

    [Fact]
    public void ForgedSourceAuthorizationAndUnverifiedStateFailClosed()
    {
        var definition = Definition();
        var request = Request(NativeSupernaturalTransitionKind.Acquire, "acquire");
        Assert.Equal("unresolved", Plan(Character(), Journal(), definition, request,
            new Authority { DenyDefinition = true }).Status);
        Assert.Equal("unresolved", Plan(Character(), Journal(), definition, request,
            new Authority { DenyAcquisition = true }).Status);
        var (acquired, journal) = Committed(Plan(Character(), Journal(), definition, request));
        Assert.Equal("invalid", Plan(acquired, journal, definition,
            Request(NativeSupernaturalTransitionKind.ChangeExpression, "change",
                journal.Revision, expression: Expression("active")),
            new Authority { DenyStoredInfluence = true }).Status);
        Assert.Equal("unresolved", Plan(acquired, journal, definition,
            Request(NativeSupernaturalTransitionKind.ChangeExpression, "change",
                journal.Revision, expression: Expression("active")),
            new Authority { DenyExpression = true }).Status);
    }

    [Fact]
    public void DuplicateEventStaleRevisionAndDuplicateAcquisitionAreRejected()
    {
        var definition = Definition();
        var (state, ledger) = Committed(Plan(Character(), Journal(), definition,
            Request(NativeSupernaturalTransitionKind.Acquire, "acquire")));
        Assert.Equal("invalid", Plan(state, ledger, definition,
            Request(NativeSupernaturalTransitionKind.Acquire, "second", ledger.Revision)).Status);
        Assert.Equal("invalid", Plan(state, ledger, definition,
            Request(NativeSupernaturalTransitionKind.ChangeExpression, "acquire", ledger.Revision,
                expression: Expression("active"))).Status);
        Assert.Equal("invalid", Plan(state, ledger, definition,
            Request(NativeSupernaturalTransitionKind.ChangeExpression, "change", 0,
                expression: Expression("active"))).Status);
        Assert.Equal("unresolved", Plan(state, ledger, definition,
            Request(NativeSupernaturalTransitionKind.ChangeExpression, "change", ledger.Revision,
                expression: Expression("active")), new Authority { DenyJournal = true }).Status);
    }

    [Fact]
    public void UnderlyingCharacterChangesNeverResetUnrelatedSupernaturalGrants()
    {
        var definition = Definition([Node("initial", immediate: true,
            grants: [Grant("feature")])]);
        var (state, journal) = Committed(Plan(Character(2), Journal(), definition,
            Request(NativeSupernaturalTransitionKind.Acquire, "acquire")));
        var changed = state with { PrimaryMechanics = Character(6).PrimaryMechanics };
        var (advanced, _) = Committed(Plan(changed, journal, definition,
            Request(NativeSupernaturalTransitionKind.ChangeExpression, "change",
                journal.Revision, expression: Expression("active"))));
        Assert.Equal(6, advanced.PrimaryMechanics.Advancements[0].Level);
        Assert.Equal("capability.feature",
            advanced.PersistentInfluences![0].Progression![0].Grants[0].DefinitionId);
        Assert.Equal(2, state.PrimaryMechanics.Advancements[0].Level);
    }

    [Fact]
    public void UnresolvedEligibilityNeverAwardsAProgression()
    {
        var definition = Definition([Node("conditioned", eligibilityKey: "condition.test",
            grants: [Grant("conditional")])]);
        var (state, ledger) = Committed(Plan(Character(), Journal(), definition,
            Request(NativeSupernaturalTransitionKind.Acquire, "acquire")));
        var request = Request(NativeSupernaturalTransitionKind.Advance, "advance",
            ledger.Revision, nodeId: "conditioned");
        Assert.Equal("unresolved", Plan(state, ledger, definition, request,
            new Authority { Eligibility = NativeSupernaturalEligibilityVerdict.Unresolved }).Status);
        Assert.Equal("ineligible", Plan(state, ledger, definition, request,
            new Authority { Eligibility = NativeSupernaturalEligibilityVerdict.Failed }).Status);
    }
}
