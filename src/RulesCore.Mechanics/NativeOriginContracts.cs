using System.Text.Json;

namespace RulesCore.Mechanics;

/// <summary>
/// These contracts describe Origin evidence, decisions, and resulting state. They do not
/// themselves determine inheritance eligibility, execute mechanics, or issue grants.
/// Source occurrences are intentionally distinct from canonical mechanical identities.
/// </summary>
public enum NativeOriginIntent { Direct, Mixed, Custom, Transformation }
public enum NativeOriginSourceKind { Native, ManifestedProfile, Custom }
public enum NativeOriginInputKind
{
    BiologicalParent, PlantSource, ExistingCreature, Material, Creator,
    MagicalSource, Animator, ProcessSpecific
}
public enum NativeOriginCharacteristicKind { Intrinsic, Trait, Other }
public enum NativeOriginEligibility { Unreviewed, Eligible, Restricted, Unresolved, DmAuthorized }
public enum NativeOriginChoiceTiming { Creation, OnUse, Unknown }
public enum NativeOriginRelationshipKind
{
    Duplicate, Equivalent, Upgrade, Substitution, Exclusion, Requires,
    LinkedParameters, SelectionCardinality, ResourceCoordination, Unknown
}
public enum NativeOriginResolutionAction
{
    RetainBoth, RetainFirst, RetainSecond, ConsolidateEvidence, Replace, Defer
}
public enum NativeOriginAdjudication { Supported, Conflict, Unresolved, DmAuthorized }
public enum NativeOriginReviewStatus { StructurallyConsistent, Incomplete, Unresolved, Conflicting, Invalid }
public enum NativeOriginIssueSeverity { Incomplete, Unresolved, Conflict, Invalid }
public enum NativeOriginEventStage { Pending, InProgress, Completed }

/// <summary>A method ID in the selected native rule, not a second hard-coded method taxonomy.</summary>
public sealed record NativeOriginMethodReference(string RuleId, string MethodId);

/// <summary>
/// Exact selected-native method definition, read from the catalog. AuthoredDefinition
/// retains future fields without losing information or inventing executable semantics.
/// </summary>
public sealed record NativeOriginMethodDefinition(
    NativeOriginMethodReference Reference, string Name, string? Specializes,
    string? Stage, bool? CreatesNewIndividual, string? Operation,
    JsonElement AuthoredDefinition);

public sealed record NativeOriginSourceReference(
    NativeOriginSourceKind Kind, string Id, string? Version = null);

/// <summary>ProcessSpecific allows future authored roles without a C# enum migration.</summary>
public sealed record NativeOriginInputRole(
    NativeOriginInputKind Kind, string? ProcessRoleId = null);

/// <summary>
/// Authored requirements for a particular process, including nonparent roles.
/// These are evidence for Phase 2 to check, not a hard-coded parent count.
/// </summary>
public sealed record NativeOriginInputRequirement(
    string Id, NativeOriginInputRole Role, int MinimumCount,
    int? MaximumCount = null, string? NativeRuleReference = null,
    string? TableDecisionId = null);

public sealed record NativeOriginContributor(
    string Id, NativeOriginInputRole Role, NativeOriginSourceReference Source,
    NativeOriginCurrentConfiguration? ManifestedProfile = null,
    IReadOnlyList<string>? HistoricalSourceIds = null);

/// <summary>
/// OccurrencePath is the source-local location of a particular intrinsic or trait
/// definition. Identical names or shared concept IDs do not establish equivalence.
/// </summary>
public sealed record NativeOriginSourceOccurrence(
    NativeOriginSourceReference Source, string OccurrencePath);

public sealed record NativeOriginCharacteristicIdentity(
    NativeOriginCharacteristicKind Kind,
    NativeOriginSourceOccurrence Occurrence,
    string? MechanicalDefinitionId = null,
    string? TraitConceptId = null,
    string? IntrinsicSlotId = null);

public sealed record NativeOriginSelectionBasis(
    NativeOriginEligibility Eligibility, string? RuleReference = null,
    string? TableDecisionId = null, string? Explanation = null);

/// <summary>
/// FiniteOptions is an authored finite set; OptionCatalogId points to an external
/// native catalog requiring a trusted eligibility adapter. On-use decisions are
/// never treated as persistent character-creation selections.
/// </summary>
public sealed record NativeOriginChoiceRequirement(
    string Id, NativeOriginSourceOccurrence Occurrence, NativeOriginChoiceTiming Timing,
    int Minimum, int Maximum, IReadOnlyList<string>? FiniteOptions = null,
    string? OptionCatalogId = null, string? ParameterKey = null);

public sealed record NativeOriginChoiceSelection(
    string RequirementId, IReadOnlyList<string> SelectedOptionIds,
    string? CatalogEligibilityEvidenceId = null);

/// <summary>
/// Candidate IDs are proposal-local, not persistent grant identities. OfferedValue
/// and OfferedParameters are raw authoring evidence; an unresolved binding must
/// remain unresolved until a real rule or a scoped table decision resolves it.
/// </summary>
public sealed record NativeOriginCandidate(
    string Id, NativeOriginCharacteristicIdentity Identity,
    NativeOriginSelectionBasis SelectionBasis, string? ContributorId = null,
    JsonElement? OfferedValue = null,
    IReadOnlyDictionary<string, JsonElement>? OfferedParameters = null,
    IReadOnlyList<NativeOriginChoiceRequirement>? RequiredChoices = null,
    IReadOnlyList<string>? UnresolvedEvidence = null);

