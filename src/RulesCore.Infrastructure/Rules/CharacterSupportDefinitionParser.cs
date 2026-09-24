using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Parses normalized Character-support metadata into typed recovery, passive-value,
/// qualification, applicability, choice, roll, and effect definitions.
/// </summary>
internal static class CharacterSupportDefinitionParser
{
    internal static bool TryGetCharacterSupport(
        JsonElement document,
        out JsonElement support)
    {
        support = default;
        return document.ValueKind == JsonValueKind.Object
            && document.TryGetProperty("_rulesCore", out var rulesCore)
            && rulesCore.ValueKind == JsonValueKind.Object
            && rulesCore.TryGetProperty("characterSupport", out support)
            && support.ValueKind == JsonValueKind.Object;
    }
    
    internal static void ParseRecoveryProcedures(
        JsonElement support,
        ResolvedRuleCatalogItemView rule,
        IReadOnlyList<CharacterMechanicSourceAttributionView> attributions,
        ICollection<CharacterRecoveryProcedureDefinition> output)
    {
        if (!TryReadArray(support, "recoveryProcedures", out var procedures))
        {
            return;
        }
    
        foreach (var value in procedures.EnumerateArray())
        {
            RequireObject(value, "recovery procedure");
            var key = RequireString(value, "key", "Recovery procedure");
            var displayName = RequireString(value, "displayName", $"Recovery procedure '{key}'");
            var inputs = ReadInputs(value);
            var applicability = ReadApplicability(
                value,
                rule.PackageKey,
                inputs);
            var choices = ReadChoices(value);
            var rolls = ReadRolls(value);
            var effects = ReadEffects(value);
            var runtime = ReadRuntimeRequirements(
                value,
                applicability,
                inputs,
                choices,
                rolls,
                effects);
    
            output.Add(new CharacterRecoveryProcedureDefinition(
                key,
                displayName,
                ReadOptionalString(value, "presentationRole"),
                ReadOptionalBoolean(value, "available") ?? true,
                applicability,
                inputs,
                choices,
                rolls,
                effects,
                runtime,
                attributions));
        }
    }
    
    internal static void ParsePassiveValues(
        JsonElement support,
        ResolvedRuleCatalogItemView rule,
        IReadOnlyList<CharacterMechanicSourceAttributionView> attributions,
        IReadOnlyDictionary<string, ResolvedRuleCatalogItemView> ruleByConceptKey,
        ICollection<CharacterPassiveValueDefinition> output)
    {
        if (!TryReadArray(support, "passiveValues", out var values))
        {
            return;
        }
    
        foreach (var value in values.EnumerateArray())
        {
            RequireObject(value, "passive value");
            var key = RequireString(value, "key", "Passive value");
            var displayName = RequireString(value, "displayName", $"Passive value '{key}'");
            var inputs = ReadInputs(value);
            var applicability = ReadApplicability(
                value,
                rule.PackageKey,
                inputs);
            var evaluationKind = ReadOptionalString(value, "evaluationKind")
                ?? CharacterMechanicEvaluationKinds.None;
            if (evaluationKind is not (
                    CharacterMechanicEvaluationKinds.None
                    or CharacterMechanicEvaluationKinds.Sum
                    or CharacterMechanicEvaluationKinds.SourceValue))
            {
                throw new InvalidDataException(
                    $"Passive value '{key}' uses unsupported evaluation kind '{evaluationKind}'.");
            }
    
            var relatedConceptKey = ReadOptionalString(value, "relatedConceptKey");
            Guid? relatedRuleConceptId = relatedConceptKey is not null
                && ruleByConceptKey.TryGetValue(relatedConceptKey, out var relatedRule)
                    ? relatedRule.RuleConceptId
                    : null;
    
            var mechanic = new CharacterMechanicDefinition(
                key,
                CharacterMechanicKinds.PassiveValue,
                displayName,
                evaluationKind,
                ReadOptionalInteger(value, "constant") ?? 0,
                inputs,
                TargetInputKey: null,
                BaseMechanicKey: ReadOptionalString(value, "relatedMechanicKey"),
                applicability,
                ConditionalRollRules: [],
                BooleanRequirements: [],
                Source: null);
    
            output.Add(new CharacterPassiveValueDefinition(
                mechanic,
                ReadOptionalBoolean(value, "available") ?? true,
                ReadOptionalString(value, "presentationRole"),
                ReadOptionalString(value, "relatedMechanicKey"),
                relatedConceptKey,
                relatedRuleConceptId,
                ReadOptionalString(value, "relatedAbilityKey"),
                attributions));
        }
    }
    
