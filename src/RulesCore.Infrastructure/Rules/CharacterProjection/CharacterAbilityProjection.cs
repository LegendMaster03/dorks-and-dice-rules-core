using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

internal static class CharacterAbilityProjector
{
    private static readonly string[] Abilities =
    [
        "strength",
        "dexterity",
        "constitution",
        "intelligence",
        "wisdom",
        "charisma"
    ];

    public static void Project(
        CharacterProjectionRule rule,
        CharacterProjectionContext context)
    {
        if (!context.IsSelected(rule.Catalog.ConceptKey)
            || !CharacterProjectionJson.TryGetProperty(rule.Document, "ability", out var ability)
            || ability.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var entries = ability.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.Object)
            .ToArray();
        if (entries.Length == 0)
        {
            return;
        }

        var selectedIndex = 0;
        if (entries.Length > 1)
        {
            var setChoiceKey = $"choice.{rule.Catalog.ConceptKey}.ability-set";
            var setOptions = entries
                .Select((entry, index) => new CharacterChoiceOptionView(
                    index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    DescribeAbilitySet(entry, index),
                    null))
                .ToArray();

            if (!context.Choices.TryGetValue(setChoiceKey, out var supplied)
                || !TryResolveSetIndex(setOptions, supplied, out selectedIndex))
            {
                context.ChoiceViews[setChoiceKey] = new CharacterChoiceView(
                    setChoiceKey,
                    $"choice-group.{rule.Catalog.ConceptKey}.ability-set",
                    $"{rule.Catalog.DisplayName} Ability Score Set",
                    "ability-score-set",
                    CharacterResolutionStates.ChoiceRequired,
                    setOptions,
                    supplied,
                    rule.Catalog.ConceptKey,
                    rule.Provenance);

                foreach (var possible in PotentialAbilities(entries))
                {
                    context.RequireAbilityChoice(possible, setChoiceKey);
                }

                if (!string.IsNullOrWhiteSpace(supplied))
                {
                    context.Conflicts.Add(new CharacterProjectionConflictView(
                        $"conflict.{setChoiceKey}",
                        "invalid-runtime-choice",
                        $"'{supplied}' is not an available ability-score set for '{rule.Catalog.DisplayName}'.",
                        PotentialAbilities(entries)
                            .Select(value => $"ability.{value}.score")
                            .ToArray(),
                        [rule.Catalog.ConceptKey]));
                }
                return;
            }

            context.ChoiceViews[setChoiceKey] = new CharacterChoiceView(
                setChoiceKey,
                $"choice-group.{rule.Catalog.ConceptKey}.ability-set",
                $"{rule.Catalog.DisplayName} Ability Score Set",
                "ability-score-set",
                CharacterResolutionStates.Resolved,
                setOptions,
                selectedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                rule.Catalog.ConceptKey,
                rule.Provenance);
        }

        ProjectEntry(
            rule,
            context,
            entries[selectedIndex],
            selectedIndex);
    }

