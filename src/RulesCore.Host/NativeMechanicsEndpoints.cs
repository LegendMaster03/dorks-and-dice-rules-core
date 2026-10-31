using System.Diagnostics;
using System.Globalization;
using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

public sealed record NativeResourceActionRequest(
    NativeCharacterRequest Character, string ResourceId, int Current,
    int? Spend = null, NativeRecoveryEvent? Rest = null);

public sealed record NativeConditionRollRequest(
    string RulesetId, string Fingerprint, IReadOnlyList<string> ConditionIds,
    NativeRollContext Roll, NativeEffectContext? Facts = null);

/// <summary>
/// Versioned, authenticated mechanics surface. Ordinary Character Sheet delegation
/// is accepted only on the public ingress; private-tunnel provenance only on the
/// private ingress. Neither surface accepts anonymous requests.
/// </summary>
public static class NativeMechanicsEndpoints
{
    public static void MapAuthenticatedNativeMechanics(this WebApplication app, bool privateTunnel)
    {
        var group = app.MapGroup(privateTunnel
            ? "/api/private/mechanics/v1" : "/api/mechanics/v1");
        group.AddEndpointFilter(async (invocation, next) =>
        {
            var validator = invocation.HttpContext.RequestServices
                .GetRequiredService<CharacterSheetMechanicsTicketValidator>();
            var failure = await validator.ValidateAsync(invocation.HttpContext, privateTunnel);
            return failure is not null ? failure : await next(invocation);
        });

        // Raw native definitions remain on the private, source-scoped surface.
        // The ordinary delegated surface serves calculations, not unrestricted content.
        if (privateTunnel) group.MapNativeCharacterCatalog();

        group.MapGet("/ruleset", (NativeCatalogStore store) =>
        {
            var catalog = store.Snapshot;
            return catalog is null ? Error(503, "native_catalog_unavailable") : Results.Ok(new {
                contractVersion = 1,
                rulesetId = catalog.RulesetId,
                schemaVersion = catalog.SchemaVersion,
                fingerprint = catalog.Fingerprint,
                capabilities = new { characterProjection = true, resourceResolution = true,
                    nativeCatalogSearch = privateTunnel, nativeCatalogBatchResolve = privateTunnel,
                    nativeOptionSearch = true, advancementEligibility = "supported-subset",
                    conditionRollMode = "conditional-subset",
                    creatureProjection = "partial", sourceConversion = false,
                    campaignEffectiveRules = false }
            });
        });

        // Native-only option identities for Character Sheet's builder. Titles only:
        // no raw rule definitions, source bodies, or user-private homebrew.
        group.MapGet("/catalog/options", (HttpRequest request, NativeCatalogStore store) =>
        {
            var snapshot = store.Snapshot;
            if (snapshot is null) return Error(503, "native_catalog_unavailable");
            if (request.Query.Keys.Any(key => key is not (
                "fingerprint" or "category" or "q" or "limit" or "offset")) ||
                !SingleQueryValue(request, "fingerprint", out var fingerprint) ||
                !SingleQueryValue(request, "category", out var category) ||
                !SingleQueryValue(request, "q", out var query) ||
                !SingleQueryValue(request, "limit", out var limitText) ||
                !SingleQueryValue(request, "offset", out var offsetText) ||
                category is not ("class" or "subclass" or "patron" or "pact" or
                    "invocation" or "condition" or "leveled-condition") ||
                (query?.Length ?? 0) > 200 ||
                !PageNumber(limitText, 50, 1, 100, out var limit) ||
                !PageNumber(offsetText, 0, 0, 1_000_000, out var offset))
                return Error(400, "invalid_option_query");
            if (fingerprint != snapshot.Fingerprint)
                return Error(409, "ruleset_version_mismatch");
            var page = snapshot.Search(query, category, offset, limit);
            return Results.Ok(new {
                contractVersion = 1, rulesetId = snapshot.RulesetId,
                fingerprint = snapshot.Fingerprint, category,
                total = page.Total, offset = page.Offset, limit = page.Limit,
                options = page.Items.Select(item => new {
                    id = item.Id, name = item.Name, kind = item.Category
                }).ToArray()
            });
        });

        group.MapPost("/advancements/eligibility", (NativeEligibilityRequest request,
            NativeCatalogStore store, HttpContext context) =>
        {
            var start = Stopwatch.GetTimestamp();
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (request is null || request.Character is null)
                return Error(400, "invalid_eligibility_request");
            var result = new NativeAdvancementEligibility(catalog).Evaluate(request);
            Timing(context, "native-advancement-eligibility", start);
            return result.Status == "invalid"
                ? Results.Json(result, statusCode: result.Requirements.Any(
                    requirement => requirement.Key == "ruleset") ? 409 : 400)
                : Results.Ok(result);
        });

        group.MapPost("/characters/project", (NativeCharacterRequest request,
            NativeCatalogStore store, HttpContext context) =>
        {
            var start = Stopwatch.GetTimestamp();
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            var result = new NativeCharacterProjector(catalog).Project(request, context.RequestAborted);
            Timing(context, "native-character-project", start);
            return result.Status == "invalid"
                ? Results.Json(result, statusCode: 409)
                : Results.Ok(result);
        });

        group.MapPost("/conditions/roll-mode", (NativeConditionRollRequest request,
            NativeCatalogStore store, HttpContext context) =>
        {
            var start = Stopwatch.GetTimestamp();
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (request is null || request.Roll is null || request.ConditionIds is null ||
                request.ConditionIds.Count > 64 ||
                request.ConditionIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 160))
                return Error(400, "invalid_condition_roll_request");
            if (!string.Equals(request.RulesetId, catalog.RulesetId, StringComparison.Ordinal) ||
                !string.Equals(request.Fingerprint, catalog.Fingerprint, StringComparison.Ordinal))
                return Error(409, "ruleset_version_mismatch");
            var result = NativeConditionEffects.RollMode(catalog, request.ConditionIds,
                request.Roll, request.Facts);
            Timing(context, "native-condition-roll", start);
            return result.Status switch
            {
                "resolved" => Results.Ok(result),
                "invalid-input" => Results.Json(result, statusCode: 400),
                _ => Results.Json(result, statusCode: 422)
            };
        });

