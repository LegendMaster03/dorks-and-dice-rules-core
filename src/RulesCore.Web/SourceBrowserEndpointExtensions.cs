using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Web;

public static class SourceBrowserEndpointExtensions
{
    public static void MapSourceBrowserEndpoints(this WebApplication app)
    {
        app.MapGet("/api/sources/entities/page", async (
            string? entityType,
            string? q,
            int? limit,
            int? offset,
            HttpContext httpContext,
            ISourceEntitySearchService search,
            CancellationToken cancellationToken) =>
        {
            var userId = CurrentUserId(httpContext);
            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await search.SearchAccessiblePageAsync(
                userId,
                entityType,
                q,
                limit ?? 100,
                offset ?? 0,
                cancellationToken));
        });

        app.MapGet("/api/sources/library/publications", async (
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            httpContext.Response.Headers.CacheControl = "no-store";
            var browser = new SourceLibraryBrowserService(dbContext);
            return Results.Ok(await browser.GetPublicationsAsync(
                CurrentUserId(httpContext),
                cancellationToken));
        });

        app.MapGet("/api/sources/library/publications/{publicationId:guid}", async (
            Guid publicationId,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            httpContext.Response.Headers.CacheControl = "no-store";
            var browser = new SourceLibraryBrowserService(dbContext);
            var publication = await browser.GetPublicationAsync(
                publicationId,
                CurrentUserId(httpContext),
                cancellationToken);
            return publication is null ? Results.NotFound() : Results.Ok(publication);
        });

        app.MapGet("/api/sources/library/entities/page", async (
            Guid? publicationId,
            string? entityType,
            string? q,
            int? limit,
            int? offset,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            httpContext.Response.Headers.CacheControl = "no-store";
            var browser = new SourceLibraryBrowserService(dbContext);
            return Results.Ok(await browser.SearchAsync(
                CurrentUserId(httpContext),
                publicationId,
                entityType,
                q,
                limit ?? 100,
                offset ?? 0,
                cancellationToken));
        });

        app.MapGet("/api/sources/library/publications/{publicationId:guid}/entities/{entityId:guid}", async (
            Guid publicationId,
            Guid entityId,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            httpContext.Response.Headers.CacheControl = "no-store";
            var browser = new SourceLibraryBrowserService(dbContext);
            var entity = await browser.GetEntityAsync(
                publicationId,
                entityId,
                CurrentUserId(httpContext),
                cancellationToken);
            return entity is null ? Results.NotFound() : Results.Ok(entity);
        });
    }

    private static string? CurrentUserId(HttpContext httpContext) =>
        HostedToolAuthenticationMiddleware
            .GetAuthenticationContext(httpContext)?
            .User.Id;
}
