using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

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

        app.MapGet("/api/sources/entities/{entityId:guid}/native", async (
            Guid entityId,
            HttpContext httpContext,
            ISourceCatalogService catalog,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var userId = HostedToolAuthenticationMiddleware
                .GetAuthenticationContext(httpContext)?
                .User.Id;
            httpContext.Response.Headers.CacheControl = "no-store";

            var entity = await catalog.GetLatestAccessibleEntityAsync(
                entityId,
                userId,
                cancellationToken);
            if (entity is null)
            {
                return Results.NotFound();
            }

            var rawJson = await dbContext.SourceEntityRevisions
                .AsNoTracking()
                .Where(value => value.SourceEntityId == entityId
                    && value.RevisionNumber == entity.RevisionNumber)
                .Select(value => value.RawJson)
                .SingleOrDefaultAsync(cancellationToken);
            if (rawJson is null)
            {
                return Results.NotFound();
            }

            return Results.Content(rawJson, "application/json");
        });
    }
}
