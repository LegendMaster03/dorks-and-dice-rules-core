using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Web;

public static class CampaignRuleAuthoringEndpointExtensions
{
    public static void MapCampaignRuleAuthoringEndpoints(this WebApplication app)
    {
        app.MapGet("/api/campaigns/{campaignId:guid}/rules/authoring", async (
            Guid campaignId,
            HttpContext httpContext,
            ICampaignRulesAuthoringService authoring,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireCampaignRulesEditAuthority(
                httpContext,
                campaignId,
                out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(await authoring.GetOverviewAsync(
                    campaignId,
                    authenticationContext!.User.Id,
                    cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return InvalidRequest(exception);
            }
        });

        app.MapGet("/api/campaigns/{campaignId:guid}/rules/authoring/concepts/{conceptId:guid}", async (
            Guid campaignId,
            Guid conceptId,
            HttpContext httpContext,
            ICampaignRulesAuthoringService authoring,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireCampaignRulesEditAuthority(
                httpContext,
                campaignId,
                out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var concept = await authoring.GetConceptAsync(
                    campaignId,
                    conceptId,
                    authenticationContext!.User.Id,
                    cancellationToken);
                if (concept is null)
                {
                    return Results.NotFound();
                }

                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(concept);
            }
            catch (ArgumentException exception)
            {
                return InvalidRequest(exception);
            }
        });

        app.MapGet("/api/campaigns/{campaignId:guid}/rules/baselines", async (
            Guid campaignId,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireCampaignRulesEditAuthority(
                httpContext,
                campaignId,
                out _);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var discovery = new CampaignBaselineDiscoveryService(dbContext);
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(await discovery.GetCandidatesAsync(
                    campaignId,
                    cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return InvalidRequest(exception);
            }
        });

        app.MapGet("/api/campaigns/{campaignId:guid}/rules/baselines/{rulesetRevisionId:guid}/preview", async (
            Guid campaignId,
            Guid rulesetRevisionId,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireCampaignRulesEditAuthority(
                httpContext,
                campaignId,
                out _);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var discovery = new CampaignBaselineDiscoveryService(dbContext);
                var preview = await discovery.PreviewAsync(
                    campaignId,
                    rulesetRevisionId,
                    cancellationToken);
                if (preview is null)
                {
                    return Results.NotFound();
                }

                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(preview);
            }
            catch (ArgumentException exception)
            {
                return InvalidRequest(exception);
            }
        });
    }

    private static IResult? RequireCampaignRulesEditAuthority(
        HttpContext httpContext,
        Guid campaignId,
        out ToolHostAuthenticationContext? authenticationContext)
    {
        authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
        if (authenticationContext is null)
        {
            return Results.Unauthorized();
        }

        if (!RulesAuthority.CanAccessCampaignRules(authenticationContext, campaignId))
        {
            return Results.NotFound();
        }

        return RulesAuthority.CanEditCampaignRules(authenticationContext, campaignId)
            ? null
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static IResult InvalidRequest(Exception exception) =>
        Results.Problem(
            title: "Invalid campaign rule authoring request",
            detail: exception.Message,
            statusCode: StatusCodes.Status400BadRequest);
}
