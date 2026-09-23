using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Evaluates already-built Character mechanics catalogs. Catalog construction, source
/// projection, and persistence remain separate concerns owned by the consumer/catalog modules.
/// </summary>
internal static class CharacterMechanicsCatalogEvaluator
{
    private const string AbilityContributionRole = "ability";
    private const string CompetencyContributionRole = "competency";

    internal static CharacterMechanicsBatchEvaluationView EvaluateBatchFromCatalog(
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
    
    
    internal static CharacterMechanicEvaluationView? EvaluateFromCatalog(
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
            var effectiveIntegerInputs = new Dictionary<string, int>(
                integerInputs,
                StringComparer.Ordinal);
            var effectiveStringInputs = new Dictionary<string, string>(
                stringInputs,
                StringComparer.Ordinal);
    
            if (known.Check?.CompetencyComposition is { } composition
                && request.Competency is { } competencyInput)
            {
                if (effectiveIntegerInputs.ContainsKey(composition.ContributionInputKey))
                {
                    throw new ArgumentException(
                        $"Mechanic '{mechanic.MechanicKey}' received both a direct competency contribution and a Rules Core competency composition request.");
                }
    
                var composed = EvaluateCompetencyForCheck(catalog, competencyInput);
                effectiveIntegerInputs[composition.ContributionInputKey] =
                    composed.CompetencyContribution;
    
                if (effectiveStringInputs.TryGetValue(
                        composition.ConceptKeyInputKey,
                        out var suppliedConceptKey)
                    && !string.Equals(
                        suppliedConceptKey,
                        composed.ConceptKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        $"Mechanic '{mechanic.MechanicKey}' competency concept '{suppliedConceptKey}' does not match composed competency '{composed.ConceptKey}'.");
                }
    
                effectiveStringInputs[composition.ConceptKeyInputKey] = composed.ConceptKey;
            }
    
            var evaluation = CharacterMechanicEvaluator.Evaluate(
                known,
                effectiveIntegerInputs,
                booleanInputs,
                effectiveStringInputs,
                MapContributorInputs(request.ContributorGroups));
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
                AppliedRollRules: [],
                ContributorGroups: []);
        }
    
        if (mechanic.Competency is null)
        {
            throw new InvalidOperationException(
                $"Competency mechanic '{mechanic.MechanicKey}' does not expose a competency profile.");
        }
    
        var selectedProfileRevisionId = request.CompetencyProfileSourceEntityRevisionId
            ?? mechanic.Competency.DefaultProfileSourceEntityRevisionId;
        if (!selectedProfileRevisionId.HasValue)
        {
            throw new InvalidOperationException(
                $"Competency mechanic '{mechanic.MechanicKey}' has no default evaluatable profile. Select an explicit competency profile.");
        }
    
        var profile = mechanic.Competency.Profiles.SingleOrDefault(value =>
            value.SourceEntityRevisionId == selectedProfileRevisionId.Value);
        if (profile is null)
        {
            throw new KeyNotFoundException(
                $"Competency profile source revision '{selectedProfileRevisionId}' is not available for mechanic '{mechanic.MechanicKey}'.");
        }
        if (!profile.CanEvaluate)
        {
            throw new InvalidOperationException(
                $"Competency profile '{profile.ProfileKey}' for mechanic '{mechanic.MechanicKey}' can not yet be evaluated faithfully.");
        }
    
        var missingProfileCapabilities = profile.RequiredCapabilityKeys
            .Where(value => !availableCapabilities.Contains(value))
            .ToArray();
        if (missingProfileCapabilities.Length > 0)
        {
            throw new InvalidOperationException(
                $"Competency profile '{profile.ProfileKey}' requires Character capability: {string.Join(", ", missingProfileCapabilities)}.");
        }
    
