using System.Text.Json;
using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Web;

public sealed record HostedSourceUploadMatchRequest(
    string Json,
    string? FallbackSourceCode = null,
    IReadOnlyList<string>? IncludedSourceCodes = null);

public sealed record HostedSourceUploadMatchView(
    IReadOnlyList<string> SourceCodes,
    IReadOnlyList<HostedSourceMatchView> Matches,
    bool FullyCovered);

public static class HostedSourceEndpointExtensions
{
    public static void MapHostedSourceEndpoints(this WebApplication app)
    {
        app.MapGet("/api/global/rules/hosted-sources", async (
            bool? includeDisabled,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            ISourceImportService importer,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireRulesLawyer(httpContext, out _);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            var service = new HostedSourceService(dbContext, importer);
            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await service.ListAsync(includeDisabled ?? true, cancellationToken));
        });

        app.MapGet("/api/global/rules/hosted-sources/{definitionId:guid}", async (
            Guid definitionId,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            ISourceImportService importer,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireRulesLawyer(httpContext, out _);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var service = new HostedSourceService(dbContext, importer);
                var definition = await service.GetAsync(definitionId, cancellationToken);
                if (definition is null)
                {
                    return Results.NotFound();
                }
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(definition);
            }
            catch (ArgumentException exception)
            {
                return InvalidHostedSource(exception.Message);
            }
        });

        app.MapPut("/api/global/rules/hosted-sources/{definitionKey}", async (
            string definitionKey,
            SetHostedSourceDefinitionRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            ISourceImportService importer,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireRulesLawyer(
                httpContext,
                out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var service = new HostedSourceService(dbContext, importer);
                var definition = await service.SetAsync(
                    definitionKey,
                    request,
                    authenticationContext!.User.Id,
                    cancellationToken);
                httpContext.Response.Headers.CacheControl = "no-store";
                return definition.CreatedRevision && definition.RevisionNumber == 1
                    ? Results.Created(
                        $"/api/global/rules/hosted-sources/{definition.Id:D}",
                        definition)
                    : Results.Ok(definition);
            }
            catch (ArgumentException exception)
            {
                return InvalidHostedSource(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return HostedSourceConflict(exception.Message);
            }
        });

        app.MapPost("/api/global/rules/hosted-sources/{definitionId:guid}/preview", async (
            Guid definitionId,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            ISourceImportService importer,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireRulesLawyer(httpContext, out _);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var service = new HostedSourceService(dbContext, importer);
                var preview = await service.PreviewAsync(definitionId, cancellationToken);
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(preview);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ArgumentException exception)
            {
                return InvalidHostedSource(exception.Message);
            }
            catch (InvalidDataException exception)
            {
                return InvalidHostedSource(exception.Message);
            }
            catch (JsonException exception)
            {
                return InvalidHostedSource(exception.Message);
            }
            catch (NotSupportedException exception)
            {
                return InvalidHostedSource(exception.Message);
            }
            catch (HttpRequestException exception)
            {
                return HostedSourceUnavailable(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return HostedSourceConflict(exception.Message);
            }
        });

        app.MapPost("/api/global/rules/hosted-sources/{definitionId:guid}/refresh", async (
            Guid definitionId,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            ISourceImportService importer,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireRulesLawyer(httpContext, out _);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var service = new HostedSourceService(dbContext, importer);
                var refreshed = await service.RefreshAsync(definitionId, cancellationToken);
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(refreshed);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ArgumentException exception)
            {
                return InvalidHostedSource(exception.Message);
            }
            catch (InvalidDataException exception)
            {
                return InvalidHostedSource(exception.Message);
            }
            catch (JsonException exception)
            {
                return InvalidHostedSource(exception.Message);
            }
            catch (NotSupportedException exception)
            {
                return InvalidHostedSource(exception.Message);
            }
            catch (HttpRequestException exception)
            {
                return HostedSourceUnavailable(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return HostedSourceConflict(exception.Message);
            }
        });

        app.MapPost("/api/source-admin/import/hosted-matches", async (
            HostedSourceUploadMatchRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            ISourceImportService importer,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireSourceMatchAuthority(httpContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var available = SourceAdminImportPartitioner.InspectSourceCodes(
                    request.Json,
                    request.FallbackSourceCode);
                var selected = FiveEToolsDocumentInspector.NormalizeSourceCodes(
                    request.IncludedSourceCodes);
                var sourceCodes = selected.Count == 0
                    ? available
                    : selected.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
                if (selected.Count > 0)
                {
                    var missing = sourceCodes
                        .Where(value => !available.Contains(value, StringComparer.OrdinalIgnoreCase))
                        .ToArray();
                    if (missing.Length > 0)
                    {
                        throw new InvalidDataException(
                            $"The selected source-code partition is not present in the uploaded document: {string.Join(", ", missing)}.");
                    }
                }

                var service = new HostedSourceService(dbContext, importer);
                var matches = await service.FindMatchesAsync(sourceCodes, cancellationToken);
                var covered = matches
                    .SelectMany(value => value.MatchedSourceCodes)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var fullyCovered = sourceCodes.Count > 0
                    && sourceCodes.All(covered.Contains);
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(new HostedSourceUploadMatchView(
                    sourceCodes,
                    matches,
                    fullyCovered));
            }
            catch (ArgumentException exception)
            {
                return InvalidHostedSource(exception.Message);
            }
            catch (InvalidDataException exception)
            {
                return InvalidHostedSource(exception.Message);
            }
            catch (JsonException exception)
            {
                return InvalidHostedSource(exception.Message);
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

    private static IResult? RequireSourceMatchAuthority(HttpContext httpContext)
    {
        var authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
        if (authenticationContext is null)
        {
            return Results.Unauthorized();
        }

        return RulesAuthority.CanEditGlobalRules(authenticationContext)
            || SourceAdministrationAuthority.CanAdministerSources(authenticationContext)
                ? null
                : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static IResult InvalidHostedSource(string detail) =>
        Results.Problem(
            title: "Invalid hosted source",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult HostedSourceConflict(string detail) =>
        Results.Problem(
            title: "Hosted source conflict",
            detail: detail,
            statusCode: StatusCodes.Status409Conflict);

    private static IResult HostedSourceUnavailable(string detail) =>
        Results.Problem(
            title: "Hosted source unavailable",
            detail: detail,
            statusCode: StatusCodes.Status502BadGateway);
}
