using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Converts legacy rule-bearing 5e.tools fluff revisions into companion content. Canonical
/// occurrence bindings are removed only for revisions whose companion payload was persisted
/// successfully; malformed or otherwise unconvertible legacy revisions remain visible for review.
/// </summary>
internal sealed class LegacyStandaloneFluffMigrationService(RulesCoreDbContext dbContext)
{
    public async Task<int> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var store = new SourceCompanionContentStore(dbContext);
        await store.EnsureSchemaAsync(cancellationToken);

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

        var entityIds = legacy.Select(value => value.Id).ToArray();
        var revisions = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => entityIds.Contains(value.SourceEntityId))
            .OrderBy(value => value.SourceEntityId)
            .ThenBy(value => value.RevisionNumber)
            .ToArrayAsync(cancellationToken);
        var byId = legacy.ToDictionary(value => value.Id);
        var migratedRevisionIds = new List<Guid>(revisions.Length);

        // Historical backfill deliberately persists the entire legacy companion corpus before the
        // caller resolves pending attachments. Calling SourceCompanionContentStore.PersistAsync for
        // every revision would resolve the package after every insert, repeatedly rescanning and
        // upserting the same attachments as the corpus grows.
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
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

                await PersistCompanionAsync(
                    connection,
                    entity.SourcePackageId,
                    revision.SourceRepresentationId,
                    companion,
                    cancellationToken);
                migratedRevisionIds.Add(revision.Id);
            }

            if (migratedRevisionIds.Count != 0)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    DELETE FROM source_entity_occurrence_binding
                    WHERE source_entity_revision_id = ANY(@revision_ids);
                    """;
                AddParameter(command, "@revision_ids", migratedRevisionIds.ToArray());
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }

        // CanonicalDataReconciliationService performs one corpus-wide historical attachment pass
        // after this migration returns. Keeping resolution there also covers pending companions
        // that did not originate from legacy fluff rows.
        return migratedRevisionIds.Count;
    }

    private static async Task PersistCompanionAsync(
        DbConnection connection,
        Guid sourcePackageId,
        Guid sourceRepresentationId,
        NormalizedSourceCompanionContent companion,
        CancellationToken cancellationToken)
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
                target_identity_json = EXCLUDED.target_identity_json
            WHERE source_companion_content.target_identity_json IS DISTINCT FROM EXCLUDED.target_identity_json;
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
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException("Companion identity can not be blank.");
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new InvalidDataException($"Companion identity can not exceed {maxLength} characters.");
        }
        return normalized;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record LegacyEntity(
        Guid Id,
        Guid SourcePackageId,
        string EntityType,
        string Name,
        string? SourceCode,
        string NativeKey);
}
