using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Replays the current Rules Core interpretation over immutable source-native revisions.
///
/// RawJson, native fingerprints, revision numbers, and representation bytes are never rewritten.
/// Only derived ContentJson and canonical semantic association may change.
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
        var query = Scope(
                dbContext.SourceEntityRevisions.AsNoTracking(),
                packageKey)
            .Where(value =>
                value.NormalizationVersion < SourceNormalizationVersion.Current);
        if (!retryFailed)
        {
            query = query.Where(value =>
                value.NormalizationAttemptVersion < SourceNormalizationVersion.Current);
        }

        var revisionIds = await query
            .OrderBy(value => value.ImportedAt)
            .ThenBy(value => value.Id)
            .Select(value => value.Id)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        var attempted = 0;
        var updated = 0;
        var unchanged = 0;
        var reassociated = 0;
        var failures = new List<SourceNormalizationFailureView>();
        foreach (var revisionId in revisionIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = await ReconcileRevisionAsync(
                revisionId,
                retryFailed,
                cancellationToken);
            dbContext.ChangeTracker.Clear();

            if (!outcome.Attempted)
            {
                continue;
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

    private async Task<RevisionOutcome> ReconcileRevisionAsync(
        Guid revisionId,
        bool retryFailed,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        try
        {
            var revision = await dbContext.SourceEntityRevisions
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM source_entity_revision
                    WHERE source_entity_revision_id = {revisionId}
                    FOR UPDATE
                    """)
                .SingleAsync(cancellationToken);

            if (revision.NormalizationVersion >= SourceNormalizationVersion.Current
                || (!retryFailed
                    && revision.NormalizationAttemptVersion >= SourceNormalizationVersion.Current))
            {
                await transaction.CommitAsync(cancellationToken);
                return RevisionOutcome.Skipped;
            }

            var entity = await dbContext.SourceEntities
                .SingleAsync(value => value.Id == revision.SourceEntityId, cancellationToken);
            var representation = await dbContext.SourceRepresentations
                .AsNoTracking()
                .SingleAsync(value => value.Id == revision.SourceRepresentationId, cancellationToken);

            var candidate = await BuildCandidateAsync(
                entity,
                revision,
                representation,
                cancellationToken);
            var normalized = NormalizedSourceImportService.TranslateAndNormalizeRecord(
                candidate.Representation,
                candidate.Record);

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
            var failure = await MarkFailureAsync(
                revisionId,
                exception,
                cancellationToken);
            return new RevisionOutcome(
                Attempted: true,
                ContentUpdated: false,
                CanonicalReassociated: false,
                Failure: failure);
        }
    }

    private async Task<TranslationCandidate> BuildCandidateAsync(
        SourceEntity entity,
        SourceEntityRevision revision,
        SourceRepresentation representation,
        CancellationToken cancellationToken)
    {
        var artifact = new SourceRepresentationArtifact(
            representation.FileName,
            representation.ContentBytes.ToArray(),
            representation.OriginIdentity,
            representation.SourceUri,
            representation.MediaType);

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
                return new TranslationCandidate(reparsed, record, WasReconstructed: false);
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
        var record = new NormalizedSourceRecord(
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
                [record],
                publications,
                representation.MetadataJson),
            record,
            WasReconstructed: true);
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
        public static RevisionOutcome Skipped { get; } =
            new(false, false, false, null);
    }
}
