using System.Collections.Frozen;
using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// One installed, catalog-verified product requiring a manufacturing recipe.
/// The caller supplies the already-validated selected material slots, not a price.
/// </summary>
internal sealed record NativeCraftingQuotedSelection(
    string SourceDefinitionId, IReadOnlyDictionary<string, string>? MaterialSlots,
    string? ConstructionVariantId = null, string? InstanceId = null);

// Presentation-only copies of already validated recipe operations and external
// ingredient prices. No performer readiness, resource expenditure or grant.
public sealed record NativeCraftingQuotedStage(string StepId, string CompetencyId,
    int MinimumRanks, IReadOnlyList<string> Dependencies,
    IReadOnlyList<NativeCraftingMaterialQuantity> Inputs,
    IReadOnlyList<NativeCraftingMaterialQuantity> Outputs);
public sealed record NativeCraftingQuotedRecipe(string? InstanceId, string SourceDefinitionId,
    string RecipeId, IReadOnlyList<NativeCraftingQuotedStage> Stages);
public sealed record NativeCraftingQuotedIngredient(string DefinitionId, long Quantity,
    int? UnitCost, long? KnownCost);
public sealed record NativeCraftingQuotePresentation(string Status,
    IReadOnlyList<NativeCraftingQuotedRecipe> Recipes,
    IReadOnlyList<NativeCraftingQuotedIngredient> Ingredients);

internal sealed record NativeCraftingResourceQuote(
    long KnownCost, IReadOnlyDictionary<string, long> RequiredResources,
    IReadOnlyList<NativeOriginContractIssue> Issues)
{
    // Read-only material evidence; not a finished-item weight or encumbrance
    // calculation. A recipe ingredient may be consumed without being retained.
    public long KnownRetainedMaterialMassPounds { get; init; }
    public IReadOnlyList<string> UnresolvedRetainedMassMaterials { get; init; } = [];
    // A complete result requires every selected manufactured product to have
    // author-verified material accounting. Ordinary quotes remain partial.
    public bool IsPhysicalMassComplete { get; init; }
    public IReadOnlyList<string> ProductsWithUncertifiedMass { get; init; } = [];
    public NativeCraftingQuotePresentation? Presentation { get; init; }
}

