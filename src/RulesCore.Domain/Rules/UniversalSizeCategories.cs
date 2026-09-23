namespace RulesCore.Domain.Rules;

public sealed record UniversalSizeCategoryDefinition(
    string DisplayName,
    string SourceCode,
    int ThreeXArmorClassModifier,
    int ThreeXGrappleModifier);

/// <summary>
/// Universal semantic creature-size categories. Source codes are ingestion representations;
/// edition-specific consequences consume these shared categories.
/// </summary>
public static class UniversalSizeCategories
{
    public static IReadOnlyList<UniversalSizeCategoryDefinition> All { get; } =
    [
        new("Fine", "F", 8, -16),
        new("Diminutive", "D", 4, -12),
        new("Tiny", "T", 2, -8),
        new("Small", "S", 1, -4),
        new("Medium", "M", 0, 0),
        new("Large", "L", -1, 4),
        new("Huge", "H", -2, 8),
        new("Gargantuan", "G", -4, 12),
        new("Colossal", "C", -8, 16)
    ];

    public static bool TryResolve(
        string? value,
        out UniversalSizeCategoryDefinition definition)
    {
        definition = null!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        var found = All.FirstOrDefault(candidate =>
            string.Equals(
                candidate.DisplayName,
                normalized,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                candidate.SourceCode,
                normalized,
                StringComparison.OrdinalIgnoreCase));
        if (found is null)
        {
            return false;
        }

        definition = found;
        return true;
    }

    public static string Normalize(string value) =>
        TryResolve(value, out var definition)
            ? definition.DisplayName
            : value.Trim();
}
