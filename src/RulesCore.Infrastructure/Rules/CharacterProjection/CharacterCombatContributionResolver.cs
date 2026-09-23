using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Collects explicit and conditional open-ended combat mechanic contributions shared by Armor Class resolvers.
/// </summary>
internal static class CharacterCombatContributionResolver
{
    internal static IReadOnlyList<CharacterMechanicContributionView> CollectOpenMechanicContributions(
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
    
}
