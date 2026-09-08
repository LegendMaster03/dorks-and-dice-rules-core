using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

public sealed class CampaignRulesAuthoringService(RulesCoreDbContext dbContext)
    : ICampaignRulesAuthoringService
{
    public async Task<CampaignRulesAuthoringOverviewView> GetOverviewAsync(
        Guid campaignId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        RequireGuid(campaignId, nameof(campaignId));
        _ = RequireUserId(userId);

        var selectedBaseline = await dbContext.CampaignRulesetSelections
            .AsNoTracking()
            .Include(value => value.RulesetRevision)
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.SelectionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var latestPublished = await dbContext.CampaignRulesetRevisions
            .AsNoTracking()
            .Include(value => value.BaselineSelection)
                .ThenInclude(value => value.RulesetRevision)
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        PublishedCampaignRulesetAuthoringSummaryView? publishedSummary = null;
        Dictionary<Guid, Guid?> publishedDecisionIds = [];
        if (latestPublished is not null)
        {
            var publishedEntries = await dbContext.CampaignRulesetRevisionEntries
                .AsNoTracking()
                .Where(value => value.CampaignRulesetRevisionId == latestPublished.Id)
                .Select(value => new
                {
                    value.RuleConceptId,
                    value.CampaignRuleDecisionId
                })
                .ToArrayAsync(cancellationToken);

            publishedDecisionIds = publishedEntries.ToDictionary(
                value => value.RuleConceptId,
                value => value.CampaignRuleDecisionId);
            publishedSummary = ToSummary(latestPublished, publishedEntries.Length);
        }

        if (selectedBaseline is null)
        {
            return new CampaignRulesAuthoringOverviewView(
                campaignId,
                null,
                publishedSummary,
                HasUnpublishedBaselineChange: false,
                NeedsPublication: false,
                ConceptCount: 0,
                ConceptsWithOverrides: 0,
                PendingOverrideCount: 0,
                Concepts: []);
        }

        var baselineSummary = ToSummary(selectedBaseline);
        var baselineEntries = await dbContext.RulesetRevisionEntries
            .AsNoTracking()
            .Include(value => value.RuleConcept)
            .Include(value => value.GlobalRuleDecision)
            .Where(value => value.RulesetRevisionId == selectedBaseline.RulesetRevisionId)
            .OrderBy(value => value.RuleConcept.Key)
            .ToArrayAsync(cancellationToken);

        var baselineConceptIds = baselineEntries
            .Select(value => value.RuleConceptId)
            .ToArray();
        var allCampaignDecisions = baselineConceptIds.Length == 0
            ? []
            : await dbContext.CampaignRuleDecisions
                .AsNoTracking()
                .Where(value => value.CampaignId == campaignId
                    && baselineConceptIds.Contains(value.RuleConceptId))
                .OrderBy(value => value.RuleConceptId)
                .ThenByDescending(value => value.DecisionNumber)
                .ToArrayAsync(cancellationToken);
        var latestDecisions = allCampaignDecisions
            .GroupBy(value => value.RuleConceptId)
            .ToDictionary(group => group.Key, group => group.First());

        var conceptViews = baselineEntries
            .Select(entry =>
            {
                latestDecisions.TryGetValue(entry.RuleConceptId, out var latestDecision);
                publishedDecisionIds.TryGetValue(entry.RuleConceptId, out var publishedDecisionId);
                var hasUnpublishedOverride = latestDecision is not null
                    && publishedDecisionId != latestDecision.Id;

                return new CampaignRuleAuthoringConceptSummaryView(
                    entry.RuleConcept.Id,
                    entry.RuleConcept.Key,
                    entry.RuleConcept.EntityType,
                    entry.RuleConcept.DisplayName,
                    entry.GlobalRuleDecision.Id,
                    entry.GlobalRuleDecision.DecisionNumber,
                    entry.GlobalRuleDecision.DecisionKind,
                    latestDecision?.Id,
                    latestDecision?.DecisionNumber,
                    latestDecision?.DecisionKind,
                    publishedDecisionId,
                    hasUnpublishedOverride);
            })
            .ToArray();

        var hasUnpublishedBaseline = latestPublished is null
            || latestPublished.BaselineSelectionId != selectedBaseline.Id;
        var pendingOverrideCount = conceptViews.Count(value => value.HasUnpublishedOverrideChange);
        return new CampaignRulesAuthoringOverviewView(
            campaignId,
            baselineSummary,
            publishedSummary,
            hasUnpublishedBaseline,
            hasUnpublishedBaseline || pendingOverrideCount > 0,
            conceptViews.Length,
            conceptViews.Count(value => value.LatestCampaignDecisionId is not null),
            pendingOverrideCount,
            conceptViews);
    }

    public async Task<CampaignRuleAuthoringConceptView?> GetConceptAsync(
        Guid campaignId,
        Guid ruleConceptId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        RequireGuid(campaignId, nameof(campaignId));
        RequireGuid(ruleConceptId, nameof(ruleConceptId));
        var normalizedUserId = RequireUserId(userId);

        var selectedBaseline = await dbContext.CampaignRulesetSelections
            .AsNoTracking()
            .Include(value => value.RulesetRevision)
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.SelectionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (selectedBaseline is null)
        {
            return null;
        }

        var baselineEntry = await dbContext.RulesetRevisionEntries
            .AsNoTracking()
            .Include(value => value.RuleConcept)
            .Include(value => value.GlobalRuleDecision)
                .ThenInclude(value => value.SelectedSourceEntityRevision)
                .ThenInclude(value => value.SourceEntity)
            .SingleOrDefaultAsync(
                value => value.RulesetRevisionId == selectedBaseline.RulesetRevisionId
                    && value.RuleConceptId == ruleConceptId,
                cancellationToken);
        if (baselineEntry is null)
        {
            return null;
        }

        var bindings = await dbContext.RuleConceptSourceBindings
            .AsNoTracking()
            .Where(value => value.RuleConceptId == ruleConceptId)
            .OrderBy(value => value.CreatedAt)
            .ThenBy(value => value.Id)
            .ToArrayAsync(cancellationToken);

        var latestCampaignDecision = await dbContext.CampaignRuleDecisions
            .AsNoTracking()
            .Where(value => value.CampaignId == campaignId
                && value.RuleConceptId == ruleConceptId)
            .OrderByDescending(value => value.DecisionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var latestPublished = await dbContext.CampaignRulesetRevisions
            .AsNoTracking()
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        Guid? publishedCampaignDecisionId = null;
        if (latestPublished is not null)
        {
            publishedCampaignDecisionId = await dbContext.CampaignRulesetRevisionEntries
                .AsNoTracking()
                .Where(value => value.CampaignRulesetRevisionId == latestPublished.Id
                    && value.RuleConceptId == ruleConceptId)
                .Select(value => value.CampaignRuleDecisionId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var boundSourceIds = bindings.Select(value => value.SourceEntityId).ToArray();
        var accessibleSources = boundSourceIds.Length == 0
            ? []
            : await dbContext.SourceEntities
                .AsNoTracking()
                .Include(value => value.Revisions)
                .Include(value => value.SourceEdition)
                    .ThenInclude(value => value.SourceWork)
                    .ThenInclude(value => value.SourcePackage)
                .Where(value => boundSourceIds.Contains(value.Id)
                    && (value.SourceEdition.SourceWork.SourcePackage.IsPublic
                        || value.SourceEdition.SourceWork.SourcePackage.UserGrants
                            .Any(grant => grant.UserId == normalizedUserId)))
                .OrderBy(value => value.Name)
                .ThenBy(value => value.SourceCode)
                .ToArrayAsync(cancellationToken);

        var sourceViews = accessibleSources.Select(ToSourceView).ToArray();
        var hasUnpublishedBaseline = latestPublished is null
            || latestPublished.BaselineSelectionId != selectedBaseline.Id;
        var hasUnpublishedOverride = latestCampaignDecision is not null
            && publishedCampaignDecisionId != latestCampaignDecision.Id;

        return new CampaignRuleAuthoringConceptView(
            campaignId,
            ToSummary(selectedBaseline),
            ToView(baselineEntry.RuleConcept),
            ToView(baselineEntry.GlobalRuleDecision),
            bindings.Select(ToView).ToArray(),
            sourceViews,
            bindings.Length - sourceViews.Length,
            latestCampaignDecision is null ? null : ToView(latestCampaignDecision),
            publishedCampaignDecisionId,
            latestPublished?.RevisionNumber,
            hasUnpublishedBaseline,
            hasUnpublishedOverride);
    }

    private static CampaignRulesetSelectionAuthoringSummaryView ToSummary(
        CampaignRulesetSelection selection) =>
        new(
            selection.Id,
            selection.SelectionNumber,
            selection.RulesetRevisionId,
            selection.RulesetRevision.RevisionNumber,
            selection.RulesetRevision.Fingerprint,
            selection.SelectedByUserId,
            selection.SelectedAt);

    private static PublishedCampaignRulesetAuthoringSummaryView ToSummary(
        CampaignRulesetRevision revision,
        int entryCount) =>
        new(
            revision.Id,
            revision.RevisionNumber,
            revision.Fingerprint,
            revision.BaselineSelectionId,
            revision.BaselineSelection.RulesetRevisionId,
            revision.BaselineSelection.RulesetRevision.RevisionNumber,
            revision.BaselineSelection.RulesetRevision.Fingerprint,
            revision.PublishedByUserId,
            revision.PublishedAt,
            entryCount);

    private static RuleConceptView ToView(RuleConcept concept) =>
        new(
            concept.Id,
            concept.Key,
            concept.EntityType,
            concept.DisplayName,
            concept.CreatedByUserId,
            concept.CreatedAt);

    private static RuleConceptSourceBindingView ToView(RuleConceptSourceBinding binding) =>
        new(
            binding.Id,
            binding.RuleConceptId,
            binding.SourceEntityId,
            binding.CreatedByUserId,
            binding.CreatedAt);

    private static GlobalRuleDecisionView ToView(GlobalRuleDecision decision)
    {
        var mergePatch = decision.DecisionKind == RuleDecisionKinds.JsonMergePatch
            ? JsonMergePatch.ParsePatch(decision.PatchJson)
            : (JsonElement?)null;
        var structuredPatch = decision.DecisionKind == RuleDecisionKinds.JsonRulePatch
            ? JsonRulePatch.ParsePatch(decision.PatchJson)
            : (JsonElement?)null;

        return new GlobalRuleDecisionView(
            decision.Id,
            decision.RuleConceptId,
            decision.DecisionNumber,
            decision.DecisionKind,
            decision.SelectedSourceEntityRevisionId,
            decision.SelectedSourceEntityRevision.SourceEntityId,
            decision.SelectedSourceEntityRevision.RevisionNumber,
            decision.SelectedSourceEntityRevision.Fingerprint,
            decision.PatchFingerprint,
            mergePatch,
            structuredPatch,
            decision.Note,
            decision.CreatedByUserId,
            decision.CreatedAt);
    }

    private static CampaignRuleDecisionView ToView(CampaignRuleDecision decision)
    {
        var mergePatch = decision.DecisionKind == CampaignRuleDecisionKinds.JsonMergePatch
            ? JsonMergePatch.ParsePatch(decision.PatchJson)
            : (JsonElement?)null;
        var structuredPatch = decision.DecisionKind == CampaignRuleDecisionKinds.JsonRulePatch
            ? JsonRulePatch.ParsePatch(decision.PatchJson)
            : (JsonElement?)null;

        return new CampaignRuleDecisionView(
            decision.Id,
            decision.CampaignId,
            decision.RuleConceptId,
            decision.DecisionNumber,
            decision.DecisionKind,
            decision.SelectedSourceEntityRevisionId,
            decision.PatchFingerprint,
            mergePatch,
            structuredPatch,
            decision.Note,
            decision.CreatedByUserId,
            decision.CreatedAt,
            Created: false);
    }

    private static GlobalRuleAuthoringSourceView ToSourceView(
        Domain.Sources.SourceEntity source)
    {
        var edition = source.SourceEdition;
        var work = edition.SourceWork;
        var package = work.SourcePackage;
        return new GlobalRuleAuthoringSourceView(
            source.Id,
            source.EntityType,
            source.Name,
            source.SourceCode,
            package.Key,
            package.DisplayName,
            work.Key,
            work.DisplayName,
            edition.Key,
            edition.DisplayName,
            source.Revisions
                .OrderByDescending(value => value.RevisionNumber)
                .Select(value => new GlobalRuleAuthoringSourceRevisionView(
                    value.Id,
                    value.RevisionNumber,
                    value.Fingerprint,
                    value.ImportedAt))
                .ToArray());
    }

    private static void RequireGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Value can not be an empty GUID.", parameterName);
        }
    }

    private static string RequireUserId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value can not be blank.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > 200)
        {
            throw new ArgumentException("Value can not exceed 200 characters.", nameof(value));
        }
        return normalized;
    }
}
