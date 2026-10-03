using System.Text.Json;
using System.Text.RegularExpressions;
using RulesCore.Application.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Projects starting equipment from already-resolved Class and Background rules. Source/version
/// reconciliation has already happened before Character projection; only Character decisions that
/// remain in the effective rule are exposed here.
/// </summary>
internal static class CharacterStartingEquipmentProjector
{
    private const string ChoiceKind = "starting-equipment";
    private const string ItemGrantKind = "starting-equipment-item";
    private const string CustomGrantKind = "starting-equipment-custom";
    private const string CurrencyGrantKind = "starting-equipment-currency";

    internal static void Project(
        IReadOnlyList<CharacterProjectionRule> rules,
        CharacterProjectionContext context)
    {
        if (context.StartingClassChoiceRequired)
        {
            return;
        }

        var itemCatalog = BuildItemCatalog(rules);
        var startingClass = rules.FirstOrDefault(rule =>
            string.Equals(rule.Catalog.EntityType, "class", StringComparison.OrdinalIgnoreCase)
            && context.IsStartingClass(rule.Catalog.ConceptKey) == true);

        var projectBackground = true;
        if (startingClass is not null
            && CharacterProjectionJson.TryGetProperty(
                startingClass.Document,
                "startingEquipment",
                out var classStartingEquipment))
        {
            var outcome = ProjectClassStartingEquipment(
                startingClass,
                context,
                classStartingEquipment,
                itemCatalog);
            if (outcome is StartingEquipmentOutcome.Unresolved or StartingEquipmentOutcome.Gold)
            {
                projectBackground = false;
            }
            else if (outcome == StartingEquipmentOutcome.Equipment)
            {
                projectBackground = ReadBoolean(
                    classStartingEquipment,
                    "additionalFromBackground") ?? true;
            }
        }

        if (!projectBackground)
        {
            return;
        }

        foreach (var rule in rules)
        {
            if (!string.Equals(rule.Catalog.EntityType, "background", StringComparison.OrdinalIgnoreCase)
                || !context.IsSelected(rule.Catalog.ConceptKey)
                || !CharacterProjectionJson.TryGetProperty(
                    rule.Document,
                    "startingEquipment",
                    out var startingEquipment))
            {
                continue;
            }

            ProjectEquipmentData(rule, context, startingEquipment, itemCatalog);
        }
    }

    private static StartingEquipmentOutcome ProjectClassStartingEquipment(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement startingEquipment,
        ItemCatalog itemCatalog)
    {
        var goldAlternative = CharacterProjectionJson.String(startingEquipment, "goldAlternative");
        if (string.IsNullOrWhiteSpace(goldAlternative))
        {
            ProjectEquipmentData(rule, context, startingEquipment, itemCatalog);
            return StartingEquipmentOutcome.Equipment;
        }

        var selectedMethod = ProjectMethodChoice(rule, context, goldAlternative);
        if (selectedMethod is null)
        {
            return StartingEquipmentOutcome.Unresolved;
        }

        if (string.Equals(selectedMethod, "equipment", StringComparison.OrdinalIgnoreCase))
        {
            ProjectEquipmentData(rule, context, startingEquipment, itemCatalog);
            return StartingEquipmentOutcome.Equipment;
        }

        ProjectGoldAlternative(rule, context, goldAlternative);
        return StartingEquipmentOutcome.Gold;
    }

    private static string? ProjectMethodChoice(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        string goldAlternative)
    {
        string? selectedMethod = null;
        var displayExpression = GoldExpressionDisplay(goldAlternative);
        CharacterStartingProficiencyProjector.ProjectChoiceGroup(
            rule,
            context,
            pathKey: "starting-equipment-method",
            groupIndex: 0,
            count: 1,
            options:
            [
                new CharacterChoiceOptionView("equipment", "Starting equipment", null),
                new CharacterChoiceOptionView("gold", $"Starting gold ({displayExpression})", null)
            ],
            sourceShape: "starting-equipment-method",
            kind: ChoiceKind,
            optionLabel: "Starting Equipment Method",
            onSelected: selected => selectedMethod = selected.Value);
        return selectedMethod;
    }

