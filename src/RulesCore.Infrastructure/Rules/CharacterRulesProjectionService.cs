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
        var effectiveMechanicCatalog =
            CharacterMechanicsConsumerBoundary.ProjectEffective(mechanicCatalog);
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
        CharacterSpellcastingResolver.ResolveMechanics(context);
        CharacterSpellcastingResolver.ResolveResources(context);
        CharacterHealthResolver.Resolve(context);
        CharacterPrerequisiteResolver.Resolve(context);
        CharacterLegacyMechanicsResolver.Resolve(context, mechanicCatalog);

        var projectedMechanics = context.Mechanics.Values
            .Where(value => context.ShouldIncludeMechanic(value.MechanicKey))
            .Select(value => value.Help is null
                ? value with { Help = CharacterContextualHelpProjection.For(value.MechanicKey) }
                : value)
            .OrderBy(value => value.Kind, StringComparer.Ordinal)
            .ThenBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.MechanicKey, StringComparer.Ordinal)
            .ToArray();

        return new CharacterRulesProjectionView(
            rules.Scope,
            rules.CampaignId,
            rules.RevisionNumber,
            rules.PublishedAt,
            projectedMechanics,
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
            context.Equipment.Values.OrderBy(value => value.ItemKey, StringComparer.Ordinal).ToArray(),
            effectiveMechanicCatalog.Competencies,
            ProjectCompetencyRelationships(effectiveMechanicCatalog),
            rules.Rules
                .Where(value => value.Resolution is not null)
                .Select(value => new CharacterRuleResolutionView(
                    value.ConceptKey,
                    value.Resolution!.State,
                    value.Resolution.RequiresAdjudication,
                    value.Resolution.SourceEntityRevisionId,
                    value.Resolution.SourceRevisionNumber))
                .OrderBy(value => value.ConceptKey, StringComparer.Ordinal)
                .ToArray(),
            HelpTopics: KnownCharacterContextualHelp.All
                .Select(value => CharacterContextualHelpProjection.For(value.TopicKey)!)
                .OrderBy(value => value.TopicKey, StringComparer.Ordinal)
                .ToArray());
    }

    private static IReadOnlyList<CharacterMechanicRelationshipView> ProjectCompetencyRelationships(
        CharacterMechanicsCatalogView catalog) =>
        catalog.Mechanics
            .Where(value => value.Competency is not null)
            .SelectMany(value => value.Relationships)
            .DistinctBy(value => value.RelationshipKey, StringComparer.Ordinal)
            .OrderBy(value => value.RelationshipKey, StringComparer.Ordinal)
            .ToArray();

}