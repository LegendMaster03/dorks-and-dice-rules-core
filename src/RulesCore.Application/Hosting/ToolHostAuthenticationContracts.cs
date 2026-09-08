namespace RulesCore.Application.Hosting;

public static class ToolHostAuthenticationHeaders
{
    public const string Ticket = "X-Dorks-Tool-Auth-Ticket";
    public const string IntrospectionPath = "X-Dorks-Tool-Auth-Introspection-Path";
}

public sealed record ToolHostUserContext(
    string Id,
    string DisplayName);

public sealed record ToolHostCampaignContext(
    Guid Id,
    string Name,
    string Role);

public sealed record ToolHostAuthenticationContext(
    int ContractVersion,
    string ToolSlug,
    string SiteMode,
    ToolHostUserContext User,
    IReadOnlyList<string> GlobalRoles,
    IReadOnlyList<ToolHostCampaignContext> Campaigns)
{
    public bool HasGlobalRole(string role) =>
        GlobalRoles.Contains(role, StringComparer.Ordinal);

    public bool HasCampaignRole(Guid campaignId, string role) =>
        Campaigns.Any(campaign =>
            campaign.Id == campaignId
            && string.Equals(campaign.Role, role, StringComparison.Ordinal));
}

public interface IToolHostAuthenticationClient
{
    Task<ToolHostAuthenticationContext?> RedeemAsync(
        string ticket,
        string introspectionPath,
        CancellationToken cancellationToken = default);
}
