using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Resolves 3.x base attack, saving throws, Armor Class variants, grapple, and size-dependent combat modifiers.
/// </summary>
internal static class CharacterThreeXCombatResolver
{
    internal static void Resolve(CharacterProjectionContext context)
    {
        if (context.ThreeXBaseAttackContributions.Count > 0)
        {
            var value = checked(context.ThreeXBaseAttackContributions.Sum(contribution =>
                contribution.NumericValue ?? 0));
            context.Mechanics["combat.base-attack-bonus"] = new CharacterResolvedMechanicView(
                "combat.base-attack-bonus",
                "combat-value",
                "Base Attack Bonus",
                CharacterResolutionStates.Resolved,
                value,
                null,
                null,
                [],
                [],
                [],
                [],
                context.ThreeXBaseAttackContributions.ToArray(),
                CharacterProjectionContext.EmptyProvenance());
        }
    
        var saveAbilities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fortitude"] = "constitution",
            ["reflex"] = "dexterity",
            ["will"] = "wisdom"
        };
    
        foreach (var (save, ability) in saveAbilities)
        {
            if (!context.ThreeXSaveContributions.TryGetValue(save, out var baseContributions)
                || baseContributions.Count == 0)
            {
                continue;
            }
    
            var baseValue = checked(baseContributions.Sum(contribution =>
                contribution.NumericValue ?? 0));
            context.Mechanics[$"save.{save}.base"] = new CharacterResolvedMechanicView(
                $"save.{save}.base",
                "saving-throw-base",
                $"{CharacterProjectionJson.Humanize(save)} Base Save",
                CharacterResolutionStates.Resolved,
                baseValue,
                null,
                null,
                [],
                [],
                [],
                [],
                baseContributions.ToArray(),
                CharacterProjectionContext.EmptyProvenance());
    
            if (!CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, $"ability.{ability}.modifier", out var abilityModifier))
            {
                context.Mechanics[$"save.{save}"] = new CharacterResolvedMechanicView(
                    $"save.{save}",
                    "saving-throw",
                    CharacterProjectionJson.Humanize(save),
                    CharacterResolutionStates.MissingCharacterInput,
                    null,
                    null,
                    null,
                    [$"ability.{ability}.base"],
                    [],
                    [],
                    [],
                    baseContributions.ToArray(),
                    CharacterProjectionContext.EmptyProvenance());
                continue;
            }
    
            var other = context.IntegerFacts.GetValueOrDefault($"save.{save}.other");
            var contributions = new List<CharacterMechanicContributionView>(baseContributions)
            {
                CharacterProjectionResolutionHelpers.Contribution(
                    $"ability.{ability}.modifier",
                    $"{CharacterProjectionJson.Humanize(ability)} modifier",
                    abilityModifier)
            };
            if (other != 0)
            {
                contributions.Add(CharacterProjectionResolutionHelpers.Contribution($"save.{save}.other", "Other modifiers", other));
            }
    
