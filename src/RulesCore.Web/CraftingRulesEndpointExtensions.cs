using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;

namespace RulesCore.Web;

public static class CraftingRulesEndpointExtensions
{
    public static void MapCraftingRulesEndpoints(this WebApplication app)
    {
        app.MapPost("/api/rules/crafting/manufacturing/resolve", async (
            ManufacturingResolutionRequest request,
            HttpContext httpContext,
            ICraftingRulesService crafting,
            CancellationToken cancellationToken) =>
            await ResolveGlobal(
                httpContext,
                token => crafting.ResolveGlobalManufacturingAsync(
                    request,
                    token,
                    cancellationToken)));

        app.MapPost("/api/rules/crafting/enchanting/resolve", async (
            EnchantingResolutionRequest request,
            HttpContext httpContext,
            ICraftingRulesService crafting,
            CancellationToken cancellationToken) =>
            await ResolveGlobal(
                httpContext,
                token => crafting.ResolveGlobalEnchantingAsync(
                    request,
                    token,
                    cancellationToken)));

        app.MapPost("/api/campaigns/{campaignId:guid}/rules/crafting/manufacturing/resolve", async (
            Guid campaignId,
            ManufacturingResolutionRequest request,
            HttpContext httpContext,
            ICraftingRulesService crafting,
            CancellationToken cancellationToken) =>
            await ResolveCampaign(
                campaignId,
                httpContext,
                userId => crafting.ResolveCampaignManufacturingAsync(
                    campaignId,
                    request,
                    userId,
                    cancellationToken)));

        app.MapPost("/api/campaigns/{campaignId:guid}/rules/crafting/enchanting/resolve", async (
            Guid campaignId,
            EnchantingResolutionRequest request,
            HttpContext httpContext,
            ICraftingRulesService crafting,
            CancellationToken cancellationToken) =>
            await ResolveCampaign(
                campaignId,
                httpContext,
                userId => crafting.ResolveCampaignEnchantingAsync(
                    campaignId,
                    request,
                    userId,
                    cancellationToken)));
    }

    private static async Task<IResult> ResolveGlobal(
        HttpContext httpContext,
        Func<string?, Task<CraftingCheckResolutionView>> resolve)
    {
        try
        {
            var userId = HostedToolAuthenticationMiddleware
                .GetAuthenticationContext(httpContext)?
                .User.Id;
            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await resolve(userId));
        }
        catch (Exception exception) when (IsInvalidRequest(exception))
        {
            return InvalidRequest(exception);
        }
    }

    private static async Task<IResult> ResolveCampaign(
        Guid campaignId,
        HttpContext httpContext,
        Func<string, Task<CraftingCheckResolutionView>> resolve)
    {
        var authenticationContext =
            HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
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
            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await resolve(authenticationContext.User.Id));
        }
        catch (Exception exception) when (IsInvalidRequest(exception))
        {
            return InvalidRequest(exception);
        }
    }

    private static bool IsInvalidRequest(Exception exception) =>
        exception is ArgumentException
        or KeyNotFoundException
        or InvalidOperationException
        or OverflowException;

    private static IResult InvalidRequest(Exception exception) =>
        Results.Problem(
            title: "Invalid crafting rules request",
            detail: exception.Message,
            statusCode: StatusCodes.Status400BadRequest);
}
