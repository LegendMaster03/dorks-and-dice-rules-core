namespace RulesCore.Mechanics;

/// <summary>
/// Competency ranks are the progression: 0 Untrained, 1 Apprentice,
/// 2 Journeyman, 3 Expert, 4 Artisan, 5 Master, 6 Grandmaster.
/// The named milestones do not create a second progression track.
/// Higher accumulated ranks retain Grandmaster capability without changing
/// the separate character-grant aggregation or imposing a new gameplay cap.
/// </summary>
public static class NativeCompetencyRankNames
{
    public static string? ForRank(int rank) => rank switch
    {
        0 => "Untrained",
        1 => "Apprentice",
        2 => "Journeyman",
        3 => "Expert",
        4 => "Artisan",
        5 => "Master",
        >= 6 and <= 1_000_000 => "Grandmaster",
        _ => null
    };
}

/// <summary>
/// Only the six recognized magical item rarities are covered. Ordinary
/// nonmagical work has no implicit rarity or Competency rank assignment.
/// </summary>
public enum NativeCraftingItemRarity
{
    Common = 1,
    Uncommon = 2,
    Rare = 3,
    VeryRare = 4,
    Legendary = 5,
    Artifact = 6
}

/// <summary>
/// Inputs supplied by an authorized character/recipe/inventory workflow.
/// Knowledge can come from a fundamental technique or a specific recipe.
/// Materials include all required ingredients and essence; neither requirement
/// is inferred from rarity. An unknown input remains unresolved, never true.
/// </summary>
public sealed record NativeCraftingQualificationRequest(
    string? ItemRarity,
    int? CrafterRanks,
    bool? HasRequiredKnowledge,
    bool? HasRequiredMaterials,
    string? RequiredSpecializationId = null,
    IReadOnlyCollection<string>? KnownSpecializationIds = null);

public sealed record NativeCraftingQualificationResult(
    string Status,
    bool? Eligible,
    int? MinimumRanks,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Checks only that an instructor's earned Competency ranks permit teaching the
/// requested rank. It does not grant training, bypass advancement costs,
/// authorize an NPC, or infer mastery from an NPC stat block.
/// </summary>
public sealed record NativeProfessionalTrainingRequest(
    int? InstructorRanks,
    int? TargetRanks);

public sealed record NativeProfessionalTrainingResult(
    string Status, bool? Eligible, string? Reason = null);

/// <summary>
/// Pure capability gates for crafting and instruction. No source or character
/// ownership is established here. The caller must verify that ranks, recipe
/// access, inventory and training authority are genuine before evaluating.
/// This utility does not perform harvesting, manufacture or resource mutation.
/// </summary>
public static class NativeCraftingQualification
{
    private const int TransportRankLimit = 1_000_000;

    public static NativeCraftingQualificationResult Evaluate(
        NativeCraftingQualificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var missing = new List<string>();
        var denied = new List<string>();
        int? required = null;

        if (TryParseRarity(request.ItemRarity, out var rarity))
            required = (int)rarity;
        else
            missing.Add("unknown-item-rarity");

        // Rank 0 is a valid Untrained state, but qualifies for no magic
        // item rarity. Null means unknown, not Untrained.
        if (request.CrafterRanks is not int ranks ||
            ranks is < 0 or > TransportRankLimit)
            missing.Add("competency-rank-unresolved");
        else if (required is int minimum && ranks < minimum)
            denied.Add("competency-rank-below-item-rarity");

        if (request.HasRequiredKnowledge is null)
            missing.Add("recipe-knowledge-unresolved");
        else if (!request.HasRequiredKnowledge.Value)
            denied.Add("recipe-knowledge-missing");

        if (request.HasRequiredMaterials is null)
            missing.Add("materials-unresolved");
        else if (!request.HasRequiredMaterials.Value)
            denied.Add("required-materials-missing");

        // Specialized formulas are still ordinary recipes. A specialization
        // does not grant a new rank or trait, and absent requirements are not
        // treated as a hidden specialization restriction.
        if (request.RequiredSpecializationId is { } specialization)
        {
            if (string.IsNullOrWhiteSpace(specialization) ||
                specialization.Length > 160)
                missing.Add("specialization-requirement-invalid");
            else if (request.KnownSpecializationIds is null)
                missing.Add("specialization-knowledge-unresolved");
            else if (!request.KnownSpecializationIds.Contains(specialization,
                         StringComparer.Ordinal))
                denied.Add("required-specialization-missing");
        }

        if (denied.Count > 0)
            return new("ineligible", false, required, denied.Concat(missing).ToArray());
        if (missing.Count > 0)
            return new("unresolved", null, required, missing);
        return new("eligible", true, required, []);
    }

    public static NativeProfessionalTrainingResult EvaluateTraining(
        NativeProfessionalTrainingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.InstructorRanks is not int instructor ||
            instructor is < 0 or > TransportRankLimit ||
            request.TargetRanks is not int target ||
            target is < 1 or > 6)
            return new("unresolved", null, "competency-rank-unresolved");

        // Untrained instructors (rank 0) cannot teach rank 1 or beyond.
        // Training to rank 0 is not an advancement, so it is not a target.
        return target <= instructor
            ? new("eligible", true)
            : new("ineligible", false, "instructor-rank-below-target");
    }

    public static bool TryParseRarity(
        string? value, out NativeCraftingItemRarity rarity)
    {
        rarity = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 40 ||
            !value.All(c => char.IsLetter(c) || c is ' ' or '-'))
            return false;
        var compact = new string(value.Where(c => c is not (' ' or '-')).ToArray());
        return Enum.TryParse(compact, ignoreCase: true, out rarity) &&
            Enum.IsDefined(rarity);
    }
}
