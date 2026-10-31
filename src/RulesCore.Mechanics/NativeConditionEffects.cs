using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Evaluates the reviewed numeric subset of native condition effects. It does not
/// silently interpret text, source-specific predicates, or arbitrary roll grants.
/// The caller must retain the returned issues alongside any resolved values.
/// </summary>
public sealed record NativeConditionCalculation(NativeNumeric Result, IReadOnlyList<NativeIssue> Issues);

/// <summary>
/// A roll is evaluated from the affected creature's point of view. Attack roles
/// are explicit so attacks BY and AGAINST the creature are never conflated.
/// </summary>
public sealed record NativeRollContext(string AppliesTo, string Subject, string? Ability = null);

/// <summary>
/// Explicit facts about one roll or attack. Absent facts mean unknown, not false.
/// Native predicate keys are whitelisted and never evaluated as arbitrary expressions.
/// </summary>
public sealed record NativeEffectContext(
    IReadOnlyDictionary<string, bool>? Flags = null,
    IReadOnlyDictionary<string, string>? Qualifiers = null,
    IReadOnlyList<string>? SourceDamageTypes = null);

public sealed record NativeConditionRollMode(string Status, NativeRollMode? Mode,
    IReadOnlyList<NativeIssue> Issues);

public static class NativeConditionEffects
{
    public static NativeConditionRollMode RollMode(NativeCatalogSnapshot catalog,
        IReadOnlyList<string> conditionIds, NativeRollContext context,
        NativeEffectContext? facts = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(conditionIds);
        ArgumentNullException.ThrowIfNull(context);
        var issues = new List<NativeIssue>();
        if (context.AppliesTo is not ("attack-roll" or "ability-check" or "saving-throw") ||
            context.Subject is not ("affected-creature" or "attacks-by-affected-creature" or
                "attacks-against-affected-creature" or "charmer"))
        {
            issues.Add(new("roll-context-invalid", "roll", "Roll type and subject must be explicit."));
            return new("invalid-input", null, issues);
        }
        var advantage = false;
        var disadvantage = false;
        foreach (var (id, effect) in Effects(catalog, conditionIds, issues))
        {
            if (ReadString(effect, "type") != "roll-mode" ||
                ReadString(effect, "appliesTo") != context.AppliesTo) continue;
            var subject = ReadString(effect, "subject");
            if (subject is not null)
            {
                if (subject != context.Subject) continue;
            }
            else if (context.Subject != "affected-creature" &&
                     !(context.AppliesTo == "attack-roll" &&
                       context.Subject == "attacks-by-affected-creature"))
            {
                continue;
            }
            var ability = ReadString(effect, "ability");
            if (ability is not null && string.IsNullOrWhiteSpace(context.Ability))
            {
                issues.Add(new("roll-ability-required", id,
                    "The native roll effect requires an explicit saving throw or check ability."));
                continue;
            }
            if (ability is not null && ability != context.Ability) continue;
            var match = Predicate(effect, facts);
            if (match is null)
            {
                issues.Add(new("conditional-roll-unresolved", id,
                    "The roll predicate is unsupported or its required event facts are missing."));
                continue;
            }
            if (match == false) continue;
            switch (ReadString(effect, "mode"))
            {
                case "advantage": advantage = true; break;
                case "disadvantage": disadvantage = true; break;
                default:
                    issues.Add(new("roll-mode-unsupported", id,
                        "Unknown native roll modifier."));
                    break;
            }
        }
        if (issues.Count > 0)
            return new("unsupported", null, issues);
        return new("resolved", advantage == disadvantage ? NativeRollMode.Normal :
            advantage ? NativeRollMode.Advantage : NativeRollMode.Disadvantage, issues);
    }

