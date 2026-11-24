using System.Text.Json;

namespace RulesCore.Mechanics;

/// <summary>
/// Authorization is supplied by an authenticated application boundary, never by serialized
/// proposal claims. Implementations must verify actor, evidence, campaign scope and target.
/// </summary>
public interface INativeOriginAuthority
{
    bool Authorizes(NativeTableRuleDecision decision, NativeTableDecisionScope trustedScope);

    /// <summary>Verifies both membership and the evidence for an open catalog selection.</summary>
    bool AuthorizesCatalogChoice(string catalogId, string optionId, string evidenceId);

    // A parent's committed feat does not prove the descendant's eligibility.
    // Existing table/native providers remain fail-closed until they explicitly
    // support this character-specific decision.
    bool AuthorizesInheritedFeat(NativeOriginCandidate candidate, string featId) => false;
}

public enum NativeOriginConstructionStatus
{
    Invalid, Incomplete, Unresolved, Conflicting, Authorized
}

/// <summary>
/// TrustedScope is populated by the authenticated host, not read from the proposal.
/// A null scope or authority prevents all table-effect and open-catalog authorization.
/// ExistingIndependentGrants are preexisting entitlements; this engine does not mint them.
/// </summary>
public sealed record NativeOriginConstructionRequest(
    NativeOriginProposal Proposal,
    NativeTableDecisionScope? TrustedScope = null,
    IReadOnlyList<NativeOriginEffectiveGrant>? ExistingIndependentGrants = null,
    IReadOnlyList<NativeOriginIndependentGrantApplicability>? IndependentGrantApplicability = null,
    IReadOnlyList<NativeOriginPersistentInfluence>? PersistentInfluences = null,
    bool AutoManifestDirectDefaults = true,
    // Host-owned creation policy, not a player-editable proposal field.
    // A true requirement without a complete policy fails closed; the opt-in
    // boundary preserves older integrations until native prices are calibrated.
    bool RequireCreationAllocation = false,
    NativeOriginAllocationPolicy? CreationAllocationPolicy = null);

/// <summary>
/// EvaluatedProposal includes authoritative discovered candidates and actual selections.
/// A partial profile has only individually authorized grants; it is not playable until
/// Status is Authorized. Consumers must honor Status and Issues, not merely Profile != null.
/// </summary>
public sealed record NativeOriginConstructionResult(
    NativeOriginConstructionStatus Status,
    NativeOriginProposal EvaluatedProposal,
    NativeOriginProfile? Profile,
    IReadOnlyList<NativeOriginContractIssue> Issues)
{
    public bool IsMechanicallyAuthorized => Status == NativeOriginConstructionStatus.Authorized;

    // Read-only biological Combine evidence; never grants or authorizes traits.
    public NativeOriginBiologicalCombination? BiologicalCombination { get; init; }

    // Set only when a host requests an actual initial-creation allocation gate.
    // Unlike the historical preview, this assessment affects Status and Issues.
    public NativeOriginAllocationAssessment? CreationAllocation { get; init; }
}
