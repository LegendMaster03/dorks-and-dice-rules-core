using System.Globalization;

namespace RulesCore.Domain.Rules;

public static class TravelEnvironmentMechanicKinds
{
    public const string DistanceRate = "distance-rate";
    public const string DistanceFactor = "distance-factor";
    public const string MovementCostFactor = "movement-cost-factor";
    public const string TravelTimeCostFactor = "travel-time-cost-factor";
    public const string CheckDc = "check-dc";
    public const string Duration = "duration";
}

public static class TravelEnvironmentResolutionKinds
{
    public const string LookupQuantity = "lookup-quantity";
    public const string LookupFactor = "lookup-factor";
    public const string ConstantFactor = "constant-factor";
    public const string LinearCheckDc = "linear-check-dc";
    public const string MaximumApplicableCheckDc = "maximum-applicable-check-dc";
    public const string ThresholdFactor = "threshold-factor";
}

public static class TravelEnvironmentInputValueKinds
{
    public const string Integer = "integer";
    public const string Boolean = "boolean";
    public const string String = "string";
    public const string StringList = "string-list";
}

public static class TravelEnvironmentEvaluationStates
{
    public const string Resolved = "resolved";
    public const string InputRequired = "input-required";
    public const string NotApplicable = "not-applicable";
}

public sealed record TravelEnvironmentInputDefinition(
    string Key,
    string ValueKind,
    bool Required,
    IReadOnlyList<string>? AllowedValues = null);

public sealed record TravelEnvironmentSelector(
    string InputKey,
    string Value);

public sealed record TravelEnvironmentQuantity(
    decimal Value,
    string Unit,
    string? PerUnit = null);

public sealed record TravelEnvironmentQuantityRow(
    IReadOnlyList<TravelEnvironmentSelector> Selectors,
    TravelEnvironmentQuantity Quantity);

public sealed record TravelEnvironmentFactorRow(
    IReadOnlyList<TravelEnvironmentSelector> Selectors,
    decimal Factor);

public sealed record TravelEnvironmentLinearCheckDefinition(
    int BaseDc,
    string StepInputKey,
    int DcPerStep,
    int MinimumStepValue,
    string? AbilityKey,
    string? CompetencyConceptKey,
    string? Cadence,
    string? FailureConsequenceKey);

public sealed record TravelEnvironmentCheckOption(
    string Key,
    int Dc);

public sealed record TravelEnvironmentMaximumCheckDefinition(
    string OptionInputKey,
    IReadOnlyList<TravelEnvironmentCheckOption> Options,
    string? AbilityKey,
    string? CompetencyConceptKey,
    string? Cadence,
    string? FailureConsequenceKey);

public sealed record TravelEnvironmentThresholdFactorDefinition(
    string ThresholdInputKey,
    int MinimumInclusive,
    string? ApplicabilityBooleanInputKey,
    decimal Factor);

public sealed record TravelEnvironmentMechanicDefinition(
    string MechanicKey,
    string Kind,
    string DisplayName,
    string ResolutionKind,
    IReadOnlyList<TravelEnvironmentInputDefinition> Inputs,
    IReadOnlyList<TravelEnvironmentQuantityRow>? QuantityRows = null,
    IReadOnlyList<TravelEnvironmentFactorRow>? FactorRows = null,
    decimal? ConstantFactor = null,
    TravelEnvironmentLinearCheckDefinition? LinearCheck = null,
    TravelEnvironmentMaximumCheckDefinition? MaximumCheck = null,
    TravelEnvironmentThresholdFactorDefinition? ThresholdFactor = null,
    string? FactorSemantic = null,
    string? Scale = null);

public sealed record TravelEnvironmentResolutionInput(
    IReadOnlyDictionary<string, int>? IntegerInputs = null,
    IReadOnlyDictionary<string, bool>? BooleanInputs = null,
    IReadOnlyDictionary<string, string>? StringInputs = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? StringListInputs = null);

public sealed record TravelEnvironmentCheckResolution(
    int Dc,
    string? AbilityKey,
    string? CompetencyConceptKey,
    string? Cadence,
    string? FailureConsequenceKey);

public sealed record TravelEnvironmentMechanicEvaluation(
    string State,
    TravelEnvironmentQuantity? Quantity,
    decimal? Factor,
    TravelEnvironmentCheckResolution? Check,
    IReadOnlyList<string> MissingInputKeys);

