using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Bulk read model for internal consumers that need a complete effective rules snapshot.
/// Unlike <see cref="ResolvedRulesCatalogService"/>, this path does not repeatedly rebuild
/// browser facets, counts, fallback state, and OFFSET pages while walking the same revision.
/// </summary>
internal sealed class ResolvedRulesSnapshotService(RulesCoreDbContext dbContext)
{
    internal async Task<ResolvedRulesCatalogView> ReadGlobalAsync(
        string? userId,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = NormalizeOptionalUserId(userId);
        var revision = await dbContext.RulesetRevisions
            .AsNoTracking()
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => new RevisionReference(value.Id, value.RevisionNumber, value.PublishedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (revision is null)
        {
            var fallbackOnly = await BuildFallbackItemsAsync(
                normalizedUserId,
                excludedConceptIds: [],
                cancellationToken);
            return new ResolvedRulesCatalogView(
                "global", null, null, null, fallbackOnly.Length, [], [], fallbackOnly);
        }

        var entries = await dbContext.RulesetRevisionEntries
            .AsNoTracking()
            .Include(value => value.RuleConcept)
            .Include(value => value.GlobalRuleDecision)
            .Include(value => value.SourceEntityRevision)
                .ThenInclude(value => value.SourceEntity)
                    .ThenInclude(value => value.SourcePackage)
            .Where(value => value.RulesetRevisionId == revision.Id)
            .Where(value => value.SourceEntityRevision.SourceEntity.SourcePackage.IsPublic
                || (normalizedUserId != null
                    && value.SourceEntityRevision.SourceEntity.SourcePackage.UserGrants
                        .Any(grant => grant.UserId == normalizedUserId)))
            .OrderBy(value => value.SourceEntityRevision.SourceEntity.EntityType)
            .ThenBy(value => value.RuleConcept.DisplayName)
            .ThenBy(value => value.RuleConcept.Key)
            .ThenBy(value => value.RuleConceptId)
            .ToArrayAsync(cancellationToken);

        var published = new ResolvedRuleCatalogItemView[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            using var sourceDocument = JsonDocument.Parse(entry.SourceEntityRevision.GetMechanicalContentJson());
            var document = ApplyGlobalDecision(sourceDocument.RootElement, entry.GlobalRuleDecision);
            var source = entry.SourceEntityRevision.SourceEntity;
            published[index] = new ResolvedRuleCatalogItemView(
                entry.RuleConceptId,
                entry.RuleConcept.Key,
                source.EntityType,
                entry.RuleConcept.DisplayName,
                entry.GlobalRuleDecision.DecisionKind,
                HasCampaignOverride: false,
                source.Id,
                entry.SourceEntityRevisionId,
                entry.SourceEntityRevision.RevisionNumber,
                source.Name,
                source.SourceCode ?? string.Empty,
                source.SourcePackage.Key,
                source.SourcePackage.DisplayName,
                string.Empty,
                string.Empty,
                BrowserFields: [],
                Relationships: [],
                Document: document,
                Resolution: EffectiveRuleResolutionView.Resolved(
                    entry.SourceEntityRevisionId,
                    entry.SourceEntityRevision.RevisionNumber));
        }

        published = await ResolvedRuleCatalogEditionMetadata.AttachAsync(dbContext, published, cancellationToken);
        published = await AttachRelationshipsAsync(published, cancellationToken);

        var fallbacks = await BuildFallbackItemsAsync(
            normalizedUserId,
            entries.Select(value => value.RuleConceptId).ToArray(),
            cancellationToken);
        var rules = published
            .Concat(fallbacks)
            .OrderBy(value => value.EntityType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.ConceptKey, StringComparer.Ordinal)
            .ToArray();

        return new ResolvedRulesCatalogView(
            "global", null, revision.RevisionNumber, revision.PublishedAt, rules.Length, [], [], rules);
    }

    internal async Task<ResolvedRulesCatalogView> ReadCampaignAsync(
        Guid campaignId,
        string userId,
        CancellationToken cancellationToken)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("Campaign ID can not be empty.", nameof(campaignId));
        }
        var normalizedUserId = RequireUserId(userId);
        var revision = await dbContext.CampaignRulesetRevisions
            .AsNoTracking()
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => new RevisionReference(value.Id, value.RevisionNumber, value.PublishedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (revision is null)
        {
            var fallbackOnly = await BuildFallbackItemsAsync(
                normalizedUserId,
                excludedConceptIds: [],
                cancellationToken);
            return new ResolvedRulesCatalogView(
                "campaign", campaignId, null, null, fallbackOnly.Length, [], [], fallbackOnly);
        }

        var entries = await dbContext.CampaignRulesetRevisionEntries
            .AsNoTracking()
            .Include(value => value.RuleConcept)
            .Include(value => value.CampaignRuleDecision)
            .Include(value => value.BaselineRulesetRevisionEntry)
                .ThenInclude(value => value.GlobalRuleDecision)
            .Include(value => value.SourceEntityRevision)
                .ThenInclude(value => value.SourceEntity)
                    .ThenInclude(value => value.SourcePackage)
            .Where(value => value.CampaignRulesetRevisionId == revision.Id)
            .Where(value => value.SourceEntityRevision.SourceEntity.SourcePackage.IsPublic
                || value.SourceEntityRevision.SourceEntity.SourcePackage.UserGrants
                    .Any(grant => grant.UserId == normalizedUserId))
            .OrderBy(value => value.SourceEntityRevision.SourceEntity.EntityType)
            .ThenBy(value => value.RuleConcept.DisplayName)
            .ThenBy(value => value.RuleConcept.Key)
            .ThenBy(value => value.RuleConceptId)
            .ToArrayAsync(cancellationToken);

        var published = new ResolvedRuleCatalogItemView[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            using var sourceDocument = JsonDocument.Parse(entry.SourceEntityRevision.GetMechanicalContentJson());
            var document = sourceDocument.RootElement.Clone();
            if (entry.CampaignRuleDecision?.DecisionKind != CampaignRuleDecisionKinds.SelectSource)
            {
                document = ApplyGlobalDecision(document, entry.BaselineRulesetRevisionEntry.GlobalRuleDecision);
            }
            if (entry.CampaignRuleDecision is not null)
            {
                document = ApplyCampaignDecision(document, entry.CampaignRuleDecision);
            }

            var source = entry.SourceEntityRevision.SourceEntity;
            published[index] = new ResolvedRuleCatalogItemView(
                entry.RuleConceptId,
                entry.RuleConcept.Key,
                source.EntityType,
                entry.RuleConcept.DisplayName,
                entry.CampaignRuleDecision?.DecisionKind ?? CampaignRuleDecisionKinds.InheritGlobal,
                entry.CampaignRuleDecision is not null
                    && entry.CampaignRuleDecision.DecisionKind != CampaignRuleDecisionKinds.InheritGlobal,
                source.Id,
                entry.SourceEntityRevisionId,
                entry.SourceEntityRevision.RevisionNumber,
                source.Name,
                source.SourceCode ?? string.Empty,
                source.SourcePackage.Key,
                source.SourcePackage.DisplayName,
                string.Empty,
                string.Empty,
                BrowserFields: [],
                Relationships: [],
                Document: document,
                Resolution: EffectiveRuleResolutionView.Resolved(
                    entry.SourceEntityRevisionId,
                    entry.SourceEntityRevision.RevisionNumber));
        }

        published = await ResolvedRuleCatalogEditionMetadata.AttachAsync(dbContext, published, cancellationToken);
        published = await AttachRelationshipsAsync(published, cancellationToken);

        var fallbacks = await BuildFallbackItemsAsync(
            normalizedUserId,
            entries.Select(value => value.RuleConceptId).ToArray(),
            cancellationToken);
        var rules = published
            .Concat(fallbacks)
            .OrderBy(value => value.EntityType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.ConceptKey, StringComparer.Ordinal)
            .ToArray();

        return new ResolvedRulesCatalogView(
            "campaign", campaignId, revision.RevisionNumber, revision.PublishedAt, rules.Length, [], [], rules);
    }

