using System.Text.Json;

namespace RulesCore.Application.Rules;

public static class RuleDocumentChangeKinds
{
    public const string Add = "add";
    public const string Remove = "remove";
    public const string Replace = "replace";
}

public sealed record RuleDocumentChangeView(
    string Path,
    string ChangeKind,
    JsonElement? Before,
    JsonElement? After);

public sealed record RulePatchPreviewView(
    Guid RuleConceptId,
    string ConceptKey,
    string EntityType,
    string DisplayName,
    string Scope,
    Guid? CampaignId,
    string DecisionKind,
    Guid BaseSourceEntityRevisionId,
    Guid EffectiveSourceEntityRevisionId,
    string? PatchFingerprint,
    JsonElement? MergePatch,
    JsonElement? StructuredPatch,
    JsonElement BaseDocument,
    JsonElement PreviewDocument,
    IReadOnlyList<RuleDocumentChangeView> Changes);

public interface IRulePatchPreviewService
{
    Task<RulePatchPreviewView?> PreviewGlobalAsync(
        Guid ruleConceptId,
        SetGlobalRuleDecisionRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<RulePatchPreviewView?> PreviewCampaignAsync(
        Guid campaignId,
        Guid ruleConceptId,
        SetCampaignRuleDecisionRequest request,
        string userId,
        CancellationToken cancellationToken = default);
}
