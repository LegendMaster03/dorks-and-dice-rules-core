using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Resolves weapon attack profiles, proficiency state, attack bonuses, and damage expressions.
/// </summary>
internal static class CharacterWeaponAttackResolver
{
    internal static void Resolve(CharacterProjectionContext context)
    {
        foreach (var weapon in context.WeaponAttacks.Values)
        {
            var attackKey = $"attack.{weapon.ConceptKey}";
            var damageModifierKey = $"damage.{weapon.ConceptKey}.modifier";
            var actionKey = $"action.attack.{weapon.ConceptKey}";
            var choiceKey = $"weapon.{weapon.ConceptKey}.attack-ability";
    
            var ability = weapon.ItemType switch
            {
                "R" => "dexterity",
                "M" when !weapon.Finesse => "strength",
                _ => null
            };
    
            if (weapon.Finesse)
            {
                if (!context.Choices.TryGetValue(choiceKey, out var selectedAbility))
                {
                    context.Mechanics[attackKey] = CharacterProjectionResolutionHelpers.Unresolved(
                        attackKey,
                        "attack-bonus",
                        $"{weapon.DisplayName} Attack Bonus",
                        CharacterResolutionStates.ChoiceRequired,
                        requiredChoices: [choiceKey],
                        provenance: weapon.Provenance);
                    context.Mechanics[damageModifierKey] = CharacterProjectionResolutionHelpers.Unresolved(
                        damageModifierKey,
                        "damage-modifier",
                        $"{weapon.DisplayName} Damage Modifier",
                        CharacterResolutionStates.ChoiceRequired,
                        requiredChoices: [choiceKey],
                        provenance: weapon.Provenance);
                    SetWeaponActionState(
                        context,
                        actionKey,
                        CharacterResolutionStates.ChoiceRequired,
                        weapon.DamageExpression);
                    continue;
                }
    
                var normalizedAbility = CharacterProjectionJson.NormalizeAbilityKey(selectedAbility);
                if (normalizedAbility is not ("strength" or "dexterity"))
                {
                    context.Mechanics[attackKey] = CharacterProjectionResolutionHelpers.Unresolved(
                        attackKey,
                        "attack-bonus",
                        $"{weapon.DisplayName} Attack Bonus",
                        CharacterResolutionStates.ChoiceRequired,
                        requiredChoices: [choiceKey],
                        provenance: weapon.Provenance);
                    context.Conflicts.Add(new CharacterProjectionConflictView(
                        $"conflict.{choiceKey}",
                        "invalid-runtime-choice",
                        $"Finesse weapon '{weapon.DisplayName}' requires Strength or Dexterity for '{choiceKey}'.",
                        [attackKey, damageModifierKey],
                        [weapon.ConceptKey]));
                    SetWeaponActionState(
                        context,
                        actionKey,
                        CharacterResolutionStates.ChoiceRequired,
                        weapon.DamageExpression);
                    continue;
                }
                ability = normalizedAbility;
            }
    
            if (ability is null
                || !CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, $"ability.{ability}.modifier", out var abilityModifier))
            {
                context.Mechanics[attackKey] = CharacterProjectionResolutionHelpers.Unresolved(
                    attackKey,
                    "attack-bonus",
                    $"{weapon.DisplayName} Attack Bonus",
                    CharacterResolutionStates.MissingCharacterInput,
                    [$"ability.{ability ?? "weapon"}.base"],
                    provenance: weapon.Provenance);
                context.Mechanics[damageModifierKey] = CharacterProjectionResolutionHelpers.Unresolved(
                    damageModifierKey,
                    "damage-modifier",
                    $"{weapon.DisplayName} Damage Modifier",
                    CharacterResolutionStates.MissingCharacterInput,
                    [$"ability.{ability ?? "weapon"}.base"],
                    provenance: weapon.Provenance);
                SetWeaponActionState(
                    context,
                    actionKey,
                    CharacterResolutionStates.MissingCharacterInput,
                    weapon.DamageExpression);
                continue;
            }
    
            var damageOther = context.IntegerFacts.GetValueOrDefault(
                $"damage.{weapon.ConceptKey}.other");
            var damageModifier = checked(
                abilityModifier
                + weapon.DamageBonus
                + damageOther);
            var damageContributions = new List<CharacterMechanicContributionView>
            {
                CharacterProjectionResolutionHelpers.Contribution(
                    $"ability.{ability}.modifier",
                    $"{CharacterProjectionJson.Humanize(ability)} modifier",
                    abilityModifier)
            };
            if (weapon.DamageBonus != 0)
            {
                damageContributions.Add(new CharacterMechanicContributionView(
                    $"{weapon.ConceptKey}.weapon-damage-bonus",
                    "Weapon damage bonus",
                    CharacterEffectOperations.Add,
                    weapon.DamageBonus,
                    null,
                    weapon.ConceptKey,
                    weapon.Provenance));
            }
            if (damageOther != 0)
            {
                damageContributions.Add(CharacterProjectionResolutionHelpers.Contribution(
                    $"damage.{weapon.ConceptKey}.other",
                    "Other damage modifiers",
                    damageOther));
            }
            context.Mechanics[damageModifierKey] = new CharacterResolvedMechanicView(
                damageModifierKey,
                "damage-modifier",
                $"{weapon.DisplayName} Damage Modifier",
                CharacterResolutionStates.Resolved,
                damageModifier,
                ability,
                null,
                [],
                [],
                [],
                [],
                damageContributions,
                weapon.Provenance);
    
            var proficiencyFactKey = $"weapon.{weapon.ConceptKey}.proficient";
            bool? proficient = context.BooleanFacts.TryGetValue(
                    proficiencyFactKey,
                    out var explicitProficient)
                ? explicitProficient
                : HasWeaponProficiency(context, weapon)
                    ? true
                    : null;
    
            if (!proficient.HasValue)
            {
                context.Mechanics[attackKey] = CharacterProjectionResolutionHelpers.Unresolved(
                    attackKey,
                    "attack-bonus",
                    $"{weapon.DisplayName} Attack Bonus",
                    CharacterResolutionStates.MissingCharacterInput,
                    [proficiencyFactKey],
                    provenance: weapon.Provenance);
                SetWeaponActionState(
                    context,
                    actionKey,
                    CharacterResolutionStates.MissingCharacterInput,
                    AppendSignedModifier(weapon.DamageExpression, damageModifier));
                continue;
            }
    
            var proficiency = 0;
            if (proficient.Value
                && !CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, "proficiency.standard", out proficiency))
            {
                context.Mechanics[attackKey] = CharacterProjectionResolutionHelpers.Unresolved(
                    attackKey,
                    "attack-bonus",
                    $"{weapon.DisplayName} Attack Bonus",
                    CharacterResolutionStates.MissingCharacterInput,
                    ["advancement.levels"],
                    provenance: weapon.Provenance);
                SetWeaponActionState(
                    context,
                    actionKey,
                    CharacterResolutionStates.MissingCharacterInput,
                    AppendSignedModifier(weapon.DamageExpression, damageModifier));
                continue;
            }
    
