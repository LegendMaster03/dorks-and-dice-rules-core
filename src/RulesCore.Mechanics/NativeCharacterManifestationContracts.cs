namespace RulesCore.Mechanics;

/// <summary>
/// An owned alternate sheet survives all manifestations. A manifestation records
/// one activation of that sheet, not a copy of its authoritative progression.
/// Lifecycle and effective contribution are deliberately separate dimensions.
/// </summary>
public enum NativeManifestationLifecycle { Active, Ended }
public enum NativeManifestationContribution { Contributing, Suppressed }
public enum NativeManifestationTransitionAction { Activate, End, Suppress, Restore }
public enum NativeManifestationInteractionKind { Coexist, Suppress, Replace, Terminate }
public enum NativeManifestationResourceOwner { Primary, Sheet, Influence, Manifestation }
public enum NativeManifestationResourceMode { Inherit, Separate, CaptureMaximum, AdjustMaximum }

/// <summary>
/// Current quantity and recorded maximum belong to the named persistent owner,
/// not to a derived effective-character projection. Resource IDs are unique per
/// character. Only a transactional host may spend, recover, or revise a pool.
/// </summary>
public sealed record NativeManifestationResourceState(
    string Id, string DefinitionId, NativeOriginSourceOccurrence SourceOccurrence,
    string EvidenceId, NativeManifestationResourceOwner OwnerKind, string OwnerId,
    int Current, int Maximum);

/// <summary>
/// An explicit policy for showing one existing resource in one form. A maximum
/// change never changes Current. Captures are numbers stored on the instance
/// at activation; normal inheritance always reads the latest owner state.
/// </summary>
public sealed record NativeManifestationResourcePolicy(
    string ResourceId, NativeManifestationResourceMode Mode,
    int? MaximumAdjustment = null, string? CaptureKey = null);

/// <summary>
/// A single source-authorized activation, retained after it ends for history.
/// EndingConditions name authored triggers, not universal duration or reversion
/// behavior. SourceInfluenceId, when present, binds the form to an acquired
/// influence rather than to a similarly named monster or Species definition.
/// </summary>
public sealed record NativeManifestationInstance(
    string Id, string SheetId, string DefinitionId,
    NativeOriginSourceOccurrence DefinitionOccurrence, string? SourceInfluenceId,
    string ActivationEventId, string ActivationEvidenceId,
    NativeOriginDecisionBasis ActivationBasis,
    NativeManifestationLifecycle Lifecycle,
    NativeManifestationContribution Contribution,
    IReadOnlyDictionary<string, int> ActivationCaptures,
    IReadOnlyList<string> EndingConditions,
    IReadOnlyList<NativeManifestationResourcePolicy> ResourcePolicies,
    string LastEventId);

/// <summary>
/// A source-authored, host-authenticated decision about precisely the listed
/// simultaneous instances. Coexistence is representable, but unsupported
/// overlapping mechanics remain unresolved. Replace is effective replacement;
/// Terminate requires separately recorded authorized End transitions.
/// </summary>
public sealed record NativeManifestationInteractionDecision(
    string Id, NativeManifestationInteractionKind Kind,
    IReadOnlyList<string> InstanceIds, string? SelectedInstanceId,
    NativeOriginSourceOccurrence DefinitionOccurrence,
    NativeOriginDecisionBasis Basis, string AuthorizationEvidenceId);

/// <summary>
/// This is a host-owned journal snapshot, not an Origin configuration.
/// Revision and event identity are used for optimistic-concurrency planning;
/// the Character Sheet host must enforce their atomic persistence.
/// </summary>
public sealed record NativeManifestationLedger(
    string CharacterId, long Revision,
    IReadOnlyList<NativeManifestationInstance> Instances,
    IReadOnlyList<NativeManifestationResourceState> Resources,
    IReadOnlyList<string> ProcessedEventIds,
    IReadOnlyList<NativeManifestationInteractionDecision>? Interactions = null);

/// <summary>
/// Every operation has its own rule occurrence, authorization basis and evidence.
/// Activate carries a new instance and optionally rule-verified instance-owned
/// resource pools. End, Suppress and Restore reference an existing instance.
/// A switch is an ordered list of steps committed under one event/revision.
/// </summary>
public sealed record NativeManifestationTransitionStep(
    NativeManifestationTransitionAction Action,
    string TargetInstanceId,
    NativeOriginSourceOccurrence RuleOccurrence,
    NativeOriginDecisionBasis Basis,
    string AuthorizationEvidenceId,
    NativeManifestationInstance? NewInstance = null,
    IReadOnlyList<NativeManifestationResourceState>? NewResources = null);

public sealed record NativeManifestationTransitionRequest(
    string CharacterId, long ExpectedRevision, string EventId,
    IReadOnlyList<NativeManifestationTransitionStep> Steps,
    IReadOnlyList<NativeManifestationInteractionDecision>? ReplacementInteractions = null);

public sealed record NativeManifestationTransitionPlan(
    string Status, long ExpectedRevision, NativeManifestationLedger? Proposed,
    IReadOnlyList<NativeIssue> Issues);

/// <summary>A derived resource view with stable authoritative ownership.</summary>
public sealed record NativeManifestationEffectiveResource(
    string Id, NativeManifestationResourceOwner OwnerKind, string OwnerId,
    int Current, int RecordedMaximum, int EffectiveMaximum,
    NativeManifestationResourceMode Policy);

/// <summary>
/// A partially resolved effective character. Alternate form Class features,
/// Hit Point interactions, and unverified resource policies remain withheld.
/// Earned sheet and influence entitlements retain their independent provenance.
/// </summary>
public sealed record NativeManifestationEffectiveCharacter(
    string Status, string EffectiveSheetId, string? ManifestationInstanceId,
    NativeCharacterRequest? EffectiveMechanics,
    IReadOnlyList<NativeSheetResolvedProgression> SheetProgression,
    IReadOnlyList<NativeSheetResolvedGrant> SheetGrants,
    IReadOnlyList<NativeInfluenceResolvedProgression> InfluenceProgression,
    IReadOnlyList<NativeInfluenceResolvedGrant> InfluenceGrants,
    IReadOnlyList<NativeInfluenceResolvedEffect> AppliedEffects,
    IReadOnlyList<NativeManifestationEffectiveResource> Resources,
    IReadOnlyList<NativeIssue> Issues);

/// <summary>
/// A real authenticated host must implement every callback. This interface
/// deliberately supplies no permissive implementation or caller-derived grant
/// authority. Revisions, receipts, exact rule occurrences, form eligibility,
/// resource ownership and scoped DM authority need independent verification.
/// </summary>
public interface INativeCharacterManifestationAuthority : INativeCharacterInfluenceAuthority
{
    bool AuthorizesLedger(string characterId, NativeManifestationLedger ledger);
    bool AuthorizesInstance(string characterId, NativeManifestationInstance instance,
        NativePersistentCharacterSheet sheet);
    bool AuthorizesResource(string characterId, NativeManifestationResourceState resource);
    bool AuthorizesResourcePolicy(string characterId, NativeManifestationInstance instance,
        NativeManifestationResourcePolicy policy);
    bool AuthorizesInteraction(string characterId, NativeManifestationInteractionDecision decision);
    bool AuthorizesTransition(string characterId, NativeManifestationLedger current,
        NativeManifestationTransitionRequest request, NativeManifestationTransitionStep step);
}
