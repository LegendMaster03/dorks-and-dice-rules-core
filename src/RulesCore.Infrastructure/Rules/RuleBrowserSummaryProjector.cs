using System.Globalization;
using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules;

internal static class RuleBrowserSummaryProjector
{
    private static readonly IReadOnlyDictionary<string, string> SchoolNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "Abjuration",
            ["C"] = "Conjuration",
            ["D"] = "Divination",
            ["E"] = "Enchantment",
            ["V"] = "Evocation",
            ["I"] = "Illusion",
            ["N"] = "Necromancy",
            ["T"] = "Transmutation"
        };

    private static readonly string[] AbilityKeys = ["str", "dex", "con", "int", "wis", "cha"];

    public static IReadOnlyList<ResolvedRuleBrowserFieldView> Project(
        string entityType,
        JsonElement document)
    {
        var normalized = RuleConceptEntityTypes.Normalize(entityType);
        var fields = new List<ResolvedRuleBrowserFieldView>();

        switch (normalized)
        {
            case "monster":
                Add(fields, "type", "Type", ReadMonsterType(document));
                Add(fields, "cr", "CR", ReadChallengeRating(document));
                Add(fields, "size", "Size", ReadSize(document));
                break;

            case "spell":
                Add(fields, "level", "Level", ReadSpellLevel(document));
                Add(fields, "school", "School", ReadSpellSchool(document));
                break;

            case RuleConceptEntityTypes.Class:
                Add(fields, "hitDie", "Hit Die", ReadHitDie(document));
                break;

            case "race":
            case "species":
                Add(fields, "ability", "Ability", ReadAbility(document));
                Add(fields, "size", "Size", ReadSize(document));
                break;

            case "item":
            case "magicItem":
                Add(fields, "type", "Type", ReadDisplayScalar(document, "type"));
                Add(fields, "rarity", "Rarity", ReadDisplayScalar(document, "rarity"));
                break;

            case "skill":
                Add(fields, "ability", "Ability", ReadAbilityCode(document));
                break;

            case "feat":
                Add(fields, "category", "Category", ReadDisplayScalar(document, "category"));
                break;
        }

        return fields;
    }

    private static void Add(
        ICollection<ResolvedRuleBrowserFieldView> fields,
        string key,
        string label,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        fields.Add(new ResolvedRuleBrowserFieldView(key, label, value));
    }

    private static string? ReadMonsterType(JsonElement document)
    {
        if (!document.TryGetProperty("type", out var value)) return null;
        if (value.ValueKind == JsonValueKind.String) return TitleCase(value.GetString());
        if (value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("type", out var nested)
            && nested.ValueKind == JsonValueKind.String)
        {
            return TitleCase(nested.GetString());
        }
        return ReadScalar(value);
    }

    private static string? ReadChallengeRating(JsonElement document)
    {
        if (!document.TryGetProperty("cr", out var value)) return null;
        if (value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("cr", out var nested))
        {
            return ReadScalar(nested);
        }
        return ReadScalar(value);
    }

    private static string? ReadSize(JsonElement document)
    {
        if (!document.TryGetProperty("size", out var value)) return null;
        if (value.ValueKind == JsonValueKind.Array)
        {
            var sizes = value.EnumerateArray()
                .Select(ReadScalar)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => UniversalSizeCategories.Normalize(item!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return sizes.Length == 0 ? null : string.Join("/", sizes);
        }

        var scalar = ReadScalar(value);
        return scalar is null ? null : UniversalSizeCategories.Normalize(scalar);
    }

    private static string? ReadSpellLevel(JsonElement document)
    {
        if (!document.TryGetProperty("level", out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var level))
        {
            return level == 0 ? "Cantrip" : Ordinal(level);
        }

        var scalar = ReadScalar(value);
        return scalar == "0" ? "Cantrip" : scalar;
    }

    private static string? ReadSpellSchool(JsonElement document)
    {
        if (!document.TryGetProperty("school", out var value)) return null;
        var scalar = ReadScalar(value);
        if (scalar is null) return null;
        return SchoolNames.TryGetValue(scalar, out var display) ? display : TitleCase(scalar);
    }

    private static string? ReadHitDie(JsonElement document)
    {
        if (!document.TryGetProperty("hd", out var value)) return null;
        if (value.ValueKind != JsonValueKind.Object) return ReadScalar(value);

        if (!value.TryGetProperty("faces", out var facesElement)
            || !facesElement.TryGetInt32(out var faces))
        {
            return null;
        }

        var number = 1;
        if (value.TryGetProperty("number", out var numberElement))
        {
            numberElement.TryGetInt32(out number);
            if (number < 1) number = 1;
        }

        return number == 1 ? $"d{faces}" : $"{number}d{faces}";
    }

    private static string? ReadAbilityCode(JsonElement document)
    {
        if (!document.TryGetProperty("ability", out var value)) return null;
        var scalar = ReadScalar(value);
        return scalar?.ToUpperInvariant();
    }

    private static string? ReadAbility(JsonElement document)
    {
        if (!document.TryGetProperty("ability", out var value)) return null;
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString()?.ToUpperInvariant();
        }

        var objects = value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object).ToArray()
            : value.ValueKind == JsonValueKind.Object ? [value] : [];

        foreach (var obj in objects)
        {
            var bonuses = new List<string>();
            foreach (var key in AbilityKeys)
            {
                if (!obj.TryGetProperty(key, out var bonus)) continue;
                var scalar = ReadScalar(bonus);
                if (scalar is null) continue;
                if (bonus.ValueKind == JsonValueKind.Number
                    && bonus.TryGetInt32(out var number)
                    && number >= 0)
                {
                    scalar = $"+{number}";
                }
                bonuses.Add($"{key.ToUpperInvariant()} {scalar}");
            }

            if (bonuses.Count > 0) return string.Join(", ", bonuses);
            if (obj.TryGetProperty("choose", out _)) return "Choice";
        }

        return null;
    }

    private static string? ReadDisplayScalar(JsonElement document, string propertyName)
    {
        if (!document.TryGetProperty(propertyName, out var value)) return null;
        return TitleCase(ReadScalar(value));
    }

    private static string? ReadScalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "Yes",
        JsonValueKind.False => "No",
        _ => null
    };

    private static string? TitleCase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().Replace('_', ' ').Replace('-', ' ');
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
    }

    private static string Ordinal(int value)
    {
        var suffix = (value % 100) is 11 or 12 or 13
            ? "th"
            : (value % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };
        return $"{value}{suffix}";
    }
}
