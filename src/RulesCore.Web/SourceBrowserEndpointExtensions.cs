using RulesCore.Application.Sources;

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
            var userId = HostedToolAuthenticationMiddleware
                .GetAuthenticationContext(httpContext)?
                .User.Id;
            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await search.SearchAccessiblePageAsync(
                userId,
                entityType,
                q,
                limit ?? 100,
                offset ?? 0,
                cancellationToken));
        });
    }
}