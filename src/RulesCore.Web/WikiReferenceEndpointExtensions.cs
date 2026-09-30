using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Web;

public static class WikiReferenceEndpointExtensions
{
    public static void MapWikiReferenceEndpoints(this WebApplication app)
    {
        app.MapGet("/internal/wiki/references", async (
            string? entityType,
            string? categoryMode,
            string? q,
            string? source,
            string? package,
            string? edition,
            int? limit,
            int? offset,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var callerFailure = RequireInternalCaller(httpContext, out var authenticationContext);
            if (callerFailure is not null) return callerFailure;

            try
            {
                var catalog = await new WikiReferenceCatalogService(dbContext).GetGlobalCatalogAsync(
                    authenticationContext!.User.Id,
                    entityType,
                    categoryMode,
                    q,
                    source,
                    package,
                    edition,
                    limit ?? 200,
                    offset ?? 0,
                    cancellationToken);
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(catalog);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception);
            }
        });

        app.MapGet("/internal/wiki/references/{referenceIdentity}", async (
            string referenceIdentity,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var callerFailure = RequireInternalCaller(httpContext, out var authenticationContext);
            if (callerFailure is not null) return callerFailure;

            try
            {
                var detail = await new WikiReferenceCatalogService(dbContext).GetGlobalDetailAsync(
                    authenticationContext!.User.Id,
                    referenceIdentity,
                    cancellationToken);
                if (detail is null) return Results.NotFound();
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(detail);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception);
            }
        });

        app.MapGet("/internal/wiki/references/{referenceIdentity}/class-family", async (
            string referenceIdentity,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var callerFailure = RequireInternalCaller(httpContext, out var authenticationContext);
            if (callerFailure is not null) return callerFailure;

            try
            {
                var relationships = await new WikiReferenceClassFamilyService(dbContext).GetGlobalAsync(
                    authenticationContext!.User.Id,
                    referenceIdentity,
                    cancellationToken);
                if (relationships is null) return Results.NotFound();
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(relationships);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception);
            }
        });

        app.MapGet("/internal/wiki/campaigns/{campaignId:guid}/references", async (
            Guid campaignId,
            string? entityType,
            string? categoryMode,
            string? q,
            string? source,
            string? package,
            string? edition,
            bool? overridesOnly,
            int? limit,
            int? offset,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authenticationFailure = RequireCampaignRead(httpContext, campaignId, out var authenticationContext);
            if (authenticationFailure is not null) return authenticationFailure;

            try
            {
                var service = new WikiReferenceCatalogService(dbContext);
                var catalog = overridesOnly == true
                    ? await service.GetCampaignOverridesCatalogAsync(
                        campaignId,
                        authenticationContext!.User.Id,
                        entityType,
                        categoryMode,
                        q,
                        source,
                        package,
                        edition,
                        limit ?? 200,
                        offset ?? 0,
                        cancellationToken)
                    : await service.GetCampaignCatalogAsync(
                        campaignId,
                        authenticationContext!.User.Id,
                        entityType,
                        categoryMode,
                        q,
                        source,
                        package,
                        edition,
                        limit ?? 200,
                        offset ?? 0,
                        cancellationToken);
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(catalog);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception);
            }
        });

        app.MapGet("/internal/wiki/campaigns/{campaignId:guid}/references/{referenceIdentity}", async (
            Guid campaignId,
            string referenceIdentity,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authenticationFailure = RequireCampaignRead(httpContext, campaignId, out var authenticationContext);
            if (authenticationFailure is not null) return authenticationFailure;

            try
            {
                var detail = await new WikiReferenceCatalogService(dbContext).GetCampaignDetailAsync(
                    campaignId,
                    authenticationContext!.User.Id,
                    referenceIdentity,
                    cancellationToken);
                if (detail is null) return Results.NotFound();
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(detail);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception);
            }
        });

        app.MapGet("/internal/wiki/campaigns/{campaignId:guid}/references/{referenceIdentity}/class-family", async (
            Guid campaignId,
            string referenceIdentity,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authenticationFailure = RequireCampaignRead(httpContext, campaignId, out var authenticationContext);
            if (authenticationFailure is not null) return authenticationFailure;

            try
            {
                var relationships = await new WikiReferenceClassFamilyService(dbContext).GetCampaignAsync(
                    campaignId,
                    authenticationContext!.User.Id,
                    referenceIdentity,
                    cancellationToken);
                if (relationships is null) return Results.NotFound();
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(relationships);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception);
            }
        });

        app.MapPost("/internal/wiki/references/comparison", async (
            WikiReferenceComparisonRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var callerFailure = RequireInternalCaller(httpContext, out var authenticationContext);
            if (callerFailure is not null) return callerFailure;

            try
            {
                var userId = authenticationContext!.User.Id;
                var references = new WikiReferenceCatalogService(dbContext);
                if (!await references.ReferenceContainsRevisionsAsync(
                        userId,
                        request.ReferenceIdentity,
                        request.LeftSourceEntityRevisionId,
                        request.RightSourceEntityRevisionId,
                        cancellationToken))
                {
                    return Results.NotFound();
                }

                var comparison = await new RuleSemanticComparisonService(dbContext)
                    .CompareAccessibleSourcesAsync(
                        request.LeftSourceEntityRevisionId,
                        request.RightSourceEntityRevisionId,
                        userId,
                        cancellationToken);
                if (comparison is null) return Results.NotFound();
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(comparison);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception);
            }
        });
    }

    private static IResult? RequireInternalCaller(
        HttpContext httpContext,
        out ToolHostAuthenticationContext? authenticationContext)
    {
        authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
        return RulesWikiInternalApiBoundary.IsRulesWikiCaller(authenticationContext)
            ? null
            : Results.NotFound();
    }

    private static IResult? RequireCampaignRead(
        HttpContext httpContext,
        Guid campaignId,
        out ToolHostAuthenticationContext? authenticationContext)
    {
        var callerFailure = RequireInternalCaller(httpContext, out authenticationContext);
        if (callerFailure is not null) return callerFailure;
        return RulesAuthority.CanAccessCampaignRules(authenticationContext!, campaignId)
            ? null
            : Results.NotFound();
    }

    private static IResult BadRequest(ArgumentException exception) =>
        Results.Problem(
            title: "Invalid Rules Wiki reference request",
            detail: exception.Message,
            statusCode: StatusCodes.Status400BadRequest);
}
