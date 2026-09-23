using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Rules;

public sealed class CharacterMechanicsConsumerService(RulesCoreDbContext dbContext)
    : ICharacterMechanicsConsumerService
{
    private readonly IResolvedRulesCatalogService resolvedRules =
        new ResolvedRulesCatalogService(dbContext);
    private const int PageSize = 500;
    private const string AbilityContributionRole = "ability";
    private const string CompetencyContributionRole = "competency";

    public async Task<CharacterMechanicsCatalogView> GetGlobalAsync(
        string? userId,
        bool includeUnavailable = false,
        CancellationToken cancellationToken = default)
    {
        var rules = await ReadAllGlobalRulesAsync(userId, cancellationToken);
        return await BuildCatalogAsync(rules, userId, includeUnavailable, cancellationToken);
    }

    public async Task<CharacterMechanicsCatalogView> GetCampaignAsync(
        Guid campaignId,
        string userId,
        bool includeUnavailable = false,
        CancellationToken cancellationToken = default)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("Campaign ID can not be empty.", nameof(campaignId));
        }
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(userId));
        }

        var normalizedUserId = userId.Trim();
        var rules = await ReadAllCampaignRulesAsync(campaignId, normalizedUserId, cancellationToken);
        return await BuildCatalogAsync(
            rules,
            normalizedUserId,
            includeUnavailable,
            cancellationToken);
    }

    public async Task<CharacterMechanicEvaluationView?> EvaluateGlobalAsync(
        string mechanicKey,
        CharacterMechanicEvaluationRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var catalog = await GetGlobalAsync(userId, includeUnavailable: false, cancellationToken);
        return CharacterMechanicsCatalogEvaluator.EvaluateFromCatalog(catalog, mechanicKey, request);
    }

    public async Task<CharacterMechanicsBatchEvaluationView> EvaluateGlobalBatchAsync(
        CharacterMechanicsBatchEvaluationRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var catalog = await GetGlobalAsync(userId, includeUnavailable: false, cancellationToken);
        return CharacterMechanicsCatalogEvaluator.EvaluateBatchFromCatalog(catalog, request);
    }

    public async Task<CharacterMechanicEvaluationView?> EvaluateCampaignAsync(
        Guid campaignId,
        string mechanicKey,
        CharacterMechanicEvaluationRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var catalog = await GetCampaignAsync(
            campaignId,
            userId,
            includeUnavailable: false,
            cancellationToken);
        return CharacterMechanicsCatalogEvaluator.EvaluateFromCatalog(catalog, mechanicKey, request);
    }

    public async Task<CharacterMechanicsBatchEvaluationView> EvaluateCampaignBatchAsync(
        Guid campaignId,
        CharacterMechanicsBatchEvaluationRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var catalog = await GetCampaignAsync(
            campaignId,
            userId,
            includeUnavailable: false,
            cancellationToken);
        return CharacterMechanicsCatalogEvaluator.EvaluateBatchFromCatalog(catalog, request);
    }

    public async Task<CharacterSupportProjectionView> ProjectGlobalSupportAsync(
        CharacterSupportProjectionRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rules = await ReadAllGlobalRulesAsync(userId, cancellationToken);
        var catalog = await CharacterSupportCatalogBuilder.BuildAsync(
            dbContext,
            rules,
            cancellationToken);
        return CharacterSupportResolver.Project(catalog, request);
    }

    public async Task<CharacterSupportProjectionView> ProjectCampaignSupportAsync(
        Guid campaignId,
        CharacterSupportProjectionRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalizedUserId = ValidateCampaignSupportRequest(campaignId, userId);
        var rules = await ReadAllCampaignRulesAsync(
            campaignId,
            normalizedUserId,
            cancellationToken);
        var catalog = await CharacterSupportCatalogBuilder.BuildAsync(
            dbContext,
            rules,
            cancellationToken);
        return CharacterSupportResolver.Project(catalog, request);
    }

    public async Task<CharacterRecoveryResolutionView?> ResolveGlobalRecoveryAsync(
        string procedureKey,
        CharacterRecoveryResolutionRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rules = await ReadAllGlobalRulesAsync(userId, cancellationToken);
        var catalog = await CharacterSupportCatalogBuilder.BuildAsync(
            dbContext,
            rules,
            cancellationToken);
        return CharacterSupportResolver.ResolveRecovery(
            catalog,
            procedureKey,
            request);
    }

    public async Task<CharacterRecoveryResolutionView?> ResolveCampaignRecoveryAsync(
        Guid campaignId,
        string procedureKey,
        CharacterRecoveryResolutionRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalizedUserId = ValidateCampaignSupportRequest(campaignId, userId);
        var rules = await ReadAllCampaignRulesAsync(
            campaignId,
            normalizedUserId,
            cancellationToken);
        var catalog = await CharacterSupportCatalogBuilder.BuildAsync(
            dbContext,
            rules,
            cancellationToken);
        return CharacterSupportResolver.ResolveRecovery(
            catalog,
            procedureKey,
            request);
    }

    private static string ValidateCampaignSupportRequest(
        Guid campaignId,
        string userId)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException(
                "Campaign ID can not be empty.",
                nameof(campaignId));
        }
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException(
                "User ID can not be blank.",
                nameof(userId));
        }
        return userId.Trim();
    }

    private async Task<CharacterMechanicsCatalogView> BuildCatalogAsync(
        ResolvedRulesCatalogView rules,
        string? userId,
        bool includeUnavailable,
        CancellationToken cancellationToken)
    {
        var effectivePackageKeys = rules.Rules
            .Select(value => value.PackageKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var providerByPackageKey = await ReadProvidersAsync(rules.Rules, cancellationToken);
        var sourceUriByRevision = await ReadSourceUrisAsync(rules.Rules, cancellationToken);
        var publicationByRevision = await ReadPublicationAttributionsAsync(
            rules.Rules,
            cancellationToken);
        var competencyRules = rules.Rules.Where(IsCompetencyRule).ToArray();
        var competencyDocuments = await ReadMechanicalDocumentsAsync(
            competencyRules,
            cancellationToken);
        var competencyProfilesByConcept = await ReadCompetencyProfilesAsync(
            competencyRules,
            userId,
            cancellationToken);

        var mechanics = new List<CharacterMechanicView>();

        foreach (var definition in KnownCharacterMechanics.All)
        {
            var available = IsAvailable(definition, effectivePackageKeys);
            if (!available && !includeUnavailable)
            {
                continue;
            }

            mechanics.Add(ToStaticView(definition, available));
        }

        var relationshipViews = await BuildCompetencyRelationshipsAsync(
            rules.Rules,
            cancellationToken);
        var relationshipByMechanic = relationshipViews
            .SelectMany(value => new[] { value.ParentMechanicKey }.Concat(value.ComponentMechanicKeys)
                .Select(key => new { key, relationship = value }))
            .GroupBy(value => value.key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CharacterMechanicRelationshipView>)group
                    .Select(value => value.relationship)
                    .OrderBy(value => value.RelationshipKey, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        foreach (var rule in rules.Rules
                     .Where(IsCompetencyRule)
                     .OrderBy(value => value.ConceptKey, StringComparer.Ordinal))
        {
            var mechanicKey = CompetencyMechanicKey(rule.ConceptKey);
            relationshipByMechanic.TryGetValue(mechanicKey, out var relationships);
            relationships ??= [];

            var derivation = relationships.SingleOrDefault(value =>
                string.Equals(value.ParentMechanicKey, mechanicKey, StringComparison.Ordinal)
                && string.Equals(
                    value.EffectiveResolutionKind,
                    MechanicalRelationshipResolutionKinds.DeriveParent,
                    StringComparison.Ordinal)
                && value.CanResolve);

            var publicationMetadata = publicationByRevision.GetValueOrDefault(
                rule.SourceEntityRevisionId,
                []);
            var competency = BuildCompetencyDefinition(
                rule,
                competencyDocuments.GetValueOrDefault(rule.SourceEntityRevisionId),
                publicationMetadata,
                competencyProfilesByConcept.GetValueOrDefault(rule.RuleConceptId, []));
            var inputs = BuildCompetencyInputs(derivation, competency);

            var sourceUri = sourceUriByRevision.GetValueOrDefault(rule.SourceEntityRevisionId);
            var provider = providerByPackageKey.GetValueOrDefault(rule.PackageKey)
                ?? rule.PackageDisplayName;
            var effectiveRuleAttributions = BuildRuleAttributions(
                rule,
                provider,
                sourceUri,
                publicationMetadata);
            var canonicalAttributions = competency.Profiles
                .SelectMany(value => value.SourceAttributions ?? [])
                .Concat(effectiveRuleAttributions)
                .GroupBy(value => new
                {
                    value.PackageKey,
                    value.SourceCode,
                    value.SourceRevisionNumber,
                    value.WorkKey,
                    value.ReferenceKey,
                    value.ReferenceUri
                })
                .Select(group => group
                    .OrderByDescending(value => !string.IsNullOrWhiteSpace(value.ReferenceTitle))
                    .ThenByDescending(value => !string.IsNullOrWhiteSpace(value.WorkDisplayName))
                    .First())
                .OrderBy(value => value.WorkDisplayName, StringComparer.Ordinal)
                .ThenBy(value => value.PackageKey, StringComparer.Ordinal)
                .ThenBy(value => value.SourceRevisionNumber)
                .ToArray();
            var mechanicalProfileAttributions = competency.DefaultProfileSourceEntityRevisionId is Guid defaultProfileRevisionId
                ? competency.Profiles
                    .Where(value => value.SourceEntityRevisionId == defaultProfileRevisionId)
                    .SelectMany(value => value.SourceAttributions ?? [])
                    .Distinct()
                    .ToArray()
                : [];
            mechanics.Add(new CharacterMechanicView(
                mechanicKey,
                CharacterMechanicKinds.Competency,
                rule.DisplayName,
                rule.ConceptKey,
                rule.RuleConceptId,
                IsAvailableUnderRuleset: true,
                new CharacterMechanicApplicabilityView(
                    CharacterMechanicApplicabilityKinds.Always,
                    RequiresCharacterState: true,
                    RequiredCapabilityKeys: [],
                    SourcePackageKey: rule.PackageKey),
                derivation is null
                    ? CharacterMechanicEvaluationKinds.CompetencyProfile
                    : CharacterMechanicEvaluationKinds.CompositeCompetency,
                CanEvaluate: derivation is not null
                    || competency.Profiles.Any(value => value.CanEvaluate),
                Constant: 0,
                TargetInputKey: null,
                BaseMechanicKey: null,
                inputs,
                relationships,
                ConditionalRollRules: [],
                BooleanRequirements: [],
                Check: null,
                Competency: competency,
                ContributorGroups: [],
                SourceAttributions: canonicalAttributions,
                Provenance: new CharacterMechanicProvenanceView(
                    canonicalAttributions,
                    mechanicalProfileAttributions,
                    effectiveRuleAttributions)));
        }

        mechanics = AttachStaticRelationships(mechanics);
        mechanics = UniversalCompetencyProjection.ApplySemanticIdentity(mechanics);
        mechanics = AttachCompetencyIdentityFacets(mechanics);

        var orderedMechanics = mechanics
            .OrderBy(value => value.Kind, StringComparer.Ordinal)
            .ThenBy(value => value.DisplayName, StringComparer.Ordinal)
            .ThenBy(value => value.MechanicKey, StringComparer.Ordinal)
            .ToArray();
        var universalCompetencies =
            UniversalCompetencyProjection.BuildCatalog(orderedMechanics);

        return new CharacterMechanicsCatalogView(
            rules.Scope,
            rules.CampaignId,
            rules.RevisionNumber,
            rules.PublishedAt,
            orderedMechanics,
            universalCompetencies);
    }

    private async Task<IReadOnlyList<CharacterMechanicRelationshipView>> BuildCompetencyRelationshipsAsync(
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        var byConceptKey = rules
            .Where(IsCompetencyRule)
            .GroupBy(value => value.ConceptKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var result = new List<CharacterMechanicRelationshipView>();
        var service = new MechanicalRelationshipService(dbContext);
        foreach (var definition in KnownMechanicalRelationships.All)
        {
            var references = new[] { definition.Parent }.Concat(definition.Components).ToArray();
            var present = references
                .Where(value => byConceptKey.ContainsKey(value.ConceptKey))
                .ToArray();
            if (present.Length == 0)
            {
                continue;
            }

            var current = byConceptKey[present[0].ConceptKey];
            var recommendation = (await service.GetForConceptAsync(
                    current.RuleConceptId,
                    cancellationToken))
                .Single(value => string.Equals(
                    value.Relationship.Key,
                    definition.Key,
                    StringComparison.Ordinal));

            var missing = references
                .Where(value => !byConceptKey.ContainsKey(value.ConceptKey))
                .Select(value => CompetencyMechanicKey(value.ConceptKey))
                .ToArray();
            var canResolve = missing.Length == 0
                && string.Equals(
                    recommendation.EffectiveResolutionKind,
                    MechanicalRelationshipResolutionKinds.DeriveParent,
                    StringComparison.Ordinal);

            result.Add(new CharacterMechanicRelationshipView(
                definition.Key,
                definition.Kind,
                CompetencyMechanicKey(definition.Parent.ConceptKey),
                definition.Components
                    .Select(value => CompetencyMechanicKey(value.ConceptKey))
                    .ToArray(),
                definition.Composition,
                definition.Direction,
                recommendation.EffectiveResolutionKind,
                canResolve,
                missing));
        }

        return result;
    }

    private static List<CharacterMechanicView> AttachStaticRelationships(
        List<CharacterMechanicView> mechanics)
    {
        var byKey = mechanics.ToDictionary(value => value.MechanicKey, StringComparer.Ordinal);
        foreach (var definition in KnownCharacterMechanics.Relationships)
        {
            var memberKeys = new[] { definition.ParentMechanicKey }
                .Concat(definition.ComponentMechanicKeys)
                .ToArray();
            if (!memberKeys.Any(byKey.ContainsKey))
            {
                continue;
            }

            var missing = memberKeys.Where(value => !byKey.ContainsKey(value)).ToArray();
            var relationship = new CharacterMechanicRelationshipView(
                definition.Key,
                definition.Kind,
                definition.ParentMechanicKey,
                definition.ComponentMechanicKeys,
                definition.Composition,
                definition.Direction,
                EffectiveResolutionKind: null,
                CanResolve: missing.Length == 0,
                MissingMechanicKeys: missing);

            foreach (var key in memberKeys.Where(byKey.ContainsKey))
            {
                var current = byKey[key];
                var updated = current with
                {
                    Relationships = current.Relationships
                        .Concat([relationship])
                        .OrderBy(value => value.RelationshipKey, StringComparer.Ordinal)
                        .ToArray()
                };
                byKey[key] = updated;
                var index = mechanics.FindIndex(value =>
                    string.Equals(value.MechanicKey, key, StringComparison.Ordinal));
                mechanics[index] = updated;
            }
        }

        return mechanics;
    }

    private static List<CharacterMechanicView> AttachCompetencyIdentityFacets(
        List<CharacterMechanicView> mechanics)
    {
        var groups = mechanics
            .Where(value => value.Competency is not null
                && !string.IsNullOrWhiteSpace(value.Competency.IdentityKey))
            .GroupBy(
                value => value.Competency!.IdentityKey!,
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 0)
            .ToArray();

        foreach (var group in groups)
        {
            var members = group.ToArray();
            var profiles = members
                .SelectMany(value => value.Competency!.Profiles
                    .Select(profile => new { Mechanic = value, Profile = profile }))
                .ToArray();
            var facets = profiles
                .GroupBy(
                    value => string.IsNullOrWhiteSpace(value.Profile.FacetType)
                        ? string.Equals(
                            value.Profile.CompetencyKind,
                            CharacterCompetencyKinds.Tool,
                            StringComparison.OrdinalIgnoreCase)
                            ? "tool"
                            : "skill"
                        : value.Profile.FacetType!,
                    StringComparer.OrdinalIgnoreCase)
                .Select(facet => new CharacterCompetencyFacetView(
                    facet.Key,
                    facet.Select(value => value.Profile.SourceEntityRevisionId)
                        .Distinct()
                        .OrderBy(value => value)
                        .ToArray(),
                    facet.Any(value => value.Profile.SupportsRanks),
                    facet.Any(value => value.Profile.SupportsClassSkillState),
                    facet.Any(value => value.Profile.SupportsTrainingState),
                    facet.Select(value => value.Mechanic.MechanicKey)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray()))
                .OrderBy(value => value.FacetType, StringComparer.Ordinal)
                .ToArray();

            var identityNames = members
                .Select(value => value.Competency!.IdentityName)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var trainingKeys = members
                .Select(value => value.Competency!.SharedTrainingKey)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var relationships = members
                .SelectMany(value => value.Competency!.RelatedCompetencies ?? [])
                .Distinct()
                .OrderBy(value => value.Kind, StringComparer.Ordinal)
                .ThenBy(value => value.TargetType, StringComparer.Ordinal)
                .ThenBy(value => value.TargetName, StringComparer.Ordinal)
                .ThenBy(value => value.Scope, StringComparer.Ordinal)
                .ToArray();

            foreach (var member in members)
            {
                var competency = member.Competency! with
                {
                    IdentityName = identityNames.Length == 1
                        ? identityNames[0]
                        : member.Competency!.IdentityName,
                    SharedTrainingKey = trainingKeys.Length == 1
                        ? trainingKeys[0]
                        : member.Competency!.SharedTrainingKey,
                    Facets = facets,
                    RelatedCompetencies = relationships
                };
                var updated = member with { Competency = competency };
                var memberIndex = mechanics.FindIndex(value =>
                    string.Equals(
                        value.MechanicKey,
                        member.MechanicKey,
                        StringComparison.Ordinal));
                mechanics[memberIndex] = updated;
            }
        }

        return mechanics;
    }

    private static CharacterMechanicView ToStaticView(
        CharacterMechanicDefinition definition,
        bool available)
    {
        var sourceAttributions = definition.Source is null
            ? (IReadOnlyList<CharacterMechanicSourceAttributionView>)[]
            :
            [
                new CharacterMechanicSourceAttributionView(
                    definition.Source.PackageKey,
                    definition.Source.PackageDisplayName,
                    definition.Source.Provider,
                    null,
                    null,
                    definition.Source.WorkKey,
                    definition.Source.WorkDisplayName,
                    definition.Source.GameEdition,
                    definition.Source.ReleaseKind,
                    definition.Source.PublicationDate,
                    definition.Source.WorkKey,
                    definition.Source.WorkDisplayName,
                    definition.Source.ReferenceUri,
                    definition.Source.PresentationRequired,
                    definition.Source.ReferenceLinkRequired)
            ];

        return new CharacterMechanicView(
            definition.Key,
            definition.Kind,
            definition.DisplayName,
            ConceptKey: null,
            RuleConceptId: null,
            available,
            new CharacterMechanicApplicabilityView(
                definition.Applicability.Kind,
                definition.Applicability.RequiresCharacterState,
                definition.Applicability.RequiredCapabilityKeys,
                definition.Applicability.SourcePackageKey),
            definition.EvaluationKind,
            available
                && definition.EvaluationKind != CharacterMechanicEvaluationKinds.None,
            definition.Constant,
            definition.TargetInputKey,
            definition.BaseMechanicKey,
            definition.Inputs.Select(value => new CharacterMechanicInputView(
                value.Key,
                value.ValueKind,
                value.Origin,
                value.Required,
                value.ParticipatesInValue,
                value.DefaultInteger,
                value.IncludeWhenBooleanInputKey,
                value.IncludeWhenBooleanValue)).ToArray(),
            Relationships: [],
            definition.ConditionalRollRules.Select(value => new CharacterMechanicConditionalRollRuleView(
                value.Key,
                value.Conditions.Select(condition => new CharacterMechanicBooleanConditionView(
                    condition.InputKey,
                    condition.ExpectedValue)).ToArray(),
                value.RollMode,
                value.TargetMechanicKeys)).ToArray(),
            definition.BooleanRequirements.Select(value => new CharacterMechanicBooleanRequirementView(
                value.InputKey,
                value.ExpectedValue)).ToArray(),
            Check: definition.Check is null
                ? null
                : new CharacterMechanicCheckView(
                    new CharacterCheckAbilityView(
                        definition.Check.Ability.ResolutionKind,
                        definition.Check.Ability.FixedAbilityKey,
                        definition.Check.Ability.AllowedAbilityKeys ?? []),
                    new CharacterCheckCompetencyView(
                        definition.Check.Competency.ResolutionKind,
                        definition.Check.Competency.AllowedCompetencyKinds,
                        definition.Check.Competency.FixedConceptKey),
                    definition.Check.CompetencyComposition is null
                        ? null
                        : new CharacterCheckCompetencyCompositionView(
                            definition.Check.CompetencyComposition.ConceptKeyInputKey,
                            definition.Check.CompetencyComposition.ContributionInputKey)),
            Competency: null,
            ContributorGroups: (definition.ContributorGroups ?? [])
                .Select(group => new CharacterMechanicContributorGroupView(
                    group.Key,
                    group.MaximumCountStringInputKey,
                    group.MaximumCountByStringValue,
                    group.Inputs.Select(input => new CharacterMechanicInputView(
                        input.Key,
                        input.ValueKind,
                        input.Origin,
                        input.Required,
                        input.ParticipatesInValue,
                        input.DefaultInteger,
                        input.IncludeWhenBooleanInputKey,
                        input.IncludeWhenBooleanValue)).ToArray(),
                    new CharacterMechanicContributorValueView(
                        group.Value.AmountIntegerInputKey,
                        group.Value.FullAmountBooleanInputKey,
                        group.Value.FullAmountWhenBooleanValue,
                        group.Value.AlternateNumerator,
                        group.Value.AlternateDenominator,
                        group.Value.AlternateRoundingKind,
                        group.Value.RequireNonNegativeAmount),
                    group.BooleanRequirements.Select(requirement =>
                        new CharacterMechanicBooleanRequirementView(
                            requirement.InputKey,
                            requirement.ExpectedValue)).ToArray(),
                    group.StandardHelpActionApplies))
                .ToArray(),
            sourceAttributions);
    }

    private static bool IsAvailable(
        CharacterMechanicDefinition definition,
        IReadOnlySet<string> effectivePackageKeys) =>
        definition.Applicability.Kind switch
        {
            CharacterMechanicApplicabilityKinds.Always => true,
            CharacterMechanicApplicabilityKinds.CharacterCapability => true,
            CharacterMechanicApplicabilityKinds.ExternalPublicRules => true,
            CharacterMechanicApplicabilityKinds.AccessibleSource =>
                definition.Applicability.SourcePackageKey is string packageKey
                && effectivePackageKeys.Contains(packageKey),
            _ => false
        };

    private static IReadOnlyList<CharacterMechanicInputView> BuildCompetencyInputs(
        CharacterMechanicRelationshipView? derivation,
        CharacterCompetencyDefinitionView competency)
    {
        if (derivation is not null)
        {
            return derivation.ComponentMechanicKeys.Select(value =>
                new CharacterMechanicInputView(
                    ConceptKeyFromCompetencyMechanicKey(value),
                    CharacterMechanicInputValueKinds.Integer,
                    CharacterMechanicInputOrigins.Derived,
                    Required: true,
                    ParticipatesInValue: true,
                    DefaultInteger: null,
                    IncludeWhenBooleanInputKey: null,
                    IncludeWhenBooleanValue: null))
                .ToArray();
        }

        if (!competency.DefaultProfileSourceEntityRevisionId.HasValue)
        {
            return [];
        }

        return competency.Profiles
            .SingleOrDefault(value =>
                value.SourceEntityRevisionId == competency.DefaultProfileSourceEntityRevisionId.Value)
            ?.Inputs
            ?? [];
    }

    private static CharacterCompetencyDefinitionView BuildCompetencyDefinition(
        ResolvedRuleCatalogItemView rule,
        string? selectedMechanicalJson,
        IReadOnlyList<PublicationAttribution> selectedPublications,
        IReadOnlyList<CharacterCompetencyProfileView> profiles)
    {
        var selectedGameEdition = selectedPublications
            .Select(value => value.GameEdition)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var selectedProfile = BuildCompetencyProfile(
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

    private static CharacterCompetencyProfileView? BuildCompetencyProfile(
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
            RelatedCompetencies: []);
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

    private static string? ReadSourceNativeName(string? mechanicalJson)
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

    private static IReadOnlyList<CharacterMechanicSourceAttributionView> BuildRuleAttributions(
        ResolvedRuleCatalogItemView rule,
        string provider,
        string? sourceUri,
        IReadOnlyList<PublicationAttribution> publications)
    {
        if (publications.Count == 0)
        {
            return
            [
                new CharacterMechanicSourceAttributionView(
                    rule.PackageKey,
                    rule.PackageDisplayName,
                    provider,
                    rule.SourceCode,
                    rule.SourceRevisionNumber,
                    WorkKey: null,
                    WorkDisplayName: null,
                    GameEdition: null,
                    ReleaseKind: null,
                    PublicationDate: null,
                    ReferenceKey: null,
                    ReferenceTitle: rule.SourceEntityName,
                    ReferenceUri: sourceUri,
                    PresentationRequired: false,
                    ReferenceLinkRequired: false)
            ];
        }

        return publications
            .OrderBy(value => value.WorkDisplayName, StringComparer.Ordinal)
            .ThenBy(value => value.WorkKey, StringComparer.Ordinal)
            .Select(value => new CharacterMechanicSourceAttributionView(
                rule.PackageKey,
                rule.PackageDisplayName,
                provider,
                rule.SourceCode,
                rule.SourceRevisionNumber,
                value.WorkKey,
                value.WorkDisplayName,
                value.GameEdition,
                value.ReleaseKind,
                value.PublicationDate,
                ReferenceKey: null,
                ReferenceTitle: rule.SourceEntityName,
                ReferenceUri: sourceUri,
                PresentationRequired: false,
                ReferenceLinkRequired: false))
            .ToArray();
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<PublicationAttribution>>> ReadPublicationAttributionsAsync(
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        var revisionIds = rules.Select(value => value.SourceEntityRevisionId).Distinct().ToArray();
        if (revisionIds.Length == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<PublicationAttribution>>();
        }

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DISTINCT
                    binding.source_entity_revision_id,
                    publication.canonical_key,
                    publication.display_name,
                    publication.game_edition,
                    publication.release_kind,
                    publication.publication_date
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                JOIN canonical_publication publication
                    ON publication.canonical_publication_id = occurrence.canonical_publication_id
                WHERE binding.source_entity_revision_id = ANY(@revision_ids);
                """;
            AddParameter(command, "@revision_ids", revisionIds);

            var values = new Dictionary<Guid, List<PublicationAttribution>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var revisionId = reader.GetGuid(0);
                if (!values.TryGetValue(revisionId, out var publications))
                {
                    publications = [];
                    values.Add(revisionId, publications);
                }

                publications.Add(new PublicationAttribution(
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5)));
            }

            return values.ToDictionary(
                value => value.Key,
                value => (IReadOnlyList<PublicationAttribution>)value.Value
                    .Distinct()
                    .ToArray());
        }
        catch (DbException)
        {
            return new Dictionary<Guid, IReadOnlyList<PublicationAttribution>>();
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<CharacterCompetencyProfileView>>> ReadCompetencyProfilesAsync(
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        string? userId,
        CancellationToken cancellationToken)
    {
        var conceptIds = rules.Select(value => value.RuleConceptId).Distinct().ToArray();
        if (conceptIds.Length == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<CharacterCompetencyProfileView>>();
        }

        await CanonicalRuleBindingStore.EnsureSchemaAsync(dbContext, cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                WITH RECURSIVE concept_entities(rule_concept_id, canonical_entity_id) AS (
                    SELECT rule_concept_id, canonical_entity_id
                    FROM rule_concept_source_binding
                    WHERE rule_concept_id = ANY(@concept_ids)
                    UNION
                    SELECT parent.rule_concept_id, relationship.to_canonical_entity_id
                    FROM canonical_entity_relationship relationship
                    JOIN concept_entities parent
                        ON parent.canonical_entity_id = relationship.from_canonical_entity_id
                    WHERE relationship.relationship_kind = 'revision'
                ),
                latest AS (
                    SELECT DISTINCT ON (source_entity_id)
                        source_entity_id,
                        source_entity_revision_id
                    FROM source_entity_revision
                    ORDER BY source_entity_id, revision_number DESC
                )
                SELECT DISTINCT
                    concept_entity.rule_concept_id,
                    revision.source_entity_revision_id,
                    revision.content_json,
                    revision.raw_json,
                    source.entity_type,
                    publication.game_edition,
                    package.package_key,
                    package.display_name,
                    package.provider,
                    source.source_code,
                    revision.revision_number,
                    publication.canonical_key,
                    publication.display_name,
                    publication.release_kind,
                    publication.publication_date,
                    representation.source_uri
                FROM concept_entities concept_entity
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_entity_id = concept_entity.canonical_entity_id
                JOIN source_entity_occurrence_binding occurrence_binding
                    ON occurrence_binding.canonical_source_occurrence_id = occurrence.canonical_source_occurrence_id
                JOIN latest
                    ON latest.source_entity_revision_id = occurrence_binding.source_entity_revision_id
                JOIN source_entity_revision revision
                    ON revision.source_entity_revision_id = latest.source_entity_revision_id
                JOIN source_entity source
                    ON source.source_entity_id = revision.source_entity_id
                JOIN source_representation representation
                    ON representation.source_representation_id = revision.source_representation_id
                JOIN canonical_publication publication
                    ON publication.canonical_publication_id = occurrence.canonical_publication_id
                JOIN source_package package
                    ON package.source_package_id = source.source_package_id
                WHERE package.is_public
                    OR (@user_id IS NOT NULL AND EXISTS (
                        SELECT 1
                        FROM user_source_grant grant_row
                        WHERE grant_row.source_package_id = package.source_package_id
                            AND grant_row.user_id = @user_id))
                ORDER BY concept_entity.rule_concept_id, revision.source_entity_revision_id;
                """;
            AddParameter(command, "@concept_ids", conceptIds);
            AddNullableStringParameter(command, "@user_id", NormalizeOptionalUserId(userId));

            var profiles = new Dictionary<Guid, List<CharacterCompetencyProfileView>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var conceptId = reader.GetGuid(0);
                var revisionId = reader.GetGuid(1);
                var contentJson = reader.IsDBNull(2) ? null : reader.GetString(2);
                var rawJson = reader.GetString(3);
                var entityType = reader.GetString(4);
                var gameEdition = reader.IsDBNull(5) ? null : reader.GetString(5);
                var attribution = new CharacterMechanicSourceAttributionView(
                    reader.GetString(6),
                    reader.GetString(7),
                    reader.GetString(8),
                    reader.IsDBNull(9) ? null : reader.GetString(9),
                    reader.GetInt32(10),
                    reader.IsDBNull(11) ? null : reader.GetString(11),
                    reader.IsDBNull(12) ? null : reader.GetString(12),
                    gameEdition,
                    reader.IsDBNull(13) ? null : reader.GetString(13),
                    reader.IsDBNull(14) ? null : reader.GetFieldValue<DateOnly>(14),
                    ReferenceKey: null,
                    ReferenceTitle: null,
                    ReferenceUri: reader.IsDBNull(15) ? null : reader.GetString(15),
                    PresentationRequired: false,
                    ReferenceLinkRequired: false);
                var mechanicalJson =
                    string.IsNullOrWhiteSpace(contentJson) ? rawJson : contentJson;
                var profile = BuildCompetencyProfile(
                    revisionId,
                    entityType,
                    ReadSourceNativeName(mechanicalJson),
                    mechanicalJson,
                    gameEdition,
                    [attribution]);
                if (profile is null)
                {
                    continue;
                }

                if (!profiles.TryGetValue(conceptId, out var conceptProfiles))
                {
                    conceptProfiles = [];
                    profiles.Add(conceptId, conceptProfiles);
                }
                conceptProfiles.Add(profile);
            }

            return profiles.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<CharacterCompetencyProfileView>)pair.Value
                    .GroupBy(value => new { value.SourceEntityRevisionId, value.ProfileKey })
                    .Select(group =>
                    {
                        var first = group.First();
                        var attributions = group
                            .SelectMany(value => value.SourceAttributions ?? [])
                            .Distinct()
                            .OrderBy(value => value.WorkDisplayName, StringComparer.Ordinal)
                            .ThenBy(value => value.PackageKey, StringComparer.Ordinal)
                            .ToArray();
                        return first with { SourceAttributions = attributions };
                    })
                    .OrderBy(value => value.ProfileKey, StringComparer.Ordinal)
                    .ThenBy(value => value.GameEdition, StringComparer.Ordinal)
                    .ThenBy(value => value.SourceEntityRevisionId)
                    .ToArray());
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ReadMechanicalDocumentsAsync(
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        var revisionIds = rules.Select(value => value.SourceEntityRevisionId).Distinct().ToArray();
        if (revisionIds.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var revisions = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => revisionIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        return revisions.ToDictionary(
            value => value.Id,
            value => string.IsNullOrWhiteSpace(value.ContentJson)
                ? value.RawJson
                : value.ContentJson!);
    }

    private async Task<IReadOnlyDictionary<string, string>> ReadProvidersAsync(
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        var packageKeys = rules.Select(value => value.PackageKey).Distinct().ToArray();
        if (packageKeys.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return await dbContext.SourcePackages
            .AsNoTracking()
            .Where(value => packageKeys.Contains(value.Key))
            .ToDictionaryAsync(
                value => value.Key,
                value => value.Provider,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string?>> ReadSourceUrisAsync(
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        var revisionIds = rules.Select(value => value.SourceEntityRevisionId).Distinct().ToArray();
        if (revisionIds.Length == 0)
        {
            return new Dictionary<Guid, string?>();
        }

        return await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => revisionIds.Contains(value.Id))
            .Select(value => new
            {
                value.Id,
                value.SourceRepresentation.SourceUri
            })
            .ToDictionaryAsync(
                value => value.Id,
                value => value.SourceUri,
                cancellationToken);
    }

    private async Task<ResolvedRulesCatalogView> ReadAllGlobalRulesAsync(
        string? userId,
        CancellationToken cancellationToken)
    {
        var all = new List<ResolvedRuleCatalogItemView>();
        ResolvedRulesCatalogView? page = null;
        for (var offset = 0; ; offset += PageSize)
        {
            page = await resolvedRules.GetGlobalPageAsync(
                userId,
                limit: PageSize,
                offset: offset,
                cancellationToken: cancellationToken);
            all.AddRange(page.Rules);
            if (page.Rules.Count < PageSize)
            {
                break;
            }
        }

        page ??= new ResolvedRulesCatalogView("global", null, null, null, 0, [], [], []);
        return page with { Rules = all };
    }

    private async Task<ResolvedRulesCatalogView> ReadAllCampaignRulesAsync(
        Guid campaignId,
        string userId,
        CancellationToken cancellationToken)
    {
        var all = new List<ResolvedRuleCatalogItemView>();
        ResolvedRulesCatalogView? page = null;
        for (var offset = 0; ; offset += PageSize)
        {
            page = await resolvedRules.GetCampaignPageAsync(
                campaignId,
                userId,
                limit: PageSize,
                offset: offset,
                cancellationToken: cancellationToken);
            all.AddRange(page.Rules);
            if (page.Rules.Count < PageSize)
            {
                break;
            }
        }

        page ??= new ResolvedRulesCatalogView("campaign", campaignId, null, null, 0, [], [], []);
        return page with { Rules = all };
    }

    private static bool IsCompetencyRule(ResolvedRuleCatalogItemView value) =>
        string.Equals(value.EntityType, "skill", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value.EntityType, "tool", StringComparison.OrdinalIgnoreCase);

    private static string CompetencyMechanicKey(string conceptKey) =>
        $"competency.{conceptKey.Trim().ToLowerInvariant()}";

    private static string ConceptKeyFromCompetencyMechanicKey(string mechanicKey)
    {
        const string prefix = "competency.";
        if (!mechanicKey.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Mechanic key '{mechanicKey}' is not a competency mechanic key.",
                nameof(mechanicKey));
        }
        return mechanicKey[prefix.Length..];
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void AddNullableStringParameter(DbCommand command, string name, string? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.String;
        parameter.Value = (object?)value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string? NormalizeOptionalUserId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record PublicationAttribution(
        string WorkKey,
        string WorkDisplayName,
        string? GameEdition,
        string? ReleaseKind,
        DateOnly? PublicationDate);
}