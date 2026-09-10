using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

public sealed class SourceRevisionReviewService(RulesCoreDbContext dbContext)
    : ISourceRevisionReviewService
{
    public async Task<IReadOnlyList<SourceRevisionReviewItemView>> GetPendingAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = RequireUserId(userId);

        var decisions = await dbContext.GlobalRuleDecisions
            .AsNoTracking()
            .Include(value => value.RuleConcept)
            .Include(value => value.SelectedSourceEntityRevision)
                .ThenInclude(value => value.SourceEntity)
                .ThenInclude(value => value.SourceEdition)
                .ThenInclude(value => value.SourceWork)
                .ThenInclude(value => value.SourcePackage)
            .Where(value =>
                value.SelectedSourceEntityRevision.SourceEntity.SourceEdition.SourceWork.SourcePackage.IsPublic
                || value.SelectedSourceEntityRevision.SourceEntity.SourceEdition.SourceWork.SourcePackage.UserGrants
                    .Any(grant => grant.UserId == normalizedUserId))
            .OrderBy(value => value.RuleConceptId)
            .ThenByDescending(value => value.DecisionNumber)
            .ToArrayAsync(cancellationToken);

        var latestDecisions = decisions
            .GroupBy(value => value.RuleConceptId)
            .Select(group => group.First())
            .ToArray();
        if (latestDecisions.Length == 0)
        {
            return [];
        }

        var sourceEntityIds = latestDecisions
            .Select(value => value.SelectedSourceEntityRevision.SourceEntityId)
            .Distinct()
            .ToArray();
        var revisions = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => sourceEntityIds.Contains(value.SourceEntityId))
            .OrderBy(value => value.SourceEntityId)
            .ThenByDescending(value => value.RevisionNumber)
            .ToArrayAsync(cancellationToken);
        var revisionsByEntity = revisions
            .GroupBy(value => value.SourceEntityId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        return latestDecisions
            .Select(decision =>
            {
                var selected = decision.SelectedSourceEntityRevision;
                var source = selected.SourceEntity;
                var sourceRevisions = revisionsByEntity[source.Id];
                var latest = sourceRevisions[0];
                if (latest.RevisionNumber <= selected.RevisionNumber)
                {
                    return null;
                }

                var edition = source.SourceEdition;
                var work = edition.SourceWork;
                var package = work.SourcePackage;
                return new SourceRevisionReviewItemView(
                    decision.RuleConceptId,
                    decision.RuleConcept.Key,
                    decision.RuleConcept.EntityType,
                    decision.RuleConcept.DisplayName,
                    decision.Id,
                    decision.DecisionNumber,
                    decision.DecisionKind,
                    source.Id,
                    source.Name,
                    source.SourceCode,
                    package.Key,
                    package.DisplayName,
                    edition.Key,
                    edition.DisplayName,
                    selected.Id,
                    selected.RevisionNumber,
                    selected.Fingerprint,
                    selected.ImportedAt,
                    latest.Id,
                    latest.RevisionNumber,
                    latest.Fingerprint,
                    latest.ImportedAt,
                    sourceRevisions.Count(value => value.RevisionNumber > selected.RevisionNumber));
            })
            .Where(value => value is not null)
            .Select(value => value!)
            .OrderBy(value => value.EntityType, StringComparer.Ordinal)
            .ThenBy(value => value.DisplayName, StringComparer.Ordinal)
            .ThenBy(value => value.ConceptKey, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<SourceRevisionReviewPreviewView?> PreviewAsync(
        Guid ruleConceptId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (ruleConceptId == Guid.Empty)
        {
            throw new ArgumentException("Value can not be an empty GUID.", nameof(ruleConceptId));
        }
        var normalizedUserId = RequireUserId(userId);
        var pending = await GetPendingAsync(normalizedUserId, cancellationToken);
        var update = pending.SingleOrDefault(value => value.RuleConceptId == ruleConceptId);
        if (update is null)
        {
            return null;
        }

        var decision = await dbContext.GlobalRuleDecisions
            .AsNoTracking()
            .SingleAsync(value => value.Id == update.GlobalRuleDecisionId, cancellationToken);
        var revisions = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => value.Id == update.SelectedSourceEntityRevisionId
                || value.Id == update.LatestSourceEntityRevisionId)
            .ToDictionaryAsync(value => value.Id, cancellationToken);

        using var selectedSource = JsonDocument.Parse(
            revisions[update.SelectedSourceEntityRevisionId].RawJson);
        var currentResolved = ApplyDecision(
            selectedSource.RootElement,
            decision.DecisionKind,
            decision.PatchJson);

        using var latestSource = JsonDocument.Parse(
            revisions[update.LatestSourceEntityRevisionId].RawJson);
        try
        {
            var candidateResolved = ApplyDecision(
                latestSource.RootElement,
                decision.DecisionKind,
                decision.PatchJson);
            return new SourceRevisionReviewPreviewView(
                update,
                PatchCompatible: true,
                CompatibilityMessage: null,
                currentResolved,
                candidateResolved,
                JsonDocumentDiff.Compare(currentResolved, candidateResolved));
        }
        catch (Exception exception) when (IsPatchCompatibilityFailure(exception))
        {
            return new SourceRevisionReviewPreviewView(
                update,
                PatchCompatible: false,
                CompatibilityMessage:
                    $"The existing {decision.DecisionKind} decision can not be applied to source revision #{update.LatestRevisionNumber}: {exception.Message}",
                currentResolved,
                CandidateResolvedDocument: null,
                Changes: []);
        }
    }

    private static JsonElement ApplyDecision(
        JsonElement source,
        string decisionKind,
        string? patchJson) => decisionKind switch
    {
        RuleDecisionKinds.JsonMergePatch => JsonMergePatch.Apply(source, patchJson),
        RuleDecisionKinds.JsonRulePatch => JsonRulePatch.Apply(source, patchJson),
        _ => source.Clone()
    };

    private static bool IsPatchCompatibilityFailure(Exception exception) =>
        exception is ArgumentException
            or InvalidOperationException
            or JsonException
            or KeyNotFoundException;

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
