using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Tests;

public sealed class CharacterSupportResolverTests
{
    [Fact]
    public void EmptyCatalogProducesNoSupportRows()
    {
        var projection = CharacterSupportResolver.Project(
            Catalog(),
            new CharacterSupportProjectionRequest());

        Assert.Empty(projection.RecoveryProcedures);
        Assert.Empty(projection.PassiveValues);
        Assert.Empty(projection.Qualifications);
    }

    [Fact]
    public void RecoveryDiscoveryPreservesRolesAdditionalProceduresCapabilitiesAndCrossEditionSemantics()
    {
        var shortThreeX = Recovery(
            "recovery.3x-eight-hours",
            "Eight-Hour Recovery",
            CharacterRecoveryPresentationRoles.ShortRest,
            effects:
            [
                Effect(
                    "recover-nonlethal",
                    "resource",
                    "damage.nonlethal",
                    "adjust",
                    amount: -1)
            ],
            attribution: Attribution("3.5e"));
        var shortFiveX = Recovery(
            "recovery.5x-short-rest",
            "Short Rest",
            CharacterRecoveryPresentationRoles.ShortRest,
            requiredCapabilities: ["recovery.hit-dice"],
            effects:
            [
                Effect(
                    "recover-hp",
                    "resource",
                    "hit-points",
                    "adjust",
                    amount: 4)
            ],
            attribution: Attribution("5e"));
        var longRest = Recovery(
            "recovery.long-rest",
            "Long Rest",
            CharacterRecoveryPresentationRoles.LongRest);
        var focus = Recovery(
            "recovery.psionic-focus",
            "Renew Psionic Focus",
            "psionic-focus",
            requiredCapabilities: ["psionics"]);

        var catalog = Catalog(recovery:
        [
            shortThreeX,
            shortFiveX,
            longRest,
            focus
        ]);

        var unresolved = CharacterSupportResolver.Project(
            catalog,
            new CharacterSupportProjectionRequest());
        Assert.Equal(4, unresolved.RecoveryProcedures.Count);
        Assert.Equal(
            2,
            unresolved.RecoveryProcedures.Count(value =>
                value.PresentationRole == CharacterRecoveryPresentationRoles.ShortRest));
        Assert.Contains(
            unresolved.RecoveryProcedures,
            value => value.ProcedureKey == focus.Key
                && value.ApplicabilityState
                    == CharacterSupportResolutionStates.UnresolvedCharacterState);
        Assert.Contains(
            unresolved.RecoveryProcedures,
            value => value.ProcedureKey == shortThreeX.Key
                && value.SourceAttributions.Single().GameEdition == "3.5e");
        Assert.Contains(
            unresolved.RecoveryProcedures,
            value => value.ProcedureKey == shortFiveX.Key
                && value.SourceAttributions.Single().GameEdition == "5e");

        var explicitCapabilities = CharacterSupportResolver.Project(
            catalog,
            new CharacterSupportProjectionRequest(
                CapabilityKeys: ["recovery.hit-dice"]));
        Assert.Contains(
            explicitCapabilities.RecoveryProcedures,
            value => value.ProcedureKey == shortFiveX.Key
                && value.ApplicabilityState
                    == CharacterSupportResolutionStates.Applicable);
        Assert.Contains(
            explicitCapabilities.RecoveryProcedures,
            value => value.ProcedureKey == focus.Key
                && value.ApplicabilityState
                    == CharacterSupportResolutionStates.NotApplicable);
    }

    [Fact]
    public void RecoveryRollsPreserveRuleDerivedRollModeForConsumers()
    {
        var procedure = Recovery(
            "recovery.roll-mode",
            "Mode-aware Recovery",
            CharacterRecoveryPresentationRoles.ShortRest,
            rolls:
            [
                new CharacterRecoveryRollDefinition(
                    "save",
                    "d20",
                    "Roll the save.",
                    true,
                    "check.recovery",
                    CharacterMechanicRollModes.Emphasis)
            ]);

        var result = CharacterSupportResolver.ResolveRecovery(
            Catalog(recovery: [procedure]),
            procedure.Key,
            new CharacterRecoveryResolutionRequest());

        Assert.NotNull(result);
        var roll = Assert.Single(result!.PendingRolls);
        Assert.Equal("d20", roll.RollKind);
        Assert.Equal(CharacterMechanicRollModes.Emphasis, roll.RollMode);
    }

