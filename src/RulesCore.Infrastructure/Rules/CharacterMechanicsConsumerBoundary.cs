using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Removes source/profile alternatives from normal consumer mechanics responses. Rules Core keeps
/// the rich catalog internally and exposes it only on explicitly authorized administrative routes.
/// </summary>
public static class CharacterMechanicsConsumerBoundary
{
    public static CharacterMechanicsCatalogView ProjectEffective(
        CharacterMechanicsCatalogView detailed)
    {
        ArgumentNullException.ThrowIfNull(detailed);

        var mechanicsByKey = detailed.Mechanics.ToDictionary(
            value => value.MechanicKey,
            StringComparer.Ordinal);
        var selectedBySemanticKey = new Dictionary<string, CharacterMechanicView>(
            StringComparer.Ordinal);

        var competencies = (detailed.Competencies ?? [])
            .Select(universal =>
            {
                var candidates = universal.MechanicKeys
                    .Select(key => mechanicsByKey.GetValueOrDefault(key))
                    .Where(value => value is not null)
                    .Cast<CharacterMechanicView>()
                    .Where(value => value.Competency is not null)
                    .ToArray();
                var selected = SelectEffectiveCompetencyMechanic(universal, candidates);
                if (selected is not null)
                {
                    selectedBySemanticKey[universal.SemanticKey] = selected;
                }

                return SanitizeUniversal(universal, selected);
            })
            .ToArray();

        var selectedCompetencyKeys = selectedBySemanticKey.Values
            .Select(value => value.MechanicKey)
            .ToHashSet(StringComparer.Ordinal);

        var mechanics = detailed.Mechanics
            .Where(value => value.Competency is null
                || selectedCompetencyKeys.Contains(value.MechanicKey))
            .Select(value => SanitizeMechanic(value, selectedCompetencyKeys))
            .OrderBy(value => value.Kind, StringComparer.Ordinal)
            .ThenBy(value => value.DisplayName, StringComparer.Ordinal)
            .ThenBy(value => value.MechanicKey, StringComparer.Ordinal)
            .ToArray();

        return detailed with
        {
            Mechanics = mechanics,
            Competencies = competencies
        };
    }

