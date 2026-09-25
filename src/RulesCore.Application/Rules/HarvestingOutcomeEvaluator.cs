using RulesCore.Domain.Rules;

namespace RulesCore.Application.Rules;

/// <summary>
/// Executes the Harvesting procedure after Rules Core has resolved the applicable creature table.
/// Consumers supply runtime results and ordering; Rules Core owns helper eligibility, helper limits,
/// cumulative Harvest DC calculation, and award determination.
/// </summary>
public static class HarvestingOutcomeEvaluator
{
    public static HarvestingOutcomeView Evaluate(
        HarvestingResolvedTableView table,
        HarvestingOutcomeRequest request)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Table);
        ArgumentNullException.ThrowIfNull(request.HarvestOrderComponentKeys);

        if (request.HarvestOrderComponentKeys.Count == 0)
        {
            throw new ArgumentException(
                "Harvest order must contain at least one component.",
                nameof(request));
        }

        var byKey = table.Components.ToDictionary(
            component => component.Key,
            StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<HarvestingComponentView>();
        foreach (var suppliedKey in request.HarvestOrderComponentKeys)
        {
            if (string.IsNullOrWhiteSpace(suppliedKey))
            {
                throw new ArgumentException(
                    "Harvest order component keys can not be blank.",
                    nameof(request));
            }

            var key = suppliedKey.Trim();
            if (!seen.Add(key))
            {
                throw new ArgumentException(
                    $"Harvest order contains component '{key}' more than once.",
                    nameof(request));
            }
            if (!byKey.TryGetValue(key, out var component))
            {
                throw new KeyNotFoundException(
                    $"Harvest order component '{key}' is not present in the resolved Harvesting table.");
            }
            ordered.Add(component);
        }

        var helpers = request.Helpers ?? [];
        var creatureSize = ResolveCreatureSize(table.CreatureSize, request.CreatureSize, helpers.Count);

        var totalDefinition = KnownCharacterMechanics.FindByKey(KnownHarvestingRules.TotalMechanicKey)
            ?? throw new InvalidOperationException("Harvesting total mechanic is not registered.");
        var stringInputs = new Dictionary<string, string>(StringComparer.Ordinal);
        if (creatureSize is not null)
        {
            stringInputs["creatureSize"] = creatureSize;
        }

        var contributorGroups =
            new Dictionary<string, IReadOnlyList<CharacterMechanicContributorInputValues>>(
                StringComparer.OrdinalIgnoreCase);
        if (helpers.Count > 0)
        {
            contributorGroups["helpers"] = helpers
                .Select(helper => new CharacterMechanicContributorInputValues(
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        ["proficiencyBonus"] = helper.ProficiencyBonus
                    },
                    new Dictionary<string, bool>(StringComparer.Ordinal)
                    {
                        ["isProficient"] = helper.IsProficient,
                        ["participatedForEntireDuration"] = helper.ParticipatedForEntireDuration,
                        ["isAssessmentParticipant"] = helper.IsAssessmentParticipant,
                        ["isCarvingParticipant"] = helper.IsCarvingParticipant
                    },
                    new Dictionary<string, string>(StringComparer.Ordinal)))
                .ToArray();
        }

        var evaluation = CharacterMechanicEvaluator.Evaluate(
            totalDefinition,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["assessmentResult"] = request.AssessmentResult,
                ["carvingResult"] = request.CarvingResult
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["sameActor"] = request.SameActor
            },
            stringInputs,
            contributorGroups);

        var helperEvaluation = evaluation.ContributorGroups
            .Single(value => string.Equals(value.Key, "helpers", StringComparison.OrdinalIgnoreCase));

        var cumulativeDc = 0;
        var outcomes = new List<HarvestingComponentOutcomeView>(ordered.Count);
        foreach (var component in ordered)
        {
            cumulativeDc = checked(cumulativeDc + component.ComponentDc);
            outcomes.Add(new HarvestingComponentOutcomeView(
                component.Key,
                component.DisplayName,
                component.ComponentDc,
                cumulativeDc,
                component.Quantity,
                component.Origin,
                evaluation.Value >= cumulativeDc));
        }

        var rollMode = request.SameActor
            ? CharacterMechanicRollModes.Disadvantage
            : CharacterMechanicRollModes.Normal;
        return new HarvestingOutcomeView(
            table,
            rollMode,
            rollMode,
            creatureSize,
            request.AssessmentResult,
            request.CarvingResult,
            helperEvaluation.ContributorCount,
            helperEvaluation.Value,
            evaluation.Value,
            outcomes);
    }

    private static string? ResolveCreatureSize(
        string? tableSize,
        string? suppliedSize,
        int helperCount)
    {
        var resolvedTableSize = NormalizeKnownSize(tableSize, "resolved creature");
        var resolvedSuppliedSize = NormalizeKnownSize(suppliedSize, "supplied creature");

        if (resolvedTableSize is not null
            && resolvedSuppliedSize is not null
            && !string.Equals(
                resolvedTableSize,
                resolvedSuppliedSize,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Supplied creature size '{resolvedSuppliedSize}' conflicts with resolved creature size '{resolvedTableSize}'.");
        }

        var effective = resolvedTableSize ?? resolvedSuppliedSize;
        if (helperCount > 0 && effective is null)
        {
            throw new ArgumentException(
                "Creature size is required when Harvesting helpers are supplied.");
        }

        return effective;
    }

    private static string? NormalizeKnownSize(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        if (!UniversalSizeCategories.TryResolve(value, out var size)
            || !KnownHarvestingRules.HelperLimitsByCreatureSize.ContainsKey(size.DisplayName))
        {
            throw new ArgumentException(
                $"{label} size '{value}' is not supported by the Harvesting helper rules.");
        }
        return size.DisplayName;
    }
}
