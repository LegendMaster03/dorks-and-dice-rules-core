using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

internal static class CharacterProjectionJson
{
    private static readonly Dictionary<string, string> AbilityKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["str"] = "strength",
            ["strength"] = "strength",
            ["dex"] = "dexterity",
            ["dexterity"] = "dexterity",
            ["con"] = "constitution",
            ["constitution"] = "constitution",
            ["int"] = "intelligence",
            ["intelligence"] = "intelligence",
            ["wis"] = "wisdom",
            ["wisdom"] = "wisdom",
            ["cha"] = "charisma",
            ["charisma"] = "charisma"
        };

    public static string NormalizeAbilityKey(string value) =>
        AbilityKeys.TryGetValue(value?.Trim() ?? string.Empty, out var key)
            ? key
            : (value?.Trim().ToLowerInvariant()
                ?? throw new ArgumentNullException(nameof(value)));

    public static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out value);
    }

    public static string? String(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return null;
        }
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    public static bool? Boolean(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return null;
        }
        if (value.ValueKind == JsonValueKind.True)
        {
            return true;
        }
        if (value.ValueKind == JsonValueKind.False)
        {
            return false;
        }
        if (value.ValueKind == JsonValueKind.String
            && bool.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }
        return null;
    }

    public static string NormalizeResourceSystemKey(string value) =>
        string.Join(
            '-',
            value.Trim().ToLowerInvariant()
                .Split(
                    [' ', '/', '_', '-'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public static int? Integer(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return null;
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }
        return value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), out number)
                ? number
                : null;
    }

    public static decimal? Decimal(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return null;
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }
        return value.ValueKind == JsonValueKind.String
            && decimal.TryParse(
                value.GetString(),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out number)
                ? number
                : null;
    }

    public static IReadOnlyList<string> Strings(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return [];
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            var item = value.GetString();
            return string.IsNullOrWhiteSpace(item) ? [] : [item.Trim()];
        }
        if (value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .ToArray();
    }

    public static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var tail = value.Trim().Split(['.', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault() ?? value.Trim();
        return string.Join(
            ' ',
            tail.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    public static string? RangeText(JsonElement document)
    {
        if (!TryGetProperty(document, "range", out var range))
        {
            return null;
        }
        if (range.ValueKind == JsonValueKind.String)
        {
            return range.GetString();
        }
        if (range.ValueKind != JsonValueKind.Object)
        {
            return range.ToString();
        }

        if (TryGetProperty(range, "distance", out var distance)
            && distance.ValueKind == JsonValueKind.Object)
        {
            var type = String(distance, "type");
            var amount = Integer(distance, "amount");
            if (amount is not null)
            {
                return $"{amount} {type ?? "units"}";
            }
            if (!string.IsNullOrWhiteSpace(type))
            {
                return type;
            }
        }

        return String(range, "type") ?? range.ToString();
    }
}
