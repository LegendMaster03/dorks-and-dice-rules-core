using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Web;

public static class SourceVersioningEndpointExtensions
{
    public static void MapSourceVersioningEndpoints(this WebApplication app)
    {
        app.MapGet("/api/global/rules/versioning/entities/{sourceEntityId:guid}/candidates", async (
            Guid sourceEntityId,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireRulesLawyer(httpContext, out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var versioning = new SourceVersioningService(dbContext);
                var result = await versioning.DetectVersionsAsync(
                    sourceEntityId,
                    authenticationContext!.User.Id,
                    cancellationToken);
                if (result is null)
                {
                    return Results.NotFound();
                }
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidVersioningRequest(exception.Message);
            }
        });

        app.MapPost("/api/global/rules/versioning/entities/{sourceEntityId:guid}/bind", async (
            Guid sourceEntityId,
            BindDetectedSourceVersionRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireRulesLawyer(httpContext, out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var versioning = new SourceVersioningService(dbContext);
                var result = await versioning.BindToConceptAsync(
                    sourceEntityId,
                    request.RuleConceptId,
                    authenticationContext!.User.Id,
                    cancellationToken);
                if (result is null)
                {
                    return Results.NotFound();
                }

                await RuleAutoResolutionService.TryResolveAsync(
                    dbContext,
                    request.RuleConceptId,
                    authenticationContext.User.Id,
                    cancellationToken);

                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidVersioningRequest(exception.Message);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException exception)
            {
                return VersioningConflict(exception.Message);
            }
        });

        app.MapPost("/api/global/rules/versioning/lineage", async (
            CreateSourceLineageRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireRulesLawyer(httpContext, out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var versioning = new SourceVersioningService(dbContext);
                var result = await versioning.CreateLineageAsync(
                    request,
                    authenticationContext!.User.Id,
                    cancellationToken);
                if (result is null)
                {
                    return Results.NotFound();
                }
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidVersioningRequest(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return VersioningConflict(exception.Message);
            }
        });

        app.MapPost("/api/global/rules/versioning/lineage/{lineageId:guid}/void", async (
            Guid lineageId,
            VoidSourceLineageRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireRulesLawyer(httpContext, out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var versioning = new SourceVersioningService(dbContext);
                var result = await versioning.VoidLineageAsync(
                    lineageId,
                    request,
                    authenticationContext!.User.Id,
                    cancellationToken);
                if (result is null)
                {
                    return Results.NotFound();
                }
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidVersioningRequest(exception.Message);
            }
        });

        app.MapGet("/api/global/rules/concepts/{conceptId:guid}/consolidation", async (
            Guid conceptId,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireRulesLawyer(httpContext, out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var versioning = new SourceVersioningService(dbContext);
                var authoring = new GlobalRulesAuthoringService(dbContext);
                var consolidation = new RuleConsolidationService(dbContext, authoring, versioning);
                var result = await consolidation.GetAsync(
                    conceptId,
                    authenticationContext!.User.Id,
                    cancellationToken);
                if (result is null)
                {
                    return Results.NotFound();
                }
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidVersioningRequest(exception.Message);
            }
        });
    }

    private static IResult? RequireRulesLawyer(
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

    private static IResult InvalidVersioningRequest(string detail) =>
        Results.Problem(
            title: "Invalid source versioning request",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult VersioningConflict(string detail) =>
        Results.Problem(
            title: "Source versioning conflict",
            detail: detail,
            statusCode: StatusCodes.Status409Conflict);
}