using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Rules;

public sealed class GlobalRulesService(RulesCoreDbContext dbContext) : IGlobalRulesService
{
    public async Task<RuleMutationResult<RuleConceptView>> CreateConceptAsync(
        CreateRuleConceptRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = RequireActor(actorUserId);
        var key = NormalizeKey(request.Key, nameof(request.Key), 300);
        var entityType = NormalizeKey(request.EntityType, nameof(request.EntityType), 120);
        var displayName = RequireText(request.DisplayName, nameof(request.DisplayName), 300);

        var existing = await dbContext.RuleConcepts
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Key == key, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.EntityType, entityType, StringComparison.Ordinal)
                || !string.Equals(existing.DisplayName, displayName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Rule concept '{key}' already exists with different immutable metadata.");
            }

            return new RuleMutationResult<RuleConceptView>(ToView(existing), Created: false);
        }

        var concept = new RuleConcept
        {
            Id = Guid.NewGuid(),
            Key = key,
            EntityType = entityType,
            DisplayName = displayName,
            CreatedByUserId = actor,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.RuleConcepts.Add(concept);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new RuleMutationResult<RuleConceptView>(ToView(concept), Created: true);
    }

    public async Task<RuleMutationResult<RuleConceptSourceBindingView>> BindSourceEntityAsync(
        Guid ruleConceptId,
        BindRuleConceptSourceRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = RequireActor(actorUserId);

        var conceptExists = await dbContext.RuleConcepts
            .AnyAsync(value => value.Id == ruleConceptId, cancellationToken);
        if (!conceptExists)
        {
            throw new KeyNotFoundException($"Rule concept '{ruleConceptId}' does not exist.");
        }

        var sourceEntity = await dbContext.SourceEntities
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == request.SourceEntityId, cancellationToken);
        if (sourceEntity is null)
        {
            throw new KeyNotFoundException($"Source entity '{request.SourceEntityId}' does not exist.");
        }

        var existing = await dbContext.RuleConceptSourceBindings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.RuleConceptId == ruleConceptId
                    && value.SourceEntityId == sourceEntity.Id,
                cancellationToken);
        if (existing is not null)
        {
            await RuleAutoResolutionService.TryResolveAsync(
                dbContext,
                ruleConceptId,
                actor,
                cancellationToken);
            return new RuleMutationResult<RuleConceptSourceBindingView>(
                ToView(existing),
                Created: false);
        }

        var binding = new RuleConceptSourceBinding
        {
            Id = Guid.NewGuid(),
            RuleConceptId = ruleConceptId,
            SourceEntityId = sourceEntity.Id,
            CreatedByUserId = actor,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.RuleConceptSourceBindings.Add(binding);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RuleAutoResolutionService.TryResolveAsync(
            dbContext,
            ruleConceptId,
            actor,
            cancellationToken);

        return new RuleMutationResult<RuleConceptSourceBindingView>(
            ToView(binding),
            Created: true);
    }

    public async Task<RuleMutationResult<GlobalRuleDecisionView>> SetDecisionAsync(
        Guid ruleConceptId,
        SetGlobalRuleDecisionRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = RequireActor(actorUserId);
        var note = NormalizeOptional(request.Note, 2000, nameof(request.Note));
        if (request.MergePatch.HasValue && request.StructuredPatch is not null)
        {
            throw new ArgumentException(
                "A global rule decision can not include both a legacy JSON merge patch and a structured rule patch.");
        }

        var mergePatch = request.MergePatch.HasValue
            ? JsonMergePatch.Normalize(request.MergePatch.Value)
            : null;
        var structuredPatch = request.StructuredPatch is not null
            ? JsonRulePatch.Normalize(request.StructuredPatch)
            : null;
        var decisionKind = structuredPatch is not null
            ? RuleDecisionKinds.JsonRulePatch
            : mergePatch is not null
                ? RuleDecisionKinds.JsonMergePatch
                : RuleDecisionKinds.SelectSource;
        var patchJson = structuredPatch?.Json ?? mergePatch?.Json;
        var patchFingerprint = structuredPatch?.Fingerprint ?? mergePatch?.Fingerprint;
        var contributions = SourceFrameworkStore.NormalizeDecisionContributions(request.Contributions);
        if (contributions.Any(value => value.SourceEntityRevisionId == request.SourceEntityRevisionId))
        {
            throw new ArgumentException(
                "The selected base source revision can not also be recorded as a consolidation contribution.",
                nameof(request));
        }
        var contributionFingerprint = SourceFrameworkStore.ComputeContributionFingerprint(contributions);

        await SourceFrameworkStore.EnsureSchemaAsync(dbContext, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var conceptExists = await dbContext.RuleConcepts
            .AnyAsync(value => value.Id == ruleConceptId, cancellationToken);
        if (!conceptExists)
        {
            throw new KeyNotFoundException($"Rule concept '{ruleConceptId}' does not exist.");
        }

        var sourceRevision = await dbContext.SourceEntityRevisions
            .Include(value => value.SourceEntity)
            .SingleOrDefaultAsync(
                value => value.Id == request.SourceEntityRevisionId,
                cancellationToken);
        if (sourceRevision is null)
        {
            throw new KeyNotFoundException(
                $"Source entity revision '{request.SourceEntityRevisionId}' does not exist.");
        }

        var isBound = await dbContext.RuleConceptSourceBindings
            .AnyAsync(
                value => value.RuleConceptId == ruleConceptId
                    && value.SourceEntityId == sourceRevision.SourceEntityId,
                cancellationToken);
        if (!isBound)
        {
            throw new InvalidOperationException(
                "The selected source revision belongs to an entity that is not bound to this rule concept.");
        }

        await ValidateContributionsAsync(
            ruleConceptId,
            contributions,
            actor,
            cancellationToken);

        var latest = await dbContext.GlobalRuleDecisions
            .AsNoTracking()
            .Include(value => value.SelectedSourceEntityRevision)
                .ThenInclude(value => value.SourceEntity)
            .Where(value => value.RuleConceptId == ruleConceptId)
            .OrderByDescending(value => value.DecisionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is not null)
        {
            var existingContributions = await SourceFrameworkStore.GetDecisionContributionsAsync(
                dbContext,
                latest.Id,
                cancellationToken);
            var existingContributionFingerprint = SourceFrameworkStore.ComputeContributionFingerprint(
                existingContributions);
            if (latest.DecisionKind == decisionKind
                && latest.SelectedSourceEntityRevisionId == sourceRevision.Id
                && string.Equals(latest.PatchFingerprint, patchFingerprint, StringComparison.Ordinal)
                && string.Equals(latest.Note, note, StringComparison.Ordinal)
                && string.Equals(existingContributionFingerprint, contributionFingerprint, StringComparison.Ordinal))
            {
                await transaction.CommitAsync(cancellationToken);
                return new RuleMutationResult<GlobalRuleDecisionView>(ToView(latest), Created: false);
            }
        }

        var decision = new GlobalRuleDecision
        {
            Id = Guid.NewGuid(),
            RuleConceptId = ruleConceptId,
            DecisionNumber = (latest?.DecisionNumber ?? 0) + 1,
            DecisionKind = decisionKind,
            SelectedSourceEntityRevisionId = sourceRevision.Id,
            SelectedSourceEntityRevision = sourceRevision,
            PatchJson = patchJson,
            PatchFingerprint = patchFingerprint,
            Note = note,
            CreatedByUserId = actor,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.GlobalRuleDecisions.Add(decision);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SourceFrameworkStore.InsertDecisionContributionsAsync(
            dbContext,
            decision.Id,
            contributions,
            actor,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RuleMutationResult<GlobalRuleDecisionView>(ToView(decision), Created: true);
    }

    public async Task<PublishedRulesetRevisionView> PublishAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = RequireActor(actorUserId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var allDecisions = await dbContext.GlobalRuleDecisions
            .AsNoTracking()
            .Include(value => value.RuleConcept)
            .Include(value => value.SelectedSourceEntityRevision)
            .ToArrayAsync(cancellationToken);
        var currentDecisions = allDecisions
            .GroupBy(value => value.RuleConceptId)
            .Select(group => group.OrderByDescending(value => value.DecisionNumber).First())
            .OrderBy(value => value.RuleConcept.Key, StringComparer.Ordinal)
            .ToArray();

        if (currentDecisions.Length == 0)
        {
            throw new InvalidOperationException("There are no global rule decisions to publish.");
        }

        var fingerprint = ComputeRulesetFingerprint(currentDecisions);
        var latestRevision = await dbContext.RulesetRevisions
            .AsNoTracking()
            .OrderByDescending(value => value.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestRevision is not null
            && string.Equals(latestRevision.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            var entryCount = await dbContext.RulesetRevisionEntries
                .CountAsync(value => value.RulesetRevisionId == latestRevision.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToView(latestRevision, entryCount, createdRevision: false);
        }

        var revision = new RulesetRevision
        {
            Id = Guid.NewGuid(),
            RevisionNumber = (latestRevision?.RevisionNumber ?? 0) + 1,
            Fingerprint = fingerprint,
            PublishedByUserId = actor,
            PublishedAt = DateTimeOffset.UtcNow
        };
        dbContext.RulesetRevisions.Add(revision);

        foreach (var decision in currentDecisions)
        {
            dbContext.RulesetRevisionEntries.Add(new RulesetRevisionEntry
            {
                Id = Guid.NewGuid(),
                RulesetRevisionId = revision.Id,
                RuleConceptId = decision.RuleConceptId,
                GlobalRuleDecisionId = decision.Id,
                SourceEntityRevisionId = decision.SelectedSourceEntityRevisionId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(revision, currentDecisions.Length, createdRevision: true);
    }

    public async Task<ResolvedRuleView?> ResolveLatestAsync(
        string conceptKey,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var key = NormalizeKey(conceptKey, nameof(conceptKey), 300);
        var normalizedUserId = NormalizeOptionalUserId(userId);
        var latestRevision = await dbContext.RulesetRevisions
            .AsNoTracking()
            .OrderByDescending(value => value.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestRevision is null)
        {
            return null;
        }

        var entry = await dbContext.RulesetRevisionEntries
            .AsNoTracking()
            .Include(value => value.RuleConcept)
            .Include(value => value.GlobalRuleDecision)
            .Include(value => value.SourceEntityRevision)
                .ThenInclude(value => value.SourceEntity)
                .ThenInclude(value => value.SourceEdition)
                .ThenInclude(value => value.SourceWork)
                .ThenInclude(value => value.SourcePackage)
            .SingleOrDefaultAsync(
                value => value.RulesetRevisionId == latestRevision.Id
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
        var decision = entry.GlobalRuleDecision;
        var concept = entry.RuleConcept;
        var contributionResolution = await RuleContributionResolution.ResolveAsync(
            dbContext,
            decision.Id,
            normalizedUserId,
            cancellationToken);
        if (!contributionResolution.Accessible)
        {
            return null;
        }

        using var sourceDocument = JsonDocument.Parse(sourceRevision.RawJson);
        var resolvedDocument = ApplyDecisionPatch(
            sourceDocument.RootElement,
            decision.DecisionKind,
            decision.PatchJson);
        var mergePatch = decision.DecisionKind == RuleDecisionKinds.JsonMergePatch
            ? JsonMergePatch.ParsePatch(decision.PatchJson)
            : (JsonElement?)null;
        var structuredPatch = decision.DecisionKind == RuleDecisionKinds.JsonRulePatch
            ? JsonRulePatch.ParsePatch(decision.PatchJson)
            : (JsonElement?)null;

        return new ResolvedRuleView(
            concept.Id,
            concept.Key,
            concept.EntityType,
            concept.DisplayName,
            latestRevision.RevisionNumber,
            latestRevision.Fingerprint,
            latestRevision.PublishedAt,
            decision.Id,
            decision.DecisionNumber,
            decision.DecisionKind,
            decision.Note,
            decision.PatchFingerprint,
            mergePatch,
            structuredPatch,
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
            contributionResolution.Contributions,
            resolvedDocument);
    }

    private async Task ValidateContributionsAsync(
        Guid ruleConceptId,
        IReadOnlyCollection<NormalizedDecisionContribution> contributions,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        if (contributions.Count == 0)
        {
            return;
        }

        var revisionIds = contributions.Select(value => value.SourceEntityRevisionId).ToArray();
        var revisions = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Include(value => value.SourceEntity)
                .ThenInclude(value => value.SourceEdition)
                .ThenInclude(value => value.SourceWork)
                .ThenInclude(value => value.SourcePackage)
                .ThenInclude(value => value.UserGrants)
            .Where(value => revisionIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        if (revisions.Length != revisionIds.Length)
        {
            throw new InvalidOperationException(
                "One or more consolidation source revisions do not exist or are no longer available.");
        }

        var sourceEntityIds = revisions.Select(value => value.SourceEntityId).Distinct().ToArray();
        var boundSourceIds = await dbContext.RuleConceptSourceBindings
            .AsNoTracking()
            .Where(value => value.RuleConceptId == ruleConceptId
                && sourceEntityIds.Contains(value.SourceEntityId))
            .Select(value => value.SourceEntityId)
            .ToArrayAsync(cancellationToken);
        var boundSet = boundSourceIds.ToHashSet();

        foreach (var revision in revisions)
        {
            if (!boundSet.Contains(revision.SourceEntityId))
            {
                throw new InvalidOperationException(
                    "Every consolidation contribution must come from a source entity bound to this rule concept.");
            }

            var package = revision.SourceEntity.SourceEdition.SourceWork.SourcePackage;
            if (!package.IsPublic && !package.UserGrants.Any(grant => grant.UserId == actorUserId))
            {
                throw new InvalidOperationException(
                    "A consolidation contribution is not accessible to the current Rules Lawyer.");
            }
        }
    }

    private static JsonElement ApplyDecisionPatch(
        JsonElement source,
        string decisionKind,
        string? patchJson) => decisionKind switch
    {
        RuleDecisionKinds.JsonMergePatch => JsonMergePatch.Apply(source, patchJson),
        RuleDecisionKinds.JsonRulePatch => JsonRulePatch.Apply(source, patchJson),
        _ => source.Clone()
    };

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

    private static PublishedRulesetRevisionView ToView(
        RulesetRevision revision,
        int entryCount,
        bool createdRevision) =>
        new(
            revision.Id,
            revision.RevisionNumber,
            revision.Fingerprint,
            revision.PublishedByUserId,
            revision.PublishedAt,
            entryCount,
            createdRevision);

    private static string ComputeRulesetFingerprint(IReadOnlyList<GlobalRuleDecision> decisions)
    {
        var canonical = string.Join(
            '\n',
            decisions.Select(decision =>
                string.Join(
                    '\u001f',
                    decision.RuleConcept.Key,
                    decision.Id.ToString("D"),
                    decision.DecisionKind,
                    decision.SelectedSourceEntityRevisionId.ToString("D"),
                    decision.SelectedSourceEntityRevision.Fingerprint,
                    decision.PatchFingerprint ?? string.Empty)));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string RequireActor(string actorUserId) =>
        RequireText(actorUserId, nameof(actorUserId), 200);

    private static string NormalizeKey(string value, string parameterName, int maxLength) =>
        RequireText(value, parameterName, maxLength).ToLowerInvariant();

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
}
