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
    private const int RelationshipLimit = 1024;
    private static readonly Regex SafeId = new(
        @"\A[a-z0-9-]+(?:\.[a-z0-9-]+)+\z",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    internal static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{rulesetId}/origins/methods/{scope}",
            (string rulesetId, string scope, HttpRequest request, NativeCatalogRegistry registry,
                NativePublicDistributionPolicy policy, NativeReferenceAccessPolicy access) =>
                Read(rulesetId, request, registry, policy, access, catalog => Methods(catalog, scope)));
        group.MapGet("/{rulesetId}/origins/component-sources/{targetId}",
            (string rulesetId, string targetId, HttpRequest request, NativeCatalogRegistry registry,
                NativePublicDistributionPolicy policy, NativeReferenceAccessPolicy access) =>
                Read(rulesetId, request, registry, policy, access, catalog => ComponentSources(catalog, targetId)));
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

    private static IResult Methods(NativeCatalogSnapshot catalog, string scope)
    {
        if (catalog.TryGetById("rule.creature-origination", out var rule) && rule is not null &&
            ArrayLength(rule.Definition, "methodDefinitions") > FamilyLimit)
            return Error(422, "origin_reference_family_limit");
        if (scope == "all") return NativeOriginDiscoveryEndpoints.ProjectMethods(catalog, null);
        if (!ValidId(scope) || !scope.StartsWith("creature-type.", StringComparison.Ordinal))
            return Error(400, "invalid_creature_type");
        if (!catalog.TryGetById(scope, out var type) || type?.Category != "creatureType")
            return Error(404, "creature_type_not_found");
        return NativeOriginDiscoveryEndpoints.ProjectMethods(catalog, scope);
    }

    private static IResult Hierarchy(NativeCatalogSnapshot catalog, string family)
    {
        string[] categories = family switch
        {
            "biological" => ["species", "ancestry"],
            "upbringing" => ["upbringing"],
            "construct-components" => ["construct-component"],
            "characteristics" => ["origin-trait"],
            "backgrounds" => ["background"],
            "construct-options" => ["construct-design", "construct-enhancement", "construct-power-source", "construct-material", "crafting-recipe"],
            _ => []
        };
        if (categories.Length == 0) return Error(404, "origin_family_not_found");
        if (!TryFamily(catalog, categories, out var members))
            return Error(422, "origin_reference_family_limit");
        var rows = members.Select(e => new
        {
            id = e.Id, name = e.Name, kind = e.Category,
            parentId = Text(e.Definition, "parent"),
            selectable = Selectable(e),
            defaultUpbringingId = Text(e.Definition, "defaultUpbringing"),
            acquisitionMethodIds = Strings(e.Definition, "acquisitionMethods"),
            status = Text(e.Definition, "status"),
            rulesStatus = Text(e.Definition, "rulesStatus"),
            componentCategory = Text(e.Definition, "componentCategory"),
            ancestrySelectionRequired = RequiresAncestry(e.Definition),
            capabilities = Strings(e.Definition, "provides"),
            compatibleComponentCapabilities = Strings(e.Definition, "compatibleComponentCapabilities"),
            producesDefinitionId = Text(e.Definition, "producesDefinitionId"),
            characteristicFamily = e.Category != "origin-trait" ? null :
                e.SourcePath.StartsWith("native/origination/biological/genes/", StringComparison.Ordinal) ? "biological" :
                e.SourcePath.StartsWith("native/origination/construct-assembly/", StringComparison.Ordinal) ? "construct" : null
        }).ToArray();
        return Results.Ok(new { contractVersion = 1, rulesetId = catalog.RulesetId,
            fingerprint = catalog.Fingerprint, family, complete = true, rows });
    }

    // Species/Ancestry use the existing preset default. Life experience requires
    // explicit selectable=true; other families are reference records, not choices.
    private static bool? Selectable(NativeRuleEntity entity) => entity.Category switch
    {
        "species" or "ancestry" => !entity.Definition.TryGetProperty("selectable", out var biological) ||
            biological.ValueKind != JsonValueKind.False,
        "upbringing" or "background" => entity.Definition.TryGetProperty("selectable", out var cultural) &&
            cultural.ValueKind == JsonValueKind.True,
        _ => null
    };

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
                if (current.Definition.TryGetProperty("parent", out _))
                    return Error(422, "origin_reference_lineage_invalid");
                if (current.Category == "ancestry")
                    return Error(422, "origin_reference_lineage_incomplete");
                break;
            }
            if (!catalog.TryGetById(parent, out current) || current is null)
                return Error(422, "origin_reference_lineage_incomplete");
        }
        if (chain.Sum(e => (long)ArrayLength(e.Definition, "traits")) > RelationshipLimit)
            return Error(422, "origin_reference_relationship_limit");
        chain.Reverse();
        var lineage = chain.Select(e => new
        {
            id = e.Id, name = e.Name, kind = e.Category, sourcePath = e.SourcePath,
            inheritedSource = e.Id != sourceId,
            definition = e.Definition,
            traitOccurrences = Occurrences(catalog, e)
        }).ToArray();
        return Results.Ok(new { contractVersion = 1, rulesetId = catalog.RulesetId,
            fingerprint = catalog.Fingerprint, sourceId, lineage, effectiveProjectionAvailable = false,
            directReference = upbringing ? null : DirectReference(catalog, selected, chain) });
    }

    private static bool RequiresAncestry(JsonElement definition) =>
        definition.TryGetProperty("ancestrySelection", out var selection) &&
        selection.ValueKind == JsonValueKind.Object &&
        selection.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.True;

    // Reuse the existing source deconstructor; never construct a character or
    // claim that a reference selection grants a trait. Values are restricted to
    // a unique fixed intrinsic offer and unresolved choices remain visible.
    private static object? DirectReference(NativeCatalogSnapshot catalog, NativeRuleEntity selected,
        IReadOnlyList<NativeRuleEntity> chain)
    {
        if (selected.Definition.TryGetProperty("selectable", out var selectable) &&
            selectable.ValueKind == JsonValueKind.False ||
            selected.Category == "species" && RequiresAncestry(selected.Definition))
            return null;
        var (proposal, issues) = new NativeOriginSourceDeconstructor(catalog).Deconstruct(
            new NativeOriginProposal("reference", NativeOriginIntent.Direct,
                new("rule.creature-origination", "biological"), [], [], [],
                SelectedSpeciesId: chain[0].Id,
                SelectedAncestryId: selected.Category == "ancestry" ? selected.Id : null));
        var candidates = proposal.Candidates.Select(candidate => new
        {
            sourceId = candidate.Identity.Occurrence.Source.Id,
            path = candidate.Identity.Occurrence.OccurrencePath,
            slot = candidate.Identity.IntrinsicSlotId,
            offeredValue = candidate.OfferedValue,
            authoredParameters = candidate.OfferedParameters,
            requiredChoices = candidate.RequiredChoices,
            unresolvedEvidence = candidate.UnresolvedEvidence,
            sourceRestriction = candidate.SelectionBasis.Eligibility == NativeOriginEligibility.Restricted
                ? candidate.SelectionBasis.Explanation : null
        }).ToArray();
        FixedIntrinsic[] fixedValues = issues.Count != 0 ? [] : proposal.Candidates
            .Where(c => c.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic &&
                c.SelectionBasis.Eligibility != NativeOriginEligibility.Restricted)
            .GroupBy(c => c.Identity.IntrinsicSlotId, StringComparer.Ordinal)
            .Where(g => g.Key is "creatureType" or "size" or "walkingSpeedFeet" &&
                g.Count() == 1 && g.Single().SelectionBasis.Eligibility == NativeOriginEligibility.Eligible &&
                g.Single().OfferedValue is not null &&
                (g.Single().RequiredChoices?.Count ?? 0) == 0 &&
                (g.Single().UnresolvedEvidence?.Count ?? 0) == 0)
            .Select(g => new FixedIntrinsic(g.Key!, g.Single().OfferedValue!.Value,
                g.Single().Identity.Occurrence.Source.Id, g.Single().Identity.Occurrence.OccurrencePath))
            .ToArray();
        return new { methodId = "biological", referenceOnly = true, candidates, fixedValues, issues };
    }

    private sealed record FixedIntrinsic(string Slot, JsonElement Value, string SourceId, string Path);

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
        if (!TryFamily(catalog, ["species", "ancestry", "construct-component", "construct-design",
            "construct-enhancement", "construct-power-source", "construct-material"], out var members))
            return Error(422, "origin_reference_family_limit");
        if (members.Sum(e => (long)ArrayLength(e.Definition, "traits") +
            ArrayLength(e.Definition, "operationalOriginTraitIds")) > RelationshipLimit)
            return Error(422, "origin_reference_relationship_limit");
        var sources = new List<SourceLink>();
        foreach (var e in members)
        {
            foreach (var occurrence in Occurrences(catalog, e))
                if (occurrence.CanonicalTraitId == traitId)
                    sources.Add(new(e.Id, e.Name, e.Category, occurrence.Path, "biological-occurrence"));
            if (e.Category.StartsWith("construct-", StringComparison.Ordinal) &&
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
            "construct-power-source", "construct-material"], out var members))
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
            configuration, recipes, materialCompatibilityAvailable = false,
            materialAssignmentOptions = members
                .Where(e => configuration.MaterialSlots.Count != 0 && e.Category == "construct-material")
                .Select(e => new { id = e.Id, name = e.Name }).ToArray() });
    }

    private static IResult ComponentSources(NativeCatalogSnapshot catalog, string targetId)
    {
        if (!ValidId(targetId)) return Error(400, "invalid_rule_id");
        if (!catalog.TryGetById(targetId, out var target) || target is null ||
            target.Category is not ("construct-design" or "construct-enhancement" or
                "construct-power-source" or "construct-material" or "crafting-recipe"))
            return Error(404, "construct_reference_target_not_found");
        if (!TryFamily(catalog, ["construct-component", "construct-design", "construct-enhancement",
            "construct-power-source", "construct-material", "crafting-recipe"], out var members))
            return Error(422, "origin_reference_family_limit");
        var evaluator = new NativeConstructAssemblyEvaluator(catalog);
        var sources = new List<SourceLink>();
        foreach (var e in members.Where(e => e.Category == "construct-component"))
        {
            var config = evaluator.DiscoverComponent(e.Id);
            if (config is null) continue;
            void Link(string path, string relation) => sources.Add(new(e.Id, e.Name, e.Category, path, relation));
            if (config.Designs.Any(x => x.Id == targetId)) Link("/provides", "compatible-design");
            if (config.Enhancements.Any(x => x.Id == targetId)) Link("/provides", "compatible-enhancement");
            if (config.PowerSources.Any(x => x.Id == targetId)) Link("/powerSourceSlot", "compatible-power-source");
            if (target.Category == "construct-material" && config.MaterialSlots.Count != 0)
                Link("/materialSlots", "material-assignment-candidate");
            if (config.DefaultRecipeId == targetId) Link("/craftingRecipeId", "default-recipe");
            if (config.ConstructionVariants.Any(x => x.RecipeId == targetId))
                Link("/constructionVariants", "variant-recipe");
        }
        if (sources.Count > RelationshipLimit)
            return Error(422, "origin_reference_relationship_limit");
        // The output pointer belongs to the recipe, not to its product.
        var productId = target.Category == "crafting-recipe" ? Text(target.Definition, "producesDefinitionId") : null;
        var output = productId is not null && catalog.TryGetById(productId, out var product) && product is not null
            ? new { id = product.Id, name = product.Name, kind = product.Category, path = "/producesDefinitionId" } : null;
        var recipes = members.Where(e => e.Category == "crafting-recipe" &&
            Text(e.Definition, "producesDefinitionId") == targetId)
            .Select(e => new { id = e.Id, name = e.Name }).ToArray();
        return Results.Ok(new { contractVersion = 1, rulesetId = catalog.RulesetId,
            fingerprint = catalog.Fingerprint, targetId, complete = true, sources, output, recipes });
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

    private static int ArrayLength(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var items) &&
        items.ValueKind == JsonValueKind.Array ? items.GetArrayLength() : 0;
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
