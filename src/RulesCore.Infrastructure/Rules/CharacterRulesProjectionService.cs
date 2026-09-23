using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules.CharacterProjection;

namespace RulesCore.Infrastructure.Rules;

public sealed class CharacterRulesProjectionService(RulesCoreDbContext dbContext)
    : ICharacterRulesProjectionService
{
    private readonly CharacterResolvedRulesReader rulesReader =
        new(new ResolvedRulesCatalogService(dbContext));
    private readonly CharacterMechanicsConsumerService mechanics = new(dbContext);

    private static readonly IReadOnlyList<ICharacterRuleProjectionModule> Modules =
    [
        new RaceCharacterRuleProjectionModule(),
        new ClassCharacterRuleProjectionModule(),
        new ItemCharacterRuleProjectionModule(),
        new SpellCharacterRuleProjectionModule(),
        new GenericCharacterRuleProjectionModule()
    ];

    public async Task<CharacterRulesProjectionView> ResolveGlobalAsync(
        CharacterRulesProjectionRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rules = await rulesReader.ReadAllGlobalAsync(userId, cancellationToken);
        var mechanicCatalog = await mechanics.GetGlobalAsync(
            userId,
            includeUnavailable: true,
            cancellationToken);
        return Resolve(request, rules, mechanicCatalog);
    }

    public async Task<CharacterRulesProjectionView> ResolveCampaignAsync(
        Guid campaignId,
        CharacterRulesProjectionRequest request,
        string userId,
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
        ArgumentNullException.ThrowIfNull(request);

        var rules = await rulesReader.ReadAllCampaignAsync(campaignId, userId.Trim(), cancellationToken);
        var mechanicCatalog = await mechanics.GetCampaignAsync(
            campaignId,
            userId.Trim(),
            includeUnavailable: true,
            cancellationToken);
        return Resolve(request, rules, mechanicCatalog);
    }

    private CharacterRulesProjectionView Resolve(
        CharacterRulesProjectionRequest request,
        ResolvedRulesCatalogView rules,
        CharacterMechanicsCatalogView mechanicCatalog)
    {
        var context = new CharacterProjectionContext(request);
        CharacterCoreMechanicsResolver.SeedCallerCapabilities(context);
        CharacterProjectionCatalogRegistrar.RegisterMechanicCatalogIdentities(context, mechanicCatalog);

        var projectionRules = rules.Rules
            .Where(value => value.Document is not null)
            .Select(value => new CharacterProjectionRule(
                value,
                value.Document!.Value,
                CharacterProjectionResolutionHelpers.EffectiveProvenance(value)))
            .ToArray();

        foreach (var rule in projectionRules)
        {
            context.RegisterRuleIdentity(
                rule.Catalog.ConceptKey,
                rule.Catalog.DisplayName,
                rule.Catalog.EntityType);
            CharacterProjectionCatalogRegistrar.RegisterRuleMetadata(context, rule);
        }
        context.ResolveStartingClass();

        foreach (var rule in projectionRules)
        {
            foreach (var module in Modules)
            {
                if (module.Handles(rule, context))
                {
                    module.Project(rule, context);
                }
            }
        }

        CharacterCoreMechanicsResolver.Resolve(context, mechanicCatalog);
        ResolveSpellcastingMechanics(context);
        ResolveSpellcastingResources(context);
        ResolveHealthMechanics(context);
        ResolvePrerequisites(context);
        ResolveLegacyMechanicPlaceholders(context, mechanicCatalog);

        return new CharacterRulesProjectionView(
            rules.Scope,
            rules.CampaignId,
            rules.RevisionNumber,
            rules.PublishedAt,
            context.Mechanics.Values
                .Where(value => context.ShouldIncludeMechanic(value.MechanicKey))
                .OrderBy(value => value.Kind, StringComparer.Ordinal)
                .ThenBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.MechanicKey, StringComparer.Ordinal)
                .ToArray(),
            context.CapabilityViews.Values.OrderBy(value => value.CapabilityKey, StringComparer.Ordinal).ToArray(),
            context.Grants.OrderBy(value => value.GrantKey, StringComparer.Ordinal).ToArray(),
            context.Effects.OrderBy(value => value.EffectKey, StringComparer.Ordinal).ToArray(),
            context.Movement.Values.OrderBy(value => value.MovementKey, StringComparer.Ordinal).ToArray(),
            context.Qualifications.Values.OrderBy(value => value.QualificationKey, StringComparer.Ordinal).ToArray(),
            context.Actions.Values.OrderBy(value => value.ActionKey, StringComparer.Ordinal).ToArray(),
            context.Features.Values.OrderBy(value => value.FeatureKey, StringComparer.Ordinal).ToArray(),
            context.Resources.Values.OrderBy(value => value.ResourceKey, StringComparer.Ordinal).ToArray(),
            context.Spellcasting.Values.OrderBy(value => value.SpellcastingKey, StringComparer.Ordinal).ToArray(),
            context.Procedures.Values.OrderBy(value => value.ProcedureKey, StringComparer.Ordinal).ToArray(),
            context.ChoiceViews.Values.OrderBy(value => value.ChoiceKey, StringComparer.Ordinal).ToArray(),
            context.Prerequisites.Values.OrderBy(value => value.ConceptKey, StringComparer.Ordinal).ToArray(),
            context.Conflicts.OrderBy(value => value.ConflictKey, StringComparer.Ordinal).ToArray(),
            context.Equipment.Values.OrderBy(value => value.ItemKey, StringComparer.Ordinal).ToArray());
    }

    private static void ResolveSpellcastingMechanics(CharacterProjectionContext context)
    {
        if (!context.Spellcasting.Values.Any(value =>
                value.CastingAbilityKey is not null
                && !string.Equals(value.ResourceSystemKey, "spell-slots-3x", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        foreach (var system in context.Spellcasting.Values
                     .Where(value =>
                         value.CastingAbilityKey is not null
                         && !string.Equals(value.ResourceSystemKey, "spell-slots-3x", StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            var ability = CharacterProjectionJson.NormalizeAbilityKey(system.CastingAbilityKey!);
            if (!CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, $"ability.{ability}.modifier", out var abilityModifier)
                || !CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, "proficiency.standard", out var proficiency))
            {
                continue;
            }

            var dcKey = system.SaveDcMechanicKey ?? $"{system.SpellcastingKey}.save-dc";
            var attackKey = system.SpellAttackMechanicKey ?? $"{system.SpellcastingKey}.attack";
            context.Mechanics[dcKey] = new CharacterResolvedMechanicView(
                dcKey,
                "spell-save-dc",
                $"{system.DisplayName} Save DC",
                CharacterResolutionStates.Resolved,
                checked(8 + abilityModifier + proficiency),
                null,
                null,
                [],
                [],
                [],
                [],
                [
                    CharacterProjectionResolutionHelpers.Contribution("spellcasting.save-dc.base", "Base", 8),
                    CharacterProjectionResolutionHelpers.Contribution($"ability.{ability}.modifier", $"{CharacterProjectionJson.Humanize(ability)} modifier", abilityModifier),
                    CharacterProjectionResolutionHelpers.Contribution("proficiency.standard", "Proficiency bonus", proficiency)
                ],
                system.Provenance);
            context.Mechanics[attackKey] = new CharacterResolvedMechanicView(
                attackKey,
                "spell-attack",
                $"{system.DisplayName} Attack",
                CharacterResolutionStates.Resolved,
                checked(abilityModifier + proficiency),
                null,
                null,
                [],
                [],
                [],
                [],
                [
                    CharacterProjectionResolutionHelpers.Contribution($"ability.{ability}.modifier", $"{CharacterProjectionJson.Humanize(ability)} modifier", abilityModifier),
                    CharacterProjectionResolutionHelpers.Contribution("proficiency.standard", "Proficiency bonus", proficiency)
                ],
                system.Provenance);
        }
    }

    private static void ResolvePrerequisites(CharacterProjectionContext context)
    {
        foreach (var pair in context.Prerequisites.ToArray())
        {
            var conceptKey = pair.Key;
            var prerequisite = pair.Value;
            var evaluated = prerequisite.Requirements
                .Select(requirement => EvaluatePrerequisiteRequirement(context, requirement))
                .ToArray();

            var groupResults = evaluated
                .GroupBy(requirement => requirement.GroupKey ?? requirement.RequirementKey, StringComparer.Ordinal)
                .Select(group =>
                {
                    var required = Math.Max(
                        1,
                        group.Select(value => value.GroupMatchCount).DefaultIfEmpty(1).Max());
                    var satisfied = group.Count(value => value.Satisfied == true);
                    var unknown = group.Count(value => value.Satisfied is null);
                    bool? result = satisfied >= required
                        ? true
                        : satisfied + unknown < required
                            ? false
                            : null;
                    return result;
                })
                .ToArray();

            bool? overall = groupResults.Any(value => value == false)
                ? false
                : groupResults.All(value => value == true)
                    ? true
                    : null;
            var state = overall.HasValue
                ? CharacterResolutionStates.Resolved
                : CharacterResolutionStates.ApplicableUnresolved;

            var resolved = prerequisite with
            {
                State = state,
                Satisfied = overall,
                Requirements = evaluated
            };
            context.Prerequisites[conceptKey] = resolved;

            context.Qualifications[$"qualification.prerequisite.{conceptKey}"] =
                new CharacterQualificationView(
                    $"qualification.prerequisite.{conceptKey}",
                    "prerequisite",
                    $"{CharacterProjectionJson.Humanize(conceptKey)} Prerequisites",
                    overall,
                    state,
                    [],
                    prerequisite.Provenance);

            if (overall == false && context.IsSelected(conceptKey))
            {
                var conflictKey = $"conflict.prerequisite.{conceptKey}";
                if (!context.Conflicts.Any(value =>
                        string.Equals(value.ConflictKey, conflictKey, StringComparison.Ordinal)))
                {
                    context.Conflicts.Add(new CharacterProjectionConflictView(
                        conflictKey,
                        "prerequisite-unsatisfied",
                        $"The Character does not satisfy the effective prerequisites for selected concept '{conceptKey}'.",
                        [],
                        [conceptKey]));
                }
            }
        }
    }

    private static CharacterPrerequisiteRequirementView EvaluatePrerequisiteRequirement(
        CharacterProjectionContext context,
        CharacterPrerequisiteRequirementView requirement)
    {
        if (!string.Equals(requirement.Operator, ">=", StringComparison.Ordinal)
            || requirement.NumericValue is not int threshold)
        {
            return requirement with
            {
                Satisfied = null,
                State = CharacterResolutionStates.ApplicableUnresolved,
                Reason = "The normalized prerequisite uses an operator or value shape that the Character resolver does not yet evaluate."
            };
        }

        int actual;
        string actualDescription;
        switch (requirement.Kind)
        {
            case "ability-score":
                if (string.IsNullOrWhiteSpace(requirement.TargetKey)
                    || !CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, requirement.TargetKey, out actual))
                {
                    return requirement with
                    {
                        Satisfied = null,
                        State = CharacterResolutionStates.MissingCharacterInput,
                        Reason = $"Character value '{requirement.TargetKey ?? "ability score"}' is unavailable."
                    };
                }
                actualDescription = requirement.TargetKey;
                break;

            case "skill-ranks":
                if (!context.HasCompetencyRanksInput
                    || string.IsNullOrWhiteSpace(requirement.TextValue)
                    || !context.TryFindCompetencyRanks(
                        requirement.TextValue,
                        out actual,
                        out var competencyConceptKey))
                {
                    return requirement with
                    {
                        Satisfied = null,
                        State = CharacterResolutionStates.MissingCharacterInput,
                        Reason = $"Skill ranks for '{requirement.TextValue ?? "unknown skill"}' are unavailable."
                    };
                }
                actualDescription = competencyConceptKey ?? requirement.TextValue;
                break;

            case "class-level":
                if (string.IsNullOrWhiteSpace(requirement.TextValue)
                    || !context.TryFindAdvancementLevel(requirement.TextValue, out actual))
                {
                    return requirement with
                    {
                        Satisfied = null,
                        State = CharacterResolutionStates.MissingCharacterInput,
                        Reason = $"Advancement level for '{requirement.TextValue ?? "unknown class"}' is unavailable."
                    };
                }
                actualDescription = requirement.TextValue;
                break;

            default:
                return requirement with
                {
                    Satisfied = null,
                    State = CharacterResolutionStates.ApplicableUnresolved,
                    Reason = $"Prerequisite kind '{requirement.Kind}' is preserved but is not yet executable."
                };
        }

        var satisfied = actual >= threshold;
        return requirement with
        {
            Satisfied = satisfied,
            State = CharacterResolutionStates.Resolved,
            Reason = $"{actualDescription} is {actual}; requirement is >= {threshold}."
        };
    }

    private static void ResolveSpellcastingResources(CharacterProjectionContext context)
    {
        if (!context.Capabilities.Contains("spellcasting"))
        {
            context.Spellcasting.Remove("spellcasting.resource-choice");
            return;
        }

        ResolvePactMagicResources(context);

        if (!context.Capabilities.Contains("spellcasting.standard"))
        {
            context.Spellcasting.Remove("spellcasting.resource-choice");
            context.Resources.Remove("resource.spellcasting");
            return;
        }

        context.Spellcasting.TryGetValue(
            "spellcasting.resource-choice",
            out var resourceChoice);
        if (resourceChoice is not null
            && !string.Equals(
                resourceChoice.State,
                CharacterResolutionStates.Resolved,
                StringComparison.Ordinal))
        {
            context.Resources["resource.spellcasting"] = new CharacterResourceView(
                "resource.spellcasting",
                "Spellcasting Resource",
                resourceChoice.State,
                null,
                null,
                null,
                [],
                resourceChoice.Provenance);
            return;
        }

        var selectedSystem = resourceChoice?.ResourceSystemKey;
        var progressions = context.SpellSlotProgressions.Values
            .OrderBy(value => value.ConceptKey, StringComparer.Ordinal)
            .ToArray();

        if (progressions.Length == 0)
        {
            var missingResourceKey =
                string.Equals(
                    selectedSystem,
                    "spell-points",
                    StringComparison.OrdinalIgnoreCase)
                    ? "resource.spell-points"
                    : "resource.spell-slots";
            context.Resources[missingResourceKey] = new CharacterResourceView(
                missingResourceKey,
                string.Equals(
                    selectedSystem,
                    "spell-points",
                    StringComparison.OrdinalIgnoreCase)
                    ? "Spell Points"
                    : "Spell Slots",
                CharacterResolutionStates.ApplicableUnresolved,
                null,
                null,
                null,
                [],
                resourceChoice?.Provenance
                    ?? CharacterProjectionContext.EmptyProvenance());
            return;
        }

        if (string.Equals(selectedSystem, "spell-points", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryResolveSpellPointCasterLevel(
                    progressions,
                    out var spellPointCasterLevel,
                    out var spellPointCasterLevelContributions,
                    out var spellPointFailureReason)
                || !CharacterSpellPointRules.TryGetProgression(
                    spellPointCasterLevel,
                    out var spellPointProgression))
            {
                context.Resources["resource.spell-points"] = new CharacterResourceView(
                    "resource.spell-points",
                    "Spell Points",
                    CharacterResolutionStates.ApplicableUnresolved,
                    null,
                    null,
                    null,
                    [],
                    resourceChoice?.Provenance
                        ?? CharacterProjectionContext.EmptyProvenance());
                context.Conflicts.Add(new CharacterProjectionConflictView(
                    "conflict.spellcasting.spell-points",
                    "spell-point-progression",
                    spellPointFailureReason
                        ?? $"Effective spell-point caster level {spellPointCasterLevel} is outside the supported official progression.",
                    ["resource.spell-points"],
                    progressions.Select(value => value.ConceptKey).ToArray()));
                SetSpellcastingResourceSystem(
                    context,
                    "spell-points",
                    CharacterResolutionStates.ApplicableUnresolved);
                return;
            }

            var pointContributions = progressions
                .Select(progression => new CharacterMechanicContributionView(
                    $"{progression.ConceptKey}.spell-point-caster-level",
                    $"{progression.DisplayName} spell-point caster level",
                    CharacterEffectOperations.Add,
                    spellPointCasterLevelContributions.GetValueOrDefault(
                        progression.ConceptKey),
                    progression.CasterProgression,
                    progression.ConceptKey,
                    progression.Provenance))
                .Append(new CharacterMechanicContributionView(
                    $"spellcasting.spell-points.level-{spellPointProgression.CasterLevel}",
                    $"Spell-point table level {spellPointProgression.CasterLevel}",
                    CharacterEffectOperations.Set,
                    spellPointProgression.MaximumPoints,
                    $"maximum-slot-level:{spellPointProgression.MaximumSlotLevel}",
                    null,
                    CharacterProjectionContext.EmptyProvenance()))
                .ToArray();

            context.CurrentResources.TryGetValue(
                "resource.spell-points",
                out var currentPoints);
            context.Resources["resource.spell-points"] = new CharacterResourceView(
                "resource.spell-points",
                "Spell Points",
                CharacterResolutionStates.Resolved,
                context.CurrentResources.ContainsKey("resource.spell-points")
                    ? currentPoints
                    : null,
                spellPointProgression.MaximumPoints,
                null,
                pointContributions,
                resourceChoice?.Provenance
                    ?? CharacterProjectionContext.EmptyProvenance());

            context.Mechanics["spellcasting.spell-points.maximum-slot-level"] =
                new CharacterResolvedMechanicView(
                    "spellcasting.spell-points.maximum-slot-level",
                    "spellcasting",
                    "Maximum Spell-Point Slot Level",
                    CharacterResolutionStates.Resolved,
                    spellPointProgression.MaximumSlotLevel,
                    null,
                    "spell-level",
                    [],
                    [],
                    [],
                    [],
                    [
                        CharacterProjectionResolutionHelpers.Contribution(
                            $"spellcasting.spell-points.level-{spellPointProgression.CasterLevel}",
                            $"Spell-point table level {spellPointProgression.CasterLevel}",
                            spellPointProgression.MaximumSlotLevel)
                    ],
                    resourceChoice?.Provenance
                        ?? CharacterProjectionContext.EmptyProvenance());

            for (var spellLevel = 1;
                 spellLevel <= spellPointProgression.MaximumSlotLevel;
                 spellLevel++)
            {
                if (!CharacterSpellPointRules.TryGetSlotCost(
                        spellLevel,
                        out var pointCost))
                {
                    continue;
                }

                var costKey =
                    $"spellcasting.spell-points.slot-cost.level-{spellLevel}";
                context.Mechanics[costKey] = new CharacterResolvedMechanicView(
                    costKey,
                    "spellcasting",
                    $"{Ordinal(spellLevel)}-Level Slot Spell-Point Cost",
                    CharacterResolutionStates.Resolved,
                    pointCost,
                    null,
                    "spell-points",
                    [],
                    [],
                    [],
                    [],
                    [],
                    resourceChoice?.Provenance
                        ?? CharacterProjectionContext.EmptyProvenance());

                if (CharacterSpellPointRules.HasPerLongRestCreationLimit(spellLevel))
                {
                    var limitKey =
                        $"spellcasting.spell-points.slot-creation-limit.level-{spellLevel}";
                    context.Mechanics[limitKey] = new CharacterResolvedMechanicView(
                        limitKey,
                        "spellcasting",
                        $"{Ordinal(spellLevel)}-Level Spell-Point Slot Creation Limit",
                        CharacterResolutionStates.Resolved,
                        CharacterSpellPointRules.HighLevelSlotCreationLimit,
                        "per-long-rest",
                        "slot",
                        [],
                        [],
                        [],
                        [],
                        [],
                        resourceChoice?.Provenance
                            ?? CharacterProjectionContext.EmptyProvenance());
                }
            }

            SetSpellcastingResourceSystem(
                context,
                "spell-points",
                CharacterResolutionStates.Resolved);
            return;
        }

        if (selectedSystem is not null
            && !string.Equals(selectedSystem, "spell-slots", StringComparison.OrdinalIgnoreCase))
        {
            context.Resources["resource.spellcasting"] = new CharacterResourceView(
                "resource.spellcasting",
                "Spellcasting Resource",
                CharacterResolutionStates.ApplicableUnresolved,
                null,
                null,
                null,
                [],
                resourceChoice?.Provenance
                    ?? CharacterProjectionContext.EmptyProvenance());
            return;
        }

        IReadOnlyList<int> slotMaximums;
        CharacterSpellSlotProgression referenceProgression;
        int? effectiveCasterLevel = null;
        IReadOnlyDictionary<string, int>? casterLevelContributions = null;

        if (progressions.Length > 1)
        {
            if (!TryResolveMulticlassSpellSlots(
                    progressions,
                    out slotMaximums,
                    out referenceProgression,
                    out var combinedCasterLevel,
                    out var contributions,
                    out var failureReason))
            {
                context.Resources["resource.spell-slots"] = new CharacterResourceView(
                    "resource.spell-slots",
                    "Spell Slots",
                    CharacterResolutionStates.ApplicableUnresolved,
                    null,
                    null,
                    null,
                    [],
                    CharacterProjectionContext.EmptyProvenance());
                context.Conflicts.Add(new CharacterProjectionConflictView(
                    "conflict.spellcasting.multiclass-slots",
                    "multiclass-spell-slot-progression",
                    failureReason
                        ?? "The selected spellcasting progressions can not be combined from the effective source tables.",
                    ["resource.spell-slots"],
                    progressions.Select(value => value.ConceptKey).ToArray()));
                return;
            }

            effectiveCasterLevel = combinedCasterLevel;
            casterLevelContributions = contributions;
        }
        else
        {
            referenceProgression = progressions[0];
            slotMaximums = referenceProgression.SlotsBySpellLevel;
        }

        for (var index = 0; index < slotMaximums.Count; index++)
        {
            var maximum = slotMaximums[index];
            if (maximum <= 0)
            {
                continue;
            }

            var spellLevel = index + 1;
            var key = $"resource.spell-slot.{spellLevel}";
            context.CurrentResources.TryGetValue(key, out var current);

            var maximumContributions = new List<CharacterMechanicContributionView>();
            if (effectiveCasterLevel is int combinedLevel
                && casterLevelContributions is not null)
            {
                foreach (var progression in progressions)
                {
                    maximumContributions.Add(new CharacterMechanicContributionView(
                        $"{progression.ConceptKey}.effective-caster-level",
                        $"{progression.DisplayName} effective caster level",
                        CharacterEffectOperations.Add,
                        casterLevelContributions.GetValueOrDefault(progression.ConceptKey),
                        progression.CasterProgression,
                        progression.ConceptKey,
                        progression.Provenance));
                }

                maximumContributions.Add(new CharacterMechanicContributionView(
                    $"spellcasting.multiclass-slots.level-{spellLevel}",
                    $"Combined caster level {combinedLevel} slot table",
                    CharacterEffectOperations.Set,
                    maximum,
                    $"effective-caster-level:{combinedLevel}",
                    referenceProgression.ConceptKey,
                    referenceProgression.Provenance));
            }
            else
            {
                maximumContributions.Add(new CharacterMechanicContributionView(
                    $"{referenceProgression.ConceptKey}.spell-slots.level-{spellLevel}",
                    $"{referenceProgression.DisplayName} level {referenceProgression.ClassLevel} slot table",
                    CharacterEffectOperations.Set,
                    maximum,
                    referenceProgression.CasterProgression,
                    referenceProgression.ConceptKey,
                    referenceProgression.Provenance));
            }

            context.Resources[key] = new CharacterResourceView(
                key,
                $"{Ordinal(spellLevel)}-Level Spell Slots",
                CharacterResolutionStates.Resolved,
                context.CurrentResources.ContainsKey(key) ? current : null,
                maximum,
                null,
                maximumContributions,
                effectiveCasterLevel is null
                    ? referenceProgression.Provenance
                    : CharacterProjectionContext.EmptyProvenance());
        }

        SetSpellcastingResourceSystem(
            context,
            "spell-slots",
            CharacterResolutionStates.Resolved);
    }

    private static bool TryResolveSpellPointCasterLevel(
        IReadOnlyList<CharacterSpellSlotProgression> progressions,
        out int effectiveCasterLevel,
        out IReadOnlyDictionary<string, int> casterLevelContributions,
        out string? failureReason)
    {
        effectiveCasterLevel = 0;
        failureReason = null;
        var contributions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var progression in progressions)
        {
            if (!TryGetEffectiveCasterLevel(
                    progression.ClassLevel,
                    progression.CasterProgression,
                    out var contribution))
            {
                casterLevelContributions = contributions;
                failureReason =
                    $"Caster progression '{progression.CasterProgression ?? "unknown"}' from '{progression.DisplayName}' does not have a normalized spell-point weighting.";
                return false;
            }

            contributions[progression.ConceptKey] = contribution;
            effectiveCasterLevel = checked(effectiveCasterLevel + contribution);
        }

        casterLevelContributions = contributions;
        return true;
    }

    private static bool TryResolveMulticlassSpellSlots(
        IReadOnlyList<CharacterSpellSlotProgression> progressions,
        out IReadOnlyList<int> slots,
        out CharacterSpellSlotProgression referenceProgression,
        out int effectiveCasterLevel,
        out IReadOnlyDictionary<string, int> casterLevelContributions,
        out string? failureReason)
    {
        slots = [];
        referenceProgression = progressions[0];
        effectiveCasterLevel = 0;
        failureReason = null;
        var contributions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var progression in progressions)
        {
            if (!TryGetEffectiveCasterLevel(
                    progression.ClassLevel,
                    progression.CasterProgression,
                    out var contribution))
            {
                casterLevelContributions = contributions;
                failureReason =
                    $"Caster progression '{progression.CasterProgression ?? "unknown"}' from '{progression.DisplayName}' does not have normalized multiclass weighting.";
                return false;
            }

            contributions[progression.ConceptKey] = contribution;
            effectiveCasterLevel = checked(effectiveCasterLevel + contribution);
        }

        casterLevelContributions = contributions;
        if (effectiveCasterLevel <= 0)
        {
            slots = [];
            return true;
        }

        var candidates = new List<(CharacterSpellSlotProgression Progression, IReadOnlyList<int> Slots)>();
        foreach (var progression in progressions)
        {
            if (!TryGetSourceClassLevelForEffectiveCasterLevel(
                    effectiveCasterLevel,
                    progression.CasterProgression,
                    out var sourceClassLevel)
                || sourceClassLevel <= 0
                || sourceClassLevel > progression.SlotsByClassLevel.Count)
            {
                continue;
            }

            candidates.Add((
                progression,
                progression.SlotsByClassLevel[sourceClassLevel - 1]));
        }

        if (candidates.Count == 0)
        {
            failureReason =
                $"No selected source slot table can represent combined effective caster level {effectiveCasterLevel}.";
            return false;
        }

        var referenceCandidate = candidates
            .OrderByDescending(value => value.Slots.Count)
            .ThenBy(value => value.Progression.ConceptKey, StringComparer.Ordinal)
            .First();
        referenceProgression = referenceCandidate.Progression;
        var referenceSlots = referenceCandidate.Slots;
        var width = candidates.Max(value => value.Slots.Count);

        foreach (var candidate in candidates)
        {
            for (var index = 0; index < width; index++)
            {
                var expected = index < referenceSlots.Count ? referenceSlots[index] : 0;
                var actual = index < candidate.Slots.Count ? candidate.Slots[index] : 0;
                if (actual == expected)
                {
                    continue;
                }

                failureReason =
                    $"Selected source spell-slot tables disagree at combined effective caster level {effectiveCasterLevel}.";
                return false;
            }
        }

        slots = Enumerable.Range(0, width)
            .Select(index => index < referenceSlots.Count ? referenceSlots[index] : 0)
            .ToArray();
        return true;
    }

    private static bool TryGetEffectiveCasterLevel(
        int classLevel,
        string? casterProgression,
        out int effectiveLevel)
    {
        effectiveLevel = 0;
        if (classLevel < 0)
        {
            return false;
        }

        switch (casterProgression?.Trim().ToLowerInvariant())
        {
            case "full":
                effectiveLevel = classLevel;
                return true;
            case "1/2":
                effectiveLevel = classLevel / 2;
                return true;
            case "artificer":
                effectiveLevel = (classLevel + 1) / 2;
                return true;
            case "1/3":
                effectiveLevel = classLevel / 3;
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetSourceClassLevelForEffectiveCasterLevel(
        int effectiveCasterLevel,
        string? casterProgression,
        out int sourceClassLevel)
    {
        sourceClassLevel = 0;
        if (effectiveCasterLevel <= 0)
        {
            return false;
        }

        switch (casterProgression?.Trim().ToLowerInvariant())
        {
            case "full":
                sourceClassLevel = effectiveCasterLevel;
                return true;
            case "1/2":
                sourceClassLevel = checked(effectiveCasterLevel * 2);
                return true;
            case "artificer":
                sourceClassLevel = checked((effectiveCasterLevel * 2) - 1);
                return true;
            case "1/3":
                sourceClassLevel = checked(effectiveCasterLevel * 3);
                return true;
            default:
                return false;
        }
    }

    private static void ResolvePactMagicResources(CharacterProjectionContext context)
    {
        foreach (var progression in context.PactMagicProgressions.Values
                     .OrderBy(value => value.ConceptKey, StringComparer.Ordinal))
        {
            var key =
                $"resource.pact-slot.{progression.ConceptKey}.level-{progression.SlotLevel}";
            context.CurrentResources.TryGetValue(key, out var current);
            context.Resources[key] = new CharacterResourceView(
                key,
                $"{progression.DisplayName} {Ordinal(progression.SlotLevel)}-Level Pact Slots",
                CharacterResolutionStates.Resolved,
                context.CurrentResources.ContainsKey(key) ? current : null,
                progression.SlotCount,
                null,
                [new CharacterMechanicContributionView(
                    $"{progression.ConceptKey}.pact-slots.level-{progression.SlotLevel}",
                    $"{progression.DisplayName} level {progression.ClassLevel} Pact Magic table",
                    CharacterEffectOperations.Set,
                    progression.SlotCount,
                    $"slot-level:{progression.SlotLevel}",
                    progression.ConceptKey,
                    progression.Provenance)],
                progression.Provenance);

            var spellcastingKey = $"spellcasting.{progression.ConceptKey}";
            if (context.Spellcasting.TryGetValue(spellcastingKey, out var spellcasting))
            {
                context.Spellcasting[spellcastingKey] = spellcasting with
                {
                    State = CharacterResolutionStates.Resolved,
                    ResourceSystemKey = "pact-magic"
                };
            }
        }
    }

    private static void SetSpellcastingResourceSystem(
        CharacterProjectionContext context,
        string resourceSystemKey,
        string state)
    {
        foreach (var pair in context.Spellcasting
                     .Where(value =>
                         !string.Equals(
                             value.Key,
                             "spellcasting.resource-choice",
                             StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(
                             value.Value.ResourceSystemKey,
                             "pact-magic",
                             StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            context.Spellcasting[pair.Key] = pair.Value with
            {
                State = state,
                ResourceSystemKey = resourceSystemKey
            };
        }
    }

    private static string Ordinal(int value)
    {
        var mod100 = value % 100;
        if (mod100 is 11 or 12 or 13)
        {
            return $"{value}th";
        }

        return (value % 10) switch
        {
            1 => $"{value}st",
            2 => $"{value}nd",
            3 => $"{value}rd",
            _ => $"{value}th"
        };
    }

    private static void ResolveHealthMechanics(CharacterProjectionContext context)
    {
        const string key = "health.maximum-hp";
        var profiles = context.HitDice.Values
            .Where(value => value.ClassLevel > 0)
            .OrderBy(value => value.ConceptKey, StringComparer.Ordinal)
            .ToArray();
        if (profiles.Length == 0)
        {
            if (context.HitDice.Count > 0)
            {
                context.Mechanics[key] = CharacterProjectionResolutionHelpers.Unresolved(
                    key,
                    "health",
                    "Maximum HP",
                    CharacterResolutionStates.MissingCharacterInput,
                    ["advancement.levels"]);
            }
            return;
        }

        if (profiles.Any(value => value.Faces is null))
        {
            context.Mechanics[key] = CharacterProjectionResolutionHelpers.Unresolved(
                key,
                "health",
                "Maximum HP",
                CharacterResolutionStates.SourceUnavailable,
                provenance: CharacterProjectionContext.EmptyProvenance());
            return;
        }

        var suppliedByLevel =
            new Dictionary<(string ConceptKey, int ClassLevel), CharacterHitPointGainInput>();
        var hasConflict = false;

        foreach (var supplied in context.HitPointGains)
        {
            var conceptKey = supplied.ConceptKey?.Trim();
            var profile = string.IsNullOrWhiteSpace(conceptKey)
                ? null
                : profiles.FirstOrDefault(value => string.Equals(
                    value.ConceptKey,
                    conceptKey,
                    StringComparison.OrdinalIgnoreCase));
            if (profile is null
                || supplied.ClassLevel <= 0
                || supplied.ClassLevel > profile.ClassLevel)
            {
                hasConflict = true;
                context.Conflicts.Add(new CharacterProjectionConflictView(
                    $"conflict.health.hit-point-gain.{conceptKey ?? "unknown"}.level-{supplied.ClassLevel}",
                    "invalid-hit-point-gain",
                    "A supplied hit-point gain does not correspond to a selected class level.",
                    [key],
                    string.IsNullOrWhiteSpace(conceptKey) ? [] : [conceptKey]));
                continue;
            }

            var inputKey = HitPointGainInputKey(profile.ConceptKey, supplied.ClassLevel);
            if (supplied.HitDieValue <= 0 || supplied.HitDieValue > profile.Faces!.Value)
            {
                hasConflict = true;
                context.Conflicts.Add(new CharacterProjectionConflictView(
                    $"conflict.{inputKey}",
                    "invalid-hit-die-value",
                    $"Hit-die value {supplied.HitDieValue} for {profile.DisplayName} level {supplied.ClassLevel} must be between 1 and {profile.Faces.Value}.",
                    [key],
                    [profile.ConceptKey]));
                continue;
            }

            var identity = (profile.ConceptKey.ToUpperInvariant(), supplied.ClassLevel);
            if (!suppliedByLevel.TryAdd(identity, supplied))
            {
                hasConflict = true;
                context.Conflicts.Add(new CharacterProjectionConflictView(
                    $"conflict.{inputKey}.duplicate",
                    "duplicate-hit-point-gain",
                    $"More than one hit-point gain was supplied for {profile.DisplayName} level {supplied.ClassLevel}.",
                    [key],
                    [profile.ConceptKey]));
            }
        }

        if (hasConflict)
        {
            context.Mechanics[key] = CharacterProjectionResolutionHelpers.Unresolved(
                key,
                "health",
                "Maximum HP",
                CharacterResolutionStates.Conflict);
            return;
        }

        var missingInputs = new List<string>();
        var resolvedGains =
            new List<(CharacterHitDieProfile Profile, int Level, int HitDieValue)>();
        foreach (var profile in profiles)
        {
            for (var level = 1; level <= profile.ClassLevel; level++)
            {
                var inputKey = HitPointGainInputKey(profile.ConceptKey, level);
                var identity = (profile.ConceptKey.ToUpperInvariant(), level);
                if (suppliedByLevel.TryGetValue(identity, out var supplied))
                {
                    if (context.Rolls.ContainsKey(inputKey))
                    {
                        context.Conflicts.Add(new CharacterProjectionConflictView(
                            $"conflict.{inputKey}.duplicate",
                            "duplicate-hit-point-gain",
                            $"Both a resolved hit-point gain and a runtime roll were supplied for {profile.DisplayName} level {level}.",
                            [key],
                            [profile.ConceptKey]));
                        hasConflict = true;
                        continue;
                    }
                    resolvedGains.Add((profile, level, supplied.HitDieValue));
                    continue;
                }

                if (context.Rolls.TryGetValue(inputKey, out var rolled))
                {
                    if (rolled <= 0 || rolled > profile.Faces!.Value)
                    {
                        context.Conflicts.Add(new CharacterProjectionConflictView(
                            $"conflict.{inputKey}",
                            "invalid-hit-die-value",
                            $"Hit-die roll {rolled} for {profile.DisplayName} level {level} must be between 1 and {profile.Faces.Value}.",
                            [key],
                            [profile.ConceptKey]));
                        hasConflict = true;
                        continue;
                    }
                    resolvedGains.Add((profile, level, rolled));
                    continue;
                }

                missingInputs.Add(inputKey);
            }
        }

        if (hasConflict)
        {
            context.Mechanics[key] = CharacterProjectionResolutionHelpers.Unresolved(
                key,
                "health",
                "Maximum HP",
                CharacterResolutionStates.Conflict);
            return;
        }

        if (!CharacterProjectionResolutionHelpers.TryResolvedNumeric(context, "ability.constitution.modifier", out var constitutionModifier))
        {
            missingInputs.Add("ability.constitution.base");
        }

        if (missingInputs.Count > 0)
        {
            context.Mechanics[key] = CharacterProjectionResolutionHelpers.Unresolved(
                key,
                "health",
                "Maximum HP",
                CharacterResolutionStates.MissingCharacterInput,
                missingInputs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            return;
        }

        var contributions = new List<CharacterMechanicContributionView>();
        long total = 0;
        foreach (var gain in resolvedGains)
        {
            var effectiveGain = StandardDndCharacterMath.HitPointGain(
                gain.HitDieValue,
                constitutionModifier);
            total = checked(total + effectiveGain);
            contributions.Add(new CharacterMechanicContributionView(
                HitPointGainInputKey(gain.Profile.ConceptKey, gain.Level),
                $"{gain.Profile.DisplayName} level {gain.Level} hit points",
                CharacterEffectOperations.Add,
                effectiveGain,
                $"hit die {gain.HitDieValue}; Constitution modifier {constitutionModifier}",
                gain.Profile.ConceptKey,
                gain.Profile.Provenance));
        }

        context.Mechanics[key] = new CharacterResolvedMechanicView(
            key,
            "health",
            "Maximum HP",
            CharacterResolutionStates.Resolved,
            checked((int)total),
            null,
            "hit points",
            [],
            [],
            [],
            [],
            contributions,
            CharacterProjectionContext.EmptyProvenance());
    }

    private static string HitPointGainInputKey(string conceptKey, int classLevel) =>
        $"health.hit-point-gain.{conceptKey}.level-{classLevel}";

    private static void ResolveLegacyMechanicPlaceholders(
        CharacterProjectionContext context,
        CharacterMechanicsCatalogView catalog)
    {
        foreach (var mechanic in catalog.Mechanics.Where(value =>
                     value.Competency is null
                     && value.IsAvailableUnderRuleset))
        {
            if (context.Mechanics.ContainsKey(mechanic.MechanicKey))
            {
                continue;
            }

            var requiredCapabilities = mechanic.Applicability.RequiredCapabilityKeys
                .Where(value => !context.Capabilities.Contains(value))
                .ToArray();
            if (requiredCapabilities.Length > 0)
            {
                continue;
            }

            if (mechanic.MechanicKey is "save.fortitude" or "save.reflex" or "save.will"
                or "combat.base-attack-bonus" or "combat.grapple"
                or "defense.ac.touch" or "defense.ac.flat-footed"
                or "defense.damage-reduction" or "defense.spell-resistance"
                or "resource.nonlethal-damage")
            {
                context.Mechanics[mechanic.MechanicKey] = CharacterProjectionResolutionHelpers.Unresolved(
                    mechanic.MechanicKey,
                    mechanic.Kind,
                    mechanic.DisplayName,
                    CharacterResolutionStates.ApplicableUnresolved,
                    provenance: mechanic.Provenance ?? CharacterProjectionResolutionHelpers.Provenance(mechanic.SourceAttributions));
            }
        }
    }

}