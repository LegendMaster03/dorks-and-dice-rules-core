using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

public sealed class SourceNormalizationService(RulesCoreDbContext dbContext)
    : ISourceNormalizationService
{
    public Task<IReadOnlyList<SourceNormalizationCandidateView>> GetCandidatesAsync(
        string userId,
        string? entityType = null,
        string? query = null,
        int limit = 100,
        CancellationToken cancellationToken = default) =>
        GetCandidatesPageAsync(userId, entityType, query, limit, 0, cancellationToken);

    public async Task<IReadOnlyList<SourceNormalizationCandidateView>> GetCandidatesPageAsync(
        string userId,
        string? entityType = null,
        string? query = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = RequireUserId(userId);
        if (limit is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 200.");
        }
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset can not be negative.");
        }

        var normalizedEntityType = NormalizeOptional(entityType)?.ToLowerInvariant();
        var normalizedQuery = NormalizeOptional(query)?.ToLowerInvariant();

        var sourceQuery = dbContext.SourceEntities
            .AsNoTracking()
            .Where(value => value.Revisions.Any())
            .Where(value => !dbContext.RuleConceptSourceBindings
                .Any(binding => binding.SourceEntityId == value.Id))
            .Where(value => value.SourceEdition.SourceWork.SourcePackage.IsPublic
                || value.SourceEdition.SourceWork.SourcePackage.UserGrants
                    .Any(grant => grant.UserId == normalizedUserId));

        if (normalizedEntityType is not null)
        {
            sourceQuery = sourceQuery.Where(
                value => value.EntityType.ToLower() == normalizedEntityType);
        }

        if (normalizedQuery is not null)
        {
            sourceQuery = sourceQuery.Where(value =>
                value.Name.ToLower().Contains(normalizedQuery)
                || value.SourceCode.ToLower().Contains(normalizedQuery)
                || value.SourceEdition.DisplayName.ToLower().Contains(normalizedQuery)
                || value.SourceEdition.SourceWork.DisplayName.ToLower().Contains(normalizedQuery)
                || value.SourceEdition.SourceWork.SourcePackage.DisplayName.ToLower().Contains(normalizedQuery));
        }

        var sources = await sourceQuery
            .OrderBy(value => value.EntityType)
            .ThenBy(value => value.Name)
            .ThenBy(value => value.SourceCode)
            .ThenBy(value => value.Id)
            .Skip(offset)
            .Select(value => new CandidateSource(
                value.Id,
                value.EntityType,
                value.Name,
                value.SourceCode,
                value.Revisions
                    .OrderByDescending(revision => revision.RevisionNumber)
                    .Select(revision => revision.RevisionNumber)
                    .First(),
                value.Revisions
                    .OrderByDescending(revision => revision.RevisionNumber)
                    .Select(revision => revision.ImportedAt)
                    .First(),
                value.SourceEdition.SourceWork.SourcePackage.Key,
                value.SourceEdition.SourceWork.SourcePackage.DisplayName,
                value.SourceEdition.SourceWork.Key,
                value.SourceEdition.SourceWork.DisplayName,
                value.SourceEdition.Key,
                value.SourceEdition.DisplayName))
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        if (sources.Length == 0)
        {
            return [];
        }

        var suggestions = sources
            .Select(source => new
            {
                Source = source,
                Key = BuildSuggestedConceptKey(source.EntityType, source.Name)
            })
            .ToArray();
        var keys = suggestions.Select(value => value.Key).Distinct().ToArray();
        var concepts = await dbContext.RuleConcepts
            .AsNoTracking()
            .Where(value => keys.Contains(value.Key))
            .ToDictionaryAsync(value => value.Key, StringComparer.Ordinal, cancellationToken);

        return suggestions.Select(value =>
        {
            concepts.TryGetValue(value.Key, out var concept);
            var normalizedSourceType = NormalizeEntityType(value.Source.EntityType);
            var suggestionKind = concept is null
                ? SourceNormalizationSuggestionKinds.NewConcept
                : string.Equals(concept.EntityType, normalizedSourceType, StringComparison.Ordinal)
                    ? SourceNormalizationSuggestionKinds.ExistingConcept
                    : SourceNormalizationSuggestionKinds.Conflict;

            return new SourceNormalizationCandidateView(
                value.Source.Id,
                value.Source.EntityType,
                value.Source.Name,
                value.Source.SourceCode,
                value.Source.LatestRevisionNumber,
                value.Source.LatestImportedAt,
                value.Source.PackageKey,
                value.Source.PackageDisplayName,
                value.Source.WorkKey,
                value.Source.WorkDisplayName,
                value.Source.EditionKey,
                value.Source.EditionDisplayName,
                value.Key,
                concept?.Id,
                concept?.DisplayName,
                suggestionKind);
        }).ToArray();
    }

    public async Task<AcceptedSourceNormalizationView?> AcceptAsync(
        Guid sourceEntityId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        RequireGuid(sourceEntityId, nameof(sourceEntityId));
        var normalizedUserId = RequireUserId(userId);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var source = await dbContext.SourceEntities
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.Id == sourceEntityId
                    && (value.SourceEdition.SourceWork.SourcePackage.IsPublic
                        || value.SourceEdition.SourceWork.SourcePackage.UserGrants
                            .Any(grant => grant.UserId == normalizedUserId)),
                cancellationToken);
        if (source is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var suggestedKey = BuildSuggestedConceptKey(source.EntityType, source.Name);
        var normalizedEntityType = NormalizeEntityType(source.EntityType);
        var existingBindings = await dbContext.RuleConceptSourceBindings
            .AsNoTracking()
            .Include(value => value.RuleConcept)
            .Where(value => value.SourceEntityId == sourceEntityId)
            .ToArrayAsync(cancellationToken);

        if (existingBindings.Length > 0)
        {
            var matching = existingBindings.SingleOrDefault(
                value => string.Equals(value.RuleConcept.Key, suggestedKey, StringComparison.Ordinal));
            if (matching is null)
            {
                throw new InvalidOperationException(
                    "This source entity is already bound to a different rule concept. Review it manually instead of accepting the automatic suggestion.");
            }

            await transaction.CommitAsync(cancellationToken);
            return new AcceptedSourceNormalizationView(
                ToView(matching.RuleConcept),
                ToView(matching),
                CreatedConcept: false,
                CreatedBinding: false);
        }

        var concept = await dbContext.RuleConcepts
            .SingleOrDefaultAsync(value => value.Key == suggestedKey, cancellationToken);
        var createdConcept = false;
        if (concept is null)
        {
            concept = new RuleConcept
            {
                Id = Guid.NewGuid(),
                Key = suggestedKey,
                EntityType = normalizedEntityType,
                DisplayName = source.Name.Trim(),
                CreatedByUserId = normalizedUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.RuleConcepts.Add(concept);
            createdConcept = true;
        }
        else if (!string.Equals(concept.EntityType, normalizedEntityType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Suggested key '{suggestedKey}' already belongs to a concept with entity type '{concept.EntityType}'. Review this source manually.");
        }

        var binding = new RuleConceptSourceBinding
        {
            Id = Guid.NewGuid(),
            RuleConceptId = concept.Id,
            SourceEntityId = source.Id,
            CreatedByUserId = normalizedUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            RuleConcept = concept
        };
        dbContext.RuleConceptSourceBindings.Add(binding);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AcceptedSourceNormalizationView(
            ToView(concept),
            ToView(binding),
            createdConcept,
            CreatedBinding: true);
    }

    internal static string BuildSuggestedConceptKey(string entityType, string name)
    {
        var typeSegment = Slugify(entityType, "entity");
        var availableNameLength = Math.Max(1, 300 - typeSegment.Length - 1);
        var nameSegment = Slugify(name, "item");
        if (nameSegment.Length > availableNameLength)
        {
            nameSegment = nameSegment[..availableNameLength].Trim('-');
        }
        if (nameSegment.Length == 0)
        {
            nameSegment = StableFallback(name, availableNameLength);
        }
        return $"{typeSegment}.{nameSegment}";
    }

    private static string Slugify(string value, string fallbackPrefix)
    {
        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var pendingSeparator = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }
                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else if (builder.Length > 0)
            {
                pendingSeparator = true;
            }
        }

        var result = builder.ToString().Trim('-');
        return result.Length > 0 ? result : StableFallback(value, 32, fallbackPrefix);
    }

    private static string StableFallback(string value, int maxLength, string prefix = "item")
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
        var availableHashLength = Math.Max(1, maxLength - prefix.Length - 1);
        var hashLength = Math.Min(12, availableHashLength);
        var candidate = $"{prefix}-{hash[..hashLength]}";
        return candidate.Length <= maxLength ? candidate : candidate[..maxLength];
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

    private static string NormalizeEntityType(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            throw new InvalidOperationException("A source entity can not have a blank entity type.");
        }
        if (normalized.Length > 120)
        {
            throw new InvalidOperationException("A source entity type can not exceed 120 characters.");
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

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void RequireGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Value can not be empty.", parameterName);
        }
    }

    private sealed record CandidateSource(
        Guid Id,
        string EntityType,
        string Name,
        string SourceCode,
        int LatestRevisionNumber,
        DateTimeOffset LatestImportedAt,
        string PackageKey,
        string PackageDisplayName,
        string WorkKey,
        string WorkDisplayName,
        string EditionKey,
        string EditionDisplayName);
}
