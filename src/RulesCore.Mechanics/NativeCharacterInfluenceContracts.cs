namespace RulesCore.Mechanics;

/// <summary>
/// An acquired influence is not a form, a new Origin baseline, or an award of
/// every grant in its definition. Expression is independently authenticated
/// state and can change without revoking separately acquired progression.
/// </summary>
public sealed record NativeCharacterPersistentInfluence(
    string Id,
    string DefinitionId,
    NativeOriginSourceReference DefinitionSource,
    string AcquisitionEvidenceId,
    string ExpressionEvidenceId,
    IReadOnlyDictionary<string, string> Expression,
    IReadOnlyList<NativeCharacterInfluenceEffect> Effects,
    IReadOnlyList<NativeSheetEarnedProgression>? Progression = null,
    IReadOnlyList<NativeInfluenceGrantApplicability>? GrantApplicability = null);

/// <summary>
/// These are supported target fields in the existing native character input.
/// No executable scripting, generic monster-statblock copying, or resource
/// current-quantity mutation is implied by these targets.
/// </summary>
public enum NativeInfluenceCharacteristic
{
    AbilityScore,
    SkillRanks, SkillProficient, SkillMiscModifier, SkillAbility,
    CompetencyRanks, CompetencyProficient, CompetencyMiscModifier, CompetencyAbility,
    DefenseContribution, HitPointGain, BaseWalkSpeed, ConditionId
}

/// <summary>
/// Replace writes an explicitly authorized value; Adjust adds an authorized
/// numeric delta to the latest inherited value; Add/Suppress only edit derived
/// condition IDs. Suppress can also clear a skill/competency Ability binding or
/// the optional walk speed. These operations do not revoke an earned grant.
/// </summary>
public enum NativeInfluenceOperation { Replace, Adjust, Add, Suppress }

/// <summary>
/// Comparisons are authored per expression dimension. Dimension names have
/// exact ordinal identity even when the host supplies a differently configured
/// dictionary. Numeric comparisons use invariant decimal values rather than
/// a universal severity percentage.
/// </summary>
public enum NativeInfluenceExpressionComparison
{
    Equals, NotEquals, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual
}

public sealed record NativeInfluenceExpressionCondition(
    string Dimension, NativeInfluenceExpressionComparison Comparison, string Value);

/// <summary>
/// Source-qualified prerequisite for an effect or grant-use decision. The
/// resolver requires the definition AND the exact occurrence among records
/// independently acquired for this same influence. A missing record makes the
/// rule inactive; it does not invent an advancement. The trusted host must
/// separately verify acquisition and the repeatability of a source award.
/// </summary>
public enum NativeInfluenceEarnedKind { Progression, Grant }

public sealed record NativeInfluenceEarnedRequirement(
    NativeInfluenceEarnedKind Kind,
    string DefinitionId,
    NativeOriginSourceOccurrence Occurrence);

/// <summary>
/// A source-authored atomic characteristic change. Every affected sheet must
/// be named explicitly, including @primary. Omitting an alternate sheet never
/// makes the effect globally applicable to it. Exact RequiredExpression,
/// typed ExpressionConditions and source-qualified RequiredEarned are
/// conjunctive activation gates. None is a universal severity schedule or
/// authority to award an unearned capability. The host verifies the exact
/// occurrence, rule, evidence and target scope.
/// </summary>
public sealed record NativeCharacterInfluenceEffect(
    string Id,
    NativeOriginSourceOccurrence DefinitionOccurrence,
    string AuthorizationEvidenceId,
    NativeOriginDecisionBasis Basis,
    IReadOnlyDictionary<string, string> RequiredExpression,
    IReadOnlyList<string> TargetSheetIds,
    NativeInfluenceCharacteristic Characteristic,
    NativeInfluenceOperation Operation,
    string? Key = null,
    int? NumericValue = null,
    bool? BooleanValue = null,
    string? TextValue = null,
    IReadOnlyList<NativeInfluenceExpressionCondition>? ExpressionConditions = null,
    IReadOnlyList<NativeInfluenceEarnedRequirement>? RequiredEarned = null);

