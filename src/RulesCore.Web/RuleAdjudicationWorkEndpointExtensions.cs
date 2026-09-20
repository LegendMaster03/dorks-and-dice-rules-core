using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Web;

public static class RuleAdjudicationWorkEndpointExtensions
{
    public static void MapRuleAdjudicationWorkEndpoints(this WebApplication app)
    {
        app.MapPost("/api/global/rules/adjudication/discover", async (
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var failure = RequireGlobalRulesAuthority(httpContext, out var authenticationContext);
            if (failure is not null) return failure;
            try
            {
                var result = await new RuleAdjudicationWorkService(dbContext).DiscoverAsync(
                    authenticationContext!.User.Id,
                    cancellationToken);
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidRequest(exception.Message);
            }
        });

        app.MapGet("/api/global/rules/adjudication/work", async (
            string? kind,
            string? state,
            bool? includePublishedCompleted,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var failure = RequireGlobalRulesAuthority(httpContext, out var authenticationContext);
            if (failure is not null) return failure;
            try
            {
                var result = await new RuleAdjudicationWorkService(dbContext).ListAsync(
                    authenticationContext!.User.Id,
                    kind,
                    state,
                    includePublishedCompleted ?? false,
                    cancellationToken);
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidRequest(exception.Message);
            }
        });

        app.MapGet("/api/global/rules/adjudication/work/{workItemId:guid}", async (
            Guid workItemId,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var failure = RequireGlobalRulesAuthority(httpContext, out var authenticationContext);
            if (failure is not null) return failure;
            try
            {
                var result = await new RuleAdjudicationWorkService(dbContext).GetAsync(
                    workItemId,
                    authenticationContext!.User.Id,
                    cancellationToken);
                if (result is null) return Results.NotFound();
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidRequest(exception.Message);
            }
        });

        app.MapPost("/api/global/rules/adjudication/work/{workItemId:guid}/begin", async (
            Guid workItemId,
            RuleAdjudicationVersionRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
            await MutateAsync(
                workItemId,
                httpContext,
                dbContext,
                (service, actor) => service.BeginReviewAsync(workItemId, request, actor, cancellationToken)));

        app.MapPost("/api/global/rules/adjudication/work/{workItemId:guid}/clarification", async (
            Guid workItemId,
            RequestRuleAdjudicationClarificationRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
            await MutateAsync(
                workItemId,
                httpContext,
                dbContext,
                (service, actor) => service.RequestClarificationAsync(workItemId, request, actor, cancellationToken)));

        app.MapPost("/api/global/rules/adjudication/work/{workItemId:guid}/clarification/answer", async (
            Guid workItemId,
            AnswerRuleAdjudicationClarificationRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
            await MutateAsync(
                workItemId,
                httpContext,
                dbContext,
                (service, actor) => service.AnswerClarificationAsync(workItemId, request, actor, cancellationToken)));

        app.MapPost("/api/global/rules/adjudication/work/{workItemId:guid}/escalate", async (
            Guid workItemId,
            EscalateRuleAdjudicationWorkRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
            await MutateAsync(
                workItemId,
                httpContext,
                dbContext,
                (service, actor) => service.EscalateAsync(workItemId, request, actor, cancellationToken)));

        app.MapPost("/api/global/rules/adjudication/work/{workItemId:guid}/defer", async (
            Guid workItemId,
            DeferRuleAdjudicationWorkRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
            await MutateAsync(
                workItemId,
                httpContext,
                dbContext,
                (service, actor) => service.DeferAsync(workItemId, request, actor, cancellationToken)));

        app.MapPost("/api/global/rules/adjudication/work/{workItemId:guid}/reopen", async (
            Guid workItemId,
            RuleAdjudicationVersionRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
            await MutateAsync(
                workItemId,
                httpContext,
                dbContext,
                (service, actor) => service.ReopenAsync(workItemId, request, actor, cancellationToken)));
    }

    private static async Task<IResult> MutateAsync(
        Guid workItemId,
        HttpContext httpContext,
        RulesCoreDbContext dbContext,
        Func<RuleAdjudicationWorkService, string, Task<RuleAdjudicationWorkSummaryView?>> mutation)
    {
        var failure = RequireGlobalRulesAuthority(httpContext, out var authenticationContext);
        if (failure is not null) return failure;
        try
        {
            var result = await mutation(new RuleAdjudicationWorkService(dbContext), authenticationContext!.User.Id);
            if (result is null) return Results.NotFound();
            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Ok(result);
        }
        catch (RuleAdjudicationConcurrencyException exception)
        {
            return Conflict("Adjudication work changed", exception.Message);
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict("Adjudication workflow conflict", exception.Message);
        }
    }

    private static IResult? RequireGlobalRulesAuthority(
        HttpContext httpContext,
        out ToolHostAuthenticationContext? authenticationContext)
    {
        authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
        if (authenticationContext is null) return Results.Unauthorized();
        return RulesAuthority.CanEditGlobalRules(authenticationContext)
            ? null
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static IResult InvalidRequest(string detail) =>
        Results.Problem(
            title: "Invalid adjudication request",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult Conflict(string title, string detail) =>
        Results.Problem(
            title: title,
            detail: detail,
            statusCode: StatusCodes.Status409Conflict);
}
