using System.Text.Json;

namespace RulesCore.Application.Rules;

public sealed record SelectCampaignRulesetBaselineRequest(
    Guid RulesetRevisionId);

public sealed record SetCampaignRuleDecisionRequest(
    string DecisionKind,
    Guid? SourceEntityRevisionId,
    string? Note,
    JsonElement? MergePatch = null,
    RuleStructuredPatchRequest? StructuredPatch = null);

public sealed record CampaignRulesetSelectionView(
    Guid Id,
    Guid CampaignId,
    int SelectionNumber,
    Guid RulesetRevisionId,
    int RulesetRevisionNumber,
    string RulesetFingerprint,
    string SelectedByUserId,
    DateTimeOffset SelectedAt,
    bool Created);

public sealed record CampaignRuleDecisionView(
    Guid Id,
    Guid CampaignId,
    Guid RuleConceptId,
    int DecisionNumber,
    string DecisionKind,
    Guid? SourceEntityRevisionId,
    string? PatchFingerprint,
    JsonElement? MergePatch,
    JsonElement? StructuredPatch,
    string? Note,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    bool Created);

public sealed record PublishedCampaignRulesetRevisionView(
    Guid Id,
    Guid CampaignId,
    int RevisionNumber,
    string Fingerprint,
    Guid BaselineSelectionId,
    Guid BaselineRulesetRevisionId,
    int BaselineRulesetRevisionNumber,
    string BaselineRulesetFingerprint,
    string PublishedByUserId,
    DateTimeOffset PublishedAt,
    int EntryCount,
    bool CreatedRevision);

public sealed record ResolvedCampaignRuleView(
    Guid CampaignId,
    Guid RuleConceptId,
    string ConceptKey,
    string EntityType,
    string DisplayName,
    int CampaignRulesetRevisionNumber,
    string CampaignRulesetFingerprint,
    DateTimeOffset CampaignRulesetPublishedAt,
    Guid BaselineRulesetRevisionId,
    int BaselineRulesetRevisionNumber,
    string BaselineRulesetFingerprint,
    Guid GlobalRuleDecisionId,
    int GlobalDecisionNumber,
    string GlobalDecisionKind,
    string? GlobalPatchFingerprint,
    JsonElement? GlobalMergePatch,
    JsonElement? GlobalStructuredPatch,
    IReadOnlyList<ResolvedRuleContributionView> GlobalContributions,
    Guid? CampaignRuleDecisionId,
    int? CampaignDecisionNumber,
    string EffectiveDecisionKind,
    string? CampaignDecisionNote,
    string? CampaignPatchFingerprint,
    JsonElement? CampaignMergePatch,
    JsonElement? CampaignStructuredPatch,
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

public interface ICampaignRulesService
{
    Task<CampaignRulesetSelectionView> SelectBaselineAsync(
        Guid campaignId,
        SelectCampaignRulesetBaselineRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<CampaignRuleDecisionView> SetDecisionAsync(
        Guid campaignId,
        Guid ruleConceptId,
        SetCampaignRuleDecisionRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<PublishedCampaignRulesetRevisionView> PublishAsync(
        Guid campaignId,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<ResolvedCampaignRuleView?> ResolveLatestAsync(
        Guid campaignId,
        string conceptKey,
        string? userId,
        CancellationToken cancellationToken = default);
}
