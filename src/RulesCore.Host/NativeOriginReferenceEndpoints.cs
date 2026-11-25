using System.Text.Json;
using System.Text.RegularExpressions;
using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>Snapshot-pinned, first-party authored Origin reference relationships.
/// No character proposal, grant eligibility or execution is evaluated here.</summary>
internal static class NativeOriginReferenceEndpoints
{
    private const int FamilyLimit = 256;
    private const int LineageLimit = 64;
    private static readonly Regex SafeId = new(
        @"\A[a-z0-9-]+(?:\.[a-z0-9-]+)+\z",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    internal static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{rulesetId}/origins/hierarchy/{family}",
            (string rulesetId, string family, HttpRequest request, NativeCatalogRegistry registry,
                NativePublicDistributionPolicy policy, NativeReferenceAccessPolicy access) =>
                Read(rulesetId, request, registry, policy, access, catalog => Hierarchy(catalog, family)));
        group.MapGet("/{rulesetId}/origins/source/{sourceId}",
            (string rulesetId, string sourceId, HttpRequest request, NativeCatalogRegistry registry,
                NativePublicDistributionPolicy policy, NativeReferenceAccessPolicy access) =>
                Read(rulesetId, request, registry, policy, access, catalog => Source(catalog, sourceId)));
        group.MapGet("/{rulesetId}/origins/trait-sources/{traitId}",
            (string rulesetId, string traitId, HttpRequest request, NativeCatalogRegistry registry,
                NativePublicDistributionPolicy policy, NativeReferenceAccessPolicy access) =>
                Read(rulesetId, request, registry, policy, access, catalog => TraitSources(catalog, traitId)));
        group.MapGet("/{rulesetId}/origins/component/{componentId}",
            (string rulesetId, string componentId, HttpRequest request, NativeCatalogRegistry registry,
                NativePublicDistributionPolicy policy, NativeReferenceAccessPolicy access) =>
                Read(rulesetId, request, registry, policy, access, catalog => Component(catalog, componentId)));
    }

    private static IResult Read(string rulesetId, HttpRequest request, NativeCatalogRegistry registry,
        NativePublicDistributionPolicy policy, NativeReferenceAccessPolicy access,
        Func<NativeCatalogSnapshot, IResult> project)
    {
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        if (rulesetId != "first-party-001")
            return Error(404, "origin_reference_unavailable");
        var catalog = registry.TryGetStore(rulesetId, out var store) ? store!.Snapshot : null;
        if (catalog is null) return Error(503, "native_catalog_unavailable");
        if (!NativePublicEndpoints.MayRead(catalog, policy, access, true))
            return Error(404, "ruleset_not_found");
        if (request.Query.Count != 1 ||
            !request.Query.TryGetValue("fingerprint", out var fingerprint) ||
            fingerprint.Count != 1 || string.IsNullOrWhiteSpace(fingerprint[0]) ||
            fingerprint[0]!.Length > 160)
            return Error(400, "invalid_query");
        if (fingerprint[0] != catalog.Fingerprint)
            return Error(409, "ruleset_version_mismatch");
        return project(catalog);
    }

    private static IResult Hierarchy(NativeCatalogSnapshot catalog, string family)
    {
        string[] categories = family switch
        {
            "biological" => ["species", "ancestry"],
            "upbringing" => ["upbringing"],
            "construct-components" => ["construct-component"],
            _ => []
        };
        if (categories.Length == 0) return Error(404, "origin_family_not_found");
        if (!TryFamily(catalog, categories, out var members))
            return Error(422, "origin_reference_family_limit");
        var rows = members.Select(e => new
        {
            id = e.Id, name = e.Name, kind = e.Category,
            parentId = Text(e.Definition, "parent"),
            selectable = e.Category == "construct-component" ? (bool?)null :
                !e.Definition.TryGetProperty("selectable", out var flag) || flag.ValueKind != JsonValueKind.False,
            defaultUpbringingId = Text(e.Definition, "defaultUpbringing"),
            acquisitionMethodIds = Strings(e.Definition, "acquisitionMethods"),
            status = Text(e.Definition, "status"),
            rulesStatus = Text(e.Definition, "rulesStatus"),
            componentCategory = Text(e.Definition, "componentCategory")
        }).ToArray();
        return Results.Ok(new { contractVersion = 1, rulesetId = catalog.RulesetId,
            fingerprint = catalog.Fingerprint, family, complete = true, rows });
    }

    private static IResult Source(NativeCatalogSnapshot catalog, string sourceId)
    {
        if (!ValidId(sourceId)) return Error(400, "invalid_rule_id");
        if (!catalog.TryGetById(sourceId, out var selected) || selected is null ||
            selected.Category is not ("species" or "ancestry" or "upbringing"))
            return Error(404, "origin_source_not_found");
        var upbringing = selected.Category == "upbringing";
        var chain = new List<NativeRuleEntity>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        NativeRuleEntity? current = selected;
        while (current is not null)
        {
            if (!visited.Add(current.Id) || chain.Count >= LineageLimit ||
                (upbringing ? current.Category != "upbringing" :
                    current.Category is not ("species" or "ancestry")))
                return Error(422, "origin_reference_lineage_invalid");
            chain.Add(current);
            var parent = Text(current.Definition, "parent");
            if (parent is null)
            {
                if (current.Category == "ancestry")
                    return Error(422, "origin_reference_lineage_incomplete");
                break;
            }
            if (!catalog.TryGetById(parent, out current) || current is null)
                return Error(422, "origin_reference_lineage_incomplete");
        }
        chain.Reverse();
        var lineage = chain.Select(e => new
        {
            id = e.Id, name = e.Name, kind = e.Category, sourcePath = e.SourcePath,
            inheritedSource = e.Id != sourceId,
            definition = e.Definition,
            traitOccurrences = Occurrences(catalog, e)
        }).ToArray();
        return Results.Ok(new { contractVersion = 1, rulesetId = catalog.RulesetId,
            fingerprint = catalog.Fingerprint, sourceId, lineage, effectiveProjectionAvailable = false });
    }

    private sealed record TraitOccurrence(string SourceId, string Path, int Index,
        string? CanonicalTraitId, string? LocalId, JsonElement Trait);

    private static TraitOccurrence[] Occurrences(NativeCatalogSnapshot catalog, NativeRuleEntity entity)
    {
        if (entity.Category is not ("species" or "ancestry") ||
            !entity.Definition.TryGetProperty("traits", out var traits) ||
            traits.ValueKind != JsonValueKind.Array) return [];
        return traits.EnumerateArray().Select((trait, index) =>
            new TraitOccurrence(entity.Id, "/traits/" + index, index,
                catalog.OriginTraitDefinitionId(entity.Id, index), Text(trait, "id"), trait)).ToArray();
    }

    private sealed record SourceLink(string SourceId, string Name, string Kind, string Path, string Relation);

    private static IResult TraitSources(NativeCatalogSnapshot catalog, string traitId)
    {
        if (!ValidId(traitId)) return Error(400, "invalid_rule_id");
        if (!catalog.TryGetById(traitId, out var trait) || trait?.Category != "origin-trait")
            return Error(404, "origin_trait_not_found");
        if (!TryFamily(catalog, ["species", "ancestry", "construct-component"], out var members))
            return Error(422, "origin_reference_family_limit");
        var sources = new List<SourceLink>();
        foreach (var e in members)
        {
            foreach (var occurrence in Occurrences(catalog, e))
                if (occurrence.CanonicalTraitId == traitId)
                    sources.Add(new(e.Id, e.Name, e.Category, occurrence.Path, "biological-occurrence"));
            if (e.Category == "construct-component" &&
                e.Definition.TryGetProperty("operationalOriginTraitIds", out var ids) &&
                ids.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var id in ids.EnumerateArray())
                {
                    if (id.ValueKind == JsonValueKind.String && id.GetString() == traitId)
                        sources.Add(new(e.Id, e.Name, e.Category, "/operationalOriginTraitIds/" + index,
                            "construct-operational-reference"));
                    index++;
                }
            }
        }
        return Results.Ok(new { contractVersion = 1, rulesetId = catalog.RulesetId,
            fingerprint = catalog.Fingerprint, traitId, complete = true, sources });
    }

    private static IResult Component(NativeCatalogSnapshot catalog, string componentId)
    {
        if (!ValidId(componentId)) return Error(400, "invalid_rule_id");
        if (!catalog.TryGetById(componentId, out var component) || component?.Category != "construct-component")
            return Error(404, "construct_component_not_found");
        if (!TryFamily(catalog, ["crafting-recipe", "construct-design", "construct-enhancement",
            "construct-power-source"], out var members))
            return Error(422, "origin_reference_family_limit");
        var configuration = new NativeConstructAssemblyEvaluator(catalog).DiscoverComponent(componentId);
        if (configuration is null) return Error(404, "construct_component_not_found");
        var recipes = members.Where(e => e.Category == "crafting-recipe" &&
            Text(e.Definition, "producesDefinitionId") == componentId)
            .Select(e => new { id = e.Id, name = e.Name }).ToArray();
        return Results.Ok(new { contractVersion = 1, rulesetId = catalog.RulesetId,
            fingerprint = catalog.Fingerprint, componentId,
            componentCategory = Text(component.Definition, "componentCategory"),
            sourcePath = component.SourcePath, definition = component.Definition,
            configuration, recipes, materialCompatibilityAvailable = false });
    }

    // Complete-family semantics: never return a silently truncated tree or reverse lookup.
    // Only named Origin categories are scanned, not the entire catalog.
    private static bool TryFamily(NativeCatalogSnapshot catalog, string[] categories, out NativeRuleEntity[] members)
    {
        members = [];
        if (categories.Sum(catalog.CountByCategory) > FamilyLimit) return false;
        var result = new List<NativeRuleEntity>();
        foreach (var category in categories)
            for (var offset = 0; offset < catalog.CountByCategory(category); offset += 100)
                result.AddRange(catalog.Search(category: category, offset: offset, limit: 100).Items);
        members = result.OrderBy(e => e.Id, StringComparer.Ordinal).ToArray();
        return true;
    }

    private static bool ValidId(string id) => id.Length <= 160 && SafeId.IsMatch(id);
    private static string? Text(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var item) &&
        item.ValueKind == JsonValueKind.String ? item.GetString() : null;
    private static string[] Strings(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var items) &&
        items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()!).ToArray() : [];
    private static IResult Error(int status, string code) =>
        Results.Json(new { error = code }, statusCode: status);
}
