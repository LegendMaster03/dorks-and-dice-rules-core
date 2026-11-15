using System.Text.Json;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Synthetic acceptance fixtures. None of these values defines actual vampire,
/// curse, form transition, or supernatural advancement gameplay.
/// </summary>
public sealed class NativeCharacterInfluenceResolverTests
{
    private sealed class Authority : INativeCharacterInfluenceAuthority
    {
        public bool DenyInfluence { get; set; }
        public bool DenyExpression { get; set; }
        public bool DenyEffect { get; set; }
        public bool DenyProgression { get; set; }
        public bool DenyGrant { get; set; }
        public bool DenyApplicability { get; set; }
        public int InfluenceCallbacks { get; private set; }

        public bool AuthorizesPrimary(string characterId, NativeCharacterRequest primary) =>
            characterId == "character.test";
        public bool AuthorizesSheet(string characterId, NativePersistentCharacterSheet sheet) => true;
        public bool AuthorizesProgression(string characterId, string ownerSheetId,
            NativeSheetEarnedProgression progression) => true;
        public bool AuthorizesGrant(string characterId, string ownerSheetId,
            NativeSheetEarnedProgression progression, NativeSheetEarnedGrant grant) => true;
        public bool AuthorizesApplicability(string characterId, string sheetId,
            NativeSheetGrantApplicability decision) => true;
        public bool AuthorizesInfluence(string characterId, NativeCharacterPersistentInfluence influence)
        {
            InfluenceCallbacks++;
            return !DenyInfluence;
        }
        public bool AuthorizesExpression(string characterId, NativeCharacterPersistentInfluence influence) =>
            !DenyExpression;
        public bool AuthorizesEffect(string characterId, string influenceId,
            NativeCharacterInfluenceEffect effect) => !DenyEffect;
        public bool AuthorizesInfluenceProgression(string characterId, string influenceId,
            NativeSheetEarnedProgression progression) => !DenyProgression;
        public bool AuthorizesInfluenceGrant(string characterId, string influenceId,
            NativeSheetEarnedProgression progression, NativeSheetEarnedGrant grant) => !DenyGrant;
        public bool AuthorizesInfluenceApplicability(string characterId, string influenceId,
            NativeInfluenceGrantApplicability decision) => !DenyApplicability;
    }

    private static NativeOriginSourceReference Source() =>
        new(NativeOriginSourceKind.Native, "synthetic.supernatural", "v1");

    private static NativeOriginSourceOccurrence Occurrence(string path) =>
        new(Source(), path);

    private static NativeOriginDecisionBasis Rule() =>
        new(NativeOriginAdjudication.Supported, NativeRuleReference: "rule.synthetic");

    private static IReadOnlyDictionary<string, string> Expression(params (string Key, string Value)[] entries) =>
        entries.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

    private static NativeCharacterInfluenceEffect Effect(string id,
        NativeInfluenceCharacteristic characteristic,
        NativeInfluenceOperation operation = NativeInfluenceOperation.Replace,
        string? key = null, int? number = null, bool? boolean = null, string? text = null,
        IReadOnlyDictionary<string, string>? gate = null,
        IReadOnlyList<string>? targets = null) =>
        new(id, Occurrence("effects." + id), "receipt.effect." + id, Rule(),
            gate ?? Expression(), targets ?? [NativeCharacterSheetResolver.PrimarySheetId],
            characteristic, operation, key, number, boolean, text);

    private static NativeSheetEarnedProgression Progression(
        string id = "progress.vampire", string grantId = "grant.vampire") =>
        new(id, "synthetic.progression.definition", Occurrence("progress." + id),
            "receipt.progress." + id,
            [new(grantId, "synthetic.capability", Occurrence("grants." + grantId),
                "receipt.grant." + grantId)],
            new Dictionary<string, string> { ["chosen"] = "yes" });

    private static NativeInfluenceGrantApplicability Use(string phase,
        NativeOriginGrantApplicabilityState state, string grantId = "grant.vampire") =>
        new(grantId, state, Rule(), Expression(("phase", phase)),
            "receipt.applicability." + phase);

