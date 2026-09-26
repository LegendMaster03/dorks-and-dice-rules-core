using RulesCore.Domain.Rules;

namespace RulesCore.Application.Rules;

public static class TravelEnvironmentMechanicStates
{
    public const string Resolved = "resolved";
    public const string RequiresAdjudication = "requires-adjudication";
    public const string Conflicted = "conflicted";
}

public sealed record TravelEnvironmentCatalogView(
    string Scope,
    Guid? CampaignId,
    int? RevisionNumber,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<TravelEnvironmentMechanicView> Mechanics);

public sealed record TravelEnvironmentMechanicView(
    string MechanicKey,
    string State,
    bool CanResolve,
    TravelEnvironmentMechanicDefinition? Definition,
    IReadOnlyList<string> ConceptKeys,
    IReadOnlyList<Guid> RuleConceptIds,
    IReadOnlyList<CharacterMechanicSourceAttributionView> SourceAttributions,
    IReadOnlyList<EffectiveRuleResolutionView> RuleResolutions);

public sealed record TravelEnvironmentResolutionRequest(
    Dictionary<string, int>? IntegerInputs = null,
    Dictionary<string, bool>? BooleanInputs = null,
    Dictionary<string, string>? StringInputs = null,
    Dictionary<string, IReadOnlyList<string>>? StringListInputs = null);

public sealed record TravelEnvironmentEvaluationView(
    string MechanicKey,
    string MechanicState,
    string EvaluationState,
    TravelEnvironmentQuantity? Quantity,
    decimal? Factor,
    TravelEnvironmentCheckResolution? Check,
    IReadOnlyList<string> MissingInputKeys,
    IReadOnlyList<CharacterMechanicSourceAttributionView> SourceAttributions,
    IReadOnlyList<EffectiveRuleResolutionView> RuleResolutions);

public interface ITravelEnvironmentConsumerService
{
    Task<TravelEnvironmentCatalogView> GetGlobalAsync(
        string? userId,
        CancellationToken cancellationToken = default);

    Task<TravelEnvironmentCatalogView> GetCampaignAsync(
        Guid campaignId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<TravelEnvironmentEvaluationView?> ResolveGlobalAsync(
        string mechanicKey,
        TravelEnvironmentResolutionRequest request,
        string? userId,
        CancellationToken cancellationToken = default);

    Task<TravelEnvironmentEvaluationView?> ResolveCampaignAsync(
        Guid campaignId,
        string mechanicKey,
        TravelEnvironmentResolutionRequest request,
        string userId,
        CancellationToken cancellationToken = default);
}
