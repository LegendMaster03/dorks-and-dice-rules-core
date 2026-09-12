using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;

namespace RulesCore.Web;

public static class GlobalRuleAuthoringEndpointExtensions
{
    public static void MapGlobalRuleAuthoringEndpoints(this WebApplication app)
    {
        // Program currently composes database-backed endpoint families through this extension.
        // Each family still owns its own authorization boundary.
        app.MapSourceAdministrationEndpoints();
        app.MapHostedSourceEndpoints();
        app.MapSourceAcquisitionEndpoints();
        app.MapSourceBrowserEndpoints();
        app.MapSourceNormalizationEndpoints();
        app.MapSourceVersioningEndpoints();
        app.MapResolvedRulesCatalogEndpoints();
        app.MapSourceRevisionReviewEndpoints();

        app.MapGet("/api/global/rules/authoring", async (
            HttpContext httpContext,
            IGlobalRulesAuthoringService authoring,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireGlobalRulesAuthority(
                httpContext,
                out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await authoring.GetOverviewAsync(
                authenticationContext!.User.Id,
                cancellationToken));
        });

        app.MapGet("/api/global/rules/authoring/concepts/{conceptId:guid}", async (
            Guid conceptId,
            HttpContext httpContext,
            IGlobalRulesAuthoringService authoring,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireGlobalRulesAuthority(
                httpContext,
                out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var concept = await authoring.GetConceptAsync(
                    conceptId,
                    authenticationContext!.User.Id,
                    cancellationToken);
                if (concept is null)
                {
                    return Results.NotFound();
                }

                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(concept);
            }
            catch (ArgumentException exception)
            {
                return Results.Problem(
                    title: "Invalid global rule authoring request",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        });
    }

    private static IResult? RequireGlobalRulesAuthority(
        HttpContext httpContext,
        out ToolHostAuthenticationContext? authenticationContext)
    {
        authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
        if (authenticationContext is null)
        {
            return Results.Unauthorized();
        }

        return RulesAuthority.CanEditGlobalRules(authenticationContext)
            ? null
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }
}