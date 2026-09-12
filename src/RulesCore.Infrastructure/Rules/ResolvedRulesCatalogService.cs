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

    public async Task<ResolvedRulesCatalogView> GetGlobalPageAsync(
        string? userId,
        string? entityType = null,
        string? query = null,
        int limit = 200,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        ValidateOffset(offset);
        var normalizedUserId = NormalizeOptionalUserId(userId);
        var normalizedEntityType = NormalizeOptional(entityType)?.ToLowerInvariant();
        var normalizedQuery = NormalizeOptional(query)?.ToLowerInvariant();

        var revision = await dbContext.RulesetRevisions
            .AsNoTracking()
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => new RevisionReference(value.Id, value.RevisionNumber, value.PublishedAt))
            .FirstOrDefaultAsync(cancellationToken);
        if (revision is null)
        {
            return new ResolvedRulesCatalogView("global", null, null, null, []);
        }

        var entries = dbContext.RulesetRevisionEntries
            .AsNoTracking()
            .Where(value => value.RulesetRevisionId == revision.Id)
            .Where(value => value.SourceEntityRevision.SourceEntity.SourceEdition.SourceWork.SourcePackage.IsPublic
                || (normalizedUserId != null
                    && value.SourceEntityRevision.SourceEntity.SourceEdition.SourceWork.SourcePackage.UserGrants
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
                || value.SourceEntityRevision.SourceEntity.SourceCode.ToLower().Contains(normalizedQuery)
                || value.SourceEntityRevision.SourceEntity.SourceEdition.DisplayName.ToLower().Contains(normalizedQuery)
                || value.SourceEntityRevision.SourceEntity.SourceEdition.SourceWork.SourcePackage.DisplayName
                    .ToLower().Contains(normalizedQuery));
        }

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
                value.SourceEntityRevision.SourceEntity.SourceCode,
                value.SourceEntityRevision.SourceEntity.SourceEdition.SourceWork.SourcePackage.Key,
                value.SourceEntityRevision.SourceEntity.SourceEdition.SourceWork.SourcePackage.DisplayName,
                value.SourceEntityRevision.SourceEntity.SourceEdition.Key,
                value.SourceEntityRevision.SourceEntity.SourceEdition.DisplayName))
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        return new ResolvedRulesCatalogView(
            "global",
            null,
            revision.RevisionNumber,
            revision.PublishedAt,
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

    public async Task<ResolvedRulesCatalogView> GetCampaignPageAsync(
        Guid campaignId,
        string userId,
        string? entityType = null,
        string? query = null,
        int limit = 200,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        RequireGuid(campaignId, nameof(campaignId));
        ValidateLimit(limit);
        ValidateOffset(offset);
        var normalizedUserId = RequireUserId(userId);
        var normalizedEntityType = NormalizeOptional(entityType)?.ToLowerInvariant();
        var normalizedQuery = NormalizeOptional(query)?.ToLowerInvariant();

        var revision = await dbContext.CampaignRulesetRevisions
            .AsNoTracking()
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => new RevisionReference(value.Id, value.RevisionNumber, value.PublishedAt))
            .FirstOrDefaultAsync(cancellationToken);
        if (revision is null)
        {
            return new ResolvedRulesCatalogView("campaign", campaignId, null, null, []);
        }

        var entries = dbContext.CampaignRulesetRevisionEntries
            .AsNoTracking()
            .Where(value => value.CampaignRulesetRevisionId == revision.Id)
            .Where(value => value.SourceEntityRevision.SourceEntity.SourceEdition.SourceWork.SourcePackage.IsPublic
                || value.SourceEntityRevision.SourceEntity.SourceEdition.SourceWork.SourcePackage.UserGrants
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
                || value.SourceEntityRevision.SourceEntity.SourceCode.ToLower().Contains(normalizedQuery)
                || value.SourceEntityRevision.SourceEntity.SourceEdition.DisplayName.ToLower().Contains(normalizedQuery)
                || value.SourceEntityRevision.SourceEntity.SourceEdition.SourceWork.SourcePackage.DisplayName
                    .ToLower().Contains(normalizedQuery));
        }

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
                value.SourceEntityRevision.SourceEntity.SourceCode,
                value.SourceEntityRevision.SourceEntity.SourceEdition.SourceWork.SourcePackage.Key,
                value.SourceEntityRevision.SourceEntity.SourceEdition.SourceWork.SourcePackage.DisplayName,
                value.SourceEntityRevision.SourceEntity.SourceEdition.Key,
                value.SourceEntityRevision.SourceEntity.SourceEdition.DisplayName))
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        return new ResolvedRulesCatalogView(
            "campaign",
            campaignId,
            revision.RevisionNumber,
            revision.PublishedAt,
            rules);
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
