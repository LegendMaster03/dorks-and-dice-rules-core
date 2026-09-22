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
            ["Alchemy"] = RelatedTool("Alchemy", "Alchemist's Supplies")
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
        if (TryResolveKnowledgeSpecialty(normalizedName, out var knowledge))
        {
            return knowledge;
        }

        if (isThirdEdition
            && ThirdEditionOnly.TryGetValue(normalizedName, out var thirdEdition))
        {
            return thirdEdition;
        }

        return Direct.TryGetValue(normalizedName, out var direct)
            ? direct
            : null;
    }

    private static bool TryResolveKnowledgeSpecialty(
        string sourceName,
        out PcGenCompetencyConversion conversion)
    {
        const string prefix = "Knowledge (";
        conversion = null!;
        if (!sourceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !sourceName.EndsWith(')')
            || sourceName.Length <= prefix.Length + 1)
        {
            return false;
        }

        var specialty = sourceName[prefix.Length..^1].Trim();
        if (string.IsNullOrWhiteSpace(specialty))
        {
            return false;
        }

        var target = char.ToUpperInvariant(specialty[0]) + specialty[1..];
        conversion = Skill(sourceName, target);
        return true;
    }

    internal static bool IsExactIdentityTranslation(PcGenCompetencyConversion conversion) =>
        conversion.Relationship is "direct-equivalence" or "direct-cross-type"
        && string.IsNullOrWhiteSpace(conversion.Scope);

    private static PcGenCompetencyConversion Skill(string sourceName, string targetName) =>
        new(sourceName, targetName, "skill", "direct-equivalence");

    private static PcGenCompetencyConversion RelatedTool(string sourceName, string targetName) =>
        new(
            sourceName,
            targetName,
            "tool",
            "related-competency",
            PreserveSourceMechanicalName: true);

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
