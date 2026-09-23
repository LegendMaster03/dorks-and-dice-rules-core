using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Resolves universal competency state, compatibility keys, ranks, training, class-skill state, and composite relationships.
/// </summary>
internal static class CharacterCompetencyResolver
{
    internal static void Resolve(
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
    
    
}
