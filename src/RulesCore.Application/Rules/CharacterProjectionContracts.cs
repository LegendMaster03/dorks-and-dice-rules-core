namespace RulesCore.Application.Rules;

public sealed record CharacterSelectedConceptInput(
    string ConceptKey,
    string? OccurrenceKey = null);

public sealed record CharacterAdvancementFactInput(
    string ConceptKey,
    int Level,
    string? OccurrenceKey = null,
    string? ParentConceptKey = null);

public sealed record CharacterRuntimeChoiceInput(
    string ChoiceKey,
    string Value);

public sealed record CharacterRuntimeRollInput(
    string RollKey,
    int Value);

public sealed record CharacterHitPointGainInput(
    string ConceptKey,
    int ClassLevel,
    int HitDieValue,
    string? OccurrenceKey = null);

public sealed record CharacterChoiceOptionView(
    string Value,
    string DisplayName,
    string? ConceptKey);

public sealed record CharacterChoiceView(
    string ChoiceKey,
    string GroupKey,
    string DisplayName,
    string Kind,
    string State,
    IReadOnlyList<CharacterChoiceOptionView> Options,
    string? SelectedValue,
    string? SourceConceptKey,
    CharacterMechanicProvenanceView Provenance);

public sealed record CharacterRulesProjectionRequest(
    Dictionary<string, int>? BaseAbilityScores = null,
    IReadOnlyList<CharacterSelectedConceptInput>? SelectedConcepts = null,
    IReadOnlyList<CharacterAdvancementFactInput>? Advancements = null,
    IReadOnlyList<string>? CapabilityKeys = null,
    Dictionary<string, int>? CompetencyRanks = null,
    IReadOnlyList<string>? TrainingKeys = null,
    IReadOnlyList<string>? ClassSkillKeys = null,
    IReadOnlyList<string>? EquippedItemConceptKeys = null,
    IReadOnlyList<string>? KnownSpellConceptKeys = null,
    IReadOnlyList<string>? PreparedSpellConceptKeys = null,
    Dictionary<string, int>? CurrentResources = null,
    IReadOnlyList<string>? ConditionKeys = null,
    Dictionary<string, int>? IntegerFacts = null,
    Dictionary<string, bool>? BooleanFacts = null,
    Dictionary<string, string>? StringFacts = null,
    IReadOnlyList<CharacterRuntimeChoiceInput>? Choices = null,
    IReadOnlyList<CharacterRuntimeRollInput>? Rolls = null,
    IReadOnlyList<string>? RequestedMechanicKeys = null,
    IReadOnlyList<CharacterHitPointGainInput>? HitPointGains = null,
    IReadOnlyList<string>? ItemConceptKeys = null);

public sealed record CharacterMechanicProvenanceView(
    IReadOnlyList<CharacterMechanicSourceAttributionView> CanonicalConcept,
    IReadOnlyList<CharacterMechanicSourceAttributionView> MechanicalProfile,
    IReadOnlyList<CharacterMechanicSourceAttributionView> EffectiveRule);

public sealed record CharacterMechanicContributionView(
    string ContributionKey,
    string Label,
    string Operation,
    int? NumericValue,
    string? TextValue,
    string? SourceConceptKey,
    CharacterMechanicProvenanceView? Provenance = null,
    string? StateKind = null,
    string? ConditionKey = null);

public sealed record CharacterResolvedMechanicView(
    string MechanicKey,
    string Kind,
    string DisplayName,
    string State,
    int? NumericValue,
    string? TextValue,
    string? Unit,
    IReadOnlyList<string> MissingCharacterInputs,
    IReadOnlyList<string> MissingCapabilities,
    IReadOnlyList<string> RequiredChoices,
    IReadOnlyList<string> RequiredRolls,
    IReadOnlyList<CharacterMechanicContributionView> Contributions,
    CharacterMechanicProvenanceView Provenance,
    CharacterContextualHelpView? Help = null);

public sealed record CharacterRuleEffectView(
    string EffectKey,
    string Kind,
    string Operation,
    string TargetKey,
    int? NumericValue,
    string? TextValue,
    string? ConditionKey,
    string? SourceConceptKey,
    CharacterMechanicProvenanceView Provenance);

public sealed record CharacterCapabilityView(
    string CapabilityKey,
    string DisplayName,
    IReadOnlyList<string> GrantedByConceptKeys,
    CharacterMechanicProvenanceView Provenance);

public sealed record CharacterGrantView(
    string GrantKey,
    string Kind,
    string TargetKey,
    string DisplayName,
    string? SourceConceptKey,
    CharacterMechanicProvenanceView Provenance);

public sealed record CharacterMovementModeView(
    string MovementKey,
    string DisplayName,
    string State,
    int? Value,
    string? Unit,
    IReadOnlyList<CharacterMechanicContributionView> Contributions,
    CharacterMechanicProvenanceView Provenance);

public sealed record CharacterQualificationView(
    string QualificationKey,
    string Category,
    string DisplayName,
    bool? IsQualified,
    string State,
    IReadOnlyList<string> GrantedByConceptKeys,
    CharacterMechanicProvenanceView Provenance);

public sealed record CharacterAttackResolutionView(
    string? TargetDefenseKey,
    string RollMode,
    IReadOnlyList<string> TargetStateKeys);

