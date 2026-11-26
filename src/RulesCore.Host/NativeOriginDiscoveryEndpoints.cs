using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// Read-only native configuration discovery. Only bounded builder metadata is
/// returned on ordinary delegated ingress, not private rule definitions.
/// </summary>
public static class NativeOriginDiscoveryEndpoints
{
    public static void MapNativeOriginDiscovery(this RouteGroupBuilder group)
    {
        group.MapGet("/origins/methods", (string fingerprint, string? creatureTypeId,
            NativeCatalogStore store, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (fingerprint != catalog.Fingerprint) return Error(409, "ruleset_version_mismatch");
            if (creatureTypeId is not null &&
                (string.IsNullOrWhiteSpace(creatureTypeId) || creatureTypeId.Length > 160))
                return Error(400, "invalid_creature_type");
            return ProjectMethods(catalog, creatureTypeId);
        });

        group.MapGet("/origins/choice-catalog", (string fingerprint, string catalogId,
            NativeCatalogStore store, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (fingerprint != catalog.Fingerprint) return Error(409, "ruleset_version_mismatch");
            if (string.IsNullOrWhiteSpace(catalogId) || catalogId.Length > 160)
                return Error(400, "invalid_origin_choice_catalog");
            var choices = NativeOriginChoiceCatalog.Discover(catalog, catalogId);
            if (choices.Status == "unsupported") return Error(404, "origin_choice_catalog_unsupported");
            return Results.Ok(new { contractVersion = 1, rulesetId = catalog.RulesetId,
                fingerprint = catalog.Fingerprint, previewOnly = true, choices });
        });

        group.MapGet("/origins/presets", (string fingerprint,
            NativeCatalogStore store, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (fingerprint != catalog.Fingerprint) return Error(409, "ruleset_version_mismatch");
            var presets = All(catalog, "species").Concat(All(catalog, "ancestry"))
                .Select(entity => new
                {
                    entity.Id, entity.Name, kind = entity.Category,
                    description = BoundedDescription(entity.Definition),
                    parentId = String(entity.Definition, "parent"),
                    selectable = !entity.Definition.TryGetProperty("selectable", out var flag) ||
                        flag.ValueKind != JsonValueKind.False,
                    defaultUpbringingId = String(entity.Definition, "defaultUpbringing"),
                    acquisitionMethodIds = Strings(entity.Definition, "acquisitionMethods"),
                    ancestrySelectionRequired = entity.Definition.TryGetProperty("ancestrySelection",
                        out var selection) && selection.ValueKind == JsonValueKind.Object &&
                        selection.TryGetProperty("required", out var required) &&
                        required.ValueKind == JsonValueKind.True
                }).ToArray();
            return Results.Ok(new
            {
                contractVersion = 1, rulesetId = catalog.RulesetId,
                fingerprint = catalog.Fingerprint, previewOnly = true, presets
            });
        });

        group.MapGet("/origins/construct/components/{definitionId}/configuration",
            (string definitionId, string fingerprint, NativeCatalogStore store, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (fingerprint != catalog.Fingerprint) return Error(409, "ruleset_version_mismatch");
            if (string.IsNullOrWhiteSpace(definitionId) || definitionId.Length > 160)
                return Error(400, "invalid_construct_component_id");
            var configuration = new NativeConstructAssemblyEvaluator(catalog)
                .DiscoverComponent(definitionId);
            if (configuration is null) return Error(404, "construct_component_not_found");
            return Results.Ok(new
            {
                contractVersion = 1, rulesetId = catalog.RulesetId,
                fingerprint = catalog.Fingerprint, previewOnly = true, configuration
            });
        });
    }

    /// <summary>Shared read-only method projection for delegated discovery and private reference.</summary>
    internal static IResult ProjectMethods(NativeCatalogSnapshot catalog, string? creatureTypeId)
    {
        const string ruleId = "rule.creature-origination";
        if (!catalog.TryGetById(ruleId, out var rule) || rule is null ||
            !rule.Definition.TryGetProperty("methodDefinitions", out var definitions) ||
            definitions.ValueKind != JsonValueKind.Array)
            return Error(422, "native_origination_methods_unavailable");
        var validator = new NativeOriginContractValidator(catalog);
        var availability = new NativeOriginMethodAvailabilityEvaluator(catalog);
        var methods = definitions.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.Object)
            .Select(x => String(x, "id")).Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).Select(id =>
                validator.FindMethod(new(ruleId, id!)))
            .Where(method => method is not null).Select(method =>
            {
                var defaults = creatureTypeId is null ? null :
                    availability.Resolve(creatureTypeId, method!.Reference);
                // Native listing and type defaults are not executable support.
                // This describes the existing transport, not new process rules.
                var support = method!.Reference.MethodId switch
                {
                    "biological" => "initial-preview",
                    "construct-assembly" => "assembly-preview",
                    _ when method.Stage == "post-origination" &&
                        method.Operation == "replace-complete-species-and-ancestry-configuration"
                        => "verified-reconstruction-plan",
                    _ => "reference-only"
                };
                // The current initial allowance is authored method metadata, not
                // a player-supplied price, an exception, or permission to activate.
                int? initialConstructionAllowance = method.Reference.MethodId == "construct-assembly" &&
                    method.AuthoredDefinition.TryGetProperty("initialConstructionAllowance", out var cap) &&
                    cap.ValueKind == JsonValueKind.Number && cap.TryGetInt32(out var amount) && amount >= 0
                        ? amount : null;
                return new
                {
                    method.Reference, method.Name, method.Specializes, method.Stage,
                    description = BoundedDescription(method.AuthoredDefinition),
                    initialConstructionAllowance,
                    method.CreatesNewIndividual, method.Operation, support,
                    defaultAvailability = defaults?.Status switch
                    {
                        NativeOriginMethodDefaultStatus.Available => "available",
                        NativeOriginMethodDefaultStatus.NotGranted => "not-granted",
                        NativeOriginMethodDefaultStatus.Unresolved => "unresolved",
                        _ => null
                    },
                    definingCreatureTypeId = defaults?.DefiningCreatureTypeId
                };
            }).ToArray();
        return Results.Ok(new
        {
            contractVersion = 1, rulesetId = catalog.RulesetId,
            fingerprint = catalog.Fingerprint, previewOnly = true, scope = creatureTypeId ?? "all", methods
        });
    }

    private static IEnumerable<NativeRuleEntity> All(NativeCatalogSnapshot catalog, string category)
    {
        for (var offset = 0; offset < catalog.CountByCategory(category); offset += 100)
            foreach (var item in catalog.Search(category: category, offset: offset, limit: 100).Items)
                yield return item;
    }

    private static string? String(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.String
            ? item.GetString() : null;

    // Project only authored player-facing description text, never review notes,
    // source bodies, or private definitions. Missing or oversized text stays absent.
    internal static string? BoundedDescription(JsonElement definition) =>
        String(definition, "description") is { Length: > 0 and <= 4000 } description &&
        !string.IsNullOrWhiteSpace(description) ? description : null;

    private static string[] Strings(JsonElement value, string key) =>
        value.TryGetProperty(key, out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()!).ToArray() : [];

    private static IResult Error(int status, string code) =>
        Results.Json(new { error = code }, statusCode: status);
}