    private static void ProjectGoldAlternative(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        string sourceExpression)
    {
        var mechanicKey = $"starting-equipment.gold.{rule.Catalog.ConceptKey}";
        var rollKey = $"starting-equipment.gold-roll.{rule.Catalog.ConceptKey}";
        if (!TryParseGoldAlternative(sourceExpression, out var gold))
        {
            AddSourceUnavailableConflict(
                rule,
                context,
                "starting-equipment.gold-alternative",
                $"Starting-gold expression '{sourceExpression}' can not be represented as a supported dice expression.");
            context.Mechanics[mechanicKey] = new CharacterResolvedMechanicView(
                mechanicKey,
                "starting-equipment",
                "Starting Gold",
                CharacterResolutionStates.SourceUnavailable,
                null,
                GoldExpressionDisplay(sourceExpression),
                "gp",
                [],
                [],
                [],
                [],
                [],
                rule.Provenance);
            return;
        }

        if (!context.Rolls.TryGetValue(rollKey, out var rolled))
        {
            context.Mechanics[mechanicKey] = new CharacterResolvedMechanicView(
                mechanicKey,
                "starting-equipment",
                "Starting Gold",
                CharacterResolutionStates.RollRequired,
                null,
                gold.DisplayExpression,
                "gp",
                [],
                [],
                [],
                [rollKey],
                [],
                rule.Provenance);
            return;
        }

        var minimum = gold.DiceCount;
        var maximum = checked(gold.DiceCount * gold.DieFaces);
        if (rolled < minimum || rolled > maximum)
        {
            context.Conflicts.Add(new CharacterProjectionConflictView(
                $"conflict.{rollKey}",
                "invalid-runtime-roll",
                $"Starting-gold roll {rolled} for {rule.Catalog.DisplayName} must be between {minimum} and {maximum} for {gold.DiceCount}d{gold.DieFaces}.",
                [mechanicKey],
                [rule.Catalog.ConceptKey]));
            context.Mechanics[mechanicKey] = new CharacterResolvedMechanicView(
                mechanicKey,
                "starting-equipment",
                "Starting Gold",
                CharacterResolutionStates.Conflict,
                null,
                gold.DisplayExpression,
                "gp",
                [],
                [],
                [],
                [],
                [],
                rule.Provenance);
            return;
        }

        var goldPieces = (long)rolled * gold.Multiplier;
        if (goldPieces > int.MaxValue)
        {
            AddSourceUnavailableConflict(
                rule,
                context,
                "starting-equipment.gold-alternative.quantity",
                "Resolved starting gold exceeds the supported grant quantity.");
            context.Mechanics[mechanicKey] = new CharacterResolvedMechanicView(
                mechanicKey,
                "starting-equipment",
                "Starting Gold",
                CharacterResolutionStates.SourceUnavailable,
                null,
                gold.DisplayExpression,
                "gp",
                [],
                [],
                [],
                [],
                [],
                rule.Provenance);
            return;
        }

        AddGrant(
            context,
            rule,
            $"grant.{rule.Catalog.ConceptKey}.starting-equipment.gold",
            CurrencyGrantKind,
            "gp",
            "GP",
            (int)goldPieces);
        context.Mechanics[mechanicKey] = new CharacterResolvedMechanicView(
            mechanicKey,
            "starting-equipment",
            "Starting Gold",
            CharacterResolutionStates.Resolved,
            (int)goldPieces,
            gold.DisplayExpression,
            "gp",
            [],
            [],
            [],
            [],
            [new CharacterMechanicContributionView(
                rollKey,
                $"{rule.Catalog.DisplayName} starting-gold roll",
                "set",
                (int)goldPieces,
                $"rolled {rolled}; {gold.DisplayExpression}",
                rule.Catalog.ConceptKey,
                rule.Provenance)],
            rule.Provenance);
    }

    private static void ProjectEquipmentData(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement startingEquipment,
        ItemCatalog itemCatalog)
    {
        if (startingEquipment.ValueKind == JsonValueKind.Object
            && CharacterProjectionJson.TryGetProperty(
                startingEquipment,
                "defaultData",
                out var defaultData))
        {
            ProjectGroups(rule, context, defaultData, itemCatalog);
            return;
        }

        ProjectGroups(rule, context, startingEquipment, itemCatalog);
    }

