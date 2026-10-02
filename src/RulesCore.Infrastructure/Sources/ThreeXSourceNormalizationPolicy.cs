using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Repairs derived 3.x interpretations while preserving immutable source-native records.
/// </summary>
internal static class ThreeXSourceNormalizationPolicy
{
    private static readonly HashSet<string> SourceOnlyPcGenTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "KEY", "SOURCE", "SOURCEDATE", "SOURCELONG", "SOURCEPAGE", "SOURCESHORT", "SOURCEWEB"
    };

    private static readonly HashSet<string> CommonCreatureTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "aberration", "beast", "celestial", "construct", "dragon", "elemental",
        "fey", "fiend", "giant", "humanoid", "monstrosity", "ooze", "plant", "undead"
    };

    private static readonly Regex HtmlTable = Rx(@"<table\b[^>]*>.*?</table>", RegexOptions.Singleline);
    private static readonly Regex HtmlRow = Rx(@"<tr\b[^>]*>(?<row>.*?)</tr>", RegexOptions.Singleline);
    private static readonly Regex HtmlCell = Rx(@"<(?<kind>th|td)\b[^>]*>(?<value>.*?)</(?:th|td)>", RegexOptions.Singleline);
    private static readonly Regex HtmlBreak = Rx(@"<br\s*/?>");
    private static readonly Regex HtmlBlockBoundary = Rx(@"</(?:p|div|h[1-6]|li|ul|ol)\s*>");
    private static readonly Regex HtmlTag = Rx(@"<[^>]+>", RegexOptions.Singleline);
    private static readonly Regex Whitespace = Rx(@"\s+");
    private static readonly Regex SizeAndType = Rx(
        @"^(?<size>Fine|Diminutive|Tiny|Small|Medium|Large|Huge|Gargantuan|Colossal)\s+(?<type>.+)$");
    private static readonly Regex HitDice = Rx(
        @"(?<formula>\d+d\d+(?:\s*[+-]\s*\d+)?)\s*\((?<average>\d+)\s*hp\)");
    private static readonly Regex AbilityPair = Rx(
        @"\b(?<ability>Str|Dex|Con|Int|Wis|Cha)\s+(?<score>\d+|—|-)\b");
    private static readonly Regex SavePair = Rx(@"\b(?<save>Fort|Ref|Will)\s+(?<bonus>[+-]?\d+)\b");
    private static readonly Regex SpeedPart = Rx(@"(?:(?<mode>burrow|climb|fly|swim)\s+)?(?<feet>\d+)\s*ft\.?");
    private static readonly Regex SignedNumber = Rx(@"(?<!\w)(?<value>[+-]?\d+)(?!\w)");

    public static NormalizedSourceRecord Apply(
        NormalizedSourceRepresentation representation,
        NormalizedSourceRecord record)
    {
        ArgumentNullException.ThrowIfNull(representation);
        ArgumentNullException.ThrowIfNull(record);

        if (string.Equals(representation.FormatKey, PcGenSourceFormatAdapter.Format, StringComparison.OrdinalIgnoreCase))
        {
            return NormalizePcGenRace(record);
        }

        if (string.Equals(representation.FormatKey, LegacySrdSourceFormatAdapter.Format, StringComparison.OrdinalIgnoreCase))
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
        if (content is null) return record;

        // A PCGen RACE record remains a racial/species chassis. MONSTERCLASS and a monster-oriented
        // file path describe racial-HD assembly, not a complete monster stat block.
        content.Remove("type");
        content.Remove("cr");
        var mapped = new HashSet<int>();

        var size = Last(segments, "SIZE");
        if (size is not null && TrySizeCode(size.Value, out var sizeCode))
        {
            content["size"] = new JsonArray(sizeCode);
            mapped.Add(size.Index);
        }
        else
        {
            content.Remove("size");
        }

        var movement = Last(segments, "MOVE") ?? Last(segments, "SPEED");
        if (movement is not null && TryWalkSpeed(movement.Value, out var walk, out var completelyMapped))
        {
            content["speed"] = walk;
            if (completelyMapped) mapped.Add(movement.Index);
        }
        else
        {
            content.Remove("speed");
        }

        var bonuses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in All(segments, "BONUS"))
        {
            var parts = segment.Value.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length < 3
                || !string.Equals(parts[0], "STAT", StringComparison.OrdinalIgnoreCase)
                || !TryAbility(parts[1], out var ability)
                || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
            {
                continue;
            }
            bonuses[ability] = bonuses.GetValueOrDefault(ability) + amount;
            mapped.Add(segment.Index);
        }
        if (bonuses.Count > 0)
        {
            var ability = new JsonObject();
            foreach (var bonus in bonuses.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                ability[bonus.Key] = bonus.Value;
            }
            content["ability"] = new JsonArray(ability);
        }
        else
        {
            content.Remove("ability");
        }

        foreach (var description in All(segments, "DESC").Where(value =>
                     !string.IsNullOrWhiteSpace(value.Value) && !value.Value.Contains('|', StringComparison.Ordinal)))
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
            .Where(value => !mapped.Contains(value.Index) && !SourceOnlyPcGenTags.Contains(value.Tag))
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
                values.Add(new JsonObject { ["tag"] = segment.Tag, ["value"] = segment.Value });
            }
            extension["pcgen"] = new JsonObject { ["unmappedSegments"] = values };
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
            || !TryRawBody(record.RawJson, out var body)
            || !HtmlTable.IsMatch(body))
        {
            return record;
        }

        var content = JsonNode.Parse(record.ContentJson)?.AsObject();
        if (content is null) return record;
        var fields = ParseHtmlTable(body, out var sizeAndType);
        var extension = content["_rulesCore"] as JsonObject ?? new JsonObject();
        var threeX = extension["threeX"] as JsonObject ?? new JsonObject();

        if (sizeAndType is not null) MapSizeAndType(content, threeX, sizeAndType);
        MapArmorClass(content, threeX, fields);
        MapHitDice(content, threeX, fields);
        MapMovement(content, threeX, fields);
        MapAbilities(content, threeX, fields);
        MapSaves(threeX, fields);
        MapBaseAttackAndGrapple(threeX, fields);

        if (TryField(fields, out var initiative, "initiative")) threeX["initiative"] = initiative;
        if (TryField(fields, out var cr, "challenge rating", "cr"))
        {
            threeX["challengeRating"] = cr;
            content["cr"] = cr;
        }
        if (TryField(fields, out var alignment, "alignment"))
        {
            threeX["alignment"] = alignment;
            content["alignment"] = new JsonArray(alignment);
        }

        foreach (var (label, property) in new (string Label, string Property)[]
        {
            ("attack", "attack"), ("full attack", "fullAttack"), ("damage", "damage"),
            ("space/reach", "spaceAndReach"), ("face/reach", "faceAndReach"),
            ("special attacks", "specialAttacks"), ("special qualities", "specialQualities"),
            ("skills", "skillsText"), ("feats", "featsText"), ("environment", "environment"),
            ("organization", "organization"), ("treasure", "treasure"),
            ("advancement", "advancement"), ("level adjustment", "levelAdjustment")
        })
        {
            if (TryField(fields, out var value, label)) threeX[property] = value;
        }

        var readable = StripStatTables(body);
        if (readable.Length == 0) content.Remove("entries");
        else content["entries"] = new JsonArray(readable);

        // Keep the exact body for provenance/debugging; only the ordinary presentation entry is cleaned.
        threeX["sourceBody"] = body;
        extension["threeX"] = threeX;
        content["_rulesCore"] = extension;
        return record with { ContentJson = content.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) };
    }

    private static void MapArmorClass(
        JsonObject content,
        JsonObject threeX,
        IReadOnlyDictionary<string, string> fields)
    {
        if (!TryField(fields, out var text, "armor class", "ac")) return;
        threeX["armorClass"] = text;
        if (TryFirstInt(text, out var value)) content["ac"] = new JsonArray(value);
    }

    private static void MapHitDice(
        JsonObject content,
        JsonObject threeX,
        IReadOnlyDictionary<string, string> fields)
    {
        if (!TryField(fields, out var text, "hit dice", "hit die")) return;
        threeX["hitDice"] = text;
        var match = HitDice.Match(text);
        if (!match.Success
            || !int.TryParse(match.Groups["average"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var average))
        {
            return;
        }
        content["hp"] = new JsonObject
        {
            ["average"] = average,
            ["formula"] = Regex.Replace(match.Groups["formula"].Value, @"\s+", string.Empty)
        };
    }

    private static void MapMovement(
        JsonObject content,
        JsonObject threeX,
        IReadOnlyDictionary<string, string> fields)
    {
        if (!TryField(fields, out var text, "speed", "movement")) return;
        threeX["speed"] = text;
        var speed = new JsonObject();
        foreach (Match match in SpeedPart.Matches(text))
        {
            if (!int.TryParse(match.Groups["feet"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var feet))
            {
                continue;
            }
            speed[match.Groups["mode"].Success ? match.Groups["mode"].Value.ToLowerInvariant() : "walk"] = feet;
        }
        if (speed.Count > 0) content["speed"] = speed;
    }

    private static void MapAbilities(
        JsonObject content,
        JsonObject threeX,
        IReadOnlyDictionary<string, string> fields)
    {
        if (!TryField(fields, out var text, "abilities", "ability scores")) return;
        threeX["abilities"] = text;
        foreach (Match match in AbilityPair.Matches(text))
        {
            if (int.TryParse(match.Groups["score"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var score)
                && TryAbility(match.Groups["ability"].Value, out var ability))
            {
                content[ability] = score;
            }
        }
    }

    private static void MapSaves(JsonObject threeX, IReadOnlyDictionary<string, string> fields)
    {
        if (!TryField(fields, out var text, "saves", "saving throws")) return;
        threeX["savesText"] = text;
        var saves = new JsonObject();
        foreach (Match match in SavePair.Matches(text))
        {
            if (!int.TryParse(match.Groups["bonus"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bonus))
            {
                continue;
            }
            saves[match.Groups["save"].Value.ToLowerInvariant() switch
            {
                "fort" => "fortitude",
                "ref" => "reflex",
                _ => "will"
            }] = bonus;
        }
        if (saves.Count > 0) threeX["saves"] = saves;
    }

    private static void MapBaseAttackAndGrapple(JsonObject threeX, IReadOnlyDictionary<string, string> fields)
    {
        if (!TryField(fields, out var text, "base attack/grapple", "base attack / grapple")) return;
        threeX["baseAttackAndGrapple"] = text;
        var values = SignedNumber.Matches(text).Select(match => match.Groups["value"].Value).ToArray();
        if (values.Length > 0 && TryInt(values[0], out var bab)) threeX["baseAttackBonus"] = bab;
        if (values.Length > 1 && TryInt(values[1], out var grapple)) threeX["grapple"] = grapple;
    }

    private static IReadOnlyDictionary<string, string> ParseHtmlTable(string body, out string? sizeAndType)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        sizeAndType = null;
        foreach (Match table in HtmlTable.Matches(body))
        foreach (Match row in HtmlRow.Matches(table.Value))
        {
            var cells = HtmlCell.Matches(row.Groups["row"].Value)
                .Select(match => new Cell(
                    match.Groups["kind"].Value,
                    CleanCell(match.Groups["value"].Value)))
                .ToArray();
            for (var index = 0; index < cells.Length; index++)
            {
                if (!string.Equals(cells[index].Kind, "th", StringComparison.OrdinalIgnoreCase)) continue;
                var label = cells[index].Value.Trim().TrimEnd(':').Trim().ToLowerInvariant();
                var value = index + 1 < cells.Length ? cells[index + 1].Value : string.Empty;
                if (label.Length > 0 && value.Length > 0) fields[label] = value;
            }
            if (sizeAndType is null)
            {
                sizeAndType = cells
                    .Where(cell => !string.Equals(cell.Kind, "th", StringComparison.OrdinalIgnoreCase))
                    .Select(cell => cell.Value)
                    .FirstOrDefault(value => SizeAndType.IsMatch(value));
            }
        }
        return fields;
    }

    private static void MapSizeAndType(JsonObject content, JsonObject threeX, string text)
    {
        var match = SizeAndType.Match(text);
        if (!match.Success) return;
        if (TrySizeCode(match.Groups["size"].Value, out var size)) content["size"] = new JsonArray(size);

        var typeText = match.Groups["type"].Value.Trim();
        var opening = typeText.IndexOf('(');
        var closing = typeText.LastIndexOf(')');
        var type = (opening >= 0 ? typeText[..opening] : typeText).Trim();
        if (type.Length > 0)
        {
            threeX["creatureType"] = type;
            if (CommonCreatureTypes.Contains(type)) content["type"] = type.ToLowerInvariant();
        }
        if (opening >= 0 && closing > opening)
        {
            var subtypes = typeText[(opening + 1)..closing]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (subtypes.Length > 0)
            {
                threeX["creatureSubtypes"] = new JsonArray(subtypes.Select(JsonValue.Create).ToArray());
            }
        }
    }

    private static bool TryReadPcGenRaceSegments(string rawJson, out IReadOnlyList<Segment> segments)
    {
        segments = [];
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !TryString(document.RootElement, "entityType", out var entityType)
                || !string.Equals(entityType, "race", StringComparison.OrdinalIgnoreCase)
                || !TryProperty(document.RootElement, "segments", out var values)
                || values.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var result = new List<Segment>();
            var ordinal = 0;
            foreach (var value in values.EnumerateArray())
            {
                if (value.ValueKind == JsonValueKind.Object && TryString(value, "tag", out var tag))
                {
                    var index = TryNumber(value, "index", out var explicitIndex) ? explicitIndex : ordinal;
                    _ = TryString(value, "value", out var segmentValue);
                    result.Add(new Segment(index, tag.Trim(), segmentValue.Trim()));
                }
                ordinal++;
            }
            segments = result;
            return result.Count > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryRawBody(string rawJson, out string body)
    {
        body = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && TryString(document.RootElement, "body", out body)
                && !string.IsNullOrWhiteSpace(body);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string StripStatTables(string body)
    {
        var value = HtmlTable.Replace(body, string.Empty);
        value = HtmlBreak.Replace(value, "\n");
        value = HtmlBlockBoundary.Replace(value, "\n");
        value = WebUtility.HtmlDecode(HtmlTag.Replace(value, string.Empty))
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = value.Split('\n').Select(line => line.Trim()).ToArray();
        var result = new List<string>();
        var blank = true;
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                if (!blank) result.Add(string.Empty);
                blank = true;
            }
            else
            {
                result.Add(line);
                blank = false;
            }
        }
        while (result.Count > 0 && result[^1].Length == 0) result.RemoveAt(result.Count - 1);
        return string.Join("\n", result).Trim();
    }

    private static string CleanCell(string value)
    {
        value = HtmlBreak.Replace(value, " ");
        value = WebUtility.HtmlDecode(HtmlTag.Replace(value, string.Empty));
        return Whitespace.Replace(value, " ").Trim();
    }

    private static bool TryWalkSpeed(string value, out int walk, out bool completelyMapped)
    {
        walk = 0;
        var tokens = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        completelyMapped = tokens.Length == 2;
        for (var index = 0; index + 1 < tokens.Length; index += 2)
        {
            if (!int.TryParse(tokens[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var distance))
            {
                completelyMapped = false;
                continue;
            }
            if (string.Equals(tokens[index], "Walk", StringComparison.OrdinalIgnoreCase)) walk = distance;
        }
        return walk > 0;
    }

    private static bool TrySizeCode(string value, out string code)
    {
        code = value.Trim().ToLowerInvariant() switch
        {
            "f" or "fine" => "F", "d" or "diminutive" => "D", "t" or "tiny" => "T",
            "s" or "small" => "S", "m" or "medium" => "M", "l" or "large" => "L",
            "h" or "huge" => "H", "g" or "gargantuan" => "G", "c" or "colossal" => "C",
            _ => string.Empty
        };
        return code.Length > 0;
    }

    private static bool TryAbility(string value, out string ability)
    {
        ability = value.Trim().ToLowerInvariant() switch
        {
            "str" or "strength" => "str", "dex" or "dexterity" => "dex",
            "con" or "constitution" => "con", "int" or "intelligence" => "int",
            "wis" or "wisdom" => "wis", "cha" or "charisma" => "cha",
            _ => string.Empty
        };
        return ability.Length > 0;
    }

    private static bool TryField(IReadOnlyDictionary<string, string> fields, out string value, params string[] names)
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

    private static bool TryFirstInt(string value, out int result)
    {
        result = 0;
        var match = SignedNumber.Match(value);
        return match.Success && TryInt(match.Groups["value"].Value, out result);
    }

    private static bool TryInt(string value, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static IEnumerable<Segment> All(IReadOnlyList<Segment> segments, string tag) =>
        segments.Where(value => string.Equals(value.Tag, tag, StringComparison.OrdinalIgnoreCase));

    private static Segment? Last(IReadOnlyList<Segment> segments, string tag) =>
        segments.LastOrDefault(value => string.Equals(value.Tag, tag, StringComparison.OrdinalIgnoreCase));

    private static bool TryString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        if (!TryProperty(element, name, out var property) || property.ValueKind != JsonValueKind.String) return false;
        value = property.GetString() ?? string.Empty;
        return value.Length > 0;
    }

    private static bool TryNumber(JsonElement element, string name, out int value)
    {
        value = 0;
        return TryProperty(element, name, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value);
    }

    private static bool TryProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value)) return true;
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static Regex Rx(string pattern, RegexOptions extra = RegexOptions.None) =>
        new(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | extra);

    private sealed record Segment(int Index, string Tag, string Value);
    private sealed record Cell(string Kind, string Value);
}
