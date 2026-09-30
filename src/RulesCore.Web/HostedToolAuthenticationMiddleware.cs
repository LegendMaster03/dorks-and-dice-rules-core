using System.Security.Claims;
using RulesCore.Application.Hosting;

namespace RulesCore.Web;

public sealed class HostedToolAuthenticationMiddleware(RequestDelegate next)
{
    private const string ContextItemKey = "RulesCore.ToolHostAuthenticationContext";

    public async Task InvokeAsync(
        HttpContext httpContext,
        IToolHostAuthenticationClient authenticationClient)
    {
        var isRulesWikiSharedRequest = httpContext.Request.Path.StartsWithSegments(
            RulesWikiInternalApiBoundary.SharedPrefix,
            StringComparison.Ordinal);
        var tickets = httpContext.Request.Headers[ToolHostAuthenticationHeaders.Ticket];
        var introspectionPaths = httpContext.Request.Headers[ToolHostAuthenticationHeaders.IntrospectionPath];
        var hasTicketHeader = tickets.Count > 0;
        var hasIntrospectionHeader = introspectionPaths.Count > 0;

        if (!hasTicketHeader && !hasIntrospectionHeader)
        {
            if (isRulesWikiSharedRequest)
            {
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next(httpContext);
            return;
        }

        httpContext.Response.Headers.CacheControl = "no-store";
        if (!hasTicketHeader
            || !hasIntrospectionHeader
            || tickets.Count != 1
            || introspectionPaths.Count != 1
            || string.IsNullOrWhiteSpace(tickets[0])
            || string.IsNullOrWhiteSpace(introspectionPaths[0]))
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        ToolHostAuthenticationContext? authenticationContext;
        try
        {
            authenticationContext = await authenticationClient.RedeemAsync(
                tickets[0]!,
                introspectionPaths[0]!,
                httpContext.RequestAborted);
        }
        catch (InvalidOperationException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }
        catch (InvalidDataException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        catch (ArgumentException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        catch (HttpRequestException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }
        catch (OperationCanceledException) when (!httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            return;
        }

        if (authenticationContext is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (isRulesWikiSharedRequest)
        {
            if (!RulesWikiInternalApiBoundary.IsRulesWikiCaller(authenticationContext)
                || !RulesWikiInternalApiBoundary.TryMapSharedPath(httpContext.Request.Path, out var corePath))
            {
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            // The private adapter deliberately reuses the existing application endpoint handlers
            // in-process. Rules Wiki never calls the public HTTP path; the rewrite occurs only after
            // Site has authenticated the immediate delegating Tool as Rules Wiki.
            httpContext.Request.Path = corePath;
        }

        httpContext.Items[ContextItemKey] = authenticationContext;
        httpContext.User = BuildPrincipal(authenticationContext);
        await next(httpContext);
    }

    public static ToolHostAuthenticationContext? GetAuthenticationContext(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(ContextItemKey, out var value)
            ? value as ToolHostAuthenticationContext
            : null;

    private static ClaimsPrincipal BuildPrincipal(ToolHostAuthenticationContext context)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, context.User.Id),
            new(ClaimTypes.Name, context.User.DisplayName),
            new("dorks-and-dice:site-mode", context.SiteMode)
        };

        claims.AddRange(context.GlobalRoles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(context.Campaigns.Select(campaign =>
            new Claim("dorks-and-dice:campaign-role", $"{campaign.Id:D}:{campaign.Role}")));

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            authenticationType: "DorksAndDiceToolHost",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role));
    }
}
