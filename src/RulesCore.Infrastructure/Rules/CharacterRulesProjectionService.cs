using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules.CharacterProjection;

namespace RulesCore.Infrastructure.Rules;

public sealed class CharacterRulesProjectionService(RulesCoreDbContext dbContext)
    : ICharacterRulesProjectionService
{
    private const int PageSize = 500;
    private static readonly string[] StandardAbilities =
    [
        "strength",
        "dexterity",
        "constitution",
        "intelligence",
        "wisdom",
        "charisma"
    ];

    private readonly ResolvedRulesCatalogService resolvedRules = new(dbContext);
    private readonly CharacterMechanicsConsumerService mechanics = new(dbContext);

    private static readonly IReadOnlyList<ICharacterRuleProjectionModule> Modules =
    [
        new RaceCharacterRuleProjectionModule(),
        new ClassCharacterRuleProjectionModule(),
        new ItemCharacterRuleProjectionModule(),
        new SpellCharacterRuleProjectionModule(),
        new GenericCharacterRuleProjectionModule()
    ];

    public async Task<CharacterRulesProjectionView> ResolveGlobalAsync(
        CharacterRulesProjectionRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rules = await ReadAllGlobalRulesAsync(userId, cancellationToken);
        var mechanicCatalog = await mechanics.GetGlobalAsync(
            userId,
            includeUnavailable: true,
            cancellationToken);
        return Resolve(request, rules, mechanicCatalog);
    }

    public async Task<CharacterRulesProjectionView> ResolveCampaignAsync(
        Guid campaignId,
        CharacterRulesProjectionRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("Campaign ID can not be empty.", nameof(campaignId));
        }
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(userId));
        }
        ArgumentNullException.ThrowIfNull(request);

        var rules = await ReadAllCampaignRulesAsync(campaignId, userId.Trim(), cancellationToken);
        var mechanicCatalog = await mechanics.GetCampaignAsync(
            campaignId,
            userId.Trim(),
            includeUnavailable: true,
            cancellationToken);
        return Resolve(request, rules, mechanicCatalog);
    }

    private CharacterRulesProjectionView Resolve(
        CharacterRulesProjectionRequest request,
        ResolvedRulesCatalogView rules,
        CharacterMechanicsCatalogView mechanicCatalog)
    {
        var context = new CharacterProjectionContext(request);
        SeedCallerCapabilities(context);

        var projectionRules = rules.Rules
            .Where(value => value.Document is not null)
            .Select(value => new CharacterProjectionRule(
                value,
                value.Document!.Value,
                EffectiveProvenance(value)))
            .ToArray();

        foreach (var rule in projectionRules)
        {
            context.RegisterRuleIdentity(rule.Catalog.ConceptKey, rule.Catalog.DisplayName);
        }

        foreach (var rule in projectionRules)
        {
            foreach (var module in Modules)
            {
                if (module.Handles(rule, context))
                {
                    module.Project(rule, context);
                }
            }
        }

        ResolveCoreMechanics(context, mechanicCatalog);
        ResolveSpellcastingMechanics(context);
        ResolveSpellcastingResources(context);
        ResolveHealthMechanics(context);
        ResolvePrerequisites(context);
        ResolveLegacyMechanicPlaceholders(context, mechanicCatalog);

        return new CharacterRulesProjectionView(
            rules.Scope,
            rules.CampaignId,
            rules.RevisionNumber,
            rules.PublishedAt,
            context.Mechanics.Values
                .Where(value => context.ShouldIncludeMechanic(value.MechanicKey))
                .OrderBy(value => value.Kind, StringComparer.Ordinal)
                .ThenBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.MechanicKey, StringComparer.Ordinal)
                .ToArray(),
            context.CapabilityViews.Values.OrderBy(value => value.CapabilityKey, StringComparer.Ordinal).ToArray(),
            context.Grants.OrderBy(value => value.GrantKey, StringComparer.Ordinal).ToArray(),
            context.Effects.OrderBy(value => value.EffectKey, StringComparer.Ordinal).ToArray(),
            context.Movement.Values.OrderBy(value => value.MovementKey, StringComparer.Ordinal).ToArray(),
            context.Qualifications.Values.OrderBy(value => value.QualificationKey, StringComparer.Ordinal).ToArray(),
            context.Actions.Values.OrderBy(value => value.ActionKey, StringComparer.Ordinal).ToArray(),
            context.Features.Values.OrderBy(value => value.FeatureKey, StringComparer.Ordinal).ToArray(),
            context.Resources.Values.OrderBy(value => value.ResourceKey, StringComparer.Ordinal).ToArray(),
            context.Spellcasting.Values.OrderBy(value => value.SpellcastingKey, StringComparer.Ordinal).ToArray(),
            context.Procedures.Values.OrderBy(value => value.ProcedureKey, StringComparer.Ordinal).ToArray(),
            context.Prerequisites.Values.OrderBy(value => value.ConceptKey, StringComparer.Ordinal).ToArray(),
            context.Conflicts.OrderBy(value => value.ConflictKey, StringComparer.Ordinal).ToArray());
    }

    private static void SeedCallerCapabilities(CharacterProjectionContext context)
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

    private static void ResolveCoreMechanics(
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
            var hasBase = context.BaseAbilityScores.TryGetValue(ability, out var baseScore)
                || context.BaseAbilityScores.TryGetValue(baseKey, out baseScore)
                || context.IntegerFacts.TryGetValue(baseKey, out baseScore);
            var requiredChoices = context.RequiredAbilityChoices.OrderBy(value => value, StringComparer.Ordinal).ToArray();

            if (!hasBase)
            {
                context.Mechanics[scoreKey] = Unresolved(
                    scoreKey,
                    "ability-score",
                    $"{CharacterProjectionJson.Humanize(ability)} Score",
                    CharacterResolutionStates.MissingCharacterInput,
                    [baseKey],
                    requiredChoices: requiredChoices);
                context.Mechanics[modifierKey] = Unresolved(
                    modifierKey,
                    "ability-modifier",
                    $"{CharacterProjectionJson.Humanize(ability)} Modifier",
                    CharacterResolutionStates.MissingCharacterInput,
                    [baseKey],
                    requiredChoices: requiredChoices);
                continue;
            }

            long total = baseScore;
            var breakdown = new List<CharacterMechanicContributionView>
            {
                new(
                    baseKey,
                    "Base score",
                    CharacterEffectOperations.Set,
                    baseScore,
                    null,
                    null,
                    CharacterProjectionContext.EmptyProvenance())
            };
            foreach (var contribution in contributions)
            {
                if (contribution.NumericValue is int value)
                {
                    total += value;
                    breakdown.Add(contribution);
                }
            }

            if (requiredChoices.Length > 0)
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
                context.Mechanics[modifierKey] = Unresolved(
                    modifierKey,
                    "ability-modifier",
                    $"{CharacterProjectionJson.Humanize(ability)} Modifier",
                    CharacterResolutionStates.ChoiceRequired,
                    requiredChoices: requiredChoices);
                continue;
            }

            var effective = checked((int)total);
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
            context.Mechanics[key] = Unresolved(
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
                    context.Mechanics[attackKey] = Unresolved(
                        attackKey,
                        "attack-bonus",
                        $"{weapon.DisplayName} Attack Bonus",
                        CharacterResolutionStates.ChoiceRequired,
                        requiredChoices: [choiceKey],
                        provenance: weapon.Provenance);
                    context.Mechanics[damageModifierKey] = Unresolved(
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
                    context.Mechanics[attackKey] = Unresolved(
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
                || !TryResolvedNumeric(context, $"ability.{ability}.modifier", out var abilityModifier))
            {
                context.Mechanics[attackKey] = Unresolved(
                    attackKey,
                    "attack-bonus",
                    $"{weapon.DisplayName} Attack Bonus",
                    CharacterResolutionStates.MissingCharacterInput,
                    [$"ability.{ability ?? "weapon"}.base"],
                    provenance: weapon.Provenance);
                context.Mechanics[damageModifierKey] = Unresolved(
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
                Contribution(
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
                damageContributions.Add(Contribution(
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
                context.Mechanics[attackKey] = Unresolved(
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
                && !TryResolvedNumeric(context, "proficiency.standard", out proficiency))
            {
                context.Mechanics[attackKey] = Unresolved(
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
                Contribution(
                    $"ability.{ability}.modifier",
                    $"{CharacterProjectionJson.Humanize(ability)} modifier",
                    abilityModifier)
            };
            if (proficient.Value)
            {
                attackContributions.Add(Contribution(
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
                attackContributions.Add(Contribution(
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

        if (armor.Length == 0 && shields.Length == 0 && unclassified.Length == 0)
        {
            return;
        }

        if (unclassified.Length > 0)
        {
            context.Mechanics["defense.ac.total"] = Unresolved(
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
            context.Mechanics["defense.ac.total"] = Unresolved(
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
            context.Mechanics["defense.ac.total"] = Unresolved(
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

        if (armor.Length == 0)
        {
            context.Mechanics["defense.ac.total"] = Unresolved(
                "defense.ac.total",
                "defense",
                "Armor Class",
                CharacterResolutionStates.MissingCharacterInput,
                ["defense.ac.unarmored-formula"]);
            return;
        }

        if (!TryResolvedNumeric(context, "ability.dexterity.modifier", out var dexterityModifier))
        {
            context.Mechanics["defense.ac.total"] = Unresolved(
                "defense.ac.total",
                "defense",
                "Armor Class",
                CharacterResolutionStates.MissingCharacterInput,
                ["ability.dexterity.base"]);
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
            context.Mechanics["defense.ac.total"] = Unresolved(
                "defense.ac.total",
                "defense",
                "Armor Class",
                CharacterResolutionStates.ApplicableUnresolved,
                ["defense.ac.armor-role"]);
            return;
        }

        var shieldBonus = shields.SingleOrDefault()?.NumericValue ?? 0;
        var other = context.IntegerFacts.GetValueOrDefault("defense.ac.other");
        var total = checked(armorBase + dexterityContribution + shieldBonus + other);

        var contributions = new List<CharacterMechanicContributionView>
        {
            new(
                armorEffect.EffectKey,
                "Armor base",
                CharacterEffectOperations.Set,
                armorBase,
                armorEffect.TextValue,
                armorEffect.SourceConceptKey,
                armorEffect.Provenance),
            Contribution(
                "ability.dexterity.modifier",
                "Dexterity contribution",
                dexterityContribution)
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
            contributions.Add(Contribution("defense.ac.other", "Other modifiers", other));
        }

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
            [contributions[0]],
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

            if (!TryResolvedNumeric(context, $"ability.{ability}.modifier", out var abilityModifier))
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
                Contribution(
                    $"ability.{ability}.modifier",
                    $"{CharacterProjectionJson.Humanize(ability)} modifier",
                    abilityModifier)
            };
            if (other != 0)
            {
                contributions.Add(Contribution($"save.{save}.other", "Other modifiers", other));
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

        ResolveThreeXGrapple(context);
    }

    private static void ResolveThreeXGrapple(CharacterProjectionContext context)
    {
        if (!TryResolvedNumeric(context, "combat.base-attack-bonus", out var baseAttackBonus))
        {
            return;
        }
        if (!TryResolvedNumeric(context, "ability.strength.modifier", out var strengthModifier))
        {
            context.Mechanics["combat.grapple"] = Unresolved(
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
        else if (!TryThreeXSizeModifier(context.SizeCategory, out sizeModifier))
        {
            context.Mechanics["combat.grapple"] = Unresolved(
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
                Contribution("combat.base-attack-bonus", "Base attack bonus", baseAttackBonus),
                Contribution("ability.strength.modifier", "Strength modifier", strengthModifier),
                Contribution("combat.grapple.size-modifier", "Size modifier", sizeModifier),
                Contribution("combat.grapple.other", "Other modifiers", other)
            ],
            CharacterProjectionContext.EmptyProvenance());
    }

    private static bool TryThreeXSizeModifier(string? sizeCategory, out int modifier)
    {
        modifier = sizeCategory?.Trim().ToUpperInvariant() switch
        {
            "F" or "FINE" => -16,
            "D" or "DIMINUTIVE" => -12,
            "T" or "TINY" => -8,
            "S" or "SMALL" => -4,
            "M" or "MEDIUM" => 0,
            "L" or "LARGE" => 4,
            "H" or "HUGE" => 8,
            "G" or "GARGANTUAN" => 12,
            "C" or "COLOSSAL" => 16,
            _ => int.MinValue
        };
        return modifier != int.MinValue;
    }

    private static void ResolveInitiative(CharacterProjectionContext context)
    {
        const string key = "combat.initiative";
        if (!TryResolvedNumeric(context, "ability.dexterity.modifier", out var dexterity))
        {
            context.Mechanics[key] = Unresolved(
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
                Contribution("ability.dexterity.modifier", "Dexterity modifier", dexterity),
                Contribution("combat.initiative.other", "Other initiative modifiers", other)
            ],
            CharacterProjectionContext.EmptyProvenance());
    }

    private static void ResolveAbilitySavingThrows(CharacterProjectionContext context)
    {
        if (!context.UsesStandardProficiency)
        {
            return;
        }

        var proficiencyResolved = TryResolvedNumeric(context, "proficiency.standard", out var proficiency);
        foreach (var ability in StandardAbilities)
        {
            var key = $"save.{ability}";
            if (!TryResolvedNumeric(context, $"ability.{ability}.modifier", out var modifier))
            {
                context.Mechanics[key] = Unresolved(
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
                context.Mechanics[key] = Unresolved(
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
                Contribution($"ability.{ability}.modifier", $"{CharacterProjectionJson.Humanize(ability)} modifier", modifier)
            };
            if (proficient)
            {
                contributions.Add(Contribution("proficiency.standard", "Proficiency bonus", proficiency));
            }
            if (other != 0)
            {
                contributions.Add(Contribution($"{key}.other", "Other modifiers", other));
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
            context.RegisterCompetencyIdentity(
                mechanic.DisplayName,
                competency.ConceptKey,
                competency.FamilyName,
                competency.Specialty);
            var profile = SelectProfile(competency, context);
            if (profile is null)
            {
                context.Mechanics[mechanic.MechanicKey] = Unresolved(
                    mechanic.MechanicKey,
                    "competency",
                    mechanic.DisplayName,
                    CharacterResolutionStates.MissingCapability,
                    missingCapabilities: competency.Profiles
                        .SelectMany(value => value.RequiredCapabilityKeys)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray(),
                    provenance: mechanic.Provenance ?? Provenance(mechanic.SourceAttributions));
                continue;
            }

            if (!profile.CanEvaluate || string.IsNullOrWhiteSpace(profile.GoverningAbilityKey))
            {
                context.Mechanics[mechanic.MechanicKey] = Unresolved(
                    mechanic.MechanicKey,
                    "competency",
                    mechanic.DisplayName,
                    CharacterResolutionStates.ApplicableUnresolved,
                    provenance: mechanic.Provenance ?? Provenance(mechanic.SourceAttributions));
                continue;
            }

            var ability = CharacterProjectionJson.NormalizeAbilityKey(profile.GoverningAbilityKey);
            if (!TryResolvedNumeric(context, $"ability.{ability}.modifier", out var abilityModifier))
            {
                context.Mechanics[mechanic.MechanicKey] = Unresolved(
                    mechanic.MechanicKey,
                    "competency",
                    mechanic.DisplayName,
                    CharacterResolutionStates.MissingCharacterInput,
                    [$"ability.{ability}.base"],
                    provenance: mechanic.Provenance ?? Provenance(mechanic.SourceAttributions));
                continue;
            }

            var contributions = new List<CharacterMechanicContributionView>
            {
                Contribution($"ability.{ability}.modifier", $"{CharacterProjectionJson.Humanize(ability)} modifier", abilityModifier)
            };
            var total = abilityModifier;

            if (profile.SupportsClassSkillState)
            {
                var explicitClassSkill = context.ClassSkillKeys.Contains(competency.ConceptKey)
                    || context.ClassSkillKeys.Contains(mechanic.MechanicKey);
                var derivedSources = context.FindClassSkillGrantSources(
                    mechanic.DisplayName,
                    profile.FamilyName);
                bool? isClassSkill = explicitClassSkill || derivedSources.Count > 0
                    ? true
                    : context.HasClassSkillInput || context.HasDerivedClassSkillData
                        ? false
                        : null;
                context.Qualifications[$"qualification.class-skill.{competency.ConceptKey}"] =
                    new CharacterQualificationView(
                        $"qualification.class-skill.{competency.ConceptKey}",
                        "class-skill",
                        $"{mechanic.DisplayName} Class Skill",
                        isClassSkill,
                        isClassSkill.HasValue
                            ? CharacterResolutionStates.Resolved
                            : CharacterResolutionStates.ApplicableUnresolved,
                        derivedSources,
                        mechanic.Provenance ?? Provenance(mechanic.SourceAttributions));
            }

            if (profile.SupportsRanks)
            {
                if (!context.HasCompetencyRanksInput
                    || !context.CompetencyRanks.TryGetValue(competency.ConceptKey, out var ranks))
                {
                    context.Mechanics[mechanic.MechanicKey] = Unresolved(
                        mechanic.MechanicKey,
                        "competency",
                        mechanic.DisplayName,
                        CharacterResolutionStates.MissingCharacterInput,
                        [$"{competency.ConceptKey}.ranks"],
                        provenance: mechanic.Provenance ?? Provenance(mechanic.SourceAttributions));
                    continue;
                }
                total = checked(total + ranks);
                contributions.Add(Contribution(
                    $"{competency.ConceptKey}.ranks",
                    "Ranks",
                    ranks));

                if (profile.TrainedOnly == true && ranks <= 0)
                {
                    context.Mechanics[mechanic.MechanicKey] = Unresolved(
                        mechanic.MechanicKey,
                        "competency",
                        mechanic.DisplayName,
                        CharacterResolutionStates.MissingCapability,
                        missingCapabilities: [$"competency.trained.{competency.ConceptKey}"],
                        provenance: mechanic.Provenance ?? Provenance(mechanic.SourceAttributions));
                    continue;
                }
            }

            if (profile.SupportsTrainingState)
            {
                if (!context.HasTrainingInput)
                {
                    context.Mechanics[mechanic.MechanicKey] = Unresolved(
                        mechanic.MechanicKey,
                        "competency",
                        mechanic.DisplayName,
                        CharacterResolutionStates.MissingCharacterInput,
                        [$"{competency.ConceptKey}.trained"],
                        provenance: mechanic.Provenance ?? Provenance(mechanic.SourceAttributions));
                    continue;
                }

                if (context.TrainingKeys.Contains(competency.ConceptKey))
                {
                    if (!TryResolvedNumeric(context, "proficiency.standard", out var proficiency))
                    {
                        context.Mechanics[mechanic.MechanicKey] = Unresolved(
                            mechanic.MechanicKey,
                            "competency",
                            mechanic.DisplayName,
                            CharacterResolutionStates.MissingCharacterInput,
                            ["advancement.levels"],
                            provenance: mechanic.Provenance ?? Provenance(mechanic.SourceAttributions));
                        continue;
                    }
                    total = checked(total + proficiency);
                    contributions.Add(Contribution("proficiency.standard", "Training proficiency", proficiency));
                }
            }

            var other = context.IntegerFacts.GetValueOrDefault($"{mechanic.MechanicKey}.other");
            total = checked(total + other);
            if (other != 0)
            {
                contributions.Add(Contribution($"{mechanic.MechanicKey}.other", "Other modifiers", other));
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
                mechanic.Provenance ?? Provenance(mechanic.SourceAttributions));
            projected[competency.ConceptKey] = total;
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
                        Contribution(
                            $"competency.{component.ConceptKey}",
                            component.DisplayName,
                            projected[component.ConceptKey])).ToArray()
                };
            }
        }
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

    private static void ResolveSpellcastingMechanics(CharacterProjectionContext context)
    {
        if (!context.Spellcasting.Values.Any(value =>
                value.CastingAbilityKey is not null
                && !string.Equals(value.ResourceSystemKey, "spell-slots-3x", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        foreach (var system in context.Spellcasting.Values
                     .Where(value =>
                         value.CastingAbilityKey is not null
                         && !string.Equals(value.ResourceSystemKey, "spell-slots-3x", StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            var ability = CharacterProjectionJson.NormalizeAbilityKey(system.CastingAbilityKey!);
            if (!TryResolvedNumeric(context, $"ability.{ability}.modifier", out var abilityModifier)
                || !TryResolvedNumeric(context, "proficiency.standard", out var proficiency))
            {
                continue;
            }

            var dcKey = system.SaveDcMechanicKey ?? $"{system.SpellcastingKey}.save-dc";
            var attackKey = system.SpellAttackMechanicKey ?? $"{system.SpellcastingKey}.attack";
            context.Mechanics[dcKey] = new CharacterResolvedMechanicView(
                dcKey,
                "spell-save-dc",
                $"{system.DisplayName} Save DC",
                CharacterResolutionStates.Resolved,
                checked(8 + abilityModifier + proficiency),
                null,
                null,
                [],
                [],
                [],
                [],
                [
                    Contribution("spellcasting.save-dc.base", "Base", 8),
                    Contribution($"ability.{ability}.modifier", $"{CharacterProjectionJson.Humanize(ability)} modifier", abilityModifier),
                    Contribution("proficiency.standard", "Proficiency bonus", proficiency)
                ],
                system.Provenance);
            context.Mechanics[attackKey] = new CharacterResolvedMechanicView(
                attackKey,
                "spell-attack",
                $"{system.DisplayName} Attack",
                CharacterResolutionStates.Resolved,
                checked(abilityModifier + proficiency),
                null,
                null,
                [],
                [],
                [],
                [],
                [
                    Contribution($"ability.{ability}.modifier", $"{CharacterProjectionJson.Humanize(ability)} modifier", abilityModifier),
                    Contribution("proficiency.standard", "Proficiency bonus", proficiency)
                ],
                system.Provenance);
        }
    }

    private static void ResolvePrerequisites(CharacterProjectionContext context)
    {
        foreach (var pair in context.Prerequisites.ToArray())
        {
            var conceptKey = pair.Key;
            var prerequisite = pair.Value;
            var evaluated = prerequisite.Requirements
                .Select(requirement => EvaluatePrerequisiteRequirement(context, requirement))
                .ToArray();

            var groupResults = evaluated
                .GroupBy(requirement => requirement.GroupKey ?? requirement.RequirementKey, StringComparer.Ordinal)
                .Select(group =>
                {
                    var required = Math.Max(
                        1,
                        group.Select(value => value.GroupMatchCount).DefaultIfEmpty(1).Max());
                    var satisfied = group.Count(value => value.Satisfied == true);
                    var unknown = group.Count(value => value.Satisfied is null);
                    bool? result = satisfied >= required
                        ? true
                        : satisfied + unknown < required
                            ? false
                            : null;
                    return result;
                })
                .ToArray();

            bool? overall = groupResults.Any(value => value == false)
                ? false
                : groupResults.All(value => value == true)
                    ? true
                    : null;
            var state = overall.HasValue
                ? CharacterResolutionStates.Resolved
                : CharacterResolutionStates.ApplicableUnresolved;

            var resolved = prerequisite with
            {
                State = state,
                Satisfied = overall,
                Requirements = evaluated
            };
            context.Prerequisites[conceptKey] = resolved;

            context.Qualifications[$"qualification.prerequisite.{conceptKey}"] =
                new CharacterQualificationView(
                    $"qualification.prerequisite.{conceptKey}",
                    "prerequisite",
                    $"{CharacterProjectionJson.Humanize(conceptKey)} Prerequisites",
                    overall,
                    state,
                    [],
                    prerequisite.Provenance);

            if (overall == false && context.IsSelected(conceptKey))
            {
                var conflictKey = $"conflict.prerequisite.{conceptKey}";
                if (!context.Conflicts.Any(value =>
                        string.Equals(value.ConflictKey, conflictKey, StringComparison.Ordinal)))
                {
                    context.Conflicts.Add(new CharacterProjectionConflictView(
                        conflictKey,
                        "prerequisite-unsatisfied",
                        $"The Character does not satisfy the effective prerequisites for selected concept '{conceptKey}'.",
                        [],
                        [conceptKey]));
                }
            }
        }
    }

    private static CharacterPrerequisiteRequirementView EvaluatePrerequisiteRequirement(
        CharacterProjectionContext context,
        CharacterPrerequisiteRequirementView requirement)
    {
        if (!string.Equals(requirement.Operator, ">=", StringComparison.Ordinal)
            || requirement.NumericValue is not int threshold)
        {
            return requirement with
            {
                Satisfied = null,
                State = CharacterResolutionStates.ApplicableUnresolved,
                Reason = "The normalized prerequisite uses an operator or value shape that the Character resolver does not yet evaluate."
            };
        }

        int actual;
        string actualDescription;
        switch (requirement.Kind)
        {
            case "ability-score":
                if (string.IsNullOrWhiteSpace(requirement.TargetKey)
                    || !TryResolvedNumeric(context, requirement.TargetKey, out actual))
                {
                    return requirement with
                    {
                        Satisfied = null,
                        State = CharacterResolutionStates.MissingCharacterInput,
                        Reason = $"Character value '{requirement.TargetKey ?? "ability score"}' is unavailable."
                    };
                }
                actualDescription = requirement.TargetKey;
                break;

            case "skill-ranks":
                if (!context.HasCompetencyRanksInput
                    || string.IsNullOrWhiteSpace(requirement.TextValue)
                    || !context.TryFindCompetencyRanks(
                        requirement.TextValue,
                        out actual,
                        out var competencyConceptKey))
                {
                    return requirement with
                    {
                        Satisfied = null,
                        State = CharacterResolutionStates.MissingCharacterInput,
                        Reason = $"Skill ranks for '{requirement.TextValue ?? "unknown skill"}' are unavailable."
                    };
                }
                actualDescription = competencyConceptKey ?? requirement.TextValue;
                break;

            case "class-level":
                if (string.IsNullOrWhiteSpace(requirement.TextValue)
                    || !context.TryFindAdvancementLevel(requirement.TextValue, out actual))
                {
                    return requirement with
                    {
                        Satisfied = null,
                        State = CharacterResolutionStates.MissingCharacterInput,
                        Reason = $"Advancement level for '{requirement.TextValue ?? "unknown class"}' is unavailable."
                    };
                }
                actualDescription = requirement.TextValue;
                break;

            default:
                return requirement with
                {
                    Satisfied = null,
                    State = CharacterResolutionStates.ApplicableUnresolved,
                    Reason = $"Prerequisite kind '{requirement.Kind}' is preserved but is not yet executable."
                };
        }

        var satisfied = actual >= threshold;
        return requirement with
        {
            Satisfied = satisfied,
            State = CharacterResolutionStates.Resolved,
            Reason = $"{actualDescription} is {actual}; requirement is >= {threshold}."
        };
    }

    private static void ResolveSpellcastingResources(CharacterProjectionContext context)
    {
        if (!context.Capabilities.Contains("spellcasting"))
        {
            context.Spellcasting.Remove("spellcasting.resource-choice");
            return;
        }

        context.Spellcasting.TryGetValue(
            "spellcasting.resource-choice",
            out var resourceChoice);
        if (resourceChoice is not null
            && !string.Equals(
                resourceChoice.State,
                CharacterResolutionStates.Resolved,
                StringComparison.Ordinal))
        {
            context.Resources["resource.spellcasting"] = new CharacterResourceView(
                "resource.spellcasting",
                "Spellcasting Resource",
                resourceChoice.State,
                null,
                null,
                null,
                [],
                resourceChoice.Provenance);
            return;
        }

        var selectedSystem = resourceChoice?.ResourceSystemKey;
        if (string.Equals(selectedSystem, "spell-points", StringComparison.OrdinalIgnoreCase))
        {
            context.CurrentResources.TryGetValue(
                "resource.spell-points",
                out var currentPoints);
            context.Resources["resource.spell-points"] = new CharacterResourceView(
                "resource.spell-points",
                "Spell Points",
                CharacterResolutionStates.ApplicableUnresolved,
                context.CurrentResources.ContainsKey("resource.spell-points")
                    ? currentPoints
                    : null,
                null,
                null,
                [],
                resourceChoice?.Provenance
                    ?? CharacterProjectionContext.EmptyProvenance());
            SetSpellcastingResourceSystem(
                context,
                "spell-points",
                CharacterResolutionStates.ApplicableUnresolved);
            return;
        }

        if (selectedSystem is not null
            && !string.Equals(selectedSystem, "spell-slots", StringComparison.OrdinalIgnoreCase))
        {
            context.Resources["resource.spellcasting"] = new CharacterResourceView(
                "resource.spellcasting",
                "Spellcasting Resource",
                CharacterResolutionStates.ApplicableUnresolved,
                null,
                null,
                null,
                [],
                resourceChoice?.Provenance
                    ?? CharacterProjectionContext.EmptyProvenance());
            return;
        }

        var progressions = context.SpellSlotProgressions.Values
            .OrderBy(value => value.ConceptKey, StringComparer.Ordinal)
            .ToArray();
        if (progressions.Length == 0)
        {
            if (selectedSystem is not null)
            {
                context.Resources["resource.spell-slots"] = new CharacterResourceView(
                    "resource.spell-slots",
                    "Spell Slots",
                    CharacterResolutionStates.ApplicableUnresolved,
                    null,
                    null,
                    null,
                    [],
                    resourceChoice?.Provenance
                        ?? CharacterProjectionContext.EmptyProvenance());
            }
            return;
        }

        if (progressions.Length > 1)
        {
            context.Resources["resource.spell-slots"] = new CharacterResourceView(
                "resource.spell-slots",
                "Spell Slots",
                CharacterResolutionStates.ApplicableUnresolved,
                null,
                null,
                null,
                [],
                CharacterProjectionContext.EmptyProvenance());
            context.Conflicts.Add(new CharacterProjectionConflictView(
                "conflict.spellcasting.multiclass-slots",
                "multiclass-spell-slot-progression",
                "Multiple selected classes provide spell-slot tables. Rules Core preserves those source progressions but will not combine them until the effective multiclass caster-level rule is normalized.",
                ["resource.spell-slots"],
                progressions.Select(value => value.ConceptKey).ToArray()));
            return;
        }

        var progression = progressions[0];
        for (var index = 0; index < progression.SlotsBySpellLevel.Count; index++)
        {
            var maximum = progression.SlotsBySpellLevel[index];
            if (maximum <= 0)
            {
                continue;
            }

            var spellLevel = index + 1;
            var key = $"resource.spell-slot.{spellLevel}";
            context.CurrentResources.TryGetValue(key, out var current);
            context.Resources[key] = new CharacterResourceView(
                key,
                $"{Ordinal(spellLevel)}-Level Spell Slots",
                CharacterResolutionStates.Resolved,
                context.CurrentResources.ContainsKey(key) ? current : null,
                maximum,
                null,
                [new CharacterMechanicContributionView(
                    $"{progression.ConceptKey}.spell-slots.level-{spellLevel}",
                    $"{progression.DisplayName} level {progression.ClassLevel} slot table",
                    CharacterEffectOperations.Set,
                    maximum,
                    progression.CasterProgression,
                    progression.ConceptKey,
                    progression.Provenance)],
                progression.Provenance);
        }

        SetSpellcastingResourceSystem(
            context,
            "spell-slots",
            CharacterResolutionStates.Resolved);
    }

    private static void SetSpellcastingResourceSystem(
        CharacterProjectionContext context,
        string resourceSystemKey,
        string state)
    {
        foreach (var pair in context.Spellcasting
                     .Where(value =>
                         !string.Equals(
                             value.Key,
                             "spellcasting.resource-choice",
                             StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            context.Spellcasting[pair.Key] = pair.Value with
            {
                State = state,
                ResourceSystemKey = resourceSystemKey
            };
        }
    }

    private static string Ordinal(int value)
    {
        var mod100 = value % 100;
        if (mod100 is 11 or 12 or 13)
        {
            return $"{value}th";
        }

        return (value % 10) switch
        {
            1 => $"{value}st",
            2 => $"{value}nd",
            3 => $"{value}rd",
            _ => $"{value}th"
        };
    }

    private static void ResolveHealthMechanics(CharacterProjectionContext context)
    {
        if (context.Resources.Values.All(value =>
            !value.ResourceKey.StartsWith("resource.hit-die.", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        const string key = "health.maximum-hp";
        var totalLevels = context.Resources.Values
            .Where(value => value.ResourceKey.StartsWith("resource.hit-die.", StringComparison.OrdinalIgnoreCase))
            .Sum(value => value.MaximumValue ?? 0);
        if (totalLevels == 0)
        {
            context.Mechanics[key] = Unresolved(
                key,
                "health",
                "Maximum HP",
                CharacterResolutionStates.MissingCharacterInput,
                ["advancement.levels"]);
            return;
        }

        context.Mechanics[key] = Unresolved(
            key,
            "health",
            "Maximum HP",
            CharacterResolutionStates.RollRequired,
            requiredRolls: ["health.hit-point-gain"],
            provenance: CharacterProjectionContext.EmptyProvenance());
    }

    private static void ResolveLegacyMechanicPlaceholders(
        CharacterProjectionContext context,
        CharacterMechanicsCatalogView catalog)
    {
        foreach (var mechanic in catalog.Mechanics.Where(value =>
                     value.Competency is null
                     && value.IsAvailableUnderRuleset))
        {
            if (context.Mechanics.ContainsKey(mechanic.MechanicKey))
            {
                continue;
            }

            var requiredCapabilities = mechanic.Applicability.RequiredCapabilityKeys
                .Where(value => !context.Capabilities.Contains(value))
                .ToArray();
            if (requiredCapabilities.Length > 0)
            {
                continue;
            }

            if (mechanic.MechanicKey is "save.fortitude" or "save.reflex" or "save.will"
                or "combat.base-attack-bonus" or "combat.grapple"
                or "defense.ac.touch" or "defense.ac.flat-footed"
                or "defense.damage-reduction" or "defense.spell-resistance"
                or "resource.nonlethal-damage")
            {
                context.Mechanics[mechanic.MechanicKey] = Unresolved(
                    mechanic.MechanicKey,
                    mechanic.Kind,
                    mechanic.DisplayName,
                    CharacterResolutionStates.ApplicableUnresolved,
                    provenance: mechanic.Provenance ?? Provenance(mechanic.SourceAttributions));
            }
        }
    }

    private async Task<ResolvedRulesCatalogView> ReadAllGlobalRulesAsync(
        string? userId,
        CancellationToken cancellationToken)
    {
        var first = await resolvedRules.GetGlobalPageAsync(
            userId,
            limit: PageSize,
            offset: 0,
            cancellationToken: cancellationToken);
        if (first.TotalCount <= first.Rules.Count)
        {
            return first;
        }

        var all = new List<ResolvedRuleCatalogItemView>(first.Rules);
        for (var offset = PageSize; offset < first.TotalCount; offset += PageSize)
        {
            var page = await resolvedRules.GetGlobalPageAsync(
                userId,
                limit: PageSize,
                offset: offset,
                cancellationToken: cancellationToken);
            all.AddRange(page.Rules);
        }
        return first with { Rules = all };
    }

    private async Task<ResolvedRulesCatalogView> ReadAllCampaignRulesAsync(
        Guid campaignId,
        string userId,
        CancellationToken cancellationToken)
    {
        var first = await resolvedRules.GetCampaignPageAsync(
            campaignId,
            userId,
            limit: PageSize,
            offset: 0,
            cancellationToken: cancellationToken);
        if (first.TotalCount <= first.Rules.Count)
        {
            return first;
        }

        var all = new List<ResolvedRuleCatalogItemView>(first.Rules);
        for (var offset = PageSize; offset < first.TotalCount; offset += PageSize)
        {
            var page = await resolvedRules.GetCampaignPageAsync(
                campaignId,
                userId,
                limit: PageSize,
                offset: offset,
                cancellationToken: cancellationToken);
            all.AddRange(page.Rules);
        }
        return first with { Rules = all };
    }

    private static bool TryResolvedNumeric(
        CharacterProjectionContext context,
        string key,
        out int value)
    {
        value = default;
        if (!context.Mechanics.TryGetValue(key, out var mechanic)
            || !string.Equals(mechanic.State, CharacterResolutionStates.Resolved, StringComparison.Ordinal)
            || mechanic.NumericValue is not int numeric)
        {
            return false;
        }
        value = numeric;
        return true;
    }

    private static CharacterResolvedMechanicView Unresolved(
        string key,
        string kind,
        string displayName,
        string state,
        IReadOnlyList<string>? missingInputs = null,
        IReadOnlyList<string>? missingCapabilities = null,
        IReadOnlyList<string>? requiredChoices = null,
        IReadOnlyList<string>? requiredRolls = null,
        CharacterMechanicProvenanceView? provenance = null) =>
        new(
            key,
            kind,
            displayName,
            state,
            null,
            null,
            null,
            missingInputs ?? [],
            missingCapabilities ?? [],
            requiredChoices ?? [],
            requiredRolls ?? [],
            [],
            provenance ?? CharacterProjectionContext.EmptyProvenance());

    private static CharacterMechanicContributionView Contribution(
        string key,
        string label,
        int value) =>
        new(
            key,
            label,
            CharacterEffectOperations.Add,
            value,
            null,
            null,
            CharacterProjectionContext.EmptyProvenance());

    private static CharacterMechanicProvenanceView EffectiveProvenance(
        ResolvedRuleCatalogItemView rule)
    {
        var attribution = new CharacterMechanicSourceAttributionView(
            rule.PackageKey,
            rule.PackageDisplayName,
            null,
            rule.SourceCode,
            rule.SourceRevisionNumber,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            false);
        return new CharacterMechanicProvenanceView([], [], [attribution]);
    }

    private static CharacterMechanicProvenanceView Provenance(
        IReadOnlyList<CharacterMechanicSourceAttributionView> attributions) =>
        new(attributions, attributions, attributions);
}
