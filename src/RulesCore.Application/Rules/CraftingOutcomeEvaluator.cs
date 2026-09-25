using RulesCore.Domain.Rules;

namespace RulesCore.Application.Rules;

public static class CraftingOutcomeEvaluator
{
    public static CraftingCheckResolutionView ResolveManufacturing(
        CharacterRulesProjectionView projection,
        ManufacturingResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Competency);

        var competency = ResolveCompetency(
            projection,
            request.Competency,
            includeGoverningAbility: true);

        var rollMode = !competency.IsQualified && !request.HasQualifiedGuidance
            ? CharacterMechanicRollModes.Disadvantage
            : CharacterMechanicRollModes.Normal;

        var manufacturingAbility = ResolveManufacturingAbility(
            projection,
            request,
            competency);

        int? total = request.D20Roll is int roll
            ? checked(roll + manufacturingAbility + competency.CompetencyContribution + request.OtherModifier)
            : null;

        var outcome = ResolveManufacturingOutcome(total, request.TargetDc);

        return new CraftingCheckResolutionView(
            "manufacturing",
            "Manufacturing Check",
            competency.Key,
            competency.DisplayName,
            competency.Manual,
            competency.IsQualified,
            rollMode,
            competency.CompetencyContribution,
            competency.AbilityContribution,
            request.OtherModifier,
            request.D20Roll,
            total,
            request.TargetDc,
            outcome.MeetsTarget,
            outcome.Kind,
            outcome.Margin,
            FlawCount: null,
            outcome.InputsConsumed,
            outcome.ProducesFunctionalOutput);
    }

    public static CraftingCheckResolutionView ResolveEnchanting(
        CharacterRulesProjectionView projection,
        EnchantingResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(request);

        var competencyInput = request.Competency ?? ResolveCreatureTypeCompetency(request.CreatureType);
        var competency = ResolveCompetency(
            projection,
            competencyInput,
            includeGoverningAbility: false);
        var spellcastingAbility = ResolveSpellcastingAbility(projection, request.SpellcastingKey);
        var abilityModifier = ResolveAbilityModifier(projection, spellcastingAbility);

        int? total = request.D20Roll is int roll
            ? checked(roll + abilityModifier + competency.CompetencyContribution + request.OtherModifier)
            : null;

        var outcome = ResolveEnchantingOutcome(total, request.TargetDc);

        return new CraftingCheckResolutionView(
            "enchanting",
            "Enchanting Check",
            competency.Key,
            competency.DisplayName,
            competency.Manual,
            competency.IsQualified,
            CharacterMechanicRollModes.Normal,
            competency.CompetencyContribution,
            abilityModifier,
            request.OtherModifier,
            request.D20Roll,
            total,
            request.TargetDc,
            outcome.MeetsTarget,
            outcome.Kind,
            outcome.Margin,
            outcome.FlawCount,
            outcome.InputsConsumed,
            outcome.ProducesFunctionalOutput);
    }

    private static CraftingOutcome ResolveManufacturingOutcome(
        int? total,
        int? targetDc)
    {
        if (total is not int totalValue || targetDc is not int dc)
        {
            return CraftingOutcome.Pending;
        }

        var margin = checked(totalValue - dc);
        return margin >= 0
            ? new CraftingOutcome(
                CraftingOutcomeKinds.Completed,
                true,
                margin,
                null,
                InputsConsumed: true,
                ProducesFunctionalOutput: true)
            : new CraftingOutcome(
                CraftingOutcomeKinds.Failed,
                false,
                margin,
                null,
                InputsConsumed: true,
                ProducesFunctionalOutput: false);
    }

    private static CraftingOutcome ResolveEnchantingOutcome(
        int? total,
        int? targetDc)
    {
        if (total is not int totalValue || targetDc is not int dc)
        {
            return CraftingOutcome.Pending;
        }

        var margin = checked(totalValue - dc);
        if (margin <= -13)
        {
            return new CraftingOutcome(
                CraftingOutcomeKinds.Destroyed,
                false,
                margin,
                FlawCount: null,
                InputsConsumed: true,
                ProducesFunctionalOutput: false);
        }

        var flaws = margin switch
        {
            >= 0 => 0,
            >= -4 => 1,
            >= -8 => 2,
            _ => 3
        };
        return new CraftingOutcome(
            flaws == 0
                ? CraftingOutcomeKinds.Completed
                : CraftingOutcomeKinds.CompletedWithFlaws,
            margin >= 0,
            margin,
            flaws,
            InputsConsumed: true,
            ProducesFunctionalOutput: true);
    }

    private static CraftingCompetencyInput ResolveCreatureTypeCompetency(string? creatureType)
    {
        var definition = KnownHarvestingRules.FindCreatureType(creatureType);
        if (definition is null)
        {
            throw new ArgumentException(
                "Enchanting requires a known creature type, a universal competency, or a manual competency entry.");
        }

        return new CraftingCompetencyInput(definition.CompetencyKey);
    }

    private static ResolvedCompetency ResolveCompetency(
        CharacterRulesProjectionView projection,
        CraftingCompetencyInput input,
        bool includeGoverningAbility)
    {
        var hasKey = !string.IsNullOrWhiteSpace(input.CompetencyKey);
        var hasManual = input.Manual is not null;
        if (hasKey == hasManual)
        {
            throw new ArgumentException(
                "Supply exactly one universal competency key or manual competency entry.");
        }

        if (input.Manual is not null)
        {
            if (string.IsNullOrWhiteSpace(input.Manual.DisplayName))
            {
                throw new ArgumentException("Manual competency display name can not be blank.");
            }

            return new ResolvedCompetency(
                "manual",
                input.Manual.DisplayName.Trim(),
                input.Manual.IsQualified,
                0,
                input.Manual.Contribution,
                true);
        }

        var key = NormalizeCompetencyKey(input.CompetencyKey!);
        var mechanic = projection.Mechanics.SingleOrDefault(value =>
            string.Equals(value.MechanicKey, key, StringComparison.OrdinalIgnoreCase));

        if (mechanic is null)
        {
            throw new KeyNotFoundException(
                $"Universal competency '{key}' is not present in the resolved Character projection. Use manual entry when no Rules Core concept exists.");
        }
        if (!string.Equals(mechanic.State, "resolved", StringComparison.OrdinalIgnoreCase)
            || mechanic.NumericValue is null)
        {
            throw new InvalidOperationException(
                $"Universal competency '{key}' is not resolved for this Character.");
        }

        var ability = mechanic.Contributions
            .Where(value => value.ContributionKey.StartsWith("ability.", StringComparison.OrdinalIgnoreCase)
                && value.ContributionKey.EndsWith(".modifier", StringComparison.OrdinalIgnoreCase))
            .Sum(value => value.NumericValue ?? 0);
        var nonAbility = mechanic.Contributions
            .Where(value => !(value.ContributionKey.StartsWith("ability.", StringComparison.OrdinalIgnoreCase)
                && value.ContributionKey.EndsWith(".modifier", StringComparison.OrdinalIgnoreCase)))
            .Sum(value => value.NumericValue ?? 0);

        return new ResolvedCompetency(
            key,
            mechanic.DisplayName,
            IsQualified(mechanic),
            includeGoverningAbility ? ability : 0,
            includeGoverningAbility ? nonAbility : nonAbility,
            false);
    }

    private static bool IsQualified(CharacterResolvedMechanicView mechanic) =>
        mechanic.Contributions.Any(value =>
            (string.Equals(value.ContributionKey, "proficiency.standard", StringComparison.OrdinalIgnoreCase)
                && (value.NumericValue ?? 0) > 0)
            || (value.ContributionKey.EndsWith(".ranks", StringComparison.OrdinalIgnoreCase)
                && (value.NumericValue ?? 0) > 0));

    private static string ResolveSpellcastingAbility(
        CharacterRulesProjectionView projection,
        string? requestedSpellcastingKey)
    {
        var available = projection.Spellcasting
            .Where(value =>
                string.Equals(value.State, "resolved", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(value.CastingAbilityKey))
            .ToArray();

        if (!string.IsNullOrWhiteSpace(requestedSpellcastingKey))
        {
            var selected = available.SingleOrDefault(value =>
                string.Equals(
                    value.SpellcastingKey,
                    requestedSpellcastingKey.Trim(),
                    StringComparison.OrdinalIgnoreCase));
            return selected?.CastingAbilityKey
                ?? throw new KeyNotFoundException(
                    $"Spellcasting profile '{requestedSpellcastingKey}' is not resolved for this Character.");
        }

        return available.Length switch
        {
            1 => available[0].CastingAbilityKey!,
            0 => throw new InvalidOperationException(
                "Enchanting requires a resolved spellcasting ability."),
            _ => throw new InvalidOperationException(
                "This Character has multiple resolved spellcasting profiles. Select the spellcasting profile used for Enchanting.")
        };
    }

    private static int ResolveAbilityModifier(
        CharacterRulesProjectionView projection,
        string abilityKey)
    {
        var key = $"ability.{abilityKey.Trim().ToLowerInvariant()}.modifier";
        var mechanic = projection.Mechanics.SingleOrDefault(value =>
            string.Equals(value.MechanicKey, key, StringComparison.OrdinalIgnoreCase));
        if (mechanic?.NumericValue is not int value
            || !string.Equals(mechanic.State, "resolved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Spellcasting ability modifier '{key}' is not resolved for this Character.");
        }
        return value;
    }

    private static string NormalizeCompetencyKey(string value)
    {
        var normalized = value.Trim();
        return normalized.StartsWith("competency.", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"competency.{normalized}";
    }

    private sealed record CraftingOutcome(
        string Kind,
        bool? MeetsTarget,
        int? Margin,
        int? FlawCount,
        bool InputsConsumed,
        bool ProducesFunctionalOutput)
    {
        public static CraftingOutcome Pending { get; } =
            new(
                CraftingOutcomeKinds.Pending,
                MeetsTarget: null,
                Margin: null,
                FlawCount: null,
                InputsConsumed: false,
                ProducesFunctionalOutput: false);
    }

    private sealed record ResolvedCompetency(
        string Key,
        string DisplayName,
        bool IsQualified,
        int AbilityContribution,
        int CompetencyContribution,
        bool Manual);
}
