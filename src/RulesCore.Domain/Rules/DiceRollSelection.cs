namespace RulesCore.Domain.Rules;

public static class DiceRollSelectionModes
{
    public const string Normal = "normal";
    public const string Advantage = "advantage";
    public const string Disadvantage = "disadvantage";
    public const string Emphasis = "emphasis";

    public static IReadOnlyList<string> All { get; } =
        [Normal, Advantage, Disadvantage, Emphasis];
}

public sealed record DiceRollSelectionResult(
    string SelectionMode,
    IReadOnlyList<int> Rolls,
    int? SelectedRollIndex,
    int? SelectedRoll,
    IReadOnlyList<int> CandidateRollIndices,
    bool RequiresChoice);

/// <summary>
/// Deterministic roll-selection semantics shared by every Dorks & Dice roller.
///
/// Advantage rolls twice and keeps the higher number.
/// Disadvantage rolls twice and keeps the lower number.
/// Emphasis rolls twice and keeps the number furthest from 10.
///
/// Emphasis can produce a genuine unresolved tie (for example 7 and 13).
/// Rules Core preserves that ambiguity instead of inventing a tie-break rule.
/// </summary>
public static class DiceRollSelector
{
    public const int EmphasisPivot = 10;

    public static int RequiredRollCount(string? selectionMode) =>
        NormalizeMode(selectionMode) == DiceRollSelectionModes.Normal ? 1 : 2;

    public static DiceRollSelectionResult Select(
        string? selectionMode,
        IReadOnlyList<int> rolls)
    {
        ArgumentNullException.ThrowIfNull(rolls);
        var mode = NormalizeMode(selectionMode);
        var required = RequiredRollCount(mode);
        if (rolls.Count != required)
        {
            throw new ArgumentException(
                $"Roll mode '{mode}' requires exactly {required} raw roll(s).",
                nameof(rolls));
        }

        if (rolls.Any(value => value <= 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(rolls),
                "Raw die results must be positive.");
        }

        if (mode == DiceRollSelectionModes.Normal)
        {
            return Resolved(mode, rolls, 0);
        }

        var first = rolls[0];
        var second = rolls[1];

        if (mode == DiceRollSelectionModes.Advantage)
        {
            return Resolved(mode, rolls, second > first ? 1 : 0);
        }

        if (mode == DiceRollSelectionModes.Disadvantage)
        {
            return Resolved(mode, rolls, second < first ? 1 : 0);
        }

        var firstDistance = Math.Abs(first - EmphasisPivot);
        var secondDistance = Math.Abs(second - EmphasisPivot);
        if (firstDistance > secondDistance)
        {
            return Resolved(mode, rolls, 0);
        }
        if (secondDistance > firstDistance)
        {
            return Resolved(mode, rolls, 1);
        }
        if (first == second)
        {
            return Resolved(mode, rolls, 0);
        }

        return new DiceRollSelectionResult(
            mode,
            rolls.ToArray(),
            null,
            null,
            [0, 1],
            true);
    }

    public static string NormalizeMode(string? selectionMode)
    {
        var normalized = string.IsNullOrWhiteSpace(selectionMode)
            ? DiceRollSelectionModes.Normal
            : selectionMode.Trim().ToLowerInvariant();

        return DiceRollSelectionModes.All.Contains(normalized, StringComparer.Ordinal)
            ? normalized
            : throw new ArgumentException(
                $"Unknown roll selection mode '{selectionMode}'.",
                nameof(selectionMode));
    }

    private static DiceRollSelectionResult Resolved(
        string mode,
        IReadOnlyList<int> rolls,
        int selectedIndex) =>
        new(
            mode,
            rolls.ToArray(),
            selectedIndex,
            rolls[selectedIndex],
            [selectedIndex],
            false);
}
