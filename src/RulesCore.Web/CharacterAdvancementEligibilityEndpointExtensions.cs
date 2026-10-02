using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;

namespace RulesCore.Web;

public static class CharacterAdvancementEligibilityEndpointExtensions
{
    public static void MapCharacterAdvancementEligibilityEndpoints(this WebApplication app)
    {
        app.MapPost("/api/rules/character-advancement/eligibility", async (
            CharacterAdvancementEligibilityRequest request,
            HttpContext httpContext,
            ICharacterAdvancementEligibilityService eligibility,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var userId = HostedToolAuthenticationMiddleware
                    .GetAuthenticationContext(httpContext)?
                    .User.Id;
                httpContext.Response.Headers.CacheControl = "no-store";
                var result = await eligibility.EvaluateGlobalAsync(
                    request,
                    userId,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidEligibilityRequest(exception);
            }
            catch (InvalidOperationException exception)
            {
                return InvalidEligibilityRequest(exception);
            }
            catch (OverflowException exception)
            {
                return InvalidEligibilityRequest(exception);
            }
        }).PublicRulesCoreApi();

        app.MapPost("/api/campaigns/{campaignId:guid}/rules/character-advancement/eligibility", async (
            Guid campaignId,
            CharacterAdvancementEligibilityRequest request,
            HttpContext httpContext,
            ICharacterAdvancementEligibilityService eligibility,
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
                var result = await eligibility.EvaluateCampaignAsync(
                    campaignId,
                    request,
                    authenticationContext.User.Id,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidEligibilityRequest(exception);
            }
            catch (InvalidOperationException exception)
            {
                return InvalidEligibilityRequest(exception);
            }
            catch (OverflowException exception)
            {
                return InvalidEligibilityRequest(exception);
            }
        }).PublicRulesCoreApi();
    }

    private static IResult InvalidEligibilityRequest(Exception exception) =>
        Results.Problem(
            title: "Invalid Character advancement eligibility request",
            detail: exception.Message,
            statusCode: StatusCodes.Status400BadRequest);
}
