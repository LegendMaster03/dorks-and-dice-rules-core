using System.Text.Json;

namespace RulesCore.Application.Rules;

public sealed record CreateRuleConceptRequest(
    string Key,
    string EntityType,
    string DisplayName);

public sealed record BindRuleConceptSourceRequest(
    Guid SourceEntityId);

public sealed record SetGlobalRuleDecisionRequest(
    Guid SourceEntityRevisionId,
    string? Note,
    JsonElement? MergePatch = null,
    RuleStructuredPatchRequest? StructuredPatch = null,
    IReadOnlyList<RuleConsolidationContributionRequest>? Contributions = null);

public sealed record RuleMutationResult<T>(
    T Value,
    bool Created);

public sealed record RuleConceptView(
    Guid Id,
    string Key,
    string EntityType,
    string DisplayName,
    string CreatedByUserId,
    DateTimeOffset CreatedAt);

public sealed record RuleConceptSourceBindingView(
    Guid Id,
    Guid RuleConceptId,
    Guid SourceEntityId,
    string CreatedByUserId,
    DateTimeOffset CreatedAt);

public sealed record GlobalRuleDecisionView(
    Guid Id,
    Guid RuleConceptId,
    int DecisionNumber,
    string DecisionKind,
    Guid SourceEntityRevisionId,
    Guid SourceEntityId,
    int SourceRevisionNumber,
    string SourceFingerprint,
    string? PatchFingerprint,
    JsonElement? MergePatch,
    JsonElement? StructuredPatch,
    string? Note,
    string CreatedByUserId,
    DateTimeOffset CreatedAt);

public sealed record PublishedRulesetRevisionView(
    Guid Id,
    int RevisionNumber,
    string Fingerprint,
    string PublishedByUserId,
    DateTimeOffset PublishedAt,
    int EntryCount,
    bool CreatedRevision);

public sealed record ResolvedRuleView(
    Guid RuleConceptId,
    string ConceptKey,
    string EntityType,
    string DisplayName,
    int RulesetRevisionNumber,
    string RulesetFingerprint,
    DateTimeOffset RulesetPublishedAt,
    Guid GlobalRuleDecisionId,
    int GlobalDecisionNumber,
    string DecisionKind,
    string? DecisionNote,
    string? GlobalPatchFingerprint,
    JsonElement? GlobalMergePatch,
    JsonElement? GlobalStructuredPatch,
    Guid SourceEntityId,
    Guid SourceEntityRevisionId,
    int SourceRevisionNumber,
    string SourceFingerprint,
    string SourceEntityName,
    string SourceCode,
    string PackageKey,
    string PackageDisplayName,
    string WorkKey,
    string WorkDisplayName,
    string EditionKey,
    string EditionDisplayName,
    JsonElement Document);

public interface IGlobalRulesService
{
    Task<RuleMutationResult<RuleConceptView>> CreateConceptAsync(
        CreateRuleConceptRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<RuleMutationResult<RuleConceptSourceBindingView>> BindSourceEntityAsync(
        Guid ruleConceptId,
        BindRuleConceptSourceRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<RuleMutationResult<GlobalRuleDecisionView>> SetDecisionAsync(
        Guid ruleConceptId,
        SetGlobalRuleDecisionRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<PublishedRulesetRevisionView> PublishAsync(
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<ResolvedRuleView?> ResolveLatestAsync(
        string conceptKey,
        string? userId,
        CancellationToken cancellationToken = default);
}
