using System.Net;
using System.Text;
using System.Text.Json;
using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Resolves canonical hosted-source resources into bounded, filtered immutable documents.
/// Network acquisition, GitHub/index expansion, and aggregate construction are isolated from
/// hosted-source definition persistence.
/// </summary>
internal sealed class HostedSourceRemoteResolver(HttpClient httpClient)
{
    private const long MaxRemoteDocumentBytes = 64L * 1024L * 1024L;
    private const int MaxResolvedDocuments = 2000;

    internal async Task<HostedSourceRemoteResolution> ResolveAsync(
        HostedSourceDefinitionView definition,
        CancellationToken cancellationToken)
    {
        var documents = new List<ResolvedSourceDocument>();
        foreach (var resource in definition.Resources)
        {
            switch (resource.Kind)
            {
                case HostedSourceResourceKinds.DirectJson:
                    await AddResolvedDocumentAsync(
                        definition,
                        resource.Kind,
                        resource.Uri,
                        new Uri(resource.Uri, UriKind.Absolute),
                        documents,
                        skipUnsupportedJson: false,
                        cancellationToken);
                    break;
    
                case HostedSourceResourceKinds.JsonIndex:
                    await ResolveJsonIndexAsync(
                        definition,
                        resource,
                        documents,
                        cancellationToken);
                    break;
    
                case HostedSourceResourceKinds.GitHubTree:
                    await ResolveGitHubTreeAsync(
                        definition,
                        resource,
                        documents,
                        cancellationToken);
                    break;
    
                default:
                    throw new NotSupportedException(
                        $"Hosted source resource kind '{resource.Kind}' is not supported.");
            }
        }
    
        if (documents.Count == 0)
        {
            throw new InvalidDataException(
                "The hosted source did not resolve to any importable entities for its configured source-code filter.");
        }
    
        return new HostedSourceRemoteResolution(documents.Select(ToView).ToArray(), BuildAggregateJson(documents));
    }
    
