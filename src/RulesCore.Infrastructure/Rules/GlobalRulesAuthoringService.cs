using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

public sealed class GlobalRulesAuthoringService(RulesCoreDbContext dbContext)
    : IGlobalRulesAuthoringService
{
    public async Task<GlobalRulesAuthoringOverviewView> GetOverviewAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = RequireUserId(userId);

        var concepts = await dbContext.RuleConcepts
            .AsNoTracking()
            .OrderBy(value => value.Key)
            .ToArrayAsync(cancellationToken);

        var bindingCounts = await dbContext.RuleConceptSourceBindings
            .AsNoTracking()
            .GroupBy(value => value.RuleConceptId)
            .Select(group => new { RuleConceptId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(value => value.RuleConceptId, value => value.Count, cancellationToken);

        var decisions = await dbContext.GlobalRuleDecisions
            .AsNoTracking()
            .OrderBy(value => value.RuleConceptId)
            .ThenByDescending(value => value.DecisionNumber)
            .ToArrayAsync(cancellationToken);
        var latestDecisions = decisions
            .GroupBy(value => value.RuleConceptId)
            .ToDictionary(group => group.Key, group => group.First());
        var staleAutomaticConceptIds = new HashSet<Guid>();
        foreach (var pair in latestDecisions.ToArray())
        {
            if (!RuleAutoResolutionService.IsAutomaticDecision(pair.Value))
            {
                continue;
            }

            if (!await RuleAutoResolutionService.IsCurrentAutomaticDecisionAsync(
                dbContext,
                pair.Value,
                normalizedUserId,
                cancellationToken))
            {
                latestDecisions.Remove(pair.Key);
                staleAutomaticConceptIds.Add(pair.Key);
            }
        }

        var latestPublished = await dbContext.RulesetRevisions
            .AsNoTracking()
            .OrderByDescending(value => value.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        Dictionary<Guid, Guid> publishedDecisionIds = [];
        PublishedRulesetAuthoringSummaryView? publishedSummary = null;
        if (latestPublished is not null)
        {
            var entries = await dbContext.RulesetRevisionEntries
                .AsNoTracking()
                .Where(value => value.RulesetRevisionId == latestPublished.Id)
                .ToArrayAsync(cancellationToken);
            publishedDecisionIds = entries.ToDictionary(
                value => value.RuleConceptId,
                value => value.GlobalRuleDecisionId);
            publishedSummary = new PublishedRulesetAuthoringSummaryView(
                latestPublished.Id,
                latestPublished.RevisionNumber,
                latestPublished.Fingerprint,
                latestPublished.PublishedByUserId,
                latestPublished.PublishedAt,
                entries.Length);
        }

        var conceptViews = concepts
            .Select(concept =>
            {
                latestDecisions.TryGetValue(concept.Id, out var latestDecision);
                publishedDecisionIds.TryGetValue(concept.Id, out var publishedDecisionId);
                Guid? publishedId = publishedDecisionId == Guid.Empty ? null : publishedDecisionId;
                var hasUnpublishedChanges = staleAutomaticConceptIds.Contains(concept.Id)
                    || (latestDecision is not null && publishedId != latestDecision.Id);

                return new GlobalRuleAuthoringConceptSummaryView(
                    concept.Id,
                    concept.Key,
                    concept.EntityType,
                    concept.DisplayName,
                    bindingCounts.GetValueOrDefault(concept.Id),
                    latestDecision?.DecisionNumber,
                    latestDecision?.DecisionKind,
                    latestDecision?.Id,
                    publishedId,
                    hasUnpublishedChanges);
            })
            .ToArray();

        return new GlobalRulesAuthoringOverviewView(
            publishedSummary,
            conceptViews.Length,
            conceptViews.Count(value => value.LatestDecisionId is not null),
            conceptViews.Count(value => value.HasUnpublishedChanges),
            conceptViews);
    }

    public async Task<GlobalRuleAuthoringConceptView?> GetConceptAsync(
        Guid ruleConceptId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (ruleConceptId == Guid.Empty)
        {
            throw new ArgumentException("Value can not be an empty GUID.", nameof(ruleConceptId));
        }
        var normalizedUserId = RequireUserId(userId);

        var concept = await dbContext.RuleConcepts
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == ruleConceptId, cancellationToken);
        if (concept is null)
        {
            return null;
        }

        var bindings = await dbContext.RuleConceptSourceBindings
            .AsNoTracking()
            .Where(value => value.RuleConceptId == ruleConceptId)
            .OrderBy(value => value.CreatedAt)
            .ThenBy(value => value.Id)
            .ToArrayAsync(cancellationToken);

        var latestDecision = await dbContext.GlobalRuleDecisions
            .AsNoTracking()
            .Include(value => value.SelectedSourceEntityRevision)
                .ThenInclude(value => value.SourceEntity)
            .Where(value => value.RuleConceptId == ruleConceptId)
            .OrderByDescending(value => value.DecisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        var staleAutomaticDecision = latestDecision is not null
            && RuleAutoResolutionService.IsAutomaticDecision(latestDecision)
            && !await RuleAutoResolutionService.IsCurrentAutomaticDecisionAsync(
                dbContext,
                latestDecision,
                normalizedUserId,
                cancellationToken);
        if (staleAutomaticDecision)
        {
            latestDecision = null;
        }

        var latestPublished = await dbContext.RulesetRevisions
            .AsNoTracking()
            .OrderByDescending(value => value.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        Guid? publishedDecisionId = null;
        if (latestPublished is not null)
        {
            publishedDecisionId = await dbContext.RulesetRevisionEntries
                .AsNoTracking()
                .Where(value => value.RulesetRevisionId == latestPublished.Id
                    && value.RuleConceptId == ruleConceptId)
                .Select(value => (Guid?)value.GlobalRuleDecisionId)
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

        var sourceViews = accessibleSources
            .Select(source =>
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
            })
            .ToArray();

        return new GlobalRuleAuthoringConceptView(
            ToView(concept),
            bindings.Select(ToView).ToArray(),
            sourceViews,
            bindings.Length - sourceViews.Length,
            latestDecision is null ? null : ToView(latestDecision),
            publishedDecisionId,
            latestPublished?.RevisionNumber,
            staleAutomaticDecision
                || (latestDecision is not null && publishedDecisionId != latestDecision.Id));
    }

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
            : (System.Text.Json.JsonElement?)null;
        var structuredPatch = decision.DecisionKind == RuleDecisionKinds.JsonRulePatch
            ? JsonRulePatch.ParsePatch(decision.PatchJson)
            : (System.Text.Json.JsonElement?)null;

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