using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Resolves the standard/equipment-driven Armor Class model and open Armor Class contributions.
/// </summary>
internal static class CharacterArmorClassResolver
{
    internal static void Resolve(CharacterProjectionContext context)
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
        var openContributions = CharacterCombatContributionResolver.CollectOpenMechanicContributions(
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
    
}
