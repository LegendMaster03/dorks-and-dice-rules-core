using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Evaluates projected prerequisite requirements against the effective Character state.
/// </summary>
internal static class CharacterPrerequisiteResolver
{
    internal static void Resolve(CharacterProjectionContext context)
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
                    || !CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, requirement.TargetKey, out actual))
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
    
}
