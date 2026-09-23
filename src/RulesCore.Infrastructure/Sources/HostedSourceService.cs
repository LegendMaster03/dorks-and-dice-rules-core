using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed class HostedSourceService : IHostedSourceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HttpClient SharedHttpClient =
        HostedSourceRemoteResolver.CreateSharedHttpClient();

    private readonly RulesCoreDbContext dbContext;
    private readonly ISourceImportService importer;
    private readonly HostedSourceRemoteResolver remoteResolver;

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
        remoteResolver = new HostedSourceRemoteResolver(httpClient);
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
        var resolved = await remoteResolver.ResolveAsync(definition, cancellationToken);
        var preview = await importer.Preview5eToolsDocumentAsync(
            BuildImportRequest(definition, resolved.AggregateJson),
            cancellationToken);
        return new HostedSourcePreviewView(
            definition,
            resolved.Documents,
            preview);
    }

    public async Task<HostedSourceRefreshView> RefreshAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        var definition = await RequireEnabledDefinitionAsync(definitionId, cancellationToken);
        var resolved = await remoteResolver.ResolveAsync(definition, cancellationToken);
        var import = await importer.Import5eToolsDocumentAsync(
            BuildImportRequest(definition, resolved.AggregateJson),
            cancellationToken);
        return new HostedSourceRefreshView(
            definition,
            resolved.Documents,
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
        HostedSourceUriPolicy.ValidateShape(uri);
        if (string.Equals(kind, HostedSourceResourceKinds.GitHubTree, StringComparison.Ordinal))
        {
            HostedSourceRemoteResolver.ValidateGitHubTreeUri(uri);
        }
        return new HostedSourceResourceView(kind, uri.AbsoluteUri);
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