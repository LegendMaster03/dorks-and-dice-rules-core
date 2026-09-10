using System.Text.Json;

namespace RulesCore.Application.Rules;

public sealed record SourceRevisionReviewItemView(
    Guid RuleConceptId,
    string ConceptKey,
    string EntityType,
    string DisplayName,
    Guid GlobalRuleDecisionId,
    int GlobalDecisionNumber,
    string DecisionKind,
    Guid SourceEntityId,
    string SourceEntityName,
    string SourceCode,
    string PackageKey,
    string PackageDisplayName,
    string EditionKey,
    string EditionDisplayName,
    Guid SelectedSourceEntityRevisionId,
    int SelectedRevisionNumber,
    string SelectedFingerprint,
    DateTimeOffset SelectedImportedAt,
    Guid LatestSourceEntityRevisionId,
    int LatestRevisionNumber,
    string LatestFingerprint,
    DateTimeOffset LatestImportedAt,
    int NewerRevisionCount);

public sealed record SourceRevisionReviewPreviewView(
    SourceRevisionReviewItemView Update,
    bool PatchCompatible,
    string? CompatibilityMessage,
    JsonElement CurrentResolvedDocument,
    JsonElement? CandidateResolvedDocument,
    IReadOnlyList<RuleDocumentChangeView> Changes);

public interface ISourceRevisionReviewService
{
    Task<IReadOnlyList<SourceRevisionReviewItemView>> GetPendingAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<SourceRevisionReviewPreviewView?> PreviewAsync(
        Guid ruleConceptId,
        string userId,
        CancellationToken cancellationToken = default);
}
