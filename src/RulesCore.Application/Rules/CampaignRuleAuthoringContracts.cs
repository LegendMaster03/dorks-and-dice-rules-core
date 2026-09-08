namespace RulesCore.Application.Rules;

public sealed record CampaignRulesAuthoringOverviewView(
    Guid CampaignId,
    CampaignRulesetSelectionAuthoringSummaryView? SelectedBaseline,
    PublishedCampaignRulesetAuthoringSummaryView? LatestPublishedRuleset,
    bool HasUnpublishedBaselineChange,
    bool NeedsPublication,
    int ConceptCount,
    int ConceptsWithOverrides,
    int PendingOverrideCount,
    IReadOnlyList<CampaignRuleAuthoringConceptSummaryView> Concepts);

public sealed record CampaignRulesetSelectionAuthoringSummaryView(
    Guid Id,
    int SelectionNumber,
    Guid RulesetRevisionId,
    int RulesetRevisionNumber,
    string RulesetFingerprint,
    string SelectedByUserId,
    DateTimeOffset SelectedAt);

public sealed record PublishedCampaignRulesetAuthoringSummaryView(
    Guid Id,
    int RevisionNumber,
    string Fingerprint,
    Guid BaselineSelectionId,
    Guid BaselineRulesetRevisionId,
    int BaselineRulesetRevisionNumber,
    string BaselineRulesetFingerprint,
    string PublishedByUserId,
    DateTimeOffset PublishedAt,
    int EntryCount);

public sealed record CampaignRuleAuthoringConceptSummaryView(
    Guid Id,
    string Key,
    string EntityType,
    string DisplayName,
    Guid BaselineGlobalDecisionId,
    int BaselineGlobalDecisionNumber,
    string BaselineGlobalDecisionKind,
    Guid? LatestCampaignDecisionId,
    int? LatestCampaignDecisionNumber,
    string? LatestCampaignDecisionKind,
    Guid? PublishedCampaignDecisionId,
    bool HasUnpublishedOverrideChange);

public sealed record CampaignRuleAuthoringConceptView(
    Guid CampaignId,
    CampaignRulesetSelectionAuthoringSummaryView SelectedBaseline,
    RuleConceptView Concept,
    GlobalRuleDecisionView BaselineGlobalDecision,
    IReadOnlyList<RuleConceptSourceBindingView> Bindings,
    IReadOnlyList<GlobalRuleAuthoringSourceView> AccessibleSources,
    int RestrictedBindingCount,
    CampaignRuleDecisionView? LatestCampaignDecision,
    Guid? PublishedCampaignDecisionId,
    int? PublishedCampaignRulesetRevisionNumber,
    bool HasUnpublishedBaselineChange,
    bool HasUnpublishedOverrideChange);

public interface ICampaignRulesAuthoringService
{
    Task<CampaignRulesAuthoringOverviewView> GetOverviewAsync(
        Guid campaignId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<CampaignRuleAuthoringConceptView?> GetConceptAsync(
        Guid campaignId,
        Guid ruleConceptId,
        string userId,
        CancellationToken cancellationToken = default);
}
