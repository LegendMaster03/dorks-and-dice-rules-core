using System.Collections.Frozen;
using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Non-executable preliminary creature facts. Source-shaped abilities and text
/// are never represented as successfully translated native combat effects.
/// </summary>
public sealed record NativeCreatureProjection(
    string RulesetId,
    string Fingerprint,
    string EntityId,
    string Status,
    IReadOnlyDictionary<string, NativeNumeric> Values,
    IReadOnlyList<NativeIssue> Issues,
    string? ChallengeRating = null);

public sealed class NativeCreatureProjector(NativeCatalogSnapshot catalog)
{
    public NativeCreatureProjection Project(string entityId)
    {
        var values = new SortedDictionary<string, NativeNumeric>(StringComparer.Ordinal);
        var issues = new List<NativeIssue>();
        string? challengeRating = null;
        if (!catalog.TryGetById(entityId, out var record) || record?.Category != "monster")
        {
            issues.Add(new("native-monster-unavailable", entityId, "Monster identity unavailable."));
            return Result("unavailable");
        }
        if (!TryObject(record.Definition, "definition", out var definition))
        {
            issues.Add(new("stat-block-unconverted", entityId, "Native typed stats unavailable."));
            return Result("unsupported");
        }
        var abilities = new (string shortName, string name)[] {
            ("str", "strength"), ("dex", "dexterity"), ("con", "constitution"),
            ("int", "intelligence"), ("wis", "wisdom"), ("cha", "charisma") };
        foreach (var (sourceName, ability) in abilities)
        {
            if (ReadNumber(definition, sourceName) is int score)
            {
                values[$"ability.{ability}.score"] = new("resolved", score);
                values[$"ability.{ability}.modifier"] = new("resolved",
                    (int)Math.Floor((score - 10) / 2.0));
            }
            else issues.Add(new("ability-unconverted", $"{entityId}.{sourceName}",
                "Source ability is absent or not normalized."));
        }
        if (definition.TryGetProperty("ac", out var armor) && armor.ValueKind == JsonValueKind.Array &&
            armor.GetArrayLength() == 1 && armor[0].ValueKind == JsonValueKind.Number &&
            armor[0].TryGetInt32(out var normal))
            values["ac.normal"] = new("resolved", normal);
        else issues.Add(new("defense-unconverted", entityId,
            "Normal AC is not a single numeric value."));
        // Normal AC alone cannot be decomposed into Touch and Flat-Footed contributions.
        values["ac.touch"] = new("unsupported", null, "Typed defenses are required.");
        values["ac.flat-footed"] = new("unsupported", null, "Typed defenses are required.");
        if (TryObject(definition, "hp", out var hitPoints) &&
            ReadNumber(hitPoints, "average") is int hp)
            values["hit-points.maximum"] = new("resolved", hp);
        else issues.Add(new("hit-points-unconverted", entityId, "Source HP is not normalized."));
        if (TryObject(definition, "speed", out var speeds))
            foreach (var move in speeds.EnumerateObject())
                if (move.Value.ValueKind == JsonValueKind.Number && move.Value.TryGetInt32(out var feet))
                    values[$"movement.{move.Name}"] = new("resolved", feet);
        // CR is preserved as a source-defined string (including fractional CR); it
        // is informational and must not be mistaken for a native proficiency rule.
        if (definition.TryGetProperty("cr", out var challenge))
        {
            if (challenge.ValueKind == JsonValueKind.String)
                challengeRating = challenge.GetString();
            else if (challenge.ValueKind == JsonValueKind.Number)
                challengeRating = challenge.GetRawText();
            else if (TryObject(definition, "cr", out var challengeObject) &&
                     challengeObject.TryGetProperty("cr", out var crValue) &&
                     crValue.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                challengeRating = crValue.ValueKind == JsonValueKind.String
                    ? crValue.GetString() : crValue.GetRawText();
        }

        // The 2024 source initiative proficiency multiplier is *not* an
        // initiative modifier. Falling back to Dexterity would silently discard
        // the explicit initiative bonus and produce incorrect encounter order.
        if (definition.TryGetProperty("initiative", out var initiative))
        {
            if (initiative.ValueKind == JsonValueKind.Number && initiative.TryGetInt32(out var direct))
                values["initiative"] = new("resolved", direct);
            else if (initiative.ValueKind == JsonValueKind.Object &&
                     ReadNumber(initiative, "bonus") is int explicitBonus)
                values["initiative"] = new("resolved", explicitBonus);
            else
            {
                values["initiative"] = new("unsupported", null,
                    "Declared source initiative profile has no approved native computation.");
                issues.Add(new("initiative-profile-unconverted", entityId,
                    "Explicit source initiative proficiency must not be ignored."));
            }
        }
        else if (values.TryGetValue("ability.dexterity.modifier", out var dex) &&
                 dex.Value is int dexModifier)
            values["initiative"] = new("resolved", dexModifier);
        // Attacks, traits, defenses, spell effects and source authorization remain explicit.
        issues.Add(new("source-conversion-required", entityId,
            "Imported/source-shaped monster content has not passed versioned mechanics conversion."));
        return Result("partial");

        NativeCreatureProjection Result(string status) =>
            new(catalog.RulesetId, catalog.Fingerprint, entityId, status,
                values.ToFrozenDictionary(StringComparer.Ordinal), issues.ToArray(),
                challengeRating);
    }

    private static bool TryObject(JsonElement parent, string name, out JsonElement result)
    {
        result = default;
        return parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out result) &&
            result.ValueKind == JsonValueKind.Object;
    }
    private static int? ReadNumber(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
}
