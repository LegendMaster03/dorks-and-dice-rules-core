namespace RulesCore.Domain.Rules;

public static class CharacterMechanicKinds
{
    public const string Competency = "competency";
    public const string Check = "check";
    public const string SavingThrow = "saving-throw";
    public const string Defense = "defense";
    public const string CombatValue = "combat-value";
    public const string Resource = "resource";
}

public static class CharacterMechanicEvaluationKinds
{
    public const string None = "none";
    public const string SourceValue = "source-value";
    public const string Sum = "sum";
    public const string CompetencyProfile = "competency-profile";
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
    public const string CharacterCapability = "character-capability";
    public const string ExternalPublicRules = "external-public-rules";
    public const string AccessibleSource = "accessible-source";
}

public static class CharacterCheckAbilityResolutionKinds
{
    public const string Fixed = "fixed";
    public const string CallerSelected = "caller-selected";
    public const string RuleResolved = "rule-resolved";
    public const string CharacterResolved = "character-resolved";
    public const string None = "none";
}

public static class CharacterCheckCompetencyResolutionKinds
{
    public const string Fixed = "fixed";
    public const string CallerSelected = "caller-selected";
    public const string RuleResolved = "rule-resolved";
    public const string CharacterResolved = "character-resolved";
    public const string None = "none";
}

public static class CharacterCompetencyKinds
{
    public const string Skill = "skill";
    public const string SpecializedSkill = "specialized-skill";
    public const string Tool = "tool";
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
    int? DefaultInteger = null,
    string? IncludeWhenBooleanInputKey = null,
    bool? IncludeWhenBooleanValue = null);

public sealed record CharacterMechanicApplicabilityDefinition(
    string Kind,
    bool RequiresCharacterState,
    IReadOnlyList<string> RequiredCapabilityKeys,
    string? SourcePackageKey = null);

public sealed record CharacterCheckAbilityDefinition(
    string ResolutionKind,
    string? FixedAbilityKey = null,
    IReadOnlyList<string>? AllowedAbilityKeys = null);

public sealed record CharacterCheckCompetencyDefinition(
    string ResolutionKind,
    IReadOnlyList<string> AllowedCompetencyKinds,
    string? FixedConceptKey = null);

public sealed record CharacterCheckCompetencyCompositionDefinition(
    string ConceptKeyInputKey,
    string ContributionInputKey);

public sealed record CharacterMechanicCheckDefinition(
    CharacterCheckAbilityDefinition Ability,
    CharacterCheckCompetencyDefinition Competency,
    CharacterCheckCompetencyCompositionDefinition? CompetencyComposition = null);

public sealed record CharacterMechanicSourceReference(
    string? PackageKey,
    string? PackageDisplayName,
    string WorkKey,
    string WorkDisplayName,
    string Provider,
    string? GameEdition,
    string? ReleaseKind,
    DateOnly? PublicationDate,
    string ReferenceUri,
    bool PresentationRequired,
    bool ReferenceLinkRequired);

public sealed record CharacterMechanicBooleanConditionDefinition(
    string InputKey,
    bool ExpectedValue);

public sealed record CharacterMechanicConditionalRollRuleDefinition(
    string Key,
    IReadOnlyList<CharacterMechanicBooleanConditionDefinition> Conditions,
    string RollMode,
    IReadOnlyList<string> TargetMechanicKeys);

public sealed record CharacterMechanicBooleanRequirementDefinition(
    string InputKey,
    bool ExpectedValue);
public static class CharacterMechanicContributorRoundingKinds
{
    public const string Floor = "floor";
}

public sealed record CharacterMechanicContributorValueDefinition(
    string AmountIntegerInputKey,
    string FullAmountBooleanInputKey,
    bool FullAmountWhenBooleanValue,
    int AlternateNumerator,
    int AlternateDenominator,
    string AlternateRoundingKind,
    bool RequireNonNegativeAmount = true);

public sealed record CharacterMechanicContributorGroupDefinition(
    string Key,
    string? MaximumCountStringInputKey,
    IReadOnlyDictionary<string, int> MaximumCountByStringValue,
    IReadOnlyList<CharacterMechanicInputDefinition> Inputs,
    CharacterMechanicContributorValueDefinition Value,
    IReadOnlyList<CharacterMechanicBooleanRequirementDefinition> BooleanRequirements,
    bool StandardHelpActionApplies);

