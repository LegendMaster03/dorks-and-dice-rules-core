using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Projects source-derived Subclass acquisition choices from compatible Subclass feature progression.
/// The earliest source-native Subclass feature level is treated as the acquisition threshold instead
/// of imposing an edition-specific level in Character consumers.
/// </summary>
internal static class CharacterSubclassAdvancementProjector
{
    internal static void Project(
        IReadOnlyList<CharacterProjectionRule> rules,
        CharacterProjectionContext context)
    {
        var ruleByConcept = rules.ToDictionary(
            rule => rule.Catalog.ConceptKey,
            StringComparer.OrdinalIgnoreCase);
        var subclassRules = rules
            .Where(rule => string.Equals(
                rule.Catalog.EntityType,
                "subclass",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var classAdvancement in (context.Request.Advancements ?? [])
                     .Where(value => value.Level > 0)
                     .Where(value =>
                         ruleByConcept.TryGetValue(value.ConceptKey, out var rule)
                         && string.Equals(
                             rule.Catalog.EntityType,
                             "class",
                             StringComparison.OrdinalIgnoreCase))
                     .GroupBy(value => value.ConceptKey, StringComparer.OrdinalIgnoreCase)
                     .Select(group => new
                     {
                         ConceptKey = group.Key,
                         Level = group.Sum(value => Math.Max(value.Level, 0))
                     }))
        {
            if (!ruleByConcept.TryGetValue(classAdvancement.ConceptKey, out var classRule))
            {
                continue;
            }

            var compatible = subclassRules
                .Where(rule => rule.Catalog.Relationships.Any(relationship =>
                    string.Equals(relationship.Kind, "parent-class", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(relationship.RelatedEntityType, "class", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        relationship.RelatedConceptKey,
                        classAdvancement.ConceptKey,
                        StringComparison.OrdinalIgnoreCase)))
                .Select(rule => new SubclassCandidate(rule, AcquisitionLevel(rule)))
                .ToArray();
            if (compatible.Length == 0)
            {
                continue;
            }

            var structurallySelected = (context.Request.Advancements ?? [])
                .Where(value => value.Level > 0)
                .Where(value => !string.IsNullOrWhiteSpace(value.ParentConceptKey))
                .FirstOrDefault(value =>
                    string.Equals(
                        value.ParentConceptKey,
                        classAdvancement.ConceptKey,
                        StringComparison.OrdinalIgnoreCase)
                    && ruleByConcept.TryGetValue(value.ConceptKey, out var selectedRule)
                    && string.Equals(
                        selectedRule.Catalog.EntityType,
                        "subclass",
                        StringComparison.OrdinalIgnoreCase));

            var choiceKey = $"advancement.{classAdvancement.ConceptKey}.subclass";
            var groupKey = $"choice-group.{classAdvancement.ConceptKey}.subclass";
            var available = compatible
                .Where(candidate =>
                    candidate.AcquisitionLevel is int acquisitionLevel
                    && acquisitionLevel <= classAdvancement.Level)
                .OrderBy(candidate => candidate.Rule.Catalog.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Rule.Catalog.ConceptKey, StringComparer.Ordinal)
                .ToArray();
            var options = available
                .Select(candidate => new CharacterChoiceOptionView(
                    candidate.Rule.Catalog.ConceptKey,
                    candidate.Rule.Catalog.DisplayName,
                    candidate.Rule.Catalog.ConceptKey))
                .ToArray();

            if (structurallySelected is not null)
            {
                var selectedCandidate = compatible.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Rule.Catalog.ConceptKey,
                        structurallySelected.ConceptKey,
                        StringComparison.OrdinalIgnoreCase));
                if (selectedCandidate is null)
                {
                    AddConflict(
                        context,
                        classAdvancement.ConceptKey,
                        structurallySelected.ConceptKey,
                        "subclass-parent-mismatch",
                        $"Selected Subclass '{structurallySelected.ConceptKey}' is not compatible with {classRule.Catalog.DisplayName}.");
                    continue;
                }
                if (selectedCandidate.AcquisitionLevel is not int acquisitionLevel)
                {
                    AddConflict(
                        context,
                        classAdvancement.ConceptKey,
                        structurallySelected.ConceptKey,
                        "subclass-acquisition-unresolved",
                        $"Rules Core can not determine the source-native acquisition level for {selectedCandidate.Rule.Catalog.DisplayName}.");
                    continue;
                }

                var satisfied = classAdvancement.Level >= acquisitionLevel;
                context.ChoiceViews[choiceKey] = new CharacterChoiceView(
                    choiceKey,
                    groupKey,
                    $"{classRule.Catalog.DisplayName} Subclass",
                    "subclass",
                    satisfied
                        ? CharacterResolutionStates.Resolved
                        : CharacterResolutionStates.ChoiceRequired,
                    options,
                    selectedCandidate.Rule.Catalog.ConceptKey,
                    classRule.Catalog.ConceptKey,
                    classRule.Provenance);
                if (!satisfied)
                {
                    AddConflict(
                        context,
                        classAdvancement.ConceptKey,
                        selectedCandidate.Rule.Catalog.ConceptKey,
                        "subclass-acquisition-level",
                        $"{selectedCandidate.Rule.Catalog.DisplayName} becomes available at {classRule.Catalog.DisplayName} level {acquisitionLevel}; the current Class level is {classAdvancement.Level}.");
                }
                continue;
            }

            if (options.Length == 0)
            {
                continue;
            }

            if (!context.Choices.TryGetValue(choiceKey, out var supplied))
            {
                context.ChoiceViews[choiceKey] = new CharacterChoiceView(
                    choiceKey,
                    groupKey,
                    $"{classRule.Catalog.DisplayName} Subclass",
                    "subclass",
                    CharacterResolutionStates.ChoiceRequired,
                    options,
                    null,
                    classRule.Catalog.ConceptKey,
                    classRule.Provenance);
                continue;
            }

            var selected = options.FirstOrDefault(option =>
                string.Equals(option.Value, supplied, StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.ConceptKey, supplied, StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.DisplayName, supplied, StringComparison.OrdinalIgnoreCase));
            if (selected is null)
            {
                context.ChoiceViews[choiceKey] = new CharacterChoiceView(
                    choiceKey,
                    groupKey,
                    $"{classRule.Catalog.DisplayName} Subclass",
                    "subclass",
                    CharacterResolutionStates.ChoiceRequired,
                    options,
                    supplied,
                    classRule.Catalog.ConceptKey,
                    classRule.Provenance);
                AddConflict(
                    context,
                    classAdvancement.ConceptKey,
                    supplied,
                    "invalid-runtime-choice",
                    $"'{supplied}' is not an available Subclass for {classRule.Catalog.DisplayName} at level {classAdvancement.Level}.");
                continue;
            }

            context.ChoiceViews[choiceKey] = new CharacterChoiceView(
                choiceKey,
                groupKey,
                $"{classRule.Catalog.DisplayName} Subclass",
                "subclass",
                CharacterResolutionStates.Resolved,
                options,
                selected.Value,
                classRule.Catalog.ConceptKey,
                classRule.Provenance);
        }
    }

    private static int? AcquisitionLevel(CharacterProjectionRule rule) =>
        ClassFamilyFeatureReferenceParser.Project(rule.Catalog.EntityType, rule.Document)
            .Where(feature => feature.Level is > 0)
            .Select(feature => feature.Level!.Value)
            .DefaultIfEmpty()
            .Min() is var minimum && minimum > 0
                ? minimum
                : null;

    private static void AddConflict(
        CharacterProjectionContext context,
        string classConceptKey,
        string subclassConceptKey,
        string kind,
        string message)
    {
        var conflictKey = $"conflict.advancement.{classConceptKey}.subclass.{kind}";
        if (context.Conflicts.Any(value =>
                string.Equals(value.ConflictKey, conflictKey, StringComparison.Ordinal)))
        {
            return;
        }

        context.Conflicts.Add(new CharacterProjectionConflictView(
            conflictKey,
            kind,
            message,
            [],
            [classConceptKey, subclassConceptKey]));
    }

    private sealed record SubclassCandidate(
        CharacterProjectionRule Rule,
        int? AcquisitionLevel);
}
