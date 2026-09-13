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

public sealed class CurrentUserSourceService : ICurrentUserSourceService
{
    private const long MaxRemoteDocumentBytes = 64L * 1024L * 1024L;
    private const int MaxResolvedDocuments = 2000;
    private static readonly HttpClient SharedHttpClient = CreateSharedHttpClient();

    private readonly RulesCoreDbContext dbContext;
    private readonly INormalizedSourceImportService importer;
    private readonly ISourceFormatAdapterRegistry adapters;
    private readonly ISourceGrantService grants;
    private readonly HttpClient httpClient;

    // Compatibility constructor retained for existing integration/bootstrap callers while
    // current-user ingestion moves to the normalized adapter pipeline.
    public CurrentUserSourceService(
        RulesCoreDbContext dbContext,
        ISourceImportService legacyImporter,
        ISourceGrantService grants)
        : this(
            dbContext,
            new NormalizedSourceImportService(dbContext),
            CreateDefaultRegistry(),
            grants,
            SharedHttpClient)
    {
        _ = legacyImporter;
    }

    public CurrentUserSourceService(
        RulesCoreDbContext dbContext,
        ISourceImportService legacyImporter,
        ISourceGrantService grants,
        HttpClient httpClient)
        : this(
            dbContext,
            new NormalizedSourceImportService(dbContext),
            CreateDefaultRegistry(),
            grants,
            httpClient)
    {
        _ = legacyImporter;
    }

