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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RequeueInterruptedImportJobsAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            var nextRefreshSweep = DateTimeOffset.UtcNow.AddMinutes(10);

            while (!stoppingToken.IsCancellationRequested)
            {
                var processedImportJob = await ProcessNextImportJobAsync(stoppingToken);
                if (processedImportJob) continue;

                if (DateTimeOffset.UtcNow >= nextRefreshSweep)
                {
                    await RunRefreshSweepAsync(stoppingToken);
                    nextRefreshSweep = DateTimeOffset.UtcNow.AddHours(1);
                }

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
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

            var sourceUri = new Uri(job.Url, UriKind.Absolute);
            using var httpClient = new HttpClient(new CurrentUserSourceProgressHttpHandler(
                sourceUri,
                (progress, cancellationToken) =>
                    ReportImportProgressAsync(job.Id, progress, cancellationToken)))
            {
                Timeout = TimeSpan.FromMinutes(2)
            };
            var progressImporter = new ProgressReportingNormalizedSourceImportService(
                normalizedImporter,
                (progress, cancellationToken) =>
                    ReportImportProgressAsync(job.Id, progress, cancellationToken));
            var sourceService = new CurrentUserSourceService(
                dbContext,
                progressImporter,
                adapters,
                grants,
                httpClient);

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

                await jobs.UpdateProgressAsync(
                    job.Id,
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

                await jobs.UpdateProgressAsync(
                    job.Id,
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
                await jobs.UpdateProgressAsync(
                    job.Id,
                    new CurrentUserSourceImportProgress(
                        "preparing",
                        Detail: "Preparing a clean import attempt"),
                    stoppingToken);
                var cleanup = new IncompleteCurrentUserSourceImportCleanupService(dbContext);
                var resetPartial = await cleanup.CleanupWebAddAsync(
                    job.UserId,
                    job.Url,
                    stoppingToken);
                if (resetPartial)
                {
                    await jobs.UpdateProgressAsync(
                        job.Id,
                        new CurrentUserSourceImportProgress(
                            "preparing",
                            Detail: "Removed incomplete data from the previous failed attempt"),
                        stoppingToken);
                }

                source = await sourceService.AddAsync(
                    job.UserId,
                    new AddCurrentUserSourceRequest(
                        CurrentUserSourceKinds.Web,
                        Url: job.Url),
                    stoppingToken);

                await jobs.UpdateProgressAsync(
                    job.Id,
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

                    if (string.Equals(
                            job.Operation,
                            CurrentUserSourceImportJobOperations.Add,
                            StringComparison.Ordinal))
                    {
                        try
                        {
                            var cleanup = new IncompleteCurrentUserSourceImportCleanupService(dbContext);
                            await cleanup.CleanupWebAddAsync(job.UserId, job.Url, stoppingToken);
                        }
                        catch (Exception cleanupException)
                        {
                            logger.LogWarning(
                                cleanupException,
                                "Rules Core could not clean incomplete Web source data for failed job {JobId}; the next retry will attempt cleanup again.",
                                job.Id);
                        }
                    }
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
}
