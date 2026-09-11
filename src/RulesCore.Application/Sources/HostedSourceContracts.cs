namespace RulesCore.Application.Sources;

public static class HostedSourceResourceKinds
{
    public const string DirectJson = "direct-json";
    public const string JsonIndex = "json-index";
    public const string GitHubTree = "github-tree";
    public const string HtmlIndex = "html-index";

    public static bool IsSupported(string? value) =>
        string.Equals(value, DirectJson, StringComparison.Ordinal)
        || string.Equals(value, JsonIndex, StringComparison.Ordinal)
        || string.Equals(value, GitHubTree, StringComparison.Ordinal)
        || string.Equals(value, HtmlIndex, StringComparison.Ordinal);
}

public static class HostedSourceFormatKinds
{
    public const string FiveEToolsJson = "5etools-json";
    public const string LegacySrdText = "legacy-srd-text";

    public static bool IsSupported(string? value) =>
        string.Equals(value, FiveEToolsJson, StringComparison.Ordinal)
        || string.Equals(value, LegacySrdText, StringComparison.Ordinal);
}

public sealed record HostedSourceResourceRequest(
    string Kind,
    string Uri);

public sealed record SetHostedSourceDefinitionRequest(
    string DisplayName,
    string FormatKind,
    string PackageKey,
    string PackageDisplayName,
    string Provider,
    string? License,
    bool IsPublic,
    string WorkKey,
    string WorkDisplayName,
    string EditionKey,
    string EditionDisplayName,
    string? GameEdition,
    string? ReleaseKind,
    DateOnly? PublicationDate,
    IReadOnlyList<string>? IncludedSourceCodes,
    IReadOnlyList<HostedSourceResourceRequest> Resources,
    bool IsEnabled = true,
    string? Note = null);

public sealed record HostedSourceResourceView(
    string Kind,
    string Uri);

public sealed record HostedSourceDefinitionView(
    Guid Id,
    string Key,
    int RevisionNumber,
    string Fingerprint,
    string DisplayName,
    string FormatKind,
    string PackageKey,
    string PackageDisplayName,
    string Provider,
    string? License,
    bool IsPublic,
    string WorkKey,
    string WorkDisplayName,
    string EditionKey,
    string EditionDisplayName,
    string? GameEdition,
    string? ReleaseKind,
    DateOnly? PublicationDate,
    IReadOnlyList<string> IncludedSourceCodes,
    IReadOnlyList<HostedSourceResourceView> Resources,
    bool IsEnabled,
    string? Note,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    bool CreatedRevision = false);

public sealed record HostedSourceResolvedDocument(
    string ResourceKind,
    string ResourceUri,
    string ResolvedUri,
    int SelectedEntityCount);

public sealed record HostedSourcePreviewView(
    HostedSourceDefinitionView Definition,
    IReadOnlyList<HostedSourceResolvedDocument> Documents,
    SourceImportPreviewResult Preview);

public sealed record HostedSourceRefreshView(
    HostedSourceDefinitionView Definition,
    IReadOnlyList<HostedSourceResolvedDocument> Documents,
    SourceImportResult Import);

public sealed record HostedSourceMatchView(
    Guid DefinitionId,
    string DefinitionKey,
    string DisplayName,
    int RevisionNumber,
    string PackageKey,
    string WorkKey,
    string EditionKey,
    IReadOnlyList<string> IncludedSourceCodes,
    IReadOnlyList<string> MatchedSourceCodes,
    bool ExactSourceCodeMatch);

public interface IHostedSourceService
{
    Task<IReadOnlyList<HostedSourceDefinitionView>> ListAsync(
        bool includeDisabled,
        CancellationToken cancellationToken = default);

    Task<HostedSourceDefinitionView?> GetAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default);

    Task<HostedSourceDefinitionView> SetAsync(
        string definitionKey,
        SetHostedSourceDefinitionRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HostedSourceMatchView>> FindMatchesAsync(
        IReadOnlyCollection<string> sourceCodes,
        CancellationToken cancellationToken = default);

    Task<HostedSourcePreviewView> PreviewAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default);

    Task<HostedSourceRefreshView> RefreshAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default);
}