    [Fact]
    public void RecoveryResolutionReturnsStructuredEffectsChoicesRollsAndRejectsOpaqueOutcomes()
    {
        var procedure = Recovery(
            "recovery.fixture",
            "Fixture Recovery",
            CharacterRecoveryPresentationRoles.ShortRest,
            inputs:
            [
                Input(
                    "pointsToSpend",
                    CharacterMechanicInputValueKinds.Integer,
                    CharacterMechanicInputOrigins.CharacterState,
                    required: true)
            ],
            choices:
            [
                new CharacterRecoveryChoiceDefinition(
                    "resource",
                    "Choose the resource to recover.",
                    true,
                    [
                        new("focus", "Focus", "focus-points"),
                        new("stamina", "Stamina", "stamina-points")
                    ])
            ],
            rolls:
            [
                new CharacterRecoveryRollDefinition(
                    "recoveryRoll",
                    "die",
                    "Roll recovery.",
                    true,
                    "check.recovery")
            ],
            effects:
            [
                Effect(
                    "spend-points",
                    "resource",
                    "recovery-points",
                    "expend",
                    amountInputKey: "pointsToSpend"),
                new CharacterRecoveryEffectDefinition(
                    "recover-selected",
                    "resource",
                    TargetKey: null,
                    TargetChoiceKey: "resource",
                    "adjust",
                    Amount: null,
                    AmountInputKey: null,
                    AmountRollKey: "recoveryRoll",
                    Value: null,
                    ValueChoiceKey: null,
                    ReferenceKey: "rule-defined-limit",
                    Conditions: [])
            ],
            requiresResourceExpenditure: true);

        var catalog = Catalog(recovery: [procedure]);

        var missingInput = CharacterSupportResolver.ResolveRecovery(
            catalog,
            procedure.Key,
            new CharacterRecoveryResolutionRequest());
        Assert.NotNull(missingInput);
        Assert.Equal(
            CharacterRecoveryResolutionStatuses.InputRequired,
            missingInput.Status);
        Assert.Contains("pointsToSpend", missingInput.MissingInputKeys);

        var choiceRequired = CharacterSupportResolver.ResolveRecovery(
            catalog,
            procedure.Key,
            new CharacterRecoveryResolutionRequest(
                IntegerInputs: new Dictionary<string, int>
                {
                    ["pointsToSpend"] = 1
                }));
        Assert.NotNull(choiceRequired);
        Assert.Equal(
            CharacterRecoveryResolutionStatuses.ChoiceRequired,
            choiceRequired.Status);
        Assert.Single(choiceRequired.PendingChoices);
        Assert.Single(choiceRequired.PendingRolls);

        var rollRequired = CharacterSupportResolver.ResolveRecovery(
            catalog,
            procedure.Key,
            new CharacterRecoveryResolutionRequest(
                IntegerInputs: new Dictionary<string, int>
                {
                    ["pointsToSpend"] = 1
                },
                Choices: new Dictionary<string, string>
                {
                    ["resource"] = "focus"
                }));
        Assert.NotNull(rollRequired);
        Assert.Equal(
            CharacterRecoveryResolutionStatuses.RollRequired,
            rollRequired.Status);
        Assert.Single(rollRequired.PendingRolls);

        var resolved = CharacterSupportResolver.ResolveRecovery(
            catalog,
            procedure.Key,
            new CharacterRecoveryResolutionRequest(
                IntegerInputs: new Dictionary<string, int>
                {
                    ["pointsToSpend"] = 1
                },
                Choices: new Dictionary<string, string>
                {
                    ["resource"] = "focus"
                },
                Rolls: new Dictionary<string, int>
                {
                    ["recoveryRoll"] = 6
                }));
        Assert.NotNull(resolved);
        Assert.Equal(
            CharacterRecoveryResolutionStatuses.Resolved,
            resolved.Status);
        Assert.Equal(2, resolved.Consequences.Count);

        var expenditure = Assert.Single(
            resolved.Consequences,
            value => value.EffectKey == "spend-points");
        Assert.Equal("expend", expenditure.Operation);
        Assert.Equal(1, expenditure.Amount);

        var recovery = Assert.Single(
            resolved.Consequences,
            value => value.EffectKey == "recover-selected");
        Assert.Equal("focus-points", recovery.TargetKey);
        Assert.Equal(6, recovery.Amount);
        Assert.Equal("rule-defined-limit", recovery.ReferenceKey);

        Assert.Throws<ArgumentException>(() =>
            CharacterSupportResolver.ResolveRecovery(
                catalog,
                procedure.Key,
                new CharacterRecoveryResolutionRequest(
                    IntegerInputs: new Dictionary<string, int>
                    {
                        ["newHp"] = 999
                    })));
    }

