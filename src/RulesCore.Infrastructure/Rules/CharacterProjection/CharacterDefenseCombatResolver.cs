using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Resolves armor class, 3.x armor variants, base attack/grapple, open combat effects, and size modifiers.
/// </summary>
internal static class CharacterDefenseCombatResolver
{
    internal static void ResolveArmorClass(CharacterProjectionContext context)
    {
        var armor = context.Effects
            .Where(value =>
                string.Equals(value.Kind, CharacterEffectKinds.MechanicContribution, StringComparison.OrdinalIgnoreCase)
                && string.Equals(value.TargetKey, "defense.ac.armor-base", StringComparison.OrdinalIgnoreCase)
                && value.NumericValue.HasValue)
            .ToArray();
        var shields = context.Effects
            .Where(value =>
                string.Equals(value.Kind, CharacterEffectKinds.MechanicContribution, StringComparison.OrdinalIgnoreCase)
                && string.Equals(value.TargetKey, "defense.ac.shield-bonus", StringComparison.OrdinalIgnoreCase)
                && value.NumericValue.HasValue)
            .ToArray();
        var unclassified = context.Effects
            .Where(value =>
                string.Equals(value.Kind, CharacterEffectKinds.MechanicContribution, StringComparison.OrdinalIgnoreCase)
                && string.Equals(value.TargetKey, "defense.ac.unclassified", StringComparison.OrdinalIgnoreCase)
                && value.NumericValue.HasValue)
            .ToArray();
    
        var hasDexterityInput =
            context.BaseAbilityScores.ContainsKey("dexterity")
            || context.BaseAbilityScores.ContainsKey("ability.dexterity.base")
            || context.IntegerFacts.ContainsKey("ability.dexterity.base");
        if (armor.Length == 0
            && shields.Length == 0
            && unclassified.Length == 0
            && !hasDexterityInput)
        {
            return;
        }
    
        if (unclassified.Length > 0)
        {
            context.Mechanics["defense.ac.total"] = CharacterProjectionResolutionHelpers.Unresolved(
                "defense.ac.total",
                "defense",
                "Armor Class",
                CharacterResolutionStates.ApplicableUnresolved,
                ["defense.ac.unclassified-item-role"]);
            context.Conflicts.Add(new CharacterProjectionConflictView(
                "conflict.defense.ac.unclassified",
                "unclassified-ac-contribution",
                "At least one equipped item exposes an Armor Class value without a normalized armor or shield role, so Rules Core will not guess how it stacks.",
                ["defense.ac.total"],
                unclassified
                    .Select(value => value.SourceConceptKey)
                    .Where(value => value is not null)
                    .Cast<string>()
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()));
            return;
        }
    
        if (armor.Length > 1)
        {
            context.Mechanics["defense.ac.total"] = CharacterProjectionResolutionHelpers.Unresolved(
                "defense.ac.total",
                "defense",
                "Armor Class",
                CharacterResolutionStates.Conflict);
            context.Conflicts.Add(new CharacterProjectionConflictView(
                "conflict.defense.ac.multiple-armor",
                "multiple-armor-formulas",
                "More than one equipped item provides an armor base formula. Rules Core will not choose one implicitly.",
                ["defense.ac.total"],
                armor
                    .Select(value => value.SourceConceptKey)
                    .Where(value => value is not null)
                    .Cast<string>()
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()));
            return;
        }
    
        if (shields.Length > 1)
        {
            context.Mechanics["defense.ac.total"] = CharacterProjectionResolutionHelpers.Unresolved(
                "defense.ac.total",
                "defense",
                "Armor Class",
                CharacterResolutionStates.Conflict);
            context.Conflicts.Add(new CharacterProjectionConflictView(
                "conflict.defense.ac.multiple-shields",
                "multiple-shield-bonuses",
                "More than one equipped shield provides an Armor Class bonus. Rules Core will not stack them implicitly.",
                ["defense.ac.total"],
                shields
                    .Select(value => value.SourceConceptKey)
                    .Where(value => value is not null)
                    .Cast<string>()
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()));
            return;
        }
    
        if (!CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, "ability.dexterity.modifier", out var dexterityModifier))
        {
            context.Mechanics["defense.ac.total"] = CharacterProjectionResolutionHelpers.Unresolved(
                "defense.ac.total",
                "defense",
                "Armor Class",
                CharacterResolutionStates.MissingCharacterInput,
                ["ability.dexterity.base"]);
            return;
        }
    
        var shieldBonus = shields.SingleOrDefault()?.NumericValue ?? 0;
        var other = context.IntegerFacts.GetValueOrDefault("defense.ac.other");
        var openContributions = CollectOpenMechanicContributions(
            context,
            "defense.ac.contribution.");
        var openContributionTotal = openContributions.Sum(value => value.NumericValue ?? 0);
    
