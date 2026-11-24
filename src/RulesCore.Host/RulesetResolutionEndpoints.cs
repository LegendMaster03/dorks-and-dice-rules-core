using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

public sealed record RulesetEvaluationRequest(string? Fingerprint, RulesetResolutionRequest? Resolution);

/// <summary>
/// Stateless representative mechanics on the existing authenticated ingresses.
/// Until Site provides ruleset entitlements, foreign package access additionally
/// requires independent public distribution approval of the exact fingerprint.
/// </summary>
internal static class RulesetResolutionEndpoints
{
    public static void MapRulesetResolution(this WebApplication app, bool privateTunnel)
    {
        var group = app.MapGroup((privateTunnel ? "/api/private/mechanics/v1" : "/api/mechanics/v1")
            + "/rulesets/{rulesetId}");
        group.AddEndpointFilter(async (invocation, next) =>
        {
            var validator = invocation.HttpContext.RequestServices
                .GetRequiredService<CharacterSheetMechanicsTicketValidator>();
            var failure = await validator.ValidateAsync(invocation.HttpContext, privateTunnel);
            return failure is not null ? failure : await next(invocation);
        });

        group.MapGet("/capabilities", (string rulesetId, NativeCatalogRegistry registry,
            NativePublicDistributionPolicy policy) =>
        {
            var snapshot = Select(rulesetId, registry, policy);
            if (snapshot is null) return NotFound();
            return Results.Ok(new { rulesetId, fingerprint = snapshot.Fingerprint,
                operations = RulesetResolution.Operations(rulesetId), complete = false });
        });

        group.MapPost("/resolution", (string rulesetId, RulesetEvaluationRequest request,
            NativeCatalogRegistry registry, NativePublicDistributionPolicy policy) =>
        {
            var snapshot = Select(rulesetId, registry, policy);
            if (snapshot is null) return NotFound();
            if (request.Resolution is null || string.IsNullOrWhiteSpace(request.Fingerprint))
                return Results.BadRequest(new { error = "invalid_request" });
            if (!string.Equals(request.Fingerprint, snapshot.Fingerprint, StringComparison.Ordinal))
                return Results.Json(new { error = "catalog_fingerprint_mismatch" }, statusCode: 409);
            var result = RulesetResolution.Evaluate(snapshot.RulesetId, request.Resolution);
            return Results.Json(new { rulesetId, fingerprint = snapshot.Fingerprint, result },
                statusCode: result.Status == "resolved" ? 200 : result.Status == "invalid" ? 400 : 422);
        });
    }

    private static NativeCatalogSnapshot? Select(string id, NativeCatalogRegistry registry,
        NativePublicDistributionPolicy policy)
    {
        if (!registry.TryGetStore(id, out var store) || store!.Snapshot is not { } snapshot ||
            snapshot.RulesetId != id || !policy.Allows(snapshot))
            return null;
        return snapshot;
    }

    private static IResult NotFound() => Results.NotFound(new { error = "ruleset_not_found" });
}
