namespace RulesCore.Application.Rules;

public sealed record CraftingManualCompetencyInput(
    string DisplayName,
    int Contribution,
    bool IsQualified);

public sealed record CraftingCompetencyInput(
    string? CompetencyKey = null,
    CraftingManualCompetencyInput? Manual = null);

public sealed record ManufacturingResolutionRequest(
    CharacterRulesProjectionRequest Character,
    CraftingCompetencyInput Competency,
    bool HasQualifiedGuidance = false,
    int? D20Roll = null,
    int OtherModifier = 0,
    int? TargetDc = null);

public sealed record EnchantingResolutionRequest(
    CharacterRulesProjectionRequest Character,
    string? CreatureType = null,
    CraftingCompetencyInput? Competency = null,
    string? SpellcastingKey = null,
    int? D20Roll = null,
    int OtherModifier = 0,
    int? TargetDc = null);

public static class CraftingOutcomeKinds
{
    public const string Pending = "pending";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string CompletedWithFlaws = "completed-with-flaws";
    public const string Destroyed = "destroyed";
}

public sealed record CraftingCheckResolutionView(
    string ProcedureKey,
    string DisplayName,
    string CompetencyKey,
    string CompetencyDisplayName,
    bool ManualCompetency,
    bool IsQualified,
    string RollMode,
    int CompetencyContribution,
    int AbilityContribution,
    int OtherModifier,
    int? D20Roll,
    int? Total,
    int? TargetDc,
    bool? MeetsTarget,
    string Outcome = CraftingOutcomeKinds.Pending,
    int? Margin = null,
    int? FlawCount = null,
    bool InputsConsumed = false,
    bool ProducesFunctionalOutput = false);

public interface ICraftingRulesService
{
    Task<CraftingCheckResolutionView> ResolveGlobalManufacturingAsync(
        ManufacturingResolutionRequest request,
        string? userId,
        CancellationToken cancellationToken = default);

    Task<CraftingCheckResolutionView> ResolveCampaignManufacturingAsync(
        Guid campaignId,
        ManufacturingResolutionRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<CraftingCheckResolutionView> ResolveGlobalEnchantingAsync(
        EnchantingResolutionRequest request,
        string? userId,
        CancellationToken cancellationToken = default);

    Task<CraftingCheckResolutionView> ResolveCampaignEnchantingAsync(
        Guid campaignId,
        EnchantingResolutionRequest request,
        string userId,
        CancellationToken cancellationToken = default);
}