    private static NativeCharacterPersistentInfluence Influence(
        string id = "influence.vampire",
        IReadOnlyDictionary<string, string>? expression = null,
        IReadOnlyList<NativeCharacterInfluenceEffect>? effects = null,
        IReadOnlyList<NativeSheetEarnedProgression>? progression = null,
        IReadOnlyList<NativeInfluenceGrantApplicability>? applicability = null) =>
        new(id, "definition." + id, Source(), "receipt.acquire." + id,
            "receipt.expression." + id, expression ?? Expression(("phase", "dormant")),
            effects ?? [], progression, applicability);

    private static NativePersistentCharacterSheet Alternate() =>
        new("sheet.wolf", NativeCharacterSheetResolver.PrimarySheetId,
            "synthetic.wolf", Source(), "receipt.sheet.wolf",
            Overrides: new(
                Skills: new(false, new Dictionary<string, NativeSheetTrainingPatch>
                {
                    ["skill.listen"] = new(Proficient: new(true))
                })),
            Progression: [Progression("progress.wolf", "grant.wolf")]);

    private static NativeCharacterState Character(
        IReadOnlyList<NativeCharacterPersistentInfluence>? influences = null,
        IReadOnlyList<NativePersistentCharacterSheet>? sheets = null) =>
        new("character.test", new NativeCharacterRequest(
            "first-party-001", "synthetic-catalog",
            new Dictionary<string, int>
            {
                ["strength"] = 10, ["dexterity"] = 12, ["wisdom"] = 14
            },
            [new NativeAdvancement("class.test", 2, "level.2")],
            Skills: new Dictionary<string, NativeSkillTraining>
            {
                ["skill.listen"] = new(Ranks: 2, Proficient: false,
                    MiscModifier: 1, Ability: "wisdom")
            },
            DefenseContributions: new Dictionary<string, int> { ["natural"] = 1 },
            CurrentResources: new Dictionary<string, int> { ["health"] = 8 },
            HitPointGains: new Dictionary<string, int> { ["class.test"] = 4 },
            ConditionIds: ["condition.original"], BaseWalkSpeed: 30),
            sheets ?? [], influences);

    private static NativeCharacterInfluenceResolution Resolve(NativeCharacterState state,
        Authority? authority = null, string target = NativeCharacterSheetResolver.PrimarySheetId) =>
        NativeCharacterInfluenceResolver.Resolve(state, target, authority ?? new Authority());

