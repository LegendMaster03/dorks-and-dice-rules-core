namespace RulesCore.Domain.Rules;

public static class RuleConceptEntityTypes
{
    public const string Class = "class";
    public const string Subclass = "subclass";
    public const string PrestigeClass = "prestigeClass";
    public const string Species = "species";
    public const string Subspecies = "subspecies";

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Rule concept entity type can not be blank.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > 120)
        {
            throw new ArgumentException("Rule concept entity type can not exceed 120 characters.", nameof(value));
        }

        if (string.Equals(normalized, Class, StringComparison.OrdinalIgnoreCase))
        {
            return Class;
        }
        if (string.Equals(normalized, Subclass, StringComparison.OrdinalIgnoreCase))
        {
            return Subclass;
        }
        if (string.Equals(normalized, PrestigeClass, StringComparison.OrdinalIgnoreCase))
        {
            return PrestigeClass;
        }
        if (string.Equals(normalized, Species, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "race", StringComparison.OrdinalIgnoreCase))
        {
            return Species;
        }
        if (string.Equals(normalized, Subspecies, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "subrace", StringComparison.OrdinalIgnoreCase))
        {
            return Subspecies;
        }

        return normalized.ToLowerInvariant();
    }
}

public static class RuleConceptRelationshipKinds
{
    public const string ParentClass = "parent-class";
    public const string ParentSpecies = "parent-species";
}
