using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed record CanonicalPublisherPropagationResult(
    int UpdatedPublications,
    int ConflictingPublications);

public sealed class CanonicalPublicationPublisherService(RulesCoreDbContext dbContext)
{
    public async Task<CanonicalPublisherPropagationResult> ReconcilePackageAsync(
        Guid sourcePackageId,
        CancellationToken cancellationToken = default)
    {
        if (sourcePackageId == Guid.Empty)
        {
            throw new ArgumentException("Source package ID can not be empty.", nameof(sourcePackageId));
        }

        // Canonical identity must be indexed before publisher reconciliation; this service
        // intentionally does not create canonical publications on its own.
        await SourcePublisherStore.EnsureSchemaAsync(dbContext, cancellationToken);

        var candidates = await ReadCandidatesAsync(sourcePackageId, cancellationToken);
        var updated = 0;
        var conflicts = 0;
        foreach (var candidate in candidates)
        {
            if (candidate.Publisher is null)
            {
                continue;
            }

            if (candidate.CanonicalPublisher is null)
            {
                var fingerprint = CanonicalSourceIdentity.BibliographicFingerprint(
                    new CanonicalPublicationEvidence(
                        candidate.DisplayName,
                        candidate.Publisher,
                        candidate.GameEdition,
                        candidate.PublicationDate));
                updated += await SetPublisherIfMissingAsync(
                    candidate.CanonicalPublicationId,
                    candidate.Publisher,
                    fingerprint,
                    cancellationToken);
                continue;
            }

            if (!string.Equals(
                    candidate.CanonicalPublisher,
                    candidate.Publisher,
                    StringComparison.Ordinal))
            {
                conflicts++;
            }
        }

        return new CanonicalPublisherPropagationResult(updated, conflicts);
    }

    private async Task<IReadOnlyList<PublisherCandidate>> ReadCandidatesAsync(
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
                SELECT DISTINCT
                    occurrence.canonical_publication_id,
                    publication.display_name,
                    publication.publisher,
                    publication.game_edition,
                    publication.publication_date,
                    metadata.publisher
                FROM source_entity entity
                JOIN source_edition edition
                    ON edition.source_edition_id = entity.source_edition_id
                JOIN source_work work
                    ON work.source_work_id = edition.source_work_id
                JOIN source_edition_metadata metadata
                    ON metadata.source_edition_id = edition.source_edition_id
                JOIN source_entity_occurrence_binding binding
                    ON binding.source_entity_id = entity.source_entity_id
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                JOIN canonical_publication publication
                    ON publication.canonical_publication_id = occurrence.canonical_publication_id
                WHERE work.source_package_id = @package_id
                    AND metadata.publisher IS NOT NULL
                ORDER BY occurrence.canonical_publication_id;
                """;
            AddParameter(command, "@package_id", sourcePackageId);

            var rows = new List<PublisherCandidate>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new PublisherCandidate(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : DateOnly.FromDateTime(reader.GetDateTime(4)),
                    reader.GetString(5)));
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

    private async Task<int> SetPublisherIfMissingAsync(
        Guid canonicalPublicationId,
        string publisher,
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
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE canonical_publication
                SET publisher = @publisher,
                    bibliographic_fingerprint = @bibliographic_fingerprint
                WHERE canonical_publication_id = @publication_id
                    AND publisher IS NULL;
                """;
            AddParameter(command, "@publisher", publisher);
            AddParameter(command, "@bibliographic_fingerprint", bibliographicFingerprint);
            AddParameter(command, "@publication_id", canonicalPublicationId);
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record PublisherCandidate(
        Guid CanonicalPublicationId,
        string DisplayName,
        string? CanonicalPublisher,
        string? GameEdition,
        DateOnly? PublicationDate,
        string? Publisher);
}
