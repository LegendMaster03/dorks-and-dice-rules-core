using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Resolves hit dice, maximum hit points, nonlethal damage, and related health mechanics.
/// </summary>
internal static class CharacterHealthResolver
{
    internal static void Resolve(CharacterProjectionContext context)
    {
        const string key = "health.maximum-hp";
        var profiles = context.HitDice.Values
            .Where(value => value.ClassLevel > 0)
            .OrderBy(value => value.ConceptKey, StringComparer.Ordinal)
            .ToArray();
        if (profiles.Length == 0)
        {
            if (context.HitDice.Count > 0)
            {
                context.Mechanics[key] = CharacterProjectionResolutionHelpers.Unresolved(
                    key,
                    "health",
                    "Maximum HP",
                    CharacterResolutionStates.MissingCharacterInput,
                    ["advancement.levels"]);
            }
            return;
        }
    
        if (profiles.Any(value => value.Faces is null))
        {
            context.Mechanics[key] = CharacterProjectionResolutionHelpers.Unresolved(
                key,
                "health",
                "Maximum HP",
                CharacterResolutionStates.SourceUnavailable,
                provenance: CharacterProjectionContext.EmptyProvenance());
            return;
        }
    
        var suppliedByLevel =
            new Dictionary<(string ConceptKey, int ClassLevel), CharacterHitPointGainInput>();
        var hasConflict = false;
    
        foreach (var supplied in context.HitPointGains)
        {
            var conceptKey = supplied.ConceptKey?.Trim();
            var profile = string.IsNullOrWhiteSpace(conceptKey)
                ? null
                : profiles.FirstOrDefault(value => string.Equals(
                    value.ConceptKey,
                    conceptKey,
                    StringComparison.OrdinalIgnoreCase));
            if (profile is null
                || supplied.ClassLevel <= 0
                || supplied.ClassLevel > profile.ClassLevel)
            {
                hasConflict = true;
                context.Conflicts.Add(new CharacterProjectionConflictView(
                    $"conflict.health.hit-point-gain.{conceptKey ?? "unknown"}.level-{supplied.ClassLevel}",
                    "invalid-hit-point-gain",
                    "A supplied hit-point gain does not correspond to a selected class level.",
                    [key],
                    string.IsNullOrWhiteSpace(conceptKey) ? [] : [conceptKey]));
                continue;
            }
    
            var inputKey = HitPointGainInputKey(profile.ConceptKey, supplied.ClassLevel);
            if (supplied.HitDieValue <= 0 || supplied.HitDieValue > profile.Faces!.Value)
            {
                hasConflict = true;
                context.Conflicts.Add(new CharacterProjectionConflictView(
                    $"conflict.{inputKey}",
                    "invalid-hit-die-value",
                    $"Hit-die value {supplied.HitDieValue} for {profile.DisplayName} level {supplied.ClassLevel} must be between 1 and {profile.Faces.Value}.",
                    [key],
                    [profile.ConceptKey]));
                continue;
            }
    
            var identity = (profile.ConceptKey.ToUpperInvariant(), supplied.ClassLevel);
            if (!suppliedByLevel.TryAdd(identity, supplied))
            {
                hasConflict = true;
                context.Conflicts.Add(new CharacterProjectionConflictView(
                    $"conflict.{inputKey}.duplicate",
                    "duplicate-hit-point-gain",
                    $"More than one hit-point gain was supplied for {profile.DisplayName} level {supplied.ClassLevel}.",
                    [key],
                    [profile.ConceptKey]));
            }
        }
    
        if (hasConflict)
        {
            context.Mechanics[key] = CharacterProjectionResolutionHelpers.Unresolved(
                key,
                "health",
                "Maximum HP",
                CharacterResolutionStates.Conflict);
            return;
        }
    
        var missingInputs = new List<string>();
        var resolvedGains =
            new List<(CharacterHitDieProfile Profile, int Level, int HitDieValue)>();
        foreach (var profile in profiles)
        {
            for (var level = 1; level <= profile.ClassLevel; level++)
            {
                var inputKey = HitPointGainInputKey(profile.ConceptKey, level);
                var identity = (profile.ConceptKey.ToUpperInvariant(), level);
                if (suppliedByLevel.TryGetValue(identity, out var supplied))
                {
                    if (context.Rolls.ContainsKey(inputKey))
                    {
                        context.Conflicts.Add(new CharacterProjectionConflictView(
                            $"conflict.{inputKey}.duplicate",
                            "duplicate-hit-point-gain",
                            $"Both a resolved hit-point gain and a runtime roll were supplied for {profile.DisplayName} level {level}.",
                            [key],
                            [profile.ConceptKey]));
                        hasConflict = true;
                        continue;
                    }
                    resolvedGains.Add((profile, level, supplied.HitDieValue));
                    continue;
                }
    
                if (context.Rolls.TryGetValue(inputKey, out var rolled))
                {
                    if (rolled <= 0 || rolled > profile.Faces!.Value)
                    {
                        context.Conflicts.Add(new CharacterProjectionConflictView(
                            $"conflict.{inputKey}",
                            "invalid-hit-die-value",
                            $"Hit-die roll {rolled} for {profile.DisplayName} level {level} must be between 1 and {profile.Faces.Value}.",
                            [key],
                            [profile.ConceptKey]));
                        hasConflict = true;
                        continue;
                    }
                    resolvedGains.Add((profile, level, rolled));
                    continue;
                }
    
                missingInputs.Add(inputKey);
            }
        }
    
        if (hasConflict)
        {
            context.Mechanics[key] = CharacterProjectionResolutionHelpers.Unresolved(
                key,
                "health",
                "Maximum HP",
                CharacterResolutionStates.Conflict);
            return;
        }
    
        if (!CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, "ability.constitution.modifier", out var constitutionModifier))
        {
            missingInputs.Add("ability.constitution.base");
        }
    
        if (missingInputs.Count > 0)
        {
            context.Mechanics[key] = CharacterProjectionResolutionHelpers.Unresolved(
                key,
                "health",
                "Maximum HP",
                CharacterResolutionStates.MissingCharacterInput,
                missingInputs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            return;
        }
    
        var contributions = new List<CharacterMechanicContributionView>();
        long total = 0;
        foreach (var gain in resolvedGains)
        {
            var effectiveGain = StandardDndCharacterMath.HitPointGain(
                gain.HitDieValue,
                constitutionModifier);
            total = checked(total + effectiveGain);
            contributions.Add(new CharacterMechanicContributionView(
                HitPointGainInputKey(gain.Profile.ConceptKey, gain.Level),
                $"{gain.Profile.DisplayName} level {gain.Level} hit points",
                CharacterEffectOperations.Add,
                effectiveGain,
                $"hit die {gain.HitDieValue}; Constitution modifier {constitutionModifier}",
                gain.Profile.ConceptKey,
                gain.Profile.Provenance));
        }
    
        context.Mechanics[key] = new CharacterResolvedMechanicView(
            key,
            "health",
            "Maximum HP",
            CharacterResolutionStates.Resolved,
            checked((int)total),
            null,
            "hit points",
            [],
            [],
            [],
            [],
            contributions,
            CharacterProjectionContext.EmptyProvenance());
    }
    
    private static string HitPointGainInputKey(string conceptKey, int classLevel) =>
        $"health.hit-point-gain.{conceptKey}.level-{classLevel}";
    
}