            var attackOther = context.IntegerFacts.GetValueOrDefault(
                $"attack.{weapon.ConceptKey}.other");
            var total = checked(
                abilityModifier
                + (proficient.Value ? proficiency : 0)
                + weapon.AttackBonus
                + attackOther);
            var attackContributions = new List<CharacterMechanicContributionView>
            {
                CharacterProjectionResolutionHelpers.Contribution(
                    $"ability.{ability}.modifier",
                    $"{CharacterProjectionJson.Humanize(ability)} modifier",
                    abilityModifier)
            };
            if (proficient.Value)
            {
                attackContributions.Add(CharacterProjectionResolutionHelpers.Contribution(
                    "proficiency.standard",
                    "Proficiency bonus",
                    proficiency));
            }
            if (weapon.AttackBonus != 0)
            {
                attackContributions.Add(new CharacterMechanicContributionView(
                    $"{weapon.ConceptKey}.weapon-attack-bonus",
                    "Weapon attack bonus",
                    CharacterEffectOperations.Add,
                    weapon.AttackBonus,
                    null,
                    weapon.ConceptKey,
                    weapon.Provenance));
            }
            if (attackOther != 0)
            {
                attackContributions.Add(CharacterProjectionResolutionHelpers.Contribution(
                    $"attack.{weapon.ConceptKey}.other",
                    "Other attack modifiers",
                    attackOther));
            }
    
            context.Mechanics[attackKey] = new CharacterResolvedMechanicView(
                attackKey,
                "attack-bonus",
                $"{weapon.DisplayName} Attack Bonus",
                CharacterResolutionStates.Resolved,
                total,
                ability,
                null,
                [],
                [],
                [],
                [],
                attackContributions,
                weapon.Provenance);
            SetWeaponActionState(
                context,
                actionKey,
                CharacterResolutionStates.Resolved,
                AppendSignedModifier(weapon.DamageExpression, damageModifier));
        }
    }
    
    private static bool HasWeaponProficiency(
        CharacterProjectionContext context,
        CharacterWeaponAttackProfile weapon)
    {
        var candidates = new List<string>
        {
            $"weapon.{weapon.ConceptKey}.proficient",
            $"qualification.weapons.{SlugKey(weapon.DisplayName)}"
        };
        if (!string.IsNullOrWhiteSpace(weapon.WeaponCategory))
        {
            candidates.Add(
                $"qualification.weapons.{SlugKey(weapon.WeaponCategory)}");
            candidates.Add(
                $"qualification.weapons.{SlugKey($"{weapon.WeaponCategory} weapons")}");
        }
    
        return candidates.Any(context.Capabilities.Contains);
    }
    
    private static void SetWeaponActionState(
        CharacterProjectionContext context,
        string actionKey,
        string state,
        string damageExpression)
    {
        if (context.Actions.TryGetValue(actionKey, out var action))
        {
            context.Actions[actionKey] = action with
            {
                State = state,
                DamageExpression = damageExpression
            };
        }
    }
    
    private static string AppendSignedModifier(string expression, int modifier) =>
        modifier switch
        {
            > 0 => $"{expression} + {modifier}",
            < 0 => $"{expression} - {Math.Abs(modifier)}",
            _ => expression
        };
    
    private static string SlugKey(string value) =>
        string.Join(
            '-',
            value.Trim().ToLowerInvariant()
                .Split(
                    [' ', '/', '_', '-', '|'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    
}