    private async Task ResolveJsonIndexAsync(
        HostedSourceDefinitionView definition,
        HostedSourceResourceView resource,
        List<ResolvedSourceDocument> documents,
        CancellationToken cancellationToken)
    {
        var indexUri = new Uri(resource.Uri, UriKind.Absolute);
        var indexJson = await FetchTextAsync(indexUri, cancellationToken);
        using var indexDocument = JsonDocument.Parse(indexJson);
        var references = new List<string>();
        CollectJsonReferences(indexDocument.RootElement, references);
    
        var resolvedUris = references
            .Select(value => ResolveIndexReference(indexUri, value))
            .DistinctBy(value => value.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (resolvedUris.Length > MaxResolvedDocuments)
        {
            throw new InvalidDataException(
                $"Hosted JSON index expands to {resolvedUris.Length} documents; the maximum is {MaxResolvedDocuments}.");
        }
    
        foreach (var resolvedUri in resolvedUris)
        {
            await AddResolvedDocumentAsync(
                definition,
                resource.Kind,
                resource.Uri,
                resolvedUri,
                documents,
                skipUnsupportedJson: true,
                cancellationToken);
        }
    }
    
    private async Task ResolveGitHubTreeAsync(
        HostedSourceDefinitionView definition,
        HostedSourceResourceView resource,
        List<ResolvedSourceDocument> documents,
        CancellationToken cancellationToken)
    {
        var sourceUri = new Uri(resource.Uri, UriKind.Absolute);
        var tree = ParseGitHubTreeUri(sourceUri);
        var treeApiUri = new Uri(
            $"https://api.github.com/repos/{Uri.EscapeDataString(tree.Owner)}/{Uri.EscapeDataString(tree.Repository)}/git/trees/{Uri.EscapeDataString(tree.Reference)}?recursive=1");
        var treeJson = await FetchTextAsync(treeApiUri, cancellationToken);
        using var treeDocument = JsonDocument.Parse(treeJson);
        if (treeDocument.RootElement.TryGetProperty("truncated", out var truncated)
            && truncated.ValueKind == JsonValueKind.True)
        {
            throw new InvalidDataException(
                "GitHub truncated the repository tree. Configure narrower hosted-source resources instead of importing an incomplete tree.");
        }
        if (!treeDocument.RootElement.TryGetProperty("tree", out var entries)
            || entries.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("GitHub tree response did not contain a tree array.");
        }
    
        var prefix = tree.Path.Trim('/');
        var paths = entries.EnumerateArray()
            .Where(entry =>
                entry.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && string.Equals(type.GetString(), "blob", StringComparison.Ordinal)
                && entry.TryGetProperty("path", out var path)
                && path.ValueKind == JsonValueKind.String)
            .Select(entry => entry.GetProperty("path").GetString()!)
            .Where(path =>
                path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrEmpty(prefix)
                    || string.Equals(path, prefix, StringComparison.Ordinal)
                    || path.StartsWith(prefix + "/", StringComparison.Ordinal)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (paths.Length > MaxResolvedDocuments)
        {
            throw new InvalidDataException(
                $"GitHub tree contains {paths.Length} JSON documents beneath the configured path; the maximum is {MaxResolvedDocuments}. Configure a narrower tree or indexes/direct documents.");
        }
    
        foreach (var path in paths)
        {
            var rawUri = BuildGitHubRawUri(tree, path);
            await AddResolvedDocumentAsync(
                definition,
                resource.Kind,
                resource.Uri,
                rawUri,
                documents,
                skipUnsupportedJson: true,
                cancellationToken);
        }
    }
    
    private async Task AddResolvedDocumentAsync(
        HostedSourceDefinitionView definition,
        string resourceKind,
        string resourceUri,
        Uri resolvedUri,
        List<ResolvedSourceDocument> documents,
        bool skipUnsupportedJson,
        CancellationToken cancellationToken)
    {
        if (documents.Count >= MaxResolvedDocuments)
        {
            throw new InvalidDataException(
                $"Hosted source resolved more than {MaxResolvedDocuments} importable documents.");
        }
    
        var json = await FetchTextAsync(resolvedUri, cancellationToken);
        string filtered;
        int selectedEntityCount;
        try
        {
            filtered = FiveEToolsDocumentInspector.FilterBySourceCodes(
                json,
                definition.EditionKey,
                definition.IncludedSourceCodes,
                out selectedEntityCount);
        }
        catch (Exception exception) when (
            skipUnsupportedJson
            && (exception is JsonException || exception is InvalidDataException))
        {
            return;
        }
    
        if (selectedEntityCount == 0)
        {
            return;
        }
    
        documents.Add(new ResolvedSourceDocument(
            resourceKind,
            resourceUri,
            resolvedUri.AbsoluteUri,
            selectedEntityCount,
            filtered));
    }
    
    private async Task<string> FetchTextAsync(Uri uri, CancellationToken cancellationToken)
    {
        await HostedSourceUriPolicy.EnsureRemoteSafeAsync(uri, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.ParseAdd("application/json, text/json;q=0.9, */*;q=0.1");
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    
        if ((int)response.StatusCode is >= 300 and < 400)
        {
            throw new HttpRequestException(
                $"Hosted source '{uri}' redirected to another location. Register the final canonical HTTPS URL instead of relying on redirects.");
        }
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxRemoteDocumentBytes)
        {
            throw new InvalidDataException(
                $"Hosted source '{uri}' exceeds the {MaxRemoteDocumentBytes / (1024 * 1024)} MiB per-document limit.");
        }
    
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.LongLength > MaxRemoteDocumentBytes)
        {
            throw new InvalidDataException(
                $"Hosted source '{uri}' exceeds the {MaxRemoteDocumentBytes / (1024 * 1024)} MiB per-document limit.");
        }
        return Encoding.UTF8.GetString(bytes);
    }
    
    
    private static string BuildAggregateJson(IReadOnlyList<ResolvedSourceDocument> documents)
    {
        var buckets = new SortedDictionary<string, List<JsonElement>>(StringComparer.Ordinal);
        foreach (var resolved in documents)
        {
            using var document = JsonDocument.Parse(resolved.Json);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!FiveEToolsDocumentInspector.IsImportableArray(property))
                {
                    continue;
                }
    
                if (!buckets.TryGetValue(property.Name, out var items))
                {
                    items = [];
                    buckets[property.Name] = items;
                }
    
                foreach (var item in property.Value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        items.Add(item.Clone());
                    }
                }
            }
        }
    
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var bucket in buckets)
            {
                writer.WritePropertyName(bucket.Key);
                writer.WriteStartArray();
                foreach (var item in bucket.Value)
                {
                    item.WriteTo(writer);
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    
    
    private static HostedSourceResolvedDocument ToView(ResolvedSourceDocument document) =>
        new(
            document.ResourceKind,
            document.ResourceUri,
            document.ResolvedUri,
            document.SelectedEntityCount);
    
    
    private static GitHubTreeLocation ParseGitHubTreeUri(Uri uri)
    {
        if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A github-tree resource must use a github.com tree URL.");
        }
        var segments = uri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        if (segments.Length < 4
            || !string.Equals(segments[2], "tree", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "A github-tree resource must use https://github.com/{owner}/{repository}/tree/{ref}/{optional-path}.");
        }
    
        return new GitHubTreeLocation(
            segments[0],
            segments[1],
            segments[3],
            segments.Length > 4 ? string.Join('/', segments.Skip(4)) : string.Empty);
    }
    
    private static Uri BuildGitHubRawUri(GitHubTreeLocation tree, string path)
    {
        var escapedPath = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
        return new Uri(
            $"https://raw.githubusercontent.com/{Uri.EscapeDataString(tree.Owner)}/{Uri.EscapeDataString(tree.Repository)}/{Uri.EscapeDataString(tree.Reference)}/{escapedPath}");
    }
    
    private static void CollectJsonReferences(JsonElement element, ICollection<string> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    CollectJsonReferences(property.Value, values);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectJsonReferences(item, values);
                }
                break;
            case JsonValueKind.String:
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value)
                    && value.Trim().EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    values.Add(value.Trim());
                }
                break;
        }
    }
    
    private static Uri ResolveIndexReference(Uri indexUri, string value)
    {
        if (!Uri.TryCreate(indexUri, value, out var resolved))
        {
            throw new InvalidDataException(
                $"JSON index contained an invalid document reference '{value}'.");
        }
        HostedSourceUriPolicy.ValidateShape(resolved);
        return resolved;
    }
    
    
    internal static HttpClient CreateSharedHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.GZip
                | DecompressionMethods.Deflate
                | DecompressionMethods.Brotli
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DorksAndDice-RulesCore/1.0");
        return client;
    }
    
    
    private sealed record ResolvedSourceDocument(
        string ResourceKind,
        string ResourceUri,
        string ResolvedUri,
        int SelectedEntityCount,
        string Json);
    
    
    private sealed record GitHubTreeLocation(
        string Owner,
        string Repository,
        string Reference,
        string Path);
    
}

internal sealed record HostedSourceRemoteResolution(
    IReadOnlyList<HostedSourceResolvedDocument> Documents,
    string AggregateJson);
