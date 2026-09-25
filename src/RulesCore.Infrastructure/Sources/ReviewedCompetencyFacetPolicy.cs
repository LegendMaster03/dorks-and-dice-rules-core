namespace RulesCore.Infrastructure.Sources;

internal sealed record ReviewedCompetencyFacetRelationship(
    string Kind,
    string TargetType,
    string TargetName,
    string? Scope = null,
    bool SharesTrainingState = false);

internal sealed record ReviewedCompetencyFacetIdentity(
    string IdentityKey,
    string IdentityName,
    string FacetType,
    IReadOnlyList<ReviewedCompetencyFacetRelationship>? RelatedCompetencies = null);

internal static class ReviewedCompetencyFacetPolicy
{
    private static readonly IReadOnlyDictionary<string, ReviewedCompetencyFacetIdentity> LaterTools =
        new Dictionary<string, ReviewedCompetencyFacetIdentity>(StringComparer.OrdinalIgnoreCase)
        {
            ["Alchemist's Supplies"] = Shared("alchemy", "Alchemy"),
            ["Brewer's Supplies"] = Standalone(
                "brewing",
                "Brewing",
                Related("Brewer")),
            ["Calligrapher's Supplies"] = Shared("calligraphy", "Calligraphy"),
            ["Carpenter's Tools"] = Shared("carpentry", "Carpentry"),
            ["Cartographer's Tools"] = Standalone("cartography", "Cartography"),
            ["Cobbler's Tools"] = Shared("cobbling", "Cobbling"),
            ["Cook's Utensils"] = Standalone(
                "cooking",
                "Cooking",
                Related("Cook")),
            ["Glassblower's Tools"] = Standalone("glassblowing", "Glassblowing"),
            ["Jeweler's Tools"] = Shared("gemcutting", "Gemcutting"),
            ["Leatherworker's Tools"] = Shared("leatherworking", "Leatherworking"),
            ["Mason's Tools"] = Shared("stonemasonry", "Stonemasonry"),
            ["Painter's Supplies"] = Shared("painting", "Painting"),
            ["Potter's Tools"] = Shared("pottery", "Pottery"),
            ["Smith's Tools"] = Standalone(
                "smithing",
                "Smithing",
                Related("Blacksmithing"),
                Related("Armorsmithing"),
                Related("Weaponsmithing")),
            ["Tinker's Tools"] = Standalone(
                "tinkering",
                "Tinkering",
                Related("Locksmithing"),
                Related("Trapmaking")),
            ["Weaver's Tools"] = Shared("weaving", "Weaving"),
            ["Woodcarver's Tools"] = Standalone(
                "woodcarving",
                "Woodcarving",
                Related("Bowmaking"),
                Related("Carpentry")),
            ["Disguise Kit"] = Standalone(
                "disguise-kit",
                "Disguise Kit",
                new ReviewedCompetencyFacetRelationship(
                    "related-competency",
                    "skill",
                    "Disguise")),
            ["Forgery Kit"] = Shared("forgery", "Forgery"),
            ["Herbalism Kit"] = Standalone(
                "herbalism",
                "Herbalism",
                Related("Herbalist"),
                Related("Apothecary")),
            ["Navigator's Tools"] = Standalone(
                "navigation",
                "Navigation",
                Related("Guide"),
                Related("Sailor")),
            ["Poisoner's Kit"] = Standalone("poisoning", "Poisoning"),
            ["Thieves' Tools"] = Standalone("thieves-tools", "Thieves' Tools")
        };

    public static ReviewedCompetencyFacetIdentity? ResolveLaterFacet(
        string entityType,
        string? name)
    {
        if (!string.Equals(entityType, "tool", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return LaterTools.TryGetValue(name.Trim(), out var identity)
            ? identity
            : null;
    }

    private static ReviewedCompetencyFacetIdentity Shared(
        string identityKey,
        string identityName) =>
        new(identityKey, identityName, "tool");

    private static ReviewedCompetencyFacetIdentity Standalone(
        string identityKey,
        string identityName,
        params ReviewedCompetencyFacetRelationship[] relatedCompetencies) =>
        new(
            identityKey,
            identityName,
            "tool",
            relatedCompetencies.Length == 0 ? null : relatedCompetencies);

    private static ReviewedCompetencyFacetRelationship Related(string targetName) =>
        new("related-competency", "competency", targetName);
}
