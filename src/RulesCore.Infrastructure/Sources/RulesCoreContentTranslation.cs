using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Translates source-native records into the Rules Core mechanical content contract.
/// Native 5e.tools entity objects are the reference shapes. Other formats map only
/// mechanically faithful fields and retain source-specific mechanics under _rulesCore.
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

    private static readonly HashSet<string> FiveEMonsterTypes = new(StringComparer.OrdinalIgnoreCase)
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

    public static NormalizedSourceRecord TranslateRecord(
        NormalizedSourceRepresentation representation,
        NormalizedSourceRecord record)
    {
        ArgumentNullException.ThrowIfNull(representation);
        ArgumentNullException.ThrowIfNull(record);

        if (!string.IsNullOrWhiteSpace(record.ContentJson))
        {
            return record;
        }

        if (string.Equals(
                representation.FormatKey,
                FiveEToolsSourceFormatAdapter.Format,
                StringComparison.OrdinalIgnoreCase))
        {
            // Native 5e.tools content already is the reference mechanical representation.
            // Never deserialize it into a narrower DTO; unknown/future fields must survive.
            return record with { ContentJson = record.RawJson };
        }

        if (!string.Equals(
                representation.FormatKey,
                PcGenSourceFormatAdapter.Format,
                StringComparison.OrdinalIgnoreCase)
            || record.EntityType.StartsWith("pcgen-", StringComparison.OrdinalIgnoreCase))
        {
            return record;
        }

        var native = TryReadPcGenRecord(record.RawJson);
        if (native is null)
        {
            return record;
        }

        var entityType = NormalizePcGenEntityType(record.EntityType, native.Path, native.Segments);
        var normalizedRecord = string.Equals(entityType, record.EntityType, StringComparison.Ordinal)
            ? record
            : record with { EntityType = entityType };
        var contentJson = TranslatePcGen(
            representation,
            normalizedRecord,
            record.EntityType,
            native.Segments);
        return normalizedRecord with { ContentJson = contentJson };
    }

    private static PcGenNativeRecord? TryReadPcGenRecord(string rawJson)
    {
        using var native = JsonDocument.Parse(rawJson);
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
        var path = ReadString(native.RootElement, "path") ?? string.Empty;
        return new PcGenNativeRecord(path, segments);
    }

    private static string NormalizePcGenEntityType(
        string entityType,
        string path,
        IReadOnlyList<PcGenSegment> segments)
    {
        if (string.Equals(entityType, "ability", StringComparison.OrdinalIgnoreCase)
            && All(segments, "CATEGORY").Any(value =>
                string.Equals(value.Value, "FEAT", StringComparison.OrdinalIgnoreCase)))
        {
            return "feat";
        }

        if (string.Equals(entityType, "race", StringComparison.OrdinalIgnoreCase)
            && (All(segments, "MONSTERCLASS").Any() || IsMonsterSourcePath(path)))
        {
            return "monster";
        }

        return entityType;
    }

    private static bool IsMonsterSourcePath(string path)
    {
        var segments = path.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(value =>
            value.Equals("monster", StringComparison.OrdinalIgnoreCase)
            || value.Equals("monsters", StringComparison.OrdinalIgnoreCase)
            || value.Contains("monster_manual", StringComparison.OrdinalIgnoreCase)
            || value.Contains("monsters_", StringComparison.OrdinalIgnoreCase)
            || value.Contains("_monsters", StringComparison.OrdinalIgnoreCase));
    }

    private static string TranslatePcGen(
        NormalizedSourceRepresentation representation,
        NormalizedSourceRecord record,
        string nativeEntityType,
        IReadOnlyList<PcGenSegment> segments)
    {
        var mapped = new HashSet<int>();
        var edition = ResolveEdition(representation, record);
        var competencyConversion = string.Equals(nativeEntityType, "skill", StringComparison.OrdinalIgnoreCase)
            ? PcGenCompetencyConversions.Resolve(record.Name, edition)
            : null;
        var mechanicalName = competencyConversion is not null && !competencyConversion.PreserveSourceMechanicalName
            ? competencyConversion.TargetName
            : record.Name;
        var content = new JsonObject
        {
            ["name"] = mechanicalName
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
            edition,
            nativeEntityType,
            record.EntityType,
            record.Name,
            competencyConversion,
            segments,
            mapped);

        return content.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static PcGenSegment? ReadSegment(JsonElement value, int ordinal)
    {
        var tag = ReadString(value, "Tag") ?? ReadString(value, "tag");
        if (tag is null)
        {
            return null;
        }

        var index = TryReadInt32(value, "Index", out var explicitIndex)
            ? explicitIndex
            : TryReadInt32(value, "index", out explicitIndex)
                ? explicitIndex
                : ordinal;
        return new PcGenSegment(
            index,
            tag.Trim(),
            (ReadString(value, "Value") ?? ReadString(value, "value") ?? string.Empty).Trim(),
            (ReadString(value, "Raw") ?? ReadString(value, "raw") ?? string.Empty).Trim());
    }

    private static void MapPage(
        JsonObject content,
        IReadOnlyList<PcGenSegment> segments,
        ISet<int> mapped)
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

    private static void MapDescriptions(
        JsonObject content,
        IReadOnlyList<PcGenSegment> segments,
        ISet<int> mapped)
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
            // PCGen descriptions can contain conditional suffixes separated by pipes.
            // Preserve those source expressions under _rulesCore even though the full
            // string is also retained as readable entry text.
            if (!description.Value.Contains('|'))
            {
                mapped.Add(description.Index);
            }
        }
        content["entries"] = entries;
    }

    private static void MapSpell(
        JsonObject content,
        IReadOnlyList<PcGenSegment> segments,
        ISet<int> mapped)
    {
        if (TryGetUniqueSpellLevel(segments, out var level))
        {
            content["level"] = level;
            // CLASSES/DOMAINS remain unmapped because they also encode class/domain access.
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
            var tokens = components.Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => value.Trim())
                .ToArray();
            var componentObject = new JsonObject();
            if (tokens.Any(value => string.Equals(value, "V", StringComparison.OrdinalIgnoreCase)))
            {
                componentObject["v"] = true;
            }
            if (tokens.Any(value => string.Equals(value, "S", StringComparison.OrdinalIgnoreCase)))
            {
                componentObject["s"] = true;
            }
            if (componentObject.Count > 0)
            {
                content["components"] = componentObject;
            }
            if (tokens.All(value =>
                    string.Equals(value, "V", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "S", StringComparison.OrdinalIgnoreCase)))
            {
                mapped.Add(components.Index);
            }
        }

        var duration = Last(segments, "DURATION");
        if (duration is not null
            && string.Equals(duration.Value.Trim(), "Instantaneous", StringComparison.OrdinalIgnoreCase))
        {
            content["duration"] = new JsonArray(new JsonObject { ["type"] = "instant" });
            mapped.Add(duration.Index);
        }
    }

    private static bool TryGetUniqueSpellLevel(
        IReadOnlyList<PcGenSegment> segments,
        out int level)
    {
        var levels = new HashSet<int>();
        foreach (var segment in All(segments, "CLASSES").Concat(All(segments, "DOMAINS")))
        {
            foreach (var assignment in segment.Value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var equals = assignment.LastIndexOf('=');
                if (equals < 0 || equals + 1 >= assignment.Length)
                {
                    continue;
                }
                if (int.TryParse(
                        assignment[(equals + 1)..].Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var candidate))
                {
                    levels.Add(candidate);
                }
            }
        }

        if (levels.Count == 1)
        {
            level = levels.Single();
            return true;
        }

        level = 0;
        return false;
    }

    private static void MapFeat(
        JsonObject content,
        IReadOnlyList<PcGenSegment> segments,
        ISet<int> mapped)
    {
        foreach (var category in All(segments, "CATEGORY").Where(value =>
                     string.Equals(value.Value, "FEAT", StringComparison.OrdinalIgnoreCase)))
        {
            mapped.Add(category.Index);
        }

        var multiple = Last(segments, "MULT");
        if (multiple is not null && TryParseBoolean(multiple.Value, out var repeatable))
        {
            content["repeatable"] = repeatable;
            mapped.Add(multiple.Index);
        }
    }

    private static void MapItem(
        JsonObject content,
        IReadOnlyList<PcGenSegment> segments,
        ISet<int> mapped)
    {
        var weight = Last(segments, "WT") ?? Last(segments, "WEIGHT");
        if (weight is not null
            && decimal.TryParse(weight.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var weightNumber))
        {
            content["weight"] = weightNumber;
            mapped.Add(weight.Index);
        }

        var cost = Last(segments, "COST");
        if (cost is not null && TryParsePcGenCostAsCopper(cost.Value, out var copperValue))
        {
            content["value"] = copperValue;
            mapped.Add(cost.Index);
        }
    }

    private static void MapRace(
        JsonObject content,
        IReadOnlyList<PcGenSegment> segments,
        ISet<int> mapped)
    {
        MapSize(content, segments, mapped);
        MapRaceWalkSpeed(content, segments, mapped);

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

    private static void MapMonster(
        JsonObject content,
        IReadOnlyList<PcGenSegment> segments,
        ISet<int> mapped)
    {
        MapSize(content, segments, mapped);

        var type = Last(segments, "RACETYPE") ?? Last(segments, "TYPE");
        if (type is not null)
        {
            var normalized = type.Value.Trim().ToLowerInvariant();
            if (FiveEMonsterTypes.Contains(normalized))
            {
                content["type"] = normalized;
                mapped.Add(type.Index);
            }
        }

        var movement = Last(segments, "MOVE") ?? Last(segments, "SPEED");
        if (movement is not null && TryParseMonsterSpeed(movement.Value, out var speed, out var complete))
        {
            content["speed"] = speed;
            if (complete)
            {
                mapped.Add(movement.Index);
            }
        }

        var challengeRating = Last(segments, "CR");
        if (challengeRating is not null && !string.IsNullOrWhiteSpace(challengeRating.Value))
        {
            content["cr"] = challengeRating.Value.Trim();
            mapped.Add(challengeRating.Index);
        }

        // PCGen 3.x monster race records normally contain racial ability bonuses,
        // natural-armor bonuses, and MONSTERCLASS racial-HD declarations rather than
        // final ability scores, total AC, or a ready-made HP formula. Those values are
        // deliberately left in _rulesCore instead of being misrepresented as 5e fields.
    }

    private static void MapSize(
        JsonObject content,
        IReadOnlyList<PcGenSegment> segments,
        ISet<int> mapped)
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

    private static void MapRaceWalkSpeed(
        JsonObject content,
        IReadOnlyList<PcGenSegment> segments,
        ISet<int> mapped)
    {
        var movement = Last(segments, "MOVE") ?? Last(segments, "SPEED");
        if (movement is null)
        {
            return;
        }

        var tokens = movement.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 2)
        {
            return;
        }

        int? walk = null;
        var complete = tokens.Length % 2 == 0;
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

        if (walk.HasValue)
        {
            content["speed"] = walk.Value;
        }
        if (complete && tokens.Length == 2 && walk.HasValue)
        {
            mapped.Add(movement.Index);
        }
    }

    private static bool TryParseMonsterSpeed(
        string value,
        out JsonObject speed,
        out bool complete)
    {
        speed = new JsonObject();
        var tokens = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        complete = tokens.Length > 0 && tokens.Length % 2 == 0;
        for (var index = 0; index + 1 < tokens.Length; index += 2)
        {
            if (!int.TryParse(tokens[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var distance))
            {
                complete = false;
                continue;
            }

            var key = tokens[index].Trim().ToLowerInvariant() switch
            {
                "walk" => "walk",
                "fly" => "fly",
                "swim" => "swim",
                "climb" => "climb",
                "burrow" => "burrow",
                _ => null
            };
            if (key is null)
            {
                complete = false;
                continue;
            }
            speed[key] = distance;
        }
        return speed.Count > 0;
    }

    private static void AddRulesCoreExtensions(
        JsonObject content,
        string? edition,
        string nativeEntityType,
        string normalizedEntityType,
        string nativeName,
        PcGenCompetencyConversion? competencyConversion,
        IReadOnlyList<PcGenSegment> segments,
        ISet<int> mapped)
    {
        var unmapped = segments
            .Where(value => !mapped.Contains(value.Index))
            .Where(value => !SourceOnlyPcGenTags.Contains(value.Tag))
            .OrderBy(value => value.Index)
            .ToArray();

        var context = new JsonObject
        {
            ["sourceFormat"] = PcGenSourceFormatAdapter.Format,
            ["nativeEntityType"] = nativeEntityType
        };
        if (!string.IsNullOrWhiteSpace(edition))
        {
            context["edition"] = edition;
        }
        if (!string.Equals(nativeEntityType, normalizedEntityType, StringComparison.OrdinalIgnoreCase))
        {
            context["translatedEntityType"] = normalizedEntityType;
        }
        if (competencyConversion is not null)
        {
            context["nativeName"] = nativeName;
        }

        var extension = new JsonObject
        {
            ["context"] = context
        };
        if (competencyConversion is not null)
        {
            var conversion = new JsonObject
            {
                ["relationship"] = competencyConversion.Relationship,
                ["sourceType"] = "skill",
                ["sourceName"] = competencyConversion.SourceName,
                ["targetType"] = competencyConversion.TargetType,
                ["targetName"] = competencyConversion.TargetName
            };
            if (!string.IsNullOrWhiteSpace(competencyConversion.Scope))
            {
                conversion["scope"] = competencyConversion.Scope;
            }
            if (competencyConversion.PreserveSourceMechanicalName)
            {
                conversion["mechanicalNamePreserved"] = true;
            }
            extension["competencyConversion"] = conversion;
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

    private static bool TryParsePcGenCostAsCopper(string value, out int copperValue)
    {
        copperValue = 0;
        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0
            || !decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return false;
        }

        // PCGen's EQUIPMENT COST tag is denominated in gold pieces when no unit is present.
        var multiplier = parts.Length == 1
            ? 100m
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

    private static bool IsAbility(string value) =>
        value.Equals("STR", StringComparison.OrdinalIgnoreCase)
        || value.Equals("DEX", StringComparison.OrdinalIgnoreCase)
        || value.Equals("CON", StringComparison.OrdinalIgnoreCase)
        || value.Equals("INT", StringComparison.OrdinalIgnoreCase)
        || value.Equals("WIS", StringComparison.OrdinalIgnoreCase)
        || value.Equals("CHA", StringComparison.OrdinalIgnoreCase);

    private static PcGenSegment? Last(IReadOnlyList<PcGenSegment> segments, string tag) =>
        segments.LastOrDefault(value => string.Equals(value.Tag, tag, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<PcGenSegment> All(IReadOnlyList<PcGenSegment> segments, string tag) =>
        segments.Where(value => string.Equals(value.Tag, tag, StringComparison.OrdinalIgnoreCase));

    private static string? ReadString(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool TryReadInt32(JsonElement value, string propertyName, out int result)
    {
        result = 0;
        return value.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out result);
    }

    private sealed record PcGenNativeRecord(string Path, IReadOnlyList<PcGenSegment> Segments);

    private sealed record PcGenSegment(int Index, string Tag, string Value, string Raw);
}
