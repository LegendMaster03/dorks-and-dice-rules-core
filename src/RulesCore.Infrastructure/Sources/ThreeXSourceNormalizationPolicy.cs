using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Corrects source-format-specific 3.x interpretations after the source translator has run.
/// The source-native record and RawJson remain immutable; this policy only repairs derived
/// Rules Core entity classification and mechanical content.
/// </summary>
internal static class ThreeXSourceNormalizationPolicy
{
    private static readonly HashSet<string> SourceOnlyPcGenTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "KEY",
        "SOURCE",
        "SOURCEDATE",
        "SOURCELONG",
        "SOURCEPAGE",
        "SOURCESHORT",
        "SOURCEWEB"
    };

    private static readonly HashSet<string> CommonCreatureTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "aberration",
        "beast",
        "celestial",
        "construct",
        "dragon",
        "elemental",
        "fey",
        "fiend",
        "giant",
        "humanoid",
        "monstrosity",
        "ooze",
        "plant",
        "undead"
    };

    private static readonly Regex HtmlTable = new(
        @"<table\b[^>]*>.*?</table>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex HtmlRow = new(
        @"<tr\b[^>]*>(?<row>.*?)</tr>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex HtmlCell = new(
        @"<(?<kind>th|td)\b[^>]*>(?<value>.*?)</(?:th|td)>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex HtmlBreak = new(
        @"<br\s*/?>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HtmlBlockBoundary = new(
        @"</(?:p|div|h[1-6]|li|ul|ol)\s*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HtmlTag = new(
        @"<[^>]+>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex CollapsedWhitespace = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SizeAndType = new(
        @"^(?<size>Fine|Diminutive|Tiny|Small|Medium|Large|Huge|Gargantuan|Colossal)\s+(?<type>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HitDiceWithAverage = new(
        @"(?<formula>\d+d\d+(?:\s*[+-]\s*\d+)?)\s*\((?<average>\d+)\s*hp\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex AbilityPair = new(
        @"\b(?<ability>Str|Dex|Con|Int|Wis|Cha)\s+(?<score>\d+|—|-)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SavePair = new(
        @"\b(?<save>Fort|Ref|Will)\s+(?<bonus>[+-]?\d+)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SpeedPart = new(
        @"(?:(?<mode>burrow|climb|fly|swim)\s+)?(?<feet>\d+)\s*ft\.?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SignedNumber = new(
        @"(?<!\w)(?<value>[+-]?\d+)(?!\w)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static NormalizedSourceRecord Apply(
        NormalizedSourceRepresentation representation,
        NormalizedSourceRecord record)
    {
        ArgumentNullException.ThrowIfNull(representation);
        ArgumentNullException.ThrowIfNull(record);

        if (string.Equals(
                representation.FormatKey,
                PcGenSourceFormatAdapter.Format,
                StringComparison.OrdinalIgnoreCase))
        {
            return NormalizePcGenRace(record);
        }

        if (string.Equals(
                representation.FormatKey,
                LegacySrdSourceFormatAdapter.Format,
                StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeLegacySrdMonster(record);
        }

        return record;
    }

    private static NormalizedSourceRecord NormalizePcGenRace(NormalizedSourceRecord record)
    {
        if (!string.Equals(record.EntityType, "monster", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(record.ContentJson)
            || !TryReadPcGenRaceSegments(record.RawJson, out var segments))
        {
            return record;
        }

        var content = JsonNode.Parse(record.ContentJson)?.AsObject();
        if (content is null)
        {
            return record;
        }

        // PCGen represents both player races and monster racial chassis with RACE records.
        // MONSTERCLASS and placement below a monsters directory describe racial-HD assembly;
        // they do not turn the RACE record into a complete monster stat block.
        content.Remove("type");
        content.Remove("cr");

        var mapped = new HashSet<int>();
        var size = Last(segments, "SIZE");
        if (size is not null && TryNormalizeSizeCode(size.Value, out var sizeCode))
        {
            content["size"] = new JsonArray(sizeCode);
            mapped.Add(size.Index);
        }
        else
        {
            content.Remove("size");
        }

        var movement = Last(segments, "MOVE") ?? Last(segments, "SPEED");
        if (movement is not null && TryReadPcGenWalkSpeed(movement.Value, out var walk, out var complete))
        {
            content["speed"] = walk;
            if (complete)
            {
                mapped.Add(movement.Index);
            }
        }
        else
        {
            content.Remove("speed");
        }

        var abilityBonuses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var bonus in All(segments, "BONUS"))
        {
            var parts = bonus.Value.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length < 3
                || !string.Equals(parts[0], "STAT", StringComparison.OrdinalIgnoreCase)
                || !TryNormalizeAbility(parts[1], out var ability)
                || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
            {
                continue;
            }

            abilityBonuses[ability] = abilityBonuses.GetValueOrDefault(ability) + amount;
            mapped.Add(bonus.Index);
        }

        if (abilityBonuses.Count > 0)
        {
            var ability = new JsonObject();
            foreach (var value in abilityBonuses.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                ability[value.Key] = value.Value;
            }
            content["ability"] = new JsonArray(ability);
        }
        else
        {
            content.Remove("ability");
        }

        foreach (var description in All(segments, "DESC").Where(value =>
                     !string.IsNullOrWhiteSpace(value.Value)
                     && !value.Value.Contains('|', StringComparison.Ordinal)))
        {
            mapped.Add(description.Index);
        }

        var extension = content["_rulesCore"] as JsonObject ?? new JsonObject();
        var context = extension["context"] as JsonObject ?? new JsonObject();
        context["sourceFormat"] = PcGenSourceFormatAdapter.Format;
        context["nativeEntityType"] = "race";
        context.Remove("translatedEntityType");
        extension["context"] = context;

        var unmapped = segments
            .Where(value => !mapped.Contains(value.Index))
            .Where(value => !SourceOnlyPcGenTags.Contains(value.Tag))
            .OrderBy(value => value.Index)
            .ToArray();
        if (unmapped.Length == 0)
        {
            extension.Remove("pcgen");
        }
        else
        {
            var values = new JsonArray();
            foreach (var segment in unmapped)
            {
                values.Add(new JsonObject
                {
                    ["tag"] = segment.Tag,
                    ["value"] = segment.Value
                });
            }
            extension["pcgen"] = new JsonObject
            {
                ["unmappedSegments"] = values
            };
        }
        content["_rulesCore"] = extension;

        return record with
        {
            EntityType = "race",
            ContentJson = content.ToJsonString(new JsonSerializerOptions { WriteIndented = false })
        };
    }

    private static NormalizedSourceRecord NormalizeLegacySrdMonster(NormalizedSourceRecord record)
    {
        if (!string.Equals(record.EntityType, "monster", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(record.ContentJson)
            || !TryReadRawBody(record.RawJson, out var body)
            || !HtmlTable.IsMatch(body))
        {
            return record;
        }

        var content = JsonNode.Parse(record.ContentJson)?.AsObject();
        if (content is null)
        {
            return record;
        }

        var fields = ParseHtmlStatTable(body, out var sizeAndType);
        var extension = content["_rulesCore"] as JsonObject ?? new JsonObject();
        var threeX = extension["threeX"] as JsonObject ?? new JsonObject();

        if (!string.IsNullOrWhiteSpace(sizeAndType))
        {
            MapLegacySizeAndType(content, threeX, sizeAndType!);
        }

        if (TryField(fields, out var armorClass, "armor class", "ac"))
        {
            threeX["armorClass"] = armorClass;
            if (TryFirstInt(armorClass, out var ac))
            {
                content["ac"] = new JsonArray(ac);
            }
        }

        if (TryField(fields, out var hitDice, "hit dice", "hit die"))
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

        if (TryField(fields, out var initiative, "initiative"))
        {
            threeX["initiative"] = initiative;
        }

        if (TryField(fields, out var movement, "speed", "movement"))
        {
            threeX["speed"] = movement;
            var speed = ParseSpeed(movement);
            if (speed.Count > 0)
            {
                content["speed"] = speed;
            }
        }

        if (TryField(fields, out var abilities, "abilities", "ability scores"))
        {
            threeX["abilities"] = abilities;
            MapAbilityScores(content, abilities);
        }

        if (TryField(fields, out var saves, "saves", "saving throws"))
        {
            threeX["savesText"] = saves;
            var saveObject = new JsonObject();
            foreach (Match match in SavePair.Matches(saves))
            {
                if (!int.TryParse(match.Groups["bonus"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bonus))
                {
                    continue;
                }
                var key = match.Groups["save"].Value.ToLowerInvariant() switch
                {
                    "fort" => "fortitude",
                    "ref" => "reflex",
                    _ => "will"
                };
                saveObject[key] = bonus;
            }
            if (saveObject.Count > 0)
            {
                threeX["saves"] = saveObject;
            }
        }

        if (TryField(fields, out var baseAttackAndGrapple, "base attack/grapple", "base attack / grapple"))
        {
            threeX["baseAttackAndGrapple"] = baseAttackAndGrapple;
            var numbers = SignedNumber.Matches(baseAttackAndGrapple)
                .Select(value => value.Groups["value"].Value)
                .ToArray();
            if (numbers.Length > 0 && TryParseInt(numbers[0], out var baseAttack))
            {
                threeX["baseAttackBonus"] = baseAttack;
            }
            if (numbers.Length > 1 && TryParseInt(numbers[1], out var grapple))
            {
                threeX["grapple"] = grapple;
            }
        }

        CopyField(threeX, fields, "attack", "attack");
        CopyField(threeX, fields, "full attack", "fullAttack");
        CopyField(threeX, fields, "damage", "damage");
        CopyField(threeX, fields, "space/reach", "spaceAndReach");
        CopyField(threeX, fields, "face/reach", "faceAndReach");
        CopyField(threeX, fields, "special attacks", "specialAttacks");
        CopyField(threeX, fields, "special qualities", "specialQualities");
        CopyField(threeX, fields, "skills", "skillsText");
        CopyField(threeX, fields, "feats", "featsText");
        CopyField(threeX, fields, "environment", "environment");
        CopyField(threeX, fields, "organization", "organization");
        CopyField(threeX, fields, "treasure", "treasure");
        CopyField(threeX, fields, "advancement", "advancement");
        CopyField(threeX, fields, "level adjustment", "levelAdjustment");

        if (TryField(fields, out var challengeRating, "challenge rating", "cr"))
        {
            threeX["challengeRating"] = challengeRating;
            content["cr"] = challengeRating;
        }

        if (TryField(fields, out var alignment, "alignment"))
        {
            threeX["alignment"] = alignment;
            content["alignment"] = new JsonArray(alignment);
        }

        var readableBody = StripHtmlStatTables(body);
        if (string.IsNullOrWhiteSpace(readableBody))
        {
            content.Remove("entries");
        }
        else
        {
            content["entries"] = new JsonArray(readableBody);
        }

        // The exact source body remains available for provenance/debugging even though raw table
        // markup is no longer exposed as ordinary rules text.
        threeX["sourceBody"] = body;
        extension["threeX"] = threeX;
        content["_rulesCore"] = extension;

        return record with
        {
            ContentJson = content.ToJsonString(new JsonSerializerOptions { WriteIndented = false })
        };
    }

    private static bool TryReadPcGenRaceSegments(string rawJson, out IReadOnlyList<PcGenRawSegment> segments)
    {
        segments = [];
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !TryReadString(document.RootElement, "entityType", out var entityType)
                || !string.Equals(entityType, "race", StringComparison.OrdinalIgnoreCase)
                || !TryGetProperty(document.RootElement, "segments", out var values)
                || values.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var result = new List<PcGenRawSegment>();
            var fallbackIndex = 0;
            foreach (var value in values.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.Object
                    || !TryReadString(value, "tag", out var tag))
                {
                    fallbackIndex++;
                    continue;
                }
                var index = TryReadInt(value, "index", out var explicitIndex)
                    ? explicitIndex
                    : fallbackIndex;
                _ = TryReadString(value, "value", out var segmentValue);
                result.Add(new PcGenRawSegment(index, tag.Trim(), segmentValue?.Trim() ?? string.Empty));
                fallbackIndex++;
            }
            segments = result;
            return result.Count > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IReadOnlyDictionary<string, string> ParseHtmlStatTable(
        string body,
        out string? sizeAndType)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        sizeAndType = null;
        foreach (Match table in HtmlTable.Matches(body))
        {
            foreach (Match row in HtmlRow.Matches(table.Value))
            {
                var cells = HtmlCell.Matches(row.Groups["row"].Value)
                    .Select(value => new HtmlCellValue(
                        value.Groups["kind"].Value,
                        CleanHtmlCell(value.Groups["value"].Value)))
                    .ToArray();
                if (cells.Length == 0)
                {
                    continue;
                }

                for (var index = 0; index < cells.Length; index++)
                {
                    if (!string.Equals(cells[index].Kind, "th", StringComparison.OrdinalIgnoreCase)
                        || string.IsNullOrWhiteSpace(cells[index].Value))
                    {
                        continue;
                    }
                    var label = NormalizeLabel(cells[index].Value);
                    var value = index + 1 < cells.Length ? cells[index + 1].Value : string.Empty;
                    if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(value))
                    {
                        fields[label] = value;
                    }
                }

                if (sizeAndType is null)
                {
                    foreach (var cell in cells.Where(value => !string.Equals(value.Kind, "th", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (SizeAndType.IsMatch(cell.Value))
                        {
                            sizeAndType = cell.Value;
                            break;
                        }
                    }
                }
            }
        }
        return fields;
    }

    private static void MapLegacySizeAndType(JsonObject content, JsonObject threeX, string value)
    {
        var match = SizeAndType.Match(value);
        if (!match.Success)
        {
            return;
        }

        if (TryNormalizeSizeCode(match.Groups["size"].Value, out var sizeCode))
        {
            content["size"] = new JsonArray(sizeCode);
        }

        var typeText = match.Groups["type"].Value.Trim();
        var opening = typeText.IndexOf('(');
        var closing = typeText.LastIndexOf(')');
        var type = (opening >= 0 ? typeText[..opening] : typeText).Trim();
        if (!string.IsNullOrWhiteSpace(type))
        {
            threeX["creatureType"] = type;
            if (CommonCreatureTypes.Contains(type))
            {
                content["type"] = type.ToLowerInvariant();
            }
        }

        if (opening >= 0 && closing > opening)
        {
            var subtypes = typeText[(opening + 1)..closing]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (subtypes.Length > 0)
            {
                threeX["creatureSubtypes"] = new JsonArray(
                    subtypes.Select(value => JsonValue.Create(value)).ToArray());
            }
        }
    }

    private static JsonObject ParseSpeed(string value)
    {
        var result = new JsonObject();
        foreach (Match match in SpeedPart.Matches(value))
        {
            if (!int.TryParse(match.Groups["feet"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var feet))
            {
                continue;
            }
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
            if (!int.TryParse(match.Groups["score"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var score))
            {
                continue;
            }
            if (TryNormalizeAbility(match.Groups["ability"].Value, out var ability))
            {
                content[ability] = score;
            }
        }
    }

    private static string StripHtmlStatTables(string body)
    {
        var value = HtmlTable.Replace(body, string.Empty);
        value = HtmlBreak.Replace(value, "\n");
        value = HtmlBlockBoundary.Replace(value, "\n");
        value = HtmlTag.Replace(value, string.Empty);
        value = WebUtility.HtmlDecode(value);
        value = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = value.Split('\n')
            .Select(line => line.Trim())
            .ToArray();
        var result = new List<string>();
        var previousBlank = true;
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                if (!previousBlank)
                {
                    result.Add(string.Empty);
                }
                previousBlank = true;
                continue;
            }
            result.Add(line);
            previousBlank = false;
        }
        while (result.Count > 0 && result[^1].Length == 0)
        {
            result.RemoveAt(result.Count - 1);
        }
        return string.Join("\n", result).Trim();
    }

    private static string CleanHtmlCell(string value)
    {
        var normalized = HtmlBreak.Replace(value, " ");
        normalized = HtmlTag.Replace(normalized, string.Empty);
        normalized = WebUtility.HtmlDecode(normalized);
        return CollapsedWhitespace.Replace(normalized, " ").Trim();
    }

    private static string NormalizeLabel(string value) =>
        value.Trim().TrimEnd(':').Trim().ToLowerInvariant();

    private static bool TryReadRawBody(string rawJson, out string body)
    {
        body = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && TryReadString(document.RootElement, "body", out body)
                && !string.IsNullOrWhiteSpace(body);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryNormalizeSizeCode(string value, out string sizeCode)
    {
        sizeCode = value.Trim().ToLowerInvariant() switch
        {
            "f" or "fine" => "F",
            "d" or "diminutive" => "D",
            "t" or "tiny" => "T",
            "s" or "small" => "S",
            "m" or "medium" => "M",
            "l" or "large" => "L",
            "h" or "huge" => "H",
            "g" or "gargantuan" => "G",
            "c" or "colossal" => "C",
            _ => string.Empty
        };
        return sizeCode.Length > 0;
    }

    private static bool TryNormalizeAbility(string value, out string ability)
    {
        ability = value.Trim().ToLowerInvariant() switch
        {
            "str" or "strength" => "str",
            "dex" or "dexterity" => "dex",
            "con" or "constitution" => "con",
            "int" or "intelligence" => "int",
            "wis" or "wisdom" => "wis",
            "cha" or "charisma" => "cha",
            _ => string.Empty
        };
        return ability.Length > 0;
    }

    private static bool TryReadPcGenWalkSpeed(string value, out int walk, out bool complete)
    {
        walk = 0;
        var tokens = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        complete = tokens.Length == 2;
        for (var index = 0; index + 1 < tokens.Length; index += 2)
        {
            if (!int.TryParse(tokens[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var distance))
            {
                complete = false;
                continue;
            }
            if (string.Equals(tokens[index], "Walk", StringComparison.OrdinalIgnoreCase))
            {
                walk = distance;
            }
        }
        return walk > 0;
    }

    private static bool TryField(
        IReadOnlyDictionary<string, string> fields,
        out string value,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (fields.TryGetValue(name, out var candidate) && !string.IsNullOrWhiteSpace(candidate))
            {
                value = candidate;
                return true;
            }
        }
        value = string.Empty;
        return false;
    }

    private static void CopyField(
        JsonObject target,
        IReadOnlyDictionary<string, string> fields,
        string label,
        string property)
    {
        if (TryField(fields, out var value, label))
        {
            target[property] = value;
        }
    }

    private static bool TryFirstInt(string value, out int result)
    {
        var match = SignedNumber.Match(value);
        return match.Success && TryParseInt(match.Groups["value"].Value, out result);
    }

    private static bool TryParseInt(string value, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static IEnumerable<PcGenRawSegment> All(
        IReadOnlyList<PcGenRawSegment> segments,
        string tag) =>
        segments.Where(value => string.Equals(value.Tag, tag, StringComparison.OrdinalIgnoreCase));

    private static PcGenRawSegment? Last(
        IReadOnlyList<PcGenRawSegment> segments,
        string tag) =>
        segments.LastOrDefault(value => string.Equals(value.Tag, tag, StringComparison.OrdinalIgnoreCase));

    private static bool TryReadString(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (!TryGetProperty(element, propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        value = property.GetString();
        return value is not null;
    }

    private static bool TryReadInt(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        return TryGetProperty(element, propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value);
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value))
        {
            return true;
        }
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private sealed record PcGenRawSegment(int Index, string Tag, string Value);
    private sealed record HtmlCellValue(string Kind, string Value);
}
