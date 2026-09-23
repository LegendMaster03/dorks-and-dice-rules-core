using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Reads and constructs adjudication-facing evidence, visibility, normalization, and
/// publication state without owning workflow transitions.
/// </summary>
internal sealed class RuleAdjudicationEvidenceReader(
    RulesCoreDbContext dbContext,
    RuleAdjudicationWorkStore store)
{
    private const int DiscoveryPageSize = 200;

    internal async Task<RuleAdjudicationWorkSummaryView> BuildSummaryAsync(
        StoredRuleAdjudicationWorkItem row,
        string actor,
        CancellationToken cancellationToken)
    {
        string? conceptKey = null;
        string? displayName = null;
        string? entityType = null;
        if (row.RuleConceptId is Guid conceptId)
        {
            var concept = await dbContext.RuleConcepts.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == conceptId, cancellationToken);
            conceptKey = concept?.Key;
            displayName = concept?.DisplayName;
            entityType = concept?.EntityType;
        }
        if (displayName is null && row.SourceEntityId is Guid sourceEntityId)
        {
            var source = await dbContext.SourceEntities.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == sourceEntityId, cancellationToken);
            displayName = source?.Name;
            entityType ??= source?.EntityType;
        }
    
        var completedButUnpublished = false;
        var published = false;
        if (row.State == RuleAdjudicationWorkStates.Completed && row.RuleConceptId is Guid resolvedConceptId)
        {
            var latest = await GetLatestDecisionAsync(resolvedConceptId, cancellationToken);
            if (latest is not null)
            {
                var publication = await GetCurrentPublicationAsync(resolvedConceptId, cancellationToken);
                published = publication?.DecisionId == latest.Id;
                completedButUnpublished = !published;
            }
        }
    
        var events = await store.GetEventsAsync(row.Id, cancellationToken);
        var deterministicReason = events
            .LastOrDefault(value => value.EventKind is RuleAdjudicationWorkEventKinds.DeterministicResolutionApplied
                or RuleAdjudicationWorkEventKinds.DeterministicResolutionRequiresReview)?.Message;
    
        return new RuleAdjudicationWorkSummaryView(
            row.Id,
            row.WorkKind,
            row.State,
            row.Version,
            row.RuleConceptId,
            row.SourceEntityId,
            conceptKey,
            displayName,
            entityType,
            row.State == RuleAdjudicationWorkStates.WaitingForHuman && row.Question is not null && row.Answer is null,
            completedButUnpublished,
            published,
            deterministicReason,
            row.ManualReason,
            row.DeferredReason,
            row.CreatedAt,
            row.UpdatedAt);
    }
    
    internal async Task<IReadOnlyList<RuleAdjudicationSourceRevisionEvidenceView>> BuildSourceEvidenceAsync(
        Guid conceptId,
        string actor,
        CancellationToken cancellationToken)
    {
        var sourceIds = await CanonicalRuleBindingStore.GetAccessibleSourceEntityIdsForConceptAsync(
            dbContext,
            conceptId,
            actor,
            cancellationToken);
        if (sourceIds.Count == 0) return [];
        var ignored = (await new GlobalSourceDispositionService(dbContext).GetIgnoredPackageIdsAsync(cancellationToken)).ToHashSet();
        var sources = await dbContext.SourceEntities
            .AsNoTracking()
            .Include(value => value.SourcePackage)
            .Include(value => value.Revisions)
            .Where(value => sourceIds.Contains(value.Id) && !ignored.Contains(value.SourcePackageId))
            .ToArrayAsync(cancellationToken);
        var results = new List<RuleAdjudicationSourceRevisionEvidenceView>();
        foreach (var source in sources)
        {
            var revision = source.Revisions.OrderByDescending(value => value.RevisionNumber).FirstOrDefault();
            if (revision is null) continue;
            var publication = await CanonicalPublicationMetadataReader.ReadAsync(
                dbContext,
                source.Id,
                cancellationToken);
            var edition = publication?.GameEdition ?? source.FormatKey;
            using var document = JsonDocument.Parse(revision.GetMechanicalContentJson());
            results.Add(new RuleAdjudicationSourceRevisionEvidenceView(
                source.Id,
                revision.Id,
                revision.RevisionNumber,
                revision.Fingerprint,
                source.Name,
                source.SourceCode ?? string.Empty,
                source.SourcePackage.Key,
                source.SourcePackage.DisplayName,
                edition,
                edition,
                revision.ImportedAt,
                document.RootElement.Clone()));
        }
        return results
            .OrderBy(value => value.EditionDisplayName, StringComparer.Ordinal)
            .ThenBy(value => value.SourceCode, StringComparer.Ordinal)
            .ThenBy(value => value.SourceEntityId)
            .ToArray();
    }
    
    internal async Task<IReadOnlyList<RuleSemanticComparisonView>> BuildSemanticComparisonsAsync(
        Guid conceptId,
        IReadOnlyList<RuleAdjudicationSourceRevisionEvidenceView> sources,
        string actor,
        CancellationToken cancellationToken)
    {
        if (sources.Count < 2) return [];
        var service = new RuleSemanticComparisonService(dbContext);
        var results = new List<RuleSemanticComparisonView>();
        for (var left = 0; left < sources.Count - 1; left++)
        {
            for (var right = left + 1; right < sources.Count; right++)
            {
                var comparison = await service.CompareAsync(
                    new RuleSemanticComparisonRequest(
                        new RuleAdjudicationScopeRequest(RuleAdjudicationScopeKinds.Global),
                        conceptId,
                        sources[left].SourceEntityRevisionId,
                        sources[right].SourceEntityRevisionId),
                    actor,
                    cancellationToken);
                if (comparison is not null) results.Add(comparison);
            }
        }
        return results;
    }
    
    internal async Task<SourceNormalizationCandidateView?> FindNormalizationCandidateAsync(
        Guid sourceEntityId,
        string actor,
        CancellationToken cancellationToken)
    {
        var service = new SourceNormalizationService(dbContext);
        for (var offset = 0; ; offset += DiscoveryPageSize)
        {
            var page = await service.GetCandidatesPageAsync(
                actor,
                limit: DiscoveryPageSize,
                offset: offset,
                cancellationToken: cancellationToken);
            var candidate = page.SingleOrDefault(value => value.SourceEntityId == sourceEntityId);
            if (candidate is not null)
            {
                var gameEdition = await ReadCanonicalGameEditionAsync(sourceEntityId, cancellationToken);
                return string.IsNullOrWhiteSpace(gameEdition)
                    ? candidate
                    : candidate with
                    {
                        EditionKey = gameEdition,
                        EditionDisplayName = gameEdition
                    };
            }
            if (page.Count < DiscoveryPageSize) return null;
        }
    }
    
    internal async Task<SourceRevisionReviewPreviewView> NormalizeSourceUpdateEvidenceAsync(
        SourceRevisionReviewPreviewView preview,
        CancellationToken cancellationToken)
    {
        var gameEdition = await ReadCanonicalGameEditionAsync(
            preview.Update.SourceEntityId,
            cancellationToken);
        return string.IsNullOrWhiteSpace(gameEdition)
            ? preview
            : preview with
            {
                Update = preview.Update with
                {
                    EditionKey = gameEdition,
                    EditionDisplayName = gameEdition
                }
            };
    }
    
    internal async Task<string?> ReadCanonicalGameEditionAsync(
        Guid sourceEntityId,
        CancellationToken cancellationToken) =>
        (await CanonicalPublicationMetadataReader.ReadAsync(
            dbContext,
            sourceEntityId,
            cancellationToken))?.GameEdition;
    
    internal async Task<IReadOnlyList<SourceRevisionReviewItemView>> GetVisiblePendingSourceUpdatesAsync(
        string actor,
        CancellationToken cancellationToken)
    {
        var pending = await new SourceRevisionReviewService(dbContext).GetPendingAsync(actor, cancellationToken);
        return await new SourceRevisionRejectionService(dbContext).FilterRejectedAsync(pending, cancellationToken);
    }
    
    internal async Task<bool> CanViewAsync(
        StoredRuleAdjudicationWorkItem row,
        string actor,
        CancellationToken cancellationToken)
    {
        if (row.WorkKind == RuleAdjudicationWorkKinds.GlobalRuleAdjudication) return true;
        if (row.SourceEntityId is not Guid sourceEntityId) return row.RuleConceptId is not null;
        return await dbContext.SourceEntities.AsNoTracking().AnyAsync(
            value => value.Id == sourceEntityId
                && (value.SourcePackage.IsPublic
                    || value.SourcePackage.UserGrants.Any(grant => grant.UserId == actor)),
            cancellationToken);
    }
    
    internal async Task<GlobalRuleDecision?> GetLatestDecisionAsync(Guid conceptId, CancellationToken cancellationToken) =>
        await dbContext.GlobalRuleDecisions
            .AsNoTracking()
            .Where(value => value.RuleConceptId == conceptId)
            .OrderByDescending(value => value.DecisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    
    internal async Task<bool> IsUsableDecisionAsync(
        GlobalRuleDecision decision,
        string actor,
        CancellationToken cancellationToken) =>
        !RuleAutoResolutionService.IsAutomaticDecision(decision)
        || await RuleAutoResolutionService.IsCurrentAutomaticDecisionAsync(dbContext, decision, actor, cancellationToken);
    
    internal async Task<CurrentPublication?> GetCurrentPublicationAsync(Guid conceptId, CancellationToken cancellationToken)
    {
        var latestRevision = await dbContext.RulesetRevisions.AsNoTracking()
            .OrderByDescending(value => value.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestRevision is null) return null;
        var entry = await dbContext.RulesetRevisionEntries.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.RulesetRevisionId == latestRevision.Id && value.RuleConceptId == conceptId,
                cancellationToken);
        return entry is null
            ? null
            : new CurrentPublication(
                latestRevision.Id,
                latestRevision.RevisionNumber,
                latestRevision.PublishedByUserId,
                entry.GlobalRuleDecisionId);
    }
    

    internal sealed record CurrentPublication(
        Guid RulesetRevisionId,
        int RevisionNumber,
        string PublishedByUserId,
        Guid DecisionId);
}
