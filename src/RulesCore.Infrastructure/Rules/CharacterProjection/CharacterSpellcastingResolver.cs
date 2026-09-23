using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Resolves spellcasting mechanics and resource systems, including spell slots, spell points,
/// multiclass caster progression, and pact magic.
/// </summary>
internal static class CharacterSpellcastingResolver
{
    internal static void ResolveMechanics(CharacterProjectionContext context)
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
            if (!CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, $"ability.{ability}.modifier", out var abilityModifier)
                || !CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, "proficiency.standard", out var proficiency))
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
                    CharacterProjectionResolutionHelpers.Contribution("spellcasting.save-dc.base", "Base", 8),
                    CharacterProjectionResolutionHelpers.Contribution($"ability.{ability}.modifier", $"{CharacterProjectionJson.Humanize(ability)} modifier", abilityModifier),
                    CharacterProjectionResolutionHelpers.Contribution("proficiency.standard", "Proficiency bonus", proficiency)
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
                    CharacterProjectionResolutionHelpers.Contribution($"ability.{ability}.modifier", $"{CharacterProjectionJson.Humanize(ability)} modifier", abilityModifier),
                    CharacterProjectionResolutionHelpers.Contribution("proficiency.standard", "Proficiency bonus", proficiency)
                ],
                system.Provenance);
        }
    }
    
    
    internal static void ResolveResources(CharacterProjectionContext context)
    {
        if (!context.Capabilities.Contains("spellcasting"))
        {
            context.Spellcasting.Remove("spellcasting.resource-choice");
            return;
        }
    
        ResolvePactMagicResources(context);
    
        if (!context.Capabilities.Contains("spellcasting.standard"))
        {
            context.Spellcasting.Remove("spellcasting.resource-choice");
            context.Resources.Remove("resource.spellcasting");
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
        var progressions = context.SpellSlotProgressions.Values
            .OrderBy(value => value.ConceptKey, StringComparer.Ordinal)
            .ToArray();
    
        if (progressions.Length == 0)
        {
            var missingResourceKey =
                string.Equals(
                    selectedSystem,
                    "spell-points",
                    StringComparison.OrdinalIgnoreCase)
                    ? "resource.spell-points"
                    : "resource.spell-slots";
            context.Resources[missingResourceKey] = new CharacterResourceView(
                missingResourceKey,
                string.Equals(
                    selectedSystem,
                    "spell-points",
                    StringComparison.OrdinalIgnoreCase)
                    ? "Spell Points"
                    : "Spell Slots",
                CharacterResolutionStates.ApplicableUnresolved,
                null,
                null,
                null,
                [],
                resourceChoice?.Provenance
                    ?? CharacterProjectionContext.EmptyProvenance());
            return;
        }
    
        if (string.Equals(selectedSystem, "spell-points", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryResolveSpellPointCasterLevel(
                    progressions,
                    out var spellPointCasterLevel,
                    out var spellPointCasterLevelContributions,
                    out var spellPointFailureReason)
                || !CharacterSpellPointRules.TryGetProgression(
                    spellPointCasterLevel,
                    out var spellPointProgression))
            {
                context.Resources["resource.spell-points"] = new CharacterResourceView(
                    "resource.spell-points",
                    "Spell Points",
                    CharacterResolutionStates.ApplicableUnresolved,
                    null,
                    null,
                    null,
                    [],
                    resourceChoice?.Provenance
                        ?? CharacterProjectionContext.EmptyProvenance());
                context.Conflicts.Add(new CharacterProjectionConflictView(
                    "conflict.spellcasting.spell-points",
                    "spell-point-progression",
                    spellPointFailureReason
                        ?? $"Effective spell-point caster level {spellPointCasterLevel} is outside the supported official progression.",
                    ["resource.spell-points"],
                    progressions.Select(value => value.ConceptKey).ToArray()));
                SetSpellcastingResourceSystem(
                    context,
                    "spell-points",
                    CharacterResolutionStates.ApplicableUnresolved);
                return;
            }
    
            var pointContributions = progressions
                .Select(progression => new CharacterMechanicContributionView(
                    $"{progression.ConceptKey}.spell-point-caster-level",
                    $"{progression.DisplayName} spell-point caster level",
                    CharacterEffectOperations.Add,
                    spellPointCasterLevelContributions.GetValueOrDefault(
                        progression.ConceptKey),
                    progression.CasterProgression,
                    progression.ConceptKey,
                    progression.Provenance))
                .Append(new CharacterMechanicContributionView(
                    $"spellcasting.spell-points.level-{spellPointProgression.CasterLevel}",
                    $"Spell-point table level {spellPointProgression.CasterLevel}",
                    CharacterEffectOperations.Set,
                    spellPointProgression.MaximumPoints,
                    $"maximum-slot-level:{spellPointProgression.MaximumSlotLevel}",
                    null,
                    CharacterProjectionContext.EmptyProvenance()))
                .ToArray();
    
            context.CurrentResources.TryGetValue(
                "resource.spell-points",
                out var currentPoints);
            context.Resources["resource.spell-points"] = new CharacterResourceView(
                "resource.spell-points",
                "Spell Points",
                CharacterResolutionStates.Resolved,
                context.CurrentResources.ContainsKey("resource.spell-points")
                    ? currentPoints
                    : null,
                spellPointProgression.MaximumPoints,
                null,
                pointContributions,
                resourceChoice?.Provenance
                    ?? CharacterProjectionContext.EmptyProvenance());
    
            context.Mechanics["spellcasting.spell-points.maximum-slot-level"] =
                new CharacterResolvedMechanicView(
                    "spellcasting.spell-points.maximum-slot-level",
                    "spellcasting",
                    "Maximum Spell-Point Slot Level",
                    CharacterResolutionStates.Resolved,
                    spellPointProgression.MaximumSlotLevel,
                    null,
                    "spell-level",
                    [],
                    [],
                    [],
                    [],
                    [
                        CharacterProjectionResolutionHelpers.Contribution(
                            $"spellcasting.spell-points.level-{spellPointProgression.CasterLevel}",
                            $"Spell-point table level {spellPointProgression.CasterLevel}",
                            spellPointProgression.MaximumSlotLevel)
                    ],
                    resourceChoice?.Provenance
                        ?? CharacterProjectionContext.EmptyProvenance());
    
            for (var spellLevel = 1;
                 spellLevel <= spellPointProgression.MaximumSlotLevel;
                 spellLevel++)
            {
                if (!CharacterSpellPointRules.TryGetSlotCost(
                        spellLevel,
                        out var pointCost))
                {
                    continue;
                }
    
                var costKey =
                    $"spellcasting.spell-points.slot-cost.level-{spellLevel}";
                context.Mechanics[costKey] = new CharacterResolvedMechanicView(
                    costKey,
                    "spellcasting",
                    $"{Ordinal(spellLevel)}-Level Slot Spell-Point Cost",
                    CharacterResolutionStates.Resolved,
                    pointCost,
                    null,
                    "spell-points",
                    [],
                    [],
                    [],
                    [],
                    [],
                    resourceChoice?.Provenance
                        ?? CharacterProjectionContext.EmptyProvenance());
    
                if (CharacterSpellPointRules.HasPerLongRestCreationLimit(spellLevel))
                {
                    var limitKey =
                        $"spellcasting.spell-points.slot-creation-limit.level-{spellLevel}";
                    context.Mechanics[limitKey] = new CharacterResolvedMechanicView(
                        limitKey,
                        "spellcasting",
                        $"{Ordinal(spellLevel)}-Level Spell-Point Slot Creation Limit",
                        CharacterResolutionStates.Resolved,
                        CharacterSpellPointRules.HighLevelSlotCreationLimit,
                        "per-long-rest",
                        "slot",
                        [],
                        [],
                        [],
                        [],
                        [],
                        resourceChoice?.Provenance
                            ?? CharacterProjectionContext.EmptyProvenance());
                }
            }
    
            SetSpellcastingResourceSystem(
                context,
                "spell-points",
                CharacterResolutionStates.Resolved);
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
    
        IReadOnlyList<int> slotMaximums;
        CharacterSpellSlotProgression referenceProgression;
        int? effectiveCasterLevel = null;
        IReadOnlyDictionary<string, int>? casterLevelContributions = null;
    
        if (progressions.Length > 1)
        {
            if (!TryResolveMulticlassSpellSlots(
                    progressions,
                    out slotMaximums,
                    out referenceProgression,
                    out var combinedCasterLevel,
                    out var contributions,
                    out var failureReason))
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
                    failureReason
                        ?? "The selected spellcasting progressions can not be combined from the effective source tables.",
                    ["resource.spell-slots"],
                    progressions.Select(value => value.ConceptKey).ToArray()));
                return;
            }
    
            effectiveCasterLevel = combinedCasterLevel;
            casterLevelContributions = contributions;
        }
        else
        {
            referenceProgression = progressions[0];
            slotMaximums = referenceProgression.SlotsBySpellLevel;
        }
    
        for (var index = 0; index < slotMaximums.Count; index++)
        {
            var maximum = slotMaximums[index];
            if (maximum <= 0)
            {
                continue;
            }
    
            var spellLevel = index + 1;
            var key = $"resource.spell-slot.{spellLevel}";
            context.CurrentResources.TryGetValue(key, out var current);
    
            var maximumContributions = new List<CharacterMechanicContributionView>();
            if (effectiveCasterLevel is int combinedLevel
                && casterLevelContributions is not null)
            {
                foreach (var progression in progressions)
                {
                    maximumContributions.Add(new CharacterMechanicContributionView(
                        $"{progression.ConceptKey}.effective-caster-level",
                        $"{progression.DisplayName} effective caster level",
                        CharacterEffectOperations.Add,
                        casterLevelContributions.GetValueOrDefault(progression.ConceptKey),
                        progression.CasterProgression,
                        progression.ConceptKey,
                        progression.Provenance));
                }
    
                maximumContributions.Add(new CharacterMechanicContributionView(
                    $"spellcasting.multiclass-slots.level-{spellLevel}",
                    $"Combined caster level {combinedLevel} slot table",
                    CharacterEffectOperations.Set,
                    maximum,
                    $"effective-caster-level:{combinedLevel}",
                    referenceProgression.ConceptKey,
                    referenceProgression.Provenance));
            }
            else
            {
                maximumContributions.Add(new CharacterMechanicContributionView(
                    $"{referenceProgression.ConceptKey}.spell-slots.level-{spellLevel}",
                    $"{referenceProgression.DisplayName} level {referenceProgression.ClassLevel} slot table",
                    CharacterEffectOperations.Set,
                    maximum,
                    referenceProgression.CasterProgression,
                    referenceProgression.ConceptKey,
                    referenceProgression.Provenance));
            }
    
            context.Resources[key] = new CharacterResourceView(
                key,
                $"{Ordinal(spellLevel)}-Level Spell Slots",
                CharacterResolutionStates.Resolved,
                context.CurrentResources.ContainsKey(key) ? current : null,
                maximum,
                null,
                maximumContributions,
                effectiveCasterLevel is null
                    ? referenceProgression.Provenance
                    : CharacterProjectionContext.EmptyProvenance());
        }
    
        SetSpellcastingResourceSystem(
            context,
            "spell-slots",
            CharacterResolutionStates.Resolved);
    }
    
    private static bool TryResolveSpellPointCasterLevel(
        IReadOnlyList<CharacterSpellSlotProgression> progressions,
        out int effectiveCasterLevel,
        out IReadOnlyDictionary<string, int> casterLevelContributions,
        out string? failureReason)
    {
        effectiveCasterLevel = 0;
        failureReason = null;
        var contributions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    
        foreach (var progression in progressions)
        {
            if (!TryGetEffectiveCasterLevel(
                    progression.ClassLevel,
                    progression.CasterProgression,
                    out var contribution))
            {
                casterLevelContributions = contributions;
                failureReason =
                    $"Caster progression '{progression.CasterProgression ?? "unknown"}' from '{progression.DisplayName}' does not have a normalized spell-point weighting.";
                return false;
            }
    
            contributions[progression.ConceptKey] = contribution;
            effectiveCasterLevel = checked(effectiveCasterLevel + contribution);
        }
    
        casterLevelContributions = contributions;
        return true;
    }
    
    private static bool TryResolveMulticlassSpellSlots(
        IReadOnlyList<CharacterSpellSlotProgression> progressions,
        out IReadOnlyList<int> slots,
        out CharacterSpellSlotProgression referenceProgression,
        out int effectiveCasterLevel,
        out IReadOnlyDictionary<string, int> casterLevelContributions,
        out string? failureReason)
    {
        slots = [];
        referenceProgression = progressions[0];
        effectiveCasterLevel = 0;
        failureReason = null;
        var contributions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    
        foreach (var progression in progressions)
        {
            if (!TryGetEffectiveCasterLevel(
                    progression.ClassLevel,
                    progression.CasterProgression,
                    out var contribution))
            {
                casterLevelContributions = contributions;
                failureReason =
                    $"Caster progression '{progression.CasterProgression ?? "unknown"}' from '{progression.DisplayName}' does not have normalized multiclass weighting.";
                return false;
            }
    
            contributions[progression.ConceptKey] = contribution;
            effectiveCasterLevel = checked(effectiveCasterLevel + contribution);
        }
    
        casterLevelContributions = contributions;
        if (effectiveCasterLevel <= 0)
        {
            slots = [];
            return true;
        }
    
        var candidates = new List<(CharacterSpellSlotProgression Progression, IReadOnlyList<int> Slots)>();
        foreach (var progression in progressions)
        {
            if (!TryGetSourceClassLevelForEffectiveCasterLevel(
                    effectiveCasterLevel,
                    progression.CasterProgression,
                    out var sourceClassLevel)
                || sourceClassLevel <= 0
                || sourceClassLevel > progression.SlotsByClassLevel.Count)
            {
                continue;
            }
    
            candidates.Add((
                progression,
                progression.SlotsByClassLevel[sourceClassLevel - 1]));
        }
    
        if (candidates.Count == 0)
        {
            failureReason =
                $"No selected source slot table can represent combined effective caster level {effectiveCasterLevel}.";
            return false;
        }
    
        var referenceCandidate = candidates
            .OrderByDescending(value => value.Slots.Count)
            .ThenBy(value => value.Progression.ConceptKey, StringComparer.Ordinal)
            .First();
        referenceProgression = referenceCandidate.Progression;
        var referenceSlots = referenceCandidate.Slots;
        var width = candidates.Max(value => value.Slots.Count);
    
        foreach (var candidate in candidates)
        {
            for (var index = 0; index < width; index++)
            {
                var expected = index < referenceSlots.Count ? referenceSlots[index] : 0;
                var actual = index < candidate.Slots.Count ? candidate.Slots[index] : 0;
                if (actual == expected)
                {
                    continue;
                }
    
                failureReason =
                    $"Selected source spell-slot tables disagree at combined effective caster level {effectiveCasterLevel}.";
                return false;
            }
        }
    
        slots = Enumerable.Range(0, width)
            .Select(index => index < referenceSlots.Count ? referenceSlots[index] : 0)
            .ToArray();
        return true;
    }
    
    private static bool TryGetEffectiveCasterLevel(
        int classLevel,
        string? casterProgression,
        out int effectiveLevel)
    {
        effectiveLevel = 0;
        if (classLevel < 0)
        {
            return false;
        }
    
        switch (casterProgression?.Trim().ToLowerInvariant())
        {
            case "full":
                effectiveLevel = classLevel;
                return true;
            case "1/2":
                effectiveLevel = classLevel / 2;
                return true;
            case "artificer":
                effectiveLevel = (classLevel + 1) / 2;
                return true;
            case "1/3":
                effectiveLevel = classLevel / 3;
                return true;
            default:
                return false;
        }
    }
    
    private static bool TryGetSourceClassLevelForEffectiveCasterLevel(
        int effectiveCasterLevel,
        string? casterProgression,
        out int sourceClassLevel)
    {
        sourceClassLevel = 0;
        if (effectiveCasterLevel <= 0)
        {
            return false;
        }
    
        switch (casterProgression?.Trim().ToLowerInvariant())
        {
            case "full":
                sourceClassLevel = effectiveCasterLevel;
                return true;
            case "1/2":
                sourceClassLevel = checked(effectiveCasterLevel * 2);
                return true;
            case "artificer":
                sourceClassLevel = checked((effectiveCasterLevel * 2) - 1);
                return true;
            case "1/3":
                sourceClassLevel = checked(effectiveCasterLevel * 3);
                return true;
            default:
                return false;
        }
    }
    
    private static void ResolvePactMagicResources(CharacterProjectionContext context)
    {
        foreach (var progression in context.PactMagicProgressions.Values
                     .OrderBy(value => value.ConceptKey, StringComparer.Ordinal))
        {
            var key =
                $"resource.pact-slot.{progression.ConceptKey}.level-{progression.SlotLevel}";
            context.CurrentResources.TryGetValue(key, out var current);
            context.Resources[key] = new CharacterResourceView(
                key,
                $"{progression.DisplayName} {Ordinal(progression.SlotLevel)}-Level Pact Slots",
                CharacterResolutionStates.Resolved,
                context.CurrentResources.ContainsKey(key) ? current : null,
                progression.SlotCount,
                null,
                [new CharacterMechanicContributionView(
                    $"{progression.ConceptKey}.pact-slots.level-{progression.SlotLevel}",
                    $"{progression.DisplayName} level {progression.ClassLevel} Pact Magic table",
                    CharacterEffectOperations.Set,
                    progression.SlotCount,
                    $"slot-level:{progression.SlotLevel}",
                    progression.ConceptKey,
                    progression.Provenance)],
                progression.Provenance);
    
            var spellcastingKey = $"spellcasting.{progression.ConceptKey}";
            if (context.Spellcasting.TryGetValue(spellcastingKey, out var spellcasting))
            {
                context.Spellcasting[spellcastingKey] = spellcasting with
                {
                    State = CharacterResolutionStates.Resolved,
                    ResourceSystemKey = "pact-magic"
                };
            }
        }
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
                             StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(
                             value.Value.ResourceSystemKey,
                             "pact-magic",
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
    
}
