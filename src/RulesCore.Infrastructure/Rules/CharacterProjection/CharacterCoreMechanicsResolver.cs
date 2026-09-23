using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Resolves the core Character mechanics after rule-projection modules populate the shared
/// projection context: abilities, proficiency, weapons, armor class, 3.x combat, initiative,
/// saving throws, and competencies.
/// </summary>
internal static class CharacterCoreMechanicsResolver
{
    private static readonly string[] StandardAbilities =
    [
        "strength",
        "dexterity",
        "constitution",
        "intelligence",
        "wisdom",
        "charisma"
    ];

    internal static void SeedCallerCapabilities(CharacterProjectionContext context)
    {
        foreach (var capability in context.Capabilities.ToArray())
        {
            context.CapabilityViews[capability] = new CharacterCapabilityView(
                capability,
                CharacterProjectionJson.Humanize(capability),
                [],
                CharacterProjectionContext.EmptyProvenance());
        }
    }
    
    internal static void Resolve(
        CharacterProjectionContext context,
        CharacterMechanicsCatalogView mechanicCatalog)
    {
        ResolveAbilities(context);
        ResolveProficiency(context);
        ResolveWeaponAttacks(context);
        ResolveArmorClass(context);
        ResolveThreeXCombatMechanics(context);
        ResolveInitiative(context);
        ResolveAbilitySavingThrows(context);
        ResolveCompetencies(context, mechanicCatalog);
    }
    
