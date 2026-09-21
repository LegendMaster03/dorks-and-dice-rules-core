using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

internal static class NormalizedCharacterPrerequisiteProjector
{
    public static void Project(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement character)
    {
        if (!context.IsSelected(rule.Catalog.ConceptKey)
            || !CharacterProjectionJson.TryGetProperty(character, "prerequisites", out var prerequisites)
            || prerequisites.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var requirements = new List<CharacterPrerequisiteRequirementView>();
        var groupOrdinal = 0;
        foreach (var group in prerequisites.EnumerateArray())
        {
            if (group.ValueKind != JsonValueKind.Object
                || !CharacterProjectionJson.TryGetProperty(group, "requirements", out var groupRequirements)
                || groupRequirements.ValueKind != JsonValueKind.Array)
            {
                groupOrdinal++;
                continue;
            }

            var groupKey = CharacterProjectionJson.String(group, "key")
                ?? $"{rule.Catalog.ConceptKey}.prerequisite-group.{groupOrdinal}";
            var matchCount = CharacterProjectionJson.Integer(group, "matchCount")
                ?? groupRequirements.GetArrayLength();
            if (matchCount < 1)
            {
                groupOrdinal++;
                continue;
            }

            var itemOrdinal = 0;
            foreach (var requirement in groupRequirements.EnumerateArray())
            {
                if (requirement.ValueKind != JsonValueKind.Object)
                {
                    itemOrdinal++;
                    continue;
                }

                requirements.Add(new CharacterPrerequisiteRequirementView(
                    $"{rule.Catalog.ConceptKey}.prerequisite.{groupOrdinal}.{itemOrdinal++}",
                    CharacterProjectionJson.String(requirement, "kind") ?? "source-defined",
                    CharacterProjectionJson.String(requirement, "targetKey"),
                    CharacterProjectionJson.String(requirement, "operator"),
                    CharacterProjectionJson.Integer(requirement, "value"),
                    CharacterProjectionJson.String(requirement, "targetName"),
                    null,
                    CharacterResolutionStates.ApplicableUnresolved,
                    "Normalized source prerequisite has not yet been evaluated against Character state.",
                    groupKey,
                    matchCount));
            }
            groupOrdinal++;
        }

        if (requirements.Count == 0)
        {
            return;
        }

        context.Prerequisites[rule.Catalog.ConceptKey] = new CharacterPrerequisiteView(
            rule.Catalog.ConceptKey,
            CharacterResolutionStates.ApplicableUnresolved,
            null,
            requirements,
            rule.Provenance);
    }
}
