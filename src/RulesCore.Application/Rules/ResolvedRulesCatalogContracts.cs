namespace RulesCore.Application.Rules;

public sealed record ResolvedRulesCatalogView(
    string Scope,
    Guid? CampaignId,
    int? RevisionNumber,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<ResolvedRuleCatalogItemView> Rules);

public sealed record ResolvedRuleCatalogItemView(
    Guid RuleConceptId,
    string ConceptKey,
    string EntityType,
    string DisplayName,
    string EffectiveDecisionKind,
    bool HasCampaignOverride,
    Guid SourceEntityId,
    Guid SourceEntityRevisionId,
    int SourceRevisionNumber,
    string SourceEntityName,
    string SourceCode,
    string PackageKey,
    string PackageDisplayName,
    string EditionKey,
    string EditionDisplayName);

public interface IResolvedRulesCatalogService
{
    Task<ResolvedRulesCatalogView> GetGlobalAsync(
        string? userId,
        string? entityType = null,
        string? query = null,
        int limit = 200,
        CancellationToken cancellationToken = default);

    Task<ResolvedRulesCatalogView> GetGlobalPageAsync(
        string? userId,
        string? entityType = null,
        string? query = null,
        int limit = 200,
        int offset = 0,
        CancellationToken cancellationToken = default);

    Task<ResolvedRulesCatalogView> GetCampaignAsync(
        Guid campaignId,
        string userId,
        string? entityType = null,
        string? query = null,
        int limit = 200,
        CancellationToken cancellationToken = default);

    Task<ResolvedRulesCatalogView> GetCampaignPageAsync(
        Guid campaignId,
        string userId,
        string? entityType = null,
        string? query = null,
        int limit = 200,
        int offset = 0,
        CancellationToken cancellationToken = default);
}
