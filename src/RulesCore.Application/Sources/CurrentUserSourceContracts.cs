namespace RulesCore.Application.Sources;

public static class CurrentUserSourceKinds
{
    public const string Upload = "upload";
    public const string Web = "web";

    public static bool IsSupported(string? value) =>
        string.Equals(value, Upload, StringComparison.Ordinal)
        || string.Equals(value, Web, StringComparison.Ordinal);
}

public sealed record AddCurrentUserSourceRequest(
    string Kind,
    string? FileName = null,
    string? Json = null,
    string? Url = null);

public sealed record CurrentUserSourceView(
    Guid Id,
    string Kind,
    string DisplayName,
    string? Url,
    Guid SourcePackageId,
    int SourceCodeCount,
    int EntityCount,
    IReadOnlyList<string> SourceCodes,
    DateTimeOffset AddedAt,
    DateTimeOffset RefreshedAt);

public interface ICurrentUserSourceService
{
    Task<IReadOnlyList<CurrentUserSourceView>> ListAsync(
        string currentUserId,
        CancellationToken cancellationToken = default);

    Task<CurrentUserSourceView> AddAsync(
        string currentUserId,
        AddCurrentUserSourceRequest request,
        CancellationToken cancellationToken = default);

    Task<CurrentUserSourceView?> RefreshAsync(
        string currentUserId,
        Guid currentUserSourceId,
        CancellationToken cancellationToken = default);
}
