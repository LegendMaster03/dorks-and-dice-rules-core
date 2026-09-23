using RulesCore.Domain.Rules;

namespace RulesCore.Application.Rules;

public static class CharacterSupportResolutionStates
{
    public const string Applicable = "applicable";
    public const string Resolved = "resolved";
    public const string NotApplicable = "not-applicable";
    public const string UnresolvedCharacterState = "unresolved-character-state";
    public const string UnresolvedRuleDefinition = "unresolved-rule-definition";
    public const string Unavailable = "unavailable";
}

public static class CharacterRecoveryResolutionStatuses
{
    public const string Resolved = "resolved";
    public const string Unavailable = "unavailable";
    public const string NotApplicable = "not-applicable";
    public const string InputRequired = "input-required";
    public const string ChoiceRequired = "choice-required";
    public const string RollRequired = "roll-required";
}

public static class CharacterRecoveryPresentationRoles
{
    public const string ShortRest = "short-rest";
    public const string LongRest = "long-rest";
}

public sealed record CharacterSupportInputValues(
    Dictionary<string, int>? IntegerInputs = null,
    Dictionary<string, bool>? BooleanInputs = null,
    Dictionary<string, string>? StringInputs = null);

public sealed record CharacterSupportProjectionRequest(
    IReadOnlyDictionary<string, CharacterSupportInputValues>? FactsBySupportKey = null,
    IReadOnlyList<string>? CapabilityKeys = null);

public sealed record CharacterRecoveryResolutionRequest(
    Dictionary<string, int>? IntegerInputs = null,
    Dictionary<string, bool>? BooleanInputs = null,
    Dictionary<string, string>? StringInputs = null,
    IReadOnlyList<string>? CapabilityKeys = null,
    Dictionary<string, string>? Choices = null,
    Dictionary<string, int>? Rolls = null);

public sealed record CharacterRecoveryRuntimeRequirementsView(
    bool RequiresCharacterState,
    bool RequiresPlayerChoices,
    bool RequiresRolls,
    bool RequiresResourceExpenditure,
    bool RequiresOtherRuntimeFacts);

public sealed record CharacterRecoveryChoiceOptionView(
    string Key,
    string DisplayName,
    string? Value);

public sealed record CharacterRecoveryChoiceView(
    string Key,
    string Prompt,
    bool Required,
    IReadOnlyList<CharacterRecoveryChoiceOptionView> Options);

public sealed record CharacterRecoveryRollView(
    string Key,
    string RollKind,
    string Prompt,
    bool Required,
    string? MechanicKey);

public sealed record CharacterRecoveryEffectView(
    string EffectKey,
    string TargetKind,
    string TargetKey,
    string Operation,
    int? Amount,
    string? Value,
    string? ReferenceKey);

public sealed record CharacterRecoveryProcedureView(
    string ProcedureKey,
    string DisplayName,
    string? PresentationRole,
    bool IsAvailableUnderRuleset,
    string ApplicabilityState,
    CharacterMechanicApplicabilityView Applicability,
    IReadOnlyList<string> MissingCapabilityKeys,
    IReadOnlyList<CharacterMechanicInputView> Inputs,
    IReadOnlyList<CharacterRecoveryChoiceView> Choices,
    IReadOnlyList<CharacterRecoveryRollView> Rolls,
    CharacterRecoveryRuntimeRequirementsView RuntimeRequirements,
    IReadOnlyList<CharacterMechanicSourceAttributionView> SourceAttributions);

public sealed record CharacterPassiveValueView(
    string MechanicKey,
    string DisplayName,
    string? PresentationRole,
    bool IsAvailableUnderRuleset,
    string ResolutionState,
    int? Value,
    CharacterMechanicApplicabilityView Applicability,
    IReadOnlyList<string> MissingCapabilityKeys,
    IReadOnlyList<string> MissingInputKeys,
    string? RelatedMechanicKey,
    string? RelatedConceptKey,
    Guid? RelatedRuleConceptId,
    string? RelatedAbilityKey,
    IReadOnlyList<CharacterMechanicInputView> Inputs,
    IReadOnlyList<CharacterMechanicSourceAttributionView> SourceAttributions);

public sealed record CharacterSupportValueView(
    string ValueKind,
    int? IntegerValue,
    bool? BooleanValue,
    string? StringValue);

