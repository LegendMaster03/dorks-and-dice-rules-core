using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using RulesCore.Content;

namespace RulesCore.Host;

// Explicit transport DTOs; no internal NativeRuleEntity (including SourcePath) is serialized.
public sealed record PublicRulesetCapabilities(bool Read, bool Write);
public sealed record PublicRulesetSummary(string Id, int EntityCount, string Fingerprint,
    PublicRulesetCapabilities Capabilities);
public sealed record PublicRulesetsResponse(PublicRulesetSummary[] Rulesets);
public sealed record PublicRuleSummary(string Id, string Name, string Kind);
public sealed record PublicCategoryFacet(string Kind, int Count);
public sealed record PublicRulesPage(string RulesetId, string Fingerprint, int Total, int Offset, int Limit,
    PublicCategoryFacet[] CategoryFacets, PublicRuleSummary[] Rules);
public sealed record PublicRuleDetail(string Id, string Name, string Kind, JsonElement Definition);
public sealed record PublicRuleResponse(string RulesetId, string Fingerprint, PublicRuleDetail Rule);

/// <summary>GET-only native consumer API, mapped exclusively on PublicOnly ingress.</summary>
public static class NativePublicEndpoints
{
    private static readonly Regex SafeRulesetId = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex SafeCategory = new(
        "^[a-zA-Z][a-zA-Z0-9-]{0,79}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex SafeRuleId = new(
        "^[a-z0-9-]+(?:\\.[a-z0-9-]+)+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static void MapNativePublicReferences(this WebApplication app)
    {
        var group = app.MapGroup("/api/rulesets");
        group.AddEndpointFilter(async (invocation, next) =>
        {
            invocation.HttpContext.Response.Headers.CacheControl = "no-store";
            invocation.HttpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return await next(invocation);
        });

        group.MapGet("", (HttpRequest request, NativeCatalogStore store, NativePublicDistributionPolicy policy) =>
        {
            var snapshot = store.Snapshot;
            if (snapshot is null) return Unavailable();
            if (request.Query.Count != 0) return Results.BadRequest(new { error = "invalid_query" });
            PublicRulesetSummary[] visible = policy.Allows(snapshot)
                ? [new PublicRulesetSummary(snapshot.RulesetId, snapshot.Count, snapshot.Fingerprint,
                    new PublicRulesetCapabilities(true, false))]
                : [];
            return Results.Ok(new PublicRulesetsResponse(visible));
        });

        group.MapGet("/{rulesetId}/rules",
            (string rulesetId, HttpRequest request, NativeCatalogStore store,
                NativePublicDistributionPolicy policy) =>
        {
            var snapshot = store.Snapshot;
            if (snapshot is null) return Unavailable();
            if (!ValidRulesetId(rulesetId)) return InvalidId();
            if (!Matches(snapshot, policy, rulesetId)) return NotFound();

            if (request.Query.Keys.Any(key => key is not ("q" or "category" or "limit" or "offset")) ||
                !ReadQuery(request, "q", out var query) || !ReadQuery(request, "category", out var category) ||
                !ReadQuery(request, "limit", out var rawLimit) || !ReadQuery(request, "offset", out var rawOffset) ||
                (query?.Length ?? 0) > 200 ||
                (!string.IsNullOrEmpty(category) && !SafeCategory.IsMatch(category)) ||
                !PageNumber(rawLimit, 50, 1, 100, out var limit) ||
                !PageNumber(rawOffset, 0, 0, 1_000_000, out var offset))
                return Results.BadRequest(new { error = "invalid_query" });

            var page = snapshot.Search(query, category, offset, limit);
            return Results.Ok(new PublicRulesPage(snapshot.RulesetId, snapshot.Fingerprint, page.Total,
                page.Offset, page.Limit,
                snapshot.CountsByCategory.OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p => new PublicCategoryFacet(p.Key, p.Value)).ToArray(),
                page.Items.Select(e => new PublicRuleSummary(e.Id, e.Name, e.Category)).ToArray()));
        });

        group.MapGet("/{rulesetId}/rules/{ruleId}",
            (string rulesetId, string ruleId, HttpRequest request, NativeCatalogStore store,
                NativePublicDistributionPolicy policy) =>
        {
            var snapshot = store.Snapshot;
            if (snapshot is null) return Unavailable();
            if (!ValidRulesetId(rulesetId) || ruleId.Length > 160 || !SafeRuleId.IsMatch(ruleId))
                return InvalidId();
            if (!Matches(snapshot, policy, rulesetId)) return NotFound();
            if (request.Query.Count != 0) return Results.BadRequest(new { error = "invalid_query" });
            if (!snapshot.TryGetById(ruleId, out var rule) || rule is null)
                return Results.NotFound(new { error = "rule_not_found" });
            return Results.Ok(new PublicRuleResponse(snapshot.RulesetId, snapshot.Fingerprint,
                new PublicRuleDetail(rule.Id, rule.Name, rule.Category, rule.Definition)));
        });
    }

    private static bool Matches(NativeCatalogSnapshot snapshot, NativePublicDistributionPolicy policy,
        string rulesetId) =>
        string.Equals(rulesetId, snapshot.RulesetId, StringComparison.Ordinal) && policy.Allows(snapshot);

    private static bool ValidRulesetId(string id) => id.Length <= 80 && SafeRulesetId.IsMatch(id);
    private static IResult NotFound() => Results.NotFound(new { error = "ruleset_not_found" });
    private static IResult InvalidId() => Results.BadRequest(new { error = "invalid_id" });
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
