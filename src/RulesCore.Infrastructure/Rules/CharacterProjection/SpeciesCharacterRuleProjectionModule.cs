using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

internal sealed class SpeciesCharacterRuleProjectionModule : ICharacterRuleProjectionModule
{
    public bool Handles(CharacterProjectionRule rule, CharacterProjectionContext context) =>
        context.IsSelected(rule.Catalog.ConceptKey)
        && (string.Equals(rule.Catalog.EntityType, RuleConceptEntityTypes.Species, StringComparison.OrdinalIgnoreCase)
            || string.Equals(rule.Catalog.EntityType, RuleConceptEntityTypes.Subspecies, StringComparison.OrdinalIgnoreCase));

    public void Project(CharacterProjectionRule rule, CharacterProjectionContext context)
    {
        context.AddFeature(
            $"feature.{rule.Catalog.ConceptKey}",
            rule.Catalog.DisplayName,
            string.Equals(rule.Catalog.EntityType, RuleConceptEntityTypes.Subspecies, StringComparison.OrdinalIgnoreCase)
                ? "subspecies"
                : "species",
            CharacterResolutionStates.Resolved,
            rule.Catalog.ConceptKey,
            rule.Provenance,
            rule.Catalog.EntityType);

        ProjectSize(rule, context);
        ProjectMovement(rule, context);
    }

    private static void ProjectSize(
        CharacterProjectionRule rule,
        CharacterProjectionContext context)
    {
        if (!CharacterProjectionJson.TryGetProperty(rule.Document, "size", out var size))
        {
            return;
        }

        var values = size.ValueKind switch
        {
            JsonValueKind.String => new[] { size.GetString() },
            JsonValueKind.Array => size.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString())
                .ToArray(),
            _ => []
        };
        var normalized = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => CharacterProjectionContext.NormalizeSizeCategory(value!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalized.Length == 0)
        {
            return;
        }

        if (normalized.Length == 1)
        {
            context.AddSizeCategory(
                normalized[0],
                rule.Catalog.ConceptKey,
                rule.Provenance);
            return;
        }

        var choiceKey = $"choice.{rule.Catalog.ConceptKey}.size-category";
        var options = normalized
            .Select(value => new CharacterChoiceOptionView(
                value,
                value,
                rule.Catalog.ConceptKey))
            .ToArray();

        if (!context.Choices.TryGetValue(choiceKey, out var selected))
        {
            context.ChoiceViews[choiceKey] = new CharacterChoiceView(
                choiceKey,
                $"choice-group.{rule.Catalog.ConceptKey}.size-category",
                $"{rule.Catalog.DisplayName} Size",
                "size-category",
                CharacterResolutionStates.ChoiceRequired,
                options,
                null,
                rule.Catalog.ConceptKey,
                rule.Provenance);
            context.Mechanics["character.size-category"] = new CharacterResolvedMechanicView(
                "character.size-category",
                "character-metadata",
                "Size",
                CharacterResolutionStates.ChoiceRequired,
                null,
                null,
                null,
                [],
                [],
                [choiceKey],
                [],
                [],
                rule.Provenance);
            return;
        }

        var normalizedSelection = CharacterProjectionContext.NormalizeSizeCategory(selected);
        var resolved = normalized.FirstOrDefault(value => string.Equals(
            value,
            normalizedSelection,
            StringComparison.OrdinalIgnoreCase));
        if (resolved is null)
        {
            context.ChoiceViews[choiceKey] = new CharacterChoiceView(
                choiceKey,
                $"choice-group.{rule.Catalog.ConceptKey}.size-category",
                $"{rule.Catalog.DisplayName} Size",
                "size-category",
                CharacterResolutionStates.Conflict,
                options,
                selected,
                rule.Catalog.ConceptKey,
                rule.Provenance);
            context.Mechanics["character.size-category"] = new CharacterResolvedMechanicView(
                "character.size-category",
                "character-metadata",
                "Size",
                CharacterResolutionStates.Conflict,
                null,
                null,
                null,
                [],
                [],
                [choiceKey],
                [],
                [],
                rule.Provenance);
            context.Conflicts.Add(new CharacterProjectionConflictView(
                $"conflict.{choiceKey}",
                "invalid-choice",
                $"Size choice '{selected}' is not allowed by '{rule.Catalog.DisplayName}'.",
                ["character.size-category"],
                [rule.Catalog.ConceptKey]));
            return;
        }

        context.ChoiceViews[choiceKey] = new CharacterChoiceView(
            choiceKey,
            $"choice-group.{rule.Catalog.ConceptKey}.size-category",
            $"{rule.Catalog.DisplayName} Size",
            "size-category",
            CharacterResolutionStates.Resolved,
            options,
            resolved,
            rule.Catalog.ConceptKey,
            rule.Provenance);
        context.AddSizeCategory(resolved, rule.Catalog.ConceptKey, rule.Provenance);
    }

    private static void ProjectMovement(CharacterProjectionRule rule, CharacterProjectionContext context)
    {
        if (!CharacterProjectionJson.TryGetProperty(rule.Document, "speed", out var speed))
        {
            return;
        }

        if (speed.ValueKind == JsonValueKind.Number && speed.TryGetInt32(out var walk))
        {
            context.Movement["movement.walk"] = new CharacterMovementModeView(
                "movement.walk",
                "Walk",
                CharacterResolutionStates.Resolved,
                walk,
                "ft",
                [new CharacterMechanicContributionView(
                    $"{rule.Catalog.ConceptKey}.speed",
                    rule.Catalog.DisplayName,
                    CharacterEffectOperations.Set,
                    walk,
                    null,
                    rule.Catalog.ConceptKey,
                    rule.Provenance)],
                rule.Provenance);
            return;
        }

        if (speed.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in speed.EnumerateObject())
        {
            int? value = property.Value.ValueKind == JsonValueKind.Number
                && property.Value.TryGetInt32(out var number)
                    ? number
                    : CharacterProjectionJson.Integer(property.Value, "number")
                        ?? CharacterProjectionJson.Integer(property.Value, "amount");
            var key = $"movement.{property.Name.ToLowerInvariant()}";
            context.Movement[key] = new CharacterMovementModeView(
                key,
                CharacterProjectionJson.Humanize(property.Name),
                value is null
                    ? CharacterResolutionStates.ApplicableUnresolved
                    : CharacterResolutionStates.Resolved,
                value,
                "ft",
                value is null
                    ? []
                    : [new CharacterMechanicContributionView(
                        $"{rule.Catalog.ConceptKey}.speed.{property.Name}",
                        rule.Catalog.DisplayName,
                        CharacterEffectOperations.Set,
                        value,
                        null,
                        rule.Catalog.ConceptKey,
                        rule.Provenance)],
                rule.Provenance);
        }
    }
}
