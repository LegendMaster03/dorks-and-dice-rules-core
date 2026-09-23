using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Resolves initiative and ability-based saving throws.
/// </summary>
internal static class CharacterInitiativeSaveResolver
{
    internal static void ResolveInitiative(CharacterProjectionContext context)
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
    
    internal static void ResolveAbilitySavingThrows(CharacterProjectionContext context)
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
    
}
