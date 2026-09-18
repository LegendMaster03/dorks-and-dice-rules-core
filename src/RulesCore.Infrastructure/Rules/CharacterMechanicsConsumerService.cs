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
        return await BuildCatalogAsync(rules, includeUnavailable, cancellationToken);
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

        var rules = await ReadAllCampaignRulesAsync(campaignId, userId.Trim(), cancellationToken);
        return await BuildCatalogAsync(rules, includeUnavailable, cancellationToken);
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

    private async Task<CharacterMechanicsCatalogView> BuildCatalogAsync(
        ResolvedRulesCatalogView rules,
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
        var competencyDocuments = await ReadMechanicalDocumentsAsync(
            rules.Rules.Where(IsCompetencyRule).ToArray(),
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
                publicationMetadata);
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
                value.BooleanInputKey,
                value.WhenValue,
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
        if (competency.ArmorCheckPenaltyApplies is not false)
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
        string? mechanicalJson,
        IReadOnlyList<PublicationAttribution> publications)
    {
        var specialty = ParseSpecialty(rule.DisplayName);
        var isTool = string.Equals(rule.EntityType, "tool", StringComparison.OrdinalIgnoreCase);
        var isThreeX = publications.Any(value =>
                string.Equals(value.GameEdition, "3e", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value.GameEdition, "3.5e", StringComparison.OrdinalIgnoreCase))
            || string.Equals(rule.SourceCode, "SRD3", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rule.SourceCode, "SRD35", StringComparison.OrdinalIgnoreCase);

        var governingAbility = ReadGoverningAbilityKey(mechanicalJson);
        var trainedOnly = ReadOptionalCompetencyBoolean(mechanicalJson, "trainedOnly");
        var useUntrained = ReadPcGenBooleanTag(mechanicalJson, "USEUNTRAINED");
        if (!trainedOnly.HasValue && useUntrained.HasValue)
        {
            trainedOnly = !useUntrained.Value;
        }
        var armorCheckPenaltyApplies = ReadOptionalCompetencyBoolean(
                mechanicalJson,
                "armorCheckPenaltyApplies")
            ?? ReadPcGenBooleanTag(mechanicalJson, "ACHECK");

        return new CharacterCompetencyDefinitionView(
            isTool
                ? CharacterCompetencyKinds.Tool
                : specialty.Specialty is null
                    ? CharacterCompetencyKinds.Skill
                    : CharacterCompetencyKinds.SpecializedSkill,
            specialty.FamilyName,
            specialty.Specialty,
            governingAbility,
            SupportsRanks: isThreeX && !isTool,
            SupportsClassSkillState: isThreeX && !isTool,
            SupportsTrainingState: true,
            TrainedOnly: trainedOnly,
            ArmorCheckPenaltyApplies: armorCheckPenaltyApplies);
    }

    private static (string? FamilyName, string? Specialty) ParseSpecialty(string displayName)
    {
        foreach (var family in new[] { "Craft", "Knowledge", "Perform", "Profession" })
        {
            var prefix = $"{family} (";
            if (displayName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && displayName.EndsWith(')')
                && displayName.Length > prefix.Length + 1)
            {
                var specialty = displayName[prefix.Length..^1].Trim();
                if (!string.IsNullOrWhiteSpace(specialty))
                {
                    return (family, specialty);
                }
            }
        }

        return (null, null);
    }

    private static string? ReadGoverningAbilityKey(string? mechanicalJson)
    {
        if (string.IsNullOrWhiteSpace(mechanicalJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(mechanicalJson);
            if (document.RootElement.TryGetProperty("ability", out var ability)
                && ability.ValueKind == JsonValueKind.String)
            {
                return NormalizeAbilityKey(ability.GetString());
            }

            if (document.RootElement.TryGetProperty("_rulesCore", out var rulesCore)
                && rulesCore.ValueKind == JsonValueKind.Object
                && rulesCore.TryGetProperty("pcgen", out var pcgen)
                && pcgen.ValueKind == JsonValueKind.Object
                && pcgen.TryGetProperty("unmappedSegments", out var segments)
                && segments.ValueKind == JsonValueKind.Array)
            {
                foreach (var segment in segments.EnumerateArray())
                {
                    if (segment.ValueKind == JsonValueKind.Object
                        && segment.TryGetProperty("tag", out var tag)
                        && tag.ValueKind == JsonValueKind.String
                        && string.Equals(tag.GetString(), "KEYSTAT", StringComparison.OrdinalIgnoreCase)
                        && segment.TryGetProperty("value", out var value)
                        && value.ValueKind == JsonValueKind.String)
                    {
                        return NormalizeAbilityKey(value.GetString());
                    }
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static bool? ReadOptionalCompetencyBoolean(string? mechanicalJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(mechanicalJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(mechanicalJson);
            if (TryReadBoolean(document.RootElement, propertyName, out var direct))
            {
                return direct;
            }

            if (document.RootElement.TryGetProperty("_rulesCore", out var rulesCore)
                && rulesCore.ValueKind == JsonValueKind.Object
                && rulesCore.TryGetProperty("competency", out var competency)
                && competency.ValueKind == JsonValueKind.Object
                && TryReadBoolean(competency, propertyName, out var normalized))
            {
                return normalized;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static bool? ReadPcGenBooleanTag(string? mechanicalJson, string tagName)
    {
        var value = ReadPcGenTagValue(mechanicalJson, tagName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (string.Equals(value, "YES", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (string.Equals(value, "NO", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "FALSE", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return null;
    }

    private static string? ReadPcGenTagValue(string? mechanicalJson, string tagName)
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
                || !rulesCore.TryGetProperty("pcgen", out var pcgen)
                || pcgen.ValueKind != JsonValueKind.Object
                || !pcgen.TryGetProperty("unmappedSegments", out var segments)
                || segments.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var segment in segments.EnumerateArray().Reverse())
            {
                if (segment.ValueKind == JsonValueKind.Object
                    && segment.TryGetProperty("tag", out var tag)
                    && tag.ValueKind == JsonValueKind.String
                    && string.Equals(tag.GetString(), tagName, StringComparison.OrdinalIgnoreCase)
                    && segment.TryGetProperty("value", out var value)
                    && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString()?.Trim();
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static bool TryReadBoolean(JsonElement value, string propertyName, out bool result)
    {
        result = false;
        if (!value.TryGetProperty(propertyName, out var property)
            || (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False))
        {
            return false;
        }

        result = property.GetBoolean();
        return true;
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
