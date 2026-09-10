using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

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
                .ThenInclude(value => value.UserGrants)
            .OrderBy(value => value.RuleConceptId)
            .ThenByDescending(value => value.DecisionNumber)
            .ToArrayAsync(cancellationToken);

        var latestDecisions = decisions
            .GroupBy(value => value.RuleConceptId)
            .Select(group => group.First())
            .Where(decision =>
            {
                var package = decision.SelectedSourceEntityRevision
                    .SourceEntity.SourceEdition.SourceWork.SourcePackage;
                return package.IsPublic
                    || package.UserGrants.Any(grant => grant.UserId == normalizedUserId);
            })
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
        RequireGuid(ruleConceptId, nameof(ruleConceptId));
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

    public async Task<AdoptedSourceRevisionView?> AdoptLatestAsync(
        Guid ruleConceptId,
        AdoptLatestSourceRevisionRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireGuid(ruleConceptId, nameof(ruleConceptId));
        RequireGuid(request.ExpectedGlobalRuleDecisionId, nameof(request.ExpectedGlobalRuleDecisionId));
        RequireGuid(request.ExpectedLatestSourceEntityRevisionId, nameof(request.ExpectedLatestSourceEntityRevisionId));
        var actor = RequireUserId(actorUserId);
        var expectedFingerprint = RequireFingerprint(request.ExpectedLatestFingerprint);
        await SourceFrameworkStore.EnsureSchemaAsync(dbContext, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var current = await dbContext.GlobalRuleDecisions
            .AsNoTracking()
            .Include(value => value.RuleConcept)
            .Include(value => value.SelectedSourceEntityRevision)
                .ThenInclude(value => value.SourceEntity)
                .ThenInclude(value => value.SourceEdition)
                .ThenInclude(value => value.SourceWork)
                .ThenInclude(value => value.SourcePackage)
                .ThenInclude(value => value.UserGrants)
            .Where(value => value.RuleConceptId == ruleConceptId)
            .OrderByDescending(value => value.DecisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            throw new KeyNotFoundException($"Rule concept '{ruleConceptId}' has no global rule decision.");
        }

        if (current.Id != request.ExpectedGlobalRuleDecisionId)
        {
            throw new InvalidOperationException(
                "The global rule decision changed after this source update was reviewed. Reload the review before adopting a source revision.");
        }

        var package = current.SelectedSourceEntityRevision
            .SourceEntity.SourceEdition.SourceWork.SourcePackage;
        if (!package.IsPublic && !package.UserGrants.Any(grant => grant.UserId == actor))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var latestRevision = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => value.SourceEntityId == current.SelectedSourceEntityRevision.SourceEntityId)
            .OrderByDescending(value => value.RevisionNumber)
            .FirstAsync(cancellationToken);

        if (latestRevision.RevisionNumber <= current.SelectedSourceEntityRevision.RevisionNumber)
        {
            throw new InvalidOperationException(
                "This global rule decision no longer has a newer source revision to adopt.");
        }

        if (latestRevision.Id != request.ExpectedLatestSourceEntityRevisionId
            || !string.Equals(latestRevision.Fingerprint, expectedFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The source entity changed after this update was reviewed. Reload the review before adopting a source revision.");
        }

        using (var latestSource = JsonDocument.Parse(latestRevision.RawJson))
        {
            try
            {
                _ = ApplyDecision(
                    latestSource.RootElement,
                    current.DecisionKind,
                    current.PatchJson);
            }
            catch (Exception exception) when (IsPatchCompatibilityFailure(exception))
            {
                throw new InvalidOperationException(
                    $"The existing {current.DecisionKind} decision can not be carried forward to source revision #{latestRevision.RevisionNumber}: {exception.Message}",
                    exception);
            }
        }

        var adopted = new GlobalRuleDecision
        {
            Id = Guid.NewGuid(),
            RuleConceptId = current.RuleConceptId,
            DecisionNumber = current.DecisionNumber + 1,
            DecisionKind = current.DecisionKind,
            SelectedSourceEntityRevisionId = latestRevision.Id,
            PatchJson = current.PatchJson,
            PatchFingerprint = current.PatchFingerprint,
            Note = current.Note,
            CreatedByUserId = actor,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.GlobalRuleDecisions.Add(adopted);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SourceFrameworkStore.CopyDecisionContributionsAsync(
            dbContext,
            current.Id,
            adopted.Id,
            actor,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AdoptedSourceRevisionView(
            current.RuleConceptId,
            current.RuleConcept.Key,
            current.Id,
            adopted.Id,
            adopted.DecisionNumber,
            adopted.DecisionKind,
            current.SelectedSourceEntityRevisionId,
            latestRevision.Id,
            latestRevision.RevisionNumber,
            latestRevision.Fingerprint,
            adopted.PatchFingerprint,
            adopted.Note,
            adopted.CreatedByUserId,
            adopted.CreatedAt,
            RequiresPublication: true);
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
            or InvalidDataException
            or JsonException
            or KeyNotFoundException;

    private static void RequireGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Value can not be an empty GUID.", parameterName);
        }
    }

    private static string RequireFingerprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Expected source fingerprint can not be blank.", nameof(value));
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "Expected source fingerprint must be a 64-character SHA-256 hexadecimal value.",
                nameof(value));
        }
        return normalized;
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
