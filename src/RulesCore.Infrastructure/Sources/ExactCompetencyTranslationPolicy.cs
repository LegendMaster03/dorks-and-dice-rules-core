using System.Text.Json;
using System.Text.Json.Nodes;
using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Applies reviewed competency translations before SourceEntity persistence and canonical
/// reconciliation. These mappings are identity translations, not Rules Lawyer relationships:
/// source-native identity remains in RawJson/NativeIdentityJson while the normalized record uses
/// the later competency name/type.
/// </summary>
internal static class ExactCompetencyTranslationPolicy
{
    public const string IdentityVersion = "rules-core-exact-competency-v1";
    public const string MarkerProperty = "exactCompetencyIdentity";

    private static readonly IReadOnlyDictionary<string, HashSet<string>> CanonicalTargets =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["skill"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "Deception",
                "Persuasion",
                "Animal Handling",
                "Medicine",
                "Intimidation",
                "Arcana",
                "History",
                "Nature",
                "Religion",
                "Insight",
                "Sleight of Hand",
                "Survival"
            },
            ["tool"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "Alchemist's Supplies",
                "Forgery Kit"
            }
        };

    public static NormalizedSourceRecord Apply(
        NormalizedSourceRepresentation representation,
        NormalizedSourceRecord record)
    {
        ArgumentNullException.ThrowIfNull(representation);
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.ContentJson))
        {
            return record;
        }

        JsonObject? content;
        try
        {
            content = JsonNode.Parse(record.ContentJson) as JsonObject;
        }
        catch (JsonException)
        {
            return record;
        }
        if (content is null)
        {
            return record;
        }

        JsonObject? extension = content["_rulesCore"] as JsonObject;
        extension?.Remove(MarkerProperty);

        var targetType = record.EntityType;
        var targetName = record.Name;
        var exact = false;

        if (string.Equals(
                representation.FormatKey,
                PcGenSourceFormatAdapter.Format,
                StringComparison.OrdinalIgnoreCase)
            && TryReadUnscopedPcGenConversion(extension, out var convertedType, out var convertedName))
        {
            targetType = convertedType;
            targetName = convertedName;
            exact = true;
        }
        else if (IsCanonicalTarget(record.EntityType, record.Name))
        {
            exact = true;
        }

        if (!exact)
        {
            return extension is null
                ? record
                : record with { ContentJson = content.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) };
        }

        extension ??= new JsonObject();
        content["_rulesCore"] = extension;
        extension[MarkerProperty] = new JsonObject
        {
            ["version"] = IdentityVersion,
            ["key"] = CanonicalSourceIdentity.OccurrenceKey(targetType, targetName),
            ["entityType"] = targetType,
            ["name"] = targetName
        };

        if (!string.Equals(targetType, record.EntityType, StringComparison.OrdinalIgnoreCase)
            && extension["context"] is JsonObject context)
        {
            context["translatedEntityType"] = targetType;
        }

        content["name"] = targetName;
        return record with
        {
            EntityType = targetType,
            Name = targetName,
            ContentJson = content.ToJsonString(new JsonSerializerOptions { WriteIndented = false })
        };
    }

    public static string CanonicalSemanticFingerprint(NormalizedSourceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (TryReadExactIdentityKey(record.ContentJson, out var identityKey))
        {
            return CanonicalSourceIdentity.Fingerprint($"{IdentityVersion}\n{identityKey}");
        }

        var document = string.IsNullOrWhiteSpace(record.ContentJson)
            ? record.RawJson
            : record.ContentJson;
        return CanonicalSourceIdentity.SemanticFingerprint(document);
    }

    public static bool IsCanonicalTarget(string entityType, string name) =>
        CanonicalTargets.TryGetValue(entityType.Trim(), out var names)
        && names.Contains(name.Trim());

    private static bool TryReadUnscopedPcGenConversion(
        JsonObject? extension,
        out string targetType,
        out string targetName)
    {
        targetType = string.Empty;
        targetName = string.Empty;
        if (extension?["competencyConversion"] is not JsonObject conversion)
        {
            return false;
        }

        var relationship = ReadString(conversion, "relationship");
        var sourceType = ReadString(conversion, "sourceType");
        var convertedType = ReadString(conversion, "targetType");
        var convertedName = ReadString(conversion, "targetName");
        var scope = ReadString(conversion, "scope");
        if ((relationship is not "direct-equivalence" and not "direct-cross-type")
            || !string.Equals(sourceType, "skill", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(convertedType)
            || string.IsNullOrWhiteSpace(convertedName)
            || !string.IsNullOrWhiteSpace(scope)
            || !IsCanonicalTarget(convertedType, convertedName))
        {
            return false;
        }

        targetType = convertedType.Trim();
        targetName = convertedName.Trim();
        return true;
    }

    private static bool TryReadExactIdentityKey(string? contentJson, out string identityKey)
    {
        identityKey = string.Empty;
        if (string.IsNullOrWhiteSpace(contentJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(contentJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("_rulesCore", out var extension)
                || extension.ValueKind != JsonValueKind.Object
                || !extension.TryGetProperty(MarkerProperty, out var identity)
                || identity.ValueKind != JsonValueKind.Object
                || !identity.TryGetProperty("version", out var version)
                || version.ValueKind != JsonValueKind.String
                || !string.Equals(version.GetString(), IdentityVersion, StringComparison.Ordinal)
                || !identity.TryGetProperty("key", out var key)
                || key.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(key.GetString()))
            {
                return false;
            }

            identityKey = key.GetString()!.Trim();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadString(JsonObject value, string propertyName) =>
        value[propertyName] is JsonValue property
        && property.TryGetValue<string>(out var result)
            ? result
            : null;
}
