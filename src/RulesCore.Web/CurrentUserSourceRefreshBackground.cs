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
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            var nextRefreshSweep = DateTimeOffset.UtcNow;

            while (!stoppingToken.IsCancellationRequested)
            {
                var processedImportJob = await ProcessNextImportJobAsync(stoppingToken);
                if (processedImportJob)
                {
                    continue;
                }

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

    private async Task<bool> ProcessNextImportJobAsync(CancellationToken stoppingToken)
    {
        ClaimedCurrentUserSourceImportJob? job = null;
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
            var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
            var jobs = new CurrentUserSourceImportJobService(dbContext);

            job = await jobs.ClaimNextAsync(stoppingToken);
            if (job is null)
            {
                return false;
            }

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

                var refresh = new CurrentUserWebSourceRefreshService(
                    dbContext,
                    importer,
                    grants);
                source = await refresh.RefreshOneAsync(
                    job.UserId,
                    job.CurrentUserSourceId.Value,
                    stoppingToken);
                if (source is null)
                {
                    throw new KeyNotFoundException(
                        "The Web source was removed before its queued refresh could run.");
                }
            }
            else
            {
                var sourceService = new CurrentUserSourceService(
                    dbContext,
                    importer,
                    grants);
                source = await sourceService.AddAsync(
                    job.UserId,
                    new AddCurrentUserSourceRequest(
                        CurrentUserSourceKinds.Web,
                        Url: job.Url),
                    stoppingToken);

                var refresh = new CurrentUserWebSourceRefreshService(
                    dbContext,
                    importer,
                    grants);
                await refresh.RecordInitialVersionAsync(
                    source.Id,
                    job.Url,
                    stoppingToken);
            }

            await jobs.CompleteAsync(job.Id, source.Id, stoppingToken);
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            if (job is not null)
            {
                await RecordInterruptedJobAsync(job.Id);
            }
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

    private async Task RunRefreshSweepAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
            var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
            var refresh = new CurrentUserWebSourceRefreshService(
                dbContext,
                importer,
                grants);
            await refresh.RefreshDueAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Automatic Rules Core Web source refresh failed.");
        }
    }

    private async Task RecordInterruptedJobAsync(Guid jobId)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var jobs = new CurrentUserSourceImportJobService(dbContext);
            await jobs.FailAsync(
                jobId,
                new InvalidOperationException(
                    "The Web source import was interrupted by service shutdown. Queue it again after Rules Core restarts."),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Rules Core could not record an interrupted Web source import job {JobId}.",
                jobId);
        }
    }
}
