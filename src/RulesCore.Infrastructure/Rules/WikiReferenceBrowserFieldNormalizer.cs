using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Converts source-native shorthand used by normalized 5e/5.5e records into human-readable
/// browser values for the private Rules Wiki catalog. The public Rules Core browser projection
/// remains unchanged.
/// </summary>
public static class WikiReferenceBrowserFieldNormalizer
{
    private static readonly IReadOnlyDictionary<string, string> FeatCategoryLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["D"] = "Dragonmark",
            ["DG"] = "Dark Gift",
            ["G"] = "General",
            ["O"] = "Origin",
            ["FS"] = "Fighting Style",
            ["EB"] = "Epic Boon"
        };

    private static readonly IReadOnlyDictionary<string, string> ItemTypeLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "Ammunition",
            ["AT"] = "Artisan Tool",
            ["EXP"] = "Explosive",
            ["FD"] = "Food or Drink",
            ["G"] = "Adventuring Gear",
            ["GS"] = "Gaming Set",
            ["HA"] = "Heavy Armor",
            ["INS"] = "Instrument",
            ["LA"] = "Light Armor",
            ["M"] = "Melee Weapon",
            ["MA"] = "Medium Armor",
            ["P"] = "Potion",
            ["R"] = "Ranged Weapon",
            ["RD"] = "Rod",
            ["RG"] = "Ring",
            ["S"] = "Shield",
            ["SC"] = "Scroll",
            ["SCF"] = "Spellcasting Focus",
            ["ST"] = "Staff",
            ["T"] = "Tool",
            ["WD"] = "Wand",
            ["W"] = "Wondrous Item"
        };

    public static WikiReferenceCatalogView Normalize(WikiReferenceCatalogView catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.References.Count == 0) return catalog;

        var references = catalog.References
            .Select(NormalizeReference)
            .ToArray();
        return catalog with { References = references };
    }

    private static WikiReferenceItemView NormalizeReference(WikiReferenceItemView reference)
    {
        var category = RuleConceptEntityTypes.Normalize(reference.BrowseVariation.Category)
            .ToLowerInvariant();
        var fields = reference.BrowserFields.ToArray();

        switch (category)
        {
            case "feat":
                Replace(fields, "category", FormatFeatCategory);
                break;

            case "item":
            case "magicitem":
            case "equipment":
                Replace(fields, "type", FormatItemType);
                break;
        }

        return reference with { BrowserFields = fields };
    }

    private static void Replace(
        ResolvedRuleBrowserFieldView[] fields,
        string key,
        Func<string, string> formatter)
    {
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            if (!string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase)) continue;
            var formatted = formatter(field.Value);
            if (!string.IsNullOrWhiteSpace(formatted) && !string.Equals(formatted, field.Value, StringComparison.Ordinal))
            {
                fields[index] = field with { Value = formatted };
            }
            return;
        }
    }

    private static string FormatFeatCategory(string value) =>
        FeatCategoryLabels.TryGetValue(value.Trim(), out var label) ? label : value;

    private static string FormatItemType(string value)
    {
        var trimmed = value.Trim();
        var separator = trimmed.IndexOf('|');
        var code = separator >= 0 ? trimmed[..separator] : trimmed;
        return ItemTypeLabels.TryGetValue(code, out var label) ? label : value;
    }
}