    public static CharacterMechanicEvaluationRequest SanitizeEvaluationRequest(
        CharacterMechanicEvaluationRequest request,
        IReadOnlySet<string> allowedMechanicKeys)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(allowedMechanicKeys);
        return request with
        {
            CompetencyProfileSourceEntityRevisionId = null,
            Competency = SanitizeCompetencyInput(request.Competency, allowedMechanicKeys)
        };
    }

    public static void RequireEffectiveMechanic(
        CharacterMechanicsCatalogView effectiveCatalog,
        string mechanicKey)
    {
        if (!effectiveCatalog.Mechanics.Any(value =>
                string.Equals(value.MechanicKey, mechanicKey, StringComparison.Ordinal)))
        {
            throw new KeyNotFoundException(
                $"Mechanic '{mechanicKey}' is not an effective consumer mechanic.");
        }
    }

    private static CharacterMechanicCompetencyInput? SanitizeCompetencyInput(
        CharacterMechanicCompetencyInput? input,
        IReadOnlySet<string> allowedMechanicKeys)
    {
        if (input is null)
        {
            return null;
        }

        if (!allowedMechanicKeys.Contains(input.MechanicKey))
        {
            throw new KeyNotFoundException(
                $"Mechanic '{input.MechanicKey}' is not an effective consumer mechanic.");
        }

        return input with
        {
            CompetencyProfileSourceEntityRevisionId = null,
            Components = input.Components?
                .Select(value => SanitizeCompetencyInput(value, allowedMechanicKeys)!)
                .ToArray()
        };
    }

    private static CharacterMechanicView? SelectEffectiveCompetencyMechanic(
        CharacterUniversalCompetencyView universal,
        IReadOnlyList<CharacterMechanicView> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var semantic = candidates.FirstOrDefault(value =>
            string.Equals(value.MechanicKey, universal.SemanticKey, StringComparison.Ordinal));
        if (semantic is not null)
        {
            return semantic;
        }

        return candidates
            .OrderByDescending(value => LatestPublicationDate(value.SourceAttributions))
            .ThenByDescending(value => LatestRevisionNumber(value.SourceAttributions))
            .ThenBy(value => value.MechanicKey, StringComparer.Ordinal)
            .First();
    }

    private static CharacterUniversalCompetencyView SanitizeUniversal(
        CharacterUniversalCompetencyView universal,
        CharacterMechanicView? selected)
    {
        if (selected?.Competency is not CharacterCompetencyDefinitionView competency)
        {
            return universal with
            {
                MechanicKeys = [],
                CompatibilityMechanicKeys = [],
                SourceAliases = [],
                Profiles = [],
                Facets = [],
                SourceAttributions = [],
                Resolution = null
            };
        }

        var effective = EffectiveCompetencyValues(universal.IdentityKey, competency);
        var mechanics = new CharacterUniversalCompetencyMechanicsView(
            effective.CompetencyKind,
            new CharacterUniversalGoverningAbilityView(
                string.IsNullOrWhiteSpace(effective.GoverningAbilityKey) ? "none" : "fixed",
                effective.GoverningAbilityKey,
                string.IsNullOrWhiteSpace(effective.GoverningAbilityKey)
                    ? []
                    : [effective.GoverningAbilityKey!]),
            effective.SupportsRanks,
            effective.SupportsClassSkillState,
            effective.SupportsTrainingState,
            effective.TrainedOnly,
            effective.ArmorCheckPenaltyApplies,
            string.IsNullOrWhiteSpace(effective.EvaluationProfileKey)
                ? []
                : [effective.EvaluationProfileKey!],
            string.IsNullOrWhiteSpace(effective.EvaluationKind)
                ? []
                : [effective.EvaluationKind!],
            effective.CanEvaluate);

        return universal with
        {
            MechanicKeys = [selected.MechanicKey],
            CompatibilityMechanicKeys = [],
            SourceAliases = [],
            Profiles = [],
            Facets = [],
            SourceAttributions = selected.SourceAttributions,
            Mechanics = mechanics,
            Resolution = selected.Resolution
        };
    }

    private static CharacterMechanicView SanitizeMechanic(
        CharacterMechanicView mechanic,
        IReadOnlySet<string> selectedCompetencyKeys)
    {
        var relationships = mechanic.Relationships
            .Where(value =>
                (!IsCompetencyKey(value.ParentMechanicKey)
                    || selectedCompetencyKeys.Contains(value.ParentMechanicKey))
                && value.ComponentMechanicKeys.All(key =>
                    !IsCompetencyKey(key) || selectedCompetencyKeys.Contains(key)))
            .ToArray();

        if (mechanic.Competency is not CharacterCompetencyDefinitionView competency)
        {
            return mechanic with { Relationships = relationships };
        }

        var identityKey = competency.IdentityKey
            ?? mechanic.ConceptKey
            ?? mechanic.MechanicKey;
        var effective = EffectiveCompetencyValues(identityKey, competency);
        var safeCompetency = competency with
        {
            CompetencyKind = effective.CompetencyKind,
            GoverningAbilityKey = effective.GoverningAbilityKey,
            SupportsRanks = effective.SupportsRanks,
            SupportsClassSkillState = effective.SupportsClassSkillState,
            SupportsTrainingState = effective.SupportsTrainingState,
            TrainedOnly = effective.TrainedOnly,
            ArmorCheckPenaltyApplies = effective.ArmorCheckPenaltyApplies,
            DefaultProfileSourceEntityRevisionId = null,
            Profiles = [],
            Facets = [],
            RelatedCompetencies = competency.RelatedCompetencies ?? []
        };

        return mechanic with
        {
            Relationships = relationships,
            Competency = safeCompetency
        };
    }

    private static EffectiveCompetencyValuesView EffectiveCompetencyValues(
        string identityKey,
        CharacterCompetencyDefinitionView competency)
    {
        var known = KnownUniversalCompetencies.ResolveMechanics(identityKey);
        var profile = competency.DefaultProfileSourceEntityRevisionId is Guid defaultId
            ? competency.Profiles.FirstOrDefault(value => value.SourceEntityRevisionId == defaultId)
            : null;
        profile ??= competency.Profiles
            .OrderByDescending(value => LatestPublicationDate(value.SourceAttributions ?? []))
            .ThenByDescending(value => LatestRevisionNumber(value.SourceAttributions ?? []))
            .FirstOrDefault();

        return new EffectiveCompetencyValuesView(
            known?.CompetencyKind ?? profile?.CompetencyKind ?? competency.CompetencyKind,
            KnownUniversalCompetencies.NormalizeAbilityKey(
                known?.GoverningAbilityKey
                    ?? profile?.GoverningAbilityKey
                    ?? competency.GoverningAbilityKey),
            known?.SupportsRanks ?? profile?.SupportsRanks ?? competency.SupportsRanks,
            known?.SupportsClassSkillState
                ?? profile?.SupportsClassSkillState
                ?? competency.SupportsClassSkillState,
            known?.SupportsTrainingState
                ?? profile?.SupportsTrainingState
                ?? competency.SupportsTrainingState,
            known?.TrainedOnly ?? profile?.TrainedOnly ?? competency.TrainedOnly,
            known?.ArmorCheckPenaltyApplies
                ?? profile?.ArmorCheckPenaltyApplies
                ?? competency.ArmorCheckPenaltyApplies,
            known?.EvaluationProfileKey ?? profile?.EvaluationProfileKey,
            known?.EvaluationKind ?? profile?.EvaluationKind,
            known?.CanEvaluate ?? profile?.CanEvaluate ?? false);
    }

    private static DateOnly LatestPublicationDate(
        IReadOnlyList<CharacterMechanicSourceAttributionView> values) =>
        values
            .Where(value => value.PublicationDate.HasValue)
            .Select(value => value.PublicationDate!.Value)
            .DefaultIfEmpty(DateOnly.MinValue)
            .Max();

    private static int LatestRevisionNumber(
        IReadOnlyList<CharacterMechanicSourceAttributionView> values) =>
        values
            .Where(value => value.SourceRevisionNumber.HasValue)
            .Select(value => value.SourceRevisionNumber!.Value)
            .DefaultIfEmpty(0)
            .Max();

    private static bool IsCompetencyKey(string key) =>
        key.StartsWith("competency.", StringComparison.Ordinal);

    private sealed record EffectiveCompetencyValuesView(
        string CompetencyKind,
        string? GoverningAbilityKey,
        bool SupportsRanks,
        bool SupportsClassSkillState,
        bool SupportsTrainingState,
        bool? TrainedOnly,
        bool? ArmorCheckPenaltyApplies,
        string? EvaluationProfileKey,
        string? EvaluationKind,
        bool CanEvaluate);
}
