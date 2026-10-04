using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RulesCore.Application.Sources;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Replays the current Rules Core interpretation over immutable source-native revisions.
///
/// RawJson, native fingerprints, revision numbers, and representation bytes are never rewritten.
/// Derived ContentJson, reviewed derived SourceEntity type/name corrections, and canonical semantic
/// association may change.
/// </summary>
public sealed class SourceNormalizationMaintenanceService(
    RulesCoreDbContext dbContext,
    ISourceFormatAdapterRegistry adapters)
    : ISourceNormalizationMaintenanceService
{
    private const int MaximumBatchSize = 500;

    public async Task<SourceNormalizationStatusView> GetStatusAsync(
        string? packageKey = null,
        CancellationToken cancellationToken = default)
    {
        var query = Scope(
            dbContext.SourceEntityRevisions.AsNoTracking(),
            packageKey);
        var revisionCount = await query.CountAsync(cancellationToken);
        var currentCount = await query.CountAsync(
            value => value.NormalizationVersion >= SourceNormalizationVersion.Current,
            cancellationToken);
        var failedCount = await query.CountAsync(
            value => value.NormalizationVersion < SourceNormalizationVersion.Current
                && value.NormalizationAttemptVersion >= SourceNormalizationVersion.Current
                && value.NormalizationError != null,
            cancellationToken);
        var pendingCount = await query.CountAsync(
            value => value.NormalizationVersion < SourceNormalizationVersion.Current
                && value.NormalizationAttemptVersion < SourceNormalizationVersion.Current,
            cancellationToken);

        return new SourceNormalizationStatusView(
            SourceNormalizationVersion.Current,
            revisionCount,
            currentCount,
            pendingCount,
            failedCount);
    }

    public async Task<SourceNormalizationRunView> ReconcileAsync(
        int limit = 25,
        bool retryFailed = false,
        string? packageKey = null,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaximumBatchSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                $"Normalization reconciliation limit must be between 1 and {MaximumBatchSize}.");
        }

        SourceImportExecutionPolicy.Apply(dbContext);

        var attempted = 0;
        var updated = 0;
        var unchanged = 0;
        var reassociated = 0;
        var failures = new List<SourceNormalizationFailureView>();
        for (var index = 0; index < limit; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = await ReconcileNextRevisionAsync(
                retryFailed,
                packageKey,
                cancellationToken);
            dbContext.ChangeTracker.Clear();

            if (!outcome.Attempted)
            {
                break;
            }

            attempted++;
            if (outcome.Failure is not null)
            {
                failures.Add(outcome.Failure);
                continue;
            }

            if (outcome.ContentUpdated)
            {
                updated++;
            }
            else
            {
                unchanged++;
            }
            if (outcome.CanonicalReassociated)
            {
                reassociated++;
            }
        }

        var status = await GetStatusAsync(packageKey, cancellationToken);
        return new SourceNormalizationRunView(
            SourceNormalizationVersion.Current,
            attempted,
            updated,
            unchanged,
            reassociated,
            failures.Count,
            status.PendingRevisionCount,
            failures);
    }

    private async Task<RevisionOutcome> ReconcileNextRevisionAsync(
        bool retryFailed,
        string? packageKey,
        CancellationToken cancellationToken)
    {
        Guid? revisionId = null;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        try
        {
            revisionId = await ClaimNextRevisionIdAsync(
                retryFailed,
                packageKey,
                cancellationToken);
            if (revisionId is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return RevisionOutcome.NoWork;
            }

            var revision = await dbContext.SourceEntityRevisions
                .SingleAsync(value => value.Id == revisionId.Value, cancellationToken);
            var entity = await dbContext.SourceEntities
                .SingleAsync(value => value.Id == revision.SourceEntityId, cancellationToken);
            var representation = await dbContext.SourceRepresentations
                .AsNoTracking()
                .Include(value => value.ContentBlob)
                .SingleAsync(value => value.Id == revision.SourceRepresentationId, cancellationToken);

            var candidate = await BuildCandidateAsync(
                entity,
                revision,
                representation,
                cancellationToken);
            var normalized = NormalizedSourceImportService.TranslateAndNormalizeRecord(
                candidate.Representation,
                candidate.Record);
            ApplyReviewedEpicEntityIdentityMigration(entity, normalized);
            NormalizedSourceImportService.EnsureEntityIdentityMatches(entity, normalized);

            if (!string.Equals(
                    NormalizedSourceImportService.CanonicalJsonFingerprint(normalized.RawJson),
                    revision.Fingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The current source adapter no longer reproduces the immutable native record. "
                    + "This revision requires a local source reparse rather than a translation-only backfill.");
            }

            if (string.IsNullOrWhiteSpace(normalized.ContentJson)
                && !string.IsNullOrWhiteSpace(revision.ContentJson)
                && candidate.WasReconstructed)
            {
                throw new InvalidDataException(
                    "The current translator can not reconstruct the existing mechanical content "
                    + "from the preserved native record without reparsing its source representation.");
            }

            var contentUpdated = !NormalizedSourceImportService.JsonEquivalentOptional(
                revision.ContentJson,
                normalized.ContentJson);
            if (contentUpdated)
            {
                revision.ContentJson = normalized.ContentJson;
            }

            var canonicalReassociated = false;
            var publication = await ReadBoundPublicationAsync(revision.Id, cancellationToken);
            if (publication is not null)
            {
                var semanticFingerprint =
                    NormalizedSourceImportService.SemanticFingerprint(normalized);
                await new CanonicalSourceRepresentationService(dbContext).AssociateSourceEntityAsync(
                    entity.Id,
                    revision.Id,
                    new CanonicalPublicationEvidence(
                        publication.DisplayName,
                        publication.Publisher,
                        publication.GameEdition,
                        publication.PublicationDate,
                        OccurrenceFingerprints: [semanticFingerprint]),
                    new CanonicalSourceOccurrenceEvidence(
                        normalized.EntityType,
                        normalized.Name,
                        normalized.LocatorKey ?? revision.LocatorKey,
                        semanticFingerprint),
                    representation.FormatKey,
                    cancellationToken,
                    normalized.CanonicalAliases,
                    allowTranslationOnlyReassociation: true);
                canonicalReassociated = true;
            }

            revision.NormalizationVersion = SourceNormalizationVersion.Current;
            revision.NormalizationAttemptVersion = SourceNormalizationVersion.Current;
            revision.NormalizationAttemptedAt = DateTimeOffset.UtcNow;
            revision.NormalizationError = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new RevisionOutcome(
                Attempted: true,
                ContentUpdated: contentUpdated,
                CanonicalReassociated: canonicalReassociated,
                Failure: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            if (revisionId is null)
            {
                throw;
            }

            var failure = await MarkFailureAsync(
                revisionId.Value,
                exception,
                cancellationToken);
            return new RevisionOutcome(
                Attempted: true,
                ContentUpdated: false,
                CanonicalReassociated: false,
                Failure: failure);
        }
    }

    private async Task<Guid?> ClaimNextRevisionIdAsync(
        bool retryFailed,
        string? packageKey,
        CancellationToken cancellationToken)
    {
        var currentTransaction = dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Source normalization work must be claimed inside a database transaction.");
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = currentTransaction.GetDbTransaction();
        command.CommandText = """
            SELECT revision.source_entity_revision_id
            FROM source_entity_revision revision
            JOIN source_entity entity
                ON entity.source_entity_id = revision.source_entity_id
            JOIN source_package package
                ON package.source_package_id = entity.source_package_id
            WHERE revision.normalization_version < @current_version
                AND (@retry_failed
                    OR revision.normalization_attempt_version < @current_version)
                AND (@package_key IS NULL OR package.package_key = @package_key)
                AND NOT EXISTS (
                    SELECT 1
                    FROM source_entity_revision earlier
                    WHERE earlier.source_entity_id = revision.source_entity_id
                        AND earlier.normalization_version < @current_version
                        AND (@retry_failed
                            OR earlier.normalization_attempt_version < @current_version)
                        AND (
                            earlier.imported_at < revision.imported_at
                            OR (
                                earlier.imported_at = revision.imported_at
                                AND earlier.source_entity_revision_id
                                    < revision.source_entity_revision_id)))
            ORDER BY revision.imported_at, revision.source_entity_revision_id
            LIMIT 1
            FOR UPDATE OF revision, entity SKIP LOCKED;
            """;
        AddParameter(command, "@current_version", SourceNormalizationVersion.Current);
        AddParameter(command, "@retry_failed", retryFailed);
        var packageParameter = command.CreateParameter();
        packageParameter.ParameterName = "@package_key";
        packageParameter.DbType = DbType.String;
        packageParameter.Value = string.IsNullOrWhiteSpace(packageKey)
            ? DBNull.Value
            : packageKey.Trim();
        command.Parameters.Add(packageParameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid id ? id : null;
    }

    private async Task<TranslationCandidate> BuildCandidateAsync(
        SourceEntity entity,
        SourceEntityRevision revision,
        SourceRepresentation representation,
        CancellationToken cancellationToken)
    {
        var artifact = new SourceRepresentationArtifact(
            representation.FileName,
            representation.ContentBlob.ContentBytes.ToArray(),
            representation.OriginIdentity,
            representation.SourceUri,
            representation.MediaType);
        var normalizationEvidence = await ReadNormalizationCompanionEvidenceAsync(
            entity,
            cancellationToken);

        var reparsed = string.Equals(
                representation.FormatKey,
                LegacySrdSourceFormatAdapter.Format,
                StringComparison.Ordinal)
            ? new LegacySrdSourceFormatAdapter().TryRead(artifact)
            : adapters.TryRead(artifact);
        if (reparsed is not null)
        {
            var record = reparsed.Records.SingleOrDefault(value =>
                string.Equals(value.NativeKey, entity.NativeKey, StringComparison.Ordinal));
            if (record is not null
                && string.Equals(
                    NormalizedSourceImportService.CanonicalJsonFingerprint(record.RawJson),
                    revision.Fingerprint,
                    StringComparison.Ordinal))
            {
                return new TranslationCandidate(
                    reparsed with { NormalizationCompanionEvidence = normalizationEvidence },
                    record,
                    WasReconstructed: false);
            }
        }

        var publication = await ReadBoundPublicationAsync(revision.Id, cancellationToken);
        var publicationKey = publication is null ? null : "stored-publication";
        IReadOnlyList<NormalizedSourcePublication>? publications = publication is null
            ? null
            : [
                new NormalizedSourcePublication(
                    publicationKey!,
                    publication.DisplayName,
                    publication.Publisher,
                    publication.GameEdition,
                    publication.PublicationDate)
            ];

        var recordName = ReadRawString(revision.RawJson, "name") ?? entity.Name;
        var nativeEntityType = ReadRawString(revision.RawJson, "entityType")
            ?? entity.EntityType;
        var reconstructedRecord = new NormalizedSourceRecord(
            nativeEntityType,
            recordName,
            entity.SourceCode,
            entity.NativeKey,
            revision.RawJson,
            revision.LocatorKey,
            publicationKey,
            entity.NativeIdentityJson);

        return new TranslationCandidate(
            new NormalizedSourceRepresentation(
                representation.FormatKey,
                artifact,
                [reconstructedRecord],
                publications,
                representation.MetadataJson)
            {
                NormalizationCompanionEvidence = normalizationEvidence
            },
            reconstructedRecord,
            WasReconstructed: true);
    }

    private async Task<IReadOnlyList<NormalizedSourceCompanionContent>> ReadNormalizationCompanionEvidenceAsync(
        SourceEntity entity,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                entity.FormatKey,
                FiveEToolsSourceFormatAdapter.Format,
                StringComparison.Ordinal)
            || !string.Equals(entity.EntityType, "spell", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return await new SourceNormalizationCompanionEvidenceReader(dbContext)
            .ReadForSourceEntityAsync(
                entity.Id,
                FiveEToolsCompanionSourceFormatAdapter.SpellSourceLookupCompanionKind,
                cancellationToken);
    }

    private async Task<StoredPublication?> ReadBoundPublicationAsync(
        Guid revisionId,
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
                    publication.display_name,
                    publication.publisher,
                    publication.game_edition,
                    publication.publication_date
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id =
                        binding.canonical_source_occurrence_id
                JOIN canonical_publication publication
                    ON publication.canonical_publication_id =
                        occurrence.canonical_publication_id
                WHERE binding.source_entity_revision_id = @revision_id
                LIMIT 1;
                """;
            AddParameter(command, "@revision_id", revisionId);
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

    private async Task<SourceNormalizationFailureView> MarkFailureAsync(
        Guid revisionId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.SourceEntityRevisions
            .Include(value => value.SourceEntity)
            .SingleAsync(value => value.Id == revisionId, cancellationToken);
        if (row.NormalizationVersion < SourceNormalizationVersion.Current)
        {
            row.NormalizationAttemptVersion = SourceNormalizationVersion.Current;
            row.NormalizationAttemptedAt = DateTimeOffset.UtcNow;
            row.NormalizationError = Truncate(
                exception.Message,
                1000);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new SourceNormalizationFailureView(
            row.Id,
            row.SourceEntity.Name,
            row.NormalizationError ?? exception.Message);
    }

    private static void ApplyReviewedEpicEntityIdentityMigration(
        SourceEntity entity,
        NormalizedSourceRecord normalized)
    {
        var typeMatches = string.Equals(
            entity.EntityType,
            normalized.EntityType,
            StringComparison.Ordinal);
        var nameMatches = string.Equals(
            entity.Name,
            normalized.Name,
            StringComparison.Ordinal);
        if (typeMatches && nameMatches)
        {
            return;
        }

        var sourceCodeMatches = string.Equals(
            entity.SourceCode,
            normalized.SourceCode,
            StringComparison.Ordinal);
        if (!sourceCodeMatches
            || !EpicContentNormalizationPolicy.IsReviewedIdentityMigration(
                entity.EntityType,
                entity.Name,
                normalized))
        {
            return;
        }

        entity.EntityType = normalized.EntityType;
        entity.Name = normalized.Name;
    }

    private static IQueryable<SourceEntityRevision> Scope(
        IQueryable<SourceEntityRevision> query,
        string? packageKey)
    {
        if (string.IsNullOrWhiteSpace(packageKey))
        {
            return query;
        }

        var normalized = packageKey.Trim();
        return query.Where(value =>
            value.SourceEntity.SourcePackage.Key == normalized);
    }

    private static string? ReadRawString(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()!.Trim()
                : null;
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    private static void AddParameter(
        DbCommand command,
        string name,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record TranslationCandidate(
        NormalizedSourceRepresentation Representation,
        NormalizedSourceRecord Record,
        bool WasReconstructed);

    private sealed record StoredPublication(
        string DisplayName,
        string? Publisher,
        string? GameEdition,
        DateOnly? PublicationDate);

    private sealed record RevisionOutcome(
        bool Attempted,
        bool ContentUpdated,
        bool CanonicalReassociated,
        SourceNormalizationFailureView? Failure)
    {
        public static RevisionOutcome NoWork { get; } =
            new(false, false, false, null);
    }
}
