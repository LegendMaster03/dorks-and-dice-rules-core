using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// A source-authored manufacturing variant. Size is optional: most component
/// recipes do not define the whole body's creature-size classification.
/// </summary>
internal sealed record NativeConstructResolvedRecipeVariant(
    string RecipeId, string? BodySize, string? VariantId);

internal static class NativeConstructRecipeVariantResolver
{
    internal static readonly IReadOnlySet<string> CreatureSizes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "tiny", "small", "medium", "large", "huge", "gargantuan"
        };

    internal static NativeConstructResolvedRecipeVariant? Resolve(
        NativeCatalogSnapshot catalog, string sourceDefinitionId, string? selectedVariantId,
        string path, Action<string, string, NativeOriginIssueSeverity, string> issue)
    {
        if (!catalog.TryGetById(sourceDefinitionId, out var source) ||
            source is null)
        {
            issue("construct-recipe-variant-source-unknown", path,
                NativeOriginIssueSeverity.Invalid,
                "Only a native, catalog-verified component or option can select a recipe.");
            return null;
        }

        var raw = source.Definition;
        if (selectedVariantId is null)
        {
            if (!raw.TryGetProperty("craftingRecipeId", out var defaultRecipe) ||
                defaultRecipe.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(defaultRecipe.GetString()))
            {
                issue("crafting-quote-recipe-unresolved", path,
                    NativeOriginIssueSeverity.Unresolved,
                    "A default recipe or a catalog-authored selected variant is required.");
                return null;
            }
            return ValidateRecipe(defaultRecipe.GetString()!, null);
        }

        if (selectedVariantId.Length is < 1 or > 96 ||
            !raw.TryGetProperty("constructionVariants", out var variants) ||
            variants.ValueKind != JsonValueKind.Array ||
            variants.GetArrayLength() is < 1 or > 100)
        {
            issue("construct-construction-variant-invalid", path,
                NativeOriginIssueSeverity.Invalid,
                "A variant selection must exist in the native definition.");
            return null;
        }

        var selected = variants.EnumerateArray()
            .Where(v => v.ValueKind == JsonValueKind.Object &&
                        v.TryGetProperty("id", out var id) &&
                        id.ValueKind == JsonValueKind.String &&
                        id.GetString() == selectedVariantId)
            .ToArray();
        if (selected.Length != 1)
        {
            issue("construct-construction-variant-invalid", path,
                NativeOriginIssueSeverity.Invalid,
                "The selected construction variant must be uniquely authored.");
            return null;
        }
        var choice = selected[0];
        if (!choice.TryGetProperty("craftingRecipeId", out var choiceRecipe) ||
            choiceRecipe.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(choiceRecipe.GetString()))
        {
            issue("construct-construction-variant-recipe-invalid", path,
                NativeOriginIssueSeverity.Invalid,
                "The selected variant requires a native crafting recipe.");
            return null;
        }

        string? bodySize = null;
        if (choice.TryGetProperty("bodySize", out var size))
        {
            if (size.ValueKind != JsonValueKind.String ||
                !CreatureSizes.Contains(size.GetString()!))
            {
                issue("construct-construction-variant-size-invalid", path,
                    NativeOriginIssueSeverity.Invalid,
                    "The selected body-size classification must be a native creature size.");
                return null;
            }
            if (!raw.TryGetProperty("definesBodySize", out var defines) ||
                defines.ValueKind != JsonValueKind.True ||
                source.Category != "construct-component")
            {
                issue("construct-construction-variant-size-unqualified", path,
                    NativeOriginIssueSeverity.Invalid,
                    "Only a verified body-defining component may author whole-creature size.");
                return null;
            }
            bodySize = size.GetString();
        }
        return ValidateRecipe(choiceRecipe.GetString()!, bodySize);

        NativeConstructResolvedRecipeVariant? ValidateRecipe(string recipeId, string? size)
        {
            if (!catalog.TryGetById(recipeId, out var recipe) ||
                recipe?.Category != "crafting-recipe" ||
                !recipe.Definition.TryGetProperty("producesDefinitionId", out var product) ||
                product.ValueKind != JsonValueKind.String ||
                product.GetString() != sourceDefinitionId)
            {
                issue("construct-construction-variant-product-invalid", path,
                    NativeOriginIssueSeverity.Invalid,
                    "The selected recipe must manufacture its exact native source definition.");
                return null;
            }
            return new(recipeId, size, selectedVariantId);
        }
    }
}

/// <summary>
/// Resolves the body-size classification from actual selected manufacturing
/// variants. An assembly can have any topology; no root frame is mandatory.
/// Ambiguous or contradictory body-defining parts never silently choose a size.
/// </summary>
internal static class NativeConstructBodySizeResolver
{
    internal sealed record DerivedSize(
        string Size, string DefinitionId, string InstanceId, string VariantId);

    internal static DerivedSize? Resolve(
        NativeCatalogSnapshot catalog,
        IReadOnlyList<NativeConstructComponentInstance> components,
        Action<string, string, NativeOriginIssueSeverity, string> issue)
    {
        var sources = new List<DerivedSize>();
        foreach (var instance in components)
        {
            if (instance is null || instance.ConstructionVariantId is null)
                continue;
            var path = "components." + instance.InstanceId + ".constructionVariantId";
            var variant = NativeConstructRecipeVariantResolver.Resolve(
                catalog, instance.DefinitionId, instance.ConstructionVariantId, path, issue);
            if (variant?.BodySize is string size)
                sources.Add(new(size, instance.DefinitionId,
                    instance.InstanceId, instance.ConstructionVariantId));
        }
        if (sources.Count == 0) return null;
        if (sources.Select(x => x.Size).Distinct(StringComparer.Ordinal).Count() > 1)
        {
            issue("construct-body-size-conflict", "components",
                NativeOriginIssueSeverity.Conflict,
                "Body-defining components disagree about the completed creature's size.");
            return null;
        }
        // A single representative is sufficient when all the verified
        // whole-body source selections agree, without manufacturing a new
        // independent intrinsic or assigning size to every mounted part.
        return sources.OrderBy(x => x.InstanceId, StringComparer.Ordinal).First();
    }
}
