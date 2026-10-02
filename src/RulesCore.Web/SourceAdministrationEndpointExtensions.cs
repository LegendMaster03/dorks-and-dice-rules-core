using System.Text.Json;
using RulesCore.Application.Hosting;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Web;

public static class SourceAdministrationEndpointExtensions
{
    public static void MapSourceAdministrationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/source-admin/import/reconciliation", async (
            HttpContext httpContext,
            CanonicalDataReconciliationMaintenanceJob maintenance,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireCorpusReconciliationAuthority(httpContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await maintenance.GetStatusAsync(cancellationToken));
        });

        app.MapPost("/api/source-admin/import/reconciliation", async (
            HttpContext httpContext,
            CanonicalDataReconciliationMaintenanceJob maintenance,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireCorpusReconciliationAuthority(httpContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            var result = await maintenance.StartAsync(cancellationToken);
            httpContext.Response.Headers.CacheControl = "no-store";
            return result.Started
                ? Results.Accepted("/api/source-admin/import/reconciliation", result.Status)
                : Results.Ok(result.Status);
        });

        app.MapGet("/api/source-admin/import/rule-concept-coverage", async (
            HttpContext httpContext,
            RuleConceptCoverageMaintenanceJob maintenance,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireCorpusReconciliationAuthority(httpContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await maintenance.GetStatusAsync(cancellationToken));
        });

        app.MapPost("/api/source-admin/import/rule-concept-coverage", async (
            HttpContext httpContext,
            RuleConceptCoverageMaintenanceJob maintenance,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireCorpusReconciliationAuthority(httpContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            var authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext)!;
            var result = await maintenance.StartAsync(authenticationContext.User.Id, cancellationToken);
            httpContext.Response.Headers.CacheControl = "no-store";
            return result.Started
                ? Results.Accepted("/api/source-admin/import/rule-concept-coverage", result.Status)
                : Results.Ok(result.Status);
        });

        app.MapPost("/api/source-admin/import/preview", async (
            SourceAdminImportRequest request,
            HttpContext httpContext,
            ISourceImportService importer,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireSourceAdministrationAuthority(httpContext, out _);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var preparation = SourceAdminImportPartitioner.Prepare(request);
                var preview = await importer.Preview5eToolsDocumentAsync(
                    preparation.LogicalRequest,
                    cancellationToken);
                if (preparation.Warnings.Count > 0)
                {
                    preview = preview with
                    {
                        Warnings = preview.Warnings.Concat(preparation.Warnings).ToArray()
                    };
                }

                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(preview);
            }
            catch (ArgumentException exception)
            {
                return InvalidImport(exception.Message);
            }
            catch (InvalidDataException exception)
            {
                return InvalidImport(exception.Message);
            }
            catch (JsonException exception)
            {
                return InvalidImport(exception.Message);
            }
        });

        app.MapPost("/api/source-admin/import", async (
            SourceAdminImportRequest request,
            HttpContext httpContext,
            ISourceImportService importer,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireSourceAdministrationAuthority(httpContext, out _);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var preparation = SourceAdminImportPartitioner.Prepare(request);
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(await importer.Import5eToolsDocumentAsync(
                    preparation.LogicalRequest,
                    cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return InvalidImport(exception.Message);
            }
            catch (InvalidDataException exception)
            {
                return InvalidImport(exception.Message);
            }
            catch (JsonException exception)
            {
                return InvalidImport(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return Results.Problem(
                    title: "Source import conflict",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status409Conflict);
            }
        });

        app.MapGet("/api/source-admin/packages", async (
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
                var administration = new SourceAccessAdministrationService(dbContext);
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(await administration.GetPackagesAsync(
                    authenticationContext!.User.Id,
                    cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return InvalidAccessRequest(exception.Message);
            }
        });

        app.MapPost("/api/source-admin/packages/{sourcePackageId:guid}/current-user-grant", async (
            Guid sourcePackageId,
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
                var administration = new SourceAccessAdministrationService(dbContext);
                var result = await administration.GrantCurrentUserAsync(
                    authenticationContext!.User.Id,
                    sourcePackageId,
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
                return InvalidAccessRequest(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return AccessConflict(exception.Message);
            }
        });

        app.MapDelete("/api/source-admin/packages/{sourcePackageId:guid}/current-user-grant", async (
            Guid sourcePackageId,
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
                var administration = new SourceAccessAdministrationService(dbContext);
                var result = await administration.RevokeCurrentUserAsync(
                    authenticationContext!.User.Id,
                    sourcePackageId,
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
                return InvalidAccessRequest(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return AccessConflict(exception.Message);
            }
        });
    }

    private static IResult? RequireCorpusReconciliationAuthority(HttpContext httpContext)
    {
        var authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
        if (authenticationContext is null)
        {
            return Results.Unauthorized();
        }

        return SourceAdministrationAuthority.CanReconcileExistingCorpus(authenticationContext)
            ? null
            : Results.StatusCode(StatusCodes.Status403Forbidden);
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

    private static IResult InvalidImport(string detail) =>
        Results.Problem(
            title: "Invalid source import",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult InvalidAccessRequest(string detail) =>
        Results.Problem(
            title: "Invalid source access request",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult AccessConflict(string detail) =>
        Results.Problem(
            title: "Source access conflict",
            detail: detail,
            statusCode: StatusCodes.Status409Conflict);
}
