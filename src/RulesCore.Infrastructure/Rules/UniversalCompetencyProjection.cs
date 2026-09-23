using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules;

internal static class UniversalCompetencyProjection
{
    public static List<CharacterMechanicView> ApplySemanticIdentity(
        List<CharacterMechanicView> mechanics)
    {
        for (var index = 0; index < mechanics.Count; index++)
        {
            var mechanic = mechanics[index];
            if (mechanic.Competency is null)
            {
                continue;
            }

            var identity = ResolveIdentity(mechanic);
            var profiles = mechanic.Competency.Profiles
                .Select(profile => profile with
                {
                    FamilyName = identity.FamilyName,
                    Specialty = identity.IsFamily || identity.FamilyName is null
                        ? null
                        : identity.DisplayName,
                    IsFamily = identity.IsFamily,
                    IdentityKey = identity.IdentityKey,
                    IdentityName = identity.DisplayName,
                    SharedTrainingKey = identity.TrainingStateKey
                })
                .ToArray();

            mechanics[index] = mechanic with
            {
                Competency = mechanic.Competency with
                {
                    FamilyName = identity.FamilyName,
                    Specialty = identity.IsFamily || identity.FamilyName is null
                        ? null
                        : identity.DisplayName,
                    IdentityKey = identity.IdentityKey,
                    IdentityName = identity.DisplayName,
                    SharedTrainingKey = identity.TrainingStateKey,
                    IsFamily = identity.IsFamily,
                    Profiles = profiles
                }
            };
        }

        return mechanics;
    }

