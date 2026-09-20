using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Rules;

public sealed class RuleAdjudicationConcurrencyException(string message) : InvalidOperationException(message);

public sealed class RuleAdjudicationWorkService(RulesCoreDbContext dbContext)
{
    private const int DiscoveryPageSize = 200;
    private readonly RuleAdjudicationWorkStore store = new(dbContext);

    public async Task<RuleAdjudicationDiscoveryView> DiscoverAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = RequireActor(actorUserId);
        await store.EnsureSchemaAsync(cancellationToken);

        var normalizationCount = 0;
        var normalization = new SourceNormalizationService(dbContext);
        for (var offset = 0; ; offset += DiscoveryPageSize)
        {
            var candidates = await normalization.GetCandidatesPageAsync(
                actor,
                limit: DiscoveryPageSize,
                offset: offset,
                cancellationToken: cancellationToken);
            foreach (var candidate in candidates)
            {
                var initialState = string.Equals(
                    candidate.SuggestionKind,
                    SourceNormalizationSuggestionKinds.Conflict,
                    StringComparison.Ordinal)
                    ? RuleAdjudicationWorkStates.ManualResolutionRequired
                    : RuleAdjudicationWorkStates.Pending;
                var manualReason = initialState == RuleAdjudicationWorkStates.ManualResolutionRequired
                    ? "The deterministic normalization suggestion conflicts with an existing concept and requires manual review."
                    : null;
                var item = await store.UpsertAsync(
                    $"normalization:{candidate.SourceEntityId:D}",
                    RuleAdjudicationWorkKinds.NormalizationReview,
                    initialState,
                    ruleConceptId: candidate.SuggestedConceptId,
                    sourceEntityId: candidate.SourceEntityId,
                    sourceRevisionId: null,
                    expectedGlobalRuleDecisionId: null,
                    manualReason,
                    cancellationToken);
                await store.AppendEventAsync(
                    item.Id,
                    RuleAdjudicationWorkEventKinds.Discovered,
                    actorUserId: null,
                    item.Version,
                    "An accessible unbound source entity produced a deterministic normalization suggestion.",
                    item.RuleConceptId,
                    item.SourceEntityId,
                    globalRuleDecisionId: null,
                    rulesetRevisionId: null,
                    dedupeKey: "discovered",
                    cancellationToken);
                normalizationCount++;
            }
            if (candidates.Count < DiscoveryPageSize) break;
        }

        var adjudicationCount = 0;
        var deterministicApplied = 0;
        var conceptIds = await dbContext.RuleConcepts
            .AsNoTracking()
            .Where(value => value.SourceBindings.Any())
            .OrderBy(value => value.Id)
            .Select(value => value.Id)
            .ToArrayAsync(cancellationToken);

        foreach (var conceptId in conceptIds)
        {
            var latestDecision = await GetLatestDecisionAsync(conceptId, cancellationToken);
            if (latestDecision is not null && await IsUsableDecisionAsync(latestDecision, actor, cancellationToken))
            {
                continue;
            }

            var item = await store.UpsertAsync(
                $"global-rule:{conceptId:D}",
                RuleAdjudicationWorkKinds.GlobalRuleAdjudication,
                RuleAdjudicationWorkStates.Pending,
                conceptId,
                sourceEntityId: null,
                sourceRevisionId: null,
                expectedGlobalRuleDecisionId: latestDecision?.Id,
                manualReason: null,
                cancellationToken);
            await store.AppendEventAsync(
                item.Id,
                RuleAdjudicationWorkEventKinds.Discovered,
                actorUserId: null,
                item.Version,
                "A bound rule concept does not have a usable current global decision.",
                conceptId,
                sourceEntityId: null,
                globalRuleDecisionId: latestDecision?.Id,
                rulesetRevisionId: null,
                dedupeKey: "discovered",
                cancellationToken);
            adjudicationCount++;

            var result = await RuleAutoResolutionService.TryResolveAsync(
                dbContext,
                conceptId,
                actor,
                cancellationToken);
            var eventKind = result.Eligible && (result.Applied || result.DecisionId is not null)
                ? RuleAdjudicationWorkEventKinds.DeterministicResolutionApplied
                : RuleAdjudicationWorkEventKinds.DeterministicResolutionRequiresReview;
            var resultKey = $"deterministic:{Hash(result.Reason)}:{result.DecisionId?.ToString("D") ?? "none"}";
            await store.AppendEventAsync(
                item.Id,
                eventKind,
                actorUserId: result.Applied ? actor : null,
                item.Version,
                result.Reason,
                conceptId,
                sourceEntityId: null,
                globalRuleDecisionId: result.DecisionId,
                rulesetRevisionId: null,
                dedupeKey: resultKey,
                cancellationToken);
            if (result.Applied) deterministicApplied++;
        }

        var revisionReview = new SourceRevisionReviewService(dbContext);
        var rejection = new SourceRevisionRejectionService(dbContext);
        var pendingUpdates = await rejection.FilterRejectedAsync(
            await revisionReview.GetPendingAsync(actor, cancellationToken),
            cancellationToken);
        foreach (var update in pendingUpdates)
        {
            var item = await store.UpsertAsync(
                $"source-update:{update.RuleConceptId:D}:{update.GlobalRuleDecisionId:D}:{update.LatestSourceEntityRevisionId:D}",
                RuleAdjudicationWorkKinds.SourceUpdateReview,
                RuleAdjudicationWorkStates.Pending,
                update.RuleConceptId,
                update.SourceEntityId,
                update.LatestSourceEntityRevisionId,
                update.GlobalRuleDecisionId,
                manualReason: null,
                cancellationToken);
            await store.AppendEventAsync(
                item.Id,
                RuleAdjudicationWorkEventKinds.Discovered,
                actorUserId: null,
                item.Version,
                $"The current decision pins source revision #{update.SelectedRevisionNumber}, while revision #{update.LatestRevisionNumber} is available for review.",
                update.RuleConceptId,
                update.SourceEntityId,
                update.GlobalRuleDecisionId,
                rulesetRevisionId: null,
                dedupeKey: "discovered",
                cancellationToken);
        }

