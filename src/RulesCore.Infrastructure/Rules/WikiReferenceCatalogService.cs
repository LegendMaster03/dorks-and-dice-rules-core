using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// First-party Rules Wiki read model. This service projects every source variation the caller
/// may read into stable logical reference concepts without changing the public resolved-rules API.
/// </summary>
public sealed class WikiReferenceCatalogService(RulesCoreDbContext dbContext)
{
    private const int MaximumLimit = 500;
    private const int MaximumEntityTypeLength = 120;
    private const int MaximumQueryLength = 500;
    private const int MaximumFilterLength = 256;
    private const int MaximumReferenceIdentityLength = 512;
    private const int MaximumUserIdLength = 200;

    public Task<WikiReferenceCatalogView> GetGlobalCatalogAsync(
        string? userId,
        string? entityType,
        string? categoryMode,
        string? query,
        string? sourceCode,
        string? packageKey,
        string? edition,
        int limit,
        int offset,
        CancellationToken cancellationToken = default) =>
        GetCatalogAsync(
            scope: "global",
            campaignId: null,
            NormalizeOptionalUserId(userId),
            entityType,
            categoryMode,
            query,
            sourceCode,
            packageKey,
            edition,
            overridesOnly: false,
            limit,
            offset,
            cancellationToken);

    public Task<WikiReferenceCatalogView> GetCampaignCatalogAsync(
        Guid campaignId,
        string userId,
        string? entityType,
        string? categoryMode,
        string? query,
        string? sourceCode,
        string? packageKey,
        string? edition,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        if (campaignId == Guid.Empty) throw new ArgumentException("Campaign ID can not be empty.", nameof(campaignId));
        var normalizedUserId = NormalizeRequiredUserId(userId);
        return GetCatalogAsync(
            "campaign",
            campaignId,
            normalizedUserId,
            entityType,
            categoryMode,
            query,
            sourceCode,
            packageKey,
            edition,
            overridesOnly: false,
            limit,
            offset,
            cancellationToken);
    }

    public Task<WikiReferenceCatalogView> GetCampaignOverridesCatalogAsync(
        Guid campaignId,
        string userId,
        string? entityType,
        string? categoryMode,
        string? query,
        string? sourceCode,
        string? packageKey,
        string? edition,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        if (campaignId == Guid.Empty) throw new ArgumentException("Campaign ID can not be empty.", nameof(campaignId));
        var normalizedUserId = NormalizeRequiredUserId(userId);
        return GetCatalogAsync(
            "campaign",
            campaignId,
            normalizedUserId,
            entityType,
            categoryMode,
            query,
            sourceCode,
            packageKey,
            edition,
            overridesOnly: true,
            limit,
            offset,
            cancellationToken);
    }

    public Task<WikiReferenceDetailView?> GetGlobalDetailAsync(
        string? userId,
        string referenceIdentity,
        CancellationToken cancellationToken = default) =>
        GetDetailAsync(
            "global",
            campaignId: null,
            NormalizeOptionalUserId(userId),
            referenceIdentity,
            cancellationToken);

    public Task<WikiReferenceDetailView?> GetCampaignDetailAsync(
        Guid campaignId,
        string userId,
        string referenceIdentity,
        CancellationToken cancellationToken = default)
    {
        if (campaignId == Guid.Empty) throw new ArgumentException("Campaign ID can not be empty.", nameof(campaignId));
        var normalizedUserId = NormalizeRequiredUserId(userId);
        return GetDetailAsync(
            "campaign",
            campaignId,
            normalizedUserId,
            referenceIdentity,
            cancellationToken);
    }

    public async Task<bool> ReferenceContainsRevisionsAsync(
        string? userId,
        string referenceIdentity,
        Guid leftRevisionId,
        Guid rightRevisionId,
        CancellationToken cancellationToken = default)
    {
        if (leftRevisionId == Guid.Empty || rightRevisionId == Guid.Empty) return false;
        var normalizedReferenceIdentity = NormalizeRequiredReferenceIdentity(referenceIdentity);
        var state = await BuildStateAsync(
            "global",
            campaignId: null,
            NormalizeOptionalUserId(userId),
            cancellationToken);
        var group = FindGroup(state, normalizedReferenceIdentity);
        if (group is null) return false;
        var revisionIds = group.Variations.Select(value => value.SourceEntityRevisionId).ToHashSet();
        return revisionIds.Contains(leftRevisionId) && revisionIds.Contains(rightRevisionId);
    }

