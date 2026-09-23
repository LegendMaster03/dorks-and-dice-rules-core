namespace RulesCore.Application.Sources;

/// <summary>
/// Version of Rules Core's persisted derived source interpretation. Increment this value when
/// translation/normalization logic changes in a way that should be replayed over preserved
/// native source revisions.
/// </summary>
public static class SourceNormalizationVersion
{
    public const int Current = 1;
}

public sealed record SourceNormalizationStatusView(
    int CurrentVersion,
    int RevisionCount,
    int CurrentRevisionCount,
    int PendingRevisionCount,
    int FailedRevisionCount);

public sealed record SourceNormalizationFailureView(
    Guid SourceEntityRevisionId,
    string EntityName,
    string Message);

public sealed record SourceNormalizationRunView(
    int CurrentVersion,
    int AttemptedRevisionCount,
    int UpdatedContentCount,
    int UnchangedContentCount,
    int CanonicalReassociationCount,
    int FailedRevisionCount,
    int RemainingPendingRevisionCount,
    IReadOnlyList<SourceNormalizationFailureView> Failures);

public interface ISourceNormalizationMaintenanceService
{
    Task<SourceNormalizationStatusView> GetStatusAsync(
        string? packageKey = null,
        CancellationToken cancellationToken = default);

    Task<SourceNormalizationRunView> ReconcileAsync(
        int limit = 25,
        bool retryFailed = false,
        string? packageKey = null,
        CancellationToken cancellationToken = default);
}
