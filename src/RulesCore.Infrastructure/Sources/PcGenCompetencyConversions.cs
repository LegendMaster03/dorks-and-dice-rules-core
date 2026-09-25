namespace RulesCore.Infrastructure.Sources;

internal sealed record PcGenCompetencyConversion(
    string SourceName,
    string TargetName,
    string TargetType,
    string Relationship,
    string? Scope = null,
    bool PreserveSourceMechanicalName = false,
    string? SharedCompetencyKey = null,
    string? SharedCompetencyName = null,
    string? FacetType = null);

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
            ["Craft (alchemy)"] = SharedToolFacet(
                "Craft (alchemy)",
                "Alchemist's Supplies",
                "alchemy",
                "Alchemy"),
            ["Craft (calligraphy)"] = SharedToolFacet(
                "Craft (calligraphy)",
                "Calligrapher's Supplies",
                "calligraphy",
                "Calligraphy"),
            ["Craft (carpentry)"] = SharedToolFacet(
                "Craft (carpentry)",
                "Carpenter's Tools",
                "carpentry",
                "Carpentry"),
            ["Craft (cobbling)"] = SharedToolFacet(
                "Craft (cobbling)",
                "Cobbler's Tools",
                "cobbling",
                "Cobbling"),
            ["Craft (gemcutting)"] = SharedToolFacet(
                "Craft (gemcutting)",
                "Jeweler's Tools",
                "gemcutting",
                "Gemcutting"),
            ["Craft (leatherworking)"] = SharedToolFacet(
                "Craft (leatherworking)",
                "Leatherworker's Tools",
                "leatherworking",
                "Leatherworking"),
            ["Craft (painting)"] = SharedToolFacet(
                "Craft (painting)",
                "Painter's Supplies",
                "painting",
                "Painting"),
            ["Craft (pottery)"] = SharedToolFacet(
                "Craft (pottery)",
                "Potter's Tools",
                "pottery",
                "Pottery"),
            ["Craft (stonemasonry)"] = SharedToolFacet(
                "Craft (stonemasonry)",
                "Mason's Tools",
                "stonemasonry",
                "Stonemasonry"),
            ["Craft (weaving)"] = SharedToolFacet(
                "Craft (weaving)",
                "Weaver's Tools",
                "weaving",
                "Weaving"),
            ["Forgery"] = SharedToolFacet(
                "Forgery",
                "Forgery Kit",
                "forgery",
                "Forgery"),
            ["Open Lock"] = Tool(
                "Open Lock",
                "Thieves' Tools",
                scope: "open-lock",
                preserveSourceMechanicalName: true),
            ["Disable Device"] = Tool(
                "Disable Device",
                "Thieves' Tools",
                scope: "disable-device",
                preserveSourceMechanicalName: true),
            ["Disguise"] = RelatedTool("Disguise", "Disguise Kit")
        };

    private static readonly IReadOnlyDictionary<string, PcGenCompetencyConversion> ThirdEditionOnly =
        new Dictionary<string, PcGenCompetencyConversion>(StringComparer.OrdinalIgnoreCase)
        {
            ["Pick Pocket"] = Skill("Pick Pocket", "Sleight of Hand"),
            ["Wilderness Lore"] = Skill("Wilderness Lore", "Survival"),
            ["Alchemy"] = SharedToolFacet(
                "Alchemy",
                "Alchemist's Supplies",
                "alchemy",
                "Alchemy")
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

    internal static bool EstablishesSharedCompetencyIdentity(
        PcGenCompetencyConversion conversion) =>
        string.Equals(
            conversion.Relationship,
            "shared-competency-facet",
            StringComparison.Ordinal)
        && string.IsNullOrWhiteSpace(conversion.Scope)
        && !string.IsNullOrWhiteSpace(conversion.SharedCompetencyKey);

    private static PcGenCompetencyConversion Skill(string sourceName, string targetName) =>
        new(sourceName, targetName, "skill", "direct-equivalence");

    private static PcGenCompetencyConversion RelatedTool(string sourceName, string targetName) =>
        new(
            sourceName,
            targetName,
            "tool",
            "related-competency",
            PreserveSourceMechanicalName: true);

    private static PcGenCompetencyConversion SharedToolFacet(
        string sourceName,
        string targetName,
        string sharedCompetencyKey,
        string sharedCompetencyName) =>
        new(
            sourceName,
            targetName,
            "tool",
            "shared-competency-facet",
            PreserveSourceMechanicalName: true,
            SharedCompetencyKey: sharedCompetencyKey,
            SharedCompetencyName: sharedCompetencyName,
            FacetType: "skill");

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
