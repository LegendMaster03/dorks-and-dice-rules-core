using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

internal static class CharacterSupportCatalogBuilder
{
    public static async Task<CharacterSupportCatalogDefinition> BuildAsync(
        RulesCoreDbContext dbContext,
        ResolvedRulesCatalogView rules,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(rules);

        if (rules.Rules.Count == 0 || rules.RevisionNumber is null)
        {
            return Empty(rules);
        }

        var documents = await ReadEffectiveDocumentsAsync(
            dbContext,
            rules,
            cancellationToken);
        var attributions = await ReadAttributionsAsync(
            dbContext,
            rules.Rules,
            cancellationToken);
        var ruleByConceptKey = rules.Rules.ToDictionary(
            value => value.ConceptKey,
            value => value,
            StringComparer.OrdinalIgnoreCase);

        var recovery = new List<CharacterRecoveryProcedureDefinition>();
        var passive = new List<CharacterPassiveValueDefinition>();
        var qualifications = new List<CharacterQualificationDefinition>();

        foreach (var rule in rules.Rules)
        {
            if (!documents.TryGetValue(rule.RuleConceptId, out var document))
            {
                continue;
            }
            if (!TryGetCharacterSupport(document, out var support))
            {
                continue;
            }

            var sourceAttributions = attributions.GetValueOrDefault(
                rule.RuleConceptId,
                []);
            ParseRecoveryProcedures(
                support,
                rule,
                sourceAttributions,
                recovery);
            ParsePassiveValues(
                support,
                rule,
                sourceAttributions,
                ruleByConceptKey,
                passive);
            ParseQualifications(
                support,
                rule,
                sourceAttributions,
                ruleByConceptKey,
                qualifications);
        }

        EnsureUniqueKeys(
            recovery.Select(value => value.Key),
            "recovery procedure");
        EnsureUniqueKeys(
            passive.Select(value => value.Mechanic.Key),
            "passive value");
        EnsureUniqueKeys(
            qualifications.Select(value => value.Key),
            "qualification");

        return new CharacterSupportCatalogDefinition(
            rules.Scope,
            rules.CampaignId,
            rules.RevisionNumber,
            rules.PublishedAt,
            recovery,
            passive,
            qualifications);
    }

    private static CharacterSupportCatalogDefinition Empty(
        ResolvedRulesCatalogView rules) =>
        new(
            rules.Scope,
            rules.CampaignId,
            rules.RevisionNumber,
            rules.PublishedAt,
            [],
            [],
            []);

