using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

public sealed class CharacterMechanicsConsumerService(RulesCoreDbContext dbContext)
    : ICharacterMechanicsConsumerService
{
    private readonly IResolvedRulesCatalogService resolvedRules =
        new ResolvedRulesCatalogService(dbContext);
    private const int PageSize = 500;

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
        return EvaluateFromCatalog(catalog, mechanicKey, request);
    }

    public async Task<CharacterMechanicsBatchEvaluationView> EvaluateGlobalBatchAsync(
        CharacterMechanicsBatchEvaluationRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var catalog = await GetGlobalAsync(userId, includeUnavailable: false, cancellationToken);
        return EvaluateBatchFromCatalog(catalog, request);
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
        return EvaluateFromCatalog(catalog, mechanicKey, request);
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
        return EvaluateBatchFromCatalog(catalog, request);
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
                competencyProfilesByConcept.GetValueOrDefault(rule.RuleConceptId, []));
            var inputs = BuildCompetencyInputs(derivation, competency);

            var sourceUri = sourceUriByRevision.GetValueOrDefault(rule.SourceEntityRevisionId);
            var provider = providerByPackageKey.GetValueOrDefault(rule.PackageKey)
                ?? rule.PackageDisplayName;
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
                    ? CharacterMechanicEvaluationKinds.SourceValue
                    : CharacterMechanicEvaluationKinds.CompositeCompetency,
                CanEvaluate: true,
                Constant: 0,
                TargetInputKey: null,
                BaseMechanicKey: null,
                inputs,
                relationships,
                ConditionalRollRules: [],
                BooleanRequirements: [],
                Check: null,
                Competency: competency,
                SourceAttributions: BuildRuleAttributions(
                    rule,
                    provider,
                    sourceUri,
                    publicationMetadata)));
        }

        mechanics = AttachStaticRelationships(mechanics);

        return new CharacterMechanicsCatalogView(
            rules.Scope,
            rules.CampaignId,
            rules.RevisionNumber,
            rules.PublishedAt,
            mechanics
                .OrderBy(value => value.Kind, StringComparer.Ordinal)
                .ThenBy(value => value.DisplayName, StringComparer.Ordinal)
                .ThenBy(value => value.MechanicKey, StringComparer.Ordinal)
                .ToArray());
    }

    private static CharacterMechanicsBatchEvaluationView EvaluateBatchFromCatalog(
        CharacterMechanicsCatalogView catalog,
        CharacterMechanicsBatchEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Evaluations);
        if (request.Evaluations.Count > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "A mechanic evaluation batch can contain at most 100 evaluations.");
        }

        var evaluations = request.Evaluations
            .Select(item =>
            {
                if (string.IsNullOrWhiteSpace(item.MechanicKey))
                {
                    throw new ArgumentException(
                        "Batch mechanic keys can not be blank.",
                        nameof(request));
                }
                ArgumentNullException.ThrowIfNull(item.Evaluation);
                return new CharacterMechanicBatchEvaluationItemView(
                    item.MechanicKey,
                    EvaluateFromCatalog(catalog, item.MechanicKey, item.Evaluation));
            })
            .ToArray();

        return new CharacterMechanicsBatchEvaluationView(
            catalog.Scope,
            catalog.CampaignId,
            catalog.RevisionNumber,
            catalog.PublishedAt,
            evaluations);
    }

    private static CharacterMechanicEvaluationView? EvaluateFromCatalog(
        CharacterMechanicsCatalogView catalog,
        string mechanicKey,
        CharacterMechanicEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalizedKey = RequireMechanicKey(mechanicKey);
        var mechanic = catalog.Mechanics.SingleOrDefault(value =>
            string.Equals(value.MechanicKey, normalizedKey, StringComparison.OrdinalIgnoreCase));
        if (mechanic is null || !mechanic.IsAvailableUnderRuleset)
        {
            return null;
        }

        var availableCapabilities = (request.CapabilityKeys ?? [])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingCapabilities = mechanic.Applicability.RequiredCapabilityKeys
            .Where(value => !availableCapabilities.Contains(value))
            .ToArray();
        if (missingCapabilities.Length > 0)
        {
            throw new InvalidOperationException(
                $"Mechanic '{mechanic.MechanicKey}' requires Character capability: {string.Join(", ", missingCapabilities)}.");
        }

        var integerInputs = request.IntegerInputs
            ?? new Dictionary<string, int>(StringComparer.Ordinal);
        var booleanInputs = request.BooleanInputs
            ?? new Dictionary<string, bool>(StringComparer.Ordinal);
        var stringInputs = request.StringInputs
            ?? new Dictionary<string, string>(StringComparer.Ordinal);

        var known = KnownCharacterMechanics.FindByKey(mechanic.MechanicKey);
        if (known is not null)
        {
            var evaluation = CharacterMechanicEvaluator.Evaluate(
                known,
                integerInputs,
                booleanInputs,
                stringInputs);
            return ToEvaluationView(mechanic.MechanicKey, known.EvaluationKind, evaluation);
        }

        if (mechanic.ConceptKey is null)
        {
            throw new InvalidOperationException(
                $"Mechanic '{mechanic.MechanicKey}' has no evaluatable definition or rule concept.");
        }

        var composite = mechanic.Relationships.SingleOrDefault(value =>
            string.Equals(value.ParentMechanicKey, mechanic.MechanicKey, StringComparison.Ordinal)
            && string.Equals(value.Kind, MechanicalRelationshipKinds.CompositeSkill, StringComparison.Ordinal)
            && string.Equals(
                value.EffectiveResolutionKind,
                MechanicalRelationshipResolutionKinds.DeriveParent,
                StringComparison.Ordinal)
            && value.CanResolve);
        if (composite is not null)
        {
            var definition = KnownMechanicalRelationships.FindByKey(composite.RelationshipKey)
                ?? throw new InvalidOperationException(
                    $"Mechanical relationship '{composite.RelationshipKey}' is not registered.");
            var componentValues = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var component in definition.Components)
            {
                if (!integerInputs.TryGetValue(component.ConceptKey, out var value))
                {
                    throw new KeyNotFoundException(
                        $"Composite competency '{mechanic.MechanicKey}' requires integer input '{component.ConceptKey}'.");
                }
                componentValues.Add(component.ConceptKey, value);
            }

            var modifiers = (request.Modifiers ?? [])
                .Select(value => new CompetencyModifier(value.TargetConceptKey, value.Value))
                .ToArray();
            var evaluation = CompositeCompetencyEvaluator.Evaluate(
                definition,
                componentValues,
                modifiers);
            return new CharacterMechanicEvaluationView(
                mechanic.MechanicKey,
                CharacterMechanicEvaluationKinds.CompositeCompetency,
                evaluation.ParentValue,
                Target: null,
                MeetsTarget: null,
                RequirementsSatisfied: true,
                UnsatisfiedRequirementKeys: [],
                AppliedRollRules: []);
        }

        if (!integerInputs.TryGetValue("value", out var sourceValue))
        {
            throw new KeyNotFoundException(
                $"Competency mechanic '{mechanic.MechanicKey}' requires integer input 'value'.");
        }

        return new CharacterMechanicEvaluationView(
            mechanic.MechanicKey,
            CharacterMechanicEvaluationKinds.SourceValue,
            sourceValue,
            Target: null,
            MeetsTarget: null,
            RequirementsSatisfied: true,
            UnsatisfiedRequirementKeys: [],
            AppliedRollRules: []);
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
                        definition.Check.Competency.FixedConceptKey)),
            Competency: null,
            sourceAttributions);
    }

    private static CharacterMechanicEvaluationView ToEvaluationView(
        string mechanicKey,
        string evaluationKind,
        CharacterMechanicEvaluation evaluation) =>
        new(
            mechanicKey,
            evaluationKind,
            evaluation.Value,
            evaluation.Target,
            evaluation.MeetsTarget,
            evaluation.RequirementsSatisfied,
            evaluation.UnsatisfiedRequirementKeys,
            evaluation.AppliedRollRules.Select(value => new CharacterMechanicAppliedRollRuleView(
                value.Key,
                value.RollMode,
                value.TargetMechanicKeys)).ToArray());

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
        var inputs = new List<CharacterMechanicInputView>();
        if (derivation is null)
        {
            inputs.Add(new CharacterMechanicInputView(
                "value",
                CharacterMechanicInputValueKinds.Integer,
                CharacterMechanicInputOrigins.CharacterState,
                Required: true,
                ParticipatesInValue: true,
                DefaultInteger: null,
                IncludeWhenBooleanInputKey: null,
                IncludeWhenBooleanValue: null));
        }
        else
        {
            inputs.AddRange(derivation.ComponentMechanicKeys.Select(value =>
                new CharacterMechanicInputView(
                    ConceptKeyFromCompetencyMechanicKey(value),
                    CharacterMechanicInputValueKinds.Integer,
                    CharacterMechanicInputOrigins.CharacterState,
                    Required: true,
                    ParticipatesInValue: true,
                    DefaultInteger: null,
                    IncludeWhenBooleanInputKey: null,
                    IncludeWhenBooleanValue: null)));
        }

        if (competency.SupportsRanks)
        {
            inputs.Add(new CharacterMechanicInputView(
                "ranks",
                CharacterMechanicInputValueKinds.Integer,
                CharacterMechanicInputOrigins.CharacterState,
                Required: false,
                ParticipatesInValue: false,
                DefaultInteger: null,
                IncludeWhenBooleanInputKey: null,
                IncludeWhenBooleanValue: null));
        }
        if (competency.SupportsClassSkillState)
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
        if (competency.SupportsTrainingState)
        {
            inputs.Add(new CharacterMechanicInputView(
                "trainingState",
                CharacterMechanicInputValueKinds.String,
                CharacterMechanicInputOrigins.CharacterState,
                Required: false,
                ParticipatesInValue: false,
                DefaultInteger: null,
                IncludeWhenBooleanInputKey: null,
                IncludeWhenBooleanValue: null));
        }
        if (competency.ArmorCheckPenaltyApplies == true
            || competency.Profiles.Any(value => value.ArmorCheckPenaltyApplies == true))
        {
            inputs.Add(new CharacterMechanicInputView(
                "armorCheckPenaltyAdjustment",
                CharacterMechanicInputValueKinds.Integer,
                CharacterMechanicInputOrigins.Derived,
                Required: false,
                ParticipatesInValue: false,
                DefaultInteger: null,
                IncludeWhenBooleanInputKey: null,
                IncludeWhenBooleanValue: null));
        }

        return inputs;
    }

    private static CharacterCompetencyDefinitionView BuildCompetencyDefinition(
        ResolvedRuleCatalogItemView rule,
        string? selectedMechanicalJson,
        IReadOnlyList<CharacterCompetencyProfileView> profiles)
    {
        var selectedProfile = ParseNormalizedCompetencyProfile(
            rule.SourceEntityRevisionId,
            selectedMechanicalJson);
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

        return new CharacterCompetencyDefinitionView(
            selectedProfile?.CompetencyKind ?? defaultKind,
            selectedProfile?.FamilyName,
            selectedProfile?.Specialty,
            selectedProfile?.GoverningAbilityKey,
            SupportsRanks: allProfiles.Any(value => value.SupportsRanks),
            SupportsClassSkillState: allProfiles.Any(value => value.SupportsClassSkillState),
            SupportsTrainingState: allProfiles.Length == 0
                || allProfiles.Any(value => value.SupportsTrainingState),
            TrainedOnly: selectedProfile?.TrainedOnly,
            ArmorCheckPenaltyApplies: selectedProfile?.ArmorCheckPenaltyApplies,
            Profiles: allProfiles);
    }

    private static CharacterCompetencyProfileView? ParseNormalizedCompetencyProfile(
        Guid sourceEntityRevisionId,
        string? mechanicalJson)
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

            return new CharacterCompetencyProfileView(
                sourceEntityRevisionId,
                profileKey,
                ReadStringArray(competency, "requiredCapabilityKeys"),
                kind,
                ReadString(competency, "familyName"),
                ReadString(competency, "specialty"),
                ReadString(competency, "governingAbilityKey"),
                ReadBoolean(competency, "supportsRanks") ?? false,
                ReadBoolean(competency, "supportsClassSkillState") ?? false,
                ReadBoolean(competency, "supportsTrainingState") ?? false,
                ReadBoolean(competency, "trainedOnly"),
                ReadBoolean(competency, "armorCheckPenaltyApplies"),
                ReadString(competency, "gameEdition"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

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
                    revision.raw_json
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
                var profile = ParseNormalizedCompetencyProfile(
                    revisionId,
                    string.IsNullOrWhiteSpace(contentJson) ? rawJson : contentJson);
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
                    .Select(group => group.First())
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
            value => value.GetMechanicalContentJson());
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

        page ??= new ResolvedRulesCatalogView("global", null, null, null, []);
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

        page ??= new ResolvedRulesCatalogView("campaign", campaignId, null, null, []);
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
        parameter.Value = value ?? DBNull.Value;
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

    private static string RequireMechanicKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Mechanic key can not be blank.", nameof(value));
        }
        var normalized = value.Trim();
        if (normalized.Length > 400)
        {
            throw new ArgumentException("Mechanic key can not exceed 400 characters.", nameof(value));
        }
        return normalized;
    }
}