    internal static void ParseQualifications(
        JsonElement support,
        ResolvedRuleCatalogItemView rule,
        IReadOnlyList<CharacterMechanicSourceAttributionView> attributions,
        IReadOnlyDictionary<string, ResolvedRuleCatalogItemView> ruleByConceptKey,
        ICollection<CharacterQualificationDefinition> output)
    {
        if (!TryReadArray(support, "qualifications", out var values))
        {
            return;
        }
    
        foreach (var value in values.EnumerateArray())
        {
            RequireObject(value, "qualification");
            var key = RequireString(value, "key", "Qualification");
            var displayName = RequireString(value, "displayName", $"Qualification '{key}'");
            var category = RequireString(value, "category", $"Qualification '{key}'");
            if (!value.TryGetProperty("stateInput", out var stateInputElement))
            {
                throw new InvalidDataException(
                    $"Qualification '{key}' must define stateInput.");
            }
            var stateInput = ReadInput(
                stateInputElement,
                $"Qualification '{key}' stateInput");
            var inputs = new[] { stateInput };
            var applicability = ReadApplicability(
                value,
                rule.PackageKey,
                inputs,
                defaultRequiresCharacterState: true);
            var associatedConceptKey = ReadOptionalString(
                value,
                "associatedConceptKey");
            Guid? associatedRuleConceptId = associatedConceptKey is not null
                && ruleByConceptKey.TryGetValue(associatedConceptKey, out var associatedRule)
                    ? associatedRule.RuleConceptId
                    : null;
    
            output.Add(new CharacterQualificationDefinition(
                key,
                displayName,
                category,
                ReadOptionalString(value, "family"),
                ReadOptionalBoolean(value, "available") ?? true,
                applicability,
                stateInput,
                associatedConceptKey,
                associatedRuleConceptId,
                attributions));
        }
    }
    
    private static CharacterMechanicApplicabilityDefinition ReadApplicability(
        JsonElement owner,
        string sourcePackageKey,
        IReadOnlyList<CharacterMechanicInputDefinition> inputs,
        bool defaultRequiresCharacterState = false)
    {
        var requiresCharacterState = defaultRequiresCharacterState
            || inputs.Any(value =>
                string.Equals(
                    value.Origin,
                    CharacterMechanicInputOrigins.CharacterState,
                    StringComparison.Ordinal));
    
        if (!owner.TryGetProperty("applicability", out var applicability))
        {
            return new CharacterMechanicApplicabilityDefinition(
                CharacterMechanicApplicabilityKinds.Always,
                requiresCharacterState,
                [],
                sourcePackageKey);
        }
    
        RequireObject(applicability, "character support applicability");
        var capabilities = ReadStringArray(
            applicability,
            "requiredCapabilityKeys");
        requiresCharacterState =
            ReadOptionalBoolean(applicability, "requiresCharacterState")
            ?? requiresCharacterState;
        var kind = ReadOptionalString(applicability, "kind")
            ?? (capabilities.Count > 0
                ? CharacterMechanicApplicabilityKinds.CharacterCapability
                : CharacterMechanicApplicabilityKinds.Always);
    
        return new CharacterMechanicApplicabilityDefinition(
            kind,
            requiresCharacterState,
            capabilities,
            sourcePackageKey);
    }
    
    private static IReadOnlyList<CharacterMechanicInputDefinition> ReadInputs(
        JsonElement owner)
    {
        if (!TryReadArray(owner, "inputs", out var values))
        {
            return [];
        }
    
        return values.EnumerateArray()
            .Select((value, index) => ReadInput(value, $"inputs[{index}]"))
            .ToArray();
    }
    
