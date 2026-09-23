using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

internal sealed class GenericCharacterRuleProjectionModule : ICharacterRuleProjectionModule
{
    public bool Handles(CharacterProjectionRule rule, CharacterProjectionContext context) =>
        context.IsSelected(rule.Catalog.ConceptKey)
        || context.ActiveConditions.Contains(rule.Catalog.ConceptKey)
        || rule.Catalog.ConceptKey.StartsWith("house.", StringComparison.OrdinalIgnoreCase);

    public void Project(CharacterProjectionRule rule, CharacterProjectionContext context)
    {
        if (context.IsSelected(rule.Catalog.ConceptKey)
            && !context.Features.ContainsKey($"feature.{rule.Catalog.ConceptKey}"))
        {
            context.AddFeature(
                $"feature.{rule.Catalog.ConceptKey}",
                rule.Catalog.DisplayName,
                rule.Catalog.EntityType,
                CharacterResolutionStates.Resolved,
                rule.Catalog.ConceptKey,
                rule.Provenance);
        }

        if (context.IsSelected(rule.Catalog.ConceptKey))
        {
            CharacterAbilityProjector.Project(
                rule,
                context);
            CharacterStartingProficiencyProjector.ProjectSourceSkills(
                rule,
                context,
                rule.Document);
            CharacterStructuredProficiencyProjector.Project(
                rule,
                context,
                rule.Document,
                "source-proficiencies");
            CharacterLanguageProficiencyProjector.Project(
                rule,
                context,
                rule.Document,
                "source-proficiencies");
        }

        if (context.ActiveConditions.Contains(rule.Catalog.ConceptKey))
        {
            context.AddFeature(
                $"condition.{rule.Catalog.ConceptKey}",
                rule.Catalog.DisplayName,
                "condition",
                CharacterResolutionStates.ApplicableUnresolved,
                rule.Catalog.ConceptKey,
                rule.Provenance);
        }

        ProjectPrerequisites(rule, context);
        ProjectNormalizedCharacterExtension(rule, context);
        ProjectKnownHouseRule(rule, context);
    }

    private static void ProjectPrerequisites(
        CharacterProjectionRule rule,
        CharacterProjectionContext context)
    {
        if (!context.IsSelected(rule.Catalog.ConceptKey))
        {
            return;
        }

        if (!CharacterProjectionJson.TryGetProperty(rule.Document, "prerequisite", out var prerequisite)
            && !CharacterProjectionJson.TryGetProperty(rule.Document, "prerequisites", out prerequisite))
        {
            return;
        }

        context.Prerequisites[rule.Catalog.ConceptKey] = new CharacterPrerequisiteView(
            rule.Catalog.ConceptKey,
            CharacterResolutionStates.ApplicableUnresolved,
            null,
            [new CharacterPrerequisiteRequirementView(
                $"{rule.Catalog.ConceptKey}.source-prerequisite",
                "source-defined",
                null,
                null,
                null,
                null,
                null,
                CharacterResolutionStates.ApplicableUnresolved,
                "The source defines a prerequisite, but this source representation does not yet expose a normalized prerequisite expression.")],
            rule.Provenance);
    }

    private static void ProjectNormalizedCharacterExtension(
        CharacterProjectionRule rule,
        CharacterProjectionContext context)
    {
        if (!CharacterProjectionJson.TryGetProperty(rule.Document, "_rulesCore", out var rulesCore)
            || !CharacterProjectionJson.TryGetProperty(rulesCore, "character", out var character)
            || character.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var capability in CharacterProjectionJson.Strings(character, "capabilities"))
        {
            context.AddCapability(
                capability,
                CharacterProjectionJson.Humanize(capability),
                rule.Catalog.ConceptKey,
                rule.Provenance);
        }

        NormalizedCharacterPrerequisiteProjector.Project(rule, context, character);
        ProjectEffects(rule, context, character);
        ProjectMovement(rule, context, character);
        ProjectResources(rule, context, character);
        ProjectActions(rule, context, character);
        ProjectProcedures(rule, context, character);
        ProjectPassives(rule, context, character);
        ProjectQualifications(rule, context, character);
    }

