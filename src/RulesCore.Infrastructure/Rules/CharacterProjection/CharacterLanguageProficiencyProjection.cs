using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

internal static class CharacterLanguageProficiencyProjector
{
    public static void Project(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement container,
        string pathPrefix)
    {
        if (!CharacterProjectionJson.TryGetProperty(
                container,
                "languageProficiencies",
                out var collection))
        {
            return;
        }

        var entries = collection.ValueKind == JsonValueKind.Array
            ? collection.EnumerateArray().ToArray()
            : collection.ValueKind == JsonValueKind.Object
                ? [collection]
                : [];

        for (var groupIndex = 0; groupIndex < entries.Length; groupIndex++)
        {
            var entry = entries[groupIndex];
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var properties = entry.EnumerateObject().ToArray();

            // Fixed languages must be known before category choices are expanded so
            // source property order can not make an already-known language selectable.
            foreach (var property in properties.Where(value =>
                         value.Value.ValueKind == JsonValueKind.True
                         && !IsLanguageChoiceCategoryToken(value.Name)
                         && !string.Equals(
                             value.Name,
                             "choose",
                             StringComparison.OrdinalIgnoreCase)))
            {
                context.AddLanguageKnowledge(
                    context.ResolveLanguageChoiceOption(property.Name),
                    rule.Catalog.ConceptKey,
                    rule.Provenance);
            }

            foreach (var property in properties)
            {
                if (string.Equals(
                        property.Name,
                        "choose",
                        StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.Object)
                {
                    ProjectChoose(
                        rule,
                        context,
                        property.Value,
                        pathPrefix,
                        groupIndex);
                    continue;
                }

                if (TryReadChoiceCount(property.Value, out var count)
                    && TryExpandCategory(
                        context,
                        property.Name,
                        out var options,
                        out var sourceUnavailable))
                {
                    CharacterStartingProficiencyProjector.ProjectChoiceGroup(
                        rule,
                        context,
                        $"{pathPrefix}.language-proficiencies.{Slug(property.Name)}",
                        groupIndex,
                        count,
                        options,
                        property.Name,
                        kind: "language-proficiency",
                        optionLabel: "Language Proficiency",
                        onSelected: selected => context.AddLanguageKnowledge(
                            selected,
                            rule.Catalog.ConceptKey,
                            rule.Provenance),
                        forceSourceUnavailable: sourceUnavailable);
                }
            }
        }
    }

    private static void ProjectChoose(
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

        var forceSourceUnavailable = false;
        var options = new List<CharacterChoiceOptionView>();
        foreach (var sourceValue in sourceValues)
        {
            if (TryExpandCategory(
                    context,
                    sourceValue,
                    out var categoryOptions,
                    out var categoryUnavailable))
            {
                options.AddRange(categoryOptions);
                forceSourceUnavailable |= categoryUnavailable;
                continue;
            }

            options.Add(context.ResolveLanguageChoiceOption(sourceValue));
        }

        var distinct = options
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
            $"{pathPrefix}.language-proficiencies",
            groupIndex,
            count,
            distinct,
            "choose.from",
            kind: "language-proficiency",
            optionLabel: "Language Proficiency",
            onSelected: selected => context.AddLanguageKnowledge(
                selected,
                rule.Catalog.ConceptKey,
                rule.Provenance),
            forceSourceUnavailable: forceSourceUnavailable);
    }

    private static bool IsLanguageChoiceCategoryToken(string sourceValue) =>
        sourceValue.Trim().ToLowerInvariant() is
            "any" or
            "anylanguage" or
            "anystandard" or
            "anyexotic" or
            "anyrare";

    private static bool TryExpandCategory(
        CharacterProjectionContext context,
        string sourceValue,
        out IReadOnlyList<CharacterChoiceOptionView> options,
        out bool sourceUnavailable)
    {
        options = [];
        sourceUnavailable = false;
        var normalized = sourceValue.Trim().ToLowerInvariant();

        switch (normalized)
        {
            case "any":
            case "anylanguage":
                options = context.AllLanguageChoiceOptions();
                break;
            case "anystandard":
                options = context.LanguageChoiceOptionsForCategory("standard");
                break;
            case "anyexotic":
                options = context.LanguageChoiceOptionsForCategory("exotic");
                break;
            case "anyrare":
                options = context.LanguageChoiceOptionsForCategory("rare");
                break;
            default:
                return false;
        }

        sourceUnavailable = options.Count == 0;
        return true;
    }

    private static bool TryReadChoiceCount(JsonElement value, out int count)
    {
        count = 0;
        if (value.ValueKind == JsonValueKind.True)
        {
            count = 1;
            return true;
        }
        return value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out count)
            && count > 0;
    }

    private static string Slug(string value) =>
        string.Join(
            '-',
            value.Trim().ToLowerInvariant()
                .Split(
                    [' ', '/', '_', '-', '.', '|', '\''],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
