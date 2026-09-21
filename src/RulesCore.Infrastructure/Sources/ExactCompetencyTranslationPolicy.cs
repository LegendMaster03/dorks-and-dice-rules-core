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
    public const string LegacyCompetencyNormalizationVersion = "legacy-srd-competency-v2";

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
        var result = record;
        var reviewedConvertedIdentity = false;
        JsonObject? content = null;
        JsonObject? extension = null;
        var isPcGen = string.Equals(
            representation.FormatKey,
            PcGenSourceFormatAdapter.Format,
            StringComparison.OrdinalIgnoreCase);
        var isLegacySrd = string.Equals(
            representation.FormatKey,
            LegacySrdSourceFormatAdapter.Format,
            StringComparison.OrdinalIgnoreCase);

        if ((isPcGen || isLegacySrd) && !string.IsNullOrWhiteSpace(record.ContentJson))
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
        }

        if (isPcGen
            && TryReadUnscopedPcGenConversion(extension, out var convertedType, out var convertedName))
        {
            targetType = convertedType;
            targetName = convertedName;
            reviewedConvertedIdentity = true;
        }
        else if (isLegacySrd && content is not null && extension is not null)
        {
            var edition = ResolveEdition(representation, record);
            if (extension["context"] is JsonObject normalizationContext)
            {
                normalizationContext["competencyNormalizationVersion"] = LegacyCompetencyNormalizationVersion;
            }

            if (string.Equals(record.EntityType, "skill", StringComparison.OrdinalIgnoreCase)
                && IsThreeXEdition(edition))
            {
                var conversion = PcGenCompetencyConversions.Resolve(record.Name, edition);
                var effectiveType = conversion is not null
                    && string.IsNullOrWhiteSpace(conversion.Scope)
                        ? conversion.TargetType
                        : record.EntityType;

                extension["competency"] = RulesCoreContentTranslation.BuildThreeXCompetencyMetadata(
                    record.Name,
                    effectiveType,
                    edition!);

                if (conversion is not null)
                {
                    extension["competencyConversion"] =
                        RulesCoreContentTranslation.BuildCompetencyConversionMetadata(conversion);
                    if (extension["context"] is JsonObject conversionContext)
                    {
                        conversionContext["nativeName"] = record.Name;
                    }

                    if (string.IsNullOrWhiteSpace(conversion.Scope))
                    {
                        targetType = conversion.TargetType;
                        targetName = conversion.TargetName;
                        reviewedConvertedIdentity = true;
                    }
                }
            }

            result = result with
            {
                ContentJson = content.ToJsonString(
                    new JsonSerializerOptions { WriteIndented = false })
            };
        }

        if (!reviewedConvertedIdentity && !IsCanonicalTarget(targetType, targetName))
        {
            return result;
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
            return result with { CanonicalIdentityKey = canonicalIdentityKey };
        }

        if (content is null)
        {
            return result with
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
        return result with
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

    internal static bool IsReviewedLegacyCompetencyMigration(
        string currentEntityType,
        string currentName,
        NormalizedSourceRecord translatedRecord)
    {
        if (!string.Equals(currentEntityType, "skill", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(currentName)
            || string.IsNullOrWhiteSpace(translatedRecord.CanonicalIdentityKey)
            || string.IsNullOrWhiteSpace(translatedRecord.ContentJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(translatedRecord.ContentJson);
            if (!document.RootElement.TryGetProperty("_rulesCore", out var rulesCore)
                || rulesCore.ValueKind != JsonValueKind.Object
                || !rulesCore.TryGetProperty("context", out var context)
                || context.ValueKind != JsonValueKind.Object
                || !string.Equals(
                    ReadString(context, "sourceFormat"),
                    LegacySrdSourceFormatAdapter.Format,
                    StringComparison.Ordinal)
                || !string.Equals(
                    ReadString(context, "competencyNormalizationVersion"),
                    LegacyCompetencyNormalizationVersion,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var edition = ReadString(context, "edition");
            var conversion = PcGenCompetencyConversions.Resolve(currentName, edition);
            return conversion is not null
                && string.IsNullOrWhiteSpace(conversion.Scope)
                && string.Equals(
                    conversion.TargetType,
                    translatedRecord.EntityType,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    conversion.TargetName,
                    translatedRecord.Name,
                    StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ResolveEdition(
        NormalizedSourceRepresentation representation,
        NormalizedSourceRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.PublicationLocalKey))
        {
            return null;
        }

        return (representation.Publications ?? [])
            .FirstOrDefault(value => string.Equals(
                value.LocalKey,
                record.PublicationLocalKey,
                StringComparison.OrdinalIgnoreCase))
            ?.GameEdition;
    }

    private static bool IsThreeXEdition(string? edition) =>
        string.Equals(edition, "3e", StringComparison.OrdinalIgnoreCase)
        || string.Equals(edition, "3.5e", StringComparison.OrdinalIgnoreCase);

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
            || !string.IsNullOrWhiteSpace(scope))
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

    private static string? ReadString(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