    private async Task<WikiReferenceCatalogView> GetCatalogAsync(
        string scope,
        Guid? campaignId,
        string? userId,
        string? entityType,
        string? categoryMode,
        string? query,
        string? sourceCode,
        string? packageKey,
        string? edition,
        bool overridesOnly,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        ValidatePage(limit, offset);
        var normalizedCategoryMode = WikiReferenceCategoryModes.Normalize(categoryMode);
        var normalizedEntityType = NormalizeOptionalCategory(entityType);
        var normalizedQuery = NormalizeOptional(query, nameof(query), MaximumQueryLength)?.ToLowerInvariant();
        var normalizedSource = NormalizeOptional(sourceCode, nameof(sourceCode), MaximumFilterLength);
        var normalizedPackage = NormalizeOptional(packageKey, nameof(packageKey), MaximumFilterLength);
        var normalizedEditionInput = NormalizeOptional(edition, nameof(edition), MaximumFilterLength);
        var normalizedEdition = NormalizeEdition(normalizedEditionInput);

        var state = await BuildStateAsync(scope, campaignId, userId, cancellationToken);
        IEnumerable<ReferenceGroup> filtered = overridesOnly
            ? state.Groups.Where(group => group.Item.HasCampaignOverride)
            : state.Groups;

        if (normalizedQuery is not null)
        {
            filtered = filtered.Where(group => MatchesSearch(group, normalizedQuery));
        }
        if (normalizedSource is not null)
        {
            filtered = filtered.Where(group => group.Variations.Any(value =>
                string.Equals(value.SourceCode, normalizedSource, StringComparison.OrdinalIgnoreCase)));
        }
        if (normalizedPackage is not null)
        {
            filtered = filtered.Where(group => group.Variations.Any(value =>
                string.Equals(value.PackageKey, normalizedPackage, StringComparison.OrdinalIgnoreCase)));
        }
        if (normalizedEdition is not null)
        {
            filtered = filtered.Where(group => group.Variations.Any(value =>
                string.Equals(value.EditionKey, normalizedEdition, StringComparison.OrdinalIgnoreCase)));
        }

        var facetGroups = filtered.ToArray();
        var entityFacets = BuildEntityFacets(facetGroups, normalizedCategoryMode);
        var sourceFacets = BuildFacet(
            facetGroups,
            group => group.Variations.Select(value => (value.SourceCode, value.SourceCode)));
        var packageFacets = BuildFacet(
            facetGroups,
            group => group.Variations.Select(value => (value.PackageKey, value.PackageDisplayName)));
        var editionFacets = BuildFacet(
            facetGroups,
            group => group.Variations
                .Where(value => !string.IsNullOrWhiteSpace(value.EditionKey))
                .Select(value => (value.EditionKey, value.EditionDisplayName)));

        if (normalizedEntityType is not null)
        {
            filtered = facetGroups.Where(group => CategoryMatches(
                group,
                normalizedEntityType,
                normalizedCategoryMode));
        }
        else
        {
            filtered = facetGroups;
        }

        var all = filtered
            .OrderBy(group => group.Item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Item.ReferenceIdentity, StringComparer.Ordinal)
            .ToArray();
        var pageGroups = all.Skip(offset).Take(limit).ToArray();

        // Mechanical documents are intentionally page-scoped. Grouping, authorization, search,
        // totals and facets operate on lightweight metadata; the potentially large content_json/
        // raw_json payloads are loaded only for effective rows returned on this page.
        var documents = await ReadDocumentsAsync(
            pageGroups.Select(group => group.EffectiveVariation.SourceEntityRevisionId).ToArray(),
            cancellationToken);
        var page = pageGroups
            .Select(group => HydrateCatalogItem(group, documents))
            .ToArray();

        return new WikiReferenceCatalogView(
            scope,
            campaignId,
            state.Publication.RevisionNumber,
            state.Publication.PublishedAt,
            all.Length,
            normalizedCategoryMode,
            entityFacets,
            sourceFacets,
            packageFacets,
            editionFacets,
            page);
    }

