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
    string? ToolSlug,
    string SiteMode,
    ToolHostUserContext User,
    IReadOnlyList<string> GlobalRoles,
    IReadOnlyList<ToolHostCampaignContext> Campaigns)
{
    public string? ToolKey { get; init; }

    /// <summary>
    /// Immediate Tool-to-Tool delegation source supplied by the Site for delegated target
    /// contexts. Normal browser-to-Tool authentication contexts leave these fields null.
    /// </summary>
    public string? DelegatedFromToolKey { get; init; }
    public string? DelegatedFromToolSlug { get; init; }

    /// <summary>
    /// Effective account roles scoped to SiteMode. Null identifies an older Site payload that
    /// predates this additive version-1 field; an empty collection is an authoritative no-role result.
    /// </summary>
    public IReadOnlyList<string>? ScopedRoles { get; init; }

    public bool HasGlobalRole(string role) =>
        GlobalRoles.Contains(role, StringComparer.Ordinal);

    public bool HasScopedRole(string role) =>
        ScopedRoles?.Contains(role, StringComparer.Ordinal) == true;

    public bool HasCampaignRole(Guid campaignId, string role) =>
        Campaigns.Any(campaign =>
            campaign.Id == campaignId
            && string.Equals(campaign.Role, role, StringComparison.Ordinal));

    public bool IsDelegatedFrom(string toolKey) =>
        string.Equals(DelegatedFromToolKey, toolKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(DelegatedFromToolSlug, toolKey, StringComparison.OrdinalIgnoreCase);
}

public interface IToolHostAuthenticationClient
{
    Task<ToolHostAuthenticationContext?> RedeemAsync(
        string ticket,
        string introspectionPath,
        CancellationToken cancellationToken = default);
}
