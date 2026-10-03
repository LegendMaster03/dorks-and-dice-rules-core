using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Web;

internal sealed class CurrentUserSourceRefreshBackground(
    IServiceScopeFactory scopeFactory,
    ILogger<CurrentUserSourceRefreshBackground> logger)
    : BackgroundService
{
    private const long SourceMaintenanceAdvisoryLockKey = 0x444E4452434D4149L;
    private const string TransientNormalizationFailureMarker = "transient failure";
    private const int MaximumTransientNormalizationRetries = 3;
    private static readonly TimeSpan CoordinatorElectionInterval = TimeSpan.FromSeconds(2);

    private readonly Dictionary<Guid, int> transientNormalizationRetryCounts = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        MaintenanceOwnershipLease? coordinatorLease = null;
        try
        {
            var nextCoordinatorElection = DateTimeOffset.MinValue;
            var nextRefreshSweep = DateTimeOffset.UtcNow.AddMinutes(10);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (coordinatorLease is not null
                    && coordinatorLease.Connection.State != System.Data.ConnectionState.Open)
                {
                    logger.LogWarning(
                        "Rules Core source maintenance coordinator lease connection was lost; returning to coordinator election.");
                    coordinatorLease.Dispose();
                    coordinatorLease = null;
                    nextCoordinatorElection = DateTimeOffset.MinValue;
                }

                var now = DateTimeOffset.UtcNow;
                if (coordinatorLease is null && now >= nextCoordinatorElection)
                {
                    coordinatorLease = await TryAcquireMaintenanceOwnershipLeaseAsync(stoppingToken);
                    nextCoordinatorElection = now.Add(CoordinatorElectionInterval);
                    if (coordinatorLease is not null)
                    {
                        logger.LogInformation(
                            "Rules Core source maintenance worker acquired the coordinator lease; import recovery, deduplication, and refresh sweeps are enabled on this process.");
                        await RequeuePreviouslyFailedTransientNormalizationBackfillsAsync(stoppingToken);
                        await ConsolidateDuplicateSourcePackagesAsync(stoppingToken);
                        await RequeueInterruptedImportJobsAsync(stoppingToken);
                        nextRefreshSweep = DateTimeOffset.UtcNow.AddMinutes(10);
                    }
                }

                if (coordinatorLease is not null)
                {
                    var processedImportJob = await ProcessNextImportJobAsync(stoppingToken);
                    if (processedImportJob) continue;
                }

                // Normalization is deliberately not coordinator-owned. Each Rules Core process
                // can claim a different revision with FOR UPDATE SKIP LOCKED, so the public and
                // private ingresses contribute to the same backfill without duplicating work.
                var processedNormalization =
                    await ProcessNextNormalizationBackfillAsync(stoppingToken);

                if (coordinatorLease is not null
                    && DateTimeOffset.UtcNow >= nextRefreshSweep)
                {
                    await RunRefreshSweepAsync(stoppingToken);
                    nextRefreshSweep = DateTimeOffset.UtcNow.AddHours(1);
                }

                if (processedNormalization) continue;
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        finally
        {
            if (coordinatorLease is not null)
            {
                await ReleaseMaintenanceOwnershipAsync(coordinatorLease.Connection);
                coordinatorLease.Dispose();
            }
        }
    }

    private async Task RequeuePreviouslyFailedTransientNormalizationBackfillsAsync(
        CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        var requeued = await dbContext.SourceEntityRevisions
            .Where(value =>
                value.NormalizationVersion < SourceNormalizationVersion.Current
                && value.NormalizationAttemptVersion >= SourceNormalizationVersion.Current
                && value.NormalizationError != null
                && EF.Functions.ILike(
                    value.NormalizationError,
                    $"%{TransientNormalizationFailureMarker}%"))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        value => value.NormalizationAttemptVersion,
                        SourceNormalizationVersion.Current - 1)
                    .SetProperty(value => value.NormalizationError, (string?)null),
                stoppingToken);

        if (requeued > 0)
        {
            logger.LogWarning(
                "Rules Core requeued {RevisionCount} source-normalization revision(s) that previously failed with a transient database error.",
                requeued);
        }
    }

    private async Task ConsolidateDuplicateSourcePackagesAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        var result = await new SourcePackageDeduplicationService(dbContext)
            .ConsolidateAsync(stoppingToken);
        if (result.ConsolidatedPackageCount > 0
            || result.RenamedSharedPackageCount > 0
            || result.RetainedReferencedDuplicateCount > 0)
        {
            logger.LogInformation(
                "Rules Core source deduplication scanned {CandidateCount} private package(s), consolidated {ConsolidatedCount}, normalized {RenamedCount} shared package key(s), and retained {RetainedCount} referenced duplicate(s).",
                result.CandidatePackageCount,
                result.ConsolidatedPackageCount,
                result.RenamedSharedPackageCount,
                result.RetainedReferencedDuplicateCount);
        }
    }

    private async Task RequeueInterruptedImportJobsAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        var jobs = new CurrentUserSourceImportJobService(dbContext);
        var requeued = await jobs.RequeueInterruptedRunningJobsAsync(stoppingToken);
        if (requeued > 0)
        {
            logger.LogWarning(
                "Rules Core requeued {JobCount} Web source import job(s) left running by a previous process.",
                requeued);
        }
    }

    private async Task<bool> ProcessNextImportJobAsync(CancellationToken stoppingToken)
    {
        ClaimedCurrentUserSourceImportJob? job = null;
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var legacyImporter = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
            var normalizedImporter = scope.ServiceProvider.GetRequiredService<INormalizedSourceImportService>();
            var adapters = scope.ServiceProvider.GetRequiredService<ISourceFormatAdapterRegistry>();
            var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
            var jobs = new CurrentUserSourceImportJobService(dbContext);

            job = await jobs.ClaimNextAsync(stoppingToken);
            if (job is null) return false;

            CurrentUserSourceImportProgress? retainedProgress = null;
            async Task ReportProgressAsync(
                CurrentUserSourceImportProgress progress,
                CancellationToken progressCancellationToken)
            {
                retainedProgress = MergeImportProgress(retainedProgress, progress);
                await ReportImportProgressAsync(
                    job.Id,
                    retainedProgress,
                    progressCancellationToken);
            }

            var sourceUri = new Uri(job.Url, UriKind.Absolute);
            using var httpClient = new HttpClient(new CurrentUserSourceProgressHttpHandler(
                sourceUri,
                ReportProgressAsync))
            {
                Timeout = TimeSpan.FromMinutes(2)
            };
            var sourceService = new CurrentUserSourceService(
                dbContext,
                normalizedImporter,
                adapters,
                grants,
                httpClient,
                ReportProgressAsync);

            CurrentUserSourceView? source;
            if (string.Equals(
                    job.Operation,
                    CurrentUserSourceImportJobOperations.Refresh,
                    StringComparison.Ordinal))
            {
                if (job.CurrentUserSourceId is null)
                {
                    throw new InvalidOperationException(
                        "Queued Web source refresh did not identify the source to refresh.");
                }

                await ReportProgressAsync(
                    new CurrentUserSourceImportProgress(
                        "checking",
                        Detail: "Checking the current source registration before refresh"),
                    stoppingToken);
                source = await sourceService.RefreshAsync(
                    job.UserId,
                    job.CurrentUserSourceId.Value,
                    stoppingToken);
                if (source is null)
                {
                    throw new KeyNotFoundException(
                        "The Web source was removed before its queued refresh could run.");
                }

                await ReportProgressAsync(
                    new CurrentUserSourceImportProgress(
                        "finalizing",
                        source.EntityCount,
                        source.EntityCount,
                        "Recording refreshed source registration and upstream version",
                        EntitiesPersisted: source.EntityCount),
                    stoppingToken);
                var refreshMetadata = new CurrentUserWebSourceRefreshService(
                    dbContext,
                    legacyImporter,
                    grants);
                await refreshMetadata.RecordInitialVersionAsync(
                    source.Id,
                    job.Url,
                    stoppingToken);
            }
            else
            {
                await ReportProgressAsync(
                    new CurrentUserSourceImportProgress(
                        "preparing",
                        Detail: "Preparing import; previously completed source files will be reused"),
                    stoppingToken);

                // Add imports are intentionally resumable. Each normalized representation
                // commits atomically, so a shutdown can leave a valid prefix of a large
                // source tree in the private package. Do not delete that work before retry.
                // AddAsync uses the same deterministic package/origin identities and the
                // normalized importer reuses committed representations/entities/revisions.
                source = await sourceService.AddAsync(
                    job.UserId,
                    new AddCurrentUserSourceRequest(
                        CurrentUserSourceKinds.Web,
                        Url: job.Url),
                    stoppingToken);

                await ReportProgressAsync(
                    new CurrentUserSourceImportProgress(
                        "finalizing",
                        source.EntityCount,
                        source.EntityCount,
                        "Recording source registration, access grant, and upstream version",
                        EntitiesPersisted: source.EntityCount),
                    stoppingToken);
                var refreshMetadata = new CurrentUserWebSourceRefreshService(
                    dbContext,
                    legacyImporter,
                    grants);
                await refreshMetadata.RecordInitialVersionAsync(
                    source.Id,
                    job.Url,
                    stoppingToken);
            }

            var deduplication = await new SourcePackageDeduplicationService(dbContext)
                .ConsolidateAsync(stoppingToken);
            if (deduplication.ConsolidatedPackageCount > 0
                || deduplication.RenamedSharedPackageCount > 0
                || deduplication.RetainedReferencedDuplicateCount > 0)
            {
                logger.LogInformation(
                    "Rules Core post-import deduplication scanned {CandidateCount} private package(s), consolidated {ConsolidatedCount}, normalized {RenamedCount} shared package key(s), and retained {RetainedCount} referenced duplicate(s).",
                    deduplication.CandidatePackageCount,
                    deduplication.ConsolidatedPackageCount,
                    deduplication.RenamedSharedPackageCount,
                    deduplication.RetainedReferencedDuplicateCount);
            }

            await jobs.CompleteAsync(job.Id, source.Id, stoppingToken);
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            if (job is not null) await RecordInterruptedJobAsync(job.Id);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Rules Core Web source import job {JobId} failed.",
                job?.Id);
            if (job is not null)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                    var jobs = new CurrentUserSourceImportJobService(dbContext);
                    await jobs.FailAsync(job.Id, exception, stoppingToken);

                    // Retain committed representations from failed Add jobs. A later retry
                    // can resume from those immutable units instead of downloading/persisting
                    // the complete source tree again. Explicit maintenance can still remove
                    // an abandoned incomplete package when desired.
                }
                catch (Exception recordException)
                {
                    logger.LogError(
                        recordException,
                        "Rules Core could not record failure for Web source import job {JobId}.",
                        job.Id);
                }
            }
            return true;
        }
    }

    private async Task<bool> ProcessNextNormalizationBackfillAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var maintenance = scope.ServiceProvider
                .GetRequiredService<ISourceNormalizationMaintenanceService>();
            var result = await maintenance.ReconcileAsync(
                limit: 1,
                retryFailed: false,
                packageKey: null,
                cancellationToken: stoppingToken);
            if (result.AttemptedRevisionCount == 0)
            {
                return false;
            }

            if (result.FailedRevisionCount > 0)
            {
                var failure = result.Failures[0];
                logger.LogWarning(
                    "Rules Core could not backfill source revision {RevisionId} ({EntityName}): {Message}",
                    failure.SourceEntityRevisionId,
                    failure.EntityName,
                    failure.Message);

                if (IsTransientNormalizationFailure(failure.Message))
                {
                    var retryCount = transientNormalizationRetryCounts.GetValueOrDefault(
                        failure.SourceEntityRevisionId) + 1;
                    transientNormalizationRetryCounts[failure.SourceEntityRevisionId] = retryCount;

                    if (retryCount <= MaximumTransientNormalizationRetries)
                    {
                        await RequeueNormalizationRevisionAsync(
                            failure.SourceEntityRevisionId,
                            stoppingToken);
                        logger.LogWarning(
                            "Rules Core requeued transient source-normalization failure {RevisionId} for retry {RetryCount}/{MaximumRetryCount}.",
                            failure.SourceEntityRevisionId,
                            retryCount,
                            MaximumTransientNormalizationRetries);
                        return false;
                    }

                    logger.LogError(
                        "Rules Core exhausted transient source-normalization retries for revision {RevisionId}.",
                        failure.SourceEntityRevisionId);
                }
            }
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Automatic Rules Core source-normalization backfill failed.");
            return false;
        }
    }

    private async Task RequeueNormalizationRevisionAsync(
        Guid revisionId,
        CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        await dbContext.SourceEntityRevisions
            .Where(value =>
                value.Id == revisionId
                && value.NormalizationVersion < SourceNormalizationVersion.Current)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        value => value.NormalizationAttemptVersion,
                        SourceNormalizationVersion.Current - 1)
                    .SetProperty(value => value.NormalizationError, (string?)null),
                stoppingToken);
    }

    private static bool IsTransientNormalizationFailure(string message) =>
        message.Contains(
            TransientNormalizationFailureMarker,
            StringComparison.OrdinalIgnoreCase);

    private static CurrentUserSourceImportProgress MergeImportProgress(
        CurrentUserSourceImportProgress? previous,
        CurrentUserSourceImportProgress current)
    {
        if (previous is null) return current;
        return current with
        {
            FilesDiscovered = current.FilesDiscovered ?? previous.FilesDiscovered,
            FilesProcessed = current.FilesProcessed ?? previous.FilesProcessed,
            CompatibleFiles = current.CompatibleFiles ?? previous.CompatibleFiles,
            ImportUnitsProcessed = current.ImportUnitsProcessed ?? previous.ImportUnitsProcessed,
            ImportUnitTotal = current.ImportUnitTotal ?? previous.ImportUnitTotal,
            RecordsDiscovered = current.RecordsDiscovered ?? previous.RecordsDiscovered,
            RecordsTranslated = current.RecordsTranslated ?? previous.RecordsTranslated,
            EntitiesPersisted = current.EntitiesPersisted ?? previous.EntitiesPersisted,
            NewEntities = current.NewEntities ?? previous.NewEntities,
            UnchangedEntities = current.UnchangedEntities ?? previous.UnchangedEntities,
            NewRevisions = current.NewRevisions ?? previous.NewRevisions,
            TranslationOnlyUpdates = current.TranslationOnlyUpdates ?? previous.TranslationOnlyUpdates,
            PublicationsProcessed = current.PublicationsProcessed ?? previous.PublicationsProcessed,
            PublicationTotal = current.PublicationTotal ?? previous.PublicationTotal,
            ReconciliationIssueCount = current.ReconciliationIssueCount
                ?? previous.ReconciliationIssueCount,
            RepresentationsStored = current.RepresentationsStored ?? previous.RepresentationsStored,
            RepresentationsReused = current.RepresentationsReused ?? previous.RepresentationsReused
        };
    }

    private async Task ReportImportProgressAsync(
        Guid jobId,
        CurrentUserSourceImportProgress progress,
        CancellationToken cancellationToken)
    {
        // Normalized imports hold a long-running transaction on their scoped DbContext.
        // Progress must commit independently so the UI can observe persistence and
        // reconciliation while that import transaction is still in flight.
        await using var progressScope = scopeFactory.CreateAsyncScope();
        var progressDbContext = progressScope.ServiceProvider
            .GetRequiredService<RulesCoreDbContext>();
        var progressJobs = new CurrentUserSourceImportJobService(progressDbContext);
        await progressJobs.UpdateProgressAsync(jobId, progress, cancellationToken);
    }

    private async Task RunRefreshSweepAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
            var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
            var refresh = new CurrentUserWebSourceRefreshService(dbContext, importer, grants);
            await refresh.RefreshDueAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Automatic Rules Core Web source refresh failed.");
        }
    }

    private async Task RecordInterruptedJobAsync(Guid jobId)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var jobs = new CurrentUserSourceImportJobService(dbContext);
            await jobs.RequeueRunningJobAsync(
                jobId,
                "Web source import was interrupted by service shutdown; waiting to retry",
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Rules Core could not requeue interrupted Web source import job {JobId}.",
                jobId);
        }
    }

    private async Task<MaintenanceOwnershipLease?> TryAcquireMaintenanceOwnershipLeaseAsync(
        CancellationToken cancellationToken)
    {
        var scope = scopeFactory.CreateScope();
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var connection = dbContext.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);
            if (!await TryAcquireMaintenanceOwnershipAsync(connection, cancellationToken))
            {
                scope.Dispose();
                return null;
            }

            return new MaintenanceOwnershipLease(scope, connection);
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    private static async Task<bool> TryAcquireMaintenanceOwnershipAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@lock_key);";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@lock_key";
        parameter.Value = SourceMaintenanceAdvisoryLockKey;
        command.Parameters.Add(parameter);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private async Task ReleaseMaintenanceOwnershipAsync(DbConnection connection)
    {
        try
        {
            if (connection.State != System.Data.ConnectionState.Open)
            {
                return;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_advisory_unlock(@lock_key);";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@lock_key";
            parameter.Value = SourceMaintenanceAdvisoryLockKey;
            command.Parameters.Add(parameter);
            await command.ExecuteScalarAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Rules Core could not explicitly release the source maintenance coordinator lease; PostgreSQL will release it when the connection closes.");
        }
    }

    private sealed class MaintenanceOwnershipLease(
        IServiceScope scope,
        DbConnection connection) : IDisposable
    {
        public DbConnection Connection { get; } = connection;

        public void Dispose() => scope.Dispose();
    }
}
