namespace RulesCore.Application.Rules;

public sealed record RejectLatestSourceRevisionRequest(
    Guid ExpectedGlobalRuleDecisionId,
    Guid ExpectedLatestSourceEntityRevisionId,
    string ExpectedLatestFingerprint,
    string Reason);

public sealed record SourceRevisionRejectionView(
    Guid Id,
    Guid RuleConceptId,
    string ConceptKey,
    Guid GlobalRuleDecisionId,
    Guid SourceEntityRevisionId,
    int SourceRevisionNumber,
    string SourceFingerprint,
    string Reason,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    bool Created);
