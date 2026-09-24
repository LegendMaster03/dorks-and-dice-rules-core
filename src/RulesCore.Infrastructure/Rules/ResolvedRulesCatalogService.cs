using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

public sealed class ResolvedRulesCatalogService(RulesCoreDbContext dbContext)
    : IResolvedRulesCatalogService
{
    public Task<ResolvedRulesCatalogView> GetGlobalAsync(
        string? userId,
        string? entityType = null,
        string? query = null,
        int limit = 200,
        CancellationToken cancellationToken = default) =>
        GetGlobalPageAsync(userId, entityType, query, limit, 0, cancellationToken);

    public Task<ResolvedRulesCatalogView> GetGlobalPageAsync(
        string? userId,
        string? entityType = null,
        string? query = null,
        int limit = 200,
        int offset = 0,
        CancellationToken cancellationToken = default) =>
        GetGlobalFilteredPageAsync(
            userId,
            entityType,
            query,
            sourceCode: null,
            limit: limit,
            offset: offset,
            cancellationToken: cancellationToken);

    public async Task<ResolvedRulesCatalogView> GetGlobalFilteredPageAsync(
        string? userId,
        string? entityType,
        string? query,
        string? sourceCode,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        ValidateOffset(offset);
        var normalizedUserId = NormalizeOptionalUserId(userId);
        var normalizedEntityType = NormalizeOptional(entityType)?.ToLowerInvariant();
        var normalizedQuery = NormalizeOptional(query)?.ToLowerInvariant();
        var normalizedSourceCode = NormalizeOptional(sourceCode)?.ToLowerInvariant();

        var revision = await dbContext.RulesetRevisions
            .AsNoTracking()
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => new RevisionReference(value.Id, value.RevisionNumber, value.PublishedAt))
            .FirstOrDefaultAsync(cancellationToken);
        if (revision is null)
        {
            return await BuildFallbackOnlyCatalogAsync(
                "global",
                campaignId: null,
                normalizedUserId,
                normalizedEntityType,
                normalizedQuery,
                normalizedSourceCode,
                overridesOnly: false,
                limit,
                offset,
                cancellationToken);
        }

        var accessibleEntries = dbContext.RulesetRevisionEntries
            .AsNoTracking()
            .Where(value => value.RulesetRevisionId == revision.Id)
            .Where(value => value.SourceEntityRevision.SourceEntity.SourcePackage.IsPublic
                || (normalizedUserId != null
                    && value.SourceEntityRevision.SourceEntity.SourcePackage.UserGrants
                        .Any(grant => grant.UserId == normalizedUserId)));
        var publishedAccessibleConceptIds = await accessibleEntries
            .Select(value => value.RuleConceptId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var entries = accessibleEntries;
        if (normalizedQuery is not null)
        {
            entries = entries.Where(value =>
                value.RuleConcept.DisplayName.ToLower().Contains(normalizedQuery)
                || value.RuleConcept.Key.ToLower().Contains(normalizedQuery)
                || value.SourceEntityRevision.SourceEntity.Name.ToLower().Contains(normalizedQuery)
                || (value.SourceEntityRevision.SourceEntity.SourceCode != null
                    && value.SourceEntityRevision.SourceEntity.SourceCode.ToLower().Contains(normalizedQuery))
                || value.SourceEntityRevision.SourceEntity.FormatKey.ToLower().Contains(normalizedQuery)
                || value.SourceEntityRevision.SourceEntity.SourcePackage.DisplayName.ToLower().Contains(normalizedQuery));
        }

        ResolvedRuleCatalogEntityTypeFacetView[] entityTypeFacets = [];
        if (offset == 0)
        {
            var entityFacetEntries = entries;
            if (normalizedSourceCode is not null)
            {
                entityFacetEntries = entityFacetEntries.Where(value =>
                    value.SourceEntityRevision.SourceEntity.SourceCode != null
                    && value.SourceEntityRevision.SourceEntity.SourceCode.ToLower() == normalizedSourceCode);
            }
            var entityTypeFacetRows = await entityFacetEntries
                .GroupBy(value => value.RuleConcept.EntityType)
                .Select(group => new
                {
                    EntityType = group.Key,
                    Count = group.Count()
                })
                .ToArrayAsync(cancellationToken);
            entityTypeFacets = entityTypeFacetRows
                .OrderBy(value => value.EntityType, StringComparer.OrdinalIgnoreCase)
                .Select(value => new ResolvedRuleCatalogEntityTypeFacetView(
                    value.EntityType,
                    value.Count))
                .ToArray();
        }

        if (normalizedEntityType is not null)
        {
            entries = entries.Where(value => value.RuleConcept.EntityType.ToLower() == normalizedEntityType);
        }

        ResolvedRuleCatalogSourceFacetView[] sourceFacets = [];
        if (offset == 0)
        {
            var sourceFacetRows = await entries
                .Where(value => value.SourceEntityRevision.SourceEntity.SourceCode != null
                    && value.SourceEntityRevision.SourceEntity.SourceCode != "")
                .GroupBy(value => value.SourceEntityRevision.SourceEntity.SourceCode!)
                .Select(group => new
                {
                    SourceCode = group.Key,
                    Count = group.Count()
                })
                .ToArrayAsync(cancellationToken);
            sourceFacets = sourceFacetRows
                .OrderBy(value => value.SourceCode, StringComparer.OrdinalIgnoreCase)
                .Select(value => new ResolvedRuleCatalogSourceFacetView(
                    value.SourceCode,
                    value.Count))
                .ToArray();
        }

        if (normalizedSourceCode is not null)
        {
            entries = entries.Where(value =>
                value.SourceEntityRevision.SourceEntity.SourceCode != null
                && value.SourceEntityRevision.SourceEntity.SourceCode.ToLower() == normalizedSourceCode);
        }

        var fallbackAll = await BuildFallbackItemsAsync(
            normalizedUserId,
            publishedAccessibleConceptIds,
            normalizedQuery,
            cancellationToken);
        var fallbackFiltered = FilterFallbackItems(
            fallbackAll,
            normalizedEntityType,
            normalizedSourceCode);

        if (offset == 0)
        {
            entityTypeFacets = MergeEntityTypeFacets(
                entityTypeFacets,
                FilterFallbackItems(fallbackAll, entityType: null, normalizedSourceCode));
            sourceFacets = MergeSourceFacets(
                sourceFacets,
                FilterFallbackItems(fallbackAll, normalizedEntityType, sourceCode: null));
        }

        var publishedTotalCount = await entries.CountAsync(cancellationToken);
        var publishedTake = offset < publishedTotalCount
            ? Math.Min(limit, publishedTotalCount - offset)
            : 0;
        ResolvedRuleCatalogItemView[] rules = [];
        if (publishedTake > 0)
        {
            rules = await entries
                .OrderBy(value => value.RuleConcept.EntityType)
                .ThenBy(value => value.RuleConcept.DisplayName)
                .ThenBy(value => value.RuleConcept.Key)
                .ThenBy(value => value.RuleConceptId)
                .Skip(offset)
                .Select(value => new ResolvedRuleCatalogItemView(
                    value.RuleConceptId,
                    value.RuleConcept.Key,
                    value.RuleConcept.EntityType,
                    value.RuleConcept.DisplayName,
                    value.GlobalRuleDecision.DecisionKind,
                    HasCampaignOverride: false,
                    value.SourceEntityRevision.SourceEntityId,
                    value.SourceEntityRevisionId,
                    value.SourceEntityRevision.RevisionNumber,
                    value.SourceEntityRevision.SourceEntity.Name,
                    value.SourceEntityRevision.SourceEntity.SourceCode ?? string.Empty,
                    value.SourceEntityRevision.SourceEntity.SourcePackage.Key,
                    value.SourceEntityRevision.SourceEntity.SourcePackage.DisplayName,
                    value.SourceEntityRevision.SourceEntity.FormatKey,
                    value.SourceEntityRevision.SourceEntity.FormatKey,
                    (IReadOnlyList<ResolvedRuleBrowserFieldView>)null!,
                    (IReadOnlyList<ResolvedRuleRelationshipView>)null!,
                    null))
                .Take(publishedTake)
                .ToArrayAsync(cancellationToken);
            rules = await AttachRelationshipsAsync(rules, cancellationToken);
            rules = await AttachGlobalBrowserFieldsAsync(
                revision.Id,
                rules,
                cancellationToken);
        }

        var fallbackSkip = Math.Max(0, offset - publishedTotalCount);
        var fallbackTake = Math.Max(0, limit - rules.Length);
        if (fallbackTake > 0)
        {
            rules = rules
                .Concat(fallbackFiltered.Skip(fallbackSkip).Take(fallbackTake))
                .ToArray();
        }

        return new ResolvedRulesCatalogView(
            "global",
            null,
            revision.RevisionNumber,
            revision.PublishedAt,
            publishedTotalCount + fallbackFiltered.Length,
            entityTypeFacets,
            sourceFacets,
            rules);
    }

    public Task<ResolvedRulesCatalogView> GetCampaignAsync(
        Guid campaignId,
        string userId,
        string? entityType = null,
        string? query = null,
        int limit = 200,
        CancellationToken cancellationToken = default) =>
        GetCampaignPageAsync(campaignId, userId, entityType, query, limit, 0, cancellationToken);

    public Task<ResolvedRulesCatalogView> GetCampaignPageAsync(
        Guid campaignId,
        string userId,
        string? entityType = null,
        string? query = null,
        int limit = 200,
        int offset = 0,
        CancellationToken cancellationToken = default) =>
        GetCampaignFilteredPageAsync(
            campaignId,
            userId,
            entityType,
            query,
            sourceCode: null,
            overridesOnly: false,
            limit: limit,
            offset: offset,
            cancellationToken: cancellationToken);

    public async Task<ResolvedRulesCatalogView> GetCampaignFilteredPageAsync(
        Guid campaignId,
        string userId,
        string? entityType,
        string? query,
        string? sourceCode,
        bool overridesOnly,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        RequireGuid(campaignId, nameof(campaignId));
        ValidateLimit(limit);
        ValidateOffset(offset);
        var normalizedUserId = RequireUserId(userId);
        var normalizedEntityType = NormalizeOptional(entityType)?.ToLowerInvariant();
        var normalizedQuery = NormalizeOptional(query)?.ToLowerInvariant();
        var normalizedSourceCode = NormalizeOptional(sourceCode)?.ToLowerInvariant();

        var revision = await dbContext.CampaignRulesetRevisions
            .AsNoTracking()
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => new RevisionReference(value.Id, value.RevisionNumber, value.PublishedAt))
            .FirstOrDefaultAsync(cancellationToken);
        if (revision is null)
        {
            return await BuildFallbackOnlyCatalogAsync(
                "campaign",
                campaignId,
                normalizedUserId,
                normalizedEntityType,
                normalizedQuery,
                normalizedSourceCode,
                overridesOnly,
                limit,
                offset,
                cancellationToken);
        }

        var accessibleEntries = dbContext.CampaignRulesetRevisionEntries
            .AsNoTracking()
            .Where(value => value.CampaignRulesetRevisionId == revision.Id)
            .Where(value => value.SourceEntityRevision.SourceEntity.SourcePackage.IsPublic
                || value.SourceEntityRevision.SourceEntity.SourcePackage.UserGrants
                    .Any(grant => grant.UserId == normalizedUserId));
        var publishedAccessibleConceptIds = await accessibleEntries
            .Select(value => value.RuleConceptId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var entries = accessibleEntries;
        if (normalizedQuery is not null)
        {
            entries = entries.Where(value =>
                value.RuleConcept.DisplayName.ToLower().Contains(normalizedQuery)
                || value.RuleConcept.Key.ToLower().Contains(normalizedQuery)
                || value.SourceEntityRevision.SourceEntity.Name.ToLower().Contains(normalizedQuery)
                || (value.SourceEntityRevision.SourceEntity.SourceCode != null
                    && value.SourceEntityRevision.SourceEntity.SourceCode.ToLower().Contains(normalizedQuery))
                || value.SourceEntityRevision.SourceEntity.FormatKey.ToLower().Contains(normalizedQuery)
                || value.SourceEntityRevision.SourceEntity.SourcePackage.DisplayName.ToLower().Contains(normalizedQuery));
        }

        if (overridesOnly)
        {
            entries = entries.Where(value =>
                value.CampaignRuleDecision != null
                && value.CampaignRuleDecision.DecisionKind != CampaignRuleDecisionKinds.InheritGlobal);
        }

        ResolvedRuleCatalogEntityTypeFacetView[] entityTypeFacets = [];
        if (offset == 0)
        {
            var entityFacetEntries = entries;
            if (normalizedSourceCode is not null)
            {
                entityFacetEntries = entityFacetEntries.Where(value =>
                    value.SourceEntityRevision.SourceEntity.SourceCode != null
                    && value.SourceEntityRevision.SourceEntity.SourceCode.ToLower() == normalizedSourceCode);
            }
            var entityTypeFacetRows = await entityFacetEntries
                .GroupBy(value => value.RuleConcept.EntityType)
                .Select(group => new
                {
                    EntityType = group.Key,
                    Count = group.Count()
                })
                .ToArrayAsync(cancellationToken);
            entityTypeFacets = entityTypeFacetRows
                .OrderBy(value => value.EntityType, StringComparer.OrdinalIgnoreCase)
                .Select(value => new ResolvedRuleCatalogEntityTypeFacetView(
                    value.EntityType,
                    value.Count))
                .ToArray();
        }

        if (normalizedEntityType is not null)
        {
            entries = entries.Where(value => value.RuleConcept.EntityType.ToLower() == normalizedEntityType);
        }

        ResolvedRuleCatalogSourceFacetView[] sourceFacets = [];
        if (offset == 0)
        {
            var sourceFacetRows = await entries
                .Where(value => value.SourceEntityRevision.SourceEntity.SourceCode != null
                    && value.SourceEntityRevision.SourceEntity.SourceCode != "")
                .GroupBy(value => value.SourceEntityRevision.SourceEntity.SourceCode!)
                .Select(group => new
                {
                    SourceCode = group.Key,
                    Count = group.Count()
                })
                .ToArrayAsync(cancellationToken);
            sourceFacets = sourceFacetRows
                .OrderBy(value => value.SourceCode, StringComparer.OrdinalIgnoreCase)
                .Select(value => new ResolvedRuleCatalogSourceFacetView(
                    value.SourceCode,
                    value.Count))
                .ToArray();
        }

        if (normalizedSourceCode is not null)
        {
            entries = entries.Where(value =>
                value.SourceEntityRevision.SourceEntity.SourceCode != null
                && value.SourceEntityRevision.SourceEntity.SourceCode.ToLower() == normalizedSourceCode);
        }

        var fallbackAll = overridesOnly
            ? []
            : await BuildFallbackItemsAsync(
                normalizedUserId,
                publishedAccessibleConceptIds,
                normalizedQuery,
                cancellationToken);
        var fallbackFiltered = FilterFallbackItems(
            fallbackAll,
            normalizedEntityType,
            normalizedSourceCode);

        if (offset == 0 && !overridesOnly)
        {
            entityTypeFacets = MergeEntityTypeFacets(
                entityTypeFacets,
                FilterFallbackItems(fallbackAll, entityType: null, normalizedSourceCode));
            sourceFacets = MergeSourceFacets(
                sourceFacets,
                FilterFallbackItems(fallbackAll, normalizedEntityType, sourceCode: null));
        }

        var publishedTotalCount = await entries.CountAsync(cancellationToken);
        var publishedTake = offset < publishedTotalCount
            ? Math.Min(limit, publishedTotalCount - offset)
            : 0;
        ResolvedRuleCatalogItemView[] rules = [];
        if (publishedTake > 0)
        {
            rules = await entries
                .OrderBy(value => value.RuleConcept.EntityType)
                .ThenBy(value => value.RuleConcept.DisplayName)
                .ThenBy(value => value.RuleConcept.Key)
                .ThenBy(value => value.RuleConceptId)
                .Skip(offset)
                .Select(value => new ResolvedRuleCatalogItemView(
                    value.RuleConceptId,
                    value.RuleConcept.Key,
                    value.RuleConcept.EntityType,
                    value.RuleConcept.DisplayName,
                    value.CampaignRuleDecision == null
                        ? CampaignRuleDecisionKinds.InheritGlobal
                        : value.CampaignRuleDecision.DecisionKind,
                    value.CampaignRuleDecision != null
                        && value.CampaignRuleDecision.DecisionKind != CampaignRuleDecisionKinds.InheritGlobal,
                    value.SourceEntityRevision.SourceEntityId,
                    value.SourceEntityRevisionId,
                    value.SourceEntityRevision.RevisionNumber,
                    value.SourceEntityRevision.SourceEntity.Name,
                    value.SourceEntityRevision.SourceEntity.SourceCode ?? string.Empty,
                    value.SourceEntityRevision.SourceEntity.SourcePackage.Key,
                    value.SourceEntityRevision.SourceEntity.SourcePackage.DisplayName,
                    value.SourceEntityRevision.SourceEntity.FormatKey,
                    value.SourceEntityRevision.SourceEntity.FormatKey,
                    (IReadOnlyList<ResolvedRuleBrowserFieldView>)null!,
                    (IReadOnlyList<ResolvedRuleRelationshipView>)null!,
                    null))
                .Take(publishedTake)
                .ToArrayAsync(cancellationToken);
            rules = await AttachRelationshipsAsync(rules, cancellationToken);
            rules = await AttachCampaignBrowserFieldsAsync(
                revision.Id,
                rules,
                cancellationToken);
        }

        var fallbackSkip = Math.Max(0, offset - publishedTotalCount);
        var fallbackTake = Math.Max(0, limit - rules.Length);
        if (fallbackTake > 0)
        {
            rules = rules
                .Concat(fallbackFiltered.Skip(fallbackSkip).Take(fallbackTake))
                .ToArray();
        }

        return new ResolvedRulesCatalogView(
            "campaign",
            campaignId,
            revision.RevisionNumber,
            revision.PublishedAt,
            publishedTotalCount + fallbackFiltered.Length,
            entityTypeFacets,
            sourceFacets,
            rules);
    }

    private async Task<ResolvedRulesCatalogView> BuildFallbackOnlyCatalogAsync(
        string scope,
        Guid? campaignId,
        string? userId,
        string? entityType,
        string? query,
        string? sourceCode,
        bool overridesOnly,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        if (overridesOnly)
        {
            return new ResolvedRulesCatalogView(
                scope,
                campaignId,
                null,
                null,
                0,
                [],
                [],
                []);
        }

        var all = await BuildFallbackItemsAsync(
            userId,
            excludedConceptIds: [],
            query,
            cancellationToken);
        var filtered = FilterFallbackItems(all, entityType, sourceCode);
        var entityFacets = offset == 0
            ? MergeEntityTypeFacets(
                [],
                FilterFallbackItems(all, entityType: null, sourceCode))
            : [];
        var sourceFacets = offset == 0
            ? MergeSourceFacets(
                [],
                FilterFallbackItems(all, entityType, sourceCode: null))
            : [];

        return new ResolvedRulesCatalogView(
            scope,
            campaignId,
            null,
            null,
            filtered.Length,
            entityFacets,
            sourceFacets,
            filtered.Skip(offset).Take(limit).ToArray());
    }

    private async Task<ResolvedRuleCatalogItemView[]> BuildFallbackItemsAsync(
        string? userId,
        IReadOnlyCollection<Guid> excludedConceptIds,
        string? normalizedQuery,
        CancellationToken cancellationToken)
    {
        var concepts = dbContext.RuleConcepts.AsNoTracking().AsQueryable();
        if (excludedConceptIds.Count > 0)
        {
            concepts = concepts.Where(value => !excludedConceptIds.Contains(value.Id));
        }
        if (normalizedQuery is not null)
        {
            concepts = concepts.Where(value =>
                value.DisplayName.ToLower().Contains(normalizedQuery)
                || value.Key.ToLower().Contains(normalizedQuery));
        }

        var candidates = await concepts
            .OrderBy(value => value.EntityType)
            .ThenBy(value => value.DisplayName)
            .ThenBy(value => value.Key)
            .ThenBy(value => value.Id)
            .ToArrayAsync(cancellationToken);
        var resolver = new EffectiveRuleFallbackResolver(dbContext);
        var result = new List<ResolvedRuleCatalogItemView>();
        foreach (var concept in candidates)
        {
            var fallback = await resolver.ResolveAsync(concept, userId, cancellationToken);
            if (fallback is null)
            {
                continue;
            }

            var source = fallback.Source;
            var revision = fallback.Revision;
            using var document = JsonDocument.Parse(revision.GetMechanicalContentJson());
            var resolvedDocument = document.RootElement.Clone();
            var edition = fallback.Publication?.GameEdition ?? source.FormatKey;
            result.Add(new ResolvedRuleCatalogItemView(
                concept.Id,
                concept.Key,
                RuleConceptEntityTypes.Normalize(concept.EntityType),
                concept.DisplayName,
                RuleResolutionStates.UnresolvedFallback,
                HasCampaignOverride: false,
                source.Id,
                revision.Id,
                revision.RevisionNumber,
                source.Name,
                source.SourceCode ?? string.Empty,
                source.SourcePackage.Key,
                source.SourcePackage.DisplayName,
                edition,
                edition,
                RuleBrowserSummaryProjector.Project(concept.EntityType, resolvedDocument),
                Relationships: [],
                Document: resolvedDocument,
                Resolution: EffectiveRuleResolutionView.UnresolvedFallback(
                    revision.Id,
                    revision.RevisionNumber)));
        }

        return await AttachRelationshipsAsync(result, cancellationToken);
    }

    private static ResolvedRuleCatalogItemView[] FilterFallbackItems(
        IReadOnlyList<ResolvedRuleCatalogItemView> values,
        string? entityType,
        string? sourceCode) =>
        values
            .Where(value => entityType is null
                || string.Equals(value.EntityType, entityType, StringComparison.OrdinalIgnoreCase))
            .Where(value => sourceCode is null
                || string.Equals(value.SourceCode, sourceCode, StringComparison.OrdinalIgnoreCase))
            .ToArray();

    private static ResolvedRuleCatalogEntityTypeFacetView[] MergeEntityTypeFacets(
        IReadOnlyList<ResolvedRuleCatalogEntityTypeFacetView> published,
        IReadOnlyList<ResolvedRuleCatalogItemView> fallbacks) =>
        published
            .Concat(fallbacks
                .GroupBy(value => value.EntityType, StringComparer.OrdinalIgnoreCase)
                .Select(group => new ResolvedRuleCatalogEntityTypeFacetView(
                    group.Key,
                    group.Count())))
            .GroupBy(value => value.EntityType, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ResolvedRuleCatalogEntityTypeFacetView(
                group.First().EntityType,
                group.Sum(value => value.Count)))
            .OrderBy(value => value.EntityType, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static ResolvedRuleCatalogSourceFacetView[] MergeSourceFacets(
        IReadOnlyList<ResolvedRuleCatalogSourceFacetView> published,
        IReadOnlyList<ResolvedRuleCatalogItemView> fallbacks) =>
        published
            .Concat(fallbacks
                .Where(value => !string.IsNullOrWhiteSpace(value.SourceCode))
                .GroupBy(value => value.SourceCode, StringComparer.OrdinalIgnoreCase)
                .Select(group => new ResolvedRuleCatalogSourceFacetView(
                    group.Key,
                    group.Count())))
            .GroupBy(value => value.SourceCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ResolvedRuleCatalogSourceFacetView(
                group.First().SourceCode,
                group.Sum(value => value.Count)))
            .OrderBy(value => value.SourceCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private async Task<ResolvedRuleCatalogItemView[]> AttachGlobalBrowserFieldsAsync(
        Guid rulesetRevisionId,
        IReadOnlyCollection<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        if (rules.Count == 0) return [];

        var conceptIds = rules.Select(value => value.RuleConceptId).ToArray();
        var entries = await dbContext.RulesetRevisionEntries
            .AsNoTracking()
            .Include(value => value.SourceEntityRevision)
            .Include(value => value.GlobalRuleDecision)
            .Where(value => value.RulesetRevisionId == rulesetRevisionId
                && conceptIds.Contains(value.RuleConceptId))
            .ToArrayAsync(cancellationToken);

        var entityTypes = rules.ToDictionary(value => value.RuleConceptId, value => value.EntityType);
        var resolvedByConcept = new Dictionary<Guid, (IReadOnlyList<ResolvedRuleBrowserFieldView> Fields, JsonElement Document)>();
        foreach (var entry in entries)
        {
            using var sourceDocument = JsonDocument.Parse(
                entry.SourceEntityRevision.GetMechanicalContentJson());
            var resolved = ApplyGlobalDecision(
                sourceDocument.RootElement,
                entry.GlobalRuleDecision);
            resolvedByConcept[entry.RuleConceptId] = (
                RuleBrowserSummaryProjector.Project(
                    entityTypes[entry.RuleConceptId],
                    resolved),
                resolved.Clone());
        }

        return rules
            .Select(rule =>
            {
                var projected = resolvedByConcept.GetValueOrDefault(rule.RuleConceptId);
                return rule with
                {
                    BrowserFields = projected.Fields ?? [],
                    Document = projected.Document.ValueKind == JsonValueKind.Undefined
                        ? null
                        : projected.Document
                };
            })
            .ToArray();
    }

    private async Task<ResolvedRuleCatalogItemView[]> AttachCampaignBrowserFieldsAsync(
        Guid campaignRulesetRevisionId,
        IReadOnlyCollection<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        if (rules.Count == 0) return [];

        var conceptIds = rules.Select(value => value.RuleConceptId).ToArray();
        var entries = await dbContext.CampaignRulesetRevisionEntries
            .AsNoTracking()
            .Include(value => value.SourceEntityRevision)
            .Include(value => value.CampaignRuleDecision)
            .Include(value => value.BaselineRulesetRevisionEntry)
                .ThenInclude(value => value.GlobalRuleDecision)
            .Where(value => value.CampaignRulesetRevisionId == campaignRulesetRevisionId
                && conceptIds.Contains(value.RuleConceptId))
            .ToArrayAsync(cancellationToken);

        var entityTypes = rules.ToDictionary(value => value.RuleConceptId, value => value.EntityType);
        var resolvedByConcept = new Dictionary<Guid, (IReadOnlyList<ResolvedRuleBrowserFieldView> Fields, JsonElement Document)>();
        foreach (var entry in entries)
        {
            using var sourceDocument = JsonDocument.Parse(
                entry.SourceEntityRevision.GetMechanicalContentJson());
            var resolved = sourceDocument.RootElement.Clone();
            if (entry.CampaignRuleDecision?.DecisionKind != CampaignRuleDecisionKinds.SelectSource)
            {
                resolved = ApplyGlobalDecision(
                    resolved,
                    entry.BaselineRulesetRevisionEntry.GlobalRuleDecision);
            }
            if (entry.CampaignRuleDecision is not null)
            {
                resolved = ApplyCampaignDecision(resolved, entry.CampaignRuleDecision);
            }

            resolvedByConcept[entry.RuleConceptId] = (
                RuleBrowserSummaryProjector.Project(
                    entityTypes[entry.RuleConceptId],
                    resolved),
                resolved.Clone());
        }

        return rules
            .Select(rule =>
            {
                var projected = resolvedByConcept.GetValueOrDefault(rule.RuleConceptId);
                return rule with
                {
                    BrowserFields = projected.Fields ?? [],
                    Document = projected.Document.ValueKind == JsonValueKind.Undefined
                        ? null
                        : projected.Document
                };
            })
            .ToArray();
    }

    private static JsonElement ApplyGlobalDecision(
        JsonElement source,
        GlobalRuleDecision decision) => decision.DecisionKind switch
    {
        RuleDecisionKinds.JsonMergePatch => JsonMergePatch.Apply(source, decision.PatchJson),
        RuleDecisionKinds.JsonRulePatch => JsonRulePatch.Apply(source, decision.PatchJson),
        _ => source.Clone()
    };

    private static JsonElement ApplyCampaignDecision(
        JsonElement source,
        CampaignRuleDecision decision) => decision.DecisionKind switch
    {
        CampaignRuleDecisionKinds.JsonMergePatch => JsonMergePatch.Apply(source, decision.PatchJson),
        CampaignRuleDecisionKinds.JsonRulePatch => JsonRulePatch.Apply(source, decision.PatchJson),
        _ => source.Clone()
    };

    private async Task<ResolvedRuleCatalogItemView[]> AttachRelationshipsAsync(
        IReadOnlyCollection<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        if (rules.Count == 0)
        {
            return [];
        }

        var relationships = await RuleConceptRelationshipStore.GetOutgoingAsync(
            dbContext,
            rules.Select(value => value.RuleConceptId).ToArray(),
            cancellationToken);

        return rules
            .Select(rule => rule with
            {
                EntityType = RuleConceptEntityTypes.Normalize(rule.EntityType),
                Resolution = rule.Resolution
                    ?? EffectiveRuleResolutionView.Resolved(
                        rule.SourceEntityRevisionId,
                        rule.SourceRevisionNumber),
                Relationships = relationships.TryGetValue(rule.RuleConceptId, out var related)
                    ? related.Select(value => new ResolvedRuleRelationshipView(
                        value.Kind,
                        value.RelatedRuleConceptId,
                        value.RelatedConceptKey,
                        RuleConceptEntityTypes.Normalize(value.RelatedEntityType),
                        value.RelatedDisplayName)).ToArray()
                    : []
            })
            .ToArray();
    }

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 500.");
        }
    }

    private static void ValidateOffset(int offset)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset can not be negative.");
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeOptionalUserId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        return RequireUserId(value);
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

    private static void RequireGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Value can not be empty.", parameterName);
        }
    }

    private sealed record RevisionReference(Guid Id, int RevisionNumber, DateTimeOffset PublishedAt);
}
