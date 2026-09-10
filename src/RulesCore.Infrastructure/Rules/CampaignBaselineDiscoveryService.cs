using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

public sealed class CampaignBaselineDiscoveryService(RulesCoreDbContext dbContext)
    : ICampaignBaselineDiscoveryService
{
    public async Task<IReadOnlyList<CampaignBaselineCandidateSummaryView>> GetCandidatesAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        RequireGuid(campaignId, nameof(campaignId));

        var selectedBaselineId = await dbContext.CampaignRulesetSelections
            .AsNoTracking()
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.SelectionNumber)
            .Select(value => (Guid?)value.RulesetRevisionId)
            .FirstOrDefaultAsync(cancellationToken);

        var publishedBaselineId = await dbContext.CampaignRulesetRevisions
            .AsNoTracking()
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => (Guid?)value.BaselineSelection.RulesetRevisionId)
            .FirstOrDefaultAsync(cancellationToken);

        var revisions = await dbContext.RulesetRevisions
            .AsNoTracking()
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => new
            {
                value.Id,
                value.RevisionNumber,
                value.Fingerprint,
                value.PublishedByUserId,
                value.PublishedAt,
                EntryCount = value.Entries.Count
            })
            .ToArrayAsync(cancellationToken);

        return revisions
            .Select(value => new CampaignBaselineCandidateSummaryView(
                value.Id,
                value.RevisionNumber,
                value.Fingerprint,
                value.PublishedByUserId,
                value.PublishedAt,
                value.EntryCount,
                IsSelectedBaseline: selectedBaselineId == value.Id,
                IsPublishedCampaignBaseline: publishedBaselineId == value.Id))
            .ToArray();
    }

    public async Task<CampaignBaselineMigrationPreviewView?> PreviewAsync(
        Guid campaignId,
        Guid rulesetRevisionId,
        CancellationToken cancellationToken = default)
    {
        RequireGuid(campaignId, nameof(campaignId));
        RequireGuid(rulesetRevisionId, nameof(rulesetRevisionId));

        var candidate = await dbContext.RulesetRevisions
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == rulesetRevisionId, cancellationToken);
        if (candidate is null)
        {
            return null;
        }

        var currentSelection = await dbContext.CampaignRulesetSelections
            .AsNoTracking()
            .Include(value => value.RulesetRevision)
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.SelectionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var publishedCampaignRevision = await dbContext.CampaignRulesetRevisions
            .AsNoTracking()
            .Include(value => value.BaselineSelection)
                .ThenInclude(value => value.RulesetRevision)
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var candidateEntries = await LoadEntriesAsync(candidate.Id, cancellationToken);
        var currentEntries = currentSelection is null
            ? []
            : await LoadEntriesAsync(currentSelection.RulesetRevisionId, cancellationToken);

        var conceptIds = candidateEntries
            .Select(value => value.RuleConceptId)
            .Concat(currentEntries.Select(value => value.RuleConceptId))
            .Distinct()
            .ToArray();

        var campaignDecisions = conceptIds.Length == 0
            ? []
            : await dbContext.CampaignRuleDecisions
                .AsNoTracking()
                .Where(value => value.CampaignId == campaignId && conceptIds.Contains(value.RuleConceptId))
                .ToArrayAsync(cancellationToken);
        var latestDecisionByConcept = campaignDecisions
            .GroupBy(value => value.RuleConceptId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(value => value.DecisionNumber).First());

        var currentByConcept = currentEntries.ToDictionary(value => value.RuleConceptId);
        var candidateByConcept = candidateEntries.ToDictionary(value => value.RuleConceptId);
        var concepts = currentEntries
            .Select(value => value.RuleConcept)
            .Concat(candidateEntries.Select(value => value.RuleConcept))
            .GroupBy(value => value.Id)
            .Select(group => group.First())
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToArray();

        var changes = new List<CampaignBaselineConceptChangeView>(concepts.Length);
        foreach (var concept in concepts)
        {
            currentByConcept.TryGetValue(concept.Id, out var currentEntry);
            candidateByConcept.TryGetValue(concept.Id, out var candidateEntry);
            latestDecisionByConcept.TryGetValue(concept.Id, out var campaignDecision);

            var changeKind = GetChangeKind(currentEntry, candidateEntry);
            var overrideImpact = GetOverrideImpact(
                campaignDecision,
                currentEntry is not null,
                candidateEntry is not null);

            changes.Add(new CampaignBaselineConceptChangeView(
                concept.Id,
                concept.Key,
                concept.EntityType,
                concept.DisplayName,
                changeKind,
                currentEntry?.GlobalRuleDecision.DecisionNumber,
                currentEntry?.GlobalRuleDecision.DecisionKind,
                candidateEntry?.GlobalRuleDecision.DecisionNumber,
                candidateEntry?.GlobalRuleDecision.DecisionKind,
                campaignDecision?.DecisionNumber,
                campaignDecision?.DecisionKind,
                overrideImpact));
        }

        return new CampaignBaselineMigrationPreviewView(
            campaignId,
            currentSelection is null
                ? null
                : ToReference(currentSelection.RulesetRevision),
            publishedCampaignRevision is null
                ? null
                : ToReference(publishedCampaignRevision.BaselineSelection.RulesetRevision),
            ToReference(candidate),
            AddedConceptCount: changes.Count(value => value.ChangeKind == "added"),
            RemovedConceptCount: changes.Count(value => value.ChangeKind == "removed"),
            ChangedConceptCount: changes.Count(value => value.ChangeKind == "changed"),
            UnchangedConceptCount: changes.Count(value => value.ChangeKind == "unchanged"),
            OverridesRemainingActiveCount: changes.Count(value => value.OverrideImpact == "remains-active"),
            OverridesBecomingInactiveCount: changes.Count(value => value.OverrideImpact == "becomes-inactive"),
            HistoricalOverridesBecomingActiveCount: changes.Count(value => value.OverrideImpact == "becomes-active"),
            changes);
    }

    private async Task<BaselineEntry[]> LoadEntriesAsync(
        Guid rulesetRevisionId,
        CancellationToken cancellationToken)
    {
        return await dbContext.RulesetRevisionEntries
            .AsNoTracking()
            .Where(value => value.RulesetRevisionId == rulesetRevisionId)
            .Select(value => new BaselineEntry(
                value.RuleConceptId,
                value.GlobalRuleDecisionId,
                value.RuleConcept,
                value.GlobalRuleDecision))
            .ToArrayAsync(cancellationToken);
    }

    private static string GetChangeKind(BaselineEntry? current, BaselineEntry? candidate)
    {
        if (current is null)
        {
            return "added";
        }
        if (candidate is null)
        {
            return "removed";
        }
        return current.GlobalRuleDecisionId == candidate.GlobalRuleDecisionId
            ? "unchanged"
            : "changed";
    }

    private static string? GetOverrideImpact(
        CampaignRuleDecision? decision,
        bool existsInCurrent,
        bool existsInCandidate)
    {
        if (decision is null)
        {
            return null;
        }
        if (existsInCurrent && existsInCandidate)
        {
            return "remains-active";
        }
        if (existsInCurrent && !existsInCandidate)
        {
            return "becomes-inactive";
        }
        if (!existsInCurrent && existsInCandidate)
        {
            return "becomes-active";
        }
        return null;
    }

    private static CampaignBaselineReferenceView ToReference(RulesetRevision revision) =>
        new(
            revision.Id,
            revision.RevisionNumber,
            revision.Fingerprint,
            revision.PublishedAt);

    private static void RequireGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Value can not be empty.", parameterName);
        }
    }

    private sealed record BaselineEntry(
        Guid RuleConceptId,
        Guid GlobalRuleDecisionId,
        RuleConcept RuleConcept,
        GlobalRuleDecision GlobalRuleDecision);
}