        group.MapGet("/creatures/{entityId}", (string entityId,
            NativeCatalogStore store, HttpContext context) =>
        {
            var start = Stopwatch.GetTimestamp();
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (entityId.Length is < 2 or > 160 || entityId.Any(c =>
                !(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '-' or '.')))
                return Error(400, "invalid_entity_id");
            var result = new NativeCreatureProjector(catalog).Project(entityId);
            Timing(context, "native-creature-project", start);
            return result.Status == "unavailable" ? Results.NotFound(result) : Results.Ok(result);
        });

        group.MapPost("/resources/resolve", (NativeResourceActionRequest request,
            NativeCatalogStore store, HttpContext context) =>
        {
            var start = Stopwatch.GetTimestamp();
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (request.Character is null || string.IsNullOrWhiteSpace(request.ResourceId) ||
                (request.Spend.HasValue == request.Rest.HasValue))
                return Error(400, "invalid_resource_action");
            var projection = new NativeCharacterProjector(catalog).Project(request.Character,
                context.RequestAborted);
            if (projection.Status == "invalid") return Error(409, "ruleset_version_mismatch");
            var matching = projection.Resources.Where(r => r.Id == request.ResourceId).Take(2).ToArray();
            if (matching.Length != 1) return Error(422, "resource_unavailable_or_ambiguous");
            var resource = matching[0];
            var result = request.Spend is int spend
                ? NativeResourceResolution.Spend(resource, request.Current, spend)
                : NativeResourceResolution.Recover(resource, request.Current, request.Rest!.Value);
            Timing(context, "native-resource-resolve", start);
            return result.Status is "resolved" ? Results.Ok(result)
                : Results.Json(result, statusCode: 422);
        });
    }

    private static bool SingleQueryValue(HttpRequest request, string key, out string? result)
    {
        result = null;
        if (!request.Query.TryGetValue(key, out var values)) return true;
        if (values.Count != 1) return false;
        result = values[0];
        return result is not null;
    }

    private static bool PageNumber(string? text, int fallback, int min, int max, out int result)
    {
        result = fallback;
        return text is null || (int.TryParse(text, NumberStyles.None,
            CultureInfo.InvariantCulture, out result) && result >= min && result <= max);
    }

    private static IResult Error(int status, string code) =>
        Results.Json(new { error = code }, statusCode: status);

    private static void Timing(HttpContext context, string metric, long start)
    {
        var milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        context.Response.Headers.Append("Server-Timing", metric + ";dur=" +
            milliseconds.ToString("0.###", CultureInfo.InvariantCulture));
    }
}
