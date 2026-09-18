using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Web;

public static class RuleWorkspaceEndpointExtensions
{
    public static void MapRuleWorkspaceEndpoints(this WebApplication app)
    {
        app.MapGet("/api/workspace/scopes", (HttpContext httpContext) =>
        {
            var authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
            var scopes = new List<RuleAdjudicationScopeView>
            {
                new(
                    RuleAdjudicationScopeKinds.Global,
                    null,
                    "Global Rules",
                    CanBrowse: true,
                    CanAdjudicate: authenticationContext is not null
                        && RulesAuthority.CanEditGlobalRules(authenticationContext))
            };

            if (authenticationContext is not null
                && string.Equals(authenticationContext.SiteMode, RulesAuthority.DorksAndDiceMode, StringComparison.Ordinal))
            {
                scopes.AddRange(authenticationContext.Campaigns.Select(campaign =>
                    new RuleAdjudicationScopeView(
                        RuleAdjudicationScopeKinds.Campaign,
                        campaign.Id,
                        $"Campaign: {campaign.Name}",
                        CanBrowse: true,
                        CanAdjudicate: RulesAuthority.CanEditCampaignRules(authenticationContext, campaign.Id))));
            }

            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new RuleWorkspaceScopesView(scopes));
        });

        app.MapPost("/api/rules/comparison", async (
            RuleSourceComparisonRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
            try
            {
                var comparison = await new RuleSemanticComparisonService(dbContext).CompareSourcesAsync(
                    request,
                    authenticationContext?.User.Id,
                    cancellationToken);
                if (comparison is null)
                {
                    return Results.NotFound();
                }

                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(comparison);
            }
            catch (ArgumentException exception)
            {
                return Results.Problem(
                    title: "Invalid rule comparison request",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        });

        app.MapPost("/api/workspace/comparison", async (
            RuleSemanticComparisonRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
            if (authenticationContext is null)
            {
                return Results.Unauthorized();
            }
            if (!RulesAuthority.CanEditScope(authenticationContext, request.Scope))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            try
            {
                var comparison = await new RuleSemanticComparisonService(dbContext).CompareAsync(
                    request,
                    authenticationContext.User.Id,
                    cancellationToken);
                if (comparison is null)
                {
                    return Results.NotFound();
                }

                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(comparison);
            }
            catch (ArgumentException exception)
            {
                return Results.Problem(
                    title: "Invalid semantic comparison request",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        });

        app.MapGet("/api/campaigns/{campaignId:guid}/rules/{conceptKey}/global-baseline", async (
            Guid campaignId,
            string conceptKey,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
            if (authenticationContext is null)
            {
                return Results.Unauthorized();
            }
            if (!RulesAuthority.CanAccessCampaignRules(authenticationContext, campaignId))
            {
                return Results.NotFound();
            }

            try
            {
                var baseline = await new CampaignRuleBaselineService(dbContext).ResolveAsync(
                    campaignId,
                    conceptKey,
                    authenticationContext.User.Id,
                    cancellationToken);
                if (baseline is null)
                {
                    return Results.NotFound();
                }

                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(baseline);
            }
            catch (ArgumentException exception)
            {
                return Results.Problem(
                    title: "Invalid campaign baseline request",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        });
    }
}