    private async Task<WikiReferenceDetailView?> GetDetailAsync(
        string scope,
        Guid? campaignId,
        string? userId,
        string referenceIdentity,
        CancellationToken cancellationToken)
    {
        var normalizedReferenceIdentity = NormalizeRequiredReferenceIdentity(referenceIdentity);

        var state = await BuildStateAsync(scope, campaignId, userId, cancellationToken);
        var group = FindGroup(state, normalizedReferenceIdentity);
        if (group is null) return null;

        // Detail/history is where complete mechanical documents are needed. Load only the
        // accessible variations in this logical history rather than the complete corpus.
        var documents = await ReadDocumentsAsync(
            group.Variations.Select(value => value.SourceEntityRevisionId).ToArray(),
            cancellationToken);
        var variations = group.Variations
            .OrderByDescending(value => value.PublicationDate.HasValue)
            .ThenByDescending(value => value.PublicationDate)
            .ThenByDescending(value => EditionRank(value.EditionKey))
            .ThenBy(value => value.PublicationKey, StringComparer.Ordinal)
            .ThenBy(value => value.SourceCode, StringComparer.Ordinal)
            .Select(value => ToDetailVariation(
                value,
                RequireDocument(documents, value.SourceEntityRevisionId),
                value.SourceEntityRevisionId == group.Item.EffectiveVariation.SourceEntityRevisionId))
            .ToArray();

        var effectiveDocument = RequireDocument(
            documents,
            group.EffectiveVariation.SourceEntityRevisionId);
        if (group.Item.ConceptKey is not null
            && group.Item.ResolutionState != WikiReferenceResolutionStates.UnresolvedFallback)
        {
            if (scope == "campaign" && campaignId.HasValue)
            {
                var resolved = await new CampaignRulesService(dbContext).ResolveLatestAsync(
                    campaignId.Value,
                    group.Item.ConceptKey,
                    userId,
                    cancellationToken);
                if (resolved is not null) effectiveDocument = resolved.Document.Clone();
            }
            else
            {
                var resolved = await new GlobalRulesService(dbContext).ResolveLatestAsync(
                    group.Item.ConceptKey,
                    userId,
                    cancellationToken);
                if (resolved is not null) effectiveDocument = resolved.Document.Clone();
            }
        }

        var reference = HydrateCatalogItem(group, documents);
        return new WikiReferenceDetailView(
            scope,
            campaignId,
            reference,
            variations,
            effectiveDocument);
    }

