using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Rules;

public sealed class SourceVersioningService(RulesCoreDbContext dbContext) : ISourceVersioningService
{
    private static readonly HashSet<string> ReferenceProperties = new(
        ["reprintedAs", "otherSources", "additionalSources", "previousVersion", "previousVersions", "versions", "seeAlso"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ContentStopWords = new(
        ["with", "that", "this", "from", "your", "have", "when", "then", "into", "they", "their", "there", "which", "while", "where", "about", "source", "page", "name"],
        StringComparer.OrdinalIgnoreCase);

    public async Task<SourceVersionDetectionView?> DetectVersionsAsync(
        Guid sourceEntityId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        RequireGuid(sourceEntityId, nameof(sourceEntityId));
        var actor = RequireUserId(userId);
        await SourceFrameworkStore.EnsureSchemaAsync(dbContext, cancellationToken);

        var source = await AccessibleSources(actor)
            .SingleOrDefaultAsync(value => value.Id == sourceEntityId, cancellationToken);
        if (source is null)
        {
            return null;
        }

        var entityType = source.EntityType.ToLower();
        var candidates = await AccessibleSources(actor)
            .Where(value => value.Id != source.Id && value.EntityType.ToLower() == entityType)
            .OrderBy(value => value.Name)
            .Take(500)
            .ToArrayAsync(cancellationToken);

        var allIds = candidates.Select(value => value.Id).Append(source.Id).ToArray();
        var bindings = await dbContext.RuleConceptSourceBindings
            .AsNoTracking()
            .Include(value => value.RuleConcept)
            .Where(value => allIds.Contains(value.SourceEntityId))
            .ToArrayAsync(cancellationToken);
        var bindingsBySource = bindings
            .GroupBy(value => value.SourceEntityId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<RuleConceptReferenceView>)group
                    .Select(value => new RuleConceptReferenceView(
                        value.RuleConcept.Id,
                        value.RuleConcept.Key,
                        value.RuleConcept.EntityType,
                        value.RuleConcept.DisplayName))
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .ToArray());

        var lineage = await SourceFrameworkStore.GetLineageForSourcesAsync(
            dbContext,
            allIds,
            includeVoided: false,
            cancellationToken);

        bindingsBySource.TryGetValue(source.Id, out var sourceConcepts);
        sourceConcepts ??= [];
        var sourceView = await ToEntityViewAsync(source, sourceConcepts, cancellationToken);
        var sourceLatest = Latest(source);
        var sourceMetadata = await SourceFrameworkStore.GetEditionMetadataAsync(
            dbContext,
            source.SourceEditionId,
            cancellationToken);

        var scored = new List<SourceVersionCandidateView>();
        foreach (var candidate in candidates)
        {
            bindingsBySource.TryGetValue(candidate.Id, out var candidateConcepts);
            candidateConcepts ??= [];
            var candidateMetadata = await SourceFrameworkStore.GetEditionMetadataAsync(
                dbContext,
                candidate.SourceEditionId,
                cancellationToken);
            var reasons = new List<string>();
            var confidence = ScoreCandidate(
                source,
                sourceLatest,
                sourceConcepts,
                sourceMetadata,
                candidate,
                Latest(candidate),
                candidateConcepts,
                candidateMetadata,
                lineage,
                reasons);
            if (confidence < 30)
            {
                continue;
            }

            scored.Add(new SourceVersionCandidateView(
                await ToEntityViewAsync(candidate, candidateConcepts, cancellationToken),
                confidence,
                reasons));
        }

        return new SourceVersionDetectionView(
            sourceView,
            scored
                .OrderByDescending(value => value.Confidence)
                .ThenBy(value => value.Candidate.Name, StringComparer.Ordinal)
                .ThenBy(value => value.Candidate.SourceCode, StringComparer.Ordinal)
                .Take(50)
                .ToArray());
    }

    public async Task<RuleMutationResult<RuleConceptSourceBindingView>?> BindToConceptAsync(
        Guid sourceEntityId,
        Guid ruleConceptId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        RequireGuid(sourceEntityId, nameof(sourceEntityId));
        RequireGuid(ruleConceptId, nameof(ruleConceptId));
        var actor = RequireUserId(actorUserId);
        await SourceFrameworkStore.EnsureSchemaAsync(dbContext, cancellationToken);

        var source = await AccessibleSources(actor)
            .SingleOrDefaultAsync(value => value.Id == sourceEntityId, cancellationToken);
        if (source is null)
        {
            return null;
        }
        var concept = await dbContext.RuleConcepts
            .SingleOrDefaultAsync(value => value.Id == ruleConceptId, cancellationToken);
        if (concept is null)
        {
            throw new KeyNotFoundException($"Rule concept '{ruleConceptId}' does not exist.");
        }
        if (!string.Equals(
                concept.EntityType.Trim(),
                source.EntityType.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Source entity type '{source.EntityType}' can not be bound to concept type '{concept.EntityType}'.");
        }

        var existing = await dbContext.RuleConceptSourceBindings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.RuleConceptId == concept.Id && value.SourceEntityId == source.Id,
                cancellationToken);
        if (existing is not null)
        {
            return new RuleMutationResult<RuleConceptSourceBindingView>(ToView(existing), Created: false);
        }

        var binding = new RulesCore.Domain.Rules.RuleConceptSourceBinding
        {
            Id = Guid.NewGuid(),
            RuleConceptId = concept.Id,
            SourceEntityId = source.Id,
            CreatedByUserId = actor,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.RuleConceptSourceBindings.Add(binding);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new RuleMutationResult<RuleConceptSourceBindingView>(ToView(binding), Created: true);
    }

    public async Task<SourceLineageMutationView?> CreateLineageAsync(
        CreateSourceLineageRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireGuid(request.FromSourceEntityId, nameof(request.FromSourceEntityId));
        RequireGuid(request.ToSourceEntityId, nameof(request.ToSourceEntityId));
        if (request.FromSourceEntityId == request.ToSourceEntityId)
        {
            throw new ArgumentException("A source entity can not have a lineage relationship to itself.");
        }
        var actor = RequireUserId(actorUserId);
        var kind = RequireLineageKind(request.RelationshipKind);
        var note = NormalizeOptional(request.Note, 2000, nameof(request.Note));
        await SourceFrameworkStore.EnsureSchemaAsync(dbContext, cancellationToken);

        var accessibleCount = await AccessibleSources(actor)
            .CountAsync(
                value => value.Id == request.FromSourceEntityId || value.Id == request.ToSourceEntityId,
                cancellationToken);
        if (accessibleCount != 2)
        {
            return null;
        }

        var result = await SourceFrameworkStore.CreateLineageAsync(
            dbContext,
            request.FromSourceEntityId,
            request.ToSourceEntityId,
            kind,
            note,
            actor,
            cancellationToken);
        return new SourceLineageMutationView(ToView(result.Lineage), result.Changed);
    }

    public async Task<SourceLineageMutationView?> VoidLineageAsync(
        Guid lineageId,
        VoidSourceLineageRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireGuid(lineageId, nameof(lineageId));
        var actor = RequireUserId(actorUserId);
        var reason = NormalizeOptional(request.Reason, 1000, nameof(request.Reason));
        await SourceFrameworkStore.EnsureSchemaAsync(dbContext, cancellationToken);

        var existing = await SourceFrameworkStore.GetLineageByIdAsync(
            dbContext,
            lineageId,
            cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var accessibleCount = await AccessibleSources(actor)
            .CountAsync(
                value => value.Id == existing.FromSourceEntityId || value.Id == existing.ToSourceEntityId,
                cancellationToken);
        if (accessibleCount != 2)
        {
            return null;
        }

        var result = await SourceFrameworkStore.VoidLineageAsync(
            dbContext,
            lineageId,
            reason,
            actor,
            cancellationToken);
        return result is null
            ? null
            : new SourceLineageMutationView(ToView(result.Value.Lineage), result.Value.Changed);
    }

    public async Task<IReadOnlyList<SourceLineageView>> GetActiveLineageForSourcesAsync(
        IReadOnlyCollection<Guid> sourceEntityIds,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceEntityIds);
        var actor = RequireUserId(userId);
        await SourceFrameworkStore.EnsureSchemaAsync(dbContext, cancellationToken);
        if (sourceEntityIds.Count == 0)
        {
            return [];
        }

        var requested = sourceEntityIds.Where(value => value != Guid.Empty).Distinct().ToArray();
        var accessibleIds = await AccessibleSources(actor)
            .Where(value => requested.Contains(value.Id))
            .Select(value => value.Id)
            .ToArrayAsync(cancellationToken);
        var accessibleSet = accessibleIds.ToHashSet();
        var lineage = await SourceFrameworkStore.GetLineageForSourcesAsync(
            dbContext,
            accessibleIds,
            includeVoided: false,
            cancellationToken);
        return lineage
            .Where(value => accessibleSet.Contains(value.FromSourceEntityId)
                && accessibleSet.Contains(value.ToSourceEntityId))
            .Select(ToView)
            .ToArray();
    }

    private IQueryable<SourceEntity> AccessibleSources(string userId) =>
        dbContext.SourceEntities
            .AsNoTracking()
            .Include(value => value.Revisions)
            .Include(value => value.SourceEdition)
                .ThenInclude(value => value.SourceWork)
                .ThenInclude(value => value.SourcePackage)
                .ThenInclude(value => value.UserGrants)
            .Where(value =>
                value.Revisions.Any()
                && (value.SourceEdition.SourceWork.SourcePackage.IsPublic
                    || value.SourceEdition.SourceWork.SourcePackage.UserGrants
                        .Any(grant => grant.UserId == userId)));

    private async Task<SourceVersionEntityView> ToEntityViewAsync(
        SourceEntity entity,
        IReadOnlyList<RuleConceptReferenceView> concepts,
        CancellationToken cancellationToken)
    {
        var latest = Latest(entity);
        var edition = entity.SourceEdition;
        var work = edition.SourceWork;
        var package = work.SourcePackage;
        var metadata = await SourceFrameworkStore.GetEditionMetadataAsync(
            dbContext,
            edition.Id,
            cancellationToken);
        return new SourceVersionEntityView(
            entity.Id,
            entity.EntityType,
            entity.Name,
            entity.SourceCode,
            latest.Id,
            latest.RevisionNumber,
            latest.Fingerprint,
            package.Key,
            package.DisplayName,
            work.Key,
            work.DisplayName,
            edition.Key,
            edition.DisplayName,
            metadata?.GameEdition,
            metadata?.ReleaseKind,
            metadata?.PublicationDate,
            concepts);
    }

    private static int ScoreCandidate(
        SourceEntity source,
        SourceEntityRevision sourceRevision,
        IReadOnlyList<RuleConceptReferenceView> sourceConcepts,
        StoredSourceEditionMetadata? sourceMetadata,
        SourceEntity candidate,
        SourceEntityRevision candidateRevision,
        IReadOnlyList<RuleConceptReferenceView> candidateConcepts,
        StoredSourceEditionMetadata? candidateMetadata,
        IReadOnlyList<StoredSourceLineage> lineage,
        List<string> reasons)
    {
        var directLineage = lineage.Any(value =>
            (value.FromSourceEntityId == source.Id && value.ToSourceEntityId == candidate.Id)
            || (value.FromSourceEntityId == candidate.Id && value.ToSourceEntityId == source.Id));
        if (directLineage)
        {
            reasons.Add("Confirmed source lineage connects these implementations.");
        }

        var sourceConceptIds = sourceConcepts.Select(value => value.Id).ToHashSet();
        var sameConcept = candidateConcepts.Any(value => sourceConceptIds.Contains(value.Id));
        if (sameConcept)
        {
            reasons.Add("Both implementations are already bound to the same rule concept.");
        }

        var score = directLineage || sameConcept ? 100 : 5;
        var sourceName = NormalizeComparable(source.Name);
        var candidateName = NormalizeComparable(candidate.Name);
        if (string.Equals(sourceName, candidateName, StringComparison.Ordinal))
        {
            score += 55;
            reasons.Add("Names match after normalization.");
        }
        else
        {
            var nameSimilarity = Jaccard(Tokenize(source.Name), Tokenize(candidate.Name));
            if (nameSimilarity >= 0.25)
            {
                score += (int)Math.Round(nameSimilarity * 35d);
                reasons.Add($"Name-token similarity {nameSimilarity:P0}.");
            }
        }

        if (ContainsExplicitReference(sourceRevision.RawJson, candidate.Name, candidate.SourceCode)
            || ContainsExplicitReference(candidateRevision.RawJson, source.Name, source.SourceCode))
        {
            score += 65;
            reasons.Add("Explicit source metadata references the other implementation.");
        }

        var shapeSimilarity = Jaccard(
            ExtractShape(sourceRevision.RawJson),
            ExtractShape(candidateRevision.RawJson));
        if (shapeSimilarity >= 0.4)
        {
            score += (int)Math.Round(shapeSimilarity * 10d);
            reasons.Add($"Document-shape similarity {shapeSimilarity:P0}.");
        }

        var contentSimilarity = Jaccard(
            ExtractContentTokens(sourceRevision.RawJson),
            ExtractContentTokens(candidateRevision.RawJson));
        if (contentSimilarity >= 0.15)
        {
            score += (int)Math.Round(contentSimilarity * 25d);
            reasons.Add($"Source-text token similarity {contentSimilarity:P0}.");
        }

        if (sourceMetadata?.GameEdition is not null
            && candidateMetadata?.GameEdition is not null
            && !string.Equals(sourceMetadata.GameEdition, candidateMetadata.GameEdition, StringComparison.Ordinal))
        {
            reasons.Add($"Cross-edition candidate: {sourceMetadata.GameEdition} -> {candidateMetadata.GameEdition}.");
        }

        return Math.Min(100, score);
    }

    private static bool ContainsExplicitReference(string rawJson, string targetName, string targetSourceCode)
    {
        using var document = JsonDocument.Parse(rawJson);
        var values = new List<string>();
        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (ReferenceProperties.Contains(property.Name))
                {
                    CollectStrings(property.Value, values);
                }
            }
        }

        var normalizedTarget = NormalizeComparable(targetName);
        var normalizedSource = NormalizeComparable(targetSourceCode);
        return values.Any(value =>
        {
            var normalized = NormalizeComparable(value);
            if (!normalized.Contains(normalizedTarget, StringComparison.Ordinal))
            {
                return false;
            }
            return normalizedSource.Length == 0
                || normalized.Contains(normalizedSource, StringComparison.Ordinal)
                || normalizedTarget.Length >= 6;
        });
    }

