using System.Text.Json;
using System.Text.Json.Nodes;
using RulesCore.Application.Sources;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Applies reviewed class-category corrections when an upstream format stores a more-specific
/// class kind in a generic Class record. Native keys and source-native JSON remain unchanged.
/// </summary>
internal static class ClassTaxonomyNormalizationPolicy
{
    private const string AliasQualifier = "rules-core-class-taxonomy-v1";
    private const string ReviewedDragonsClassesPath = "/alderac_entertainment_group/dragons/dragonsclasses.lst";

    public static NormalizedSourceRecord Apply(
        NormalizedSourceRepresentation representation,
        NormalizedSourceRecord record)
    {
        ArgumentNullException.ThrowIfNull(representation);
        ArgumentNullException.ThrowIfNull(record);

        if (!string.Equals(record.EntityType, RuleConceptEntityTypes.Class, StringComparison.OrdinalIgnoreCase))
        {
            return record;
        }

        var targetType = ResolveTargetType(representation.FormatKey, record);
        if (targetType is null)
        {
            return record;
        }

        return record with
        {
            EntityType = targetType,
            ContentJson = RewriteTranslatedEntityType(record.ContentJson, targetType)
        };
    }

    internal static bool IsReviewedIdentityMigration(
        string formatKey,
        string currentEntityType,
        string currentName,
        NormalizedSourceRecord normalizedRecord)
    {
        if (!string.Equals(currentEntityType, RuleConceptEntityTypes.Class, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(currentName)
            || !string.Equals(currentName, normalizedRecord.Name, StringComparison.OrdinalIgnoreCase)
            || !IsSpecificClassCategory(normalizedRecord.EntityType))
        {
            return false;
        }

        var targetType = ResolveTargetType(formatKey, normalizedRecord with
        {
            EntityType = RuleConceptEntityTypes.Class
        });
        return string.Equals(targetType, normalizedRecord.EntityType, StringComparison.OrdinalIgnoreCase);
    }

    internal static string TrustedAliasValue(string formatKey, NormalizedSourceRecord record)
    {
        if (!IsSpecificClassCategory(record.EntityType))
        {
            return record.NativeKey;
        }

        var targetType = ResolveTargetType(formatKey, record with
        {
            EntityType = RuleConceptEntityTypes.Class
        });
        if (!string.Equals(targetType, record.EntityType, StringComparison.OrdinalIgnoreCase))
        {
            return record.NativeKey;
        }

        // Earlier Rules Core versions aliased these generic upstream Class records by the bare
        // native key, which bound the incorrect generic category. Qualify only the reviewed
        // derived category so corrected records no longer resolve through that stale alias while
        // the source-native identity itself remains unchanged.
        return $"{record.NativeKey}|{AliasQualifier}:{CanonicalSourceIdentity.NormalizeIdentityPart(record.EntityType)}";
    }

    private static string? ResolveTargetType(string formatKey, NormalizedSourceRecord record)
    {
        if (string.Equals(formatKey, FiveEToolsSourceFormatAdapter.Format, StringComparison.OrdinalIgnoreCase))
        {
            return ResolveFiveEToolsTarget(record);
        }

        if (string.Equals(formatKey, PcGenSourceFormatAdapter.Format, StringComparison.OrdinalIgnoreCase))
        {
            return IsReviewedPcGenDragonsClass(record.RawJson)
                ? RuleConceptEntityTypes.PrestigeClass
                : null;
        }

        return null;
    }

    private static string? ResolveFiveEToolsTarget(NormalizedSourceRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.RawJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(record.RawJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (root.TryGetProperty("isSidekick", out var isSidekick)
                && isSidekick.ValueKind is JsonValueKind.True)
            {
                return RuleConceptEntityTypes.SidekickClass;
            }

            return record.Name.StartsWith("Prestige Class:", StringComparison.OrdinalIgnoreCase)
                ? RuleConceptEntityTypes.PrestigeClass
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsReviewedPcGenDragonsClass(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("path", out var path)
                || path.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(path.GetString()))
            {
                return false;
            }

            var normalizedPath = path.GetString()!
                .Replace('\\', '/')
                .Trim()
                .ToLowerInvariant();
            return normalizedPath.EndsWith(ReviewedDragonsClassesPath, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? RewriteTranslatedEntityType(string? contentJson, string targetType)
    {
        if (string.IsNullOrWhiteSpace(contentJson))
        {
            return contentJson;
        }

        try
        {
            var content = JsonNode.Parse(contentJson) as JsonObject;
            if (content?["_rulesCore"] is not JsonObject rulesCore
                || rulesCore["context"] is not JsonObject context)
            {
                return contentJson;
            }

            context["translatedEntityType"] = targetType;
            return content.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }
        catch (JsonException)
        {
            return contentJson;
        }
    }

    private static bool IsSpecificClassCategory(string entityType) =>
        string.Equals(entityType, RuleConceptEntityTypes.PrestigeClass, StringComparison.OrdinalIgnoreCase)
        || string.Equals(entityType, RuleConceptEntityTypes.SidekickClass, StringComparison.OrdinalIgnoreCase);
}
