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
        var entityType = RuleConceptEntityTypes.Normalize(
            RequireText(request.EntityType, nameof(request.EntityType), 120));
        var displayName = RequireText(request.DisplayName, nameof(request.DisplayName), 300);

        var existing = await dbContext.RuleConcepts
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Key == key, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(
                    RuleConceptEntityTypes.Normalize(existing.EntityType),
                    entityType,
                    StringComparison.Ordinal)
                || !string.Equals(existing.DisplayName, displayName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Rule concept '{key}' already exists with different immutable metadata.");
            }
            return new RuleMutationResult<RuleConceptView>(ToView(existing), false);
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
        return new RuleMutationResult<RuleConceptView>(ToView(concept), true);
    }

    public async Task<RuleMutationResult<RuleConceptSourceBindingView>> BindSourceEntityAsync(
        Guid ruleConceptId,
        BindRuleConceptSourceRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = RequireActor(actorUserId);
        await CanonicalRuleBindingStore.EnsureSchemaAsync(dbContext, cancellationToken);
        await RuleConceptRelationshipStore.EnsureSchemaAsync(dbContext, cancellationToken);

        var concept = await dbContext.RuleConcepts
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == ruleConceptId, cancellationToken)
            ?? throw new KeyNotFoundException($"Rule concept '{ruleConceptId}' does not exist.");

        var sourceEntity = await dbContext.SourceEntities
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == request.SourceEntityId, cancellationToken)
            ?? throw new KeyNotFoundException($"Source entity '{request.SourceEntityId}' does not exist.");
        if (!string.Equals(
                RuleConceptEntityTypes.Normalize(concept.EntityType),
                RuleConceptEntityTypes.Normalize(sourceEntity.EntityType),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Source entity type '{sourceEntity.EntityType}' can not be bound to rule concept type '{concept.EntityType}'.");
        }

        var canonicalEntityId = await CanonicalRuleBindingStore.GetCanonicalEntityIdAsync(
            dbContext,
            sourceEntity.Id,
            cancellationToken);
        var existing = await dbContext.RuleConceptSourceBindings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.RuleConceptId == ruleConceptId
                    && value.CanonicalEntityId == canonicalEntityId,
                cancellationToken);
        if (existing is not null)
        {
            await RuleAutoResolutionService.TryResolveAsync(dbContext, ruleConceptId, actor, cancellationToken);
            await RuleConceptRelationshipStore.SynchronizeSubclassParentsAsync(
                dbContext,
                actor,
                cancellationToken);
            return new RuleMutationResult<RuleConceptSourceBindingView>(ToView(existing), false);
        }

        var binding = new RuleConceptSourceBinding
        {
            Id = Guid.NewGuid(),
            RuleConceptId = ruleConceptId,
            CanonicalEntityId = canonicalEntityId,
            SourceEntityId = sourceEntity.Id,
            CreatedByUserId = actor,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.RuleConceptSourceBindings.Add(binding);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RuleAutoResolutionService.TryResolveAsync(dbContext, ruleConceptId, actor, cancellationToken);
        await RuleConceptRelationshipStore.SynchronizeSubclassParentsAsync(
            dbContext,
            actor,
            cancellationToken);
        return new RuleMutationResult<RuleConceptSourceBindingView>(ToView(binding), true);
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

        var mergePatch = request.MergePatch.HasValue ? JsonMergePatch.Normalize(request.MergePatch.Value) : null;
        var structuredPatch = request.StructuredPatch is not null ? JsonRulePatch.Normalize(request.StructuredPatch) : null;
        var decisionKind = structuredPatch is not null
            ? RuleDecisionKinds.JsonRulePatch
            : mergePatch is not null ? RuleDecisionKinds.JsonMergePatch : RuleDecisionKinds.SelectSource;
        var patchJson = structuredPatch?.Json ?? mergePatch?.Json;
        var patchFingerprint = structuredPatch?.Fingerprint ?? mergePatch?.Fingerprint;
        var contributions = SourceFrameworkStore.NormalizeDecisionContributions(request.Contributions);
        if (contributions.Any(value => value.SourceEntityRevisionId == request.SourceEntityRevisionId))
        {
            throw new ArgumentException(
                "The selected base source revision can not also be recorded as a consolidation contribution.", nameof(request));
        }
        var contributionFingerprint = SourceFrameworkStore.ComputeContributionFingerprint(contributions);

        await SourceFrameworkStore.EnsureSchemaAsync(dbContext, cancellationToken);
        await CanonicalRuleBindingStore.EnsureSchemaAsync(dbContext, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        if (!await dbContext.RuleConcepts.AnyAsync(value => value.Id == ruleConceptId, cancellationToken))
        {
            throw new KeyNotFoundException($"Rule concept '{ruleConceptId}' does not exist.");
        }

        var sourceRevision = await dbContext.SourceEntityRevisions
            .Include(value => value.SourceEntity)
                .ThenInclude(value => value.SourcePackage)
                .ThenInclude(value => value.UserGrants)
            .SingleOrDefaultAsync(value => value.Id == request.SourceEntityRevisionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Source entity revision '{request.SourceEntityRevisionId}' does not exist.");
        var sourcePackage = sourceRevision.SourceEntity.SourcePackage;
        if (!sourcePackage.IsPublic && !sourcePackage.UserGrants.Any(value => value.UserId == actor))
        {
            throw new InvalidOperationException("The selected source revision is not accessible to the current Rules Lawyer.");
        }

        if (!await CanonicalRuleBindingStore.IsSourceEntityRevisionBoundAsync(
                dbContext,
                ruleConceptId,
                sourceRevision.Id,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "The selected source revision belongs to a canonical entity that is not bound to this rule concept.");
        }

        await ValidateContributionsAsync(ruleConceptId, contributions, actor, cancellationToken);

        var latest = await dbContext.GlobalRuleDecisions
            .AsNoTracking()
            .Include(value => value.SelectedSourceEntityRevision)
                .ThenInclude(value => value.SourceEntity)
            .Where(value => value.RuleConceptId == ruleConceptId)
            .OrderByDescending(value => value.DecisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is not null)
        {
            var existingContributions = await SourceFrameworkStore.GetDecisionContributionsAsync(dbContext, latest.Id, cancellationToken);
            var existingContributionFingerprint = SourceFrameworkStore.ComputeContributionFingerprint(existingContributions);
            if (latest.DecisionKind == decisionKind
                && latest.SelectedSourceEntityRevisionId == sourceRevision.Id
                && string.Equals(latest.PatchFingerprint, patchFingerprint, StringComparison.Ordinal)
                && string.Equals(latest.Note, note, StringComparison.Ordinal)
                && string.Equals(existingContributionFingerprint, contributionFingerprint, StringComparison.Ordinal))
            {
                await transaction.CommitAsync(cancellationToken);
                return new RuleMutationResult<GlobalRuleDecisionView>(ToView(latest), false);
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
            dbContext, decision.Id, contributions, actor, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RuleMutationResult<GlobalRuleDecisionView>(ToView(decision), true);
    }

    public async Task<PublishedRulesetRevisionView> PublishAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = RequireActor(actorUserId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

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

        foreach (var decision in currentDecisions.Where(RuleAutoResolutionService.IsAutomaticDecision))
        {
            if (!await RuleAutoResolutionService.IsCurrentAutomaticDecisionAsync(dbContext, decision, actor, cancellationToken))
            {
                throw new InvalidOperationException(
                    $"Automatic resolution for rule concept '{decision.RuleConcept.Key}' is no longer current because a bound source now differs or can not be verified. Review the concept before publishing.");
            }
        }

        var fingerprint = ComputeRulesetFingerprint(currentDecisions);
        var latestRevision = await dbContext.RulesetRevisions
            .AsNoTracking()
            .OrderByDescending(value => value.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestRevision is not null && string.Equals(latestRevision.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            var entryCount = await dbContext.RulesetRevisionEntries.CountAsync(
                value => value.RulesetRevisionId == latestRevision.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToView(latestRevision, entryCount, false);
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
        return ToView(revision, currentDecisions.Length, true);
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
        if (latestRevision is null) return null;

        var entry = await dbContext.RulesetRevisionEntries
            .AsNoTracking()
            .Include(value => value.RuleConcept)
            .Include(value => value.GlobalRuleDecision)
            .SingleOrDefaultAsync(
                value => value.RulesetRevisionId == latestRevision.Id
                    && value.RuleConcept.Key == key,
                cancellationToken);
        if (entry is null) return null;

        var sourceRevision = await AccessibleCanonicalSourceResolver.ResolveRevisionAsync(
            dbContext,
            entry.RuleConceptId,
            entry.SourceEntityRevisionId,
            normalizedUserId,
            cancellationToken);
        if (sourceRevision is null) return null;

        var sourceEntity = sourceRevision.SourceEntity;
        var package = sourceEntity.SourcePackage;
        var decision = entry.GlobalRuleDecision;
        var concept = entry.RuleConcept;
        var contributionResolution = await RuleContributionResolution.ResolveAsync(
            dbContext, decision.Id, normalizedUserId, cancellationToken);
        if (!contributionResolution.Accessible) return null;

        using var sourceDocument = JsonDocument.Parse(sourceRevision.GetMechanicalContentJson());
        var resolvedDocument = ApplyDecisionPatch(sourceDocument.RootElement, decision.DecisionKind, decision.PatchJson);
        var mergePatch = decision.DecisionKind == RuleDecisionKinds.JsonMergePatch
            ? JsonMergePatch.ParsePatch(decision.PatchJson) : (JsonElement?)null;
        var structuredPatch = decision.DecisionKind == RuleDecisionKinds.JsonRulePatch
            ? JsonRulePatch.ParsePatch(decision.PatchJson) : (JsonElement?)null;

        return new ResolvedRuleView(
            concept.Id,
            concept.Key,
            RuleConceptEntityTypes.Normalize(concept.EntityType),
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
            sourceEntity.SourceCode ?? string.Empty,
            package.Key,
            package.DisplayName,
            package.Key,
            package.DisplayName,
            sourceEntity.FormatKey,
            sourceEntity.FormatKey,
            contributionResolution.Contributions,
            resolvedDocument);
    }

    public async Task<RuleConceptVersionsView?> GetAccessibleVersionsAsync(
        string conceptKey,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var key = NormalizeKey(conceptKey, nameof(conceptKey), 300);
        var normalizedUserId = NormalizeOptionalUserId(userId);
        var publishedRule = await ResolveLatestAsync(key, normalizedUserId, cancellationToken);
        if (publishedRule is null)
        {
            return null;
        }

        var accessibleSourceIds = await CanonicalRuleBindingStore.GetAccessibleSourceEntityIdsForConceptAsync(
            dbContext,
            publishedRule.RuleConceptId,
            normalizedUserId,
            cancellationToken);
        if (accessibleSourceIds.Count == 0)
        {
            return null;
        }

        var sources = await dbContext.SourceEntities
            .AsNoTracking()
            .Include(value => value.SourcePackage)
            .Include(value => value.Revisions)
            .Where(value => accessibleSourceIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        var canonicalBySource = await CanonicalRuleBindingStore.GetCanonicalEntityIdsAsync(
            dbContext,
            sources.Select(value => value.Id).ToArray(),
            cancellationToken);

        var candidates = sources
            .Select(source => new
            {
                Source = source,
                Revision = source.Revisions
                    .OrderByDescending(value => value.RevisionNumber)
                    .FirstOrDefault(),
                CanonicalEntityId = canonicalBySource.GetValueOrDefault(source.Id)
            })
            .Where(value => value.Revision is not null && value.CanonicalEntityId != Guid.Empty)
            .ToArray();

        var versions = new List<RuleConceptSourceVersionView>();
        foreach (var group in candidates
                     .GroupBy(value => value.CanonicalEntityId)
                     .OrderBy(value => value.Key))
        {
            var representative = group
                .OrderByDescending(value => value.Source.SourcePackage.IsPublic)
                .ThenBy(value => value.Source.SourceCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.Source.SourcePackage.Key, StringComparer.Ordinal)
                .ThenBy(value => value.Source.Id)
                .First();
            var revision = representative.Revision!;
            using var document = JsonDocument.Parse(revision.GetMechanicalContentJson());
            var source = representative.Source;
            versions.Add(new RuleConceptSourceVersionView(
                representative.CanonicalEntityId,
                source.Id,
                revision.Id,
                revision.RevisionNumber,
                revision.Fingerprint,
                source.Name,
                source.SourceCode ?? string.Empty,
                source.SourcePackage.Key,
                source.SourcePackage.DisplayName,
                source.FormatKey,
                revision.ImportedAt,
                group.Count(),
                document.RootElement.Clone()));
        }

        if (versions.Count == 0)
        {
            return null;
        }

        return new RuleConceptVersionsView(
            publishedRule.RuleConceptId,
            publishedRule.ConceptKey,
            publishedRule.EntityType,
            publishedRule.DisplayName,
            versions
                .OrderBy(value => value.SourceCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.PackageDisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.SourceEntityId)
                .ToArray());
    }

    private async Task ValidateContributionsAsync(
        Guid ruleConceptId,
        IReadOnlyCollection<NormalizedDecisionContribution> contributions,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        if (contributions.Count == 0) return;

        var revisionIds = contributions.Select(value => value.SourceEntityRevisionId).ToArray();
        var revisions = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Include(value => value.SourceEntity)
                .ThenInclude(value => value.SourcePackage)
                .ThenInclude(value => value.UserGrants)
            .Where(value => revisionIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        if (revisions.Length != revisionIds.Length)
        {
            throw new InvalidOperationException(
                "One or more consolidation source revisions do not exist or are no longer available.");
        }

        foreach (var revision in revisions)
        {
            if (!await CanonicalRuleBindingStore.IsSourceEntityRevisionBoundAsync(
                    dbContext,
                    ruleConceptId,
                    revision.Id,
                    cancellationToken))
            {
                throw new InvalidOperationException(
                    "Every consolidation contribution must come from a canonical entity bound to this rule concept.");
            }
            var package = revision.SourceEntity.SourcePackage;
            if (!package.IsPublic && !package.UserGrants.Any(grant => grant.UserId == actorUserId))
            {
                throw new InvalidOperationException(
                    "A consolidation contribution is not accessible to the current Rules Lawyer.");
            }
        }
    }

    private static JsonElement ApplyDecisionPatch(JsonElement source, string decisionKind, string? patchJson) => decisionKind switch
    {
        RuleDecisionKinds.JsonMergePatch => JsonMergePatch.Apply(source, patchJson),
        RuleDecisionKinds.JsonRulePatch => JsonRulePatch.Apply(source, patchJson),
        _ => source.Clone()
    };

    private static RuleConceptView ToView(RuleConcept concept) =>
        new(
            concept.Id,
            concept.Key,
            RuleConceptEntityTypes.Normalize(concept.EntityType),
            concept.DisplayName,
            concept.CreatedByUserId,
            concept.CreatedAt);

    private static RuleConceptSourceBindingView ToView(RuleConceptSourceBinding binding) =>
        new(
            binding.Id,
            binding.RuleConceptId,
            binding.CanonicalEntityId,
            binding.CreatedByUserId,
            binding.CreatedAt,
            binding.SourceEntityId);

    private static GlobalRuleDecisionView ToView(GlobalRuleDecision decision)
    {
        var mergePatch = decision.DecisionKind == RuleDecisionKinds.JsonMergePatch
            ? JsonMergePatch.ParsePatch(decision.PatchJson) : (JsonElement?)null;
        var structuredPatch = decision.DecisionKind == RuleDecisionKinds.JsonRulePatch
            ? JsonRulePatch.ParsePatch(decision.PatchJson) : (JsonElement?)null;
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
        new(revision.Id, revision.RevisionNumber, revision.Fingerprint, revision.PublishedByUserId,
            revision.PublishedAt, entryCount, createdRevision);

    private static string ComputeRulesetFingerprint(IReadOnlyList<GlobalRuleDecision> decisions)
    {
        var canonical = string.Join(
            '\n',
            decisions.Select(decision => string.Join(
                '\u001f',
                decision.RuleConcept.Key,
                decision.Id.ToString("D"),
                decision.DecisionKind,
                decision.SelectedSourceEntityRevisionId.ToString("D"),
                decision.SelectedSourceEntityRevision.Fingerprint,
                decision.PatchFingerprint ?? string.Empty)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string RequireActor(string actorUserId) => RequireText(actorUserId, nameof(actorUserId), 200);
    private static string NormalizeKey(string value, string parameterName, int maxLength) =>
        RequireText(value, parameterName, maxLength).ToLowerInvariant();

    private static string RequireText(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value can not be blank.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not exceed {maxLength} characters.", parameterName);
        }
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
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
