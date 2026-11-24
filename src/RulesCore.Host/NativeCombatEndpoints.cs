using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// Caller-owned, unverified per-combatant state for comparing two action budgets.
/// This input does not carry proof of encounter authority or action eligibility.
/// </summary>
public sealed record NativeActionBudgetExperimentRequest(
    string RulesetId, string Fingerprint, string PolicyId, string Command,
    NativeActionBudgetState? State = null);

/// <summary>
/// Authenticated stateless combat experiments. Neither operation executes
/// an authorized attack, applies damage, mutates an encounter, or approves a
/// campaign policy. The owning encounter application must verify its facts.
/// </summary>
public static class NativeCombatEndpoints
{
    public static void MapExperimentalCombatEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/experimental/combat/action-budget",
            (NativeActionBudgetExperimentRequest request, NativeCatalogStore store) =>
        {
            var catalog = store.Snapshot;
            if (catalog is null)
                return Error(503, "native_catalog_unavailable");
            if (request is null ||
                string.IsNullOrWhiteSpace(request.PolicyId) ||
                string.IsNullOrWhiteSpace(request.Command))
                return Error(400, "invalid_action_budget_request");
            if (request.RulesetId != catalog.RulesetId ||
                request.Fingerprint != catalog.Fingerprint)
                return Error(409, "ruleset_version_mismatch");

            var resolved = NativeActionBudgetExperiment.Resolve(
                request.PolicyId, request.Command, request.State);
            var response = new
            {
                contractVersion = 1,
                experimental = true,
                status = resolved.Status,
                state = resolved.State,
                reason = resolved.Reason
            };
            return resolved.Status switch
            {
                "resolved" => Results.Ok(response),
                "unavailable" => Results.Json(response, statusCode: 422),
                _ => Results.Json(response, statusCode: 400)
            };
        });

        group.MapPost("/experimental/combat/attack-check",
            (NativeCombatAttackRequest request, NativeCatalogStore store) =>
        {
            var catalog = store.Snapshot;
            if (catalog is null)
                return Error(503, "native_catalog_unavailable");
            if (request is null)
                return Error(400, "invalid_attack_check_request");

            var resolved = NativeCombatAttackResolver.Resolve(catalog, request);
            var response = new
            {
                contractVersion = 1,
                experimental = true,
                rulesetId = catalog.RulesetId,
                fingerprint = catalog.Fingerprint,
                result = resolved
            };
            return resolved.Status switch
            {
                "resolved" or "critical-resolution-required" => Results.Ok(response),
                "ruleset-version-mismatch" => Results.Json(response, statusCode: 409),
                "invalid-input" or "invalid-dice" or "unexpected-die" =>
                    Results.Json(response, statusCode: 400),
                _ => Results.Json(response, statusCode: 422)
            };
        });
        group.MapPost("/experimental/combat/damage-preview",
            (NativeCombatDamageRequest request, NativeCatalogStore store) =>
        {
            var catalog = store.Snapshot;
            if (catalog is null)
                return Error(503, "native_catalog_unavailable");
            if (request is null)
                return Error(400, "invalid_damage_preview_request");
            if (request.RulesetId != catalog.RulesetId ||
                request.Fingerprint != catalog.Fingerprint)
                return Error(409, "ruleset_version_mismatch");

            var resolved = NativeCombatDamageResolver.Resolve(request);
            var response = new
            {
                contractVersion = 1,
                experimental = true,
                rulesetId = catalog.RulesetId,
                fingerprint = catalog.Fingerprint,
                result = resolved
            };
            return resolved.Status switch
            {
                "resolved" => Results.Ok(response),
                "invalid-input" => Results.Json(response, statusCode: 400),
                _ => Results.Json(response, statusCode: 422)
            };
        });
    }

    private static IResult Error(int status, string code) =>
        Results.Json(new { error = code }, statusCode: status);
}
