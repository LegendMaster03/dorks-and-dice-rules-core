using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Resolves the historical companion corpus in one indexed pass. Normal import persistence keeps
/// using SourceCompanionContentStore's package-scoped resolution path; this service exists for the
/// one-time full-corpus maintenance operation where repeatedly rescanning candidate entities is
/// prohibitively expensive.
/// </summary>
internal sealed class HistoricalCompanionContentReconciliationService(RulesCoreDbContext dbContext)
{
    public async Task ResolvePendingAsync(CancellationToken cancellationToken = default)
    {
        var store = new SourceCompanionContentStore(dbContext);
        await store.EnsureSchemaAsync(cancellationToken);

        var contents = await ReadContentIdentityRowsAsync(cancellationToken);
        if (contents.Count == 0)
        {
            return;
        }

        var targetsByContent = contents.ToDictionary(
            value => value.Id,
            value => DeserializeTargets(value.TargetIdentityJson));
        var allTargets = targetsByContent.Values.SelectMany(value => value).ToArray();
        var targetTypes = allTargets
            .Select(value => value.EntityType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var targetSourceCodes = allTargets
            .Select(value => value.SourceCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (targetTypes.Length == 0 || targetSourceCodes.Length == 0)
        {
            return;
        }

        var entities = await dbContext.SourceEntities
            .AsNoTracking()
            .Where(value => targetTypes.Contains(value.EntityType)
                && value.SourceCode != null
                && targetSourceCodes.Contains(value.SourceCode))
            .Select(value => new CandidateEntity(
                value.Id,
                value.EntityType,
                value.Name,
                value.SourceCode))
            .ToArrayAsync(cancellationToken);
        if (entities.Length == 0)
        {
            return;
        }

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

        // Resolve each target by a normalized identity key instead of scanning every candidate
        // entity for every companion. Multiple entities can legitimately share one source-native
        // identity, so each key retains every matching latest revision.
        var candidatesByIdentity = entities
            .Where(value => latestRevisionByEntity.ContainsKey(value.Id))
            .GroupBy(value => IdentityKey.Create(value.EntityType, value.Name, value.SourceCode ?? string.Empty))
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(value => new ResolvedCandidate(
                        value.Id,
                        latestRevisionByEntity[value.Id].Id))
                    .ToArray());

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            foreach (var content in contents)
            {
                if (!targetsByContent.TryGetValue(content.Id, out var targets))
                {
                    continue;
                }

                foreach (var target in targets)
                {
                    if (!candidatesByIdentity.TryGetValue(
                            IdentityKey.Create(target.EntityType, target.Name, target.SourceCode),
                            out var candidates))
                    {
                        continue;
                    }

                    foreach (var candidate in candidates)
                    {
                        await UpsertAttachmentAsync(
                            connection,
                            content.Id,
                            candidate,
                            target.EvidenceKind,
                            cancellationToken);
                    }
                }
            }
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private async Task<IReadOnlyList<ContentIdentityRow>> ReadContentIdentityRowsAsync(
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT source_companion_content_id, target_identity_json::text
                FROM source_companion_content;
                """;
            var rows = new List<ContentIdentityRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new ContentIdentityRow(reader.GetGuid(0), reader.GetString(1)));
            }
            return rows;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task UpsertAttachmentAsync(
        DbConnection connection,
        Guid contentId,
        ResolvedCandidate candidate,
        string evidenceKind,
        CancellationToken cancellationToken)
    {
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
            DO UPDATE SET evidence_kind = EXCLUDED.evidence_kind
            WHERE source_companion_attachment.evidence_kind IS DISTINCT FROM EXCLUDED.evidence_kind;
            """;
        AddParameter(command, "@id", Guid.NewGuid());
        AddParameter(command, "@content_id", contentId);
        AddParameter(command, "@entity_id", candidate.EntityId);
        AddParameter(command, "@revision_id", candidate.RevisionId);
        AddParameter(command, "@evidence_kind", Require(evidenceKind, 80));
        AddParameter(command, "@created_at", DateTimeOffset.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
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

    private sealed record ContentIdentityRow(Guid Id, string TargetIdentityJson);
    private sealed record CandidateEntity(Guid Id, string EntityType, string Name, string? SourceCode);
    private sealed record CandidateRevision(Guid Id, Guid SourceEntityId, int RevisionNumber);
    private sealed record ResolvedCandidate(Guid EntityId, Guid RevisionId);

    private sealed record IdentityKey(string EntityType, string Name, string SourceCode)
    {
        public static IdentityKey Create(string entityType, string name, string sourceCode) =>
            new(
                CanonicalSourceIdentity.NormalizeIdentityPart(entityType),
                CanonicalSourceIdentity.NormalizeIdentityPart(name),
                CanonicalSourceIdentity.NormalizeIdentityPart(sourceCode));
    }
}
