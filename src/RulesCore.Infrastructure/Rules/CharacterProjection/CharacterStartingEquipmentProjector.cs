using System.Text.Json;
using RulesCore.Application.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Projects starting equipment from the already-resolved Class and Background rules. This projector
/// does not select a source revision or edition. Source reconciliation has already happened before
/// Character projection; the only remaining choices are Character decisions defined by the
/// effective rule itself.
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
        var itemCatalog = BuildItemCatalog(rules);

        foreach (var rule in rules)
        {
            if (!ShouldProject(rule, context)
                || !CharacterProjectionJson.TryGetProperty(
                    rule.Document,
                    "startingEquipment",
                    out var startingEquipment))
            {
                continue;
            }

            ProjectStartingEquipment(rule, context, startingEquipment, itemCatalog);
        }
    }

    private static bool ShouldProject(
        CharacterProjectionRule rule,
        CharacterProjectionContext context)
    {
        if (string.Equals(rule.Catalog.EntityType, "class", StringComparison.OrdinalIgnoreCase))
        {
            return context.IsStartingClass(rule.Catalog.ConceptKey) == true;
        }

        return string.Equals(rule.Catalog.EntityType, "background", StringComparison.OrdinalIgnoreCase)
            && context.IsSelected(rule.Catalog.ConceptKey);
    }

    private static void ProjectStartingEquipment(
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

        var equipmentType = CharacterProjectionJson.String(entry, "equipmentType");
        if (!string.IsNullOrWhiteSpace(equipmentType))
        {
            ProjectEquipmentTypeChoices(
                rule,
                context,
                itemCatalog,
                equipmentType,
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
        string equipmentType,
        int quantity,
        string path)
    {
        var candidates = itemCatalog.ForEquipmentType(equipmentType)
            .Select(item => new CharacterChoiceOptionView(
                item.Rule.Catalog.ConceptKey,
                item.Rule.Catalog.DisplayName,
                item.Rule.Catalog.ConceptKey))
            .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.Ordinal)
            .ToArray();

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
                sourceShape: equipmentType,
                kind: ChoiceKind,
                optionLabel: CharacterProjectionJson.Humanize(equipmentType),
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
            var equipmentType = CharacterProjectionJson.String(entry, "equipmentType");
            var special = CharacterProjectionJson.String(entry, "special");
            if (!string.IsNullOrWhiteSpace(itemReference))
            {
                var item = itemCatalog.Resolve(itemReference);
                parts.Add(WithQuantity(
                    displayName ?? item?.Rule.Catalog.DisplayName ?? NativeName(itemReference),
                    quantity));
            }
            else if (!string.IsNullOrWhiteSpace(equipmentType))
            {
                parts.Add(WithQuantity(CharacterProjectionJson.Humanize(equipmentType), quantity));
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
                CharacterProjectionJson.String(rule.Document, "weaponCategory")))
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

    private sealed record ItemCatalogEntry(
        CharacterProjectionRule Rule,
        string NativeName,
        string NativeSource,
        string? ItemType,
        string? WeaponCategory);

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
            if (normalized.StartsWith("weaponMartial", StringComparison.OrdinalIgnoreCase))
            {
                return items.Where(item =>
                    item.ItemType is "M" or "R"
                    && string.Equals(item.WeaponCategory, "martial", StringComparison.OrdinalIgnoreCase));
            }
            if (normalized.StartsWith("weaponSimple", StringComparison.OrdinalIgnoreCase))
            {
                return items.Where(item =>
                    item.ItemType is "M" or "R"
                    && string.Equals(item.WeaponCategory, "simple", StringComparison.OrdinalIgnoreCase));
            }

            var itemType = normalized.ToLowerInvariant() switch
            {
                "armorlight" => "LA",
                "armormedium" => "MA",
                "armorheavy" => "HA",
                "shield" => "S",
                _ => null
            };
            return itemType is null
                ? []
                : items.Where(item => string.Equals(item.ItemType, itemType, StringComparison.OrdinalIgnoreCase));
        }
    }
}
