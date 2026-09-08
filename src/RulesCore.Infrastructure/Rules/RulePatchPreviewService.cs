using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

public sealed class RulePatchPreviewService(RulesCoreDbContext dbContext) : IRulePatchPreviewService
{
    public async Task<RulePatchPreviewView?> PreviewGlobalAsync(
        Guid ruleConceptId,
        SetGlobalRuleDecisionRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireGuid(ruleConceptId, nameof(ruleConceptId));
        RequireGuid(request.SourceEntityRevisionId, nameof(request.SourceEntityRevisionId));
        var normalizedUserId = RequireUserId(userId);

        var concept = await dbContext.RuleConcepts
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == ruleConceptId, cancellationToken)
            ?? throw new KeyNotFoundException($"Rule concept '{ruleConceptId}' does not exist.");

        var sourceRevision = await GetAccessibleSourceRevisionAsync(
            request.SourceEntityRevisionId,
            normalizedUserId,
            cancellationToken);
        if (sourceRevision is null)
        {
            return null;
        }

        var sourceIsBound = await dbContext.RuleConceptSourceBindings
            .AsNoTracking()
            .AnyAsync(
                value => value.RuleConceptId == ruleConceptId
                    && value.SourceEntityId == sourceRevision.SourceEntityId,
                cancellationToken);
        if (!sourceIsBound)
        {
            throw new InvalidOperationException(
                "The selected source revision belongs to an entity that is not bound to this rule concept.");
        }

        var candidate = NormalizeGlobalCandidate(request);
        using var sourceDocument = JsonDocument.Parse(sourceRevision.RawJson);
        var baseDocument = sourceDocument.RootElement.Clone();
        var previewDocument = ApplyCandidate(
            baseDocument,
            candidate.DecisionKind,
            candidate.PatchJson);