        if (armor.Length == 0)
        {
            var total = checked(
                10 + dexterityModifier + shieldBonus + other + openContributionTotal);
            var contributions = new List<CharacterMechanicContributionView>
            {
                new(
                    "defense.ac.unarmored-base",
                    "Unarmored base",
                    CharacterEffectOperations.Set,
                    10,
                    null,
                    null,
                    CharacterProjectionContext.EmptyProvenance()),
                CharacterProjectionResolutionHelpers.Contribution(
                    "ability.dexterity.modifier",
                    "Dexterity contribution",
                    dexterityModifier)
            };
            if (shields.Length == 1)
            {
                var shield = shields[0];
                contributions.Add(new CharacterMechanicContributionView(
                    shield.EffectKey,
                    "Shield bonus",
                    CharacterEffectOperations.Add,
                    shieldBonus,
                    shield.TextValue,
                    shield.SourceConceptKey,
                    shield.Provenance));
            }
            if (other != 0)
            {
                contributions.Add(CharacterProjectionResolutionHelpers.Contribution("defense.ac.other", "Other modifiers", other));
            }
            contributions.AddRange(openContributions);
    
            context.Mechanics["defense.ac.unarmored-base"] = new CharacterResolvedMechanicView(
                "defense.ac.unarmored-base",
                "defense",
                "Unarmored Armor Class Base",
                CharacterResolutionStates.Resolved,
                10,
                null,
                null,
                [],
                [],
                [],
                [],
                [contributions[0]],
                CharacterProjectionContext.EmptyProvenance());
            context.Mechanics["defense.ac.dexterity-contribution"] = new CharacterResolvedMechanicView(
                "defense.ac.dexterity-contribution",
                "defense",
                "Armor Class Dexterity Contribution",
                CharacterResolutionStates.Resolved,
                dexterityModifier,
                null,
                null,
                [],
                [],
                [],
                [],
                [contributions[1]],
                CharacterProjectionContext.EmptyProvenance());
            if (shields.Length == 1)
            {
                context.Mechanics["defense.ac.shield-bonus"] = new CharacterResolvedMechanicView(
                    "defense.ac.shield-bonus",
                    "defense",
                    "Shield Bonus",
                    CharacterResolutionStates.Resolved,
                    shieldBonus,
                    null,
                    null,
                    [],
                    [],
                    [],
                    [],
                    [contributions.Single(value => value.Label == "Shield bonus")],
                    shields[0].Provenance);
            }
            context.Mechanics["defense.ac.total"] = new CharacterResolvedMechanicView(
                "defense.ac.total",
                "defense",
                "Armor Class",
                CharacterResolutionStates.Resolved,
                total,
                null,
                null,
                [],
                [],
                [],
                [],
                contributions,
                shields.SingleOrDefault()?.Provenance
                    ?? CharacterProjectionContext.EmptyProvenance());
            return;
        }
    
        var armorEffect = armor[0];
        var armorBase = armorEffect.NumericValue!.Value;
        var dexterityContribution = armorEffect.TextValue?.Trim().ToUpperInvariant() switch
        {
            "LA" => dexterityModifier,
            "MA" => Math.Min(dexterityModifier, 2),
            "HA" => 0,
            _ => int.MinValue
        };
        if (dexterityContribution == int.MinValue)
        {
            context.Mechanics["defense.ac.total"] = CharacterProjectionResolutionHelpers.Unresolved(
                "defense.ac.total",
                "defense",
                "Armor Class",
                CharacterResolutionStates.ApplicableUnresolved,
                ["defense.ac.armor-role"]);
            return;
        }
    
        var armoredTotal = checked(
            armorBase
            + dexterityContribution
            + shieldBonus
            + other
            + openContributionTotal);
        var armoredContributions = new List<CharacterMechanicContributionView>
        {
            new(
                armorEffect.EffectKey,
                "Armor base",
                CharacterEffectOperations.Set,
                armorBase,
                armorEffect.TextValue,
                armorEffect.SourceConceptKey,
                armorEffect.Provenance),
            CharacterProjectionResolutionHelpers.Contribution(
                "ability.dexterity.modifier",
                "Dexterity contribution",
                dexterityContribution)
        };
        if (shields.Length == 1)
        {
            var shield = shields[0];
            armoredContributions.Add(new CharacterMechanicContributionView(
                shield.EffectKey,
                "Shield bonus",
                CharacterEffectOperations.Add,
                shieldBonus,
                shield.TextValue,
                shield.SourceConceptKey,
                shield.Provenance));
        }
        if (other != 0)
        {
            armoredContributions.Add(CharacterProjectionResolutionHelpers.Contribution("defense.ac.other", "Other modifiers", other));
        }
        armoredContributions.AddRange(openContributions);
    
