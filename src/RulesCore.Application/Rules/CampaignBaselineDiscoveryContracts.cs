namespace RulesCore.Application.Rules;

public sealed record CampaignBaselineCandidateSummaryView(
    Guid RulesetRevisionId,
    int RevisionNumber,
    string Fingerprint,
    string PublishedByUserId,
    DateTimeOffset PublishedAt,
    int EntryCount,
    bool IsSelectedBaseline,
    bool IsPublishedCampaignBaseline);

public sealed record CampaignBaselineMigrationPreviewView(
    Guid CampaignId,
    CampaignBaselineReferenceView? CurrentSelectedBaseline,
    CampaignBaselineReferenceView? PublishedCampaignBaseline,
    CampaignBaselineReferenceView CandidateBaseline,
    int AddedConceptCount,
    int RemovedConceptCount,
    int ChangedConceptCount,
    int UnchangedConceptCount,
    int OverridesRemainingActiveCount,
    int OverridesBecomingInactiveCount,
    int HistoricalOverridesBecomingActiveCount,
    IReadOnlyList<CampaignBaselineConceptChangeView> Changes);

public sealed record CampaignBaselineReferenceView(
    Guid RulesetRevisionId,
    int RevisionNumber,
    string Fingerprint,
    DateTimeOffset PublishedAt);

public sealed record CampaignBaselineConceptChangeView(
    Guid RuleConceptId,
    string ConceptKey,
    string EntityType,
    string DisplayName,
    string ChangeKind,
    int? CurrentGlobalDecisionNumber,
    string? CurrentGlobalDecisionKind,
    int? CandidateGlobalDecisionNumber,
    string? CandidateGlobalDecisionKind,
    int? LatestCampaignDecisionNumber,
    string? LatestCampaignDecisionKind,
    string? OverrideImpact);

public interface ICampaignBaselineDiscoveryService
{
    Task<IReadOnlyList<CampaignBaselineCandidateSummaryView>> GetCandidatesAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<CampaignBaselineMigrationPreviewView?> PreviewAsync(
        Guid campaignId,
        Guid rulesetRevisionId,
        CancellationToken cancellationToken = default);
}
