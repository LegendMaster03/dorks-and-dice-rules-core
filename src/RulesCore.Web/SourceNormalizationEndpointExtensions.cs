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
            int? offset,
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
                return Results.Ok(await normalization.GetCandidatesPageAsync(
                    authenticationContext!.User.Id,
                    entityType,
                    q,
                    limit ?? 100,
                    offset ?? 0,
                    cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return InvalidRequest(exception.Message);
            }
        });

        app.MapGet("/api/global/rules/normalization/ignored-packages", async (
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireGlobalRulesAuthority(
                httpContext,
                out _);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await new GlobalSourceDispositionService(dbContext)
                .GetIgnoredAsync(cancellationToken));
        });

        app.MapPut("/api/global/rules/normalization/packages/{sourcePackageId:guid}/ignored", async (
            Guid sourcePackageId,
            SetGlobalSourceIgnoredRequest request,
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
                var disposition = new GlobalSourceDispositionService(dbContext);
                var result = await disposition.SetIgnoredAsync(
                    sourcePackageId,
                    request,
                    authenticationContext!.User.Id,
                    cancellationToken);
                if (request.Ignored && result is null)
                {
                    return Results.NotFound();
                }

                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(new
                {
                    sourcePackageId,
                    ignored = request.Ignored,
                    value = result
                });
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
