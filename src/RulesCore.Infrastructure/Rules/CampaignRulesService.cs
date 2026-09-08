using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

public sealed class CampaignRulesService(RulesCoreDbContext dbContext) : ICampaignRulesService
{
    public async Task<CampaignRulesetSelectionView> SelectBaselineAsync(
        Guid campaignId,
        SelectCampaignRulesetBaselineRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireGuid(campaignId, nameof(campaignId));
        RequireGuid(request.RulesetRevisionId, nameof(request.RulesetRevisionId));
        var actor = RequireText(actorUserId, nameof(actorUserId), 200);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var rulesetRevision = await dbContext.RulesetRevisions
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == request.RulesetRevisionId, cancellationToken);
        if (rulesetRevision is null)
        {
            throw new KeyNotFoundException(
                $"Global ruleset revision '{request.RulesetRevisionId}' does not exist.");
        }

        var latestSelection = await dbContext.CampaignRulesetSelections
            .AsNoTracking()
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.SelectionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestSelection is not null
            && latestSelection.RulesetRevisionId == rulesetRevision.Id)
        {
            await transaction.CommitAsync(cancellationToken);
            return ToView(latestSelection, rulesetRevision, created: false);
        }

        var selection = new CampaignRulesetSelection
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            SelectionNumber = (latestSelection?.SelectionNumber ?? 0) + 1,
            RulesetRevisionId = rulesetRevision.Id,
            SelectedByUserId = actor,
            SelectedAt = DateTimeOffset.UtcNow
        };
        dbContext.CampaignRulesetSelections.Add(selection);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToView(selection, rulesetRevision, created: true);
    }

    public async Task<CampaignRuleDecisionView> SetDecisionAsync(
        Guid campaignId,
        Guid ruleConceptId,
        SetCampaignRuleDecisionRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireGuid(campaignId, nameof(campaignId));
        RequireGuid(ruleConceptId, nameof(ruleConceptId));
        var actor = RequireText(actorUserId, nameof(actorUserId), 200);
        var decisionKind = RequireText(request.DecisionKind, nameof(request.DecisionKind), 80)
            .ToLowerInvariant();
        if (!CampaignRuleDecisionKinds.All.Contains(decisionKind))
        {
            throw new ArgumentException(
                $"Decision kind must be '{CampaignRuleDecisionKinds.SelectSource}' or '{CampaignRuleDecisionKinds.InheritGlobal}'.",
                nameof(request.DecisionKind));
        }

        var note = NormalizeOptional(request.Note, 2000, nameof(request.Note));

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var baseline = await dbContext.CampaignRulesetSelections
            .AsNoTracking()
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.SelectionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (baseline is null)
        {
            throw new InvalidOperationException(
                "The campaign must deliberately select a published global ruleset revision before creating overrides.");
        }

        var baselineContainsConcept = await dbContext.RulesetRevisionEntries
            .AsNoTracking()
            .AnyAsync(
                value => value.RulesetRevisionId == baseline.RulesetRevisionId
                    && value.RuleConceptId == ruleConceptId,
                cancellationToken);
        if (!baselineContainsConcept)
        {
            throw new KeyNotFoundException(
                $"Rule concept '{ruleConceptId}' does not exist in the campaign's selected global ruleset revision.");
        }

        Guid? selectedSourceEntityRevisionId = null;
        if (decisionKind == CampaignRuleDecisionKinds.SelectSource)
        {
            if (request.SourceEntityRevisionId is null || request.SourceEntityRevisionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "A source entity revision is required for a select-source campaign decision.",
                    nameof(request.SourceEntityRevisionId));
            }

            var sourceRevision = await dbContext.SourceEntityRevisions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    value => value.Id == request.SourceEntityRevisionId.Value,
                    cancellationToken);
            if (sourceRevision is null)
            {
                throw new KeyNotFoundException(
                    $"Source entity revision '{request.SourceEntityRevisionId}' does not exist.");
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

            selectedSourceEntityRevisionId = sourceRevision.Id;
        }
        else if (request.SourceEntityRevisionId is not null)
        {
            throw new ArgumentException(
                "An inherit-global campaign decision can not select a source entity revision.",
                nameof(request.SourceEntityRevisionId));
        }

        var latestDecision = await dbContext.CampaignRuleDecisions
            .AsNoTracking()
            .Where(value => value.CampaignId == campaignId && value.RuleConceptId == ruleConceptId)
            .OrderByDescending(value => value.DecisionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestDecision is not null
            && string.Equals(latestDecision.DecisionKind, decisionKind, StringComparison.Ordinal)
            && latestDecision.SelectedSourceEntityRevisionId == selectedSourceEntityRevisionId
            && string.Equals(latestDecision.Note, note, StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return ToView(latestDecision, created: false);
        }

        var decision = new CampaignRuleDecision
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            RuleConceptId = ruleConceptId,
            DecisionNumber = (latestDecision?.DecisionNumber ?? 0) + 1,
            DecisionKind = decisionKind,
            SelectedSourceEntityRevisionId = selectedSourceEntityRevisionId,
            Note = note,
            CreatedByUserId = actor,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CampaignRuleDecisions.Add(decision);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToView(decision, created: true);
    }

    public async Task<PublishedCampaignRulesetRevisionView> PublishAsync(
        Guid campaignId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        RequireGuid(campaignId, nameof(campaignId));
        var actor = RequireText(actorUserId, nameof(actorUserId), 200);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var baselineSelection = await dbContext.CampaignRulesetSelections
            .AsNoTracking()
            .Include(value => value.RulesetRevision)
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.SelectionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (baselineSelection is null)
        {
            throw new InvalidOperationException(
                "The campaign must deliberately select a published global ruleset revision before publication.");
        }

        var baselineEntries = await dbContext.RulesetRevisionEntries
            .AsNoTracking()
            .Include(value => value.RuleConcept)
            .Include(value => value.SourceEntityRevision)
            .Where(value => value.RulesetRevisionId == baselineSelection.RulesetRevisionId)
            .OrderBy(value => value.RuleConcept.Key)
            .ToArrayAsync(cancellationToken);
        if (baselineEntries.Length == 0)
        {
            throw new InvalidOperationException("The selected global ruleset revision contains no rules.");
        }

        var allCampaignDecisions = await dbContext.CampaignRuleDecisions
            .AsNoTracking()
            .Include(value => value.SelectedSourceEntityRevision)
            .Where(value => value.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);
        var latestDecisionByConcept = allCampaignDecisions
            .GroupBy(value => value.RuleConceptId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(value => value.DecisionNumber).First());

        var effectiveEntries = new List<EffectiveCampaignEntry>(baselineEntries.Length);
        foreach (var baselineEntry in baselineEntries)
        {
            latestDecisionByConcept.TryGetValue(baselineEntry.RuleConceptId, out var campaignDecision);

            Guid effectiveSourceRevisionId;
            string effectiveSourceFingerprint;
            if (campaignDecision is not null
                && campaignDecision.DecisionKind == CampaignRuleDecisionKinds.SelectSource)
            {
                var sourceRevision = campaignDecision.SelectedSourceEntityRevision
                    ?? throw new InvalidOperationException(
                        "A select-source campaign decision is missing its selected source revision.");
                effectiveSourceRevisionId = sourceRevision.Id;
                effectiveSourceFingerprint = sourceRevision.Fingerprint;
            }
            else
            {
                effectiveSourceRevisionId = baselineEntry.SourceEntityRevisionId;
                effectiveSourceFingerprint = baselineEntry.SourceEntityRevision.Fingerprint;
            }

            effectiveEntries.Add(new EffectiveCampaignEntry(
                baselineEntry,
                campaignDecision,
                effectiveSourceRevisionId,
                effectiveSourceFingerprint));
        }

        var fingerprint = ComputeCampaignRulesetFingerprint(
            baselineSelection,
            effectiveEntries);
        var latestRevision = await dbContext.CampaignRulesetRevisions
            .AsNoTracking()
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestRevision is not null
            && string.Equals(latestRevision.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            var entryCount = await dbContext.CampaignRulesetRevisionEntries
                .CountAsync(
                    value => value.CampaignRulesetRevisionId == latestRevision.Id,
                    cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToView(
                latestRevision,
                baselineSelection,
                entryCount,
                createdRevision: false);
        }

        var revision = new CampaignRulesetRevision
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            RevisionNumber = (latestRevision?.RevisionNumber ?? 0) + 1,
            BaselineSelectionId = baselineSelection.Id,
            Fingerprint = fingerprint,
            PublishedByUserId = actor,
            PublishedAt = DateTimeOffset.UtcNow
        };
        dbContext.CampaignRulesetRevisions.Add(revision);

        foreach (var effectiveEntry in effectiveEntries)
        {
            dbContext.CampaignRulesetRevisionEntries.Add(new CampaignRulesetRevisionEntry
            {
                Id = Guid.NewGuid(),
                CampaignRulesetRevisionId = revision.Id,
                RuleConceptId = effectiveEntry.BaselineEntry.RuleConceptId,
                BaselineRulesetRevisionEntryId = effectiveEntry.BaselineEntry.Id,
                CampaignRuleDecisionId = effectiveEntry.CampaignDecision?.Id,
                SourceEntityRevisionId = effectiveEntry.SourceEntityRevisionId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToView(
            revision,
            baselineSelection,
            effectiveEntries.Count,
            createdRevision: true);
    }

    public async Task<ResolvedCampaignRuleView?> ResolveLatestAsync(
        Guid campaignId,
        string conceptKey,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        RequireGuid(campaignId, nameof(campaignId));
        var key = RequireText(conceptKey, nameof(conceptKey), 300).ToLowerInvariant();
        var normalizedUserId = NormalizeOptionalUserId(userId);

        var latestRevision = await dbContext.CampaignRulesetRevisions
            .AsNoTracking()
            .Include(value => value.BaselineSelection)
                .ThenInclude(value => value.RulesetRevision)
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestRevision is null)
        {
            return null;
        }

        var entry = await dbContext.CampaignRulesetRevisionEntries
            .AsNoTracking()
            .Include(value => value.RuleConcept)
            .Include(value => value.BaselineRulesetRevisionEntry)
                .ThenInclude(value => value.GlobalRuleDecision)
            .Include(value => value.CampaignRuleDecision)
            .Include(value => value.SourceEntityRevision)
                .ThenInclude(value => value.SourceEntity)
                .ThenInclude(value => value.SourceEdition)
                .ThenInclude(value => value.SourceWork)
                .ThenInclude(value => value.SourcePackage)
            .SingleOrDefaultAsync(
                value => value.CampaignRulesetRevisionId == latestRevision.Id
                    && value.RuleConcept.Key == key
                    && (value.SourceEntityRevision.SourceEntity.SourceEdition.SourceWork.SourcePackage.IsPublic
                        || (normalizedUserId != null
                            && value.SourceEntityRevision.SourceEntity.SourceEdition.SourceWork.SourcePackage.UserGrants
                                .Any(grant => grant.UserId == normalizedUserId))),
                cancellationToken);
        if (entry is null)
        {
            return null;
        }

        var sourceRevision = entry.SourceEntityRevision;
        var sourceEntity = sourceRevision.SourceEntity;
        var edition = sourceEntity.SourceEdition;
        var work = edition.SourceWork;
        var package = work.SourcePackage;
        var concept = entry.RuleConcept;
        var globalDecision = entry.BaselineRulesetRevisionEntry.GlobalRuleDecision;
        var campaignDecision = entry.CampaignRuleDecision;
        var baselineRuleset = latestRevision.BaselineSelection.RulesetRevision;

        using var document = JsonDocument.Parse(sourceRevision.RawJson);
        return new ResolvedCampaignRuleView(
            campaignId,
            concept.Id,
            concept.Key,
            concept.EntityType,
            concept.DisplayName,
            latestRevision.RevisionNumber,
            latestRevision.Fingerprint,
            latestRevision.PublishedAt,
            baselineRuleset.Id,
            baselineRuleset.RevisionNumber,
            baselineRuleset.Fingerprint,
            globalDecision.Id,
            globalDecision.DecisionNumber,
            campaignDecision?.Id,
            campaignDecision?.DecisionNumber,
            campaignDecision?.DecisionKind ?? CampaignRuleDecisionKinds.InheritGlobal,
            campaignDecision?.Note,
            sourceEntity.Id,
            sourceRevision.Id,
            sourceRevision.RevisionNumber,
            sourceRevision.Fingerprint,
            sourceEntity.Name,
            sourceEntity.SourceCode,
            package.Key,
            package.DisplayName,
            work.Key,
            work.DisplayName,
            edition.Key,
            edition.DisplayName,
            document.RootElement.Clone());
    }

    private static CampaignRulesetSelectionView ToView(
        CampaignRulesetSelection selection,
        RulesetRevision rulesetRevision,
        bool created) =>
        new(
            selection.Id,
            selection.CampaignId,
            selection.SelectionNumber,
            rulesetRevision.Id,
            rulesetRevision.RevisionNumber,
            rulesetRevision.Fingerprint,
            selection.SelectedByUserId,
            selection.SelectedAt,
            created);

    private static CampaignRuleDecisionView ToView(
        CampaignRuleDecision decision,
        bool created) =>
        new(
            decision.Id,
            decision.CampaignId,
            decision.RuleConceptId,
            decision.DecisionNumber,
            decision.DecisionKind,
            decision.SelectedSourceEntityRevisionId,
            decision.Note,
            decision.CreatedByUserId,
            decision.CreatedAt,
            created);

    private static PublishedCampaignRulesetRevisionView ToView(
        CampaignRulesetRevision revision,
        CampaignRulesetSelection baselineSelection,
        int entryCount,
        bool createdRevision) =>
        new(
            revision.Id,
            revision.CampaignId,
            revision.RevisionNumber,
            revision.Fingerprint,
            baselineSelection.Id,
            baselineSelection.RulesetRevision.Id,
            baselineSelection.RulesetRevision.RevisionNumber,
            baselineSelection.RulesetRevision.Fingerprint,
            revision.PublishedByUserId,
            revision.PublishedAt,
            entryCount,
            createdRevision);

    private static string ComputeCampaignRulesetFingerprint(
        CampaignRulesetSelection baselineSelection,
        IReadOnlyList<EffectiveCampaignEntry> entries)
    {
        var lines = new List<string>
        {
            string.Join(
                '\u001f',
                baselineSelection.Id.ToString("D"),
                baselineSelection.RulesetRevisionId.ToString("D"),
                baselineSelection.RulesetRevision.Fingerprint)
        };

        lines.AddRange(entries
            .OrderBy(value => value.BaselineEntry.RuleConcept.Key, StringComparer.Ordinal)
            .Select(value => string.Join(
                '\u001f',
                value.BaselineEntry.RuleConcept.Key,
                value.BaselineEntry.Id.ToString("D"),
                value.CampaignDecision?.Id.ToString("D") ?? "baseline",
                value.CampaignDecision?.DecisionKind ?? CampaignRuleDecisionKinds.InheritGlobal,
                value.CampaignDecision?.Note ?? string.Empty,
                value.SourceEntityRevisionId.ToString("D"),
                value.SourceFingerprint)));

        var canonical = string.Join('\n', lines);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static void RequireGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Value can not be an empty GUID.", parameterName);
        }
    }

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

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not exceed {maxLength} characters.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptionalUserId(string? userId) =>
        string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();

    private sealed record EffectiveCampaignEntry(
        RulesetRevisionEntry BaselineEntry,
        CampaignRuleDecision? CampaignDecision,
        Guid SourceEntityRevisionId,
        string SourceFingerprint);
}
