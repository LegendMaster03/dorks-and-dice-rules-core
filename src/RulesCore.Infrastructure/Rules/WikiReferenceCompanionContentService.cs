using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Rules;

public sealed class WikiReferenceCompanionContentService(RulesCoreDbContext dbContext)
{
    public async Task<WikiReferenceCompanionContentCollectionView?> GetGlobalAsync(
        string? userId,
        string referenceIdentity,
        CancellationToken cancellationToken = default)
    {
        var detail = await new WikiReferenceCatalogService(dbContext)
            .GetGlobalDetailAsync(userId, referenceIdentity, cancellationToken);
        return detail is null
            ? null
            : await BuildAsync("global", null, userId, detail, cancellationToken);
    }

    public async Task<WikiReferenceCompanionContentCollectionView?> GetCampaignAsync(
        Guid campaignId,
        string userId,
        string referenceIdentity,
        CancellationToken cancellationToken = default)
    {
        var detail = await new WikiReferenceCatalogService(dbContext)
            .GetCampaignDetailAsync(campaignId, userId, referenceIdentity, cancellationToken);
        return detail is null
            ? null
            : await BuildAsync("campaign", campaignId, userId, detail, cancellationToken);
    }

    private async Task<WikiReferenceCompanionContentCollectionView> BuildAsync(
        string scope,
        Guid? campaignId,
        string? userId,
        WikiReferenceDetailView detail,
        CancellationToken cancellationToken)
    {
        var entityIds = detail.Variations.Select(value => value.SourceEntityId).Distinct().ToArray();
        var rows = await new SourceCompanionContentStore(dbContext)
            .ReadAccessibleForSourceEntitiesAsync(entityIds, userId, cancellationToken);
        var contents = rows.Select(value => new WikiReferenceCompanionContentView(
                value.Id,
                value.CompanionKind,
                value.Name,
                value.SourceCode,
                value.PackageKey,
                value.PackageDisplayName,
                value.SourceRepresentationId,
                value.SourceEntityId,
                value.SourceEntityRevisionId,
                value.EvidenceKind,
                value.ContentSha256,
                JsonDocument.Parse(value.RawJson).RootElement.Clone()))
            .ToArray();
        return new WikiReferenceCompanionContentCollectionView(
            scope,
            campaignId,
            detail.Reference.ReferenceIdentity,
            contents);
    }
}
