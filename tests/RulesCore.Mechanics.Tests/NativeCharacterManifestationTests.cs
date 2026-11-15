using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Synthetic form and source receipts only. These fixtures do not approve any
/// first-party werewolf, HP, Class or resource-transfer gameplay mechanics.
/// </summary>
public sealed class NativeCharacterManifestationTests
{
    private sealed class Authority : INativeCharacterManifestationAuthority
    {
        public bool DenyLedger { get; set; }
        public bool DenyInstance { get; set; }
        public bool DenyResource { get; set; }
        public bool DenyPolicy { get; set; }
        public bool DenyInteraction { get; set; }
        public bool DenyTransition { get; set; }
        public bool DenyInfluence { get; set; }
        public int TransitionCalls { get; private set; }

        public bool AuthorizesPrimary(string id, NativeCharacterRequest primary) => id == "character.1";
        public bool AuthorizesSheet(string id, NativePersistentCharacterSheet sheet) => id == "character.1";
        public bool AuthorizesProgression(string id, string owner, NativeSheetEarnedProgression progression) => true;
        public bool AuthorizesGrant(string id, string owner, NativeSheetEarnedProgression progression,
            NativeSheetEarnedGrant grant) => true;
        public bool AuthorizesApplicability(string id, string sheet, NativeSheetGrantApplicability decision) => true;
        public bool AuthorizesInfluence(string id, NativeCharacterPersistentInfluence influence) => !DenyInfluence;
        public bool AuthorizesExpression(string id, NativeCharacterPersistentInfluence influence) => true;
        public bool AuthorizesEffect(string id, string influence, NativeCharacterInfluenceEffect effect) => true;
        public bool AuthorizesInfluenceProgression(string id, string influence,
            NativeSheetEarnedProgression progression) => true;
        public bool AuthorizesInfluenceGrant(string id, string influence,
            NativeSheetEarnedProgression progression, NativeSheetEarnedGrant grant) => true;
        public bool AuthorizesInfluenceApplicability(string id, string influence,
            NativeInfluenceGrantApplicability decision) => true;
        public bool AuthorizesLedger(string id, NativeManifestationLedger ledger) => !DenyLedger;
        public bool AuthorizesInstance(string id, NativeManifestationInstance instance,
            NativePersistentCharacterSheet sheet) => !DenyInstance && instance.SheetId == sheet.Id;
        public bool AuthorizesResource(string id, NativeManifestationResourceState resource) => !DenyResource;
        public bool AuthorizesResourcePolicy(string id, NativeManifestationInstance instance,
            NativeManifestationResourcePolicy policy) => !DenyPolicy;
        public bool AuthorizesInteraction(string id, NativeManifestationInteractionDecision decision) =>
            !DenyInteraction;
        public bool AuthorizesTransition(string id, NativeManifestationLedger ledger,
            NativeManifestationTransitionRequest request, NativeManifestationTransitionStep step)
        {
            TransitionCalls++;
            return !DenyTransition && request.ExpectedRevision == ledger.Revision &&
                request.CharacterId == id;
        }
    }

    private static NativeOriginSourceReference Source() =>
        new(NativeOriginSourceKind.Native, "test.lycanthropy", "v1");
    private static NativeOriginSourceOccurrence Occurrence(string key) =>
        new(Source(), key);
    private static NativeOriginDecisionBasis Rule() =>
        new(NativeOriginAdjudication.Supported, NativeRuleReference: "rule.test.forms");

    private static NativeSheetEarnedProgression Progress(string id, string grantId) =>
        new("progress." + id, "progress.definition." + id, Occurrence("progress." + id),
            "receipt.progress." + id,
            [new(grantId, "grant.definition." + grantId,
                Occurrence("grants." + grantId), "receipt." + grantId)]);

