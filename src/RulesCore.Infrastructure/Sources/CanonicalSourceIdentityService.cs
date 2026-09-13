using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed class CanonicalSourceIdentityService(RulesCoreDbContext dbContext)
{
    public async Task<int> IndexPackageAsync(
        Guid sourcePackageId,
        CancellationToken cancellationToken = default)
    {
        if (sourcePackageId == Guid.Empty)
        {
            throw new ArgumentException("Source package ID can not be empty.", nameof(sourcePackageId));
        }

        await EnsureSchemaAsync(cancellationToken);
        await SourceFrameworkStore.EnsureSchemaAsync(dbContext, cancellationToken);
        var rows = await ReadPackageRowsAsync(sourcePackageId, cancellationToken);
        if (rows.Count == 0)
        {
            return 0;
        }

        var publications = new Dictionary<string, CanonicalPublicationIdentityView>(StringComparer.OrdinalIgnoreCase);
        var indexed = 0;
        foreach (var row in rows)
        {
            if (!publications.TryGetValue(row.SourceCode, out var publication))
            {
                publication = await ResolvePublicationAsync(
                    new CanonicalPublicationEvidence(
                        row.WorkDisplayName,
                        Publisher: null,
                        row.GameEdition,
                        row.PublicationDate,
                        Aliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["5etools-source-code"] = row.SourceCode
                        }),
                    cancellationToken);
                publications[row.SourceCode] = publication;
            }

            var evidence = BuildOccurrenceEvidence(row);
            var occurrenceId = await ResolveOccurrenceAsync(
                publication.Id,
                evidence,
                cancellationToken);
            if (await BindSourceEntityAsync(
                    row.SourceEntityId,
                    occurrenceId,
                    evidence,
                    "5etools-source-code+occurrence",
                    1.0,
                    cancellationToken))
            {
                indexed++;
            }
        }

        return indexed;
    }

    public async Task<int> IndexUnboundAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var packageIds = new List<Guid>();
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
                SELECT DISTINCT sp.source_package_id
                FROM source_package sp
                JOIN source_work sw ON sw.source_package_id = sp.source_package_id
                JOIN source_edition sed ON sed.source_work_id = sw.source_work_id
                JOIN source_entity se ON se.source_edition_id = sed.source_edition_id
                LEFT JOIN source_entity_occurrence_binding b
                    ON b.source_entity_id = se.source_entity_id
                WHERE b.source_entity_id IS NULL
                ORDER BY sp.source_package_id;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                packageIds.Add(reader.GetGuid(0));
            }
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }

        var indexed = 0;
        foreach (var packageId in packageIds)
        {
            indexed += await IndexPackageAsync(packageId, cancellationToken);
        }
        return indexed;
    }

    public async Task<CanonicalPublicationIdentityView> ResolvePublicationAsync(
        CanonicalPublicationEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (string.IsNullOrWhiteSpace(evidence.DisplayName))
        {
            throw new ArgumentException("Canonical publication display name can not be blank.", nameof(evidence));
        }

        await EnsureSchemaAsync(cancellationToken);
        var aliases = NormalizeAliases(evidence.Aliases);
        foreach (var alias in aliases)
        {
            var byAlias = await FindPublicationByAliasAsync(alias.Key, alias.Value, cancellationToken);
            if (byAlias is not null)
            {
                return byAlias with { MatchKind = $"alias:{alias.Key}", Confidence = 1.0 };
            }
        }

        var bibliographicFingerprint = CanonicalSourceIdentity.BibliographicFingerprint(evidence);
        var byBibliography = await FindPublicationByBibliographyAsync(
            bibliographicFingerprint,
            cancellationToken);
        if (byBibliography is not null)
        {
            await AddAliasesAsync(byBibliography.Id, aliases, cancellationToken);
            return byBibliography with { MatchKind = "bibliographic", Confidence = 0.98 };
        }

        var byContent = await FindPublicationByOccurrenceOverlapAsync(
            evidence.OccurrenceFingerprints,
            cancellationToken);
        if (byContent is not null)
        {
            await AddAliasesAsync(byContent.Id, aliases, cancellationToken);
            return byContent;
        }

        var keySeed = aliases.Count > 0
            ? $"{aliases[0].Key}:{aliases[0].Value}"
            : bibliographicFingerprint;
        var canonicalKey = $"publication-{CanonicalSourceIdentity.Fingerprint(keySeed)[..24]}";
        var created = await CreateOrReadPublicationAsync(
            canonicalKey,
            evidence,
            bibliographicFingerprint,
            cancellationToken);
        await AddAliasesAsync(created.Id, aliases, cancellationToken);
        return created with { MatchKind = "new", Confidence = 1.0 };
    }

    public async Task<Guid> ResolveOccurrenceAsync(
        Guid canonicalPublicationId,
        CanonicalSourceOccurrenceEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        if (canonicalPublicationId == Guid.Empty)
        {
            throw new ArgumentException("Canonical publication ID can not be empty.", nameof(canonicalPublicationId));
        }
        ArgumentNullException.ThrowIfNull(evidence);

        await EnsureSchemaAsync(cancellationToken);
        var occurrenceKey = CanonicalSourceIdentity.OccurrenceKey(evidence.EntityType, evidence.Name);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using (var read = connection.CreateCommand())
            {
                read.CommandText = """
                    SELECT canonical_source_occurrence_id
                    FROM canonical_source_occurrence
                    WHERE canonical_publication_id = @publication_id
                        AND occurrence_key = @occurrence_key;
                    """;
                AddParameter(read, "@publication_id", canonicalPublicationId);
                AddParameter(read, "@occurrence_key", occurrenceKey);
                var existing = await read.ExecuteScalarAsync(cancellationToken);
                if (existing is Guid existingId)
                {
                    return existingId;
                }
            }

            var id = Guid.NewGuid();
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO canonical_source_occurrence (
                    canonical_source_occurrence_id,
                    canonical_publication_id,
                    occurrence_key,
                    entity_type,
                    display_name,
                    created_at)
                VALUES (
                    @id,
                    @publication_id,
                    @occurrence_key,
                    @entity_type,
                    @display_name,
                    @created_at)
                ON CONFLICT (canonical_publication_id, occurrence_key) DO NOTHING;
                """;
            AddParameter(insert, "@id", id);
            AddParameter(insert, "@publication_id", canonicalPublicationId);
            AddParameter(insert, "@occurrence_key", occurrenceKey);
            AddParameter(insert, "@entity_type", evidence.EntityType.Trim());
            AddParameter(insert, "@display_name", evidence.Name.Trim());
            AddParameter(insert, "@created_at", DateTimeOffset.UtcNow);
            await insert.ExecuteNonQueryAsync(cancellationToken);

            await using var reread = connection.CreateCommand();
            reread.CommandText = """
                SELECT canonical_source_occurrence_id
                FROM canonical_source_occurrence
                WHERE canonical_publication_id = @publication_id
                    AND occurrence_key = @occurrence_key;
                """;
            AddParameter(reread, "@publication_id", canonicalPublicationId);
            AddParameter(reread, "@occurrence_key", occurrenceKey);
            return (Guid)(await reread.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Canonical occurrence was not readable after creation."));
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<bool> BindSourceEntityAsync(
        Guid sourceEntityId,
        Guid occurrenceId,
        CanonicalSourceOccurrenceEvidence evidence,
        string matchKind,
        double confidence,
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
                INSERT INTO source_entity_occurrence_binding (
                    source_entity_occurrence_binding_id,
                    source_entity_id,
                    canonical_source_occurrence_id,
                    semantic_fingerprint,
                    locator_key,
                    match_kind,
                    confidence,
                    created_at)
                VALUES (
                    @id,
                    @source_entity_id,
                    @occurrence_id,
                    @semantic_fingerprint,
                    @locator_key,
                    @match_kind,
                    @confidence,
                    @created_at)
                ON CONFLICT (source_entity_id) DO NOTHING;
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@source_entity_id", sourceEntityId);
            AddParameter(command, "@occurrence_id", occurrenceId);
            AddParameter(command, "@semantic_fingerprint", evidence.SemanticFingerprint);
            AddNullableParameter(command, "@locator_key", evidence.LocatorKey);
            AddParameter(command, "@match_kind", matchKind);
            AddParameter(command, "@confidence", confidence);
            AddParameter(command, "@created_at", DateTimeOffset.UtcNow);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<IReadOnlyList<PackageEntityRow>> ReadPackageRowsAsync(
        Guid packageId,
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
                    se.source_entity_id,
                    se.entity_type,
                    se.entity_name,
                    se.source_code,
                    sw.display_name,
                    sem.game_edition,
                    sem.publication_date,
                    revision.raw_json::text
                FROM source_entity se
                JOIN source_edition sed ON sed.source_edition_id = se.source_edition_id
                JOIN source_work sw ON sw.source_work_id = sed.source_work_id
                LEFT JOIN source_edition_metadata sem
                    ON sem.source_edition_id = sed.source_edition_id
                JOIN LATERAL (
                    SELECT ser.raw_json
                    FROM source_entity_revision ser
                    WHERE ser.source_entity_id = se.source_entity_id
                    ORDER BY ser.revision_number DESC
                    LIMIT 1
                ) revision ON TRUE
                WHERE sw.source_package_id = @package_id
                ORDER BY se.source_entity_id;
                """;
            AddParameter(command, "@package_id", packageId);

            var rows = new List<PackageEntityRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new PackageEntityRow(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6),
                    reader.GetString(7)));
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

    private static CanonicalSourceOccurrenceEvidence BuildOccurrenceEvidence(PackageEntityRow row)
    {
        using var document = JsonDocument.Parse(row.RawJson);
        var root = document.RootElement;
        string? locator = null;
        if (root.TryGetProperty("page", out var page))
        {
            locator = page.ValueKind switch
            {
                JsonValueKind.Number => $"page:{page.GetRawText()}",
                JsonValueKind.String when !string.IsNullOrWhiteSpace(page.GetString()) => $"page:{page.GetString()!.Trim()}",
                _ => null
            };
        }

        return new CanonicalSourceOccurrenceEvidence(
            row.EntityType,
            row.EntityName,
            locator,
            CanonicalSourceIdentity.SemanticFingerprint(row.RawJson));
    }

    private async Task<CanonicalPublicationIdentityView?> FindPublicationByAliasAsync(
        string scheme,
        string value,
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
                    cp.canonical_publication_id,
                    cp.canonical_key,
                    cp.display_name,
                    cp.publisher,
                    cp.game_edition,
                    cp.publication_date
                FROM canonical_publication_alias cpa
                JOIN canonical_publication cp
                    ON cp.canonical_publication_id = cpa.canonical_publication_id
                WHERE cpa.alias_scheme = @scheme
                    AND cpa.alias_value = @value;
                """;
            AddParameter(command, "@scheme", scheme);
            AddParameter(command, "@value", value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken)
                ? ReadPublication(reader, "alias", 1.0)
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

    private async Task<CanonicalPublicationIdentityView?> FindPublicationByBibliographyAsync(
        string fingerprint,
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
                    canonical_publication_id,
                    canonical_key,
                    display_name,
                    publisher,
                    game_edition,
                    publication_date
                FROM canonical_publication
                WHERE bibliographic_fingerprint = @fingerprint
                ORDER BY created_at
                LIMIT 2;
                """;
            AddParameter(command, "@fingerprint", fingerprint);
            var matches = new List<CanonicalPublicationIdentityView>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                matches.Add(ReadPublication(reader, "bibliographic", 0.98));
            }
            return matches.Count == 1 ? matches[0] : null;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<CanonicalPublicationIdentityView?> FindPublicationByOccurrenceOverlapAsync(
        IReadOnlyCollection<string>? occurrenceFingerprints,
        CancellationToken cancellationToken)
    {
        var fingerprints = occurrenceFingerprints?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
        if (fingerprints.Length < 3)
        {
            return null;
        }

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            var parameters = new List<string>(fingerprints.Length);
            for (var index = 0; index < fingerprints.Length; index++)
            {
                var name = $"@fp{index}";
                parameters.Add(name);
                AddParameter(command, name, fingerprints[index]);
            }
            command.CommandText = $"""
                SELECT
                    cp.canonical_publication_id,
                    cp.canonical_key,
                    cp.display_name,
                    cp.publisher,
                    cp.game_edition,
                    cp.publication_date,
                    COUNT(DISTINCT b.semantic_fingerprint) AS match_count
                FROM source_entity_occurrence_binding b
                JOIN canonical_source_occurrence cso
                    ON cso.canonical_source_occurrence_id = b.canonical_source_occurrence_id
                JOIN canonical_publication cp
                    ON cp.canonical_publication_id = cso.canonical_publication_id
                WHERE b.semantic_fingerprint IN ({string.Join(", ", parameters)})
                GROUP BY
                    cp.canonical_publication_id,
                    cp.canonical_key,
                    cp.display_name,
                    cp.publisher,
                    cp.game_edition,
                    cp.publication_date
                ORDER BY match_count DESC, cp.canonical_publication_id
                LIMIT 2;
                """;

            var matches = new List<(CanonicalPublicationIdentityView Publication, int Count)>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                matches.Add((ReadPublication(reader, "content-overlap", 0.92), reader.GetInt32(6)));
            }
            if (matches.Count == 0 || matches[0].Count < 3)
            {
                return null;
            }
            if (matches.Count > 1 && matches[1].Count == matches[0].Count)
            {
                return null;
            }
            return matches[0].Publication;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<CanonicalPublicationIdentityView> CreateOrReadPublicationAsync(
        string canonicalKey,
        CanonicalPublicationEvidence evidence,
        string bibliographicFingerprint,
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
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO canonical_publication (
                    canonical_publication_id,
                    canonical_key,
                    display_name,
                    publisher,
                    game_edition,
                    publication_date,
                    bibliographic_fingerprint,
                    created_at)
                VALUES (
                    @id,
                    @canonical_key,
                    @display_name,
                    @publisher,
                    @game_edition,
                    @publication_date,
                    @bibliographic_fingerprint,
                    @created_at)
                ON CONFLICT (canonical_key) DO NOTHING;
                """;
            AddParameter(insert, "@id", Guid.NewGuid());
            AddParameter(insert, "@canonical_key", canonicalKey);
            AddParameter(insert, "@display_name", evidence.DisplayName.Trim());
            AddNullableParameter(insert, "@publisher", NormalizeOptional(evidence.Publisher));
            AddNullableParameter(insert, "@game_edition", NormalizeOptional(evidence.GameEdition));
            AddNullableParameter(insert, "@publication_date", evidence.PublicationDate);
            AddParameter(insert, "@bibliographic_fingerprint", bibliographicFingerprint);
            AddParameter(insert, "@created_at", DateTimeOffset.UtcNow);
            await insert.ExecuteNonQueryAsync(cancellationToken);

            await using var read = connection.CreateCommand();
            read.CommandText = """
                SELECT
                    canonical_publication_id,
                    canonical_key,
                    display_name,
                    publisher,
                    game_edition,
                    publication_date
                FROM canonical_publication
                WHERE canonical_key = @canonical_key;
                """;
            AddParameter(read, "@canonical_key", canonicalKey);
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Canonical publication was not readable after creation.");
            }
            return ReadPublication(reader, "new", 1.0);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task AddAliasesAsync(
        Guid publicationId,
        IReadOnlyList<KeyValuePair<string, string>> aliases,
        CancellationToken cancellationToken)
    {
        if (aliases.Count == 0)
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
            foreach (var alias in aliases)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO canonical_publication_alias (
                        canonical_publication_alias_id,
                        canonical_publication_id,
                        alias_scheme,
                        alias_value,
                        created_at)
                    VALUES (@id, @publication_id, @scheme, @value, @created_at)
                    ON CONFLICT (alias_scheme, alias_value) DO NOTHING;
                    """;
                AddParameter(command, "@id", Guid.NewGuid());
                AddParameter(command, "@publication_id", publicationId);
                AddParameter(command, "@scheme", alias.Key);
                AddParameter(command, "@value", alias.Value);
                AddParameter(command, "@created_at", DateTimeOffset.UtcNow);
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
        dbContext.Database.ExecuteSqlRawAsync(SchemaSql, cancellationToken);

    private static IReadOnlyList<KeyValuePair<string, string>> NormalizeAliases(
        IReadOnlyDictionary<string, string>? aliases)
    {
        if (aliases is null)
        {
            return [];
        }

        return aliases
            .Where(value => !string.IsNullOrWhiteSpace(value.Key) && !string.IsNullOrWhiteSpace(value.Value))
            .Select(value => new KeyValuePair<string, string>(
                CanonicalSourceIdentity.NormalizeIdentityPart(value.Key),
                value.Value.Trim().ToLowerInvariant()))
            .Where(value => !string.IsNullOrEmpty(value.Key))
            .DistinctBy(value => $"{value.Key}\n{value.Value}", StringComparer.Ordinal)
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ThenBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static CanonicalPublicationIdentityView ReadPublication(
        DbDataReader reader,
        string matchKind,
        double confidence) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5),
            matchKind,
            confidence);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

    private sealed record PackageEntityRow(
        Guid SourceEntityId,
        string EntityType,
        string EntityName,
        string SourceCode,
        string WorkDisplayName,
        string? GameEdition,
        DateOnly? PublicationDate,
        string RawJson);

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS canonical_publication (
            canonical_publication_id uuid NOT NULL,
            canonical_key varchar(300) NOT NULL,
            display_name varchar(500) NOT NULL,
            publisher varchar(300) NULL,
            game_edition varchar(40) NULL,
            publication_date date NULL,
            bibliographic_fingerprint varchar(64) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_canonical_publication PRIMARY KEY (canonical_publication_id));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_publication_key
            ON canonical_publication(canonical_key);
        CREATE INDEX IF NOT EXISTS ix_canonical_publication_bibliographic_fingerprint
            ON canonical_publication(bibliographic_fingerprint);

        CREATE TABLE IF NOT EXISTS canonical_publication_alias (
            canonical_publication_alias_id uuid NOT NULL,
            canonical_publication_id uuid NOT NULL,
            alias_scheme varchar(100) NOT NULL,
            alias_value varchar(500) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_canonical_publication_alias PRIMARY KEY (canonical_publication_alias_id),
            CONSTRAINT fk_canonical_publication_alias_publication FOREIGN KEY (canonical_publication_id)
                REFERENCES canonical_publication(canonical_publication_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_publication_alias_identity
            ON canonical_publication_alias(alias_scheme, alias_value);
        CREATE INDEX IF NOT EXISTS ix_canonical_publication_alias_publication
            ON canonical_publication_alias(canonical_publication_id);

        CREATE TABLE IF NOT EXISTS canonical_source_occurrence (
            canonical_source_occurrence_id uuid NOT NULL,
            canonical_publication_id uuid NOT NULL,
            occurrence_key varchar(1000) NOT NULL,
            entity_type varchar(120) NOT NULL,
            display_name varchar(500) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_canonical_source_occurrence PRIMARY KEY (canonical_source_occurrence_id),
            CONSTRAINT fk_canonical_source_occurrence_publication FOREIGN KEY (canonical_publication_id)
                REFERENCES canonical_publication(canonical_publication_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_source_occurrence_identity
            ON canonical_source_occurrence(canonical_publication_id, occurrence_key);

        CREATE TABLE IF NOT EXISTS source_entity_occurrence_binding (
            source_entity_occurrence_binding_id uuid NOT NULL,
            source_entity_id uuid NOT NULL,
            canonical_source_occurrence_id uuid NOT NULL,
            semantic_fingerprint varchar(64) NOT NULL,
            locator_key varchar(500) NULL,
            match_kind varchar(100) NOT NULL,
            confidence double precision NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_entity_occurrence_binding PRIMARY KEY (source_entity_occurrence_binding_id),
            CONSTRAINT fk_source_entity_occurrence_binding_entity FOREIGN KEY (source_entity_id)
                REFERENCES source_entity(source_entity_id) ON DELETE CASCADE,
            CONSTRAINT fk_source_entity_occurrence_binding_occurrence FOREIGN KEY (canonical_source_occurrence_id)
                REFERENCES canonical_source_occurrence(canonical_source_occurrence_id) ON DELETE CASCADE,
            CONSTRAINT ck_source_entity_occurrence_binding_confidence CHECK (confidence >= 0 AND confidence <= 1));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_entity_occurrence_binding_entity
            ON source_entity_occurrence_binding(source_entity_id);
        CREATE INDEX IF NOT EXISTS ix_source_entity_occurrence_binding_occurrence
            ON source_entity_occurrence_binding(canonical_source_occurrence_id);
        CREATE INDEX IF NOT EXISTS ix_source_entity_occurrence_binding_semantic_fingerprint
            ON source_entity_occurrence_binding(semantic_fingerprint);
        """;
}
