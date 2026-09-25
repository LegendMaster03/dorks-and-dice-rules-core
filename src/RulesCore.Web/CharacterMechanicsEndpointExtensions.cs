using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Web;

public static class CharacterMechanicsEndpointExtensions
{
    public static void MapCharacterMechanicsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/rules/mechanics", async (
            bool? includeUnavailable,
            HttpContext httpContext,
            CharacterMechanicsConsumerService mechanics,
            CancellationToken cancellationToken) =>
        {
            var userId = HostedToolAuthenticationMiddleware
                .GetAuthenticationContext(httpContext)?
                .User.Id;
            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await mechanics.GetGlobalEffectiveAsync(
                userId,
                includeUnavailable ?? false,
                cancellationToken));
        });

        app.MapGet("/api/admin/rules/mechanics", async (
            bool? includeUnavailable,
            HttpContext httpContext,
            CharacterMechanicsConsumerService mechanics,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireGlobalEdit(
                httpContext,
                out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await mechanics.GetGlobalAsync(
                authenticationContext!.User.Id,
                includeUnavailable ?? false,
                cancellationToken));
        });

        app.MapGet("/api/rules/mechanics/support", async (
            HttpContext httpContext,
            ICharacterMechanicsConsumerService mechanics,
            CancellationToken cancellationToken) =>
        {
            var userId = HostedToolAuthenticationMiddleware
                .GetAuthenticationContext(httpContext)?
                .User.Id;
            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await mechanics.ProjectGlobalSupportAsync(
                new CharacterSupportProjectionRequest(),
                userId,
                cancellationToken));
        });

        app.MapPost("/api/rules/mechanics/support", async (
            CharacterSupportProjectionRequest request,
            HttpContext httpContext,
            ICharacterMechanicsConsumerService mechanics,
            CancellationToken cancellationToken) =>
        {
            var userId = HostedToolAuthenticationMiddleware
                .GetAuthenticationContext(httpContext)?
                .User.Id;
            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await mechanics.ProjectGlobalSupportAsync(
                request,
                userId,
                cancellationToken));
        });

        app.MapPost("/api/rules/mechanics/recovery/{procedureKey}/resolve", async (
            string procedureKey,
            CharacterRecoveryResolutionRequest request,
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
                var result = await mechanics.ResolveGlobalRecoveryAsync(
                    procedureKey,
                    request,
                    userId,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidSupportRequest(exception);
            }
        });

        app.MapPost("/api/rules/mechanics/evaluate", async (
            CharacterMechanicsBatchEvaluationRequest request,
            HttpContext httpContext,
            CharacterMechanicsConsumerService mechanics,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var userId = HostedToolAuthenticationMiddleware
                    .GetAuthenticationContext(httpContext)?
                    .User.Id;
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(await mechanics.EvaluateGlobalEffectiveBatchAsync(
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
            CharacterMechanicsConsumerService mechanics,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var userId = HostedToolAuthenticationMiddleware
                    .GetAuthenticationContext(httpContext)?
                    .User.Id;
                httpContext.Response.Headers.CacheControl = "no-store";
                var result = await mechanics.EvaluateGlobalEffectiveAsync(
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
            CharacterMechanicsConsumerService mechanics,
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
            return Results.Ok(await mechanics.GetCampaignEffectiveAsync(
                campaignId,
                authenticationContext!.User.Id,
                includeUnavailable ?? false,
                cancellationToken));
        });

        app.MapGet("/api/campaigns/{campaignId:guid}/admin/rules/mechanics", async (
            Guid campaignId,
            bool? includeUnavailable,
            HttpContext httpContext,
            CharacterMechanicsConsumerService mechanics,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireCampaignEdit(
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

        app.MapGet("/api/campaigns/{campaignId:guid}/rules/mechanics/support", async (
            Guid campaignId,
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
            return Results.Ok(await mechanics.ProjectCampaignSupportAsync(
                campaignId,
                new CharacterSupportProjectionRequest(),
                authenticationContext!.User.Id,
                cancellationToken));
        });

        app.MapPost("/api/campaigns/{campaignId:guid}/rules/mechanics/support", async (
            Guid campaignId,
            CharacterSupportProjectionRequest request,
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
            return Results.Ok(await mechanics.ProjectCampaignSupportAsync(
                campaignId,
                request,
                authenticationContext!.User.Id,
                cancellationToken));
        });

        app.MapPost("/api/campaigns/{campaignId:guid}/rules/mechanics/recovery/{procedureKey}/resolve", async (
            Guid campaignId,
            string procedureKey,
            CharacterRecoveryResolutionRequest request,
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
                var result = await mechanics.ResolveCampaignRecoveryAsync(
                    campaignId,
                    procedureKey,
                    request,
                    authenticationContext!.User.Id,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidSupportRequest(exception);
            }
        });

        app.MapPost("/api/campaigns/{campaignId:guid}/rules/mechanics/evaluate", async (
            Guid campaignId,
            CharacterMechanicsBatchEvaluationRequest request,
            HttpContext httpContext,
            CharacterMechanicsConsumerService mechanics,
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
                return Results.Ok(await mechanics.EvaluateCampaignEffectiveBatchAsync(
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
            CharacterMechanicsConsumerService mechanics,
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
                var result = await mechanics.EvaluateCampaignEffectiveAsync(
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

        app.MapCharacterProjectionEndpoints();
    }

    private static IResult? RequireGlobalEdit(
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

    private static IResult? RequireCampaignEdit(
        HttpContext httpContext,
        Guid campaignId,
        out ToolHostAuthenticationContext? authenticationContext)
    {
        var readFailure = RequireCampaignRead(httpContext, campaignId, out authenticationContext);
        if (readFailure is not null)
        {
            return readFailure;
        }

        return RulesAuthority.CanEditCampaignRules(authenticationContext!, campaignId)
            ? null
            : Results.StatusCode(StatusCodes.Status403Forbidden);
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

    private static IResult InvalidSupportRequest(Exception exception) =>
        Results.Problem(
            title: "Invalid Character support request",
            detail: exception.Message,
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult InvalidEvaluation(Exception exception) =>
        Results.Problem(
            title: "Invalid mechanic evaluation",
            detail: exception.Message,
            statusCode: StatusCodes.Status400BadRequest);
}
