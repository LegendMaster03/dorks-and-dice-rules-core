namespace RulesCore.Mechanics;

/// <summary>
/// Source-authored supernatural progression is independent of Species, biological
/// inheritance, and Class advancement. A definition is only a proposal until the
/// host verifies its exact rule/version or scoped table authorization.
/// </summary>
public sealed record NativeSupernaturalDefinition(
    string Id,
    NativeOriginSourceReference Source,
    IReadOnlyDictionary<string, string> InitialExpression,
    IReadOnlyList<NativeSupernaturalProgressionNode> Nodes,
    IReadOnlyList<NativeCharacterInfluenceEffect>? AcquisitionEffects = null);

/// <summary>
/// A named finite selection; one chosen value per requirement is persisted in
/// NativeSheetEarnedProgression.Choices. More complex choices need a separately
/// reconciled shared choice contract, not an implicit free-text selection.
/// </summary>
public sealed record NativeSupernaturalFiniteChoice(
    string Id, IReadOnlyList<string> Options);

public sealed record NativeSupernaturalGrantDefinition(
    string Id, string DefinitionId, NativeOriginSourceOccurrence SourceOccurrence,
    NativeSheetLevelReference? LevelReference = null,
    IReadOnlyList<NativeSupernaturalGrantUseRule>? UseRules = null);

/// <summary>
/// Authoring-time applicability for a grant belonging to this node. Its grant
/// identity is minted from the acquired instance and the authored grant key.
/// Expression and earned prerequisites are evaluated by the existing resolver.
/// </summary>
public sealed record NativeSupernaturalGrantUseRule(
    NativeOriginGrantApplicabilityState State,
    NativeOriginDecisionBasis Basis,
    IReadOnlyDictionary<string, string> RequiredExpression,
    IReadOnlyList<NativeInfluenceExpressionCondition>? ExpressionConditions = null,
    IReadOnlyList<NativeInfluenceEarnedRequirement>? RequiredEarned = null);

/// <summary>
/// A node may be awarded upon acquisition, or on an independently verified
/// transition. RequiredNodeIds are conjunctive, never automatically traversed.
/// ExclusiveGroup prevents acquiring two branches of one authored choice.
/// Event keys and eligibility keys are rules authored by the source, not
/// client-provided facts. A null minimum level imposes no level gate.
/// </summary>
public sealed record NativeSupernaturalProgressionNode(
    string Id, NativeOriginSourceOccurrence SourceOccurrence,
    bool OnAcquisition = false,
    int? MinimumCharacterLevel = null,
    IReadOnlyList<string>? RequiredNodeIds = null,
    string? ExclusiveGroup = null,
    IReadOnlyList<string>? RequiredEventKeys = null,
    IReadOnlyList<string>? RequiredEligibilityKeys = null,
    IReadOnlyList<NativeSupernaturalFiniteChoice>? Choices = null,
    IReadOnlyList<NativeSupernaturalGrantDefinition>? Grants = null,
    IReadOnlyList<NativeCharacterInfluenceEffect>? Effects = null);

public enum NativeSupernaturalTransitionKind { Acquire, Advance, ChangeExpression }
public enum NativeSupernaturalEligibilityVerdict { Satisfied, Failed, Unresolved }

/// <summary>
/// EventId identifies the transaction. RequirementEvidence binds each authored
/// event key to a distinct, independently verified event occurrence. EvidenceId
/// authenticates the operation itself and must not be interpreted as a rule.
/// </summary>
public sealed record NativeSupernaturalTransitionRequest(
    string CharacterId, long ExpectedRevision, string EventId,
    NativeSupernaturalTransitionKind Kind, string InfluenceId,
    string EvidenceId, NativeOriginDecisionBasis Basis,
    string? NodeId = null,
    IReadOnlyDictionary<string, string>? Choices = null,
    IReadOnlyDictionary<string, string>? RequirementEvidence = null,
    IReadOnlyDictionary<string, string>? NewExpression = null);

/// <summary>
/// The journal tracks consumed event evidence independently of earned grants;
/// it does not duplicate the saved character's Origin/Class/progression state.
/// The host must commit proposed state and journal together via compare-and-swap.
/// </summary>
public sealed record NativeSupernaturalTransitionJournal(
    string CharacterId, long Revision,
    IReadOnlyList<string> ProcessedEventIds,
    IReadOnlyList<string> ConsumedEligibilityEvidenceIds);

public sealed record NativeSupernaturalTransitionPlan(
    string Status, long ExpectedRevision,
    NativeCharacterState? ProposedState,
    NativeSupernaturalTransitionJournal? ProposedJournal,
    IReadOnlyList<NativeInfluenceResolvedProgression> NewProgression,
    IReadOnlyList<NativeInfluenceResolvedGrant> NewGrants,
    IReadOnlyList<NativeIssue> Issues);

/// <summary>
/// The host loads a version-pinned authored definition, authenticates the
/// character and current journal, and checks every source/event/table decision.
/// There is intentionally no permissive implementation or client-side adapter.
/// Proposals are not persisted or made authoritative by the planner.
/// </summary>
public interface INativeSupernaturalProgressionAuthority : INativeCharacterInfluenceAuthority
{
    bool AuthorizesJournal(string characterId, NativeSupernaturalTransitionJournal journal);
    bool AuthorizesDefinition(string characterId, NativeSupernaturalDefinition definition,
        NativeOriginDecisionBasis basis);
    bool AuthorizesAcquisition(string characterId, NativeSupernaturalDefinition definition,
        NativeSupernaturalTransitionRequest request);
    bool AuthorizesAdvancement(string characterId, NativeCharacterPersistentInfluence influence,
        NativeSupernaturalProgressionNode node, NativeSupernaturalTransitionRequest request);
    bool AuthorizesExpressionChange(string characterId, NativeCharacterPersistentInfluence influence,
        NativeSupernaturalTransitionRequest request);
    bool AuthorizesEventRequirement(string characterId, string influenceId, string requirementKey,
        string eventEvidenceId, NativeSupernaturalTransitionRequest request);
    NativeSupernaturalEligibilityVerdict EvaluateRequirement(string characterId, string influenceId,
        string requirementKey, NativeSupernaturalTransitionRequest request);
}
