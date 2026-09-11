using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

internal static class SourceFrameworkStore
{
    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS source_edition_metadata (
            source_edition_id uuid NOT NULL,
            game_edition varchar(20) NULL,
            release_kind varchar(40) NULL,
            publication_date date NULL,
            recorded_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_edition_metadata PRIMARY KEY (source_edition_id),
            CONSTRAINT fk_source_edition_metadata_edition FOREIGN KEY (source_edition_id)
                REFERENCES source_edition(source_edition_id) ON DELETE CASCADE);
        CREATE INDEX IF NOT EXISTS ix_source_edition_metadata_game_edition
            ON source_edition_metadata(game_edition);
        CREATE INDEX IF NOT EXISTS ix_source_edition_metadata_release_kind
            ON source_edition_metadata(release_kind);

        CREATE TABLE IF NOT EXISTS source_entity_lineage (
            source_entity_lineage_id uuid NOT NULL,
            from_source_entity_id uuid NOT NULL,
            to_source_entity_id uuid NOT NULL,
            relationship_kind varchar(80) NOT NULL,
            note varchar(2000) NULL,
            created_by_user_id varchar(200) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            void_reason varchar(1000) NULL,
            voided_by_user_id varchar(200) NULL,
            voided_at timestamp with time zone NULL,
            CONSTRAINT pk_source_entity_lineage PRIMARY KEY (source_entity_lineage_id),
            CONSTRAINT fk_source_entity_lineage_from FOREIGN KEY (from_source_entity_id)
                REFERENCES source_entity(source_entity_id),
            CONSTRAINT fk_source_entity_lineage_to FOREIGN KEY (to_source_entity_id)
                REFERENCES source_entity(source_entity_id),
            CONSTRAINT ck_source_entity_lineage_distinct CHECK (from_source_entity_id <> to_source_entity_id));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_entity_lineage_active
            ON source_entity_lineage(from_source_entity_id, to_source_entity_id, relationship_kind)
            WHERE voided_at IS NULL;
        CREATE INDEX IF NOT EXISTS ix_source_entity_lineage_from
            ON source_entity_lineage(from_source_entity_id);
        CREATE INDEX IF NOT EXISTS ix_source_entity_lineage_to
            ON source_entity_lineage(to_source_entity_id);

