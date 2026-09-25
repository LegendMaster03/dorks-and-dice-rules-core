using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Interprets normalized/native competency documents as edition-neutral Character
/// competency definitions and source-specific evaluation profiles.
/// </summary>
internal static class CharacterCompetencyProfileFactory
{
    private const string AbilityContributionRole = "ability";
    private const string CompetencyContributionRole = "competency";

    internal static CharacterCompetencyDefinitionView BuildDefinition(
        ResolvedRuleCatalogItemView rule,
        string? selectedMechanicalJson,
        IReadOnlyList<CharacterMechanicsPublicationAttribution> selectedPublications,
        IReadOnlyList<CharacterCompetencyProfileView> profiles)
    {
        var selectedGameEdition = selectedPublications
            .Select(value => value.GameEdition)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var selectedProfile = BuildProfile(
            rule.SourceEntityRevisionId,
            rule.EntityType,
            ReadSourceNativeName(selectedMechanicalJson) ?? rule.SourceEntityName,
            selectedMechanicalJson,
            selectedGameEdition);
    
        IReadOnlyList<CharacterCompetencyProfileView> selectedProfiles =
            selectedProfile is null ? [] : [selectedProfile];
        var allProfiles = profiles
            .Concat(selectedProfiles)
            .GroupBy(value => new
            {
                value.SourceEntityRevisionId,
                value.ProfileKey
            })
            .Select(group => group.First())
            .OrderBy(value => value.ProfileKey, StringComparer.Ordinal)
            .ThenBy(value => value.GameEdition, StringComparer.Ordinal)
            .ThenBy(value => value.SourceEntityRevisionId)
            .ToArray();
    
        var defaultKind = string.Equals(rule.EntityType, "tool", StringComparison.OrdinalIgnoreCase)
            ? CharacterCompetencyKinds.Tool
            : CharacterCompetencyKinds.Skill;
    
        var governingAbilityKey = selectedProfile?.GoverningAbilityKey;
        if (string.IsNullOrWhiteSpace(governingAbilityKey))
        {
            var profileAbilities = allProfiles
                .Select(value => value.GoverningAbilityKey)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (profileAbilities.Length == 1)
            {
                governingAbilityKey = profileAbilities[0];
            }
        }
    
        var familyName = selectedProfile?.FamilyName;
        if (string.IsNullOrWhiteSpace(familyName))
        {
            var familyNames = allProfiles
                .Select(value => value.FamilyName)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (familyNames.Length == 1)
            {
                familyName = familyNames[0];
            }
        }
    
        var specialty = selectedProfile?.Specialty;
        if (string.IsNullOrWhiteSpace(specialty))
        {
            var specialties = allProfiles
                .Select(value => value.Specialty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (specialties.Length == 1)
            {
                specialty = specialties[0];
            }
        }
    
        var identityKeys = allProfiles
            .Select(value => value.IdentityKey)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var identityNames = allProfiles
            .Select(value => value.IdentityName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var sharedTrainingKeys = allProfiles
            .Select(value => value.SharedTrainingKey)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    
        var facets = allProfiles
            .GroupBy(
                value => string.IsNullOrWhiteSpace(value.FacetType)
                    ? string.Equals(
                        value.CompetencyKind,
                        CharacterCompetencyKinds.Tool,
                        StringComparison.OrdinalIgnoreCase)
                        ? "tool"
                        : "skill"
                    : value.FacetType!,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new CharacterCompetencyFacetView(
                group.Key,
                group.Select(value => value.SourceEntityRevisionId)
                    .Distinct()
                    .OrderBy(value => value)
                    .ToArray(),
                group.Any(value => value.SupportsRanks),
                group.Any(value => value.SupportsClassSkillState),
                group.Any(value => value.SupportsTrainingState)))
            .OrderBy(value => value.FacetType, StringComparer.Ordinal)
            .ToArray();
    
        var relatedCompetencies = allProfiles
            .SelectMany(value => value.RelatedCompetencies ?? [])
            .Distinct()
            .OrderBy(value => value.Kind, StringComparer.Ordinal)
            .ThenBy(value => value.TargetType, StringComparer.Ordinal)
            .ThenBy(value => value.TargetName, StringComparer.Ordinal)
            .ThenBy(value => value.Scope, StringComparer.Ordinal)
            .ToArray();
    
        return new CharacterCompetencyDefinitionView(
            selectedProfile?.CompetencyKind ?? defaultKind,
            familyName,
            specialty,
            governingAbilityKey,
            selectedProfile?.SupportsRanks ?? false,
            selectedProfile?.SupportsClassSkillState ?? false,
            selectedProfile?.SupportsTrainingState ?? false,
            selectedProfile?.TrainedOnly,
            selectedProfile?.ArmorCheckPenaltyApplies,
            selectedProfile?.SourceEntityRevisionId,
            allProfiles,
            IdentityKey: identityKeys.Length == 1 ? identityKeys[0] : null,
            IdentityName: identityNames.Length == 1 ? identityNames[0] : null,
            SharedTrainingKey:
                sharedTrainingKeys.Length == 1 ? sharedTrainingKeys[0] : null,
            IsFamily:
                selectedProfile?.IsFamily == true
                || allProfiles.Any(value => value.IsFamily),
            Facets: facets,
            RelatedCompetencies: relatedCompetencies);
    }
    
    internal static CharacterCompetencyProfileView? BuildProfile(
        Guid sourceEntityRevisionId,
        string entityType,
        string? sourceEntityName,
        string? mechanicalJson,
        string? gameEdition,
        IReadOnlyList<CharacterMechanicSourceAttributionView>? sourceAttributions = null)
    {
        var normalized = ParseNormalizedCompetencyMetadata(
            sourceEntityRevisionId,
            mechanicalJson,
            sourceAttributions);
        if (normalized is not null)
        {
            return ApplyCurrentThreeXFamilyTaxonomy(
                normalized,
                sourceEntityName,
                entityType,
                gameEdition);
        }
    
        if (!IsLaterEdition(gameEdition))
        {
            return null;
        }
    
        var competencyKind = string.Equals(entityType, "tool", StringComparison.OrdinalIgnoreCase)
            ? CharacterCompetencyKinds.Tool
            : CharacterCompetencyKinds.Skill;
        var facetIdentity = ReviewedCompetencyFacetPolicy.ResolveLaterFacet(
            entityType,
            ReadNativeName(mechanicalJson));
        var inputs = BuildProficiencyCompetencyInputs();
        return new CharacterCompetencyProfileView(
            sourceEntityRevisionId,
            "dnd-5x",
            RequiredCapabilityKeys: [],
            competencyKind,
            FamilyName: null,
            Specialty: null,
            GoverningAbilityKey: ReadNativeGoverningAbilityKey(mechanicalJson),
            SupportsRanks: false,
            SupportsClassSkillState: false,
            SupportsTrainingState: true,
            TrainedOnly: false,
            ArmorCheckPenaltyApplies: false,
            EvaluationProfileKey: "proficiency-competency",
            EvaluationKind: CharacterMechanicEvaluationKinds.Sum,
            CanEvaluate: true,
            Inputs: inputs,
            BooleanRequirements: [],
            GameEdition: gameEdition,
            SourceAttributions: sourceAttributions,
            FacetType: string.Equals(
                competencyKind,
                CharacterCompetencyKinds.Tool,
                StringComparison.OrdinalIgnoreCase)
                ? "tool"
                : "skill",
            IsFamily: false,
            IdentityKey: facetIdentity?.IdentityKey,
            IdentityName: facetIdentity?.IdentityName,
            SharedTrainingKey: facetIdentity is null
                ? null
                : $"competency.{facetIdentity.IdentityKey}.training",
            RelatedCompetencies: facetIdentity?.RelatedCompetencies?
                .Select(value => new CharacterCompetencyRelationshipView(
                    value.Kind,
                    value.TargetType,
                    value.TargetName,
                    value.Scope,
                    value.SharesTrainingState))
                .ToArray()
                ?? []);
    }
    
    private static CharacterCompetencyProfileView ApplyCurrentThreeXFamilyTaxonomy(
        CharacterCompetencyProfileView profile,
        string? sourceEntityName,
        string entityType,
        string? gameEdition)
    {
        if (string.IsNullOrWhiteSpace(sourceEntityName)
            || !(string.Equals(gameEdition, "3e", StringComparison.OrdinalIgnoreCase)
                || string.Equals(gameEdition, "3.5e", StringComparison.OrdinalIgnoreCase)))
        {
            return profile;
        }
    
        var current = RulesCoreContentTranslation.BuildThreeXCompetencyMetadata(
            sourceEntityName,
            entityType,
            gameEdition!);
        var familyName = current["familyName"]?.GetValue<string>();
        var specialty = current["specialty"]?.GetValue<string>();
        var isFamily = current["isFamily"]?.GetValue<bool>() ?? false;
        var currentKind = current["kind"]?.GetValue<string>();
        return profile with
        {
            CompetencyKind = string.IsNullOrWhiteSpace(currentKind)
                ? profile.CompetencyKind
                : currentKind,
            FamilyName = familyName,
            Specialty = specialty,
            IsFamily = isFamily
        };
    }
    
    private static CharacterCompetencyProfileView? ParseNormalizedCompetencyMetadata(
        Guid sourceEntityRevisionId,
        string? mechanicalJson,
        IReadOnlyList<CharacterMechanicSourceAttributionView>? sourceAttributions)
    {
        if (sourceEntityRevisionId == Guid.Empty || string.IsNullOrWhiteSpace(mechanicalJson))
        {
            return null;
        }
    
        try
        {
            using var document = JsonDocument.Parse(mechanicalJson);
            if (!document.RootElement.TryGetProperty("_rulesCore", out var rulesCore)
                || rulesCore.ValueKind != JsonValueKind.Object
                || !rulesCore.TryGetProperty("competency", out var competency)
                || competency.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
    
            var profileKey = ReadString(competency, "profileKey");
            var kind = ReadString(competency, "kind");
            if (string.IsNullOrWhiteSpace(profileKey) || string.IsNullOrWhiteSpace(kind))
            {
                return null;
            }
    
            var supportsRanks = ReadBoolean(competency, "supportsRanks") ?? false;
            var supportsClassSkillState = ReadBoolean(competency, "supportsClassSkillState") ?? false;
            var supportsTrainingState = ReadBoolean(competency, "supportsTrainingState") ?? false;
            var trainedOnly = ReadBoolean(competency, "trainedOnly");
            var armorCheckPenaltyApplies = ReadBoolean(competency, "armorCheckPenaltyApplies");
            var evaluationProfileKey = ReadString(competency, "evaluationProfileKey") ?? "unsupported";
            var canEvaluate = ReadBoolean(competency, "canEvaluate") ?? false;
            var inputs = canEvaluate
                && string.Equals(evaluationProfileKey, "ranked-skill", StringComparison.Ordinal)
                    ? BuildRankedCompetencyInputs(
                        supportsClassSkillState,
                        supportsTrainingState,
                        armorCheckPenaltyApplies == true)
                    : [];
            var requirements = canEvaluate && trainedOnly == true
                ? (IReadOnlyList<CharacterMechanicBooleanRequirementView>)
                    [new CharacterMechanicBooleanRequirementView("isTrained", true)]
                : [];
    
            IReadOnlyList<CharacterCompetencyRelationshipView> relationships = [];
            if (rulesCore.TryGetProperty("competencyConversion", out var conversion)
                && conversion.ValueKind == JsonValueKind.Object)
            {
                var relationshipKind = ReadString(conversion, "relationship");
                var targetType = ReadString(conversion, "targetType");
                var targetName = ReadString(conversion, "targetName");
                if (!string.IsNullOrWhiteSpace(relationshipKind)
                    && !string.IsNullOrWhiteSpace(targetType)
                    && !string.IsNullOrWhiteSpace(targetName))
                {
                    relationships =
                    [
                        new CharacterCompetencyRelationshipView(
                            relationshipKind,
                            targetType,
                            targetName,
                            ReadString(conversion, "scope"),
                            SharesTrainingState: string.Equals(
                                relationshipKind,
                                "shared-competency-facet",
                                StringComparison.Ordinal))
                    ];
                }
            }
    
            return new CharacterCompetencyProfileView(
                sourceEntityRevisionId,
                profileKey,
                ReadStringArray(competency, "requiredCapabilityKeys"),
                kind,
                ReadString(competency, "familyName"),
                ReadString(competency, "specialty"),
                ReadString(competency, "governingAbilityKey"),
                supportsRanks,
                supportsClassSkillState,
                supportsTrainingState,
                trainedOnly,
                armorCheckPenaltyApplies,
                evaluationProfileKey,
                canEvaluate ? CharacterMechanicEvaluationKinds.Sum : CharacterMechanicEvaluationKinds.None,
                canEvaluate,
                inputs,
                requirements,
                ReadString(competency, "gameEdition"),
                SourceAttributions: sourceAttributions,
                FacetType: ReadString(competency, "facetType")
                    ?? (string.Equals(
                        kind,
                        CharacterCompetencyKinds.Tool,
                        StringComparison.OrdinalIgnoreCase)
                        ? "tool"
                        : "skill"),
                IsFamily: ReadBoolean(competency, "isFamily") ?? false,
                IdentityKey: ReadString(competency, "identityKey"),
                IdentityName: ReadString(competency, "identityName"),
                SharedTrainingKey: ReadString(competency, "sharedTrainingKey"),
                RelatedCompetencies: relationships);
        }
        catch (JsonException)
        {
            return null;
        }
    }
    
    private static IReadOnlyList<CharacterMechanicInputView> BuildRankedCompetencyInputs(
        bool supportsClassSkillState,
        bool supportsTrainingState,
        bool armorCheckPenaltyApplies)
    {
        var inputs = new List<CharacterMechanicInputView>
        {
            ContributionInput(
                "abilityContribution",
                CharacterMechanicInputOrigins.Derived,
                required: true,
                contributionRole: AbilityContributionRole),
            ContributionInput(
                "ranks",
                CharacterMechanicInputOrigins.CharacterState,
                required: true,
                contributionRole: CompetencyContributionRole)
        };
        if (supportsClassSkillState)
        {
            inputs.Add(new CharacterMechanicInputView(
                "classSkillState",
                CharacterMechanicInputValueKinds.Boolean,
                CharacterMechanicInputOrigins.CharacterState,
                Required: false,
                ParticipatesInValue: false,
                DefaultInteger: null,
                IncludeWhenBooleanInputKey: null,
                IncludeWhenBooleanValue: null));
        }
        if (supportsTrainingState)
        {
            inputs.Add(new CharacterMechanicInputView(
                "isTrained",
                CharacterMechanicInputValueKinds.Boolean,
                CharacterMechanicInputOrigins.CharacterState,
                Required: false,
                ParticipatesInValue: false,
                DefaultInteger: null,
                IncludeWhenBooleanInputKey: null,
                IncludeWhenBooleanValue: null));
        }
        if (armorCheckPenaltyApplies)
        {
            inputs.Add(ContributionInput(
                "armorCheckPenaltyAdjustment",
                CharacterMechanicInputOrigins.Derived,
                required: false,
                defaultInteger: 0,
                contributionRole: CompetencyContributionRole));
        }
        inputs.Add(ContributionInput(
            "otherModifier",
            CharacterMechanicInputOrigins.Derived,
            required: false,
            defaultInteger: 0,
            contributionRole: CompetencyContributionRole));
        return inputs;
    }
    
    private static IReadOnlyList<CharacterMechanicInputView> BuildProficiencyCompetencyInputs() =>
    [
        ContributionInput(
            "abilityContribution",
            CharacterMechanicInputOrigins.Derived,
            required: true,
            contributionRole: AbilityContributionRole),
        ContributionInput(
            "trainingContribution",
            CharacterMechanicInputOrigins.Derived,
            required: false,
            defaultInteger: 0,
            contributionRole: CompetencyContributionRole),
        new CharacterMechanicInputView(
            "isTrained",
            CharacterMechanicInputValueKinds.Boolean,
            CharacterMechanicInputOrigins.CharacterState,
            Required: false,
            ParticipatesInValue: false,
            DefaultInteger: null,
            IncludeWhenBooleanInputKey: null,
            IncludeWhenBooleanValue: null),
        ContributionInput(
            "otherModifier",
            CharacterMechanicInputOrigins.Derived,
            required: false,
            defaultInteger: 0,
            contributionRole: CompetencyContributionRole)
    ];
    
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
    
    internal static string? ReadSourceNativeName(string? mechanicalJson)
    {
        if (string.IsNullOrWhiteSpace(mechanicalJson))
        {
            return null;
        }
    
        try
        {
            using var document = JsonDocument.Parse(mechanicalJson);
            if (!document.RootElement.TryGetProperty("_rulesCore", out var rulesCore)
                || rulesCore.ValueKind != JsonValueKind.Object
                || !rulesCore.TryGetProperty("context", out var context)
                || context.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
    
            return ReadString(context, "nativeName");
        }
        catch (JsonException)
        {
            return null;
        }
    }
    
    private static string? ReadNativeName(string? mechanicalJson)
    {
        if (string.IsNullOrWhiteSpace(mechanicalJson))
        {
            return null;
        }
    
        try
        {
            using var document = JsonDocument.Parse(mechanicalJson);
            return document.RootElement.TryGetProperty("name", out var name)
                && name.ValueKind == JsonValueKind.String
                ? name.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
    
    private static string? ReadNativeGoverningAbilityKey(string? mechanicalJson)
    {
        if (string.IsNullOrWhiteSpace(mechanicalJson))
        {
            return null;
        }
    
        try
        {
            using var document = JsonDocument.Parse(mechanicalJson);
            return document.RootElement.TryGetProperty("ability", out var ability)
                && ability.ValueKind == JsonValueKind.String
                ? NormalizeAbilityKey(ability.GetString())
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
    
    private static string? NormalizeAbilityKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
    
        return value.Trim().ToLowerInvariant() switch
        {
            "str" or "strength" => "strength",
            "dex" or "dexterity" => "dexterity",
            "con" or "constitution" => "constitution",
            "int" or "intelligence" => "intelligence",
            "wis" or "wisdom" => "wisdom",
            "cha" or "charisma" => "charisma",
            _ => value.Trim().ToLowerInvariant()
        };
    }
    
    private static bool IsLaterEdition(string? gameEdition) =>
        string.Equals(gameEdition, "5e", StringComparison.OrdinalIgnoreCase)
        || string.Equals(gameEdition, "5.5e", StringComparison.OrdinalIgnoreCase);
    
    private static string? ReadString(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    
    private static bool? ReadBoolean(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property)
        && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            ? property.GetBoolean()
            : null;
    
    private static IReadOnlyList<string> ReadStringArray(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
    
        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
    
}

internal sealed record CharacterMechanicsPublicationAttribution(
    string WorkKey,
    string WorkDisplayName,
    string? GameEdition,
    string? ReleaseKind,
    DateOnly? PublicationDate);