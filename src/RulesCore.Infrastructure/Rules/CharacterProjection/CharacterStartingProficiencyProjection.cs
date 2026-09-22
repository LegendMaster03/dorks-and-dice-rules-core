using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

internal static class CharacterStartingProficiencyProjector
{
    public static void ProjectSkills(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement proficiencies)
    {
        if (!CharacterProjectionJson.TryGetProperty(proficiencies, "skills", out var skills))
        {
            return;
        }

        ProjectSkillCollection(
            rule,
            context,
            skills,
            "starting-proficiencies.skills");
    }

    public static void ProjectSourceSkills(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement document)
    {
        if (!CharacterProjectionJson.TryGetProperty(document, "skillProficiencies", out var skills))
        {
            return;
        }

        ProjectSkillCollection(
            rule,
            context,
            skills,
            "skill-proficiencies");
    }

    private static void ProjectSkillCollection(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement skills,
        string pathKey)
    {
        if (skills.ValueKind == JsonValueKind.String)
        {
            ProjectDirectSkill(rule, context, skills.GetString());
            return;
        }

        if (skills.ValueKind == JsonValueKind.Object)
        {
            ProjectSkillObject(
                rule,
                context,
                skills,
                pathKey,
                groupIndex: 0);
            return;
        }

        if (skills.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var groupIndex = 0;
        foreach (var entry in skills.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                ProjectDirectSkill(rule, context, entry.GetString());
                groupIndex++;
                continue;
            }

            if (entry.ValueKind == JsonValueKind.Object)
            {
                ProjectSkillObject(
                    rule,
                    context,
                    entry,
                    pathKey,
                    groupIndex);
            }
            groupIndex++;
        }
    }

