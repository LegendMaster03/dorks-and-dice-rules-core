using RulesCore.Domain.Rules;

namespace RulesCore.Application.Rules;

public static class CharacterSupportResolver
{
    public static CharacterSupportProjectionView Project(
        CharacterSupportCatalogDefinition catalog,
        CharacterSupportProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);

        var recovery = catalog.RecoveryProcedures
            .OrderBy(value => value.DisplayName, StringComparer.Ordinal)
            .ThenBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => ProjectRecovery(value, request.CapabilityKeys))
            .ToArray();
        var passive = catalog.PassiveValues
            .OrderBy(value => value.Mechanic.DisplayName, StringComparer.Ordinal)
            .ThenBy(value => value.Mechanic.Key, StringComparer.Ordinal)
            .Select(value => ProjectPassive(value, request))
            .ToArray();
        var qualifications = catalog.Qualifications
            .OrderBy(value => value.Category, StringComparer.Ordinal)
            .ThenBy(value => value.DisplayName, StringComparer.Ordinal)
            .ThenBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => ProjectQualification(value, request))
            .ToArray();

        return new CharacterSupportProjectionView(
            catalog.Scope,
            catalog.CampaignId,
            catalog.RevisionNumber,
            catalog.PublishedAt,
            recovery,
            passive,
            qualifications);
    }

    public static CharacterRecoveryResolutionView? ResolveRecovery(
        CharacterSupportCatalogDefinition catalog,
        string procedureKey,
        CharacterRecoveryResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(procedureKey))
        {
            throw new ArgumentException("Recovery procedure key can not be blank.", nameof(procedureKey));
        }

        var procedure = catalog.RecoveryProcedures.SingleOrDefault(value =>
            string.Equals(value.Key, procedureKey.Trim(), StringComparison.OrdinalIgnoreCase));
        if (procedure is null)
        {
            return null;
        }

        if (!procedure.IsAvailableUnderRuleset)
        {
            return RecoveryResult(
                catalog,
                procedure,
                CharacterRecoveryResolutionStatuses.Unavailable,
                [],
                [],
                [],
                [],
                []);
        }

        ValidateRecoveryInputs(procedure, request);

        var applicability = ResolveApplicability(procedure.Applicability, request.CapabilityKeys);
        if (applicability.State == CharacterSupportResolutionStates.NotApplicable)
        {
            return RecoveryResult(
                catalog,
                procedure,
                CharacterRecoveryResolutionStatuses.NotApplicable,
                applicability.MissingCapabilityKeys,
                [],
                [],
                [],
                []);
        }
        if (applicability.State == CharacterSupportResolutionStates.UnresolvedCharacterState)
        {
            return RecoveryResult(
                catalog,
                procedure,
                CharacterRecoveryResolutionStatuses.InputRequired,
                applicability.MissingCapabilityKeys,
                [],
                [],
                [],
                []);
        }

        var facts = new CharacterSupportInputValues(
            request.IntegerInputs,
            request.BooleanInputs,
            request.StringInputs);
        var missingInputs = FindMissingRequiredInputs(procedure.Inputs, facts);
        var pendingChoices = procedure.Choices
            .Where(value => value.Required
                && !TryGetCaseInsensitive(request.Choices, value.Key, out _))
            .Select(ToChoiceView)
            .ToArray();
        var pendingRolls = procedure.Rolls
            .Where(value => value.Required
                && !TryGetCaseInsensitive(request.Rolls, value.Key, out _))
            .Select(ToRollView)
            .ToArray();

        ValidateSuppliedChoices(procedure, request.Choices);

        if (missingInputs.Count > 0)
        {
            return RecoveryResult(
                catalog,
                procedure,
                CharacterRecoveryResolutionStatuses.InputRequired,
                [],
                missingInputs,
                pendingChoices,
                pendingRolls,
                []);
        }
        if (pendingChoices.Length > 0)
        {
            return RecoveryResult(
                catalog,
                procedure,
                CharacterRecoveryResolutionStatuses.ChoiceRequired,
                [],
                [],
                pendingChoices,
                pendingRolls,
                []);
        }
        if (pendingRolls.Length > 0)
        {
            return RecoveryResult(
                catalog,
                procedure,
                CharacterRecoveryResolutionStatuses.RollRequired,
                [],
                [],
                [],
                pendingRolls,
                []);
        }

        var consequences = procedure.Effects
            .Where(effect => EffectApplies(effect, request))
            .Select(effect => ResolveEffect(procedure, effect, request))
            .ToArray();

        return RecoveryResult(
            catalog,
            procedure,
            CharacterRecoveryResolutionStatuses.Resolved,
            [],
            [],
            [],
            [],
            consequences);
    }

    private static CharacterRecoveryProcedureView ProjectRecovery(
        CharacterRecoveryProcedureDefinition definition,
        IReadOnlyList<string>? capabilityKeys)
    {
        var applicability = definition.IsAvailableUnderRuleset
            ? ResolveApplicability(definition.Applicability, capabilityKeys)
            : new ApplicabilityResult(CharacterSupportResolutionStates.Unavailable, []);

        return new CharacterRecoveryProcedureView(
            definition.Key,
            definition.DisplayName,
            definition.PresentationRole,
            definition.IsAvailableUnderRuleset,
            applicability.State,
            ToApplicabilityView(definition.Applicability),
            applicability.MissingCapabilityKeys,
            definition.Inputs.Select(ToInputView).ToArray(),
            definition.Choices.Select(ToChoiceView).ToArray(),
            definition.Rolls.Select(ToRollView).ToArray(),
            definition.RuntimeRequirements,
            definition.SourceAttributions);
    }

    private static CharacterPassiveValueView ProjectPassive(
        CharacterPassiveValueDefinition definition,
        CharacterSupportProjectionRequest request)
    {
        var mechanic = definition.Mechanic;
        if (!definition.IsAvailableUnderRuleset)
        {
            return PassiveResult(
                definition,
                CharacterSupportResolutionStates.Unavailable,
                null,
                [],
                []);
        }

        var applicability = ResolveApplicability(mechanic.Applicability, request.CapabilityKeys);
        if (applicability.State != CharacterSupportResolutionStates.Applicable)
        {
            return PassiveResult(
                definition,
                applicability.State,
                null,
                applicability.MissingCapabilityKeys,
                []);
        }

        var facts = FindFacts(request.FactsBySupportKey, mechanic.Key);
        var missing = FindMissingRequiredInputs(mechanic.Inputs, facts);
        if (missing.Count > 0)
        {
            return PassiveResult(
                definition,
                CharacterSupportResolutionStates.UnresolvedCharacterState,
                null,
                [],
                missing);
        }

        if (string.Equals(
                mechanic.EvaluationKind,
                CharacterMechanicEvaluationKinds.None,
                StringComparison.Ordinal))
        {
            return PassiveResult(
                definition,
                CharacterSupportResolutionStates.UnresolvedRuleDefinition,
                null,
                [],
                []);
        }

        var evaluation = CharacterMechanicEvaluator.Evaluate(
            mechanic,
            facts.IntegerInputs ?? EmptyIntegers,
            facts.BooleanInputs ?? EmptyBooleans,
            facts.StringInputs ?? EmptyStrings);

        if (!evaluation.RequirementsSatisfied)
        {
            return PassiveResult(
                definition,
                CharacterSupportResolutionStates.NotApplicable,
                null,
                [],
                evaluation.UnsatisfiedRequirementKeys);
        }

        return PassiveResult(
            definition,
            CharacterSupportResolutionStates.Resolved,
            evaluation.Value,
            [],
            []);
    }

    private static CharacterSupportQualificationView ProjectQualification(
        CharacterQualificationDefinition definition,
        CharacterSupportProjectionRequest request)
    {
        if (!definition.IsAvailableUnderRuleset)
        {
            return QualificationResult(
                definition,
                CharacterSupportResolutionStates.Unavailable,
                null,
                [],
                []);
        }

        var applicability = ResolveApplicability(definition.Applicability, request.CapabilityKeys);
        if (applicability.State != CharacterSupportResolutionStates.Applicable)
        {
            return QualificationResult(
                definition,
                applicability.State,
                null,
                applicability.MissingCapabilityKeys,
                []);
        }

        var facts = FindFacts(request.FactsBySupportKey, definition.Key);
        if (!TryReadValue(definition.StateInput, facts, out var state))
        {
            return QualificationResult(
                definition,
                CharacterSupportResolutionStates.UnresolvedCharacterState,
                null,
                [],
                [definition.StateInput.Key]);
        }

        return QualificationResult(
            definition,
            CharacterSupportResolutionStates.Resolved,
            state,
            [],
            []);
    }

    private static CharacterPassiveValueView PassiveResult(
        CharacterPassiveValueDefinition definition,
        string resolutionState,
        int? value,
        IReadOnlyList<string> missingCapabilityKeys,
        IReadOnlyList<string> missingInputKeys) =>
        new(
            definition.Mechanic.Key,
            definition.Mechanic.DisplayName,
            definition.PresentationRole,
            definition.IsAvailableUnderRuleset,
            resolutionState,
            value,
            ToApplicabilityView(definition.Mechanic.Applicability),
            missingCapabilityKeys,
            missingInputKeys,
            definition.RelatedMechanicKey,
            definition.RelatedConceptKey,
            definition.RelatedRuleConceptId,
            definition.RelatedAbilityKey,
            definition.Mechanic.Inputs.Select(ToInputView).ToArray(),
            definition.SourceAttributions);

    private static CharacterSupportQualificationView QualificationResult(
        CharacterQualificationDefinition definition,
        string resolutionState,
        CharacterSupportValueView? state,
        IReadOnlyList<string> missingCapabilityKeys,
        IReadOnlyList<string> missingInputKeys) =>
        new(
            definition.Key,
            definition.DisplayName,
            definition.Category,
            definition.Family,
            definition.IsAvailableUnderRuleset,
            resolutionState,
            state,
            ToApplicabilityView(definition.Applicability),
            missingCapabilityKeys,
            missingInputKeys,
            definition.AssociatedConceptKey,
            definition.AssociatedRuleConceptId,
            ToInputView(definition.StateInput),
            definition.SourceAttributions);

    private static CharacterRecoveryResolutionView RecoveryResult(
        CharacterSupportCatalogDefinition catalog,
        CharacterRecoveryProcedureDefinition procedure,
        string status,
        IReadOnlyList<string> missingCapabilityKeys,
        IReadOnlyList<string> missingInputKeys,
        IReadOnlyList<CharacterRecoveryChoiceView> pendingChoices,
        IReadOnlyList<CharacterRecoveryRollView> pendingRolls,
        IReadOnlyList<CharacterRecoveryEffectView> consequences) =>
        new(
            catalog.Scope,
            catalog.CampaignId,
            catalog.RevisionNumber,
            catalog.PublishedAt,
            procedure.Key,
            procedure.DisplayName,
            procedure.PresentationRole,
            status,
            missingCapabilityKeys,
            missingInputKeys,
            pendingChoices,
            pendingRolls,
            consequences,
            procedure.SourceAttributions);

    private static ApplicabilityResult ResolveApplicability(
        CharacterMechanicApplicabilityDefinition applicability,
        IReadOnlyList<string>? capabilityKeys)
    {
        if (applicability.RequiredCapabilityKeys.Count == 0)
        {
            return new ApplicabilityResult(CharacterSupportResolutionStates.Applicable, []);
        }

        if (capabilityKeys is null)
        {
            return new ApplicabilityResult(
                CharacterSupportResolutionStates.UnresolvedCharacterState,
                applicability.RequiredCapabilityKeys);
        }

        var available = capabilityKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = applicability.RequiredCapabilityKeys
            .Where(value => !available.Contains(value))
            .ToArray();
        return missing.Length == 0
            ? new ApplicabilityResult(CharacterSupportResolutionStates.Applicable, [])
            : new ApplicabilityResult(CharacterSupportResolutionStates.NotApplicable, missing);
    }

    private static IReadOnlyList<string> FindMissingRequiredInputs(
        IReadOnlyList<CharacterMechanicInputDefinition> inputs,
        CharacterSupportInputValues facts)
    {
        var booleanInputs = facts.BooleanInputs ?? EmptyBooleans;
        return inputs
            .Where(input => input.Required && InputIsActive(input, booleanInputs))
            .Where(input => !InputIsPresent(input, facts))
            .Select(input => input.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool InputIsPresent(
        CharacterMechanicInputDefinition input,
        CharacterSupportInputValues facts) =>
        input.ValueKind switch
        {
            CharacterMechanicInputValueKinds.Integer =>
                TryGetCaseInsensitive(facts.IntegerInputs, input.Key, out _)
                || input.DefaultInteger.HasValue,
            CharacterMechanicInputValueKinds.Boolean =>
                TryGetCaseInsensitive(facts.BooleanInputs, input.Key, out _),
            CharacterMechanicInputValueKinds.String =>
                TryGetCaseInsensitive(facts.StringInputs, input.Key, out var value)
                && !string.IsNullOrWhiteSpace(value),
            _ => false
        };

    private static bool InputIsActive(
        CharacterMechanicInputDefinition input,
        IReadOnlyDictionary<string, bool> booleanInputs)
    {
        if (string.IsNullOrWhiteSpace(input.IncludeWhenBooleanInputKey))
        {
            return true;
        }
        return input.IncludeWhenBooleanValue.HasValue
            && TryGetCaseInsensitive(
                booleanInputs,
                input.IncludeWhenBooleanInputKey,
                out var supplied)
            && supplied == input.IncludeWhenBooleanValue.Value;
    }

    private static bool TryReadValue(
        CharacterMechanicInputDefinition input,
        CharacterSupportInputValues facts,
        out CharacterSupportValueView? value)
    {
        value = null;
        switch (input.ValueKind)
        {
            case CharacterMechanicInputValueKinds.Integer:
                if (TryGetCaseInsensitive(facts.IntegerInputs, input.Key, out var integer))
                {
                    value = new CharacterSupportValueView(input.ValueKind, integer, null, null);
                    return true;
                }
                if (input.DefaultInteger.HasValue)
                {
                    value = new CharacterSupportValueView(
                        input.ValueKind,
                        input.DefaultInteger.Value,
                        null,
                        null);
                    return true;
                }
                return false;
            case CharacterMechanicInputValueKinds.Boolean:
                if (TryGetCaseInsensitive(facts.BooleanInputs, input.Key, out var boolean))
                {
                    value = new CharacterSupportValueView(input.ValueKind, null, boolean, null);
                    return true;
                }
                return false;
            case CharacterMechanicInputValueKinds.String:
                if (TryGetCaseInsensitive(facts.StringInputs, input.Key, out var text)
                    && !string.IsNullOrWhiteSpace(text))
                {
                    value = new CharacterSupportValueView(input.ValueKind, null, null, text);
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    private static void ValidateRecoveryInputs(
        CharacterRecoveryProcedureDefinition procedure,
        CharacterRecoveryResolutionRequest request)
    {
        ValidateTypedInputKeys(
            procedure,
            request.IntegerInputs?.Keys,
            CharacterMechanicInputValueKinds.Integer);
        ValidateTypedInputKeys(
            procedure,
            request.BooleanInputs?.Keys,
            CharacterMechanicInputValueKinds.Boolean);
        ValidateTypedInputKeys(
            procedure,
            request.StringInputs?.Keys,
            CharacterMechanicInputValueKinds.String);

        foreach (var key in request.Choices?.Keys ?? Enumerable.Empty<string>())
        {
            if (!procedure.Choices.Any(value =>
                    string.Equals(value.Key, key, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(
                    $"Recovery procedure '{procedure.Key}' does not define choice '{key}'.");
            }
        }

        foreach (var key in request.Rolls?.Keys ?? Enumerable.Empty<string>())
        {
            if (!procedure.Rolls.Any(value =>
                    string.Equals(value.Key, key, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(
                    $"Recovery procedure '{procedure.Key}' does not define roll '{key}'.");
            }
        }
    }

    private static void ValidateTypedInputKeys(
        CharacterRecoveryProcedureDefinition procedure,
        IEnumerable<string>? suppliedKeys,
        string valueKind)
    {
        foreach (var key in suppliedKeys ?? [])
        {
            var definition = procedure.Inputs.SingleOrDefault(value =>
                string.Equals(value.Key, key, StringComparison.OrdinalIgnoreCase));
            if (definition is null)
            {
                throw new ArgumentException(
                    $"Recovery procedure '{procedure.Key}' does not define input '{key}'. Final Character state can not be submitted as an undeclared recovery input.");
            }
            if (!string.Equals(definition.ValueKind, valueKind, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Recovery procedure '{procedure.Key}' input '{key}' expects value kind '{definition.ValueKind}', not '{valueKind}'.");
            }
        }
    }

    private static void ValidateSuppliedChoices(
        CharacterRecoveryProcedureDefinition procedure,
        IReadOnlyDictionary<string, string>? suppliedChoices)
    {
        if (suppliedChoices is null)
        {
            return;
        }

        foreach (var pair in suppliedChoices)
        {
            var choice = procedure.Choices.Single(value =>
                string.Equals(value.Key, pair.Key, StringComparison.OrdinalIgnoreCase));
            if (!choice.Options.Any(option =>
                    string.Equals(option.Key, pair.Value, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(
                    $"Recovery procedure '{procedure.Key}' choice '{choice.Key}' does not define option '{pair.Value}'.");
            }
        }
    }

    private static bool EffectApplies(
        CharacterRecoveryEffectDefinition effect,
        CharacterRecoveryResolutionRequest request)
    {
        foreach (var condition in effect.Conditions)
        {
            if (!string.IsNullOrWhiteSpace(condition.ChoiceKey))
            {
                if (!TryGetCaseInsensitive(request.Choices, condition.ChoiceKey, out var selected)
                    || !string.Equals(
                        selected,
                        condition.ExpectedChoiceValue,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                continue;
            }

            if (string.IsNullOrWhiteSpace(condition.InputKey))
            {
                throw new InvalidOperationException(
                    $"Recovery effect '{effect.Key}' contains a condition without an input or choice key.");
            }

            if (condition.ExpectedInteger.HasValue)
            {
                if (!TryGetCaseInsensitive(
                        request.IntegerInputs,
                        condition.InputKey,
                        out var integer)
                    || integer != condition.ExpectedInteger.Value)
                {
                    return false;
                }
                continue;
            }
            if (condition.ExpectedBoolean.HasValue)
            {
                if (!TryGetCaseInsensitive(
                        request.BooleanInputs,
                        condition.InputKey,
                        out var boolean)
                    || boolean != condition.ExpectedBoolean.Value)
                {
                    return false;
                }
                continue;
            }
            if (condition.ExpectedString is not null)
            {
                if (!TryGetCaseInsensitive(
                        request.StringInputs,
                        condition.InputKey,
                        out var text)
                    || !string.Equals(
                        text,
                        condition.ExpectedString,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                continue;
            }

            throw new InvalidOperationException(
                $"Recovery effect '{effect.Key}' condition for '{condition.InputKey}' has no expected value.");
        }

        return true;
    }

    private static CharacterRecoveryEffectView ResolveEffect(
        CharacterRecoveryProcedureDefinition procedure,
        CharacterRecoveryEffectDefinition effect,
        CharacterRecoveryResolutionRequest request)
    {
        var targetKey = effect.TargetKey;
        if (!string.IsNullOrWhiteSpace(effect.TargetChoiceKey))
        {
            targetKey = ResolveChoiceValue(procedure, request, effect.TargetChoiceKey);
        }
        if (string.IsNullOrWhiteSpace(targetKey))
        {
            throw new InvalidOperationException(
                $"Recovery effect '{effect.Key}' does not resolve a target key.");
        }

        int? amount = effect.Amount;
        if (!string.IsNullOrWhiteSpace(effect.AmountInputKey))
        {
            if (!TryGetCaseInsensitive(request.IntegerInputs, effect.AmountInputKey, out var supplied))
            {
                throw new InvalidOperationException(
                    $"Recovery effect '{effect.Key}' requires integer input '{effect.AmountInputKey}'.");
            }
            amount = supplied;
        }
        if (!string.IsNullOrWhiteSpace(effect.AmountRollKey))
        {
            if (!TryGetCaseInsensitive(request.Rolls, effect.AmountRollKey, out var rolled))
            {
                throw new InvalidOperationException(
                    $"Recovery effect '{effect.Key}' requires roll '{effect.AmountRollKey}'.");
            }
            amount = rolled;
        }

        var value = effect.Value;
        if (!string.IsNullOrWhiteSpace(effect.ValueChoiceKey))
        {
            value = ResolveChoiceValue(procedure, request, effect.ValueChoiceKey);
        }

        return new CharacterRecoveryEffectView(
            effect.Key,
            effect.TargetKind,
            targetKey,
            effect.Operation,
            amount,
            value,
            effect.ReferenceKey);
    }

    private static string ResolveChoiceValue(
        CharacterRecoveryProcedureDefinition procedure,
        CharacterRecoveryResolutionRequest request,
        string choiceKey)
    {
        if (!TryGetCaseInsensitive(request.Choices, choiceKey, out var selectedKey))
        {
            throw new InvalidOperationException(
                $"Recovery procedure '{procedure.Key}' requires choice '{choiceKey}'.");
        }

        var choice = procedure.Choices.Single(value =>
            string.Equals(value.Key, choiceKey, StringComparison.OrdinalIgnoreCase));
        var option = choice.Options.Single(value =>
            string.Equals(value.Key, selectedKey, StringComparison.OrdinalIgnoreCase));
        return option.Value ?? option.Key;
    }

    private static CharacterSupportInputValues FindFacts(
        IReadOnlyDictionary<string, CharacterSupportInputValues>? factsBySupportKey,
        string supportKey)
    {
        if (factsBySupportKey is null)
        {
            return new CharacterSupportInputValues();
        }

        foreach (var pair in factsBySupportKey)
        {
            if (string.Equals(pair.Key, supportKey, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value ?? new CharacterSupportInputValues();
            }
        }

        return new CharacterSupportInputValues();
    }

    private static CharacterMechanicApplicabilityView ToApplicabilityView(
        CharacterMechanicApplicabilityDefinition value) =>
        new(
            value.Kind,
            value.RequiresCharacterState,
            value.RequiredCapabilityKeys,
            value.SourcePackageKey);

    private static CharacterMechanicInputView ToInputView(
        CharacterMechanicInputDefinition value) =>
        new(
            value.Key,
            value.ValueKind,
            value.Origin,
            value.Required,
            value.ParticipatesInValue,
            value.DefaultInteger,
            value.IncludeWhenBooleanInputKey,
            value.IncludeWhenBooleanValue);

    private static CharacterRecoveryChoiceView ToChoiceView(
        CharacterRecoveryChoiceDefinition value) =>
        new(
            value.Key,
            value.Prompt,
            value.Required,
            value.Options.Select(option => new CharacterRecoveryChoiceOptionView(
                option.Key,
                option.DisplayName,
                option.Value)).ToArray());

    private static CharacterRecoveryRollView ToRollView(
        CharacterRecoveryRollDefinition value) =>
        new(
            value.Key,
            value.RollKind,
            value.Prompt,
            value.Required,
            value.MechanicKey,
            CharacterMechanicRollModes.Normalize(value.RollMode));

    private static bool TryGetCaseInsensitive<T>(
        IReadOnlyDictionary<string, T>? values,
        string key,
        out T value)
    {
        if (values is not null)
        {
            foreach (var pair in values)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }
        }

        value = default!;
        return false;
    }

    private static readonly IReadOnlyDictionary<string, int> EmptyIntegers =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyDictionary<string, bool> EmptyBooleans =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyDictionary<string, string> EmptyStrings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private sealed record ApplicabilityResult(
        string State,
        IReadOnlyList<string> MissingCapabilityKeys);
}