    private static async Task<IReadOnlyDictionary<Guid, JsonElement>> ReadEffectiveDocumentsAsync(
        RulesCoreDbContext dbContext,
        ResolvedRulesCatalogView rules,
        CancellationToken cancellationToken)
    {
        var conceptIds = rules.Rules
            .Select(value => value.RuleConceptId)
            .ToArray();
        if (conceptIds.Length == 0 || rules.RevisionNumber is null)
        {
            return new Dictionary<Guid, JsonElement>();
        }

        if (string.Equals(rules.Scope, "global", StringComparison.OrdinalIgnoreCase))
        {
            var revisionId = await dbContext.RulesetRevisions
                .AsNoTracking()
                .Where(value => value.RevisionNumber == rules.RevisionNumber.Value)
                .Select(value => value.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (revisionId == Guid.Empty)
            {
                return new Dictionary<Guid, JsonElement>();
            }

            var entries = await dbContext.RulesetRevisionEntries
                .AsNoTracking()
                .Include(value => value.SourceEntityRevision)
                .Include(value => value.GlobalRuleDecision)
                .Where(value => value.RulesetRevisionId == revisionId
                    && conceptIds.Contains(value.RuleConceptId))
                .ToArrayAsync(cancellationToken);

            return entries.ToDictionary(
                value => value.RuleConceptId,
                value =>
                {
                    using var source = JsonDocument.Parse(
                        value.SourceEntityRevision.GetMechanicalContentJson());
                    return ApplyGlobalDecision(
                        source.RootElement,
                        value.GlobalRuleDecision);
                });
        }

        if (!string.Equals(rules.Scope, "campaign", StringComparison.OrdinalIgnoreCase)
            || rules.CampaignId is null)
        {
            throw new InvalidOperationException(
                $"Unsupported Character support rules scope '{rules.Scope}'.");
        }

        var campaignRevisionId = await dbContext.CampaignRulesetRevisions
            .AsNoTracking()
            .Where(value => value.CampaignId == rules.CampaignId.Value
                && value.RevisionNumber == rules.RevisionNumber.Value)
            .Select(value => value.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (campaignRevisionId == Guid.Empty)
        {
            return new Dictionary<Guid, JsonElement>();
        }

        var campaignEntries = await dbContext.CampaignRulesetRevisionEntries
            .AsNoTracking()
            .Include(value => value.SourceEntityRevision)
            .Include(value => value.CampaignRuleDecision)
            .Include(value => value.BaselineRulesetRevisionEntry)
                .ThenInclude(value => value.GlobalRuleDecision)
            .Where(value => value.CampaignRulesetRevisionId == campaignRevisionId
                && conceptIds.Contains(value.RuleConceptId))
            .ToArrayAsync(cancellationToken);

        return campaignEntries.ToDictionary(
            value => value.RuleConceptId,
            value =>
            {
                using var source = JsonDocument.Parse(
                    value.SourceEntityRevision.GetMechanicalContentJson());
                var resolved = source.RootElement.Clone();
                if (value.CampaignRuleDecision?.DecisionKind
                    != CampaignRuleDecisionKinds.SelectSource)
                {
                    resolved = ApplyGlobalDecision(
                        resolved,
                        value.BaselineRulesetRevisionEntry.GlobalRuleDecision);
                }
                if (value.CampaignRuleDecision is not null)
                {
                    resolved = ApplyCampaignDecision(
                        resolved,
                        value.CampaignRuleDecision);
                }
                return resolved;
            });
    }

    private static JsonElement ApplyGlobalDecision(
        JsonElement source,
        GlobalRuleDecision decision) => decision.DecisionKind switch
    {
        RuleDecisionKinds.JsonMergePatch =>
            JsonMergePatch.Apply(source, decision.PatchJson),
        RuleDecisionKinds.JsonRulePatch =>
            JsonRulePatch.Apply(source, decision.PatchJson),
        _ => source.Clone()
    };

    private static JsonElement ApplyCampaignDecision(
        JsonElement source,
        CampaignRuleDecision decision) => decision.DecisionKind switch
    {
        CampaignRuleDecisionKinds.JsonMergePatch =>
            JsonMergePatch.Apply(source, decision.PatchJson),
        CampaignRuleDecisionKinds.JsonRulePatch =>
            JsonRulePatch.Apply(source, decision.PatchJson),
        _ => source.Clone()
    };

    private static bool TryGetCharacterSupport(
        JsonElement document,
        out JsonElement support)
    {
        support = default;
        return document.ValueKind == JsonValueKind.Object
            && document.TryGetProperty("_rulesCore", out var rulesCore)
            && rulesCore.ValueKind == JsonValueKind.Object
            && rulesCore.TryGetProperty("characterSupport", out support)
            && support.ValueKind == JsonValueKind.Object;
    }

    private static void ParseRecoveryProcedures(
        JsonElement support,
        ResolvedRuleCatalogItemView rule,
        IReadOnlyList<CharacterMechanicSourceAttributionView> attributions,
        ICollection<CharacterRecoveryProcedureDefinition> output)
    {
        if (!TryReadArray(support, "recoveryProcedures", out var procedures))
        {
            return;
        }

        foreach (var value in procedures.EnumerateArray())
        {
            RequireObject(value, "recovery procedure");
            var key = RequireString(value, "key", "Recovery procedure");
            var displayName = RequireString(value, "displayName", $"Recovery procedure '{key}'");
            var inputs = ReadInputs(value);
            var applicability = ReadApplicability(
                value,
                rule.PackageKey,
                inputs);
            var choices = ReadChoices(value);
            var rolls = ReadRolls(value);
            var effects = ReadEffects(value);
            var runtime = ReadRuntimeRequirements(
                value,
                applicability,
                inputs,
                choices,
                rolls,
                effects);

            output.Add(new CharacterRecoveryProcedureDefinition(
                key,
                displayName,
                ReadOptionalString(value, "presentationRole"),
                ReadOptionalBoolean(value, "available") ?? true,
                applicability,
                inputs,
                choices,
                rolls,
                effects,
                runtime,
                attributions));
        }
    }

    private static void ParsePassiveValues(
        JsonElement support,
        ResolvedRuleCatalogItemView rule,
        IReadOnlyList<CharacterMechanicSourceAttributionView> attributions,
        IReadOnlyDictionary<string, ResolvedRuleCatalogItemView> ruleByConceptKey,
        ICollection<CharacterPassiveValueDefinition> output)
    {
        if (!TryReadArray(support, "passiveValues", out var values))
        {
            return;
        }

        foreach (var value in values.EnumerateArray())
        {
            RequireObject(value, "passive value");
            var key = RequireString(value, "key", "Passive value");
            var displayName = RequireString(value, "displayName", $"Passive value '{key}'");
            var inputs = ReadInputs(value);
            var applicability = ReadApplicability(
                value,
                rule.PackageKey,
                inputs);
            var evaluationKind = ReadOptionalString(value, "evaluationKind")
                ?? CharacterMechanicEvaluationKinds.None;
            if (evaluationKind is not (
                    CharacterMechanicEvaluationKinds.None
                    or CharacterMechanicEvaluationKinds.Sum
                    or CharacterMechanicEvaluationKinds.SourceValue))
            {
                throw new InvalidDataException(
                    $"Passive value '{key}' uses unsupported evaluation kind '{evaluationKind}'.");
            }

            var relatedConceptKey = ReadOptionalString(value, "relatedConceptKey");
            Guid? relatedRuleConceptId = relatedConceptKey is not null
                && ruleByConceptKey.TryGetValue(relatedConceptKey, out var relatedRule)
                    ? relatedRule.RuleConceptId
                    : null;

            var mechanic = new CharacterMechanicDefinition(
                key,
                CharacterMechanicKinds.PassiveValue,
                displayName,
                evaluationKind,
                ReadOptionalInteger(value, "constant") ?? 0,
                inputs,
                TargetInputKey: null,
                BaseMechanicKey: ReadOptionalString(value, "relatedMechanicKey"),
                applicability,
                ConditionalRollRules: [],
                BooleanRequirements: [],
                Source: null);

            output.Add(new CharacterPassiveValueDefinition(
                mechanic,
                ReadOptionalBoolean(value, "available") ?? true,
                ReadOptionalString(value, "presentationRole"),
                ReadOptionalString(value, "relatedMechanicKey"),
                relatedConceptKey,
                relatedRuleConceptId,
                ReadOptionalString(value, "relatedAbilityKey"),
                attributions));
        }
    }

    private static void ParseQualifications(
        JsonElement support,
        ResolvedRuleCatalogItemView rule,
        IReadOnlyList<CharacterMechanicSourceAttributionView> attributions,
        IReadOnlyDictionary<string, ResolvedRuleCatalogItemView> ruleByConceptKey,
        ICollection<CharacterQualificationDefinition> output)
    {
        if (!TryReadArray(support, "qualifications", out var values))
        {
            return;
        }

        foreach (var value in values.EnumerateArray())
        {
            RequireObject(value, "qualification");
            var key = RequireString(value, "key", "Qualification");
            var displayName = RequireString(value, "displayName", $"Qualification '{key}'");
            var category = RequireString(value, "category", $"Qualification '{key}'");
            if (!value.TryGetProperty("stateInput", out var stateInputElement))
            {
                throw new InvalidDataException(
                    $"Qualification '{key}' must define stateInput.");
            }
            var stateInput = ReadInput(
                stateInputElement,
                $"Qualification '{key}' stateInput");
            var inputs = new[] { stateInput };
            var applicability = ReadApplicability(
                value,
                rule.PackageKey,
                inputs,
                defaultRequiresCharacterState: true);
            var associatedConceptKey = ReadOptionalString(
                value,
                "associatedConceptKey");
            Guid? associatedRuleConceptId = associatedConceptKey is not null
                && ruleByConceptKey.TryGetValue(associatedConceptKey, out var associatedRule)
                    ? associatedRule.RuleConceptId
                    : null;

            output.Add(new CharacterQualificationDefinition(
                key,
                displayName,
                category,
                ReadOptionalString(value, "family"),
                ReadOptionalBoolean(value, "available") ?? true,
                applicability,
                stateInput,
                associatedConceptKey,
                associatedRuleConceptId,
                attributions));
        }
    }

    private static CharacterMechanicApplicabilityDefinition ReadApplicability(
        JsonElement owner,
        string sourcePackageKey,
        IReadOnlyList<CharacterMechanicInputDefinition> inputs,
        bool defaultRequiresCharacterState = false)
    {
        var requiresCharacterState = defaultRequiresCharacterState
            || inputs.Any(value =>
                string.Equals(
                    value.Origin,
                    CharacterMechanicInputOrigins.CharacterState,
                    StringComparison.Ordinal));

        if (!owner.TryGetProperty("applicability", out var applicability))
        {
            return new CharacterMechanicApplicabilityDefinition(
                CharacterMechanicApplicabilityKinds.Always,
                requiresCharacterState,
                [],
                sourcePackageKey);
        }

        RequireObject(applicability, "character support applicability");
        var capabilities = ReadStringArray(
            applicability,
            "requiredCapabilityKeys");
        requiresCharacterState =
            ReadOptionalBoolean(applicability, "requiresCharacterState")
            ?? requiresCharacterState;
        var kind = ReadOptionalString(applicability, "kind")
            ?? (capabilities.Count > 0
                ? CharacterMechanicApplicabilityKinds.CharacterCapability
                : CharacterMechanicApplicabilityKinds.Always);

        return new CharacterMechanicApplicabilityDefinition(
            kind,
            requiresCharacterState,
            capabilities,
            sourcePackageKey);
    }

    private static IReadOnlyList<CharacterMechanicInputDefinition> ReadInputs(
        JsonElement owner)
    {
        if (!TryReadArray(owner, "inputs", out var values))
        {
            return [];
        }

        return values.EnumerateArray()
            .Select((value, index) => ReadInput(value, $"inputs[{index}]"))
            .ToArray();
    }

    private static CharacterMechanicInputDefinition ReadInput(
        JsonElement value,
        string context)
    {
        RequireObject(value, context);
        var key = RequireString(value, "key", context);
        var valueKind = RequireString(value, "valueKind", context);
        if (valueKind is not (
                CharacterMechanicInputValueKinds.Integer
                or CharacterMechanicInputValueKinds.Boolean
                or CharacterMechanicInputValueKinds.String))
        {
            throw new InvalidDataException(
                $"{context} '{key}' uses unsupported value kind '{valueKind}'.");
        }

        var origin = RequireString(value, "origin", context);
        if (origin is not (
                CharacterMechanicInputOrigins.CharacterState
                or CharacterMechanicInputOrigins.SourceInput
                or CharacterMechanicInputOrigins.Runtime
                or CharacterMechanicInputOrigins.Derived))
        {
            throw new InvalidDataException(
                $"{context} '{key}' uses unsupported input origin '{origin}'.");
        }

        var defaultInteger = ReadOptionalInteger(value, "defaultInteger");
        if (defaultInteger.HasValue
            && !string.Equals(
                valueKind,
                CharacterMechanicInputValueKinds.Integer,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{context} '{key}' defines defaultInteger for non-integer input.");
        }

        return new CharacterMechanicInputDefinition(
            key,
            valueKind,
            origin,
            ReadOptionalBoolean(value, "required") ?? false,
            ReadOptionalBoolean(value, "participatesInValue") ?? false,
            defaultInteger,
            ReadOptionalString(value, "includeWhenBooleanInputKey"),
            ReadOptionalBoolean(value, "includeWhenBooleanValue"));
    }

    private static IReadOnlyList<CharacterRecoveryChoiceDefinition> ReadChoices(
        JsonElement owner)
    {
        if (!TryReadArray(owner, "choices", out var values))
        {
            return [];
        }

        var choices = new List<CharacterRecoveryChoiceDefinition>();
        foreach (var value in values.EnumerateArray())
        {
            RequireObject(value, "recovery choice");
            var key = RequireString(value, "key", "Recovery choice");
            var prompt = RequireString(value, "prompt", $"Recovery choice '{key}'");
            if (!TryReadArray(value, "options", out var optionValues))
            {
                throw new InvalidDataException(
                    $"Recovery choice '{key}' must define options.");
            }

            var options = optionValues.EnumerateArray()
                .Select(option =>
                {
                    RequireObject(option, $"Recovery choice '{key}' option");
                    var optionKey = RequireString(
                        option,
                        "key",
                        $"Recovery choice '{key}' option");
                    return new CharacterRecoveryChoiceOptionDefinition(
                        optionKey,
                        ReadOptionalString(option, "displayName") ?? optionKey,
                        ReadOptionalString(option, "value"));
                })
                .ToArray();
            if (options.Length == 0)
            {
                throw new InvalidDataException(
                    $"Recovery choice '{key}' must define at least one option.");
            }
            EnsureUniqueKeys(
                options.Select(option => option.Key),
                $"recovery choice '{key}' option");

            choices.Add(new CharacterRecoveryChoiceDefinition(
                key,
                prompt,
                ReadOptionalBoolean(value, "required") ?? true,
                options));
        }

        EnsureUniqueKeys(choices.Select(value => value.Key), "recovery choice");
        return choices;
    }

    private static IReadOnlyList<CharacterRecoveryRollDefinition> ReadRolls(
        JsonElement owner)
    {
        if (!TryReadArray(owner, "rolls", out var values))
        {
            return [];
        }

        var rolls = values.EnumerateArray()
            .Select(value =>
            {
                RequireObject(value, "recovery roll");
                var key = RequireString(value, "key", "Recovery roll");
                return new CharacterRecoveryRollDefinition(
                    key,
                    RequireString(value, "rollKind", $"Recovery roll '{key}'"),
                    RequireString(value, "prompt", $"Recovery roll '{key}'"),
                    ReadOptionalBoolean(value, "required") ?? true,
                    ReadOptionalString(value, "mechanicKey"));
            })
            .ToArray();
        EnsureUniqueKeys(rolls.Select(value => value.Key), "recovery roll");
        return rolls;
    }

    private static IReadOnlyList<CharacterRecoveryEffectDefinition> ReadEffects(
        JsonElement owner)
    {
        if (!TryReadArray(owner, "effects", out var values))
        {
            return [];
        }

        var effects = values.EnumerateArray()
            .Select(value =>
            {
                RequireObject(value, "recovery effect");
                var key = RequireString(value, "key", "Recovery effect");
                var targetKey = ReadOptionalString(value, "targetKey");
                var targetChoiceKey = ReadOptionalString(value, "targetChoiceKey");
                if (targetKey is null && targetChoiceKey is null)
                {
                    throw new InvalidDataException(
                        $"Recovery effect '{key}' must define targetKey or targetChoiceKey.");
                }
                if (targetKey is not null && targetChoiceKey is not null)
                {
                    throw new InvalidDataException(
                        $"Recovery effect '{key}' can not define both targetKey and targetChoiceKey.");
                }

                var amount = ReadOptionalInteger(value, "amount");
                var amountInputKey = ReadOptionalString(value, "amountInputKey");
                var amountRollKey = ReadOptionalString(value, "amountRollKey");
                var amountSources = new object?[]
                {
                    amount,
                    amountInputKey,
                    amountRollKey
                }.Count(item => item is not null);
                if (amountSources > 1)
                {
                    throw new InvalidDataException(
                        $"Recovery effect '{key}' can define only one amount source.");
                }

                var literalValue = ReadOptionalString(value, "value");
                var valueChoiceKey = ReadOptionalString(value, "valueChoiceKey");
                if (literalValue is not null && valueChoiceKey is not null)
                {
                    throw new InvalidDataException(
                        $"Recovery effect '{key}' can not define both value and valueChoiceKey.");
                }

                return new CharacterRecoveryEffectDefinition(
                    key,
                    RequireString(value, "targetKind", $"Recovery effect '{key}'"),
                    targetKey,
                    targetChoiceKey,
                    RequireString(value, "operation", $"Recovery effect '{key}'"),
                    amount,
                    amountInputKey,
                    amountRollKey,
                    literalValue,
                    valueChoiceKey,
                    ReadOptionalString(value, "referenceKey"),
                    ReadEffectConditions(value, key));
            })
            .ToArray();
        EnsureUniqueKeys(effects.Select(value => value.Key), "recovery effect");
        return effects;
    }

    private static IReadOnlyList<CharacterRecoveryEffectConditionDefinition> ReadEffectConditions(
        JsonElement owner,
        string effectKey)
    {
        if (!TryReadArray(owner, "conditions", out var values))
        {
            return [];
        }

        return values.EnumerateArray()
            .Select(value =>
            {
                RequireObject(value, $"Recovery effect '{effectKey}' condition");
                var inputKey = ReadOptionalString(value, "inputKey");
                var choiceKey = ReadOptionalString(value, "choiceKey");
                if ((inputKey is null) == (choiceKey is null))
                {
                    throw new InvalidDataException(
                        $"Recovery effect '{effectKey}' condition must define exactly one of inputKey or choiceKey.");
                }

                var expectedInteger = ReadOptionalInteger(value, "expectedInteger");
                var expectedBoolean = ReadOptionalBoolean(value, "expectedBoolean");
                var expectedString = ReadOptionalString(value, "expectedString");
                var expectedChoiceValue = ReadOptionalString(
                    value,
                    "expectedChoiceValue");
                if (choiceKey is not null)
                {
                    if (expectedChoiceValue is null)
                    {
                        throw new InvalidDataException(
                            $"Recovery effect '{effectKey}' choice condition must define expectedChoiceValue.");
                    }
                    return new CharacterRecoveryEffectConditionDefinition(
                        null,
                        null,
                        null,
                        null,
                        choiceKey,
                        expectedChoiceValue);
                }

                var expectedCount = new object?[]
                {
                    expectedInteger,
                    expectedBoolean,
                    expectedString
                }.Count(item => item is not null);
                if (expectedCount != 1)
                {
                    throw new InvalidDataException(
                        $"Recovery effect '{effectKey}' input condition must define exactly one expected value.");
                }
                return new CharacterRecoveryEffectConditionDefinition(
                    inputKey,
                    expectedInteger,
                    expectedBoolean,
                    expectedString,
                    null,
                    null);
            })
            .ToArray();
    }

    private static CharacterRecoveryRuntimeRequirementsView ReadRuntimeRequirements(
        JsonElement owner,
        CharacterMechanicApplicabilityDefinition applicability,
        IReadOnlyList<CharacterMechanicInputDefinition> inputs,
        IReadOnlyList<CharacterRecoveryChoiceDefinition> choices,
        IReadOnlyList<CharacterRecoveryRollDefinition> rolls,
        IReadOnlyList<CharacterRecoveryEffectDefinition> effects)
    {
        JsonElement runtime = default;
        var hasRuntime = owner.TryGetProperty("runtimeRequirements", out runtime)
            && runtime.ValueKind == JsonValueKind.Object;

        bool ReadFlag(string key) =>
            hasRuntime && (ReadOptionalBoolean(runtime, key) ?? false);

        return new CharacterRecoveryRuntimeRequirementsView(
            applicability.RequiresCharacterState
                || inputs.Any(value => string.Equals(
                    value.Origin,
                    CharacterMechanicInputOrigins.CharacterState,
                    StringComparison.Ordinal))
                || ReadFlag("requiresCharacterState"),
            choices.Any(value => value.Required)
                || ReadFlag("requiresPlayerChoices"),
            rolls.Any(value => value.Required)
                || ReadFlag("requiresRolls"),
            effects.Any(value => string.Equals(
                    value.Operation,
                    "expend",
                    StringComparison.OrdinalIgnoreCase))
                || ReadFlag("requiresResourceExpenditure"),
            inputs.Any(value => string.Equals(
                    value.Origin,
                    CharacterMechanicInputOrigins.Runtime,
                    StringComparison.Ordinal))
                || ReadFlag("requiresOtherRuntimeFacts"));
    }

    private static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<CharacterMechanicSourceAttributionView>>> ReadAttributionsAsync(
        RulesCoreDbContext dbContext,
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        if (rules.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<CharacterMechanicSourceAttributionView>>();
        }

        var packageKeys = rules.Select(value => value.PackageKey).Distinct().ToArray();
        var providerByPackageKey = await dbContext.SourcePackages
            .AsNoTracking()
            .Where(value => packageKeys.Contains(value.Key))
            .ToDictionaryAsync(
                value => value.Key,
                value => value.Provider,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

        var revisionIds = rules
            .Select(value => value.SourceEntityRevisionId)
            .Distinct()
            .ToArray();
        var sourceUriByRevision = await dbContext.SourceEntityRevisions
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
        var publications = await ReadPublicationAttributionsAsync(
            dbContext,
            revisionIds,
            cancellationToken);

        return rules.ToDictionary(
            rule => rule.RuleConceptId,
            rule => BuildAttributions(
                rule,
                providerByPackageKey.GetValueOrDefault(rule.PackageKey)
                    ?? rule.PackageDisplayName,
                sourceUriByRevision.GetValueOrDefault(
                    rule.SourceEntityRevisionId),
                publications.GetValueOrDefault(
                    rule.SourceEntityRevisionId,
                    [])));
    }

    private static IReadOnlyList<CharacterMechanicSourceAttributionView> BuildAttributions(
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
                    ReferenceKey: rule.ConceptKey,
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
                ReferenceKey: rule.ConceptKey,
                ReferenceTitle: rule.SourceEntityName,
                ReferenceUri: sourceUri,
                PresentationRequired: false,
                ReferenceLinkRequired: false))
            .ToArray();
    }

    private static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<PublicationAttribution>>> ReadPublicationAttributionsAsync(
        RulesCoreDbContext dbContext,
        IReadOnlyList<Guid> revisionIds,
        CancellationToken cancellationToken)
    {
        if (revisionIds.Count == 0)
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
            AddParameter(command, "@revision_ids", revisionIds.ToArray());

            var values = new Dictionary<Guid, List<PublicationAttribution>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var revisionId = reader.GetGuid(0);
                if (!values.TryGetValue(revisionId, out var items))
                {
                    items = [];
                    values.Add(revisionId, items);
                }

                items.Add(new PublicationAttribution(
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5)
                        ? null
                        : reader.GetFieldValue<DateOnly>(5)));
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

    private static void AddParameter(
        DbCommand command,
        string name,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static bool TryReadArray(
        JsonElement owner,
        string propertyName,
        out JsonElement value)
    {
        value = default;
        if (!owner.TryGetProperty(propertyName, out var property))
        {
            return false;
        }
        if (property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                $"Character support property '{propertyName}' must be an array.");
        }
        value = property;
        return true;
    }

    private static void RequireObject(JsonElement value, string context)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{context} must be an object.");
        }
    }