        context.Mechanics["defense.ac.armor-base"] = new CharacterResolvedMechanicView(
            "defense.ac.armor-base",
            "defense",
            "Armor Base",
            CharacterResolutionStates.Resolved,
            armorBase,
            armorEffect.TextValue,
            null,
            [],
            [],
            [],
            [],
            [armoredContributions[0]],
            armorEffect.Provenance);
        context.Mechanics["defense.ac.dexterity-contribution"] = new CharacterResolvedMechanicView(
            "defense.ac.dexterity-contribution",
            "defense",
            "Armor Class Dexterity Contribution",
            CharacterResolutionStates.Resolved,
            dexterityContribution,
            null,
            null,
            [],
            [],
            [],
            [],
            [armoredContributions[1]],
            CharacterProjectionContext.EmptyProvenance());
        if (shields.Length == 1)
        {
            context.Mechanics["defense.ac.shield-bonus"] = new CharacterResolvedMechanicView(
                "defense.ac.shield-bonus",
                "defense",
                "Shield Bonus",
                CharacterResolutionStates.Resolved,
                shieldBonus,
                null,
                null,
                [],
                [],
                [],
                [],
                [armoredContributions.Single(value => value.Label == "Shield bonus")],
                shields[0].Provenance);
        }
        context.Mechanics["defense.ac.total"] = new CharacterResolvedMechanicView(
            "defense.ac.total",
            "defense",
            "Armor Class",
            CharacterResolutionStates.Resolved,
            armoredTotal,
            null,
            null,
            [],
            [],
            [],
            [],
            armoredContributions,
            armorEffect.Provenance);
    }
    
    internal static void ResolveThreeXCombatMechanics(CharacterProjectionContext context)
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
        var openTotalContributions = CollectOpenMechanicContributions(
            context,
            "defense.ac.contribution.");
        var openTotal = openTotalContributions.Sum(value => value.NumericValue ?? 0);
        var openTouchContributions = CollectOpenMechanicContributions(
            context,
            "defense.ac.touch.contribution.");
        var openTouchTotal = openTouchContributions.Sum(value => value.NumericValue ?? 0);
        var openFlatFootedContributions = CollectOpenMechanicContributions(
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
    
    private static IReadOnlyList<CharacterMechanicContributionView> CollectOpenMechanicContributions(
        CharacterProjectionContext context,
        string targetPrefix)
    {
        var result = new List<CharacterMechanicContributionView>();
    
        foreach (var (key, value) in context.IntegerFacts
                     .Where(pair => pair.Key.StartsWith(
                         targetPrefix,
                         StringComparison.OrdinalIgnoreCase))
                     .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (key.Length <= targetPrefix.Length)
            {
                continue;
            }
            result.Add(CharacterProjectionResolutionHelpers.Contribution(
                key,
                CharacterProjectionJson.Humanize(key),
                value));
        }
    
        foreach (var effect in context.Effects
                     .Where(value =>
                         string.Equals(
                             value.Kind,
                             CharacterEffectKinds.MechanicContribution,
                             StringComparison.OrdinalIgnoreCase)
                         && string.Equals(
                             value.Operation,
                             CharacterEffectOperations.Add,
                             StringComparison.OrdinalIgnoreCase)
                         && value.NumericValue.HasValue
                         && value.TargetKey.StartsWith(
                             targetPrefix,
                             StringComparison.OrdinalIgnoreCase)
                         && IsEffectActive(context, value))
                     .OrderBy(value => value.EffectKey, StringComparer.OrdinalIgnoreCase))
        {
            result.Add(new CharacterMechanicContributionView(
                effect.EffectKey,
                CharacterProjectionJson.Humanize(effect.TargetKey),
                effect.Operation,
                effect.NumericValue,
                effect.TextValue,
                effect.SourceConceptKey,
                effect.Provenance,
                !string.IsNullOrWhiteSpace(effect.ConditionKey)
                    ? "temporary"
                    : "persistent",
                effect.ConditionKey));
        }
    
        return result;
    }
    
    private static bool IsEffectActive(
        CharacterProjectionContext context,
        CharacterRuleEffectView effect) =>
        string.IsNullOrWhiteSpace(effect.ConditionKey)
        || context.ActiveConditions.Contains(effect.ConditionKey)
        || (context.BooleanFacts.TryGetValue(effect.ConditionKey, out var active)
            && active);
    
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
