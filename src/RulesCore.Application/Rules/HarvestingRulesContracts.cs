namespace RulesCore.Application.Rules;

public sealed record HarvestingSourceView(
    string WorkKey,
    string WorkDisplayName,
    string Provider,
    string GameEdition,
    string ReleaseKind,
    DateOnly PublicationDate,
    string ReferenceUri);

public sealed record HarvestingComponentView(
    string Key,
    string DisplayName,
    int ComponentDc,
    int? Quantity = null,
    string? Origin = null);

public sealed record HarvestingCreatureTypeView(
    string Key,
    string DisplayName,
    string SkillConceptKey,
    string SkillDisplayName,
    IReadOnlyList<HarvestingComponentView> DefaultComponents);

public sealed record HarvestingProcedureCheckView(
    string MechanicKey,
    string AbilityKey,
    string AbilityDisplayName,
    string DefaultRollMode);

public sealed record HarvestingHelperRulesView(
    IReadOnlyDictionary<string, int> MaximumByCreatureSize,
    bool StandardHelpActionApplies);

public sealed record HarvestingProcedureView(
    HarvestingProcedureCheckView Assessment,
    HarvestingProcedureCheckView Carving,
    string TotalMechanicKey,
    string SameActorRollMode,
    string ComponentDcAggregation,
    string AwardMode,
    HarvestingHelperRulesView Helpers);

public sealed record HarvestingRulesCatalogView(
    HarvestingSourceView Source,
    IReadOnlyList<HarvestingCreatureTypeView> CreatureTypes,
    HarvestingProcedureView Procedure);

public sealed record HarvestingComponentEditRequest(
    string Key,
    string? DisplayName = null,
    int? ComponentDc = null,
    int? Quantity = null);

public sealed record HarvestingTableEditRequest(
    IReadOnlyList<string>? RemoveComponentKeys = null,
    IReadOnlyList<HarvestingComponentEditRequest>? UpsertComponents = null);

public sealed record HarvestingTableResolutionRequest(
    string? CreatureConceptKey = null,
    string? CreatureType = null,
    HarvestingTableEditRequest? ManualEdits = null);

public sealed record HarvestingResolvedTableView(
    HarvestingSourceView Source,
    string CreatureType,
    string CreatureTypeDisplayName,
    string SkillConceptKey,
    string SkillDisplayName,
    IReadOnlyList<HarvestingComponentView> Components,
    string? CreatureConceptKey = null,
    string? CreatureDisplayName = null,
    string? CreatureSize = null,
    bool CreatureOverridesApplied = false,
    bool ManualEditsApplied = false);

public interface IHarvestingRulesService
{
    HarvestingRulesCatalogView GetCatalog();

    Task<HarvestingResolvedTableView?> ResolveGlobalAsync(
        HarvestingTableResolutionRequest request,
        string? userId,
        CancellationToken cancellationToken = default);

    Task<HarvestingResolvedTableView?> ResolveCampaignAsync(
        Guid campaignId,
        HarvestingTableResolutionRequest request,
        string userId,
        CancellationToken cancellationToken = default);
}