    [Fact]
    public void PassiveValuesAreRuleDefinedArbitraryAndHonestlyUnresolved()
    {
        var fixedAutomatic = Passive(
            "passive.take-ten-search",
            "Automatic Search",
            constant: 17,
            inputs: [],
            relatedConceptKey: "skill.search",
            relatedAbilityKey: "intelligence",
            attribution: Attribution("3.5e"));
        var derived = Passive(
            "passive.awareness",
            "Awareness",
            constant: 3,
            inputs:
            [
                Input(
                    "awarenessContribution",
                    CharacterMechanicInputValueKinds.Integer,
                    CharacterMechanicInputOrigins.Derived,
                    required: true,
                    participates: true)
            ],
            attribution: Attribution("fixture"));
        var alternate = Passive(
            "passive.sense-danger",
            "Sense Danger",
            constant: 5,
            inputs:
            [
                Input(
                    "dangerContribution",
                    CharacterMechanicInputValueKinds.Integer,
                    CharacterMechanicInputOrigins.Derived,
                    required: true,
                    participates: true)
            ],
            attribution: Attribution("fixture"));

        var catalog = Catalog(passive: [fixedAutomatic, derived, alternate]);
        var unresolved = CharacterSupportResolver.Project(
            catalog,
            new CharacterSupportProjectionRequest());

        var automatic = Assert.Single(
            unresolved.PassiveValues,
            value => value.MechanicKey == fixedAutomatic.Mechanic.Key);
        Assert.Equal(CharacterSupportResolutionStates.Resolved, automatic.ResolutionState);
        Assert.Equal(17, automatic.Value);
        Assert.Equal("skill.search", automatic.RelatedConceptKey);
        Assert.Equal("intelligence", automatic.RelatedAbilityKey);

        var pending = Assert.Single(
            unresolved.PassiveValues,
            value => value.MechanicKey == derived.Mechanic.Key);
        Assert.Equal(
            CharacterSupportResolutionStates.UnresolvedCharacterState,
            pending.ResolutionState);
        Assert.Null(pending.Value);
        Assert.Contains("awarenessContribution", pending.MissingInputKeys);

        var resolved = CharacterSupportResolver.Project(
            catalog,
            new CharacterSupportProjectionRequest(
                FactsBySupportKey:
                    new Dictionary<string, CharacterSupportInputValues>
                    {
                        [derived.Mechanic.Key] = new(
                            IntegerInputs: new Dictionary<string, int>
                            {
                                ["awarenessContribution"] = 8
                            }),
                        [alternate.Mechanic.Key] = new(
                            IntegerInputs: new Dictionary<string, int>
                            {
                                ["dangerContribution"] = 2
                            })
                    }));

        Assert.Equal(
            11,
            Assert.Single(
                resolved.PassiveValues,
                value => value.MechanicKey == derived.Mechanic.Key).Value);
        Assert.Equal(
            7,
            Assert.Single(
                resolved.PassiveValues,
                value => value.MechanicKey == alternate.Mechanic.Key).Value);
        Assert.DoesNotContain(
            resolved.PassiveValues,
            value => value.MechanicKey.Contains("perception", StringComparison.OrdinalIgnoreCase));

        Assert.NotEqual(10, fixedAutomatic.Mechanic.Constant);
        Assert.NotEqual(10, derived.Mechanic.Constant);
    }

