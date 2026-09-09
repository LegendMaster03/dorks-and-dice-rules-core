using System.Text.Json;
using RulesCore.Application.Hosting;
using RulesCore.Application.Sources;

namespace RulesCore.Web;

public static class SourceAdministrationEndpointExtensions
{
    public static void MapSourceAdministrationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/source-admin/import", async (
            Import5eToolsDocumentRequest request,
            HttpContext httpContext,
            ISourceImportService importer,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireSourceAdministrationAuthority(
                httpContext,
                out _);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(await importer.Import5eToolsDocumentAsync(
                    request,
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
}
