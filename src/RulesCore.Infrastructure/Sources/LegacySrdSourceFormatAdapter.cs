using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Reads the reviewed 3e/3.5e SRD maintenance snapshots as their own native format.
/// RawJson preserves the source record exactly; ContentJson is Rules Core's mechanical
/// translation and may evolve without creating a native source revision.
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
        if (artifact.Content.Length == 0 || !IsCandidate(artifact.FileName, artifact.Content))
            return null;

        using var document = JsonDocument.Parse(artifact.Content);
        if (document.RootElement.ValueKind != JsonValueKind.Object) return null;

        var records = new List<NormalizedSourceRecord>();
        var duplicates = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var group in document.RootElement.EnumerateObject())
        {
            if (group.Value.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in group.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !TryRequiredString(item, "name", out var name)
                    || !TryRequiredString(item, "source", out var source)
                    || !TryRequiredString(item, "uniqueId", out var uniqueId)
                    || !TryRequiredString(item, "documentUri", out _)
                    || !item.TryGetProperty("body", out var body)
                    || body.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                // This key intentionally matches the former pseudo-5e.tools import so known
                // bootstrap entities can be migrated in place without changing source IDs.
                var baseKey = $"{group.Name}|{source}|{name}|{uniqueId}";
                duplicates.TryGetValue(baseKey, out var duplicateOrdinal);
                duplicates[baseKey] = duplicateOrdinal + 1;
                var nativeKey = duplicateOrdinal == 0
                    ? baseKey
                    : $"{baseKey}|duplicate-{duplicateOrdinal}";
                var rawJson = item.GetRawText();
                records.Add(new NormalizedSourceRecord(
                    group.Name,
                    name,
                    source,
                    nativeKey,
                    rawJson,
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
                        group.Name,
                        name,
                        source,
                        rawJson,
                        InferEdition(source))
                });
            }
        }

        if (records.Count == 0) return null;

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
                    // The format-neutral reviewed identity is authoritative for new
                    // reconciliation. Retain the former 5e.tools-scoped alias so existing
                    // installations can be recognized and migrated in place.
                    [TrustedCanonicalAliasPolicy.PublicationSourceCodeScheme] = source,
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

    private static bool TryRequiredString(JsonElement value, string propertyName, out string result)
    {
        result = string.Empty;
        if (!value.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            return false;
        }
        result = property.GetString()!.Trim();
        return true;
    }
}

internal static class LegacySrdMechanicalTranslator
{
    private static readonly Regex LabelLine = new(
        @"(?m)^(?<label>[A-Za-z][A-Za-z /&()\-]{1,48}):\s*(?<value>[^\r\n]*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AbilityPair = new(
        @"\b(?<ability>Str|Dex|Con|Int|Wis|Cha)\s+(?<score>\d+|—|-)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SavePair = new(
        @"\b(?<save>Fort|Ref|Will)\s+(?<bonus>[+-]?\d+)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SignedNumber = new(
        @"(?<!\w)(?<value>[+-]?\d+)(?!\w)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex HitDiceWithAverage = new(
        @"(?<formula>\d+d\d+(?:\s*[+-]\s*\d+)?)\s*\((?<average>\d+)\s*hp\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SpeedPart = new(
        @"(?:(?<mode>burrow|climb|fly|swim)\s+)?(?<feet>\d+)\s*ft\.?",
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

    // Only creature types with a faithful common 5e-derived type field are mapped here.
    // 3.x-only types such as Animal, Outsider, Monstrous Humanoid, and Vermin remain in
    // _rulesCore.threeX.creatureType instead of being semantically rewritten.
    private static readonly HashSet<string> CommonCreatureTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "aberration", "beast", "celestial", "construct", "dragon", "elemental",
        "fey", "fiend", "giant", "humanoid", "monstrosity", "ooze", "plant", "undead"
    };

