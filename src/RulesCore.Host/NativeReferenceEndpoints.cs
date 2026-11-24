using System.Globalization;
using System.Text.RegularExpressions;
using RulesCore.Content;

namespace RulesCore.Host;

/// <summary>Read-only native reference operations; registered on PrivateOnly ingress.</summary>
public static class NativeReferenceEndpoints
{
    private static readonly Regex SafeCategory = new(
        "^[a-zA-Z][a-zA-Z0-9-]{0,79}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex SafeRuleId = new(
        "^[a-z0-9-]+(?:\\.[a-z0-9-]+)+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static void MapNativePrivateReferences(this WebApplication app)
    {
        var group = app.MapGroup("/api/private/rulesets");
        group.AddEndpointFilter(async (invocation, next) =>
        {
            var validator = invocation.HttpContext.RequestServices.GetRequiredService<PrivateReferenceTicketValidator>();
            var failure = await validator.ValidateAsync(invocation.HttpContext);
            if (failure is not null) return failure;
            if (invocation.HttpContext.RequestServices.GetRequiredService<RulesCoreSettings>()
                    .Ruleset.RulesetId != "first-party-001")
                return Results.Json(new { error = "catalog_access_denied" }, statusCode: 403);
            return await next(invocation);
        });

        group.MapGet("", (NativeCatalogRegistry registry, NativePublicDistributionPolicy policy) =>
        {
            var snapshots = registry.RulesetIds.OrderBy(id => id, StringComparer.Ordinal)
                .Select(id => registry.GetStore(id).Snapshot)
                .OfType<NativeCatalogSnapshot>().ToArray();
            return snapshots.Length == 0 ? Unavailable() : Results.Ok(new
            {
                rulesets = snapshots.Where(snapshot => NativePublicEndpoints.MayRead(snapshot, policy, true))
                    .Select(snapshot => new
                    {
                        id = snapshot.RulesetId,
                        entityCount = snapshot.Count,
                        fingerprint = snapshot.Fingerprint,
                        capabilities = new { read = true, write = false }
                    }).ToArray()
            });
        });

        group.MapGet("/{rulesetId}/rules", (string rulesetId, HttpRequest request, NativeCatalogRegistry registry,
            NativePublicDistributionPolicy policy) =>
        {
            var snapshot = Select(rulesetId, registry);
            if (snapshot is null && rulesetId == "first-party-001") return Unavailable();
            if (snapshot is null || !NativePublicEndpoints.MayRead(snapshot, policy, true))
                return Results.NotFound(new { error = "ruleset_not_found" });

            if (request.Query.Keys.Any(key => key is not ("q" or "category" or "limit" or "offset")) ||
                !ReadQuery(request, "q", out var query) || !ReadQuery(request, "category", out var category) ||
                !ReadQuery(request, "limit", out var rawLimit) || !ReadQuery(request, "offset", out var rawOffset) ||
                (query?.Length ?? 0) > 200 ||
                (!string.IsNullOrEmpty(category) && !SafeCategory.IsMatch(category)) ||
                !PageNumber(rawLimit, 50, 1, 100, out var limit) ||
                !PageNumber(rawOffset, 0, 0, 1_000_000, out var offset))
                return Results.BadRequest(new { error = "invalid_query" });

            var page = snapshot.Search(query, category, offset, limit);
            return Results.Ok(new
            {
                rulesetId = snapshot.RulesetId,
                fingerprint = snapshot.Fingerprint,
                total = page.Total,
                offset = page.Offset,
                limit = page.Limit,
                categoryFacets = snapshot.CountsByCategory.OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p => new { kind = p.Key, count = p.Value }).ToArray(),
                rules = page.Items.Select(e => new { id = e.Id, name = e.Name, kind = e.Category }).ToArray()
            });
        });

        group.MapGet("/{rulesetId}/rules/{ruleId}",
            (string rulesetId, string ruleId, NativeCatalogRegistry registry,
                NativePublicDistributionPolicy policy) =>
        {
            var snapshot = Select(rulesetId, registry);
            if (snapshot is null && rulesetId == "first-party-001") return Unavailable();
            if (snapshot is null || !NativePublicEndpoints.MayRead(snapshot, policy, true))
                return Results.NotFound(new { error = "ruleset_not_found" });
            if (ruleId.Length > 160 || !SafeRuleId.IsMatch(ruleId))
                return Results.BadRequest(new { error = "invalid_rule_id" });
            if (!snapshot.TryGetById(ruleId, out var rule) || rule is null)
                return Results.NotFound(new { error = "rule_not_found" });
            return Results.Ok(new
            {
                rulesetId = snapshot.RulesetId,
                fingerprint = snapshot.Fingerprint,
                rule = new { id = rule.Id, name = rule.Name, kind = rule.Category, definition = rule.Definition }
            });
        });
    }

    private static NativeCatalogSnapshot? Select(string rulesetId, NativeCatalogRegistry registry) =>
        registry.TryGetStore(rulesetId, out var store) ? store!.Snapshot : null;

    private static IResult Unavailable() =>
        Results.Json(new { error = "native_catalog_unavailable" }, statusCode: 503);

    private static bool ReadQuery(HttpRequest request, string key, out string? value)
    {
        value = null;
        if (!request.Query.TryGetValue(key, out var values)) return true;
        if (values.Count != 1) return false;
        value = values[0];
        return value is not null;
    }

    private static bool PageNumber(string? value, int fallback, int min, int max, out int result)
    {
        result = fallback;
        if (value is null) return true;
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) &&
               result >= min && result <= max;
    }
}