    private static void ProjectGroups(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement groups,
        ItemCatalog itemCatalog)
    {
        if (groups.ValueKind == JsonValueKind.Object)
        {
            ProjectGroup(rule, context, groups, itemCatalog, groupIndex: 0);
            return;
        }

        if (groups.ValueKind != JsonValueKind.Array)
        {
            AddSourceUnavailableConflict(
                rule,
                context,
                "starting-equipment.shape",
                "The resolved starting-equipment rule is not in a structured object or array form.");
            return;
        }

        var groupIndex = 0;
        foreach (var group in groups.EnumerateArray())
        {
            if (group.ValueKind == JsonValueKind.Object)
            {
                ProjectGroup(rule, context, group, itemCatalog, groupIndex);
            }
            else
            {
                AddSourceUnavailableConflict(
                    rule,
                    context,
                    $"starting-equipment.group.{groupIndex}",
                    "A starting-equipment group is not represented as a structured object.");
            }
            groupIndex++;
        }
    }

    private static void ProjectGroup(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement group,
        ItemCatalog itemCatalog,
        int groupIndex)
    {
        if (group.TryGetProperty("_", out var fixedEntries))
        {
            ProjectEntries(
                rule,
                context,
                fixedEntries,
                itemCatalog,
                groupIndex,
                optionKey: "fixed");
        }

        var alternatives = group.EnumerateObject()
            .Where(property => property.Name != "_")
            .ToArray();
        if (alternatives.Length == 0)
        {
            return;
        }

        if (alternatives.Length == 1)
        {
            ProjectEntries(
                rule,
                context,
                alternatives[0].Value,
                itemCatalog,
                groupIndex,
                alternatives[0].Name);
            return;
        }

        var options = alternatives
            .Select(property => new CharacterChoiceOptionView(
                property.Name,
                DescribePackage(property.Value, itemCatalog),
                null))
            .ToArray();

        CharacterStartingProficiencyProjector.ProjectChoiceGroup(
            rule,
            context,
            pathKey: "starting-equipment",
            groupIndex,
            count: 1,
            options,
            sourceShape: "starting-equipment-package",
            kind: ChoiceKind,
            optionLabel: $"Starting Equipment {groupIndex + 1}",
            onSelected: selected =>
            {
                var selectedPackage = alternatives.FirstOrDefault(property =>
                    string.Equals(property.Name, selected.Value, StringComparison.OrdinalIgnoreCase));
                if (selectedPackage.Value.ValueKind != JsonValueKind.Undefined)
                {
                    ProjectEntries(
                        rule,
                        context,
                        selectedPackage.Value,
                        itemCatalog,
                        groupIndex,
                        selectedPackage.Name);
                }
            });
    }