            context.Mechanics[$"save.{save}"] = new CharacterResolvedMechanicView(
                $"save.{save}",
                "saving-throw",
                CharacterProjectionJson.Humanize(save),
                CharacterResolutionStates.Resolved,
                checked(baseValue + abilityModifier + other),
                null,
                null,
                [],
                [],
                [],
                [],
                contributions,
                CharacterProjectionContext.EmptyProvenance());
        }
    
        ResolveThreeXArmorClasses(context);
        ResolveThreeXGrapple(context);
    }
    
    private static void ResolveThreeXArmorClasses(CharacterProjectionContext context)
    {
        var resolvesTouch = context.Capabilities.Contains("defense.ac.touch");
        var resolvesFlatFooted = context.Capabilities.Contains("defense.ac.flat-footed");
        if (!resolvesTouch && !resolvesFlatFooted)
        {
            return;
        }
    
        if (!CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, "ability.dexterity.modifier", out var dexterityModifier))
        {
            if (resolvesTouch)
            {
                context.Mechanics["defense.ac.touch"] = CharacterProjectionResolutionHelpers.Unresolved(
                    "defense.ac.touch",
                    "defense",
                    "Touch Armor Class",
                    CharacterResolutionStates.MissingCharacterInput,
                    ["ability.dexterity.base"]);
            }
            if (resolvesFlatFooted)
            {
                context.Mechanics["defense.ac.flat-footed"] = CharacterProjectionResolutionHelpers.Unresolved(
                    "defense.ac.flat-footed",
                    "defense",
                    "Flat-Footed Armor Class",
                    CharacterResolutionStates.MissingCharacterInput,
                    ["ability.dexterity.base"]);
            }
            return;
        }
    
        int sizeModifier;
        if (context.IntegerFacts.TryGetValue("defense.ac.size-modifier", out var explicitSizeModifier))
        {
            sizeModifier = explicitSizeModifier;
        }
        else if (context.HasSizeCategoryConflict)
        {
            if (resolvesTouch)
            {
                context.Mechanics["defense.ac.touch"] = CharacterProjectionResolutionHelpers.Unresolved(
                    "defense.ac.touch",
                    "defense",
                    "Touch Armor Class",
                    CharacterResolutionStates.Conflict);
            }
            if (resolvesFlatFooted)
            {
                context.Mechanics["defense.ac.flat-footed"] = CharacterProjectionResolutionHelpers.Unresolved(
                    "defense.ac.flat-footed",
                    "defense",
                    "Flat-Footed Armor Class",
                    CharacterResolutionStates.Conflict);
            }
            if (!context.UsesStandardProficiency)
            {
                context.Mechanics["defense.ac.total"] = CharacterProjectionResolutionHelpers.Unresolved(
                    "defense.ac.total",
                    "defense",
                    "Armor Class",
                    CharacterResolutionStates.Conflict);
            }
            return;
        }
        else if (!TryThreeXArmorClassSizeModifier(context.SizeCategory, out sizeModifier))
        {
            if (resolvesTouch)
            {
                context.Mechanics["defense.ac.touch"] = CharacterProjectionResolutionHelpers.Unresolved(
                    "defense.ac.touch",
                    "defense",
                    "Touch Armor Class",
                    CharacterResolutionStates.MissingCharacterInput,
                    ["defense.ac.size-modifier"]);
            }
            if (resolvesFlatFooted)
            {
                context.Mechanics["defense.ac.flat-footed"] = CharacterProjectionResolutionHelpers.Unresolved(
                    "defense.ac.flat-footed",
                    "defense",
                    "Flat-Footed Armor Class",
                    CharacterResolutionStates.MissingCharacterInput,
                    ["defense.ac.size-modifier"]);
            }
            if (!context.UsesStandardProficiency)
            {
                context.Mechanics["defense.ac.total"] = CharacterProjectionResolutionHelpers.Unresolved(
                    "defense.ac.total",
                    "defense",
                    "Armor Class",
                    CharacterResolutionStates.MissingCharacterInput,
                    ["defense.ac.size-modifier"]);
            }
            return;
        }
    
        var dexterityContribution =
            context.IntegerFacts.TryGetValue(
                "defense.ac.dexterity-contribution",
                out var explicitDexterityContribution)
                ? explicitDexterityContribution
                : dexterityModifier;
        var armorBonus = context.IntegerFacts.GetValueOrDefault("defense.ac.armor-bonus");
        var projectedShieldBonuses = context.Effects
            .Where(value =>
                string.Equals(
                    value.TargetKey,
                    "defense.ac.shield-bonus",
                    StringComparison.OrdinalIgnoreCase)
                && value.NumericValue.HasValue)
            .Select(value => value.NumericValue!.Value)
            .ToArray();
        var hasExplicitShieldBonus = context.IntegerFacts.TryGetValue(
            "defense.ac.shield-bonus",
            out var explicitShieldBonus);
        var conflictingProjectedShields =
            !hasExplicitShieldBonus && projectedShieldBonuses.Length > 1;
        var shieldBonus = hasExplicitShieldBonus
            ? explicitShieldBonus
            : projectedShieldBonuses.SingleOrDefault();
        var naturalArmorBonus =
            context.IntegerFacts.GetValueOrDefault("defense.ac.natural-armor-bonus");
        var deflectionBonus =
            context.IntegerFacts.GetValueOrDefault("defense.ac.deflection-bonus");
        var dodgeContribution =
            context.IntegerFacts.GetValueOrDefault("defense.ac.dodge-contribution");
        var openTotalContributions = CharacterCombatContributionResolver.CollectOpenMechanicContributions(
            context,
            "defense.ac.contribution.");
        var openTotal = openTotalContributions.Sum(value => value.NumericValue ?? 0);
        var openTouchContributions = CharacterCombatContributionResolver.CollectOpenMechanicContributions(
            context,
            "defense.ac.touch.contribution.");
        var openTouchTotal = openTouchContributions.Sum(value => value.NumericValue ?? 0);
        var openFlatFootedContributions = CharacterCombatContributionResolver.CollectOpenMechanicContributions(
            context,
            "defense.ac.flat-footed.contribution.");
        var openFlatFootedTotal =
            openFlatFootedContributions.Sum(value => value.NumericValue ?? 0);
    
        var preserveExistingAcConflict =
            context.Mechanics.TryGetValue("defense.ac.total", out var existingArmorClass)
            && existingArmorClass.State is
                CharacterResolutionStates.Conflict
                or CharacterResolutionStates.ApplicableUnresolved;
    
        if (!context.UsesStandardProficiency && !preserveExistingAcConflict)
        {
            var other = context.IntegerFacts.GetValueOrDefault("defense.ac.other");
            context.Mechanics["defense.ac.total"] = new CharacterResolvedMechanicView(
                "defense.ac.total",
                "defense",
                "Armor Class",
                CharacterResolutionStates.Resolved,
                checked(
                    10
                    + armorBonus
                    + shieldBonus
                    + dexterityContribution
                    + sizeModifier
                    + naturalArmorBonus
                    + deflectionBonus
                    + dodgeContribution
                    + other
                    + openTotal),
                null,
                null,
                [],
                [],
                [],
                [],
                [
                    CharacterProjectionResolutionHelpers.Contribution("defense.ac.base", "Base", 10),
                    CharacterProjectionResolutionHelpers.Contribution("defense.ac.armor-bonus", "Armor bonus", armorBonus),
                    CharacterProjectionResolutionHelpers.Contribution("defense.ac.shield-bonus", "Shield bonus", shieldBonus),
                    CharacterProjectionResolutionHelpers.Contribution(
                        "defense.ac.dexterity-contribution",
                        "Dexterity contribution",
                        dexterityContribution),
                    CharacterProjectionResolutionHelpers.Contribution("defense.ac.size-modifier", "Size modifier", sizeModifier),
                    CharacterProjectionResolutionHelpers.Contribution(
                        "defense.ac.natural-armor-bonus",
                        "Natural armor bonus",
                        naturalArmorBonus),
                    CharacterProjectionResolutionHelpers.Contribution(
                        "defense.ac.deflection-bonus",
                        "Deflection bonus",
                        deflectionBonus),
                    CharacterProjectionResolutionHelpers.Contribution(
                        "defense.ac.dodge-contribution",
                        "Dodge contribution",
                        dodgeContribution),
                    CharacterProjectionResolutionHelpers.Contribution("defense.ac.other", "Other modifiers", other),
                    .. openTotalContributions
                ],
                CharacterProjectionContext.EmptyProvenance());
        }
    
        if (resolvesTouch)
        {
            var touchOther =
                context.IntegerFacts.GetValueOrDefault("defense.ac.touch.other");
            context.Mechanics["defense.ac.touch"] = new CharacterResolvedMechanicView(
                "defense.ac.touch",
                "defense",
                "Touch Armor Class",
                CharacterResolutionStates.Resolved,
                checked(
                    10
                    + dexterityContribution
                    + sizeModifier
                    + deflectionBonus
                    + dodgeContribution
                    + touchOther
                    + openTouchTotal),
                null,
                null,
                [],
                [],
                [],
                [],
                [
                    CharacterProjectionResolutionHelpers.Contribution("defense.ac.touch.base", "Base", 10),
                    CharacterProjectionResolutionHelpers.Contribution(
                        "defense.ac.dexterity-contribution",
                        "Dexterity contribution",
                        dexterityContribution),
                    CharacterProjectionResolutionHelpers.Contribution("defense.ac.size-modifier", "Size modifier", sizeModifier),
                    CharacterProjectionResolutionHelpers.Contribution(
                        "defense.ac.deflection-bonus",
                        "Deflection bonus",
                        deflectionBonus),
                    CharacterProjectionResolutionHelpers.Contribution(
                        "defense.ac.dodge-contribution",
                        "Dodge contribution",
                        dodgeContribution),
                    CharacterProjectionResolutionHelpers.Contribution(
                        "defense.ac.touch.other",
                        "Other applicable modifiers",
                        touchOther),
                    .. openTouchContributions
                ],
                CharacterProjectionContext.EmptyProvenance());
        }
    
        if (resolvesFlatFooted && conflictingProjectedShields)
        {
            context.Mechanics["defense.ac.flat-footed"] = CharacterProjectionResolutionHelpers.Unresolved(
                "defense.ac.flat-footed",
                "defense",
                "Flat-Footed Armor Class",
                CharacterResolutionStates.Conflict);
        }
        else if (resolvesFlatFooted)
        {
            var flatFootedDexterityContribution =
                context.IntegerFacts.TryGetValue(
                    "defense.ac.flat-footed.dexterity-contribution",
                    out var explicitFlatFootedDexterityContribution)
                    ? explicitFlatFootedDexterityContribution
                    : Math.Min(dexterityContribution, 0);
            var flatFootedDodgeContribution =
                context.IntegerFacts.GetValueOrDefault(
                    "defense.ac.flat-footed.dodge-contribution");
            var flatFootedOther =
                context.IntegerFacts.GetValueOrDefault(
                    "defense.ac.flat-footed.other");
            context.Mechanics["defense.ac.flat-footed"] =
                new CharacterResolvedMechanicView(
                    "defense.ac.flat-footed",
                    "defense",
                    "Flat-Footed Armor Class",
                    CharacterResolutionStates.Resolved,
                    checked(
                        10
                        + armorBonus
                        + shieldBonus
                        + flatFootedDexterityContribution
                        + sizeModifier
                        + naturalArmorBonus
                        + deflectionBonus
                        + flatFootedDodgeContribution
                        + flatFootedOther
                        + openFlatFootedTotal),
                    null,
                    null,
                    [],
                    [],
                    [],
                    [],
                    [
                        CharacterProjectionResolutionHelpers.Contribution("defense.ac.flat-footed.base", "Base", 10),
                        CharacterProjectionResolutionHelpers.Contribution(
                            "defense.ac.armor-bonus",
                            "Armor bonus",
                            armorBonus),
                        CharacterProjectionResolutionHelpers.Contribution(
                            "defense.ac.shield-bonus",
                            "Shield bonus",
                            shieldBonus),
                        CharacterProjectionResolutionHelpers.Contribution(
                            "defense.ac.flat-footed.dexterity-contribution",
                            "Flat-footed Dexterity contribution",
                            flatFootedDexterityContribution),
                        CharacterProjectionResolutionHelpers.Contribution(
                            "defense.ac.size-modifier",
                            "Size modifier",
                            sizeModifier),
                        CharacterProjectionResolutionHelpers.Contribution(
                            "defense.ac.natural-armor-bonus",
                            "Natural armor bonus",
                            naturalArmorBonus),
                        CharacterProjectionResolutionHelpers.Contribution(
                            "defense.ac.deflection-bonus",
                            "Deflection bonus",
                            deflectionBonus),
                        CharacterProjectionResolutionHelpers.Contribution(
                            "defense.ac.flat-footed.dodge-contribution",
                            "Flat-footed dodge contribution",
                            flatFootedDodgeContribution),
                        CharacterProjectionResolutionHelpers.Contribution(
                            "defense.ac.flat-footed.other",
                            "Other applicable modifiers",
                            flatFootedOther),
                        .. openFlatFootedContributions
                    ],
                    CharacterProjectionContext.EmptyProvenance());
        }
    }
    
    
    private static bool TryThreeXArmorClassSizeModifier(
        string? sizeCategory,
        out int modifier)
    {
        if (!UniversalSizeCategories.TryResolve(sizeCategory, out var size))
        {
            modifier = default;
            return false;
        }
    
        modifier = size.ThreeXArmorClassModifier;
        return true;
    }
    
    private static void ResolveThreeXGrapple(CharacterProjectionContext context)
    {
        if (!CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, "combat.base-attack-bonus", out var baseAttackBonus))
        {
            return;
        }
        if (!CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, "ability.strength.modifier", out var strengthModifier))
        {
            context.Mechanics["combat.grapple"] = CharacterProjectionResolutionHelpers.Unresolved(
                "combat.grapple",
                "combat-value",
                "Grapple",
                CharacterResolutionStates.MissingCharacterInput,
                ["ability.strength.base"]);
            return;
        }
    
        int sizeModifier;
        if (context.IntegerFacts.TryGetValue("combat.grapple.size-modifier", out var explicitSizeModifier))
        {
            sizeModifier = explicitSizeModifier;
        }
        else if (context.HasSizeCategoryConflict)
        {
            context.Mechanics["combat.grapple"] = CharacterProjectionResolutionHelpers.Unresolved(
                "combat.grapple",
                "combat-value",
                "Grapple",
                CharacterResolutionStates.Conflict);
            return;
        }
        else if (!TryThreeXSizeModifier(context.SizeCategory, out sizeModifier))
        {
            context.Mechanics["combat.grapple"] = CharacterProjectionResolutionHelpers.Unresolved(
                "combat.grapple",
                "combat-value",
                "Grapple",
                CharacterResolutionStates.MissingCharacterInput,
                ["combat.grapple.size-modifier"]);
            return;
        }
    
        var other = context.IntegerFacts.GetValueOrDefault("combat.grapple.other");
        context.Mechanics["combat.grapple"] = new CharacterResolvedMechanicView(
            "combat.grapple",
            "combat-value",
            "Grapple",
            CharacterResolutionStates.Resolved,
            checked(baseAttackBonus + strengthModifier + sizeModifier + other),
            null,
            null,
            [],
            [],
            [],
            [],
            [
                CharacterProjectionResolutionHelpers.Contribution("combat.base-attack-bonus", "Base attack bonus", baseAttackBonus),
                CharacterProjectionResolutionHelpers.Contribution("ability.strength.modifier", "Strength modifier", strengthModifier),
                CharacterProjectionResolutionHelpers.Contribution("combat.grapple.size-modifier", "Size modifier", sizeModifier),
                CharacterProjectionResolutionHelpers.Contribution("combat.grapple.other", "Other modifiers", other)
            ],
            CharacterProjectionContext.EmptyProvenance());
    }
    
    private static bool TryThreeXSizeModifier(string? sizeCategory, out int modifier)
    {
        if (!UniversalSizeCategories.TryResolve(sizeCategory, out var size))
        {
            modifier = default;
            return false;
        }
    
        modifier = size.ThreeXGrappleModifier;
        return true;
    }
    
}
