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
                    COALESCE(entity.source_code, ''),
                    entity.entity_name,
                    revision.raw_json::text,
                    publication.canonical_publication_id,
                    publication.display_name,
                    publication.publisher,
                    publication.game_edition,
                    publication.publication_date
                FROM source_entity entity
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
                WHERE entity.source_package_id = @package_id
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
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetGuid(4),
                    reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    reader.IsDBNull(8) ? null : reader.GetFieldValue<DateOnly>(8)));
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
                CanonicalSourceIdentity.NormalizeIdentityPart(row.EntityName),
                StringComparison.Ordinal);
        if (canUpgradeTitle && !string.IsNullOrWhiteSpace(observation.Title))
        {
            title = observation.Title;
        }

        var conflict = false;
        var publisher = row.CanonicalPublisher;
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

        var gameEdition = row.CanonicalGameEdition;
        if (gameEdition is null && observation.GameEdition is not null)
        {
            gameEdition = observation.GameEdition;
        }
        else if (gameEdition is not null
                 && observation.GameEdition is not null
                 && !string.Equals(gameEdition, observation.GameEdition, StringComparison.Ordinal))
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
            || !string.Equals(gameEdition, row.CanonicalGameEdition, StringComparison.Ordinal)
            || publicationDate != row.CanonicalPublicationDate;
        if (!changed)
        {
            return new CanonicalReconciliationOutcome(false, conflict);
        }

        var fingerprint = CanonicalSourceIdentity.BibliographicFingerprint(
            new CanonicalPublicationEvidence(
                title,
                publisher,
                gameEdition,
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
                    game_edition = @game_edition,
                    publication_date = @publication_date,
                    bibliographic_fingerprint = @bibliographic_fingerprint
                WHERE canonical_publication_id = @publication_id;
                """;
            AddParameter(command, "@display_name", title);
            AddNullableParameter(command, "@publisher", publisher);
            AddNullableParameter(command, "@game_edition", gameEdition);
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
        var gameEdition = MapEdition(ReadString(root, "edition"));
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

        return new PublicationObservation(title, publisher, gameEdition, publicationDate);
    }

    private static string? MapEdition(string? edition) =>
        string.Equals(edition, "classic", StringComparison.OrdinalIgnoreCase)
            ? "5e"
            : string.Equals(edition, "one", StringComparison.OrdinalIgnoreCase)
                ? "5.5e"
                : null;

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
        string SourceCode,
        string EntityName,
        string RawJson,
        Guid CanonicalPublicationId,
        string CanonicalDisplayName,
        string? CanonicalPublisher,
        string? CanonicalGameEdition,
        DateOnly? CanonicalPublicationDate);

    private sealed record PublicationObservation(
        string Title,
        string? Publisher,
        string? GameEdition,
        DateOnly? PublicationDate);

    private sealed record CanonicalReconciliationOutcome(bool Updated, bool Conflict);
}
