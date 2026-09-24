using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Web;

public static class DiceRollEndpointExtensions
{
    public static void MapDiceRollEndpoints(this WebApplication app)
    {
        app.MapGet("/api/rules/dice", () => Results.Ok(new
        {
            selectionModes = DiceRollSelectionModes.All,
            emphasisPivot = DiceRollSelector.EmphasisPivot,
            emphasisTieBehavior = "manual-choice",
            semantics = new
            {
                normal = "Roll once and use that result.",
                advantage = "Roll twice and use the higher result.",
                disadvantage = "Roll twice and use the lower result.",
                emphasis = "Roll twice and use the result furthest from 10."
            }
        }));

        app.MapPost("/api/rules/dice/roll", (
            DiceRollRequest request,
            HttpContext httpContext,
            IDiceRoller roller) =>
        {
            try
            {
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(roller.Roll(request));
            }
            catch (ArgumentException exception)
            {
                return InvalidRollRequest(exception);
            }
            catch (OverflowException exception)
            {
                return InvalidRollRequest(exception);
            }
        });
    }

    private static IResult InvalidRollRequest(Exception exception) =>
        Results.Problem(
            title: "Invalid dice roll request",
            detail: exception.Message,
            statusCode: StatusCodes.Status400BadRequest);
}
