using System.Security.Cryptography;
using System.Text.Json;

namespace RulesCore.Infrastructure.Rules;

internal static class RuleSemanticCompatibility
{
    private static readonly HashSet<string> IgnoredRootProperties = new(
        [
            "name",
            "source",
            "page",
            "id",
            "uniqueId",
            "reprintedAs",
            "otherSources",
            "additionalSources",
            "previousVersion",
            "previousVersions",
            "versions",
            "seeAlso",
            "edition",
            "srd",
            "srd52",
            "basicRules",
            "basicRules2024",
            "freeRules2024"
        ],
        StringComparer.OrdinalIgnoreCase);

    public static string ComputeFingerprint(string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonicalRuleContent(writer, document.RootElement, isRoot: true);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    public static bool IsSubset(string subsetJson, string supersetJson)
    {
        using var subset = JsonDocument.Parse(subsetJson);
        using var superset = JsonDocument.Parse(supersetJson);
        return IsSubset(subset.RootElement, superset.RootElement, isRoot: true);
    }

    private static bool IsSubset(JsonElement subset, JsonElement superset, bool isRoot)
    {
        if (subset.ValueKind != superset.ValueKind)
        {
            return false;
        }

        switch (subset.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in subset.EnumerateObject())
                {
                    if (isRoot && IgnoredRootProperties.Contains(property.Name))
                    {
                        continue;
                    }
                    if (!superset.TryGetProperty(property.Name, out var supersetValue)
                        || !IsSubset(property.Value, supersetValue, isRoot: false))
                    {
                        return false;
                    }
                }
                return true;

            case JsonValueKind.Array:
                return IsOrderedSubset(subset, superset);

            default:
                return JsonElement.DeepEquals(subset, superset);
        }
    }

    private static bool IsOrderedSubset(JsonElement subset, JsonElement superset)
    {
        var supersetItems = superset.EnumerateArray().ToArray();
        var nextSupersetIndex = 0;

        foreach (var subsetItem in subset.EnumerateArray())
        {
            var matched = false;
            while (nextSupersetIndex < supersetItems.Length)
            {
                if (IsSubset(subsetItem, supersetItems[nextSupersetIndex], isRoot: false))
                {
                    matched = true;
                    nextSupersetIndex++;
                    break;
                }
                nextSupersetIndex++;
            }

            if (!matched)
            {
                return false;
            }
        }

        return true;
    }

    private static void WriteCanonicalRuleContent(
        Utf8JsonWriter writer,
        JsonElement element,
        bool isRoot)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element
                    .EnumerateObject()
                    .Where(value => !isRoot || !IgnoredRootProperties.Contains(value.Name))
                    .OrderBy(value => value.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalRuleContent(writer, property.Value, isRoot: false);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var child in element.EnumerateArray())
                {
                    WriteCanonicalRuleContent(writer, child, isRoot: false);
                }
                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}