/// <summary>
/// One source-qualified decision for an independently earned influence grant.
/// Multiple decisions for different expressions are permitted, but two matching
/// decisions for one grant are unresolved rather than input-order precedence.
/// An absent matching decision leaves current usability unresolved; neither
/// absence nor Suppressed removes the independently acquired grant.
/// </summary>
public sealed record NativeInfluenceGrantApplicability(
    string GrantId,
    NativeOriginGrantApplicabilityState State,
    NativeOriginDecisionBasis Basis,
    IReadOnlyDictionary<string, string> RequiredExpression,
    string AuthorizationEvidenceId,
    IReadOnlyList<NativeInfluenceExpressionCondition>? ExpressionConditions = null,
    IReadOnlyList<NativeInfluenceEarnedRequirement>? RequiredEarned = null);

/// <summary>
/// The existing sheet authority remains mandatory for the original primary and
/// all persistent alternate sheets. These additional callbacks must verify
/// persisted influence occurrences, expression changes, earned advancement,
/// and exact effect and applicability decisions independently.
/// </summary>
public interface INativeCharacterInfluenceAuthority : INativeCharacterSheetAuthority
{
    bool AuthorizesInfluence(string characterId, NativeCharacterPersistentInfluence influence);
    bool AuthorizesExpression(string characterId, NativeCharacterPersistentInfluence influence);
    bool AuthorizesEffect(string characterId, string influenceId, NativeCharacterInfluenceEffect effect);
    bool AuthorizesInfluenceProgression(string characterId, string influenceId,
        NativeSheetEarnedProgression progression);
    bool AuthorizesInfluenceGrant(string characterId, string influenceId,
        NativeSheetEarnedProgression progression, NativeSheetEarnedGrant grant);
    bool AuthorizesInfluenceApplicability(string characterId, string influenceId,
        NativeInfluenceGrantApplicability decision);
}

/// <summary>
/// A verified rule may choose exactly one already-authorized writer when a
/// collision contains precisely these active effect instance IDs. The writer
/// IDs refer to exact independently acquired effect occurrences, not names or
/// definition IDs. This is neither permission to acquire a grant nor an
/// automatic arithmetic stacking or first/last-wins rule.
/// </summary>
public sealed record NativeInfluenceOverlapSelection(
    string Id,
    IReadOnlyList<string> EffectIds,
    string SelectedEffectId,
    NativeOriginSourceOccurrence RuleOccurrence,
    NativeOriginDecisionBasis Basis,
    string AuthorizationEvidenceId);

/// <summary>
/// Optional shared authority extension. Existing influence consumers can omit
/// this interface; any supplied overlap selection then fails closed. Workstream
/// A acquisition authorization is not replaced or weakened by this callback.
/// </summary>
public interface INativeCharacterInfluenceOverlapAuthority
{
    bool AuthorizesOverlapSelection(string characterId, NativeInfluenceOverlapSelection selection);
}

public sealed record NativeInfluenceResolvedEffect(
    string InfluenceId, NativeCharacterInfluenceEffect Effect);

public sealed record NativeInfluenceResolvedProgression(
    string OwnerInfluenceId, NativeSheetEarnedProgression Record);

public sealed record NativeInfluenceResolvedGrant(
    string OwnerInfluenceId,
    string ProgressionId,
    NativeSheetEarnedGrant Entitlement,
    NativeOriginGrantApplicabilityState Applicability,
    NativeOriginDecisionBasis? ApplicabilityBasis);

/// <summary>
/// Read-only effective mechanics. "unresolved" deliberately withholds a
/// mechanics result when two active effects collide or a required target has
/// no supported interaction; it does not pick an arbitrary application order.
/// The base character and owned alternate sheets remain unchanged.
/// </summary>
public sealed record NativeCharacterInfluenceResolution(
    string Status,
    NativeCharacterRequest? EffectiveMechanics,
    IReadOnlyList<NativeInfluenceResolvedEffect> AppliedEffects,
    IReadOnlyList<NativeInfluenceResolvedProgression> Progression,
    IReadOnlyList<NativeInfluenceResolvedGrant> Grants,
    IReadOnlyList<NativeIssue> Issues);
