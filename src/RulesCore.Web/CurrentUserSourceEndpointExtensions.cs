using System.Text.Json;
using RulesCore.Application.Hosting;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Web;

public static class CurrentUserSourceEndpointExtensions
{
    private const string DorksAndDiceMode = "dorks-and-dice";

    public static void MapCurrentUserSourceEndpoints(this WebApplication app)
    {
        app.MapGet("/api/sources/current-user", async (
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            ISourceImportService importer,
            ISourceGrantService grants,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireSignedInDorksAndDiceAccount(
                httpContext,
                out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            var service = new CurrentUserSourceService(dbContext, importer, grants);
            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await service.ListAsync(
                authenticationContext!.User.Id,
                cancellationToken));
        });

        app.MapPost("/api/sources/current-user", async (
            AddCurrentUserSourceRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            ISourceImportService importer,
            ISourceGrantService grants,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireSignedInDorksAndDiceAccount(
                httpContext,
                out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var service = new CurrentUserSourceService(dbContext, importer, grants);
                var source = await service.AddAsync(
                    authenticationContext!.User.Id,
                    request,
                    cancellationToken);

                if (string.Equals(source.Kind, CurrentUserSourceKinds.Web, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(source.Url))
                {
                    var refresh = new CurrentUserWebSourceRefreshService(
                        dbContext,
                        importer,
                        grants);
                    await refresh.RecordInitialVersionAsync(
                        source.Id,
                        source.Url,
                        cancellationToken);
                }

                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(source);
            }
            catch (ArgumentException exception)
            {
                return InvalidSource(exception.Message);
            }
            catch (InvalidDataException exception)
            {
                return InvalidSource(exception.Message);
            }
            catch (JsonException exception)
            {
                return InvalidSource(exception.Message);
            }
            catch (HttpRequestException exception)
            {
                return Results.Problem(
                    title: "Web source unavailable",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            }
            catch (InvalidOperationException exception)
            {
                return Results.Problem(
                    title: "Source could not be added",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status409Conflict);
            }
        });

        app.MapPost("/api/sources/current-user/{currentUserSourceId:guid}/refresh", async (
            Guid currentUserSourceId,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            ISourceImportService importer,
            ISourceGrantService grants,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireSignedInDorksAndDiceAccount(
                httpContext,
                out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var refresh = new CurrentUserWebSourceRefreshService(
                    dbContext,
                    importer,
                    grants);
                var source = await refresh.RefreshOneAsync(
                    authenticationContext!.User.Id,
                    currentUserSourceId,
                    cancellationToken);
                if (source is null)
                {
                    return Results.NotFound();
                }
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(source);
            }
            catch (ArgumentException exception)
            {
                return InvalidSource(exception.Message);
            }
            catch (InvalidDataException exception)
            {
                return InvalidSource(exception.Message);
            }
            catch (JsonException exception)
            {
                return InvalidSource(exception.Message);
            }
            catch (HttpRequestException exception)
            {
                return Results.Problem(
                    title: "Web source unavailable",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            }
            catch (InvalidOperationException exception)
            {
                return Results.Problem(
                    title: "Source could not be refreshed",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status409Conflict);
            }
        });
    }

    private static IResult? RequireSignedInDorksAndDiceAccount(
        HttpContext httpContext,
        out ToolHostAuthenticationContext? authenticationContext)
    {
        authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
        if (authenticationContext is null)
        {
            return Results.Unauthorized();
        }
        return string.Equals(
                authenticationContext.SiteMode,
                DorksAndDiceMode,
                StringComparison.Ordinal)
            ? null
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static IResult InvalidSource(string detail) =>
        Results.Problem(
            title: "Invalid source",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);
}
