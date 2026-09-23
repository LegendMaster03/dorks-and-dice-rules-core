using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Registers rule/catalog identities and selectable metadata before rule projection modules run.
/// </summary>
internal static class CharacterProjectionCatalogRegistrar
{
    internal static void RegisterRuleMetadata(
        CharacterProjectionContext context,
        CharacterProjectionRule rule)
    {
        RegisterToolChoiceCategory(context, rule);
        RegisterLanguageChoiceIdentity(context, rule);
        RegisterWeaponCatalogEntry(context, rule);
        RegisterFeatureCatalogEntry(context, rule);
    }

    internal static void RegisterMechanicCatalogIdentities(
        CharacterProjectionContext context,
        CharacterMechanicsCatalogView catalog)
    {
        foreach (var mechanic in catalog.Mechanics.Where(value => value.Competency is not null))
        {
            var competency = mechanic.Competency!;
            var competencyConceptKey = mechanic.ConceptKey
                ?? throw new InvalidOperationException(
                    $"Competency mechanic '{mechanic.MechanicKey}' does not expose a concept key.");
            context.RegisterCompetencyIdentity(
                mechanic.DisplayName,
                competencyConceptKey,
                competency.FamilyName,
                competency.Specialty,
                competency.CompetencyKind);
        }
    }
    
    private static void RegisterToolChoiceCategory(
        CharacterProjectionContext context,
        CharacterProjectionRule rule)
    {
        if (!string.Equals(
                rule.Catalog.EntityType,
                "tool",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
    
        var category = ReadNormalizedToolCategory(rule.Document);
        if (category is not null)
        {
            context.RegisterToolChoiceCategory(rule.Catalog.ConceptKey, category);
        }
    }
    
    private static void RegisterLanguageChoiceIdentity(
        CharacterProjectionContext context,
        CharacterProjectionRule rule)
    {
        if (!string.Equals(
                rule.Catalog.EntityType,
                "language",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
    
        string? category = null;
        if (CharacterProjectionJson.TryGetProperty(rule.Document, "_rulesCore", out var rulesCore)
            && CharacterProjectionJson.TryGetProperty(rulesCore, "languageCategory", out var normalized)
            && normalized.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(normalized.GetString()))
        {
            category = normalized.GetString()!.Trim();
        }
        else
        {
            category = CharacterProjectionJson.String(rule.Document, "type");
        }
    
        context.RegisterLanguageChoiceIdentity(
            rule.Catalog.ConceptKey,
            rule.Catalog.DisplayName,
            category);
    }
    
    private static void RegisterWeaponCatalogEntry(
        CharacterProjectionContext context,
        CharacterProjectionRule rule)
    {
        if (!string.Equals(
                rule.Catalog.EntityType,
                "item",
                StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                rule.Catalog.EntityType,
                "baseitem",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
    
        var rawType = CharacterProjectionJson.String(rule.Document, "type");
        if (string.IsNullOrWhiteSpace(rawType))
        {
            return;
        }
    
        var itemType = rawType
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault()?
            .Trim()
            .ToUpperInvariant();
        if (itemType is not ("M" or "R"))
        {
            return;
        }
    
        var properties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in new[] { "property", "propertyAdd" })
        {
            if (!CharacterProjectionJson.TryGetProperty(rule.Document, field, out var values)
                || values.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
    
            foreach (var value in values.EnumerateArray())
            {
                var raw = value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Object => CharacterProjectionJson.String(value, "uid")
                        ?? CharacterProjectionJson.String(value, "abbreviation"),
                    _ => null
                };
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }
    
                var property = raw
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault()?
                    .Trim();
                if (string.IsNullOrWhiteSpace(property))
                {
                    continue;
                }
    
                properties.Add(property.ToUpperInvariant() switch
                {
                    "F" => "finesse",
                    "L" => "light",
                    _ => property.ToLowerInvariant()
                });
            }
        }
    
        context.RegisterWeaponCatalogEntry(
            rule.Catalog.ConceptKey,
            rule.Catalog.DisplayName,
            itemType,
            CharacterProjectionJson.String(rule.Document, "weaponCategory"),
            properties,
            rule.Provenance);
    }
    
    private static string? ReadNormalizedToolCategory(JsonElement document)
    {
        if (CharacterProjectionJson.TryGetProperty(document, "_rulesCore", out var rulesCore)
            && CharacterProjectionJson.TryGetProperty(rulesCore, "toolCategory", out var normalized)
            && normalized.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(normalized.GetString()))
        {
            return CharacterProjectionJson.NormalizeResourceSystemKey(normalized.GetString()!);
        }
    
        var rawType = CharacterProjectionJson.String(document, "type");
        if (string.IsNullOrWhiteSpace(rawType))
        {
            return null;
        }
    
        var type = rawType
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault()?
            .Trim()
            .ToUpperInvariant();
        return type switch
        {
            "AT" => "artisans-tool",
            "INS" => "musical-instrument",
            "GS" => "gaming-set",
            "T" => "tool",
            _ => null
        };
    }
    
    private static void RegisterFeatureCatalogEntry(
        CharacterProjectionContext context,
        CharacterProjectionRule rule)
    {
        var isClassFeature = string.Equals(
            rule.Catalog.EntityType,
            "classFeature",
            StringComparison.OrdinalIgnoreCase);
        var isSubclassFeature = string.Equals(
            rule.Catalog.EntityType,
            "subclassFeature",
            StringComparison.OrdinalIgnoreCase);
        if (!isClassFeature && !isSubclassFeature)
        {
            return;
        }
    
        var className = CharacterProjectionJson.String(rule.Document, "className");
        var level = CharacterProjectionJson.Integer(rule.Document, "level");
        if (string.IsNullOrWhiteSpace(className) || level is null or <= 0)
        {
            return;
        }
    
        var subclassName = isSubclassFeature
            ? CharacterProjectionJson.String(rule.Document, "subclassShortName")
                ?? CharacterProjectionJson.String(rule.Document, "subclassName")
            : null;
        if (isSubclassFeature && string.IsNullOrWhiteSpace(subclassName))
        {
            return;
        }
    
        context.FeatureCatalog.Add(new CharacterFeatureCatalogEntry(
            rule.Catalog.ConceptKey,
            rule.Catalog.EntityType,
            rule.Catalog.DisplayName,
            className.Trim(),
            CharacterProjectionJson.String(rule.Document, "classSource"),
            subclassName?.Trim(),
            isSubclassFeature
                ? CharacterProjectionJson.String(rule.Document, "subclassSource")
                : null,
            level.Value,
            rule.Catalog.SourceCode,
            ReadNormalizedFeatureEffects(rule),
            rule.Provenance));
    }
    
    private static IReadOnlyList<CharacterRuleEffectView> ReadNormalizedFeatureEffects(
        CharacterProjectionRule rule)
    {
        if (!CharacterProjectionJson.TryGetProperty(rule.Document, "_rulesCore", out var rulesCore)
            || !CharacterProjectionJson.TryGetProperty(rulesCore, "character", out var character)
            || !CharacterProjectionJson.TryGetProperty(character, "effects", out var effects)
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
                    ?? $"{rule.Catalog.ConceptKey}.effect.{index}",
                CharacterProjectionJson.String(item, "kind") ?? CharacterEffectKinds.Other,
                CharacterProjectionJson.String(item, "operation")
                    ?? CharacterEffectOperations.Add,
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
    
}