    private static string RequireString(
        JsonElement owner,
        string propertyName,
        string context) =>
        ReadOptionalString(owner, propertyName)
        ?? throw new InvalidDataException(
            $"{context} must define non-blank string '{propertyName}'.");

    private static string? ReadOptionalString(
        JsonElement owner,
        string propertyName) =>
        owner.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    private static bool? ReadOptionalBoolean(
        JsonElement owner,
        string propertyName)
    {
        if (!owner.TryGetProperty(propertyName, out var value))
        {
            return null;
        }
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidDataException(
                $"Character support property '{propertyName}' must be boolean.")
        };
    }

    private static int? ReadOptionalInteger(
        JsonElement owner,
        string propertyName)
    {
        if (!owner.TryGetProperty(propertyName, out var value))
        {
            return null;
        }
        if (value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var number))
        {
            throw new InvalidDataException(
                $"Character support property '{propertyName}' must be a 32-bit integer.");
        }
        return number;
    }

    private static IReadOnlyList<string> ReadStringArray(
        JsonElement owner,
        string propertyName)
    {
        if (!owner.TryGetProperty(propertyName, out var value))
        {
            return [];
        }
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                $"Character support property '{propertyName}' must be an array.");
        }

        var result = value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(item.GetString())
                    ? item.GetString()!.Trim()
                    : throw new InvalidDataException(
                        $"Character support property '{propertyName}' contains a blank or non-string value."))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return result;
    }

    private static void EnsureUniqueKeys(
        IEnumerable<string> keys,
        string kind)
    {
        var duplicate = keys
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException(
                $"Effective Character support defines duplicate {kind} key '{duplicate.Key}'.");
        }
    }

    private sealed record PublicationAttribution(
        string WorkKey,
        string WorkDisplayName,
        string? GameEdition,
        string? ReleaseKind,
        DateOnly? PublicationDate);
}