public sealed record CharacterSupportQualificationView(
    string QualificationKey,
    string DisplayName,
    string Category,
    string? Family,
    bool IsAvailableUnderRuleset,
    string ResolutionState,
    CharacterSupportValueView? State,
    CharacterMechanicApplicabilityView Applicability,
    IReadOnlyList<string> MissingCapabilityKeys,
    IReadOnlyList<string> MissingInputKeys,
    string? AssociatedConceptKey,
    Guid? AssociatedRuleConceptId,
    CharacterMechanicInputView StateInput,
    IReadOnlyList<CharacterMechanicSourceAttributionView> SourceAttributions);

public sealed record CharacterSupportProjectionView(
    string Scope,
    Guid? CampaignId,
    int? RevisionNumber,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<CharacterRecoveryProcedureView> RecoveryProcedures,
    IReadOnlyList<CharacterPassiveValueView> PassiveValues,
    IReadOnlyList<CharacterSupportQualificationView> Qualifications);

public sealed record CharacterRecoveryResolutionView(
    string Scope,
    Guid? CampaignId,
    int? RevisionNumber,
    DateTimeOffset? PublishedAt,
    string ProcedureKey,
    string DisplayName,
    string? PresentationRole,
    string Status,
    IReadOnlyList<string> MissingCapabilityKeys,
    IReadOnlyList<string> MissingInputKeys,
    IReadOnlyList<CharacterRecoveryChoiceView> PendingChoices,
    IReadOnlyList<CharacterRecoveryRollView> PendingRolls,
    IReadOnlyList<CharacterRecoveryEffectView> Consequences,
    IReadOnlyList<CharacterMechanicSourceAttributionView> SourceAttributions);

public sealed record CharacterRecoveryChoiceOptionDefinition(
    string Key,
    string DisplayName,
    string? Value);

public sealed record CharacterRecoveryChoiceDefinition(
    string Key,
    string Prompt,
    bool Required,
    IReadOnlyList<CharacterRecoveryChoiceOptionDefinition> Options);

public sealed record CharacterRecoveryRollDefinition(
    string Key,
    string RollKind,
    string Prompt,
    bool Required,
    string? MechanicKey);

public sealed record CharacterRecoveryEffectConditionDefinition(
    string? InputKey,
    int? ExpectedInteger,
    bool? ExpectedBoolean,
    string? ExpectedString,
    string? ChoiceKey,
    string? ExpectedChoiceValue);

public sealed record CharacterRecoveryEffectDefinition(
    string Key,
    string TargetKind,
    string? TargetKey,
    string? TargetChoiceKey,
    string Operation,
    int? Amount,
    string? AmountInputKey,
    string? AmountRollKey,
    string? Value,
    string? ValueChoiceKey,
    string? ReferenceKey,
    IReadOnlyList<CharacterRecoveryEffectConditionDefinition> Conditions);

public sealed record CharacterRecoveryProcedureDefinition(
    string Key,
    string DisplayName,
    string? PresentationRole,
    bool IsAvailableUnderRuleset,
    CharacterMechanicApplicabilityDefinition Applicability,
    IReadOnlyList<CharacterMechanicInputDefinition> Inputs,
    IReadOnlyList<CharacterRecoveryChoiceDefinition> Choices,
    IReadOnlyList<CharacterRecoveryRollDefinition> Rolls,
    IReadOnlyList<CharacterRecoveryEffectDefinition> Effects,
    CharacterRecoveryRuntimeRequirementsView RuntimeRequirements,
    IReadOnlyList<CharacterMechanicSourceAttributionView> SourceAttributions);

public sealed record CharacterPassiveValueDefinition(
    CharacterMechanicDefinition Mechanic,
    bool IsAvailableUnderRuleset,
    string? PresentationRole,
    string? RelatedMechanicKey,
    string? RelatedConceptKey,
    Guid? RelatedRuleConceptId,
    string? RelatedAbilityKey,
    IReadOnlyList<CharacterMechanicSourceAttributionView> SourceAttributions);

public sealed record CharacterQualificationDefinition(
    string Key,
    string DisplayName,
    string Category,
    string? Family,
    bool IsAvailableUnderRuleset,
    CharacterMechanicApplicabilityDefinition Applicability,
    CharacterMechanicInputDefinition StateInput,
    string? AssociatedConceptKey,
    Guid? AssociatedRuleConceptId,
    IReadOnlyList<CharacterMechanicSourceAttributionView> SourceAttributions);

public sealed record CharacterSupportCatalogDefinition(
    string Scope,
    Guid? CampaignId,
    int? RevisionNumber,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<CharacterRecoveryProcedureDefinition> RecoveryProcedures,
    IReadOnlyList<CharacterPassiveValueDefinition> PassiveValues,
    IReadOnlyList<CharacterQualificationDefinition> Qualifications);