public static class TravelEnvironmentMechanicEvaluator
{
    private static readonly IReadOnlyDictionary<string, int> EmptyIntegers =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyDictionary<string, bool> EmptyBooleans =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyDictionary<string, string> EmptyStrings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptyStringLists =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    public static TravelEnvironmentMechanicEvaluation Evaluate(
        TravelEnvironmentMechanicDefinition definition,
        TravelEnvironmentResolutionInput? input = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        input ??= new TravelEnvironmentResolutionInput();

        var integers = input.IntegerInputs ?? EmptyIntegers;
        var booleans = input.BooleanInputs ?? EmptyBooleans;
        var strings = input.StringInputs ?? EmptyStrings;
        var stringLists = input.StringListInputs ?? EmptyStringLists;
        var missing = definition.Inputs
            .Where(value => value.Required && !HasInput(value, integers, booleans, strings, stringLists))
            .Select(value => value.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length > 0)
        {
            return new TravelEnvironmentMechanicEvaluation(
                TravelEnvironmentEvaluationStates.InputRequired,
                null,
                null,
                null,
                missing);
        }

        ValidateAllowedValues(definition, strings, stringLists);

        return definition.ResolutionKind switch
        {
            TravelEnvironmentResolutionKinds.LookupQuantity =>
                ResolveQuantity(definition, integers, booleans, strings),
            TravelEnvironmentResolutionKinds.LookupFactor =>
                ResolveFactor(definition, integers, booleans, strings),
            TravelEnvironmentResolutionKinds.ConstantFactor =>
                ResolveConstantFactor(definition),
            TravelEnvironmentResolutionKinds.LinearCheckDc =>
                ResolveLinearCheck(definition, integers),
            TravelEnvironmentResolutionKinds.MaximumApplicableCheckDc =>
                ResolveMaximumCheck(definition, stringLists),
            TravelEnvironmentResolutionKinds.ThresholdFactor =>
                ResolveThresholdFactor(definition, integers, booleans),
            _ => throw new InvalidOperationException(
                $"Travel/environment mechanic '{definition.MechanicKey}' uses unsupported resolution kind '{definition.ResolutionKind}'.")
        };
    }

    private static TravelEnvironmentMechanicEvaluation ResolveQuantity(
        TravelEnvironmentMechanicDefinition definition,
        IReadOnlyDictionary<string, int> integers,
        IReadOnlyDictionary<string, bool> booleans,
        IReadOnlyDictionary<string, string> strings)
    {
        var row = (definition.QuantityRows ?? [])
            .SingleOrDefault(value => Matches(value.Selectors, integers, booleans, strings));
        return row is null
            ? NotApplicable()
            : Resolved(quantity: row.Quantity);
    }

    private static TravelEnvironmentMechanicEvaluation ResolveFactor(
        TravelEnvironmentMechanicDefinition definition,
        IReadOnlyDictionary<string, int> integers,
        IReadOnlyDictionary<string, bool> booleans,
        IReadOnlyDictionary<string, string> strings)
    {
        var row = (definition.FactorRows ?? [])
            .SingleOrDefault(value => Matches(value.Selectors, integers, booleans, strings));
        return row is null
            ? NotApplicable()
            : Resolved(factor: row.Factor);
    }

    private static TravelEnvironmentMechanicEvaluation ResolveConstantFactor(
        TravelEnvironmentMechanicDefinition definition)
    {
        if (definition.ConstantFactor is null)
        {
            throw new InvalidOperationException(
                $"Travel/environment mechanic '{definition.MechanicKey}' has no constant factor.");
        }
        return Resolved(factor: definition.ConstantFactor.Value);
    }

    private static TravelEnvironmentMechanicEvaluation ResolveLinearCheck(
        TravelEnvironmentMechanicDefinition definition,
        IReadOnlyDictionary<string, int> integers)
    {
        var check = definition.LinearCheck
            ?? throw new InvalidOperationException(
                $"Travel/environment mechanic '{definition.MechanicKey}' has no linear check definition.");
        if (!integers.TryGetValue(check.StepInputKey, out var step))
        {
            return InputRequired(check.StepInputKey);
        }
        if (step < check.MinimumStepValue)
        {
            return NotApplicable();
        }

        var dc = checked(check.BaseDc + checked(step * check.DcPerStep));
        return Resolved(check: new TravelEnvironmentCheckResolution(
            dc,
            check.AbilityKey,
            check.CompetencyConceptKey,
            check.Cadence,
            check.FailureConsequenceKey));
    }