        var profileEvaluation = EvaluateCompetencyProfile(
            mechanic.MechanicKey,
            profile,
            integerInputs,
            booleanInputs,
            stringInputs,
            includeAbilityContribution: true);
        return new CharacterMechanicEvaluationView(
            mechanic.MechanicKey,
            CharacterMechanicEvaluationKinds.CompetencyProfile,
            profileEvaluation.Value,
            Target: null,
            MeetsTarget: null,
            profileEvaluation.RequirementsSatisfied,
            profileEvaluation.UnsatisfiedRequirementKeys,
            AppliedRollRules: [],
            ContributorGroups: [],
            CompetencyBreakdown: new CharacterCompetencyEvaluationBreakdownView(
                profileEvaluation.AbilityContribution,
                profileEvaluation.CompetencyContribution),
            CompetencyProfileSourceEntityRevisionId: profile.SourceEntityRevisionId);
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
                value.TargetMechanicKeys)).ToArray(),
            evaluation.ContributorGroups.Select(value => new CharacterMechanicContributorGroupEvaluationView(
                value.Key,
                value.ContributorCount,
                value.MaximumContributorCount,
                value.Value)).ToArray());
    
    
    private static (string ConceptKey, int CompetencyContribution) EvaluateCompetencyForCheck(
        CharacterMechanicsCatalogView catalog,
        CharacterMechanicCompetencyInput input) =>
        EvaluateCompetencyForCheck(
            catalog,
            input,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    
    private static (string ConceptKey, int CompetencyContribution) EvaluateCompetencyForCheck(
        CharacterMechanicsCatalogView catalog,
        CharacterMechanicCompetencyInput input,
        ISet<string> evaluationPath)
    {
        if (string.IsNullOrWhiteSpace(input.MechanicKey))
        {
            throw new ArgumentException("Composed competency mechanic key can not be blank.");
        }
    
        var normalizedMechanicKey = input.MechanicKey.Trim();
        if (!evaluationPath.Add(normalizedMechanicKey))
        {
            throw new InvalidOperationException(
                $"Competency composition cycle detected at '{normalizedMechanicKey}'.");
        }
    
        try
        {
            var mechanic = catalog.Mechanics.SingleOrDefault(value =>
                string.Equals(value.MechanicKey, normalizedMechanicKey, StringComparison.OrdinalIgnoreCase));
            if (mechanic is null
                || !mechanic.IsAvailableUnderRuleset
                || !string.Equals(mechanic.Kind, CharacterMechanicKinds.Competency, StringComparison.Ordinal))
            {
                throw new KeyNotFoundException(
                    $"Composed competency mechanic '{input.MechanicKey}' is not available.");
            }
            if (mechanic.Competency is null || mechanic.ConceptKey is null)
            {
                throw new InvalidOperationException(
                    $"Composed competency mechanic '{mechanic.MechanicKey}' does not expose competency semantics.");
            }
    
            if (string.Equals(
                    mechanic.EvaluationKind,
                    CharacterMechanicEvaluationKinds.CompositeCompetency,
                    StringComparison.Ordinal))
            {
                return EvaluateCompositeCompetencyForCheck(
                    catalog,
                    mechanic,
                    input,
                    evaluationPath);
            }
    
            if ((input.Components?.Count ?? 0) > 0)
            {
                throw new InvalidOperationException(
                    $"Competency '{mechanic.MechanicKey}' is not derived from components under the effective mechanical relationship.");
            }
            if ((input.Modifiers?.Count ?? 0) > 0)
            {
                throw new InvalidOperationException(
                    $"Relationship modifiers can only be supplied when competency '{mechanic.MechanicKey}' is effectively derived from components.");
            }
    
            var selectedRevisionId = input.CompetencyProfileSourceEntityRevisionId
                ?? mechanic.Competency.DefaultProfileSourceEntityRevisionId;
            if (!selectedRevisionId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Composed competency '{mechanic.MechanicKey}' has no default evaluatable profile.");
            }
    
            var profile = mechanic.Competency.Profiles.SingleOrDefault(value =>
                value.SourceEntityRevisionId == selectedRevisionId.Value);
            if (profile is null)
            {
                throw new KeyNotFoundException(
                    $"Competency profile source revision '{selectedRevisionId}' is not available for mechanic '{mechanic.MechanicKey}'.");
            }
            if (!profile.CanEvaluate)
            {
                throw new InvalidOperationException(
                    $"Competency profile '{profile.ProfileKey}' for mechanic '{mechanic.MechanicKey}' can not yet be evaluated faithfully.");
            }
    
            var availableCapabilities = (input.CapabilityKeys ?? [])
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingCapabilities = profile.RequiredCapabilityKeys
                .Where(value => !availableCapabilities.Contains(value))
                .ToArray();
            if (missingCapabilities.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Competency profile '{profile.ProfileKey}' requires Character capability: {string.Join(", ", missingCapabilities)}.");
            }
    
            var evaluation = EvaluateCompetencyProfile(
                mechanic.MechanicKey,
                profile,
                input.IntegerInputs ?? new Dictionary<string, int>(StringComparer.Ordinal),
                input.BooleanInputs ?? new Dictionary<string, bool>(StringComparer.Ordinal),
                input.StringInputs ?? new Dictionary<string, string>(StringComparer.Ordinal),
                includeAbilityContribution: false);
            if (!evaluation.RequirementsSatisfied)
            {
                throw new InvalidOperationException(
                    $"Competency profile '{profile.ProfileKey}' does not satisfy requirement(s): {string.Join(", ", evaluation.UnsatisfiedRequirementKeys)}.");
            }
    
            return (mechanic.ConceptKey, evaluation.CompetencyContribution);
        }
        finally
        {
            evaluationPath.Remove(normalizedMechanicKey);
        }
    }
    
    private static (string ConceptKey, int CompetencyContribution) EvaluateCompositeCompetencyForCheck(
        CharacterMechanicsCatalogView catalog,
        CharacterMechanicView mechanic,
        CharacterMechanicCompetencyInput input,
        ISet<string> evaluationPath)
    {
        var relationship = mechanic.Relationships.SingleOrDefault(value =>
            string.Equals(value.ParentMechanicKey, mechanic.MechanicKey, StringComparison.Ordinal)
            && string.Equals(value.Kind, MechanicalRelationshipKinds.CompositeSkill, StringComparison.Ordinal)
            && string.Equals(
                value.EffectiveResolutionKind,
                MechanicalRelationshipResolutionKinds.DeriveParent,
                StringComparison.Ordinal)
            && value.CanResolve)
            ?? throw new InvalidOperationException(
                $"Composite competency '{mechanic.MechanicKey}' does not have an effective derive-parent relationship.");
    
        var definition = KnownMechanicalRelationships.FindByKey(relationship.RelationshipKey)
            ?? throw new InvalidOperationException(
                $"Mechanical relationship '{relationship.RelationshipKey}' is not registered.");
        var suppliedComponents = input.Components ?? [];
        var duplicateComponentKeys = suppliedComponents
            .Where(value => !string.IsNullOrWhiteSpace(value.MechanicKey))
            .GroupBy(value => value.MechanicKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateComponentKeys.Length > 0)
        {
            throw new ArgumentException(
                $"Composite competency '{mechanic.MechanicKey}' received duplicate component input(s): {string.Join(", ", duplicateComponentKeys)}.");
        }
    
        var expectedMechanicKeys = definition.Components
            .ToDictionary(
                component => CompetencyMechanicKey(component.ConceptKey),
                component => component,
                StringComparer.OrdinalIgnoreCase);
        var unexpectedComponentKeys = suppliedComponents
            .Select(value => value.MechanicKey?.Trim())
            .Where(value => string.IsNullOrWhiteSpace(value)
                || !expectedMechanicKeys.ContainsKey(value))
            .Select(value => string.IsNullOrWhiteSpace(value) ? "<blank>" : value!)
            .ToArray();
        if (unexpectedComponentKeys.Length > 0)
        {
            throw new ArgumentException(
                $"Composite competency '{mechanic.MechanicKey}' received unknown component input(s): {string.Join(", ", unexpectedComponentKeys)}.");
        }
    
        var componentValues = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var component in definition.Components)
        {
            var componentMechanicKey = CompetencyMechanicKey(component.ConceptKey);
            var componentInput = suppliedComponents.SingleOrDefault(value =>
                string.Equals(
                    value.MechanicKey?.Trim(),
                    componentMechanicKey,
                    StringComparison.OrdinalIgnoreCase));
            if (componentInput is null)
            {
                throw new KeyNotFoundException(
                    $"Composite competency '{mechanic.MechanicKey}' requires component competency input '{componentMechanicKey}'.");
            }
    
            var componentEvaluation = EvaluateCompetencyForCheck(
                catalog,
                componentInput,
                evaluationPath);
            if (!string.Equals(
                    componentEvaluation.ConceptKey,
                    component.ConceptKey,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Composite competency '{mechanic.MechanicKey}' component '{componentMechanicKey}' resolved to unexpected concept '{componentEvaluation.ConceptKey}'.");
            }
    
            componentValues.Add(
                component.ConceptKey,
                componentEvaluation.CompetencyContribution);
        }
    
        var modifiers = (input.Modifiers ?? [])
            .Select(value => new CompetencyModifier(value.TargetConceptKey, value.Value))
            .ToArray();
        var evaluation = CompositeCompetencyEvaluator.Evaluate(
            definition,
            componentValues,
            modifiers);
        return (mechanic.ConceptKey!, evaluation.ParentValue);
    }
    
    
    private static IReadOnlyDictionary<string, IReadOnlyList<CharacterMechanicContributorInputValues>>
        MapContributorInputs(IReadOnlyList<CharacterMechanicContributorGroupInput>? contributorGroups)
    {
        if (contributorGroups is null || contributorGroups.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<CharacterMechanicContributorInputValues>>(
                StringComparer.OrdinalIgnoreCase);
        }
    
        var result = new Dictionary<string, IReadOnlyList<CharacterMechanicContributorInputValues>>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var group in contributorGroups)
        {
            if (string.IsNullOrWhiteSpace(group.GroupKey))
            {
                throw new ArgumentException("Contributor group key can not be blank.");
            }
            if (result.ContainsKey(group.GroupKey))
            {
                throw new ArgumentException(
                    $"Contributor group '{group.GroupKey}' was supplied more than once.");
            }
    
            result[group.GroupKey.Trim()] = (group.Contributors ?? [])
                .Select(value => new CharacterMechanicContributorInputValues(
                    value.IntegerInputs
                        ?? new Dictionary<string, int>(StringComparer.Ordinal),
                    value.BooleanInputs
                        ?? new Dictionary<string, bool>(StringComparer.Ordinal),
                    value.StringInputs
                        ?? new Dictionary<string, string>(StringComparer.Ordinal)))
                .ToArray();
        }
    
        return result;
    }
    
    
    private static (
        int Value,
        int AbilityContribution,
        int CompetencyContribution,
        bool RequirementsSatisfied,
        IReadOnlyList<string> UnsatisfiedRequirementKeys)
        EvaluateCompetencyProfile(
            string mechanicKey,
            CharacterCompetencyProfileView profile,
            IReadOnlyDictionary<string, int> integerInputs,
            IReadOnlyDictionary<string, bool> booleanInputs,
            IReadOnlyDictionary<string, string> stringInputs,
            bool includeAbilityContribution)
    {
        if (!string.Equals(profile.EvaluationKind, CharacterMechanicEvaluationKinds.Sum, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Competency profile '{profile.ProfileKey}' for mechanic '{mechanicKey}' does not use a supported evaluation kind.");
        }
    
        long total = 0;
        long abilityContribution = 0;
        long competencyContribution = 0;
        foreach (var input in profile.Inputs)
        {
            if (!InputIsActive(input, booleanInputs))
            {
                continue;
            }
    
            var isAbilityContribution = string.Equals(
                input.ContributionRole,
                AbilityContributionRole,
                StringComparison.Ordinal);
            if (isAbilityContribution && !includeAbilityContribution)
            {
                continue;
            }
    
            var supplied = input.ValueKind switch
            {
                CharacterMechanicInputValueKinds.Integer =>
                    integerInputs.ContainsKey(input.Key) || input.DefaultInteger.HasValue,
                CharacterMechanicInputValueKinds.Boolean =>
                    booleanInputs.ContainsKey(input.Key),
                CharacterMechanicInputValueKinds.String =>
                    stringInputs.TryGetValue(input.Key, out var stringValue)
                    && !string.IsNullOrWhiteSpace(stringValue),
                _ => throw new InvalidOperationException(
                    $"Competency profile '{profile.ProfileKey}' uses unknown input value kind '{input.ValueKind}'.")
            };
            if (input.Required && !supplied)
            {
                throw new KeyNotFoundException(
                    $"Competency mechanic '{mechanicKey}' requires {input.ValueKind} input '{input.Key}' for profile '{profile.ProfileKey}'.");
            }
    
            if (!input.ParticipatesInValue
                || !string.Equals(input.ValueKind, CharacterMechanicInputValueKinds.Integer, StringComparison.Ordinal))
            {
                continue;
            }
    
            var contribution = integerInputs.TryGetValue(input.Key, out var value)
                ? value
                : input.DefaultInteger ?? 0;
            total += contribution;
    
            if (isAbilityContribution)
            {
                abilityContribution += contribution;
            }
            else if (string.Equals(
                         input.ContributionRole,
                         CompetencyContributionRole,
                         StringComparison.Ordinal))
            {
                competencyContribution += contribution;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Competency profile '{profile.ProfileKey}' input '{input.Key}' participates in value without a recognized contribution role.");
            }
        }
    
        var unsatisfied = profile.BooleanRequirements
            .Where(requirement =>
                !booleanInputs.TryGetValue(requirement.InputKey, out var supplied)
                || supplied != requirement.ExpectedValue)
            .Select(requirement => requirement.InputKey)
            .ToArray();
        return (
            checked((int)total),
            checked((int)abilityContribution),
            checked((int)competencyContribution),
            unsatisfied.Length == 0,
            unsatisfied);
    }
    
    private static bool InputIsActive(
        CharacterMechanicInputView input,
        IReadOnlyDictionary<string, bool> booleanInputs)
    {
        if (string.IsNullOrWhiteSpace(input.IncludeWhenBooleanInputKey))
        {
            return true;
        }
    
        return input.IncludeWhenBooleanValue.HasValue
            && booleanInputs.TryGetValue(input.IncludeWhenBooleanInputKey, out var supplied)
            && supplied == input.IncludeWhenBooleanValue.Value;
    }
    
    
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