namespace RulesCore.Domain.Rules;

public static class CharacterMechanicKinds
{
    public const string Competency = "competency";
    public const string Check = "check";
    public const string Defense = "defense";
    public const string CombatValue = "combat-value";
    public const string Resource = "resource";
}

public static class CharacterMechanicEvaluationKinds
{
    public const string None = "none";
    public const string SourceValue = "source-value";
    public const string Sum = "sum";
    public const string CompositeCompetency = "composite-competency";
}

public static class CharacterMechanicInputValueKinds
{
    public const string Integer = "integer";
    public const string Boolean = "boolean";
    public const string String = "string";
}

public static class CharacterMechanicInputOrigins
{
    public const string CharacterState = "character-state";
    public const string SourceInput = "source-input";
    public const string Runtime = "runtime";
    public const string Derived = "derived";
}

public static class CharacterMechanicApplicabilityKinds
{
    public const string Always = "always";
    public const string RulesetEdition = "ruleset-edition";
    public const string AccessibleSource = "accessible-source";
}

public static class CharacterMechanicRollModes
{
    public const string Disadvantage = "disadvantage";
}

public static class CharacterMechanicRelationshipKinds
{
    public const string CompositeCheck = "composite-check";
}

public static class CharacterMechanicCompositionKinds
{
    public const string Sum = "sum";
}

public sealed record CharacterMechanicInputDefinition(
    string Key,
    string ValueKind,
    string Origin,
    bool Required,
    bool ParticipatesInValue = false,
    int? DefaultInteger = null);

public sealed record CharacterMechanicApplicabilityDefinition(
    string Kind,
    bool RequiresCharacterState,
    IReadOnlyList<string> EditionKeys,
    string? SourcePackageKey = null);

public sealed record CharacterMechanicSourceReference(
    string PackageKey,
    string PackageDisplayName,
    string WorkKey,
    string WorkDisplayName,
    string Provider,
    string? GameEdition,
    string? ReleaseKind,
    DateOnly? PublicationDate,
    string ReferenceUri,
    bool PresentationRequired,
    bool ReferenceLinkRequired);

public sealed record CharacterMechanicConditionalRollRuleDefinition(
    string Key,
    string BooleanInputKey,
    bool WhenValue,
    string RollMode,
    IReadOnlyList<string> TargetMechanicKeys);

public sealed record CharacterMechanicBooleanRequirementDefinition(
    string InputKey,
    bool ExpectedValue);

public sealed record CharacterMechanicDefinition(
    string Key,
    string Kind,
    string DisplayName,
    string EvaluationKind,
    int Constant,
    IReadOnlyList<CharacterMechanicInputDefinition> Inputs,
    string? TargetInputKey,
    string? BaseMechanicKey,
    CharacterMechanicApplicabilityDefinition Applicability,
    IReadOnlyList<CharacterMechanicConditionalRollRuleDefinition> ConditionalRollRules,
    IReadOnlyList<CharacterMechanicBooleanRequirementDefinition> BooleanRequirements,
    CharacterMechanicSourceReference? Source = null);

public sealed record CharacterMechanicRelationshipDefinition(
    string Key,
    string Kind,
    string ParentMechanicKey,
    IReadOnlyList<string> ComponentMechanicKeys,
    string Composition,
    string Direction);

public sealed record AppliedCharacterMechanicRollRule(
    string Key,
    string RollMode,
    IReadOnlyList<string> TargetMechanicKeys);

public sealed record CharacterMechanicEvaluation(
    int Value,
    int? Target,
    bool? MeetsTarget,
    bool RequirementsSatisfied,
    IReadOnlyList<string> UnsatisfiedRequirementKeys,
    IReadOnlyList<AppliedCharacterMechanicRollRule> AppliedRollRules);

public static class CharacterMechanicEvaluator
{
    public static CharacterMechanicEvaluation Evaluate(
        CharacterMechanicDefinition definition,
        IReadOnlyDictionary<string, int> integerInputs,
        IReadOnlyDictionary<string, bool> booleanInputs,
        IReadOnlyDictionary<string, string> stringInputs)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(integerInputs);
        ArgumentNullException.ThrowIfNull(booleanInputs);
        ArgumentNullException.ThrowIfNull(stringInputs);