public sealed record CharacterMechanicContributorInputValues(
    IReadOnlyDictionary<string, int> IntegerInputs,
    IReadOnlyDictionary<string, bool> BooleanInputs,
    IReadOnlyDictionary<string, string> StringInputs);

public sealed record CharacterMechanicContributorGroupEvaluation(
    string Key,
    int ContributorCount,
    int? MaximumContributorCount,
    int Value);


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
    CharacterMechanicSourceReference? Source = null,
    CharacterMechanicCheckDefinition? Check = null,
    IReadOnlyList<CharacterMechanicContributorGroupDefinition>? ContributorGroups = null);

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
    IReadOnlyList<AppliedCharacterMechanicRollRule> AppliedRollRules,
    IReadOnlyList<CharacterMechanicContributorGroupEvaluation> ContributorGroups);

public static class CharacterMechanicEvaluator
{
    public static CharacterMechanicEvaluation Evaluate(
        CharacterMechanicDefinition definition,
        IReadOnlyDictionary<string, int> integerInputs,
        IReadOnlyDictionary<string, bool> booleanInputs,
        IReadOnlyDictionary<string, string> stringInputs,
        IReadOnlyDictionary<string, IReadOnlyList<CharacterMechanicContributorInputValues>>? contributorGroups = null)
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
            foreach (var input in definition.Inputs.Where(value =>
                         value.ParticipatesInValue
                         && IsInputActive(value, booleanInputs)))
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

        var evaluatedContributorGroups = EvaluateContributorGroups(
            definition,
            contributorGroups ?? new Dictionary<string, IReadOnlyList<CharacterMechanicContributorInputValues>>(
                StringComparer.OrdinalIgnoreCase),
            stringInputs);
        foreach (var contributorGroup in evaluatedContributorGroups)
        {
            total += contributorGroup.Value;
        }

