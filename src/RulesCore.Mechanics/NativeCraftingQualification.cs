namespace RulesCore.Mechanics;

/// <summary>
/// Named mastery milestones for crafting qualification. These are not the
/// numeric Competency ranks stored in the current character training profile.
/// The conversion from earned ranks to these milestones requires a separately
/// approved progression and must be performed by an authorized caller.
/// </summary>
public enum NativeCompetencyMasteryTier
{
    Apprentice = 1,
    Journeyman = 2,
    Expert = 3,
    Artisan = 4,
    Master = 5,
    Grandmaster = 6
}

/// <summary>
/// Only the six recognized magical item rarities are covered. Ordinary
/// nonmagical work has no implicit rarity or mastery-tier assignment.
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
    NativeCompetencyMasteryTier? CrafterTier,
    bool? HasRequiredKnowledge,
    bool? HasRequiredMaterials);

public sealed record NativeCraftingQualificationResult(
    string Status,
    bool? Eligible,
    NativeCompetencyMasteryTier? MinimumTier,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Checks only that an instructor's earned mastery permits teaching the
/// requested tier. It does not grant training, bypass advancement costs,
/// authorize an NPC, or infer mastery from an NPC stat block.
/// </summary>
public sealed record NativeProfessionalTrainingRequest(
    NativeCompetencyMasteryTier? InstructorTier,
    NativeCompetencyMasteryTier? TargetTier);

public sealed record NativeProfessionalTrainingResult(
    string Status, bool? Eligible, string? Reason = null);

/// <summary>
/// Pure capability gates for crafting and instruction. No source or character
/// ownership is established here. The caller must verify that mastery, recipe
/// access, inventory and training authority are genuine before evaluating.
/// This utility does not perform harvesting, manufacture or resource mutation.
/// </summary>
public static class NativeCraftingQualification
{
    public static NativeCraftingQualificationResult Evaluate(
        NativeCraftingQualificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var missing = new List<string>();
        var denied = new List<string>();
        NativeCompetencyMasteryTier? required = null;

        if (TryParseRarity(request.ItemRarity, out var rarity))
            required = (NativeCompetencyMasteryTier)(int)rarity;
        else
            missing.Add("unknown-item-rarity");

        if (request.CrafterTier is not { } tier ||
            !Enum.IsDefined(tier))
            missing.Add("unknown-competency-mastery-tier");
        else if (required is { } minimum && tier < minimum)
            denied.Add("competency-tier-below-item-rarity");

        if (request.HasRequiredKnowledge is null)
            missing.Add("recipe-knowledge-unresolved");
        else if (!request.HasRequiredKnowledge.Value)
            denied.Add("recipe-knowledge-missing");

        if (request.HasRequiredMaterials is null)
            missing.Add("materials-unresolved");
        else if (!request.HasRequiredMaterials.Value)
            denied.Add("required-materials-missing");

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

        if (request.InstructorTier is not { } instructor ||
            request.TargetTier is not { } target ||
            !Enum.IsDefined(instructor) || !Enum.IsDefined(target))
            return new("unresolved", null, "mastery-tier-unresolved");

        return target <= instructor
            ? new("eligible", true)
            : new("ineligible", false, "instructor-tier-below-target");
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