    public CurrentUserSourceService(
        RulesCoreDbContext dbContext,
        INormalizedSourceImportService importer,
        ISourceFormatAdapterRegistry adapters,
        ISourceGrantService grants,
        HttpClient httpClient)
    {
        this.dbContext = dbContext;
        this.importer = importer;
        this.adapters = adapters;
        this.grants = grants;
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<CurrentUserSourceView>> ListAsync(
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId(currentUserId);
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
                    current_user_source_id,
                    source_kind,
                    display_name,
                    source_url,
                    source_package_id,
                    source_codes_json::text AS source_codes_json,
                    entity_count,
                    added_at,
                    refreshed_at
                FROM current_user_source
                WHERE user_id = @user_id
                ORDER BY added_at DESC, current_user_source_id;
                """;
            AddParameter(command, "@user_id", userId);

            var results = new List<CurrentUserSourceView>();
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

    public async Task<CurrentUserSourceView> AddAsync(
        string currentUserId,
        AddCurrentUserSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = RequireUserId(currentUserId);
        var kind = NormalizeKind(request.Kind);

        string displayName;
        string? sourceUrl;
        string originIdentity;
        string provider;
        IReadOnlyList<NormalizedSourceRepresentation> representations;

        if (kind == CurrentUserSourceKinds.Upload)
        {
            var bytes = ReadUploadBytes(request);
            var fileName = NormalizeOptional(request.FileName, 500)
                ?? (request.Content is not null ? "Uploaded source.json" : "Uploaded source");
            var hash = Fingerprint(bytes);
            var artifact = new SourceRepresentationArtifact(
                fileName,
                bytes,
                $"upload:{hash}",
                MediaType: null);
            var representation = adapters.TryRead(artifact)
                ?? throw new InvalidDataException("The uploaded file is not compatible with Rules Core.");
            representations = [representation];
            displayName = fileName;
            sourceUrl = null;
            originIdentity = artifact.OriginIdentity;
            provider = "user-upload";
        }
        else
        {
            var uri = RequireWebSourceUri(request.Url);
            representations = await ResolveWebSourceAsync(uri, cancellationToken);
            if (representations.Count == 0)
            {
                throw new InvalidDataException("The Web source did not contain any compatible files.");
            }
            displayName = WebSourceDisplayName(uri);
            sourceUrl = uri.AbsoluteUri;
            originIdentity = $"web:{NormalizeWebOrigin(uri)}";
            provider = uri.Host;
        }

        var originKey = Fingerprint(Encoding.UTF8.GetBytes(originIdentity));
        var packageKey = $"user-source-{Fingerprint(Encoding.UTF8.GetBytes($"{userId}\n{originIdentity}"))[..24]}";
        var packageDisplayName = kind == CurrentUserSourceKinds.Web
            ? $"Web source {sourceUrl}"
            : $"Uploaded source {originKey[..12]}";

        Guid? packageId = null;
        var entityCount = 0;
        var publicationKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var representation in representations)
        {
            var imported = await importer.ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    packageDisplayName,
                    provider,
                    License: null,
                    IsPublic: false,
                    representation),
                cancellationToken);
            packageId ??= imported.PackageId;
            if (packageId != imported.PackageId)
            {
                throw new InvalidOperationException(
                    "One added source unexpectedly resolved to multiple source packages.");
            }
            entityCount += imported.Entities.Count;
            foreach (var key in imported.SourceCodes)
            {
                publicationKeys.Add(key);
            }
        }

        if (packageId is null || publicationKeys.Count == 0)
        {
            throw new InvalidDataException(
                kind == CurrentUserSourceKinds.Web
                    ? "The Web source did not contain any compatible files."
                    : "The uploaded file is not compatible with Rules Core.");
        }

        await grants.GrantAsync(userId, packageId.Value, cancellationToken);
        await EnsureSchemaAsync(cancellationToken);
        return await UpsertRegistrationAsync(
            userId,
            kind,
            displayName,
            sourceUrl,
            packageId.Value,
            originKey,
            publicationKeys.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            entityCount,
            cancellationToken);
    }

    public async Task<CurrentUserSourceView?> RefreshAsync(
        string currentUserId,
        Guid currentUserSourceId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId(currentUserId);
        if (currentUserSourceId == Guid.Empty)
        {
            throw new ArgumentException("Source ID can not be empty.", nameof(currentUserSourceId));
        }

        var existing = (await ListAsync(userId, cancellationToken))
            .SingleOrDefault(value => value.Id == currentUserSourceId);
        if (existing is null)
        {
            return null;
        }
        if (!string.Equals(existing.Kind, CurrentUserSourceKinds.Web, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(existing.Url))
        {
            throw new InvalidOperationException(
                "Uploaded files are immutable snapshots and can not be refreshed. Upload the newer file as a source instead.");
        }

        return await AddAsync(
            userId,
            new AddCurrentUserSourceRequest(CurrentUserSourceKinds.Web, Url: existing.Url),
            cancellationToken);
    }

    private async Task<IReadOnlyList<NormalizedSourceRepresentation>> ResolveWebSourceAsync(
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
        var tree = ParseGitHubTreeUri(sourceUri);
        var treeApiUri = new Uri(
            $"https://api.github.com/repos/{Uri.EscapeDataString(tree.Owner)}/{Uri.EscapeDataString(tree.Repository)}/git/trees/{Uri.EscapeDataString(tree.Reference)}?recursive=1");
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

        var results = new List<NormalizedSourceRepresentation>();
        foreach (var path in paths)
        {
            var rawUri = BuildGitHubRawUri(tree, path);
            FetchedDocument fetched;
            try
            {
                fetched = await FetchBytesAsync(rawUri, cancellationToken);
            }
            catch (HttpRequestException)
            {
                continue;
            }

            var artifact = new SourceRepresentationArtifact(
                Path.GetFileName(path),
                fetched.Bytes,
                $"web:{NormalizeWebOrigin(sourceUri)}#{path}",
                rawUri.AbsoluteUri,
                fetched.MediaType);
            var representation = adapters.TryRead(artifact);
            if (representation is not null)
            {
                results.Add(representation);
            }
        }

        if (results.Count == 0)
        {
            throw new InvalidDataException("The Web source did not contain any compatible files.");
        }
        return results;
    }

    private async Task<FetchedDocument> FetchBytesAsync(Uri uri, CancellationToken cancellationToken)
    {
        await EnsureRemoteUriSafeAsync(uri, cancellationToken);
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

    private async Task<CurrentUserSourceView> UpsertRegistrationAsync(
        string userId,
        string kind,
        string displayName,
        string? sourceUrl,
        Guid packageId,
        string originKey,
        IReadOnlyList<string> publicationKeys,
        int entityCount,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var sourceCodesJson = JsonSerializer.Serialize(publicationKeys);
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
                INSERT INTO current_user_source (
                    current_user_source_id,
                    user_id,
                    source_kind,
                    display_name,
                    source_url,
                    source_package_id,
                    origin_key,
                    source_codes_json,
                    entity_count,
                    added_at,
                    refreshed_at)
                VALUES (
                    @id,
                    @user_id,
                    @kind,
                    @display_name,
                    @source_url,
                    @package_id,
                    @origin_key,
                    CAST(@source_codes_json AS jsonb),
                    @entity_count,
                    @added_at,
                    @refreshed_at)
                ON CONFLICT (user_id, origin_key)
                DO UPDATE SET
                    source_kind = EXCLUDED.source_kind,
                    display_name = EXCLUDED.display_name,
                    source_url = EXCLUDED.source_url,
                    source_package_id = EXCLUDED.source_package_id,
                    source_codes_json = EXCLUDED.source_codes_json,
                    entity_count = EXCLUDED.entity_count,
                    refreshed_at = EXCLUDED.refreshed_at
                RETURNING
                    current_user_source_id,
                    source_kind,
                    display_name,
                    source_url,
                    source_package_id,
                    source_codes_json::text AS source_codes_json,
                    entity_count,
                    added_at,
                    refreshed_at;
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@user_id", userId);
            AddParameter(command, "@kind", kind);
            AddParameter(command, "@display_name", displayName);
            AddNullableParameter(command, "@source_url", sourceUrl);
            AddParameter(command, "@package_id", packageId);
            AddParameter(command, "@origin_key", originKey);
            AddParameter(command, "@source_codes_json", sourceCodesJson);
            AddParameter(command, "@entity_count", entityCount);
            AddParameter(command, "@added_at", now);
            AddParameter(command, "@refreshed_at", now);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Added source was not readable after it was saved.");
            }
            return ReadView(reader);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private Task EnsureSchemaAsync(CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(CurrentUserSourceSchemaSql, cancellationToken);

    private static byte[] ReadUploadBytes(AddCurrentUserSourceRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ContentBase64))
        {
            try
            {
                var bytes = Convert.FromBase64String(request.ContentBase64);
                if (bytes.Length == 0)
                {
                    throw new InvalidDataException("Uploaded source file can not be empty.");
                }
                return bytes;
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException("The uploaded file content is not valid Base64.", exception);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Content))
        {
            return Encoding.UTF8.GetBytes(request.Content);
        }
        throw new InvalidDataException("Uploaded source file can not be empty.");
    }

    private static CurrentUserSourceView ReadView(DbDataReader reader)
    {
        var sourceCodesJson = reader.GetString(reader.GetOrdinal("source_codes_json"));
        var sourceCodes = JsonSerializer.Deserialize<string[]>(sourceCodesJson) ?? [];
        return new CurrentUserSourceView(
            reader.GetGuid(reader.GetOrdinal("current_user_source_id")),
            reader.GetString(reader.GetOrdinal("source_kind")),
            reader.GetString(reader.GetOrdinal("display_name")),
            GetNullableString(reader, "source_url"),
            reader.GetGuid(reader.GetOrdinal("source_package_id")),
            sourceCodes.Length,
            reader.GetInt32(reader.GetOrdinal("entity_count")),
            sourceCodes,
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("added_at")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("refreshed_at")));
    }

    private static ISourceFormatAdapterRegistry CreateDefaultRegistry() =>
        new SourceFormatAdapterRegistry([
            new FiveEToolsSourceFormatAdapter(),
            new PdfSourceFormatAdapter()
        ]);

    private static Uri RequireWebSourceUri(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Web source URL must be an absolute HTTPS URL.", nameof(value));
        }
        if (!string.IsNullOrEmpty(uri.UserInfo)
            || string.Equals(uri.DnsSafeHost, "localhost", StringComparison.OrdinalIgnoreCase)
            || uri.AbsoluteUri.Length > 2000)
        {
            throw new ArgumentException("Web source URL is not allowed.", nameof(value));
        }
        return uri;
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

    private static Uri BuildGitHubRawUri(GitHubTreeLocation tree, string path)
    {
        var escapedPath = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
        return new Uri(
            $"https://raw.githubusercontent.com/{Uri.EscapeDataString(tree.Owner)}/{Uri.EscapeDataString(tree.Repository)}/{Uri.EscapeDataString(tree.Reference)}/{escapedPath}");
    }

    private static async Task EnsureRemoteUriSafeAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(uri.DnsSafeHost, out var literal))
        {
            if (!IsPublicAddress(literal))
            {
                throw new InvalidOperationException(
                    $"Web source address '{uri.DnsSafeHost}' is not a public network address.");
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
                $"Web source host '{uri.DnsSafeHost}' could not be resolved.",
                exception);
        }
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
        {
            throw new InvalidOperationException(
                $"Web source host '{uri.DnsSafeHost}' resolves to a non-public network address.");
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

    private static string WebSourceDisplayName(Uri uri)
    {
        if (string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                return $"{segments[0]}/{segments[1]}";
            }
        }
        return uri.Host + uri.AbsolutePath.TrimEnd('/');
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

    private static string NormalizeKind(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Source kind can not be blank.", nameof(value));
        }
        var normalized = value.Trim().ToLowerInvariant();
        if (!CurrentUserSourceKinds.IsSupported(normalized))
        {
            throw new ArgumentException("Source kind must be 'upload' or 'web'.", nameof(value));
        }
        return normalized;
    }

    private static string RequireUserId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(value));
        }
        var normalized = value.Trim();
        if (normalized.Length > 200)
        {
            throw new ArgumentException("User ID can not exceed 200 characters.", nameof(value));
        }
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string Fingerprint(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static HttpClient CreateSharedHttpClient() => new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    })
    {
        Timeout = TimeSpan.FromMinutes(2)
    };

    private static string? GetNullableString(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void AddNullableParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private sealed record GitHubTreeLocation(
        string Owner,
        string Repository,
        string Reference,
        string Path);

    private sealed record FetchedDocument(byte[] Bytes, string? MediaType);

    private const string CurrentUserSourceSchemaSql = """
        CREATE TABLE IF NOT EXISTS current_user_source (
            current_user_source_id uuid NOT NULL,
            user_id varchar(200) NOT NULL,
            source_kind varchar(20) NOT NULL,
            display_name varchar(500) NOT NULL,
            source_url varchar(2000) NULL,
            source_package_id uuid NOT NULL,
            origin_key varchar(64) NOT NULL,
            source_codes_json jsonb NOT NULL,
            entity_count integer NOT NULL,
            added_at timestamp with time zone NOT NULL,
            refreshed_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_current_user_source PRIMARY KEY (current_user_source_id),
            CONSTRAINT fk_current_user_source_package FOREIGN KEY (source_package_id)
                REFERENCES source_package(source_package_id) ON DELETE CASCADE,
            CONSTRAINT ck_current_user_source_kind CHECK (source_kind IN ('upload', 'web')));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_current_user_source_user_origin
            ON current_user_source(user_id, origin_key);
        CREATE INDEX IF NOT EXISTS ix_current_user_source_user
            ON current_user_source(user_id, added_at DESC);
        ALTER TABLE current_user_source
            ADD COLUMN IF NOT EXISTS upstream_version varchar(500) NULL;
        ALTER TABLE current_user_source
            ADD COLUMN IF NOT EXISTS last_checked_at timestamp with time zone NULL;
        ALTER TABLE current_user_source
            ADD COLUMN IF NOT EXISTS last_refresh_error varchar(1000) NULL;
        CREATE INDEX IF NOT EXISTS ix_current_user_source_due_web_refresh
            ON current_user_source(last_checked_at)
            WHERE source_kind = 'web';
        """;
}
