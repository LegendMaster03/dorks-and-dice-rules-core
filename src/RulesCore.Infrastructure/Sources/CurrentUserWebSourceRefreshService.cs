using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed class CurrentUserWebSourceRefreshService
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(24);

    private static readonly HttpClient SharedHttpClient = CreateSharedHttpClient();

    private readonly RulesCoreDbContext dbContext;
    private readonly ISourceImportService importer;
    private readonly ISourceGrantService grants;
    private readonly HttpClient httpClient;

    public CurrentUserWebSourceRefreshService(
        RulesCoreDbContext dbContext,
        ISourceImportService importer,
        ISourceGrantService grants)
        : this(dbContext, importer, grants, SharedHttpClient)
    {
    }

    public CurrentUserWebSourceRefreshService(
        RulesCoreDbContext dbContext,
        ISourceImportService importer,
        ISourceGrantService grants,
        HttpClient httpClient)
    {
        this.dbContext = dbContext;
        this.importer = importer;
        this.grants = grants;
        this.httpClient = httpClient;
    }

    public async Task RecordInitialVersionAsync(
        Guid currentUserSourceId,
        string url,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        try
        {
            var probe = await ProbeVersionAsync(new Uri(url, UriKind.Absolute), cancellationToken);
            await MarkCheckedAsync(
                [currentUserSourceId],
                probe.Known ? probe.Token : null,
                error: null,
                cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or InvalidOperationException
            or JsonException
            or SocketException)
        {
            await MarkCheckedAsync(
                [currentUserSourceId],
                version: null,
                error: LimitError(exception.Message),
                cancellationToken);
        }
    }

    public async Task<CurrentUserSourceView?> RefreshOneAsync(
        string currentUserId,
        Guid currentUserSourceId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var registration = await ReadRegistrationAsync(
            currentUserId,
            currentUserSourceId,
            cancellationToken);
        if (registration is null)
        {
            return null;
        }
        if (!string.Equals(registration.Kind, CurrentUserSourceKinds.Web, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(registration.Url))
        {
            throw new InvalidOperationException(
                "Uploaded files are immutable snapshots and can not be refreshed. Upload the newer file as a source instead.");
        }

        var probe = await ProbeVersionAsync(new Uri(registration.Url, UriKind.Absolute), cancellationToken);
        var sourceService = new CurrentUserSourceService(dbContext, importer, grants);
        if (probe.Known
            && !string.IsNullOrWhiteSpace(registration.UpstreamVersion)
            && string.Equals(registration.UpstreamVersion, probe.Token, StringComparison.Ordinal))
        {
            await MarkCheckedAsync(
                [registration.Id],
                probe.Token,
                error: null,
                cancellationToken);
            return (await sourceService.ListAsync(currentUserId, cancellationToken))
                .SingleOrDefault(value => value.Id == registration.Id);
        }

        var refreshed = await sourceService.RefreshAsync(
            currentUserId,
            currentUserSourceId,
            cancellationToken);
        if (refreshed is null)
        {
            return null;
        }

        // CurrentUserSourceService routes refreshes through the normalized format adapter
        // pipeline, which performs canonical publication/occurrence association itself.
        // Do not invoke the legacy 5e.tools package indexer here.
        await MarkCheckedAsync(
            [registration.Id],
            probe.Known ? probe.Token : null,
            error: null,
            cancellationToken);
        return refreshed;
    }

    public async Task<int> RefreshDueAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var due = await ReadDueRegistrationsAsync(
            DateTimeOffset.UtcNow - RefreshInterval,
            cancellationToken);
        if (due.Count == 0)
        {
            return 0;
        }

        var refreshedCount = 0;
        foreach (var group in due
                     .Where(value => !string.IsNullOrWhiteSpace(value.Url))
                     .GroupBy(value => value.Url!, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            VersionProbe probe;
            try
            {
                probe = await ProbeVersionAsync(new Uri(group.Key, UriKind.Absolute), cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException
                or InvalidOperationException
                or JsonException
                or SocketException)
            {
                await MarkCheckedAsync(
                    group.Select(value => value.Id).ToArray(),
                    version: null,
                    error: LimitError(exception.Message),
                    cancellationToken);
                continue;
            }

            foreach (var registration in group)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (probe.Known
                    && !string.IsNullOrWhiteSpace(registration.UpstreamVersion)
                    && string.Equals(registration.UpstreamVersion, probe.Token, StringComparison.Ordinal))
                {
                    await MarkCheckedAsync(
                        [registration.Id],
                        probe.Token,
                        error: null,
                        cancellationToken);
                    continue;
                }

                try
                {
                    var sourceService = new CurrentUserSourceService(dbContext, importer, grants);
                    var refreshed = await sourceService.RefreshAsync(
                        registration.UserId,
                        registration.Id,
                        cancellationToken);
                    if (refreshed is not null)
                    {
                        refreshedCount++;
                    }
                    await MarkCheckedAsync(
                        [registration.Id],
                        probe.Known ? probe.Token : null,
                        error: null,
                        cancellationToken);
                }
                catch (Exception exception) when (exception is HttpRequestException
                    or InvalidOperationException
                    or InvalidDataException
                    or JsonException)
                {
                    await MarkCheckedAsync(
                        [registration.Id],
                        registration.UpstreamVersion,
                        error: LimitError(exception.Message),
                        cancellationToken);
                }
            }
        }

        return refreshedCount;
    }

    private async Task<VersionProbe> ProbeVersionAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        await EnsureRemoteUriSafeAsync(uri, cancellationToken);
        if (string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.Contains("/tree/", StringComparison.OrdinalIgnoreCase))
        {
            return await ProbeGitHubTreeVersionAsync(uri, cancellationToken);
        }

        using var request = new HttpRequestMessage(HttpMethod.Head, uri);
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
        if (response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented)
        {
            return VersionProbe.Unknown;
        }
        response.EnsureSuccessStatusCode();

        var etag = response.Headers.ETag?.ToString();
        var lastModified = response.Content.Headers.LastModified?.ToString("R");
        if (string.IsNullOrWhiteSpace(etag) && string.IsNullOrWhiteSpace(lastModified))
        {
            return VersionProbe.Unknown;
        }

        return new VersionProbe(
            Known: true,
            Token: $"http:{CanonicalSourceIdentity.Fingerprint($"{etag}\n{lastModified}")}");
    }

    private async Task<VersionProbe> ProbeGitHubTreeVersionAsync(
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        var tree = ParseGitHubTreeUri(sourceUri);
        var commitUri = new Uri(
            $"https://api.github.com/repos/{Uri.EscapeDataString(tree.Owner)}/{Uri.EscapeDataString(tree.Repository)}/commits/{Uri.EscapeDataString(tree.Reference)}");
        await EnsureRemoteUriSafeAsync(commitUri, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, commitUri);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd("dorks-and-dice-rules-core/1.0");
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!document.RootElement.TryGetProperty("sha", out var sha)
            || sha.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(sha.GetString()))
        {
            throw new InvalidDataException("GitHub did not return a commit identity for the Web source.");
        }

        return new VersionProbe(true, $"github-commit:{sha.GetString()!.Trim().ToLowerInvariant()}");
    }

    private async Task<CurrentUserSourceRegistration?> ReadRegistrationAsync(
        string userId,
        Guid currentUserSourceId,
        CancellationToken cancellationToken)
    {
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
                    user_id,
                    source_kind,
                    source_url,
                    upstream_version,
                    last_checked_at
                FROM current_user_source
                WHERE current_user_source_id = @id
                    AND user_id = @user_id;
                """;
            AddParameter(command, "@id", currentUserSourceId);
            AddParameter(command, "@user_id", userId.Trim());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken)
                ? ReadRegistration(reader)
                : null;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<IReadOnlyList<CurrentUserSourceRegistration>> ReadDueRegistrationsAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
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
                    user_id,
                    source_kind,
                    source_url,
                    upstream_version,
                    last_checked_at
                FROM current_user_source
                WHERE source_kind = 'web'
                    AND source_url IS NOT NULL
                    AND (last_checked_at IS NULL OR last_checked_at <= @cutoff)
                ORDER BY source_url, current_user_source_id;
                """;
            AddParameter(command, "@cutoff", cutoff);
            var rows = new List<CurrentUserSourceRegistration>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(ReadRegistration(reader));
            }
            return rows;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task MarkCheckedAsync(
        IReadOnlyCollection<Guid> ids,
        string? version,
        string? error,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            foreach (var id in ids)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    UPDATE current_user_source
                    SET last_checked_at = @checked_at,
                        upstream_version = COALESCE(@upstream_version, upstream_version),
                        last_refresh_error = @last_refresh_error
                    WHERE current_user_source_id = @id;
                    """;
                AddParameter(command, "@checked_at", DateTimeOffset.UtcNow);
                AddNullableParameter(command, "@upstream_version", version);
                AddNullableParameter(command, "@last_refresh_error", error);
                AddParameter(command, "@id", id);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
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
        dbContext.Database.ExecuteSqlRawAsync(RefreshSchemaSql, cancellationToken);

    private static CurrentUserSourceRegistration ReadRegistration(DbDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5));

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
        return new GitHubTreeLocation(segments[0], segments[1], segments[3]);
    }

    private static async Task EnsureRemoteUriSafeAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException("Web source version checks require an HTTPS URL without embedded credentials.");
        }

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

    private static string LimitError(string message) =>
        message.Length <= 1000 ? message : message[..1000];

    private static HttpClient CreateSharedHttpClient() => new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    })
    {
        Timeout = TimeSpan.FromMinutes(2)
    };

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

    private sealed record VersionProbe(bool Known, string? Token)
    {
        public static VersionProbe Unknown { get; } = new(false, null);
    }

    private sealed record CurrentUserSourceRegistration(
        Guid Id,
        string UserId,
        string Kind,
        string? Url,
        string? UpstreamVersion,
        DateTimeOffset? LastCheckedAt);

    private sealed record GitHubTreeLocation(string Owner, string Repository, string Reference);

    private const string RefreshSchemaSql = """
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
