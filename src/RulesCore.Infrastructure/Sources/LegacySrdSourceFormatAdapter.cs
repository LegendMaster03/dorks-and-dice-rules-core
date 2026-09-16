using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Reads the reviewed 3e/3.5e SRD snapshot representation without pretending that the
/// legacy records are native 5e.tools entities. Native evidence stays in RawJson while
/// mechanical translation is delegated to the legacy SRD translator below.
/// </summary>
public sealed class LegacySrdSourceFormatAdapter : ISourceFormatAdapter
{
    public const string Format = "legacy-srd-snapshot";

    public string FormatKey => Format;

    public bool IsCandidate(string? fileName, ReadOnlySpan<byte> content)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        return string.Equals(name, "srd-3e.json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "srd-3-5e.json", StringComparison.OrdinalIgnoreCase);
    }

    public NormalizedSourceRepresentation? TryRead(SourceRepresentationArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        if (!IsCandidate(artifact.FileName, artifact.Content) || artifact.Content.Length == 0)
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(artifact.Content);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var records = new List<NormalizedSourceRecord>();
        var duplicateCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in property.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !TryReadRequiredString(item, "name", out var name)
                    || !TryReadRequiredString(item, "source", out var source)
                    || !TryReadRequiredString(item, "uniqueId", out var uniqueId)
                    || !TryReadRequiredString(item, "documentUri", out _)
                    || !item.TryGetProperty("body", out var body)
                    || body.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                // Preserve the native key and native identity produced by the former
                // pseudo-5e.tools path so existing source_entity rows can be migrated in
                // place instead of becoming duplicate source identities.
                var baseKey = $"{property.Name}|{source}|{name}|{uniqueId}";
                duplicateCounts.TryGetValue(baseKey, out var duplicateOrdinal);
                duplicateCounts[baseKey] = duplicateOrdinal + 1;
                var nativeKey = duplicateOrdinal == 0
                    ? baseKey
                    : $"{baseKey}|duplicate-{duplicateOrdinal}";
                var rawJson = item.GetRawText();
                var record = new NormalizedSourceRecord(
                    property.Name,
                    name,
                    source,
                    nativeKey,
                    rawJson,
                    LocatorKey: null,
                    PublicationLocalKey: $"source:{source}",
                    NativeIdentityJson: JsonSerializer.Serialize(new
                    {
                        source,
                        id = (string?)null,
                        uniqueId,
                        parentSource = (string?)null,
                        edition = (string?)null
                    }))
                {
                    ContentJson = LegacySrdMechanicalTranslator.Translate(
                        property.Name,
                        name,
                        source,
                        rawJson,
                        InferEdition(source))
                };
                records.Add(record);
            }
        }

        if (records.Count == 0)
        {
            return null;
        }

        var publications = records
            .Select(value => value.SourceCode)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(source => new NormalizedSourcePublication(
                $"source:{source}",
                source,
                GameEdition: InferEdition(source),
                ExternalIdentifiers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["5etools-source-code"] = source
                }))
            .ToArray();

        return new NormalizedSourceRepresentation(
            Format,
            artifact,
            records,
            publications,
            JsonSerializer.Serialize(new
            {
                schemaFamily = "reviewed-legacy-srd-snapshot",
                nativeRecordCount = records.Count,
                semantics = "source-native-legacy-records-with-rules-core-mechanical-translation"
            }));
    }

    private static string? InferEdition(string sourceCode) => sourceCode.ToUpperInvariant() switch
    {
        "SRD3" => "3e",
        "SRD35" => "3.5e",
        _ => null
    };

    private static bool TryReadRequiredString(JsonElement value, string name, out string result)
    {
        result = string.Empty;
        if (!value.TryGetProperty(name, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            return false;
        }
        result = property.GetString()!.Trim();
        return true;
    }
}