/// <summary>A selection is not a grant, even if it names an eligible candidate.</summary>
public sealed record NativeOriginManifestation(
    string Id, string CandidateId, JsonElement? SelectedValue = null,
    IReadOnlyDictionary<string, JsonElement>? SelectedParameters = null,
    IReadOnlyList<NativeOriginChoiceSelection>? Choices = null,
    string? TableDecisionId = null);

/// <summary>General relationship evidence, without Species-specific dispatch.</summary>
public sealed record NativeOriginRelationship(
    string Id, NativeOriginRelationshipKind Kind, IReadOnlyList<string> CandidateIds,
    string? AuthoredRuleReference = null, string? Detail = null);

/// <summary>A cross-characteristic selector, not an inferred mechanic equivalence.</summary>
public sealed record NativeOriginCharacteristicSelector(
    string? CandidateId = null, string? MechanicalDefinitionId = null,
    string? IntrinsicSlotId = null, string? SourceId = null,
    string? ParameterKey = null, JsonElement? ExpectedValue = null);

/// <summary>
/// Explicit whole-profile prerequisites, exclusions, cardinalities and
/// linked parameters; authoring evidence is not executed by Phase 1.
/// </summary>
public sealed record NativeOriginConstraint(
    string Id, NativeOriginRelationshipKind Kind,
    IReadOnlyList<NativeOriginCharacteristicSelector> Subjects,
    IReadOnlyList<NativeOriginCharacteristicSelector> Conditions,
    int? Minimum = null, int? Maximum = null,
    IReadOnlyDictionary<string, string>? LinkedParameterPaths = null,
    string? NativeRuleReference = null, string? TableDecisionId = null);

public sealed record NativeOriginDecisionBasis(
    NativeOriginAdjudication Adjudication, string? NativeRuleReference = null,
    string? TableDecisionId = null, string? Explanation = null);

/// <summary>
/// A resolution is an explicit proposal, not implicit stacking or an inferred
/// semantic match. Conflict and unresolved states remain first-class outcomes.
/// </summary>
public sealed record NativeOriginResolution(
    string RelationshipId, NativeOriginResolutionAction Action,
    NativeOriginDecisionBasis Basis);

/// <summary>
/// This identity is earned by the future evaluator, not by candidate discovery.
/// Candidate IDs provide offer provenance; manifestation and grant IDs identify
/// separate stages of the same acquired characteristic.
/// </summary>
public sealed record NativeOriginEffectiveGrant(
    string Id, string ManifestationId, NativeOriginCharacteristicIdentity Identity,
    IReadOnlyList<string> SourceCandidateIds,
    JsonElement? EffectiveValue = null,
    IReadOnlyDictionary<string, JsonElement>? Parameters = null,
    IReadOnlyList<NativeOriginChoiceSelection>? ResolvedChoices = null,
    NativeOriginDecisionBasis? Authorization = null);

/// <summary>
/// Only InherentGrants belong to the current Species/Ancestry configuration.
/// A mixed or custom profile may have no named first-party Species or Ancestry.
/// </summary>
public sealed record NativeOriginCurrentConfiguration(
    string? SpeciesId, string? AncestryId,
    IReadOnlyList<NativeOriginEffectiveGrant> InherentGrants,
    string? ProfileId = null);

/// <summary>
/// A completed event may preserve the prior configuration for audit, but it does
/// not confer any grant from that configuration on the current character.
/// Stages permit delayed supernatural reconstruction without a forced instant swap.
/// </summary>
public sealed record NativeOriginHistoryEvent(
    string Id, NativeOriginMethodReference Method, NativeOriginEventStage Stage,
    IReadOnlyList<string> ContributorIds, bool ReplacesConfiguration,
    NativeOriginCurrentConfiguration? PreviousConfiguration = null,
    string? ResultingProfileId = null, string? TableDecisionId = null);

public sealed record NativeOriginProposal(
    string Id, NativeOriginIntent Intent, NativeOriginMethodReference Method,
    IReadOnlyList<NativeOriginContributor> Contributors,
    IReadOnlyList<NativeOriginCandidate> Candidates,
    IReadOnlyList<NativeOriginManifestation> Manifestations,
    string? SelectedSpeciesId = null, string? SelectedAncestryId = null,
    NativeOriginCurrentConfiguration? ExistingConfiguration = null,
    IReadOnlyList<NativeOriginRelationship>? Relationships = null,
    IReadOnlyList<NativeOriginResolution>? Resolutions = null,
    IReadOnlyList<NativeTableRuleDecision>? TableDecisions = null,
    IReadOnlyList<NativeOriginHistoryEvent>? History = null,
    IReadOnlyList<NativeOriginConstraint>? Constraints = null,
    IReadOnlyList<NativeOriginInputRequirement>? InputRequirements = null);

/// <summary>
/// Independent grants are separated so a Rite does not discard class, Background,
/// Upbringing, training, or other non-Species grants. Historical configurations
/// are not active packages. No public runtime Origin evaluator exists in Phase 1.
/// </summary>
public sealed record NativeOriginProfile(
    string Id, NativeOriginCurrentConfiguration Current,
    IReadOnlyList<NativeOriginEffectiveGrant> IndependentGrants,
    IReadOnlyList<NativeOriginHistoryEvent> History,
    NativeOriginDecisionBasis Adjudication,
    string? ProposalId = null);

public sealed record NativeOriginContractIssue(
    string Code, string Path, NativeOriginIssueSeverity Severity, string Detail);

/// <summary>
/// Structural consistency is not gameplay authorization, first-party support,
/// balance certification, or proof that the proposed grants have been executed.
/// </summary>
public sealed record NativeOriginContractReview(
    NativeOriginReviewStatus Status, IReadOnlyList<NativeOriginContractIssue> Issues);