    [Fact]
    public void QualificationsSupportArbitraryCategoriesStatesCapabilitiesAndCanonicalIdentity()
    {
        var stealthConceptId = Guid.NewGuid();
        var martialConceptId = Guid.NewGuid();
        var qualifications = new[]
        {
            Qualification(
                "qualification.skill.stealth",
                "Stealth Training",
                "skill-training",
                "skills",
                Input(
                    "trainingState",
                    CharacterMechanicInputValueKinds.String,
                    CharacterMechanicInputOrigins.CharacterState,
                    required: true),
                associatedConceptKey: "skill.stealth",
                associatedRuleConceptId: stealthConceptId),
            Qualification(
                "qualification.weapon.martial",
                "Martial Weapons",
                "weapon-group",
                "martial",
                Input(
                    "isProficient",
                    CharacterMechanicInputValueKinds.Boolean,
                    CharacterMechanicInputOrigins.CharacterState,
                    required: true),
                requiredCapabilities: ["weapons.martial"],
                associatedConceptKey: "weapon.martial",
                associatedRuleConceptId: martialConceptId),
            Qualification(
                "qualification.language.draconic",
                "Draconic",
                "language",
                null,
                Input(
                    "knowledge",
                    CharacterMechanicInputValueKinds.String,
                    CharacterMechanicInputOrigins.CharacterState,
                    required: true)),
            Qualification(
                "qualification.class-skill.knowledge-arcana",
                "Knowledge (Arcana) Class Skill",
                "class-skill",
                "knowledge",
                Input(
                    "isClassSkill",
                    CharacterMechanicInputValueKinds.Boolean,
                    CharacterMechanicInputOrigins.CharacterState,
                    required: true)),
            Qualification(
                "qualification.future.resonance",
                "Resonance Certification",
                "future-resonance-certification",
                "resonance",
                Input(
                    "rank",
                    CharacterMechanicInputValueKinds.Integer,
                    CharacterMechanicInputOrigins.CharacterState,
                    required: true))
        };

        var catalog = Catalog(qualifications: qualifications);
        var unresolved = CharacterSupportResolver.Project(
            catalog,
            new CharacterSupportProjectionRequest());

        Assert.Equal(5, unresolved.Qualifications.Count);
        Assert.All(
            unresolved.Qualifications,
            value => Assert.Equal(
                CharacterSupportResolutionStates.UnresolvedCharacterState,
                value.ResolutionState));

        var resolved = CharacterSupportResolver.Project(
            catalog,
            new CharacterSupportProjectionRequest(
                FactsBySupportKey:
                    new Dictionary<string, CharacterSupportInputValues>
                    {
                        ["qualification.skill.stealth"] = new(
                            StringInputs: new Dictionary<string, string>
                            {
                                ["trainingState"] = "trained"
                            }),
                        ["qualification.weapon.martial"] = new(
                            BooleanInputs: new Dictionary<string, bool>
                            {
                                ["isProficient"] = true
                            }),
                        ["qualification.language.draconic"] = new(
                            StringInputs: new Dictionary<string, string>
                            {
                                ["knowledge"] = "known"
                            }),
                        ["qualification.class-skill.knowledge-arcana"] = new(
                            BooleanInputs: new Dictionary<string, bool>
                            {
                                ["isClassSkill"] = true
                            }),
                        ["qualification.future.resonance"] = new(
                            IntegerInputs: new Dictionary<string, int>
                            {
                                ["rank"] = 3
                            })
                    },
                CapabilityKeys: ["weapons.martial"]));

        Assert.Equal(
            "trained",
            Assert.Single(
                resolved.Qualifications,
                value => value.QualificationKey == "qualification.skill.stealth")
                .State!.StringValue);
        var martial = Assert.Single(
            resolved.Qualifications,
            value => value.QualificationKey == "qualification.weapon.martial");
        Assert.True(martial.State!.BooleanValue);
        Assert.Equal(martialConceptId, martial.AssociatedRuleConceptId);
        Assert.Equal("weapon.martial", martial.AssociatedConceptKey);
        Assert.Equal(
            "known",
            Assert.Single(
                resolved.Qualifications,
                value => value.Category == "language").State!.StringValue);
        Assert.True(
            Assert.Single(
                resolved.Qualifications,
                value => value.Category == "class-skill").State!.BooleanValue);
        Assert.Equal(
            3,
            Assert.Single(
                resolved.Qualifications,
                value => value.Category == "future-resonance-certification")
                .State!.IntegerValue);

        var capabilityMissing = CharacterSupportResolver.Project(
            catalog,
            new CharacterSupportProjectionRequest(
                CapabilityKeys: []));
        Assert.Equal(
            CharacterSupportResolutionStates.NotApplicable,
            Assert.Single(
                capabilityMissing.Qualifications,
                value => value.QualificationKey == "qualification.weapon.martial")
                .ResolutionState);
    }

