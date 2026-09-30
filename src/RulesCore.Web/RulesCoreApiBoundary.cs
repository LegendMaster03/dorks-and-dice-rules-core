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
/// Enforces the transport-level boundary between Rules Core's public consumer API and the
/// private first-party Rules Wiki API. Private endpoints require a Site-issued delegated target
/// context whose immediate source is Rules Wiki. End-user/domain authorization remains the
/// responsibility of the endpoint and underlying services.
/// </summary>
public sealed class RulesCoreApiBoundaryMiddleware(RequestDelegate next)
{
    public const string RulesWikiToolKey = "rules-wiki";

    public async Task InvokeAsync(HttpContext httpContext)
    {
        var endpoint = httpContext.GetEndpoint();
        if (endpoint is null || !httpContext.Request.Path.StartsWithSegments("/api"))
        {
            await next(httpContext);
            return;
        }

        if (endpoint.Metadata.GetMetadata<PublicRulesCoreApiMetadata>() is not null)
        {
            await next(httpContext);
            return;
        }

        var authenticationContext =
            HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
        if (authenticationContext is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!authenticationContext.IsDelegatedFrom(RulesWikiToolKey))
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(httpContext);
    }
}
