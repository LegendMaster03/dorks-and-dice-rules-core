using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Translates source-native records into the Rules Core mechanical content contract.
/// The contract uses native 5e.tools entity shapes as its reference model and adds
/// explicit Rules Core extensions when source mechanics do not have a faithful 5e.tools field.
/// </summary>
internal static class RulesCoreContentTranslation
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

    private static readonly IReadOnlyDictionary<string, string> SpellSchoolCodes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Abjuration"] = "A",
            ["Conjuration"] = "C",
            ["Divination"] = "D",
            ["Enchantment"] = "E",
            ["Evocation"] = "V",
            ["Illusion"] = "I",
            ["Necromancy"] = "N",
            ["Transmutation"] = "T"
        };

    private static readonly HashSet<string> MonsterTypes = new(StringComparer.OrdinalIgnoreCase)
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

    public static string? Translate(
        NormalizedSourceRepresentation representation,
        NormalizedSourceRecord record)
    {
        ArgumentNullException.ThrowIfNull(representation);
        ArgumentNullException.ThrowIfNull(record);

        if (!string.IsNullOrWhiteSpace(record.ContentJson))
        {
            return record.ContentJson;
        }

        if (string.Equals(representation.FormatKey, FiveEToolsSourceFormatAdapter.Format, StringComparison.OrdinalIgnoreCase))
        {
            // Native 5e.tools content is already the reference mechanical representation.
            // Do not deserialize into a narrower DTO; unknown/future upstream fields must survive.
            return record.RawJson;
        }

        if (!string.Equals(representation.FormatKey, PcGenSourceFormatAdapter.Format, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return TranslatePcGen(representation, record);
    }

    private static string? TranslatePcGen(
        NormalizedSourceRepresentation representation,
        NormalizedSourceRecord record)
    {
        if (record.EntityType.StartsWith("pcgen-", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        using var native = JsonDocument.Parse(record.RawJson);
        if (native.RootElement.ValueKind != JsonValueKind.Object
            || !native.RootElement.TryGetProperty("kind", out var kind)
            || !string.Equals(kind.GetString(), "record", StringComparison.OrdinalIgnoreCase)
            || !native.RootElement.TryGetProperty("segments", out var segmentArray)
            || segmentArray.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var segments = segmentArray.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.Object)
            .Select((value, ordinal) => ReadSegment(value, ordinal))
            .Where(value => value is not null)
            .Cast<PcGenSegment>()
            .ToArray();

        var mapped = new HashSet<int>();
        var content = new JsonObject
        {
            ["name"] = record.Name
        };
        if (!string.IsNullOrWhiteSpace(record.SourceCode))
        {
            content["source"] = record.SourceCode;
        }

        MapPage(content, segments, mapped);
        MapDescriptions(content, segments, mapped);

        switch (record.EntityType.ToLowerInvariant())
        {
            case "spell":
                MapSpell(content, segments, mapped);
                break;
            case "feat":
                MapFeat(content, segments, mapped);
                break;
            case "item":
            case "equipment":
                MapItem(content, segments, mapped);
                break;
            case "race":
            case "species":
                MapRace(content, segments, mapped);
                break;
            case "monster":
            case "creature":
                MapMonster(content, segments, mapped);
                break;
        }

        AddRulesCoreExtensions(
            content,
            ResolveEdition(representation, record),
            segments,
            mapped);

        return content.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static PcGenSegment? ReadSegment(JsonElement value, int ordinal)
    {
        var tag = ReadString(value, "Tag");
        if (tag is null)
        {
            return null;
        }

        var index = value.TryGetProperty("Index", out var indexValue)
            && indexValue.ValueKind == JsonValueKind.Number
            && indexValue.TryGetInt32(out var explicitIndex)
                ? explicitIndex
                : ordinal;
        return new PcGenSegment(
            index,
            tag.Trim(),
            ReadString(value, "Value")?.Trim() ?? string.Empty,
            ReadString(value, "Raw")?.Trim() ?? string.Empty);
    }

    private static void MapPage(JsonObject content, IReadOnlyList<PcGenSegment> segments, ISet<int> mapped)
    {
        var page = Last(segments, "SOURCEPAGE");
        if (page is null)
        {
            return;
        }

        var normalized = page.Value.Trim();
        if (normalized.StartsWith("p.", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..].Trim();
        }
        else if (normalized.StartsWith("page", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[4..].Trim().TrimStart(':').Trim();
        }

        if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            content["page"] = number;
        }
        else if (!string.IsNullOrWhiteSpace(normalized))
        {
            content["page"] = normalized;
        }
        mapped.Add(page.Index);
    }

    private static void MapDescriptions(JsonObject content, IReadOnlyList<PcGenSegment> segments, ISet<int> mapped)
    {
        var descriptions = All(segments, "DESC")
            .Where(value => !string.IsNullOrWhiteSpace(value.Value))
            .ToArray();
        if (descriptions.Length == 0)
        {
            return;
        }

        var entries = new JsonArray();
        foreach (var description in descriptions)
        {
            entries.Add(description.Value);
            mapped.Add(description.Index);
        }
        content["entries"] = entries;
    }

    private static void MapSpell(JsonObject content, IReadOnlyList<PcGenSegment> segments, ISet<int> mapped)
    {
        var level = Last(segments, "LEVEL");
        if (level is not null
            && int.TryParse(level.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var levelNumber))
        {
            content["level"] = levelNumber;
            mapped.Add(level.Index);
        }

        var school = Last(segments, "SCHOOL");
        if (school is not null && SpellSchoolCodes.TryGetValue(school.Value, out var code))
        {
            content["school"] = code;
            mapped.Add(school.Index);
        }

        var components = Last(segments, "COMPS") ?? Last(segments, "COMPONENTS");
        if (components is not null)
        {
            var componentObject = new JsonObject();
            var tokens = components.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                if (string.Equals(token, "V", StringComparison.OrdinalIgnoreCase)) componentObject["v"] = true;
                else if (string.Equals(token, "S", StringComparison.OrdinalIgnoreCase)) componentObject["s"] = true;
            }
            if (componentObject.Count > 0)
            {
                content["components"] = componentObject;
                mapped.Add(components.Index);
            }
        }

        var duration = Last(segments, "DURATION");
        if (duration is not null && string.Equals(duration.Value.Trim(), "Instantaneous", StringComparison.OrdinalIgnoreCase))
        {
            content["duration"] = new JsonArray(new JsonObject { ["type"] = "instant" });
            mapped.Add(duration.Index);
        }
    }

    private static void MapFeat(JsonObject content, IReadOnlyList<PcGenSegment> segments, ISet<int> mapped)
    {
        var multiple = Last(segments, "MULT");
        if (multiple is not null && TryParseBoolean(multiple.Value, out var repeatable))
        {
            content["repeatable"] = repeatable;
            mapped.Add(multiple.Index);
        }
    }

    private static void MapItem(JsonObject content, IReadOnlyList<PcGenSegment> segments, ISet<int> mapped)
    {
        var weight = Last(segments, "WT") ?? Last(segments, "WEIGHT");
        if (weight is not null
            && decimal.TryParse(weight.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var weightNumber))
        {
            content["weight"] = weightNumber;
            mapped.Add(weight.Index);
        }

        var cost = Last(segments, "COST");
        if (cost is not null && TryParseCopperValue(cost.Value, out var copperValue))
        {
            content["value"] = copperValue;
            mapped.Add(cost.Index);
        }
    }

    private static void MapRace(JsonObject content, IReadOnlyList<PcGenSegment> segments, ISet<int> mapped)
    {
        MapSize(content, segments, mapped);

        var move = Last(segments, "MOVE") ?? Last(segments, "SPEED");
        if (move is not null && TryParseLeadingNumber(move.Value, out var speed))
        {
            content["speed"] = speed;
            mapped.Add(move.Index);
        }

        var abilityBonuses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var bonus in All(segments, "BONUS"))
        {
            var parts = bonus.Value.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length < 3
                || !string.Equals(parts[0], "STAT", StringComparison.OrdinalIgnoreCase)
                || !IsAbility(parts[1])
                || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
            {
                continue;
            }
            var key = parts[1].ToLowerInvariant();
            abilityBonuses[key] = abilityBonuses.GetValueOrDefault(key) + amount;
            mapped.Add(bonus.Index);
        }

        if (abilityBonuses.Count > 0)
        {
            var ability = new JsonObject();
            foreach (var bonus in abilityBonuses.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                ability[bonus.Key] = bonus.Value;
            }
            content["ability"] = new JsonArray(ability);
        }
    }

    private static void MapMonster(JsonObject content, IReadOnlyList<PcGenSegment> segments, ISet<int> mapped)
    {
        MapSize(content, segments, mapped);

        var type = Last(segments, "TYPE");
        if (type is not null)
        {
            var normalized = type.Value.Trim().ToLowerInvariant();
            if (MonsterTypes.Contains(normalized))
            {
                content["type"] = normalized;
                mapped.Add(type.Index);
            }
        }

        foreach (var ability in new[] { "STR", "DEX", "CON", "INT", "WIS", "CHA" })
        {
            var segment = Last(segments, ability);
            if (segment is null
                || !int.TryParse(segment.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var score))
            {
                continue;
            }
            content[ability.ToLowerInvariant()] = score;
            mapped.Add(segment.Index);
        }

        var armorClass = Last(segments, "AC");
        if (armorClass is not null && TryParseLeadingNumber(armorClass.Value, out var ac))
        {
            content["ac"] = new JsonArray(ac);
            mapped.Add(armorClass.Index);
        }

        var hpValue = Last(segments, "HP");
        var hitDice = Last(segments, "HD");
        var hp = new JsonObject();
        if (hpValue is not null && TryParseLeadingNumber(hpValue.Value, out var averageHp))
        {
            hp["average"] = averageHp;
            mapped.Add(hpValue.Index);
        }
        if (hitDice is not null && LooksLikeDiceFormula(hitDice.Value))
        {
            hp["formula"] = hitDice.Value.Trim();
            mapped.Add(hitDice.Index);
        }
        if (hp.Count > 0)
        {
            content["hp"] = hp;
        }

        var move = Last(segments, "MOVE") ?? Last(segments, "SPEED");
        if (move is not null && TryParseLeadingNumber(move.Value, out var speed))
        {
            content["speed"] = new JsonObject { ["walk"] = speed };
            mapped.Add(move.Index);
        }

        var challengeRating = Last(segments, "CR");
        if (challengeRating is not null && !string.IsNullOrWhiteSpace(challengeRating.Value))
        {
            content["cr"] = challengeRating.Value.Trim();
            mapped.Add(challengeRating.Index);
        }
    }

    private static void MapSize(JsonObject content, IReadOnlyList<PcGenSegment> segments, ISet<int> mapped)
    {
        var size = Last(segments, "SIZE");
        if (size is null)
        {
            return;
        }

        var normalized = size.Value.Trim().ToUpperInvariant();
        if (normalized is "T" or "S" or "M" or "L" or "H" or "G")
        {
            content["size"] = new JsonArray(normalized);
            mapped.Add(size.Index);
        }
    }

    private static void AddRulesCoreExtensions(
        JsonObject content,
        string? edition,
        IReadOnlyList<PcGenSegment> segments,
        ISet<int> mapped)
    {
        var unmapped = segments
            .Where(value => !mapped.Contains(value.Index))
            .Where(value => !SourceOnlyPcGenTags.Contains(value.Tag))
            .OrderBy(value => value.Tag, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Value, StringComparer.Ordinal)
            .ThenBy(value => value.Raw, StringComparer.Ordinal)
            .ToArray();

        if (string.IsNullOrWhiteSpace(edition) && unmapped.Length == 0)
        {
            return;
        }

        var extension = new JsonObject();
        if (!string.IsNullOrWhiteSpace(edition))
        {
            extension["edition"] = edition;
        }

        if (unmapped.Length > 0)
        {
            var segmentsJson = new JsonArray();
            foreach (var segment in unmapped)
            {
                segmentsJson.Add(new JsonObject
                {
                    ["tag"] = segment.Tag,
                    ["value"] = segment.Value
                });
            }
            extension["pcgen"] = new JsonObject
            {
                ["unmappedSegments"] = segmentsJson
            };
        }

        content["_rulesCore"] = extension;
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

    private static PcGenSegment? Last(IReadOnlyList<PcGenSegment> segments, string tag) =>
        segments.LastOrDefault(value => string.Equals(value.Tag, tag, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<PcGenSegment> All(IReadOnlyList<PcGenSegment> segments, string tag) =>
        segments.Where(value => string.Equals(value.Tag, tag, StringComparison.OrdinalIgnoreCase));

    private static string? ReadString(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool TryParseBoolean(string value, out bool result)
    {
        if (string.Equals(value.Trim(), "YES", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value.Trim(), "TRUE", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }
        if (string.Equals(value.Trim(), "NO", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value.Trim(), "FALSE", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }
        result = false;
        return false;
    }

    private static bool TryParseCopperValue(string value, out int copperValue)
    {
        copperValue = 0;
        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0
            || !decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return false;
        }

        var multiplier = parts.Length == 1
            ? 1m
            : parts[1].ToLowerInvariant() switch
            {
                "cp" => 1m,
                "sp" => 10m,
                "gp" => 100m,
                "pp" => 1000m,
                _ => 0m
            };
        if (multiplier == 0m)
        {
            return false;
        }

        var copper = amount * multiplier;
        if (copper < 0m || copper > int.MaxValue || copper != decimal.Truncate(copper))
        {
            return false;
        }
        copperValue = decimal.ToInt32(copper);
        return true;
    }

    private static bool TryParseLeadingNumber(string value, out int number)
    {
        number = 0;
        var token = new string(value.Trim().TakeWhile(character => char.IsDigit(character) || character == '-' || character == '+').ToArray());
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
    }

    private static bool LooksLikeDiceFormula(string value)
    {
        var normalized = value.Trim();
        var d = normalized.IndexOf('d', StringComparison.OrdinalIgnoreCase);
        return d > 0
            && normalized[..d].All(char.IsDigit)
            && normalized[(d + 1)..].TakeWhile(char.IsDigit).Any();
    }

    private static bool IsAbility(string value) =>
        value.Equals("STR", StringComparison.OrdinalIgnoreCase)
        || value.Equals("DEX", StringComparison.OrdinalIgnoreCase)
        || value.Equals("CON", StringComparison.OrdinalIgnoreCase)
        || value.Equals("INT", StringComparison.OrdinalIgnoreCase)
        || value.Equals("WIS", StringComparison.OrdinalIgnoreCase)
        || value.Equals("CHA", StringComparison.OrdinalIgnoreCase);

    private sealed record PcGenSegment(int Index, string Tag, string Value, string Raw);
}
