using System.Net.Http.Headers;
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
                    Path.GetFileName(sourceUri.AbsolutePath)),
                cancellationToken);
        }

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch
        {
            if (isRawCandidate)
            {
                await ReportCandidateAttemptAsync(requestUri, cancellationToken);
            }
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
                    "importing",
                    1,
                    1,
                    "Normalizing and importing source content"),
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
        {
            replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        response.Content = replacement;
        originalContent.Dispose();

        try
        {
            using var document = JsonDocument.Parse(bytes);
            if (!document.RootElement.TryGetProperty("tree", out var entries)
                || entries.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var count = entries.EnumerateArray().Count(entry =>
            {
                if (!entry.TryGetProperty("type", out var type)
                    || type.ValueKind != JsonValueKind.String
                    || !string.Equals(type.GetString(), "blob", StringComparison.Ordinal)
                    || !entry.TryGetProperty("path", out var pathValue)
                    || pathValue.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                var path = pathValue.GetString()!;
                if (!string.IsNullOrEmpty(githubPathPrefix)
                    && !string.Equals(path, githubPathPrefix, StringComparison.Ordinal)
                    && !path.StartsWith(githubPathPrefix + "/", StringComparison.Ordinal))
                {
                    return false;
                }

                var extension = Path.GetExtension(path);
                return string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
            });

            totalCandidates = count;
            await TryReportAsync(
                new CurrentUserSourceImportProgress(
                    "downloading",
                    0,
                    count,
                    count == 1 ? "1 candidate source file" : $"{count} candidate source files"),
                cancellationToken);
        }
        catch (JsonException)
        {
            // The source service owns validation of the GitHub response. Progress reporting
            // must never turn an otherwise useful server error into a different failure.
        }
    }

    private async Task ReportCandidateAttemptAsync(
        Uri? requestUri,
        CancellationToken cancellationToken)
    {
        var current = Interlocked.Increment(ref downloadedCandidates);
        var total = totalCandidates;
        var detail = requestUri is null
            ? null
            : Uri.UnescapeDataString(requestUri.AbsolutePath.TrimStart('/'));
        await TryReportAsync(
            new CurrentUserSourceImportProgress(
                "downloading",
                current,
                total,
                detail),
            cancellationToken);

        if (total is > 0 && current >= total.Value)
        {
            await TryReportAsync(
                new CurrentUserSourceImportProgress(
                    "importing",
                    Detail: "Normalizing and importing compatible source files"),
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
            // Progress is advisory. A transient status-write failure must not corrupt or abort
            // the source import itself.
        }
    }

    private static bool IsGitHubTreeRequest(Uri? uri) =>
        uri is not null
        && string.Equals(uri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.Contains("/git/trees/", StringComparison.OrdinalIgnoreCase);

    private static bool IsGitHubRawCandidate(Uri? uri)
    {
        if (uri is null
            || !string.Equals(uri.Host, "raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var extension = Path.GetExtension(uri.AbsolutePath);
        return string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadGitHubPathPrefix(Uri sourceUri)
    {
        if (!string.Equals(sourceUri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var segments = sourceUri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        if (segments.Length <= 4
            || !string.Equals(segments[2], "tree", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }
        return string.Join('/', segments.Skip(4));
    }
}
