using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Projects the effective Subclass decision created by a parent Class progression.
/// Character consumers receive only the resolved choice; source-native progression details stay
/// inside Rules Core.
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
                             StringComparison.OrdinalIgnoreCase)))
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
                .Select(rule => new SubclassCandidate(rule, AcquisitionLevel(rule.Catalog)))
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
                    && ParentOccurrenceMatches(value, classAdvancement)
                    && ruleByConcept.TryGetValue(value.ConceptKey, out var selectedRule)
                    && string.Equals(
                        selectedRule.Catalog.EntityType,
                        "subclass",
                        StringComparison.OrdinalIgnoreCase));

            var choiceKey = ChoiceKey(classAdvancement);
            var groupKey = $"choice-group.{choiceKey}";
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
                ProjectStructuralSelection(
                    context,
                    classRule,
                    classAdvancement,
                    compatible,
                    structurallySelected,
                    choiceKey,
                    groupKey,
                    options);
                continue;
            }

            if (options.Length == 0)
            {
                continue;
            }

            if (!context.Choices.TryGetValue(choiceKey, out var supplied))
            {
                context.ChoiceViews[choiceKey] = Choice(
                    classRule,
                    choiceKey,
                    groupKey,
                    CharacterResolutionStates.ChoiceRequired,
                    options,
                    selectedValue: null);
                continue;
            }

            var selected = options.FirstOrDefault(option =>
                string.Equals(option.Value, supplied, StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.ConceptKey, supplied, StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.DisplayName, supplied, StringComparison.OrdinalIgnoreCase));
            if (selected is null)
            {
                context.ChoiceViews[choiceKey] = Choice(
                    classRule,
                    choiceKey,
                    groupKey,
                    CharacterResolutionStates.ChoiceRequired,
                    options,
                    supplied);
                AddConflict(
                    context,
                    classAdvancement,
                    supplied,
                    "invalid-runtime-choice",
                    $"'{supplied}' is not an available Subclass for {classRule.Catalog.DisplayName} at level {classAdvancement.Level}.");
                continue;
            }

            context.ChoiceViews[choiceKey] = Choice(
                classRule,
                choiceKey,
                groupKey,
                CharacterResolutionStates.Resolved,
                options,
                selected.Value);
        }
    }

    private static void ProjectStructuralSelection(
        CharacterProjectionContext context,
        CharacterProjectionRule classRule,
        CharacterAdvancementFactInput classAdvancement,
        IReadOnlyList<SubclassCandidate> compatible,
        CharacterAdvancementFactInput structurallySelected,
        string choiceKey,
        string groupKey,
        IReadOnlyList<CharacterChoiceOptionView> options)
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
                classAdvancement,
                structurallySelected.ConceptKey,
                "subclass-parent-mismatch",
                $"Selected Subclass '{structurallySelected.ConceptKey}' is not compatible with {classRule.Catalog.DisplayName}.");
            return;
        }
        if (selectedCandidate.AcquisitionLevel is not int acquisitionLevel)
        {
            AddConflict(
                context,
                classAdvancement,
                structurallySelected.ConceptKey,
                "subclass-acquisition-unresolved",
                $"Rules Core can not determine the effective acquisition level for {selectedCandidate.Rule.Catalog.DisplayName}.");
            return;
        }

        var satisfied = classAdvancement.Level >= acquisitionLevel;
        context.ChoiceViews[choiceKey] = Choice(
            classRule,
            choiceKey,
            groupKey,
            satisfied
                ? CharacterResolutionStates.Resolved
                : CharacterResolutionStates.ChoiceRequired,
            options,
            selectedCandidate.Rule.Catalog.ConceptKey);
        if (!satisfied)
        {
            AddConflict(
                context,
                classAdvancement,
                selectedCandidate.Rule.Catalog.ConceptKey,
                "subclass-acquisition-level",
                $"{selectedCandidate.Rule.Catalog.DisplayName} becomes available at {classRule.Catalog.DisplayName} level {acquisitionLevel}; the current Class level is {classAdvancement.Level}.");
        }
    }

    private static bool ParentOccurrenceMatches(
        CharacterAdvancementFactInput subclassAdvancement,
        CharacterAdvancementFactInput classAdvancement)
    {
        if (string.IsNullOrWhiteSpace(subclassAdvancement.ParentOccurrenceKey))
        {
            return true;
        }

        return string.Equals(
            subclassAdvancement.ParentOccurrenceKey.Trim(),
            classAdvancement.OccurrenceKey?.Trim(),
            StringComparison.Ordinal);
    }

    private static CharacterChoiceView Choice(
        CharacterProjectionRule classRule,
        string choiceKey,
        string groupKey,
        string state,
        IReadOnlyList<CharacterChoiceOptionView> options,
        string? selectedValue) =>
        new(
            choiceKey,
            groupKey,
            $"{classRule.Catalog.DisplayName} Subclass",
            "subclass",
            state,
            options,
            selectedValue,
            classRule.Catalog.ConceptKey,
            classRule.Provenance);

    private static string ChoiceKey(CharacterAdvancementFactInput classAdvancement)
    {
        var occurrence = classAdvancement.OccurrenceKey?.Trim();
        return string.IsNullOrWhiteSpace(occurrence)
            ? $"advancement.{classAdvancement.ConceptKey}.subclass"
            : $"advancement.{classAdvancement.ConceptKey}.{occurrence}.subclass";
    }

    internal static int? AcquisitionLevel(ResolvedRuleCatalogItemView rule)
    {
        if (rule.Document is not JsonElement document
            || document.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (CharacterProjectionJson.TryGetProperty(document, "_rulesCore", out var rulesCore)
            && CharacterProjectionJson.TryGetProperty(rulesCore, "character", out var character))
        {
            var normalized = CharacterProjectionJson.Integer(character, "acquisitionLevel")
                ?? CharacterProjectionJson.Integer(character, "subclassAcquisitionLevel");
            if (normalized is > 0)
            {
                return normalized;
            }
        }

        var levels = ClassFamilyFeatureReferenceParser
            .Project(rule.EntityType, document)
            .Where(feature => feature.Level is > 0)
            .Select(feature => feature.Level!.Value)
            .ToArray();
        return levels.Length == 0 ? null : levels.Min();
    }

    private static void AddConflict(
        CharacterProjectionContext context,
        CharacterAdvancementFactInput classAdvancement,
        string subclassConceptKey,
        string kind,
        string message)
    {
        var occurrenceSegment = string.IsNullOrWhiteSpace(classAdvancement.OccurrenceKey)
            ? string.Empty
            : $".{classAdvancement.OccurrenceKey.Trim()}";
        var conflictKey = $"conflict.advancement.{classAdvancement.ConceptKey}{occurrenceSegment}.subclass.{kind}";
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
            [classAdvancement.ConceptKey, subclassConceptKey]));
    }

    private sealed record SubclassCandidate(
        CharacterProjectionRule Rule,
        int? AcquisitionLevel);
}