    private static void ResolveAbilities(CharacterProjectionContext context)
    {
        foreach (var ability in StandardAbilities)
        {
            var baseKey = $"ability.{ability}.base";
            var scoreKey = $"ability.{ability}.score";
            var modifierKey = $"ability.{ability}.modifier";
            var contributions = context.AbilityContributions.GetValueOrDefault(ability) ?? [];
            var persistentContributions = contributions
                .Where(value => !string.Equals(
                    value.StateKind,
                    "temporary",
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var temporaryContributions = contributions
                .Where(value => string.Equals(
                    value.StateKind,
                    "temporary",
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var hasBase = context.BaseAbilityScores.TryGetValue(ability, out var baseScore)
                || context.BaseAbilityScores.TryGetValue(baseKey, out baseScore)
                || context.IntegerFacts.TryGetValue(baseKey, out baseScore);
            var requiredChoices = context.RequiredAbilityChoicesFor(ability);
    
            if (!hasBase)
            {
                context.Mechanics[scoreKey] = CharacterProjectionResolutionHelpers.Unresolved(
                    scoreKey,
                    "ability-score",
                    $"{CharacterProjectionJson.Humanize(ability)} Score",
                    CharacterResolutionStates.MissingCharacterInput,
                    [baseKey],
                    requiredChoices: requiredChoices);
                context.Mechanics[modifierKey] = CharacterProjectionResolutionHelpers.Unresolved(
                    modifierKey,
                    "ability-modifier",
                    $"{CharacterProjectionJson.Humanize(ability)} Modifier",
                    CharacterResolutionStates.MissingCharacterInput,
                    [baseKey],
                    requiredChoices: requiredChoices);
                continue;
            }
    
            var baseContribution = new CharacterMechanicContributionView(
                baseKey,
                "Base score",
                CharacterEffectOperations.Set,
                baseScore,
                null,
                null,
                CharacterProjectionContext.EmptyProvenance(),
                "base");
            context.Mechanics[baseKey] = new CharacterResolvedMechanicView(
                baseKey,
                "ability-score-input",
                $"{CharacterProjectionJson.Humanize(ability)} Base Score",
                CharacterResolutionStates.Resolved,
                baseScore,
                null,
                null,
                [],
                [],
                [],
                [],
                [baseContribution],
                CharacterProjectionContext.EmptyProvenance());
    
            var breakdown = new List<CharacterMechanicContributionView> { baseContribution };
            breakdown.AddRange(persistentContributions);
            breakdown.AddRange(temporaryContributions);
    
            if (requiredChoices.Count > 0)
            {
                context.Mechanics[scoreKey] = new CharacterResolvedMechanicView(
                    scoreKey,
                    "ability-score",
                    $"{CharacterProjectionJson.Humanize(ability)} Score",
                    CharacterResolutionStates.ChoiceRequired,
                    null,
                    null,
                    null,
                    [],
                    [],
                    requiredChoices,
                    [],
                    breakdown,
                    CharacterProjectionContext.EmptyProvenance());
                context.Mechanics[modifierKey] = CharacterProjectionResolutionHelpers.Unresolved(
                    modifierKey,
                    "ability-modifier",
                    $"{CharacterProjectionJson.Humanize(ability)} Modifier",
                    CharacterResolutionStates.ChoiceRequired,
                    requiredChoices: requiredChoices);
                continue;
            }
    
            var ordinaryScore = checked(
                baseScore + persistentContributions.Sum(value => value.NumericValue ?? 0));
            var temporaryAdjustment =
                temporaryContributions.Sum(value => value.NumericValue ?? 0);
            var effective = checked(ordinaryScore + temporaryAdjustment);
            var ordinaryModifier = StandardDndCharacterMath.AbilityModifier(ordinaryScore);
            var modifier = StandardDndCharacterMath.AbilityModifier(effective);
    
            context.Mechanics[scoreKey] = new CharacterResolvedMechanicView(
                scoreKey,
                "ability-score",
                $"{CharacterProjectionJson.Humanize(ability)} Score",
                CharacterResolutionStates.Resolved,
                effective,
                null,
                null,
                [],
                [],
                [],
                [],
                breakdown,
                CharacterProjectionContext.EmptyProvenance());
            context.Mechanics[modifierKey] = new CharacterResolvedMechanicView(
                modifierKey,
                "ability-modifier",
                $"{CharacterProjectionJson.Humanize(ability)} Modifier",
                CharacterResolutionStates.Resolved,
                modifier,
                null,
                null,
                [],
                [],
                [],
                [],
                [new CharacterMechanicContributionView(
                    scoreKey,
                    "Effective ability score",
                    CharacterEffectOperations.Set,
                    effective,
                    null,
                    null,
                    CharacterProjectionContext.EmptyProvenance())],
                CharacterProjectionContext.EmptyProvenance());
    
            if (temporaryContributions.Length == 0)
            {
                continue;
            }
    
            var ordinaryScoreKey = $"ability.{ability}.ordinary-score";
            var ordinaryModifierKey = $"ability.{ability}.ordinary-modifier";
            var temporaryAdjustmentKey = $"ability.{ability}.temporary-adjustment";
            var temporaryScoreKey = $"ability.{ability}.temporary-score";
            var temporaryModifierKey = $"ability.{ability}.temporary-modifier";
            var ordinaryBreakdown = new List<CharacterMechanicContributionView> { baseContribution };
            ordinaryBreakdown.AddRange(persistentContributions);
    
            context.Mechanics[ordinaryScoreKey] = new CharacterResolvedMechanicView(
                ordinaryScoreKey,
                "ability-score",
                $"{CharacterProjectionJson.Humanize(ability)} Ordinary Score",
                CharacterResolutionStates.Resolved,
                ordinaryScore,
                null,
                null,
                [],
                [],
                [],
                [],
                ordinaryBreakdown,
                CharacterProjectionContext.EmptyProvenance());
            context.Mechanics[ordinaryModifierKey] = new CharacterResolvedMechanicView(
                ordinaryModifierKey,
                "ability-modifier",
                $"{CharacterProjectionJson.Humanize(ability)} Ordinary Modifier",
                CharacterResolutionStates.Resolved,
                ordinaryModifier,
                null,
                null,
                [],
                [],
                [],
                [],
                [new CharacterMechanicContributionView(
                    ordinaryScoreKey,
                    "Ordinary ability score",
                    CharacterEffectOperations.Set,
                    ordinaryScore,
                    null,
                    null,
                    CharacterProjectionContext.EmptyProvenance())],
                CharacterProjectionContext.EmptyProvenance());
            context.Mechanics[temporaryAdjustmentKey] = new CharacterResolvedMechanicView(
                temporaryAdjustmentKey,
                "ability-temporary-adjustment",
                $"{CharacterProjectionJson.Humanize(ability)} Temporary Adjustment",
                CharacterResolutionStates.Resolved,
                temporaryAdjustment,
                null,
                null,
                [],
                [],
                [],
                [],
                temporaryContributions,
                CharacterProjectionContext.EmptyProvenance());
            context.Mechanics[temporaryScoreKey] = new CharacterResolvedMechanicView(
                temporaryScoreKey,
                "ability-score",
                $"{CharacterProjectionJson.Humanize(ability)} Temporary Score",
                CharacterResolutionStates.Resolved,
                effective,
                null,
                null,
                [],
                [],
                [],
                [],
                breakdown,
                CharacterProjectionContext.EmptyProvenance());
            context.Mechanics[temporaryModifierKey] = new CharacterResolvedMechanicView(
                temporaryModifierKey,
                "ability-modifier",
                $"{CharacterProjectionJson.Humanize(ability)} Temporary Modifier",
                CharacterResolutionStates.Resolved,
                modifier,
                null,
                null,
                [],
                [],
                [],
                [],
                [new CharacterMechanicContributionView(
                    temporaryScoreKey,
                    "Temporary ability score",
                    CharacterEffectOperations.Set,
                    effective,
                    null,
                    null,
                    CharacterProjectionContext.EmptyProvenance(),
                    "temporary")],
                CharacterProjectionContext.EmptyProvenance());
        }
    }
    
    private static void ResolveProficiency(CharacterProjectionContext context)
    {
        if (!context.UsesStandardProficiency)
        {
            return;
        }
    
        const string key = "proficiency.standard";
        if (context.StandardProficiencyLevel <= 0)
        {
            context.Mechanics[key] = CharacterProjectionResolutionHelpers.Unresolved(
                key,
                "proficiency",
                "Proficiency Bonus",
                CharacterResolutionStates.MissingCharacterInput,
                ["advancement.levels"]);
            return;
        }
    
        var value = StandardDndCharacterMath.ProficiencyBonusForCharacterLevel(
            context.StandardProficiencyLevel);
        context.Mechanics[key] = new CharacterResolvedMechanicView(
            key,
            "proficiency",
            "Proficiency Bonus",
            CharacterResolutionStates.Resolved,
            value,
            null,
            null,
            [],
            [],
            [],
            [],
            [new CharacterMechanicContributionView(
                "advancement.levels",
                "Character level",
                CharacterEffectOperations.Set,
                context.StandardProficiencyLevel,
                null,
                null,
                CharacterProjectionContext.EmptyProvenance())],
            CharacterProjectionContext.EmptyProvenance());
        context.Capabilities.Add("proficiency.standard");
    }
    
    
    private static void ResolveWeaponAttacks(CharacterProjectionContext context)
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
    
    private static void ResolveArmorClass(CharacterProjectionContext context)
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
    
    private static void ResolveThreeXCombatMechanics(CharacterProjectionContext context)
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
    
    private static void ResolveInitiative(CharacterProjectionContext context)
    {
        const string key = "combat.initiative";
        if (!CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, "ability.dexterity.modifier", out var dexterity))
        {
            context.Mechanics[key] = CharacterProjectionResolutionHelpers.Unresolved(
                key,
                "combat-value",
                "Initiative",
                CharacterResolutionStates.MissingCharacterInput,
                ["ability.dexterity.base"]);
            return;
        }
    
        var other = context.IntegerFacts.GetValueOrDefault("combat.initiative.other");
        context.Mechanics[key] = new CharacterResolvedMechanicView(
            key,
            "combat-value",
            "Initiative",
            CharacterResolutionStates.Resolved,
            checked(dexterity + other),
            null,
            null,
            [],
            [],
            [],
            [],
            [
                CharacterProjectionResolutionHelpers.Contribution("ability.dexterity.modifier", "Dexterity modifier", dexterity),
                CharacterProjectionResolutionHelpers.Contribution("combat.initiative.other", "Other initiative modifiers", other)
            ],
            CharacterProjectionContext.EmptyProvenance());
    }
    
    private static void ResolveAbilitySavingThrows(CharacterProjectionContext context)
    {
        if (!context.UsesStandardProficiency)
        {
            return;
        }
    
        var proficiencyResolved = CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, "proficiency.standard", out var proficiency);
        foreach (var ability in StandardAbilities)
        {
            var key = $"save.{ability}";
            if (!CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, $"ability.{ability}.modifier", out var modifier))
            {
                context.Mechanics[key] = CharacterProjectionResolutionHelpers.Unresolved(
                    key,
                    "saving-throw",
                    $"{CharacterProjectionJson.Humanize(ability)} Saving Throw",
                    CharacterResolutionStates.MissingCharacterInput,
                    [$"ability.{ability}.base"]);
                continue;
            }
    
            var proficient = context.SaveProficiencyAbilities.Contains(ability);
            if (proficient && !proficiencyResolved)
            {
                context.Mechanics[key] = CharacterProjectionResolutionHelpers.Unresolved(
                    key,
                    "saving-throw",
                    $"{CharacterProjectionJson.Humanize(ability)} Saving Throw",
                    CharacterResolutionStates.MissingCharacterInput,
                    ["advancement.levels"]);
                continue;
            }
    
            var other = context.IntegerFacts.GetValueOrDefault($"{key}.other");
            var value = checked(modifier + (proficient ? proficiency : 0) + other);
            var contributions = new List<CharacterMechanicContributionView>
            {
                CharacterProjectionResolutionHelpers.Contribution($"ability.{ability}.modifier", $"{CharacterProjectionJson.Humanize(ability)} modifier", modifier)
            };
            if (proficient)
            {
                contributions.Add(CharacterProjectionResolutionHelpers.Contribution("proficiency.standard", "Proficiency bonus", proficiency));
            }
            if (other != 0)
            {
                contributions.Add(CharacterProjectionResolutionHelpers.Contribution($"{key}.other", "Other modifiers", other));
            }
    
            context.Mechanics[key] = new CharacterResolvedMechanicView(
                key,
                "saving-throw",
                $"{CharacterProjectionJson.Humanize(ability)} Saving Throw",
                CharacterResolutionStates.Resolved,
                value,
                null,
                null,
                [],
                [],
                [],
                [],
                contributions,
                CharacterProjectionContext.EmptyProvenance());
        }
    }
    
    private static void ResolveCompetencies(
        CharacterProjectionContext context,
        CharacterMechanicsCatalogView catalog)
    {
        var projected = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var competencyMechanics = catalog.Mechanics
            .Where(value => value.Competency is not null)
            .OrderBy(value => value.MechanicKey, StringComparer.Ordinal)
            .ToArray();
    
        foreach (var mechanic in competencyMechanics)
        {
            var competency = mechanic.Competency!;
            var competencyConceptKey = mechanic.ConceptKey
                ?? throw new InvalidOperationException(
                    $"Competency mechanic '{mechanic.MechanicKey}' does not expose a concept key.");
            var universal = FindUniversalCompetency(catalog, mechanic);
            var stateKeys = BuildCompetencyStateKeys(
                mechanic,
                competencyConceptKey,
                universal,
                includeTrainingStateKey: false);
            var trainingStateKeys = BuildCompetencyStateKeys(
                mechanic,
                competencyConceptKey,
                universal,
                includeTrainingStateKey: true);
    
            context.RegisterCompetencyIdentity(
                mechanic.DisplayName,
                competencyConceptKey,
                competency.FamilyName,
                competency.Specialty,
                competency.CompetencyKind);
            var profile = SelectProfile(competency, context);
            if (profile is null)
            {
                context.Mechanics[mechanic.MechanicKey] = CharacterProjectionResolutionHelpers.Unresolved(
                    mechanic.MechanicKey,
                    "competency",
                    mechanic.DisplayName,
                    CharacterResolutionStates.MissingCapability,
                    missingCapabilities: competency.Profiles
                        .SelectMany(value => value.RequiredCapabilityKeys)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray(),
                    provenance: mechanic.Provenance ?? CharacterProjectionResolutionHelpers.Provenance(mechanic.SourceAttributions));
                continue;
            }
    
            if (!profile.CanEvaluate || string.IsNullOrWhiteSpace(profile.GoverningAbilityKey))
            {
                context.Mechanics[mechanic.MechanicKey] = CharacterProjectionResolutionHelpers.Unresolved(
                    mechanic.MechanicKey,
                    "competency",
                    mechanic.DisplayName,
                    CharacterResolutionStates.ApplicableUnresolved,
                    provenance: mechanic.Provenance ?? CharacterProjectionResolutionHelpers.Provenance(mechanic.SourceAttributions));
                continue;
            }
    
            var ability = CharacterProjectionJson.NormalizeAbilityKey(profile.GoverningAbilityKey);
            if (!CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, $"ability.{ability}.modifier", out var abilityModifier))
            {
                context.Mechanics[mechanic.MechanicKey] = CharacterProjectionResolutionHelpers.Unresolved(
                    mechanic.MechanicKey,
                    "competency",
                    mechanic.DisplayName,
                    CharacterResolutionStates.MissingCharacterInput,
                    [$"ability.{ability}.base"],
                    provenance: mechanic.Provenance ?? CharacterProjectionResolutionHelpers.Provenance(mechanic.SourceAttributions));
                continue;
            }
    
            var contributions = new List<CharacterMechanicContributionView>
            {
                CharacterProjectionResolutionHelpers.Contribution(
                    $"ability.{ability}.modifier",
                    $"{CharacterProjectionJson.Humanize(ability)} modifier",
                    abilityModifier)
            };
            var total = abilityModifier;
            var rankValue = 0;
    
            if (profile.SupportsClassSkillState)
            {
                var explicitClassSkill = stateKeys.Any(context.ClassSkillKeys.Contains);
                var derivedSources = context.FindClassSkillGrantSources(
                    mechanic.DisplayName,
                    profile.FamilyName);
                bool? isClassSkill = explicitClassSkill || derivedSources.Count > 0
                    ? true
                    : context.HasClassSkillInput || context.HasDerivedClassSkillData
                        ? false
                        : null;
                context.Qualifications[$"qualification.class-skill.{competencyConceptKey}"] =
                    new CharacterQualificationView(
                        $"qualification.class-skill.{competencyConceptKey}",
                        "class-skill",
                        $"{mechanic.DisplayName} Class Skill",
                        isClassSkill,
                        isClassSkill.HasValue
                            ? CharacterResolutionStates.Resolved
                            : CharacterResolutionStates.ApplicableUnresolved,
                        derivedSources,
                        mechanic.Provenance ?? CharacterProjectionResolutionHelpers.Provenance(mechanic.SourceAttributions));
            }
    
            if (profile.SupportsRanks)
            {
                if (!context.HasCompetencyRanksInput
                    || !TryFindCompetencyRanks(
                        context.CompetencyRanks,
                        stateKeys,
                        out var rankStateKey,
                        out rankValue))
                {
                    context.Mechanics[mechanic.MechanicKey] = CharacterProjectionResolutionHelpers.Unresolved(
                        mechanic.MechanicKey,
                        "competency",
                        mechanic.DisplayName,
                        CharacterResolutionStates.MissingCharacterInput,
                        [$"{stateKeys[0]}.ranks"],
                        provenance: mechanic.Provenance ?? CharacterProjectionResolutionHelpers.Provenance(mechanic.SourceAttributions));
                    continue;
                }
    
                total = checked(total + rankValue);
                contributions.Add(CharacterProjectionResolutionHelpers.Contribution(
                    $"{rankStateKey}.ranks",
                    "Ranks",
                    rankValue));
    
                if (profile.TrainedOnly == true && rankValue <= 0)
                {
                    context.Mechanics[mechanic.MechanicKey] = CharacterProjectionResolutionHelpers.Unresolved(
                        mechanic.MechanicKey,
                        "competency",
                        mechanic.DisplayName,
                        CharacterResolutionStates.MissingCapability,
                        missingCapabilities: [$"competency.trained.{competencyConceptKey}"],
                        provenance: mechanic.Provenance ?? CharacterProjectionResolutionHelpers.Provenance(mechanic.SourceAttributions));
                    continue;
                }
            }
    
            var usesProficiencyTraining = string.Equals(
                profile.EvaluationProfileKey,
                "proficiency-competency",
                StringComparison.OrdinalIgnoreCase);
            if (profile.SupportsTrainingState && usesProficiencyTraining)
            {
                var trained = trainingStateKeys.Any(context.TrainingKeys.Contains);
                if (!trained && !context.HasTrainingInput)
                {
                    context.Mechanics[mechanic.MechanicKey] = CharacterProjectionResolutionHelpers.Unresolved(
                        mechanic.MechanicKey,
                        "competency",
                        mechanic.DisplayName,
                        CharacterResolutionStates.MissingCharacterInput,
                        [$"{trainingStateKeys[0]}.trained"],
                        provenance: mechanic.Provenance ?? CharacterProjectionResolutionHelpers.Provenance(mechanic.SourceAttributions));
                    continue;
                }
    
                if (profile.TrainedOnly == true && !trained)
                {
                    context.Mechanics[mechanic.MechanicKey] = CharacterProjectionResolutionHelpers.Unresolved(
                        mechanic.MechanicKey,
                        "competency",
                        mechanic.DisplayName,
                        CharacterResolutionStates.MissingCapability,
                        missingCapabilities: [$"competency.trained.{competencyConceptKey}"],
                        provenance: mechanic.Provenance ?? CharacterProjectionResolutionHelpers.Provenance(mechanic.SourceAttributions));
                    continue;
                }
    
                if (trained)
                {
                    if (!CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, "proficiency.standard", out var proficiency))
                    {
                        context.Mechanics[mechanic.MechanicKey] = CharacterProjectionResolutionHelpers.Unresolved(
                            mechanic.MechanicKey,
                            "competency",
                            mechanic.DisplayName,
                            CharacterResolutionStates.MissingCharacterInput,
                            ["advancement.levels"],
                            provenance: mechanic.Provenance ?? CharacterProjectionResolutionHelpers.Provenance(mechanic.SourceAttributions));
                        continue;
                    }
    
                    total = checked(total + proficiency);
                    contributions.Add(CharacterProjectionResolutionHelpers.Contribution(
                        "proficiency.standard",
                        "Training proficiency",
                        proficiency));
                }
            }
    
            var other = context.IntegerFacts.GetValueOrDefault($"{mechanic.MechanicKey}.other");
            total = checked(total + other);
            if (other != 0)
            {
                contributions.Add(CharacterProjectionResolutionHelpers.Contribution(
                    $"{mechanic.MechanicKey}.other",
                    "Other modifiers",
                    other));
            }
    
            context.Mechanics[mechanic.MechanicKey] = new CharacterResolvedMechanicView(
                mechanic.MechanicKey,
                "competency",
                mechanic.DisplayName,
                CharacterResolutionStates.Resolved,
                total,
                null,
                null,
                [],
                [],
                [],
                [],
                contributions,
                mechanic.Provenance ?? CharacterProjectionResolutionHelpers.Provenance(mechanic.SourceAttributions));
            projected[competencyConceptKey] = total;
        }
    
