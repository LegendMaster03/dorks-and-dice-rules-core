using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

internal sealed class SpellCharacterRuleProjectionModule : ICharacterRuleProjectionModule
{
    public bool Handles(CharacterProjectionRule rule, CharacterProjectionContext context) =>
        (context.KnownSpells.Contains(rule.Catalog.ConceptKey)
            || context.PreparedSpells.Contains(rule.Catalog.ConceptKey))
        && string.Equals(rule.Catalog.EntityType, "spell", StringComparison.OrdinalIgnoreCase);

    public void Project(CharacterProjectionRule rule, CharacterProjectionContext context)
    {
        context.AddFeature(
            $"feature.{rule.Catalog.ConceptKey}",
            rule.Catalog.DisplayName,
            "spell",
            CharacterResolutionStates.Resolved,
            rule.Catalog.ConceptKey,
            rule.Provenance);

        var actionType = ReadActionType(rule.Document);
        var actionKey = $"action.spell.{rule.Catalog.ConceptKey}";
        var components = ReadComponents(rule.Document, out var materialComponent);
        context.Actions[actionKey] = new CharacterActionView(
            actionKey,
            rule.Catalog.DisplayName,
            actionType,
            CharacterResolutionStates.ApplicableUnresolved,
            null,
            null,
            null,
            CharacterProjectionJson.RangeText(rule.Document),
            null,
            null,
            "spellcasting.resource",
            null,
            ["spellcasting"],
            rule.Provenance,
            rule.Catalog.ConceptKey,
            CharacterProjectionJson.Integer(rule.Document, "level"),
            CharacterProjectionJson.String(rule.Document, "school"),
            ReadCastingTime(rule.Document),
            components,
            materialComponent,
            ReadDuration(rule.Document),
            ReadRitual(rule.Document),
            ReadConcentration(rule.Document));
    }

    private static string? ReadActionType(JsonElement document)
    {
        if (!CharacterProjectionJson.TryGetProperty(document, "time", out var time)
            || time.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var first = time.EnumerateArray().FirstOrDefault();
        return first.ValueKind == JsonValueKind.Object
            ? CharacterProjectionJson.String(first, "unit")
            : null;
    }

    private static string? ReadCastingTime(JsonElement document)
    {
        if (!CharacterProjectionJson.TryGetProperty(document, "time", out var time)
            || time.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var first = time.EnumerateArray().FirstOrDefault();
        if (first.ValueKind != JsonValueKind.Object)
        {
            return first.ValueKind == JsonValueKind.String ? first.GetString() : null;
        }
        var unit = CharacterProjectionJson.String(first, "unit");
        var number = CharacterProjectionJson.Integer(first, "number");
        return number is not null && !string.IsNullOrWhiteSpace(unit)
            ? $"{number} {unit}"
            : unit;
    }

    private static IReadOnlyList<string> ReadComponents(
        JsonElement document,
        out string? materialComponent)
    {
        materialComponent = null;
        if (!CharacterProjectionJson.TryGetProperty(document, "components", out var components)
            || components.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var result = new List<string>();
        if (CharacterProjectionJson.Boolean(components, "v") == true)
        {
            result.Add("V");
        }
        if (CharacterProjectionJson.Boolean(components, "s") == true)
        {
            result.Add("S");
        }
        if (CharacterProjectionJson.TryGetProperty(components, "m", out var material)
            && material.ValueKind is not JsonValueKind.False and not JsonValueKind.Null
                and not JsonValueKind.Undefined)
        {
            result.Add("M");
            materialComponent = material.ValueKind switch
            {
                JsonValueKind.String => material.GetString(),
                JsonValueKind.Object => CharacterProjectionJson.String(material, "text"),
                _ => null
            };
        }
        return result;
    }

    private static string? ReadDuration(JsonElement document)
    {
        if (!CharacterProjectionJson.TryGetProperty(document, "duration", out var duration)
            || duration.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var first = duration.EnumerateArray().FirstOrDefault();
        if (first.ValueKind != JsonValueKind.Object)
        {
            return first.ValueKind == JsonValueKind.String ? first.GetString() : null;
        }
        if (CharacterProjectionJson.TryGetProperty(first, "duration", out var timed)
            && timed.ValueKind == JsonValueKind.Object)
        {
            var amount = CharacterProjectionJson.Integer(timed, "amount");
            var unit = CharacterProjectionJson.String(timed, "type");
            if (amount is not null && !string.IsNullOrWhiteSpace(unit))
            {
                return $"{amount} {unit}";
            }
        }
        return CharacterProjectionJson.String(first, "type");
    }

    private static bool? ReadRitual(JsonElement document)
    {
        var direct = CharacterProjectionJson.Boolean(document, "ritual");
        if (direct is not null)
        {
            return direct;
        }
        return CharacterProjectionJson.TryGetProperty(document, "meta", out var meta)
            ? CharacterProjectionJson.Boolean(meta, "ritual")
            : null;
    }

    private static bool? ReadConcentration(JsonElement document)
    {
        if (!CharacterProjectionJson.TryGetProperty(document, "duration", out var duration)
            || duration.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var values = duration.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.Object)
            .Select(value => CharacterProjectionJson.Boolean(value, "concentration"))
            .Where(value => value is not null)
            .Cast<bool>()
            .ToArray();
        return values.Length == 0 ? null : values.Any(value => value);
    }
}

