using System.Text.Json;

namespace RulesCore.Application.Rules;

public sealed record GlobalRulesAuthoringOverviewView(
    PublishedRulesetAuthoringSummaryView? LatestPublishedRuleset,
    int ConceptCount,
    int ConceptsWithDecisions,
    int PendingDecisionCount,
    IReadOnlyList<GlobalRuleAuthoringConceptSummaryView> Concepts);

public sealed record PublishedRulesetAuthoringSummaryView(
    Guid Id,
    int RevisionNumber,
    string Fingerprint,
    string PublishedByUserId,
    DateTimeOffset PublishedAt,
    int EntryCount);

public sealed record GlobalRuleAuthoringConceptSummaryView(
    Guid Id,
    string Key,
    string EntityType,
    string DisplayName,
    int BindingCount,
    int? LatestDecisionNumber,
    string? LatestDecisionKind,
    Guid? LatestDecisionId,
    Guid? PublishedDecisionId,
    bool HasUnpublishedChanges);

public sealed record GlobalRuleAuthoringConceptView(
    RuleConceptView Concept,
    IReadOnlyList<RuleConceptSourceBindingView> Bindings,
    IReadOnlyList<GlobalRuleAuthoringSourceView> AccessibleSources,
    int RestrictedBindingCount,
    GlobalRuleDecisionView? LatestDecision,
    Guid? PublishedDecisionId,
    int? PublishedRulesetRevisionNumber,
    bool HasUnpublishedChanges);

public sealed record GlobalRuleAuthoringSourceView(
    Guid SourceEntityId,
    string EntityType,
    string Name,
    string SourceCode,
    string PackageKey,
    string PackageDisplayName,
    string WorkKey,
    string WorkDisplayName,
    string EditionKey,
    string EditionDisplayName,
    IReadOnlyList<GlobalRuleAuthoringSourceRevisionView> Revisions);

public sealed record GlobalRuleAuthoringSourceRevisionView(
    Guid Id,
    int RevisionNumber,
    string Fingerprint,
    DateTimeOffset ImportedAt);

public interface IGlobalRulesAuthoringService
{
    Task<GlobalRulesAuthoringOverviewView> GetOverviewAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<GlobalRuleAuthoringConceptView?> GetConceptAsync(
        Guid ruleConceptId,
        string userId,
        CancellationToken cancellationToken = default);
}