    private async Task<ResolvedRuleCatalogItemView[]> BuildFallbackItemsAsync(
        string? userId,
        IReadOnlyCollection<Guid> excludedConceptIds,
        CancellationToken cancellationToken)
    {
        var concepts = dbContext.RuleConcepts.AsNoTracking().AsQueryable();
        if (excludedConceptIds.Count > 0)
        {
            concepts = concepts.Where(value => !excludedConceptIds.Contains(value.Id));
        }

        var candidates = await concepts
            .OrderBy(value => value.EntityType)
            .ThenBy(value => value.DisplayName)
            .ThenBy(value => value.Key)
            .ThenBy(value => value.Id)
            .ToArrayAsync(cancellationToken);
        if (candidates.Length == 0)
        {
            return [];
        }

        var resolved = await new EffectiveRuleFallbackResolver(dbContext).ResolveManyAsync(
            candidates,
            userId,
            cancellationToken);
        var result = new List<ResolvedRuleCatalogItemView>(resolved.Count);
        foreach (var concept in candidates)
        {
            if (!resolved.TryGetValue(concept.Id, out var fallback))
            {
                continue;
            }

            var source = fallback.Source;
            var sourceRevision = fallback.Revision;
            using var document = JsonDocument.Parse(sourceRevision.GetMechanicalContentJson());
            var resolvedDocument = document.RootElement.Clone();
            var edition = ResolvedRuleCatalogEditionMetadata.Normalize(fallback.Publication?.GameEdition);
            var effectiveEntityType = RuleConceptEntityTypes.Normalize(source.EntityType);
            result.Add(new ResolvedRuleCatalogItemView(
                concept.Id,
                concept.Key,
                effectiveEntityType,
                concept.DisplayName,
                RuleResolutionStates.UnresolvedFallback,
                HasCampaignOverride: false,
                source.Id,
                sourceRevision.Id,
                sourceRevision.RevisionNumber,
                source.Name,
                source.SourceCode ?? string.Empty,
                source.SourcePackage.Key,
                source.SourcePackage.DisplayName,
                edition,
                edition,
                BrowserFields: [],
                Relationships: [],
                Document: resolvedDocument,
                Resolution: EffectiveRuleResolutionView.UnresolvedFallback(
                    sourceRevision.Id,
                    sourceRevision.RevisionNumber)));
        }

        return await AttachRelationshipsAsync(result, cancellationToken);
    }

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

    private static JsonElement ApplyGlobalDecision(JsonElement source, GlobalRuleDecision decision) =>
        decision.DecisionKind switch
        {
            RuleDecisionKinds.JsonMergePatch => JsonMergePatch.Apply(source, decision.PatchJson),
            RuleDecisionKinds.JsonRulePatch => JsonRulePatch.Apply(source, decision.PatchJson),
            _ => source.Clone()
        };

    private static JsonElement ApplyCampaignDecision(JsonElement source, CampaignRuleDecision decision) =>
        decision.DecisionKind switch
        {
            CampaignRuleDecisionKinds.JsonMergePatch => JsonMergePatch.Apply(source, decision.PatchJson),
            CampaignRuleDecisionKinds.JsonRulePatch => JsonRulePatch.Apply(source, decision.PatchJson),
            _ => source.Clone()
        };

    private static string? NormalizeOptionalUserId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : RequireUserId(value);

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

    private sealed record RevisionReference(Guid Id, int RevisionNumber, DateTimeOffset PublishedAt);
}