    private static CharacterSupportCatalogDefinition Catalog(
        IReadOnlyList<CharacterRecoveryProcedureDefinition>? recovery = null,
        IReadOnlyList<CharacterPassiveValueDefinition>? passive = null,
        IReadOnlyList<CharacterQualificationDefinition>? qualifications = null) =>
        new(
            "global",
            null,
            7,
            new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero),
            recovery ?? [],
            passive ?? [],
            qualifications ?? []);

    private static CharacterRecoveryProcedureDefinition Recovery(
        string key,
        string displayName,
        string? role,
        IReadOnlyList<string>? requiredCapabilities = null,
        IReadOnlyList<CharacterMechanicInputDefinition>? inputs = null,
        IReadOnlyList<CharacterRecoveryChoiceDefinition>? choices = null,
        IReadOnlyList<CharacterRecoveryRollDefinition>? rolls = null,
        IReadOnlyList<CharacterRecoveryEffectDefinition>? effects = null,
        bool requiresResourceExpenditure = false,
        CharacterMechanicSourceAttributionView? attribution = null)
    {
        var normalizedInputs = inputs ?? [];
        var applicability = Applicability(
            requiredCapabilities,
            normalizedInputs.Any(value =>
                value.Origin == CharacterMechanicInputOrigins.CharacterState));
        var normalizedChoices = choices ?? [];
        var normalizedRolls = rolls ?? [];
        var normalizedEffects = effects ?? [];
        return new CharacterRecoveryProcedureDefinition(
            key,
            displayName,
            role,
            true,
            applicability,
            normalizedInputs,
            normalizedChoices,
            normalizedRolls,
            normalizedEffects,
            new CharacterRecoveryRuntimeRequirementsView(
                applicability.RequiresCharacterState,
                normalizedChoices.Any(value => value.Required),
                normalizedRolls.Any(value => value.Required),
                requiresResourceExpenditure,
                normalizedInputs.Any(value =>
                    value.Origin == CharacterMechanicInputOrigins.Runtime)),
            attribution is null ? [] : [attribution]);
    }

    private static CharacterPassiveValueDefinition Passive(
        string key,
        string displayName,
        int constant,
        IReadOnlyList<CharacterMechanicInputDefinition> inputs,
        string? relatedConceptKey = null,
        string? relatedAbilityKey = null,
        CharacterMechanicSourceAttributionView? attribution = null) =>
        new(
            new CharacterMechanicDefinition(
                key,
                CharacterMechanicKinds.PassiveValue,
                displayName,
                CharacterMechanicEvaluationKinds.Sum,
                constant,
                inputs,
                TargetInputKey: null,
                BaseMechanicKey: null,
                Applicability(),
                ConditionalRollRules: [],
                BooleanRequirements: []),
            true,
            PresentationRole: null,
            RelatedMechanicKey: null,
            relatedConceptKey,
            RelatedRuleConceptId: null,
            relatedAbilityKey,
            attribution is null ? [] : [attribution]);

    private static CharacterQualificationDefinition Qualification(
        string key,
        string displayName,
        string category,
        string? family,
        CharacterMechanicInputDefinition stateInput,
        IReadOnlyList<string>? requiredCapabilities = null,
        string? associatedConceptKey = null,
        Guid? associatedRuleConceptId = null) =>
        new(
            key,
            displayName,
            category,
            family,
            true,
            Applicability(requiredCapabilities, requiresCharacterState: true),
            stateInput,
            associatedConceptKey,
            associatedRuleConceptId,
            []);

    private static CharacterMechanicApplicabilityDefinition Applicability(
        IReadOnlyList<string>? requiredCapabilities = null,
        bool requiresCharacterState = false)
    {
        var capabilities = requiredCapabilities ?? [];
        return new CharacterMechanicApplicabilityDefinition(
            capabilities.Count == 0
                ? CharacterMechanicApplicabilityKinds.Always
                : CharacterMechanicApplicabilityKinds.CharacterCapability,
            requiresCharacterState,
            capabilities);
    }

    private static CharacterMechanicInputDefinition Input(
        string key,
        string valueKind,
        string origin,
        bool required,
        bool participates = false) =>
        new(key, valueKind, origin, required, participates);

    private static CharacterRecoveryEffectDefinition Effect(
        string key,
        string targetKind,
        string targetKey,
        string operation,
        int? amount = null,
        string? amountInputKey = null) =>
        new(
            key,
            targetKind,
            targetKey,
            TargetChoiceKey: null,
            operation,
            amount,
            amountInputKey,
            AmountRollKey: null,
            Value: null,
            ValueChoiceKey: null,
            ReferenceKey: null,
            Conditions: []);

    private static CharacterMechanicSourceAttributionView Attribution(
        string edition) =>
        new(
            "fixture-package",
            "Fixture Package",
            "fixture",
            "FIX",
            1,
            $"fixture-{edition}",
            $"Fixture {edition}",
            edition,
            "test",
            new DateOnly(2026, 9, 21),
            "fixture",
            "Fixture",
            null,
            false,
            false);
}
