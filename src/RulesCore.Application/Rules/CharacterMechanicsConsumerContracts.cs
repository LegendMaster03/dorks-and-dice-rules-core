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
    bool IsApplicableUnderRuleset,
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
    IReadOnlyList<CharacterMechanicSourceAttributionView> SourceAttributions);

public sealed record CharacterMechanicApplicabilityView(
    string Kind,
    bool RequiresCharacterState,
    IReadOnlyList<string> EditionKeys,
    string? SourcePackageKey);

public sealed record CharacterMechanicInputView(
    string Key,
    string ValueKind,
    string Origin,
    bool Required,
    bool ParticipatesInValue,
    int? DefaultInteger);

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

public sealed record CharacterMechanicConditionalRollRuleView(
    string Key,
    string BooleanInputKey,
    bool WhenValue,
    string RollMode,
    IReadOnlyList<string> TargetMechanicKeys);

public sealed record CharacterMechanicBooleanRequirementView(
    string InputKey,
    bool ExpectedValue);

public sealed record CharacterMechanicSourceAttributionView(
    string PackageKey,
    string PackageDisplayName,
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
    IReadOnlyList<CharacterMechanicModifierInput>? Modifiers = null);

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

    Task<CharacterMechanicEvaluationView?> EvaluateCampaignAsync(
        Guid campaignId,
        string mechanicKey,
        CharacterMechanicEvaluationRequest request,
        string userId,
        CancellationToken cancellationToken = default);
}