    private static void ProjectEntries(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement entries,
        ItemCatalog itemCatalog,
        int groupIndex,
        string optionKey)
    {
        if (entries.ValueKind != JsonValueKind.Array)
        {
            ProjectEntry(
                rule,
                context,
                entries,
                itemCatalog,
                groupIndex,
                optionKey,
                entryIndex: 0);
            return;
        }

        var entryIndex = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            ProjectEntry(
                rule,
                context,
                entry,
                itemCatalog,
                groupIndex,
                optionKey,
                entryIndex++);
        }
    }

    private static void ProjectEntry(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement entry,
        ItemCatalog itemCatalog,
        int groupIndex,
        string optionKey,
        int entryIndex)
    {
        var path = $"{groupIndex}.{Slug(optionKey)}.{entryIndex}";

        if (entry.ValueKind == JsonValueKind.String)
        {
            ProjectExactItem(
                rule,
                context,
                itemCatalog,
                entry.GetString(),
                displayName: null,
                quantity: 1,
                path);
            return;
        }

        if (entry.ValueKind != JsonValueKind.Object)
        {
            AddSourceUnavailableConflict(
                rule,
                context,
                $"starting-equipment.entry.{path}",
                "A starting-equipment entry is not represented in a supported structured form.");
            return;
        }

        var quantity = CharacterProjectionJson.Integer(entry, "quantity") ?? 1;
        if (quantity <= 0)
        {
            AddSourceUnavailableConflict(
                rule,
                context,
                $"starting-equipment.entry.{path}.quantity",
                "A starting-equipment entry has a non-positive quantity.");
            return;
        }

        var handled = false;
        var itemReference = CharacterProjectionJson.String(entry, "item");
        if (!string.IsNullOrWhiteSpace(itemReference))
        {
            ProjectExactItem(
                rule,
                context,
                itemCatalog,
                itemReference,
                CharacterProjectionJson.String(entry, "displayName"),
                quantity,
                path);
            handled = true;
        }

        var equipmentTypes = CharacterProjectionJson.Strings(entry, "equipmentTypes")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        var equipmentType = CharacterProjectionJson.String(entry, "equipmentType");
        if (!string.IsNullOrWhiteSpace(equipmentType))
        {
            equipmentTypes.Insert(0, equipmentType);
        }
        if (equipmentTypes.Count > 0)
        {
            ProjectEquipmentTypeChoices(
                rule,
                context,
                itemCatalog,
                equipmentTypes,
                quantity,
                path);
            handled = true;
        }

        var special = CharacterProjectionJson.String(entry, "special");
        if (!string.IsNullOrWhiteSpace(special))
        {
            AddGrant(
                context,
                rule,
                $"grant.{rule.Catalog.ConceptKey}.starting-equipment.{path}.custom",
                CustomGrantKind,
                $"custom.{Slug(special)}",
                special,
                quantity);
            handled = true;
        }

        if (TryReadInt64(entry, "value", out var value))
        {
            AddCurrencyGrants(rule, context, value, $"{path}.value");
            handled = true;
        }
        if (TryReadInt64(entry, "containsValue", out var containsValue))
        {
            AddCurrencyGrants(rule, context, containsValue, $"{path}.contains-value");
            handled = true;
        }

        if (!handled)
        {
            AddSourceUnavailableConflict(
                rule,
                context,
                $"starting-equipment.entry.{path}",
                "A starting-equipment entry uses a structured form that is not currently recognized.");
        }
    }

    private static void ProjectExactItem(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        ItemCatalog itemCatalog,
        string? reference,
        string? displayName,
        int quantity,
        string path)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            AddSourceUnavailableConflict(
                rule,
                context,
                $"starting-equipment.item.{path}",
                "A starting-equipment item reference is blank.");
            return;
        }

        var item = itemCatalog.Resolve(reference);
        if (item is null)
        {
            AddSourceUnavailableConflict(
                rule,
                context,
                $"starting-equipment.item.{path}",
                $"Starting-equipment item '{reference}' can not be resolved to one effective item rule.");
            return;
        }

        AddGrant(
            context,
            rule,
            $"grant.{rule.Catalog.ConceptKey}.starting-equipment.{path}.item",
            ItemGrantKind,
            item.Rule.Catalog.ConceptKey,
            string.IsNullOrWhiteSpace(displayName) ? item.Rule.Catalog.DisplayName : displayName.Trim(),
            quantity);
    }

    private static void ProjectEquipmentTypeChoices(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        ItemCatalog itemCatalog,
        IReadOnlyList<string> equipmentTypes,
        int quantity,
        string path)
    {
        var normalizedTypes = equipmentTypes
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var candidates = normalizedTypes
            .SelectMany(itemCatalog.ForEquipmentType)
            .DistinctBy(item => item.Rule.Catalog.ConceptKey, StringComparer.OrdinalIgnoreCase)
            .Select(item => new CharacterChoiceOptionView(
                item.Rule.Catalog.ConceptKey,
                item.Rule.Catalog.DisplayName,
                item.Rule.Catalog.ConceptKey))
            .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.Ordinal)
            .ToArray();

        var sourceShape = string.Join("|", normalizedTypes);
        var label = string.Join(
            " or ",
            normalizedTypes.Select(CharacterProjectionJson.Humanize));
        for (var slot = 0; slot < quantity; slot++)
        {
            var slotPath = $"starting-equipment-item.{path}.{slot}";
            CharacterStartingProficiencyProjector.ProjectChoiceGroup(
                rule,
                context,
                pathKey: slotPath,
                groupIndex: 0,
                count: 1,
                candidates,
                sourceShape,
                kind: ChoiceKind,
                optionLabel: label,
                onSelected: selected =>
                {
                    var selectedItem = itemCatalog.ByConceptKey(selected.ConceptKey ?? selected.Value);
                    if (selectedItem is not null)
                    {
                        AddGrant(
                            context,
                            rule,
                            $"grant.{rule.Catalog.ConceptKey}.starting-equipment.{path}.{slot}.item",
                            ItemGrantKind,
                            selectedItem.Rule.Catalog.ConceptKey,
                            selectedItem.Rule.Catalog.DisplayName,
                            1);
                    }
                },
                forceSourceUnavailable: candidates.Length == 0);
        }
    }

    private static void AddCurrencyGrants(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        long copperValue,
        string path)
    {
        if (copperValue < 0)
        {
            AddSourceUnavailableConflict(
                rule,
                context,
                $"starting-equipment.currency.{path}",
                "Starting-equipment currency can not have a negative value.");
            return;
        }

        var remaining = copperValue;
        AddCurrency("gp", 100, "GP");
        AddCurrency("sp", 10, "SP");
        AddCurrency("cp", 1, "CP");

        void AddCurrency(string key, long copperPerUnit, string display)
        {
            var count = remaining / copperPerUnit;
            remaining %= copperPerUnit;
            if (count <= 0)
            {
                return;
            }
            if (count > int.MaxValue)
            {
                AddSourceUnavailableConflict(
                    rule,
                    context,
                    $"starting-equipment.currency.{path}.{key}",
                    "Starting-equipment currency exceeds the supported grant quantity.");
                return;
            }

            AddGrant(
                context,
                rule,
                $"grant.{rule.Catalog.ConceptKey}.starting-equipment.{path}.{key}",
                CurrencyGrantKind,
                key,
                display,
                (int)count);
        }
    }

    private static void AddGrant(
        CharacterProjectionContext context,
        CharacterProjectionRule rule,
        string baseGrantKey,
        string kind,
        string targetKey,
        string displayName,
        int quantity)
    {
        for (var index = 0; index < quantity; index++)
        {
            context.Grants.Add(new CharacterGrantView(
                quantity == 1 ? baseGrantKey : $"{baseGrantKey}.{index}",
                kind,
                targetKey,
                displayName,
                rule.Catalog.ConceptKey,
                rule.Provenance));
        }
    }

    private static string DescribePackage(JsonElement package, ItemCatalog itemCatalog)
    {
        if (package.ValueKind != JsonValueKind.Array)
        {
            return "Starting equipment option";
        }

        var parts = new List<string>();
        foreach (var entry in package.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                var item = itemCatalog.Resolve(entry.GetString() ?? string.Empty);
                parts.Add(item?.Rule.Catalog.DisplayName ?? NativeName(entry.GetString()));
                continue;
            }
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var quantity = CharacterProjectionJson.Integer(entry, "quantity") ?? 1;
            var displayName = CharacterProjectionJson.String(entry, "displayName");
            var itemReference = CharacterProjectionJson.String(entry, "item");
            var equipmentTypes = CharacterProjectionJson.Strings(entry, "equipmentTypes")
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
            var equipmentType = CharacterProjectionJson.String(entry, "equipmentType");
            if (!string.IsNullOrWhiteSpace(equipmentType))
            {
                equipmentTypes.Insert(0, equipmentType);
            }
            var special = CharacterProjectionJson.String(entry, "special");
            if (!string.IsNullOrWhiteSpace(itemReference))
            {
                var item = itemCatalog.Resolve(itemReference);
                parts.Add(WithQuantity(
                    displayName ?? item?.Rule.Catalog.DisplayName ?? NativeName(itemReference),
                    quantity));
            }
            else if (equipmentTypes.Count > 0)
            {
                parts.Add(WithQuantity(
                    string.Join(" or ", equipmentTypes.Select(CharacterProjectionJson.Humanize)),
                    quantity));
            }
            else if (!string.IsNullOrWhiteSpace(special))
            {
                parts.Add(WithQuantity(special, quantity));
            }

            if (TryReadInt64(entry, "value", out var value))
            {
                parts.Add(FormatCopperValue(value));
            }
            if (TryReadInt64(entry, "containsValue", out var containsValue))
            {
                parts.Add(FormatCopperValue(containsValue));
            }
        }

        return parts.Count == 0
            ? "Starting equipment option"
            : string.Join(", ", parts);
    }

    private static string WithQuantity(string value, int quantity) =>
        quantity <= 1 ? value : $"{value} ×{quantity}";

    private static string FormatCopperValue(long copperValue)
    {
        var parts = new List<string>();
        var remaining = copperValue;
        var gp = remaining / 100;
        remaining %= 100;
        var sp = remaining / 10;
        var cp = remaining % 10;
        if (gp > 0) parts.Add($"{gp} GP");
        if (sp > 0) parts.Add($"{sp} SP");
        if (cp > 0) parts.Add($"{cp} CP");
        return parts.Count == 0 ? "0 CP" : string.Join(" ", parts);
    }

    private static bool TryParseGoldAlternative(
        string sourceExpression,
        out GoldAlternative alternative)
    {
        var display = GoldExpressionDisplay(sourceExpression);
        var match = Regex.Match(
            display,
            @"(?<count>\d+)\s*d\s*(?<faces>\d+)(?:\s*(?:×|x|\*)\s*(?<multiplier>\d+))?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success
            || !int.TryParse(match.Groups["count"].Value, out var count)
            || !int.TryParse(match.Groups["faces"].Value, out var faces)
            || count <= 0
            || faces <= 0)
        {
            alternative = default!;
            return false;
        }

        var multiplier = 1;
        if (match.Groups["multiplier"].Success
            && (!int.TryParse(match.Groups["multiplier"].Value, out multiplier)
                || multiplier <= 0))
        {
            alternative = default!;
            return false;
        }

        alternative = new GoldAlternative(count, faces, multiplier, display);
        return true;
    }

    private static string GoldExpressionDisplay(string sourceExpression)
    {
        var value = sourceExpression.Trim();
        if (value.StartsWith("{@dice ", StringComparison.OrdinalIgnoreCase)
            && value.EndsWith('}'))
        {
            value = value[7..^1];
            value = value.Split('|', 2, StringSplitOptions.TrimEntries)[0];
        }
        return value;
    }

    private static bool TryReadInt64(JsonElement element, string propertyName, out long value)
    {
        value = 0;
        if (!CharacterProjectionJson.TryGetProperty(element, propertyName, out var property))
        {
            return false;
        }
        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out value))
        {
            return true;
        }
        return property.ValueKind == JsonValueKind.String
            && long.TryParse(property.GetString(), out value);
    }

    private static bool? ReadBoolean(JsonElement element, string propertyName)
    {
        if (!CharacterProjectionJson.TryGetProperty(element, propertyName, out var property))
        {
            return null;
        }
        if (property.ValueKind == JsonValueKind.True)
        {
            return true;
        }
        if (property.ValueKind == JsonValueKind.False)
        {
            return false;
        }
        return property.ValueKind == JsonValueKind.String
            && bool.TryParse(property.GetString(), out var parsed)
                ? parsed
                : null;
    }

    private static void AddSourceUnavailableConflict(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        string suffix,
        string message)
    {
        context.Conflicts.Add(new CharacterProjectionConflictView(
            $"conflict.{rule.Catalog.ConceptKey}.{suffix}",
            "source-unavailable",
            $"{rule.Catalog.DisplayName}: {message}",
            [],
            [rule.Catalog.ConceptKey]));
    }

    private static ItemCatalog BuildItemCatalog(IReadOnlyList<CharacterProjectionRule> rules) =>
        new(rules
            .Where(rule =>
                string.Equals(rule.Catalog.EntityType, "item", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rule.Catalog.EntityType, "baseitem", StringComparison.OrdinalIgnoreCase))
            .Select(rule => new ItemCatalogEntry(
                rule,
                CharacterProjectionJson.String(rule.Document, "name") ?? rule.Catalog.DisplayName,
                CharacterProjectionJson.String(rule.Document, "source") ?? rule.Catalog.SourceCode,
                NormalizeItemType(CharacterProjectionJson.String(rule.Document, "type")),
                CharacterProjectionJson.String(rule.Document, "weaponCategory"),
                CharacterProjectionJson.String(rule.Document, "scfType")))
            .ToArray());

    private static string? NormalizeItemType(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Split('|', 2)[0].Trim().ToUpperInvariant();

    private static string NativeName(string? reference) =>
        string.IsNullOrWhiteSpace(reference)
            ? "Unknown item"
            : reference.Split('|', 2, StringSplitOptions.TrimEntries)[0];

    private static string Slug(string value) =>
        string.Join(
            '-',
            value.Trim().ToLowerInvariant()
                .Split(
                    [' ', '/', '_', '-', '|', '.', ':'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private sealed record GoldAlternative(
        int DiceCount,
        int DieFaces,
        int Multiplier,
        string DisplayExpression);

    private sealed record ItemCatalogEntry(
        CharacterProjectionRule Rule,
        string NativeName,
        string NativeSource,
        string? ItemType,
        string? WeaponCategory,
        string? SpellcastingFocusType);

    private sealed class ItemCatalog(IReadOnlyList<ItemCatalogEntry> items)
    {
        private readonly Dictionary<string, ItemCatalogEntry> byConcept = items
            .GroupBy(value => value.Rule.Catalog.ConceptKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        public ItemCatalogEntry? ByConceptKey(string conceptKey) =>
            byConcept.GetValueOrDefault(conceptKey);

        public ItemCatalogEntry? Resolve(string reference)
        {
            var parts = reference.Split('|', StringSplitOptions.TrimEntries);
            var name = parts[0];
            var source = parts.Length > 1 ? parts[1] : null;
            var byName = items
                .Where(item =>
                    string.Equals(item.NativeName, name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Rule.Catalog.DisplayName, name, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (!string.IsNullOrWhiteSpace(source))
            {
                var exact = byName.Where(item =>
                        string.Equals(item.NativeSource, source, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (exact.Length == 1)
                {
                    return exact[0];
                }
            }
            return byName.Length == 1 ? byName[0] : null;
        }

        public IEnumerable<ItemCatalogEntry> ForEquipmentType(string equipmentType)
        {
            var normalized = equipmentType.Trim();
            return normalized.ToLowerInvariant() switch
            {
                "weaponany" => Weapons(null, null),
                "weaponsimple" => Weapons("simple", null),
                "weaponsimplemelee" => Weapons("simple", "M"),
                "weaponsimpleranged" => Weapons("simple", "R"),
                "weaponmartial" => Weapons("martial", null),
                "weaponmartialmelee" => Weapons("martial", "M"),
                "weaponmartialranged" => Weapons("martial", "R"),
                "armorlight" => OfType("LA"),
                "armormedium" => OfType("MA"),
                "armorheavy" => OfType("HA"),
                "shield" => OfType("S"),
                "instrumentmusical" => OfType("INS"),
                "toolartisan" => OfType("AT"),
                "setgaming" => OfType("GS"),
                "tool" => items.Where(item => item.ItemType is "T" or "AT" or "GS" or "INS"),
                "focusspellcasting" => OfType("SCF"),
                "focusspellcastingarcane" => SpellcastingFocus("arcane"),
                "focusspellcastingholy" => SpellcastingFocus("holy"),
                "focusspellcastingdruidic" => SpellcastingFocus("druid"),
                _ => []
            };
        }

        private IEnumerable<ItemCatalogEntry> Weapons(string? category, string? itemType) =>
            items.Where(item =>
                item.ItemType is "M" or "R"
                && (category is null
                    || string.Equals(item.WeaponCategory, category, StringComparison.OrdinalIgnoreCase))
                && (itemType is null
                    || string.Equals(item.ItemType, itemType, StringComparison.OrdinalIgnoreCase)));

        private IEnumerable<ItemCatalogEntry> OfType(string itemType) =>
            items.Where(item => string.Equals(
                item.ItemType,
                itemType,
                StringComparison.OrdinalIgnoreCase));

        private IEnumerable<ItemCatalogEntry> SpellcastingFocus(string focusType) =>
            items.Where(item =>
                string.Equals(item.ItemType, "SCF", StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    item.SpellcastingFocusType,
                    focusType,
                    StringComparison.OrdinalIgnoreCase));
    }

    private enum StartingEquipmentOutcome
    {
        Unresolved,
        Equipment,
        Gold
    }
}
