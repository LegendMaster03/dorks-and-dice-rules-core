using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed record PublicationMetadataReconciliationResult(
    int UpdatedPublications,
    int ConflictingPublications);

public sealed class FiveEToolsPublicationMetadataReconciliationService(RulesCoreDbContext dbContext)
{
    public async Task<PublicationMetadataReconciliationResult> ReconcilePackageAsync(
        Guid sourcePackageId,
        CancellationToken cancellationToken = default)
    {
        if (sourcePackageId == Guid.Empty)
        {
            throw new ArgumentException("Source package ID can not be empty.", nameof(sourcePackageId));
        }

        await SourceFrameworkStore.EnsureSchemaAsync(dbContext, cancellationToken);
        var rows = await ReadMetadataEntitiesAsync(sourcePackageId, cancellationToken);
        var updated = 0;
        var conflicts = 0;

        foreach (var row in rows)
        {
            PublicationObservation? observation;
            try
            {
                observation = ParseObservation(row.RawJson);
            }
            catch (JsonException)
            {
                continue;
            }
            if (observation is null)
            {
                continue;
            }

            if (observation.PublicationDate is DateOnly publicationDate)
            {
                try
                {
                    await SourceFrameworkStore.MergeEditionMetadataAsync(
                        dbContext,
                        row.SourceEditionId,
                        gameEdition: null,
                        releaseKind: null,
                        publicationDate,
                        cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    conflicts++;
                }
            }

            if (!string.IsNullOrWhiteSpace(observation.Publisher))
            {
                try
                {
                    await SourcePublisherStore.MergePublisherAsync(
                        dbContext,
                        row.SourceEditionId,
                        observation.Publisher,
                        cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    conflicts++;
                }
            }

            var outcome = await ReconcileCanonicalPublicationAsync(
                row,
                observation,
                cancellationToken);
            updated += outcome.Updated ? 1 : 0;
            conflicts += outcome.Conflict ? 1 : 0;
        }

        return new PublicationMetadataReconciliationResult(updated, conflicts);
    }

    private async Task<IReadOnlyList<MetadataEntityRow>> ReadMetadataEntitiesAsync(
        Guid sourcePackageId,
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
                    entity.source_entity_id,
                    entity.source_edition_id,
                    entity.source_code,
                    work.display_name,
                    revision.raw_json::text,
                    publication.canonical_publication_id,
                    publication.display_name,
                    publication.publisher,
                    publication.game_edition,
                    publication.publication_date
                FROM source_entity entity
                JOIN source_edition edition
                    ON edition.source_edition_id = entity.source_edition_id
                JOIN source_work work
                    ON work.source_work_id = edition.source_work_id
                JOIN LATERAL (
                    SELECT raw_json
                    FROM source_entity_revision candidate
                    WHERE candidate.source_entity_id = entity.source_entity_id
                    ORDER BY candidate.revision_number DESC
                    LIMIT 1) revision ON TRUE
                JOIN source_entity_occurrence_binding binding
                    ON binding.source_entity_id = entity.source_entity_id
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                JOIN canonical_publication publication
                    ON publication.canonical_publication_id = occurrence.canonical_publication_id
                WHERE work.source_package_id = @package_id
                    AND lower(entity.entity_type) IN ('book', 'adventure')
                ORDER BY entity.source_code, entity.source_entity_id;
                """;
            AddParameter(command, "@package_id", sourcePackageId);

            var rows = new List<MetadataEntityRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new MetadataEntityRow(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetGuid(5),
                    reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8),
                    reader.IsDBNull(9) ? null : reader.GetFieldValue<DateOnly>(9)));
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

    private async Task<CanonicalReconciliationOutcome> ReconcileCanonicalPublicationAsync(
        MetadataEntityRow row,
        PublicationObservation observation,
        CancellationToken cancellationToken)
    {
        var title = row.CanonicalDisplayName;
        var canUpgradeTitle = string.Equals(
                CanonicalSourceIdentity.NormalizeIdentityPart(row.CanonicalDisplayName),
                CanonicalSourceIdentity.NormalizeIdentityPart(row.SourceCode),
                StringComparison.Ordinal)
            || string.Equals(
                CanonicalSourceIdentity.NormalizeIdentityPart(row.CanonicalDisplayName),
                CanonicalSourceIdentity.NormalizeIdentityPart(row.WorkDisplayName),
                StringComparison.Ordinal);
        if (canUpgradeTitle && !string.IsNullOrWhiteSpace(observation.Title))
        {
            title = observation.Title;
        }

        var publisher = row.CanonicalPublisher;
        var conflict = false;
        if (publisher is null && !string.IsNullOrWhiteSpace(observation.Publisher))
        {
            publisher = observation.Publisher;
        }
        else if (publisher is not null
                 && observation.Publisher is not null
                 && !string.Equals(publisher, observation.Publisher, StringComparison.Ordinal))
        {
            conflict = true;
        }

        var publicationDate = row.CanonicalPublicationDate;
        if (publicationDate is null && observation.PublicationDate is not null)
        {
            publicationDate = observation.PublicationDate;
        }
        else if (publicationDate is not null
                 && observation.PublicationDate is not null
                 && publicationDate.Value != observation.PublicationDate.Value)
        {
            conflict = true;
        }

        var changed = !string.Equals(title, row.CanonicalDisplayName, StringComparison.Ordinal)
            || !string.Equals(publisher, row.CanonicalPublisher, StringComparison.Ordinal)
            || publicationDate != row.CanonicalPublicationDate;
        if (!changed)
        {
            return new CanonicalReconciliationOutcome(false, conflict);
        }

        var fingerprint = CanonicalSourceIdentity.BibliographicFingerprint(
            new CanonicalPublicationEvidence(
                title,
                publisher,
                row.GameEdition,
                publicationDate));

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
                SET display_name = @display_name,
                    publisher = @publisher,
                    publication_date = @publication_date,
                    bibliographic_fingerprint = @bibliographic_fingerprint
                WHERE canonical_publication_id = @publication_id;
                """;
            AddParameter(command, "@display_name", title);
            AddNullableParameter(command, "@publisher", publisher);
            AddNullableParameter(command, "@publication_date", publicationDate);
            AddParameter(command, "@bibliographic_fingerprint", fingerprint);
            AddParameter(command, "@publication_id", row.CanonicalPublicationId);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new CanonicalReconciliationOutcome(true, conflict);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static PublicationObservation? ParseObservation(string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var title = ReadString(root, "name");
        if (title is null)
        {
            return null;
        }

        var publisher = ReadString(root, "publisher");
        var published = ReadString(root, "published");
        DateOnly? publicationDate = null;
        if (published is not null
            && DateOnly.TryParseExact(
                published,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate))
        {
            publicationDate = parsedDate;
        }

        return new PublicationObservation(title, publisher, publicationDate);
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            return null;
        }
        return value.GetString()!.Trim();
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

    private sealed record MetadataEntityRow(
        Guid SourceEntityId,
        Guid SourceEditionId,
        string SourceCode,
        string WorkDisplayName,
        string RawJson,
        Guid CanonicalPublicationId,
        string CanonicalDisplayName,
        string? CanonicalPublisher,
        string? GameEdition,
        DateOnly? CanonicalPublicationDate);

    private sealed record PublicationObservation(
        string Title,
        string? Publisher,
        DateOnly? PublicationDate);

    private sealed record CanonicalReconciliationOutcome(bool Updated, bool Conflict);
}
