using System.Text.Json;
using System.Text.Json.Nodes;
using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Applies reviewed competency translations before SourceEntity persistence and canonical
/// reconciliation. These mappings are identity translations, not Rules Lawyer relationships:
/// source-native identity remains in RawJson/NativeIdentityJson, translated mechanics remain in
/// ContentJson, and the cross-edition canonical identity key stays outside mechanical content.
/// </summary>
internal static class ExactCompetencyTranslationPolicy
{
    public const string IdentityVersion = "rules-core-exact-competency-v1";

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

        var targetType = record.EntityType;
        var targetName = record.Name;
        JsonObject? content = null;
        JsonObject? extension = null;

        if (string.Equals(
                representation.FormatKey,
                PcGenSourceFormatAdapter.Format,
                StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(record.ContentJson))
        {
            try
            {
                content = JsonNode.Parse(record.ContentJson) as JsonObject;
                extension = content?["_rulesCore"] as JsonObject;
            }
            catch (JsonException)
            {
                content = null;
                extension = null;
            }

            if (TryReadUnscopedPcGenConversion(extension, out var convertedType, out var convertedName))
            {
                targetType = convertedType;
                targetName = convertedName;
            }
        }

        if (!IsCanonicalTarget(targetType, targetName))
        {
            return record;
        }

        var canonicalIdentityKey = CanonicalSourceIdentity.OccurrenceKey(targetType, targetName);

        // Native 5e.tools records are already the Rules Core mechanical schema. Identity metadata
        // must never be injected into ContentJson because doing so would make direct ingestion
        // lossy. The canonical identity key is carried separately on the normalized record.
        if (string.Equals(
                representation.FormatKey,
                FiveEToolsSourceFormatAdapter.Format,
                StringComparison.OrdinalIgnoreCase))
        {
            return record with { CanonicalIdentityKey = canonicalIdentityKey };
        }

        if (content is null)
        {
            return record with
            {
                EntityType = targetType,
                Name = targetName,
                CanonicalIdentityKey = canonicalIdentityKey
            };
        }

        if (!string.Equals(targetType, record.EntityType, StringComparison.OrdinalIgnoreCase)
            && extension?["context"] is JsonObject context)
        {
            context["translatedEntityType"] = targetType;
        }

        content["name"] = targetName;
        return record with
        {
            EntityType = targetType,
            Name = targetName,
            ContentJson = content.ToJsonString(new JsonSerializerOptions { WriteIndented = false }),
            CanonicalIdentityKey = canonicalIdentityKey
        };
    }

    public static bool IsCanonicalTarget(string entityType, string name) =>
        CanonicalTargets.TryGetValue(entityType.Trim(), out var names)
        && names.Contains(name.Trim());

    public static string CanonicalFingerprint(string canonicalIdentityKey)
    {
        if (string.IsNullOrWhiteSpace(canonicalIdentityKey))
        {
            throw new ArgumentException("Canonical identity key can not be blank.", nameof(canonicalIdentityKey));
        }
        return CanonicalSourceIdentity.Fingerprint(
            $"{IdentityVersion}\n{canonicalIdentityKey.Trim().ToLowerInvariant()}");
    }

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

    private static string? ReadString(JsonObject value, string propertyName) =>
        value[propertyName] is JsonValue property
        && property.TryGetValue<string>(out var result)
            ? result
            : null;
}
