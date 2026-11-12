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
/// A complete inherent Species/Ancestry configuration, usable as either the
/// authoritative underlying baseline or the currently effective form. Neither
/// role is implicit in this value type; the owning profile distinguishes them.
/// A mixed or custom configuration may have no first-party Species label.
/// </summary>
public sealed record NativeOriginCurrentConfiguration(
    string? SpeciesId, string? AncestryId,
    IReadOnlyList<NativeOriginEffectiveGrant> InherentGrants,
    string? ProfileId = null);

/// <summary>
/// A completed historical reconstruction may retain its former baseline for audit,
/// but the snapshot is not a restoration target or a current grant source.
/// ReplacesConfiguration describes a persistent baseline replacement (for example,
/// a Rite), not activation of a temporary alternate form. Stages permit delayed
/// permanent reconstruction; reversible form activations remain separate state.
/// </summary>
public sealed record NativeOriginHistoryEvent(
    string Id, NativeOriginMethodReference Method, NativeOriginEventStage Stage,
    IReadOnlyList<string> ContributorIds, bool ReplacesConfiguration,
    NativeOriginCurrentConfiguration? PreviousConfiguration = null,
    string? ResultingProfileId = null, string? TableDecisionId = null);

/// <summary>
/// Persistent effects are separate from inherent Species/Ancestry. Their presence
/// does not imply an available form, an active shift, or additional baseline grants.
/// AcquisitionEvidenceId identifies provenance, not automatically a native Origin
/// method or an approved progression.
/// </summary>
public sealed record NativeOriginPersistentInfluence(
    string Id, NativeOriginSourceReference Source, string AcquisitionEvidenceId,
    NativeOriginDecisionBasis? Authorization = null);

/// <summary>
/// Identifies the currently active temporary substitution and its evidence.
/// The effective grants live in NativeOriginProfile.Current; the authoritative
/// baseline lives in NativeOriginProfile.Underlying. There is deliberately no
/// captured baseline or reversion snapshot on this record.
/// </summary>
public sealed record NativeOriginActiveTemporaryTransformation(
    string Id, string FormId, NativeOriginSourceReference EffectSource,
    string ActivationEvidenceId, string? PersistentInfluenceId = null,
    IReadOnlyList<string>? EffectiveFormGrantIds = null,
    NativeOriginDecisionBasis? Authorization = null);

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
    IReadOnlyList<NativeOriginInputRequirement>? InputRequirements = null,
    // ExistingConfiguration is the underlying baseline, never an active form.
    NativeOriginCurrentConfiguration? ExistingEffectiveConfiguration = null,
    NativeOriginActiveTemporaryTransformation? ActiveTemporaryTransformation = null);

/// <summary>
/// Underlying is the latest authoritative baseline, including any permanent Rite
/// or other legitimate baseline changes. Current is the mechanically effective
/// configuration. Without an active temporary transformation they should match.
/// When a temporary form is active, Current contains exactly the inherent grants
/// applicable to that form, not an automatic union with Underlying.
/// Independent grants and the persistent condition are stored separately.
/// Reversion must resolve Underlying at the time of reversion; no runtime
/// transition or reversion logic is implemented by this Phase 1 contract.
/// </summary>
public sealed record NativeOriginProfile(
    string Id, NativeOriginCurrentConfiguration Underlying,
    NativeOriginCurrentConfiguration Current,
    IReadOnlyList<NativeOriginEffectiveGrant> IndependentGrants,
    IReadOnlyList<NativeOriginHistoryEvent> History,
    NativeOriginDecisionBasis Adjudication,
    string? ProposalId = null,
    IReadOnlyList<NativeOriginPersistentInfluence>? PersistentInfluences = null,
    NativeOriginActiveTemporaryTransformation? ActiveTemporaryTransformation = null);

public sealed record NativeOriginContractIssue(
    string Code, string Path, NativeOriginIssueSeverity Severity, string Detail);

/// <summary>
/// Structural consistency is not gameplay authorization, first-party support,
/// balance certification, or proof that the proposed grants have been executed.
/// </summary>
public sealed record NativeOriginContractReview(
    NativeOriginReviewStatus Status, IReadOnlyList<NativeOriginContractIssue> Issues);
