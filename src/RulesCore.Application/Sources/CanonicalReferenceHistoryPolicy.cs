namespace RulesCore.Application.Sources;

/// <summary>
/// Defines the project-level logical-reference compatibility rule independently of source format.
/// Mechanical equality is deliberately not part of this policy: changed mechanics are revisions
/// within a history unless authoritative reconciliation explicitly separates the identities.
/// </summary>
public static class CanonicalReferenceHistoryPolicy
{
    private static readonly HashSet<(string Left, string Right)> CompatibleMigrations =
    [
        Pair("race", "species"),
        Pair("subrace", "subspecies"),
        Pair("prestigeClass", "subclass")
    ];

    public static bool AreCategoriesCompatible(string? left, string? right)
    {
        var normalizedLeft = NormalizeCategory(left);
        var normalizedRight = NormalizeCategory(right);
        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return false;
        }

        return string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal)
            || CompatibleMigrations.Contains(Pair(normalizedLeft, normalizedRight));
    }

    public static string NormalizeCategory(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : CanonicalSourceIdentity.NormalizeIdentityPart(value);

    private static (string Left, string Right) Pair(string left, string right)
    {
        var normalizedLeft = CanonicalSourceIdentity.NormalizeIdentityPart(left);
        var normalizedRight = CanonicalSourceIdentity.NormalizeIdentityPart(right);
        return string.CompareOrdinal(normalizedLeft, normalizedRight) <= 0
            ? (normalizedLeft, normalizedRight)
            : (normalizedRight, normalizedLeft);
    }
}
