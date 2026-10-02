using System.Text.Json;
using RulesCore.Application.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Projects 2024 Weapon Mastery grants as ordinary Rules Core runtime choices.
/// Weapon mastery properties and scalable class selection counts come from structured source data;
/// fixed class and feat grants use reviewed source semantics after the relevant source feature has
/// been confirmed.
/// </summary>
internal static class CharacterWeaponMasteryProjector
{
    private const string MasteryLabel = "Weapon Mastery";
    private const string WeaponMasterFeatName = "Weapon Master";
    private const string RevisedPlayersHandbookSource = "XPHB";

    internal static void Project(
        IReadOnlyList<CharacterProjectionRule> rules,
        CharacterProjectionContext context)
    {
        var masteryByWeapon = BuildWeaponMasteryCatalog(rules, context);
        if (masteryByWeapon.Count == 0)
        {
            return;
        }

        ProjectClassMasteries(rules, context, masteryByWeapon);
        ProjectWeaponMasterFeat(rules, context, masteryByWeapon);
    }

    private static void ProjectClassMasteries(
        IReadOnlyList<CharacterProjectionRule> rules,
        CharacterProjectionContext context,
        IReadOnlyDictionary<string, MasteryWeapon> masteryByWeapon)
    {
        foreach (var rule in rules.Where(value =>
                     string.Equals(value.Catalog.EntityType, "class", StringComparison.OrdinalIgnoreCase)
                     && context.IsSelected(value.Catalog.ConceptKey)))
        {
            var classLevel = context.AdvancementLevel(rule.Catalog.ConceptKey);
            if (classLevel <= 0 || !HasActiveWeaponMasteryFeature(rule.Document, classLevel))
            {
                continue;
            }

            var masteryClassName = MasteryClassName(rule.Document, rule.Catalog.DisplayName);
            var count = ReadTableMasteryCount(rule.Document, classLevel)
                ?? FixedMasteryCount(masteryClassName);
            if (count is null or <= 0)
            {
                continue;
            }

            var classKey = NormalizeClassKey(masteryClassName);
            var options = BuildOptions(
                masteryByWeapon.Values.Where(value =>
                    IsEligibleWeapon(classKey, value.Weapon, context)));

            CharacterStartingProficiencyProjector.ProjectChoiceGroup(
                rule,
                context,
                pathKey: "weapon-mastery",
                groupIndex: 0,
                count: count.Value,
                options,
                sourceShape: "weapon-mastery",
                kind: "weapon-mastery",
                optionLabel: MasteryLabel,
                onSelected: _ => { });
        }
    }

    private static void ProjectWeaponMasterFeat(
        IReadOnlyList<CharacterProjectionRule> rules,
        CharacterProjectionContext context,
        IReadOnlyDictionary<string, MasteryWeapon> masteryByWeapon)
    {
        foreach (var rule in rules.Where(value =>
                     string.Equals(value.Catalog.EntityType, "feat", StringComparison.OrdinalIgnoreCase)
                     && context.IsSelected(value.Catalog.ConceptKey)
                     && IsWeaponMasterFeat(value.Document)))
        {
            var options = BuildOptions(
                masteryByWeapon.Values.Where(value =>
                    IsSimpleOrMartialCategory(value.Weapon.WeaponCategory)
                    && HasWeaponProficiency(context, value.Weapon)));

            CharacterStartingProficiencyProjector.ProjectChoiceGroup(
                rule,
                context,
                pathKey: "weapon-mastery",
                groupIndex: 0,
                count: 1,
                options,
                sourceShape: "weapon-master-feat",
                kind: "weapon-mastery",
                optionLabel: MasteryLabel,
                onSelected: _ => { });
        }
    }

    private static CharacterChoiceOptionView[] BuildOptions(IEnumerable<MasteryWeapon> weapons) =>
        weapons
            .Select(value => new CharacterChoiceOptionView(
                value.Weapon.ConceptKey,
                $"{value.Weapon.DisplayName} — {value.MasteryProperty}",
                value.Weapon.ConceptKey))
            .OrderBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyDictionary<string, MasteryWeapon> BuildWeaponMasteryCatalog(
        IReadOnlyList<CharacterProjectionRule> rules,
        CharacterProjectionContext context)
    {
        var result = new Dictionary<string, MasteryWeapon>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules.Where(value =>
                     string.Equals(value.Catalog.EntityType, "item", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(value.Catalog.EntityType, "baseitem", StringComparison.OrdinalIgnoreCase)))
        {
            if (!context.WeaponCatalog.TryGetValue(rule.Catalog.ConceptKey, out var weapon)
                || !CharacterProjectionJson.TryGetProperty(rule.Document, "mastery", out var mastery)
                || mastery.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var raw = mastery.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString())
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var property = raw.Split('|', 2, StringSplitOptions.TrimEntries)[0];
            if (string.IsNullOrWhiteSpace(property))
            {
                continue;
            }

            result[weapon.ConceptKey] = new MasteryWeapon(weapon, property);
        }

        return result;
    }

