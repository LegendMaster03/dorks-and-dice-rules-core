using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Resolves base/temporary abilities, derived modifiers, and proficiency progression.
/// </summary>
internal static class CharacterAbilityMechanicsResolver
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

    internal static void ResolveAbilities(CharacterProjectionContext context)
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
    
    internal static void ResolveProficiency(CharacterProjectionContext context)
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
    
    
}