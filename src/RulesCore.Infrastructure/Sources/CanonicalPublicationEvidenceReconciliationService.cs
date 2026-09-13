using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed record CanonicalPublicationEvidenceReconciliationResult(
    bool Updated,
    IReadOnlyList<string> ConflictingFields);

public sealed class CanonicalPublicationEvidenceReconciliationService(RulesCoreDbContext dbContext)
{
    public async Task<CanonicalPublicationEvidenceReconciliationResult> ReconcileAsync(
        Guid canonicalPublicationId,
        Guid sourceEntityId,
        CanonicalPublicationEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        if (canonicalPublicationId == Guid.Empty)
        {
            throw new ArgumentException("Canonical publication ID can not be empty.", nameof(canonicalPublicationId));
        }
        if (sourceEntityId == Guid.Empty)
        {
            throw new ArgumentException("Source entity ID can not be empty.", nameof(sourceEntityId));
        }
        ArgumentNullException.ThrowIfNull(evidence);

        await EnsureSchemaAsync(cancellationToken);
        var current = await ReadPublicationAsync(canonicalPublicationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Canonical publication '{canonicalPublicationId}' was not found.");

        var conflicts = new List<string>();
        var publisher = Merge("publisher", current.Publisher, Normalize(evidence.Publisher), conflicts);
        var gameEdition = Merge("game-edition", current.GameEdition, Normalize(evidence.GameEdition), conflicts);
        var publicationDate = MergeDate(current.PublicationDate, evidence.PublicationDate, conflicts);

        foreach (var field in conflicts)
        {
            var canonicalValue = field switch
            {
                "publisher" => current.Publisher,
                "game-edition" => current.GameEdition,
                "publication-date" => current.PublicationDate?.ToString("yyyy-MM-dd"),
                _ => null
            };
            var observedValue = field switch
            {
                "publisher" => Normalize(evidence.Publisher),
                "game-edition" => Normalize(evidence.GameEdition),
                "publication-date" => evidence.PublicationDate?.ToString("yyyy-MM-dd"),
                _ => null
            };
            await RecordConflictAsync(
                canonicalPublicationId,
                sourceEntityId,
                field,
                canonicalValue,
                observedValue,
                cancellationToken);
        }

        var updated = !string.Equals(publisher, current.Publisher, StringComparison.Ordinal)
            || !string.Equals(gameEdition, current.GameEdition, StringComparison.Ordinal)
            || publicationDate != current.PublicationDate;
        if (updated)
        {
            var fingerprint = CanonicalSourceIdentity.BibliographicFingerprint(
                new CanonicalPublicationEvidence(
                    current.DisplayName,
                    publisher,
                    gameEdition,
                    publicationDate,
                    OccurrenceFingerprints: evidence.OccurrenceFingerprints));
            await UpdatePublicationAsync(
                canonicalPublicationId,
                publisher,
                gameEdition,
                publicationDate,
                fingerprint,
                cancellationToken);
        }

        return new CanonicalPublicationEvidenceReconciliationResult(updated, conflicts);
    }

    private async Task<StoredPublication?> ReadPublicationAsync(
        Guid publicationId,
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
                SELECT display_name, publisher, game_edition, publication_date
                FROM canonical_publication
                WHERE canonical_publication_id = @id;
                """;
            AddParameter(command, "@id", publicationId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }
            return new StoredPublication(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetFieldValue<DateOnly>(3));
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task UpdatePublicationAsync(
        Guid publicationId,
        string? publisher,
        string? gameEdition,
        DateOnly? publicationDate,
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
                UPDATE canonical_publication
                SET publisher = @publisher,
                    game_edition = @game_edition,
                    publication_date = @publication_date,
                    bibliographic_fingerprint = @fingerprint
                WHERE canonical_publication_id = @id;
                """;
            AddNullableParameter(command, "@publisher", publisher);
            AddNullableParameter(command, "@game_edition", gameEdition);
            AddNullableParameter(command, "@publication_date", publicationDate);
            AddParameter(command, "@fingerprint", fingerprint);
            AddParameter(command, "@id", publicationId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task RecordConflictAsync(
        Guid publicationId,
        Guid sourceEntityId,
        string field,
        string? canonicalValue,
        string? observedValue,
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
                INSERT INTO canonical_publication_evidence_conflict (
                    canonical_publication_evidence_conflict_id,
                    canonical_publication_id,
                    source_entity_id,
                    field_name,
                    canonical_value,
                    observed_value,
                    recorded_at)
                VALUES (@id, @publication_id, @source_entity_id, @field_name,
                        @canonical_value, @observed_value, @recorded_at)
                ON CONFLICT (canonical_publication_id, source_entity_id, field_name)
                DO UPDATE SET
                    canonical_value = EXCLUDED.canonical_value,
                    observed_value = EXCLUDED.observed_value,
                    recorded_at = EXCLUDED.recorded_at;
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@publication_id", publicationId);
            AddParameter(command, "@source_entity_id", sourceEntityId);
            AddParameter(command, "@field_name", field);
            AddNullableParameter(command, "@canonical_value", canonicalValue);
            AddNullableParameter(command, "@observed_value", observedValue);
            AddParameter(command, "@recorded_at", DateTimeOffset.UtcNow);
            await command.ExecuteNonQueryAsync(cancellationToken);
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

    private static string? Merge(
        string field,
        string? canonical,
        string? observed,
        ICollection<string> conflicts)
    {
        if (canonical is null)
        {
            return observed;
        }
        if (observed is not null && !string.Equals(canonical, observed, StringComparison.Ordinal))
        {
            conflicts.Add(field);
        }
        return canonical;
    }

    private static DateOnly? MergeDate(
        DateOnly? canonical,
        DateOnly? observed,
        ICollection<string> conflicts)
    {
        if (canonical is null)
        {
            return observed;
        }
        if (observed is not null && canonical.Value != observed.Value)
        {
            conflicts.Add("publication-date");
        }
        return canonical;
    }

    private static string? Normalize(string? value) =>
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

    private sealed record StoredPublication(
        string DisplayName,
        string? Publisher,
        string? GameEdition,
        DateOnly? PublicationDate);

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS canonical_publication_evidence_conflict (
            canonical_publication_evidence_conflict_id uuid NOT NULL,
            canonical_publication_id uuid NOT NULL,
            source_entity_id uuid NOT NULL,
            field_name varchar(80) NOT NULL,
            canonical_value varchar(1000) NULL,
            observed_value varchar(1000) NULL,
            recorded_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_canonical_publication_evidence_conflict PRIMARY KEY (canonical_publication_evidence_conflict_id),
            CONSTRAINT fk_canonical_publication_evidence_conflict_publication FOREIGN KEY (canonical_publication_id)
                REFERENCES canonical_publication(canonical_publication_id) ON DELETE CASCADE,
            CONSTRAINT fk_canonical_publication_evidence_conflict_source_entity FOREIGN KEY (source_entity_id)
                REFERENCES source_entity(source_entity_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_publication_evidence_conflict_observation
            ON canonical_publication_evidence_conflict(canonical_publication_id, source_entity_id, field_name);
        CREATE INDEX IF NOT EXISTS ix_canonical_publication_evidence_conflict_publication
            ON canonical_publication_evidence_conflict(canonical_publication_id);
        """;
}