    private static CharacterMechanicInputDefinition ReadInput(
        JsonElement value,
        string context)
    {
        RequireObject(value, context);
        var key = RequireString(value, "key", context);
        var valueKind = RequireString(value, "valueKind", context);
        if (valueKind is not (
                CharacterMechanicInputValueKinds.Integer
                or CharacterMechanicInputValueKinds.Boolean
                or CharacterMechanicInputValueKinds.String))
        {
            throw new InvalidDataException(
                $"{context} '{key}' uses unsupported value kind '{valueKind}'.");
        }
    
        var origin = RequireString(value, "origin", context);
        if (origin is not (
                CharacterMechanicInputOrigins.CharacterState
                or CharacterMechanicInputOrigins.SourceInput
                or CharacterMechanicInputOrigins.Runtime
                or CharacterMechanicInputOrigins.Derived))
        {
            throw new InvalidDataException(
                $"{context} '{key}' uses unsupported input origin '{origin}'.");
        }
    
        var defaultInteger = ReadOptionalInteger(value, "defaultInteger");
        if (defaultInteger.HasValue
            && !string.Equals(
                valueKind,
                CharacterMechanicInputValueKinds.Integer,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{context} '{key}' defines defaultInteger for non-integer input.");
        }
    
        return new CharacterMechanicInputDefinition(
            key,
            valueKind,
            origin,
            ReadOptionalBoolean(value, "required") ?? false,
            ReadOptionalBoolean(value, "participatesInValue") ?? false,
            defaultInteger,
            ReadOptionalString(value, "includeWhenBooleanInputKey"),
            ReadOptionalBoolean(value, "includeWhenBooleanValue"));
    }
    
    private static IReadOnlyList<CharacterRecoveryChoiceDefinition> ReadChoices(
        JsonElement owner)
    {
        if (!TryReadArray(owner, "choices", out var values))
        {
            return [];
        }
    
        var choices = new List<CharacterRecoveryChoiceDefinition>();
        foreach (var value in values.EnumerateArray())
        {
            RequireObject(value, "recovery choice");
            var key = RequireString(value, "key", "Recovery choice");
            var prompt = RequireString(value, "prompt", $"Recovery choice '{key}'");
            if (!TryReadArray(value, "options", out var optionValues))
            {
                throw new InvalidDataException(
                    $"Recovery choice '{key}' must define options.");
            }
    
            var options = optionValues.EnumerateArray()
                .Select(option =>
                {
                    RequireObject(option, $"Recovery choice '{key}' option");
                    var optionKey = RequireString(
                        option,
                        "key",
                        $"Recovery choice '{key}' option");
                    return new CharacterRecoveryChoiceOptionDefinition(
                        optionKey,
                        ReadOptionalString(option, "displayName") ?? optionKey,
                        ReadOptionalString(option, "value"));
                })
                .ToArray();
            if (options.Length == 0)
            {
                throw new InvalidDataException(
                    $"Recovery choice '{key}' must define at least one option.");
            }
            EnsureUniqueKeys(
                options.Select(option => option.Key),
                $"recovery choice '{key}' option");
    
            choices.Add(new CharacterRecoveryChoiceDefinition(
                key,
                prompt,
                ReadOptionalBoolean(value, "required") ?? true,
                options));
        }
    
        EnsureUniqueKeys(choices.Select(value => value.Key), "recovery choice");
        return choices;
    }
    
    private static IReadOnlyList<CharacterRecoveryRollDefinition> ReadRolls(
        JsonElement owner)
    {
        if (!TryReadArray(owner, "rolls", out var values))
        {
            return [];
        }
    
        var rolls = values.EnumerateArray()
            .Select(value =>
            {
                RequireObject(value, "recovery roll");
                var key = RequireString(value, "key", "Recovery roll");
                var rollMode = ReadOptionalString(value, "rollMode")
                    ?? CharacterMechanicRollModes.Normal;
                if (!CharacterMechanicRollModes.IsKnown(rollMode))
                {
                    throw new InvalidDataException(
                        $"Recovery roll '{key}' uses unsupported roll mode '{rollMode}'.");
                }

                return new CharacterRecoveryRollDefinition(
                    key,
                    RequireString(value, "rollKind", $"Recovery roll '{key}'"),
                    RequireString(value, "prompt", $"Recovery roll '{key}'"),
                    ReadOptionalBoolean(value, "required") ?? true,
                    ReadOptionalString(value, "mechanicKey"),
                    CharacterMechanicRollModes.Normalize(rollMode));
            })
            .ToArray();
        EnsureUniqueKeys(rolls.Select(value => value.Key), "recovery roll");
        return rolls;
    }
    
    private static IReadOnlyList<CharacterRecoveryEffectDefinition> ReadEffects(
        JsonElement owner)
    {
        if (!TryReadArray(owner, "effects", out var values))
        {
            return [];
        }
    
        var effects = values.EnumerateArray()
            .Select(value =>
            {
                RequireObject(value, "recovery effect");
                var key = RequireString(value, "key", "Recovery effect");
                var targetKey = ReadOptionalString(value, "targetKey");
                var targetChoiceKey = ReadOptionalString(value, "targetChoiceKey");
                if (targetKey is null && targetChoiceKey is null)
                {
                    throw new InvalidDataException(
                        $"Recovery effect '{key}' must define targetKey or targetChoiceKey.");
                }
                if (targetKey is not null && targetChoiceKey is not null)
                {
                    throw new InvalidDataException(
                        $"Recovery effect '{key}' can not define both targetKey and targetChoiceKey.");
                }
    
                var amount = ReadOptionalInteger(value, "amount");
                var amountInputKey = ReadOptionalString(value, "amountInputKey");
                var amountRollKey = ReadOptionalString(value, "amountRollKey");
                var amountSources = new object?[]
                {
                    amount,
                    amountInputKey,
                    amountRollKey
                }.Count(item => item is not null);
                if (amountSources > 1)
                {
                    throw new InvalidDataException(
                        $"Recovery effect '{key}' can define only one amount source.");
                }
    
                var literalValue = ReadOptionalString(value, "value");
                var valueChoiceKey = ReadOptionalString(value, "valueChoiceKey");
                if (literalValue is not null && valueChoiceKey is not null)
                {
                    throw new InvalidDataException(
                        $"Recovery effect '{key}' can not define both value and valueChoiceKey.");
                }
    
                return new CharacterRecoveryEffectDefinition(
                    key,
                    RequireString(value, "targetKind", $"Recovery effect '{key}'"),
                    targetKey,
                    targetChoiceKey,
                    RequireString(value, "operation", $"Recovery effect '{key}'"),
                    amount,
                    amountInputKey,
                    amountRollKey,
                    literalValue,
                    valueChoiceKey,
                    ReadOptionalString(value, "referenceKey"),
                    ReadEffectConditions(value, key));
            })
            .ToArray();
        EnsureUniqueKeys(effects.Select(value => value.Key), "recovery effect");
        return effects;
    }
    
    private static IReadOnlyList<CharacterRecoveryEffectConditionDefinition> ReadEffectConditions(
        JsonElement owner,
        string effectKey)
    {
        if (!TryReadArray(owner, "conditions", out var values))
        {
            return [];
        }
    
        return values.EnumerateArray()
            .Select(value =>
            {
                RequireObject(value, $"Recovery effect '{effectKey}' condition");
                var inputKey = ReadOptionalString(value, "inputKey");
                var choiceKey = ReadOptionalString(value, "choiceKey");
                if ((inputKey is null) == (choiceKey is null))
                {
                    throw new InvalidDataException(
                        $"Recovery effect '{effectKey}' condition must define exactly one of inputKey or choiceKey.");
                }
    
                var expectedInteger = ReadOptionalInteger(value, "expectedInteger");
                var expectedBoolean = ReadOptionalBoolean(value, "expectedBoolean");
                var expectedString = ReadOptionalString(value, "expectedString");
                var expectedChoiceValue = ReadOptionalString(
                    value,
                    "expectedChoiceValue");
                if (choiceKey is not null)
                {
                    if (expectedChoiceValue is null)
                    {
                        throw new InvalidDataException(
                            $"Recovery effect '{effectKey}' choice condition must define expectedChoiceValue.");
                    }
                    return new CharacterRecoveryEffectConditionDefinition(
                        null,
                        null,
                        null,
                        null,
                        choiceKey,
                        expectedChoiceValue);
                }
    
                var expectedCount = new object?[]
                {
                    expectedInteger,
                    expectedBoolean,
                    expectedString
                }.Count(item => item is not null);
                if (expectedCount != 1)
                {
                    throw new InvalidDataException(
                        $"Recovery effect '{effectKey}' input condition must define exactly one expected value.");
                }
                return new CharacterRecoveryEffectConditionDefinition(
                    inputKey,
                    expectedInteger,
                    expectedBoolean,
                    expectedString,
                    null,
                    null);
            })
            .ToArray();
    }
    
    private static CharacterRecoveryRuntimeRequirementsView ReadRuntimeRequirements(
        JsonElement owner,
        CharacterMechanicApplicabilityDefinition applicability,
        IReadOnlyList<CharacterMechanicInputDefinition> inputs,
        IReadOnlyList<CharacterRecoveryChoiceDefinition> choices,
        IReadOnlyList<CharacterRecoveryRollDefinition> rolls,
        IReadOnlyList<CharacterRecoveryEffectDefinition> effects)
    {
        JsonElement runtime = default;
        var hasRuntime = owner.TryGetProperty("runtimeRequirements", out runtime)
            && runtime.ValueKind == JsonValueKind.Object;
    
        bool ReadFlag(string key) =>
            hasRuntime && (ReadOptionalBoolean(runtime, key) ?? false);
    
        return new CharacterRecoveryRuntimeRequirementsView(
            applicability.RequiresCharacterState
                || inputs.Any(value => string.Equals(
                    value.Origin,
                    CharacterMechanicInputOrigins.CharacterState,
                    StringComparison.Ordinal))
                || ReadFlag("requiresCharacterState"),
            choices.Any(value => value.Required)
                || ReadFlag("requiresPlayerChoices"),
            rolls.Any(value => value.Required)
                || ReadFlag("requiresRolls"),
            effects.Any(value => string.Equals(
                    value.Operation,
                    "expend",
                    StringComparison.OrdinalIgnoreCase))
                || ReadFlag("requiresResourceExpenditure"),
            inputs.Any(value => string.Equals(
                    value.Origin,
                    CharacterMechanicInputOrigins.Runtime,
                    StringComparison.Ordinal))
                || ReadFlag("requiresOtherRuntimeFacts"));
    }
    
    
    private static bool TryReadArray(
        JsonElement owner,
        string propertyName,
        out JsonElement value)
    {
        value = default;
        if (!owner.TryGetProperty(propertyName, out var property))
        {
            return false;
        }
        if (property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                $"Character support property '{propertyName}' must be an array.");
        }
        value = property;
        return true;
    }
    
