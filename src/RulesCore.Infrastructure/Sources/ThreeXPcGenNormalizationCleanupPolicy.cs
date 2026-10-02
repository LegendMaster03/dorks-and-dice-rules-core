using System.Text.Json;
using System.Text.Json.Nodes;
using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Removes PCGen source syntax from the rule-bearing unmapped segment list once an exact semantic
/// projection has been persisted elsewhere in the normalized document. RawJson remains the
/// immutable source record, so this cleanup loses no provenance while allowing cross-format
/// semantic comparison to use the translated mechanics rather than duplicated source notation.
/// </summary>
internal static class ThreeXPcGenNormalizationCleanupPolicy
{
    public static NormalizedSourceRecord Apply(
        NormalizedSourceRepresentation representation,
        NormalizedSourceRecord record)
    {
        ArgumentNullException.ThrowIfNull(representation);
        ArgumentNullException.ThrowIfNull(record);

        if (!string.Equals(
                representation.FormatKey,
                PcGenSourceFormatAdapter.Format,
                StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(record.ContentJson))
        {
            return record;
        }

        var content = JsonNode.Parse(record.ContentJson) as JsonObject;
        if (content?["_rulesCore"] is not JsonObject extension
            || extension["pcgen"] is not JsonObject pcgen
            || pcgen["unmappedSegments"] is not JsonArray unmapped)
        {
            return record;
        }

        var entityType = record.EntityType.Trim().ToLowerInvariant();
        var retained = new JsonArray();
        var changed = false;
        foreach (var node in unmapped)
        {
            if (node is not JsonObject segment
                || !TryString(segment, "tag", out var tag)
                || !TryString(segment, "value", out var value)
                || !IsTranslated(entityType, tag, value))
            {
                retained.Add(node?.DeepClone());
                continue;
            }
            changed = true;
        }

        if (!changed)
        {
            return record;
        }

        if (retained.Count == 0)
        {
            pcgen.Remove("unmappedSegments");
        }
        else
        {
            pcgen["unmappedSegments"] = retained;
        }
        if (pcgen.Count == 0)
        {
            extension.Remove("pcgen");
        }
        content["_rulesCore"] = extension;
        return record with
        {
            ContentJson = content.ToJsonString(new JsonSerializerOptions { WriteIndented = false })
        };
    }

    private static bool IsTranslated(string entityType, string tag, string value)
    {
        var normalizedTag = tag.Trim().ToUpperInvariant();
        return entityType switch
        {
            "race" or "species" => IsTranslatedRace(normalizedTag, value),
            "spell" => normalizedTag is
                "COMPS" or "COMPONENTS" or "CASTTIME" or "CASTINGTIME" or "RANGE"
                or "TARGETAREA" or "DURATION" or "SAVEINFO" or "SPELLRES" or "DESCRIPTOR"
                or "SUBSCHOOL",
            "item" or "equipment" => normalizedTag is
                "TYPE" or "SIZE" or "SLOTS" or "WIELD" or "CRITRANGE" or "CRITMULT" or "DAMAGE",
            "feat" => normalizedTag == "TYPE",
            "deity" => IsTranslatedDeity(normalizedTag, value),
            "language" => normalizedTag is "TYPE" or "SCRIPT",
            "domain" => normalizedTag is "SPELLS" or "CSKILL",
            "template" => normalizedTag is "CR" or "LEVELADJUSTMENT" or "RACETYPE" or "TYPE" or "SIZE",
            "weapon-proficiency" or "armor-proficiency" or "shield-proficiency" => normalizedTag == "TYPE",
            _ => false
        };
    }

    private static bool IsTranslatedRace(string tag, string value)
    {
        if (tag is
            "VISION" or "FAVCLASS" or "RACETYPE" or "RACESUBTYPE" or "LANGBONUS" or "CR"
            or "LEVELADJUSTMENT" or "MONSTERCLASS" or "HITDICEADVANCEMENT"
            or "FACE" or "REACH" or "DR" or "SR" or "NATURALATTACKS" or "TYPE"
            or "UNENCUMBEREDMOVE")
        {
            return true;
        }
        if (tag is "STARTFEATS" or "LEGS" or "HANDS" or "XTRASKILLPTSPERLVL")
        {
            return int.TryParse(value, out _);
        }
        if (tag == "DEFINESTAT")
        {
            var parts = value.Split('|', StringSplitOptions.TrimEntries);
            return parts.Length >= 3
                && string.Equals(parts[0], "MINVALUE", StringComparison.OrdinalIgnoreCase)
                && IsAbility(parts[1])
                && int.TryParse(parts[2], out _);
        }
        if (tag == "TEMPLATE")
        {
            return value.Trim().StartsWith("CHOOSE:", StringComparison.OrdinalIgnoreCase);
        }
        if (tag == "AUTO")
        {
            var first = value.Split('|', 2, StringSplitOptions.TrimEntries)[0];
            return string.Equals(first, "LANG", StringComparison.OrdinalIgnoreCase);
        }
        if (tag == "BONUS")
        {
            var parts = value.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length >= 3
                && string.Equals(parts[0], "VAR", StringComparison.OrdinalIgnoreCase)
                && string.Equals(parts[1], "DarkvisionRange", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(parts[2], out _))
            {
                return true;
            }
            if (parts.Length >= 4
                && string.Equals(parts[0], "COMBAT", StringComparison.OrdinalIgnoreCase)
                && string.Equals(parts[1], "AC", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(parts[2], out _)
                && parts.Skip(3).Any(item =>
                    string.Equals(item, "TYPE=NaturalArmor", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
            return parts.Length >= 3
                && string.Equals(parts[0], "SAVE", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private static bool IsTranslatedDeity(string tag, string value)
    {
        if (tag is "ALIGN" or "DOMAINS" or "DEITYWEAP" or "PANTHEON")
        {
            return true;
        }
        if (tag == "FACT")
        {
            var name = value.Split('|', 2, StringSplitOptions.TrimEntries)[0];
            return string.Equals(name, "Title", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Symbol", StringComparison.OrdinalIgnoreCase);
        }
        if (tag == "FACTSET")
        {
            var name = value.Split('|', 2, StringSplitOptions.TrimEntries)[0];
            return string.Equals(name, "Pantheon", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private static bool IsAbility(string value) =>
        value.Trim().ToLowerInvariant() is
            "str" or "strength" or "dex" or "dexterity" or "con" or "constitution"
            or "int" or "intelligence" or "wis" or "wisdom" or "cha" or "charisma";

    private static bool TryString(JsonObject value, string name, out string result)
    {
        result = string.Empty;
        if (value[name] is not JsonValue property
            || !property.TryGetValue<string>(out var candidate)
            || string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }
        result = candidate;
        return true;
    }
}