    [Fact]
    public void AcquisitionAloneDoesNotGrantProgressionOrModifyPrimary()
    {
        var state = Character([Influence()]);
        var result = Resolve(state);
        Assert.Equal("resolved", result.Status);
        Assert.Empty(result.Progression);
        Assert.Empty(result.Grants);
        Assert.Empty(result.AppliedEffects);
        Assert.Equal(10, result.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(10, state.PrimaryMechanics.AbilityScores["strength"]);
    }

    [Fact]
    public void SeparateVampireExpressionDecisionsRevealOnlyAuthoredEffects()
    {
        var effects = new[]
        {
            Effect("vampire-strength", NativeInfluenceCharacteristic.AbilityScore,
                key: "strength", number: 13, gate: Expression(("fangs", "manifest"))),
            Effect("vampire-speed", NativeInfluenceCharacteristic.BaseWalkSpeed,
                number: 35, gate: Expression(("senses", "manifest")))
        };
        NativeCharacterState State(string fangs, string senses) =>
            Character([Influence(expression: Expression(("fangs", fangs), ("senses", senses)),
                effects: effects)]);
        var dormant = Resolve(State("dormant", "dormant"));
        var first = Resolve(State("manifest", "dormant"));
        var second = Resolve(State("manifest", "manifest"));
        Assert.Empty(dormant.AppliedEffects);
        Assert.Equal(10, dormant.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(13, first.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(30, first.EffectiveMechanics.BaseWalkSpeed);
        Assert.Equal(35, second.EffectiveMechanics!.BaseWalkSpeed);
        Assert.Equal(2, second.AppliedEffects.Count);
    }

    [Fact]
    public void ReversibleMultidimensionalCurseUsesLatestPrimaryNotStaleSnapshot()
    {
        var effects = new[]
        {
            Effect("curse-strength", NativeInfluenceCharacteristic.AbilityScore,
                NativeInfluenceOperation.Adjust, "strength", number: -3,
                gate: Expression(("body", "altered"))),
            Effect("curse-speed", NativeInfluenceCharacteristic.BaseWalkSpeed,
                number: 20, gate: Expression(("movement", "altered")))
        };
        NativeCharacterState State(string body, string movement) =>
            Character([Influence("influence.curse",
                Expression(("body", body), ("movement", movement)), effects)]);
        var severe = State("altered", "altered");
        Assert.Equal(7, Resolve(severe).EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(20, Resolve(severe).EffectiveMechanics!.BaseWalkSpeed);

        var restored = Resolve(State("normal", "normal"));
        Assert.Equal(10, restored.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(30, restored.EffectiveMechanics.BaseWalkSpeed);

        var stronger = severe with
        {
            PrimaryMechanics = severe.PrimaryMechanics with
            {
                AbilityScores = new Dictionary<string, int>
                {
                    ["strength"] = 15, ["dexterity"] = 12, ["wisdom"] = 14
                }
            }
        };
        Assert.Equal(12, Resolve(stronger).EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(15, stronger.PrimaryMechanics.AbilityScores["strength"]);
    }

    [Fact]
    public void InfluencePatchesPreserveUnrelatedNestedTrainingProperties()
    {
        var effect = Effect("listen-ranks", NativeInfluenceCharacteristic.SkillRanks,
            NativeInfluenceOperation.Adjust, "skill.listen", number: 2);
        var result = Resolve(Character([Influence(effects: [effect])]));
        var training = result.EffectiveMechanics!.Skills!["skill.listen"];
        Assert.Equal(4, training.Ranks);
        Assert.False(training.Proficient);
        Assert.Equal(1, training.MiscModifier);
        Assert.Equal("wisdom", training.Ability);
    }

    [Fact]
    public void EarnedProgressionSurvivesExpressionLossAndUseRequiresAuthoredDecision()
    {
        var progression = Progression();
        var decisions = new[]
        {
            Use("manifest", NativeOriginGrantApplicabilityState.Applicable),
            Use("dormant", NativeOriginGrantApplicabilityState.Suppressed)
        };
        NativeCharacterState State(string phase) =>
            Character([Influence(expression: Expression(("phase", phase)),
                progression: [progression], applicability: decisions)]);
        var active = Resolve(State("manifest"));
        var dormant = Resolve(State("dormant"));
        var restored = Resolve(State("manifest"));
        Assert.Single(dormant.Progression);
        Assert.Single(dormant.Grants);
        Assert.Equal("influence.vampire", dormant.Grants[0].OwnerInfluenceId);
        Assert.Equal("progress.vampire", dormant.Grants[0].ProgressionId);
        Assert.Equal("grant.vampire", dormant.Grants[0].Entitlement.Id);
        Assert.Equal(NativeOriginGrantApplicabilityState.Applicable, active.Grants[0].Applicability);
        Assert.Equal(NativeOriginGrantApplicabilityState.Suppressed, dormant.Grants[0].Applicability);
        Assert.Equal(NativeOriginGrantApplicabilityState.Applicable, restored.Grants[0].Applicability);

        var noDecision = Resolve(Character([Influence(progression: [progression])]));
        Assert.Equal(NativeOriginGrantApplicabilityState.Unresolved, noDecision.Grants[0].Applicability);
    }

    [Fact]
    public void AlternateSheetsKeepTheirOwnOverridesAndAdvancement()
    {
        var effect = Effect("primary-only", NativeInfluenceCharacteristic.AbilityScore,
            key: "strength", number: 16);
        var state = Character([Influence(effects: [effect])], [Alternate()]);
        var normal = Resolve(state);
        var wolf = Resolve(state, target: "sheet.wolf");
        Assert.Equal(16, normal.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(10, wolf.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.True(wolf.EffectiveMechanics.Skills!["skill.listen"].Proficient);
        Assert.Equal(2, wolf.EffectiveMechanics.Advancements[0].Level);
        Assert.Equal(4, wolf.EffectiveMechanics.HitPointGains!["class.test"]);

        var updated = state with
        {
            PrimaryMechanics = state.PrimaryMechanics with
            {
                AbilityScores = new Dictionary<string, int>
                {
                    ["strength"] = 18, ["dexterity"] = 12, ["wisdom"] = 14
                },
                Advancements = [new NativeAdvancement("class.test", 3, "level.3")]
            }
        };
        var wolfAfter = Resolve(updated, target: "sheet.wolf");
        Assert.Equal(18, wolfAfter.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(3, wolfAfter.EffectiveMechanics.Advancements[0].Level);
        Assert.True(wolfAfter.EffectiveMechanics.Skills!["skill.listen"].Proficient);
        Assert.Single(NativeCharacterSheetResolver.Resolve(updated, "sheet.wolf",
            new Authority()).Progression);
    }

    [Fact]
    public void ExplicitAlternateScopeCanModifyFormWithoutChangingPrimary()
    {
        var effect = Effect("wolf-sense", NativeInfluenceCharacteristic.SkillMiscModifier,
            NativeInfluenceOperation.Adjust, "skill.listen", number: 3,
            targets: ["sheet.wolf"]);
        var state = Character([Influence(effects: [effect])], [Alternate()]);
        Assert.Empty(Resolve(state).AppliedEffects);
        var alt = Resolve(state, target: "sheet.wolf");
        Assert.Single(alt.AppliedEffects);
        Assert.Equal(4, alt.EffectiveMechanics!.Skills!["skill.listen"].MiscModifier);
        Assert.Equal(1, state.PrimaryMechanics.Skills!["skill.listen"].MiscModifier);
    }

    [Fact]
    public void DisjointNumericChangesComposeWithoutInventingPrecedence()
    {
        var a = Influence("influence.a", effects:
        [
            Effect("strength-a", NativeInfluenceCharacteristic.AbilityScore,
                key: "strength", number: 14)
        ]);
        var b = Influence("influence.b", effects:
        [
            Effect("defense-b", NativeInfluenceCharacteristic.DefenseContribution,
                NativeInfluenceOperation.Adjust, "natural", number: 2)
        ]);
        var result = Resolve(Character([a, b]));
        Assert.Equal("resolved", result.Status);
        Assert.Equal(14, result.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(3, result.EffectiveMechanics.DefenseContributions!["natural"]);
    }

    [Fact]
    public void OverlapDoesNotUseInputOrderAsAStackingRule()
    {
        var a = Influence("influence.a", effects:
        [
            Effect("strength-a", NativeInfluenceCharacteristic.AbilityScore,
                key: "strength", number: 13)
        ]);
        var b = Influence("influence.b", effects:
        [
            Effect("strength-b", NativeInfluenceCharacteristic.AbilityScore,
                NativeInfluenceOperation.Adjust, "strength", number: 2)
        ]);
        var first = Resolve(Character([a, b]));
        var reverse = Resolve(Character([b, a]));
        Assert.Equal("unresolved", first.Status);
        Assert.Null(first.EffectiveMechanics);
        Assert.Equal("unresolved", reverse.Status);
        Assert.Null(reverse.EffectiveMechanics);
        Assert.Contains(first.Issues, x => x.Code == "influence-overlap-unresolved");
    }

    [Fact]
    public void UnauthorizedInfluenceExpressionEffectProgressionGrantAndDecisionFailClosed()
    {
        var state = Character([Influence(
            effects: [Effect("test-effect", NativeInfluenceCharacteristic.AbilityScore,
                key: "strength", number: 12)],
            progression: [Progression()],
            applicability: [Use("dormant", NativeOriginGrantApplicabilityState.Suppressed)])]);
        foreach (var deny in new Action<Authority>[]
        {
            a => a.DenyInfluence = true, a => a.DenyExpression = true,
            a => a.DenyEffect = true, a => a.DenyProgression = true,
            a => a.DenyGrant = true, a => a.DenyApplicability = true
        })
        {
            var authority = new Authority();
            deny(authority);
            var result = Resolve(state, authority);
            Assert.Equal("invalid", result.Status);
            Assert.Null(result.EffectiveMechanics);
            Assert.Contains(result.Issues, x => x.Code.StartsWith("unauthorized-", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void MalformedRecordsDoNotReachInfluenceAuthority()
    {
        var invalidEffect = Effect("broken", NativeInfluenceCharacteristic.AbilityScore,
            key: "strength", number: 12) with { NumericValue = null };
        var state = Character([Influence(effects: [invalidEffect])]);
        var authority = new Authority();
        var result = Resolve(state, authority);
        Assert.Equal("invalid", result.Status);
        Assert.Equal(0, authority.InfluenceCallbacks);
        Assert.Contains(result.Issues, x => x.Code == "invalid-influence-effect");

        var missingVersion = Character([Influence() with
        {
            DefinitionSource = new NativeOriginSourceReference(
                NativeOriginSourceKind.Native, "synthetic.supernatural")
        }]);
        Assert.Equal("invalid", Resolve(missingVersion).Status);
    }

    [Fact]
    public void CrossSheetDuplicateAdvancementOrGrantIdentityFailsClosed()
    {
        var influence = Influence(progression: [Progression("progress.wolf", "grant.wolf")]);
        var result = Resolve(Character([influence], [Alternate()]));
        Assert.Equal("invalid", result.Status);
        Assert.Null(result.EffectiveMechanics);
        Assert.Contains(result.Issues, x => x.Code == "invalid-influence-progression");
    }

    [Fact]
    public void ConflictingConditionalGrantDecisionsAreUnresolved()
    {
        var first = new NativeInfluenceGrantApplicability("grant.vampire",
            NativeOriginGrantApplicabilityState.Applicable, Rule(),
            Expression(("phase", "manifest")), "receipt.1");
        var second = new NativeInfluenceGrantApplicability("grant.vampire",
            NativeOriginGrantApplicabilityState.Suppressed, Rule(),
            Expression(("moon", "full")), "receipt.2");
        var result = Resolve(Character([Influence(
            expression: Expression(("phase", "manifest"), ("moon", "full")),
            progression: [Progression()], applicability: [first, second])]));
        Assert.Equal("partial", result.Status);
        Assert.Equal(NativeOriginGrantApplicabilityState.Unresolved, result.Grants[0].Applicability);
        Assert.Contains(result.Issues, x => x.Code == "influence-grant-applicability-unresolved");
    }

    [Fact]
    public void ChangesToDerivedHitPointMaxInputsDoNotRestoreCurrentResources()
    {
        var effect = Effect("hp-growth", NativeInfluenceCharacteristic.HitPointGain,
            NativeInfluenceOperation.Adjust, "class.test", number: 3);
        var state = Character([Influence(effects: [effect])]);
        var view = Resolve(state);
        Assert.Equal(7, view.EffectiveMechanics!.HitPointGains!["class.test"]);
        Assert.Equal(8, view.EffectiveMechanics.CurrentResources!["health"]);
        Assert.Equal(4, state.PrimaryMechanics.HitPointGains!["class.test"]);
        Assert.Equal(8, state.PrimaryMechanics.CurrentResources!["health"]);
    }

    [Fact]
    public void ResolutionIsRepeatableDetachedAndTraceable()
    {
        var effects = new[]
        {
            Effect("sight", NativeInfluenceCharacteristic.SkillAbility,
                key: "skill.listen", text: "intelligence")
        };
        var expression = new Dictionary<string, string> { ["phase"] = "manifest" };
        var targets = new List<string> { NativeCharacterSheetResolver.PrimarySheetId };
        effects[0] = effects[0] with { TargetSheetIds = targets, RequiredExpression = expression };
        var state = Character([Influence(expression: expression, effects: effects,
            progression: [Progression()])]);
        var before = JsonSerializer.Serialize(state);
        var one = Resolve(state);
        var two = Resolve(state);
        Assert.Equal("resolved", one.Status);
        Assert.Equal(JsonSerializer.Serialize(one.EffectiveMechanics),
            JsonSerializer.Serialize(two.EffectiveMechanics));
        Assert.Equal(before, JsonSerializer.Serialize(state));
        Assert.Equal("effects.sight", one.AppliedEffects[0].Effect.DefinitionOccurrence.OccurrencePath);
        Assert.Equal("receipt.acquire.influence.vampire",
            state.PersistentInfluences![0].AcquisitionEvidenceId);
        Assert.Equal("receipt.progress.progress.vampire",
            one.Progression[0].Record.AcquisitionEvidenceId);

        targets.Clear();
        expression["phase"] = "dormant";
        Assert.Single(one.AppliedEffects[0].Effect.TargetSheetIds);
        Assert.Equal("manifest", one.AppliedEffects[0].Effect.RequiredExpression["phase"]);
        Assert.Equal("intelligence", one.EffectiveMechanics!.Skills!["skill.listen"].Ability);
    }

    [Fact]
    public void SuppressionEditsOnlyDerivedConditionOrOptionalBinding()
    {
        var remove = Effect("suppress-condition", NativeInfluenceCharacteristic.ConditionId,
            NativeInfluenceOperation.Suppress, "condition.original");
        var clear = Effect("suppress-ability", NativeInfluenceCharacteristic.SkillAbility,
            NativeInfluenceOperation.Suppress, "skill.listen");
        var result = Resolve(Character([Influence(effects: [remove, clear])]));
        Assert.DoesNotContain("condition.original", result.EffectiveMechanics!.ConditionIds!);
        Assert.Null(result.EffectiveMechanics.Skills!["skill.listen"].Ability);
    }

    [Fact]
    public void LocalAlternateOverrideAndTargetedInfluenceRemainUnresolved()
    {
        var effect = Effect("wolf-training", NativeInfluenceCharacteristic.SkillProficient,
            key: "skill.listen", boolean: false, targets: ["sheet.wolf"]);
        var state = Character([Influence(effects: [effect])], [Alternate()]);
        var result = Resolve(state, target: "sheet.wolf");
        Assert.Equal("unresolved", result.Status);
        Assert.Null(result.EffectiveMechanics);
        Assert.Contains(result.Issues, x => x.Code == "influence-sheet-overlap-unresolved");
        Assert.True(Alternate().Overrides!.Skills!.Entries["skill.listen"].Proficient!.Value);
    }

    [Fact]
    public void IndependentNestedPropertiesMayBeModifiedTogether()
    {
        var a = Influence("influence.a", effects:
        [
            Effect("ranks", NativeInfluenceCharacteristic.SkillRanks,
                NativeInfluenceOperation.Adjust, "skill.listen", number: 2)
        ]);
        var b = Influence("influence.b", effects:
        [
            Effect("proficient", NativeInfluenceCharacteristic.SkillProficient,
                key: "skill.listen", boolean: true)
        ]);
        var result = Resolve(Character([a, b]));
        Assert.Equal("resolved", result.Status);
        Assert.Equal(4, result.EffectiveMechanics!.Skills!["skill.listen"].Ranks);
        Assert.True(result.EffectiveMechanics.Skills["skill.listen"].Proficient);
        Assert.Equal(1, result.EffectiveMechanics.Skills["skill.listen"].MiscModifier);
    }

    [Fact]
    public void MissingAdjustTargetAndIntegerOverflowAreNotImplicitGameplayDefaults()
    {
        var absent = Effect("missing-defense", NativeInfluenceCharacteristic.DefenseContribution,
            NativeInfluenceOperation.Adjust, "not-present", number: 2);
        var overflow = Effect("overflow", NativeInfluenceCharacteristic.AbilityScore,
            NativeInfluenceOperation.Adjust, "strength", number: int.MaxValue);
        foreach (var effect in new[] { absent, overflow })
        {
            var state = Character([Influence(effects: [effect])]);
            var result = Resolve(state);
            Assert.Equal("unresolved", result.Status);
            Assert.Null(result.EffectiveMechanics);
            Assert.Contains(result.Issues, x => x.Code == "influence-value-unresolved");
            Assert.Equal(10, state.PrimaryMechanics.AbilityScores["strength"]);
        }
    }

    [Fact]
    public void UnknownExplicitFormTargetFailsBeforeInfluenceAuthorization()
    {
        var effect = Effect("unknown-form", NativeInfluenceCharacteristic.AbilityScore,
            key: "strength", number: 14, targets: ["sheet.not-owned"]);
        var authority = new Authority();
        var result = Resolve(Character([Influence(effects: [effect])]), authority);
        Assert.Equal("invalid", result.Status);
        Assert.Null(result.EffectiveMechanics);
        Assert.Equal(0, authority.InfluenceCallbacks);
        Assert.Contains(result.Issues, x => x.Code == "invalid-influence-effect");
    }

    [Fact]
    public void DuplicateGrantAcrossSheetAndInfluenceFailsIndependentlyOfProgressionId()
    {
        var state = Character(
            [Influence(progression: [Progression("progress.influence", "grant.wolf")])],
            [Alternate()]);
        var result = Resolve(state);
        Assert.Equal("invalid", result.Status);
        Assert.Contains(result.Issues, x => x.Code == "invalid-influence-grant");
    }
}
