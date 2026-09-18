namespace RulesCore.Application.Rules;

public sealed record CharacterMechanicsCatalogView(
    string Scope,
    Guid? CampaignId,
    int? RevisionNumber,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<CharacterMechanicView> Mechanics);

public sealed record CharacterMechanicView(
    string MechanicKey,
    string Kind,
    string DisplayName,
    string? ConceptKey,
    Guid? RuleConceptId,
    bool IsAvailableUnderRuleset,
    CharacterMechanicApplicabilityView Applicability,
    string EvaluationKind,
    bool CanEvaluate,
    int Constant,
    string? TargetInputKey,
    string? BaseMechanicKey,
    IReadOnlyList<CharacterMechanicInputView> Inputs,
    IReadOnlyList<CharacterMechanicRelationshipView> Relationships,
    IReadOnlyList<CharacterMechanicConditionalRollRuleView> ConditionalRollRules,
    IReadOnlyList<CharacterMechanicBooleanRequirementView> BooleanRequirements,
    CharacterMechanicCheckView? Check,
    CharacterCompetencyDefinitionView? Competency,
    IReadOnlyList<CharacterMechanicSourceAttributionView> SourceAttributions);

public sealed record CharacterMechanicApplicabilityView(
    string Kind,
    bool RequiresCharacterState,
    IReadOnlyList<string> RequiredCapabilityKeys,
    string? SourcePackageKey);

public sealed record CharacterMechanicInputView(
    string Key,
    string ValueKind,
    string Origin,
    bool Required,
    bool ParticipatesInValue,
    int? DefaultInteger,
    string? IncludeWhenBooleanInputKey,
    bool? IncludeWhenBooleanValue);

public sealed record CharacterCheckAbilityView(
    string ResolutionKind,
    string? FixedAbilityKey,
    IReadOnlyList<string> AllowedAbilityKeys);

public sealed record CharacterCheckCompetencyView(
    string ResolutionKind,
    IReadOnlyList<string> AllowedCompetencyKinds,
    string? FixedConceptKey);

public sealed record CharacterMechanicCheckView(
    CharacterCheckAbilityView Ability,
    CharacterCheckCompetencyView Competency);

public sealed record CharacterCompetencyProfileView(
    Guid SourceEntityRevisionId,
    string ProfileKey,
    IReadOnlyList<string> RequiredCapabilityKeys,
    string CompetencyKind,
    string? FamilyName,
    string? Specialty,
    string? GoverningAbilityKey,
    bool SupportsRanks,
    bool SupportsClassSkillState,
    bool SupportsTrainingState,
    bool? TrainedOnly,
    bool? ArmorCheckPenaltyApplies,
    string? GameEdition);

public sealed record CharacterCompetencyDefinitionView(
    string CompetencyKind,
    string? FamilyName,
    string? Specialty,
    string? GoverningAbilityKey,
    bool SupportsRanks,
    bool SupportsClassSkillState,
    bool SupportsTrainingState,
    bool? TrainedOnly,
    bool? ArmorCheckPenaltyApplies,
    IReadOnlyList<CharacterCompetencyProfileView> Profiles);

public sealed record CharacterMechanicRelationshipView(
    string RelationshipKey,
    string Kind,
    string ParentMechanicKey,
    IReadOnlyList<string> ComponentMechanicKeys,
    string Composition,
    string Direction,
    string? EffectiveResolutionKind,
    bool CanResolve,
    IReadOnlyList<string> MissingMechanicKeys);

public sealed record CharacterMechanicBooleanConditionView(
    string InputKey,
    bool ExpectedValue);

public sealed record CharacterMechanicConditionalRollRuleView(
    string Key,
    IReadOnlyList<CharacterMechanicBooleanConditionView> Conditions,
    string RollMode,
    IReadOnlyList<string> TargetMechanicKeys);

public sealed record CharacterMechanicBooleanRequirementView(
    string InputKey,
    bool ExpectedValue);

public sealed record CharacterMechanicSourceAttributionView(
    string? PackageKey,
    string? PackageDisplayName,
    string Provider,
    string? SourceCode,
    int? SourceRevisionNumber,
    string? WorkKey,
    string? WorkDisplayName,
    string? GameEdition,
    string? ReleaseKind,
    DateOnly? PublicationDate,
    string? ReferenceKey,
    string? ReferenceTitle,
    string? ReferenceUri,
    bool PresentationRequired,
    bool ReferenceLinkRequired);

public sealed record CharacterMechanicModifierInput(
    string TargetConceptKey,
    int Value);

public sealed record CharacterMechanicEvaluationRequest(
    Dictionary<string, int>? IntegerInputs = null,
    Dictionary<string, bool>? BooleanInputs = null,
    Dictionary<string, string>? StringInputs = null,
    IReadOnlyList<CharacterMechanicModifierInput>? Modifiers = null,
    IReadOnlyList<string>? CapabilityKeys = null);

public sealed record CharacterMechanicBatchEvaluationItemRequest(
    string MechanicKey,
    CharacterMechanicEvaluationRequest Evaluation);

public sealed record CharacterMechanicsBatchEvaluationRequest(
    IReadOnlyList<CharacterMechanicBatchEvaluationItemRequest> Evaluations);

public sealed record CharacterMechanicBatchEvaluationItemView(
    string MechanicKey,
    CharacterMechanicEvaluationView? Evaluation);

public sealed record CharacterMechanicsBatchEvaluationView(
    string Scope,
    Guid? CampaignId,
    int? RevisionNumber,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<CharacterMechanicBatchEvaluationItemView> Evaluations);

public sealed record CharacterMechanicAppliedRollRuleView(
    string Key,
    string RollMode,
    IReadOnlyList<string> TargetMechanicKeys);

public sealed record CharacterMechanicEvaluationView(
    string MechanicKey,
    string EvaluationKind,
    int Value,
    int? Target,
    bool? MeetsTarget,
    bool RequirementsSatisfied,
    IReadOnlyList<string> UnsatisfiedRequirementKeys,
    IReadOnlyList<CharacterMechanicAppliedRollRuleView> AppliedRollRules);

public interface ICharacterMechanicsConsumerService
{
    Task<CharacterMechanicsCatalogView> GetGlobalAsync(
        string? userId,
        bool includeUnavailable = false,
        CancellationToken cancellationToken = default);

    Task<CharacterMechanicsCatalogView> GetCampaignAsync(
        Guid campaignId,
        string userId,
        bool includeUnavailable = false,
        CancellationToken cancellationToken = default);

    Task<CharacterMechanicEvaluationView?> EvaluateGlobalAsync(
        string mechanicKey,
        CharacterMechanicEvaluationRequest request,
        string? userId,
        CancellationToken cancellationToken = default);

    Task<CharacterMechanicsBatchEvaluationView> EvaluateGlobalBatchAsync(
        CharacterMechanicsBatchEvaluationRequest request,
        string? userId,
        CancellationToken cancellationToken = default);

    Task<CharacterMechanicEvaluationView?> EvaluateCampaignAsync(
        Guid campaignId,
        string mechanicKey,
        CharacterMechanicEvaluationRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<CharacterMechanicsBatchEvaluationView> EvaluateCampaignBatchAsync(
        Guid campaignId,
        CharacterMechanicsBatchEvaluationRequest request,
        string userId,
        CancellationToken cancellationToken = default);
}
