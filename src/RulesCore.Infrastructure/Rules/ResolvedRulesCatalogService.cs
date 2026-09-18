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
            return new ResolvedRulesCatalogView("global", null, null, null, 0, [], []);
        }

        var entries = dbContext.RulesetRevisionEntries
            .AsNoTracking()
            .Where(value => value.RulesetRevisionId == revision.Id)
            .Where(value => value.SourceEntityRevision.SourceEntity.SourcePackage.IsPublic
                || (normalizedUserId != null
                    && value.SourceEntityRevision.SourceEntity.SourcePackage.UserGrants
                        .Any(grant => grant.UserId == normalizedUserId)));

        if (normalizedEntityType is not null)
        {
            entries = entries.Where(value => value.RuleConcept.EntityType.ToLower() == normalizedEntityType);
        }

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

        var sourceFacets = await entries
            .Where(value => value.SourceEntityRevision.SourceEntity.SourceCode != null
                && value.SourceEntityRevision.SourceEntity.SourceCode != "")
            .GroupBy(value => value.SourceEntityRevision.SourceEntity.SourceCode!)
            .Select(group => new ResolvedRuleCatalogSourceFacetView(
                group.Key,
                group.Count()))
            .OrderBy(value => value.SourceCode)
            .ToArrayAsync(cancellationToken);

        if (normalizedSourceCode is not null)
        {
            entries = entries.Where(value =>
                value.SourceEntityRevision.SourceEntity.SourceCode != null
                && value.SourceEntityRevision.SourceEntity.SourceCode.ToLower() == normalizedSourceCode);
        }

        var totalCount = await entries.CountAsync(cancellationToken);
        var rules = await entries
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
                (IReadOnlyList<ResolvedRuleRelationshipView>)null!))
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        rules = await AttachRelationshipsAsync(rules, cancellationToken);
        rules = await AttachGlobalBrowserFieldsAsync(
            revision.Id,
            rules,
            cancellationToken);

        return new ResolvedRulesCatalogView(
            "global",
            null,
            revision.RevisionNumber,
            revision.PublishedAt,
            totalCount,
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
            return new ResolvedRulesCatalogView("campaign", campaignId, null, null, 0, [], []);
        }

        var entries = dbContext.CampaignRulesetRevisionEntries
            .AsNoTracking()
            .Where(value => value.CampaignRulesetRevisionId == revision.Id)
            .Where(value => value.SourceEntityRevision.SourceEntity.SourcePackage.IsPublic
                || value.SourceEntityRevision.SourceEntity.SourcePackage.UserGrants
                    .Any(grant => grant.UserId == normalizedUserId));

        if (normalizedEntityType is not null)
        {
            entries = entries.Where(value => value.RuleConcept.EntityType.ToLower() == normalizedEntityType);
        }

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

        var sourceFacets = await entries
            .Where(value => value.SourceEntityRevision.SourceEntity.SourceCode != null
                && value.SourceEntityRevision.SourceEntity.SourceCode != "")
            .GroupBy(value => value.SourceEntityRevision.SourceEntity.SourceCode!)
            .Select(group => new ResolvedRuleCatalogSourceFacetView(
                group.Key,
                group.Count()))
            .OrderBy(value => value.SourceCode)
            .ToArrayAsync(cancellationToken);

        if (normalizedSourceCode is not null)
        {
            entries = entries.Where(value =>
                value.SourceEntityRevision.SourceEntity.SourceCode != null
                && value.SourceEntityRevision.SourceEntity.SourceCode.ToLower() == normalizedSourceCode);
        }

        var totalCount = await entries.CountAsync(cancellationToken);
        var rules = await entries
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
                (IReadOnlyList<ResolvedRuleRelationshipView>)null!))
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        rules = await AttachRelationshipsAsync(rules, cancellationToken);
        rules = await AttachCampaignBrowserFieldsAsync(
            revision.Id,
            rules,
            cancellationToken);

        return new ResolvedRulesCatalogView(
            "campaign",
            campaignId,
            revision.RevisionNumber,
            revision.PublishedAt,
            totalCount,
            sourceFacets,
            rules);
    }

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
        var fieldsByConcept = new Dictionary<Guid, IReadOnlyList<ResolvedRuleBrowserFieldView>>();
        foreach (var entry in entries)
        {
            using var sourceDocument = JsonDocument.Parse(
                entry.SourceEntityRevision.GetMechanicalContentJson());
            var resolved = ApplyGlobalDecision(
                sourceDocument.RootElement,
                entry.GlobalRuleDecision);
            fieldsByConcept[entry.RuleConceptId] = RuleBrowserSummaryProjector.Project(
                entityTypes[entry.RuleConceptId],
                resolved);
        }

        return rules
            .Select(rule => rule with
            {
                BrowserFields = fieldsByConcept.GetValueOrDefault(rule.RuleConceptId) ?? []
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
        var fieldsByConcept = new Dictionary<Guid, IReadOnlyList<ResolvedRuleBrowserFieldView>>();
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

            fieldsByConcept[entry.RuleConceptId] = RuleBrowserSummaryProjector.Project(
                entityTypes[entry.RuleConceptId],
                resolved);
        }

        return rules
            .Select(rule => rule with
            {
                BrowserFields = fieldsByConcept.GetValueOrDefault(rule.RuleConceptId) ?? []
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