    public static string Translate(
        string entityType,
        string name,
        string sourceCode,
        string rawJson,
        string? edition)
    {
        using var document = JsonDocument.Parse(rawJson);
        var body = ReadString(document.RootElement, "body") ?? string.Empty;
        var content = new JsonObject
        {
            ["name"] = name,
            ["source"] = sourceCode
        };
        if (!string.IsNullOrWhiteSpace(body)) content["entries"] = new JsonArray(body);

        var fields = ParseFields(body);
        var threeX = new JsonObject();
        var extension = new JsonObject
        {
            ["context"] = new JsonObject
            {
                ["sourceFormat"] = LegacySrdSourceFormatAdapter.Format,
                ["nativeEntityType"] = entityType,
                ["edition"] = edition
            },
            ["threeX"] = threeX
        };

        if (IsMonster(entityType)) TranslateMonster(content, threeX, fields);
        else PreserveFields(threeX, fields);

        if (!string.IsNullOrWhiteSpace(body)) threeX["sourceBody"] = body;
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
            if (TryField(fields, out var size, "Size")) MapSize(content, threeX, size);
            if (TryField(fields, out var type, "Type")) MapType(content, threeX, type);
        }

        CopyField(threeX, fields, "Alignment", "alignment");
        if (TryField(fields, out var alignment, "Alignment")) content["alignment"] = new JsonArray(alignment);

        if (TryField(fields, out var armorClass, "Armor Class", "AC"))
        {
            threeX["armorClass"] = armorClass;
            if (TryFirstInt(armorClass, out var ac)) content["ac"] = new JsonArray(ac);
        }

