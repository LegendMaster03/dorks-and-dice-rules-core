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
        Guid sourceEntityRevisionId,
        CanonicalPublicationEvidence publicationEvidence,
        CanonicalSourceOccurrenceEvidence occurrenceEvidence,
        string representationKind,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? canonicalAliases = null)
    {
        if (sourceEntityId == Guid.Empty)
        {
            throw new ArgumentException("Source entity ID can not be empty.", nameof(sourceEntityId));
        }
        if (sourceEntityRevisionId == Guid.Empty)
        {
            throw new ArgumentException("Source entity revision ID can not be empty.", nameof(sourceEntityRevisionId));
        }
        ArgumentNullException.ThrowIfNull(publicationEvidence);
        ArgumentNullException.ThrowIfNull(occurrenceEvidence);
        if (string.IsNullOrWhiteSpace(representationKind))
        {
            throw new ArgumentException("Representation kind can not be blank.", nameof(representationKind));
        }

        var sourcePackageId = await dbContext.SourceEntities
            .AsNoTracking()
            .Where(value => value.Id == sourceEntityId)
            .Select(value => (Guid?)value.SourcePackageId)
            .SingleOrDefaultAsync(cancellationToken);
        if (sourcePackageId is null)
        {
            throw new KeyNotFoundException($"Source entity '{sourceEntityId}' does not exist.");
        }
        if (!await dbContext.SourceEntityRevisions
                .AsNoTracking()
                .AnyAsync(
                    value => value.Id == sourceEntityRevisionId && value.SourceEntityId == sourceEntityId,
                    cancellationToken))
        {
            throw new KeyNotFoundException(
                $"Source entity revision '{sourceEntityRevisionId}' does not belong to source entity '{sourceEntityId}'.");
        }

        var scopedPublicationEvidence = await new PackageScopedPublicationEvidenceService(dbContext)
            .EnrichAsync(sourcePackageId.Value, publicationEvidence, cancellationToken);
        var publication = await new CanonicalPublicationIdentityService(dbContext)
            .ResolveAsync(scopedPublicationEvidence, cancellationToken);

        var canonicalEntities = new CanonicalEntityStore(dbContext);
        await canonicalEntities.EnsureSchemaAsync(cancellationToken);
        var priorAssociation = await FindPriorCanonicalAssociationAsync(
            sourceEntityId,
            sourceEntityRevisionId,
            cancellationToken);
        var normalizedFingerprint = occurrenceEvidence.SemanticFingerprint.Trim().ToLowerInvariant();
        var aliasedCanonicalEntityId = await new CanonicalEntityAliasStore(dbContext)
            .ResolveAnyAsync(canonicalAliases, normalizedFingerprint, cancellationToken);
        var semanticOccurrence = await FindUniqueSemanticOccurrenceAsync(
            publication.Id,
            occurrenceEvidence,
            cancellationToken);

        if (aliasedCanonicalEntityId.HasValue && priorAssociation is not null)
        {
            var sameSourceSemantics = string.Equals(
                priorAssociation.SemanticFingerprint,
                normalizedFingerprint,
                StringComparison.Ordinal);
            if (sameSourceSemantics && priorAssociation.CanonicalEntityId != aliasedCanonicalEntityId.Value)
            {
                throw new CanonicalReconciliationConflictException(
                    "A trusted canonical alias can not move an unchanged source revision to a different canonical entity.");
            }
            if (!sameSourceSemantics && priorAssociation.CanonicalEntityId == aliasedCanonicalEntityId.Value)
            {
                throw new CanonicalReconciliationConflictException(
                    "A trusted canonical alias can not collapse a mechanically changed source revision into its prior canonical entity.");
            }
        }
        if (aliasedCanonicalEntityId.HasValue
            && semanticOccurrence?.CanonicalEntityId is Guid semanticCanonicalEntityId
            && semanticCanonicalEntityId != aliasedCanonicalEntityId.Value)
        {
            throw new CanonicalReconciliationConflictException(
                "Trusted source-lineage identity conflicts with an existing exact semantic occurrence.");
        }

        Guid occurrenceId;
        Guid canonicalEntityId;
        string occurrenceMatchKind;
        double confidence;
        if (aliasedCanonicalEntityId.HasValue)
        {
            canonicalEntityId = aliasedCanonicalEntityId.Value;
            if (semanticOccurrence is not null)
            {
                occurrenceId = semanticOccurrence.OccurrenceId;
                if (!semanticOccurrence.CanonicalEntityId.HasValue)
                {
                    await SetOccurrenceCanonicalEntityAsync(
                        occurrenceId,
                        canonicalEntityId,
                        cancellationToken);
                }
            }
            else
            {
                occurrenceId = await ResolveOccurrenceAsync(
                    publication.Id,
                    canonicalEntityId,
                    occurrenceEvidence,
                    cancellationToken);
            }
            occurrenceMatchKind = "strong-alias";
            confidence = 1.0;
        }
        else if (semanticOccurrence is not null)
        {
            occurrenceId = semanticOccurrence.OccurrenceId;
            if (!semanticOccurrence.CanonicalEntityId.HasValue)
            {
                var legacyEntity = await canonicalEntities.ResolveAsync(
                    occurrenceEvidence with
                    {
                        EntityType = semanticOccurrence.EntityType,
                        Name = semanticOccurrence.DisplayName
                    },
                    cancellationToken);
                canonicalEntityId = legacyEntity.Id;
                await SetOccurrenceCanonicalEntityAsync(
                    occurrenceId,
                    canonicalEntityId,
                    cancellationToken);
            }
            else
            {
                canonicalEntityId = semanticOccurrence.CanonicalEntityId.Value;
            }
            occurrenceMatchKind = "semantic-fingerprint";
            confidence = 0.99;
        }
        else
        {
            var inheritedCanonicalEntityId = priorAssociation is not null
                && string.Equals(
                    priorAssociation.SemanticFingerprint,
                    normalizedFingerprint,
                    StringComparison.Ordinal)
                ? priorAssociation.CanonicalEntityId
                : (Guid?)null;
            var canonicalEntity = inheritedCanonicalEntityId.HasValue
                ? await canonicalEntities.ReadAsync(inheritedCanonicalEntityId.Value, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Canonical entity '{inheritedCanonicalEntityId.Value}' referenced by source history no longer exists.")
                : await canonicalEntities.ResolveAsync(occurrenceEvidence, cancellationToken);
            canonicalEntityId = canonicalEntity.Id;

            occurrenceId = await ResolveOccurrenceAsync(
                publication.Id,
                canonicalEntityId,
                occurrenceEvidence,
                cancellationToken);
            occurrenceMatchKind = inheritedCanonicalEntityId.HasValue
                ? "source-revision-lineage"
                : "identity-key";
            confidence = 1.0;
        }

        if (priorAssociation is not null
            && priorAssociation.CanonicalEntityId != canonicalEntityId
            && !string.Equals(
                priorAssociation.SemanticFingerprint,
                normalizedFingerprint,
                StringComparison.Ordinal))
        {
            await new CanonicalEntityRelationshipStore(dbContext).RelateRevisionAsync(
                priorAssociation.CanonicalEntityId,
                canonicalEntityId,
                "source-revision-lineage",
                1.0,
                cancellationToken);
        }

        await BindAsync(
            sourceEntityId,
            sourceEntityRevisionId,
            occurrenceId,
            occurrenceEvidence,
            $"{NormalizeRepresentationKind(representationKind)}:{occurrenceMatchKind}",
            confidence,
            cancellationToken);

        await SeedTrustedAliasesAsync(
            canonicalEntityId,
            canonicalAliases,
            normalizedFingerprint,
            cancellationToken);

        return new CanonicalSourceAssociationView(
            publication,
            occurrenceId,
            publication.MatchKind,
            occurrenceMatchKind,
            Math.Min(publication.Confidence, confidence));
    }

    private async Task<Guid> ResolveOccurrenceAsync(
        Guid publicationId,
        Guid canonicalEntityId,
        CanonicalSourceOccurrenceEvidence evidence,
        CancellationToken cancellationToken)
    {
        var locator = NormalizeOptional(evidence.LocatorKey);
        var occurrenceKey = BuildOccurrenceKey(canonicalEntityId, locator);
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
                AddParameter(read, "@publication_id", publicationId);
                AddParameter(read, "@occurrence_key", occurrenceKey);
                var existing = await read.ExecuteScalarAsync(cancellationToken);
                if (existing is Guid existingId)
                {
                    return existingId;
                }
            }

            var id = Guid.NewGuid();
            await using (var insert = connection.CreateCommand())
            {
                insert.CommandText = """
                    INSERT INTO canonical_source_occurrence (
                        canonical_source_occurrence_id,
                        canonical_publication_id,
                        canonical_entity_id,
                        occurrence_key,
                        entity_type,
                        display_name,
                        created_at)
                    VALUES (@id, @publication_id, @canonical_entity_id, @occurrence_key,
                            @entity_type, @display_name, @created_at)
                    ON CONFLICT (canonical_publication_id, occurrence_key) DO NOTHING;
                    """;
                AddParameter(insert, "@id", id);
                AddParameter(insert, "@publication_id", publicationId);
                AddParameter(insert, "@canonical_entity_id", canonicalEntityId);
                AddParameter(insert, "@occurrence_key", occurrenceKey);
                AddParameter(insert, "@entity_type", evidence.EntityType.Trim());
                AddParameter(insert, "@display_name", evidence.Name.Trim());
                AddParameter(insert, "@created_at", DateTimeOffset.UtcNow);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var reread = connection.CreateCommand();
            reread.CommandText = """
                SELECT canonical_source_occurrence_id
                FROM canonical_source_occurrence
                WHERE canonical_publication_id = @publication_id
                    AND occurrence_key = @occurrence_key;
                """;
            AddParameter(reread, "@publication_id", publicationId);
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

    private async Task<SemanticOccurrenceMatch?> FindUniqueSemanticOccurrenceAsync(
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
                    SELECT DISTINCT
                        occurrence.canonical_source_occurrence_id,
                        occurrence.canonical_entity_id,
                        occurrence.entity_type,
                        occurrence.display_name
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
                    SELECT DISTINCT
                        occurrence.canonical_source_occurrence_id,
                        occurrence.canonical_entity_id,
                        occurrence.entity_type,
                        occurrence.display_name
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

            var matches = new List<SemanticOccurrenceMatch>(2);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                matches.Add(new SemanticOccurrenceMatch(
                    reader.GetGuid(0),
                    reader.IsDBNull(1) ? null : reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetString(3)));
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

    private async Task<PriorCanonicalAssociation?> FindPriorCanonicalAssociationAsync(
        Guid sourceEntityId,
        Guid currentRevisionId,
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
                SELECT occurrence.canonical_entity_id, binding.semantic_fingerprint
                FROM source_entity_revision revision
                JOIN source_entity_occurrence_binding binding
                    ON binding.source_entity_revision_id = revision.source_entity_revision_id
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                WHERE revision.source_entity_id = @source_entity_id
                    AND revision.source_entity_revision_id <> @current_revision_id
                    AND occurrence.canonical_entity_id IS NOT NULL
                ORDER BY revision.revision_number DESC
                LIMIT 1;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            AddParameter(command, "@current_revision_id", currentRevisionId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken)
                ? new PriorCanonicalAssociation(reader.GetGuid(0), reader.GetString(1))
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

    private async Task SetOccurrenceCanonicalEntityAsync(
        Guid occurrenceId,
        Guid canonicalEntityId,
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
                UPDATE canonical_source_occurrence
                SET canonical_entity_id = @canonical_entity_id
                WHERE canonical_source_occurrence_id = @occurrence_id
                    AND canonical_entity_id IS NULL;
                """;
            AddParameter(command, "@canonical_entity_id", canonicalEntityId);
            AddParameter(command, "@occurrence_id", occurrenceId);
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

    private async Task BindAsync(
        Guid sourceEntityId,
        Guid sourceEntityRevisionId,
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
                    WHERE source_entity_revision_id = @source_entity_revision_id;
                    """;
                AddParameter(existingCommand, "@source_entity_revision_id", sourceEntityRevisionId);
                var existing = await existingCommand.ExecuteScalarAsync(cancellationToken);
                if (existing is Guid existingOccurrenceId)
                {
                    if (existingOccurrenceId != occurrenceId)
                    {
                        throw new InvalidOperationException(
                            $"Source entity revision '{sourceEntityRevisionId}' is already associated with a different canonical occurrence.");
                    }
                    return;
                }
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO source_entity_occurrence_binding (
                    source_entity_occurrence_binding_id,
                    source_entity_id,
                    source_entity_revision_id,
                    canonical_source_occurrence_id,
                    semantic_fingerprint,
                    locator_key,
                    match_kind,
                    confidence,
                    created_at)
                VALUES (@id, @source_entity_id, @source_entity_revision_id, @occurrence_id, @semantic_fingerprint,
                        @locator_key, @match_kind, @confidence, @created_at);
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@source_entity_id", sourceEntityId);
            AddParameter(command, "@source_entity_revision_id", sourceEntityRevisionId);
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

    private async Task SeedTrustedAliasesAsync(
        Guid canonicalEntityId,
        IReadOnlyDictionary<string, string>? canonicalAliases,
        string semanticFingerprint,
        CancellationToken cancellationToken)
    {
        if (canonicalAliases is null || canonicalAliases.Count == 0)
        {
            return;
        }

        var aliases = new CanonicalEntityAliasStore(dbContext);
        foreach (var alias in canonicalAliases.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            if (!TrustedSourceLineageRegistry.IsRegisteredScheme(alias.Key))
            {
                continue;
            }

            await aliases.RegisterAsync(
                canonicalEntityId,
                alias.Key,
                alias.Value,
                semanticFingerprint,
                "trusted-lineage-first-import",
                1.0,
                cancellationToken);
        }
    }

    private static string BuildOccurrenceKey(Guid canonicalEntityId, string? locatorKey)
    {
        var locator = string.IsNullOrWhiteSpace(locatorKey)
            ? "primary"
            : CanonicalSourceIdentity.Fingerprint(locatorKey.Trim().ToLowerInvariant())[..20];
        return $"entity:{canonicalEntityId:N}:locator:{locator}";
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

    private sealed record SemanticOccurrenceMatch(
        Guid OccurrenceId,
        Guid? CanonicalEntityId,
        string EntityType,
        string DisplayName);

    private sealed record PriorCanonicalAssociation(
        Guid CanonicalEntityId,
        string SemanticFingerprint);
}