    private async Task<ReferenceState> BuildStateAsync(
        string scope,
        Guid? campaignId,
        string? userId,
        CancellationToken cancellationToken)
    {
        var variations = await ReadAccessibleVariationsAsync(userId, cancellationToken);
        if (variations.Count == 0)
        {
            return new ReferenceState([], new Dictionary<string, ReferenceGroup>(StringComparer.OrdinalIgnoreCase),
                new ScopePublication(null, null, new Dictionary<Guid, EffectiveEntry>()));
        }

        var accessibleCanonicalIds = variations
            .Select(value => value.CanonicalEntityId)
            .Distinct()
            .ToArray();
        var historyEdges = await ReadSameHistoryEdgesAsync(accessibleCanonicalIds, cancellationToken);
        var components = BuildComponents(accessibleCanonicalIds, historyEdges);
        var conceptBindings = await ReadConceptBindingsAsync(accessibleCanonicalIds, cancellationToken);
        var publication = scope == "campaign" && campaignId.HasValue
            ? await ReadCampaignPublicationAsync(campaignId.Value, cancellationToken)
            : await ReadGlobalPublicationAsync(cancellationToken);

        var outgoing = await RuleConceptRelationshipStore.GetOutgoingAsync(
            dbContext,
            conceptBindings.Select(value => value.RuleConceptId).Distinct().ToArray(),
            cancellationToken);

        var groups = new List<ReferenceGroup>();
        var lookup = new Dictionary<string, ReferenceGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in components)
        {
            var componentIds = component.ToHashSet();
            var componentVariations = variations
                .Where(value => componentIds.Contains(value.CanonicalEntityId))
                .ToArray();
            if (componentVariations.Length == 0) continue;

            var bindings = conceptBindings
                .Where(value => componentIds.Contains(value.CanonicalEntityId))
                .GroupBy(value => value.RuleConceptId)
                .Select(group => group
                    .OrderBy(value => value.CreatedAt)
                    .ThenBy(value => value.CanonicalEntityId)
                    .First())
                .ToArray();
            var identityBinding = bindings
                .OrderBy(value => value.CreatedAt)
                .ThenBy(value => value.ConceptKey, StringComparer.Ordinal)
                .ThenBy(value => value.RuleConceptId)
                .FirstOrDefault();
            var accessibleRevisionIds = componentVariations
                .Select(value => value.SourceEntityRevisionId)
                .ToHashSet();
            var effectiveCandidates = bindings
                .Select(binding => publication.Entries.TryGetValue(binding.RuleConceptId, out var entry)
                    ? (Binding: binding, Entry: entry)
                    : (Binding: binding, Entry: (EffectiveEntry?)null))
                .Where(value => value.Entry is not null
                    && accessibleRevisionIds.Contains(value.Entry.SourceEntityRevisionId))
                .Select(value => (value.Binding, Entry: value.Entry!))
                .ToArray();

            VariationRecord effectiveVariation;
            ConceptBinding? preferredBinding;
            string resolutionState;
            bool hasCampaignOverride = false;

            if (effectiveCandidates.Length > 0)
            {
                // A logical history can span mechanically distinct RuleConcepts. Each concept
                // remains type-coherent; the Wiki representative is selected from explicit,
                // published Rules Layer decisions. Campaign overrides outrank inherited entries,
                // then the most recently authored published decision wins deterministically.
                var selected = effectiveCandidates
                    .Select(value => new
                    {
                        value.Binding,
                        value.Entry,
                        Variation = componentVariations.Single(item =>
                            item.SourceEntityRevisionId == value.Entry.SourceEntityRevisionId)
                    })
                    .OrderByDescending(value => value.Entry.HasCampaignOverride)
                    .ThenByDescending(value => value.Entry.DecisionCreatedAt)
                    .ThenByDescending(value => value.Entry.DecisionNumber)
                    .ThenBy(value => value.Entry.DecisionId)
                    .ThenBy(value => value.Binding.ConceptKey, StringComparer.Ordinal)
                    .First();
                effectiveVariation = selected.Variation;
                preferredBinding = selected.Binding;
                hasCampaignOverride = selected.Entry.HasCampaignOverride;
                resolutionState = scope == "campaign"
                    ? selected.Entry.HasCampaignOverride
                        ? WikiReferenceResolutionStates.CampaignOverride
                        : WikiReferenceResolutionStates.Inherited
                    : WikiReferenceResolutionStates.Resolved;
            }
            else
            {
                effectiveVariation = SelectFallback(componentVariations);
                preferredBinding = bindings
                    .Where(value => string.Equals(
                        value.EntityType,
                        effectiveVariation.EntityType,
                        StringComparison.OrdinalIgnoreCase))
                    .OrderBy(value => value.CreatedAt)
                    .ThenBy(value => value.ConceptKey, StringComparer.Ordinal)
                    .FirstOrDefault();
                resolutionState = WikiReferenceResolutionStates.UnresolvedFallback;
            }

            // Logical identity is intentionally independent of which category is effective.
            // The earliest Rules Layer binding is a stable anchor once a logical history has
            // entered Rules Layer; source-only histories retain the existing canonical anchor.
            var referenceIdentity = identityBinding?.ConceptKey
                ?? $"canonical:{component.OrderBy(value => value).First():N}";
            var displayName = preferredBinding?.DisplayName
                ?? identityBinding?.DisplayName
                ?? effectiveVariation.Name;
            var effectiveCategory = effectiveVariation.EntityType;
            var relationships = preferredBinding is not null
                && outgoing.TryGetValue(preferredBinding.RuleConceptId, out var refs)
                ? refs.Select(value => new ResolvedRuleRelationshipView(
                    value.Kind,
                    value.RelatedRuleConceptId,
                    value.RelatedConceptKey,
                    value.RelatedEntityType,
                    value.RelatedDisplayName)).ToArray()
                : [];
            var item = new WikiReferenceItemView(
                referenceIdentity,
                preferredBinding?.RuleConceptId,
                preferredBinding?.ConceptKey,
                displayName,
                effectiveCategory,
                effectiveCategory,
                effectiveVariation.EditionKey,
                effectiveVariation.EditionDisplayName,
                resolutionState,
                hasCampaignOverride,
                ToSummaryVariation(effectiveVariation, isEffective: true),
                BuildCategoryHistory(componentVariations),
                [],
                relationships);
            var group = new ReferenceGroup(item, componentVariations, effectiveVariation);
            groups.Add(group);

            lookup[item.ReferenceIdentity] = group;
            if (item.ConceptKey is not null) lookup[item.ConceptKey] = group;
            foreach (var binding in bindings)
            {
                lookup.TryAdd(binding.ConceptKey, group);
            }
            foreach (var canonicalId in component)
            {
                lookup.TryAdd($"canonical:{canonicalId:N}", group);
            }
        }

