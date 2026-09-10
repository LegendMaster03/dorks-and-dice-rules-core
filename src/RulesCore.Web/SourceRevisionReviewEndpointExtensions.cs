using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Web;

public static class SourceRevisionReviewEndpointExtensions
{
    public static void MapSourceRevisionReviewEndpoints(this WebApplication app)
    {
        app.MapGet("/api/global/rules/source-updates", async (
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireGlobalRulesAuthority(
                httpContext,
                out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            var review = new SourceRevisionReviewService(dbContext);
            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await review.GetPendingAsync(
                authenticationContext!.User.Id,
                cancellationToken));
        });

        app.MapGet("/api/global/rules/source-updates/{conceptId:guid}/preview", async (
            Guid conceptId,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
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
                var review = new SourceRevisionReviewService(dbContext);
                var preview = await review.PreviewAsync(
                    conceptId,
                    authenticationContext!.User.Id,
                    cancellationToken);
                if (preview is null)
                {
                    return Results.NotFound();
                }

                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(preview);
            }
            catch (ArgumentException exception)
            {
                return Results.Problem(
                    title: "Invalid source revision review request",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        });

        app.MapPost("/api/global/rules/source-updates/{conceptId:guid}/adopt", async (
            Guid conceptId,
            AdoptLatestSourceRevisionRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
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
                var review = new SourceRevisionReviewService(dbContext);
                var adopted = await review.AdoptLatestAsync(
                    conceptId,
                    request,
                    authenticationContext!.User.Id,
                    cancellationToken);
                if (adopted is null)
                {
                    return Results.NotFound();
                }

                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(adopted);
            }
            catch (ArgumentException exception)
            {
                return Results.Problem(
                    title: "Invalid source revision adoption request",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException exception)
            {
                return Results.Problem(
                    title: "Source revision adoption conflict",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status409Conflict);
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
