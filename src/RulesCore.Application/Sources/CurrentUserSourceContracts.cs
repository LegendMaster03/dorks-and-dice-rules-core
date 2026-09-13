using System.Text.Json.Serialization;

namespace RulesCore.Application.Sources;

public static class CurrentUserSourceKinds
{
    public const string Upload = "upload";
    public const string Web = "web";

    public static bool IsSupported(string? value) =>
        string.Equals(value, Upload, StringComparison.Ordinal)
        || string.Equals(value, Web, StringComparison.Ordinal);
}

public static class CurrentUserSourceImportJobOperations
{
    public const string Add = "add";
    public const string Refresh = "refresh";
}

public static class CurrentUserSourceImportJobStatuses
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

public sealed record AddCurrentUserSourceRequest
{
    public AddCurrentUserSourceRequest()
    {
    }

    public AddCurrentUserSourceRequest(
        string Kind,
        string? FileName = null,
        string? Json = null,
        string? Url = null)
    {
        this.Kind = Kind;
        this.FileName = FileName;
        Content = Json;
        this.Url = Url;
    }

    public string Kind { get; init; } = string.Empty;
    public string? FileName { get; init; }

    // Legacy/text transport retained for compatibility with existing callers and tests.
    // New browser uploads use ContentBase64 so binary-compatible format adapters can be added.
    public string? Content { get; init; }
    public string? ContentBase64 { get; init; }
    public string? Url { get; init; }

    [JsonIgnore]
    public string? Json
    {
        get
        {
            CompatibleCurrentUserSourceDocument? compatibleDocument;
            if (ContentBase64 is not null)
            {
                byte[] bytes;
                try
                {
                    bytes = Convert.FromBase64String(ContentBase64);
                }
                catch (FormatException exception)
                {
                    throw new InvalidDataException(
                        "The uploaded file content is not valid Base64.",
                        exception);
                }

                if (!CurrentUserSourceCompatibility.TryRead(
                        FileName,
                        bytes,
                        out compatibleDocument))
                {
                    throw new InvalidDataException(
                        "The uploaded file is not compatible with Rules Core.");
                }
            }
            else
            {
                if (!CurrentUserSourceCompatibility.TryRead(
                        FileName,
                        Content,
                        out compatibleDocument))
                {
                    throw new InvalidDataException(
                        "The uploaded file is not compatible with Rules Core.");
                }
            }

            return compatibleDocument!.ImportDocument;
        }
    }
}

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

public sealed record CurrentUserSourceImportProgress(
    string Stage,
    int? Current = null,
    int? Total = null,
    string? Detail = null);

public sealed record CurrentUserSourceImportJobView(
    Guid Id,
    string Operation,
    string Kind,
    string DisplayName,
    string? Url,
    string Status,
    Guid? CurrentUserSourceId,
    string? Error,
    string? ProgressStage,
    int? ProgressCurrent,
    int? ProgressTotal,
    string? ProgressDetail,
    DateTimeOffset? ProgressUpdatedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

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