    private static NativePersistentCharacterSheet Werewolf() => new(
        "werewolf", NativeCharacterSheetResolver.PrimarySheetId,
        "definition.werewolf", Source(), "receipt.werewolf",
        Progression: [Progress("shared", "grant.shared")],
        GrantApplicability: [new("grant.shared", NativeOriginGrantApplicabilityState.Applicable, Rule())]);
    private static NativePersistentCharacterSheet Wolf() => new(
        "wolf", "werewolf", "definition.wolf", Source(), "receipt.wolf",
        Overrides: new(AbilityScores: new(false, new Dictionary<string, int>
        {
            ["strength"] = 17
        })),
        Progression: [Progress("wolf", "grant.wolf")],
        GrantApplicability: [new("grant.wolf", NativeOriginGrantApplicabilityState.Applicable, Rule())]);
    private static NativePersistentCharacterSheet Hybrid() => new(
        "hybrid", "werewolf", "definition.hybrid", Source(), "receipt.hybrid",
        Overrides: new(AbilityScores: new(false, new Dictionary<string, int>
        {
            ["dexterity"] = 16
        })));

    private static NativeCharacterState Character(
        IReadOnlyList<NativeCharacterPersistentInfluence>? influences = null,
        IReadOnlyList<NativePersistentCharacterSheet>? sheets = null,
        int level = 1, int speed = 30) =>
        new("character.1", new NativeCharacterRequest("first-party-001", "synthetic",
            new Dictionary<string, int> {
                ["strength"] = 10, ["dexterity"] = 12, ["constitution"] = 14,
                ["intelligence"] = 12, ["wisdom"] = 14, ["charisma"] = 10
            },
            [new NativeAdvancement("class.synthetic", level, "level.1")],
            CurrentResources: new Dictionary<string, int> { ["resource.primary"] = 8 },
            BaseWalkSpeed: speed),
            sheets ?? [Werewolf(), Wolf(), Hybrid()], influences);

    private static NativeManifestationResourceState PrimaryPool(int current = 8, int maximum = 10) =>
        new("resource.primary", "resource.definition.primary", Occurrence("resources.primary"),
            "receipt.resource.primary", NativeManifestationResourceOwner.Primary,
            NativeCharacterSheetResolver.PrimarySheetId, current, maximum);
    private static NativeManifestationResourceState SharedPool() =>
        new("resource.shared", "resource.definition.shared", Occurrence("resources.shared"),
            "receipt.resource.shared", NativeManifestationResourceOwner.Sheet, "werewolf", 2, 5);
    private static NativeManifestationResourceState WolfPool(string instanceId) =>
        new("resource.wolf." + instanceId, "resource.definition.wolf",
            Occurrence("resources.wolf"), "receipt.resource.wolf." + instanceId,
            NativeManifestationResourceOwner.Manifestation, instanceId, 3, 3);

    private static NativeManifestationLedger Ledger() =>
        new("character.1", 7, [], [PrimaryPool(), SharedPool()], []);

    private static NativeManifestationInstance Form(string id, string sheet, string eventId,
        IReadOnlyList<NativeManifestationResourcePolicy>? policies = null) =>
        new(id, sheet, "transformation." + sheet, Occurrence("forms." + sheet),
            null, eventId, "receipt.activate." + eventId, Rule(),
            NativeManifestationLifecycle.Active,
            NativeManifestationContribution.Contributing,
            new Dictionary<string, int> { ["captured"] = 4 },
            ["rule.test.end-on-event"], policies ?? [], eventId);

    private static NativeManifestationTransitionStep Activate(string id, string sheet, string eventId,
        bool addWolfPool = false) =>
        new(NativeManifestationTransitionAction.Activate, id, Occurrence("actions.activate"),
            Rule(), "receipt.transition." + eventId,
            Form(id, sheet, sheet == "wolf" ?
            eventId : eventId,
                sheet == "wolf" ? [
                    new("resource.primary", NativeManifestationResourceMode.AdjustMaximum, 3),
                    new("resource.shared", NativeManifestationResourceMode.CaptureMaximum,
                        CaptureKey: "captured"),
                    new("resource.wolf." + id, NativeManifestationResourceMode.Separate)
                ] : [new("resource.primary", NativeManifestationResourceMode.Inherit)]),
            addWolfPool ? [WolfPool(id)] : null);