        return new RulePatchPreviewView(
            concept.Id,
            concept.Key,
            concept.EntityType,
            concept.DisplayName,
            "global",
            null,
            candidate.DecisionKind,
            sourceRevision.Id,
            sourceRevision.Id,
            candidate.PatchFingerprint,
            candidate.MergePatch,
            candidate.StructuredPatch,
            baseDocument,
            previewDocument,
            JsonDocumentDiff.Compare(baseDocument, previewDocument));
    }

    public async Task<RulePatchPreviewView?> PreviewCampaignAsync(
        Guid campaignId,
        Guid ruleConceptId,
        SetCampaignRuleDecisionRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireGuid(campaignId, nameof(campaignId));
        RequireGuid(ruleConceptId, nameof(ruleConceptId));
        var normalizedUserId = RequireUserId(userId);

        var baseline = await dbContext.CampaignRulesetSelections
            .AsNoTracking()
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.SelectionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (baseline is null)
        {
            throw new InvalidOperationException(
                "The campaign must deliberately select a published global ruleset revision before previewing overrides.");
        }

        var baselineEntry = await dbContext.RulesetRevisionEntries
            .AsNoTracking()
            .Include(value => value.RuleConcept)
            .Include(value => value.GlobalRuleDecision)
            .Include(value => value.SourceEntityRevision)
                .ThenInclude(value => value.SourceEntity)
                .ThenInclude(value => value.SourceEdition)
                .ThenInclude(value => value.SourceWork)
                .ThenInclude(value => value.SourcePackage)
            .SingleOrDefaultAsync(
                value => value.RulesetRevisionId == baseline.RulesetRevisionId
                    && value.RuleConceptId == ruleConceptId
                    && (value.SourceEntityRevision.SourceEntity.SourceEdition.SourceWork.SourcePackage.IsPublic
                        || value.SourceEntityRevision.SourceEntity.SourceEdition.SourceWork.SourcePackage.UserGrants
                            .Any(grant => grant.UserId == normalizedUserId)),
                cancellationToken);
        if (baselineEntry is null)
        {
            var conceptExistsInBaseline = await dbContext.RulesetRevisionEntries
                .AsNoTracking()
                .AnyAsync(
                    value => value.RulesetRevisionId == baseline.RulesetRevisionId
                        && value.RuleConceptId == ruleConceptId,
                    cancellationToken);
            if (!conceptExistsInBaseline)
            {
                throw new KeyNotFoundException(
                    $"Rule concept '{ruleConceptId}' does not exist in the campaign's selected global ruleset revision.");
            }

            return null;
        }

        var concept = baselineEntry.RuleConcept;
        var globalDecision = baselineEntry.GlobalRuleDecision;
        var baselineSourceRevision = baselineEntry.SourceEntityRevision;
        using var baselineSourceDocument = JsonDocument.Parse(baselineSourceRevision.RawJson);
        var baseDocument = ApplyCandidate(
            baselineSourceDocument.RootElement,
            globalDecision.DecisionKind,
            globalDecision.PatchJson);

        var candidate = await NormalizeCampaignCandidateAsync(
            ruleConceptId,
            request,
            normalizedUserId,
            cancellationToken);

        JsonElement previewDocument;
        Guid effectiveSourceRevisionId;
        if (candidate.DecisionKind == CampaignRuleDecisionKinds.SelectSource)
        {
            var selectedRevision = candidate.SelectedSourceRevision
                ?? throw new InvalidOperationException(
                    "A select-source preview is missing its selected source revision.");
            using var selectedDocument = JsonDocument.Parse(selectedRevision.RawJson);
            previewDocument = selectedDocument.RootElement.Clone();
            effectiveSourceRevisionId = selectedRevision.Id;
        }
        else
        {
            previewDocument = ApplyCandidate(
                baseDocument,
                candidate.DecisionKind,
                candidate.PatchJson);
            effectiveSourceRevisionId = baselineSourceRevision.Id;
        }

        return new RulePatchPreviewView(
            concept.Id,
            concept.Key,
            concept.EntityType,
            concept.DisplayName,
            "campaign",
            campaignId,
            candidate.DecisionKind,
            baselineSourceRevision.Id,
            effectiveSourceRevisionId,
            candidate.PatchFingerprint,
            candidate.MergePatch,
            candidate.StructuredPatch,
            baseDocument,
            previewDocument,
            JsonDocumentDiff.Compare(baseDocument, previewDocument));
    }

    private async Task<CampaignCandidate> NormalizeCampaignCandidateAsync(
        Guid ruleConceptId,
        SetCampaignRuleDecisionRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        var decisionKind = RequireText(request.DecisionKind, nameof(request.DecisionKind), 80)
            .ToLowerInvariant();
        if (!CampaignRuleDecisionKinds.All.Contains(decisionKind))
        {
            throw new ArgumentException(
                $"Unsupported campaign rule decision kind '{request.DecisionKind}'.",
                nameof(request.DecisionKind));
        }

        switch (decisionKind)
        {
            case CampaignRuleDecisionKinds.SelectSource:
            {
                if (request.SourceEntityRevisionId is null || request.SourceEntityRevisionId == Guid.Empty)
                {
                    throw new ArgumentException(
                        "A source entity revision is required for a select-source campaign decision.",
                        nameof(request.SourceEntityRevisionId));
                }
                RejectPatches(request, decisionKind);

                var sourceRevision = await GetAccessibleSourceRevisionAsync(
                    request.SourceEntityRevisionId.Value,
                    userId,
                    cancellationToken);
                if (sourceRevision is null)
                {
                    return new CampaignCandidate(decisionKind, null, null, null, null, null);
                }

                var sourceIsBound = await dbContext.RuleConceptSourceBindings
                    .AsNoTracking()
                    .AnyAsync(
                        value => value.RuleConceptId == ruleConceptId
                            && value.SourceEntityId == sourceRevision.SourceEntityId,
                        cancellationToken);
                if (!sourceIsBound)
                {
                    throw new InvalidOperationException(
                        "The selected source revision belongs to an entity that is not bound to this rule concept.");
                }

                return new CampaignCandidate(
                    decisionKind,
                    sourceRevision,
                    null,
                    null,
                    null,
                    null);
            }

            case CampaignRuleDecisionKinds.InheritGlobal:
                if (request.SourceEntityRevisionId is not null)
                {
                    throw new ArgumentException(
                        "An inherit-global campaign decision can not select a source entity revision.",
                        nameof(request.SourceEntityRevisionId));
                }
                RejectPatches(request, decisionKind);
                return new CampaignCandidate(decisionKind, null, null, null, null, null);

            case CampaignRuleDecisionKinds.JsonMergePatch:
            {
                RejectSourceRevision(request, decisionKind);
                if (!request.MergePatch.HasValue)
                {
                    throw new ArgumentException(
                        "A merge patch is required for a json-merge-patch campaign decision.",
                        nameof(request.MergePatch));
                }
                if (request.StructuredPatch is not null)
                {
                    throw new ArgumentException(
                        "A json-merge-patch campaign decision can not include a structured rule patch.",
                        nameof(request.StructuredPatch));
                }

                var patch = JsonMergePatch.Normalize(request.MergePatch.Value);
                return new CampaignCandidate(
                    decisionKind,
                    null,
                    patch.Json,
                    patch.Fingerprint,
                    JsonMergePatch.ParsePatch(patch.Json),
                    null);
            }

            case CampaignRuleDecisionKinds.JsonRulePatch:
            {
                RejectSourceRevision(request, decisionKind);
                if (request.MergePatch.HasValue)
                {
                    throw new ArgumentException(
                        "A json-rule-patch campaign decision can not include a legacy merge patch field.",
                        nameof(request.MergePatch));
                }
                var patch = request.StructuredPatch is not null
                    ? JsonRulePatch.Normalize(request.StructuredPatch)
                    : throw new ArgumentException(
                        "A structured patch is required for a json-rule-patch campaign decision.",
                        nameof(request.StructuredPatch));
                return new CampaignCandidate(
                    decisionKind,
                    null,
                    patch.Json,
                    patch.Fingerprint,
                    null,
                    JsonRulePatch.ParsePatch(patch.Json));
            }

            default:
                throw new InvalidOperationException("Unsupported campaign decision kind.");
        }
    }

    private static GlobalCandidate NormalizeGlobalCandidate(SetGlobalRuleDecisionRequest request)
    {
        if (request.MergePatch.HasValue && request.StructuredPatch is not null)
        {
            throw new ArgumentException(
                "A global rule decision can not include both a legacy JSON merge patch and a structured rule patch.");
        }

        if (request.StructuredPatch is not null)
        {
            var patch = JsonRulePatch.Normalize(request.StructuredPatch);
            return new GlobalCandidate(
                RuleDecisionKinds.JsonRulePatch,
                patch.Json,
                patch.Fingerprint,
                null,
                JsonRulePatch.ParsePatch(patch.Json));
        }

        if (request.MergePatch.HasValue)
        {
            var patch = JsonMergePatch.Normalize(request.MergePatch.Value);
            return new GlobalCandidate(
                RuleDecisionKinds.JsonMergePatch,
                patch.Json,
                patch.Fingerprint,
                JsonMergePatch.ParsePatch(patch.Json),
                null);
        }

        return new GlobalCandidate(
            RuleDecisionKinds.SelectSource,
            null,
            null,
            null,
            null);
    }

    private async Task<SourceEntityRevision?> GetAccessibleSourceRevisionAsync(
        Guid sourceEntityRevisionId,
        string userId,
        CancellationToken cancellationToken) =>
        await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Include(value => value.SourceEntity)
                .ThenInclude(value => value.SourceEdition)
                .ThenInclude(value => value.SourceWork)
                .ThenInclude(value => value.SourcePackage)
            .SingleOrDefaultAsync(
                value => value.Id == sourceEntityRevisionId
                    && (value.SourceEntity.SourceEdition.SourceWork.SourcePackage.IsPublic
                        || value.SourceEntity.SourceEdition.SourceWork.SourcePackage.UserGrants
                            .Any(grant => grant.UserId == userId)),
                cancellationToken);

    private static JsonElement ApplyCandidate(
        JsonElement source,
        string decisionKind,
        string? patchJson) => decisionKind switch
    {
        RuleDecisionKinds.JsonMergePatch or CampaignRuleDecisionKinds.JsonMergePatch =>
            JsonMergePatch.Apply(source, patchJson),
        RuleDecisionKinds.JsonRulePatch or CampaignRuleDecisionKinds.JsonRulePatch =>
            JsonRulePatch.Apply(source, patchJson),
        _ => source.Clone()
    };

    private static void RejectPatches(SetCampaignRuleDecisionRequest request, string decisionKind)
    {
        if (request.MergePatch.HasValue)
        {
            throw new ArgumentException(
                $"A {decisionKind} campaign decision can not include a merge patch.",
                nameof(request.MergePatch));
        }
        if (request.StructuredPatch is not null)
        {
            throw new ArgumentException(
                $"A {decisionKind} campaign decision can not include a structured rule patch.",
                nameof(request.StructuredPatch));
        }
    }

    private static void RejectSourceRevision(
        SetCampaignRuleDecisionRequest request,
        string decisionKind)
    {
        if (request.SourceEntityRevisionId is not null)
        {
            throw new ArgumentException(
                $"A {decisionKind} campaign decision can not select a separate source revision.",
                nameof(request.SourceEntityRevisionId));
        }
    }

    private static void RequireGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Value can not be an empty GUID.", parameterName);
        }
    }

    private static string RequireUserId(string value) =>
        RequireText(value, nameof(value), 200);

    private static string RequireText(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value can not be blank.", parameterName);
        }
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not exceed {maxLength} characters.", parameterName);
        }
        return normalized;
    }

    private sealed record GlobalCandidate(
        string DecisionKind,
        string? PatchJson,
        string? PatchFingerprint,
        JsonElement? MergePatch,
        JsonElement? StructuredPatch);

    private sealed record CampaignCandidate(
        string DecisionKind,
        SourceEntityRevision? SelectedSourceRevision,
        string? PatchJson,
        string? PatchFingerprint,
        JsonElement? MergePatch,
        JsonElement? StructuredPatch);
}
