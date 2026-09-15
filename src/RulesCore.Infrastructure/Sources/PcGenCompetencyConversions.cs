namespace RulesCore.Infrastructure.Sources;

internal sealed record PcGenCompetencyConversion(
    string SourceName,
    string TargetName,
    string TargetType,
    string Relationship,
    string? Scope = null,
    bool PreserveSourceMechanicalName = false);

internal static class PcGenCompetencyConversions
{
    private static readonly IReadOnlyDictionary<string, PcGenCompetencyConversion> Direct =
        new Dictionary<string, PcGenCompetencyConversion>(StringComparer.OrdinalIgnoreCase)
        {
            ["Bluff"] = Skill("Bluff", "Deception"),
            ["Diplomacy"] = Skill("Diplomacy", "Persuasion"),
            ["Handle Animal"] = Skill("Handle Animal", "Animal Handling"),
            ["Heal"] = Skill("Heal", "Medicine"),
            ["Intimidate"] = Skill("Intimidate", "Intimidation"),
            ["Knowledge (arcana)"] = Skill("Knowledge (arcana)", "Arcana"),
            ["Knowledge (history)"] = Skill("Knowledge (history)", "History"),
            ["Knowledge (nature)"] = Skill("Knowledge (nature)", "Nature"),
            ["Knowledge (religion)"] = Skill("Knowledge (religion)", "Religion"),
            ["Sense Motive"] = Skill("Sense Motive", "Insight"),
            ["Sleight of Hand"] = Skill("Sleight of Hand", "Sleight of Hand"),
            ["Survival"] = Skill("Survival", "Survival"),
            ["Craft (alchemy)"] = Tool("Craft (alchemy)", "Alchemist's Supplies"),
            ["Forgery"] = Tool("Forgery", "Forgery Kit"),
            ["Open Lock"] = Tool(
                "Open Lock",
                "Thieves' Tools",
                scope: "open-lock",
                preserveSourceMechanicalName: true)
        };

    private static readonly IReadOnlyDictionary<string, PcGenCompetencyConversion> ThirdEditionOnly =
        new Dictionary<string, PcGenCompetencyConversion>(StringComparer.OrdinalIgnoreCase)
        {
            ["Pick Pocket"] = Skill("Pick Pocket", "Sleight of Hand"),
            ["Wilderness Lore"] = Skill("Wilderness Lore", "Survival"),
            ["Alchemy"] = Tool("Alchemy", "Alchemist's Supplies")
        };

    public static PcGenCompetencyConversion? Resolve(string sourceName, string? edition)
    {
        var isThirdEdition = string.Equals(edition, "3e", StringComparison.OrdinalIgnoreCase);
        var isThreePointFive = string.Equals(edition, "3.5e", StringComparison.OrdinalIgnoreCase);
        if (!isThirdEdition && !isThreePointFive)
        {
            return null;
        }

        var normalizedName = sourceName.Trim();
        if (isThirdEdition
            && ThirdEditionOnly.TryGetValue(normalizedName, out var thirdEdition))
        {
            return thirdEdition;
        }

        return Direct.TryGetValue(normalizedName, out var direct)
            ? direct
            : null;
    }

    private static PcGenCompetencyConversion Skill(string sourceName, string targetName) =>
        new(sourceName, targetName, "skill", "direct-equivalence");

    private static PcGenCompetencyConversion Tool(
        string sourceName,
        string targetName,
        string? scope = null,
        bool preserveSourceMechanicalName = false) =>
        new(
            sourceName,
            targetName,
            "tool",
            "direct-cross-type",
            scope,
            preserveSourceMechanicalName);
}
