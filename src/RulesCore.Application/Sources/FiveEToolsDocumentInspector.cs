using System.Text;
using System.Text.Json;

namespace RulesCore.Application.Sources;

public static class FiveEToolsDocumentInspector
{
    public const string AccountSourceFallbackCode = "user-source";

    // These are native 5e.tools entity families, based on the upstream site/homebrew schema
    // families. Generic arrays such as "data" are deliberately excluded; corpus bodies are
    // handled separately by the corpus adapter once their book/adventure identity is known.
    private static readonly HashSet<string> KnownEntityArrays = new(StringComparer.OrdinalIgnoreCase)
    {
        "action",
        "adventure",
        "background",
        "baseitem",
        "bastion",
        "book",
        "boon",
        "card",
        "charoption",
        "citation",
        "class",
        "classFeature",
        "condition",
        "cult",
        "deck",
        "deity",
        "disease",
        "encounter",
        "facility",
        "feat",
        "hazard",
        "item",
        "itemEntry",
        "itemGroup",
        "language",
        "legendaryGroup",
        "magicvariant",
        "monster",
        "monsterFluff",
        "name",
        "object",
        "optionalfeature",
        "psionic",
        "quickref",
        "race",
        "raceFluff",
        "recipe",
        "reward",
        "sense",
        "skill",
        "spell",
        "spellFluff",
        "status",
        "subclass",
        "subclassFeature",
        "subrace",
        "table",
        "tableGroup",
        "trap",
        "variantrule",
        "vehicle",
        "vehicleUpgrade"
    };

    public static IReadOnlyList<string> DiscoverSourceCodes(
        string json,
        string fallbackSourceCode)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Source JSON can not be blank.", nameof(json));
        }

        using var document = JsonDocument.Parse(json);
        EnsureObjectRoot(document.RootElement);

        var requireExplicitSource = RequiresExplicitSource(fallbackSourceCode);
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!IsImportableArray(property))
            {
                continue;
            }

            foreach (var item in property.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (TryGetExplicitSourceCode(item, out var sourceCode))
                {
                    codes.Add(sourceCode);
                }
                else if (!requireExplicitSource)
                {
                    codes.Add(GetSourceCode(item, fallbackSourceCode));
                }
            }
        }

        return codes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string FilterBySourceCodes(
        string json,
        string fallbackSourceCode,
        IReadOnlyCollection<string>? includedSourceCodes,
        out int selectedEntityCount)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Source JSON can not be blank.", nameof(json));
        }

        using var document = JsonDocument.Parse(json);
        EnsureObjectRoot(document.RootElement);

        var included = NormalizeSourceCodes(includedSourceCodes);
        if (included.Count == 0)
        {
            selectedEntityCount = CountImportableEntities(document.RootElement);
            return json;
        }

        var applyOfficialMembership = IsOfficialSrdRelease(fallbackSourceCode);
        var requireExplicitSource = RequiresExplicitSource(fallbackSourceCode);
        selectedEntityCount = 0;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                if (!IsImportableArray(property))
                {
                    property.Value.WriteTo(writer);
                    continue;
                }

                writer.WriteStartArray();
                foreach (var item in property.Value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (requireExplicitSource && !TryGetExplicitSourceCode(item, out _))
                    {
                        continue;
                    }

                    var sourceCode = GetSourceCode(item, fallbackSourceCode);
                    if (!included.Contains(sourceCode)
                        || (applyOfficialMembership
                            && !OfficialSrdMembershipCatalog.IsManuallyConfirmed(
                                sourceCode,
                                property.Name,
                                item)))
                    {
                        continue;
                    }

                    item.WriteTo(writer);
                    selectedEntityCount++;
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static IReadOnlySet<string> NormalizeSourceCodes(
        IEnumerable<string>? sourceCodes)
    {
        if (sourceCodes is null)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return sourceCodes
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static string GetSourceCode(JsonElement item, string fallbackSourceCode)
    {
        if (TryGetExplicitSourceCode(item, out var sourceCode))
        {
            return sourceCode;
        }

        if (string.IsNullOrWhiteSpace(fallbackSourceCode))
        {
            throw new InvalidDataException(
                "An entity without an explicit source code requires a non-blank fallback source code.");
        }

        return fallbackSourceCode.Trim();
    }

    public static bool IsImportableArray(JsonProperty property) =>
        KnownEntityArrays.Contains(property.Name)
        && property.Value.ValueKind == JsonValueKind.Array;

    private static bool TryGetExplicitSourceCode(JsonElement item, out string sourceCode)
    {
        sourceCode = string.Empty;
        if (!item.TryGetProperty("source", out var sourceElement)
            || sourceElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(sourceElement.GetString()))
        {
            return false;
        }

        sourceCode = sourceElement.GetString()!.Trim();
        return true;
    }

    private static bool RequiresExplicitSource(string value) =>
        string.Equals(value?.Trim(), AccountSourceFallbackCode, StringComparison.OrdinalIgnoreCase);

    private static bool IsOfficialSrdRelease(string value) =>
        string.Equals(value?.Trim(), "5.1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value?.Trim(), "5.2.1", StringComparison.OrdinalIgnoreCase);

    private static int CountImportableEntities(JsonElement root)
    {
        var count = 0;
        foreach (var property in root.EnumerateObject())
        {
            if (!IsImportableArray(property))
            {
                continue;
            }

            count += property.Value.EnumerateArray().Count(item => item.ValueKind == JsonValueKind.Object);
        }
        return count;
    }

    private static void EnsureObjectRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A 5e.tools source document must have a JSON object root.");
        }
    }
}
