using System.Text.Json;
using RulesCore.Content;
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
        public bool DenyProgression { get; set; }
        public bool DenyGrant { get; set; }
        public bool PermitIndependentEnding { get; set; }
        public IReadOnlyList<NativeManifestationCharacteristicSlot>? ExpectedSlots { get; init; }
        public int TransitionCalls { get; private set; }

        public bool AuthorizesPrimary(string id, NativeCharacterRequest primary) => id == "character.1";
        public bool AuthorizesSheet(string id, NativePersistentCharacterSheet sheet) => id == "character.1";
        public bool AuthorizesProgression(string id, string owner, NativeSheetEarnedProgression progression) => !DenyProgression;
        public bool AuthorizesGrant(string id, string owner, NativeSheetEarnedProgression progression,
            NativeSheetEarnedGrant grant) => !DenyGrant;
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
            NativePersistentCharacterSheet sheet) =>
            !DenyInstance && instance.CharacterId == id &&
            instance.SheetId == sheet.Id &&
            instance.DefinitionOccurrence.Source == Source() &&
            instance.DefinitionOccurrence.OccurrencePath == "forms." + sheet.Id &&
            instance.ActivationEvidenceId == "receipt.activate." + instance.ActivationEventId &&
            (ExpectedSlots is null || instance.ContributionSlots is { } slots &&
                ExpectedSlots.SequenceEqual(slots));
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
        public bool AuthorizesUnlistedEnding(string id, NativeManifestationLedger ledger,
            NativeManifestationInstance instance, NativeManifestationTransitionStep step) =>
            PermitIndependentEnding && id == ledger.CharacterId &&
            instance.Id == step.TargetInstanceId &&
            step.EndingConditionId == "rule.test.external-displacement" &&
            step.RuleOccurrence == Occurrence("actions.external-displacement") &&
            step.Basis == Rule();
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
        new("character.1", id, sheet, "transformation." + sheet, Occurrence("forms." + sheet),
            null, eventId, "receipt.activate." + eventId, Rule(),
            NativeManifestationLifecycle.Active,
            NativeManifestationContribution.Contributing,
            new Dictionary<string, int> { ["captured"] = 4 },
            ["rule.test.end-on-event"], policies ?? [], eventId);

    private static NativeManifestationTransitionStep Activate(string id, string sheet, string eventId,
        bool addWolfPool = false) =>
        new(NativeManifestationTransitionAction.Activate, id, Occurrence("actions.activate"),
            Rule(), "receipt.transition." + eventId,
            Form(id, sheet, eventId,
                sheet == "wolf" ? [
                    new("resource.primary", NativeManifestationResourceMode.AdjustMaximum, 3),
                    new("resource.shared", NativeManifestationResourceMode.CaptureMaximum,
                        CaptureKey: "captured"),
                    new("resource.wolf." + id, NativeManifestationResourceMode.Separate)
                ] : [new("resource.primary", NativeManifestationResourceMode.Inherit)]),
            addWolfPool ? [WolfPool(id)] : null);

    private static NativeManifestationTransitionStep Action(
        NativeManifestationTransitionAction action, string id) =>
        new(action, id, Occurrence("actions." + action), Rule(), "receipt.transition." + id,
            EndingConditionId: action == NativeManifestationTransitionAction.End
                ? "rule.test.end-on-event" : null);

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

    private static NativeManifestationLedger Coexisting(
        IReadOnlyList<NativeManifestationCharacteristicSlot> wolfSlots,
        IReadOnlyList<NativeManifestationCharacteristicSlot> hybridSlots,
        IReadOnlyList<NativeManifestationResourceState>? resources = null)
    {
        var wolf = Form("manifest.wolf", "wolf", "coexist-event") with
        {
            ContributionSlots = wolfSlots
        };
        var hybrid = Form("manifest.hybrid", "hybrid", "coexist-event") with
        {
            ContributionSlots = hybridSlots
        };
        return new("character.1", 7, [wolf, hybrid],
            resources ?? [PrimaryPool()], ["coexist-event"],
            [new("decision.coexist", NativeManifestationInteractionKind.Coexist,
                [wolf.Id, hybrid.Id], null, Occurrence("interactions.coexist"),
                Rule(), "receipt.interaction.coexist")]);
    }

    [Fact]
    public void CoexistingPartialTransformationsCombineDisjointSlotsOnLatestBase()
    {
        var ledger = Coexisting(
            [new(NativeInfluenceCharacteristic.AbilityScore, "strength")],
            [new(NativeInfluenceCharacteristic.AbilityScore, "dexterity")]);
        var updated = Character(level: 5, speed: 45);
        var resolved = Resolve(updated, ledger);
        Assert.Equal("partial", resolved.Status);
        Assert.Equal("@composite", resolved.EffectiveSheetId);
        Assert.Equal(17, resolved.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(16, resolved.EffectiveMechanics.AbilityScores["dexterity"]);
        Assert.Equal(45, resolved.EffectiveMechanics.BaseWalkSpeed);
        Assert.Equal(5, resolved.EffectiveMechanics.Advancements.Single().Level);
        Assert.Null(resolved.EffectiveMechanics.CurrentResources);
        Assert.Equal(new[] { "manifest.hybrid", "manifest.wolf" },
            resolved.ContributingManifestationInstanceIds);
        Assert.Equal(new[] { "grant.shared", "grant.wolf" },
            resolved.SheetGrants.Select(item => item.Entitlement.Id).OrderBy(x => x));
        Assert.Contains(resolved.Issues, issue =>
            issue.Code == "manifestation-composite-partial");

        // No permanent write or activation-time copy is used for reversion.
        var normal = Resolve(updated, ledger with
        {
            Instances = ledger.Instances.Select(item => item with
            {
                Lifecycle = NativeManifestationLifecycle.Ended
            }).ToArray()
        });
        Assert.Equal("resolved", normal.Status);
        Assert.Equal(10, normal.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(12, normal.EffectiveMechanics.AbilityScores["dexterity"]);
        Assert.Equal(45, normal.EffectiveMechanics.BaseWalkSpeed);
        Assert.Equal(5, normal.EffectiveMechanics.Advancements.Single().Level);
    }

    [Fact]
    public void CoexistingPartialTransformationsPreserveDistinctEffectProvenance()
    {
        var primaryEffect = new NativeCharacterInfluenceEffect(
            "b:c", Occurrence("effects.primary-speed"), "receipt.primary-speed", Rule(),
            new Dictionary<string, string>(), [NativeCharacterSheetResolver.PrimarySheetId],
            NativeInfluenceCharacteristic.BaseWalkSpeed, NativeInfluenceOperation.Replace,
            NumericValue: 45);
        var formEffect = new NativeCharacterInfluenceEffect(
            "c", Occurrence("effects.hybrid-dexterity"), "receipt.hybrid-dexterity", Rule(),
            new Dictionary<string, string>(), ["hybrid"],
            NativeInfluenceCharacteristic.AbilityScore, NativeInfluenceOperation.Replace,
            Key: "dexterity", NumericValue: 16);
        var primary = new NativeCharacterPersistentInfluence(
            "a", "definition.primary", Source(), "receipt.acquire.primary",
            "receipt.expression.primary", new Dictionary<string, string>(), [primaryEffect]);
        var form = new NativeCharacterPersistentInfluence(
            "a:b", "definition.hybrid", Source(), "receipt.acquire.hybrid",
            "receipt.expression.hybrid", new Dictionary<string, string>(), [formEffect]);
        var state = Character([primary, form], [Werewolf(), Wolf(), Hybrid() with { Overrides = null }]);
        var ledger = Coexisting(
            [new(NativeInfluenceCharacteristic.AbilityScore, "strength")],
            [new(NativeInfluenceCharacteristic.AbilityScore, "dexterity")]);

        var result = Resolve(state, ledger);

        Assert.Equal("partial", result.Status);
        Assert.Equal(45, result.EffectiveMechanics!.BaseWalkSpeed);
        Assert.Equal(16, result.EffectiveMechanics.AbilityScores["dexterity"]);
        Assert.Equal(new[] { ("a", "b:c"), ("a:b", "c") },
            result.AppliedEffects.Select(item => (item.InfluenceId, item.Effect.Id)));
    }

    [Fact]
    public void CoexistingPartialTransformationsCanContributeSeparateTrainingProperties()
    {
        var wolf = Wolf() with
        {
            Overrides = new(Skills: new(false,
                new Dictionary<string, NativeSheetTrainingPatch>
                {
                    ["survival"] = new(Ranks: new(4))
                }))
        };
        var hybrid = Hybrid() with
        {
            Overrides = new(Skills: new(false,
                new Dictionary<string, NativeSheetTrainingPatch>
                {
                    ["survival"] = new(Proficient: new(true))
                }))
        };
        var character = Character(sheets: [Werewolf(), wolf, hybrid]);
        character = character with
        {
            PrimaryMechanics = character.PrimaryMechanics with
            {
                Skills = new Dictionary<string, NativeSkillTraining>
                {
                    ["survival"] = new(1, false, 2, "wisdom")
                }
            }
        };
        var ledger = Coexisting(
            [new(NativeInfluenceCharacteristic.SkillRanks, "survival")],
            [new(NativeInfluenceCharacteristic.SkillProficient, "survival")]);
        var result = Resolve(character, ledger);
        Assert.Equal("partial", result.Status);
        var training = result.EffectiveMechanics!.Skills!["survival"];
        Assert.Equal(4, training.Ranks);
        Assert.True(training.Proficient);
        Assert.Equal(2, training.MiscModifier);
        Assert.Equal("wisdom", training.Ability);
    }

    [Fact]
    public void CoexistRejectsUnwrittenNestedTrainingMember()
    {
        var wolf = Wolf() with
        {
            Overrides = new(Skills: new(false,
                new Dictionary<string, NativeSheetTrainingPatch>
                {
                    ["survival"] = new(Ranks: new(4))
                }))
        };
        var hybrid = Hybrid() with
        {
            Overrides = new(Skills: new(false,
                new Dictionary<string, NativeSheetTrainingPatch>
                {
                    ["survival"] = new(Proficient: new(true))
                }))
        };
        var character = Character(sheets: [Werewolf(), wolf, hybrid]);
        character = character with
        {
            PrimaryMechanics = character.PrimaryMechanics with
            {
                Skills = new Dictionary<string, NativeSkillTraining>
                {
                    ["survival"] = new(1, false, 2, "wisdom")
                }
            }
        };
        // A Ranks-only patch does not itself define MiscModifier, even
        // though the inherited training view has a numeric modifier.
        var result = Resolve(character, Coexisting(
            [new(NativeInfluenceCharacteristic.SkillMiscModifier, "survival")],
            [new(NativeInfluenceCharacteristic.SkillProficient, "survival")]));
        Assert.Equal("unresolved", result.Status);
        Assert.Null(result.EffectiveMechanics);
        Assert.Contains(result.Issues, x => x.Code == "manifestation-slot-unbound");
    }

    [Fact]
    public void CoexistFailsClosedWhenTwoFormsWriteSameSlot()
    {
        var result = Resolve(Character(), Coexisting(
            [new(NativeInfluenceCharacteristic.AbilityScore, "strength")],
            [new(NativeInfluenceCharacteristic.AbilityScore, "strength")]));
        Assert.Equal("unresolved", result.Status);
        Assert.Null(result.EffectiveMechanics);
        Assert.Contains(result.Issues, x => x.Code == "manifestation-slot-overlap");
    }

    [Fact]
    public void CoexistDoesNotAcceptAContradictorySelectedInstance()
    {
        var ledger = Coexisting(
            [new(NativeInfluenceCharacteristic.AbilityScore, "strength")],
            [new(NativeInfluenceCharacteristic.AbilityScore, "dexterity")]);
        ledger = ledger with
        {
            Interactions = ledger.Interactions!.Select(interaction =>
                interaction with { SelectedInstanceId = "manifest.wolf" }).ToArray()
        };
        var result = Resolve(Character(), ledger);
        Assert.Equal("invalid", result.Status);
        Assert.Null(result.EffectiveMechanics);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "invalid-manifestation-interaction");
    }

    [Fact]
    public void CoexistFailsClosedOnMissingOrUnboundContributionSlots()
    {
        var missing = Resolve(Character(), Coexisting(
            [new(NativeInfluenceCharacteristic.AbilityScore, "strength")], []));
        Assert.Equal("unresolved", missing.Status);
        Assert.Contains(missing.Issues, x => x.Code == "manifestation-slots-required");

        var unbound = Resolve(Character(), Coexisting(
            [new(NativeInfluenceCharacteristic.BaseWalkSpeed)],
            [new(NativeInfluenceCharacteristic.AbilityScore, "dexterity")]));
        Assert.Equal("unresolved", unbound.Status);
        Assert.Contains(unbound.Issues, x => x.Code == "manifestation-slot-unbound");
    }

    [Fact]
    public void AtomicCoexistActivationAndReversionUseOneTransitionEventEach()
    {
        var character = Character();
        var start = Ledger();
        var eventId = "coexist-activation";

        NativeManifestationTransitionStep Enter(string id, string sheet,
            NativeInfluenceCharacteristic field, string key) =>
            new(NativeManifestationTransitionAction.Activate, id,
                Occurrence("actions.activate"), Rule(),
                "receipt.transition." + eventId,
                Form(id, sheet, eventId) with
                {
                    ContributionSlots = [new(field, key)]
                });
        var coexist = new NativeManifestationInteractionDecision(
            "interaction.coexist", NativeManifestationInteractionKind.Coexist,
            ["manifest.wolf", "manifest.hybrid"], null,
            Occurrence("interactions.coexist"), Rule(),
            "receipt.interaction.coexist");
        var proposed = NativeCharacterManifestationTransitionPlanner.Plan(
            character, start, new(character.CharacterId, start.Revision,
                eventId,
                [
                    Enter("manifest.wolf", "wolf",
                        NativeInfluenceCharacteristic.AbilityScore, "strength"),
                    Enter("manifest.hybrid", "hybrid",
                        NativeInfluenceCharacteristic.AbilityScore, "dexterity")
                ], [coexist]), new Authority());
        Assert.Equal("planned", proposed.Status);
        Assert.Equal(8, proposed.Proposed!.Revision);
        Assert.Equal("@composite", proposed.ProposedEffective!.EffectiveSheetId);
        Assert.Equal(17, proposed.ProposedEffective.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(16, proposed.ProposedEffective.EffectiveMechanics.AbilityScores["dexterity"]);

        var reversion = NativeCharacterManifestationTransitionPlanner.Plan(
            Character(speed: 45), proposed.Proposed,
            new(character.CharacterId, proposed.Proposed.Revision, "coexist-end",
                [
                    Action(NativeManifestationTransitionAction.End, "manifest.wolf"),
                    Action(NativeManifestationTransitionAction.End, "manifest.hybrid")
                ]),
            new Authority());
        Assert.Equal("planned", reversion.Status);
        Assert.Equal(9, reversion.Proposed!.Revision);
        Assert.Equal(NativeCharacterSheetResolver.PrimarySheetId,
            reversion.ProposedEffective!.EffectiveSheetId);
        Assert.Equal(45, reversion.ProposedEffective.EffectiveMechanics!.BaseWalkSpeed);
        Assert.Empty(reversion.ProposedEffective.ContributingManifestationInstanceIds ?? []);
        Assert.All(reversion.Proposed.Instances,
            item => Assert.Equal(NativeManifestationLifecycle.Ended, item.Lifecycle));
    }

    [Fact]
    public void PartialFormKeepsItsScopeWhenAloneAndWhenAnotherFormEnds()
    {
        var hybrid = Hybrid() with
        {
            Overrides = Hybrid().Overrides! with { BaseWalkSpeed = new(65) }
        };
        var state = Character(sheets: [Werewolf(), Wolf(), hybrid], speed: 45);
        var ledger = Coexisting(
            [new(NativeInfluenceCharacteristic.AbilityScore, "strength")],
            [new(NativeInfluenceCharacteristic.AbilityScore, "dexterity")]);
        var singleActivation = new NativeManifestationTransitionStep(
            NativeManifestationTransitionAction.Activate, "manifest.hybrid",
            Occurrence("actions.activate"), Rule(), "receipt.transition.single",
            Form("manifest.hybrid", "hybrid", "single") with
            {
                ContributionSlots = [new(NativeInfluenceCharacteristic.AbilityScore, "dexterity")]
            });

        var activated = Plan(state, Ledger(), "single", singleActivation);
        AssertPartialHybrid(activated, 45);

        var endedWolf = Plan(state, ledger, "end-wolf",
            Action(NativeManifestationTransitionAction.End, "manifest.wolf"));
        AssertPartialHybrid(endedWolf, 45);
        var updated = Character(sheets: state.AlternateSheets, speed: 50);
        var reverted = Plan(updated, endedWolf.Proposed!, "end-hybrid",
            Action(NativeManifestationTransitionAction.End, "manifest.hybrid"));
        Assert.Equal("planned", reverted.Status);
        Assert.Equal(NativeCharacterSheetResolver.PrimarySheetId, reverted.ProposedEffective!.EffectiveSheetId);
        Assert.Equal(50, reverted.ProposedEffective.EffectiveMechanics!.BaseWalkSpeed);
        Assert.Equal(12, reverted.ProposedEffective.EffectiveMechanics.AbilityScores["dexterity"]);

        static void AssertPartialHybrid(NativeManifestationTransitionPlan plan, int speed)
        {
            Assert.Equal("planned", plan.Status);
            var effective = plan.ProposedEffective!;
            Assert.Equal("partial", effective.Status);
            Assert.Equal("hybrid", effective.EffectiveSheetId);
            Assert.Equal("manifest.hybrid", effective.ManifestationInstanceId);
            Assert.Equal(speed, effective.EffectiveMechanics!.BaseWalkSpeed);
            Assert.Equal(10, effective.EffectiveMechanics.AbilityScores["strength"]);
            Assert.Equal(16, effective.EffectiveMechanics.AbilityScores["dexterity"]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PartialTrainingSlotRequiresAnExistingPrimaryRecord(bool competency)
    {
        var training = new NativeSheetMapPatch<NativeSheetTrainingPatch>(false,
            new Dictionary<string, NativeSheetTrainingPatch>
            {
                ["training"] = new(Replacement: new(new NativeSkillTraining(4, true, 7, "wisdom")))
            });
        var wolf = Wolf() with
        {
            Overrides = competency ? new(Competencies: training) : new(Skills: training)
        };
        var state = Character(sheets: [Werewolf(), wolf, Hybrid()]);
        var ledger = Coexisting(
            [new(competency ? NativeInfluenceCharacteristic.CompetencyRanks :
                NativeInfluenceCharacteristic.SkillRanks, "training")],
            [new(NativeInfluenceCharacteristic.AbilityScore, "dexterity")]);

        var result = Resolve(state, ledger);
        Assert.Equal("unresolved", result.Status);
        Assert.Null(result.EffectiveMechanics);
        Assert.Contains(result.Issues, issue => issue.Code == "manifestation-slot-value-unresolved");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActivationEvidenceRejectsSubstitutedOrRemovedSlots(bool removeSlots)
    {
        var hybrid = Hybrid() with
        {
            Overrides = Hybrid().Overrides! with { BaseWalkSpeed = new(65) }
        };
        var state = Character(sheets: [Werewolf(), Wolf(), hybrid]);
        var start = Ledger();
        var authority = new Authority
        {
            ExpectedSlots = [new(NativeInfluenceCharacteristic.AbilityScore, "dexterity")]
        };
        var activation = new NativeManifestationTransitionStep(
            NativeManifestationTransitionAction.Activate, "manifest.hybrid",
            Occurrence("actions.activate"), Rule(), "receipt.transition.single",
            Form("manifest.hybrid", "hybrid", "single") with
            {
                ContributionSlots = [new(NativeInfluenceCharacteristic.AbilityScore, "dexterity")]
            });
        var request = new NativeManifestationTransitionRequest(
            state.CharacterId, start.Revision, "single", [activation]);
        var authorized = NativeCharacterManifestationTransitionPlanner.Plan(state, start, request, authority);
        Assert.Equal("planned", authorized.Status);
        Assert.Equal("partial", Resolve(state, authorized.Proposed!, authority).Status);

        var changed = authorized.Proposed!.Instances.Single() with
        {
            ContributionSlots = removeSlots ? null : [new(NativeInfluenceCharacteristic.BaseWalkSpeed)]
        };
        var rejected = NativeCharacterManifestationTransitionPlanner.Plan(state, start,
            request with { Steps = [activation with { NewInstance = changed }] }, authority);
        Assert.Equal("invalid", rejected.Status);
        Assert.Null(rejected.Proposed);
        Assert.Contains(rejected.Issues, issue => issue.Code == "unauthorized-manifestation-activation");
        var forged = Resolve(state, authorized.Proposed with { Instances = [changed] }, authority);
        Assert.Equal("invalid", forged.Status);
        Assert.Contains(forged.Issues, issue => issue.Code == "unauthorized-manifestation-instance");
    }

    [Fact]
    public void CoexistRejectsMalformedSlotsBeforeHostInstanceChecks()
    {
        var malformed = Coexisting(
            [new(NativeInfluenceCharacteristic.BaseWalkSpeed, "unexpected")],
            [new(NativeInfluenceCharacteristic.AbilityScore, "dexterity")]);
        var authority = new Authority();
        var result = Resolve(Character(), malformed, authority);
        Assert.Equal("invalid", result.Status);
        Assert.Contains(result.Issues, x => x.Code == "invalid-manifestation-instance");
    }

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

    [Theory]
    [InlineData("")]
    [InlineData("character.other")]
    public void MissingOrForeignManifestationOwnerFailsBeforeHostTransition(string owner)
    {
        var state = Character();
        var existing = ActivatedWolf(state);
        Assert.Equal(state.CharacterId, existing.Instances.Single().CharacterId);

        var forged = existing with
        {
            Instances = [existing.Instances.Single() with { CharacterId = owner }]
        };
        var resolved = Resolve(state, forged);
        Assert.Equal("invalid", resolved.Status);
        Assert.Contains(resolved.Issues,
            issue => issue.Code == "invalid-manifestation-instance");

        var activation = Activate("manifest.wolf", "wolf", "e1", true);
        var unauthorized = activation with
        {
            NewInstance = activation.NewInstance! with { CharacterId = owner }
        };
        var authority = new Authority();
        var proposed = NativeCharacterManifestationTransitionPlanner.Plan(
            state, Ledger(), new(state.CharacterId, 7, "e1", [unauthorized]),
            authority);
        Assert.Equal("invalid", proposed.Status);
        Assert.Null(proposed.Proposed);
        Assert.Null(proposed.ProposedEffective);
        Assert.Equal(0, authority.TransitionCalls);
        Assert.Contains(proposed.Issues,
            issue => issue.Code == "invalid-manifestation-instance");
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
    public void NewFormCanNotBorrowAnotherActivationPrivateResource()
    {
        var state = Character();
        var first = ActivatedWolf(state);
        var ended = Plan(state, first, "e2",
            Action(NativeManifestationTransitionAction.End, "manifest.wolf")).Proposed!;

        // The old activation and its resource are retained for provenance,
        // but the resource is not a pool owned by the new Hybrid activation.
        var hybrid = Activate("manifest.hybrid", "hybrid", "e3");
        var crossOwned = hybrid with
        {
            NewInstance = hybrid.NewInstance! with
            {
                ResourcePolicies = [
                    new("resource.wolf.manifest.wolf",
                        NativeManifestationResourceMode.Separate)
                ]
            }
        };
        var attempted = Plan(state, ended, "e3", crossOwned);
        Assert.Equal("unresolved", attempted.Status);
        Assert.Null(attempted.Proposed);
        Assert.Null(attempted.ProposedEffective);
        Assert.Contains(attempted.Issues,
            x => x.Code == "manifestation-resource-policy-unresolved");

        // Even a deliberately forged persisted journal can not surface the
        // foreign-owned quantity through read-only effective-state resolution.
        var forged = ended with
        {
            Instances = ended.Instances.Append(crossOwned.NewInstance!).ToArray(),
            ProcessedEventIds = ended.ProcessedEventIds.Append("e3").ToArray()
        };
        var evaluated = Resolve(state, forged);
        Assert.Equal("unresolved", evaluated.Status);
        Assert.Null(evaluated.EffectiveMechanics);
        Assert.Empty(evaluated.Resources);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateOrOversizedEndingConditionIsRejectedBeforeAuthorization(
        bool oversized)
    {
        var state = Character();
        var activation = Activate("manifest.wolf", "wolf", "e1", true);
        var invalid = activation with
        {
            NewInstance = activation.NewInstance! with
            {
                EndingConditions = oversized
                    ? [new string('x', 161)]
                    : ["rule.test.end-on-event", "rule.test.end-on-event"]
            }
        };
        var authority = new Authority();
        var result = NativeCharacterManifestationTransitionPlanner.Plan(
            state, Ledger(), new(state.CharacterId, 7, "e1", [invalid]), authority);
        Assert.Equal("invalid", result.Status);
        Assert.Null(result.Proposed);
        Assert.Equal(0, authority.TransitionCalls);
        Assert.Contains(result.Issues,
            x => x.Code == "invalid-manifestation-instance");
    }

    [Fact]
    public void NonActivationStepCanNotCarryEvenEmptyActivationResourcePayload()
    {
        var ledger = ActivatedWolf();
        var end = Action(NativeManifestationTransitionAction.End, "manifest.wolf")
            with { NewResources = [] };
        var authority = new Authority();
        var result = NativeCharacterManifestationTransitionPlanner.Plan(
            Character(), ledger, new("character.1", ledger.Revision, "e2", [end]),
            authority);
        Assert.Equal("invalid", result.Status);
        Assert.Null(result.Proposed);
        Assert.Equal(0, authority.TransitionCalls);
        Assert.Contains(result.Issues,
            x => x.Code == "invalid-manifestation-step");
    }

    [Theory]
    [InlineData("latent", 30)]
    [InlineData("expressed", 55)]
    public void ActiveWolfReevaluatesChangingInfluenceExpression(string phase, int speed)
    {
        var effect = new NativeCharacterInfluenceEffect(
            "effect.phase", Occurrence("effects.phase"), "receipt.effect.phase", Rule(),
            new Dictionary<string, string> { ["phase"] = "expressed" }, ["wolf"],
            NativeInfluenceCharacteristic.BaseWalkSpeed, NativeInfluenceOperation.Replace,
            NumericValue: 55);
        var influence = new NativeCharacterPersistentInfluence(
            "curse", "definition.curse", Source(), "receipt.curse",
            "receipt.expression." + phase,
            new Dictionary<string, string> { ["phase"] = phase }, [effect]);
        Assert.Equal(speed, Resolve(Character([influence]), ActivatedWolf())
            .EffectiveMechanics!.BaseWalkSpeed);
    }

    [Fact]
    public void IncorrectNonemptyFormOccurrenceAndReceiptFailHostVerification()
    {
        var state = Character();
        var ledger = ActivatedWolf();
        var wrongSource = ledger with { Instances =
            [ledger.Instances.Single() with { DefinitionOccurrence = Occurrence("forms.other") }] };
        var wrongReceipt = ledger with { Instances =
            [ledger.Instances.Single() with { ActivationEvidenceId = "invented" }] };
        Assert.Equal("invalid", Resolve(state, wrongSource).Status);
        Assert.Equal("invalid", Resolve(state, wrongReceipt).Status);
    }

    [Fact]
    public void CapacityReductionDoesNotClampRecordedCurrentQuantity()
    {
        var ledger = ActivatedWolf();
        var instance = ledger.Instances.Single();
        var updated = ledger with { Instances = [
            instance with { ResourcePolicies = [
                new("resource.primary", NativeManifestationResourceMode.AdjustMaximum, -5),
                new("resource.shared", NativeManifestationResourceMode.CaptureMaximum,
                    CaptureKey: "captured"),
                new("resource.wolf.manifest.wolf", NativeManifestationResourceMode.Separate)
            ]}
        ]};
        var resource = Resolve(Character(), updated).Resources
            .Single(x => x.Id == "resource.primary");
        Assert.Equal(5, resource.EffectiveMaximum);
        Assert.Equal(8, resource.Current);
        Assert.Equal(8, updated.Resources.Single(x => x.Id == "resource.primary").Current);
    }


    [Fact]
    public void ActiveFormCanNotHideAnUnresolvedPrimaryInfluenceConflict()
    {
        static NativeCharacterPersistentInfluence PrimaryInfluence(string suffix, int speed)
        {
            var effect = new NativeCharacterInfluenceEffect(
                "effect.primary." + suffix, Occurrence("effects.primary." + suffix),
                "receipt.effect.primary." + suffix, Rule(),
                new Dictionary<string, string>(),
                [NativeCharacterSheetResolver.PrimarySheetId],
                NativeInfluenceCharacteristic.BaseWalkSpeed,
                NativeInfluenceOperation.Replace, NumericValue: speed);
            return new NativeCharacterPersistentInfluence(
                "influence.primary." + suffix, "definition.primary." + suffix,
                Source(), "receipt.acquisition." + suffix, "receipt.expression." + suffix,
                new Dictionary<string, string>(), [effect]);
        }

        var state = Character([
            PrimaryInfluence("first", 40),
            PrimaryInfluence("second", 45)
        ]);
        var activeWolf = ActivatedWolf();
        // The two writers target the primary character, not the Wolf sheet.
        // Resolving only the selected form would otherwise appear to succeed.
        var resolved = Resolve(state, activeWolf);
        Assert.Equal("unresolved", resolved.Status);
        Assert.Null(resolved.EffectiveMechanics);
        Assert.Contains(resolved.Issues,
            x => x.Code == "influence-overlap-unresolved");

        // The same unresolved primary inputs can not be bypassed by planning
        // a new form activation from a valid, committed normal-form ledger.
        var transition = Plan(state, Ledger(), "e1",
            Activate("manifest.wolf", "wolf", "e1", true));
        Assert.Equal("unresolved", transition.Status);
        Assert.Null(transition.Proposed);
        Assert.Null(transition.ProposedEffective);
        Assert.Contains(transition.Issues,
            x => x.Code == "influence-overlap-unresolved");
    }

    [Fact]
    public void TransitionPreviewEvaluatesTargetedInfluenceConflictBeforeOfferingCommit()
    {
        var effect = new NativeCharacterInfluenceEffect(
            "effect.conflict", Occurrence("effects.conflict"), "receipt.conflict", Rule(),
            new Dictionary<string, string>(), ["wolf"],
            NativeInfluenceCharacteristic.AbilityScore, NativeInfluenceOperation.Replace,
            Key: "strength", NumericValue: 20);
        var influence = new NativeCharacterPersistentInfluence(
            "curse", "definition.curse", Source(), "receipt.curse",
            "receipt.expression", new Dictionary<string, string>(), [effect]);
        var state = Character([influence]);
        var original = Ledger();
        var proposed = Plan(state, original, "e1",
            Activate("manifest.wolf", "wolf", "e1", addWolfPool: true));

        Assert.Equal("unresolved", proposed.Status);
        Assert.Null(proposed.Proposed);
        Assert.Null(proposed.ProposedEffective);
        Assert.Contains(proposed.Issues,
            x => x.Code == "influence-sheet-overlap-unresolved");
        Assert.Empty(original.Instances);
        Assert.Equal(7, original.Revision);
    }

    [Fact]
    public void TransitionPreviewRejectsFormResourcePolicyConflict()
    {
        var step = Activate("manifest.wolf", "wolf", "e1", addWolfPool: true);
        var altered = step.NewInstance! with {
            ResourcePolicies = step.NewInstance.ResourcePolicies.Select(policy =>
                policy.ResourceId == "resource.primary"
                    ? policy with {
                        Mode = NativeManifestationResourceMode.Separate,
                        MaximumAdjustment = null
                    }
                    : policy).ToArray()
        };
        var attempt = Plan(Character(), Ledger(), "e1",
            step with { NewInstance = altered });

        Assert.Equal("unresolved", attempt.Status);
        Assert.Null(attempt.Proposed);
        Assert.Contains(attempt.Issues,
            issue => issue.Code == "manifestation-resource-policy-unresolved");
    }

    [Fact]
    public void ProposedEffectiveViewShowsNewFormButIsNotACommittedRevision()
    {
        var current = Ledger();
        var plan = Plan(Character(), current, "e1",
            Activate("manifest.wolf", "wolf", "e1", addWolfPool: true));

        Assert.Equal("planned", plan.Status);
        Assert.Equal("partial", plan.ProposedEffective!.Status);
        Assert.Equal("wolf", plan.ProposedEffective.EffectiveSheetId);
        Assert.Equal(17, plan.ProposedEffective.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(8, plan.ProposedEffective.Resources
            .Single(x => x.Id == "resource.primary").Current);
        Assert.Equal(8, plan.Proposed!.Revision);
        Assert.Equal(7, current.Revision);
        Assert.Empty(current.ProcessedEventIds);
    }

    [Fact]
    public void EndRequiresNamedConditionOrIndependentlyVerifiedLaterEvent()
    {
        var current = ActivatedWolf();
        var ordinary = Action(NativeManifestationTransitionAction.End, "manifest.wolf");
        var valid = Plan(Character(), current, "e2", ordinary);
        Assert.Equal("planned", valid.Status);

        var unlisted = ordinary with {
            EndingConditionId = "rule.test.external-displacement",
            RuleOccurrence = Occurrence("actions.external-displacement")
        };
        var denied = Plan(Character(), current, "e2", unlisted);
        Assert.Equal("invalid", denied.Status);
        Assert.Contains(denied.Issues, x => x.Code == "manifestation-ending-unverified");
        Assert.Null(denied.Proposed);

        var verified = NativeCharacterManifestationTransitionPlanner.Plan(
            Character(), current, new("character.1", current.Revision, "e2", [unlisted]),
            new Authority { PermitIndependentEnding = true });
        Assert.Equal("planned", verified.Status);
        Assert.Equal(NativeManifestationLifecycle.Ended,
            verified.Proposed!.Instances.Single().Lifecycle);
        Assert.Equal("wolf", current.Instances.Single().SheetId);
        Assert.Equal(NativeManifestationLifecycle.Active, current.Instances.Single().Lifecycle);

        var wrongSource = unlisted with { RuleOccurrence = Occurrence("actions.unrelated") };
        var rejected = NativeCharacterManifestationTransitionPlanner.Plan(
            Character(), current, new("character.1", current.Revision, "e2", [wrongSource]),
            new Authority { PermitIndependentEnding = true });
        Assert.Equal("invalid", rejected.Status);
        Assert.Null(rejected.Proposed);
    }

    [Fact]
    public void EndWithoutAnEventIdentifierFailsStructuralValidation()
    {
        var current = ActivatedWolf();
        var step = Action(NativeManifestationTransitionAction.End, "manifest.wolf")
            with { EndingConditionId = null };
        var authority = new Authority();
        var result = NativeCharacterManifestationTransitionPlanner.Plan(
            Character(), current, new("character.1", current.Revision, "e2", [step]),
            authority);
        Assert.Equal("invalid", result.Status);
        Assert.Equal(0, authority.TransitionCalls);
    }

    [Fact]
    public void TerminateInteractionRequiresAnActualEndNotAnImplicitPrecedenceRule()
    {
        var state = Character();
        var current = ActivatedWolf();
        var termination = new NativeManifestationInteractionDecision(
            "interaction.terminate", NativeManifestationInteractionKind.Terminate,
            ["manifest.wolf", "manifest.hybrid"], "manifest.hybrid",
            Occurrence("interactions.terminate"), Rule(), "receipt.terminate");
        var activate = Activate("manifest.hybrid", "hybrid", "e2");
        var noEnding = NativeCharacterManifestationTransitionPlanner.Plan(
            state, current, new("character.1", current.Revision, "e2",
                [activate], [termination]), new Authority());
        Assert.Equal("invalid", noEnding.Status);
        Assert.Null(noEnding.Proposed);
        Assert.Contains(noEnding.Issues,
            issue => issue.Code == "manifestation-termination-unexecuted");

        var explicitEnding = NativeCharacterManifestationTransitionPlanner.Plan(
            state, current, new("character.1", current.Revision, "e2", [
                Action(NativeManifestationTransitionAction.End, "manifest.wolf"),
                activate
            ], [termination]), new Authority());
        Assert.Equal("planned", explicitEnding.Status);
        Assert.Equal("hybrid", explicitEnding.ProposedEffective!.EffectiveSheetId);
        Assert.Equal(NativeManifestationLifecycle.Ended,
            explicitEnding.Proposed!.Instances.Single(x => x.Id == "manifest.wolf").Lifecycle);
    }

    [Fact]
    public void UnauthorizedInheritedProgressionAndGrantBlockTransitions()
    {
        var state = Character();
        var step = Activate("manifest.wolf", "wolf", "e1", addWolfPool: true);
        foreach (var authority in new[] {
            new Authority { DenyProgression = true },
            new Authority { DenyGrant = true }
        })
        {
            var result = NativeCharacterManifestationTransitionPlanner.Plan(
                state, Ledger(), new("character.1", 7, "e1", [step]), authority);
            Assert.Equal("invalid", result.Status);
            Assert.Null(result.Proposed);
            Assert.Equal(0, authority.TransitionCalls);
        }
    }

    [Fact]
    public void DuplicateGrantOwnerAndUnrelatedOccurrenceFailClosedBeforeTransition()
    {
        var duplicate = Character(sheets: [
            Werewolf(), Wolf() with { Progression = [Progress("other", "grant.shared")] },
            Hybrid()
        ]);
        var wrongOccurrence = Character(sheets: [
            Werewolf() with {
                Progression = [Progress("shared", "grant.shared") with {
                    SourceOccurrence = new(Source(), "")
                }]
            }, Wolf(), Hybrid()
        ]);
        Assert.Equal("invalid", Plan(duplicate, Ledger(), "e1",
            Activate("manifest.wolf", "wolf", "e1", true)).Status);
        Assert.Equal("invalid", Plan(wrongOccurrence, Ledger(), "e1",
            Activate("manifest.wolf", "wolf", "e1", true)).Status);
    }

    private sealed class OriginAuthority : INativeCharacterOriginBaselineAuthority
    {
        public bool DenyReconstruction { get; set; }
        public bool AuthorizesBaseline(string characterId, NativeOriginProfile profile) =>
            !DenyReconstruction && characterId == "character.1" &&
            profile.Id == "origin.reconstructed";
        public bool AuthorizesGrant(string characterId, NativeOriginProfile profile,
            NativeOriginEffectiveGrant grant) =>
            !DenyReconstruction && characterId == "character.1" &&
            grant.Identity.Occurrence.Source.Id == "source.verified-origin" &&
            grant.Identity.Occurrence.OccurrencePath == "/walkingSpeedFeet";
    }

    private static NativeOriginEffectiveGrant OriginWalkSpeed(int speed) => new(
        "grant.origin.walk." + speed, "manifest.origin.walk." + speed,
        new NativeOriginCharacteristicIdentity(NativeOriginCharacteristicKind.Intrinsic,
            new(new(NativeOriginSourceKind.Native, "source.verified-origin", "v1"),
                "/walkingSpeedFeet"),
            IntrinsicSlotId: "walkingSpeedFeet"),
        ["source.origin.walk"], EffectiveValue: JsonSerializer.SerializeToElement(speed),
        Authorization: new(NativeOriginAdjudication.Supported));

    private static NativeOriginProfile ReconstructedOrigin(int currentUnderlying, int staleCurrent)
    {
        NativeOriginCurrentConfiguration Config(int speed) =>
            new("species.synthetic", null, [OriginWalkSpeed(speed)],
                "origin.configuration." + speed);
        return new("origin.reconstructed", Config(currentUnderlying),
            Config(staleCurrent), [], [], new(NativeOriginAdjudication.Supported),
            ActiveTemporaryTransformation:
                new("activation.wolf", "wolf",
                    new(NativeOriginSourceKind.Native, "source.verified-origin", "v1"),
                    "receipt.old-form"));
    }

    [Fact]
    public void AuthorizedReconstructedUnderlyingPropagatesThroughActiveWolfAndReversion()
    {
        var state = Character(speed: 20);
        var manifested = ActivatedWolf(state);
        var initial = NativeCharacterSheetOriginBaselineAdapter.ApplyWalkSpeed(
            state, ReconstructedOrigin(30, 10), new OriginAuthority());
        var reconstructed = NativeCharacterSheetOriginBaselineAdapter.ApplyWalkSpeed(
            state, ReconstructedOrigin(45, 10), new OriginAuthority());

        Assert.Equal("resolved", initial.Status);
        Assert.Equal("resolved", reconstructed.Status);
        Assert.Equal(30, Resolve(initial.State!, manifested)
            .EffectiveMechanics!.BaseWalkSpeed);
        Assert.Equal(45, Resolve(reconstructed.State!, manifested)
            .EffectiveMechanics!.BaseWalkSpeed);

        var reverted = Plan(reconstructed.State!, manifested, "e2",
            Action(NativeManifestationTransitionAction.End, "manifest.wolf"));
        Assert.Equal("planned", reverted.Status);
        Assert.Equal(NativeCharacterSheetResolver.PrimarySheetId,
            reverted.ProposedEffective!.EffectiveSheetId);
        Assert.Equal(45, reverted.ProposedEffective.EffectiveMechanics!.BaseWalkSpeed);
        Assert.Equal(20, state.PrimaryMechanics.BaseWalkSpeed);
        Assert.Equal(10, ReconstructedOrigin(45, 10).Current.InherentGrants
            .Single().EffectiveValue!.Value.GetInt32());

        var denied = NativeCharacterSheetOriginBaselineAdapter.ApplyWalkSpeed(
            state, ReconstructedOrigin(45, 10),
            new OriginAuthority { DenyReconstruction = true });
        Assert.Equal("unresolved", denied.Status);
        Assert.Null(denied.State);
    }

    private sealed class NativeProjectionFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "rc-manifestation-projector-" + Guid.NewGuid().ToString("N"));
        public NativeCharacterProjector Projector { get; }
        public string Fingerprint { get; }

        public NativeProjectionFixture()
        {
            var directory = Path.Combine(_root, "first-party-001");
            Directory.CreateDirectory(Path.Combine(directory, "native"));
            File.WriteAllText(Path.Combine(directory, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            File.WriteAllText(Path.Combine(directory, "native", "class.json"), """
                {"id":"class.demo","name":"Demo","kind":"class","definition":{
                "savingThrowProficiencies":["dexterity"],
                "progression":[
                    {"level":1,"proficiencyBonus":2,"featureGrants":["class-feature.demo.known"]},
                    {"level":2,"proficiencyBonus":2,"featureGrants":[]}
                ]}}
                """);
            File.WriteAllText(Path.Combine(directory, "native", "feature.json"),
                """{"id":"class-feature.demo.known","name":"Known","kind":"classFeature"}""");
            var catalog = NativeCatalogLoader.Load(
                NativeRulesetLayout.Resolve(_root, "first-party-001"));
            Projector = new NativeCharacterProjector(catalog);
            Fingerprint = catalog.Fingerprint;
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }

    private static NativeCharacterState WithCatalog(
        NativeCharacterState state, NativeProjectionFixture fixture, int level = 1) =>
        state with
        {
            PrimaryMechanics = state.PrimaryMechanics with
            {
                Fingerprint = fixture.Fingerprint,
                Advancements = [new NativeAdvancement("class.demo", level, "level.1")]
            }
        };

    [Fact]
    public void NativeProjectionWithholdsUnverifiedFormFeaturesButPreservesNumbers()
    {
        using var fixture = new NativeProjectionFixture();
        var state = WithCatalog(Character(), fixture);
        var normal = NativeCharacterManifestationProjector.Project(
            state, Ledger(), new Authority(), fixture.Projector);
        var active = NativeCharacterManifestationProjector.Project(
            state, ActivatedWolf(state), new Authority(), fixture.Projector);

        Assert.NotNull(normal.BaseMechanics);
        Assert.Contains("class-feature.demo.known", normal.BaseMechanics.FeatureIds);
        Assert.NotNull(active.BaseMechanics);
        Assert.Equal("partial", active.Status);
        Assert.Equal("partial", active.BaseMechanics.Status);
        Assert.Empty(active.BaseMechanics.FeatureIds);
        Assert.Empty(active.BaseMechanics.Resources);
        Assert.Equal(17, active.BaseMechanics.Values["ability.strength.score"].Value);
        Assert.Contains(active.Issues, x => x.Code == "manifestation-native-features-withheld");
        Assert.Equal("wolf", active.EffectiveCharacter.EffectiveSheetId);
        Assert.Equal(new[] { "grant.shared", "grant.wolf" },
            active.EffectiveCharacter.SheetGrants.Select(x => x.Entitlement.Id));
    }

    [Fact]
    public void CompositeNativeProjectionWithholdsUnverifiedClassUseButKeepsVerifiedSlots()
    {
        using var fixture = new NativeProjectionFixture();
        var state = WithCatalog(Character(speed: 45), fixture);
        var ledger = Coexisting(
            [new(NativeInfluenceCharacteristic.AbilityScore, "strength")],
            [new(NativeInfluenceCharacteristic.AbilityScore, "dexterity")]);

        var composed = NativeCharacterManifestationProjector.Project(
            state, ledger, new Authority(), fixture.Projector);

        Assert.Equal("partial", composed.Status);
        Assert.Equal("@composite", composed.EffectiveCharacter.EffectiveSheetId);
        Assert.Equal(new[] { "manifest.hybrid", "manifest.wolf" },
            composed.EffectiveCharacter.ContributingManifestationInstanceIds);
        Assert.Equal(45, composed.EffectiveCharacter.EffectiveMechanics!.BaseWalkSpeed);
        Assert.NotNull(composed.BaseMechanics);
        Assert.Equal("partial", composed.BaseMechanics.Status);
        Assert.Equal(17, composed.BaseMechanics.Values["ability.strength.score"].Value);
        Assert.Equal(16, composed.BaseMechanics.Values["ability.dexterity.score"].Value);
        Assert.Empty(composed.BaseMechanics.FeatureIds);
        Assert.Empty(composed.BaseMechanics.Resources);
        Assert.Contains(composed.Issues,
            issue => issue.Code == "manifestation-native-features-withheld");
        Assert.Contains(composed.Issues,
            issue => issue.Code == "manifestation-composite-partial");
    }

    [Fact]
    public void NativeProjectionKeepsUpdatedClassAndSourceDerivedValuesWithoutSnapshotRollback()
    {
        using var fixture = new NativeProjectionFixture();
        var updated = WithCatalog(Character(speed: 45), fixture, level: 2);
        var active = NativeCharacterManifestationProjector.Project(
            updated, ActivatedWolf(updated), new Authority(), fixture.Projector);
        var ended = Plan(updated, ActivatedWolf(updated), "e2",
            Action(NativeManifestationTransitionAction.End, "manifest.wolf")).Proposed!;
        var reverted = NativeCharacterManifestationProjector.Project(
            updated, ended, new Authority(), fixture.Projector);

        Assert.Equal(2, active.BaseMechanics!.Values["advancement.total-level"].Value);
        Assert.Equal(45, active.EffectiveCharacter.EffectiveMechanics!.BaseWalkSpeed);
        Assert.Equal(2, reverted.BaseMechanics!.Values["advancement.total-level"].Value);
        Assert.Equal(45, reverted.EffectiveCharacter.EffectiveMechanics!.BaseWalkSpeed);
        Assert.Equal(10, reverted.BaseMechanics.Values["ability.strength.score"].Value);
    }

    [Fact]
    public void NativeProjectionRejectsStaleNativeCatalogVersion()
    {
        using var fixture = new NativeProjectionFixture();
        var state = Character();
        var result = NativeCharacterManifestationProjector.Project(
            state, Ledger(), new Authority(), fixture.Projector);
        Assert.Equal("invalid", result.Status);
        Assert.Null(result.BaseMechanics);
        Assert.Contains(result.Issues, issue => issue.Code == "ruleset-version-mismatch");
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
