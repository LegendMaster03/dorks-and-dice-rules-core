using System.Text.Json;

namespace RulesCore.Application.Rules;

/// <summary>
/// The single Core interpretation point for source-native class-family feature references.
/// Both character projection and the private Rules Wiki presentation contract consume this
/// projection so acquisition levels and unresolved references can not drift between consumers.
/// </summary>
public static class ClassFamilyFeatureReferenceParser
{
    public sealed record ParsedFeature(
        int SourceIndex,
        string Name,
        int? Level,
        string? FeatureReference);

    public static IReadOnlyList<ParsedFeature> Project(
        string? category,
        JsonElement document)
    {
        var normalizedCategory = NormalizeCategory(category);
        var isSubclass = normalizedCategory == "subclass";
        var propertyNames = normalizedCategory switch
        {
            "class" => new[] { "classFeatures" },
            "subclass" => new[] { "subclassFeatures" },
            "prestigeclass" => new[] { "prestigeClassFeatures", "classFeatures" },
            _ => []
        };
        if (propertyNames.Length == 0 || document.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        JsonElement features = default;
        var found = false;
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetProperty(document, propertyName, out features)) continue;
            found = true;
            break;
        }
        if (!found) return [];

        var entries = features.ValueKind == JsonValueKind.Array
            ? features.EnumerateArray().ToArray()
            : [features];
        var projected = new List<ParsedFeature>(entries.Length);
        for (var index = 0; index < entries.Length; index++)
        {
            if (TryProjectEntry(entries[index], isSubclass, index, out var feature))
            {
                projected.Add(feature);
            }
        }
        return projected;
    }

    private static bool TryProjectEntry(
        JsonElement entry,
        bool isSubclass,
        int sourceIndex,
        out ParsedFeature feature)
    {
        feature = default!;
        string? reference = null;
        if (entry.ValueKind == JsonValueKind.String)
        {
            reference = entry.GetString()?.Trim();
        }
        else if (entry.ValueKind == JsonValueKind.Object)
        {
            var referenceProperty = isSubclass ? "subclassFeature" : "classFeature";
            reference = ReadString(entry, referenceProperty)?.Trim();
            if (string.IsNullOrWhiteSpace(reference))
            {
                var directName = ReadString(entry, "name")?.Trim();
                var directLevel = ReadInteger(entry, "level");
                if (!string.IsNullOrWhiteSpace(directName))
                {
                    feature = new ParsedFeature(
                        sourceIndex,
                        directName,
                        directLevel is > 0 ? directLevel : null,
                        null);
                    return true;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(reference)) return false;
        var parts = reference.Split('|');
        var displayName = parts[0].Trim();
        if (displayName.Length == 0) return false;

        var levelIndex = isSubclass ? 5 : 3;
        int? level = null;
        if (parts.Length > levelIndex
            && int.TryParse(parts[levelIndex], out var parsedLevel)
            && parsedLevel > 0)
        {
            level = parsedLevel;
        }

        feature = new ParsedFeature(sourceIndex, displayName, level, reference);
        return true;
    }

    private static string NormalizeCategory(string? value) =>
        string.Concat((value ?? string.Empty)
            .Where(character => character is not '-' and not '_' && !char.IsWhiteSpace(character)))
            .ToLowerInvariant();

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value)) return true;
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            value = property.Value;
            return true;
        }
        value = default;
        return false;
    }

    private static string? ReadString(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInteger(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric)) return numeric;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out numeric)
            ? numeric
            : null;
    }
}
