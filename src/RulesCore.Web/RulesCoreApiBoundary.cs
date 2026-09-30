using Microsoft.AspNetCore.Routing;
using RulesCore.Application.Hosting;

namespace RulesCore.Web;

/// <summary>
/// Marker for Rules Core endpoints that are part of the stable external consumer API.
/// Every mapped /api endpoint without this marker is private by default.
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
/// Transport-level authorization for Rules Core API surfaces. Public consumer endpoints must be
/// explicitly marked. Existing public endpoints still mapped directly in Program.cs are listed
/// explicitly until they are moved into focused endpoint modules. Everything else under /api is
/// private to the Rules Wiki first-party delegation boundary.
/// </summary>
public static class RulesCoreApiBoundary
{
    public const string RulesWikiToolKey = "rules-wiki";

    private static readonly HashSet<string> LegacyProgramPublicRoutePatterns = new(
        StringComparer.Ordinal)
    {
        "/api",
        "/api/integration/session",
        "/api/sources",
        "/api/sources/entities",
        "/api/sources/entities/{entityId:guid}",
        "/api/rules/{conceptKey}",
        "/api/campaigns/{campaignId:guid}/rules/{conceptKey}"
    };

    public static bool Authorize(
        HttpContext httpContext,
        ToolHostAuthenticationContext? authenticationContext)
    {
        var endpoint = httpContext.GetEndpoint();
        if (endpoint is null || !httpContext.Request.Path.StartsWithSegments("/api"))
        {
            return true;
        }

        if (endpoint.Metadata.GetMetadata<PublicRulesCoreApiMetadata>() is not null)
        {
            return true;
        }

        if (endpoint is RouteEndpoint routeEndpoint
            && routeEndpoint.RoutePattern.RawText is { } routePattern
            && LegacyProgramPublicRoutePatterns.Contains(routePattern))
        {
            return true;
        }

        if (authenticationContext is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return false;
        }

        if (!authenticationContext.IsDelegatedFrom(RulesWikiToolKey))
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return false;
        }

        return true;
    }
}
