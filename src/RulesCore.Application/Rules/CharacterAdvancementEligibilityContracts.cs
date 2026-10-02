namespace RulesCore.Application.Rules;

public static class CharacterAdvancementEligibilityStates
{
    public const string Eligible = "eligible";
    public const string Ineligible = "ineligible";
    public const string Unresolved = "unresolved";
}

/// <summary>
/// Evaluates whether a Character may acquire an effective Subclass or Prestige Class.
/// The Character payload uses the same normalized public projection contract used by
/// Character mechanics so consumers do not maintain a second representation of state.
/// </summary>
public sealed record CharacterAdvancementEligibilityRequest(
    string CandidateConceptKey,
    CharacterRulesProjectionRequest Character,
    string? ParentAdvancementOccurrenceKey = null);

public sealed record CharacterAdvancementParentRequirementView(
    string ConceptKey,
    string DisplayName,
    int? RequiredLevel,
    int? CurrentLevel,
    bool? Satisfied,
    string State,
    string? Reason = null);

public sealed record CharacterAdvancementEligibilityView(
    string Scope,
    Guid? CampaignId,
    string CandidateConceptKey,
    string CandidateDisplayName,
    string CandidateKind,
    string State,
    bool? Eligible,
    CharacterAdvancementParentRequirementView? ParentClass,
    CharacterPrerequisiteView? Prerequisites,
    IReadOnlyList<CharacterProjectionConflictView> Conflicts);

public interface ICharacterAdvancementEligibilityService
{
    Task<CharacterAdvancementEligibilityView?> EvaluateGlobalAsync(
        CharacterAdvancementEligibilityRequest request,
        string? userId,
        CancellationToken cancellationToken = default);

    Task<CharacterAdvancementEligibilityView?> EvaluateCampaignAsync(
        Guid campaignId,
        CharacterAdvancementEligibilityRequest request,
        string userId,
        CancellationToken cancellationToken = default);
}