    private static void CollectStrings(JsonElement element, ICollection<string> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                values.Add(element.GetString() ?? string.Empty);
                break;
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                {
                    CollectStrings(child, values);
                }
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    CollectStrings(property.Value, values);
                }
                break;
        }
    }

    private static HashSet<string> ExtractShape(string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        return document.RootElement.ValueKind != JsonValueKind.Object
            ? []
            : document.RootElement.EnumerateObject()
                .Select(value => value.Name.ToLowerInvariant())
                .Where(value => value is not "name" and not "source" and not "page")
                .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> ExtractContentTokens(string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        CollectContentTokens(document.RootElement, tokens, propertyName: null);
        return tokens;
    }

    private static void CollectContentTokens(
        JsonElement element,
        ISet<string> tokens,
        string? propertyName)
    {
        if (propertyName is not null
            && (propertyName.Equals("name", StringComparison.OrdinalIgnoreCase)
                || propertyName.Equals("source", StringComparison.OrdinalIgnoreCase)
                || propertyName.Equals("page", StringComparison.OrdinalIgnoreCase)
                || ReferenceProperties.Contains(propertyName)))
        {
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                foreach (var token in Tokenize(element.GetString() ?? string.Empty))
                {
                    if (token.Length >= 4 && !ContentStopWords.Contains(token))
                    {
                        tokens.Add(token);
                    }
                }
                break;
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                {
                    CollectContentTokens(child, tokens, propertyName);
                }
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    CollectContentTokens(property.Value, tokens, property.Name);
                }
                break;
        }
    }

    private static HashSet<string> Tokenize(string value)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var builder = new StringBuilder();
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0)
            {
                tokens.Add(builder.ToString());
                builder.Clear();
            }
        }
        if (builder.Length > 0)
        {
            tokens.Add(builder.ToString());
        }
        return tokens;
    }

    private static string NormalizeComparable(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }
        return builder.ToString();
    }

    private static double Jaccard(IReadOnlySet<string> left, IReadOnlySet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }
        var intersection = left.Count(right.Contains);
        var union = left.Count + right.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static SourceEntityRevision Latest(SourceEntity entity) =>
        entity.Revisions.OrderByDescending(value => value.RevisionNumber).First();

    private static RuleConceptSourceBindingView ToView(RulesCore.Domain.Rules.RuleConceptSourceBinding binding) =>
        new(binding.Id, binding.RuleConceptId, binding.SourceEntityId, binding.CreatedByUserId, binding.CreatedAt);

    private static SourceLineageView ToView(StoredSourceLineage lineage) =>
        new(
            lineage.Id,
            lineage.FromSourceEntityId,
            lineage.ToSourceEntityId,
            lineage.RelationshipKind,
            lineage.Note,
            lineage.CreatedByUserId,
            lineage.CreatedAt,
            lineage.VoidedAt is not null,
            lineage.VoidReason,
            lineage.VoidedByUserId,
            lineage.VoidedAt);

    private static string RequireLineageKind(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Lineage relationship kind can not be blank.", nameof(value));
        }
        var normalized = value.Trim().ToLowerInvariant();
        if (!SourceLineageKinds.All.Contains(normalized))
        {
            throw new ArgumentException(
                $"Unsupported lineage relationship kind '{value}'.",
                nameof(value));
        }
        return normalized;
    }

    private static string RequireUserId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(value));
        }
        var normalized = value.Trim();
        if (normalized.Length > 200)
        {
            throw new ArgumentException("User ID can not exceed 200 characters.", nameof(value));
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

    private static void RequireGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Value can not be an empty GUID.", parameterName);
        }
    }
}

