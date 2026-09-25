using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;

namespace RulesCore.Web;

public static class HarvestingRulesEndpointExtensions
{
    public static void MapHarvestingRulesEndpoints(this WebApplication app)
    {
        app.MapGet("/api/rules/harvesting", (
            HttpContext httpContext,
            IHarvestingRulesService harvesting) =>
        {
            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(harvesting.GetCatalog());
        });

        app.MapPost("/api/rules/harvesting/resolve", async (
            HarvestingTableResolutionRequest request,
            HttpContext httpContext,
            IHarvestingRulesService harvesting,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var userId = HostedToolAuthenticationMiddleware
                    .GetAuthenticationContext(httpContext)?
                    .User.Id;
                httpContext.Response.Headers.CacheControl = "no-store";
                var result = await harvesting.ResolveGlobalAsync(
                    request,
                    userId,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidRequest(exception);
            }
            catch (InvalidOperationException exception)
            {
                return InvalidRequest(exception);
            }
        });

        app.MapPost("/api/rules/harvesting/outcome", async (
            HarvestingOutcomeRequest request,
            HttpContext httpContext,
            IHarvestingRulesService harvesting,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var userId = HostedToolAuthenticationMiddleware
                    .GetAuthenticationContext(httpContext)?
                    .User.Id;
                httpContext.Response.Headers.CacheControl = "no-store";
                var result = await harvesting.ResolveGlobalOutcomeAsync(
                    request,
                    userId,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                or KeyNotFoundException
                or InvalidOperationException
                or OverflowException)
            {
                return InvalidRequest(exception);
            }
        });

        app.MapPost("/api/campaigns/{campaignId:guid}/rules/harvesting/resolve", async (
            Guid campaignId,
            HarvestingTableResolutionRequest request,
            HttpContext httpContext,
            IHarvestingRulesService harvesting,
            CancellationToken cancellationToken) =>
        {
            var authenticationContext =
                HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
            if (authenticationContext is null)
            {
                return Results.Unauthorized();
            }
            if (!RulesAuthority.CanAccessCampaignRules(authenticationContext, campaignId))
            {
                return Results.NotFound();
            }

            try
            {
                httpContext.Response.Headers.CacheControl = "no-store";
                var result = await harvesting.ResolveCampaignAsync(
                    campaignId,
                    request,
                    authenticationContext.User.Id,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidRequest(exception);
            }
            catch (InvalidOperationException exception)
            {
                return InvalidRequest(exception);
            }
        });

        app.MapPost("/api/campaigns/{campaignId:guid}/rules/harvesting/outcome", async (
            Guid campaignId,
            HarvestingOutcomeRequest request,
            HttpContext httpContext,
            IHarvestingRulesService harvesting,
            CancellationToken cancellationToken) =>
        {
            var authenticationContext =
                HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
            if (authenticationContext is null)
            {
                return Results.Unauthorized();
            }
            if (!RulesAuthority.CanAccessCampaignRules(authenticationContext, campaignId))
            {
                return Results.NotFound();
            }

            try
            {
                httpContext.Response.Headers.CacheControl = "no-store";
                var result = await harvesting.ResolveCampaignOutcomeAsync(
                    campaignId,
                    request,
                    authenticationContext.User.Id,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                or KeyNotFoundException
                or InvalidOperationException
                or OverflowException)
            {
                return InvalidRequest(exception);
            }
        });
    }

    private static IResult InvalidRequest(Exception exception) =>
        Results.Problem(
            title: "Invalid harvesting rules request",
            detail: exception.Message,
            statusCode: StatusCodes.Status400BadRequest);
}