    private static void RequireObject(JsonElement value, string context)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{context} must be an object.");
        }
    }
    
    private static string RequireString(
        JsonElement owner,
        string propertyName,
        string context) =>
        ReadOptionalString(owner, propertyName)
        ?? throw new InvalidDataException(
            $"{context} must define non-blank string '{propertyName}'.");
    
    private static string? ReadOptionalString(
        JsonElement owner,
        string propertyName) =>
        owner.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;
    
    private static bool? ReadOptionalBoolean(
        JsonElement owner,
        string propertyName)
    {
        if (!owner.TryGetProperty(propertyName, out var value))
        {
            return null;
        }
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidDataException(
                $"Character support property '{propertyName}' must be boolean.")
        };
    }
    
    private static int? ReadOptionalInteger(
        JsonElement owner,
        string propertyName)
    {
        if (!owner.TryGetProperty(propertyName, out var value))
        {
            return null;
        }
        if (value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var number))
        {
            throw new InvalidDataException(
                $"Character support property '{propertyName}' must be a 32-bit integer.");
        }
        return number;
    }
    
    private static IReadOnlyList<string> ReadStringArray(
        JsonElement owner,
        string propertyName)
    {
        if (!owner.TryGetProperty(propertyName, out var value))
        {
            return [];
        }
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                $"Character support property '{propertyName}' must be an array.");
        }
    
        var result = value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(item.GetString())
                    ? item.GetString()!.Trim()
                    : throw new InvalidDataException(
                        $"Character support property '{propertyName}' contains a blank or non-string value."))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return result;
    }
    
    internal static void EnsureUniqueKeys(
        IEnumerable<string> keys,
        string kind)
    {
        var duplicate = keys
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException(
                $"Effective Character support defines duplicate {kind} key '{duplicate.Key}'.");
        }
    }
    
}
