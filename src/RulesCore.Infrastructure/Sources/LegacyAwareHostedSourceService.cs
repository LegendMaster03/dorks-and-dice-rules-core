using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed class LegacyAwareHostedSourceService : IHostedSourceService
{
    private const long MaxRemoteDocumentBytes = 64L * 1024L * 1024L;
    private const int MaxResolvedDocuments = 2000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HttpClient SharedHttpClient = CreateSharedHttpClient();

    private readonly RulesCoreDbContext dbContext;
    private readonly ISourceImportService importer;
    private readonly HttpClient httpClient;
    private readonly HostedSourceService inner;

    public LegacyAwareHostedSourceService(
        RulesCoreDbContext dbContext,
        ISourceImportService importer)
        : this(dbContext, importer, SharedHttpClient)
    {
    }

    public LegacyAwareHostedSourceService(
        RulesCoreDbContext dbContext,
        ISourceImportService importer,
        HttpClient httpClient)
    {
        this.dbContext = dbContext;
        this.importer = importer;
        this.httpClient = httpClient;
        inner = new HostedSourceService(dbContext, importer, httpClient);
    }

    public Task<IReadOnlyList<HostedSourceDefinitionView>> ListAsync(
        bool includeDisabled,
        CancellationToken cancellationToken = default) =>
        inner.ListAsync(includeDisabled, cancellationToken);

    public Task<HostedSourceDefinitionView?> GetAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default) =>
        inner.GetAsync(definitionId, cancellationToken);

    public Task<IReadOnlyList<HostedSourceMatchView>> FindMatchesAsync(
        IReadOnlyCollection<string> sourceCodes,
        CancellationToken cancellationToken = default) =>
        inner.FindMatchesAsync(sourceCodes, cancellationToken);

    public Task<HostedSourceDefinitionView> SetAsync(
        string definitionKey,
        SetHostedSourceDefinitionRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var format = request.FormatKind?.Trim().ToLowerInvariant();
        return string.Equals(format, HostedSourceFormatKinds.LegacySrdText, StringComparison.Ordinal)
            ? SetLegacyAsync(definitionKey, request, actorUserId, cancellationToken)
            : inner.SetAsync(definitionKey, request, actorUserId, cancellationToken);
    }

    public async Task<HostedSourcePreviewView> PreviewAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        var definition = await RequireDefinitionAsync(definitionId, cancellationToken);
        if (!string.Equals(
                definition.FormatKind,
                HostedSourceFormatKinds.LegacySrdText,
                StringComparison.Ordinal))
        {
            return await inner.PreviewAsync(definitionId, cancellationToken);
        }

        var resolved = await ResolveLegacyAsync(definition, cancellationToken);
        var preview = await importer.Preview5eToolsDocumentAsync(
            BuildImportRequest(definition, resolved.AggregateJson),
            cancellationToken);
        return new HostedSourcePreviewView(
            definition,
            resolved.Documents.Select(ToView).ToArray(),
            preview);
    }

    public async Task<HostedSourceRefreshView> RefreshAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        var definition = await RequireDefinitionAsync(definitionId, cancellationToken);
        if (!string.Equals(
                definition.FormatKind,
                HostedSourceFormatKinds.LegacySrdText,
                StringComparison.Ordinal))
        {
            return await inner.RefreshAsync(definitionId, cancellationToken);
        }

        var resolved = await ResolveLegacyAsync(definition, cancellationToken);
        var import = await importer.Import5eToolsDocumentAsync(
            BuildImportRequest(definition, resolved.AggregateJson),
            cancellationToken);
        return new HostedSourceRefreshView(
            definition,
            resolved.Documents.Select(ToView).ToArray(),
            import);
    }

    private async Task<HostedSourceDefinitionView> SetLegacyAsync(
        string definitionKey,
        SetHostedSourceDefinitionRequest request,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var key = NormalizeDefinitionKey(definitionKey);
        var actor = Require(actorUserId, 200, nameof(actorUserId));
        var config = NormalizeLegacyRequest(request);
        var configJson = JsonSerializer.Serialize(config, JsonOptions);
        var fingerprint = Fingerprint(configJson);

        // HostedSourceService owns the shared schema. Listing once ensures the same tables exist
        // without duplicating schema ownership in this legacy adapter.
        _ = await inner.ListAsync(includeDisabled: true, cancellationToken);

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        Guid definitionId;
        var createdRevision = false;
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        try
        {
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    SELECT hosted_source_definition_id
                    FROM hosted_source_definition
                    WHERE definition_key = @key
                    FOR UPDATE;
                    """;
                AddParameter(command, "@key", key);
                var existing = await command.ExecuteScalarAsync(cancellationToken);
                if (existing is Guid existingId)
                {
                    definitionId = existingId;
                }
                else
                {
                    definitionId = Guid.NewGuid();
                    command.CommandText = """
                        INSERT INTO hosted_source_definition (
                            hosted_source_definition_id,
                            definition_key,
                            created_at)
                        VALUES (@id, @key, @created_at);
                        """;
                    command.Parameters.Clear();
                    AddParameter(command, "@id", definitionId);
                    AddParameter(command, "@key", key);
                    AddParameter(command, "@created_at", DateTimeOffset.UtcNow);
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            var latestRevisionNumber = 0;
            string? latestFingerprint = null;
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    SELECT revision_number, fingerprint
                    FROM hosted_source_definition_revision
                    WHERE hosted_source_definition_id = @id
                    ORDER BY revision_number DESC
                    LIMIT 1;
                    """;
                AddParameter(command, "@id", definitionId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    latestRevisionNumber = reader.GetInt32(0);
                    latestFingerprint = reader.GetString(1);
                }
            }

            if (!string.Equals(latestFingerprint, fingerprint, StringComparison.Ordinal))
            {
                createdRevision = true;
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO hosted_source_definition_revision (
                        hosted_source_definition_revision_id,
                        hosted_source_definition_id,
                        revision_number,
                        fingerprint,
                        config_json,
                        created_by_user_id,
                        created_at)
                    VALUES (
                        @revision_id,
                        @definition_id,
                        @revision_number,
                        @fingerprint,
                        CAST(@config_json AS jsonb),
                        @created_by,
                        @created_at);
                    """;
                AddParameter(command, "@revision_id", Guid.NewGuid());
                AddParameter(command, "@definition_id", definitionId);
                AddParameter(command, "@revision_number", latestRevisionNumber + 1);
                AddParameter(command, "@fingerprint", fingerprint);
                AddParameter(command, "@config_json", configJson);
                AddParameter(command, "@created_by", actor);
                AddParameter(command, "@created_at", DateTimeOffset.UtcNow);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }

        var view = await inner.GetAsync(definitionId, cancellationToken)
            ?? throw new InvalidOperationException("Hosted source definition was not readable after it was saved.");
        return view with { CreatedRevision = createdRevision };
    }

    private async Task<HostedSourceDefinitionView> RequireDefinitionAsync(
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        var definition = await inner.GetAsync(definitionId, cancellationToken)
            ?? throw new KeyNotFoundException("Hosted source definition was not found.");
        if (!definition.IsEnabled)
        {
            throw new InvalidOperationException("Hosted source definition is disabled.");
        }
        if (!HostedSourceFormatKinds.IsSupported(definition.FormatKind))
        {
            throw new NotSupportedException(
                $"Hosted source format '{definition.FormatKind}' is not supported by this resolver.");
        }
        return definition;
    }

    private async Task<ResolvedSourceSet> ResolveLegacyAsync(
        HostedSourceDefinitionView definition,
        CancellationToken cancellationToken)
    {
        if (definition.IncludedSourceCodes.Count != 1)
        {
            throw new InvalidDataException(
                "A legacy SRD text source must define exactly one normalized source code.");
        }
        var sourceCode = definition.IncludedSourceCodes[0];
        var documents = new List<ResolvedSourceDocument>();

        foreach (var resource in definition.Resources)
        {
            switch (resource.Kind)
            {
                case HostedSourceResourceKinds.HtmlIndex:
                    await ResolveHtmlIndexAsync(
                        resource,
                        sourceCode,
                        documents,
                        cancellationToken);
                    break;
                case HostedSourceResourceKinds.GitHubTree:
                    await ResolveMarkdownGitHubTreeAsync(
                        resource,
                        sourceCode,
                        documents,
                        cancellationToken);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Legacy SRD text resource kind '{resource.Kind}' is not supported. Use html-index or github-tree.");
            }
        }

        if (documents.Count == 0)
        {
            throw new InvalidDataException("The legacy hosted source did not resolve to any importable entities.");
        }

        return new ResolvedSourceSet(documents, BuildAggregateJson(documents));
    }

    private async Task ResolveHtmlIndexAsync(
        HostedSourceResourceView resource,
        string sourceCode,
        List<ResolvedSourceDocument> documents,
        CancellationToken cancellationToken)
    {
        var indexUri = new Uri(resource.Uri, UriKind.Absolute);
        var html = await FetchTextAsync(indexUri, cancellationToken);
        var references = LegacySrdDocumentInspector.ResolveHtmlIndexReferences(html, indexUri);
        if (references.Count > MaxResolvedDocuments)
        {
            throw new InvalidDataException(
                $"Legacy HTML index expands to {references.Count} documents; the maximum is {MaxResolvedDocuments}.");
        }

        foreach (var resolvedUri in references)
        {
            await AddLegacyDocumentAsync(
                resource.Kind,
                resource.Uri,
                resolvedUri,
                sourceCode,
                documents,
                cancellationToken);
        }
    }

    private async Task ResolveMarkdownGitHubTreeAsync(
        HostedSourceResourceView resource,
        string sourceCode,
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
                "GitHub truncated the legacy SRD repository tree. Configure narrower resources instead of importing an incomplete tree.");
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
                path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrEmpty(prefix)
                    || string.Equals(path, prefix, StringComparison.Ordinal)
                    || path.StartsWith(prefix + "/", StringComparison.Ordinal)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (paths.Length > MaxResolvedDocuments)
        {
            throw new InvalidDataException(
                $"GitHub tree contains {paths.Length} Markdown documents beneath the configured path; the maximum is {MaxResolvedDocuments}.");
        }

        foreach (var path in paths)
        {
            await AddLegacyDocumentAsync(
                resource.Kind,
                resource.Uri,
                BuildGitHubRawUri(tree, path),
                sourceCode,
                documents,
                cancellationToken);
        }
    }

    private async Task AddLegacyDocumentAsync(
        string resourceKind,
        string resourceUri,
        Uri resolvedUri,
        string sourceCode,
        List<ResolvedSourceDocument> documents,
        CancellationToken cancellationToken)
    {
        if (documents.Count >= MaxResolvedDocuments)
        {
            throw new InvalidDataException(
                $"Hosted source resolved more than {MaxResolvedDocuments} importable documents.");
        }

        var content = await FetchTextAsync(resolvedUri, cancellationToken);
        var json = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            content,
            resolvedUri.AbsoluteUri,
            sourceCode,
            out var entityCount);
        if (entityCount == 0)
        {
            return;
        }

        documents.Add(new ResolvedSourceDocument(
            resourceKind,
            resourceUri,
            resolvedUri.AbsoluteUri,
            entityCount,
            json));
    }

    private async Task<string> FetchTextAsync(Uri uri, CancellationToken cancellationToken)
    {
        await EnsureRemoteUriSafeAsync(uri, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.ParseAdd(
            "application/json, text/json;q=0.9, text/markdown;q=0.85, text/html;q=0.8, text/plain;q=0.7, */*;q=0.1");
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

    private static async Task EnsureRemoteUriSafeAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        ValidateUriShape(uri);
        if (IPAddress.TryParse(uri.DnsSafeHost, out var literal))
        {
            if (!IsPublicAddress(literal))
            {
                throw new InvalidOperationException(
                    $"Hosted source address '{uri.DnsSafeHost}' is not a public network address.");
            }
            return;
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
        }
        catch (SocketException exception)
        {
            throw new HttpRequestException(
                $"Hosted source host '{uri.DnsSafeHost}' could not be resolved.",
                exception);
        }

        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
        {
            throw new InvalidOperationException(
                $"Hosted source host '{uri.DnsSafeHost}' resolves to a non-public network address.");
        }
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }
        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.None)
            || address.Equals(IPAddress.IPv6None))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return !(bytes[0] == 0
                || bytes[0] == 10
                || bytes[0] == 127
                || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || bytes[0] >= 224);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !address.IsIPv6LinkLocal
                && !address.IsIPv6Multicast
                && !(bytes[0] is 0xfc or 0xfd);
        }

        return false;
    }

    private static LegacyDefinitionConfig NormalizeLegacyRequest(SetHostedSourceDefinitionRequest request)
    {
        var format = Require(request.FormatKind, 80, nameof(request.FormatKind)).ToLowerInvariant();
        if (!string.Equals(format, HostedSourceFormatKinds.LegacySrdText, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Legacy hosted source format must be '{HostedSourceFormatKinds.LegacySrdText}'.",
                nameof(request));
        }
        if (request.Resources is null || request.Resources.Count == 0)
        {
            throw new ArgumentException("At least one hosted source resource is required.", nameof(request));
        }
        if (request.Resources.Count > 100)
        {
            throw new ArgumentException("A hosted source definition can not contain more than 100 root resources.", nameof(request));
        }

        var sourceCodes = (request.IncludedSourceCodes ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourceCodes.Length != 1)
        {
            throw new ArgumentException(
                "A legacy SRD text source must define exactly one normalized source code.",
                nameof(request));
        }

        var resources = request.Resources
            .Select(NormalizeLegacyResource)
            .DistinctBy(value => $"{value.Kind}\n{value.Uri}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value.Kind, StringComparer.Ordinal)
            .ThenBy(value => value.Uri, StringComparer.Ordinal)
            .ToArray();
        var gameEdition = string.IsNullOrWhiteSpace(request.GameEdition)
            ? null
            : DndEditionCatalog.NormalizeImportLabel(request.GameEdition);
        var releaseKind = SourceReleaseKinds.NormalizeImportLabel(request.ReleaseKind);

        return new LegacyDefinitionConfig(
            Require(request.DisplayName, 300, nameof(request.DisplayName)),
            format,
            Require(request.PackageKey, 200, nameof(request.PackageKey)).ToLowerInvariant(),
            Require(request.PackageDisplayName, 300, nameof(request.PackageDisplayName)),
            Require(request.Provider, 200, nameof(request.Provider)),
            NormalizeOptional(request.License, 300, nameof(request.License)),
            request.IsPublic,
            Require(request.WorkKey, 200, nameof(request.WorkKey)).ToLowerInvariant(),
            Require(request.WorkDisplayName, 300, nameof(request.WorkDisplayName)),
            Require(request.EditionKey, 200, nameof(request.EditionKey)).ToLowerInvariant(),
            Require(request.EditionDisplayName, 300, nameof(request.EditionDisplayName)),
            gameEdition,
            releaseKind,
            request.PublicationDate,
            sourceCodes,
            resources,
            request.IsEnabled,
            NormalizeOptional(request.Note, 2000, nameof(request.Note)));
    }

    private static HostedSourceResourceView NormalizeLegacyResource(HostedSourceResourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var kind = Require(request.Kind, 80, nameof(request.Kind)).ToLowerInvariant();
        if (!string.Equals(kind, HostedSourceResourceKinds.HtmlIndex, StringComparison.Ordinal)
            && !string.Equals(kind, HostedSourceResourceKinds.GitHubTree, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Legacy SRD text resources must use html-index or github-tree.",
                nameof(request));
        }
        if (!Uri.TryCreate(request.Uri?.Trim(), UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Hosted source resource URI must be absolute.", nameof(request));
        }
        ValidateUriShape(uri);
        if (string.Equals(kind, HostedSourceResourceKinds.GitHubTree, StringComparison.Ordinal))
        {
            _ = ParseGitHubTreeUri(uri);
        }
        return new HostedSourceResourceView(kind, uri.AbsoluteUri);
    }

    private static void ValidateUriShape(Uri uri)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Hosted source URIs must use HTTPS.");
        }
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("Hosted source URIs can not contain embedded credentials.");
        }
        if (string.Equals(uri.DnsSafeHost, "localhost", StringComparison.OrdinalIgnoreCase)
            || uri.AbsoluteUri.Length > 2000)
        {
            throw new ArgumentException("Hosted source URI is not allowed.");
        }
    }

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

    private static string BuildAggregateJson(IReadOnlyList<ResolvedSourceDocument> documents)
    {
        var buckets = new SortedDictionary<string, List<JsonElement>>(StringComparer.Ordinal);
        foreach (var resolved in documents)
        {
            using var document = JsonDocument.Parse(resolved.Json);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Array || property.Name.StartsWith('_'))
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

    private static Import5eToolsDocumentRequest BuildImportRequest(
        HostedSourceDefinitionView definition,
        string json) =>
        new(
            definition.PackageKey,
            definition.PackageDisplayName,
            definition.Provider,
            definition.License,
            definition.IsPublic,
            definition.WorkKey,
            definition.WorkDisplayName,
            definition.EditionKey,
            definition.EditionDisplayName,
            json,
            definition.GameEdition,
            definition.ReleaseKind,
            definition.PublicationDate);

    private static HostedSourceResolvedDocument ToView(ResolvedSourceDocument document) =>
        new(
            document.ResourceKind,
            document.ResourceUri,
            document.ResolvedUri,
            document.SelectedEntityCount);

    private static string NormalizeDefinitionKey(string value)
    {
        var key = Require(value, 200, nameof(value)).ToLowerInvariant();
        if (key.Any(character =>
                !(char.IsAsciiLetterOrDigit(character)
                    || character is '-' or '_' or '.')))
        {
            throw new ArgumentException(
                "Hosted source definition key may contain only ASCII letters, numbers, '-', '_', and '.'.",
                nameof(value));
        }
        return key;
    }

    private static string Require(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value can not be blank.", parameterName);
        }
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not exceed {maxLength} characters.", parameterName);
        }
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not exceed {maxLength} characters.", parameterName);
        }
        return normalized;
    }

    private static string Fingerprint(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static HttpClient CreateSharedHttpClient()
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

    private sealed record LegacyDefinitionConfig(
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
        string? Note);

    private sealed record ResolvedSourceDocument(
        string ResourceKind,
        string ResourceUri,
        string ResolvedUri,
        int SelectedEntityCount,
        string Json);

    private sealed record ResolvedSourceSet(
        IReadOnlyList<ResolvedSourceDocument> Documents,
        string AggregateJson);

    private sealed record GitHubTreeLocation(
        string Owner,
        string Repository,
        string Reference,
        string Path);
}