    private static void ProjectSkillObject(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement entry,
        string pathKey,
        int groupIndex)
    {
        foreach (var property in entry.EnumerateObject())
        {
            if (string.Equals(property.Name, "any", StringComparison.OrdinalIgnoreCase)
                || string.Equals(property.Name, "choose", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.True)
            {
                ProjectDirectSkill(rule, context, property.Name);
            }
        }

        if (TryReadChoice(
                entry,
                context,
                out var count,
                out var options,
                out var sourceShape))
        {
            ProjectChoiceGroup(
                rule,
                context,
                pathKey,
                groupIndex,
                count,
                options,
                sourceShape);
        }
    }

    private static void ProjectDirectSkill(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        context.AddSkillTraining(
            context.ResolveSkillChoiceOption(raw),
            rule.Catalog.ConceptKey,
            rule.Provenance);
    }

    private static bool TryReadChoice(
        JsonElement entry,
        CharacterProjectionContext context,
        out int count,
        out IReadOnlyList<CharacterChoiceOptionView> options,
        out string sourceShape)
    {
        count = 0;
        options = [];
        sourceShape = "unknown";

        if (CharacterProjectionJson.Integer(entry, "any") is int anyCount
            && anyCount > 0)
        {
            count = anyCount;
            options = context.AllSkillChoiceOptions();
            sourceShape = "any";
            return true;
        }

        if (!CharacterProjectionJson.TryGetProperty(entry, "choose", out var choose)
            || choose.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        count = CharacterProjectionJson.Integer(choose, "count") ?? 1;
        if (count <= 0)
        {
            return false;
        }

        if (!CharacterProjectionJson.TryGetProperty(choose, "from", out var from)
            || from.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        options = from.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => context.ResolveSkillChoiceOption(value!))
            .GroupBy(
                value => value.ConceptKey ?? value.Value,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        sourceShape = "from";
        return true;
    }

    private static void ProjectChoiceGroup(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        string pathKey,
        int groupIndex,
        int count,
        IReadOnlyList<CharacterChoiceOptionView> options,
        string sourceShape)
    {
        var groupKey =
            $"choice-group.{rule.Catalog.ConceptKey}.{pathKey}.{groupIndex}";
        var selectedIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceUnavailable = options.Count < count;

        if (sourceUnavailable)
        {
            context.Conflicts.Add(new CharacterProjectionConflictView(
                $"conflict.{groupKey}.options",
                "source-unavailable",
                $"{rule.Catalog.DisplayName} requires {count} skill proficiency choice(s), but only {options.Count} available option(s) can be represented for source choice shape '{sourceShape}'.",
                [],
                [rule.Catalog.ConceptKey]));
        }

        for (var slot = 0; slot < count; slot++)
        {
            var choiceKey =
                $"choice.{rule.Catalog.ConceptKey}.{pathKey}.{groupIndex}.{slot}";
            var displayName = count == 1
                ? $"{rule.Catalog.DisplayName} Skill Proficiency"
                : $"{rule.Catalog.DisplayName} Skill Proficiency {slot + 1} of {count}";

            if (sourceUnavailable)
            {
                context.ChoiceViews[choiceKey] = new CharacterChoiceView(
                    choiceKey,
                    groupKey,
                    displayName,
                    "skill-proficiency",
                    CharacterResolutionStates.SourceUnavailable,
                    options,
                    null,
                    rule.Catalog.ConceptKey,
                    rule.Provenance);
                continue;
            }

            if (!context.Choices.TryGetValue(choiceKey, out var supplied))
            {
                context.ChoiceViews[choiceKey] = new CharacterChoiceView(
                    choiceKey,
                    groupKey,
                    displayName,
                    "skill-proficiency",
                    CharacterResolutionStates.ChoiceRequired,
                    options,
                    null,
                    rule.Catalog.ConceptKey,
                    rule.Provenance);
                continue;
            }

            var selected = FindOption(options, supplied);
            if (selected is null)
            {
                context.ChoiceViews[choiceKey] = new CharacterChoiceView(
                    choiceKey,
                    groupKey,
                    displayName,
                    "skill-proficiency",
                    CharacterResolutionStates.ChoiceRequired,
                    options,
                    supplied,
                    rule.Catalog.ConceptKey,
                    rule.Provenance);
                context.Conflicts.Add(new CharacterProjectionConflictView(
                    $"conflict.{choiceKey}",
                    "invalid-runtime-choice",
                    $"'{supplied}' is not an available option for '{displayName}'.",
                    [],
                    [rule.Catalog.ConceptKey]));
                continue;
            }

            var identity = selected.ConceptKey ?? selected.Value;
            if (!selectedIdentities.Add(identity))
            {
                context.ChoiceViews[choiceKey] = new CharacterChoiceView(
                    choiceKey,
                    groupKey,
                    displayName,
                    "skill-proficiency",
                    CharacterResolutionStates.ChoiceRequired,
                    options,
                    selected.Value,
                    rule.Catalog.ConceptKey,
                    rule.Provenance);
                context.Conflicts.Add(new CharacterProjectionConflictView(
                    $"conflict.{choiceKey}.duplicate",
                    "duplicate-runtime-choice",
                    $"'{selected.DisplayName}' can not satisfy more than one selection in '{groupKey}'.",
                    [],
                    [rule.Catalog.ConceptKey]));
                continue;
            }

            context.ChoiceViews[choiceKey] = new CharacterChoiceView(
                choiceKey,
                groupKey,
                displayName,
                "skill-proficiency",
                CharacterResolutionStates.Resolved,
                options,
                selected.Value,
                rule.Catalog.ConceptKey,
                rule.Provenance);
            context.AddSkillTraining(
                selected,
                rule.Catalog.ConceptKey,
                rule.Provenance);
        }
    }

    private static CharacterChoiceOptionView? FindOption(
        IReadOnlyList<CharacterChoiceOptionView> options,
        string supplied) =>
        options.FirstOrDefault(option =>
            string.Equals(option.Value, supplied, StringComparison.OrdinalIgnoreCase)
            || string.Equals(option.ConceptKey, supplied, StringComparison.OrdinalIgnoreCase)
            || string.Equals(option.DisplayName, supplied, StringComparison.OrdinalIgnoreCase));
}