    public static IReadOnlyList<CharacterUniversalCompetencyView> BuildCatalog(
        IReadOnlyList<CharacterMechanicView> mechanics)
    {
        var result = new List<CharacterUniversalCompetencyView>();
        var groups = mechanics
            .Where(value => value.Competency is not null
                && !string.IsNullOrWhiteSpace(value.Competency.IdentityKey))
            .GroupBy(
                value => value.Competency!.IdentityKey!,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var group in groups)
        {
            var members = group.ToArray();
            var identityKey = KnownUniversalCompetencies.NormalizeIdentityKey(group.Key);
            var known = KnownUniversalCompetencies.FindByIdentityKey(identityKey);
            var displayName = known?.DisplayName
                ?? members.Select(value => value.Competency!.IdentityName)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                ?? members[0].DisplayName;
            var familyName = known?.FamilyName
                ?? SingleDistinct(members.Select(value => value.Competency!.FamilyName));
            var isFamily = known?.IsFamily == true
                || members.Any(value => value.Competency!.IsFamily);
            var trainingStateKey = isFamily
                ? null
                : members.Select(value => value.Competency!.SharedTrainingKey)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                    ?? $"competency.{identityKey}.training";

            var profiles = members
                .SelectMany(value => value.Competency!.Profiles)
                .GroupBy(value => new { value.SourceEntityRevisionId, value.ProfileKey })
                .Select(value => value.First())
                .OrderBy(value => value.ProfileKey, StringComparer.Ordinal)
                .ThenBy(value => value.GameEdition, StringComparer.Ordinal)
                .ThenBy(value => value.SourceEntityRevisionId)
                .ToArray();

            var facets = members
                .SelectMany(value => value.Competency!.Facets ?? [])
                .GroupBy(value => value.FacetType, StringComparer.OrdinalIgnoreCase)
                .Select(group => new CharacterCompetencyFacetView(
                    group.Key,
                    group.SelectMany(value => value.ProfileSourceEntityRevisionIds)
                        .Distinct()
                        .OrderBy(value => value)
                        .ToArray(),
                    group.Any(value => value.SupportsRanks),
                    group.Any(value => value.SupportsClassSkillState),
                    group.Any(value => value.SupportsTrainingState),
                    group.SelectMany(value => value.MechanicKeys ?? [])
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray()))
                .OrderBy(value => value.FacetType, StringComparer.Ordinal)
                .ToArray();

            var relationships = members
                .SelectMany(value => value.Competency!.RelatedCompetencies ?? [])
                .Distinct()
                .OrderBy(value => value.Kind, StringComparer.Ordinal)
                .ThenBy(value => value.TargetType, StringComparer.Ordinal)
                .ThenBy(value => value.TargetName, StringComparer.Ordinal)
                .ThenBy(value => value.Scope, StringComparer.Ordinal)
                .ToArray();

            var attributions = members
                .SelectMany(value => value.SourceAttributions)
                .Distinct()
                .OrderBy(value => value.WorkDisplayName, StringComparer.Ordinal)
                .ThenBy(value => value.PackageKey, StringComparer.Ordinal)
                .ThenBy(value => value.SourceRevisionNumber)
                .ToArray();

            result.Add(new CharacterUniversalCompetencyView(
                SemanticKey: $"competency.{identityKey}",
                IdentityKey: identityKey,
                DisplayName: displayName,
                FamilyName: familyName,
                IsFamily: isFamily,
                TrainingStateKey: trainingStateKey,
                ChildCompetencyKeys: ChildKeys(identityKey, isFamily),
                MechanicKeys: members.Select(value => value.MechanicKey)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                CompatibilityMechanicKeys:
                    KnownUniversalCompetencies.CompatibilityConceptKeys(identityKey)
                        .Select(value => $"competency.{value}")
                        .Where(value => !members.Any(member =>
                            string.Equals(
                                member.MechanicKey,
                                value,
                                StringComparison.OrdinalIgnoreCase)))
                        .ToArray(),
                SourceAliases: known?.SourceAliases ?? [],
                Profiles: profiles,
                Facets: facets,
                RelatedCompetencies: relationships,
                SourceAttributions: attributions));
        }

        var existing = result
            .Select(value => value.IdentityKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in KnownUniversalCompetencies.Catalog)
        {
            if (!existing.Add(definition.IdentityKey))
            {
                continue;
            }

            result.Add(new CharacterUniversalCompetencyView(
                definition.SemanticKey,
                definition.IdentityKey,
                definition.DisplayName,
                definition.FamilyName,
                definition.IsFamily,
                definition.TrainingStateKey,
                ChildKeys(definition.IdentityKey, definition.IsFamily),
                MechanicKeys: [],
                CompatibilityMechanicKeys:
                    KnownUniversalCompetencies.CompatibilityConceptKeys(definition.IdentityKey)
                        .Select(value => $"competency.{value}")
                        .ToArray(),
                SourceAliases: definition.SourceAliases ?? [],
                Profiles: [],
                Facets: [],
                RelatedCompetencies: [],
                SourceAttributions: []));
        }

        return result
            .OrderByDescending(value => value.IsFamily)
            .ThenBy(value => value.FamilyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.SemanticKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static ResolvedIdentity ResolveIdentity(CharacterMechanicView mechanic)
    {
        var competency = mechanic.Competency
            ?? throw new InvalidOperationException("Universal competency identity requires competency metadata.");

        if (!string.IsNullOrWhiteSpace(competency.IdentityKey))
        {
            var normalized = KnownUniversalCompetencies.NormalizeIdentityKey(competency.IdentityKey);
            var known = KnownUniversalCompetencies.FindByIdentityKey(normalized);
            return FromKnownOrValues(
                known,
                normalized,
                competency.IdentityName ?? mechanic.DisplayName,
                competency.FamilyName,
                competency.IsFamily,
                competency.SharedTrainingKey);
        }

        var legacy = KnownUniversalCompetencies.ResolveLegacyConceptKey(mechanic.ConceptKey);
        if (legacy is not null)
        {
            var known = KnownUniversalCompetencies.FindByIdentityKey(legacy.IdentityKey);
            return FromKnownOrValues(
                known,
                legacy.IdentityKey,
                legacy.DisplayName,
                competency.FamilyName,
                competency.IsFamily,
                competency.SharedTrainingKey);
        }

        if (!string.IsNullOrWhiteSpace(competency.FamilyName)
            && !string.IsNullOrWhiteSpace(competency.Specialty))
        {
            var known = KnownUniversalCompetencies.ResolveFamilyMember(
                competency.FamilyName,
                competency.Specialty);
            if (known is not null)
            {
                return FromKnownOrValues(
                    known,
                    known.IdentityKey,
                    known.DisplayName,
                    known.FamilyName,
                    known.IsFamily,
                    competency.SharedTrainingKey);
            }

            var specialtyIdentity =
                KnownUniversalCompetencies.NormalizeIdentityKey(competency.Specialty);
            return FromKnownOrValues(
                known: null,
                specialtyIdentity,
                competency.Specialty,
                competency.FamilyName,
                isFamily: false,
                competency.SharedTrainingKey);
        }

        var derivedIdentity = DeriveIdentityKey(mechanic);
        var definition = KnownUniversalCompetencies.FindByIdentityKey(derivedIdentity);
        return FromKnownOrValues(
            definition,
            derivedIdentity,
            competency.IdentityName ?? mechanic.DisplayName,
            competency.FamilyName,
            competency.IsFamily,
            competency.SharedTrainingKey);
    }

    private static ResolvedIdentity FromKnownOrValues(
        UniversalCompetencyDefinition? known,
        string identityKey,
        string displayName,
        string? familyName,
        bool isFamily,
        string? trainingStateKey)
    {
        var normalized = KnownUniversalCompetencies.NormalizeIdentityKey(
            known?.IdentityKey ?? identityKey);
        var resolvedIsFamily = known?.IsFamily ?? isFamily;
        return new ResolvedIdentity(
            normalized,
            known?.DisplayName ?? displayName.Trim(),
            known?.FamilyName ?? familyName,
            resolvedIsFamily,
            resolvedIsFamily
                ? null
                : trainingStateKey ?? $"competency.{normalized}.training");
    }

    private static string DeriveIdentityKey(CharacterMechanicView mechanic)
    {
        var conceptKey = mechanic.ConceptKey?.Trim();
        if (!string.IsNullOrWhiteSpace(conceptKey))
        {
            foreach (var prefix in new[] { "skill.", "tool." })
            {
                if (conceptKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && conceptKey.Length > prefix.Length)
                {
                    return KnownUniversalCompetencies.NormalizeIdentityKey(
                        conceptKey[prefix.Length..]);
                }
            }
        }

        return KnownUniversalCompetencies.NormalizeIdentityKey(mechanic.DisplayName);
    }

    private static IReadOnlyList<string> ChildKeys(string identityKey, bool isFamily)
    {
        if (!isFamily)
        {
            return [];
        }

        var family = KnownUniversalCompetencies.FindByIdentityKey(identityKey);
        if (family is null)
        {
            return [];
        }

        return KnownUniversalCompetencies.FamilyMembers
            .Where(value => string.Equals(
                value.FamilyName,
                family.DisplayName,
                StringComparison.OrdinalIgnoreCase))
            .Select(value => value.SemanticKey)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string? SingleDistinct(IEnumerable<string?> values)
    {
        var distinct = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return distinct.Length == 1 ? distinct[0] : null;
    }

    private sealed record ResolvedIdentity(
        string IdentityKey,
        string DisplayName,
        string? FamilyName,
        bool IsFamily,
        string? TrainingStateKey);
}
