using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Attack-check inputs are claimed by the authenticated encounter consumer.
/// This preview does not prove attacker ownership, ability eligibility, resource
/// expenditure, target reach, or the authenticity of supplied AC contributions.
/// </summary>
public sealed record NativeCombatAttackRequest(
    string RulesetId,
    string Fingerprint,
    string AttackKind,
    int AttackBonus,
    int FirstDie,
    int? SecondDie,
    NativeDefenseProfile DefenseProfile,
    IReadOnlyDictionary<string, int>? DefenderContributions,
    IReadOnlyList<string>? AttackerConditionIds = null,
    IReadOnlyList<string>? DefenderConditionIds = null,
    NativeRollMode BaseRollMode = NativeRollMode.Normal,
    int CriticalThreatMinimum = 20,
    NativeEffectContext? AttackerFacts = null,
    NativeEffectContext? DefenderFacts = null);

public sealed record NativeCombatAttackResult(
    string Status,
    bool? Hit,
    int? AttackTotal,
    int? ArmorClass,
    int? NaturalDie,
    NativeRollMode? RollMode,
    IReadOnlyList<NativeIssue> Issues,
    string? Reason = null);

/// <summary>
/// Deterministic first slice of the shared native attack pipeline.
/// Reuses the existing roll, defense and condition evaluators; it never applies
/// damage or invents a critical effect, a creature grant or an encounter event.
/// </summary>
public static class NativeCombatAttackResolver
{
    private static readonly HashSet<string> AllowedContributions = new(
        ["armor", "shield", "natural-armor", "dexterity",
         "dodge", "size", "deflection"], StringComparer.Ordinal);

    public static NativeCombatAttackResult Resolve(
        NativeCatalogSnapshot catalog, NativeCombatAttackRequest request)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (request is null)
            return Failure("invalid-input", "Attack request is required.");
        if (request.RulesetId != catalog.RulesetId ||
            request.Fingerprint != catalog.Fingerprint)
            return Failure("ruleset-version-mismatch",
                "A current matching native catalog fingerprint is required.");
        if (request.AttackKind is not ("melee" or "ranged") ||
            request.DefenderContributions is null ||
            request.DefenderContributions.Count > 20 ||
            request.DefenderContributions.Any(x =>
                !AllowedContributions.Contains(x.Key)) ||
            !Enum.IsDefined(request.DefenseProfile) ||
            !Enum.IsDefined(request.BaseRollMode) ||
            request.CriticalThreatMinimum is < 2 or > 20 ||
            (request.AttackerConditionIds?.Count ?? 0) > 100 ||
            (request.DefenderConditionIds?.Count ?? 0) > 100)
            return Failure("invalid-input", "Attack context or defense contributions are invalid.");

        var byAttacker = NativeConditionEffects.RollMode(catalog,
            request.AttackerConditionIds ?? [],
            new NativeRollContext("attack-roll", "attacks-by-affected-creature"),
            request.AttackerFacts);
        var againstDefender = NativeConditionEffects.RollMode(catalog,
            request.DefenderConditionIds ?? [],
            new NativeRollContext("attack-roll", "attacks-against-affected-creature"),
            request.DefenderFacts);

        var issues = byAttacker.Issues.Concat(againstDefender.Issues).ToArray();
        if (byAttacker.Status != "resolved" || againstDefender.Status != "resolved")
            return Failure("unsupported", "Condition-based attack-roll modifiers are unresolved.",
                issues);

        // Compose incomingAttackType from the selected attack procedure, not
        // from caller-provided facts that could contradict the procedure.
        var qualifiers = request.DefenderFacts?.Qualifiers is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(request.DefenderFacts.Qualifiers,
                StringComparer.Ordinal);
        if (qualifiers.TryGetValue("incomingAttackType", out var existing) &&
            existing != request.AttackKind)
            return Failure("invalid-input", "Incoming attack facts contradict attack kind.");
        qualifiers["incomingAttackType"] = request.AttackKind;
        var defenseFacts = new NativeEffectContext(
            request.DefenderFacts?.Flags, qualifiers,
            request.DefenderFacts?.SourceDamageTypes);

        NativeConditionCalculation defense;
        try
        {
            defense = NativeConditionEffects.ArmorClass(catalog,
                request.DefenderConditionIds ?? [], request.DefenseProfile,
                request.DefenderContributions, defenseFacts);
        }
        catch (OverflowException)
        {
            return Failure("invalid-input", "Armor Class exceeds the supported numeric range.");
        }
        if (defense.Result.Status != "resolved" || defense.Result.Value is null)
            return Failure("unsupported", "Condition-based Armor Class is unresolved.",
                defense.Issues);

        var advantageous = request.BaseRollMode == NativeRollMode.Advantage ||
            byAttacker.Mode == NativeRollMode.Advantage ||
            againstDefender.Mode == NativeRollMode.Advantage;
        var disadvantageous = request.BaseRollMode == NativeRollMode.Disadvantage ||
            byAttacker.Mode == NativeRollMode.Disadvantage ||
            againstDefender.Mode == NativeRollMode.Disadvantage;
        if (request.BaseRollMode == NativeRollMode.Emphasis &&
            (advantageous || disadvantageous))
            return Failure("unsupported",
                "Combining Emphasis with Advantage or Disadvantage has no approved rule.");

        var mode = request.BaseRollMode == NativeRollMode.Emphasis
            ? NativeRollMode.Emphasis
            : advantageous == disadvantageous
                ? NativeRollMode.Normal
                : advantageous ? NativeRollMode.Advantage : NativeRollMode.Disadvantage;

        return Check(defense.Result.Value.Value, request.AttackBonus, mode,
            request.FirstDie, request.SecondDie, request.CriticalThreatMinimum);
    }

    /// <summary>
    /// Check an already verified effective AC and roll mode, without catalog
    /// access. This low-level operation never applies a hit-point change.
    /// </summary>
    public static NativeCombatAttackResult Check(
        int armorClass, int attackBonus, NativeRollMode mode,
        int firstDie, int? secondDie = null, int criticalThreatMinimum = 20)
    {
        if (!Enum.IsDefined(mode) || criticalThreatMinimum is < 2 or > 20)
            return Failure("invalid-input", "Invalid roll mode or threat range.");
        var roll = NativeRolls.Resolve(mode, firstDie, secondDie);
        if (roll.Status != "resolved" || roll.Selected is null)
            return Failure(roll.Status, "Roll requires valid or additional natural dice.");
        var face = roll.Selected.Value;
        int total;
        try { total = checked(face + attackBonus); }
        catch (OverflowException)
        {
            return Failure("invalid-input", "Attack total exceeds the supported numeric range.");
        }

        // Natural 1 and natural 20 require the table's critical/failure policy.
        // An expanded threat range requires that the non-20 roll first hit AC.
        // Do not silently resolve critical damage, stacking or GM consequences.
        if (face == 1 || face == 20 ||
            (face >= criticalThreatMinimum && total >= armorClass))
            return new("critical-resolution-required", face != 1, total,
                armorClass, face, mode, [],
                "Resolve critical outcome and optional escalation before applying damage.");

        return new("resolved", total >= armorClass, total,
            armorClass, face, mode, []);
    }

    private static NativeCombatAttackResult Failure(
        string status, string reason, IReadOnlyList<NativeIssue>? issues = null) =>
        new(status, null, null, null, null, null, issues ?? [], reason);
}
