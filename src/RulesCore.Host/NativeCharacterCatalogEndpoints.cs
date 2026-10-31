using System.Globalization;
using Microsoft.AspNetCore.Routing;
using System.Text.Json;
using System.Text.RegularExpressions;
using RulesCore.Content;

namespace RulesCore.Host;

/// <summary>Native identities only; no compatibility concepts or source-path leakage.</summary>
public sealed record NativeCatalogResolveRequest(
    string RulesetId, string Fingerprint, IReadOnlyList<string> Ids);

public sealed record NativeCatalogRuleSummary(string Id, string Name, string Kind);
public sealed record NativeCatalogRuleDetail(string Id, string Name, string Kind, JsonElement Definition);
public sealed record NativeCatalogKindFacet(string Kind, int Count);

/// <summary>
/// Character Sheet discovery reads reuse the validated immutable catalog and its index.
/// Routes must only be mounted under the Character Sheet-authenticated mechanics group.
/// </summary>
public static class NativeCharacterCatalogEndpoints
{
    private static readonly Regex SafeRuleId = new(
        "^[a-z0-9-]+(?:\\.[a-z0-9-]+)+$",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex SafeCategory = new(
        "^[a-zA-Z][a-zA-Z0-9-]{0,79}$",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static RouteGroupBuilder MapNativeCharacterCatalog(this RouteGroupBuilder group)
    {
        group.MapGet("/catalog/rules", (HttpRequest request, NativeCatalogStore store) =>
        {
            var snapshot = store.Snapshot;
            if (snapshot is null) return Error(503, "native_catalog_unavailable");
            if (request.Query.Keys.Any(key => key is not (
                "fingerprint" or "q" or "category" or "limit" or "offset")) ||
                !SingleQuery(request, "fingerprint", out var fingerprint) ||
                !SingleQuery(request, "q", out var q) ||
                !SingleQuery(request, "category", out var category) ||
                !SingleQuery(request, "limit", out var rawLimit) ||
                !SingleQuery(request, "offset", out var rawOffset) ||
                (q?.Length ?? 0) > 200 ||
                (!string.IsNullOrEmpty(category) && !SafeCategory.IsMatch(category)) ||
                !PageNumber(rawLimit, 50, 1, 100, out var limit) ||
                !PageNumber(rawOffset, 0, 0, 1_000_000, out var offset))
                return Error(400, "invalid_catalog_query");
            if (!string.Equals(fingerprint, snapshot.Fingerprint, StringComparison.Ordinal))
                return Error(409, "ruleset_version_mismatch");

            var page = snapshot.Search(q, category, offset, limit);
            return Results.Ok(new
            {
                contractVersion = 1,
                rulesetId = snapshot.RulesetId,
                fingerprint = snapshot.Fingerprint,
                total = page.Total,
                offset = page.Offset,
                limit = page.Limit,
                categoryFacets = snapshot.CountsByCategory
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new NativeCatalogKindFacet(pair.Key, pair.Value)).ToArray(),
                rules = page.Items.Select(entity =>
                    new NativeCatalogRuleSummary(entity.Id, entity.Name, entity.Category)).ToArray()
            });
        });

        group.MapPost("/catalog/resolve", (NativeCatalogResolveRequest request, NativeCatalogStore store) =>
        {
            var snapshot = store.Snapshot;
            if (snapshot is null) return Error(503, "native_catalog_unavailable");
            if (request is null || request.Ids is null || request.Ids.Count is < 1 or > 100 ||
                request.Ids.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 160 ||
                    !SafeRuleId.IsMatch(id)) ||
                request.Ids.Distinct(StringComparer.Ordinal).Count() != request.Ids.Count)
                return Error(400, "invalid_catalog_id_list");
            if (!string.Equals(request.RulesetId, snapshot.RulesetId, StringComparison.Ordinal) ||
                !string.Equals(request.Fingerprint, snapshot.Fingerprint, StringComparison.Ordinal))
                return Error(409, "ruleset_version_mismatch");

            var resolved = new List<NativeCatalogRuleDetail>(request.Ids.Count);
            var missing = new List<string>();
            foreach (var id in request.Ids)
            {
                if (snapshot.TryGetById(id, out var entity) && entity is not null)
                    resolved.Add(new NativeCatalogRuleDetail(entity.Id, entity.Name,
                        entity.Category, entity.Definition));
                else
                    missing.Add(id);
            }
            return Results.Ok(new
            {
                contractVersion = 1,
                rulesetId = snapshot.RulesetId,
                fingerprint = snapshot.Fingerprint,
                status = missing.Count == 0 ? "resolved" : "partial",
                rules = resolved,
                missingIds = missing
            });
        });
        return group;
    }

    private static IResult Error(int status, string error) =>
        Results.Json(new { error }, statusCode: status);

    private static bool SingleQuery(HttpRequest request, string name, out string? value)
    {
        value = null;
        if (!request.Query.TryGetValue(name, out var values)) return true;
        if (values.Count != 1) return false;
        value = values[0];
        return value is not null;
    }

    private static bool PageNumber(string? value, int fallback, int minimum, int maximum, out int result)
    {
        result = fallback;
        return value is null || (int.TryParse(value, NumberStyles.None,
            CultureInfo.InvariantCulture, out result) && result >= minimum && result <= maximum);
    }
}