/// <summary>
/// Backend-only translation for reviewed legacy SRD records. The common mechanical
/// document receives only meaning-preserving fields. 3.x-specific mechanics remain
/// explicit beneath _rulesCore.threeX rather than being converted into fake 5e rules.
/// </summary>
internal static class LegacySrdMechanicalTranslator
{
    private static readonly Regex LabelLine = new(
        @"(?m)^(?<label>[A-Za-z][A-Za-z /&()\-]{1,48}):\s*(?<value>[^\r\n]*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SignedNumber = new(
        @"(?<!\w)(?<value>[+-]?\d+)(?!\w)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex HitPointsInDice = new(
        @"^(?<formula>\d+d\d+(?:[+-]\d+)?)\s*\((?<average>\d+)\s*hp\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SpeedPart = new(
        @"(?:(?<mode>burrow|climb|fly|swim)\s+)?(?<feet>\d+)\s*ft\.?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex AbilityPair = new(
        @"\b(?<ability>Str|Dex|Con|Int|Wis|Cha)\s+(?<score>\d+|—|-)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SavePair = new(
        @"\b(?<save>Fort|Ref|Will)\s+(?<bonus>[+-]?\d+)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SkillPair = new(
        @"(?<name>[A-Za-z][A-Za-z '\-()]+?)\s+(?<bonus>[+-]\d+)(?:,|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyDictionary<string, string> SizeCodes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tiny"] = "T",
            ["Small"] = "S",
            ["Medium"] = "M",
            ["Large"] = "L",
            ["Huge"] = "H",
            ["Gargantuan"] = "G"
        };

    private static readonly HashSet<string> CommonCreatureTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "aberration", "animal", "beast", "celestial", "construct", "dragon", "elemental",
        "fey", "fiend", "giant", "humanoid", "monstrous humanoid", "ooze", "outsider",
        "plant", "undead", "vermin"
    };

    public static string Translate(
        string entityType,
        string name,
        string sourceCode,
        string rawJson,
        string? edition)
    {
        using var sourceDocument = JsonDocument.Parse(rawJson);
        var root = sourceDocument.RootElement;
        var body = ReadString(root, "body") ?? string.Empty;
        var content = new JsonObject
        {
            ["name"] = name,
            ["source"] = sourceCode
        };
        if (!string.IsNullOrWhiteSpace(body))
        {
            content["entries"] = new JsonArray(body);
        }

        var fields = ParseFields(body);
        var extension = new JsonObject
        {
            ["context"] = new JsonObject
            {
                ["sourceFormat"] = LegacySrdSourceFormatAdapter.Format,
                ["nativeEntityType"] = entityType,
                ["edition"] = edition
            }
        };
        var threeX = new JsonObject();
        extension["threeX"] = threeX;

        if (IsMonster(entityType))
        {
            TranslateMonster(content, threeX, fields);
        }
        else
        {
            PreserveLabeledMechanics(threeX, fields);
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            threeX["sourceBody"] = body;
        }
        content["_rulesCore"] = extension;
        return content.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static void TranslateMonster(
        JsonObject content,
        JsonObject threeX,
        IReadOnlyDictionary<string, string> fields)
    {
        if (TryField(fields, out var sizeAndType, "Size/Type", "Size And Type", "SizeAndType"))
        {
            ParseSizeAndType(content, threeX, sizeAndType);
        }
        else
        {
            if (TryField(fields, out var size, "Size"))
            {
                MapSize(content, threeX, size);
            }
            if (TryField(fields, out var type, "Type"))
            {
                MapType(content, threeX, type);
            }
        }

        if (TryField(fields, out var hitDice, "Hit Dice", "Hit Die"))
        {
            threeX["hitDice"] = hitDice;
            var hitPoints = HitPointsInDice.Match(hitDice);
            if (hitPoints.Success
                && int.TryParse(hitPoints.Groups["average"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var average))
            {
                content["hp"] = new JsonObject
                {
                    ["average"] = average,
                    ["formula"] = hitPoints.Groups["formula"].Value
                };
            }
        }
        if (TryField(fields, out var hitPointsText, "Hit Points", "HP")
            && TryFirstInt(hitPointsText, out var explicitHitPoints))
        {
            content["hp"] = new JsonObject { ["average"] = explicitHitPoints };
            threeX["hitPoints"] = hitPointsText;
        }

        if (TryField(fields, out var armorClass, "Armor Class", "AC"))
        {
            threeX["armorClass"] = armorClass;
            if (TryFirstInt(armorClass, out var armorClassValue))
            {
                content["ac"] = new JsonArray(armorClassValue);
            }
        }

        if (TryField(fields, out var initiative, "Initiative")) threeX["initiative"] = initiative;
        if (TryField(fields, out var movement, "Speed", "Movement"))
        {
            threeX["speed"] = movement;
            var speed = ParseSpeed(movement);
            if (speed.Count > 0) content["speed"] = speed;
        }

        if (TryField(fields, out var abilities, "Abilities", "Ability Scores"))
        {
            threeX["abilities"] = abilities;
            foreach (Match match in AbilityPair.Matches(abilities))
            {
                if (int.TryParse(match.Groups["score"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var score))
                {
                    content[match.Groups["ability"].Value.ToLowerInvariant()] = score;
                }
            }
        }

        if (TryField(fields, out var saves, "Saves", "Saving Throws"))
        {
            threeX["savesText"] = saves;
            var saveObject = new JsonObject();
            foreach (Match match in SavePair.Matches(saves))
            {
                if (int.TryParse(match.Groups["bonus"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bonus))
                {
                    var key = match.Groups["save"].Value.ToLowerInvariant() switch
                    {
                        "fort" => "fortitude",
                        "ref" => "reflex",
                        _ => "will"
                    };
                    saveObject[key] = bonus;
                }
            }
            if (saveObject.Count > 0) threeX["saves"] = saveObject;
        }

        if (TryField(fields, out var baseAttackGrapple, "Base Attack/Grapple", "Base Attack / Grapple"))
        {
            threeX["baseAttackAndGrapple"] = baseAttackGrapple;
            var values = SignedNumber.Matches(baseAttackGrapple).Select(value => value.Groups["value"].Value).ToArray();
            if (values.Length > 0 && int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var bab))
                threeX["baseAttackBonus"] = bab;
            if (values.Length > 1 && int.TryParse(values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var grapple))
                threeX["grapple"] = grapple;
        }
        else
        {
            if (TryField(fields, out var babText, "Base Attack Bonus", "BAB"))
            {
                threeX["baseAttackBonusText"] = babText;
                if (TryFirstSignedInt(babText, out var bab)) threeX["baseAttackBonus"] = bab;
            }
            if (TryField(fields, out var grappleText, "Grapple"))
            {
                threeX["grappleText"] = grappleText;
                if (TryFirstSignedInt(grappleText, out var grapple)) threeX["grapple"] = grapple;
            }
        }

        CopyField(threeX, fields, "Attack", "attack");
        CopyField(threeX, fields, "Full Attack", "fullAttack");
        CopyField(threeX, fields, "Damage", "damage");
        CopyField(threeX, fields, "Space/Reach", "spaceAndReach");
        CopyField(threeX, fields, "Face/Reach", "faceAndReach");
        CopyField(threeX, fields, "Special Attacks", "specialAttacks");
        CopyField(threeX, fields, "Special Qualities", "specialQualities");
        CopyField(threeX, fields, "Environment", "environment");
        CopyField(threeX, fields, "Organization", "organization");
        CopyField(threeX, fields, "Treasure", "treasure");
        CopyField(threeX, fields, "Alignment", "alignment");
        CopyField(threeX, fields, "Advancement", "advancement");
        CopyField(threeX, fields, "Level Adjustment", "levelAdjustment");
        CopyField(threeX, fields, "Spell Resistance", "spellResistance");
        CopyField(threeX, fields, "Damage Reduction", "damageReduction");
        CopyField(threeX, fields, "Immunities", "immunities");
        CopyField(threeX, fields, "Resistances", "resistances");

        if (TryField(fields, out var challengeRating, "Challenge Rating", "CR"))
        {
            var normalized = challengeRating.Trim();
            if (!string.IsNullOrWhiteSpace(normalized)) content["cr"] = normalized;
            threeX["challengeRating"] = challengeRating;
        }

        if (TryField(fields, out var skills, "Skills"))
        {
            threeX["skillsText"] = skills;
            var skillObject = new JsonObject();
            foreach (Match match in SkillPair.Matches(skills))
            {
                if (int.TryParse(match.Groups["bonus"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bonus))
                {
                    skillObject[match.Groups["name"].Value.Trim()] = bonus;
                }
            }
            if (skillObject.Count > 0) threeX["skills"] = skillObject;
        }

        if (TryField(fields, out var feats, "Feats"))
        {
            threeX["featsText"] = feats;
            var values = feats.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (values.Length > 0)
            {
                var array = new JsonArray();
                foreach (var value in values) array.Add(value);
                threeX["feats"] = array;
            }
        }

        PreserveUnmappedFields(threeX, fields);
    }

    private static void ParseSizeAndType(JsonObject content, JsonObject threeX, string value)
    {
        threeX["sizeAndType"] = value;
        var firstSpace = value.IndexOf(' ');
        if (firstSpace <= 0)
        {
            MapType(content, threeX, value);
            return;
        }
        MapSize(content, threeX, value[..firstSpace]);
        MapType(content, threeX, value[(firstSpace + 1)..]);
    }

    private static void MapSize(JsonObject content, JsonObject threeX, string value)
    {
        var normalized = value.Trim();
        threeX["size"] = normalized;
        if (SizeCodes.TryGetValue(normalized, out var code))
        {
            content["size"] = new JsonArray(code);
        }
    }

    private static void MapType(JsonObject content, JsonObject threeX, string value)
    {
        var normalized = value.Trim();
        threeX["creatureType"] = normalized;
        var typeName = normalized;
        var subtypeStart = normalized.IndexOf('(');
        if (subtypeStart >= 0)
        {
            typeName = normalized[..subtypeStart].Trim();
            var subtypeEnd = normalized.LastIndexOf(')');
            var subtypeText = subtypeEnd > subtypeStart
                ? normalized[(subtypeStart + 1)..subtypeEnd]
                : normalized[(subtypeStart + 1)..];
            var subtypes = subtypeText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (subtypes.Length > 0)
            {
                var array = new JsonArray();
                foreach (var subtype in subtypes) array.Add(subtype);
                threeX["subtypes"] = array;
            }
        }
        if (CommonCreatureTypes.Contains(typeName))
        {
            content["type"] = typeName.ToLowerInvariant();
        }
    }

    private static JsonObject ParseSpeed(string value)
    {
        var result = new JsonObject();
        foreach (Match match in SpeedPart.Matches(value))
        {
            if (!int.TryParse(match.Groups["feet"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var feet))
                continue;
            var mode = match.Groups["mode"].Success
                ? match.Groups["mode"].Value.ToLowerInvariant()
                : "walk";
            result[mode] = feet;
        }
        return result;
    }

    private static IReadOnlyDictionary<string, string> ParseFields(string body)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in LabelLine.Matches(body ?? string.Empty))
        {
            var label = match.Groups["label"].Value.Trim();
            var value = match.Groups["value"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(value)) result[label] = value;
        }
        return result;
    }

    private static void PreserveLabeledMechanics(JsonObject threeX, IReadOnlyDictionary<string, string> fields)
    {
        if (fields.Count == 0) return;
        var values = new JsonObject();
        foreach (var field in fields.OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase))
            values[field.Key] = field.Value;
        threeX["fields"] = values;
    }

    private static void PreserveUnmappedFields(JsonObject threeX, IReadOnlyDictionary<string, string> fields)
    {
        var preserved = new JsonObject();
        foreach (var field in fields.OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase))
            preserved[field.Key] = field.Value;
        if (preserved.Count > 0) threeX["fields"] = preserved;
    }

    private static void CopyField(JsonObject target, IReadOnlyDictionary<string, string> fields, string sourceName, string targetName)
    {
        if (fields.TryGetValue(sourceName, out var value) && !string.IsNullOrWhiteSpace(value)) target[targetName] = value;
    }

    private static bool TryField(IReadOnlyDictionary<string, string> fields, out string value, params string[] names)
    {
        foreach (var name in names)
        {
            if (fields.TryGetValue(name, out var found) && !string.IsNullOrWhiteSpace(found))
            {
                value = found;
                return true;
            }
        }
        value = string.Empty;
        return false;
    }

    private static bool TryFirstInt(string value, out int result)
    {
        var match = Regex.Match(value ?? string.Empty, @"\b\d+\b", RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryFirstSignedInt(string value, out int result)
    {
        var match = SignedNumber.Match(value ?? string.Empty);
        return match.Success && int.TryParse(match.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool IsMonster(string entityType) =>
        string.Equals(entityType, "monster", StringComparison.OrdinalIgnoreCase)
        || string.Equals(entityType, "creature", StringComparison.OrdinalIgnoreCase);

    private static string? ReadString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