public sealed class RuleConsolidationService(
    RulesCoreDbContext dbContext,
    IGlobalRulesAuthoringService authoring,
    ISourceVersioningService versioning)
    : IRuleConsolidationService
{
    public async Task<RuleConsolidationView?> GetAsync(
        Guid ruleConceptId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (ruleConceptId == Guid.Empty)
        {
            throw new ArgumentException("Rule concept ID can not be empty.", nameof(ruleConceptId));
        }
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(userId));
        }
        var actor = userId.Trim();
        await SourceFrameworkStore.EnsureSchemaAsync(dbContext, cancellationToken);

        var detail = await authoring.GetConceptAsync(ruleConceptId, actor, cancellationToken);
        if (detail is null)
        {
            return null;
        }

        var sources = await dbContext.SourceEntities
            .AsNoTracking()
            .Include(value => value.Revisions)
            .Include(value => value.SourceEdition)
                .ThenInclude(value => value.SourceWork)
                .ThenInclude(value => value.SourcePackage)
                .ThenInclude(value => value.UserGrants)
            .Where(value =>
                dbContext.RuleConceptSourceBindings.Any(binding =>
                    binding.RuleConceptId == ruleConceptId && binding.SourceEntityId == value.Id)
                && (value.SourceEdition.SourceWork.SourcePackage.IsPublic
                    || value.SourceEdition.SourceWork.SourcePackage.UserGrants
                        .Any(grant => grant.UserId == actor)))
            .OrderBy(value => value.Name)
            .ThenBy(value => value.SourceCode)
            .ToArrayAsync(cancellationToken);

        var sourceViews = new List<RuleConsolidationSourceView>(sources.Length);
        var revisionLookup = new Dictionary<Guid, (SourceEntity Source, SourceEntityRevision Revision, StoredSourceEditionMetadata? Metadata)>();
        foreach (var source in sources)
        {
            var edition = source.SourceEdition;
            var work = edition.SourceWork;
            var package = work.SourcePackage;
            var metadata = await SourceFrameworkStore.GetEditionMetadataAsync(
                dbContext,
                edition.Id,
                cancellationToken);
            var revisions = new List<RuleConsolidationSourceRevisionView>();
            foreach (var revision in source.Revisions.OrderByDescending(value => value.RevisionNumber))
            {
                using var document = JsonDocument.Parse(revision.RawJson);
                revisions.Add(new RuleConsolidationSourceRevisionView(
                    revision.Id,
                    revision.RevisionNumber,
                    revision.Fingerprint,
                    revision.ImportedAt,
                    document.RootElement.Clone()));
                revisionLookup[revision.Id] = (source, revision, metadata);
            }

            sourceViews.Add(new RuleConsolidationSourceView(
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
                metadata?.GameEdition,
                metadata?.ReleaseKind,
                metadata?.PublicationDate,
                revisions));
        }

        var sourceIds = sources.Select(value => value.Id).ToArray();
        var lineage = await versioning.GetActiveLineageForSourcesAsync(
            sourceIds,
            actor,
            cancellationToken);

        var contributions = new List<RuleConsolidationContributionView>();
        if (detail.LatestDecision is not null)
        {
            var stored = await SourceFrameworkStore.GetDecisionContributionsAsync(
                dbContext,
                detail.LatestDecision.Id,
                cancellationToken);
            foreach (var contribution in stored)
            {
                if (!revisionLookup.TryGetValue(contribution.SourceEntityRevisionId, out var context))
                {
                    continue;
                }
                contributions.Add(new RuleConsolidationContributionView(
                    context.Revision.Id,
                    context.Revision.RevisionNumber,
                    context.Revision.Fingerprint,
                    context.Source.Id,
                    context.Source.Name,
                    context.Source.SourceCode,
                    context.Metadata?.GameEdition,
                    contribution.ContributionKind,
                    contribution.Note));
            }
        }

        return new RuleConsolidationView(
            detail.Concept,
            sourceViews,
            detail.RestrictedBindingCount,
            lineage,
            detail.LatestDecision,
            contributions);
    }
}
