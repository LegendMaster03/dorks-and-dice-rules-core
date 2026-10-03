using System.Text.Json;
using System.Text.Json.Nodes;
using RulesCore.Application.Sources;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Normalizes source-specific epic terminology and separates epic continuation material from
/// ordinary base/prestige-class concepts while preserving immutable source-native RawJson.
///
/// Epic is a cross-cutting rules tier, not an entity family. Entity types remain feat, class,
/// prestigeClass, spell, item, and so on; only continuation documents use the generic
/// classProgression/prestigeClassProgression types because they are additional progression for an
/// existing concept rather than another selectable class.
/// </summary>
internal static class EpicContentNormalizationPolicy
{
    public const string EpicFeatCategory = "Epic";
    public const string EpicFeatCanonicalTerm = "Epic Feat";

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

        if (string.Equals(
                representation.FormatKey,
                FiveEToolsSourceFormatAdapter.Format,
                StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeFiveETools(record);
        }

        if (string.Equals(
                representation.FormatKey,
                LegacySrdSourceFormatAdapter.Format,
                StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeLegacySrd(record);
        }

        if (string.Equals(
                representation.FormatKey,
                PcGenSourceFormatAdapter.Format,
                StringComparison.OrdinalIgnoreCase))
        {
            return NormalizePcGen(record);
        }

        return record;
    }

    public static bool IsReviewedIdentityMigration(
        string existingEntityType,
        string existingName,
        NormalizedSourceRecord normalized)
    {
        if (string.IsNullOrWhiteSpace(existingEntityType)
            || string.IsNullOrWhiteSpace(existingName))
        {
            return false;
        }

        if (string.Equals(
                normalized.EntityType,
                RuleConceptEntityTypes.ClassProgression,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(existingEntityType, RuleConceptEntityTypes.Class, StringComparison.OrdinalIgnoreCase)
            && string.Equals(existingName.Trim(), $"Epic {normalized.Name}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(
                normalized.EntityType,
                RuleConceptEntityTypes.PrestigeClassProgression,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(existingEntityType, RuleConceptEntityTypes.PrestigeClass, StringComparison.OrdinalIgnoreCase)
            && string.Equals(existingName.Trim(), $"Epic {normalized.Name}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(
                   normalized.EntityType,
                   RuleConceptEntityTypes.PrestigeClass,
                   StringComparison.OrdinalIgnoreCase)
               && string.Equals(existingEntityType, RuleConceptEntityTypes.Class, StringComparison.OrdinalIgnoreCase)
               && string.Equals(existingName.Trim(), normalized.Name, StringComparison.OrdinalIgnoreCase)
               && HasEpicMetadata(normalized.ContentJson);
    }

    private static NormalizedSourceRecord NormalizeFiveETools(NormalizedSourceRecord record)
    {
        if (!string.Equals(record.EntityType, "feat", StringComparison.OrdinalIgnoreCase)
            || !TryParseObject(record.ContentJson!, out var content)
            || !IsEpicBoonCategory(content))
        {
            return record;
        }

        content["category"] = EpicFeatCategory;
        AddEpicMetadata(
            content,
            kind: "feat",
            canonicalTerm: EpicFeatCanonicalTerm,
            sourceTerm: "Epic Boon Feat",
            sourceCategory: "EB");
        return record with { ContentJson = Serialize(content) };
    }

    private static NormalizedSourceRecord NormalizeLegacySrd(NormalizedSourceRecord record)
    {
        if (!TryParseObject(record.RawJson, out var source)
            || !TryParseObject(record.ContentJson!, out var content))
        {
            return record;
        }

        var documentUri = ReadString(source, "documentUri");
        var originalHeading = ReadString(source, "originalHeading") ?? record.Name;
        var sourceBody = ReadString(source, "body") ?? string.Empty;
        var isEpicDocument = IsEpicDocument(documentUri);

        if (IsLegacyContinuation(record, sourceBody, out var continuationName))
        {
            var continuationOfType = string.Equals(
                record.EntityType,
                RuleConceptEntityTypes.Class,
                StringComparison.OrdinalIgnoreCase)
                ? RuleConceptEntityTypes.Class
                : RuleConceptEntityTypes.PrestigeClass;
            var progressionType = string.Equals(
                continuationOfType,
                RuleConceptEntityTypes.Class,
                StringComparison.Ordinal)
                ? RuleConceptEntityTypes.ClassProgression
                : RuleConceptEntityTypes.PrestigeClassProgression;

            content["name"] = continuationName;
            AddEpicMetadata(
                content,
                kind: "progression",
                sourceTerm: originalHeading.Trim(),
                continuationOfEntityType: continuationOfType,
                continuationOfName: continuationName,
                startsAfterClassLevel: 20);
            return record with
            {
                EntityType = progressionType,
                Name = continuationName,
                ContentJson = Serialize(content)
            };
        }

        if (string.Equals(record.EntityType, "feat", StringComparison.OrdinalIgnoreCase)
            && (isEpicDocument || ContainsEpicMarker(originalHeading)))
        {
            content["category"] = EpicFeatCategory;
            AddEpicMetadata(
                content,
                kind: "feat",
                canonicalTerm: EpicFeatCanonicalTerm,
                sourceTerm: "Epic Feat");
            return record with { ContentJson = Serialize(content) };
        }

        if (!isEpicDocument)
        {
            return record;
        }

        AddEpicMetadata(
            content,
            kind: string.Equals(
                record.EntityType,
                RuleConceptEntityTypes.PrestigeClass,
                StringComparison.OrdinalIgnoreCase)
                ? "prestige-class"
                : "content");
        return record with { ContentJson = Serialize(content) };
    }

    private static NormalizedSourceRecord NormalizePcGen(NormalizedSourceRecord record)
    {
        if (!TryParseObject(record.RawJson, out var source)
            || !TryParseObject(record.ContentJson!, out var content))
        {
            return record;
        }

        var path = ReadString(source, "path") ?? string.Empty;
        var typeValues = ReadPcGenTagValues(source, "TYPE");
        var isEpic = typeValues.Any(ContainsEpicTypeToken) || IsEpicPcGenPath(path);
        if (!isEpic)
        {
            return record;
        }

        if (string.Equals(record.EntityType, "feat", StringComparison.OrdinalIgnoreCase))
        {
            content["category"] = EpicFeatCategory;
            AddEpicMetadata(
                content,
                kind: "feat",
                canonicalTerm: EpicFeatCanonicalTerm,
                sourceTerm: "Epic Feat");
            return record with { ContentJson = Serialize(content) };
        }

        if (IsReviewedPcGenEpicPrestigeClass(record, source, path, typeValues))
        {
            AddEpicMetadata(content, kind: "prestige-class");
            return record with
            {
                EntityType = RuleConceptEntityTypes.PrestigeClass,
                ContentJson = Serialize(content)
            };
        }

        AddEpicMetadata(content, kind: "content");
        return record with { ContentJson = Serialize(content) };
    }

    private static bool IsLegacyContinuation(
        NormalizedSourceRecord record,
        string sourceBody,
        out string continuationName)
    {
        continuationName = string.Empty;
        var isClassFamily = string.Equals(
                                record.EntityType,
                                RuleConceptEntityTypes.Class,
                                StringComparison.OrdinalIgnoreCase)
                            || string.Equals(
                                record.EntityType,
                                RuleConceptEntityTypes.PrestigeClass,
                                StringComparison.OrdinalIgnoreCase);
        if (!isClassFamily
            || !record.Name.StartsWith("Epic ", StringComparison.OrdinalIgnoreCase)
            || !sourceBody.Contains("Skill Points at Each Additional Level", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        continuationName = record.Name["Epic ".Length..].Trim();
        return continuationName.Length > 0;
    }

    private static bool IsReviewedPcGenEpicPrestigeClass(
        NormalizedSourceRecord record,
        JsonObject source,
        string path,
        IReadOnlyList<string> typeValues)
    {
        if (!string.Equals(record.EntityType, RuleConceptEntityTypes.Class, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(ReadString(source, "kind"), "class-record", StringComparison.OrdinalIgnoreCase)
            || !PathEndsWith(path, "/rsrd/epic/rsrd_classes_epic.lst"))
        {
            return false;
        }

        return typeValues.Any(value => value
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(token => string.Equals(token, "Epic", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsEpicBoonCategory(JsonObject content) =>
        string.Equals(ReadString(content, "category"), "EB", StringComparison.OrdinalIgnoreCase);

    private static bool IsEpicDocument(string? documentUri)
    {
        if (!Uri.TryCreate(documentUri, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var path = Uri.UnescapeDataString(uri.AbsolutePath).Replace('\\', '/');
        return path.Contains("/epic/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEpicPcGenPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/epic/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/epic_psionics/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathEndsWith(string path, string suffix) =>
        path.Replace('\\', '/').EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsEpicMarker(string value) =>
        value.Contains("[Epic]", StringComparison.OrdinalIgnoreCase)
        || value.Contains("(Epic)", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsEpicTypeToken(string value) =>
        value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(token => token.StartsWith("Epic", StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> ReadPcGenTagValues(JsonObject source, string tag)
    {
        if (source["segments"] is not JsonArray segments)
        {
            return [];
        }

        var values = new List<string>();
        foreach (var node in segments)
        {
            if (node is not JsonObject segment
                || !string.Equals(ReadString(segment, "Tag"), tag, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = ReadString(segment, "Value");
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }
        return values;
    }

    private static void AddEpicMetadata(
        JsonObject content,
        string kind,
        string? canonicalTerm = null,
        string? sourceTerm = null,
        string? sourceCategory = null,
        string? continuationOfEntityType = null,
        string? continuationOfName = null,
        int? startsAfterClassLevel = null)
    {
        var rulesCore = content["_rulesCore"] as JsonObject ?? new JsonObject();
        var epic = rulesCore["epic"] as JsonObject ?? new JsonObject();
        epic["tier"] = "epic";
        epic["kind"] = kind;
        if (!string.IsNullOrWhiteSpace(canonicalTerm)) epic["canonicalTerm"] = canonicalTerm;
        if (!string.IsNullOrWhiteSpace(sourceTerm)) epic["sourceTerm"] = sourceTerm;
        if (!string.IsNullOrWhiteSpace(sourceCategory)) epic["sourceCategory"] = sourceCategory;
        if (!string.IsNullOrWhiteSpace(continuationOfEntityType)
            && !string.IsNullOrWhiteSpace(continuationOfName))
        {
            epic["continuationOf"] = new JsonObject
            {
                ["entityType"] = continuationOfEntityType,
                ["name"] = continuationOfName
            };
        }
        if (startsAfterClassLevel.HasValue)
        {
            epic["startsAfterClassLevel"] = startsAfterClassLevel.Value;
        }
        rulesCore["epic"] = epic;
        content["_rulesCore"] = rulesCore;
    }

    private static bool HasEpicMetadata(string? contentJson)
    {
        if (string.IsNullOrWhiteSpace(contentJson)
            || !TryParseObject(contentJson, out var content)
            || content["_rulesCore"] is not JsonObject rulesCore
            || rulesCore["epic"] is not JsonObject epic)
        {
            return false;
        }

        return string.Equals(ReadString(epic, "tier"), "epic", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseObject(string json, out JsonObject value)
    {
        value = null!;
        try
        {
            value = JsonNode.Parse(json) as JsonObject ?? null!;
            return value is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadString(JsonObject value, string propertyName) =>
        value[propertyName] is JsonValue node && node.TryGetValue<string>(out var text)
            ? text
            : null;

    private static string Serialize(JsonObject value) =>
        value.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
}
