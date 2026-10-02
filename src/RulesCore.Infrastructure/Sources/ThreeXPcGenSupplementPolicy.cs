using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Completes mechanically straightforward PCGen projections that are intentionally more
/// conservative than the primary family translator. Final-value fields are promoted only when
/// the native tag has the same meaning in the shared content contract; chassis/source mechanics
/// remain in _rulesCore.threeX.
/// </summary>
internal static class ThreeXPcGenSupplementPolicy
{
    private static readonly Regex DarkvisionDistance = new(
        @"Darkvision\s*\(?\s*(?<feet>\d+)\s*(?:'|ft\.?)?\s*\)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

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
            || string.IsNullOrWhiteSpace(record.ContentJson)
            || !TryReadSegments(record.RawJson, out var segments))
        {
            return record;
        }

        var content = JsonNode.Parse(record.ContentJson) as JsonObject;
        if (content is null) return record;
        var extension = content["_rulesCore"] as JsonObject ?? new JsonObject();
        var threeX = extension["threeX"] as JsonObject ?? new JsonObject();

        switch (record.EntityType.Trim().ToLowerInvariant())
        {
            case "race":
            case "species":
                MapRace(content, threeX, segments);
                break;
            case "spell":
                MapSpell(threeX, segments);
                break;
            case "item":
            case "equipment":
                MapItem(threeX, segments);
                break;
            case "feat":
                MapFeat(threeX, segments);
                break;
        }

