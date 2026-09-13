using Microsoft.Extensions.Hosting;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Web;

internal sealed class CurrentUserSourceImportBackground(
    IServiceScopeFactory scopeFactory,
    ILogger<CurrentUserSourceImportBackground> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Keep application startup and integration-test startup deterministic. Web-source
            // imports are durable jobs, so they do not need to begin inside the request that queues them.
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                ClaimedCurrentUserSourceImportJob? job = null;
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                    var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                    var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
                    var jobs = scope.ServiceProvider.GetRequiredService<CurrentUserSourceImportJobService>();

                    job = await jobs.ClaimNextAsync(stoppingToken);
                    if (job is null)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                        continue;
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
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    if (job is not null)
                    {
                        try
                        {
                            await using var scope = scopeFactory.CreateAsyncScope();
                            var jobs = scope.ServiceProvider.GetRequiredService<CurrentUserSourceImportJobService>();
                            await jobs.FailAsync(
                                job.Id,
                                new InvalidOperationException(
                                    "The Web source import was interrupted by service shutdown. Queue it again after Rules Core restarts."),
                                CancellationToken.None);
                        }
                        catch (Exception exception)
                        {
                            logger.LogError(
                                exception,
                                "Rules Core could not record an interrupted Web source import job.");
                        }
                    }
                    break;
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
                            var jobs = scope.ServiceProvider.GetRequiredService<CurrentUserSourceImportJobService>();
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
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
    }
}
