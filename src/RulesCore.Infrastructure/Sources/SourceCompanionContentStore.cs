using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

internal sealed record SourceCompanionContentRow(
    Guid Id,
    string CompanionKind,
    string Name,
    string SourceCode,
    string PackageKey,
    string PackageDisplayName,
    Guid SourceRepresentationId,
    Guid SourceEntityId,
    Guid SourceEntityRevisionId,
    string EvidenceKind,
    string ContentSha256,
    string RawJson);

/// <summary>
/// Persists source-owned descriptive content independently of rule-bearing SourceEntity rows.
/// Package ownership is preserved so content authorization never inherits visibility merely from
/// the target canonical history.
/// </summary>
internal sealed class SourceCompanionContentStore(RulesCoreDbContext dbContext)
{
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Database.ExecuteSqlRawAsync(SchemaSql, cancellationToken);

    public async Task PersistAsync(
        Guid sourcePackageId,
        Guid sourceRepresentationId,
        IReadOnlyList<NormalizedSourceCompanionContent> companions,
        CancellationToken cancellationToken = default)
    {
        if (companions.Count == 0)
        {
            return;
        }

        await EnsureSchemaAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            foreach (var companion in companions)
            {
                var rawJson = RequireJsonObject(companion.RawJson);
                var contentSha = Fingerprint(rawJson);
                var targetsJson = JsonSerializer.Serialize(companion.Targets);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO source_companion_content (
                        source_companion_content_id,
                        source_package_id,
                        source_representation_id,
                        companion_kind,
                        companion_name,
                        source_code,
                        native_key,
                        target_identity_json,
                        raw_json,
                        content_sha256,
                        created_at)
                    VALUES (
                        @id,
                        @package_id,
                        @representation_id,
                        @companion_kind,
                        @companion_name,
                        @source_code,
                        @native_key,
                        CAST(@target_identity_json AS jsonb),
                        CAST(@raw_json AS jsonb),
                        @content_sha256,
                        @created_at)
                    ON CONFLICT (
                        source_package_id,
                        source_representation_id,
                        native_key,
                        content_sha256)
                    DO UPDATE SET
                        target_identity_json = EXCLUDED.target_identity_json;
                    """;
                AddParameter(command, "@id", Guid.NewGuid());
                AddParameter(command, "@package_id", sourcePackageId);
                AddParameter(command, "@representation_id", sourceRepresentationId);
                AddParameter(command, "@companion_kind", Require(companion.CompanionKind, 40));
                AddParameter(command, "@companion_name", Require(companion.Name, 300));
                AddParameter(command, "@source_code", Require(companion.SourceCode, 120));
                AddParameter(command, "@native_key", Require(companion.NativeKey, 1000));
                AddParameter(command, "@target_identity_json", targetsJson);
                AddParameter(command, "@raw_json", rawJson);
                AddParameter(command, "@content_sha256", contentSha);
                AddParameter(command, "@created_at", DateTimeOffset.UtcNow);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }

        await ResolvePendingAsync(sourcePackageId, cancellationToken);
    }

    public async Task ResolvePendingAsync(
        Guid? sourcePackageId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var contents = await ReadContentIdentityRowsAsync(sourcePackageId, cancellationToken);
        if (contents.Count == 0)
        {
            return;
        }

        var packageIds = contents.Select(value => value.SourcePackageId).Distinct().ToArray();
        var entities = await dbContext.SourceEntities
            .AsNoTracking()
            .Where(value => packageIds.Contains(value.SourcePackageId))
            .Select(value => new CandidateEntity(
                value.Id,
                value.SourcePackageId,
                value.EntityType,
                value.Name,
                value.SourceCode))
            .ToArrayAsync(cancellationToken);
        var entityIds = entities.Select(value => value.Id).ToArray();
        var revisions = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => entityIds.Contains(value.SourceEntityId))
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => new CandidateRevision(value.Id, value.SourceEntityId, value.RevisionNumber))
            .ToArrayAsync(cancellationToken);
        var latestRevisionByEntity = revisions
            .GroupBy(value => value.SourceEntityId)
            .ToDictionary(group => group.Key, group => group.First());

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            foreach (var content in contents)
            {
                var targets = DeserializeTargets(content.TargetIdentityJson);
                foreach (var target in targets)
                {
                    var targetType = CanonicalSourceIdentity.NormalizeIdentityPart(target.EntityType);
                    var targetName = CanonicalSourceIdentity.NormalizeIdentityPart(target.Name);
                    var targetSource = CanonicalSourceIdentity.NormalizeIdentityPart(target.SourceCode);
                    foreach (var entity in entities.Where(value => value.SourcePackageId == content.SourcePackageId))
                    {
                        if (!string.Equals(
                                CanonicalSourceIdentity.NormalizeIdentityPart(entity.EntityType),
                                targetType,
                                StringComparison.Ordinal)
                            || !string.Equals(
                                CanonicalSourceIdentity.NormalizeIdentityPart(entity.Name),
                                targetName,
                                StringComparison.Ordinal)
                            || !string.Equals(
                                CanonicalSourceIdentity.NormalizeIdentityPart(entity.SourceCode ?? string.Empty),
                                targetSource,
                                StringComparison.Ordinal)
                            || !latestRevisionByEntity.TryGetValue(entity.Id, out var revision))
                        {
                            continue;
                        }

                        await using var command = connection.CreateCommand();
                        command.CommandText = """
                            INSERT INTO source_companion_attachment (
                                source_companion_attachment_id,
                                source_companion_content_id,
                                source_entity_id,
                                source_entity_revision_id,
                                evidence_kind,
                                created_at)
                            VALUES (
                                @id,
                                @content_id,
                                @entity_id,
                                @revision_id,
                                @evidence_kind,
                                @created_at)
                            ON CONFLICT (source_companion_content_id, source_entity_revision_id)
                            DO UPDATE SET evidence_kind = EXCLUDED.evidence_kind;
                            """;
                        AddParameter(command, "@id", Guid.NewGuid());
                        AddParameter(command, "@content_id", content.Id);
                        AddParameter(command, "@entity_id", entity.Id);
                        AddParameter(command, "@revision_id", revision.Id);
                        AddParameter(command, "@evidence_kind", Require(target.EvidenceKind, 80));
                        AddParameter(command, "@created_at", DateTimeOffset.UtcNow);
                        await command.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
            }
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    /// <summary>
    /// Converts legacy standalone fluff revisions into companion records, then removes their
    /// canonical occurrence bindings. The original SourceEntity/Revision rows remain as immutable
    /// migration provenance, which also avoids invalidating any historical decision that might
    /// reference a mistakenly adjudicated fluff revision.
    /// </summary>
    public async Task<int> MigrateLegacyStandaloneFluffAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var legacy = await dbContext.SourceEntities
            .AsNoTracking()
            .Where(value => value.EntityType == "monsterFluff"
                || value.EntityType == "raceFluff"
                || value.EntityType == "spellFluff")
            .Select(value => new LegacyEntity(
                value.Id,
                value.SourcePackageId,
                value.EntityType,
                value.Name,
                value.SourceCode,
                value.NativeKey))
            .ToArrayAsync(cancellationToken);
        if (legacy.Length == 0)
        {
            return 0;
        }

        var ids = legacy.Select(value => value.Id).ToArray();
        var revisions = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => ids.Contains(value.SourceEntityId))
            .OrderBy(value => value.SourceEntityId)
            .ThenBy(value => value.RevisionNumber)
            .ToArrayAsync(cancellationToken);
        var byId = legacy.ToDictionary(value => value.Id);
        var migrated = 0;
        foreach (var revision in revisions)
        {
            var entity = byId[revision.SourceEntityId];
            if (!FiveEToolsCompanionSourceFormatAdapter.TryReadLegacyCompanion(
                    entity.EntityType,
                    revision.RawJson,
                    entity.Name,
                    entity.SourceCode,
                    entity.NativeKey,
                    out var companion))
            {
                continue;
            }

            await PersistAsync(
                entity.SourcePackageId,
                revision.SourceRepresentationId,
                [companion],
                cancellationToken);
            migrated++;
        }

        // This is a Source Layer correction, not a Wiki filter. Once the companion copy exists,
        // legacy fluff revisions cease participating in canonical rule histories.
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DELETE FROM source_entity_occurrence_binding
                WHERE source_entity_id = ANY(@source_entity_ids);
                """;
            AddParameter(command, "@source_entity_ids", ids);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }

        await ResolvePendingAsync(null, cancellationToken);
        return migrated;
    }

