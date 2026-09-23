using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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
    private readonly RuleAdjudicationEvidenceReader evidence =
        new(dbContext, new RuleAdjudicationWorkStore(dbContext));

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
            var latestDecision = await evidence.GetLatestDecisionAsync(conceptId, cancellationToken);
            if (latestDecision is not null && await evidence.IsUsableDecisionAsync(latestDecision, actor, cancellationToken))
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
            if (await evidence.CanViewAsync(item, actor, cancellationToken))
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
            if (!await evidence.CanViewAsync(row, actor, cancellationToken)) continue;
            var current = await ReconcileAsync(row, actor, cancellationToken);
            if (kind is not null && !string.Equals(current.WorkKind, kind, StringComparison.Ordinal)) continue;
            if (state is not null && !string.Equals(current.State, state, StringComparison.Ordinal)) continue;
            var summary = await evidence.BuildSummaryAsync(current, actor, cancellationToken);
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
        if (row is null || !await evidence.CanViewAsync(row, actor, cancellationToken)) return null;
        row = await ReconcileAsync(row, actor, cancellationToken);

        var summary = await evidence.BuildSummaryAsync(row, actor, cancellationToken);
        SourceNormalizationCandidateView? normalizationCandidate = null;
        GlobalRuleAuthoringConceptView? rule = null;
        IReadOnlyList<RuleAdjudicationSourceRevisionEvidenceView> sourceEvidence = [];
        IReadOnlyList<RuleSemanticComparisonView> semanticComparisons = [];
        RuleDeterministicResolutionEvidenceView? deterministic = null;
        SourceRevisionReviewPreviewView? sourceUpdate = null;
        RuleAdjudicationDecisionGuardView? decisionGuard = null;

        if (row.WorkKind == RuleAdjudicationWorkKinds.NormalizationReview && row.SourceEntityId is Guid sourceEntityId)
        {
            normalizationCandidate = await evidence.FindNormalizationCandidateAsync(sourceEntityId, actor, cancellationToken);
        }

        if (row.RuleConceptId is Guid conceptId
            && row.WorkKind is RuleAdjudicationWorkKinds.GlobalRuleAdjudication or RuleAdjudicationWorkKinds.SourceUpdateReview)
        {
            rule = await new GlobalRulesAuthoringService(dbContext).GetConceptAsync(conceptId, actor, cancellationToken);
            sourceEvidence = await evidence.BuildSourceEvidenceAsync(conceptId, actor, cancellationToken);
            semanticComparisons = await evidence.BuildSemanticComparisonsAsync(conceptId, sourceEvidence, actor, cancellationToken);
            var latest = await evidence.GetLatestDecisionAsync(conceptId, cancellationToken);
            decisionGuard = new RuleAdjudicationDecisionGuardView(latest?.Id, latest?.DecisionNumber);
        }

        var history = await store.GetEventsAsync(row.Id, cancellationToken);
        deterministic = BuildDeterministicEvidence(history);

        if (row.WorkKind == RuleAdjudicationWorkKinds.SourceUpdateReview
            && row.RuleConceptId is Guid sourceUpdateConceptId)
        {
            var pending = await evidence.GetVisiblePendingSourceUpdatesAsync(actor, cancellationToken);
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
                    sourceUpdate = await evidence.NormalizeSourceUpdateEvidenceAsync(
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
        if (row is null || !await evidence.CanViewAsync(row, actor, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
        if (row.State == RuleAdjudicationWorkStates.WaitingForHuman
            && string.Equals(row.Question, question, StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return await evidence.BuildSummaryAsync(row, actor, cancellationToken);
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
        return await evidence.BuildSummaryAsync(updated, actor, cancellationToken);
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
        if (row is null || !await evidence.CanViewAsync(row, actor, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
        if (row.Question is not null
            && row.Answer is not null
            && string.Equals(row.Answer, answer, StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return await evidence.BuildSummaryAsync(row, actor, cancellationToken);
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
        return await evidence.BuildSummaryAsync(updated, actor, cancellationToken);
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
        if (row is null || !await evidence.CanViewAsync(row, actor, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
        if (row.State == targetState
            && string.Equals(row.ManualReason, manualReason, StringComparison.Ordinal)
            && string.Equals(row.DeferredReason, deferredReason, StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return await evidence.BuildSummaryAsync(row, actor, cancellationToken);
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
        return await evidence.BuildSummaryAsync(updated, actor, cancellationToken);
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
            var latest = await evidence.GetLatestDecisionAsync(conceptId, cancellationToken);
            if (latest is null || !await evidence.IsUsableDecisionAsync(latest, actor, cancellationToken))
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
            var pending = await evidence.GetVisiblePendingSourceUpdatesAsync(actor, cancellationToken);
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
            var latest = await evidence.GetLatestDecisionAsync(updateConceptId, cancellationToken);
            var rejection = row.ExpectedGlobalRuleDecisionId is Guid expectedDecisionId
                && row.SourceRevisionId is Guid reviewedRevisionId
                ? await new SourceRevisionRejectionService(dbContext).GetRecordedAsync(
                    expectedDecisionId,
                    reviewedRevisionId,
                    cancellationToken)
                : null;
            var resolutionActor = rejection?.CreatedByUserId ?? latest?.CreatedByUserId;
            var resolutionMessage = rejection is null
                ? "The source-update condition is no longer outstanding under the ordinary source revision review workflow."
                : $"The reviewed source revision was explicitly rejected: {rejection.Reason}";
            var resolutionDedupeKey = rejection is null
                ? $"source-update-resolved:{latest?.Id.ToString("D") ?? "reviewed"}"
                : $"source-update-rejected:{row.ExpectedGlobalRuleDecisionId:D}:{row.SourceRevisionId:D}";
            await store.AppendEventAsync(
                row.Id,
                RuleAdjudicationWorkEventKinds.SourceUpdateResolved,
                resolutionActor,
                row.Version,
                resolutionMessage,
                updateConceptId,
                row.SourceEntityId,
                latest?.Id,
                rulesetRevisionId: null,
                dedupeKey: resolutionDedupeKey,
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
        var publication = await evidence.GetCurrentPublicationAsync(latest.RuleConceptId, cancellationToken);
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


}
