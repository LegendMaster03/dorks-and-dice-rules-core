using System.Text.Json;

namespace RulesCore.Application.Rules;

public static class RuleAdjudicationWorkKinds
{
    public const string NormalizationReview = "normalization-review";
    public const string GlobalRuleAdjudication = "global-rule-adjudication";
    public const string SourceUpdateReview = "source-update-review";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [NormalizationReview, GlobalRuleAdjudication, SourceUpdateReview],
        StringComparer.Ordinal);
}

public static class RuleAdjudicationWorkStates
{
    public const string Pending = "Pending";
    public const string AgentReview = "AgentReview";
    public const string WaitingForHuman = "WaitingForHuman";
    public const string ManualResolutionRequired = "ManualResolutionRequired";
    public const string Completed = "Completed";
    public const string Deferred = "Deferred";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [Pending, AgentReview, WaitingForHuman, ManualResolutionRequired, Completed, Deferred],
        StringComparer.Ordinal);
}

public static class RuleAdjudicationWorkEventKinds
{
    public const string Discovered = "work-discovered";
    public const string ReviewStarted = "review-started";
    public const string DeterministicResolutionApplied = "deterministic-auto-resolution-applied";
    public const string DeterministicResolutionRequiresReview = "deterministic-review-required";
    public const string NormalizationAccepted = "normalization-accepted";
    public const string ClarificationRequested = "clarification-requested";
    public const string ClarificationAnswered = "clarification-answered";
    public const string ManualEscalation = "manual-escalation";
    public const string Deferred = "deferred";
    public const string Reopened = "reopened";
    public const string DecisionAssociated = "decision-associated";
    public const string SourceUpdateResolved = "source-update-resolved";
    public const string PublicationObserved = "publication-observed";
}

public sealed record RuleAdjudicationDiscoveryView(
    int NormalizationWorkItems,
    int RuleAdjudicationWorkItems,
    int SourceUpdateWorkItems,
    int DeterministicDecisionsApplied,
    int OutstandingWorkItems);

public sealed record RuleAdjudicationWorkSummaryView(
    Guid Id,
    string WorkKind,
    string State,
    int Version,
    Guid? RuleConceptId,
    Guid? SourceEntityId,
    string? ConceptKey,
    string? DisplayName,
    string? EntityType,
    bool HasOutstandingClarification,
    bool CompletedButUnpublished,
    bool Published,
    string? DeterministicReason,
    string? ManualReason,
    string? DeferredReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record RuleAdjudicationClarificationView(
    string Question,
    string RequestedByActorId,
    DateTimeOffset RequestedAt,
    int RequestedAtVersion,
    string? Answer,
    string? AnsweredByActorId,
    DateTimeOffset? AnsweredAt);

public sealed record RuleAdjudicationWorkEventView(
    Guid Id,
    string EventKind,
    string? ActorUserId,
    int WorkVersion,
    string? Message,
    Guid? RuleConceptId,
    Guid? SourceEntityId,
    Guid? GlobalRuleDecisionId,
    Guid? RulesetRevisionId,
    DateTimeOffset OccurredAt);

public sealed record RuleAdjudicationSourceRevisionEvidenceView(
    Guid SourceEntityId,
    Guid SourceEntityRevisionId,
    int RevisionNumber,
    string Fingerprint,
    string SourceEntityName,
    string SourceCode,
    string PackageKey,
    string PackageDisplayName,
    string EditionKey,
    string EditionDisplayName,
    DateTimeOffset ImportedAt,
    JsonElement Document);

public sealed record RuleDeterministicResolutionEvidenceView(
    bool Eligible,
    bool Applied,
    Guid? DecisionId,
    int? DecisionNumber,
    string Reason);

public sealed record RuleAdjudicationDecisionGuardView(
    Guid? ExpectedLatestDecisionId,
    int? ExpectedLatestDecisionNumber);

public sealed record RuleAdjudicationWorkDetailView(
    RuleAdjudicationWorkSummaryView WorkItem,
    SourceNormalizationCandidateView? NormalizationCandidate,
    GlobalRuleAuthoringConceptView? Rule,
    IReadOnlyList<RuleAdjudicationSourceRevisionEvidenceView> AccessibleSourceRevisions,
    IReadOnlyList<RuleSemanticComparisonView> SemanticComparisons,
    RuleDeterministicResolutionEvidenceView? DeterministicResolution,
    SourceRevisionReviewPreviewView? SourceUpdate,
    RuleAdjudicationDecisionGuardView? DecisionGuard,
    RuleAdjudicationClarificationView? Clarification,
    IReadOnlyList<RuleAdjudicationWorkEventView> History);

public sealed record RuleAdjudicationVersionRequest(int ExpectedVersion);

public sealed record RequestRuleAdjudicationClarificationRequest(
    int ExpectedVersion,
    string Question);

public sealed record AnswerRuleAdjudicationClarificationRequest(
    int ExpectedVersion,
    string Answer);

public sealed record EscalateRuleAdjudicationWorkRequest(
    int ExpectedVersion,
    string Reason);

public sealed record DeferRuleAdjudicationWorkRequest(
    int ExpectedVersion,
    string Reason);
