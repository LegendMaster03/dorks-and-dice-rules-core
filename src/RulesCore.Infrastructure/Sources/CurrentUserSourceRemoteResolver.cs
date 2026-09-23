using System.Net;
using System.Text.Json;
using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Resolves current-user Web sources into normalized source representations.
/// Remote acquisition and GitHub snapshot traversal are isolated from source registration,
/// grants, import deduplication, and persistence.
/// </summary>
internal sealed class CurrentUserSourceRemoteResolver(
    ISourceFormatAdapterRegistry adapters,
    HttpClient httpClient)
{
    private const long MaxRemoteDocumentBytes = 64L * 1024L * 1024L;
    private const int MaxResolvedDocuments = 2000;

    internal async Task<IReadOnlyList<NormalizedSourceRepresentation>> ResolveAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        if (string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.Contains("/tree/", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveGitHubTreeAsync(uri, cancellationToken);
        }
    
        var fetched = await FetchBytesAsync(uri, cancellationToken);
        var fileName = Path.GetFileName(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "web-source";
        }
        var artifact = new SourceRepresentationArtifact(
            fileName,
            fetched.Bytes,
            $"web:{NormalizeWebOrigin(uri)}",
            uri.AbsoluteUri,
            fetched.MediaType);
        var representation = adapters.TryRead(artifact);
        return representation is null ? [] : [representation];
    }
    
    private async Task<IReadOnlyList<NormalizedSourceRepresentation>> ResolveGitHubTreeAsync(
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        var location = ParseGitHubTreeUri(sourceUri);
        var snapshot = await ResolveGitHubTreeSnapshotAsync(location, cancellationToken);
        var treeApiUri = new Uri(
            $"https://api.github.com/repos/{Uri.EscapeDataString(snapshot.Owner)}/{Uri.EscapeDataString(snapshot.Repository)}/git/trees/{Uri.EscapeDataString(snapshot.TreeSha)}?recursive=1");
        var treeBytes = await FetchBytesAsync(treeApiUri, cancellationToken);
        using var treeDocument = JsonDocument.Parse(treeBytes.Bytes);
        if (treeDocument.RootElement.TryGetProperty("truncated", out var truncated)
            && truncated.ValueKind == JsonValueKind.True)
        {
            throw new InvalidDataException(
                "GitHub truncated the repository tree. Use a narrower Web-source URL instead of importing an incomplete tree.");
        }
        if (!treeDocument.RootElement.TryGetProperty("tree", out var entries)
            || entries.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("GitHub tree response did not contain a tree array.");
        }
    
        var prefix = snapshot.Path.Trim('/');
        var paths = entries.EnumerateArray()
            .Where(entry =>
                entry.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && string.Equals(type.GetString(), "blob", StringComparison.Ordinal)
                && entry.TryGetProperty("path", out var path)
                && path.ValueKind == JsonValueKind.String)
            .Select(entry => entry.GetProperty("path").GetString()!)
            .Where(path =>
                (string.IsNullOrEmpty(prefix)
                    || string.Equals(path, prefix, StringComparison.Ordinal)
                    || path.StartsWith(prefix + "/", StringComparison.Ordinal))
                && adapters.IsCandidateFileName(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    
        if (paths.Length == 0)
        {
            throw new InvalidDataException("The Web source did not contain any compatible files.");
        }
        if (paths.Length > MaxResolvedDocuments)
        {
            throw new InvalidDataException(
                $"The GitHub tree contains {paths.Length} candidate source files beneath the selected path; the maximum is {MaxResolvedDocuments}.");
        }
    
        var artifacts = new List<SourceRepresentationArtifact>(paths.Length);
        foreach (var path in paths)
        {
            var rawUri = BuildGitHubRawUri(snapshot, path);
            FetchedDocument fetched;
            try
            {
                fetched = await FetchBytesAsync(rawUri, cancellationToken);
            }
            catch (HttpRequestException)
            {
                continue;
            }
    
            artifacts.Add(new SourceRepresentationArtifact(
                path,
                fetched.Bytes,
                $"web:{NormalizeWebOrigin(sourceUri)}#{path}",
                rawUri.AbsoluteUri,
                fetched.MediaType));
        }
    
        var results = adapters.TryReadMany(artifacts);
        if (results.Count == 0)
        {
            throw new InvalidDataException("The Web source did not contain any compatible files.");
        }
        return results;
    }
    
    private async Task<GitHubTreeSnapshot> ResolveGitHubTreeSnapshotAsync(
        GitHubTreeLocation tree,
        CancellationToken cancellationToken)
    {
        var commitApiUri = new Uri(
            $"https://api.github.com/repos/{Uri.EscapeDataString(tree.Owner)}/{Uri.EscapeDataString(tree.Repository)}/commits/{Uri.EscapeDataString(tree.Reference)}");
        var commitBytes = await FetchBytesAsync(commitApiUri, cancellationToken);
        using var commitDocument = JsonDocument.Parse(commitBytes.Bytes);
        var root = commitDocument.RootElement;
        if (!root.TryGetProperty("sha", out var commitShaValue)
            || commitShaValue.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(commitShaValue.GetString()))
        {
            throw new InvalidDataException("GitHub did not return a commit identity for the Web source.");
        }
        if (!root.TryGetProperty("commit", out var commit)
            || commit.ValueKind != JsonValueKind.Object
            || !commit.TryGetProperty("tree", out var commitTree)
            || commitTree.ValueKind != JsonValueKind.Object
            || !commitTree.TryGetProperty("sha", out var treeShaValue)
            || treeShaValue.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(treeShaValue.GetString()))
        {
            throw new InvalidDataException("GitHub did not return a tree identity for the Web source commit.");
        }
    
        return new GitHubTreeSnapshot(
            tree.Owner,
            tree.Repository,
            commitShaValue.GetString()!.Trim(),
            treeShaValue.GetString()!.Trim(),
            tree.Path);
    }
    
    private async Task<FetchedDocument> FetchBytesAsync(Uri uri, CancellationToken cancellationToken)
    {
        await HostedSourceUriPolicy.EnsureRemoteSafeAsync(uri, "Web source", cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.ParseAdd("application/pdf, application/json;q=0.9, text/json;q=0.8, */*;q=0.1");
        request.Headers.UserAgent.ParseAdd("dorks-and-dice-rules-core/1.0");
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if ((int)response.StatusCode is >= 300 and < 400)
        {
            throw new HttpRequestException(
                $"Web source '{uri}' redirected to another location. Add the final HTTPS URL instead.");
        }
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxRemoteDocumentBytes)
        {
            throw new InvalidDataException(
                $"Web source '{uri}' exceeds the {MaxRemoteDocumentBytes / (1024 * 1024)} MiB per-document limit.");
        }
    
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.LongLength > MaxRemoteDocumentBytes)
        {
            throw new InvalidDataException(
                $"Web source '{uri}' exceeds the {MaxRemoteDocumentBytes / (1024 * 1024)} MiB per-document limit.");
        }
        return new FetchedDocument(bytes, response.Content.Headers.ContentType?.MediaType);
    }
    
    private static GitHubTreeLocation ParseGitHubTreeUri(Uri uri)
    {
        var segments = uri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            || segments.Length < 4
            || !string.Equals(segments[2], "tree", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "A GitHub Web source must use https://github.com/{owner}/{repository}/tree/{ref}/{optional-path}.");
        }
        return new GitHubTreeLocation(
            segments[0],
            segments[1],
            segments[3],
            segments.Length > 4 ? string.Join('/', segments.Skip(4)) : string.Empty);
    }
    
    private static Uri BuildGitHubRawUri(GitHubTreeSnapshot snapshot, string path)
    {
        var escapedPath = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
        return new Uri(
            $"https://raw.githubusercontent.com/{Uri.EscapeDataString(snapshot.Owner)}/{Uri.EscapeDataString(snapshot.Repository)}/{Uri.EscapeDataString(snapshot.CommitSha)}/{escapedPath}");
    }
    
    
    private static string NormalizeWebOrigin(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Host = uri.Host.ToLowerInvariant(),
            Fragment = string.Empty
        };
        return builder.Uri.AbsoluteUri;
    }
    
    internal static HttpClient CreateSharedHttpClient() => new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    })
    {
        Timeout = TimeSpan.FromMinutes(2)
    };
    
    
    private sealed record GitHubTreeLocation(
        string Owner,
        string Repository,
        string Reference,
        string Path);
    
    private sealed record GitHubTreeSnapshot(
        string Owner,
        string Repository,
        string CommitSha,
        string TreeSha,
        string Path);
    
    private sealed record FetchedDocument(byte[] Bytes, string? MediaType);
    
}