        CREATE TABLE IF NOT EXISTS global_rule_decision_contribution (
            global_rule_decision_contribution_id uuid NOT NULL,
            global_rule_decision_id uuid NOT NULL,
            source_entity_revision_id uuid NOT NULL,
            contribution_kind varchar(40) NOT NULL,
            note varchar(1000) NULL,
            created_by_user_id varchar(200) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_global_rule_decision_contribution PRIMARY KEY (global_rule_decision_contribution_id),
            CONSTRAINT fk_global_rule_decision_contribution_decision FOREIGN KEY (global_rule_decision_id)
                REFERENCES global_rule_decision(global_rule_decision_id) ON DELETE CASCADE,
            CONSTRAINT fk_global_rule_decision_contribution_revision FOREIGN KEY (source_entity_revision_id)
                REFERENCES source_entity_revision(source_entity_revision_id));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_global_rule_decision_contribution_revision
            ON global_rule_decision_contribution(global_rule_decision_id, source_entity_revision_id);
        CREATE INDEX IF NOT EXISTS ix_global_rule_decision_contribution_source_revision
            ON global_rule_decision_contribution(source_entity_revision_id);
        """;

    public static Task EnsureSchemaAsync(
        RulesCoreDbContext dbContext,
        CancellationToken cancellationToken = default) =>
        dbContext.Database.ExecuteSqlRawAsync(SchemaSql, cancellationToken);

    public static async Task<StoredSourceEditionMetadata?> GetEditionMetadataAsync(
        RulesCoreDbContext dbContext,
        Guid sourceEditionId,
        CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(dbContext, connection);
            command.CommandText = """
                SELECT game_edition, release_kind, publication_date
                FROM source_edition_metadata
                WHERE source_edition_id = @edition_id;
                """;
            AddParameter(command, "@edition_id", sourceEditionId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new StoredSourceEditionMetadata(
                reader.IsDBNull(0) ? null : reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetFieldValue<DateOnly>(2));
        }
        finally
        {
            await CloseIfNeededAsync(dbContext, connection, openedHere);
        }
    }

    public static async Task<StoredSourceEditionMetadata> MergeEditionMetadataAsync(
        RulesCoreDbContext dbContext,
        Guid sourceEditionId,
        string? gameEdition,
        string? releaseKind,
        DateOnly? publicationDate,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetEditionMetadataAsync(dbContext, sourceEditionId, cancellationToken);
        EnsureCompatible(existing?.GameEdition, gameEdition, "game edition");
        EnsureCompatible(existing?.ReleaseKind, releaseKind, "release kind");
        EnsureCompatible(existing?.PublicationDate, publicationDate, "publication date");

        var effective = new StoredSourceEditionMetadata(
            existing?.GameEdition ?? gameEdition,
            existing?.ReleaseKind ?? releaseKind,
            existing?.PublicationDate ?? publicationDate);

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(dbContext, connection);
            command.CommandText = """
                INSERT INTO source_edition_metadata (
                    source_edition_id, game_edition, release_kind, publication_date, recorded_at)
                VALUES (@edition_id, @game_edition, @release_kind, @publication_date, @recorded_at)
                ON CONFLICT (source_edition_id) DO UPDATE SET
                    game_edition = EXCLUDED.game_edition,
                    release_kind = EXCLUDED.release_kind,
                    publication_date = EXCLUDED.publication_date;
                """;
            AddParameter(command, "@edition_id", sourceEditionId);
            AddNullableParameter(command, "@game_edition", effective.GameEdition);
            AddNullableParameter(command, "@release_kind", effective.ReleaseKind);
            AddNullableParameter(command, "@publication_date", effective.PublicationDate);
            AddParameter(command, "@recorded_at", DateTimeOffset.UtcNow);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return effective;
        }
        finally
        {
            await CloseIfNeededAsync(dbContext, connection, openedHere);
        }
    }

    public static async Task<IReadOnlyList<StoredSourceLineage>> GetLineageForSourcesAsync(
        RulesCoreDbContext dbContext,
        IReadOnlyCollection<Guid> sourceEntityIds,
        bool includeVoided,
        CancellationToken cancellationToken = default)
    {
        if (sourceEntityIds.Count == 0)
        {
            return [];
        }

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(dbContext, connection);
            command.CommandText = $$"""
                SELECT
                    source_entity_lineage_id,
                    from_source_entity_id,
                    to_source_entity_id,
                    relationship_kind,
                    note,
                    created_by_user_id,
                    created_at,
                    void_reason,
                    voided_by_user_id,
                    voided_at
                FROM source_entity_lineage
                WHERE (from_source_entity_id = ANY(@entity_ids)
                    OR to_source_entity_id = ANY(@entity_ids))
                    {{(includeVoided ? string.Empty : "AND voided_at IS NULL")}}
                ORDER BY created_at, source_entity_lineage_id;
                """;
            AddParameter(command, "@entity_ids", sourceEntityIds.ToArray());
            return await ReadLineageAsync(command, cancellationToken);
        }
        finally
        {
            await CloseIfNeededAsync(dbContext, connection, openedHere);
        }
    }

    public static async Task<StoredSourceLineage?> GetLineageByIdAsync(
        RulesCoreDbContext dbContext,
        Guid lineageId,
        CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(dbContext, connection);
            command.CommandText = """
                SELECT
                    source_entity_lineage_id,
                    from_source_entity_id,
                    to_source_entity_id,
                    relationship_kind,
                    note,
                    created_by_user_id,
                    created_at,
                    void_reason,
                    voided_by_user_id,
                    voided_at
                FROM source_entity_lineage
                WHERE source_entity_lineage_id = @lineage_id;
                """;
            AddParameter(command, "@lineage_id", lineageId);
            return (await ReadLineageAsync(command, cancellationToken)).SingleOrDefault();
        }
        finally
        {
            await CloseIfNeededAsync(dbContext, connection, openedHere);
        }
    }

    public static async Task<(StoredSourceLineage Lineage, bool Changed)> CreateLineageAsync(
        RulesCoreDbContext dbContext,
        Guid fromSourceEntityId,
        Guid toSourceEntityId,
        string relationshipKind,
        string? note,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var relevant = await GetLineageForSourcesAsync(
            dbContext,
            [fromSourceEntityId, toSourceEntityId],
            includeVoided: false,
            cancellationToken);
        var existing = relevant.SingleOrDefault(value =>
            value.FromSourceEntityId == fromSourceEntityId
            && value.ToSourceEntityId == toSourceEntityId
            && string.Equals(value.RelationshipKind, relationshipKind, StringComparison.Ordinal));
        if (existing is not null)
        {
            if (!string.Equals(existing.Note, note, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "An active lineage relationship with the same direction and kind already exists with a different note. Void it before recording a replacement.");
            }
            return (existing, false);
        }

        var lineage = new StoredSourceLineage(
            Guid.NewGuid(),
            fromSourceEntityId,
            toSourceEntityId,
            relationshipKind,
            note,
            actorUserId,
            DateTimeOffset.UtcNow,
            null,
            null,
            null);

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(dbContext, connection);
            command.CommandText = """
                INSERT INTO source_entity_lineage (
                    source_entity_lineage_id,
                    from_source_entity_id,
                    to_source_entity_id,
                    relationship_kind,
                    note,
                    created_by_user_id,
                    created_at)
                VALUES (@id, @from_id, @to_id, @kind, @note, @actor, @created_at);
                """;
            AddParameter(command, "@id", lineage.Id);
            AddParameter(command, "@from_id", lineage.FromSourceEntityId);
            AddParameter(command, "@to_id", lineage.ToSourceEntityId);
            AddParameter(command, "@kind", lineage.RelationshipKind);
            AddNullableParameter(command, "@note", lineage.Note);
            AddParameter(command, "@actor", lineage.CreatedByUserId);
            AddParameter(command, "@created_at", lineage.CreatedAt);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return (lineage, true);
        }
        finally
        {
            await CloseIfNeededAsync(dbContext, connection, openedHere);
        }
    }

    public static async Task<(StoredSourceLineage Lineage, bool Changed)?> VoidLineageAsync(
        RulesCoreDbContext dbContext,
        Guid lineageId,
        string? reason,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetLineageByIdAsync(dbContext, lineageId, cancellationToken);
        if (existing is null)
        {
            return null;
        }
        if (existing.VoidedAt is not null)
        {
            return (existing, false);
        }

        var voidedAt = DateTimeOffset.UtcNow;
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(dbContext, connection);
            command.CommandText = """
                UPDATE source_entity_lineage
                SET void_reason = @reason,
                    voided_by_user_id = @actor,
                    voided_at = @voided_at
                WHERE source_entity_lineage_id = @id
                    AND voided_at IS NULL;
                """;
            AddNullableParameter(command, "@reason", reason);
            AddParameter(command, "@actor", actorUserId);
            AddParameter(command, "@voided_at", voidedAt);
            AddParameter(command, "@id", lineageId);
            var changed = await command.ExecuteNonQueryAsync(cancellationToken) > 0;
            var updated = existing with
            {
                VoidReason = changed ? reason : existing.VoidReason,
                VoidedByUserId = changed ? actorUserId : existing.VoidedByUserId,
                VoidedAt = changed ? voidedAt : existing.VoidedAt
            };
            return (updated, changed);
        }
        finally
        {
            await CloseIfNeededAsync(dbContext, connection, openedHere);
        }
    }

    public static IReadOnlyList<NormalizedDecisionContribution> NormalizeDecisionContributions(
        IReadOnlyList<RuleConsolidationContributionRequest>? contributions)
    {
        if (contributions is null || contributions.Count == 0)
        {
            return [];
        }

        var normalized = new List<NormalizedDecisionContribution>(contributions.Count);
        var revisionIds = new HashSet<Guid>();
        foreach (var contribution in contributions)
        {
            if (contribution.SourceEntityRevisionId == Guid.Empty)
            {
                throw new ArgumentException("A consolidation contribution source revision can not be empty.");
            }
            if (!revisionIds.Add(contribution.SourceEntityRevisionId))
            {
                throw new ArgumentException("A source revision can appear only once in a consolidation contribution set.");
            }

            var kind = contribution.ContributionKind?.Trim().ToLowerInvariant();
            if (kind is null || !RuleConsolidationContributionKinds.All.Contains(kind))
            {
                throw new ArgumentException(
                    $"Unsupported consolidation contribution kind '{contribution.ContributionKind}'.");
            }

            var note = NormalizeOptional(contribution.Note, 1000, "contribution note");
            normalized.Add(new NormalizedDecisionContribution(
                contribution.SourceEntityRevisionId,
                kind,
                note));
        }

        return normalized
            .OrderBy(value => value.SourceEntityRevisionId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
    }

    public static string ComputeContributionFingerprint(
        IReadOnlyCollection<NormalizedDecisionContribution> contributions)
    {
        if (contributions.Count == 0)
        {
            return string.Empty;
        }

        var canonical = string.Join(
            '\n',
            contributions
                .OrderBy(value => value.SourceEntityRevisionId.ToString("D"), StringComparer.Ordinal)
                .Select(value => string.Join(
                    '\u001f',
                    value.SourceEntityRevisionId.ToString("D"),
                    value.ContributionKind,
                    value.Note ?? string.Empty)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static async Task<IReadOnlyList<NormalizedDecisionContribution>> GetDecisionContributionsAsync(
        RulesCoreDbContext dbContext,
        Guid globalRuleDecisionId,
        CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(dbContext, connection);
            command.CommandText = """
                SELECT source_entity_revision_id, contribution_kind, note
                FROM global_rule_decision_contribution
                WHERE global_rule_decision_id = @decision_id
                ORDER BY source_entity_revision_id, contribution_kind;
                """;
            AddParameter(command, "@decision_id", globalRuleDecisionId);
            var results = new List<NormalizedDecisionContribution>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new NormalizedDecisionContribution(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2)));
            }
            return results;
        }
        finally
        {
            await CloseIfNeededAsync(dbContext, connection, openedHere);
        }
    }

    public static async Task InsertDecisionContributionsAsync(
        RulesCoreDbContext dbContext,
        Guid globalRuleDecisionId,
        IReadOnlyCollection<NormalizedDecisionContribution> contributions,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (contributions.Count == 0)
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            foreach (var contribution in contributions)
            {
                await using var command = CreateCommand(dbContext, connection);
                command.CommandText = """
                    INSERT INTO global_rule_decision_contribution (
                        global_rule_decision_contribution_id,
                        global_rule_decision_id,
                        source_entity_revision_id,
                        contribution_kind,
                        note,
                        created_by_user_id,
                        created_at)
                    VALUES (@id, @decision_id, @revision_id, @kind, @note, @actor, @created_at);
                    """;
                AddParameter(command, "@id", Guid.NewGuid());
                AddParameter(command, "@decision_id", globalRuleDecisionId);
                AddParameter(command, "@revision_id", contribution.SourceEntityRevisionId);
                AddParameter(command, "@kind", contribution.ContributionKind);
                AddNullableParameter(command, "@note", contribution.Note);
                AddParameter(command, "@actor", actorUserId);
                AddParameter(command, "@created_at", DateTimeOffset.UtcNow);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            await CloseIfNeededAsync(dbContext, connection, openedHere);
        }
    }

    public static async Task CopyDecisionContributionsAsync(
        RulesCoreDbContext dbContext,
        Guid fromDecisionId,
        Guid toDecisionId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var contributions = await GetDecisionContributionsAsync(
            dbContext,
            fromDecisionId,
            cancellationToken);
        await InsertDecisionContributionsAsync(
            dbContext,
            toDecisionId,
            contributions,
            actorUserId,
            cancellationToken);
    }

    private static async Task<IReadOnlyList<StoredSourceLineage>> ReadLineageAsync(
        DbCommand command,
        CancellationToken cancellationToken)
    {
        var results = new List<StoredSourceLineage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new StoredSourceLineage(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9)));
        }
        return results;
    }

    private static DbCommand CreateCommand(RulesCoreDbContext dbContext, DbConnection connection)
    {
        var command = connection.CreateCommand();
        if (dbContext.Database.CurrentTransaction is { } transaction)
        {
            command.Transaction = transaction.GetDbTransaction();
        }
        return command;
    }

    private static async Task<bool> EnsureOpenAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        if (connection.State == ConnectionState.Open)
        {
            return false;
        }
        await connection.OpenAsync(cancellationToken);
        return true;
    }

    private static async Task CloseIfNeededAsync(
        RulesCoreDbContext dbContext,
        DbConnection connection,
        bool openedHere)
    {
        if (openedHere && dbContext.Database.CurrentTransaction is null)
        {
            await connection.CloseAsync();
        }
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

    private static void EnsureCompatible(string? existing, string? requested, string label)
    {
        if (existing is not null
            && requested is not null
            && !string.Equals(existing, requested, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The source edition already records {label} '{existing}', which conflicts with requested value '{requested}'.");
        }
    }

    private static void EnsureCompatible(DateOnly? existing, DateOnly? requested, string label)
    {
        if (existing.HasValue && requested.HasValue && existing.Value != requested.Value)
        {
            throw new InvalidOperationException(
                $"The source edition already records {label} '{existing:yyyy-MM-dd}', which conflicts with requested value '{requested:yyyy-MM-dd}'.");
        }
    }

    private static string? NormalizeOptional(string? value, int maxLength, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{label} can not exceed {maxLength} characters.");
        }
        return normalized;
    }
}

internal sealed record StoredSourceEditionMetadata(
    string? GameEdition,
    string? ReleaseKind,
    DateOnly? PublicationDate);

internal sealed record StoredSourceLineage(
    Guid Id,
    Guid FromSourceEntityId,
    Guid ToSourceEntityId,
    string RelationshipKind,
    string? Note,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    string? VoidReason,
    string? VoidedByUserId,
    DateTimeOffset? VoidedAt);

internal sealed record NormalizedDecisionContribution(
    Guid SourceEntityRevisionId,
    string ContributionKind,
    string? Note);
