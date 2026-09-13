using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed class CanonicalSourceRepresentationService(RulesCoreDbContext dbContext)
{
    public async Task<CanonicalSourceAssociationView> AssociateSourceEntityAsync(
        Guid sourceEntityId,
        CanonicalPublicationEvidence publicationEvidence,
        CanonicalSourceOccurrenceEvidence occurrenceEvidence,
        string representationKind,
        CancellationToken cancellationToken = default)
    {
        if (sourceEntityId == Guid.Empty)
        {
            throw new ArgumentException("Source entity ID can not be empty.", nameof(sourceEntityId));
        }
        ArgumentNullException.ThrowIfNull(publicationEvidence);
        ArgumentNullException.ThrowIfNull(occurrenceEvidence);
        if (string.IsNullOrWhiteSpace(representationKind))
        {
            throw new ArgumentException("Representation kind can not be blank.", nameof(representationKind));
        }
        if (!await dbContext.SourceEntities.AnyAsync(value => value.Id == sourceEntityId, cancellationToken))
        {
            throw new KeyNotFoundException($"Source entity '{sourceEntityId}' does not exist.");
        }

        var identity = new CanonicalSourceIdentityService(dbContext);
        var publication = await identity.ResolvePublicationAsync(publicationEvidence, cancellationToken);
        await new CanonicalPublicationEvidenceReconciliationService(dbContext).ReconcileAsync(
            publication.Id,
            sourceEntityId,
            publicationEvidence,
            cancellationToken);

        var semanticOccurrence = await FindUniqueSemanticOccurrenceAsync(
            publication.Id,
            occurrenceEvidence,
            cancellationToken);

        var occurrenceId = semanticOccurrence
            ?? await identity.ResolveOccurrenceAsync(
                publication.Id,
                occurrenceEvidence,
                cancellationToken);
        var occurrenceMatchKind = semanticOccurrence.HasValue
            ? "semantic-fingerprint"
            : "identity-key";
        var confidence = semanticOccurrence.HasValue ? 0.99 : 1.0;

        await BindAsync(
            sourceEntityId,
            occurrenceId,
            occurrenceEvidence,
            $"{NormalizeRepresentationKind(representationKind)}:{occurrenceMatchKind}",
            confidence,
            cancellationToken);

        return new CanonicalSourceAssociationView(
            publication,
            occurrenceId,
            publication.MatchKind,
            occurrenceMatchKind,
            Math.Min(publication.Confidence, confidence));
    }

    private async Task<Guid?> FindUniqueSemanticOccurrenceAsync(
        Guid publicationId,
        CanonicalSourceOccurrenceEvidence evidence,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(evidence.SemanticFingerprint))
        {
            return null;
        }

        var locatorKey = NormalizeOptional(evidence.LocatorKey);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = locatorKey is null
                ? """
                    SELECT DISTINCT occurrence.canonical_source_occurrence_id
                    FROM canonical_source_occurrence occurrence
                    JOIN source_entity_occurrence_binding binding
                        ON binding.canonical_source_occurrence_id = occurrence.canonical_source_occurrence_id
                    WHERE occurrence.canonical_publication_id = @publication_id
                        AND occurrence.entity_type = @entity_type
                        AND binding.semantic_fingerprint = @semantic_fingerprint
                    ORDER BY occurrence.canonical_source_occurrence_id
                    LIMIT 2;
                    """
                : """
                    SELECT DISTINCT occurrence.canonical_source_occurrence_id
                    FROM canonical_source_occurrence occurrence
                    JOIN source_entity_occurrence_binding binding
                        ON binding.canonical_source_occurrence_id = occurrence.canonical_source_occurrence_id
                    WHERE occurrence.canonical_publication_id = @publication_id
                        AND occurrence.entity_type = @entity_type
                        AND binding.semantic_fingerprint = @semantic_fingerprint
                        AND binding.locator_key = @locator_key
                    ORDER BY occurrence.canonical_source_occurrence_id
                    LIMIT 2;
                    """;
            AddParameter(command, "@publication_id", publicationId);
            AddParameter(command, "@entity_type", evidence.EntityType.Trim());
            AddParameter(command, "@semantic_fingerprint", evidence.SemanticFingerprint.Trim().ToLowerInvariant());
            if (locatorKey is not null)
            {
                AddParameter(command, "@locator_key", locatorKey);
            }

            var matches = new List<Guid>(2);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                matches.Add(reader.GetGuid(0));
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

    private async Task BindAsync(
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
            await using (var existingCommand = connection.CreateCommand())
            {
                existingCommand.CommandText = """
                    SELECT canonical_source_occurrence_id
                    FROM source_entity_occurrence_binding
                    WHERE source_entity_id = @source_entity_id;
                    """;
                AddParameter(existingCommand, "@source_entity_id", sourceEntityId);
                var existing = await existingCommand.ExecuteScalarAsync(cancellationToken);
                if (existing is Guid existingOccurrenceId)
                {
                    if (existingOccurrenceId != occurrenceId)
                    {
                        throw new InvalidOperationException(
                            $"Source entity '{sourceEntityId}' is already associated with a different canonical occurrence.");
                    }
                    return;
                }
            }

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
                    @created_at);
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@source_entity_id", sourceEntityId);
            AddParameter(command, "@occurrence_id", occurrenceId);
            AddParameter(command, "@semantic_fingerprint", evidence.SemanticFingerprint.Trim().ToLowerInvariant());
            AddNullableParameter(command, "@locator_key", NormalizeOptional(evidence.LocatorKey));
            AddParameter(command, "@match_kind", matchKind);
            AddParameter(command, "@confidence", confidence);
            AddParameter(command, "@created_at", DateTimeOffset.UtcNow);
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

    private static string NormalizeRepresentationKind(string value)
    {
        var normalized = CanonicalSourceIdentity.NormalizeIdentityPart(value);
        if (string.IsNullOrEmpty(normalized))
        {
            throw new ArgumentException("Representation kind must contain an alphanumeric character.", nameof(value));
        }
        return normalized.Length <= 60 ? normalized : normalized[..60];
    }

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
}
