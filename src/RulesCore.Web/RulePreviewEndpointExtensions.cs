using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Web;

public static class RulePreviewEndpointExtensions
{
    public static void MapRulePreviewEndpoints(this WebApplication app)
    {
        app.MapPost("/api/global/rules/concepts/{conceptId:guid}/preview", async (
            Guid conceptId,
            SetGlobalRuleDecisionRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireGlobalRulesAuthority(
                httpContext,
                out var authenticationContext);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var preview = await new RulePatchPreviewService(dbContext).PreviewGlobalAsync(
                    conceptId,
                    request,
                    authenticationContext!.User.Id,
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
                return InvalidPreview("Invalid global rule preview", exception);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidDataException exception)
            {
                return PreviewConflict(exception);
            }
            catch (InvalidOperationException exception)
            {
                return PreviewConflict(exception);
            }
        });

        app.MapPost("/api/campaigns/{campaignId:guid}/rules/concepts/{conceptId:guid}/preview", async (
            Guid campaignId,
            Guid conceptId,
            SetCampaignRuleDecisionRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
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
                var preview = await new RulePatchPreviewService(dbContext).PreviewCampaignAsync(
                    campaignId,
                    conceptId,
                    request,
                    authenticationContext!.User.Id,
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
                return InvalidPreview("Invalid campaign rule preview", exception);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidDataException exception)
            {
                return PreviewConflict(exception);
            }
            catch (InvalidOperationException exception)
            {
                return PreviewConflict(exception);
            }
        });
    }

    private static IResult? RequireGlobalRulesAuthority(
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

    private static IResult InvalidPreview(string title, Exception exception) =>
        Results.Problem(
            title: title,
            detail: exception.Message,
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult PreviewConflict(Exception exception) =>
        Results.Problem(
            title: "Rule preview can not be applied",
            detail: exception.Message,
            statusCode: StatusCodes.Status409Conflict);
}