        foreach (var relationship in KnownMechanicalRelationships.All)
        {
            var parentKey = $"competency.{relationship.Parent.ConceptKey}";
            if (!context.Mechanics.ContainsKey(parentKey))
            {
                continue;
            }
    
            if (relationship.Components.All(component => projected.ContainsKey(component.ConceptKey)))
            {
                var evaluated = CompositeCompetencyEvaluator.Evaluate(
                    relationship,
                    relationship.Components.ToDictionary(
                        component => component.ConceptKey,
                        component => projected[component.ConceptKey],
                        StringComparer.Ordinal),
                    []);
                var existing = context.Mechanics[parentKey];
                context.Mechanics[parentKey] = existing with
                {
                    State = CharacterResolutionStates.Resolved,
                    NumericValue = evaluated.ParentValue,
                    Contributions = relationship.Components.Select(component =>
                        CharacterProjectionResolutionHelpers.Contribution(
                            $"competency.{component.ConceptKey}",
                            component.DisplayName,
                            projected[component.ConceptKey])).ToArray()
                };
            }
        }
    }
    
    private static CharacterUniversalCompetencyView? FindUniversalCompetency(
        CharacterMechanicsCatalogView catalog,
        CharacterMechanicView mechanic) =>
        catalog.Competencies?.FirstOrDefault(value =>
            string.Equals(
                value.SemanticKey,
                mechanic.MechanicKey,
                StringComparison.OrdinalIgnoreCase)
            || value.MechanicKeys.Contains(
                mechanic.MechanicKey,
                StringComparer.OrdinalIgnoreCase)
            || value.CompatibilityMechanicKeys.Contains(
                mechanic.MechanicKey,
                StringComparer.OrdinalIgnoreCase));
    
    private static IReadOnlyList<string> BuildCompetencyStateKeys(
        CharacterMechanicView mechanic,
        string competencyConceptKey,
        CharacterUniversalCompetencyView? universal,
        bool includeTrainingStateKey)
    {
        var keys = new List<string>();
    
        static void Add(List<string> values, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || values.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }
    
            values.Add(value);
        }
    
        static string? StripMechanicPrefix(string value)
        {
            const string prefix = "competency.";
            return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? value[prefix.Length..]
                : null;
        }
    
        Add(keys, competencyConceptKey);
        Add(keys, mechanic.MechanicKey);
        Add(keys, universal?.SemanticKey);
    
        if (universal is not null)
        {
            foreach (var compatibilityKey in universal.CompatibilityMechanicKeys)
            {
                Add(keys, compatibilityKey);
                Add(keys, StripMechanicPrefix(compatibilityKey));
            }
    
            if (includeTrainingStateKey)
            {
                Add(keys, universal.TrainingStateKey);
            }
        }
    
        return keys;
    }
    
    private static bool TryFindCompetencyRanks(
        IReadOnlyDictionary<string, int> ranks,
        IReadOnlyList<string> keys,
        out string matchedKey,
        out int value)
    {
        foreach (var key in keys)
        {
            if (ranks.TryGetValue(key, out value))
            {
                matchedKey = key;
                return true;
            }
        }
    
        matchedKey = keys[0];
        value = 0;
        return false;
    }
    
    private static CharacterCompetencyProfileView? SelectProfile(
        CharacterCompetencyDefinitionView competency,
        CharacterProjectionContext context)
    {
        var compatible = competency.Profiles
            .Where(profile => profile.RequiredCapabilityKeys.All(context.Capabilities.Contains))
            .ToArray();
    
        return compatible.SingleOrDefault(profile =>
                   profile.SourceEntityRevisionId == competency.DefaultProfileSourceEntityRevisionId)
            ?? compatible
                .OrderBy(profile => profile.RequiredCapabilityKeys.Count)
                .ThenBy(profile => profile.ProfileKey, StringComparer.Ordinal)
                .ThenBy(profile => profile.SourceEntityRevisionId)
                .FirstOrDefault();
    }
    

    private static CharacterCompetencyProfileView? SelectProfile(
        CharacterCompetencyDefinitionView competency,
        CharacterProjectionContext context)
    {
        var compatible = competency.Profiles
            .Where(profile => profile.RequiredCapabilityKeys.All(context.Capabilities.Contains))
            .ToArray();
    
        return compatible.SingleOrDefault(profile =>
                   profile.SourceEntityRevisionId == competency.DefaultProfileSourceEntityRevisionId)
            ?? compatible
                .OrderBy(profile => profile.RequiredCapabilityKeys.Count)
                .ThenBy(profile => profile.ProfileKey, StringComparer.Ordinal)
                .ThenBy(profile => profile.SourceEntityRevisionId)
                .FirstOrDefault();
    }
    
}