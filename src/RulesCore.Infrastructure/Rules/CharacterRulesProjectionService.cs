using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules.CharacterProjection;

namespace RulesCore.Infrastructure.Rules;

public sealed class CharacterRulesProjectionService(RulesCoreDbContext dbContext)
    : ICharacterRulesProjectionService
{
    private const int PageSize = 500;
    private static readonly string[] StandardAbilities =
    [
        "strength",
        "dexterity",
        "constitution",
        "intelligence",
        "wisdom",
        "charisma"
    ];

    private readonly ResolvedRulesCatalogService resolvedRules = new(dbContext);
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
        var rules = await ReadAllGlobalRulesAsync(userId, cancellationToken);
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

        var rules = await ReadAllCampaignRulesAsync(campaignId, userId.Trim(), cancellationToken);
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
        SeedCallerCapabilities(context);

        var projectionRules = rules.Rules
            .Where(value => value.Document is not null)
            .Select(value => new CharacterProjectionRule(
                value,
                value.Document!.Value,
                EffectiveProvenance(value)))
            .ToArray();

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

        ResolveCoreMechanics(context, mechanicCatalog);
        ResolveSpellcastingMechanics(context);
        ResolveHealthMechanics(context);
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
            context.Prerequisites.Values.OrderBy(value => value.ConceptKey, StringComparer.Ordinal).ToArray(),
            context.Conflicts.OrderBy(value => value.ConflictKey, StringComparer.Ordinal).ToArray());
    }

    private static void SeedCallerCapabilities(CharacterProjectionContext context)
    {
        foreach (var capability in context.Capabilities.ToArray())
        {
            context.CapabilityViews[capability] = new CharacterCapabilityView(
                capability,
                CharacterProjectionJson.Humanize(capability),
                [],
                CharacterProjectionContext.EmptyProvenance());
        }
    }

    private static void ResolveCoreMechanics(
        CharacterProjectionContext context,
        CharacterMechanicsCatalogView mechanicCatalog)
    {
        ResolveAbilities(context);
        ResolveProficiency(context);
        ResolveInitiative(context);
        ResolveAbilitySavingThrows(context);
        ResolveCompetencies(context, mechanicCatalog);
    }

    private static void ResolveAbilities(CharacterProjectionContext context)
    {
        foreach (var ability in StandardAbilities)
        {
            var baseKey = $"ability.{ability}.base";
            var scoreKey = $"ability.{ability}.score";
            var modifierKey = $"ability.{ability}.modifier";
            var contributions = context.AbilityContributions.GetValueOrDefault(ability) ?? [];
            var hasBase = context.BaseAbilityScores.TryGetValue(ability, out var baseScore)
                || context.BaseAbilityScores.TryGetValue(baseKey, out baseScore)
                || context.IntegerFacts.TryGetValue(baseKey, out baseScore);
            var requiredChoices = context.RequiredAbilityChoices.OrderBy(value => value, StringComparer.Ordinal).ToArray();

            if (!hasBase)
            {
                context.Mechanics[scoreKey] = Unresolved(
                    scoreKey,
                    "ability-score",
                    $"{CharacterProjectionJson.Humanize(ability)} Score",
                    CharacterResolutionStates.MissingCharacterInput,
                    [baseKey],
                    requiredChoices: requiredChoices);
                context.Mechanics[modifierKey] = Unresolved(
                    modifierKey,
                    "ability-modifier",
                    $"{CharacterProjectionJson.Humanize(ability)} Modifier",
                    CharacterResolutionStates.MissingCharacterInput,
                    [baseKey],
                    requiredChoices: requiredChoices);
                continue;
            }

            long total = baseScore;
            var breakdown = new List<CharacterMechanicContributionView>
            {
                new(
                    baseKey,
                    "Base score",
                    CharacterEffectOperations.Set,
                    baseScore,
                    null,
                    null,
                    CharacterProjectionContext.EmptyProvenance())
            };
            foreach (var contribution in contributions)
            {
                if (contribution.NumericValue is int value)
                {
                    total += value;
                    breakdown.Add(contribution);
                }
            }

            if (requiredChoices.Length > 0)
            {
                context.Mechanics[scoreKey] = new CharacterResolvedMechanicView(
                    scoreKey,
                    "ability-score",
                    $"{CharacterProjectionJson.Humanize(ability)} Score",
                    CharacterResolutionStates.ChoiceRequired,
                    null,
                    null,
                    null,
                    [],
                    [],
                    requiredChoices,
                    [],
                    breakdown,
                    CharacterProjectionContext.EmptyProvenance());
                context.Mechanics[modifierKey] = Unresolved(
                    modifierKey,
                    "ability-modifier",
                    $"{CharacterProjectionJson.Humanize(ability)} Modifier",
                    CharacterResolutionStates.ChoiceRequired,
                    requiredChoices: requiredChoices);
                continue;
            }

            var effective = checked((int)total);
            var modifier = StandardDndCharacterMath.AbilityModifier(effective);
            context.Mechanics[scoreKey] = new CharacterResolvedMechanicView(
                scoreKey,
                "ability-score",
                $"{CharacterProjectionJson.Humanize(ability)} Score",
                CharacterResolutionStates.Resolved,
                effective,
                null,
                null,
                [],
                [],
                [],
                [],
                breakdown,
                CharacterProjectionContext.EmptyProvenance());
            context.Mechanics[modifierKey] = new CharacterResolvedMechanicView(
                modifierKey,
                "ability-modifier",
                $"{CharacterProjectionJson.Humanize(ability)} Modifier",
                CharacterResolutionStates.Resolved,
                modifier,
                null,
                null,
                [],
                [],
                [],
                [],
                [new CharacterMechanicContributionView(
                    scoreKey,
                    "Effective ability score",
                    CharacterEffectOperations.Set,
                    effective,
                    null,
                    null,
                    CharacterProjectionContext.EmptyProvenance())],
                CharacterProjectionContext.EmptyProvenance());
        }
    }

    private static void ResolveProficiency(CharacterProjectionContext context)
    {
        if (!context.UsesStandardProficiency)
        {
            return;
        }

        const string key = "proficiency.standard";
        if (context.StandardProficiencyLevel <= 0)
        {
            context.Mechanics[key] = Unresolved(
                key,
                "proficiency",
                "Proficiency Bonus",
                CharacterResolutionStates.MissingCharacterInput,
                ["advancement.levels"]);
            return;
        }

        var value = StandardDndCharacterMath.ProficiencyBonusForCharacterLevel(
            context.StandardProficiencyLevel);
        context.Mechanics[key] = new CharacterResolvedMechanicView(
            key,
            "proficiency",
            "Proficiency Bonus",
            CharacterResolutionStates.Resolved,
            value,
            null,
            null,
            [],
            [],
            [],
            [],
            [new CharacterMechanicContributionView(
                "advancement.levels",
                "Character level",
                CharacterEffectOperations.Set,
                context.StandardProficiencyLevel,
                null,
                null,
                CharacterProjectionContext.EmptyProvenance())],
            CharacterProjectionContext.EmptyProvenance());
        context.Capabilities.Add("proficiency.standard");
    }

    private static void ResolveInitiative(CharacterProjectionContext context)
    {
        const string key = "combat.initiative";
        if (!TryResolvedNumeric(context, "ability.dexterity.modifier", out var dexterity))
        {
            context.Mechanics[key] = Unresolved(
                key,
                "combat-value",
                "Initiative",
                CharacterResolutionStates.MissingCharacterInput,
                ["ability.dexterity.base"]);
            return;
        }

        var other = context.IntegerFacts.GetValueOrDefault("combat.initiative.other");
        context.Mechanics[key] = new CharacterResolvedMechanicView(
            key,
            "combat-value",
            "Initiative",
            CharacterResolutionStates.Resolved,
            checked(dexterity + other),
            null,
            null,
            [],
            [],
            [],
            [],
            [
                Contribution("ability.dexterity.modifier", "Dexterity modifier", dexterity),
                Contribution("combat.initiative.other", "Other initiative modifiers", other)
            ],
            CharacterProjectionContext.EmptyProvenance());
    }

    private static void ResolveAbilitySavingThrows(CharacterProjectionContext context)
    {
        if (!context.UsesStandardProficiency)
        {
            return;
        }

        var proficiencyResolved = TryResolvedNumeric(context, "proficiency.standard", out var proficiency);
        foreach (var ability in StandardAbilities)
        {
            var key = $"save.{ability}";
            if (!TryResolvedNumeric(context, $"ability.{ability}.modifier", out var modifier))
            {
                context.Mechanics[key] = Unresolved(
                    key,
                    "saving-throw",
                    $"{CharacterProjectionJson.Humanize(ability)} Saving Throw",
                    CharacterResolutionStates.MissingCharacterInput,
                    [$"ability.{ability}.base"]);
                continue;
            }

            var proficient = context.SaveProficiencyAbilities.Contains(ability);
            if (proficient && !proficiencyResolved)
            {
                context.Mechanics[key] = Unresolved(
                    key,
                    "saving-throw",
                    $"{CharacterProjectionJson.Humanize(ability)} Saving Throw",
                    CharacterResolutionStates.MissingCharacterInput,
                    ["advancement.levels"]);
                continue;
            }

            var other = context.IntegerFacts.GetValueOrDefault($"{key}.other");
            var value = checked(modifier + (proficient ? proficiency : 0) + other);
            var contributions = new List<CharacterMechanicContributionView>
            {
                Contribution($"ability.{ability}.modifier", $"{CharacterProjectionJson.Humanize(ability)} modifier", modifier)
            };
            if (proficient)
            {
                contributions.Add(Contribution("proficiency.standard", "Proficiency bonus", proficiency));
            }
            if (other != 0)
            {
                contributions.Add(Contribution($"{key}.other", "Other modifiers", other));
            }

            context.Mechanics[key] = new CharacterResolvedMechanicView(
                key,
                "saving-throw",
                $"{CharacterProjectionJson.Humanize(ability)} Saving Throw",
                CharacterResolutionStates.Resolved,
                value,
                null,
                null,
                [],
                [],
                [],
                [],
                contributions,
                CharacterProjectionContext.EmptyProvenance());
        }
    }

    private static void ResolveCompetencies(
        CharacterProjectionContext context,
        CharacterMechanicsCatalogView catalog)
    {
        var projected = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var competencyMechanics = catalog.Mechanics
            .Where(value => value.Competency is not null)
            .OrderBy(value => value.MechanicKey, StringComparer.Ordinal)
            .ToArray();

        foreach (var mechanic in competencyMechanics)
        {
            var competency = mechanic.Competency!;
            var profile = SelectProfile(competency, context);
            if (profile is null)
            {
                context.Mechanics[mechanic.MechanicKey] = Unresolved(
                    mechanic.MechanicKey,
                    "competency",
                    mechanic.DisplayName,
                    CharacterResolutionStates.MissingCapability,
                    missingCapabilities: competency.Profiles
                        .SelectMany(value => value.RequiredCapabilityKeys)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray(),
                    provenance: mechanic.Provenance ?? mechanic.Provenance ?? Provenance(mechanic.SourceAttributions));
                continue;
            }

            if (!profile.CanEvaluate || string.IsNullOrWhiteSpace(profile.GoverningAbilityKey))
            {
                context.Mechanics[mechanic.MechanicKey] = Unresolved(
                    mechanic.MechanicKey,
                    "competency",
                    mechanic.DisplayName,
                    CharacterResolutionStates.ApplicableUnresolved,
                    provenance: mechanic.Provenance ?? mechanic.Provenance ?? Provenance(mechanic.SourceAttributions));
                continue;
            }

            var ability = CharacterProjectionJson.NormalizeAbilityKey(profile.GoverningAbilityKey);
            if (!TryResolvedNumeric(context, $"ability.{ability}.modifier", out var abilityModifier))
            {
                context.Mechanics[mechanic.MechanicKey] = Unresolved(
                    mechanic.MechanicKey,
                    "competency",
                    mechanic.DisplayName,
                    CharacterResolutionStates.MissingCharacterInput,
                    [$"ability.{ability}.base"],
                    provenance: mechanic.Provenance ?? mechanic.Provenance ?? Provenance(mechanic.SourceAttributions));
                continue;
            }

            var contributions = new List<CharacterMechanicContributionView>
            {
                Contribution($"ability.{ability}.modifier", $"{CharacterProjectionJson.Humanize(ability)} modifier", abilityModifier)
            };
            var total = abilityModifier;

            if (profile.SupportsRanks)
            {
                var ranks = context.CompetencyRanks.GetValueOrDefault(competency.ConceptKey);
                total = checked(total + ranks);
                contributions.Add(Contribution(
                    $"{competency.ConceptKey}.ranks",
                    "Ranks",
                    ranks));
            }

            if (profile.SupportsTrainingState && context.TrainingKeys.Contains(competency.ConceptKey))
            {
                if (!TryResolvedNumeric(context, "proficiency.standard", out var proficiency))
                {
                    context.Mechanics[mechanic.MechanicKey] = Unresolved(
                        mechanic.MechanicKey,
                        "competency",
                        mechanic.DisplayName,
                        CharacterResolutionStates.MissingCharacterInput,
                        ["advancement.levels"],
                        provenance: mechanic.Provenance ?? mechanic.Provenance ?? Provenance(mechanic.SourceAttributions));
                    continue;
                }
                total = checked(total + proficiency);
                contributions.Add(Contribution("proficiency.standard", "Training proficiency", proficiency));
            }

            var other = context.IntegerFacts.GetValueOrDefault($"{mechanic.MechanicKey}.other");
            total = checked(total + other);
            if (other != 0)
            {
                contributions.Add(Contribution($"{mechanic.MechanicKey}.other", "Other modifiers", other));
            }

            context.Mechanics[mechanic.MechanicKey] = new CharacterResolvedMechanicView(
                mechanic.MechanicKey,
                "competency",
                mechanic.DisplayName,
                CharacterResolutionStates.Resolved,
                total,
                null,
                null,
                [],
                [],
                [],
                [],
                contributions,
                mechanic.Provenance ?? Provenance(mechanic.SourceAttributions));
            projected[competency.ConceptKey] = total;
        }

        foreach (var relationship in KnownMechanicalRelationships.All)
        {
            var parentKey = $"competency.{relationship.Parent.ConceptKey}";
            if (!context.Mechanics.ContainsKey(parentKey))
            {
                continue;
            }

            if (relationship.Components.All(component => projected.ContainsKey(component.ConceptKey)))
            {
                var evaluated = CompositeCompetencyEvaluator.Evaluate(
                    relationship,
                    relationship.Components.ToDictionary(
                        component => component.ConceptKey,
                        component => projected[component.ConceptKey],
                        StringComparer.Ordinal),
                    []);
                var existing = context.Mechanics[parentKey];
                context.Mechanics[parentKey] = existing with
                {
                    State = CharacterResolutionStates.Resolved,
                    NumericValue = evaluated.ParentValue,
                    Contributions = relationship.Components.Select(component =>
                        Contribution(
                            $"competency.{component.ConceptKey}",
                            component.DisplayName,
                            projected[component.ConceptKey])).ToArray()
                };
            }
        }
    }

    private static CharacterCompetencyProfileView? SelectProfile(
        CharacterCompetencyDefinitionView competency,
        CharacterProjectionContext context)
    {
        var compatible = competency.Profiles
            .Where(profile => profile.RequiredCapabilityKeys.All(context.Capabilities.Contains))
            .ToArray();

        return compatible.SingleOrDefault(profile =>
                   profile.SourceEntityRevisionId == competency.DefaultProfileSourceEntityRevisionId)
            ?? compatible
                .OrderBy(profile => profile.RequiredCapabilityKeys.Count)
                .ThenBy(profile => profile.ProfileKey, StringComparer.Ordinal)
                .ThenBy(profile => profile.SourceEntityRevisionId)
                .FirstOrDefault();
    }

    private static void ResolveSpellcastingMechanics(CharacterProjectionContext context)
    {
        if (!context.Spellcasting.Values.Any(value => value.CastingAbilityKey is not null))
        {
            return;
        }

        foreach (var system in context.Spellcasting.Values
                     .Where(value => value.CastingAbilityKey is not null)
                     .ToArray())
        {
            var ability = CharacterProjectionJson.NormalizeAbilityKey(system.CastingAbilityKey!);
            if (!TryResolvedNumeric(context, $"ability.{ability}.modifier", out var abilityModifier)
                || !TryResolvedNumeric(context, "proficiency.standard", out var proficiency))
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
                    Contribution("spellcasting.save-dc.base", "Base", 8),
                    Contribution($"ability.{ability}.modifier", $"{CharacterProjectionJson.Humanize(ability)} modifier", abilityModifier),
                    Contribution("proficiency.standard", "Proficiency bonus", proficiency)
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
                    Contribution($"ability.{ability}.modifier", $"{CharacterProjectionJson.Humanize(ability)} modifier", abilityModifier),
                    Contribution("proficiency.standard", "Proficiency bonus", proficiency)
                ],
                system.Provenance);
        }
    }

    private static void ResolveHealthMechanics(CharacterProjectionContext context)
    {
        if (context.Resources.Values.All(value =>
            !value.ResourceKey.StartsWith("resource.hit-die.", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        const string key = "health.maximum-hp";
        var totalLevels = context.Resources.Values
            .Where(value => value.ResourceKey.StartsWith("resource.hit-die.", StringComparison.OrdinalIgnoreCase))
            .Sum(value => value.MaximumValue ?? 0);
        if (totalLevels == 0)
        {
            context.Mechanics[key] = Unresolved(
                key,
                "health",
                "Maximum HP",
                CharacterResolutionStates.MissingCharacterInput,
                ["advancement.levels"]);
            return;
        }

        context.Mechanics[key] = Unresolved(
            key,
            "health",
            "Maximum HP",
            CharacterResolutionStates.RollRequired,
            requiredRolls: ["health.hit-point-gain"],
            provenance: CharacterProjectionContext.EmptyProvenance());
    }

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
                context.Mechanics[mechanic.MechanicKey] = Unresolved(
                    mechanic.MechanicKey,
                    mechanic.Kind,
                    mechanic.DisplayName,
                    CharacterResolutionStates.ApplicableUnresolved,
                    provenance: mechanic.Provenance ?? mechanic.Provenance ?? Provenance(mechanic.SourceAttributions));
            }
        }
    }

    private async Task<ResolvedRulesCatalogView> ReadAllGlobalRulesAsync(
        string? userId,
        CancellationToken cancellationToken)
    {
        var first = await resolvedRules.GetGlobalPageAsync(
            userId,
            limit: PageSize,
            offset: 0,
            cancellationToken: cancellationToken);
        if (first.TotalCount <= first.Rules.Count)
        {
            return first;
        }

        var all = new List<ResolvedRuleCatalogItemView>(first.Rules);
        for (var offset = PageSize; offset < first.TotalCount; offset += PageSize)
        {
            var page = await resolvedRules.GetGlobalPageAsync(
                userId,
                limit: PageSize,
                offset: offset,
                cancellationToken: cancellationToken);
            all.AddRange(page.Rules);
        }
        return first with { Rules = all };
    }

    private async Task<ResolvedRulesCatalogView> ReadAllCampaignRulesAsync(
        Guid campaignId,
        string userId,
        CancellationToken cancellationToken)
    {
        var first = await resolvedRules.GetCampaignPageAsync(
            campaignId,
            userId,
            limit: PageSize,
            offset: 0,
            cancellationToken: cancellationToken);
        if (first.TotalCount <= first.Rules.Count)
        {
            return first;
        }

        var all = new List<ResolvedRuleCatalogItemView>(first.Rules);
        for (var offset = PageSize; offset < first.TotalCount; offset += PageSize)
        {
            var page = await resolvedRules.GetCampaignPageAsync(
                campaignId,
                userId,
                limit: PageSize,
                offset: offset,
                cancellationToken: cancellationToken);
            all.AddRange(page.Rules);
        }
        return first with { Rules = all };
    }

    private static bool TryResolvedNumeric(
        CharacterProjectionContext context,
        string key,
        out int value)
    {
        value = default;
        if (!context.Mechanics.TryGetValue(key, out var mechanic)
            || !string.Equals(mechanic.State, CharacterResolutionStates.Resolved, StringComparison.Ordinal)
            || mechanic.NumericValue is not int numeric)
        {
            return false;
        }
        value = numeric;
        return true;
    }

    private static CharacterResolvedMechanicView Unresolved(
        string key,
        string kind,
        string displayName,
        string state,
        IReadOnlyList<string>? missingInputs = null,
        IReadOnlyList<string>? missingCapabilities = null,
        IReadOnlyList<string>? requiredChoices = null,
        IReadOnlyList<string>? requiredRolls = null,
        CharacterMechanicProvenanceView? provenance = null) =>
        new(
            key,
            kind,
            displayName,
            state,
            null,
            null,
            null,
            missingInputs ?? [],
            missingCapabilities ?? [],
            requiredChoices ?? [],
            requiredRolls ?? [],
            [],
            provenance ?? CharacterProjectionContext.EmptyProvenance());

    private static CharacterMechanicContributionView Contribution(
        string key,
        string label,
        int value) =>
        new(
            key,
            label,
            CharacterEffectOperations.Add,
            value,
            null,
            null,
            CharacterProjectionContext.EmptyProvenance());

    private static CharacterMechanicProvenanceView EffectiveProvenance(
        ResolvedRuleCatalogItemView rule)
    {
        var attribution = new CharacterMechanicSourceAttributionView(
            rule.PackageKey,
            rule.PackageDisplayName,
            null,
            rule.SourceCode,
            rule.SourceRevisionNumber,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            false);
        return new CharacterMechanicProvenanceView([], [], [attribution]);
    }

    private static CharacterMechanicProvenanceView Provenance(
        IReadOnlyList<CharacterMechanicSourceAttributionView> attributions) =>
        new(attributions, attributions, attributions);
}
