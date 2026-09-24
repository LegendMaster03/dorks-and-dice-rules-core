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
    string CompetencyKey,
    string CompetencyDisplayName,
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
    string CompetencyKey,
    string CompetencyDisplayName,
    IReadOnlyList<HarvestingComponentView> Components,
    string? CreatureConceptKey = null,
    string? CreatureDisplayName = null,
    string? CreatureSize = null,
    bool CreatureOverridesApplied = false,
    bool ManualEditsApplied = false);

public sealed record HarvestingHelperInput(
    int ProficiencyBonus,
    bool IsProficient,
    bool ParticipatedForEntireDuration = true,
    bool IsAssessmentParticipant = false,
    bool IsCarvingParticipant = false);

public sealed record HarvestingOutcomeRequest(
    HarvestingTableResolutionRequest Table,
    int AssessmentResult,
    int CarvingResult,
    bool SameActor,
    IReadOnlyList<string> HarvestOrderComponentKeys,
    string? CreatureSize = null,
    IReadOnlyList<HarvestingHelperInput>? Helpers = null);

public sealed record HarvestingComponentOutcomeView(
    string Key,
    string DisplayName,
    int ComponentDc,
    int HarvestDc,
    int? Quantity,
    string? Origin,
    bool Awarded);

public sealed record HarvestingOutcomeView(
    HarvestingResolvedTableView Table,
    string AssessmentRollMode,
    string CarvingRollMode,
    string? CreatureSize,
    int AssessmentResult,
    int CarvingResult,
    int HelperCount,
    int HelperContribution,
    int HarvestingResult,
    IReadOnlyList<HarvestingComponentOutcomeView> Components);

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

    Task<HarvestingOutcomeView?> ResolveGlobalOutcomeAsync(
        HarvestingOutcomeRequest request,
        string? userId,
        CancellationToken cancellationToken = default);

    Task<HarvestingOutcomeView?> ResolveCampaignOutcomeAsync(
        Guid campaignId,
        HarvestingOutcomeRequest request,
        string userId,
        CancellationToken cancellationToken = default);
}