        if (threeX.Count > 0) extension["threeX"] = threeX;
        content["_rulesCore"] = extension;
        return record with
        {
            ContentJson = content.ToJsonString(new JsonSerializerOptions { WriteIndented = false })
        };
    }

    private static void MapRace(
        JsonObject content,
        JsonObject threeX,
        IReadOnlyList<Segment> segments)
    {
        int? darkvision = null;
        var vision = Last(segments, "VISION");
        if (vision is not null)
        {
            var match = DarkvisionDistance.Match(vision.Value);
            if (match.Success
                && int.TryParse(match.Groups["feet"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var feet)
                && feet > 0)
            {
                darkvision = feet;
            }
            threeX["vision"] = vision.Value;
        }

        foreach (var bonus in All(segments, "BONUS"))
        {
            var parts = bonus.Value.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length >= 3
                && string.Equals(parts[0], "VAR", StringComparison.OrdinalIgnoreCase)
                && string.Equals(parts[1], "DarkvisionRange", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var feet)
                && feet > 0)
            {
                darkvision = feet;
            }
        }
        if (darkvision.HasValue)
        {
            content["darkvision"] = darkvision.Value;
        }

        var favoredClass = Last(segments, "FAVCLASS");
        if (favoredClass is not null) threeX["favoredClass"] = favoredClass.Value;

        var raceType = Last(segments, "RACETYPE");
        if (raceType is not null) threeX["raceType"] = raceType.Value;
        var raceSubtype = Last(segments, "RACESUBTYPE");
        if (raceSubtype is not null)
        {
            var values = raceSubtype.Value.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var array = new JsonArray();
            foreach (var value in values) array.Add(value);
            if (array.Count > 0) threeX["raceSubtypes"] = array;
        }

        var automaticLanguages = new List<string>();
        foreach (var auto in All(segments, "AUTO"))
        {
            var parts = auto.Value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2 && string.Equals(parts[0], "LANG", StringComparison.OrdinalIgnoreCase))
            {
                automaticLanguages.AddRange(parts.Skip(1));
            }
        }
        if (automaticLanguages.Count > 0)
        {
            var array = new JsonArray();
            foreach (var value in automaticLanguages.Distinct(StringComparer.OrdinalIgnoreCase)) array.Add(value);
            threeX["automaticLanguages"] = array;
        }

        var bonusLanguages = All(segments, "LANGBONUS")
            .SelectMany(value => value.Value.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (bonusLanguages.Length > 0)
        {
            var array = new JsonArray();
            foreach (var value in bonusLanguages) array.Add(value);
            threeX["bonusLanguages"] = array;
        }

        var challengeRating = Last(segments, "CR");
        if (challengeRating is not null) threeX["racialChallengeRating"] = challengeRating.Value;
    }

    private static void MapSpell(JsonObject threeX, IReadOnlyList<Segment> segments)
    {
        CopyLast(threeX, segments, "COMPS", "components");
        CopyLast(threeX, segments, "COMPONENTS", "components");
        CopyLast(threeX, segments, "CASTTIME", "castingTime");
        CopyLast(threeX, segments, "CASTINGTIME", "castingTime");
        CopyLast(threeX, segments, "RANGE", "range");
        CopyLast(threeX, segments, "TARGETAREA", "targetArea");
        CopyLast(threeX, segments, "DURATION", "duration");
        CopyLast(threeX, segments, "SAVEINFO", "savingThrow");
        CopyLast(threeX, segments, "SPELLRES", "spellResistance");
        CopyLast(threeX, segments, "DESCRIPTOR", "descriptor");
        CopyLast(threeX, segments, "SUBSCHOOL", "subschool");
    }

    private static void MapItem(JsonObject threeX, IReadOnlyList<Segment> segments)
    {
        CopyLast(threeX, segments, "TYPE", "itemType");
        CopyLast(threeX, segments, "SIZE", "size");
        CopyLast(threeX, segments, "SLOTS", "slots");
        CopyLast(threeX, segments, "WIELD", "wieldCategory");
        CopyLast(threeX, segments, "CRITRANGE", "criticalRange");
        CopyLast(threeX, segments, "CRITMULT", "criticalMultiplier");
        CopyLast(threeX, segments, "DAMAGE", "damage");
    }

    private static void MapFeat(JsonObject threeX, IReadOnlyList<Segment> segments)
    {
        var types = All(segments, "TYPE")
            .SelectMany(value => value.Value.Split(
                '.',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (types.Length == 0) return;
        var array = new JsonArray();
        foreach (var value in types) array.Add(value);
        threeX["featTypes"] = array;
    }

    private static void CopyLast(
        JsonObject threeX,
        IReadOnlyList<Segment> segments,
        string tag,
        string property)
    {
        var value = Last(segments, tag);
        if (value is not null && !string.IsNullOrWhiteSpace(value.Value))
        {
            threeX[property] = value.Value;
        }
    }

    private static bool TryReadSegments(string rawJson, out IReadOnlyList<Segment> segments)
    {
        segments = [];
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !TryProperty(document.RootElement, "segments", out var values)
                || values.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var result = new List<Segment>();
            var ordinal = 0;
            foreach (var value in values.EnumerateArray())
            {
                if (value.ValueKind == JsonValueKind.Object
                    && TryString(value, "tag", out var tag))
                {
                    _ = TryString(value, "value", out var segmentValue);
                    result.Add(new Segment(ordinal, tag.Trim(), segmentValue.Trim()));
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

    private static IEnumerable<Segment> All(IReadOnlyList<Segment> segments, string tag) =>
        segments.Where(value => string.Equals(value.Tag, tag, StringComparison.OrdinalIgnoreCase));

    private static Segment? Last(IReadOnlyList<Segment> segments, string tag) =>
        segments.LastOrDefault(value => string.Equals(value.Tag, tag, StringComparison.OrdinalIgnoreCase));

    private static bool TryString(JsonElement value, string name, out string result)
    {
        result = string.Empty;
        if (!TryProperty(value, name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        result = property.GetString() ?? string.Empty;
        return result.Length > 0;
    }

    private static bool TryProperty(JsonElement value, string name, out JsonElement property)
    {
        if (value.TryGetProperty(name, out property)) return true;
        foreach (var candidate in value.EnumerateObject())
        {
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }
        property = default;
        return false;
    }

    private sealed record Segment(int Index, string Tag, string Value);
}
