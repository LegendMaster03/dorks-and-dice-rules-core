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

public sealed class HostedSourceService : IHostedSourceService
{
    private const long MaxRemoteDocumentBytes = 64L * 1024L * 1024L;
    private const int MaxResolvedDocuments = 2000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HttpClient SharedHttpClient = CreateSharedHttpClient();

    private readonly RulesCoreDbContext dbContext;
    private readonly ISourceImportService importer;
    private readonly HttpClient httpClient;

    public HostedSourceService(
        RulesCoreDbContext dbContext,
        ISourceImportService importer)
        : this(dbContext, importer, SharedHttpClient)
    {
    }

    public HostedSourceService(
        RulesCoreDbContext dbContext,
        ISourceImportService importer,
        HttpClient httpClient)
    {
        this.dbContext = dbContext;
        this.importer = importer;
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<HostedSourceDefinitionView>> ListAsync(
        bool includeDisabled,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    d.hosted_source_definition_id,
                    d.definition_key,
                    r.revision_number,
                    r.fingerprint,
                    r.config_json::text AS config_json,
                    r.created_by_user_id,
                    r.created_at
                FROM hosted_source_definition d
                JOIN LATERAL (
                    SELECT revision_number, fingerprint, config_json, created_by_user_id, created_at
                    FROM hosted_source_definition_revision
                    WHERE hosted_source_definition_id = d.hosted_source_definition_id
                    ORDER BY revision_number DESC
                    LIMIT 1
                ) r ON TRUE
                WHERE @include_disabled
                    OR COALESCE((r.config_json ->> 'isEnabled')::boolean, TRUE)
                ORDER BY d.definition_key;
                """;
            AddParameter(command, "@include_disabled", includeDisabled);

            var results = new List<HostedSourceDefinitionView>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(ReadView(reader));
            }

            return results;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<HostedSourceDefinitionView?> GetAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        if (definitionId == Guid.Empty)
        {
            throw new ArgumentException("Definition ID can not be empty.", nameof(definitionId));
        }

        await EnsureSchemaAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    d.hosted_source_definition_id,
                    d.definition_key,
                    r.revision_number,
                    r.fingerprint,
                    r.config_json::text AS config_json,
                    r.created_by_user_id,
                    r.created_at
                FROM hosted_source_definition d
                JOIN LATERAL (
                    SELECT revision_number, fingerprint, config_json, created_by_user_id, created_at
                    FROM hosted_source_definition_revision
                    WHERE hosted_source_definition_id = d.hosted_source_definition_id
                    ORDER BY revision_number DESC
                    LIMIT 1
                ) r ON TRUE
                WHERE d.hosted_source_definition_id = @id;
                """;
            AddParameter(command, "@id", definitionId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? ReadView(reader) : null;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<HostedSourceDefinitionView> SetAsync(
        string definitionKey,
        SetHostedSourceDefinitionRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var key = NormalizeDefinitionKey(definitionKey);
        var actor = Require(actorUserId, 200, nameof(actorUserId));
        var config = NormalizeRequest(request);
        var configJson = JsonSerializer.Serialize(config, JsonOptions);
        var fingerprint = Fingerprint(configJson);

        await EnsureSchemaAsync(cancellationToken);
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

        var view = await GetAsync(definitionId, cancellationToken)
            ?? throw new InvalidOperationException("Hosted source definition was not readable after it was saved.");
        return view with { CreatedRevision = createdRevision };
    }

    public async Task<IReadOnlyList<HostedSourceMatchView>> FindMatchesAsync(
        IReadOnlyCollection<string> sourceCodes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceCodes);
        var requested = FiveEToolsDocumentInspector.NormalizeSourceCodes(sourceCodes);
        if (requested.Count == 0)
        {
            return [];
        }

        var definitions = await ListAsync(includeDisabled: false, cancellationToken);
        var matches = new List<HostedSourceMatchView>();
        foreach (var definition in definitions)
        {
            if (!string.Equals(
                    definition.FormatKind,
                    HostedSourceFormatKinds.FiveEToolsJson,
                    StringComparison.Ordinal)
                || definition.IncludedSourceCodes.Count == 0)
            {
                continue;
            }

            var included = FiveEToolsDocumentInspector.NormalizeSourceCodes(
                definition.IncludedSourceCodes);
            var matched = included
                .Where(requested.Contains)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (matched.Length == 0)
            {
                continue;
            }

            matches.Add(new HostedSourceMatchView(
                definition.Id,
                definition.Key,
                definition.DisplayName,
                definition.RevisionNumber,
                definition.PackageKey,
                definition.WorkKey,
                definition.EditionKey,
                definition.IncludedSourceCodes,
                matched,
                requested.SetEquals(included)));
        }

        return matches
            .OrderByDescending(value => value.ExactSourceCodeMatch)
            .ThenByDescending(value => value.MatchedSourceCodes.Count)
            .ThenBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<HostedSourcePreviewView> PreviewAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        var definition = await RequireEnabledDefinitionAsync(definitionId, cancellationToken);
        var resolved = await ResolveAsync(definition, cancellationToken);
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
        var definition = await RequireEnabledDefinitionAsync(definitionId, cancellationToken);
        var resolved = await ResolveAsync(definition, cancellationToken);
        var import = await importer.Import5eToolsDocumentAsync(
            BuildImportRequest(definition, resolved.AggregateJson),
            cancellationToken);
        return new HostedSourceRefreshView(
            definition,
            resolved.Documents.Select(ToView).ToArray(),
            import);
    }

    private async Task<HostedSourceDefinitionView> RequireEnabledDefinitionAsync(
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        var definition = await GetAsync(definitionId, cancellationToken)
            ?? throw new KeyNotFoundException("Hosted source definition was not found.");
        if (!definition.IsEnabled)
        {
            throw new InvalidOperationException("Hosted source definition is disabled.");
        }
        if (!string.Equals(
                definition.FormatKind,
                HostedSourceFormatKinds.FiveEToolsJson,
                StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Hosted source format '{definition.FormatKind}' is not supported by this resolver.");
        }
        return definition;
    }

    private async Task<ResolvedSourceSet> ResolveAsync(
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

        return new ResolvedSourceSet(documents, BuildAggregateJson(documents));
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
        await EnsureRemoteUriSafeAsync(uri, cancellationToken);
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

    private static StoredDefinitionConfig NormalizeRequest(SetHostedSourceDefinitionRequest request)
    {
        var format = Require(request.FormatKind, 80, nameof(request.FormatKind)).ToLowerInvariant();
        if (!string.Equals(format, HostedSourceFormatKinds.FiveEToolsJson, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Hosted source format must currently be '{HostedSourceFormatKinds.FiveEToolsJson}'.",
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

        var resources = request.Resources
            .Select(NormalizeResource)
            .DistinctBy(value => $"{value.Kind}\n{value.Uri}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value.Kind, StringComparer.Ordinal)
            .ThenBy(value => value.Uri, StringComparer.Ordinal)
            .ToArray();
        var sourceCodes = FiveEToolsDocumentInspector.NormalizeSourceCodes(request.IncludedSourceCodes)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var gameEdition = string.IsNullOrWhiteSpace(request.GameEdition)
            ? null
            : DndEditionCatalog.NormalizeImportLabel(request.GameEdition);
        var releaseKind = SourceReleaseKinds.NormalizeImportLabel(request.ReleaseKind);

        return new StoredDefinitionConfig(
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

    private static HostedSourceResourceView NormalizeResource(HostedSourceResourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var kind = Require(request.Kind, 80, nameof(request.Kind)).ToLowerInvariant();
        if (!HostedSourceResourceKinds.IsSupported(kind))
        {
            throw new ArgumentException(
                $"Hosted source resource kind '{kind}' is not supported.",
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
        ValidateUriShape(resolved);
        return resolved;
    }

    private static HostedSourceDefinitionView ReadView(DbDataReader reader)
    {
        var configJson = reader.GetString(reader.GetOrdinal("config_json"));
        var config = JsonSerializer.Deserialize<StoredDefinitionConfig>(configJson, JsonOptions)
            ?? throw new InvalidDataException("Hosted source configuration could not be deserialized.");
        return new HostedSourceDefinitionView(
            reader.GetGuid(reader.GetOrdinal("hosted_source_definition_id")),
            reader.GetString(reader.GetOrdinal("definition_key")),
            reader.GetInt32(reader.GetOrdinal("revision_number")),
            reader.GetString(reader.GetOrdinal("fingerprint")),
            config.DisplayName,
            config.FormatKind,
            config.PackageKey,
            config.PackageDisplayName,
            config.Provider,
            config.License,
            config.IsPublic,
            config.WorkKey,
            config.WorkDisplayName,
            config.EditionKey,
            config.EditionDisplayName,
            config.GameEdition,
            config.ReleaseKind,
            config.PublicationDate,
            config.IncludedSourceCodes,
            config.Resources,
            config.IsEnabled,
            config.Note,
            reader.GetString(reader.GetOrdinal("created_by_user_id")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")));
    }

    private Task EnsureSchemaAsync(CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(SchemaSql, cancellationToken);

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

    private sealed record StoredDefinitionConfig(
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

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS hosted_source_definition (
            hosted_source_definition_id uuid NOT NULL,
            definition_key varchar(200) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_hosted_source_definition PRIMARY KEY (hosted_source_definition_id));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_hosted_source_definition_key
            ON hosted_source_definition(definition_key);

        CREATE TABLE IF NOT EXISTS hosted_source_definition_revision (
            hosted_source_definition_revision_id uuid NOT NULL,
            hosted_source_definition_id uuid NOT NULL,
            revision_number integer NOT NULL,
            fingerprint varchar(64) NOT NULL,
            config_json jsonb NOT NULL,
            created_by_user_id varchar(200) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_hosted_source_definition_revision PRIMARY KEY (hosted_source_definition_revision_id),
            CONSTRAINT fk_hosted_source_definition_revision_definition FOREIGN KEY (hosted_source_definition_id)
                REFERENCES hosted_source_definition(hosted_source_definition_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_hosted_source_definition_revision_number
            ON hosted_source_definition_revision(hosted_source_definition_id, revision_number);
        CREATE INDEX IF NOT EXISTS ix_hosted_source_definition_revision_fingerprint
            ON hosted_source_definition_revision(fingerprint);
        """;
}
