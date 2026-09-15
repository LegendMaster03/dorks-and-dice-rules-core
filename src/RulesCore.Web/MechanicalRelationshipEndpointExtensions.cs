using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Web;

public static class MechanicalRelationshipEndpointExtensions
{
    public static void MapMechanicalRelationshipEndpoints(this WebApplication app)
    {
        app.MapGet("/api/global/rules/mechanical-relationships/concepts/{conceptId:guid}", async (
            Guid conceptId,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = RequireGlobalRulesAuthority(httpContext, out _);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            try
            {
                var result = await new MechanicalRelationshipService(dbContext)
                    .GetForConceptAsync(conceptId, cancellationToken);
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return InvalidRequest(exception.Message);
            }
        });

        app.MapPut("/api/global/rules/mechanical-relationships/{relationshipKey}/ruling", async (
            string relationshipKey,
            SetMechanicalRelationshipRulingRequest request,
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
                var result = await new MechanicalRelationshipService(dbContext)
                    .SetRulingAsync(
                        relationshipKey,
                        request,
                        authenticationContext!.User.Id,
                        cancellationToken);
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(result);
            }
            catch (KeyNotFoundException exception)
            {
                return Results.Problem(
                    title: "Mechanical relationship not found",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status404NotFound);
            }
            catch (ArgumentException exception)
            {
                return InvalidRequest(exception.Message);
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

    private static IResult InvalidRequest(string detail) =>
        Results.Problem(
            title: "Invalid mechanical relationship request",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);
}