        if (definition.EvaluationKind != CharacterMechanicEvaluationKinds.Sum
            && definition.EvaluationKind != CharacterMechanicEvaluationKinds.SourceValue)
        {
            throw new InvalidOperationException(
                $"Mechanic '{definition.Key}' does not use a directly evaluatable scalar definition.");
        }

        ValidateRequiredInputs(definition, integerInputs, booleanInputs, stringInputs);

        long total = definition.Constant;
        if (definition.EvaluationKind == CharacterMechanicEvaluationKinds.SourceValue)
        {
            if (!integerInputs.TryGetValue("value", out var value))
            {
                throw new KeyNotFoundException($"Mechanic '{definition.Key}' requires integer input 'value'.");
            }
            total = value;
        }
        else
        {
            foreach (var input in definition.Inputs.Where(value => value.ParticipatesInValue))
            {
                if (integerInputs.TryGetValue(input.Key, out var supplied))
                {
                    total += supplied;
                    continue;
                }

                if (input.DefaultInteger is int fallback)
                {
                    total += fallback;
                    continue;
                }

                if (input.Required)
                {
                    throw new KeyNotFoundException(
                        $"Mechanic '{definition.Key}' requires integer input '{input.Key}'.");
                }
            }
        }

        var target = ReadOptionalInteger(definition.TargetInputKey, integerInputs);
        var unsatisfiedRequirements = definition.BooleanRequirements
            .Where(requirement =>
                !booleanInputs.TryGetValue(requirement.InputKey, out var supplied)
                || supplied != requirement.ExpectedValue)
            .Select(requirement => requirement.InputKey)
            .ToArray();
        var appliedRollRules = definition.ConditionalRollRules
            .Where(rule =>
                booleanInputs.TryGetValue(rule.BooleanInputKey, out var supplied)
                && supplied == rule.WhenValue)
            .Select(rule => new AppliedCharacterMechanicRollRule(
                rule.Key,
                rule.RollMode,
                rule.TargetMechanicKeys))
            .ToArray();

        var final = checked((int)total);
        return new CharacterMechanicEvaluation(
            final,
            target,
            target.HasValue ? final >= target.Value : null,
            unsatisfiedRequirements.Length == 0,
            unsatisfiedRequirements,
            appliedRollRules);
    }

    private static void ValidateRequiredInputs(
        CharacterMechanicDefinition definition,
        IReadOnlyDictionary<string, int> integerInputs,
        IReadOnlyDictionary<string, bool> booleanInputs,
        IReadOnlyDictionary<string, string> stringInputs)
    {
        foreach (var input in definition.Inputs.Where(value => value.Required))
        {
            var supplied = input.ValueKind switch
            {
                CharacterMechanicInputValueKinds.Integer => integerInputs.ContainsKey(input.Key)
                    || input.DefaultInteger.HasValue,
                CharacterMechanicInputValueKinds.Boolean => booleanInputs.ContainsKey(input.Key),
                CharacterMechanicInputValueKinds.String => stringInputs.TryGetValue(input.Key, out var value)
                    && !string.IsNullOrWhiteSpace(value),
                _ => throw new InvalidOperationException(
                    $"Mechanic '{definition.Key}' uses unknown input value kind '{input.ValueKind}'.")
            };
            if (!supplied)
            {
                throw new KeyNotFoundException(
                    $"Mechanic '{definition.Key}' requires {input.ValueKind} input '{input.Key}'.");
            }
        }
    }

    private static int? ReadOptionalInteger(
        string? key,
        IReadOnlyDictionary<string, int> integerInputs) =>
        string.IsNullOrWhiteSpace(key) || !integerInputs.TryGetValue(key, out var value)
            ? null
            : value;
}

public static class KnownCharacterMechanics
{
    public const string LootTavernPackageKey = "loot-tavern-free";
    public const string LootTavernReferenceKey = "loot-tavern.harvesting-crafting-lite";

    private static readonly CharacterMechanicApplicabilityDefinition Always =
        new(CharacterMechanicApplicabilityKinds.Always, true, []);

    private static readonly CharacterMechanicApplicabilityDefinition ThreeX =
        new(CharacterMechanicApplicabilityKinds.RulesetEdition, true, ["3e", "3.5e"]);

    private static readonly CharacterMechanicApplicabilityDefinition LootTavern =
        new(
            CharacterMechanicApplicabilityKinds.AccessibleSource,
            true,
            [],
            LootTavernPackageKey);

