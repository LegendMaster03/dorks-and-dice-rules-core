using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Handles stable textual conventions in the reviewed legacy 3.x corpus that are mechanically
/// unambiguous but are not represented by the Markdown/ASCII shape expected by the common bulk
/// translator. The policy stays deliberately narrow so source prose is never treated as a
/// general-purpose rules parser.
/// </summary>
internal static class ThreeXLegacySourceCompatibilityPolicy
{
    private static readonly Regex RacialAbilityAdjustment = new(
        @"(?<sign>[+\-\u2012\u2013\u2014\u2212])\s*(?<amount>\d+)\s+(?<ability>Strength|Dexterity|Constitution|Intelligence|Wisdom|Charisma|Str|Dex|Con|Int|Wis|Cha)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HtmlBreak = new(
        @"<br\s*/?>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HtmlBlockBoundary = new(
        @"</(?:p|div|li|h[1-6]|tr|table|section|article)\s*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HtmlTag = new(
        @"<[^>]+>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

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
            || string.IsNullOrWhiteSpace(record.ContentJson)
            || !TryReadBody(record.RawJson, out var body))
        {
            return record;
        }

        var normalized = ReplayHtmlAsPlainTextWhenNeeded(representation, record, body);
        if (normalized.EntityType.Trim().ToLowerInvariant() is not ("race" or "species"))
        {
            return normalized;
        }

        return NormalizeUnicodeRacialAbilityAdjustments(normalized, body);
    }

    private static NormalizedSourceRecord ReplayHtmlAsPlainTextWhenNeeded(
        NormalizedSourceRepresentation representation,
        NormalizedSourceRecord record,
        string body)
    {
        if (!body.Contains('<', StringComparison.Ordinal)
            || !body.Contains('>', StringComparison.Ordinal))
        {
            return record;
        }

        var plainBody = HtmlBreak.Replace(body, "\n");
        plainBody = HtmlBlockBoundary.Replace(plainBody, "\n");
        plainBody = WebUtility.HtmlDecode(HtmlTag.Replace(plainBody, string.Empty));
        plainBody = string.Join(
            '\n',
            plainBody
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0));
        if (plainBody.Length == 0)
        {
            return record;
        }

        JsonObject? raw;
        try
        {
            raw = JsonNode.Parse(record.RawJson) as JsonObject;
        }
        catch (JsonException)
        {
            return record;
        }
        if (raw is null)
        {
            return record;
        }

        raw["body"] = plainBody;
        var replayed = ThreeXBulkTranslationPolicy.Apply(
            representation,
            record with
            {
                RawJson = raw.ToJsonString(new JsonSerializerOptions { WriteIndented = false })
            });
        return replayed with { RawJson = record.RawJson };
    }

    private static NormalizedSourceRecord NormalizeUnicodeRacialAbilityAdjustments(
        NormalizedSourceRecord record,
        string body)
    {
        var adjustments = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in RacialAbilityAdjustment.Matches(body))
        {
            if (!int.TryParse(
                    match.Groups["amount"].Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var magnitude)
                || magnitude < 0
                || !TryAbility(match.Groups["ability"].Value, out var abilityKey))
            {
                continue;
            }

            var sign = match.Groups["sign"].Value[0];
            var amount = sign == '+' ? magnitude : -magnitude;
            adjustments[abilityKey] = adjustments.GetValueOrDefault(abilityKey) + amount;
        }

        if (adjustments.Count == 0)
        {
            return record;
        }

        var content = JsonNode.Parse(record.ContentJson!) as JsonObject;
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
