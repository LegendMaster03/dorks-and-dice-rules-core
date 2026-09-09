using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Web;

public static class ResolvedRulesCatalogEndpointExtensions
{
    public static void MapResolvedRulesCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/rules", async (
            string? entityType,
            string? q,
            int? limit,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var userId = HostedToolAuthenticationMiddleware
                    .GetAuthenticationContext(httpContext)?
                    .User.Id;
                var catalog = new ResolvedRulesCatalogService(dbContext);
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(await catalog.GetGlobalAsync(
                    userId,
                    entityType,
                    q,
                    limit ?? 200,
                    cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return InvalidRequest(exception.Message);
            }
        });

        app.MapGet("/api/campaigns/{campaignId:guid}/rules", async (
            Guid campaignId,
            string? entityType,
            string? q,
            int? limit,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireCampaignRulesReadAuthority(
                httpContext,
                campaignId,
                out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var catalog = new ResolvedRulesCatalogService(dbContext);
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(await catalog.GetCampaignAsync(
                    campaignId,
                    authenticationContext!.User.Id,
                    entityType,
                    q,
                    limit ?? 200,
                    cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return InvalidRequest(exception.Message);
            }
        });
    }

    private static IResult? RequireCampaignRulesReadAuthority(
        HttpContext httpContext,
        Guid campaignId,
        out ToolHostAuthenticationContext? authenticationContext)
    {
        authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
        if (authenticationContext is null)
        {
            return Results.Unauthorized();
        }

        return RulesAuthority.CanAccessCampaignRules(authenticationContext, campaignId)
            ? null
            : Results.NotFound();
    }

    private static IResult InvalidRequest(string detail) =>
        Results.Problem(
            title: "Invalid rules catalog request",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);
}
