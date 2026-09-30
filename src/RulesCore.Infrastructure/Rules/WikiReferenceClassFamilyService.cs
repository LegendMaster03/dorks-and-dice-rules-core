using System.Data;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Projects Class/Subclass relationships into Rules Wiki reference identities without requiring
/// a RuleConcept. Rules Layer relationships remain available, but source-only references use
/// source-adapter native identity as the authoritative relationship boundary.
/// </summary>
public sealed class WikiReferenceClassFamilyService(RulesCoreDbContext dbContext)
{
    public Task<WikiReferenceClassFamilyView?> GetGlobalAsync(
        string? userId,
        string referenceIdentity,
        CancellationToken cancellationToken = default) =>
        GetAsync(
            scope: "global",
            campaignId: null,
            NormalizeOptionalUserId(userId),
            referenceIdentity,
            cancellationToken);

    public Task<WikiReferenceClassFamilyView?> GetCampaignAsync(
        Guid campaignId,
        string userId,
        string referenceIdentity,
        CancellationToken cancellationToken = default)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("Campaign ID can not be empty.", nameof(campaignId));
        }
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(userId));
        }
        return GetAsync(
            "campaign",
            campaignId,
            userId.Trim(),
            referenceIdentity,
            cancellationToken);
    }

    private async Task<WikiReferenceClassFamilyView?> GetAsync(
        string scope,
        Guid? campaignId,
        string? userId,
        string referenceIdentity,
        CancellationToken cancellationToken)
    {
        var catalog = new WikiReferenceCatalogService(dbContext);
        var detail = await GetDetailAsync(
            catalog,
            scope,
            campaignId,
            userId,
            referenceIdentity,
            cancellationToken);
        if (detail is null)
        {
            return null;
        }

        var type = RuleConceptEntityTypes.Normalize(detail.Reference.EffectiveCategory);
        if (!string.Equals(type, RuleConceptEntityTypes.Class, StringComparison.Ordinal)
            && !string.Equals(type, RuleConceptEntityTypes.Subclass, StringComparison.Ordinal))
        {
            return new WikiReferenceClassFamilyView(
                scope,
                campaignId,
                detail.Reference.ReferenceIdentity,
                [],
                []);
        }

        var parentAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parentDetails = new List<WikiReferenceDetailView>();

        if (string.Equals(type, RuleConceptEntityTypes.Subclass, StringComparison.Ordinal))
        {
            foreach (var relationship in detail.Reference.Relationships.Where(value =>
                         string.Equals(value.Kind, RuleConceptRelationshipKinds.ParentClass, StringComparison.OrdinalIgnoreCase)
                         && string.Equals(
                             RuleConceptEntityTypes.Normalize(value.RelatedEntityType),
                             RuleConceptEntityTypes.Class,
                             StringComparison.Ordinal)))
            {
                if (!string.IsNullOrWhiteSpace(relationship.RelatedConceptKey))
                {
                    parentAliases.Add(relationship.RelatedConceptKey);
                }
            }

            foreach (var alias in await FindSourceParentAliasesAsync(detail, userId, cancellationToken))
            {
                parentAliases.Add(alias);
            }

            parentDetails.AddRange(await ResolveDetailsAsync(
                catalog,
                scope,
                campaignId,
                userId,
                parentAliases,
                cancellationToken));
        }

        var childAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.Equals(type, RuleConceptEntityTypes.Class, StringComparison.Ordinal))
        {
            foreach (var alias in await FindChildAliasesAsync(detail, userId, cancellationToken))
            {
                childAliases.Add(alias);
            }
        }
        else
        {
            foreach (var parentDetail in parentDetails)
            {
                foreach (var alias in await FindChildAliasesAsync(parentDetail, userId, cancellationToken))
                {
                    childAliases.Add(alias);
                }
            }
        }

        var childDetails = await ResolveDetailsAsync(
            catalog,
            scope,
            campaignId,
            userId,
            childAliases,
            cancellationToken);

        return new WikiReferenceClassFamilyView(
            scope,
            campaignId,
            detail.Reference.ReferenceIdentity,
            ToRelatedReferences(parentDetails),
            ToRelatedReferences(childDetails));
    }

    private async Task<IReadOnlyList<string>> FindSourceParentAliasesAsync(
        WikiReferenceDetailView subclassDetail,
        string? userId,
        CancellationToken cancellationToken)
    {
        var subclassSources = await ReadAccessibleSourceIdentitiesAsync(
            subclassDetail.Variations.Select(value => value.SourceEntityId),
            userId,
            cancellationToken);
        var parentNativeKeys = subclassSources
            .Where(value => string.Equals(value.FormatKey, FiveEToolsSourceFormatAdapter.Format, StringComparison.Ordinal))
            .Where(value => string.Equals(
                RuleConceptEntityTypes.Normalize(value.EntityType),
                RuleConceptEntityTypes.Subclass,
                StringComparison.Ordinal))
            .Select(value => FiveEToolsNativeIdentity.TryCreateSubclassParentClassNativeKey(
                value.NativeIdentityJson,
                out var parentNativeKey)
                    ? parentNativeKey
                    : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (parentNativeKeys.Length == 0)
        {
            return [];
        }

        var parentIds = await AccessibleSourceQuery(userId)
            .Where(value => value.FormatKey == FiveEToolsSourceFormatAdapter.Format)
            .Where(value => value.EntityType.ToLower() == "class")
            .Where(value => parentNativeKeys.Contains(value.NativeKey))
            .Select(value => value.Id)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        return await ReadReferenceAliasesAsync(parentIds, cancellationToken);
    }

    private async Task<IReadOnlyList<string>> FindChildAliasesAsync(
        WikiReferenceDetailView classDetail,
        string? userId,
        CancellationToken cancellationToken)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var classSources = await ReadAccessibleSourceIdentitiesAsync(
            classDetail.Variations.Select(value => value.SourceEntityId),
            userId,
            cancellationToken);
        var parentNativeKeys = classSources
            .Where(value => string.Equals(value.FormatKey, FiveEToolsSourceFormatAdapter.Format, StringComparison.Ordinal))
            .Where(value => string.Equals(
                RuleConceptEntityTypes.Normalize(value.EntityType),
                RuleConceptEntityTypes.Class,
                StringComparison.Ordinal))
            .Select(value => value.NativeKey)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        if (parentNativeKeys.Count > 0)
        {
            // The scan is intentionally over source identity rows inside Core, not over the Wiki
            // reference catalog. Each candidate is matched by the exact source-adapter parent
            // native key, never by display name or a consumer-side class-name heuristic.
            var subclassSources = await AccessibleSourceQuery(userId)
                .Where(value => value.FormatKey == FiveEToolsSourceFormatAdapter.Format)
                .Where(value => value.EntityType.ToLower() == "subclass")
                .Select(value => new SourceIdentityRow(
                    value.Id,
                    value.FormatKey,
                    value.EntityType,
                    value.NativeKey,
                    value.NativeIdentityJson))
                .ToArrayAsync(cancellationToken);
            var childIds = subclassSources
                .Where(value => FiveEToolsNativeIdentity.TryCreateSubclassParentClassNativeKey(
                    value.NativeIdentityJson,
                    out var parentNativeKey)
                    && parentNativeKeys.Contains(parentNativeKey))
                .Select(value => value.SourceEntityId)
                .Distinct()
                .ToArray();
            foreach (var alias in await ReadReferenceAliasesAsync(childIds, cancellationToken))
            {
                aliases.Add(alias);
            }
        }

        if (classDetail.Reference.RuleConceptId is Guid parentConceptId)
        {
            foreach (var alias in await ReadRuleConceptChildAliasesAsync(parentConceptId, cancellationToken))
            {
                aliases.Add(alias);
            }
        }

        return aliases.ToArray();
    }

    private async Task<SourceIdentityRow[]> ReadAccessibleSourceIdentitiesAsync(
        IEnumerable<Guid> sourceEntityIds,
        string? userId,
        CancellationToken cancellationToken)
    {
        var ids = sourceEntityIds
            .Where(value => value != Guid.Empty)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        return await AccessibleSourceQuery(userId)
            .Where(value => ids.Contains(value.Id))
            .Select(value => new SourceIdentityRow(
                value.Id,
                value.FormatKey,
                value.EntityType,
                value.NativeKey,
                value.NativeIdentityJson))
            .ToArrayAsync(cancellationToken);
    }

    private IQueryable<RulesCore.Domain.Sources.SourceEntity> AccessibleSourceQuery(string? userId)
    {
        var normalizedUserId = NormalizeOptionalUserId(userId);
        return dbContext.SourceEntities
            .AsNoTracking()
            .Where(value => value.SourcePackage.IsPublic
                || (normalizedUserId != null
                    && value.SourcePackage.UserGrants.Any(grant => grant.UserId == normalizedUserId)));
    }

    private async Task<IReadOnlyList<string>> ReadReferenceAliasesAsync(
        IReadOnlyCollection<Guid> sourceEntityIds,
        CancellationToken cancellationToken)
    {
        var ids = sourceEntityIds
            .Where(value => value != Guid.Empty)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

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
                SELECT DISTINCT ON (revision.source_entity_id)
                       revision.source_entity_id,
                       occurrence.canonical_entity_id,
                       occurrence.canonical_source_occurrence_id
                FROM source_entity_revision revision
                JOIN source_entity_occurrence_binding binding
                  ON binding.source_entity_revision_id = revision.source_entity_revision_id
                JOIN canonical_source_occurrence occurrence
                  ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                WHERE revision.source_entity_id = ANY(@source_ids)
                ORDER BY revision.source_entity_id, revision.revision_number DESC;
                """;
            AddParameter(command, "@source_ids", ids);
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                aliases.Add(reader.IsDBNull(1)
                    ? $"occurrence:{reader.GetGuid(2):N}"
                    : $"canonical:{reader.GetGuid(1):N}");
            }
            return aliases.ToArray();
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<IReadOnlyList<string>> ReadRuleConceptChildAliasesAsync(
        Guid parentRuleConceptId,
        CancellationToken cancellationToken)
    {
        await RuleConceptRelationshipStore.EnsureSchemaAsync(dbContext, cancellationToken);
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
                SELECT child.concept_key
                FROM rule_concept_relationship relationship
                JOIN rule_concept child
                  ON child.rule_concept_id = relationship.from_rule_concept_id
                WHERE relationship.to_rule_concept_id = @parent_id
                  AND relationship.relationship_kind = @kind
                ORDER BY child.concept_key;
                """;
            AddParameter(command, "@parent_id", parentRuleConceptId);
            AddParameter(command, "@kind", RuleConceptRelationshipKinds.ParentClass);
            var aliases = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                aliases.Add(reader.GetString(0));
            }
            return aliases;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<IReadOnlyList<WikiReferenceDetailView>> ResolveDetailsAsync(
        WikiReferenceCatalogService catalog,
        string scope,
        Guid? campaignId,
        string? userId,
        IEnumerable<string> aliases,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, WikiReferenceDetailView>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in aliases
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var detail = await GetDetailAsync(
                catalog,
                scope,
                campaignId,
                userId,
                alias,
                cancellationToken);
            if (detail is not null)
            {
                results.TryAdd(detail.Reference.ReferenceIdentity, detail);
            }
        }
        return results.Values
            .OrderBy(value => value.Reference.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Reference.ReferenceIdentity, StringComparer.Ordinal)
            .ToArray();
    }

    private static Task<WikiReferenceDetailView?> GetDetailAsync(
        WikiReferenceCatalogService catalog,
        string scope,
        Guid? campaignId,
        string? userId,
        string referenceIdentity,
        CancellationToken cancellationToken) =>
        scope == "campaign" && campaignId.HasValue
            ? catalog.GetCampaignDetailAsync(
                campaignId.Value,
                userId ?? throw new InvalidOperationException("Campaign reference lookup requires a user ID."),
                referenceIdentity,
                cancellationToken)
            : catalog.GetGlobalDetailAsync(userId, referenceIdentity, cancellationToken);

    private static IReadOnlyList<WikiReferenceRelatedReferenceView> ToRelatedReferences(
        IEnumerable<WikiReferenceDetailView> details) =>
        details
            .Select(value => value.Reference)
            .DistinctBy(value => value.ReferenceIdentity, StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.ReferenceIdentity, StringComparer.Ordinal)
            .Select(value => new WikiReferenceRelatedReferenceView(
                value.ReferenceIdentity,
                value.RuleConceptId,
                value.ConceptKey,
                value.DisplayName,
                value.EffectiveCategory,
                value.BrowserLink))
            .ToArray();

    private static string? NormalizeOptionalUserId(string? userId) =>
        string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record SourceIdentityRow(
        Guid SourceEntityId,
        string FormatKey,
        string EntityType,
        string NativeKey,
        string NativeIdentityJson);
}
