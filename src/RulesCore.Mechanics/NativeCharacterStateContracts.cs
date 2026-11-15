namespace RulesCore.Mechanics;

/// <summary>
/// A trusted, character-owned source for alternate-sheet evaluation. PrimaryMechanics
/// is constructed by the authoritative host from its verified Origin, Class and
/// other records; neither this contract nor the resolver authorizes those records.
/// The existing NativeOriginProfile remains the separate Origin authority.
/// </summary>
public sealed record NativeCharacterState(
    string CharacterId,
    NativeCharacterRequest PrimaryMechanics,
    IReadOnlyList<NativePersistentCharacterSheet> AlternateSheets);

/// <summary>
/// A stable, persistent partial sheet. The parent is the primary sheet or another
/// sheet belonging to the same character, never an independent character.
/// A non-manifested sheet (such as a shared werewolf progression owner) is valid.
/// </summary>
public sealed record NativePersistentCharacterSheet(
    string Id,
    string ParentId,
    string DefinitionId,
    NativeOriginSourceReference DefinitionSource,
    string AcquisitionEvidenceId,
    NativeCharacterSheetOverrides? Overrides = null,
    IReadOnlyList<NativeSheetEarnedProgression>? Progression = null,
    IReadOnlyList<NativeSheetGrantApplicability>? GrantApplicability = null);

/// <summary>Presence of this wrapper distinguishes an explicit null from inheritance.</summary>
public sealed record NativeSheetValue<T>(T Value);

/// <summary>
/// Map entries override individual members. Replace discards all inherited entries
/// in this collection before applying the supplied members.
/// </summary>
public sealed record NativeSheetMapPatch<T>(
    bool Replace, IReadOnlyDictionary<string, T> Entries);

/// <summary>
/// A scoped collection edit. Removal does not revoke an underlying entitlement;
/// authoritative grants live in separate progression/Origin records.
/// </summary>
public sealed record NativeSheetCollectionPatch<T>(
    bool Replace, IReadOnlyList<T> Added, IReadOnlyList<T>? Removed = null);

/// <summary>
/// Partial nested-object override. Replacement resets the entire training object
/// first; all other specified properties then override it. In the absence of
/// Replacement, unspecified properties inherit from the parent training object.
/// </summary>
public sealed record NativeSheetTrainingPatch(
    NativeSheetValue<NativeSkillTraining>? Replacement = null,
    NativeSheetValue<int>? Ranks = null,
    NativeSheetValue<bool>? Proficient = null,
    NativeSheetValue<int>? MiscModifier = null,
    NativeSheetValue<string?>? Ability = null);

/// <summary>
/// First-increment typed overlays for existing NativeCharacterRequest domains.
/// Class advancements and authoritative Origin grants are deliberately absent:
/// sheets inherit them, and this read model can not acquire or replace them.
/// </summary>
public sealed record NativeCharacterSheetOverrides(
    NativeSheetMapPatch<int>? AbilityScores = null,
    NativeSheetMapPatch<NativeSheetTrainingPatch>? Skills = null,
    NativeSheetMapPatch<NativeSheetTrainingPatch>? Competencies = null,
    NativeSheetMapPatch<int>? DefenseContributions = null,
    NativeSheetMapPatch<int>? CurrentResources = null,
    NativeSheetMapPatch<int>? HitPointGains = null,
    NativeSheetCollectionPatch<string>? SelectedOptions = null,
    NativeSheetCollectionPatch<string>? ConditionIds = null,
    NativeSheetValue<int?>? BaseWalkSpeed = null);

/// <summary>
/// One advancement is owned by exactly one sheet. A canonical definition ID is
/// not a grant identity, and source occurrence plus acquisition evidence is not
/// automatically sufficient proof of ownership.
/// </summary>
public sealed record NativeSheetEarnedProgression(
    string Id,
    string DefinitionId,
    NativeOriginSourceOccurrence SourceOccurrence,
    string AcquisitionEvidenceId,
    IReadOnlyList<NativeSheetEarnedGrant> Grants,
    IReadOnlyDictionary<string, string>? Choices = null);

/// <summary>
/// A reference to an already earned, source-qualified entitlement. Resolving the
/// same grant on multiple descendants never issues or duplicates that grant.
/// </summary>
public sealed record NativeSheetEarnedGrant(
    string Id, string DefinitionId, NativeOriginSourceOccurrence SourceOccurrence,
    string AcquisitionEvidenceId);

/// <summary>
/// Applicability is separate from earned ownership. Missing applicability is
/// unresolved, not an implicit permission to use a capability. The decision
/// requires separately verified rule or DM authorization.
/// </summary>
public sealed record NativeSheetGrantApplicability(
    string GrantId, NativeOriginGrantApplicabilityState State,
    NativeOriginDecisionBasis Basis);

public sealed record NativeSheetResolvedProgression(
    string OwnerSheetId, NativeSheetEarnedProgression Record);

public sealed record NativeSheetResolvedGrant(
    string OwnerSheetId,
    string ProgressionId,
    NativeSheetEarnedGrant Entitlement,
    NativeOriginGrantApplicabilityState Applicability,
    NativeOriginDecisionBasis? ApplicabilityBasis);

/// <summary>
/// The host must authenticate the character, all sheet transitions, progression
/// acquisition, source occurrences, grants, and applicability decisions before
/// evaluation. This verifier is mandatory; client-supplied records prove nothing.
/// No implementation is supplied that trusts all assertions by default.
/// </summary>
public interface INativeCharacterSheetAuthority
{
    bool AuthorizesSheet(string characterId, NativePersistentCharacterSheet sheet);
    bool AuthorizesProgression(string characterId, string ownerSheetId,
        NativeSheetEarnedProgression progression);
    bool AuthorizesGrant(string characterId, string ownerSheetId,
        NativeSheetEarnedProgression progression, NativeSheetEarnedGrant grant);
}

/// <summary>
/// A complete mechanics projection input where possible, not a second persisted
/// character or proof of Origin/Class authority. Invalid graphs fail closed.
/// </summary>
public sealed record NativeCharacterSheetResolution(
    string Status,
    NativeCharacterRequest? EffectiveMechanics,
    IReadOnlyList<NativeSheetResolvedProgression> Progression,
    IReadOnlyList<NativeSheetResolvedGrant> Grants,
    IReadOnlyList<NativeIssue> Issues);