        if (TryField(fields, out var hitDice, "Hit Dice", "Hit Die"))
        {
            threeX["hitDice"] = hitDice;
            var match = HitDiceWithAverage.Match(hitDice);
            if (match.Success
                && int.TryParse(match.Groups["average"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hp))
            {
                content["hp"] = new JsonObject
                {
                    ["average"] = hp,
                    ["formula"] = Regex.Replace(match.Groups["formula"].Value, @"\s+", string.Empty)
                };
            }
        }
        if (TryField(fields, out var explicitHpText, "Hit Points", "HP"))
        {
            threeX["hitPoints"] = explicitHpText;
            if (TryFirstInt(explicitHpText, out var hp)) content["hp"] = new JsonObject { ["average"] = hp };
        }

        if (TryField(fields, out var initiative, "Initiative")) threeX["initiative"] = initiative;
        if (TryField(fields, out var movement, "Speed", "Movement"))
        {
            threeX["speed"] = movement;
            var normalizedSpeed = ParseSpeed(movement);
            if (normalizedSpeed.Count > 0) content["speed"] = normalizedSpeed;
        }

        if (TryField(fields, out var abilities, "Abilities", "Ability Scores"))
        {
            threeX["abilities"] = abilities;
            MapAbilityScores(content, abilities);
        }
        else
        {
            var joined = string.Join(" ", new[]
            {
                ReadField(fields, "Str"), ReadField(fields, "Dex"), ReadField(fields, "Con"),
                ReadField(fields, "Int"), ReadField(fields, "Wis"), ReadField(fields, "Cha")
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (!string.IsNullOrWhiteSpace(joined)) MapAbilityScores(content, joined);
        }

        if (TryField(fields, out var saves, "Saves", "Saving Throws"))
        {
            threeX["savesText"] = saves;
            var saveObject = new JsonObject();
            foreach (Match match in SavePair.Matches(saves))
            {
                if (!int.TryParse(match.Groups["bonus"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bonus))
                    continue;
                var key = match.Groups["save"].Value.ToLowerInvariant() switch
                {
                    "fort" => "fortitude",
                    "ref" => "reflex",
                    _ => "will"
                };
                saveObject[key] = bonus;
            }
            if (saveObject.Count > 0) threeX["saves"] = saveObject;
        }

        if (TryField(fields, out var babGrapple, "Base Attack/Grapple", "Base Attack / Grapple"))
        {
            threeX["baseAttackAndGrapple"] = babGrapple;
            var values = SignedNumber.Matches(babGrapple)
                .Select(match => match.Groups["value"].Value)
                .ToArray();
            if (values.Length > 0 && TryParseInt(values[0], out var bab)) threeX["baseAttackBonus"] = bab;
            if (values.Length > 1 && TryParseInt(values[1], out var grapple)) threeX["grapple"] = grapple;
        }
        else
        {
            CopySignedField(threeX, fields, "Base Attack Bonus", "baseAttackBonus");
            CopySignedField(threeX, fields, "BAB", "baseAttackBonus");
            CopySignedField(threeX, fields, "Grapple", "grapple");
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
        CopyField(threeX, fields, "Advancement", "advancement");
        CopyField(threeX, fields, "Level Adjustment", "levelAdjustment");
        CopyField(threeX, fields, "Spell Resistance", "spellResistance");
        CopyField(threeX, fields, "Damage Reduction", "damageReduction");
        CopyField(threeX, fields, "Immunities", "immunities");
        CopyField(threeX, fields, "Resistances", "resistances");

        if (TryField(fields, out var cr, "Challenge Rating", "CR"))
        {
            threeX["challengeRating"] = cr;
            content["cr"] = cr.Trim();
        }

        if (TryField(fields, out var skills, "Skills"))
        {
            threeX["skillsText"] = skills;
            var skillObject = new JsonObject();
            foreach (Match match in SkillPair.Matches(skills))
            {
                if (TryParseInt(match.Groups["bonus"].Value, out var bonus))
                    skillObject[match.Groups["name"].Value.Trim()] = bonus;
            }
            if (skillObject.Count > 0) threeX["skills"] = skillObject;
        }

        if (TryField(fields, out var feats, "Feats"))
        {
            threeX["featsText"] = feats;
            var featArray = new JsonArray();
            foreach (var feat in feats.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                featArray.Add(feat);
            if (featArray.Count > 0) threeX["feats"] = featArray;
        }

        PreserveFields(threeX, fields);
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

    private static void PreserveFields(JsonObject target, IReadOnlyDictionary<string, string> fields)
    {
        if (fields.Count == 0) return;
        var values = new JsonObject();
        foreach (var field in fields.OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase))
            values[field.Key] = field.Value;
        target["fields"] = values;
    }

    private static void ParseSizeAndType(JsonObject content, JsonObject threeX, string value)
    {
        threeX["sizeAndType"] = value;
        var firstSpace = value.IndexOf(' ');
        if (firstSpace < 1)
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
        if (SizeCodes.TryGetValue(normalized, out var code)) content["size"] = new JsonArray(code);
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
            var subtypes = new JsonArray();
            foreach (var subtype in subtypeText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                subtypes.Add(subtype);
            if (subtypes.Count > 0) threeX["subtypes"] = subtypes;
        }
        if (CommonCreatureTypes.Contains(typeName)) content["type"] = typeName.ToLowerInvariant();
    }

    private static JsonObject ParseSpeed(string value)
    {
        var result = new JsonObject();
        foreach (Match match in SpeedPart.Matches(value))
        {
            if (!TryParseInt(match.Groups["feet"].Value, out var feet)) continue;
            var mode = match.Groups["mode"].Success
                ? match.Groups["mode"].Value.ToLowerInvariant()
                : "walk";
            result[mode] = feet;
        }
        return result;
    }

    private static void MapAbilityScores(JsonObject content, string value)
    {
        foreach (Match match in AbilityPair.Matches(value))
        {
            if (TryParseInt(match.Groups["score"].Value, out var score))
                content[match.Groups["ability"].Value.ToLowerInvariant()] = score;
        }
    }

    private static void CopySignedField(
        JsonObject target,
        IReadOnlyDictionary<string, string> fields,
        string sourceName,
        string targetName)
    {
        if (!fields.TryGetValue(sourceName, out var value) || string.IsNullOrWhiteSpace(value)) return;
        target[$"{targetName}Text"] = value;
        if (TryFirstSignedInt(value, out var parsed)) target[targetName] = parsed;
    }

    private static void CopyField(
        JsonObject target,
        IReadOnlyDictionary<string, string> fields,
        string sourceName,
        string targetName)
    {
        if (fields.TryGetValue(sourceName, out var value) && !string.IsNullOrWhiteSpace(value))
            target[targetName] = value;
    }

    private static string? ReadField(IReadOnlyDictionary<string, string> fields, string name) =>
        fields.TryGetValue(name, out var value) ? $"{name} {value}" : null;

    private static bool TryField(
        IReadOnlyDictionary<string, string> fields,
        out string value,
        params string[] names)
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
        result = 0;
        var match = Regex.Match(value ?? string.Empty, @"\b\d+\b", RegexOptions.CultureInvariant);
        return match.Success && TryParseInt(match.Value, out result);
    }

    private static bool TryFirstSignedInt(string value, out int result)
    {
        result = 0;
        var match = SignedNumber.Match(value ?? string.Empty);
        return match.Success && TryParseInt(match.Groups["value"].Value, out result);
    }

    private static bool TryParseInt(string value, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static bool IsMonster(string entityType) =>
        string.Equals(entityType, "monster", StringComparison.OrdinalIgnoreCase)
        || string.Equals(entityType, "creature", StringComparison.OrdinalIgnoreCase);

    private static string? ReadString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
