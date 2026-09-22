namespace RulesCore.Infrastructure.Sources;

internal sealed record ReviewedCompetencyFacetIdentity(
    string IdentityKey,
    string IdentityName,
    string FacetType);

internal static class ReviewedCompetencyFacetPolicy
{
    private static readonly IReadOnlyDictionary<string, ReviewedCompetencyFacetIdentity> LaterTools =
        new Dictionary<string, ReviewedCompetencyFacetIdentity>(StringComparer.OrdinalIgnoreCase)
        {
            ["Alchemist's Supplies"] = new("alchemy", "Alchemy", "tool"),
            ["Forgery Kit"] = new("forgery", "Forgery", "tool")
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
}
