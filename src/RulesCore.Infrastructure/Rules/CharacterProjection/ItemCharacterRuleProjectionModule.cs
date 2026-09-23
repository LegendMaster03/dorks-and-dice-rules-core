using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

internal sealed class ItemCharacterRuleProjectionModule : ICharacterRuleProjectionModule
{
    public bool Handles(CharacterProjectionRule rule, CharacterProjectionContext context) =>
        context.InventoryItems.Contains(rule.Catalog.ConceptKey)
        && string.Equals(rule.Catalog.EntityType, "item", StringComparison.OrdinalIgnoreCase);

    public void Project(CharacterProjectionRule rule, CharacterProjectionContext context)
    {
        var equipped = context.EquippedItems.Contains(rule.Catalog.ConceptKey);
        var rawItemType = CharacterProjectionJson.String(rule.Document, "type");
        var itemType = NormalizeItemType(rawItemType);
        var armorRole = itemType switch
        {
            "LA" => "light-armor",
            "MA" => "medium-armor",
            "HA" => "heavy-armor",
            "S" => "shield",
            _ => null
        };
        var attunementRequirement = CharacterProjectionJson.String(rule.Document, "reqAttune")
            ?? CharacterProjectionJson.String(rule.Document, "requiresAttunement");
        var requiresAttunement = CharacterProjectionJson.Boolean(
                rule.Document,
                "requiresAttunement")
            ?? (!string.IsNullOrWhiteSpace(attunementRequirement)
                ? true
                : null);
        var propertyKeys = ReadItemPropertyKeys(rule.Document);
        context.Equipment[$"equipment.{rule.Catalog.ConceptKey}"] =
            new CharacterEquipmentDefinitionView(
                $"equipment.{rule.Catalog.ConceptKey}",
                rule.Catalog.ConceptKey,
                rule.Catalog.DisplayName,
                CharacterResolutionStates.Resolved,
                itemType ?? rawItemType,
                CharacterProjectionJson.String(rule.Document, "equipmentCategory")
                    ?? CharacterProjectionJson.String(rule.Document, "category"),
                armorRole,
                CharacterProjectionJson.Decimal(rule.Document, "weight"),
                CharacterProjectionJson.String(rule.Document, "weightUnit") ?? "lb",
                CharacterProjectionJson.String(rule.Document, "ammoType")
                    ?? CharacterProjectionJson.String(rule.Document, "ammunitionType")
                    ?? CharacterProjectionJson.String(rule.Document, "ammunition"),
                CharacterProjectionJson.String(rule.Document, "capacity"),
                requiresAttunement,
                attunementRequirement,
                propertyKeys,
                rule.Provenance);

        if (!equipped)
        {
            return;
        }

        context.AddFeature(
            $"feature.{rule.Catalog.ConceptKey}",
            rule.Catalog.DisplayName,
            "equipped-item",
            CharacterResolutionStates.Resolved,
            rule.Catalog.ConceptKey,
            rule.Provenance,
            "item");

        var ac = CharacterProjectionJson.Integer(rule.Document, "ac")
            ?? CharacterProjectionJson.Integer(rule.Document, "armorClass");
        if (ac is int armorClass)
        {
            var (operation, target) = itemType switch
            {
                "LA" or "MA" or "HA" => (
                    CharacterEffectOperations.Set,
                    "defense.ac.armor-base"),
                "S" => (
                    CharacterEffectOperations.Add,
                    "defense.ac.shield-bonus"),
                _ => (
                    CharacterEffectOperations.Add,
                    "defense.ac.unclassified")
            };
            context.AddEffect(new CharacterRuleEffectView(
                $"{rule.Catalog.ConceptKey}.armor-class",
                CharacterEffectKinds.MechanicContribution,
                operation,
                target,
                armorClass,
                itemType,
                null,
                rule.Catalog.ConceptKey,
                rule.Provenance));
        }

        var damage = CharacterProjectionJson.String(rule.Document, "dmg1")
            ?? CharacterProjectionJson.String(rule.Document, "damage");
        var damageType = CharacterProjectionJson.String(rule.Document, "dmgType");
        var weaponCategory = CharacterProjectionJson.String(rule.Document, "weaponCategory");
        if (!string.IsNullOrWhiteSpace(damage)
            || !string.IsNullOrWhiteSpace(weaponCategory))
        {
            var actionKey = $"action.attack.{rule.Catalog.ConceptKey}";
            var range = CharacterProjectionJson.RangeText(rule.Document);
            context.Actions[actionKey] = new CharacterActionView(
                actionKey,
                rule.Catalog.DisplayName,
                "attack",
                CharacterResolutionStates.ApplicableUnresolved,
                $"attack.{rule.Catalog.ConceptKey}",
                damage,
                damageType,
                range,
                null,
                null,
                null,
                null,
                [],
                rule.Provenance);

            if (itemType is "M" or "R" && !string.IsNullOrWhiteSpace(damage))
            {
                context.WeaponAttacks[rule.Catalog.ConceptKey] =
                    new CharacterWeaponAttackProfile(
                        rule.Catalog.ConceptKey,
                        rule.Catalog.DisplayName,
                        itemType,
                        string.IsNullOrWhiteSpace(weaponCategory)
                            ? null
                            : weaponCategory.Trim(),
                        HasItemProperty(rule.Document, "F"),
                        ReadSignedInteger(rule.Document, "bonusWeaponAttack")
                            ?? ReadSignedInteger(rule.Document, "bonusWeapon")
                            ?? 0,
                        ReadSignedInteger(rule.Document, "bonusWeaponDamage")
                            ?? ReadSignedInteger(rule.Document, "bonusWeapon")
                            ?? 0,
                        damage,
                        damageType,
                        range,
                        rule.Provenance);
            }
        }

        if (requiresAttunement == true)
        {
            var factKey = $"item.{rule.Catalog.ConceptKey}.attuned";
            if (!context.BooleanFacts.TryGetValue(factKey, out var attuned) || !attuned)
            {
                context.Conflicts.Add(new CharacterProjectionConflictView(
                    $"requirement.{factKey}",
                    "missing-character-input",
                    $"The equipped item '{rule.Catalog.DisplayName}' has an attunement requirement whose Character state is not satisfied.",
                    [],
                    [rule.Catalog.ConceptKey]));
            }
        }
    }

    private static string? NormalizeItemType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        return value.Split('|', 2)[0].Trim().ToUpperInvariant();
    }

    private static IReadOnlyList<string> ReadItemPropertyKeys(JsonElement document)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in new[] { "property", "propertyAdd" })
        {
            if (!CharacterProjectionJson.TryGetProperty(document, field, out var properties)
                || properties.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var property in properties.EnumerateArray())
            {
                string? raw = property.ValueKind switch
                {
                    JsonValueKind.String => property.GetString(),
                    JsonValueKind.Object => CharacterProjectionJson.String(property, "uid")
                        ?? CharacterProjectionJson.String(property, "abbreviation"),
                    _ => null
                };
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    result.Add(raw.Split('|', 2)[0].Trim());
                }
            }
        }
        return result.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool HasItemProperty(JsonElement document, string propertyCode) =>
        ReadItemPropertyKeys(document).Contains(propertyCode, StringComparer.OrdinalIgnoreCase);

    private static int? ReadSignedInteger(JsonElement document, string property)
    {
        if (!CharacterProjectionJson.TryGetProperty(document, property, out var value))
        {
            return null;
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
        {
            return numeric;
        }
        if (value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString()?.Trim();
        return int.TryParse(
            text,
            System.Globalization.NumberStyles.AllowLeadingSign,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
                ? parsed
                : null;
    }
}

