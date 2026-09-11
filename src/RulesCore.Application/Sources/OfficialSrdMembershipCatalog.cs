using System.Text.Json;

namespace RulesCore.Application.Sources;

/// <summary>
/// Manually reviewed membership constraints derived from the official Wizards SRD PDFs.
/// This is intentionally small and explicit: it constrains aggregate structured files where
/// multiple SRD generations share the same physical JSON document. Entity types not listed
/// here continue to rely on their edition-specific SRD document plus source-code filtering.
/// </summary>
public static class OfficialSrdMembershipCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlySet<string>>> NamesBySourceAndType =
        new Dictionary<string, IReadOnlyDictionary<string, IReadOnlySet<string>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["SRD51"] = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["background"] = Set("Acolyte"),
                ["feat"] = Set("Grappler"),
                ["race"] = Set(
                    "Dragonborn",
                    "Dwarf",
                    "Elf",
                    "Gnome",
                    "Half-Elf",
                    "Half-Orc",
                    "Halfling",
                    "Human",
                    "Tiefling")
            },
            ["SRD52"] = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["background"] = Set("Acolyte", "Criminal", "Sage", "Soldier"),
                ["feat"] = Set(
                    "Ability Score Improvement",
                    "Alert",
                    "Archery",
                    "Boon of Combat Prowess",
                    "Boon of Dimensional Travel",
                    "Boon of Fate",
                    "Boon of Irresistible Offense",
                    "Boon of Spell Recall",
                    "Boon of the Night Spirit",
                    "Boon of Truesight",
                    "Defense",
                    "Grappler",
                    "Great Weapon Fighting",
                    "Magic Initiate",
                    "Savage Attacker",
                    "Skilled",
                    "Two-Weapon Fighting"),
                ["race"] = Set(
                    "Dragonborn",
                    "Dwarf",
                    "Elf",
                    "Gnome",
                    "Goliath",
                    "Halfling",
                    "Human",
                    "Orc",
                    "Tiefling")
            }
        };

    public static bool HasManualConstraint(string sourceCode, string entityType) =>
        NamesBySourceAndType.TryGetValue(sourceCode, out var byType)
        && byType.ContainsKey(entityType);

    public static bool IsManuallyConfirmed(
        string sourceCode,
        string entityType,
        JsonElement entity)
    {
        if (!NamesBySourceAndType.TryGetValue(sourceCode, out var byType)
            || !byType.TryGetValue(entityType, out var names))
        {
            return true;
        }

        if (!entity.TryGetProperty("name", out var nameElement)
            || nameElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(nameElement.GetString())
            || !names.Contains(nameElement.GetString()!.Trim()))
        {
            return false;
        }

        // The shared SRD52 aggregate contains a legacy-looking Magic Initiate record with the
        // same source/name identity as the actual SRD 5.2.1 feat. The official PDF confirms the
        // 2024 feat; basicRules2024 identifies that structured representation without changing
        // durable identity or treating page number as identity.
        if (string.Equals(sourceCode, "SRD52", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(entityType, "background", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entityType, "feat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entityType, "race", StringComparison.OrdinalIgnoreCase)))
        {
            return entity.TryGetProperty("basicRules2024", out var basicRules)
                && basicRules.ValueKind == JsonValueKind.True;
        }

        return true;
    }

    public static IReadOnlySet<string> GetConfirmedNames(string sourceCode, string entityType) =>
        NamesBySourceAndType.TryGetValue(sourceCode, out var byType)
        && byType.TryGetValue(entityType, out var names)
            ? names
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlySet<string> Set(params string[] values) =>
        values.ToHashSet(StringComparer.OrdinalIgnoreCase);
}
