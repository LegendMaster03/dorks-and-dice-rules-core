namespace RulesCore.Application.Sources;

public static class SourceAcquisitionKinds
{
    public const string PhysicalCopy = "physical-copy";
    public const string DigitalCopy = "digital-copy";
    public const string Subscription = "subscription";
    public const string LicensedAccess = "licensed-access";
    public const string Other = "other";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        PhysicalCopy,
        DigitalCopy,
        Subscription,
        LicensedAccess,
        Other
    };
}

public sealed record RecordCurrentUserSourceAcquisitionRequest(
    string AcquisitionKind,
    string? Reference,
    DateTimeOffset? AcquiredAt);

public sealed record VoidCurrentUserSourceAcquisitionRequest(string? Reason);

public sealed record SourceAcquisitionView(
    Guid Id,
    Guid SourcePackageId,
    string PackageKey,
    string PackageDisplayName,
    bool PackageIsPublic,
    string AcquisitionKind,
    string? Reference,
    DateTimeOffset? AcquiredAt,
    string RecordedByUserId,
    DateTimeOffset RecordedAt,
    bool IsVoided,
    string? VoidReason,
    string? VoidedByUserId,
    DateTimeOffset? VoidedAt);

public sealed record SourceAcquisitionMutationView(
    SourceAcquisitionView Acquisition,
    bool Changed);

public interface ISourceAcquisitionService
{
    Task<IReadOnlyList<SourceAcquisitionView>> GetCurrentUserAsync(
        string currentUserId,
        CancellationToken cancellationToken = default);

    Task<SourceAcquisitionMutationView?> RecordCurrentUserAsync(
        string currentUserId,
        Guid sourcePackageId,
        RecordCurrentUserSourceAcquisitionRequest request,
        CancellationToken cancellationToken = default);

    Task<SourceAcquisitionMutationView?> VoidCurrentUserAsync(
        string currentUserId,
        Guid sourceAcquisitionId,
        VoidCurrentUserSourceAcquisitionRequest request,
        CancellationToken cancellationToken = default);
}
