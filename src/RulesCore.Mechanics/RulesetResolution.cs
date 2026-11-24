namespace RulesCore.Mechanics;

/// <summary>Caller-supplied final dice and modifiers for deterministic evaluation;
/// not a character-state authorization or a random-number-generation contract.</summary>
public sealed record D20ResolutionOptions(
    bool Advantage = false, bool Disadvantage = false, int Exhaustion = 0);

public sealed record RulesetResolutionRequest(
    string? Operation, int[]? Dice, int Modifier, int Target,
    D20ResolutionOptions? D20 = null);

public sealed record RulesetResolutionResult(
    string Status, string? Error = null, long? Total = null, long? Margin = null,
    string? Outcome = null, int? SelectedDie = null, string? RollMode = null,
    int? ExhaustionPenalty = null);

/// <summary>
/// Representative resolution dispatch. Edition-specific rules are selected by
/// the authoritative package identity, never by a caller-selected policy flag.
/// Existing first-party projections remain in their established implementation.
/// </summary>
public static class RulesetResolution
{
    public const string D20AbilityCheck = "d20-ability-check";
    public const string FateRoll = "fate-roll";

    public static string[] Operations(string rulesetId) => rulesetId switch
    {
        "dnd-5e" or "dnd-5-5e" => [D20AbilityCheck],
        "fate-condensed" => [FateRoll],
        _ => []
    };

    public static RulesetResolutionResult Evaluate(string rulesetId, RulesetResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return (rulesetId, request.Operation) switch
        {
            ("dnd-5e", D20AbilityCheck) => AbilityCheck(request, modernExhaustion: false),
            ("dnd-5-5e", D20AbilityCheck) => AbilityCheck(request, modernExhaustion: true),
            ("fate-condensed", FateRoll) => Fate(request),
            _ => new("unsupported", "unsupported_mechanic")
        };
    }

    private static RulesetResolutionResult AbilityCheck(
        RulesetResolutionRequest request, bool modernExhaustion)
    {
        var options = request.D20 ?? new D20ResolutionOptions();
        if (options.Exhaustion is < 0 or > 6) return Invalid("invalid_exhaustion");
        if (options.Exhaustion == 6)
            return new("unsupported", "actor_unable_to_check");

        // SRD 5.1 Appendix PH-A: exhaustion imposes disadvantage on ability
        // checks starting at level 1. SRD 5.2.1 Rules Glossary: subtract twice
        // the level, independently of any advantage/disadvantage.
        var disadvantage = options.Disadvantage || (!modernExhaustion && options.Exhaustion > 0);
        var advantage = options.Advantage;
        var mode = advantage == disadvantage ? "normal" : advantage ? "advantage" : "disadvantage";
        var dice = request.Dice;
        if (dice is null || dice.Length != (mode == "normal" ? 1 : 2) ||
            dice.Any(die => die is < 1 or > 20))
            return Invalid("invalid_d20_dice");
        var selected = mode == "advantage" ? dice.Max() :
            mode == "disadvantage" ? dice.Min() : dice[0];
        var penalty = modernExhaustion ? 2 * options.Exhaustion : 0;
        var total = (long)selected + request.Modifier - penalty;
        var margin = total - request.Target;
        // Ability checks (not attack rolls) use the final total, including
        // when the die shows 1 or 20.
        return new("resolved", Total: total, Margin: margin,
            Outcome: margin >= 0 ? "success" : "failure", SelectedDie: selected,
            RollMode: mode, ExhaustionPenalty: penalty);
    }

    private static RulesetResolutionResult Fate(RulesetResolutionRequest request)
    {
        if (request.D20 is not null) return Invalid("incompatible_options");
        var dice = request.Dice;
        if (dice is null || dice.Length != 4 || dice.Any(die => die is < -1 or > 1))
            return Invalid("invalid_fate_dice");
        var total = (long)dice.Sum() + request.Modifier;
        var margin = total - request.Target;
        // Fate Condensed: this is the effort/outcome category, not the
        // action-specific adjudication of costs, boosts, stress, or compels.
        return new("resolved", Total: total, Margin: margin, Outcome: margin switch
        {
            < 0 => "failure",
            0 => "tie",
            >= 3 => "success-with-style",
            _ => "success"
        });
    }

    private static RulesetResolutionResult Invalid(string reason) => new("invalid", reason);
}
