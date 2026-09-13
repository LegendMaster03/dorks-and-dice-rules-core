using System.Text.Json;

namespace RulesCore.Application.Rules;

public static class RuleSemanticDifferenceKinds
{
    public const string Addition = "addition";
    public const string Omission = "omission";
    public const string CompatibleAdditive = "compatible-additive";
    public const string Contradiction = "contradiction";
    public const string MetadataOnly = "metadata-only";
    public const string AlreadyResolved = "already-resolved";
}

public sealed record RuleSemanticDifferenceView(
    string Path,
    string Kind,
    JsonElement? Left,
    JsonElement? Right,
    string Explanation,
    bool RequiresDecision);

public sealed record RuleSemanticComparisonView(
    Guid RuleConceptId,
    Guid LeftSourceEntityRevisionId,
    Guid RightSourceEntityRevisionId,
    int UnchangedValueCount,
    int MetadataOnlyDifferenceCount,
    int CompatibleDifferenceCount,
    int ContradictionCount,
    bool CanResolveAutomatically,
    string Explanation,
    IReadOnlyList<RuleSemanticDifferenceView> Differences);

public sealed record RuleSemanticComparisonRequest(
    RuleAdjudicationScopeRequest Scope,
    Guid RuleConceptId,
    Guid LeftSourceEntityRevisionId,
    Guid RightSourceEntityRevisionId);

public interface IRuleSemanticComparisonService
{
    Task<RuleSemanticComparisonView?> CompareAsync(
        RuleSemanticComparisonRequest request,
        string userId,
        CancellationToken cancellationToken = default);
}
