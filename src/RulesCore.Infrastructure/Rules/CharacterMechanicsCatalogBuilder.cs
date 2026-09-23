using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Builds Character-facing mechanics catalogs from an already-resolved rules scope,
/// including competency semantics, relationships, source attribution, and provenance.
/// </summary>
internal sealed class CharacterMechanicsCatalogBuilder(RulesCoreDbContext dbContext)
{
    private readonly CharacterMechanicsSourceMetadataReader sourceMetadata = new(dbContext);

    private const string AbilityContributionRole = "ability";
    private const string CompetencyContributionRole = "competency";

    internal async Task<CharacterMechanicsCatalogView> BuildAsync(
        ResolvedRulesCatalogView rules,
        string? userId,
        bool includeUnavailable,
        CancellationToken cancellationToken)
    {
        var effectivePackageKeys = rules.Rules
            .Select(value => value.PackageKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var providerByPackageKey = await sourceMetadata.ReadProvidersAsync(rules.Rules, cancellationToken);
        var sourceUriByRevision = await sourceMetadata.ReadSourceUrisAsync(rules.Rules, cancellationToken);
        var publicationByRevision = await sourceMetadata.ReadPublicationAttributionsAsync(
            rules.Rules,
            cancellationToken);
        var competencyRules = rules.Rules.Where(IsCompetencyRule).ToArray();
        var competencyDocuments = await sourceMetadata.ReadMechanicalDocumentsAsync(
            competencyRules,
            cancellationToken);
        var competencyProfilesByConcept = await sourceMetadata.ReadCompetencyProfilesAsync(
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
            var competency = CharacterCompetencyProfileFactory.BuildDefinition(
                rule,
                competencyDocuments.GetValueOrDefault(rule.SourceEntityRevisionId),
                publicationMetadata,
                competencyProfilesByConcept.GetValueOrDefault(rule.RuleConceptId, []));
            var inputs = BuildCompetencyInputs(derivation, competency);
    
            var sourceUri = sourceUriByRevision.GetValueOrDefault(rule.SourceEntityRevisionId);
            var provider = providerByPackageKey.GetValueOrDefault(rule.PackageKey)
                ?? rule.PackageDisplayName;
            var effectiveRuleAttributions = CharacterMechanicsSourceMetadataReader.BuildRuleAttributions(
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
    

}