    private static TravelEnvironmentMechanicEvaluation ResolveMaximumCheck(
        TravelEnvironmentMechanicDefinition definition,
        IReadOnlyDictionary<string, IReadOnlyList<string>> stringLists)
    {
        var check = definition.MaximumCheck
            ?? throw new InvalidOperationException(
                $"Travel/environment mechanic '{definition.MechanicKey}' has no maximum-applicable check definition.");
        if (!stringLists.TryGetValue(check.OptionInputKey, out var supplied)
            || supplied.Count == 0)
        {
            return InputRequired(check.OptionInputKey);
        }

        var options = check.Options.ToDictionary(value => value.Key, StringComparer.OrdinalIgnoreCase);
        var unknown = supplied
            .Where(value => !options.ContainsKey(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (unknown.Length > 0)
        {
            throw new ArgumentException(
                $"Unknown option(s) for '{check.OptionInputKey}': {string.Join(", ", unknown)}.");
        }

        var dc = supplied.Max(value => options[value].Dc);
        return Resolved(check: new TravelEnvironmentCheckResolution(
            dc,
            check.AbilityKey,
            check.CompetencyConceptKey,
            check.Cadence,
            check.FailureConsequenceKey));
    }

    private static TravelEnvironmentMechanicEvaluation ResolveThresholdFactor(
        TravelEnvironmentMechanicDefinition definition,
        IReadOnlyDictionary<string, int> integers,
        IReadOnlyDictionary<string, bool> booleans)
    {
        var threshold = definition.ThresholdFactor
            ?? throw new InvalidOperationException(
                $"Travel/environment mechanic '{definition.MechanicKey}' has no threshold-factor definition.");
        if (!integers.TryGetValue(threshold.ThresholdInputKey, out var value))
        {
            return InputRequired(threshold.ThresholdInputKey);
        }
        if (threshold.ApplicabilityBooleanInputKey is { } applicabilityKey)
        {
            if (!booleans.TryGetValue(applicabilityKey, out var applies))
            {
                return InputRequired(applicabilityKey);
            }
            if (!applies)
            {
                return NotApplicable();
            }
        }

        return value < threshold.MinimumInclusive
            ? NotApplicable()
            : Resolved(factor: threshold.Factor);
    }

    private static bool HasInput(
        TravelEnvironmentInputDefinition definition,
        IReadOnlyDictionary<string, int> integers,
        IReadOnlyDictionary<string, bool> booleans,
        IReadOnlyDictionary<string, string> strings,
        IReadOnlyDictionary<string, IReadOnlyList<string>> stringLists) =>
        definition.ValueKind switch
        {
            TravelEnvironmentInputValueKinds.Integer => integers.ContainsKey(definition.Key),
            TravelEnvironmentInputValueKinds.Boolean => booleans.ContainsKey(definition.Key),
            TravelEnvironmentInputValueKinds.String => strings.TryGetValue(definition.Key, out var value)
                && !string.IsNullOrWhiteSpace(value),
            TravelEnvironmentInputValueKinds.StringList => stringLists.TryGetValue(definition.Key, out var values)
                && values.Count > 0,
            _ => throw new InvalidOperationException(
                $"Travel/environment input '{definition.Key}' uses unsupported value kind '{definition.ValueKind}'.")
        };

    private static void ValidateAllowedValues(
        TravelEnvironmentMechanicDefinition definition,
        IReadOnlyDictionary<string, string> strings,
        IReadOnlyDictionary<string, IReadOnlyList<string>> stringLists)
    {
        foreach (var input in definition.Inputs.Where(value => value.AllowedValues is { Count: > 0 }))
        {
            var allowed = input.AllowedValues!.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (input.ValueKind == TravelEnvironmentInputValueKinds.String
                && strings.TryGetValue(input.Key, out var supplied)
                && !allowed.Contains(supplied))
            {
                throw new ArgumentException(
                    $"Input '{input.Key}' has unsupported value '{supplied}'. Expected one of: {string.Join(", ", allowed.OrderBy(value => value, StringComparer.Ordinal))}.");
            }
            if (input.ValueKind == TravelEnvironmentInputValueKinds.StringList
                && stringLists.TryGetValue(input.Key, out var values))
            {
                var unknown = values.Where(value => !allowed.Contains(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (unknown.Length > 0)
                {
                    throw new ArgumentException(
                        $"Input '{input.Key}' has unsupported value(s): {string.Join(", ", unknown)}.");
                }
            }
        }
    }

    private static bool Matches(
        IReadOnlyList<TravelEnvironmentSelector> selectors,
        IReadOnlyDictionary<string, int> integers,
        IReadOnlyDictionary<string, bool> booleans,
        IReadOnlyDictionary<string, string> strings) =>
        selectors.All(selector =>
        {
            if (strings.TryGetValue(selector.InputKey, out var stringValue))
            {
                return string.Equals(stringValue, selector.Value, StringComparison.OrdinalIgnoreCase);
            }
            if (integers.TryGetValue(selector.InputKey, out var integerValue))
            {
                return string.Equals(
                    integerValue.ToString(CultureInfo.InvariantCulture),
                    selector.Value,
                    StringComparison.OrdinalIgnoreCase);
            }
            if (booleans.TryGetValue(selector.InputKey, out var booleanValue))
            {
                return string.Equals(
                    booleanValue.ToString(),
                    selector.Value,
                    StringComparison.OrdinalIgnoreCase);
            }
            return false;
        });

    private static TravelEnvironmentMechanicEvaluation Resolved(
        TravelEnvironmentQuantity? quantity = null,
        decimal? factor = null,
        TravelEnvironmentCheckResolution? check = null) =>
        new(
            TravelEnvironmentEvaluationStates.Resolved,
            quantity,
            factor,
            check,
            []);

    private static TravelEnvironmentMechanicEvaluation InputRequired(params string[] keys) =>
        new(
            TravelEnvironmentEvaluationStates.InputRequired,
            null,
            null,
            null,
            keys);

    private static TravelEnvironmentMechanicEvaluation NotApplicable() =>
        new(
            TravelEnvironmentEvaluationStates.NotApplicable,
            null,
            null,
            null,
            []);
}