        return new ReferenceState(groups, lookup, publication);
    }

    private async Task<IReadOnlyList<VariationRecord>> ReadAccessibleVariationsAsync(
        string? userId,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    occurrence.canonical_entity_id,
                    source.source_entity_id,
                    revision.source_entity_revision_id,
                    revision.revision_number,
                    source.entity_name,
                    source.entity_type,
                    COALESCE(source.source_code, ''),
                    package.package_key,
                    package.display_name,
                    publication.canonical_publication_id,
                    publication.canonical_key,
                    publication.display_name,
                    publication.game_edition,
                    publication.publication_date
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                JOIN canonical_publication publication
                    ON publication.canonical_publication_id = occurrence.canonical_publication_id
                JOIN source_entity_revision revision
                    ON revision.source_entity_revision_id = binding.source_entity_revision_id
                JOIN source_entity source
                    ON source.source_entity_id = revision.source_entity_id
                JOIN source_package package
                    ON package.source_package_id = source.source_package_id
                WHERE occurrence.canonical_entity_id IS NOT NULL
                  AND (package.is_public
                    OR (@user_id IS NOT NULL AND EXISTS (
                        SELECT 1
                        FROM user_source_grant grant_row
                        WHERE grant_row.source_package_id = package.source_package_id
                          AND grant_row.user_id = @user_id)))
                ORDER BY occurrence.canonical_entity_id,
                         publication.publication_date NULLS LAST,
                         publication.canonical_key,
                         source.source_code,
                         revision.revision_number;
                """;
            AddNullableStringParameter(command, "@user_id", userId);

            var values = new List<VariationRecord>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var gameEdition = reader.IsDBNull(12) ? string.Empty : reader.GetString(12);
                var normalizedEdition = NormalizeEdition(gameEdition) ?? string.Empty;
                values.Add(new VariationRecord(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.GetInt32(3),
                    reader.GetString(4),
                    NormalizeCategory(reader.GetString(5)),
                    reader.GetString(6),
                    reader.GetString(7),
                    reader.GetString(8),
                    reader.GetGuid(9),
                    reader.GetString(10),
                    reader.GetString(11),
                    normalizedEdition,
                    normalizedEdition,
                    reader.IsDBNull(13) ? null : reader.GetFieldValue<DateOnly>(13)));
            }
            return values;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private async Task<IReadOnlyDictionary<Guid, JsonElement>> ReadDocumentsAsync(
        IReadOnlyCollection<Guid> revisionIds,
        CancellationToken cancellationToken)
    {
        var ids = revisionIds
            .Where(value => value != Guid.Empty)
            .Distinct()
            .ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, JsonElement>();

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT source_entity_revision_id,
                       COALESCE(content_json, raw_json)::text
                FROM source_entity_revision
                WHERE source_entity_revision_id = ANY(@revision_ids);
                """;
            AddParameter(command, "@revision_ids", ids);

            var documents = new Dictionary<Guid, JsonElement>(ids.Length);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                using var document = JsonDocument.Parse(reader.GetString(1));
                documents[reader.GetGuid(0)] = document.RootElement.Clone();
            }
            return documents;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private async Task<IReadOnlyList<HistoryEdge>> ReadSameHistoryEdgesAsync(
        IReadOnlyList<Guid> canonicalEntityIds,
        CancellationToken cancellationToken)
    {
        if (canonicalEntityIds.Count == 0) return [];
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT from_canonical_entity_id, to_canonical_entity_id, relationship_kind
                FROM canonical_entity_relationship
                WHERE relationship_kind IN ('revision', 'rename')
                  AND from_canonical_entity_id = ANY(@canonical_ids)
                  AND to_canonical_entity_id = ANY(@canonical_ids);
                """;
            AddParameter(command, "@canonical_ids", canonicalEntityIds.ToArray());
            var values = new List<HistoryEdge>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                values.Add(new HistoryEdge(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2)));
            }
            return values;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private async Task<IReadOnlyList<ConceptBinding>> ReadConceptBindingsAsync(
        IReadOnlyList<Guid> canonicalEntityIds,
        CancellationToken cancellationToken)
    {
        if (canonicalEntityIds.Count == 0) return [];
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT binding.canonical_entity_id,
                       concept.rule_concept_id,
                       concept.concept_key,
                       concept.entity_type,
                       concept.display_name,
                       concept.created_at
                FROM rule_concept_source_binding binding
                JOIN rule_concept concept
                  ON concept.rule_concept_id = binding.rule_concept_id
                WHERE binding.canonical_entity_id = ANY(@canonical_ids);
                """;
            AddParameter(command, "@canonical_ids", canonicalEntityIds.ToArray());
            var values = new List<ConceptBinding>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(0)) continue;
                values.Add(new ConceptBinding(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    NormalizeCategory(reader.GetString(3)),
                    reader.GetString(4),
                    reader.GetFieldValue<DateTimeOffset>(5)));
            }
            return values;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private async Task<ScopePublication> ReadGlobalPublicationAsync(CancellationToken cancellationToken)
    {
        var revision = await dbContext.RulesetRevisions
            .AsNoTracking()
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => new { value.Id, value.RevisionNumber, value.PublishedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (revision is null) return new ScopePublication(null, null, new Dictionary<Guid, EffectiveEntry>());

        var rows = await dbContext.RulesetRevisionEntries
            .AsNoTracking()
            .Where(value => value.RulesetRevisionId == revision.Id)
            .Select(value => new
            {
                value.RuleConceptId,
                value.SourceEntityRevisionId,
                value.GlobalRuleDecision.DecisionKind,
                DecisionId = value.GlobalRuleDecision.Id,
                value.GlobalRuleDecision.DecisionNumber,
                value.GlobalRuleDecision.CreatedAt
            })
            .ToArrayAsync(cancellationToken);
        return new ScopePublication(
            revision.RevisionNumber,
            revision.PublishedAt,
            rows.ToDictionary(
                value => value.RuleConceptId,
                value => new EffectiveEntry(
                    value.SourceEntityRevisionId,
                    value.DecisionKind,
                    HasCampaignOverride: false,
                    value.DecisionId,
                    value.DecisionNumber,
                    value.CreatedAt)));
    }

    private async Task<ScopePublication> ReadCampaignPublicationAsync(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var revision = await dbContext.CampaignRulesetRevisions
            .AsNoTracking()
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => new { value.Id, value.RevisionNumber, value.PublishedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (revision is null) return new ScopePublication(null, null, new Dictionary<Guid, EffectiveEntry>());

        var rows = await dbContext.CampaignRulesetRevisionEntries
            .AsNoTracking()
            .Where(value => value.CampaignRulesetRevisionId == revision.Id)
            .Select(value => new
            {
                value.RuleConceptId,
                value.SourceEntityRevisionId,
                DecisionKind = value.CampaignRuleDecision == null
                    ? CampaignRuleDecisionKinds.InheritGlobal
                    : value.CampaignRuleDecision.DecisionKind,
                HasCampaignOverride = value.CampaignRuleDecision != null
                    && value.CampaignRuleDecision.DecisionKind != CampaignRuleDecisionKinds.InheritGlobal,
                CampaignDecisionId = value.CampaignRuleDecision == null
                    ? (Guid?)null
                    : value.CampaignRuleDecision.Id,
                CampaignDecisionNumber = value.CampaignRuleDecision == null
                    ? (int?)null
                    : value.CampaignRuleDecision.DecisionNumber,
                CampaignDecisionCreatedAt = value.CampaignRuleDecision == null
                    ? (DateTimeOffset?)null
                    : value.CampaignRuleDecision.CreatedAt,
                GlobalDecisionId = value.BaselineRulesetRevisionEntry.GlobalRuleDecision.Id,
                GlobalDecisionNumber = value.BaselineRulesetRevisionEntry.GlobalRuleDecision.DecisionNumber,
                GlobalDecisionCreatedAt = value.BaselineRulesetRevisionEntry.GlobalRuleDecision.CreatedAt
            })
            .ToArrayAsync(cancellationToken);
        return new ScopePublication(
            revision.RevisionNumber,
            revision.PublishedAt,
            rows.ToDictionary(
                value => value.RuleConceptId,
                value => new EffectiveEntry(
                    value.SourceEntityRevisionId,
                    value.DecisionKind,
                    value.HasCampaignOverride,
                    value.HasCampaignOverride
                        ? value.CampaignDecisionId!.Value
                        : value.GlobalDecisionId,
                    value.HasCampaignOverride
                        ? value.CampaignDecisionNumber!.Value
                        : value.GlobalDecisionNumber,
                    value.HasCampaignOverride
                        ? value.CampaignDecisionCreatedAt!.Value
                        : value.GlobalDecisionCreatedAt)));
    }

    private static IReadOnlyList<HashSet<Guid>> BuildComponents(
        IReadOnlyList<Guid> canonicalEntityIds,
        IReadOnlyList<HistoryEdge> edges)
    {
        var parent = canonicalEntityIds.ToDictionary(value => value, value => value);
        Guid Find(Guid value)
        {
            while (parent[value] != value)
            {
                parent[value] = parent[parent[value]];
                value = parent[value];
            }
            return value;
        }
        void Union(Guid left, Guid right)
        {
            var leftRoot = Find(left);
            var rightRoot = Find(right);
            if (leftRoot == rightRoot) return;
            parent[rightRoot] = leftRoot.CompareTo(rightRoot) <= 0 ? leftRoot : rightRoot;
            parent[leftRoot] = parent[rightRoot];
        }
        foreach (var edge in edges) Union(edge.FromCanonicalEntityId, edge.ToCanonicalEntityId);
        return canonicalEntityIds
            .GroupBy(Find)
            .Select(group => group.ToHashSet())
            .ToArray();
    }

    private static VariationRecord SelectFallback(IReadOnlyCollection<VariationRecord> variations) =>
        variations
            .OrderByDescending(value => value.PublicationDate.HasValue)
            .ThenByDescending(value => value.PublicationDate)
            .ThenByDescending(value => EditionRank(value.EditionKey))
            .ThenByDescending(value => value.SourceRevisionNumber)
            .ThenBy(value => value.PublicationKey, StringComparer.Ordinal)
            .ThenBy(value => value.SourceEntityRevisionId)
            .First();

    private static IReadOnlyList<WikiReferenceCategoryHistoryView> BuildCategoryHistory(
        IReadOnlyCollection<VariationRecord> variations) =>
        variations
            .GroupBy(value => value.EntityType, StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new WikiReferenceCategoryHistoryView(
                group.Key,
                group.Select(value => value.EditionDisplayName)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => EditionRank(value))
                    .ThenBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();

    private static IReadOnlyList<WikiReferenceFacetView> BuildEntityFacets(
        IReadOnlyCollection<ReferenceGroup> groups,
        string categoryMode)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            var categories = categoryMode == WikiReferenceCategoryModes.Effective
                ? [group.Item.EffectiveCategory]
                : group.Variations.Select(value => value.EntityType).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var category in categories)
            {
                counts[category] = counts.GetValueOrDefault(category) + 1;
            }
        }
        return counts
            .OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase)
            .Select(value => new WikiReferenceFacetView(value.Key, value.Key, value.Value))
            .ToArray();
    }

    private static IReadOnlyList<WikiReferenceFacetView> BuildFacet(
        IReadOnlyCollection<ReferenceGroup> groups,
        Func<ReferenceGroup, IEnumerable<(string Value, string DisplayName)>> selector)
    {
        var values = new Dictionary<string, (string DisplayName, int Count)>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            foreach (var facet in selector(group)
                         .Where(value => !string.IsNullOrWhiteSpace(value.Value))
                         .DistinctBy(value => value.Value, StringComparer.OrdinalIgnoreCase))
            {
                if (values.TryGetValue(facet.Value, out var current))
                {
                    values[facet.Value] = (current.DisplayName, current.Count + 1);
                }
                else
                {
                    values[facet.Value] = (facet.DisplayName, 1);
                }
            }
        }
        return values
            .OrderBy(value => value.Value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Key, StringComparer.OrdinalIgnoreCase)
            .Select(value => new WikiReferenceFacetView(
                value.Key,
                value.Value.DisplayName,
                value.Value.Count))
            .ToArray();
    }

    private static bool CategoryMatches(ReferenceGroup group, string entityType, string mode) =>
        mode == WikiReferenceCategoryModes.Effective
            ? string.Equals(group.Item.EffectiveCategory, entityType, StringComparison.OrdinalIgnoreCase)
            : group.Variations.Any(value => string.Equals(
                value.EntityType,
                entityType,
                StringComparison.OrdinalIgnoreCase));

    private static bool MatchesSearch(ReferenceGroup group, string query)
    {
        if (group.Item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || group.Item.ReferenceIdentity.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (group.Item.ConceptKey?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return true;
        }
        return group.Variations.Any(value =>
            value.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || value.SourceCode.Contains(query, StringComparison.OrdinalIgnoreCase)
            || value.PackageDisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || value.PublicationDisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || value.PublicationKey.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static ReferenceGroup? FindGroup(ReferenceState state, string referenceIdentity) =>
        state.Lookup.TryGetValue(referenceIdentity, out var group) ? group : null;

    private static WikiReferenceItemView HydrateCatalogItem(
        ReferenceGroup group,
        IReadOnlyDictionary<Guid, JsonElement> documents)
    {
        var document = RequireDocument(documents, group.EffectiveVariation.SourceEntityRevisionId);
        return group.Item with
        {
            BrowserFields = RuleBrowserSummaryProjector.Project(
                group.Item.EffectiveCategory,
                document)
        };
    }

    private static JsonElement RequireDocument(
        IReadOnlyDictionary<Guid, JsonElement> documents,
        Guid revisionId) =>
        documents.TryGetValue(revisionId, out var document)
            ? document
            : throw new InvalidOperationException(
                $"Accessible source revision '{revisionId}' disappeared while building the Rules Wiki reference projection.");

    private static WikiReferenceVariationSummaryView ToSummaryVariation(VariationRecord value, bool isEffective) =>
        new(
            value.CanonicalEntityId,
            value.SourceEntityId,
            value.SourceEntityRevisionId,
            value.SourceRevisionNumber,
            value.Name,
            value.EntityType,
            value.SourceCode,
            value.PackageKey,
            value.PackageDisplayName,
            value.PublicationId,
            value.PublicationKey,
            value.PublicationDisplayName,
            value.EditionKey,
            value.EditionDisplayName,
            value.PublicationDate,
            isEffective);

    private static WikiReferenceVariationView ToDetailVariation(
        VariationRecord value,
        JsonElement document,
        bool isEffective) =>
        new(
            value.CanonicalEntityId,
            value.SourceEntityId,
            value.SourceEntityRevisionId,
            value.SourceRevisionNumber,
            value.Name,
            value.EntityType,
            value.SourceCode,
            value.PackageKey,
            value.PackageDisplayName,
            value.PublicationId,
            value.PublicationKey,
            value.PublicationDisplayName,
            value.EditionKey,
            value.EditionDisplayName,
            value.PublicationDate,
            isEffective,
            document);

    internal static string NormalizeCategory(string entityType) =>
        RuleConceptEntityTypes.Normalize(entityType);

    internal static string? NormalizeOptionalCategory(string? value)
    {
        var normalized = NormalizeOptional(value, "entityType", MaximumEntityTypeLength);
        return normalized is null ? null : NormalizeCategory(normalized);
    }

    private static string? NormalizeEdition(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DndEditionCatalog.TryParse(value, out var edition)
            ? DndEditionCatalog.GetCanonicalLabel(edition)
            : value.Trim();
    }

    private static int EditionRank(string? value)
    {
        var normalized = NormalizeEdition(value);
        if (normalized is null) return -1;
        for (var index = 0; index < DndEditionCatalog.CanonicalLabels.Count; index++)
        {
            if (string.Equals(
                    DndEditionCatalog.CanonicalLabels[index],
                    normalized,
                    StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }
        return -1;
    }

    private static string? NormalizeOptional(string? value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return ValidateInput(value.Trim(), parameterName, maximumLength);
    }

    private static string NormalizeRequired(string? value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"'{parameterName}' can not be blank.", parameterName);
        }
        return ValidateInput(value.Trim(), parameterName, maximumLength);
    }

    private static string ValidateInput(string value, string parameterName, int maximumLength)
    {
        if (value.Length > maximumLength)
        {
            throw new ArgumentException(
                $"'{parameterName}' can not exceed {maximumLength} characters.",
                parameterName);
        }
        if (value.Any(char.IsControl))
        {
            throw new ArgumentException($"'{parameterName}' can not contain control characters.", parameterName);
        }
        return value;
    }

    private static string? NormalizeOptionalUserId(string? value) =>
        NormalizeOptional(value, "userId", MaximumUserIdLength);

    private static string NormalizeRequiredUserId(string? value) =>
        NormalizeRequired(value, "userId", MaximumUserIdLength);

    private static string NormalizeRequiredReferenceIdentity(string? value) =>
        NormalizeRequired(value, "referenceIdentity", MaximumReferenceIdentityLength);

    private static void ValidatePage(int limit, int offset)
    {
        if (limit < 1 || limit > MaximumLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), $"Limit must be between 1 and {MaximumLimit}.");
        }
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Offset can not be negative.");
    }

    private static void AddNullableStringParameter(DbCommand command, string name, string? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.String;
        parameter.Value = (object?)value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record VariationRecord(
        Guid CanonicalEntityId,
        Guid SourceEntityId,
        Guid SourceEntityRevisionId,
        int SourceRevisionNumber,
        string Name,
        string EntityType,
        string SourceCode,
        string PackageKey,
        string PackageDisplayName,
        Guid PublicationId,
        string PublicationKey,
        string PublicationDisplayName,
        string EditionKey,
        string EditionDisplayName,
        DateOnly? PublicationDate);

    private sealed record HistoryEdge(Guid FromCanonicalEntityId, Guid ToCanonicalEntityId, string Kind);

    private sealed record ConceptBinding(
        Guid CanonicalEntityId,
        Guid RuleConceptId,
        string ConceptKey,
        string EntityType,
        string DisplayName,
        DateTimeOffset CreatedAt);

    private sealed record EffectiveEntry(
        Guid SourceEntityRevisionId,
        string DecisionKind,
        bool HasCampaignOverride,
        Guid DecisionId,
        int DecisionNumber,
        DateTimeOffset DecisionCreatedAt);

    private sealed record ScopePublication(
        int? RevisionNumber,
        DateTimeOffset? PublishedAt,
        IReadOnlyDictionary<Guid, EffectiveEntry> Entries);

    private sealed record ReferenceGroup(
        WikiReferenceItemView Item,
        IReadOnlyList<VariationRecord> Variations,
        VariationRecord EffectiveVariation);

    private sealed record ReferenceState(
        IReadOnlyList<ReferenceGroup> Groups,
        IReadOnlyDictionary<string, ReferenceGroup> Lookup,
        ScopePublication Publication);
}