        var target = ReadOptionalInteger(definition.TargetInputKey, integerInputs);
        var unsatisfiedRequirements = definition.BooleanRequirements
            .Where(requirement =>
                !booleanInputs.TryGetValue(requirement.InputKey, out var supplied)
                || supplied != requirement.ExpectedValue)
            .Select(requirement => requirement.InputKey)
            .ToArray();
        var appliedRollRules = definition.ConditionalRollRules
            .Where(rule => rule.Conditions.All(condition =>
                booleanInputs.TryGetValue(condition.InputKey, out var supplied)
                && supplied == condition.ExpectedValue))
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
            appliedRollRules,
            evaluatedContributorGroups);
    }

    private static IReadOnlyList<CharacterMechanicContributorGroupEvaluation> EvaluateContributorGroups(
        CharacterMechanicDefinition definition,
        IReadOnlyDictionary<string, IReadOnlyList<CharacterMechanicContributorInputValues>> suppliedGroups,
        IReadOnlyDictionary<string, string> mechanicStringInputs)
    {
        var definitions = definition.ContributorGroups ?? [];
        var knownKeys = definitions
            .Select(value => value.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unexpected = suppliedGroups.Keys
            .Where(value => !knownKeys.Contains(value))
            .ToArray();
        if (unexpected.Length > 0)
        {
            throw new ArgumentException(
                $"Mechanic '{definition.Key}' does not define contributor group(s): {string.Join(", ", unexpected)}.");
        }

        var result = new List<CharacterMechanicContributorGroupEvaluation>();
        foreach (var group in definitions)
        {
            var contributors = suppliedGroups.TryGetValue(group.Key, out var supplied)
                ? supplied
                : [];
            var maximum = ResolveContributorMaximum(
                definition.Key,
                group,
                contributors.Count,
                mechanicStringInputs);
            if (maximum.HasValue && contributors.Count > maximum.Value)
            {
                throw new InvalidOperationException(
                    $"Mechanic '{definition.Key}' allows at most {maximum.Value} contributor(s) in group '{group.Key}' for the supplied context.");
            }

            long groupTotal = 0;
            foreach (var contributor in contributors)
            {
                ValidateContributorInputs(definition.Key, group, contributor);
                ValidateContributorEligibility(definition.Key, group, contributor);

                if (!contributor.IntegerInputs.TryGetValue(
                        group.Value.AmountIntegerInputKey,
                        out var amount))
                {
                    throw new KeyNotFoundException(
                        $"Contributor group '{group.Key}' requires integer input '{group.Value.AmountIntegerInputKey}'.");
                }
                if (group.Value.RequireNonNegativeAmount && amount < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        group.Value.AmountIntegerInputKey,
                        "Contributor amount can not be negative.");
                }
                if (!contributor.BooleanInputs.TryGetValue(
                        group.Value.FullAmountBooleanInputKey,
                        out var condition))
                {
                    throw new KeyNotFoundException(
                        $"Contributor group '{group.Key}' requires boolean input '{group.Value.FullAmountBooleanInputKey}'.");
                }

                if (condition == group.Value.FullAmountWhenBooleanValue)
                {
                    groupTotal += amount;
                    continue;
                }

                if (group.Value.AlternateDenominator <= 0
                    || group.Value.AlternateNumerator < 0)
                {
                    throw new InvalidOperationException(
                        $"Contributor group '{group.Key}' has an invalid alternate contribution fraction.");
                }
                if (!string.Equals(
                        group.Value.AlternateRoundingKind,
                        CharacterMechanicContributorRoundingKinds.Floor,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Contributor group '{group.Key}' uses unsupported rounding '{group.Value.AlternateRoundingKind}'.");
                }

                var scaled = checked((long)amount * group.Value.AlternateNumerator);
                groupTotal += scaled / group.Value.AlternateDenominator;
            }

            result.Add(new CharacterMechanicContributorGroupEvaluation(
                group.Key,
                contributors.Count,
                maximum,
                checked((int)groupTotal)));
        }

        return result;
    }

    private static int? ResolveContributorMaximum(
        string mechanicKey,
        CharacterMechanicContributorGroupDefinition group,
        int contributorCount,
        IReadOnlyDictionary<string, string> mechanicStringInputs)
    {
        if (string.IsNullOrWhiteSpace(group.MaximumCountStringInputKey))
        {
            return null;
        }

        if (!mechanicStringInputs.TryGetValue(group.MaximumCountStringInputKey, out var context)
            || string.IsNullOrWhiteSpace(context))
        {
            if (contributorCount == 0)
            {
                return null;
            }

            throw new KeyNotFoundException(
                $"Mechanic '{mechanicKey}' requires string input '{group.MaximumCountStringInputKey}' when contributor group '{group.Key}' is supplied.");
        }

        var match = group.MaximumCountByStringValue
            .FirstOrDefault(value => string.Equals(
                value.Key,
                context.Trim(),
                StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(match.Key))
        {
            throw new ArgumentException(
                $"Mechanic '{mechanicKey}' does not define a contributor limit for '{context}' in input '{group.MaximumCountStringInputKey}'.");
        }

        return match.Value;
    }

    private static void ValidateContributorEligibility(
        string mechanicKey,
        CharacterMechanicContributorGroupDefinition group,
        CharacterMechanicContributorInputValues contributor)
    {
        var unsatisfied = group.BooleanRequirements
            .Where(requirement =>
                !contributor.BooleanInputs.TryGetValue(requirement.InputKey, out var supplied)
                || supplied != requirement.ExpectedValue)
            .Select(requirement => requirement.InputKey)
            .ToArray();
        if (unsatisfied.Length > 0)
        {
            throw new InvalidOperationException(
                $"Mechanic '{mechanicKey}' contributor group '{group.Key}' contains an ineligible contributor. Unsatisfied requirement(s): {string.Join(", ", unsatisfied)}.");
        }
    }

    private static void ValidateContributorInputs(
        string mechanicKey,
        CharacterMechanicContributorGroupDefinition group,
        CharacterMechanicContributorInputValues contributor)
    {
        foreach (var input in group.Inputs.Where(value => value.Required))
        {
            var supplied = input.ValueKind switch
            {
                CharacterMechanicInputValueKinds.Integer =>
                    contributor.IntegerInputs.ContainsKey(input.Key),
                CharacterMechanicInputValueKinds.Boolean =>
                    contributor.BooleanInputs.ContainsKey(input.Key),
                CharacterMechanicInputValueKinds.String =>
                    contributor.StringInputs.TryGetValue(input.Key, out var value)
                    && !string.IsNullOrWhiteSpace(value),
                _ => throw new InvalidOperationException(
                    $"Mechanic '{mechanicKey}' contributor group '{group.Key}' uses unknown input value kind '{input.ValueKind}'.")
            };
            if (!supplied)
            {
                throw new KeyNotFoundException(
                    $"Mechanic '{mechanicKey}' contributor group '{group.Key}' requires {input.ValueKind} input '{input.Key}'.");
            }
        }
    }

    private static void ValidateRequiredInputs(
        CharacterMechanicDefinition definition,
        IReadOnlyDictionary<string, int> integerInputs,
        IReadOnlyDictionary<string, bool> booleanInputs,
        IReadOnlyDictionary<string, string> stringInputs)
    {
        foreach (var input in definition.Inputs.Where(value =>
                     value.Required
                     && IsInputActive(value, booleanInputs)))
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

    private static bool IsInputActive(
        CharacterMechanicInputDefinition input,
        IReadOnlyDictionary<string, bool> booleanInputs)
    {
        if (string.IsNullOrWhiteSpace(input.IncludeWhenBooleanInputKey))
        {
            return true;
        }

        if (!input.IncludeWhenBooleanValue.HasValue)
        {
            throw new InvalidOperationException(
                $"Input '{input.Key}' declares a boolean condition key without an expected value.");
        }

        return booleanInputs.TryGetValue(input.IncludeWhenBooleanInputKey, out var supplied)
            && supplied == input.IncludeWhenBooleanValue.Value;
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
    public const string LootTavernReferenceKey = "loot-tavern.harvesting-crafting-lite";

    private static readonly IReadOnlyDictionary<string, int> HarvestingHelperLimits =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tiny"] = 0,
            ["Small"] = 1,
            ["Medium"] = 2,
            ["Large"] = 4,
            ["Huge"] = 6,
            ["Gargantuan"] = 10
        };

    private static readonly CharacterMechanicApplicabilityDefinition Always =
        new(CharacterMechanicApplicabilityKinds.Always, true, []);

    private static readonly CharacterMechanicApplicabilityDefinition LootTavern =
        new(
            CharacterMechanicApplicabilityKinds.ExternalPublicRules,
            true,
            []);

    private static readonly CharacterMechanicSourceReference LootTavernHarvestingCrafting =
        new(
            null,
            null,
            LootTavernReferenceKey,
            "Harvesting & Crafting Lite",
            "Loot Tavern",
            "5e",
            "public-release",
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
                IntegerInput("competencyContribution", CharacterMechanicInputOrigins.Derived, required: true, participates: true),
                IntegerInput("otherModifier", CharacterMechanicInputOrigins.Derived, required: false, participates: true, defaultValue: 0),
                IntegerInput("targetDc", CharacterMechanicInputOrigins.SourceInput, required: false)
            ],
            "targetDc",
            null,
            Always,
            [],
            [],
            Check: new CharacterMechanicCheckDefinition(
                new CharacterCheckAbilityDefinition(
                    CharacterCheckAbilityResolutionKinds.CallerSelected),
                new CharacterCheckCompetencyDefinition(
                    CharacterCheckCompetencyResolutionKinds.CallerSelected,
                    [
                        CharacterCompetencyKinds.Skill,
                        CharacterCompetencyKinds.SpecializedSkill,
                        CharacterCompetencyKinds.Tool
                    ]),
                new CharacterCheckCompetencyCompositionDefinition(
                    "competencyKey",
                    "competencyContribution"))),

        SumMechanic(
            "save.fortitude",
            CharacterMechanicKinds.SavingThrow,
            "Fortitude Save",
            Capability("save.fortitude"),
            [
                IntegerInput("baseSave", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("constitutionModifier", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("otherModifier", CharacterMechanicInputOrigins.Derived, false, true, 0)
            ]),
        SumMechanic(
            "save.reflex",
            CharacterMechanicKinds.SavingThrow,
            "Reflex Save",
            Capability("save.reflex"),
            [
                IntegerInput("baseSave", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("dexterityModifier", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("otherModifier", CharacterMechanicInputOrigins.Derived, false, true, 0)
            ]),
        SumMechanic(
            "save.will",
            CharacterMechanicKinds.SavingThrow,
            "Will Save",
            Capability("save.will"),
            [
                IntegerInput("baseSave", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("wisdomModifier", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("otherModifier", CharacterMechanicInputOrigins.Derived, false, true, 0)
            ]),
        SumMechanic(
            "defense.ac.touch",
            CharacterMechanicKinds.Defense,
            "Touch Armor Class",
            Capability("defense.ac.touch"),
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
            Capability("defense.ac.flat-footed"),
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
        SourceValueMechanic(
            "combat.base-attack-bonus",
            CharacterMechanicKinds.CombatValue,
            "Base Attack Bonus",
            Capability("combat.base-attack-bonus")),
        SumMechanic(
            "combat.grapple",
            CharacterMechanicKinds.CombatValue,
            "Grapple Modifier",
            Capability("combat.grapple"),
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
            Capability("competency.skill-ranks"),
            [],
            []),
        SourceValueMechanic(
            "resource.nonlethal-damage",
            CharacterMechanicKinds.Resource,
            "Nonlethal Damage",
            Capability("resource.nonlethal-damage")),
        SourceValueMechanic(
            "defense.spell-resistance",
            CharacterMechanicKinds.Defense,
            "Spell Resistance",
            Capability("defense.spell-resistance")),
        new(
            "defense.damage-reduction",
            CharacterMechanicKinds.Defense,
            "Damage Reduction",
            CharacterMechanicEvaluationKinds.None,
            0,
            [StringInput("value", CharacterMechanicInputOrigins.CharacterState, true)],
            null,
            null,
            Capability("defense.damage-reduction"),
            [],
            []),

        LootCheck(
            "check.harvesting.assessment",
            "Harvesting Assessment Check",
            [
                StringInput("creatureTypeCompetencyKey", CharacterMechanicInputOrigins.SourceInput, true),
                IntegerInput("d20Roll", CharacterMechanicInputOrigins.Runtime, true, true),
                IntegerInput("intelligenceModifier", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("competencyContribution", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("otherModifier", CharacterMechanicInputOrigins.Derived, false, true, 0)
            ],
            new CharacterMechanicCheckDefinition(
                new CharacterCheckAbilityDefinition(
                    CharacterCheckAbilityResolutionKinds.Fixed,
                    "intelligence"),
                new CharacterCheckCompetencyDefinition(
                    CharacterCheckCompetencyResolutionKinds.RuleResolved,
                    [CharacterCompetencyKinds.Skill, CharacterCompetencyKinds.SpecializedSkill]),
                new CharacterCheckCompetencyCompositionDefinition(
                    "creatureTypeCompetencyKey",
                    "competencyContribution"))),
        LootCheck(
            "check.harvesting.carving",
            "Harvesting Carving Check",
            [
                StringInput("creatureTypeCompetencyKey", CharacterMechanicInputOrigins.SourceInput, true),
                IntegerInput("d20Roll", CharacterMechanicInputOrigins.Runtime, true, true),
                IntegerInput("dexterityModifier", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("competencyContribution", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("otherModifier", CharacterMechanicInputOrigins.Derived, false, true, 0)
            ],
            new CharacterMechanicCheckDefinition(
                new CharacterCheckAbilityDefinition(
                    CharacterCheckAbilityResolutionKinds.Fixed,
                    "dexterity"),
                new CharacterCheckCompetencyDefinition(
                    CharacterCheckCompetencyResolutionKinds.RuleResolved,
                    [CharacterCompetencyKinds.Skill, CharacterCompetencyKinds.SpecializedSkill]),
                new CharacterCheckCompetencyCompositionDefinition(
                    "creatureTypeCompetencyKey",
                    "competencyContribution"))),
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
                StringInput("creatureSize", CharacterMechanicInputOrigins.SourceInput, false),
                IntegerInput("targetDc", CharacterMechanicInputOrigins.SourceInput, false)
            ],
            "targetDc",
            null,
            LootTavern,
            [
                new CharacterMechanicConditionalRollRuleDefinition(
                    "harvesting.same-actor-disadvantage",
                    [new CharacterMechanicBooleanConditionDefinition("sameActor", true)],
                    CharacterMechanicRollModes.Disadvantage,
                    ["check.harvesting.assessment", "check.harvesting.carving"])
            ],
            [],
            LootTavernHarvestingCrafting,
            ContributorGroups:
            [
                new CharacterMechanicContributorGroupDefinition(
                    "helpers",
                    "creatureSize",
                    HarvestingHelperLimits,
                    [
                        IntegerInput(
                            "proficiencyBonus",
                            CharacterMechanicInputOrigins.CharacterState,
                            true),
                        BooleanInput(
                            "isProficient",
                            CharacterMechanicInputOrigins.CharacterState,
                            true),
                        BooleanInput(
                            "participatedForEntireDuration",
                            CharacterMechanicInputOrigins.Runtime,
                            true),
                        BooleanInput(
                            "isAssessmentParticipant",
                            CharacterMechanicInputOrigins.Runtime,
                            true),
                        BooleanInput(
                            "isCarvingParticipant",
                            CharacterMechanicInputOrigins.Runtime,
                            true)
                    ],
                    new CharacterMechanicContributorValueDefinition(
                        "proficiencyBonus",
                        "isProficient",
                        true,
                        AlternateNumerator: 1,
                        AlternateDenominator: 2,
                        AlternateRoundingKind: CharacterMechanicContributorRoundingKinds.Floor),
                    [
                        new CharacterMechanicBooleanRequirementDefinition(
                            "participatedForEntireDuration",
                            true),
                        new CharacterMechanicBooleanRequirementDefinition(
                            "isAssessmentParticipant",
                            false),
                        new CharacterMechanicBooleanRequirementDefinition(
                            "isCarvingParticipant",
                            false)
                    ],
                    StandardHelpActionApplies: false)
            ]),
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
                IntegerInput(
                    "toolProficiencyContribution",
                    CharacterMechanicInputOrigins.Derived,
                    true,
                    true,
                    includeWhenBooleanInputKey: "hasToolProficiency",
                    includeWhenBooleanValue: true),
                IntegerInput("otherModifier", CharacterMechanicInputOrigins.Derived, false, true, 0),
                BooleanInput("hasToolProficiency", CharacterMechanicInputOrigins.CharacterState, true),
                BooleanInput("hasQualifiedGuidance", CharacterMechanicInputOrigins.Runtime, true),
                IntegerInput("targetDc", CharacterMechanicInputOrigins.SourceInput, false)
            ],
            "targetDc",
            "check.competency",
            LootTavern,
            [
                new CharacterMechanicConditionalRollRuleDefinition(
                    "manufacturing.missing-tool-proficiency-disadvantage",
                    [
                        new CharacterMechanicBooleanConditionDefinition("hasToolProficiency", false),
                        new CharacterMechanicBooleanConditionDefinition("hasQualifiedGuidance", false)
                    ],
                    CharacterMechanicRollModes.Disadvantage,
                    ["check.crafting.manufacturing"])
            ],
            [],
            LootTavernHarvestingCrafting,
            new CharacterMechanicCheckDefinition(
                new CharacterCheckAbilityDefinition(
                    CharacterCheckAbilityResolutionKinds.RuleResolved),
                new CharacterCheckCompetencyDefinition(
                    CharacterCheckCompetencyResolutionKinds.RuleResolved,
                    [CharacterCompetencyKinds.Tool]),
                new CharacterCheckCompetencyCompositionDefinition(
                    "toolKey",
                    "toolProficiencyContribution"))),
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
                IntegerInput("competencyContribution", CharacterMechanicInputOrigins.Derived, true, true),
                IntegerInput("otherModifier", CharacterMechanicInputOrigins.Derived, false, true, 0),
                BooleanInput("hasSpellcastingAbility", CharacterMechanicInputOrigins.CharacterState, true),
                IntegerInput("targetDc", CharacterMechanicInputOrigins.SourceInput, false)
            ],
            "targetDc",
            "check.competency",
            LootTavern,
            [],
            [new CharacterMechanicBooleanRequirementDefinition("hasSpellcastingAbility", true)],
            LootTavernHarvestingCrafting,
            new CharacterMechanicCheckDefinition(
                new CharacterCheckAbilityDefinition(
                    CharacterCheckAbilityResolutionKinds.CharacterResolved),
                new CharacterCheckCompetencyDefinition(
                    CharacterCheckCompetencyResolutionKinds.RuleResolved,
                    [CharacterCompetencyKinds.Skill, CharacterCompetencyKinds.SpecializedSkill]),
                new CharacterCheckCompetencyCompositionDefinition(
                    "creatureTypeCompetencyKey",
                    "competencyContribution")))
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
        IReadOnlyList<CharacterMechanicInputDefinition> inputs,
        CharacterMechanicCheckDefinition check) =>
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
            LootTavernHarvestingCrafting,
            check);

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
        int? defaultValue = null,
        string? includeWhenBooleanInputKey = null,
        bool? includeWhenBooleanValue = null) =>
        new(
            key,
            CharacterMechanicInputValueKinds.Integer,
            origin,
            required,
            participates,
            defaultValue,
            includeWhenBooleanInputKey,
            includeWhenBooleanValue);

    private static CharacterMechanicApplicabilityDefinition Capability(string mechanicKey) =>
        new(
            CharacterMechanicApplicabilityKinds.CharacterCapability,
            true,
            [mechanicKey]);

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
