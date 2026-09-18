using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;

namespace RulesCore.Web;

public static class CharacterMechanicsEndpointExtensions
{
    public static void MapCharacterMechanicsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/rules/mechanics", async (
            bool? includeUnavailable,
            HttpContext httpContext,
            ICharacterMechanicsConsumerService mechanics,
            CancellationToken cancellationToken) =>
        {
            var userId = HostedToolAuthenticationMiddleware
                .GetAuthenticationContext(httpContext)?
                .User.Id;
            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await mechanics.GetGlobalAsync(
                userId,
                includeUnavailable ?? false,
                cancellationToken));
        });

        app.MapPost("/api/rules/mechanics/evaluate", async (
            CharacterMechanicsBatchEvaluationRequest request,
            HttpContext httpContext,
            ICharacterMechanicsConsumerService mechanics,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var userId = HostedToolAuthenticationMiddleware
                    .GetAuthenticationContext(httpContext)?
                    .User.Id;
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(await mechanics.EvaluateGlobalBatchAsync(
                    request,
                    userId,
                    cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return InvalidEvaluation(exception);
            }
            catch (KeyNotFoundException exception)
            {
                return InvalidEvaluation(exception);
            }
            catch (InvalidOperationException exception)
            {
                return InvalidEvaluation(exception);
            }
            catch (OverflowException exception)
            {
                return InvalidEvaluation(exception);
            }
        });

        app.MapPost("/api/rules/mechanics/{mechanicKey}/evaluate", async (
            string mechanicKey,
            CharacterMechanicEvaluationRequest request,
            HttpContext httpContext,
            ICharacterMechanicsConsumerService mechanics,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var userId = HostedToolAuthenticationMiddleware
                    .GetAuthenticationContext(httpContext)?
                    .User.Id;
                httpContext.Response.Headers.CacheControl = "no-store";
                var result = await mechanics.EvaluateGlobalAsync(
                    mechanicKey,
                    request,
                    userId,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidEvaluation(exception);
            }
            catch (KeyNotFoundException exception)
            {
                return InvalidEvaluation(exception);
            }
            catch (InvalidOperationException exception)
            {
                return InvalidEvaluation(exception);
            }
            catch (OverflowException exception)
            {
                return InvalidEvaluation(exception);
            }
        });

        app.MapGet("/api/campaigns/{campaignId:guid}/rules/mechanics", async (
            Guid campaignId,
            bool? includeUnavailable,
            HttpContext httpContext,
            ICharacterMechanicsConsumerService mechanics,
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
            return Results.Ok(await mechanics.GetCampaignAsync(
                campaignId,
                authenticationContext!.User.Id,
                includeUnavailable ?? false,
                cancellationToken));
        });

        app.MapPost("/api/campaigns/{campaignId:guid}/rules/mechanics/evaluate", async (
            Guid campaignId,
            CharacterMechanicsBatchEvaluationRequest request,
            HttpContext httpContext,
            ICharacterMechanicsConsumerService mechanics,
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
                return Results.Ok(await mechanics.EvaluateCampaignBatchAsync(
                    campaignId,
                    request,
                    authenticationContext!.User.Id,
                    cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return InvalidEvaluation(exception);
            }
            catch (KeyNotFoundException exception)
            {
                return InvalidEvaluation(exception);
            }
            catch (InvalidOperationException exception)
            {
                return InvalidEvaluation(exception);
            }
            catch (OverflowException exception)
            {
                return InvalidEvaluation(exception);
            }
        });

        app.MapPost("/api/campaigns/{campaignId:guid}/rules/mechanics/{mechanicKey}/evaluate", async (
            Guid campaignId,
            string mechanicKey,
            CharacterMechanicEvaluationRequest request,
            HttpContext httpContext,
            ICharacterMechanicsConsumerService mechanics,
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
                var result = await mechanics.EvaluateCampaignAsync(
                    campaignId,
                    mechanicKey,
                    request,
                    authenticationContext!.User.Id,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidEvaluation(exception);
            }
            catch (KeyNotFoundException exception)
            {
                return InvalidEvaluation(exception);
            }
            catch (InvalidOperationException exception)
            {
                return InvalidEvaluation(exception);
            }
            catch (OverflowException exception)
            {
                return InvalidEvaluation(exception);
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

    private static IResult InvalidEvaluation(Exception exception) =>
        Results.Problem(
            title: "Invalid mechanic evaluation",
            detail: exception.Message,
            statusCode: StatusCodes.Status400BadRequest);
}
