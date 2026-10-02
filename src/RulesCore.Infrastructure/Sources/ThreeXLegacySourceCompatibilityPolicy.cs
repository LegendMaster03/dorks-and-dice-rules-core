using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Handles stable textual conventions in the reviewed legacy 3.x corpus that are mechanically
/// unambiguous but are not represented by ASCII-only source text. The policy stays deliberately
/// narrow so source prose is never treated as a general-purpose rules parser.
/// </summary>
internal static class ThreeXLegacySourceCompatibilityPolicy
{
    private static readonly Regex RacialAbilityAdjustment = new(
        @"(?<sign>[+\-\u2012\u2013\u2014\u2212])\s*(?<amount>\d+)\s+(?<ability>Strength|Dexterity|Constitution|Intelligence|Wisdom|Charisma|Str|Dex|Con|Int|Wis|Cha)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static NormalizedSourceRecord Apply(
        NormalizedSourceRepresentation representation,
        NormalizedSourceRecord record)
    {
        ArgumentNullException.ThrowIfNull(representation);
        ArgumentNullException.ThrowIfNull(record);

        if (!string.Equals(
                representation.FormatKey,
                LegacySrdSourceFormatAdapter.Format,
                StringComparison.OrdinalIgnoreCase)
            || record.EntityType.Trim().ToLowerInvariant() is not ("race" or "species")
            || string.IsNullOrWhiteSpace(record.ContentJson)
            || !TryReadBody(record.RawJson, out var body))
        {
            return record;
        }

        var adjustments = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in RacialAbilityAdjustment.Matches(body))
        {
            if (!int.TryParse(
                    match.Groups["amount"].Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var magnitude)
                || magnitude < 0
                || !TryAbility(match.Groups["ability"].Value, out var ability))
            {
                continue;
            }

            var sign = match.Groups["sign"].Value[0];
            var amount = sign == '+' ? magnitude : -magnitude;
            adjustments[ability] = adjustments.GetValueOrDefault(ability) + amount;
        }

        if (adjustments.Count == 0)
        {
            return record;
        }

        var content = JsonNode.Parse(record.ContentJson) as JsonObject;
        if (content is null)
        {
            return record;
        }

        var ability = new JsonObject();
        foreach (var adjustment in adjustments.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            ability[adjustment.Key] = adjustment.Value;
        }
        content["ability"] = new JsonArray(ability);
        return record with
        {
            ContentJson = content.ToJsonString(new JsonSerializerOptions { WriteIndented = false })
        };
    }

    private static bool TryReadBody(string rawJson, out string body)
    {
        body = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("body", out var bodyElement)
                || bodyElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }
            body = bodyElement.GetString() ?? string.Empty;
            return body.Length > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryAbility(string value, out string ability)
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
}
