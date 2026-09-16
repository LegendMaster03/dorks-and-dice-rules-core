using System.Text.Json;
using RulesCore.Application.Sources;

namespace RulesCore.Web;

internal sealed class CurrentUserSourceProgressHttpHandler(
    Uri sourceUri,
    Func<CurrentUserSourceImportProgress, CancellationToken, Task> reportProgress)
    : DelegatingHandler(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    })
{
    private readonly string githubPathPrefix = ReadGitHubPathPrefix(sourceUri);
    private int downloadedCandidates;
    private int? totalCandidates;
    private int pcGenCandidates;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var requestUri = request.RequestUri;
        var isTreeRequest = IsGitHubTreeRequest(requestUri);
        var isRawCandidate = IsGitHubRawCandidate(requestUri);
        var isDirectSource = requestUri is not null
            && Uri.Compare(
                requestUri,
                sourceUri,
                UriComponents.HttpRequestUrl,
                UriFormat.SafeUnescaped,
                StringComparison.OrdinalIgnoreCase) == 0;

        if (isTreeRequest)
        {
            await TryReportAsync(
                new CurrentUserSourceImportProgress(
                    "discovering",
                    Detail: "Reading repository file list"),
                cancellationToken);
        }
        else if (isDirectSource)
        {
            await TryReportAsync(
                new CurrentUserSourceImportProgress(
                    "downloading",
                    0,
                    1,
                    Path.GetFileName(sourceUri.AbsolutePath),
                    CurrentItem: Path.GetFileName(sourceUri.AbsolutePath),
                    FilesDiscovered: 1),
                cancellationToken);
        }

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch
        {
            if (isRawCandidate) await ReportCandidateAttemptAsync(requestUri, cancellationToken);
            throw;
        }

        if (isTreeRequest && response.IsSuccessStatusCode)
        {
            await CaptureTreeProgressAsync(response, cancellationToken);
        }
        else if (isRawCandidate)
        {
            await ReportCandidateAttemptAsync(requestUri, cancellationToken);
        }
        else if (isDirectSource)
        {
            await TryReportAsync(
                new CurrentUserSourceImportProgress(
                    "parsing",
                    0,
                    1,
                    "Inspecting source format and parsing source records",
                    CurrentItem: Path.GetFileName(sourceUri.AbsolutePath),
                    FilesDiscovered: 1),
                cancellationToken);
        }

        return response;
    }

    private async Task CaptureTreeProgressAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var originalContent = response.Content;
        var bytes = await originalContent.ReadAsByteArrayAsync(cancellationToken);
        var copiedHeaders = originalContent.Headers
            .Select(header => new KeyValuePair<string, IEnumerable<string>>(header.Key, header.Value.ToArray()))
            .ToArray();
        var replacement = new ByteArrayContent(bytes);
        foreach (var header in copiedHeaders)
            replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
        response.Content = replacement;
        originalContent.Dispose();

        try
        {
            using var document = JsonDocument.Parse(bytes);
            if (!document.RootElement.TryGetProperty("tree", out var entries)
                || entries.ValueKind != JsonValueKind.Array)
                return;

            var paths = entries.EnumerateArray()
                .Where(entry =>
                    entry.TryGetProperty("type", out var type)
                    && type.ValueKind == JsonValueKind.String
                    && string.Equals(type.GetString(), "blob", StringComparison.Ordinal)
                    && entry.TryGetProperty("path", out var pathValue)
                    && pathValue.ValueKind == JsonValueKind.String)
                .Select(entry => entry.GetProperty("path").GetString()!)
                .Where(path =>
                    (string.IsNullOrEmpty(githubPathPrefix)
                        || string.Equals(path, githubPathPrefix, StringComparison.Ordinal)
                        || path.StartsWith(githubPathPrefix + "/", StringComparison.Ordinal))
                    && IsCandidatePath(path))
                .ToArray();

            totalCandidates = paths.Length;
            pcGenCandidates = paths.Count(path => IsPcGenPath(path));
            await TryReportAsync(
                new CurrentUserSourceImportProgress(
                    "downloading",
                    0,
                    paths.Length,
                    paths.Length == 1 ? "1 candidate source file" : $"{paths.Length} candidate source files",
                    FilesDiscovered: paths.Length),
                cancellationToken);
        }
        catch (JsonException)
        {
            // Source validation owns malformed GitHub responses. Progress must remain advisory.
        }
    }

    private async Task ReportCandidateAttemptAsync(
        Uri? requestUri,
        CancellationToken cancellationToken)
    {
        var current = Interlocked.Increment(ref downloadedCandidates);
        var total = totalCandidates;
        var item = requestUri is null
            ? null
            : Uri.UnescapeDataString(requestUri.AbsolutePath.TrimStart('/'));
        await TryReportAsync(
            new CurrentUserSourceImportProgress(
                "downloading",
                current,
                total,
                item,
                CurrentItem: item,
                FilesDiscovered: total),
            cancellationToken);

        if (total is > 0 && current >= total.Value)
        {
            var detail = pcGenCandidates > 0
                ? $"Parsing {pcGenCandidates} PCGen file{(pcGenCandidates == 1 ? string.Empty : "s")} as one source set"
                : $"Parsing {total.Value} candidate source file{(total.Value == 1 ? string.Empty : "s")}";
            await TryReportAsync(
                new CurrentUserSourceImportProgress(
                    "parsing",
                    0,
                    total,
                    detail,
                    FilesDiscovered: total),
                cancellationToken);
        }
    }

    private async Task TryReportAsync(
        CurrentUserSourceImportProgress progress,
        CancellationToken cancellationToken)
    {
        try
        {
            await reportProgress(progress, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Progress is advisory. A status-write failure must not abort the source import.
        }
    }

    private static bool IsGitHubTreeRequest(Uri? uri) =>
        uri is not null
        && string.Equals(uri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.Contains("/git/trees/", StringComparison.OrdinalIgnoreCase);

    private static bool IsGitHubRawCandidate(Uri? uri) =>
        uri is not null
        && string.Equals(uri.Host, "raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase)
        && IsCandidatePath(uri.AbsolutePath);

    private static bool IsCandidatePath(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".pcc", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".lst", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPcGenPath(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".pcc", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".lst", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadGitHubPathPrefix(Uri sourceUri)
    {
        if (!string.Equals(sourceUri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        var segments = sourceUri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        if (segments.Length <= 4
            || !string.Equals(segments[2], "tree", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        return string.Join('/', segments.Skip(4));
    }
}