/// <summary>
/// Quotes external recipe inputs using a native method's authored starting-resource
/// values. Intermediate stage outputs offset later inputs within the same recipe.
/// This does not manufacture items, verify historical NPC proficiency, or change
/// inventory. Both recipes and prices must come from the native catalog.
/// </summary>
internal static class NativeCraftingResourceQuoter
{
    internal static NativeCraftingResourceQuote Evaluate(
        NativeCatalogSnapshot catalog, JsonElement method,
        IReadOnlyList<NativeCraftingQuotedSelection> selections)
    {
        var issues = new List<NativeOriginContractIssue>();
        var required = new Dictionary<string, long>(StringComparer.Ordinal);
        long knownRetainedMassPounds = 0;
        var unresolvedMass = new SortedSet<string>(StringComparer.Ordinal);
        var uncertifiedProducts = new List<string>();
        var recipeViews = new List<NativeCraftingQuotedRecipe>();
        var ingredientViews = new List<NativeCraftingQuotedIngredient>();
        // Presentation is bounded independently of the existing evaluator.
        // A large legal quotation keeps its original mechanical result; only
        // the detailed presentation becomes unavailable, never silently truncated.
        const int maxPresentationStages = 2048;
        const int maxPresentationEntries = 32768;
        int presentationStages = 0, presentationEntries = 0;
        bool presentationAvailable = true;

        void Issue(string code, string path, NativeOriginIssueSeverity severity, string detail) =>
            issues.Add(new(code, path, severity, detail));

        if (selections.Count > 4096)
        {
            Issue("crafting-quote-selection-limit", "construction",
                NativeOriginIssueSeverity.Invalid, "Too many recipe selections.");
            return new(0, required, issues);
        }

        var prices = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!method.TryGetProperty("startingResourceUnitCosts", out var priceData) ||
            priceData.ValueKind != JsonValueKind.Object)
            Issue("crafting-quote-prices-unresolved", "method.startingResourceUnitCosts",
                NativeOriginIssueSeverity.Unresolved,
                "The native construction method needs a starting-resource price schedule.");
        else
        {
            foreach (var entry in priceData.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(entry.Name) ||
                    entry.Value.ValueKind != JsonValueKind.Number ||
                    !entry.Value.TryGetInt32(out var price) || price is < 0 or > 1_000_000)
                    Issue("crafting-quote-price-invalid",
                        "method.startingResourceUnitCosts." + entry.Name,
                        NativeOriginIssueSeverity.Invalid,
                        "Resource prices must be bounded nonnegative integers.");
                else if (!catalog.TryGetById(entry.Name, out _))
                    Issue("crafting-quote-resource-unknown",
                        "method.startingResourceUnitCosts." + entry.Name,
                        NativeOriginIssueSeverity.Invalid,
                        "A priced resource must be a native catalog identity.");
                else
                    prices.Add(entry.Name, price);
            }
        }

        foreach (var selection in selections)
        {
            // Each occurrence is independent. Duplicate installed parts must
            // never hide a missing mass declaration on one of those parts.
            uncertifiedProducts.Add(selection.SourceDefinitionId);
            var path = "construction." + selection.SourceDefinitionId;
            if (!catalog.TryGetById(selection.SourceDefinitionId, out var source) ||
                source is null)
            {
                Issue("crafting-quote-product-unknown", path,
                    NativeOriginIssueSeverity.Invalid,
                    "The constructed product must be a native definition.");
                continue;
            }
            var resolved = NativeConstructRecipeVariantResolver.Resolve(
                catalog, selection.SourceDefinitionId, selection.ConstructionVariantId,
                path, Issue);
            if (resolved is null) continue;
            catalog.TryGetById(resolved.RecipeId, out var recipe);

            var before = issues.Count;
            var usedMaterialSlots = new HashSet<string>(StringComparer.Ordinal);
            var operations = new List<NativeCraftingProcessStep>();
            if (recipe.Definition.TryGetProperty("stages", out var stages))
            {
                if (recipe.Definition.TryGetProperty("inputs", out _) ||
                    stages.ValueKind != JsonValueKind.Array ||
                    stages.GetArrayLength() is < 1 or > 100)
                {
                    Issue("crafting-quote-stages-invalid", path,
                        NativeOriginIssueSeverity.Invalid,
                        "A staged recipe must have one to 100 stages and no duplicate top-level inputs.");
                    continue;
                }

                foreach (var stage in stages.EnumerateArray())
                {
                    if (stage.ValueKind != JsonValueKind.Object ||
                        !String(stage, "stepId", out var stepId) ||
                        !String(stage, "competencyId", out var competencyId) ||
                        !stage.TryGetProperty("minimumRanks", out var minimumField) ||
                        minimumField.ValueKind != JsonValueKind.Number ||
                        !minimumField.TryGetInt32(out var minimum) ||
                        minimum is < 0 or > 1_000_000 ||
                        !catalog.TryGetById(competencyId!, out var competency) ||
                        competency?.Category != "competency")
                    {
                        Issue("crafting-quote-stage-invalid", path,
                            NativeOriginIssueSeverity.Invalid,
                            "Every stage needs a valid ID, native competency, and minimum rank.");
                        continue;
                    }
                    var dependencies = new List<string>();
                    if (stage.TryGetProperty("dependencies", out var deps))
                    {
                        if (deps.ValueKind != JsonValueKind.Array ||
                            deps.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(x.GetString())))
                        {
                            Issue("crafting-quote-dependencies-invalid", path + "." + stepId,
                                NativeOriginIssueSeverity.Invalid,
                                "Stage dependencies must be native-authored identifiers.");
                            continue;
                        }
                        dependencies.AddRange(deps.EnumerateArray().Select(x => x.GetString()!));
                    }
                    var inputs = ReadMaterials(catalog, stage, "inputs", true,
                        selection.MaterialSlots, path + "." + stepId, Issue,
                        usedMaterialSlots);
                    var outputs = ReadMaterials(catalog, stage, "outputs", false,
                        selection.MaterialSlots, path + "." + stepId, Issue,
                        usedMaterialSlots);
                    if (inputs is null || outputs is null) continue;
                    // Null readiness is deliberate: quoting does not assert that
                    // a historical creator owns recipes, materials, or facilities.
                    operations.Add(new(stepId!, competencyId!, minimum, null,
                        dependencies, inputs, outputs, null, null, null));
                }

                if (issues.Count != before) continue;
                var validation = NativeCraftingProcess.Evaluate(operations);
                if (validation.Issues.Count > 0)
                {
                    Issue("crafting-quote-stage-structure-invalid", path,
                        NativeOriginIssueSeverity.Invalid,
                        string.Join(", ", validation.Issues));
                    continue;
                }
                // The shared process validator has checked cycles and missing
                // prerequisites. Order stages by those dependencies, not JSON order.
                var byId = operations.ToDictionary(x => x.StepId, StringComparer.Ordinal);
                var visited = new HashSet<string>(StringComparer.Ordinal);
                var ordered = new List<NativeCraftingProcessStep>();
                void Visit(string id)
                {
                    if (!visited.Add(id)) return;
                    foreach (var dependency in byId[id].Dependencies)
                        Visit(dependency);
                    ordered.Add(byId[id]);
                }
                foreach (var step in operations) Visit(step.StepId);
                operations = ordered;
            }
            else
            {
                var inputs = ReadMaterials(catalog, recipe.Definition, "inputs", true,
                    selection.MaterialSlots, path, Issue, usedMaterialSlots);
                if (inputs is null) continue;
                operations.Add(new("manufacture", "competency.none", 0, null,
                    [], inputs, [], null, null, null));
            }
            if (issues.Count != before) continue;
            if (source.Category == "construct-component" &&
                source.Definition.TryGetProperty("materialSlots", out var selectedSlots) &&
                selectedSlots.ValueKind == JsonValueKind.Array)
            {
                foreach (var slot in selectedSlots.EnumerateArray())
                {
                    if (slot.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(slot.GetString()))
                    {
                        Issue("crafting-quote-product-material-slots-invalid", path,
                            NativeOriginIssueSeverity.Invalid,
                            "Component material slot definitions must be valid identifiers.");
                    }
                    else if (!usedMaterialSlots.Contains(slot.GetString()!))
                        Issue("crafting-quote-material-slot-unused", path,
                            NativeOriginIssueSeverity.Unresolved,
                            "Every selected component material slot must be represented in its manufacturing inputs.");
                }
            }
            if (issues.Count != before) continue;

            // Copy the already resolved material-slot quantities and the
            // dependency-ordered stage structure; do not re-evaluate or price.
            if (presentationAvailable)
            {
                presentationStages += operations.Count;
                presentationEntries += operations.Sum(x =>
                    x.Dependencies.Count + x.Inputs.Count + x.Outputs.Count);
                if (presentationStages > maxPresentationStages ||
                    presentationEntries > maxPresentationEntries)
                {
                    presentationAvailable = false;
                    recipeViews.Clear();
                }
                else
                    recipeViews.Add(new(selection.InstanceId, selection.SourceDefinitionId,
                        resolved.RecipeId, operations.Select(step => new NativeCraftingQuotedStage(
                            step.StepId, step.CompetencyId, step.MinimumRanks, step.Dependencies,
                            step.Inputs, step.Outputs)).ToArray()));
            }

            // Retention authoring is independent of purchase pricing. Only
            // ingredients explicitly retained in the finished product contribute
            // to this known partial mass; missing mass data does not become zero.
            var unknownMassBefore = unresolvedMass.Count;
            foreach (var input in operations.SelectMany(step => step.Inputs)
                .Where(material => material.RetainedInProduct))
            {
                catalog.TryGetById(input.MaterialId, out var material);
                if (material is null ||
                    !material.Definition.TryGetProperty("massPoundsPerUnit",
                        out var massValue) ||
                    massValue.ValueKind != JsonValueKind.Number ||
                    !massValue.TryGetInt32(out var mass) || mass is < 0 or > 1_000_000)
                {
                    unresolvedMass.Add(input.MaterialId);
                    continue;
                }
                try
                {
                    knownRetainedMassPounds = checked(
                        knownRetainedMassPounds + checked((long)input.Amount * mass));
                }
                catch (OverflowException)
                {
                    Issue("crafting-quote-mass-overflow", path,
                        NativeOriginIssueSeverity.Invalid,
                        "Known retained material mass exceeds supported quantities.");
                }
            }

            // A native recipe may certify complete finished-product mass only
            // after all inputs explicitly declare retention and no intermediate
            // stage outputs can be inadvertently counted twice. An additional
            // positive amount covers manufactured mass not in retained inputs.
            // Absence of a certificate is normal and keeps the sum a lower bound.
            if (recipe.Definition.TryGetProperty("physicalMassAccounting", out var accounting))
            {
                if (accounting.ValueKind != JsonValueKind.Object ||
                    !accounting.TryGetProperty("fullyAccounted", out var fully) ||
                    fully.ValueKind != JsonValueKind.True ||
                    !accounting.TryGetProperty("additionalProductMassPounds", out var extraMass) ||
                    extraMass.ValueKind != JsonValueKind.Number ||
                    !extraMass.TryGetInt32(out var additional) ||
                    additional is < 0 or > 1_000_000)
                {
                    Issue("crafting-quote-mass-certificate-invalid", path,
                        NativeOriginIssueSeverity.Invalid,
                        "A complete physical mass certificate needs a true flag and bounded nonnegative additional weight.");
                }
                else
                {
                    var hasUnmarkedInput = recipe.Definition.TryGetProperty(
                            "stages", out var rawStages)
                        ? rawStages.EnumerateArray().Any(stage =>
                            stage.TryGetProperty("inputs", out var rawInputs) &&
                            rawInputs.EnumerateArray().Any(input =>
                                !input.TryGetProperty("retainedInProduct", out _)))
                        : recipe.Definition.GetProperty("inputs").EnumerateArray()
                            .Any(input => !input.TryGetProperty("retainedInProduct", out _));

                    if (hasUnmarkedInput || operations.Any(step => step.Outputs.Count > 0))
                        Issue("crafting-quote-mass-certificate-incomplete", path,
                            NativeOriginIssueSeverity.Invalid,
                            "Complete mass cannot be certified with unmarked input retention or unaccounted intermediate outputs.");
                    else if (unresolvedMass.Count == unknownMassBefore &&
                             issues.Count == before)
                    {
                        try
                        {
                            knownRetainedMassPounds = checked(
                                knownRetainedMassPounds + additional);
                            uncertifiedProducts.RemoveAt(uncertifiedProducts.Count - 1);
                        }
                        catch (OverflowException)
                        {
                            Issue("crafting-quote-mass-overflow", path,
                                NativeOriginIssueSeverity.Invalid,
                                "Complete certified material mass exceeds supported quantities.");
                        }
                    }
                }
            }

            // A private ledger for each recipe invocation avoids double-pricing
            // its intermediate outputs while not offering hypothetical surplus
            // products as free resources for a different recipe.
            var available = new Dictionary<string, long>(StringComparer.Ordinal);
            try
            {
                foreach (var step in operations)
                {
                    foreach (var input in step.Inputs)
                    {
                        var stocked = available.GetValueOrDefault(input.MaterialId);
                        var used = Math.Min(stocked, input.Amount);
                        available[input.MaterialId] = stocked - used;
                        var external = input.Amount - used;
                        if (external > 0)
                            required[input.MaterialId] = checked(
                                required.GetValueOrDefault(input.MaterialId) + external);
                    }
                    foreach (var output in step.Outputs)
                        available[output.MaterialId] = checked(
                            available.GetValueOrDefault(output.MaterialId) + output.Amount);
                }
            }
            catch (OverflowException)
            {
                Issue("crafting-quote-quantity-overflow", path,
                    NativeOriginIssueSeverity.Invalid,
                    "Recipe resource totals exceed supported quantities.");
            }
        }

        long knownCost = 0;
        foreach (var pair in required.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (!prices.TryGetValue(pair.Key, out var unitPrice))
            {
                Issue("crafting-quote-resource-price-unresolved",
                    "method.startingResourceUnitCosts." + pair.Key,
                    NativeOriginIssueSeverity.Unresolved,
                    "A required external recipe ingredient has no authorized starting value.");
                if (presentationAvailable && required.Count <= maxPresentationEntries)
                    ingredientViews.Add(new(pair.Key, pair.Value, null, null));
                continue;
            }
            long? lineCost = null;
            try
            {
                var evaluated = checked(pair.Value * unitPrice);
                knownCost = checked(knownCost + evaluated);
                lineCost = evaluated;
            }
            catch (OverflowException)
            {
                Issue("crafting-quote-price-overflow", pair.Key,
                    NativeOriginIssueSeverity.Invalid,
                    "The total starting-resource value exceeds supported quantities.");
            }
            if (presentationAvailable && required.Count <= maxPresentationEntries)
                ingredientViews.Add(new(pair.Key, pair.Value, unitPrice, lineCost));
        }
        presentationAvailable = presentationAvailable && required.Count <= maxPresentationEntries;
        var presentation = presentationAvailable
            ? new NativeCraftingQuotePresentation(issues.Count == 0 && recipeViews.Count == selections.Count
                ? "resolved" : "partial", recipeViews.ToArray(), ingredientViews.ToArray())
            : new NativeCraftingQuotePresentation("unavailable", [], []);
        return new(knownCost, required.ToFrozenDictionary(StringComparer.Ordinal), issues)
        {
            KnownRetainedMaterialMassPounds = knownRetainedMassPounds,
            UnresolvedRetainedMassMaterials = unresolvedMass.ToArray(),
            IsPhysicalMassComplete = selections.Count > 0 &&
                uncertifiedProducts.Count == 0 && unresolvedMass.Count == 0 &&
                !issues.Any(x => x.Severity is NativeOriginIssueSeverity.Invalid
                    or NativeOriginIssueSeverity.Unresolved),
            ProductsWithUncertifiedMass = uncertifiedProducts.ToArray(),
            Presentation = presentation
        };
    }

    private static bool String(JsonElement element, string property, out string? value)
    {
        value = element.TryGetProperty(property, out var item) &&
            item.ValueKind == JsonValueKind.String ? item.GetString() : null;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static List<NativeCraftingMaterialQuantity>? ReadMaterials(
        NativeCatalogSnapshot catalog, JsonElement definition, string key,
        bool required, IReadOnlyDictionary<string, string>? assigned, string path,
        Action<string, string, NativeOriginIssueSeverity, string> issue,
        HashSet<string> usedMaterialSlots)
    {
        if (!definition.TryGetProperty(key, out var values))
        {
            if (!required) return [];
            issue("crafting-quote-inputs-unresolved", path + "." + key,
                NativeOriginIssueSeverity.Unresolved,
                "A recipe must author its required input list, even when empty.");
            return null;
        }
        if (values.ValueKind != JsonValueKind.Array || values.GetArrayLength() > 100)
        {
            issue("crafting-quote-materials-invalid", path + "." + key,
                NativeOriginIssueSeverity.Invalid,
                "Recipe inputs and outputs must be bounded material lists.");
            return null;
        }

        var result = new List<NativeCraftingMaterialQuantity>();
        foreach (var input in values.EnumerateArray())
        {
            if (input.ValueKind != JsonValueKind.Object ||
                !input.TryGetProperty("amount", out var amountElement) ||
                amountElement.ValueKind != JsonValueKind.Number ||
                !amountElement.TryGetInt32(out var amount) || amount is < 1 or > 1_000_000)
            {
                issue("crafting-quote-amount-invalid", path + "." + key,
                    NativeOriginIssueSeverity.Invalid,
                    "An ingredient needs a positive bounded integer quantity.");
                return null;
            }
            var staticId = String(input, "materialId", out var id);
            var slotId = String(input, "materialSlot", out var slot);
            if (staticId == slotId)
            {
                issue("crafting-quote-material-reference-invalid", path + "." + key,
                    NativeOriginIssueSeverity.Invalid,
                    "Specify exactly one material ID or selected component material slot.");
                return null;
            }
            if (slotId)
            {
                if (key == "inputs") usedMaterialSlots.Add(slot!);
                if (assigned is null || !assigned.TryGetValue(slot!, out id))
                {
                    issue("crafting-quote-material-slot-unresolved", path + "." + key,
                        NativeOriginIssueSeverity.Unresolved,
                        "The authored recipe requires a verified material-slot assignment.");
                    return null;
                }
            }
            if (string.IsNullOrWhiteSpace(id) || !catalog.TryGetById(id, out _))
            {
                issue("crafting-quote-material-unknown", path + "." + key,
                    NativeOriginIssueSeverity.Invalid,
                    "Recipe resource identities must exist in the native catalog.");
                return null;
            }
            var retained = false;
            if (input.TryGetProperty("retainedInProduct", out var retention))
            {
                if (key != "inputs" ||
                    retention.ValueKind is not
                        (JsonValueKind.True or JsonValueKind.False))
                {
                    issue("crafting-quote-retention-invalid", path + "." + key,
                        NativeOriginIssueSeverity.Invalid,
                        "Physical retention must be a boolean on recipe inputs.");
                    return null;
                }
                retained = retention.ValueKind == JsonValueKind.True;
            }
            result.Add(new(id, amount) { RetainedInProduct = retained });
        }
        return result;
    }
}