public sealed record CharacterActionView(
    string ActionKey,
    string DisplayName,
    string? ActionType,
    string State,
    string? AttackMechanicKey,
    string? DamageExpression,
    string? DamageType,
    string? Range,
    string? Reach,
    string? Target,
    string? ResourceKey,
    int? ResourceCost,
    IReadOnlyList<string> RequiredCapabilityKeys,
    CharacterMechanicProvenanceView Provenance,
    string? SourceConceptKey = null,
    int? SpellLevel = null,
    string? SpellSchool = null,
    string? CastingTime = null,
    IReadOnlyList<string>? SpellComponents = null,
    string? MaterialComponent = null,
    string? Duration = null,
    bool? Ritual = null,
    bool? Concentration = null,
    CharacterAttackResolutionView? AttackResolution = null);

public sealed record CharacterFeatureView(
    string FeatureKey,
    string DisplayName,
    string Kind,
    string State,
    string? SourceConceptKey,
    IReadOnlyList<CharacterRuleEffectView> Effects,
    CharacterMechanicProvenanceView Provenance,
    string? OccurrenceKey = null,
    string? GrantingSourceKind = null,
    int? AcquisitionLevel = null,
    string? FeatureConceptKey = null,
    string? FeatureEntityType = null,
    CharacterMechanicProvenanceView? FeatureProvenance = null);

public sealed record CharacterEquipmentDefinitionView(
    string ItemKey,
    string ConceptKey,
    string DisplayName,
    string State,
    string? ItemType,
    string? EquipmentCategory,
    string? ArmorRole,
    decimal? Weight,
    string? WeightUnit,
    string? AmmunitionType,
    string? Capacity,
    bool? RequiresAttunement,
    string? AttunementRequirement,
    IReadOnlyList<string> PropertyKeys,
    CharacterMechanicProvenanceView Provenance);

public sealed record CharacterResourceView(
    string ResourceKey,
    string DisplayName,
    string State,
    int? CurrentValue,
    int? MaximumValue,
    string? RecoveryProcedureKey,
    IReadOnlyList<CharacterMechanicContributionView> MaximumContributions,
    CharacterMechanicProvenanceView Provenance);

public sealed record CharacterSpellcastingView(
    string SpellcastingKey,
    string DisplayName,
    string State,
    string? CastingAbilityKey,
    string? ResourceSystemKey,
    string? SaveDcMechanicKey,
    string? SpellAttackMechanicKey,
    IReadOnlyList<string> SpellListConceptKeys,
    IReadOnlyList<string> RequiredChoices,
    CharacterMechanicProvenanceView Provenance);

public sealed record CharacterProcedureView(
    string ProcedureKey,
    string DisplayName,
    string State,
    string? PresentationRole,
    IReadOnlyList<string> RequiredCapabilityKeys,
    IReadOnlyList<string> RequiredCharacterInputs,
    IReadOnlyList<string> RequiredChoices,
    IReadOnlyList<string> RequiredRolls,
    IReadOnlyList<CharacterRuleEffectView> Effects,
    CharacterMechanicProvenanceView Provenance);

public sealed record CharacterPrerequisiteRequirementView(
    string RequirementKey,
    string Kind,
    string? TargetKey,
    string? Operator,
    int? NumericValue,
    string? TextValue,
    bool? Satisfied,
    string State,
    string? Reason,
    string? GroupKey = null,
    int GroupMatchCount = 1);

public sealed record CharacterPrerequisiteView(
    string ConceptKey,
    string State,
    bool? Satisfied,
    IReadOnlyList<CharacterPrerequisiteRequirementView> Requirements,
    CharacterMechanicProvenanceView Provenance);

public sealed record CharacterProjectionConflictView(
    string ConflictKey,
    string Kind,
    string Message,
    IReadOnlyList<string> RelatedMechanicKeys,
    IReadOnlyList<string> RelatedConceptKeys);

public sealed record CharacterRuleResolutionView(
    string ConceptKey,
    string State,
    bool RequiresAdjudication,
    Guid SourceEntityRevisionId,
    int SourceRevisionNumber);

public sealed record CharacterRulesProjectionView(
    string Scope,
    Guid? CampaignId,
    int? RevisionNumber,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<CharacterResolvedMechanicView> Mechanics,
    IReadOnlyList<CharacterCapabilityView> Capabilities,
    IReadOnlyList<CharacterGrantView> Grants,
    IReadOnlyList<CharacterRuleEffectView> Effects,
    IReadOnlyList<CharacterMovementModeView> Movement,
    IReadOnlyList<CharacterQualificationView> Qualifications,
    IReadOnlyList<CharacterActionView> Actions,
    IReadOnlyList<CharacterFeatureView> Features,
    IReadOnlyList<CharacterResourceView> Resources,
    IReadOnlyList<CharacterSpellcastingView> Spellcasting,
    IReadOnlyList<CharacterProcedureView> Procedures,
    IReadOnlyList<CharacterChoiceView> Choices,
    IReadOnlyList<CharacterPrerequisiteView> Prerequisites,
    IReadOnlyList<CharacterProjectionConflictView> Conflicts,
    IReadOnlyList<CharacterEquipmentDefinitionView> Equipment,
    IReadOnlyList<CharacterUniversalCompetencyView>? Competencies = null,
    IReadOnlyList<CharacterMechanicRelationshipView>? CompetencyRelationships = null,
    IReadOnlyList<CharacterRuleResolutionView>? RuleResolutions = null);

public interface ICharacterRulesProjectionService
{
    Task<CharacterRulesProjectionView> ResolveGlobalAsync(
        CharacterRulesProjectionRequest request,
        string? userId,
        CancellationToken cancellationToken = default);

    Task<CharacterRulesProjectionView> ResolveCampaignAsync(
        Guid campaignId,
        CharacterRulesProjectionRequest request,
        string userId,
        CancellationToken cancellationToken = default);
}
