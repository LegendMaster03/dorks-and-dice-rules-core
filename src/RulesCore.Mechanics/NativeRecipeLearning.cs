namespace RulesCore.Mechanics;

/// <summary>
/// Warcraft-inspired recipe acquisition paths, expressed as generic source
/// categories. Each recipe retains its actual source identity and requirements.
/// No source catalog or recipes are bundled by this contract.
/// </summary>
public enum NativeRecipeAcquisitionKind
{
    FundamentalAdvancement,
    Trainer,
    Research,
    Discovery,
    Treasure,
    QuestReward,
    RenownReward,
    Purchase
}

/// <summary>
/// A proposal to learn a source-defined recipe or technique. The caller must
/// verify the character's earned rank, the recipe classification and all
/// source entitlement before setting HasAuthorizedSource to true.
/// </summary>
public sealed record NativeRecipeLearningRequest(
    string CompetencyId,
    string RecipeId,
    int? LearnerRanks,
    int MinimumRanks,
    bool IsFundamental,
    NativeRecipeAcquisitionKind Acquisition,
    bool? HasAuthorizedSource,
    int? InstructorRanks = null,
    bool? InstructorKnowsRecipe = null,
    string? RequiredSpecializationId = null,
    IReadOnlyCollection<string>? KnownSpecializationIds = null);

public sealed record NativeRecipeLearningResult(
    string Status, bool? CanLearn, IReadOnlyList<string> Reasons);

/// <summary>
/// Pure recipe-knowledge eligibility; it does not award, store or publish a
/// recipe and never creates a competency rank, magic item or trait.
/// </summary>
public static class NativeRecipeLearning
{
    public static NativeRecipeLearningResult Evaluate(
        NativeRecipeLearningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var missing = new List<string>();
        var denied = new List<string>();
        if (string.IsNullOrWhiteSpace(request.CompetencyId) ||
            request.CompetencyId.Length > 160 ||
            string.IsNullOrWhiteSpace(request.RecipeId) ||
            request.RecipeId.Length > 160 ||
            request.MinimumRanks is < 0 or > 1_000_000 ||
            !Enum.IsDefined(request.Acquisition))
            return new("unresolved", null, ["invalid-recipe-learning-contract"]);

        if (request.LearnerRanks is not int ranks ||
            ranks is < 0 or > 1_000_000)
            missing.Add("learner-rank-unresolved");
        else if (ranks < request.MinimumRanks)
            denied.Add("learner-rank-below-recipe-requirement");

        if (request.HasAuthorizedSource is null)
            missing.Add("learning-source-unresolved");
        else if (!request.HasAuthorizedSource.Value)
            denied.Add("learning-source-not-authorized");

        if (request.Acquisition == NativeRecipeAcquisitionKind.FundamentalAdvancement &&
            !request.IsFundamental)
            denied.Add("specialized-recipe-not-learned-automatically");

        if (request.Acquisition == NativeRecipeAcquisitionKind.Trainer)
        {
            if (request.InstructorRanks is not int trainerRank ||
                trainerRank is < 0 or > 1_000_000)
                missing.Add("instructor-rank-unresolved");
            else if (trainerRank < request.MinimumRanks)
                denied.Add("instructor-rank-below-recipe-requirement");

            if (request.InstructorKnowsRecipe is null)
                missing.Add("instructor-knowledge-unresolved");
            else if (!request.InstructorKnowsRecipe.Value)
                denied.Add("instructor-does-not-know-recipe");
        }

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
            return new("ineligible", false, denied.Concat(missing).ToArray());
        if (missing.Count > 0)
            return new("unresolved", null, missing);
        return new("eligible", true, []);
    }
}
