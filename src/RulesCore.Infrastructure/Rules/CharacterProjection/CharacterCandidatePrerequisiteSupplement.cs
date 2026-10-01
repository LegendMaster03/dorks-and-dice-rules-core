using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Supplements effective candidate prerequisites with narrowly reviewed source evidence that has
/// not yet moved into the general normalized prerequisite translator. Public consumers still see
/// only the normalized Character prerequisite contract.
/// </summary>
internal static class CharacterCandidatePrerequisiteSupplement
{
    internal static CharacterPrerequisiteView? Project(
        ResolvedRuleCatalogItemView candidate,
        ResolvedRulesCatalogView catalog,
        CharacterRulesProjectionRequest character,
        CharacterPrerequisiteView? existing)
    {
        var supplemental = ProjectSimplePcGenFeatRequirements(candidate, catalog, character);
        if (supplemental.Count == 0)
        {
            return existing;
        }

        var requirements = (existing?.Requirements ?? [])
            .Concat(supplemental)
            .ToArray();
        var satisfied = ResolveGroups(requirements);
        return new CharacterPrerequisiteView(
            candidate.ConceptKey,
            satisfied.HasValue
                ? CharacterResolutionStates.Resolved
                : CharacterResolutionStates.ApplicableUnresolved,
            satisfied,
            requirements,
            existing?.Provenance ?? new CharacterMechanicProvenanceView([], [], []));
    }

    private static IReadOnlyList<CharacterPrerequisiteRequirementView> ProjectSimplePcGenFeatRequirements(
        ResolvedRuleCatalogItemView candidate,
        ResolvedRulesCatalogView catalog,
        CharacterRulesProjectionRequest character)
    {
        if (candidate.Document is not JsonElement document
            || !TryGetProperty(document, "_rulesCore", out var rulesCore)
            || !TryGetProperty(rulesCore, "pcgen", out var pcgen)
            || !TryGetProperty(pcgen, "unmappedSegments", out var unmapped)
            || unmapped.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var selected = new HashSet<string>(
            (character.SelectedConcepts ?? [])
                .Select(value => value.ConceptKey?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>(),
            StringComparer.OrdinalIgnoreCase);
        var feats = catalog.Rules
            .Where(value => string.Equals(value.EntityType, "feat", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var requirements = new List<CharacterPrerequisiteRequirementView>();
        var segmentOrdinal = 0;

        foreach (var segment in unmapped.EnumerateArray())
        {
            if (segment.ValueKind != JsonValueKind.Object
                || !TryGetProperty(segment, "tag", out var tagElement)
                || tagElement.ValueKind != JsonValueKind.String
                || !string.Equals(tagElement.GetString(), "PREFEAT", StringComparison.OrdinalIgnoreCase)
                || !TryGetProperty(segment, "value", out var valueElement)
                || valueElement.ValueKind != JsonValueKind.String)
            {
                segmentOrdinal++;
                continue;
            }

            var value = valueElement.GetString() ?? string.Empty;
            var parts = value.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2
                || !int.TryParse(parts[0], out var matchCount)
                || matchCount < 1)
            {
                segmentOrdinal++;
                continue;
            }

            var names = parts.Skip(1)
                .Select(NormalizeSimpleFeatName)
                .Where(value => value is not null)
                .Cast<string>()
                .ToArray();
            if (names.Length < matchCount)
            {
                segmentOrdinal++;
                continue;
            }

            var groupKey = $"{candidate.ConceptKey}.prerequisite.pcgen.prefeat.{segmentOrdinal}";
            for (var itemOrdinal = 0; itemOrdinal < names.Length; itemOrdinal++)
            {
                var featName = names[itemOrdinal];
                var matches = feats
                    .Where(value =>
                        string.Equals(value.ConceptKey, featName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(value.DisplayName, featName, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var resolvedFeat = matches.Length == 1 ? matches[0] : null;
                bool? satisfied = resolvedFeat is null
                    ? null
                    : selected.Contains(resolvedFeat.ConceptKey);
                var state = satisfied.HasValue
                    ? CharacterResolutionStates.Resolved
                    : CharacterResolutionStates.ApplicableUnresolved;
                var reason = resolvedFeat is null
                    ? $"Feat prerequisite '{featName}' can not be resolved to one effective feat concept."
                    : satisfied == true
                        ? $"Character has feat '{resolvedFeat.DisplayName}'."
                        : $"Character does not have feat '{resolvedFeat.DisplayName}'.";

                requirements.Add(new CharacterPrerequisiteRequirementView(
                    $"{groupKey}.{itemOrdinal}",
                    "feat",
                    resolvedFeat?.ConceptKey,
                    ">=",
                    1,
                    resolvedFeat?.DisplayName ?? featName,
                    satisfied,
                    state,
                    reason,
                    groupKey,
                    matchCount));
            }
            segmentOrdinal++;
        }

        return requirements;
    }

    private static bool? ResolveGroups(
        IReadOnlyList<CharacterPrerequisiteRequirementView> requirements)
    {
        var groups = requirements
            .GroupBy(value => value.GroupKey ?? value.RequirementKey, StringComparer.Ordinal)
            .Select(group =>
            {
                var required = Math.Max(
                    1,
                    group.Select(value => value.GroupMatchCount).DefaultIfEmpty(1).Max());
                var satisfied = group.Count(value => value.Satisfied == true);
                var unknown = group.Count(value => value.Satisfied is null);
                return satisfied >= required
                    ? (bool?)true
                    : satisfied + unknown < required
                        ? false
                        : null;
            })
            .ToArray();

        if (groups.Any(value => value == false)) return false;
        return groups.All(value => value == true) ? true : null;
    }

    private static string? NormalizeSimpleFeatName(string raw)
    {
        var value = raw.Trim();
        if (value.Length == 0
            || value.Contains('[', StringComparison.Ordinal)
            || value.Contains(']', StringComparison.Ordinal)
            || value.Contains("TYPE=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("CHECKMULT", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value;
    }

    private static bool TryGetProperty(
        JsonElement element,
        string name,
        out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out value))
        {
            return true;
        }
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }
}
