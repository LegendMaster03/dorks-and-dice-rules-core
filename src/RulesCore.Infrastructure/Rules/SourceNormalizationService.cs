using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

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

        await GlobalSourceDispositionService.EnsureSchemaAsync(dbContext, cancellationToken);
        await CanonicalRuleBindingStore.EnsureSchemaAsync(dbContext, cancellationToken);
        var ignoredPackageIds = await GetIgnoredPackageIdsAsync(cancellationToken);
        var alreadyBoundSourceIds = await CanonicalRuleBindingStore.GetSourceEntityIdsWithAnyRuleBindingAsync(
            dbContext,
            cancellationToken);
        var normalizedEntityType = NormalizeOptional(entityType)?.ToLowerInvariant();
        var normalizedQuery = NormalizeOptional(query)?.ToLowerInvariant();

        var sourceQuery = dbContext.SourceEntities
            .AsNoTracking()
            .Where(value => value.Revisions.Any())
            .Where(value => !ignoredPackageIds.Contains(value.SourcePackageId))
            .Where(value => !alreadyBoundSourceIds.Contains(value.Id))
            .Where(value => value.SourcePackage.IsPublic
                || value.SourcePackage.UserGrants
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
                || (value.SourceCode != null && value.SourceCode.ToLower().Contains(normalizedQuery))
                || value.FormatKey.ToLower().Contains(normalizedQuery)
                || value.SourcePackage.DisplayName.ToLower().Contains(normalizedQuery));
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
                value.SourceCode ?? string.Empty,
                value.FormatKey,
                value.Revisions
                    .OrderByDescending(revision => revision.RevisionNumber)
                    .Select(revision => revision.ContentJson)
                    .First(),
                value.Revisions
                    .OrderByDescending(revision => revision.RevisionNumber)
                    .Select(revision => revision.RevisionNumber)
                    .First(),
                value.Revisions
                    .OrderByDescending(revision => revision.RevisionNumber)
                    .Select(revision => revision.ImportedAt)
                    .First(),
                value.SourcePackage.Id,
                value.SourcePackage.Key,
                value.SourcePackage.DisplayName,
                value.SourcePackage.Key,
                value.SourcePackage.DisplayName,
                value.FormatKey,
                value.FormatKey))
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        if (sources.Length == 0)
        {
            return [];
        }

        var suggestions = sources
            .Select(source =>
            {
                var identity = ResolveSuggestedConceptIdentity(
                    source.FormatKey,
                    source.EntityType,
                    source.Name,
                    source.LatestContentJson);
                return new
                {
                    Source = source,
                    Identity = identity,
                    Key = BuildSuggestedConceptKey(identity.EntityType, identity.Name)
                };
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
            var normalizedSourceType = NormalizeEntityType(value.Identity.EntityType);
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
                value.Source.PackageId,
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

        await GlobalSourceDispositionService.EnsureSchemaAsync(dbContext, cancellationToken);
        await CanonicalRuleBindingStore.EnsureSchemaAsync(dbContext, cancellationToken);
        var ignoredPackageIds = await GetIgnoredPackageIdsAsync(cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var source = await dbContext.SourceEntities
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.Id == sourceEntityId
                    && !ignoredPackageIds.Contains(value.SourcePackageId)
                    && (value.SourcePackage.IsPublic
                        || value.SourcePackage.UserGrants
                            .Any(grant => grant.UserId == normalizedUserId)),
                cancellationToken);
        if (source is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var latestContentJson = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => value.SourceEntityId == sourceEntityId)
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => value.ContentJson)
            .FirstAsync(cancellationToken);
        var identity = ResolveSuggestedConceptIdentity(
            source.FormatKey,
            source.EntityType,
            source.Name,
            latestContentJson);
        var canonicalEntityId = await CanonicalRuleBindingStore.GetCanonicalEntityIdAsync(
            dbContext,
            sourceEntityId,
            cancellationToken);
        var suggestedKey = BuildSuggestedConceptKey(identity.EntityType, identity.Name);
        var normalizedEntityType = NormalizeEntityType(identity.EntityType);
        var existingBindings = await dbContext.RuleConceptSourceBindings
            .AsNoTracking()
            .Include(value => value.RuleConcept)
            .Where(value => value.CanonicalEntityId == canonicalEntityId)
            .ToArrayAsync(cancellationToken);

        if (existingBindings.Length > 0)
        {
            var matching = existingBindings.SingleOrDefault(
                value => string.Equals(value.RuleConcept.Key, suggestedKey, StringComparison.Ordinal));
            if (matching is null)
            {
                throw new InvalidOperationException(
                    "This canonical source entity is already bound to a different rule concept. Review it manually instead of accepting the automatic suggestion.");
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
                DisplayName = identity.Name.Trim(),
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
            CanonicalEntityId = canonicalEntityId,
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

    private static SuggestedConceptIdentity ResolveSuggestedConceptIdentity(
        string formatKey,
        string sourceEntityType,
        string sourceName,
        string? contentJson)
    {
        var fallback = new SuggestedConceptIdentity(sourceEntityType, sourceName);
        if (!string.Equals(formatKey, PcGenSourceFormatAdapter.Format, StringComparison.Ordinal)
            || !string.Equals(sourceEntityType, "skill", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(contentJson))
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(contentJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("_rulesCore", out var extension)
                || extension.ValueKind != JsonValueKind.Object
                || !extension.TryGetProperty("competencyConversion", out var conversion)
                || conversion.ValueKind != JsonValueKind.Object)
            {
                return fallback;
            }

            var relationship = ReadString(conversion, "relationship");
            var sourceType = ReadString(conversion, "sourceType");
            var conversionSourceName = ReadString(conversion, "sourceName");
            var targetType = ReadString(conversion, "targetType");
            var targetName = ReadString(conversion, "targetName");
            var scope = ReadString(conversion, "scope");
            if ((relationship is not "direct-equivalence" and not "direct-cross-type")
                || !string.Equals(sourceType, "skill", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(conversionSourceName, sourceName, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(targetType)
                || string.IsNullOrWhiteSpace(targetName)
                || !string.IsNullOrWhiteSpace(scope))
            {
                return fallback;
            }

            return new SuggestedConceptIdentity(targetType.Trim(), targetName.Trim());
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static string? ReadString(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private async Task<Guid[]> GetIgnoredPackageIdsAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT source_package_id
                FROM global_source_disposition
                WHERE restored_at IS NULL;
                """;
            var ids = new List<Guid>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }
            return ids.ToArray();
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
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
            binding.CanonicalEntityId,
            binding.CreatedByUserId,
            binding.CreatedAt,
            binding.SourceEntityId);

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

    private sealed record SuggestedConceptIdentity(string EntityType, string Name);

    private sealed record CandidateSource(
        Guid Id,
        string EntityType,
        string Name,
        string SourceCode,
        string FormatKey,
        string? LatestContentJson,
        int LatestRevisionNumber,
        DateTimeOffset LatestImportedAt,
        Guid PackageId,
        string PackageKey,
        string PackageDisplayName,
        string WorkKey,
        string WorkDisplayName,
        string EditionKey,
        string EditionDisplayName);
}
