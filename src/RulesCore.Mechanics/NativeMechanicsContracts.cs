namespace RulesCore.Mechanics;

/// <summary>All identities are native IDs; no legacy rule-concept aliasing.</summary>
public sealed record NativeAdvancement(string ClassId, int Level, string OccurrenceId);
public sealed record NativeSkillTraining(int Ranks = 0, bool Proficient = false,
    int MiscModifier = 0, string? Ability = null);
public sealed record NativeCharacterRequest(
    string RulesetId,
    string Fingerprint,
    IReadOnlyDictionary<string, int> AbilityScores,
    IReadOnlyList<NativeAdvancement> Advancements,
    IReadOnlyDictionary<string, NativeSkillTraining>? Skills = null,
    IReadOnlyDictionary<string, NativeSkillTraining>? Competencies = null,
    IReadOnlyDictionary<string, int>? DefenseContributions = null,
    IReadOnlyList<string>? SelectedOptions = null,
    IReadOnlyDictionary<string, int>? CurrentResources = null,
    IReadOnlyList<string>? ConditionIds = null,
    IReadOnlyDictionary<string, int>? HitPointGains = null,
    int? BaseWalkSpeed = null);

/// <summary>Null value is never a calculated zero; it denotes an unavailable result.</summary>
public sealed record NativeNumeric(string Status, int? Value = null, string? Reason = null);
public sealed record NativeIssue(string Code, string Path, string Detail);
public sealed record NativeResource(string Id, int Maximum, int? Current,
    string Recovery, int? SpellLevel = null);
public sealed record NativeCharacterProjection(
    int ContractVersion,
    string RulesetId,
    string Fingerprint,
    string Status,
    IReadOnlyDictionary<string, NativeNumeric> Values,
    IReadOnlyList<string> FeatureIds,
    IReadOnlyList<NativeResource> Resources,
    IReadOnlyList<string> RequiredChoices,
    IReadOnlyList<NativeIssue> Issues);

public enum NativeRollMode { Normal, Advantage, Disadvantage, Emphasis }
public sealed record NativeRollResolution(string Status, int? Selected, string? EffectKey = null);

/// <summary>Input dice are supplied by an authorized roller; the kernel never guesses rolls.</summary>
public static class NativeRolls
{
    public static NativeRollResolution Resolve(NativeRollMode mode, int first, int? second = null,
        string? applicableCancellationEffect = null)
    {
        if (first is < 1 or > 20 || (second is not null && second is < 1 or > 20))
            return new("invalid-dice", null);
        if (mode == NativeRollMode.Normal)
            return second is null ? new("resolved", first) : new("unexpected-die", null);
        if (second is null) return new("roll-required", null);
        return mode switch
        {
            NativeRollMode.Advantage => new("resolved", Math.Max(first, second.Value)),
            NativeRollMode.Disadvantage => new("resolved", Math.Min(first, second.Value)),
            NativeRollMode.Emphasis when first + second.Value == 21 =>
                applicableCancellationEffect is null
                    ? new("reroll-required", null)
                    : new("cancellation-effect", null, applicableCancellationEffect),
            NativeRollMode.Emphasis => new("resolved", Extreme(first, second.Value)),
            _ => new("unsupported", null)
        };
    }

    private static int Extreme(int a, int b)
    {
        static int Distance(int die) => Math.Min(die - 1, 20 - die);
        // Equal distances on non-cancelling dice have the same face value.
        return Distance(a) <= Distance(b) ? a : b;
    }
}

public enum NativeDefenseProfile { Normal, Touch, FlatFooted, TouchFlatFooted }
public static class NativeDefenses
{
    /// <summary>Approved defense contract: negative Dexterity always persists.</summary>
    public static int Evaluate(NativeDefenseProfile profile,
        IReadOnlyDictionary<string, int> contributions, int baseArmorClass = 10)
    {
        var touch = profile is NativeDefenseProfile.Touch or NativeDefenseProfile.TouchFlatFooted;
        var flat = profile is NativeDefenseProfile.FlatFooted or NativeDefenseProfile.TouchFlatFooted;
        var result = baseArmorClass;
        foreach (var pair in contributions)
        {
            if (touch && pair.Key is "armor" or "shield" or "natural-armor") continue;
            if (flat && pair.Key == "dodge") continue;
            result = checked(result + (flat && pair.Key == "dexterity"
                ? Math.Min(0, pair.Value) : pair.Value));
        }
        return result;
    }
}
