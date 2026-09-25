using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules;

internal static class UniversalCompetencyProjection
{
    public static List<CharacterMechanicView> ApplySemanticIdentity(
        List<CharacterMechanicView> mechanics)
    {
        mechanics.RemoveAll(value =>
        {
            if (value.Competency is null)
            {
                return false;
            }

            var identity = ResolveIdentity(value);
            return !KnownUniversalCompetencies.IsOrdinaryCharacterCompetencyIdentity(
                identity.IdentityKey);
        });

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
                    GoverningAbilityKey =
                        KnownUniversalCompetencies.NormalizeAbilityKey(
                            profile.GoverningAbilityKey),
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
                    GoverningAbilityKey =
                        KnownUniversalCompetencies.NormalizeAbilityKey(
                            mechanic.Competency.GoverningAbilityKey),
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

        AddRulesLayerFamilyMemberMechanics(mechanics);
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
                SourceAliases: KnownUniversalCompetencies.SourceAliases(identityKey),
                Profiles: profiles,
                Facets: facets,
                RelatedCompetencies: relationships,
                SourceAttributions: attributions,
                Mechanics: BuildUniversalMechanics(identityKey, known, profiles)));
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

            var normalizedMechanics = definition.IsFamily
                ? null
                : KnownUniversalCompetencies.ResolveMechanics(definition.IdentityKey);
            var normalizedProfile = normalizedMechanics is null
                ? null
                : BuildRulesLayerProfile(definition, normalizedMechanics);
            var normalizedFacet = normalizedMechanics is null
                ? null
                : BuildRulesLayerFacet(definition, normalizedMechanics);

            result.Add(new CharacterUniversalCompetencyView(
                definition.SemanticKey,
                definition.IdentityKey,
                definition.DisplayName,
                definition.FamilyName,
                definition.IsFamily,
                definition.TrainingStateKey,
                ChildKeys(definition.IdentityKey, definition.IsFamily),
                MechanicKeys: normalizedMechanics is null
                    ? []
                    : [definition.SemanticKey],
                CompatibilityMechanicKeys:
                    KnownUniversalCompetencies.CompatibilityConceptKeys(definition.IdentityKey)
                        .Select(value => $"competency.{value}")
                        .ToArray(),
                SourceAliases: KnownUniversalCompetencies.SourceAliases(definition.IdentityKey),
                Profiles: normalizedProfile is null ? [] : [normalizedProfile],
                Facets: normalizedFacet is null ? [] : [normalizedFacet],
                RelatedCompetencies: [],
                SourceAttributions: [],
                Mechanics: BuildUniversalMechanics(
                    definition.IdentityKey,
                    definition,
                    normalizedProfile is null
                        ? []
                        : [normalizedProfile])));
        }

        return result
            .OrderByDescending(value => value.IsFamily)
            .ThenBy(value => value.FamilyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.SemanticKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddRulesLayerFamilyMemberMechanics(
        List<CharacterMechanicView> mechanics)
    {
        var existingIdentities = mechanics
            .Where(value => value.Competency is not null
                && !string.IsNullOrWhiteSpace(value.Competency.IdentityKey))
            .Select(value =>
                KnownUniversalCompetencies.NormalizeIdentityKey(
                    value.Competency!.IdentityKey!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in KnownUniversalCompetencies.FamilyMembers)
        {
            if (existingIdentities.Contains(definition.IdentityKey))
            {
                continue;
            }

            var normalizedMechanics =
                KnownUniversalCompetencies.ResolveMechanics(definition.IdentityKey);
            if (normalizedMechanics is null)
            {
                continue;
            }

            mechanics.Add(BuildRulesLayerMechanic(definition, normalizedMechanics));
            existingIdentities.Add(definition.IdentityKey);
        }
    }

    private static CharacterMechanicView BuildRulesLayerMechanic(
        UniversalCompetencyDefinition definition,
        UniversalCompetencyMechanics mechanics)
    {
        var profile = BuildRulesLayerProfile(definition, mechanics);
        var facet = BuildRulesLayerFacet(definition, mechanics);
        var competency = new CharacterCompetencyDefinitionView(
            mechanics.CompetencyKind,
            definition.FamilyName,
            definition.DisplayName,
            mechanics.GoverningAbilityKey,
            mechanics.SupportsRanks,
            mechanics.SupportsClassSkillState,
            mechanics.SupportsTrainingState,
            mechanics.TrainedOnly,
            mechanics.ArmorCheckPenaltyApplies,
            DefaultProfileSourceEntityRevisionId: Guid.Empty,
            Profiles: [profile],
            IdentityKey: definition.IdentityKey,
            IdentityName: definition.DisplayName,
            SharedTrainingKey: definition.TrainingStateKey,
            IsFamily: false,
            Facets: [facet],
            RelatedCompetencies: []);

        return new CharacterMechanicView(
            definition.SemanticKey,
            CharacterMechanicKinds.Competency,
            definition.DisplayName,
            ConceptKey: definition.SemanticKey,
            RuleConceptId: null,
            IsAvailableUnderRuleset: true,
            new CharacterMechanicApplicabilityView(
                CharacterMechanicApplicabilityKinds.Always,
                RequiresCharacterState: true,
                RequiredCapabilityKeys: [],
                SourcePackageKey: null),
            mechanics.EvaluationKind,
            mechanics.CanEvaluate,
            Constant: 0,
            TargetInputKey: null,
            BaseMechanicKey: null,
            Inputs: profile.Inputs,
            Relationships: [],
            ConditionalRollRules: [],
            BooleanRequirements: [],
            Check: null,
            Competency: competency,
            ContributorGroups: [],
            SourceAttributions: [],
            Provenance: null);
    }

    private static CharacterCompetencyProfileView BuildRulesLayerProfile(
        UniversalCompetencyDefinition definition,
        UniversalCompetencyMechanics mechanics)
    {
        var inputs = BuildRulesLayerInputs(mechanics);
        IReadOnlyList<CharacterMechanicBooleanRequirementView> requirements =
            mechanics.TrainedOnly
                ? [new CharacterMechanicBooleanRequirementView("isTrained", true)]
                : [];

        return new CharacterCompetencyProfileView(
            Guid.Empty,
            $"rules-universal-{definition.FamilyName?.ToLowerInvariant() ?? "competency"}",
            RequiredCapabilityKeys: [],
            mechanics.CompetencyKind,
            definition.FamilyName,
            definition.DisplayName,
            mechanics.GoverningAbilityKey,
            mechanics.SupportsRanks,
            mechanics.SupportsClassSkillState,
            mechanics.SupportsTrainingState,
            mechanics.TrainedOnly,
            mechanics.ArmorCheckPenaltyApplies,
            mechanics.EvaluationProfileKey,
            mechanics.EvaluationKind,
            mechanics.CanEvaluate,
            inputs,
            requirements,
            GameEdition: null,
            SourceAttributions: [],
            FacetType: mechanics.FacetType,
            IsFamily: false,
            IdentityKey: definition.IdentityKey,
            IdentityName: definition.DisplayName,
            SharedTrainingKey: definition.TrainingStateKey,
            RelatedCompetencies: [],
            ProfileOrigin: "rules");
    }

    private static CharacterCompetencyFacetView BuildRulesLayerFacet(
        UniversalCompetencyDefinition definition,
        UniversalCompetencyMechanics mechanics) =>
        new(
            mechanics.FacetType,
            ProfileSourceEntityRevisionIds: [],
            mechanics.SupportsRanks,
            mechanics.SupportsClassSkillState,
            mechanics.SupportsTrainingState,
            MechanicKeys: [definition.SemanticKey]);

    private static IReadOnlyList<CharacterMechanicInputView> BuildRulesLayerInputs(
        UniversalCompetencyMechanics mechanics)
    {
        if (!mechanics.CanEvaluate
            || !string.Equals(
                mechanics.EvaluationProfileKey,
                "ranked-skill",
                StringComparison.Ordinal))
        {
            return [];
        }

        var inputs = new List<CharacterMechanicInputView>
        {
            ContributionInput(
                "abilityContribution",
                CharacterMechanicInputOrigins.Derived,
                required: true,
                contributionRole: "ability"),
            ContributionInput(
                "ranks",
                CharacterMechanicInputOrigins.CharacterState,
                required: true,
                contributionRole: "competency")
        };

        if (mechanics.SupportsClassSkillState)
        {
            inputs.Add(StateInput("classSkillState"));
        }
        if (mechanics.SupportsTrainingState)
        {
            inputs.Add(StateInput("isTrained"));
        }
        if (mechanics.ArmorCheckPenaltyApplies)
        {
            inputs.Add(ContributionInput(
                "armorCheckPenaltyAdjustment",
                CharacterMechanicInputOrigins.Derived,
                required: false,
                defaultInteger: 0,
                contributionRole: "competency"));
        }

        inputs.Add(ContributionInput(
            "otherModifier",
            CharacterMechanicInputOrigins.Derived,
            required: false,
            defaultInteger: 0,
            contributionRole: "competency"));
        return inputs;
    }

    private static CharacterMechanicInputView StateInput(string key) =>
        new(
            key,
            CharacterMechanicInputValueKinds.Boolean,
            CharacterMechanicInputOrigins.CharacterState,
            Required: false,
            ParticipatesInValue: false,
            DefaultInteger: null,
            IncludeWhenBooleanInputKey: null,
            IncludeWhenBooleanValue: null);

    private static CharacterMechanicInputView ContributionInput(
        string key,
        string origin,
        bool required,
        int? defaultInteger = null,
        string? contributionRole = null) =>
        new(
            key,
            CharacterMechanicInputValueKinds.Integer,
            origin,
            required,
            ParticipatesInValue: true,
            defaultInteger,
            IncludeWhenBooleanInputKey: null,
            IncludeWhenBooleanValue: null,
            ContributionRole: contributionRole);

    private static CharacterUniversalCompetencyMechanicsView? BuildUniversalMechanics(
        string identityKey,
        UniversalCompetencyDefinition? known,
        IReadOnlyList<CharacterCompetencyProfileView> profiles)
    {
        if (known?.IsFamily == true)
        {
            return null;
        }

        var defaults = KnownUniversalCompetencies.ResolveMechanics(identityKey);
        var defaultAbilities = defaults is null
            ? Enumerable.Empty<string>()
            : new[] { defaults.GoverningAbilityKey };
        var abilityKeys = profiles
            .Select(value =>
                KnownUniversalCompetencies.NormalizeAbilityKey(
                    value.GoverningAbilityKey))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Concat(defaultAbilities)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        var governingAbility = new CharacterUniversalGoverningAbilityView(
            abilityKeys.Length switch
            {
                0 => "none",
                1 => "fixed",
                _ => "varies-by-implementation"
            },
            FixedAbilityKey: abilityKeys.Length == 1 ? abilityKeys[0] : null,
            AbilityKeys: abilityKeys);

        var defaultProfileKeys = defaults is null
            ? Enumerable.Empty<string>()
            : new[] { defaults.EvaluationProfileKey };
        var evaluationProfileKeys = profiles
            .Select(value => value.EvaluationProfileKey)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Concat(defaultProfileKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        var defaultEvaluationKinds = defaults is null
            ? Enumerable.Empty<string>()
            : new[] { defaults.EvaluationKind };
        var evaluationKinds = profiles
            .Select(value => value.EvaluationKind)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Concat(defaultEvaluationKinds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        var competencyKind = defaults?.CompetencyKind
            ?? profiles
                .Select(value => value.CompetencyKind)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .SingleOrDefault()
            ?? CharacterCompetencyKinds.Skill;

        return new CharacterUniversalCompetencyMechanicsView(
            competencyKind,
            governingAbility,
            SupportsRanks:
                defaults?.SupportsRanks == true
                || profiles.Any(value => value.SupportsRanks),
            SupportsClassSkillState:
                defaults?.SupportsClassSkillState == true
                || profiles.Any(value => value.SupportsClassSkillState),
            SupportsTrainingState:
                defaults?.SupportsTrainingState == true
                || profiles.Any(value => value.SupportsTrainingState),
            TrainedOnly: ResolveBoolean(
                defaults?.TrainedOnly,
                profiles.Select(value => value.TrainedOnly)),
            ArmorCheckPenaltyApplies: ResolveBoolean(
                defaults?.ArmorCheckPenaltyApplies,
                profiles.Select(value => value.ArmorCheckPenaltyApplies)),
            EvaluationProfileKeys: evaluationProfileKeys,
            EvaluationKinds: evaluationKinds,
            CanEvaluate:
                defaults?.CanEvaluate == true
                || profiles.Any(value => value.CanEvaluate));
    }

    private static bool? ResolveBoolean(
        bool? defaultValue,
        IEnumerable<bool?> profileValues)
    {
        var defaultValues = defaultValue.HasValue
            ? new[] { defaultValue.Value }
            : Array.Empty<bool>();
        var values = profileValues
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Concat(defaultValues)
            .Distinct()
            .ToArray();
        return values.Length == 1 ? values[0] : null;
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
