using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Web;

public static class SourceNormalizationEndpointExtensions
{
    public static void MapSourceNormalizationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/global/rules/normalization/candidates", async (
            string? entityType,
            string? q,
            int? limit,
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
                var normalization = new SourceNormalizationService(dbContext);
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(await normalization.GetCandidatesAsync(
                    authenticationContext!.User.Id,
                    entityType,
                    q,
                    limit ?? 100,
                    cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return InvalidRequest(exception.Message);
            }
        });

        app.MapPost("/api/global/rules/normalization/entities/{sourceEntityId:guid}/accept", async (
            Guid sourceEntityId,
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
                var normalization = new SourceNormalizationService(dbContext);
                var result = await normalization.AcceptAsync(
                    sourceEntityId,
                    authenticationContext!.User.Id,
                    cancellationToken);
                if (result is null)
                {
                    return Results.NotFound();
                }

                await RuleAutoResolutionService.TryResolveAsync(
                    dbContext,
                    result.Concept.Id,
                    authenticationContext.User.Id,
                    cancellationToken);

                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidRequest(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return Results.Problem(
                    title: "Source normalization conflict",
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

    private static IResult InvalidRequest(string detail) =>
        Results.Problem(
            title: "Invalid source normalization request",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);
}