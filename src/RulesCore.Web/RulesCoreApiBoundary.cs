using Microsoft.AspNetCore.Routing;
using RulesCore.Application.Hosting;

namespace RulesCore.Web;

public enum RulesCoreApiSurfaceMode
{
    Combined,
    PublicOnly,
    PrivateOnly
}

/// <summary>
/// Marker for Rules Core endpoints that are deliberately part of the stable consumer API.
/// Unmarked /api endpoints are private by default unless they are one of the existing public route
/// patterns retained below.
/// </summary>
public sealed class PublicRulesCoreApiMetadata
{
    private PublicRulesCoreApiMetadata()
    {
    }

    public static PublicRulesCoreApiMetadata Instance { get; } = new();
}

public static class RulesCoreApiEndpointConventionExtensions
{
    public static RouteHandlerBuilder PublicRulesCoreApi(this RouteHandlerBuilder builder) =>
        builder.WithMetadata(PublicRulesCoreApiMetadata.Instance);
}

/// <summary>
/// Transport-level separation between the stable Rules Core consumer API and first-party private
/// API surfaces. Existing public routes are enumerated explicitly. Any new /api route is private
/// unless it is deliberately marked public or added to the public route set.
///
/// Private access requires a target-scoped authentication context that the Site created through an
/// explicitly configured private tunnel. Ordinary Tool delegation provenance is intentionally not
/// sufficient.
/// </summary>
public static class RulesCoreApiBoundary
{
    public const string ApiSurfaceConfigurationKey = "RulesCore:ApiSurface";

    private static readonly HashSet<string> PublicRoutePatterns = new(StringComparer.Ordinal)
    {
        "/api",
        "/api/integration/session",
        "/api/sources",
        "/api/sources/entities",
        "/api/sources/entities/{entityId:guid}",
        "/api/rules",
        "/api/rules/{conceptKey}",
        "/api/campaigns/{campaignId:guid}/rules",
        "/api/campaigns/{campaignId:guid}/rules/{conceptKey}",
        "/api/rules/mechanics",
        "/api/rules/mechanics/support",
        "/api/rules/mechanics/recovery/{procedureKey}/resolve",
        "/api/rules/character-mechanics/resolve",
        "/api/rules/mechanics/evaluate",
        "/api/rules/mechanics/{mechanicKey}/evaluate",
        "/api/campaigns/{campaignId:guid}/rules/mechanics",
        "/api/campaigns/{campaignId:guid}/rules/mechanics/support",
        "/api/campaigns/{campaignId:guid}/rules/mechanics/recovery/{procedureKey}/resolve",
        "/api/campaigns/{campaignId:guid}/rules/character-mechanics/resolve",
        "/api/campaigns/{campaignId:guid}/rules/mechanics/evaluate",
        "/api/campaigns/{campaignId:guid}/rules/mechanics/{mechanicKey}/evaluate",
        "/api/rules/crafting/manufacturing/resolve",
        "/api/rules/crafting/enchanting/resolve",
        "/api/campaigns/{campaignId:guid}/rules/crafting/manufacturing/resolve",
        "/api/campaigns/{campaignId:guid}/rules/crafting/enchanting/resolve",
        "/api/rules/harvesting",
        "/api/rules/harvesting/resolve",
        "/api/rules/harvesting/outcome",
        "/api/campaigns/{campaignId:guid}/rules/harvesting/resolve",
        "/api/campaigns/{campaignId:guid}/rules/harvesting/outcome",
        "/api/rules/travel-environment",
        "/api/rules/travel-environment/{mechanicKey}/resolve",
        "/api/campaigns/{campaignId:guid}/rules/travel-environment",
        "/api/campaigns/{campaignId:guid}/rules/travel-environment/{mechanicKey}/resolve"
    };

    public static RulesCoreApiSurfaceMode ResolveMode(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var configured = configuration[ApiSurfaceConfigurationKey];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return RulesCoreApiSurfaceMode.Combined;
        }

        if (Enum.TryParse<RulesCoreApiSurfaceMode>(configured, ignoreCase: true, out var mode)
            && Enum.IsDefined(mode))
        {
            return mode;
        }

        throw new InvalidOperationException(
            $"{ApiSurfaceConfigurationKey} must be Combined, PublicOnly, or PrivateOnly.");
    }

    public static bool Authorize(
        HttpContext httpContext,
        ToolHostAuthenticationContext? authenticationContext,
        RulesCoreApiSurfaceMode surfaceMode)
    {
        var endpoint = httpContext.GetEndpoint();
        if (endpoint is null || !httpContext.Request.Path.StartsWithSegments("/api"))
        {
            return true;
        }

        var isPublic = IsPublicEndpoint(endpoint);

        if (surfaceMode == RulesCoreApiSurfaceMode.PublicOnly && !isPublic)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return false;
        }

        if (surfaceMode == RulesCoreApiSurfaceMode.PrivateOnly && isPublic)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return false;
        }

        if (isPublic)
        {
            return true;
        }

        if (authenticationContext is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return false;
        }

        if (!authenticationContext.HasPrivateTunnelSource)
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return false;
        }

        return true;
    }

    private static bool IsPublicEndpoint(Endpoint endpoint)
    {
        if (endpoint.Metadata.GetMetadata<PublicRulesCoreApiMetadata>() is not null)
        {
            return true;
        }

        return endpoint is RouteEndpoint routeEndpoint
            && routeEndpoint.RoutePattern.RawText is { } routePattern
            && PublicRoutePatterns.Contains(routePattern);
    }
}
