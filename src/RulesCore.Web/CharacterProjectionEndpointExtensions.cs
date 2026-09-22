using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;

namespace RulesCore.Web;

public static class CharacterProjectionEndpointExtensions
{
    public static void MapCharacterProjectionEndpoints(this WebApplication app)
    {
        app.MapPost("/api/rules/character-mechanics/resolve", async (
            CharacterRulesProjectionRequest request,
            HttpContext httpContext,
            ICharacterRulesProjectionService projection,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var userId = HostedToolAuthenticationMiddleware
                    .GetAuthenticationContext(httpContext)?
                    .User.Id;
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(await projection.ResolveGlobalAsync(
                    request,
                    userId,
                    cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return InvalidProjection(exception);
            }
            catch (InvalidOperationException exception)
            {
                return InvalidProjection(exception);
            }
            catch (OverflowException exception)
            {
                return InvalidProjection(exception);
            }
        });

        app.MapPost("/api/campaigns/{campaignId:guid}/rules/character-mechanics/resolve", async (
            Guid campaignId,
            CharacterRulesProjectionRequest request,
            HttpContext httpContext,
            ICharacterRulesProjectionService projection,
            CancellationToken cancellationToken) =>
        {
            var authenticationContext = HostedToolAuthenticationMiddleware
                .GetAuthenticationContext(httpContext);
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
                return Results.Ok(await projection.ResolveCampaignAsync(
                    campaignId,
                    request,
                    authenticationContext.User.Id,
                    cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return InvalidProjection(exception);
            }
            catch (InvalidOperationException exception)
            {
                return InvalidProjection(exception);
            }
            catch (OverflowException exception)
            {
                return InvalidProjection(exception);
            }
        });
    }

    private static IResult InvalidProjection(Exception exception) =>
        Results.Problem(
            title: "Invalid Character mechanics projection",
            detail: exception.Message,
            statusCode: StatusCodes.Status400BadRequest);
}