        var all = await store.GetAllAsync(cancellationToken);
        foreach (var item in all)
        {
            if (await CanViewAsync(item, actor, cancellationToken))
            {
                await ReconcileAsync(item, actor, cancellationToken);
            }
        }

        var summaries = await ListAsync(actor, kind: null, state: null, includePublishedCompleted: false, cancellationToken);
        return new RuleAdjudicationDiscoveryView(
            normalizationCount,
            adjudicationCount,
            pendingUpdates.Count,
            deterministicApplied,
            summaries.Count(value => value.State != RuleAdjudicationWorkStates.Completed || value.CompletedButUnpublished));
    }

    public async Task<IReadOnlyList<RuleAdjudicationWorkSummaryView>> ListAsync(
        string userId,
        string? kind = null,
        string? state = null,
        bool includePublishedCompleted = false,
        CancellationToken cancellationToken = default)
    {
        var actor = RequireActor(userId);
        ValidateKind(kind);
        ValidateState(state);
        await store.EnsureSchemaAsync(cancellationToken);

        var rows = await store.GetAllAsync(cancellationToken);
        var results = new List<RuleAdjudicationWorkSummaryView>();
        foreach (var row in rows)
        {
            if (!await CanViewAsync(row, actor, cancellationToken)) continue;
            var current = await ReconcileAsync(row, actor, cancellationToken);
            if (kind is not null && !string.Equals(current.WorkKind, kind, StringComparison.Ordinal)) continue;
            if (state is not null && !string.Equals(current.State, state, StringComparison.Ordinal)) continue;
            var summary = await BuildSummaryAsync(current, actor, cancellationToken);
            if (!includePublishedCompleted
                && summary.State == RuleAdjudicationWorkStates.Completed
                && !summary.CompletedButUnpublished)
            {
                continue;
            }
            results.Add(summary);
        }

        return results
            .OrderBy(value => StateOrder(value.State))
            .ThenBy(value => value.WorkKind, StringComparer.Ordinal)
            .ThenBy(value => value.DisplayName ?? value.ConceptKey ?? value.Id.ToString("D"), StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Id)
            .ToArray();
    }

    public async Task<RuleAdjudicationWorkDetailView?> GetAsync(
        Guid workItemId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        RequireGuid(workItemId, nameof(workItemId));
        var actor = RequireActor(userId);
        await store.EnsureSchemaAsync(cancellationToken);
        var row = await store.GetAsync(workItemId, cancellationToken);
        if (row is null || !await CanViewAsync(row, actor, cancellationToken)) return null;
        row = await ReconcileAsync(row, actor, cancellationToken);

        var summary = await BuildSummaryAsync(row, actor, cancellationToken);
        SourceNormalizationCandidateView? normalizationCandidate = null;
        GlobalRuleAuthoringConceptView? rule = null;
        IReadOnlyList<RuleAdjudicationSourceRevisionEvidenceView> sourceEvidence = [];
        IReadOnlyList<RuleSemanticComparisonView> semanticComparisons = [];
        RuleDeterministicResolutionEvidenceView? deterministic = null;
        SourceRevisionReviewPreviewView? sourceUpdate = null;
        RuleAdjudicationDecisionGuardView? decisionGuard = null;

        if (row.WorkKind == RuleAdjudicationWorkKinds.NormalizationReview && row.SourceEntityId is Guid sourceEntityId)
        {
            normalizationCandidate = await FindNormalizationCandidateAsync(sourceEntityId, actor, cancellationToken);
        }

        if (row.RuleConceptId is Guid conceptId
            && row.WorkKind is RuleAdjudicationWorkKinds.GlobalRuleAdjudication or RuleAdjudicationWorkKinds.SourceUpdateReview)
        {
            rule = await new GlobalRulesAuthoringService(dbContext).GetConceptAsync(conceptId, actor, cancellationToken);
            sourceEvidence = await BuildSourceEvidenceAsync(conceptId, actor, cancellationToken);
            semanticComparisons = await BuildSemanticComparisonsAsync(conceptId, sourceEvidence, actor, cancellationToken);
            var latest = await GetLatestDecisionAsync(conceptId, cancellationToken);
            decisionGuard = new RuleAdjudicationDecisionGuardView(latest?.Id, latest?.DecisionNumber);
        }

        var history = await store.GetEventsAsync(row.Id, cancellationToken);
        deterministic = BuildDeterministicEvidence(history);

        if (row.WorkKind == RuleAdjudicationWorkKinds.SourceUpdateReview
            && row.RuleConceptId is Guid sourceUpdateConceptId)
        {
            var pending = await GetVisiblePendingSourceUpdatesAsync(actor, cancellationToken);
            if (pending.Any(value => value.RuleConceptId == sourceUpdateConceptId
                && value.GlobalRuleDecisionId == row.ExpectedGlobalRuleDecisionId
                && value.LatestSourceEntityRevisionId == row.SourceRevisionId))
            {
                sourceUpdate = await new SourceRevisionReviewService(dbContext).PreviewAsync(
                    sourceUpdateConceptId,
                    actor,
                    cancellationToken);
                if (sourceUpdate is not null)
                {
                    sourceUpdate = await NormalizeSourceUpdateEvidenceAsync(
                        sourceUpdate,
                        cancellationToken);
                }
            }
        }

        return new RuleAdjudicationWorkDetailView(
            summary,
            normalizationCandidate,
            rule,
            sourceEvidence,
            semanticComparisons,
            deterministic,
            sourceUpdate,
            decisionGuard,
            ToClarification(row),
            history);
    }

    public Task<RuleAdjudicationWorkSummaryView?> BeginReviewAsync(
        Guid workItemId,
        RuleAdjudicationVersionRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            workItemId,
            request.ExpectedVersion,
            actorUserId,
            RuleAdjudicationWorkStates.AgentReview,
            RuleAdjudicationWorkEventKinds.ReviewStarted,
            message: "Adjudication review began.",
            manualReason: null,
            deferredReason: null,
            allowFrom: [RuleAdjudicationWorkStates.Pending, RuleAdjudicationWorkStates.AgentReview],
            cancellationToken);

    public async Task<RuleAdjudicationWorkSummaryView?> RequestClarificationAsync(
        Guid workItemId,
        RequestRuleAdjudicationClarificationRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = RequireActor(actorUserId);
        var question = RequireText(request.Question, nameof(request.Question), 4000);
        await store.EnsureSchemaAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var row = await store.GetForUpdateAsync(workItemId, cancellationToken);
        if (row is null || !await CanViewAsync(row, actor, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
        if (row.State == RuleAdjudicationWorkStates.WaitingForHuman
            && string.Equals(row.Question, question, StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return await BuildSummaryAsync(row, actor, cancellationToken);
        }
        EnsureVersion(row, request.ExpectedVersion);
        if (row.State is RuleAdjudicationWorkStates.Completed or RuleAdjudicationWorkStates.Deferred or RuleAdjudicationWorkStates.ManualResolutionRequired)
        {
            throw new InvalidOperationException("This work item must be reopened before requesting clarification.");
        }

        var now = DateTimeOffset.UtcNow;
        var updated = await store.SetClarificationAsync(row, question, actor, now, cancellationToken);
        await store.AppendEventAsync(
            updated.Id,
            RuleAdjudicationWorkEventKinds.ClarificationRequested,
            actor,
            updated.Version,
            question,
            updated.RuleConceptId,
            updated.SourceEntityId,
            globalRuleDecisionId: null,
            rulesetRevisionId: null,
            dedupeKey: $"clarification-requested:{updated.Version}",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await BuildSummaryAsync(updated, actor, cancellationToken);
    }

    public async Task<RuleAdjudicationWorkSummaryView?> AnswerClarificationAsync(
        Guid workItemId,
        AnswerRuleAdjudicationClarificationRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = RequireActor(actorUserId);
        var answer = RequireText(request.Answer, nameof(request.Answer), 8000);
        await store.EnsureSchemaAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var row = await store.GetForUpdateAsync(workItemId, cancellationToken);
        if (row is null || !await CanViewAsync(row, actor, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
        if (row.Question is not null
            && row.Answer is not null
            && string.Equals(row.Answer, answer, StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return await BuildSummaryAsync(row, actor, cancellationToken);
        }
        EnsureVersion(row, request.ExpectedVersion);
        if (row.State != RuleAdjudicationWorkStates.WaitingForHuman || row.Question is null)
        {
            throw new InvalidOperationException("This work item does not have an unanswered human clarification request.");
        }

        var now = DateTimeOffset.UtcNow;
        var updated = await store.AnswerClarificationAsync(row, answer, actor, now, cancellationToken);
        await store.AppendEventAsync(
            updated.Id,
            RuleAdjudicationWorkEventKinds.ClarificationAnswered,
            actor,
            updated.Version,
            answer,
            updated.RuleConceptId,
            updated.SourceEntityId,
            globalRuleDecisionId: null,
            rulesetRevisionId: null,
            dedupeKey: $"clarification-answered:{updated.Version}",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await BuildSummaryAsync(updated, actor, cancellationToken);
    }

    public Task<RuleAdjudicationWorkSummaryView?> EscalateAsync(
        Guid workItemId,
        EscalateRuleAdjudicationWorkRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TransitionAsync(
            workItemId,
            request.ExpectedVersion,
            actorUserId,
            RuleAdjudicationWorkStates.ManualResolutionRequired,
            RuleAdjudicationWorkEventKinds.ManualEscalation,
            RequireText(request.Reason, nameof(request.Reason), 2000),
            manualReason: RequireText(request.Reason, nameof(request.Reason), 2000),
            deferredReason: null,
            allowFrom: [RuleAdjudicationWorkStates.Pending, RuleAdjudicationWorkStates.AgentReview, RuleAdjudicationWorkStates.WaitingForHuman, RuleAdjudicationWorkStates.ManualResolutionRequired],
            cancellationToken);
    }

    public Task<RuleAdjudicationWorkSummaryView?> DeferAsync(
        Guid workItemId,
        DeferRuleAdjudicationWorkRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TransitionAsync(
            workItemId,
            request.ExpectedVersion,
            actorUserId,
            RuleAdjudicationWorkStates.Deferred,
            RuleAdjudicationWorkEventKinds.Deferred,
            RequireText(request.Reason, nameof(request.Reason), 2000),
            manualReason: null,
            deferredReason: RequireText(request.Reason, nameof(request.Reason), 2000),
            allowFrom: [RuleAdjudicationWorkStates.Pending, RuleAdjudicationWorkStates.AgentReview, RuleAdjudicationWorkStates.WaitingForHuman, RuleAdjudicationWorkStates.ManualResolutionRequired, RuleAdjudicationWorkStates.Deferred],
            cancellationToken);
    }

    public Task<RuleAdjudicationWorkSummaryView?> ReopenAsync(
        Guid workItemId,
        RuleAdjudicationVersionRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            workItemId,
            request.ExpectedVersion,
            actorUserId,
            RuleAdjudicationWorkStates.Pending,
            RuleAdjudicationWorkEventKinds.Reopened,
            "Work item reopened for adjudication.",
            manualReason: null,
            deferredReason: null,
            allowFrom: [RuleAdjudicationWorkStates.Deferred, RuleAdjudicationWorkStates.ManualResolutionRequired, RuleAdjudicationWorkStates.Pending],
            cancellationToken);

    private async Task<RuleAdjudicationWorkSummaryView?> TransitionAsync(
        Guid workItemId,
        int expectedVersion,
        string actorUserId,
        string targetState,
        string eventKind,
        string message,
        string? manualReason,
        string? deferredReason,
        IReadOnlyCollection<string> allowFrom,
        CancellationToken cancellationToken)
    {
        RequireGuid(workItemId, nameof(workItemId));
        var actor = RequireActor(actorUserId);
        await store.EnsureSchemaAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var row = await store.GetForUpdateAsync(workItemId, cancellationToken);
        if (row is null || !await CanViewAsync(row, actor, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
        if (row.State == targetState
            && string.Equals(row.ManualReason, manualReason, StringComparison.Ordinal)
            && string.Equals(row.DeferredReason, deferredReason, StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return await BuildSummaryAsync(row, actor, cancellationToken);
        }
        EnsureVersion(row, expectedVersion);
        if (!allowFrom.Contains(row.State))
        {
            throw new InvalidOperationException($"Work item state '{row.State}' can not transition to '{targetState}' through this action.");
        }
        if (row.State == RuleAdjudicationWorkStates.Completed)
        {
            throw new InvalidOperationException("Completed work is derived from authoritative rule state and can not be reopened while that resolution remains current.");
        }

        var updated = await store.TransitionAsync(row, targetState, manualReason, deferredReason, cancellationToken);
        await store.AppendEventAsync(
            updated.Id,
            eventKind,
            actor,
            updated.Version,
            message,
            updated.RuleConceptId,
            updated.SourceEntityId,
            globalRuleDecisionId: null,
            rulesetRevisionId: null,
            dedupeKey: $"{eventKind}:{updated.Version}",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await BuildSummaryAsync(updated, actor, cancellationToken);
    }

    private async Task<StoredRuleAdjudicationWorkItem> ReconcileAsync(
        StoredRuleAdjudicationWorkItem row,
        string actor,
        CancellationToken cancellationToken)
    {
        if (row.WorkKind == RuleAdjudicationWorkKinds.NormalizationReview)
        {
            if (row.SourceEntityId is not Guid sourceEntityId) return row;
            var mappings = await CanonicalRuleBindingStore.GetConceptIdsBySourceEntityAsync(
                dbContext,
                [sourceEntityId],
                cancellationToken);
            if (!mappings.TryGetValue(sourceEntityId, out var concepts) || concepts.Count == 0)
            {
                return await ReopenDerivedCompletionAsync(
                    row,
                    "The source entity no longer has a Rules Layer binding, so normalization review is outstanding again.",
                    cancellationToken);
            }
            var normalizedConceptId = concepts[0];
            var binding = await dbContext.RuleConceptSourceBindings
                .AsNoTracking()
                .Where(value => value.RuleConceptId == normalizedConceptId
                    && value.SourceEntityId == sourceEntityId)
                .OrderBy(value => value.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            var updated = await store.CompleteAsync(row, normalizedConceptId, cancellationToken);
            await store.AppendEventAsync(
                updated.Id,
                RuleAdjudicationWorkEventKinds.NormalizationAccepted,
                binding?.CreatedByUserId,
                updated.Version,
                "An explicit Rules Lawyer source binding resolved the normalization work item.",
                normalizedConceptId,
                sourceEntityId,
                globalRuleDecisionId: null,
                rulesetRevisionId: null,
                dedupeKey: $"normalization-accepted:{normalizedConceptId:D}",
                cancellationToken);
            return updated;
        }

        if (row.WorkKind == RuleAdjudicationWorkKinds.GlobalRuleAdjudication && row.RuleConceptId is Guid conceptId)
        {
            var latest = await GetLatestDecisionAsync(conceptId, cancellationToken);
            if (latest is null || !await IsUsableDecisionAsync(latest, actor, cancellationToken))
            {
                return await ReopenDerivedCompletionAsync(
                    row,
                    "The previously completed rule no longer has a usable current global decision, so adjudication is outstanding again.",
                    cancellationToken);
            }
            row = await store.CompleteAsync(row, conceptId, cancellationToken);
            await store.AppendEventAsync(
                row.Id,
                RuleAdjudicationWorkEventKinds.DecisionAssociated,
                latest.CreatedByUserId,
                row.Version,
                $"Global decision #{latest.DecisionNumber} resolved the outstanding adjudication condition.",
                conceptId,
                sourceEntityId: null,
                latest.Id,
                rulesetRevisionId: null,
                dedupeKey: $"decision:{latest.Id:D}",
                cancellationToken);
            await ObservePublicationAsync(row, latest, cancellationToken);
            return row;
        }

        if (row.WorkKind == RuleAdjudicationWorkKinds.SourceUpdateReview && row.RuleConceptId is Guid updateConceptId)
        {
            var pending = await GetVisiblePendingSourceUpdatesAsync(actor, cancellationToken);
            var remainsPending = pending.Any(value => value.RuleConceptId == updateConceptId
                && value.GlobalRuleDecisionId == row.ExpectedGlobalRuleDecisionId
                && value.LatestSourceEntityRevisionId == row.SourceRevisionId);
            if (remainsPending)
            {
                return await ReopenDerivedCompletionAsync(
                    row,
                    "The reviewed source-update condition is outstanding again.",
                    cancellationToken);
            }

            row = await store.CompleteAsync(row, updateConceptId, cancellationToken);
            var latest = await GetLatestDecisionAsync(updateConceptId, cancellationToken);
            await store.AppendEventAsync(
                row.Id,
                RuleAdjudicationWorkEventKinds.SourceUpdateResolved,
                latest?.CreatedByUserId,
                row.Version,
                "The source-update condition is no longer outstanding under the ordinary source revision review workflow.",
                updateConceptId,
                row.SourceEntityId,
                latest?.Id,
                rulesetRevisionId: null,
                dedupeKey: $"source-update-resolved:{latest?.Id.ToString("D") ?? "reviewed"}",
                cancellationToken);
            if (latest is not null)
            {
                await store.AppendEventAsync(
                    row.Id,
                    RuleAdjudicationWorkEventKinds.DecisionAssociated,
                    latest.CreatedByUserId,
                    row.Version,
                    $"Global decision #{latest.DecisionNumber} is current after source-update review.",
                    updateConceptId,
                    row.SourceEntityId,
                    latest.Id,
                    rulesetRevisionId: null,
                    dedupeKey: $"decision:{latest.Id:D}",
                    cancellationToken);
                await ObservePublicationAsync(row, latest, cancellationToken);
            }
            return row;
        }

        return row;
    }

    private async Task<StoredRuleAdjudicationWorkItem> ReopenDerivedCompletionAsync(
        StoredRuleAdjudicationWorkItem row,
        string message,
        CancellationToken cancellationToken)
    {
        if (row.State != RuleAdjudicationWorkStates.Completed) return row;

        var reopened = await store.TransitionAsync(
            row,
            RuleAdjudicationWorkStates.Pending,
            manualReason: null,
            deferredReason: null,
            cancellationToken);
        await store.AppendEventAsync(
            reopened.Id,
            RuleAdjudicationWorkEventKinds.Reopened,
            actorUserId: null,
            reopened.Version,
            message,
            reopened.RuleConceptId,
            reopened.SourceEntityId,
            globalRuleDecisionId: null,
            rulesetRevisionId: null,
            dedupeKey: $"derived-reopened:{reopened.Version}",
            cancellationToken);
        return reopened;
    }

    private async Task ObservePublicationAsync(
        StoredRuleAdjudicationWorkItem row,
        GlobalRuleDecision latest,
        CancellationToken cancellationToken)
    {
        var publication = await GetCurrentPublicationAsync(latest.RuleConceptId, cancellationToken);
        if (publication is null || publication.DecisionId != latest.Id) return;
        await store.AppendEventAsync(
            row.Id,
            RuleAdjudicationWorkEventKinds.PublicationObserved,
            publication.PublishedByUserId,
            row.Version,
            $"The exact decision is present in immutable global ruleset revision #{publication.RevisionNumber}.",
            latest.RuleConceptId,
            row.SourceEntityId,
            latest.Id,
            publication.RulesetRevisionId,
            dedupeKey: $"publication:{publication.RulesetRevisionId:D}:{latest.Id:D}",
            cancellationToken);
    }

    private async Task<RuleAdjudicationWorkSummaryView> BuildSummaryAsync(
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

    private async Task<IReadOnlyList<RuleAdjudicationSourceRevisionEvidenceView>> BuildSourceEvidenceAsync(
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

    private async Task<IReadOnlyList<RuleSemanticComparisonView>> BuildSemanticComparisonsAsync(
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

    private async Task<SourceNormalizationCandidateView?> FindNormalizationCandidateAsync(
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

    private async Task<SourceRevisionReviewPreviewView> NormalizeSourceUpdateEvidenceAsync(
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

    private async Task<string?> ReadCanonicalGameEditionAsync(
        Guid sourceEntityId,
        CancellationToken cancellationToken) =>
        (await CanonicalPublicationMetadataReader.ReadAsync(
            dbContext,
            sourceEntityId,
            cancellationToken))?.GameEdition;

    private async Task<IReadOnlyList<SourceRevisionReviewItemView>> GetVisiblePendingSourceUpdatesAsync(
        string actor,
        CancellationToken cancellationToken)
    {
        var pending = await new SourceRevisionReviewService(dbContext).GetPendingAsync(actor, cancellationToken);
        return await new SourceRevisionRejectionService(dbContext).FilterRejectedAsync(pending, cancellationToken);
    }

    private async Task<bool> CanViewAsync(
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

    private async Task<GlobalRuleDecision?> GetLatestDecisionAsync(Guid conceptId, CancellationToken cancellationToken) =>
        await dbContext.GlobalRuleDecisions
            .AsNoTracking()
            .Where(value => value.RuleConceptId == conceptId)
            .OrderByDescending(value => value.DecisionNumber)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<bool> IsUsableDecisionAsync(
        GlobalRuleDecision decision,
        string actor,
        CancellationToken cancellationToken) =>
        !RuleAutoResolutionService.IsAutomaticDecision(decision)
        || await RuleAutoResolutionService.IsCurrentAutomaticDecisionAsync(dbContext, decision, actor, cancellationToken);

    private async Task<CurrentPublication?> GetCurrentPublicationAsync(Guid conceptId, CancellationToken cancellationToken)
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

    private static RuleDeterministicResolutionEvidenceView? BuildDeterministicEvidence(
        IReadOnlyList<RuleAdjudicationWorkEventView> history)
    {
        var last = history.LastOrDefault(value => value.EventKind is
            RuleAdjudicationWorkEventKinds.DeterministicResolutionApplied or
            RuleAdjudicationWorkEventKinds.DeterministicResolutionRequiresReview);
        if (last is null) return null;
        var applied = last.EventKind == RuleAdjudicationWorkEventKinds.DeterministicResolutionApplied;
        return new RuleDeterministicResolutionEvidenceView(
            applied,
            applied,
            last.GlobalRuleDecisionId,
            DecisionNumber: null,
            last.Message ?? string.Empty);
    }

    private static RuleAdjudicationClarificationView? ToClarification(StoredRuleAdjudicationWorkItem row) =>
        row.Question is null || row.QuestionRequestedBy is null || row.QuestionRequestedAt is null || row.QuestionRequestedVersion is null
            ? null
            : new RuleAdjudicationClarificationView(
                row.Question,
                row.QuestionRequestedBy,
                row.QuestionRequestedAt.Value,
                row.QuestionRequestedVersion.Value,
                row.Answer,
                row.AnsweredBy,
                row.AnsweredAt);

    private static void EnsureVersion(StoredRuleAdjudicationWorkItem row, int expectedVersion)
    {
        if (row.Version != expectedVersion)
        {
            throw new RuleAdjudicationConcurrencyException(
                $"Work item version changed from {expectedVersion} to {row.Version}. Reload the work item before applying this mutation.");
        }
    }

    private static void ValidateKind(string? value)
    {
        if (value is not null && !RuleAdjudicationWorkKinds.All.Contains(value))
        {
            throw new ArgumentException($"Unknown adjudication work kind '{value}'.", nameof(value));
        }
    }

    private static void ValidateState(string? value)
    {
        if (value is not null && !RuleAdjudicationWorkStates.All.Contains(value))
        {
            throw new ArgumentException($"Unknown adjudication work state '{value}'.", nameof(value));
        }
    }

    private static int StateOrder(string state) => state switch
    {
        RuleAdjudicationWorkStates.WaitingForHuman => 0,
        RuleAdjudicationWorkStates.ManualResolutionRequired => 1,
        RuleAdjudicationWorkStates.AgentReview => 2,
        RuleAdjudicationWorkStates.Pending => 3,
        RuleAdjudicationWorkStates.Deferred => 4,
        RuleAdjudicationWorkStates.Completed => 5,
        _ => 9
    };

    private static string RequireActor(string value) => RequireText(value, nameof(value), 200);

    private static string RequireText(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value can not be blank.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"Value can not exceed {maxLength} characters.", parameterName);
        return normalized;
    }

    private static void RequireGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty) throw new ArgumentException("Value can not be an empty GUID.", parameterName);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..16];

    private sealed record CurrentPublication(
        Guid RulesetRevisionId,
        int RevisionNumber,
        string PublishedByUserId,
        Guid DecisionId);
}

internal sealed record StoredRuleAdjudicationWorkItem(
    Guid Id,
    string WorkKey,
    string WorkKind,
    string State,
    int Version,
    Guid? RuleConceptId,
    Guid? SourceEntityId,
    Guid? SourceRevisionId,
    Guid? ExpectedGlobalRuleDecisionId,
    string? Question,
    string? QuestionRequestedBy,
    DateTimeOffset? QuestionRequestedAt,
    int? QuestionRequestedVersion,
    string? Answer,
    string? AnsweredBy,
    DateTimeOffset? AnsweredAt,
    string? ManualReason,
    string? DeferredReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed class RuleAdjudicationWorkStore(RulesCoreDbContext dbContext)
{
    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS rule_adjudication_work_item (
            rule_adjudication_work_item_id uuid NOT NULL,
            work_key varchar(500) NOT NULL,
            work_kind varchar(80) NOT NULL,
            state varchar(80) NOT NULL,
            version integer NOT NULL,
            rule_concept_id uuid NULL,
            source_entity_id uuid NULL,
            source_entity_revision_id uuid NULL,
            expected_global_rule_decision_id uuid NULL,
            clarification_question varchar(4000) NULL,
            clarification_requested_by_user_id varchar(200) NULL,
            clarification_requested_at timestamp with time zone NULL,
            clarification_requested_version integer NULL,
            clarification_answer varchar(8000) NULL,
            clarification_answered_by_user_id varchar(200) NULL,
            clarification_answered_at timestamp with time zone NULL,
            manual_reason varchar(2000) NULL,
            deferred_reason varchar(2000) NULL,
            created_at timestamp with time zone NOT NULL,
            updated_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_rule_adjudication_work_item PRIMARY KEY (rule_adjudication_work_item_id));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_rule_adjudication_work_item_key
            ON rule_adjudication_work_item(work_key);
        CREATE INDEX IF NOT EXISTS ix_rule_adjudication_work_item_state
            ON rule_adjudication_work_item(state, work_kind);
        CREATE INDEX IF NOT EXISTS ix_rule_adjudication_work_item_concept
            ON rule_adjudication_work_item(rule_concept_id);
        CREATE INDEX IF NOT EXISTS ix_rule_adjudication_work_item_source
            ON rule_adjudication_work_item(source_entity_id);

        CREATE TABLE IF NOT EXISTS rule_adjudication_work_event (
            rule_adjudication_work_event_id uuid NOT NULL,
            rule_adjudication_work_item_id uuid NOT NULL,
            event_kind varchar(100) NOT NULL,
            actor_user_id varchar(200) NULL,
            work_version integer NOT NULL,
            message text NULL,
            rule_concept_id uuid NULL,
            source_entity_id uuid NULL,
            global_rule_decision_id uuid NULL,
            ruleset_revision_id uuid NULL,
            dedupe_key varchar(700) NULL,
            occurred_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_rule_adjudication_work_event PRIMARY KEY (rule_adjudication_work_event_id),
            CONSTRAINT fk_rule_adjudication_work_event_item FOREIGN KEY (rule_adjudication_work_item_id)
                REFERENCES rule_adjudication_work_item(rule_adjudication_work_item_id) ON DELETE CASCADE);
        CREATE INDEX IF NOT EXISTS ix_rule_adjudication_work_event_item
            ON rule_adjudication_work_event(rule_adjudication_work_item_id, occurred_at, rule_adjudication_work_event_id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_rule_adjudication_work_event_dedupe
            ON rule_adjudication_work_event(rule_adjudication_work_item_id, dedupe_key)
            WHERE dedupe_key IS NOT NULL;
        ALTER TABLE rule_adjudication_work_event
            ALTER COLUMN message TYPE text;
        """;

    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.ExecuteSqlRawAsync(SchemaSql, cancellationToken);

    public async Task<StoredRuleAdjudicationWorkItem> UpsertAsync(
        string workKey,
        string workKind,
        string initialState,
        Guid? ruleConceptId,
        Guid? sourceEntityId,
        Guid? sourceRevisionId,
        Guid? expectedGlobalRuleDecisionId,
        string? manualReason,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(connection);
            command.CommandText = """
                INSERT INTO rule_adjudication_work_item (
                    rule_adjudication_work_item_id, work_key, work_kind, state, version,
                    rule_concept_id, source_entity_id, source_entity_revision_id,
                    expected_global_rule_decision_id, manual_reason,
                    created_at, updated_at)
                VALUES (@id, @work_key, @work_kind, @state, 1,
                    @rule_concept_id, @source_entity_id, @source_revision_id,
                    @expected_decision_id, @manual_reason, @now, @now)
                ON CONFLICT (work_key) DO UPDATE SET
                    rule_concept_id = COALESCE(rule_adjudication_work_item.rule_concept_id, EXCLUDED.rule_concept_id),
                    source_entity_id = COALESCE(rule_adjudication_work_item.source_entity_id, EXCLUDED.source_entity_id),
                    source_entity_revision_id = COALESCE(rule_adjudication_work_item.source_entity_revision_id, EXCLUDED.source_entity_revision_id),
                    expected_global_rule_decision_id = COALESCE(rule_adjudication_work_item.expected_global_rule_decision_id, EXCLUDED.expected_global_rule_decision_id)
                RETURNING rule_adjudication_work_item_id, work_key, work_kind, state, version,
                    rule_concept_id, source_entity_id, source_entity_revision_id, expected_global_rule_decision_id,
                    clarification_question, clarification_requested_by_user_id, clarification_requested_at,
                    clarification_requested_version, clarification_answer, clarification_answered_by_user_id,
                    clarification_answered_at, manual_reason, deferred_reason, created_at, updated_at;
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@work_key", workKey);
            AddParameter(command, "@work_kind", workKind);
            AddParameter(command, "@state", initialState);
            AddNullableParameter(command, "@rule_concept_id", ruleConceptId, DbType.Guid);
            AddNullableParameter(command, "@source_entity_id", sourceEntityId, DbType.Guid);
            AddNullableParameter(command, "@source_revision_id", sourceRevisionId, DbType.Guid);
            AddNullableParameter(command, "@expected_decision_id", expectedGlobalRuleDecisionId, DbType.Guid);
            AddNullableParameter(command, "@manual_reason", manualReason, DbType.String);
            AddParameter(command, "@now", DateTimeOffset.UtcNow);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            return ReadWorkItem(reader);
        }
        finally
        {
            await CloseIfNeededAsync(connection, openedHere);
        }
    }

    public async Task<IReadOnlyList<StoredRuleAdjudicationWorkItem>> GetAllAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(connection);
            command.CommandText = SelectColumns + " ORDER BY created_at, rule_adjudication_work_item_id;";
            var results = new List<StoredRuleAdjudicationWorkItem>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) results.Add(ReadWorkItem(reader));
            return results;
        }
        finally
        {
            await CloseIfNeededAsync(connection, openedHere);
        }
    }

    public Task<StoredRuleAdjudicationWorkItem?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        GetOneAsync(id, forUpdate: false, cancellationToken);

    public Task<StoredRuleAdjudicationWorkItem?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        GetOneAsync(id, forUpdate: true, cancellationToken);

    private async Task<StoredRuleAdjudicationWorkItem?> GetOneAsync(Guid id, bool forUpdate, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(connection);
            command.CommandText = SelectColumns + " WHERE rule_adjudication_work_item_id = @id" + (forUpdate ? " FOR UPDATE;" : ";");
            AddParameter(command, "@id", id);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? ReadWorkItem(reader) : null;
        }
        finally
        {
            await CloseIfNeededAsync(connection, openedHere);
        }
    }

    public async Task<StoredRuleAdjudicationWorkItem> CompleteAsync(
        StoredRuleAdjudicationWorkItem row,
        Guid? ruleConceptId,
        CancellationToken cancellationToken)
    {
        if (row.State == RuleAdjudicationWorkStates.Completed
            && (ruleConceptId is null || row.RuleConceptId == ruleConceptId)) return row;
        return await UpdateAsync(
            row.Id,
            """
            state = @state,
            version = version + 1,
            rule_concept_id = COALESCE(@rule_concept_id, rule_concept_id),
            updated_at = @now
            """,
            command =>
            {
                AddParameter(command, "@state", RuleAdjudicationWorkStates.Completed);
                AddNullableParameter(command, "@rule_concept_id", ruleConceptId, DbType.Guid);
                AddParameter(command, "@now", DateTimeOffset.UtcNow);
            },
            cancellationToken);
    }

    public Task<StoredRuleAdjudicationWorkItem> TransitionAsync(
        StoredRuleAdjudicationWorkItem row,
        string state,
        string? manualReason,
        string? deferredReason,
        CancellationToken cancellationToken) =>
        UpdateAsync(
            row.Id,
            """
            state = @state,
            version = version + 1,
            manual_reason = @manual_reason,
            deferred_reason = @deferred_reason,
            updated_at = @now
            """,
            command =>
            {
                AddParameter(command, "@state", state);
                AddNullableParameter(command, "@manual_reason", manualReason, DbType.String);
                AddNullableParameter(command, "@deferred_reason", deferredReason, DbType.String);
                AddParameter(command, "@now", DateTimeOffset.UtcNow);
            },
            cancellationToken);

    public Task<StoredRuleAdjudicationWorkItem> SetClarificationAsync(
        StoredRuleAdjudicationWorkItem row,
        string question,
        string actor,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        UpdateAsync(
            row.Id,
            """
            state = @state,
            version = version + 1,
            clarification_question = @question,
            clarification_requested_by_user_id = @actor,
            clarification_requested_at = @now,
            clarification_requested_version = version + 1,
            clarification_answer = NULL,
            clarification_answered_by_user_id = NULL,
            clarification_answered_at = NULL,
            updated_at = @now
            """,
            command =>
            {
                AddParameter(command, "@state", RuleAdjudicationWorkStates.WaitingForHuman);
                AddParameter(command, "@question", question);
                AddParameter(command, "@actor", actor);
                AddParameter(command, "@now", now);
            },
            cancellationToken);

    public Task<StoredRuleAdjudicationWorkItem> AnswerClarificationAsync(
        StoredRuleAdjudicationWorkItem row,
        string answer,
        string actor,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        UpdateAsync(
            row.Id,
            """
            state = @state,
            version = version + 1,
            clarification_answer = @answer,
            clarification_answered_by_user_id = @actor,
            clarification_answered_at = @now,
            updated_at = @now
            """,
            command =>
            {
                AddParameter(command, "@state", RuleAdjudicationWorkStates.AgentReview);
                AddParameter(command, "@answer", answer);
                AddParameter(command, "@actor", actor);
                AddParameter(command, "@now", now);
            },
            cancellationToken);

    public async Task AppendEventAsync(
        Guid workItemId,
        string eventKind,
        string? actorUserId,
        int workVersion,
        string? message,
        Guid? ruleConceptId,
        Guid? sourceEntityId,
        Guid? globalRuleDecisionId,
        Guid? rulesetRevisionId,
        string? dedupeKey,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(connection);
            command.CommandText = """
                INSERT INTO rule_adjudication_work_event (
                    rule_adjudication_work_event_id, rule_adjudication_work_item_id,
                    event_kind, actor_user_id, work_version, message,
                    rule_concept_id, source_entity_id, global_rule_decision_id,
                    ruleset_revision_id, dedupe_key, occurred_at)
                VALUES (@id, @work_item_id, @event_kind, @actor, @work_version, @message,
                    @rule_concept_id, @source_entity_id, @decision_id,
                    @ruleset_revision_id, @dedupe_key, @occurred_at)
                ON CONFLICT (rule_adjudication_work_item_id, dedupe_key)
                    WHERE dedupe_key IS NOT NULL DO NOTHING;
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@work_item_id", workItemId);
            AddParameter(command, "@event_kind", eventKind);
            AddNullableParameter(command, "@actor", actorUserId, DbType.String);
            AddParameter(command, "@work_version", workVersion);
            AddNullableParameter(command, "@message", message, DbType.String);
            AddNullableParameter(command, "@rule_concept_id", ruleConceptId, DbType.Guid);
            AddNullableParameter(command, "@source_entity_id", sourceEntityId, DbType.Guid);
            AddNullableParameter(command, "@decision_id", globalRuleDecisionId, DbType.Guid);
            AddNullableParameter(command, "@ruleset_revision_id", rulesetRevisionId, DbType.Guid);
            AddNullableParameter(command, "@dedupe_key", dedupeKey, DbType.String);
            AddParameter(command, "@occurred_at", DateTimeOffset.UtcNow);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await CloseIfNeededAsync(connection, openedHere);
        }
    }

    public async Task<IReadOnlyList<RuleAdjudicationWorkEventView>> GetEventsAsync(
        Guid workItemId,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(connection);
            command.CommandText = """
                SELECT rule_adjudication_work_event_id, event_kind, actor_user_id,
                    work_version, message, rule_concept_id, source_entity_id,
                    global_rule_decision_id, ruleset_revision_id, occurred_at
                FROM rule_adjudication_work_event
                WHERE rule_adjudication_work_item_id = @work_item_id
                ORDER BY occurred_at, rule_adjudication_work_event_id;
                """;
            AddParameter(command, "@work_item_id", workItemId);
            var results = new List<RuleAdjudicationWorkEventView>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new RuleAdjudicationWorkEventView(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    GetNullableString(reader, 2),
                    reader.GetInt32(3),
                    GetNullableString(reader, 4),
                    GetNullableGuid(reader, 5),
                    GetNullableGuid(reader, 6),
                    GetNullableGuid(reader, 7),
                    GetNullableGuid(reader, 8),
                    reader.GetFieldValue<DateTimeOffset>(9)));
            }
            return results;
        }
        finally
        {
            await CloseIfNeededAsync(connection, openedHere);
        }
    }

    private async Task<StoredRuleAdjudicationWorkItem> UpdateAsync(
        Guid id,
        string assignments,
        Action<DbCommand> configure,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(connection);
            command.CommandText = $"""
                UPDATE rule_adjudication_work_item
                SET {assignments}
                WHERE rule_adjudication_work_item_id = @id
                RETURNING rule_adjudication_work_item_id, work_key, work_kind, state, version,
                    rule_concept_id, source_entity_id, source_entity_revision_id, expected_global_rule_decision_id,
                    clarification_question, clarification_requested_by_user_id, clarification_requested_at,
                    clarification_requested_version, clarification_answer, clarification_answered_by_user_id,
                    clarification_answered_at, manual_reason, deferred_reason, created_at, updated_at;
                """;
            AddParameter(command, "@id", id);
            configure(command);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("The adjudication work item no longer exists.");
            }
            return ReadWorkItem(reader);
        }
        finally
        {
            await CloseIfNeededAsync(connection, openedHere);
        }
    }

    private const string SelectColumns = """
        SELECT rule_adjudication_work_item_id, work_key, work_kind, state, version,
            rule_concept_id, source_entity_id, source_entity_revision_id, expected_global_rule_decision_id,
            clarification_question, clarification_requested_by_user_id, clarification_requested_at,
            clarification_requested_version, clarification_answer, clarification_answered_by_user_id,
            clarification_answered_at, manual_reason, deferred_reason, created_at, updated_at
        FROM rule_adjudication_work_item
        """;

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        if (dbContext.Database.CurrentTransaction is { } transaction)
        {
            command.Transaction = transaction.GetDbTransaction();
        }
        return command;
    }

    private static StoredRuleAdjudicationWorkItem ReadWorkItem(DbDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetInt32(4),
        GetNullableGuid(reader, 5),
        GetNullableGuid(reader, 6),
        GetNullableGuid(reader, 7),
        GetNullableGuid(reader, 8),
        GetNullableString(reader, 9),
        GetNullableString(reader, 10),
        GetNullableDateTimeOffset(reader, 11),
        GetNullableInt32(reader, 12),
        GetNullableString(reader, 13),
        GetNullableString(reader, 14),
        GetNullableDateTimeOffset(reader, 15),
        GetNullableString(reader, 16),
        GetNullableString(reader, 17),
        reader.GetFieldValue<DateTimeOffset>(18),
        reader.GetFieldValue<DateTimeOffset>(19));

    private static Guid? GetNullableGuid(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    private static int? GetNullableInt32(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    private static string? GetNullableString(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static DateTimeOffset? GetNullableDateTimeOffset(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);

    private static async Task<bool> EnsureOpenAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State == ConnectionState.Open) return false;
        await connection.OpenAsync(cancellationToken);
        return true;
    }

    private async Task CloseIfNeededAsync(DbConnection connection, bool openedHere)
    {
        if (openedHere && dbContext.Database.CurrentTransaction is null)
        {
            await connection.CloseAsync();
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void AddNullableParameter(DbCommand command, string name, object? value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
