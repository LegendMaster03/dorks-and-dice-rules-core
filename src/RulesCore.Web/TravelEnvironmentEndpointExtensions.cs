using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;

namespace RulesCore.Web;

public static class TravelEnvironmentEndpointExtensions
{
    public static void MapTravelEnvironmentEndpoints(this WebApplication app)
    {
        app.MapGet("/api/rules/travel-environment", async (
            HttpContext httpContext,
            ITravelEnvironmentConsumerService travel,
            CancellationToken cancellationToken) =>
        {
            var userId = HostedToolAuthenticationMiddleware
                .GetAuthenticationContext(httpContext)?
                .User.Id;
            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await travel.GetGlobalAsync(userId, cancellationToken));
        });

        app.MapPost("/api/rules/travel-environment/{mechanicKey}/resolve", async (
            string mechanicKey,
            TravelEnvironmentResolutionRequest request,
            HttpContext httpContext,
            ITravelEnvironmentConsumerService travel,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var userId = HostedToolAuthenticationMiddleware
                    .GetAuthenticationContext(httpContext)?
                    .User.Id;
                httpContext.Response.Headers.CacheControl = "no-store";
                var result = await travel.ResolveGlobalAsync(
                    mechanicKey,
                    request,
                    userId,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidTravelRequest(exception);
            }
            catch (InvalidOperationException exception)
            {
                return InvalidTravelRequest(exception);
            }
            catch (OverflowException exception)
            {
                return InvalidTravelRequest(exception);
            }
        });

        app.MapGet("/api/campaigns/{campaignId:guid}/rules/travel-environment", async (
            Guid campaignId,
            HttpContext httpContext,
            ITravelEnvironmentConsumerService travel,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireCampaignRead(
                httpContext,
                campaignId,
                out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await travel.GetCampaignAsync(
                campaignId,
                authenticationContext!.User.Id,
                cancellationToken));
        });

        app.MapPost("/api/campaigns/{campaignId:guid}/rules/travel-environment/{mechanicKey}/resolve", async (
            Guid campaignId,
            string mechanicKey,
            TravelEnvironmentResolutionRequest request,
            HttpContext httpContext,
            ITravelEnvironmentConsumerService travel,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireCampaignRead(
                httpContext,
                campaignId,
                out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                httpContext.Response.Headers.CacheControl = "no-store";
                var result = await travel.ResolveCampaignAsync(
                    campaignId,
                    mechanicKey,
                    request,
                    authenticationContext!.User.Id,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidTravelRequest(exception);
            }
            catch (InvalidOperationException exception)
            {
                return InvalidTravelRequest(exception);
            }
            catch (OverflowException exception)
            {
                return InvalidTravelRequest(exception);
            }
        });
    }

    private static IResult? RequireCampaignRead(
        HttpContext httpContext,
        Guid campaignId,
        out ToolHostAuthenticationContext? authenticationContext)
    {
        authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
        if (authenticationContext is null)
        {
            return Results.Unauthorized();
        }

        return RulesAuthority.CanAccessCampaignRules(authenticationContext, campaignId)
            ? null
            : Results.NotFound();
    }

    private static IResult InvalidTravelRequest(Exception exception) =>
        Results.Problem(
            title: "Invalid travel/environment mechanic request",
            detail: exception.Message,
            statusCode: StatusCodes.Status400BadRequest);
}