    private static NativeManifestationTransitionStep Action(
        NativeManifestationTransitionAction action, string id) =>
        new(action, id, Occurrence("actions." + action), Rule(), "receipt.transition." + id);

    private static NativeManifestationTransitionPlan Plan(
        NativeCharacterState state, NativeManifestationLedger ledger, string eventId,
        params NativeManifestationTransitionStep[] steps) =>
        NativeCharacterManifestationTransitionPlanner.Plan(state, ledger,
            new(state.CharacterId, ledger.Revision, eventId, steps), new Authority());

    private static NativeManifestationLedger ActivatedWolf(
        NativeCharacterState? state = null, string id = "manifest.wolf", string eventId = "e1")
    {
        var plan = Plan(state ?? Character(), Ledger(), eventId, Activate(id, "wolf", eventId, true));
        Assert.Equal("planned", plan.Status);
        return plan.Proposed!;
    }

    private static NativeManifestationEffectiveCharacter Resolve(NativeCharacterState state,
        NativeManifestationLedger ledger, Authority? authority = null) =>
        NativeCharacterManifestationResolver.Resolve(state, ledger, authority ?? new Authority());

    [Fact]
    public void PersistentSheetsDoNotRequireOrImplyAnyManifestation()
    {
        var result = Resolve(Character(), Ledger());
        Assert.Equal("resolved", result.Status);
        Assert.Equal(NativeCharacterSheetResolver.PrimarySheetId, result.EffectiveSheetId);
        Assert.Null(result.ManifestationInstanceId);
        Assert.Empty(result.SheetProgression);
        Assert.Equal(10, result.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Single(result.Resources);
    }

    [Fact]
    public void WolfActivationReturnsPartialProjectionWithOwnedAdvancement()
    {
        var result = Resolve(Character(), ActivatedWolf());
        Assert.Equal("partial", result.Status);
        Assert.Equal("wolf", result.EffectiveSheetId);
        Assert.Equal("manifest.wolf", result.ManifestationInstanceId);
        Assert.Equal(17, result.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Null(result.EffectiveMechanics.CurrentResources);
        Assert.Equal(new[] { "grant.shared", "grant.wolf" },
            result.SheetGrants.Select(x => x.Entitlement.Id));
        Assert.Equal("werewolf", result.SheetGrants[0].OwnerSheetId);
        Assert.Equal("wolf", result.SheetGrants[1].OwnerSheetId);
        Assert.Contains(result.Issues, x => x.Code == "manifestation-class-policy-unresolved");
        Assert.Contains(result.Issues, x => x.Code == "manifestation-hit-points-policy-unresolved");
    }

    [Fact]
    public void ReversionAndReactivationPreservePersistentProgressionAndHistory()
    {
        var state = Character();
        var first = ActivatedWolf();
        var ended = Plan(state, first, "e2",
            Action(NativeManifestationTransitionAction.End, "manifest.wolf")).Proposed!;
        Assert.Equal(NativeCharacterSheetResolver.PrimarySheetId,
            Resolve(state, ended).EffectiveSheetId);
        Assert.Equal(NativeManifestationLifecycle.Ended,
            ended.Instances.Single().Lifecycle);
        var second = Plan(state, ended, "e3", Activate("manifest.wolf.2", "wolf", "e3", true));
        Assert.Equal("planned", second.Status);
        Assert.Equal(2, second.Proposed!.Instances.Count);
        Assert.Equal("wolf", Resolve(state, second.Proposed).EffectiveSheetId);
        Assert.Equal(2, Resolve(state, second.Proposed).SheetProgression.Count);
        Assert.Equal(NativeManifestationLifecycle.Ended,
            second.Proposed.Instances.Single(x => x.Id == "manifest.wolf").Lifecycle);
    }

    [Fact]
    public void SwitchingToHybridDoesNotClaimWolfOwnedProgression()
    {
        var state = Character();
        var prior = ActivatedWolf();
        var switchPlan = Plan(state, prior, "e2",
            Action(NativeManifestationTransitionAction.End, "manifest.wolf"),
            Activate("manifest.hybrid", "hybrid", "e2"));
        Assert.Equal("planned", switchPlan.Status);
        var effective = Resolve(state, switchPlan.Proposed!);
        Assert.Equal("hybrid", effective.EffectiveSheetId);
        Assert.Equal(16, effective.EffectiveMechanics!.AbilityScores["dexterity"]);
        Assert.Equal(10, effective.EffectiveMechanics.AbilityScores["strength"]);
        Assert.Equal(new[] { "grant.shared" },
            effective.SheetGrants.Select(x => x.Entitlement.Id));
        Assert.Equal("werewolf", effective.SheetProgression.Single().OwnerSheetId);
    }

    [Fact]
    public void CurrentClassAndUnderlyingOriginChangesFlowIntoActiveFormAndReversion()
    {
        var active = ActivatedWolf();
        var updated = Character(level: 5, speed: 45);
        var wolf = Resolve(updated, active);
        Assert.Equal(5, wolf.EffectiveMechanics!.Advancements.Single().Level);
        Assert.Equal(45, wolf.EffectiveMechanics.BaseWalkSpeed);
        var ended = Plan(updated, active, "e2",
            Action(NativeManifestationTransitionAction.End, "manifest.wolf")).Proposed!;
        var reverted = Resolve(updated, ended);
        Assert.Equal(45, reverted.EffectiveMechanics!.BaseWalkSpeed);
        Assert.Equal(5, reverted.EffectiveMechanics.Advancements.Single().Level);
    }

    [Fact]
    public void PrimaryInfluenceChangesExpressionWithoutAlternateForm()
    {
        var effect = new NativeCharacterInfluenceEffect(
            "effect.primary", Occurrence("effects.primary"), "receipt.effect", Rule(),
            new Dictionary<string, string> { ["curse"] = "high" },
            [NativeCharacterSheetResolver.PrimarySheetId],
            NativeInfluenceCharacteristic.AbilityScore, NativeInfluenceOperation.Adjust,
            Key: "strength", NumericValue: 2);
        NativeCharacterPersistentInfluence Influence(string stage) =>
            new("curse", "definition.curse", Source(), "receipt.curse",
                "receipt.expression", new Dictionary<string, string> { ["curse"] = stage }, [effect]);
        Assert.Equal(10, Resolve(Character([Influence("low")]), Ledger())
            .EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(12, Resolve(Character([Influence("high")]), Ledger())
            .EffectiveMechanics!.AbilityScores["strength"]);
    }

    [Fact]
    public void InfluenceOnlyTargetsExplicitlyNamedAlternateSheet()
    {
        var effect = new NativeCharacterInfluenceEffect(
            "effect.wolf", Occurrence("effects.wolf"), "receipt.effect.wolf", Rule(),
            new Dictionary<string, string>(), ["wolf"],
            NativeInfluenceCharacteristic.BaseWalkSpeed, NativeInfluenceOperation.Replace,
            NumericValue: 55);
        var influence = new NativeCharacterPersistentInfluence(
            "curse", "definition.curse", Source(), "receipt.curse",
            "receipt.expression", new Dictionary<string, string>(), [effect]);
        var state = Character([influence]);
        Assert.Equal(30, Resolve(state, Ledger()).EffectiveMechanics!.BaseWalkSpeed);
        Assert.Equal(55, Resolve(state, ActivatedWolf(state)).EffectiveMechanics!.BaseWalkSpeed);
    }

    [Fact]
    public void ResourceOwnershipIsStableAndMaximumChangeNeverRefills()
    {
        var result = Resolve(Character(), ActivatedWolf());
        Assert.Equal(3, result.Resources.Count);
        var primary = result.Resources.Single(x => x.Id == "resource.primary");
        Assert.Equal(NativeManifestationResourceOwner.Primary, primary.OwnerKind);
        Assert.Equal(8, primary.Current);
        Assert.Equal(10, primary.RecordedMaximum);
        Assert.Equal(13, primary.EffectiveMaximum);
        var wolf = result.Resources.Single(x => x.Id == "resource.wolf.manifest.wolf");
        Assert.Equal(NativeManifestationResourceOwner.Manifestation, wolf.OwnerKind);
        Assert.Equal("manifest.wolf", wolf.OwnerId);
        Assert.Equal(3, wolf.Current);
    }

    [Fact]
    public void ActivationCaptureStaysFixedWhileLiveResourceAndParentValuesChange()
    {
        var ledger = ActivatedWolf();
        var changed = ledger with { Resources = [PrimaryPool(6, 12), SharedPool()] };
        // Keep the instance-owned pool from the prior activation.
        changed = changed with { Resources = changed.Resources.Append(
            ledger.Resources.Single(x => x.OwnerKind == NativeManifestationResourceOwner.Manifestation)).ToArray() };
        var result = Resolve(Character(level: 3), changed);
        Assert.Equal(4, result.Resources.Single(x => x.Id == "resource.shared").EffectiveMaximum);
        Assert.Equal(15, result.Resources.Single(x => x.Id == "resource.primary").EffectiveMaximum);
        Assert.Equal(6, result.Resources.Single(x => x.Id == "resource.primary").Current);
        Assert.Equal(3, result.EffectiveMechanics!.Advancements.Single().Level);
    }

    [Fact]
    public void SuppressedInstancePersistsAndCanBeRestoredOnlyByAuthorizedEvent()
    {
        var state = Character();
        var active = ActivatedWolf();
        var suppressed = Plan(state, active, "e2",
            Action(NativeManifestationTransitionAction.Suppress, "manifest.wolf")).Proposed!;
        Assert.Equal(NativeCharacterSheetResolver.PrimarySheetId,
            Resolve(state, suppressed).EffectiveSheetId);
        Assert.Equal(NativeManifestationLifecycle.Active,
            suppressed.Instances.Single().Lifecycle);
        var restored = Plan(state, suppressed, "e3",
            Action(NativeManifestationTransitionAction.Restore, "manifest.wolf"));
        Assert.Equal("planned", restored.Status);
        Assert.Equal("wolf", Resolve(state, restored.Proposed!).EffectiveSheetId);
    }

    [Fact]
    public void EndedManifestationNeverResumesWhenAnotherEnds()
    {
        var state = Character();
        var first = ActivatedWolf();
        var switched = Plan(state, first, "e2",
            Action(NativeManifestationTransitionAction.End, "manifest.wolf"),
            Activate("manifest.hybrid", "hybrid", "e2")).Proposed!;
        var reverted = Plan(state, switched, "e3",
            Action(NativeManifestationTransitionAction.End, "manifest.hybrid")).Proposed!;
        Assert.Equal(NativeCharacterSheetResolver.PrimarySheetId,
            Resolve(state, reverted).EffectiveSheetId);
        Assert.All(reverted.Instances, x => Assert.Equal(NativeManifestationLifecycle.Ended, x.Lifecycle));
    }

    [Fact]
    public void StaleRevisionAndDuplicateEventRejectBeforeTransitionAuthority()
    {
        var authority = new Authority();
        var ledger = ActivatedWolf();
        var step = Action(NativeManifestationTransitionAction.End, "manifest.wolf");
        var stale = NativeCharacterManifestationTransitionPlanner.Plan(Character(), ledger,
            new("character.1", 7, "e2", [step]), authority);
        var duplicate = NativeCharacterManifestationTransitionPlanner.Plan(Character(), ledger,
            new("character.1", ledger.Revision, "e1", [step]), authority);
        Assert.Equal("invalid", stale.Status);
        Assert.Equal("invalid", duplicate.Status);
        Assert.Null(stale.Proposed);
        Assert.Null(duplicate.Proposed);
        Assert.Equal(0, authority.TransitionCalls);
    }

    [Fact]
    public void InvalidFormReferenceAndMalformedActivationEvidenceFailClosed()
    {
        var state = Character();
        var missing = Plan(state, Ledger(), "e1", Activate("manifest.bad", "monster.wolf", "e1"));
        var malformed = Plan(state, Ledger(), "e1", Activate("manifest.wolf", "wolf", "e1", true)
            with { AuthorizationEvidenceId = "" });
        Assert.Equal("invalid", missing.Status);
        Assert.Equal("invalid", malformed.Status);
        Assert.Null(missing.Proposed);
        Assert.Null(malformed.Proposed);
    }

    [Fact]
    public void DeniedFormResourceAndTransitionAuthorizationsFailClosed()
    {
        var state = Character();
        var step = Activate("manifest.wolf", "wolf", "e1", true);
        foreach (var authority in new[] {
            new Authority { DenyInstance = true },
            new Authority { DenyResource = true },
            new Authority { DenyPolicy = true },
            new Authority { DenyTransition = true }
        })
        {
            var plan = NativeCharacterManifestationTransitionPlanner.Plan(state, Ledger(),
                new("character.1", 7, "e1", [step]), authority);
            Assert.Equal("invalid", plan.Status);
            Assert.Null(plan.Proposed);
        }
        Assert.Equal("invalid", Resolve(state, ActivatedWolf(),
            new Authority { DenyLedger = true }).Status);
    }

    [Fact]
    public void CorruptGraphAndSourceOccurrenceNeverResolveAPlayableForm()
    {
        var cycle = Character(sheets: [
            Werewolf() with { ParentId = "wolf" }, Wolf(), Hybrid()
        ]);
        Assert.Equal("invalid", Resolve(cycle, Ledger()).Status);
        var active = ActivatedWolf();
        var malformed = active with
        {
            Instances = [active.Instances.Single() with {
                DefinitionOccurrence = new(Source(), "")
            }]
        };
        Assert.Equal("invalid", Resolve(Character(), malformed).Status);
    }

    [Fact]
    public void ConflictingUnruledManifestationsRemainUnresolved()
    {
        var state = Character();
        var active = ActivatedWolf();
        var attempt = Plan(state, active, "e2", Activate("manifest.hybrid", "hybrid", "e2"));
        Assert.Equal("unresolved", attempt.Status);
        Assert.Null(attempt.Proposed);
        var forcedLedger = active with {
            Instances = active.Instances.Append(Form("manifest.hybrid", "hybrid", "e2")).ToArray(),
            ProcessedEventIds = ["e1", "e2"]
        };
        Assert.Equal("unresolved", Resolve(state, forcedLedger).Status);
    }

    [Fact]
    public void AuthenticatedInteractionSelectsOneManifestationWithoutDeletingAnother()
    {
        var state = Character();
        var active = ActivatedWolf();
        var decision = new NativeManifestationInteractionDecision(
            "interaction.test", NativeManifestationInteractionKind.Suppress,
            ["manifest.wolf", "manifest.hybrid"], "manifest.hybrid",
            Occurrence("interaction"), Rule(), "receipt.interaction");
        var plan = NativeCharacterManifestationTransitionPlanner.Plan(
            state, active, new("character.1", active.Revision, "e2",
                [Activate("manifest.hybrid", "hybrid", "e2")], [decision]), new Authority());
        Assert.Equal("planned", plan.Status);
        var result = Resolve(state, plan.Proposed!);
        Assert.Equal("hybrid", result.EffectiveSheetId);
        Assert.All(plan.Proposed!.Instances, x =>
            Assert.Equal(NativeManifestationLifecycle.Active, x.Lifecycle));
        Assert.Equal("invalid", Resolve(state, plan.Proposed,
            new Authority { DenyInteraction = true }).Status);
    }

    [Fact]
    public void CoexistenceWithoutSupportedMechanicalCompositionIsUnresolved()
    {
        var active = ActivatedWolf();
        var decision = new NativeManifestationInteractionDecision(
            "interaction.coexist", NativeManifestationInteractionKind.Coexist,
            ["manifest.wolf", "manifest.hybrid"], null,
            Occurrence("interaction.coexist"), Rule(), "receipt.coexist");
        var request = new NativeManifestationTransitionRequest(
            "character.1", active.Revision, "e2",
            [Activate("manifest.hybrid", "hybrid", "e2")], [decision]);
        Assert.Equal("unresolved", NativeCharacterManifestationTransitionPlanner.Plan(
            Character(), active, request, new Authority()).Status);
    }

    [Fact]
    public void PlannerDoesNotChangeAuthoritativeLedgerOrResourceCounts()
    {
        var ledger = Ledger();
        var before = ledger.Resources.Single(x => x.Id == "resource.primary").Current;
        var plan = Plan(Character(), ledger, "e1",
            Activate("manifest.wolf", "wolf", "e1", true));
        Assert.Equal("planned", plan.Status);
        Assert.Empty(ledger.Instances);
        Assert.Empty(ledger.ProcessedEventIds);
        Assert.Equal(7, ledger.Revision);
        Assert.Equal(before, ledger.Resources.Single(x => x.Id == "resource.primary").Current);
        Assert.Equal(before, plan.Proposed!.Resources.Single(x => x.Id == "resource.primary").Current);
        Assert.Equal(8, plan.Proposed.Revision);
    }

    [Fact]
    public void RepeatResolutionIsDeterministicAndDoesNotDoubleGrantOrConsume()
    {
        var ledger = ActivatedWolf();
        var first = Resolve(Character(), ledger);
        var second = Resolve(Character(), ledger);
        Assert.Equal(first.SheetGrants.Select(x => x.Entitlement.Id),
            second.SheetGrants.Select(x => x.Entitlement.Id));
        Assert.Equal(first.Resources, second.Resources);
        Assert.Equal(first.Issues, second.Issues);
        Assert.Equal(new[] { "grant.shared", "grant.wolf" },
            first.SheetGrants.Select(x => x.Entitlement.Id));
        Assert.Equal(3, ledger.Resources.Single(x => x.Id == "resource.wolf.manifest.wolf").Current);
    }

    [Fact]
    public void MalformedResourceAndUnknownOwnerFailClosed()
    {
        var ledger = Ledger();
        var invalid = ledger with {
            Resources = [PrimaryPool(current: 11, maximum: 10), SharedPool()]
        };
        Assert.Equal("invalid", Resolve(Character(), invalid).Status);
        var noOwner = ledger with {
            Resources = [PrimaryPool(), SharedPool() with { OwnerId = "missing" }]
        };
        Assert.Equal("invalid", Resolve(Character(), noOwner).Status);
    }

    [Fact]
    public void RejectedTransitionDoesNotMutateExistingInstance()
    {
        var active = ActivatedWolf();
        var rejected = Plan(Character(), active, "e2",
            Action(NativeManifestationTransitionAction.Restore, "manifest.wolf"));
        Assert.Equal("invalid", rejected.Status);
        Assert.Equal(NativeManifestationContribution.Contributing,
            active.Instances.Single().Contribution);
        Assert.Equal("wolf", Resolve(Character(), active).EffectiveSheetId);
    }
}