    public async Task<IReadOnlyList<SourceCompanionContentRow>> ReadAccessibleForSourceEntitiesAsync(
        IReadOnlyCollection<Guid> sourceEntityIds,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        if (sourceEntityIds.Count == 0)
        {
            return [];
        }

        await EnsureSchemaAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DISTINCT ON (content.source_companion_content_id)
                    content.source_companion_content_id,
                    content.companion_kind,
                    content.companion_name,
                    content.source_code,
                    package.package_key,
                    package.display_name,
                    content.source_representation_id,
                    attachment.source_entity_id,
                    attachment.source_entity_revision_id,
                    attachment.evidence_kind,
                    content.content_sha256,
                    content.raw_json::text
                FROM source_companion_attachment attachment
                JOIN source_companion_content content
                    ON content.source_companion_content_id = attachment.source_companion_content_id
                JOIN source_package package
                    ON package.source_package_id = content.source_package_id
                WHERE attachment.source_entity_id = ANY(@source_entity_ids)
                  AND (
                        package.is_public
                        OR (
                            @user_id IS NOT NULL
                            AND EXISTS (
                                SELECT 1
                                FROM user_source_grant grant_row
                                WHERE grant_row.source_package_id = package.source_package_id
                                  AND grant_row.user_id = @user_id)))
                ORDER BY content.source_companion_content_id,
                         attachment.source_entity_revision_id DESC;
                """;
            AddParameter(command, "@source_entity_ids", sourceEntityIds.ToArray());
            AddParameter(command, "@user_id", string.IsNullOrWhiteSpace(userId) ? null : userId.Trim());
            var rows = new List<SourceCompanionContentRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new SourceCompanionContentRow(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetGuid(6),
                    reader.GetGuid(7),
                    reader.GetGuid(8),
                    reader.GetString(9),
                    reader.GetString(10),
                    reader.GetString(11)));
            }
            return rows;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private async Task<IReadOnlyList<ContentIdentityRow>> ReadContentIdentityRowsAsync(
        Guid? sourcePackageId,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sourcePackageId.HasValue
                ? """
                    SELECT source_companion_content_id, source_package_id, target_identity_json::text
                    FROM source_companion_content
                    WHERE source_package_id = @package_id;
                    """
                : """
                    SELECT source_companion_content_id, source_package_id, target_identity_json::text
                    FROM source_companion_content;
                    """;
            if (sourcePackageId.HasValue) AddParameter(command, "@package_id", sourcePackageId.Value);
            var rows = new List<ContentIdentityRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new ContentIdentityRow(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2)));
            }
            return rows;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static IReadOnlyList<NormalizedSourceCompanionTarget> DeserializeTargets(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<NormalizedSourceCompanionTarget[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Fingerprint(string rawJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawJson))).ToLowerInvariant();

    private static string RequireJsonObject(string value)
    {
        using var document = JsonDocument.Parse(value);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Source companion content must have a JSON object root.");
        }
        return document.RootElement.GetRawText();
    }

    private static string Require(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("Companion identity can not be blank.");
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new InvalidDataException($"Companion identity can not exceed {maxLength} characters.");
        return normalized;
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private sealed record CandidateEntity(Guid Id, Guid SourcePackageId, string EntityType, string Name, string? SourceCode);
    private sealed record CandidateRevision(Guid Id, Guid SourceEntityId, int RevisionNumber);
    private sealed record LegacyEntity(Guid Id, Guid SourcePackageId, string EntityType, string Name, string? SourceCode, string NativeKey);
    private sealed record ContentIdentityRow(Guid Id, Guid SourcePackageId, string TargetIdentityJson);

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS source_companion_content (
            source_companion_content_id uuid NOT NULL,
            source_package_id uuid NOT NULL,
            source_representation_id uuid NOT NULL,
            companion_kind varchar(40) NOT NULL,
            companion_name varchar(300) NOT NULL,
            source_code varchar(120) NOT NULL,
            native_key varchar(1000) NOT NULL,
            target_identity_json jsonb NOT NULL,
            raw_json jsonb NOT NULL,
            content_sha256 varchar(64) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_companion_content PRIMARY KEY (source_companion_content_id),
            CONSTRAINT fk_source_companion_content_package FOREIGN KEY (source_package_id)
                REFERENCES source_package(source_package_id) ON DELETE CASCADE,
            CONSTRAINT fk_source_companion_content_representation FOREIGN KEY (source_representation_id)
                REFERENCES source_representation(source_representation_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_companion_content_identity
            ON source_companion_content(source_package_id, source_representation_id, native_key, content_sha256);
        CREATE INDEX IF NOT EXISTS ix_source_companion_content_package
            ON source_companion_content(source_package_id);

        CREATE TABLE IF NOT EXISTS source_companion_attachment (
            source_companion_attachment_id uuid NOT NULL,
            source_companion_content_id uuid NOT NULL,
            source_entity_id uuid NOT NULL,
            source_entity_revision_id uuid NOT NULL,
            evidence_kind varchar(80) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_companion_attachment PRIMARY KEY (source_companion_attachment_id),
            CONSTRAINT fk_source_companion_attachment_content FOREIGN KEY (source_companion_content_id)
                REFERENCES source_companion_content(source_companion_content_id) ON DELETE CASCADE,
            CONSTRAINT fk_source_companion_attachment_entity FOREIGN KEY (source_entity_id)
                REFERENCES source_entity(source_entity_id) ON DELETE CASCADE,
            CONSTRAINT fk_source_companion_attachment_revision FOREIGN KEY (source_entity_revision_id)
                REFERENCES source_entity_revision(source_entity_revision_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_companion_attachment_revision
            ON source_companion_attachment(source_companion_content_id, source_entity_revision_id);
        CREATE INDEX IF NOT EXISTS ix_source_companion_attachment_entity
            ON source_companion_attachment(source_entity_id);
        """;
}
