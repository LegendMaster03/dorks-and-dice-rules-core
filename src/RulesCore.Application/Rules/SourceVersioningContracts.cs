using System.Text.Json;

namespace RulesCore.Application.Rules;

public static class SourceLineageKinds
{
    public const string PredecessorOf = "predecessor-of";
    public const string PlaytestOf = "playtest-of";
    public const string RevisedAs = "revised-as";
    public const string RenamedAs = "renamed-as";
    public const string SplitInto = "split-into";
    public const string CombinedInto = "combined-into";
    public const string RelatedVersion = "related-version";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [PredecessorOf, PlaytestOf, RevisedAs, RenamedAs, SplitInto, CombinedInto, RelatedVersion],
        StringComparer.Ordinal);
}

public static class RuleConsolidationContributionKinds
{
    public const string Incorporated = "incorporated";
    public const string Reference = "reference";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [Incorporated, Reference],
        StringComparer.Ordinal);
}

public sealed record RuleConceptReferenceView(
    Guid Id,
    string Key,
    string EntityType,
    string DisplayName);

public sealed record SourceVersionEntityView(
    Guid SourceEntityId,
    string EntityType,
    string Name,
    string SourceCode,
    Guid LatestRevisionId,
    int LatestRevisionNumber,
    string LatestFingerprint,
    string PackageKey,
    string PackageDisplayName,
    string WorkKey,
    string WorkDisplayName,
    string EditionKey,
    string EditionDisplayName,
    string? GameEdition,
    string? ReleaseKind,
    DateOnly? PublicationDate,
    IReadOnlyList<RuleConceptReferenceView> BoundConcepts);

public sealed record SourceVersionCandidateView(
    SourceVersionEntityView Candidate,
    int Confidence,
    IReadOnlyList<string> Reasons);

public sealed record SourceVersionDetectionView(
    SourceVersionEntityView Source,
    IReadOnlyList<SourceVersionCandidateView> Candidates);

public sealed record BindDetectedSourceVersionRequest(Guid RuleConceptId);

public sealed record CreateSourceLineageRequest(
    Guid FromSourceEntityId,
    Guid ToSourceEntityId,
    string RelationshipKind,
    string? Note);

public sealed record VoidSourceLineageRequest(string? Reason);

public sealed record SourceLineageView(
    Guid Id,
    Guid FromSourceEntityId,
    Guid ToSourceEntityId,
    string RelationshipKind,
    string? Note,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    bool IsVoided,
    string? VoidReason,
    string? VoidedByUserId,
    DateTimeOffset? VoidedAt);

public sealed record SourceLineageMutationView(
    SourceLineageView Lineage,
    bool Changed);

public sealed record RuleConsolidationContributionRequest(
    Guid SourceEntityRevisionId,
    string ContributionKind,
    string? Note = null);

public sealed record RuleConsolidationContributionView(
    Guid SourceEntityRevisionId,
    int SourceRevisionNumber,
    string SourceFingerprint,
    Guid SourceEntityId,
    string SourceEntityName,
    string SourceCode,
    string? GameEdition,
    string ContributionKind,
    string? Note);

public sealed record RuleConsolidationSourceRevisionView(
    Guid Id,
    int RevisionNumber,
    string Fingerprint,
    DateTimeOffset ImportedAt,
    JsonElement Document);

public sealed record RuleConsolidationSourceView(
    Guid SourceEntityId,
    string EntityType,
    string Name,
    string SourceCode,
    string PackageKey,
    string PackageDisplayName,
    string WorkKey,
    string WorkDisplayName,
    string EditionKey,
    string EditionDisplayName,
    string? GameEdition,
    string? ReleaseKind,
    DateOnly? PublicationDate,
    IReadOnlyList<RuleConsolidationSourceRevisionView> Revisions);

public sealed record RuleConsolidationView(
    RuleConceptView Concept,
    IReadOnlyList<RuleConsolidationSourceView> Sources,
    int RestrictedBindingCount,
    IReadOnlyList<SourceLineageView> Lineage,
    GlobalRuleDecisionView? LatestDecision,
    IReadOnlyList<RuleConsolidationContributionView> LatestContributions);

public interface ISourceVersioningService
{
    Task<SourceVersionDetectionView?> DetectVersionsAsync(
        Guid sourceEntityId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<RuleMutationResult<RuleConceptSourceBindingView>?> BindToConceptAsync(
        Guid sourceEntityId,
        Guid ruleConceptId,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<SourceLineageMutationView?> CreateLineageAsync(
        CreateSourceLineageRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<SourceLineageMutationView?> VoidLineageAsync(
        Guid lineageId,
        VoidSourceLineageRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SourceLineageView>> GetActiveLineageForSourcesAsync(
        IReadOnlyCollection<Guid> sourceEntityIds,
        string userId,
        CancellationToken cancellationToken = default);
}

public interface IRuleConsolidationService
{
    Task<RuleConsolidationView?> GetAsync(
        Guid ruleConceptId,
        string userId,
        CancellationToken cancellationToken = default);
}