    public static NativeConditionCalculation ArmorClass(NativeCatalogSnapshot catalog,
        IReadOnlyList<string> conditionIds, NativeDefenseProfile profile,
        IReadOnlyDictionary<string, int> contributions, NativeEffectContext? facts = null)
    {
        var issues = new List<NativeIssue>();
        var effective = new Dictionary<string, int>(contributions, StringComparer.Ordinal);
        var additive = new List<int>();
        var groups = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (id, effect) in Effects(catalog, conditionIds, issues))
        {
            if (ReadString(effect, "type") != "defense-adjustment") continue;
            var match = Predicate(effect, facts);
            if (match is null)
            {
                issues.Add(new("conditional-defense-unresolved", id,
                    "The defense predicate is unsupported or its required event facts are missing."));
                continue;
            }
            if (match == false) continue;
            switch (ReadString(effect, "operation"))
            {
                case "suppress-contributions":
                    if (!effect.TryGetProperty("contributions", out var suppressed) ||
                        suppressed.ValueKind != JsonValueKind.Array)
                    {
                        issues.Add(new("defense-effect-unsupported", id, "Invalid contribution list."));
                        break;
                    }
                    foreach (var member in suppressed.EnumerateArray())
                    {
                        var part = member.ValueKind == JsonValueKind.String ? member.GetString() : null;
                        switch (part)
                        {
                            case "positive-dexterity":
                                effective["dexterity"] = Math.Min(0, effective.GetValueOrDefault("dexterity"));
                                break;
                            case "dodge":
                                effective["dodge"] = 0;
                                break;
                            default:
                                issues.Add(new("defense-effect-unsupported", id,
                                    "Unrecognized AC contribution suppression."));
                                break;
                        }
                    }
                    break;
                case "add-modifier" when ReadString(effect, "defense") == "armor-class" &&
                    ReadInt(effect, "value") is int modifier:
                    var group = ReadString(effect, "nonStackingGroup");
                    if (group is null) additive.Add(modifier);
                    else if (!groups.TryGetValue(group, out var existing) ||
                             Math.Abs(modifier) > Math.Abs(existing)) groups[group] = modifier;
                    break;
                default:
                    issues.Add(new("defense-effect-unsupported", id,
                        "No implemented native arithmetic for this defense effect."));
                    break;
            }
        }
        if (additive.Count > 1)
            issues.Add(new("defense-stacking-unresolved", "armor-class",
                "Multiple ungrouped condition modifiers need a stacking decision."));
        if (issues.Count != 0)
            return new(new("unsupported", null, "Condition defense calculation is incomplete."), issues);
        var baseValue = NativeDefenses.Evaluate(profile, effective);
        return new(new("resolved", checked(baseValue + additive.Sum() + groups.Values.Sum())), issues);
    }

    public static NativeConditionCalculation WalkSpeed(NativeCatalogSnapshot catalog,
        IReadOnlyList<string> conditionIds, int baseSpeed,
        NativeEffectContext? facts = null)
    {
        var issues = new List<NativeIssue>();
        if (baseSpeed < 0) return new(new("invalid-input", null, "Base speed can not be negative."),
            [new("invalid-speed", "movement.walk", "Negative speed.")]);
        var value = (double)baseSpeed;
        var forcedZero = false;
        foreach (var (id, effect) in Effects(catalog, conditionIds, issues))
        {
            if (ReadString(effect, "type") != "speed") continue;
            var match = Predicate(effect, facts);
            if (match is null)
            {
                issues.Add(new("conditional-speed-unresolved", id,
                    "The speed predicate is unsupported or its required event facts are missing."));
                continue;
            }
            if (match == false) continue;
            switch (ReadString(effect, "operation"))
            {
                case "set" when ReadInt(effect, "value") == 0:
                    forcedZero = true;
                    value = 0;
                    break;
                case "multiply" when effect.TryGetProperty("factor", out var factor) &&
                     factor.ValueKind == JsonValueKind.Number && factor.TryGetDouble(out var multiplier) &&
                     multiplier >= 0 && multiplier <= 1:
                    if (!forcedZero) value *= multiplier;
                    break;
                default:
                    issues.Add(new("speed-effect-unsupported", id,
                        "Speed operation is not executable from native typed data."));
                    break;
            }
        }
        if (issues.Count != 0 || !double.IsFinite(value) || value % 1 != 0 || value > int.MaxValue)
            return new(new("unsupported", null, "Speed modifiers or rounding remain unresolved."), issues);
        return new(new("resolved", (int)value), issues);
    }

    /// <summary>
    /// Three-valued predicate evaluation: true applies, false excludes, null is
    /// unknown or unsupported. Every key in a conjunction must be understood;
    /// an unrecognized key can never accidentally turn a conditional effect on.
    /// </summary>
    private static bool? Predicate(JsonElement effect, NativeEffectContext? facts)
    {
        if (!effect.TryGetProperty("when", out var when)) return true;
        if (when.ValueKind != JsonValueKind.Object) return null;
        var matched = true;
        foreach (var clause in when.EnumerateObject())
        {
            bool? result;
            switch (clause.Name)
            {
                case "fearSourceWithinLineOfSight":
                case "anchored":
                case "attackerCanSeeAffectedCreature":
                case "targetCanSeeAffectedCreature":
                    result = clause.Value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                        facts?.Flags?.TryGetValue(clause.Name, out var actualFlag) == true
                        ? actualFlag == clause.Value.GetBoolean() : null;
                    break;
                case "purpose":
                case "incomingAttackType":
                case "attackerRelationToGrappler":
                case "attackerRelationToPinner":
                case "targetRelationToGrappler":
                    result = clause.Value.ValueKind == JsonValueKind.String &&
                        facts?.Qualifiers?.TryGetValue(clause.Name, out var actualText) == true
                        ? string.Equals(actualText, clause.Value.GetString(), StringComparison.Ordinal)
                        : null;
                    break;
                case "sourceKind":
                    result = clause.Value.ValueKind == JsonValueKind.Array &&
                        facts?.Qualifiers?.TryGetValue("sourceKind", out var sourceKind) == true &&
                        clause.Value.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String)
                        ? clause.Value.EnumerateArray().Any(x => x.GetString() == sourceKind) : null;
                    break;
                case "sourceDealsDamageType":
                    result = clause.Value.ValueKind == JsonValueKind.String &&
                        facts?.SourceDamageTypes is not null
                        ? facts.SourceDamageTypes.Contains(clause.Value.GetString()!, StringComparer.Ordinal)
                        : null;
                    break;
                case "sourceDealsAnyDamageType":
                    result = clause.Value.ValueKind == JsonValueKind.Array &&
                        facts?.SourceDamageTypes is not null &&
                        clause.Value.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String)
                        ? clause.Value.EnumerateArray().Any(x =>
                            facts.SourceDamageTypes.Contains(x.GetString()!, StringComparer.Ordinal))
                        : null;
                    break;
                default:
                    result = null;
                    break;
            }
            if (result is null) return null;
            if (result == false) matched = false;
        }
        return matched;
    }

    private static IEnumerable<(string Id, JsonElement Effect)> Effects(NativeCatalogSnapshot catalog,
        IReadOnlyList<string> conditionIds, List<NativeIssue> issues)
    {
        foreach (var id in conditionIds.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(id) || !catalog.TryGetById(id, out var condition) ||
                condition is null || condition.Category is not ("condition" or "leveled-condition"))
            {
                issues.Add(new("condition-unavailable", id ?? "", "Condition is not a native identity."));
                continue;
            }
            if (condition.Category == "leveled-condition")
            {
                issues.Add(new("condition-level-required", id, "Leveled condition needs an explicit level."));
                continue;
            }
            if (!condition.Definition.TryGetProperty("effects", out var effects) ||
                effects.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new("condition-effects-unavailable", id,
                    "No typed condition effect collection."));
                continue;
            }
            foreach (var effect in effects.EnumerateArray())
            {
                if (effect.ValueKind == JsonValueKind.Object) yield return (id, effect);
                else issues.Add(new("condition-effect-invalid", id, "Effect must be an object."));
            }
        }
    }

    private static string? ReadString(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString() : null;

    private static int? ReadInt(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var element) && element.ValueKind == JsonValueKind.Number &&
        element.TryGetInt32(out var value) ? value : null;
}