    private static readonly CharacterMechanicSourceReference LootTavernHarvestingCrafting =
        new(
            LootTavernPackageKey,
            "Loot Tavern Free Releases",
            LootTavernReferenceKey,
            "Harvesting & Crafting Lite",
            "Loot Tavern",
            "5e",
            null,
            new DateOnly(2024, 7, 3),
            "https://www.patreon.com/LootTavern/posts/helianas-and-to-107406117",
            true,
            true);

    private static readonly IReadOnlyList<CharacterMechanicDefinition> Definitions =
    [
        new(
            "check.competency",
            CharacterMechanicKinds.Check,
            "Competency Check",
            CharacterMechanicEvaluationKinds.Sum,
            0,
            [
                StringInput("abilityKey", CharacterMechanicInputOrigins.Runtime, required: true),
                StringInput("competencyKey", CharacterMechanicInputOrigins.Runtime, required: true),
                IntegerInput("d20Roll", CharacterMechanicInputOrigins.Runtime, required: true, participates: true),
                IntegerInput("abilityModifier", CharacterMechanicInputOrigins.Derived, required: true, participates: true),
                IntegerInput("competencyModifier", CharacterMechanicInputOrigins.Derived, required: true, participates: true),
                IntegerInput("otherModifier", CharacterMechanicInputOrigins.Derived, required: false, participates: true, defaultValue: 0),
                IntegerInput("targetDc", CharacterMechanicInputOrigins.SourceInput, required: false)
            ],
            "targetDc",
            null,
            Always,
            [],
            []),

        SumMechanic(
            "save.fortitude",
            CharacterMechanicKinds.Defense,
            "Fortitude Save",
            ThreeX,
            [
                IntegerInput("baseSave", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("constitutionModifier", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("otherModifier", CharacterMechanicInputOrigins.Derived, false, true, 0)
            ]),
        SumMechanic(
            "save.reflex",
            CharacterMechanicKinds.Defense,
            "Reflex Save",
            ThreeX,
            [
                IntegerInput("baseSave", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("dexterityModifier", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("otherModifier", CharacterMechanicInputOrigins.Derived, false, true, 0)
            ]),
        SumMechanic(
            "save.will",
            CharacterMechanicKinds.Defense,
            "Will Save",
            ThreeX,
            [
                IntegerInput("baseSave", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("wisdomModifier", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("otherModifier", CharacterMechanicInputOrigins.Derived, false, true, 0)
            ]),
        SumMechanic(
            "defense.ac.touch",
            CharacterMechanicKinds.Defense,
            "Touch Armor Class",
            ThreeX,
            [
                IntegerInput("dexterityContribution", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("sizeModifier", CharacterMechanicInputOrigins.Derived, false, true, 0),
                IntegerInput("deflectionBonus", CharacterMechanicInputOrigins.Derived, false, true, 0),
                IntegerInput("dodgeContribution", CharacterMechanicInputOrigins.Derived, false, true, 0),
                IntegerInput("otherApplicableModifier", CharacterMechanicInputOrigins.Derived, false, true, 0)
            ],
            constant: 10),
        SumMechanic(
            "defense.ac.flat-footed",
            CharacterMechanicKinds.Defense,
            "Flat-Footed Armor Class",
            ThreeX,
            [
                IntegerInput("armorBonus", CharacterMechanicInputOrigins.Derived, false, true, 0),
                IntegerInput("shieldBonus", CharacterMechanicInputOrigins.Derived, false, true, 0),
                IntegerInput("flatFootedDexterityContribution", CharacterMechanicInputOrigins.Derived, false, true, 0),
                IntegerInput("sizeModifier", CharacterMechanicInputOrigins.Derived, false, true, 0),
                IntegerInput("naturalArmorBonus", CharacterMechanicInputOrigins.Derived, false, true, 0),
                IntegerInput("deflectionBonus", CharacterMechanicInputOrigins.Derived, false, true, 0),
                IntegerInput("flatFootedDodgeContribution", CharacterMechanicInputOrigins.Derived, false, true, 0),
                IntegerInput("otherApplicableModifier", CharacterMechanicInputOrigins.Derived, false, true, 0)
            ],
            constant: 10),
        SourceValueMechanic("combat.base-attack-bonus", CharacterMechanicKinds.CombatValue, "Base Attack Bonus", ThreeX),
        SumMechanic(
            "combat.grapple",
            CharacterMechanicKinds.CombatValue,
            "Grapple Modifier",
            ThreeX,
            [
                IntegerInput("baseAttackBonus", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("strengthModifier", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("grappleSizeModifier", CharacterMechanicInputOrigins.Derived, false, true, 0),
                IntegerInput("otherModifier", CharacterMechanicInputOrigins.Derived, false, true, 0)
            ]),
        new(
            "competency.skill-ranks",
            CharacterMechanicKinds.Competency,
            "Skill Ranks",
            CharacterMechanicEvaluationKinds.SourceValue,
            0,
            [
                StringInput("competencyKey", CharacterMechanicInputOrigins.SourceInput, true),
                IntegerInput("value", CharacterMechanicInputOrigins.CharacterState, true)
            ],
            null,
            null,
            ThreeX,
            [],
            []),
        SourceValueMechanic("resource.nonlethal-damage", CharacterMechanicKinds.Resource, "Nonlethal Damage", ThreeX),
        SourceValueMechanic("defense.spell-resistance", CharacterMechanicKinds.Defense, "Spell Resistance", ThreeX),
        new(
            "defense.damage-reduction",
            CharacterMechanicKinds.Defense,
            "Damage Reduction",
            CharacterMechanicEvaluationKinds.None,
            0,
            [StringInput("value", CharacterMechanicInputOrigins.CharacterState, true)],
            null,
            null,
            ThreeX,
            [],
            []),

        LootCheck(
            "check.harvesting.assessment",
            "Harvesting Assessment Check",
            [
                StringInput("creatureTypeCompetencyKey", CharacterMechanicInputOrigins.SourceInput, true),
                IntegerInput("d20Roll", CharacterMechanicInputOrigins.Runtime, true, true),
                IntegerInput("intelligenceModifier", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("competencyModifier", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("otherModifier", CharacterMechanicInputOrigins.Derived, false, true, 0)
            ]),
        LootCheck(
            "check.harvesting.carving",
            "Harvesting Carving Check",
            [
                StringInput("creatureTypeCompetencyKey", CharacterMechanicInputOrigins.SourceInput, true),
                StringInput("carvingAbilitySource", CharacterMechanicInputOrigins.SourceInput, true),
                IntegerInput("d20Roll", CharacterMechanicInputOrigins.Runtime, true, true),
                IntegerInput("carvingAbilityModifier", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("competencyModifier", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("otherModifier", CharacterMechanicInputOrigins.Derived, false, true, 0)
            ]),
        new(
            "check.harvesting.total",
            CharacterMechanicKinds.Check,
            "Harvesting Check",
            CharacterMechanicEvaluationKinds.Sum,
            0,
            [
                IntegerInput("assessmentResult", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("carvingResult", CharacterMechanicInputOrigins.Derived, true, true),
                BooleanInput("sameActor", CharacterMechanicInputOrigins.Runtime, true),
                IntegerInput("targetDc", CharacterMechanicInputOrigins.SourceInput, false)
            ],
            "targetDc",
            null,
            LootTavern,
            [
                new CharacterMechanicConditionalRollRuleDefinition(
                    "harvesting.same-actor-disadvantage",
                    "sameActor",
                    true,
                    CharacterMechanicRollModes.Disadvantage,
                    ["check.harvesting.assessment", "check.harvesting.carving"])
            ],
            [],
            LootTavernHarvestingCrafting),
        new(
            "check.crafting.manufacturing",
            CharacterMechanicKinds.Check,
            "Manufacturing Check",
            CharacterMechanicEvaluationKinds.Sum,
            0,
            [
                StringInput("toolKey", CharacterMechanicInputOrigins.SourceInput, true),
                StringInput("abilityKey", CharacterMechanicInputOrigins.SourceInput, true),
                IntegerInput("d20Roll", CharacterMechanicInputOrigins.Runtime, true, true),
                IntegerInput("abilityModifier", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("proficiencyModifier", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("otherModifier", CharacterMechanicInputOrigins.Derived, false, true, 0),
                BooleanInput("hasToolProficiency", CharacterMechanicInputOrigins.CharacterState, true),
                IntegerInput("targetDc", CharacterMechanicInputOrigins.SourceInput, false)
            ],
            "targetDc",
            "check.competency",
            LootTavern,
            [
                new CharacterMechanicConditionalRollRuleDefinition(
                    "manufacturing.missing-tool-proficiency-disadvantage",
                    "hasToolProficiency",
                    false,
                    CharacterMechanicRollModes.Disadvantage,
                    ["check.crafting.manufacturing"])
            ],
            [],
            LootTavernHarvestingCrafting),
        new(
            "check.crafting.enchanting",
            CharacterMechanicKinds.Check,
            "Enchanting Check",
            CharacterMechanicEvaluationKinds.Sum,
            0,
            [
                StringInput("creatureTypeCompetencyKey", CharacterMechanicInputOrigins.SourceInput, true),
                IntegerInput("d20Roll", CharacterMechanicInputOrigins.Runtime, true, true),
                IntegerInput("spellcastingAbilityModifier", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("competencyModifier", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("otherModifier", CharacterMechanicInputOrigins.Derived, false, true, 0),
                BooleanInput("hasSpellcastingAbility", CharacterMechanicInputOrigins.CharacterState, true),
                IntegerInput("targetDc", CharacterMechanicInputOrigins.SourceInput, false)
            ],
            "targetDc",
            "check.competency",
            LootTavern,
            [],
            [new CharacterMechanicBooleanRequirementDefinition("hasSpellcastingAbility", true)],
            LootTavernHarvestingCrafting)
    ];

    private static readonly IReadOnlyList<CharacterMechanicRelationshipDefinition> RelationshipDefinitions =
    [
        new(
            "check-composite.harvesting",
            CharacterMechanicRelationshipKinds.CompositeCheck,
            "check.harvesting.total",
            ["check.harvesting.assessment", "check.harvesting.carving"],
            CharacterMechanicCompositionKinds.Sum,
            MechanicalRelationshipDirections.ComponentsToParent)
    ];

    public static IReadOnlyList<CharacterMechanicDefinition> All => Definitions;
    public static IReadOnlyList<CharacterMechanicRelationshipDefinition> Relationships => RelationshipDefinitions;

    public static CharacterMechanicDefinition? FindByKey(string key) =>
        Definitions.SingleOrDefault(value => string.Equals(value.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static CharacterMechanicDefinition LootCheck(
        string key,
        string displayName,
        IReadOnlyList<CharacterMechanicInputDefinition> inputs) =>
        new(
            key,
            CharacterMechanicKinds.Check,
            displayName,
            CharacterMechanicEvaluationKinds.Sum,
            0,
            inputs,
            null,
            "check.competency",
            LootTavern,
            [],
            [],
            LootTavernHarvestingCrafting);

    private static CharacterMechanicDefinition SumMechanic(
        string key,
        string kind,
        string displayName,
        CharacterMechanicApplicabilityDefinition applicability,
        IReadOnlyList<CharacterMechanicInputDefinition> inputs,
        int constant = 0) =>
        new(
            key,
            kind,
            displayName,
            CharacterMechanicEvaluationKinds.Sum,
            constant,
            inputs,
            null,
            null,
            applicability,
            [],
            []);

    private static CharacterMechanicDefinition SourceValueMechanic(
        string key,
        string kind,
        string displayName,
        CharacterMechanicApplicabilityDefinition applicability) =>
        new(
            key,
            kind,
            displayName,
            CharacterMechanicEvaluationKinds.SourceValue,
            0,
            [IntegerInput("value", CharacterMechanicInputOrigins.CharacterState, true)],
            null,
            null,
            applicability,
            [],
            []);

    private static CharacterMechanicInputDefinition IntegerInput(
        string key,
        string origin,
        bool required,
        bool participates = false,
        int? defaultValue = null) =>
        new(
            key,
            CharacterMechanicInputValueKinds.Integer,
            origin,
            required,
            participates,
            defaultValue);

    private static CharacterMechanicInputDefinition BooleanInput(
        string key,
        string origin,
        bool required) =>
        new(key, CharacterMechanicInputValueKinds.Boolean, origin, required);

    private static CharacterMechanicInputDefinition StringInput(
        string key,
        string origin,
        bool required) =>
        new(key, CharacterMechanicInputValueKinds.String, origin, required);
}