    private static bool HasActiveWeaponMasteryFeature(JsonElement classDocument, int classLevel)
    {
        if (!CharacterProjectionJson.TryGetProperty(classDocument, "classFeatures", out var features)
            || features.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var feature in EnumerateFeatureReferences(features))
        {
            var parts = feature.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length == 0
                || !string.Equals(parts[0], MasteryLabel, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var acquisitionLevel = parts.Reverse()
                .Select(value => int.TryParse(value, out var parsed) ? parsed : (int?)null)
                .FirstOrDefault(value => value.HasValue);
            if (!acquisitionLevel.HasValue || classLevel >= acquisitionLevel.Value)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWeaponMasterFeat(JsonElement featDocument) =>
        string.Equals(
            CharacterProjectionJson.String(featDocument, "name"),
            WeaponMasterFeatName,
            StringComparison.OrdinalIgnoreCase)
        && string.Equals(
            CharacterProjectionJson.String(featDocument, "source"),
            RevisedPlayersHandbookSource,
            StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumerateFeatureReferences(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
            yield break;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                foreach (var value in EnumerateFeatureReferences(child))
                {
                    yield return value;
                }
            }
            yield break;
        }

        if (element.ValueKind == JsonValueKind.Object
            && CharacterProjectionJson.TryGetProperty(element, "classFeature", out var reference))
        {
            foreach (var value in EnumerateFeatureReferences(reference))
            {
                yield return value;
            }
        }
    }

    private static int? ReadTableMasteryCount(JsonElement classDocument, int classLevel)
    {
        if (!CharacterProjectionJson.TryGetProperty(classDocument, "classTableGroups", out var groups)
            || groups.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var group in groups.EnumerateArray())
        {
            if (!CharacterProjectionJson.TryGetProperty(group, "colLabels", out var labels)
                || labels.ValueKind != JsonValueKind.Array
                || !CharacterProjectionJson.TryGetProperty(group, "rows", out var rows)
                || rows.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var labelArray = labels.EnumerateArray().ToArray();
            var column = Array.FindIndex(
                labelArray,
                value => value.ValueKind == JsonValueKind.String
                    && string.Equals(value.GetString(), MasteryLabel, StringComparison.OrdinalIgnoreCase));
            if (column < 0)
            {
                continue;
            }

            var rowArray = rows.EnumerateArray().ToArray();
            if (classLevel > rowArray.Length || rowArray[classLevel - 1].ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var cells = rowArray[classLevel - 1].EnumerateArray().ToArray();
            if (column >= cells.Length)
            {
                return null;
            }

            return ReadIntegerCell(cells[column]);
        }

        return null;
    }

    private static int? ReadIntegerCell(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
        {
            return number;
        }
        if (value.ValueKind == JsonValueKind.Object
            && CharacterProjectionJson.TryGetProperty(value, "value", out var nested))
        {
            return ReadIntegerCell(nested);
        }
        return null;
    }

    private static string MasteryClassName(JsonElement classDocument, string fallbackDisplayName)
    {
        var sourceName = CharacterProjectionJson.String(classDocument, "name");
        return string.IsNullOrWhiteSpace(sourceName)
            ? fallbackDisplayName
            : sourceName.Trim();
    }

    private static int? FixedMasteryCount(string className) =>
        NormalizeClassKey(className) switch
        {
            "paladin" or "ranger" or "rogue" => 2,
            _ => null
        };

    private static bool IsEligibleWeapon(
        string classKey,
        CharacterWeaponCatalogEntry weapon,
        CharacterProjectionContext context)
    {
        if (!IsSimpleOrMartialCategory(weapon.WeaponCategory))
        {
            return false;
        }

        return classKey switch
        {
            "fighter" => true,
            "barbarian" => string.Equals(weapon.ItemType, "M", StringComparison.OrdinalIgnoreCase),
            "paladin" or "ranger" or "rogue" => HasWeaponProficiency(context, weapon),
            _ => false
        };
    }

    private static bool IsSimpleOrMartialCategory(string? weaponCategory) =>
        string.Equals(weaponCategory, "simple", StringComparison.OrdinalIgnoreCase)
        || string.Equals(weaponCategory, "martial", StringComparison.OrdinalIgnoreCase);

    private static bool HasWeaponProficiency(
        CharacterProjectionContext context,
        CharacterWeaponCatalogEntry weapon)
    {
        var candidates = new List<string>
        {
            $"weapon.{weapon.ConceptKey}.proficient",
            $"qualification.weapons.{SlugKey(weapon.DisplayName)}"
        };
        if (!string.IsNullOrWhiteSpace(weapon.WeaponCategory))
        {
            candidates.Add($"qualification.weapons.{SlugKey(weapon.WeaponCategory)}");
            candidates.Add($"qualification.weapons.{SlugKey($"{weapon.WeaponCategory} weapons")}");
        }

        return candidates.Any(context.Capabilities.Contains);
    }

    private static string NormalizeClassKey(string value) =>
        SlugKey(value).Split('-', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

    private static string SlugKey(string value) =>
        string.Join(
            '-',
            value.Trim().ToLowerInvariant()
                .Split(
                    [' ', '/', '_', '-', '|'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private sealed record MasteryWeapon(
        CharacterWeaponCatalogEntry Weapon,
        string MasteryProperty);
}