    private static void ProjectEffects(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement character)
    {
        if (!CharacterProjectionJson.TryGetProperty(character, "effects", out var effects)
            || effects.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var item in effects.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                index++;
                continue;
            }

            var kind = CharacterProjectionJson.String(item, "kind") ?? CharacterEffectKinds.Other;
            var operation = CharacterProjectionJson.String(item, "operation") ?? CharacterEffectOperations.Add;
            var target = CharacterProjectionJson.String(item, "target");
            if (string.IsNullOrWhiteSpace(target))
            {
                index++;
                continue;
            }

            var effect = new CharacterRuleEffectView(
                CharacterProjectionJson.String(item, "key")
                    ?? $"{rule.Catalog.ConceptKey}.effect.{index}",
                kind,
                operation,
                target,
                CharacterProjectionJson.Integer(item, "value"),
                CharacterProjectionJson.String(item, "textValue"),
                CharacterProjectionJson.String(item, "condition"),
                rule.Catalog.ConceptKey,
                rule.Provenance);
            context.AddEffect(effect);
            index++;
        }
    }

    private static void ProjectMovement(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement character)
    {
        if (!CharacterProjectionJson.TryGetProperty(character, "movement", out var movement)
            || movement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in movement.EnumerateObject())
        {
            var value = property.Value.ValueKind == JsonValueKind.Number
                && property.Value.TryGetInt32(out var direct)
                    ? direct
                    : CharacterProjectionJson.Integer(property.Value, "value");
            var unit = property.Value.ValueKind == JsonValueKind.Object
                ? CharacterProjectionJson.String(property.Value, "unit")
                : null;
            var key = property.Name.StartsWith("movement.", StringComparison.OrdinalIgnoreCase)
                ? property.Name
                : $"movement.{property.Name}";
            context.Movement[key] = new CharacterMovementModeView(
                key,
                CharacterProjectionJson.Humanize(property.Name),
                value is null
                    ? CharacterResolutionStates.ApplicableUnresolved
                    : CharacterResolutionStates.Resolved,
                value,
                unit ?? "ft",
                value is null
                    ? []
                    : [new CharacterMechanicContributionView(
                        $"{rule.Catalog.ConceptKey}.{key}",
                        rule.Catalog.DisplayName,
                        CharacterEffectOperations.Set,
                        value,
                        null,
                        rule.Catalog.ConceptKey,
                        rule.Provenance)],
                rule.Provenance);
        }
    }

    private static void ProjectResources(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement character)
    {
        if (!CharacterProjectionJson.TryGetProperty(character, "resources", out var resources)
            || resources.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in resources.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var key = CharacterProjectionJson.String(item, "key");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }
            context.CurrentResources.TryGetValue(key, out var current);
            var maximum = CharacterProjectionJson.Integer(item, "maximum");
            context.Resources[key] = new CharacterResourceView(
                key,
                CharacterProjectionJson.String(item, "displayName") ?? CharacterProjectionJson.Humanize(key),
                maximum is null
                    ? CharacterResolutionStates.ApplicableUnresolved
                    : CharacterResolutionStates.Resolved,
                context.CurrentResources.ContainsKey(key) ? current : null,
                maximum,
                CharacterProjectionJson.String(item, "recoveryProcedure"),
                maximum is null
                    ? []
                    : [new CharacterMechanicContributionView(
                        $"{rule.Catalog.ConceptKey}.{key}.maximum",
                        rule.Catalog.DisplayName,
                        CharacterEffectOperations.Set,
                        maximum,
                        null,
                        rule.Catalog.ConceptKey,
                        rule.Provenance)],
                rule.Provenance);
        }
    }

    private static void ProjectActions(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement character)
    {
        if (!CharacterProjectionJson.TryGetProperty(character, "actions", out var actions)
            || actions.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in actions.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            var key = CharacterProjectionJson.String(item, "key");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            context.Actions[key] = new CharacterActionView(
                key,
                CharacterProjectionJson.String(item, "displayName") ?? CharacterProjectionJson.Humanize(key),
                CharacterProjectionJson.String(item, "actionType"),
                CharacterResolutionStates.Resolved,
                CharacterProjectionJson.String(item, "attackMechanic"),
                CharacterProjectionJson.String(item, "damage"),
                CharacterProjectionJson.String(item, "damageType"),
                CharacterProjectionJson.String(item, "range"),
                CharacterProjectionJson.String(item, "reach"),
                CharacterProjectionJson.String(item, "target"),
                CharacterProjectionJson.String(item, "resource"),
                CharacterProjectionJson.Integer(item, "resourceCost"),
                CharacterProjectionJson.Strings(item, "requiredCapabilities"),
                rule.Provenance);
        }
    }

    private static void ProjectProcedures(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement character)
    {
        if (!CharacterProjectionJson.TryGetProperty(character, "procedures", out var procedures)
            || procedures.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in procedures.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            var key = CharacterProjectionJson.String(item, "key");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var missingCapabilities = CharacterProjectionJson.Strings(item, "requiredCapabilities")
                .Where(value => !context.Capabilities.Contains(value))
                .ToArray();
            var missingFacts = CharacterProjectionJson.Strings(item, "requiredFacts")
                .Where(value => !context.IntegerFacts.ContainsKey(value)
                    && !context.BooleanFacts.ContainsKey(value)
                    && !context.StringFacts.ContainsKey(value))
                .ToArray();
            var requiredChoices = CharacterProjectionJson.Strings(item, "requiredChoices")
                .Where(value => !context.Choices.ContainsKey(value))
                .ToArray();
            var requiredRolls = CharacterProjectionJson.Strings(item, "requiredRolls")
                .Where(value => !context.Rolls.ContainsKey(value))
                .ToArray();

            var state = missingCapabilities.Length > 0
                ? CharacterResolutionStates.MissingCapability
                : requiredChoices.Length > 0
                    ? CharacterResolutionStates.ChoiceRequired
                    : requiredRolls.Length > 0
                        ? CharacterResolutionStates.RollRequired
                        : missingFacts.Length > 0
                            ? CharacterResolutionStates.MissingCharacterInput
                            : CharacterResolutionStates.Resolved;

            context.Procedures[key] = new CharacterProcedureView(
                key,
                CharacterProjectionJson.String(item, "displayName") ?? CharacterProjectionJson.Humanize(key),
                state,
                CharacterProjectionJson.String(item, "presentationRole"),
                CharacterProjectionJson.Strings(item, "requiredCapabilities"),
                CharacterProjectionJson.Strings(item, "requiredFacts"),
                CharacterProjectionJson.Strings(item, "requiredChoices"),
                CharacterProjectionJson.Strings(item, "requiredRolls"),
                ReadProcedureEffects(rule, item),
                rule.Provenance);
        }
    }

    private static IReadOnlyList<CharacterRuleEffectView> ReadProcedureEffects(
        CharacterProjectionRule rule,
        JsonElement procedure)
    {
        if (!CharacterProjectionJson.TryGetProperty(procedure, "effects", out var effects)
            || effects.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<CharacterRuleEffectView>();
        var index = 0;
        foreach (var item in effects.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                index++;
                continue;
            }
            var target = CharacterProjectionJson.String(item, "target");
            if (string.IsNullOrWhiteSpace(target))
            {
                index++;
                continue;
            }
            result.Add(new CharacterRuleEffectView(
                CharacterProjectionJson.String(item, "key")
                    ?? $"{rule.Catalog.ConceptKey}.procedure-effect.{index}",
                CharacterProjectionJson.String(item, "kind") ?? CharacterEffectKinds.Other,
                CharacterProjectionJson.String(item, "operation") ?? CharacterEffectOperations.Add,
                target,
                CharacterProjectionJson.Integer(item, "value"),
                CharacterProjectionJson.String(item, "textValue"),
                CharacterProjectionJson.String(item, "condition"),
                rule.Catalog.ConceptKey,
                rule.Provenance));
            index++;
        }
        return result;
    }

    private static void ProjectPassives(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement character)
    {
        if (!CharacterProjectionJson.TryGetProperty(character, "passives", out var passives)
            || passives.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in passives.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var key = CharacterProjectionJson.String(item, "key");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }
            var value = CharacterProjectionJson.Integer(item, "value");
            var textValue = CharacterProjectionJson.String(item, "textValue");
            var unit = CharacterProjectionJson.String(item, "unit");
            context.Mechanics[key] = new CharacterResolvedMechanicView(
                key,
                "passive",
                CharacterProjectionJson.String(item, "displayName") ?? CharacterProjectionJson.Humanize(key),
                value is null && string.IsNullOrWhiteSpace(textValue)
                    ? CharacterResolutionStates.ApplicableUnresolved
                    : CharacterResolutionStates.Resolved,
                value,
                textValue,
                unit,
                [],
                [],
                [],
                [],
                value is null
                    ? []
                    : [new CharacterMechanicContributionView(
                        $"{rule.Catalog.ConceptKey}.{key}",
                        rule.Catalog.DisplayName,
                        CharacterEffectOperations.Set,
                        value,
                        null,
                        rule.Catalog.ConceptKey,
                        rule.Provenance)],
                rule.Provenance);
        }
    }

    private static void ProjectQualifications(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement character)
    {
        if (!CharacterProjectionJson.TryGetProperty(character, "qualifications", out var qualifications)
            || qualifications.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in qualifications.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            var key = CharacterProjectionJson.String(item, "key");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }
            var category = CharacterProjectionJson.String(item, "category") ?? "other";
            context.Qualifications[key] = new CharacterQualificationView(
                key,
                category,
                CharacterProjectionJson.String(item, "displayName") ?? CharacterProjectionJson.Humanize(key),
                true,
                CharacterResolutionStates.Resolved,
                [rule.Catalog.ConceptKey],
                rule.Provenance);
            context.AddCapability(
                key,
                CharacterProjectionJson.Humanize(key),
                rule.Catalog.ConceptKey,
                rule.Provenance);
        }
    }

    private static void ProjectKnownHouseRule(
        CharacterProjectionRule rule,
        CharacterProjectionContext context)
    {
        var preparationRestriction = CharacterProjectionJson.String(
            rule.Document,
            "preparedSpellRestriction");
        if (!string.IsNullOrWhiteSpace(preparationRestriction))
        {
            const string key = "spellcasting.preparation-restriction";
            context.Mechanics[key] = new CharacterResolvedMechanicView(
                key,
                "spellcasting-policy",
                "Spell Preparation Restriction",
                CharacterResolutionStates.Resolved,
                null,
                preparationRestriction.Trim(),
                null,
                [],
                [],
                [],
                [],
                [new CharacterMechanicContributionView(
                    $"{rule.Catalog.ConceptKey}.prepared-spell-restriction",
                    rule.Catalog.DisplayName,
                    CharacterEffectOperations.Set,
                    null,
                    preparationRestriction.Trim(),
                    rule.Catalog.ConceptKey,
                    rule.Provenance)],
                rule.Provenance);
        }

        var mechanic = CharacterProjectionJson.String(rule.Document, "mechanic");
        var choosesResourceSystem =
            string.Equals(mechanic, "caster-resource-choice", StringComparison.OrdinalIgnoreCase)
            || CharacterProjectionJson.Boolean(
                rule.Document,
                "casterChoosesResourceSystem") == true;
        if (!choosesResourceSystem)
        {
            return;
        }

        var choiceKey = "spellcasting.resource-system";
        var available = CharacterProjectionJson.Strings(
                rule.Document,
                "availableResourceSystems")
            .Select(CharacterProjectionJson.NormalizeResourceSystemKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hasChoice = context.Choices.TryGetValue(choiceKey, out var rawChoice);
        var choice = hasChoice
            ? CharacterProjectionJson.NormalizeResourceSystemKey(rawChoice!)
            : null;
        var valid = choice is null
            || available.Length == 0
            || available.Contains(choice, StringComparer.OrdinalIgnoreCase);

        if (choice is not null && !valid)
        {
            context.Conflicts.Add(new CharacterProjectionConflictView(
                "conflict.spellcasting.resource-system",
                "invalid-runtime-choice",
                $"Spellcasting resource system '{rawChoice}' is not allowed by the effective house rule.",
                [],
                [rule.Catalog.ConceptKey]));
        }

        context.Spellcasting["spellcasting.resource-choice"] =
            new CharacterSpellcastingView(
                "spellcasting.resource-choice",
                rule.Catalog.DisplayName,
                !hasChoice || !valid
                    ? CharacterResolutionStates.ChoiceRequired
                    : CharacterResolutionStates.Resolved,
                null,
                valid ? choice : null,
                null,
                null,
                [],
                !hasChoice || !valid ? [choiceKey] : [],
                rule.Provenance);
    }
}