    private static void ProjectEntry(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement entry,
        int entryIndex)
    {
        foreach (var property in entry.EnumerateObject())
        {
            if (string.Equals(property.Name, "choose", StringComparison.OrdinalIgnoreCase)
                || property.Value.ValueKind != JsonValueKind.Number
                || !property.Value.TryGetInt32(out var amount)
                || !TryNormalizeAbility(property.Name, out var ability))
            {
                continue;
            }

            context.AddAbilityContribution(
                ability,
                new CharacterMechanicContributionView(
                    $"{rule.Catalog.ConceptKey}.ability.{ability}.{entryIndex}",
                    rule.Catalog.DisplayName,
                    CharacterEffectOperations.Add,
                    amount,
                    null,
                    rule.Catalog.ConceptKey,
                    rule.Provenance));
        }

        if (!CharacterProjectionJson.TryGetProperty(entry, "choose", out var choose)
            || choose.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (CharacterProjectionJson.TryGetProperty(choose, "weighted", out var weighted)
            && weighted.ValueKind == JsonValueKind.Object)
        {
            ProjectWeightedChoice(
                rule,
                context,
                weighted,
                entryIndex);
            return;
        }

        ProjectUniformChoice(
            rule,
            context,
            choose,
            entryIndex);
    }

    private static void ProjectUniformChoice(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement choose,
        int entryIndex)
    {
        var options = ReadAbilityOptions(choose, "from");
        var count = CharacterProjectionJson.Integer(choose, "count") ?? 1;
        var amount = CharacterProjectionJson.Integer(choose, "amount") ?? 1;
        if (count <= 0 || options.Count == 0)
        {
            ProjectUnavailableChoice(rule, context, entryIndex);
            return;
        }

        ProjectChoiceSlots(
            rule,
            context,
            entryIndex,
            options,
            Enumerable.Repeat(amount, count).ToArray(),
            "choose.from");
    }

    private static void ProjectWeightedChoice(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement weighted,
        int entryIndex)
    {
        var options = ReadAbilityOptions(weighted, "from");
        if (!CharacterProjectionJson.TryGetProperty(weighted, "weights", out var weights)
            || weights.ValueKind != JsonValueKind.Array)
        {
            ProjectUnavailableChoice(rule, context, entryIndex);
            return;
        }

        var amounts = new List<int>();
        foreach (var weight in weights.EnumerateArray())
        {
            if (weight.ValueKind != JsonValueKind.Number
                || !weight.TryGetInt32(out var amount))
            {
                ProjectUnavailableChoice(rule, context, entryIndex);
                return;
            }
            amounts.Add(amount);
        }

        if (options.Count == 0 || amounts.Count == 0)
        {
            ProjectUnavailableChoice(rule, context, entryIndex);
            return;
        }

        ProjectChoiceSlots(
            rule,
            context,
            entryIndex,
            options,
            amounts,
            "choose.weighted");
    }

    private static void ProjectChoiceSlots(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        int entryIndex,
        IReadOnlyList<CharacterChoiceOptionView> options,
        IReadOnlyList<int> amounts,
        string sourceShape)
    {
        var groupKey = $"choice-group.{rule.Catalog.ConceptKey}.ability.{entryIndex}";
        var selectedAbilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceUnavailable = options.Count < amounts.Count;

        if (sourceUnavailable)
        {
            context.Conflicts.Add(new CharacterProjectionConflictView(
                $"conflict.{groupKey}.options",
                "source-unavailable",
                $"{rule.Catalog.DisplayName} requires {amounts.Count} distinct ability selection(s), but only {options.Count} legal option(s) are represented by '{sourceShape}'.",
                options.Select(value => $"ability.{value.Value}.score").ToArray(),
                [rule.Catalog.ConceptKey]));
        }

        for (var slot = 0; slot < amounts.Count; slot++)
        {
            var amount = amounts[slot];
            var choiceKey = $"choice.{rule.Catalog.ConceptKey}.ability.{entryIndex}.{slot}";
            var label = amount >= 0
                ? $"{rule.Catalog.DisplayName} Ability +{amount}"
                : $"{rule.Catalog.DisplayName} Ability {amount}";

            if (sourceUnavailable)
            {
                context.ChoiceViews[choiceKey] = new CharacterChoiceView(
                    choiceKey,
                    groupKey,
                    label,
                    "ability-score",
                    CharacterResolutionStates.SourceUnavailable,
                    options,
                    null,
                    rule.Catalog.ConceptKey,
                    rule.Provenance);
                foreach (var option in options)
                {
                    context.RequireAbilityChoice(option.Value, choiceKey);
                }
                continue;
            }

            if (!context.Choices.TryGetValue(choiceKey, out var supplied))
            {
                context.ChoiceViews[choiceKey] = new CharacterChoiceView(
                    choiceKey,
                    groupKey,
                    label,
                    "ability-score",
                    CharacterResolutionStates.ChoiceRequired,
                    options,
                    null,
                    rule.Catalog.ConceptKey,
                    rule.Provenance);
                foreach (var option in options)
                {
                    context.RequireAbilityChoice(option.Value, choiceKey);
                }
                continue;
            }

            var selected = FindOption(options, supplied);
            if (selected is null)
            {
                context.ChoiceViews[choiceKey] = new CharacterChoiceView(
                    choiceKey,
                    groupKey,
                    label,
                    "ability-score",
                    CharacterResolutionStates.ChoiceRequired,
                    options,
                    supplied,
                    rule.Catalog.ConceptKey,
                    rule.Provenance);
                foreach (var option in options)
                {
                    context.RequireAbilityChoice(option.Value, choiceKey);
                }
                context.Conflicts.Add(new CharacterProjectionConflictView(
                    $"conflict.{choiceKey}",
                    "invalid-runtime-choice",
                    $"'{supplied}' is not an available ability for '{label}'.",
                    options.Select(value => $"ability.{value.Value}.score").ToArray(),
                    [rule.Catalog.ConceptKey]));
                continue;
            }

            if (!selectedAbilities.Add(selected.Value))
            {
                context.ChoiceViews[choiceKey] = new CharacterChoiceView(
                    choiceKey,
                    groupKey,
                    label,
                    "ability-score",
                    CharacterResolutionStates.ChoiceRequired,
                    options,
                    selected.Value,
                    rule.Catalog.ConceptKey,
                    rule.Provenance);
                foreach (var option in options)
                {
                    context.RequireAbilityChoice(option.Value, choiceKey);
                }
                context.Conflicts.Add(new CharacterProjectionConflictView(
                    $"conflict.{choiceKey}.duplicate",
                    "duplicate-runtime-choice",
                    $"'{selected.DisplayName}' can not satisfy more than one selection in '{groupKey}'.",
                    options.Select(value => $"ability.{value.Value}.score").ToArray(),
                    [rule.Catalog.ConceptKey]));
                continue;
            }

            context.ChoiceViews[choiceKey] = new CharacterChoiceView(
                choiceKey,
                groupKey,
                label,
                "ability-score",
                CharacterResolutionStates.Resolved,
                options,
                selected.Value,
                rule.Catalog.ConceptKey,
                rule.Provenance);
            context.AddAbilityContribution(
                selected.Value,
                new CharacterMechanicContributionView(
                    choiceKey,
                    label,
                    CharacterEffectOperations.Add,
                    amount,
                    null,
                    rule.Catalog.ConceptKey,
                    rule.Provenance));
        }
    }

    private static void ProjectUnavailableChoice(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        int entryIndex)
    {
        var choiceKey = $"choice.{rule.Catalog.ConceptKey}.ability.{entryIndex}.source";
        context.ChoiceViews[choiceKey] = new CharacterChoiceView(
            choiceKey,
            $"choice-group.{rule.Catalog.ConceptKey}.ability.{entryIndex}",
            $"{rule.Catalog.DisplayName} Ability Score Choice",
            "ability-score",
            CharacterResolutionStates.SourceUnavailable,
            [],
            null,
            rule.Catalog.ConceptKey,
            rule.Provenance);
        foreach (var ability in Abilities)
        {
            context.RequireAbilityChoice(ability, choiceKey);
        }
    }

    private static IReadOnlyList<CharacterChoiceOptionView> ReadAbilityOptions(
        JsonElement element,
        string propertyName)
    {
        if (!CharacterProjectionJson.TryGetProperty(element, propertyName, out var from)
            || from.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return from.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value =>
            {
                var raw = value!;
                return TryNormalizeAbility(raw, out var ability)
                    ? new CharacterChoiceOptionView(
                        ability,
                        CharacterProjectionJson.Humanize(ability),
                        null)
                    : null;
            })
            .Where(value => value is not null)
            .Cast<CharacterChoiceOptionView>()
            .GroupBy(value => value.Value, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> PotentialAbilities(
        IReadOnlyList<JsonElement> entries)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            foreach (var property in entry.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Number
                    && TryNormalizeAbility(property.Name, out var fixedAbility))
                {
                    result.Add(fixedAbility);
                }
            }

            if (!CharacterProjectionJson.TryGetProperty(entry, "choose", out var choose)
                || choose.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var source = CharacterProjectionJson.TryGetProperty(choose, "weighted", out var weighted)
                && weighted.ValueKind == JsonValueKind.Object
                    ? weighted
                    : choose;
            foreach (var option in ReadAbilityOptions(source, "from"))
            {
                result.Add(option.Value);
            }
        }

        return result.Count == 0
            ? Abilities
            : result.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static string DescribeAbilitySet(JsonElement entry, int index)
    {
        var fixedParts = entry.EnumerateObject()
            .Where(value => value.Value.ValueKind == JsonValueKind.Number)
            .Select(value =>
            {
                if (!value.Value.TryGetInt32(out var amount)
                    || !TryNormalizeAbility(value.Name, out var ability))
                {
                    return null;
                }
                var signed = amount >= 0 ? $"+{amount}" : amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return $"{CharacterProjectionJson.Humanize(ability)} {signed}";
            })
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();
        if (fixedParts.Length > 0)
        {
            return string.Join(", ", fixedParts);
        }

        if (CharacterProjectionJson.TryGetProperty(entry, "choose", out var choose)
            && choose.ValueKind == JsonValueKind.Object)
        {
            if (CharacterProjectionJson.TryGetProperty(choose, "weighted", out var weighted)
                && weighted.ValueKind == JsonValueKind.Object
                && CharacterProjectionJson.TryGetProperty(weighted, "weights", out var weights)
                && weights.ValueKind == JsonValueKind.Array)
            {
                var values = weights.EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _))
                    .Select(value => value.GetInt32())
                    .Select(value => value >= 0 ? $"+{value}" : value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .ToArray();
                if (values.Length > 0)
                {
                    return $"Choose distinct abilities: {string.Join(", ", values)}";
                }
            }

            var count = CharacterProjectionJson.Integer(choose, "count") ?? 1;
            var amount = CharacterProjectionJson.Integer(choose, "amount") ?? 1;
            return $"Choose {count} ability score(s) at {(amount >= 0 ? "+" : string.Empty)}{amount}";
        }

        return $"Ability option {index + 1}";
    }

    private static bool TryResolveSetIndex(
        IReadOnlyList<CharacterChoiceOptionView> options,
        string? supplied,
        out int index)
    {
        index = -1;
        if (string.IsNullOrWhiteSpace(supplied))
        {
            return false;
        }

        var option = options.FirstOrDefault(value =>
            string.Equals(value.Value, supplied, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value.DisplayName, supplied, StringComparison.OrdinalIgnoreCase));
        return option is not null
            && int.TryParse(
                option.Value,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out index)
            && index >= 0
            && index < options.Count;
    }

    private static CharacterChoiceOptionView? FindOption(
        IReadOnlyList<CharacterChoiceOptionView> options,
        string supplied) =>
        options.FirstOrDefault(value =>
            string.Equals(value.Value, supplied, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value.DisplayName, supplied, StringComparison.OrdinalIgnoreCase));

    private static bool TryNormalizeAbility(string value, out string ability)
    {
        ability = CharacterProjectionJson.NormalizeAbilityKey(value);
        return Abilities.Contains(ability, StringComparer.OrdinalIgnoreCase);
    }
}
