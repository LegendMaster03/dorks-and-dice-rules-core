using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

internal static class CharacterStructuredProficiencyProjector
{
    public static void Project(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement container,
        string pathPrefix)
    {
        ProjectBooleanQualifications(
            rule,
            context,
            container,
            "weaponProficiencies",
            "weapons",
            pathPrefix);
        ProjectBooleanQualifications(
            rule,
            context,
            container,
            "armorProficiencies",
            "armor",
            pathPrefix);
        ProjectTools(
            rule,
            context,
            container,
            pathPrefix);
    }

    private static void ProjectBooleanQualifications(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement container,
        string fieldName,
        string category,
        string pathPrefix)
    {
        if (!CharacterProjectionJson.TryGetProperty(container, fieldName, out var collection))
        {
            return;
        }

        var entries = collection.ValueKind == JsonValueKind.Array
            ? collection.EnumerateArray().ToArray()
            : collection.ValueKind == JsonValueKind.Object
                ? [collection]
                : [];

        var entryIndex = 0;
        foreach (var entry in entries)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                entryIndex++;
                continue;
            }

            foreach (var property in entry.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.True)
                {
                    AddQualification(
                        rule,
                        context,
                        category,
                        property.Name,
                        CharacterResolutionStates.Resolved,
                        true);
                    continue;
                }

                if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    var key =
                        $"qualification.{category}.source-expression.{Slug(property.Name)}.{entryIndex}";
                    context.Qualifications[key] = new CharacterQualificationView(
                        key,
                        category,
                        CharacterProjectionJson.Humanize(property.Name),
                        null,
                        CharacterResolutionStates.ApplicableUnresolved,
                        [rule.Catalog.ConceptKey],
                        rule.Provenance);
                    context.Conflicts.Add(new CharacterProjectionConflictView(
                        $"conflict.{pathPrefix}.{fieldName}.{entryIndex}.{Slug(property.Name)}",
                        "source-expression-unresolved",
                        $"{rule.Catalog.DisplayName} defines {category} proficiency '{property.Name}' with a structured source expression that Rules Core preserves but does not broaden into implicit grants.",
                        [],
                        [rule.Catalog.ConceptKey]));
                }
            }

            entryIndex++;
        }
    }

    private static void ProjectTools(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement container,
        string pathPrefix)
    {
        if (!CharacterProjectionJson.TryGetProperty(container, "toolProficiencies", out var collection))
        {
            return;
        }

        var entries = collection.ValueKind == JsonValueKind.Array
            ? collection.EnumerateArray().ToArray()
            : collection.ValueKind == JsonValueKind.Object
                ? [collection]
                : [];

        var groupIndex = 0;
        foreach (var entry in entries)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                groupIndex++;
                continue;
            }

            foreach (var property in entry.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.True)
                {
                    context.AddToolTraining(
                        context.ResolveToolChoiceOption(property.Name),
                        rule.Catalog.ConceptKey,
                        rule.Provenance);
                    continue;
                }

                if (property.Value.ValueKind == JsonValueKind.Number
                    && property.Value.TryGetInt32(out var count)
                    && count > 0)
                {
                    var options = ExpandToolChoiceOptions(
                        context,
                        property.Name);
                    CharacterStartingProficiencyProjector.ProjectChoiceGroup(
                        rule,
                        context,
                        $"{pathPrefix}.tool-proficiencies.{Slug(property.Name)}",
                        groupIndex,
                        count,
                        options,
                        property.Name,
                        kind: "tool-proficiency",
                        optionLabel: "Tool Proficiency",
                        onSelected: selected => context.AddToolTraining(
                            selected,
                            rule.Catalog.ConceptKey,
                            rule.Provenance));
                    continue;
                }

                if (string.Equals(property.Name, "choose", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.Object)
                {
                    ProjectToolChoose(
                        rule,
                        context,
                        property.Value,
                        pathPrefix,
                        groupIndex);
                }
            }

            groupIndex++;
        }
    }

    private static void ProjectToolChoose(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement choose,
        string pathPrefix,
        int groupIndex)
    {
        var count = CharacterProjectionJson.Integer(choose, "count") ?? 1;
        if (count <= 0
            || !CharacterProjectionJson.TryGetProperty(choose, "from", out var from)
            || from.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var sourceValues = from.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
        var forceSourceUnavailable = sourceValues
            .Where(IsToolChoiceCategoryToken)
            .Any(value => ExpandToolChoiceOptions(context, value).Count == 0);
        var options = sourceValues
            .SelectMany(value => ExpandToolChoiceOptions(context, value))
            .GroupBy(
                value => value.ConceptKey ?? value.Value,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();

        CharacterStartingProficiencyProjector.ProjectChoiceGroup(
            rule,
            context,
            $"{pathPrefix}.tool-proficiencies",
            groupIndex,
            count,
            options,
            "choose.from",
            kind: "tool-proficiency",
            optionLabel: "Tool Proficiency",
            onSelected: selected => context.AddToolTraining(
                selected,
                rule.Catalog.ConceptKey,
                rule.Provenance),
            forceSourceUnavailable: forceSourceUnavailable);
    }

    private static bool IsToolChoiceCategoryToken(string sourceValue) =>
        sourceValue.Trim().ToLowerInvariant() is
            "anytool" or
            "anyartisanstool" or
            "anymusicalinstrument" or
            "anygamingset";

    private static IReadOnlyList<CharacterChoiceOptionView> ExpandToolChoiceOptions(
        CharacterProjectionContext context,
        string sourceValue)
    {
        if (string.IsNullOrWhiteSpace(sourceValue))
        {
            return [];
        }

        return sourceValue.Trim().ToLowerInvariant() switch
        {
            "anytool" => context.AllToolChoiceOptions(),
            "anyartisanstool" => context.ToolChoiceOptionsForCategory("artisans-tool"),
            "anymusicalinstrument" => context.ToolChoiceOptionsForCategory("musical-instrument"),
            "anygamingset" => context.ToolChoiceOptionsForCategory("gaming-set"),
            _ => [context.ResolveToolChoiceOption(sourceValue)]
        };
    }

    private static void AddQualification(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        string category,
        string displayName,
        string state,
        bool? isQualified)
    {
        var key = $"qualification.{category}.{Slug(displayName)}";
        context.Qualifications[key] = new CharacterQualificationView(
            key,
            category,
            displayName,
            isQualified,
            state,
            [rule.Catalog.ConceptKey],
            rule.Provenance);
        if (isQualified == true)
        {
            context.AddCapability(
                key,
                displayName,
                rule.Catalog.ConceptKey,
                rule.Provenance);
        }
    }

    private static string Slug(string value) =>
        string.Join(
            '-',
            value.Trim().ToLowerInvariant()
                .Split(
                    [' ', '/', '_', '-', '.', '|', '\''],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
