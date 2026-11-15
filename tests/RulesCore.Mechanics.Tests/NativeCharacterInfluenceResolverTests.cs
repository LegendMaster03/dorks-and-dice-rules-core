using System.Text.Json;
using RulesCore.Content;
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
        IReadOnlyList<string>? targets = null,
        IReadOnlyList<NativeInfluenceExpressionCondition>? conditions = null,
        IReadOnlyList<NativeInfluenceEarnedRequirement>? prerequisites = null) =>
        new(id, Occurrence("effects." + id), "receipt.effect." + id, Rule(),
            gate ?? Expression(), targets ?? [NativeCharacterSheetResolver.PrimarySheetId],
            characteristic, operation, key, number, boolean, text,
            conditions, prerequisites);

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

    private static NativeInfluenceEarnedRequirement RequireProgression(
        string id) => new(NativeInfluenceEarnedKind.Progression,
            "synthetic.progression.definition", Occurrence("progress." + id));

    private static NativeInfluenceEarnedRequirement RequireGrant(
        string id) => new(NativeInfluenceEarnedKind.Grant,
            "synthetic.capability", Occurrence("grants." + id));

    private sealed class ProjectionFixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), "rc-progressive-influence-" + Guid.NewGuid().ToString("N"));
        public NativeCharacterProjector Projector { get; }

        public ProjectionFixture()
        {
            var dir = Path.Combine(_root, "first-party-001");
            Directory.CreateDirectory(Path.Combine(dir, "native"));
            File.WriteAllText(Path.Combine(dir, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            File.WriteAllText(Path.Combine(dir, "native", "class.json"), """
                {"id":"class.demo","name":"Demo","kind":"class","definition":{
                "savingThrowProficiencies":["dexterity"],
                "progression":[
                    {"level":1,"proficiencyBonus":2,"featureGrants":[],"sorceryPointMaximum":2},
                    {"level":2,"proficiencyBonus":2,"featureGrants":[],"sorceryPointMaximum":4}
                ]}}
                """);
            var catalog = NativeCatalogLoader.Load(
                NativeRulesetLayout.Resolve(_root, "first-party-001"));
            Projector = new NativeCharacterProjector(catalog);
            Fingerprint = catalog.Fingerprint;
        }

        public string Fingerprint { get; }
        public void Dispose() => Directory.Delete(_root, recursive: true);
    }

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

    [Fact]
    public void VampireEffectsRequireSeparatelyEarnedAndSourceQualifiedMilestones()
    {
        var stageOne = Progression("progress.vampire", "grant.vampire");
        var stageTwo = Progression("progress.second", "grant.second");
        var effects = new[]
        {
            Effect("earned-strength", NativeInfluenceCharacteristic.AbilityScore,
                key: "strength", number: 13, gate: Expression(("phase", "manifest")),
                prerequisites: [RequireProgression("progress.vampire")]),
            Effect("earned-defense", NativeInfluenceCharacteristic.DefenseContribution,
                NativeInfluenceOperation.Adjust, "natural", number: 2,
                gate: Expression(("phase", "manifest")),
                prerequisites: [RequireGrant("grant.second")])
        };
        NativeCharacterState State(string phase,
            IReadOnlyList<NativeSheetEarnedProgression>? earned) =>
            Character([Influence(expression: Expression(("phase", phase)),
                effects: effects, progression: earned)]);

        // Acquisition, even with the complete authored effect catalog, does
        // not issue a progression record or immediately activate its effects.
        var acquired = Resolve(State("manifest", null));
        Assert.Equal("resolved", acquired.Status);
        Assert.Empty(acquired.AppliedEffects);
        Assert.Equal(10, acquired.EffectiveMechanics!.AbilityScores["strength"]);

        var one = Resolve(State("manifest", [stageOne]));
        Assert.Equal(13, one.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(1, one.EffectiveMechanics.DefenseContributions!["natural"]);
        Assert.Single(one.AppliedEffects);

        var two = Resolve(State("manifest", [stageOne, stageTwo]));
        Assert.Equal(13, two.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(3, two.EffectiveMechanics.DefenseContributions!["natural"]);
        Assert.Equal(2, two.AppliedEffects.Count);

        // A reversible reduction suppresses the expression, not ownership.
        var dormant = Resolve(State("dormant", [stageOne, stageTwo]));
        Assert.Equal(10, dormant.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(1, dormant.EffectiveMechanics.DefenseContributions!["natural"]);
        Assert.Equal(2, dormant.Progression.Count);
        Assert.Equal(2, dormant.Grants.Count);
        Assert.Equal(2, Resolve(State("manifest", [stageOne, stageTwo])).AppliedEffects.Count);

        // Identical canonical definition names from an unrelated occurrence
        // are not an alternative source of the required earned milestone.
        var wrongOccurrence = stageOne with
        {
            SourceOccurrence = Occurrence("progress.some-other-occurrence")
        };
        var unmatched = Resolve(State("manifest", [wrongOccurrence]));
        Assert.Empty(unmatched.AppliedEffects);
        Assert.Equal(10, unmatched.EffectiveMechanics!.AbilityScores["strength"]);
    }

    [Fact]
    public void MultidimensionalNumericExpressionRangesAreReversibleWithoutAnEngineScale()
    {
        var conditions = new[]
        {
            new NativeInfluenceExpressionCondition("intensity",
                NativeInfluenceExpressionComparison.GreaterThanOrEqual, "1.25"),
            new NativeInfluenceExpressionCondition("intensity",
                NativeInfluenceExpressionComparison.LessThan, "3.5"),
            new NativeInfluenceExpressionCondition("control",
                NativeInfluenceExpressionComparison.Equals, "lost")
        };
        var effect = Effect("curse-range", NativeInfluenceCharacteristic.BaseWalkSpeed,
            number: 20, conditions: conditions);
        NativeCharacterState State(string intensity, string control) =>
            Character([Influence("influence.curse",
                expression: Expression(("intensity", intensity), ("control", control)),
                effects: [effect])]);

        Assert.Equal(30, Resolve(State("1.249", "lost")).EffectiveMechanics!.BaseWalkSpeed);
        Assert.Equal(20, Resolve(State("1.25", "lost")).EffectiveMechanics!.BaseWalkSpeed);
        Assert.Equal(20, Resolve(State("3.499", "lost")).EffectiveMechanics!.BaseWalkSpeed);
        Assert.Equal(30, Resolve(State("3.5", "lost")).EffectiveMechanics!.BaseWalkSpeed);
        Assert.Equal(30, Resolve(State("2.0", "regained")).EffectiveMechanics!.BaseWalkSpeed);
        Assert.Equal(20, Resolve(State("2.0", "lost")).EffectiveMechanics!.BaseWalkSpeed);
    }

    [Fact]
    public void InvalidNumericPredicatesAndValuesFailBeforeInfluenceCallbacks()
    {
        var badThreshold = Effect("invalid-threshold", NativeInfluenceCharacteristic.BaseWalkSpeed,
            number: 20, conditions:
            [
                new("severity", NativeInfluenceExpressionComparison.GreaterThan, "not-a-number")
            ]);
        var badActual = Effect("invalid-actual", NativeInfluenceCharacteristic.BaseWalkSpeed,
            number: 20, conditions:
            [
                new("severity", NativeInfluenceExpressionComparison.GreaterThan, "1.0")
            ]);
        foreach (var state in new[]
        {
            Character([Influence(expression: Expression(("severity", "2.0")),
                effects: [badThreshold])]),
            Character([Influence(expression: Expression(("severity", "not-a-number")),
                effects: [badActual])])
        })
        {
            var authority = new Authority();
            var result = Resolve(state, authority);
            Assert.Equal("invalid", result.Status);
            Assert.Null(result.EffectiveMechanics);
            Assert.Equal(0, authority.InfluenceCallbacks);
            Assert.Contains(result.Issues, x => x.Code == "invalid-influence-effect");
        }
    }

    [Fact]
    public void EarnedUseDecisionMayDependOnAnotherAcquiredProgression()
    {
        var first = Progression("progress.vampire", "grant.vampire");
        var second = Progression("progress.second", "grant.second");
        var use = Use("manifest", NativeOriginGrantApplicabilityState.Applicable) with
        {
            RequiredEarned = [RequireProgression("progress.second")]
        };
        NativeCharacterState State(IReadOnlyList<NativeSheetEarnedProgression> records) =>
            Character([Influence(expression: Expression(("phase", "manifest")),
                progression: records, applicability: [use])]);
        var early = Resolve(State([first]));
        var advanced = Resolve(State([first, second]));
        Assert.Equal(NativeOriginGrantApplicabilityState.Unresolved,
            early.Grants.Single(g => g.Entitlement.Id == "grant.vampire").Applicability);
        Assert.Equal(NativeOriginGrantApplicabilityState.Applicable,
            advanced.Grants.Single(g => g.Entitlement.Id == "grant.vampire").Applicability);
        Assert.Equal(2, advanced.Progression.Count);
    }

    [Fact]
    public void NumericMaximumChangesAreProjectedWithoutRefillingCurrentResources()
    {
        using var fixture = new ProjectionFixture();
        var increase = Effect("earned-health", NativeInfluenceCharacteristic.HitPointGain,
            NativeInfluenceOperation.Adjust, "level.1", number: 3);
        var state = Character([Influence(effects: [increase])]) with
        {
            PrimaryMechanics = new NativeCharacterRequest(
                "first-party-001", fixture.Fingerprint,
                new Dictionary<string, int>
                {
                    ["strength"] = 10, ["dexterity"] = 12, ["constitution"] = 12,
                    ["intelligence"] = 10, ["wisdom"] = 10, ["charisma"] = 10
                },
                [new NativeAdvancement("class.demo", 1, "level.1")],
                HitPointGains: new Dictionary<string, int> { ["level.1"] = 5 },
                CurrentResources: new Dictionary<string, int> { ["health"] = 4 })
        };

        var original = fixture.Projector.Project(state.PrimaryMechanics);
        var influenced = Resolve(state);
        Assert.Equal("resolved", influenced.Status);
        var changed = fixture.Projector.Project(influenced.EffectiveMechanics!);
        Assert.Equal("resolved", original.Values["hit-points.maximum"].Status);
        Assert.Equal("resolved", changed.Values["hit-points.maximum"].Status);
        Assert.Equal(6, original.Values["hit-points.maximum"].Value);
        Assert.Equal(9, changed.Values["hit-points.maximum"].Value);
        Assert.Equal(4, state.PrimaryMechanics.CurrentResources!["health"]);
        Assert.Equal(4, influenced.EffectiveMechanics!.CurrentResources!["health"]);
        Assert.Equal(5, state.PrimaryMechanics.HitPointGains!["level.1"]);
    }

    [Fact]
    public void InvalidEarnedRequirementFailsClosedAndDetachedGatesRemainImmutable()
    {
        var mutableRequirements = new List<NativeInfluenceEarnedRequirement>
        {
            RequireProgression("progress.vampire")
        };
        var mutableConditions = new List<NativeInfluenceExpressionCondition>
        {
            new("phase", NativeInfluenceExpressionComparison.Equals, "manifest")
        };
        var effect = Effect("dependent", NativeInfluenceCharacteristic.AbilityScore,
            key: "strength", number: 13, conditions: mutableConditions,
            prerequisites: mutableRequirements);
        var state = Character([Influence(expression: Expression(("phase", "manifest")),
            progression: [Progression()], effects: [effect])]);
        var first = Resolve(state);
        Assert.Single(first.AppliedEffects);
        mutableRequirements.Clear();
        mutableConditions.Clear();
        Assert.Single(first.AppliedEffects[0].Effect.RequiredEarned!);
        Assert.Single(first.AppliedEffects[0].Effect.ExpressionConditions!);

        var malformed = effect with
        {
            RequiredEarned =
            [
                new(NativeInfluenceEarnedKind.Progression, "",
                    Occurrence("progress.progress.vampire"))
            ]
        };
        var authority = new Authority();
        var badResult = Resolve(Character([Influence(effects: [malformed])]), authority);
        Assert.Equal("invalid", badResult.Status);
        Assert.Equal(0, authority.InfluenceCallbacks);
    }

    [Fact]
    public void PrimaryLevelChangesDerivedResourceMaximumWithoutRefillingCurrentQuantity()
    {
        using var fixture = new ProjectionFixture();
        var influence = Influence(effects:
        [
            Effect("unrelated-senses", NativeInfluenceCharacteristic.AbilityScore,
                key: "wisdom", number: 15)
        ]);
        var state = Character([influence]) with
        {
            PrimaryMechanics = new NativeCharacterRequest(
                "first-party-001", fixture.Fingerprint,
                new Dictionary<string, int>
                {
                    ["strength"] = 10, ["dexterity"] = 12, ["constitution"] = 12,
                    ["intelligence"] = 10, ["wisdom"] = 10, ["charisma"] = 10
                },
                [new NativeAdvancement("class.demo", 1, "class.demo.1")],
                CurrentResources: new Dictionary<string, int>
                {
                    ["resource.sorcery-points"] = 1
                })
        };
        var advanced = state with
        {
            PrimaryMechanics = state.PrimaryMechanics with
            {
                Advancements = [new NativeAdvancement("class.demo", 2, "class.demo.2")]
            }
        };
        var early = Resolve(state);
        var later = Resolve(advanced);
        Assert.Equal("resolved", early.Status);
        Assert.Equal("resolved", later.Status);
        Assert.Equal(15, early.EffectiveMechanics!.AbilityScores["wisdom"]);
        Assert.Equal(15, later.EffectiveMechanics!.AbilityScores["wisdom"]);

        var before = fixture.Projector.Project(early.EffectiveMechanics);
        var after = fixture.Projector.Project(later.EffectiveMechanics);
        var priorPool = Assert.Single(before.Resources, r => r.Id == "resource.sorcery-points");
        var currentPool = Assert.Single(after.Resources, r => r.Id == "resource.sorcery-points");
        Assert.Equal(2, priorPool.Maximum);
        Assert.Equal(4, currentPool.Maximum);
        Assert.Equal(1, priorPool.Current);
        Assert.Equal(1, currentPool.Current);
        Assert.Equal(1, state.PrimaryMechanics.CurrentResources!["resource.sorcery-points"]);
        Assert.Equal(1, advanced.PrimaryMechanics.CurrentResources!["resource.sorcery-points"]);
    }
}
