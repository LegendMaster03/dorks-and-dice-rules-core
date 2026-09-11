using RulesCore.Application.Hosting;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Web;

public static class SourceAcquisitionEndpointExtensions
{
    public static void MapSourceAcquisitionEndpoints(this WebApplication app)
    {
        app.MapGet("/api/source-admin/acquisitions", async (
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireSourceAdministrationAuthority(
                httpContext,
                out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var service = new SourceAcquisitionService(dbContext);
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(await service.GetCurrentUserAsync(
                    authenticationContext!.User.Id,
                    cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return InvalidRequest(exception.Message);
            }
        });

        app.MapPost("/api/source-admin/packages/{sourcePackageId:guid}/current-user-acquisitions", async (
            Guid sourcePackageId,
            RecordCurrentUserSourceAcquisitionRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireSourceAdministrationAuthority(
                httpContext,
                out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var service = new SourceAcquisitionService(dbContext);
                var result = await service.RecordCurrentUserAsync(
                    authenticationContext!.User.Id,
                    sourcePackageId,
                    request,
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
                return InvalidRequest(exception.Message);
            }
        });

        app.MapPost("/api/source-admin/acquisitions/{sourceAcquisitionId:guid}/void", async (
            Guid sourceAcquisitionId,
            VoidCurrentUserSourceAcquisitionRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireSourceAdministrationAuthority(
                httpContext,
                out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var service = new SourceAcquisitionService(dbContext);
                var result = await service.VoidCurrentUserAsync(
                    authenticationContext!.User.Id,
                    sourceAcquisitionId,
                    request,
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
                return InvalidRequest(exception.Message);
            }
        });
    }

    private static IResult? RequireSourceAdministrationAuthority(
        HttpContext httpContext,
        out ToolHostAuthenticationContext? authenticationContext)
    {
        authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
        if (authenticationContext is null)
        {
            return Results.Unauthorized();
        }

        return SourceAdministrationAuthority.CanAdministerSources(authenticationContext)
            ? null
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static IResult InvalidRequest(string detail) =>
        Results.Problem(
            title: "Invalid source acquisition request",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);
}
