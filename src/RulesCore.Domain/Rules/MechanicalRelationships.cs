namespace RulesCore.Domain.Rules;

public static class MechanicalRelationshipKinds
{
    public const string CompositeSkill = "composite-skill";
}

public static class MechanicalRelationshipCompositionKinds
{
    public const string ArithmeticMean = "arithmetic-mean";
}

public static class MechanicalRelationshipDirections
{
    public const string ComponentsToParent = "components-to-parent";
}

public sealed record MechanicalCompetencyReference(
    string ConceptKey,
    string EntityType,
    string DisplayName);

public sealed record MechanicalRelationshipDefinition(
    string Key,
    string Kind,
    MechanicalCompetencyReference Parent,
    IReadOnlyList<MechanicalCompetencyReference> Components,
    string Composition,
    string Direction);

public sealed record CompetencyModifier(
    string TargetConceptKey,
    int Value);

public sealed record CompositeCompetencyEvaluation(
    int ParentValue,
    int ExplicitParentModifier,
    IReadOnlyDictionary<string, int> EffectiveComponentValues);

public static class CompositeCompetencyEvaluator
{
    public static CompositeCompetencyEvaluation Evaluate(
        MechanicalRelationshipDefinition definition,
        IReadOnlyDictionary<string, int> componentBaseValues,
        IReadOnlyCollection<CompetencyModifier> modifiers)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(componentBaseValues);
        ArgumentNullException.ThrowIfNull(modifiers);

        if (!string.Equals(definition.Kind, MechanicalRelationshipKinds.CompositeSkill, StringComparison.Ordinal)
            || !string.Equals(definition.Composition, MechanicalRelationshipCompositionKinds.ArithmeticMean, StringComparison.Ordinal)
            || !string.Equals(definition.Direction, MechanicalRelationshipDirections.ComponentsToParent, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The relationship is not a supported components-to-parent arithmetic-mean composite.");
        }
        if (definition.Components.Count == 0)
        {
            throw new InvalidOperationException("A composite relationship must contain at least one component.");
        }

        var effectiveComponents = new Dictionary<string, int>(StringComparer.Ordinal);
        long componentTotal = 0;
        foreach (var component in definition.Components)
        {
            if (!componentBaseValues.TryGetValue(component.ConceptKey, out var baseValue))
            {
                throw new KeyNotFoundException($"No effective base value was supplied for component '{component.ConceptKey}'.");
            }

            long targetedModifier = 0;
            foreach (var modifier in modifiers.Where(value =>
                         string.Equals(value.TargetConceptKey, component.ConceptKey, StringComparison.Ordinal)))
            {
                targetedModifier += modifier.Value;
            }

            var effectiveValue = checked((int)(baseValue + targetedModifier));
            effectiveComponents.Add(component.ConceptKey, effectiveValue);
            componentTotal += effectiveValue;
        }

        // Integer division of signed integers truncates toward zero, which is the required
        // composition behavior for both positive and negative competency values.
        var mean = componentTotal / definition.Components.Count;
        long explicitParentModifier = 0;
        foreach (var modifier in modifiers.Where(value =>
                     string.Equals(value.TargetConceptKey, definition.Parent.ConceptKey, StringComparison.Ordinal)))
        {
            explicitParentModifier += modifier.Value;
        }

        return new CompositeCompetencyEvaluation(
            checked((int)(mean + explicitParentModifier)),
            checked((int)explicitParentModifier),
            effectiveComponents);
    }
}

public static class KnownMechanicalRelationships
{
    private static readonly IReadOnlyList<MechanicalRelationshipDefinition> Definitions =
    [
        Composite("skill-composite.stealth", "Stealth", ["Hide", "Move Silently"]),
        Composite("skill-composite.perception", "Perception", ["Listen", "Spot"]),
        Composite("skill-composite.athletics", "Athletics", ["Climb", "Jump", "Swim"]),
        Composite("skill-composite.acrobatics", "Acrobatics", ["Balance", "Tumble"])
    ];

    public static IReadOnlyList<MechanicalRelationshipDefinition> All => Definitions;

    public static MechanicalRelationshipDefinition? FindByKey(string relationshipKey) =>
        Definitions.SingleOrDefault(value =>
            string.Equals(value.Key, relationshipKey?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<MechanicalRelationshipDefinition> FindAllByConceptKey(string conceptKey)
    {
        var normalizedKey = conceptKey?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return [];
        }

        return Definitions
            .Where(value =>
                string.Equals(value.Parent.ConceptKey, normalizedKey, StringComparison.OrdinalIgnoreCase)
                || value.Components.Any(component =>
                    string.Equals(component.ConceptKey, normalizedKey, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static MechanicalRelationshipDefinition Composite(
        string key,
        string parent,
        IReadOnlyList<string> components) =>
        new(
            key,
            MechanicalRelationshipKinds.CompositeSkill,
            Skill(parent),
            components.Select(Skill).ToArray(),
            MechanicalRelationshipCompositionKinds.ArithmeticMean,
            MechanicalRelationshipDirections.ComponentsToParent);

    private static MechanicalCompetencyReference Skill(string displayName) =>
        new($"skill.{Slug(displayName)}", "skill", displayName);

    private static string Slug(string value) =>
        string.Join(
            '-',
            value.Trim().ToLowerInvariant()
                .Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
