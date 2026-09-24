namespace RulesCore.Application.Rules;

public static class RuleResolutionStates
{
    public const string Resolved = "resolved";
    public const string UnresolvedFallback = "unresolved-fallback";
}

public sealed record EffectiveRuleResolutionView(
    string State,
    bool RequiresAdjudication,
    bool IsFallback,
    Guid SourceEntityRevisionId,
    int SourceRevisionNumber)
{
    public static EffectiveRuleResolutionView Resolved(
        Guid sourceEntityRevisionId,
        int sourceRevisionNumber) =>
        new(
            RuleResolutionStates.Resolved,
            RequiresAdjudication: false,
            IsFallback: false,
            sourceEntityRevisionId,
            sourceRevisionNumber);

    public static EffectiveRuleResolutionView UnresolvedFallback(
        Guid sourceEntityRevisionId,
        int sourceRevisionNumber) =>
        new(
            RuleResolutionStates.UnresolvedFallback,
            RequiresAdjudication: true,
            IsFallback: true,
            sourceEntityRevisionId,
            sourceRevisionNumber);
}
