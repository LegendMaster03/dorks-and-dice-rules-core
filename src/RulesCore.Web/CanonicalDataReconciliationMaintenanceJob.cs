using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Web;

public sealed record CanonicalDataReconciliationMaintenanceStatus(
    string State,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Error);

/// <summary>
/// Runs the one-time historical canonical-data reconciliation outside the request lifetime.
/// The underlying reconciliation service retains the database advisory lock and durable completion
/// marker, so an accidental duplicate start from another Rules Core ingress process is serialized.
/// </summary>
public sealed class CanonicalDataReconciliationMaintenanceJob(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime applicationLifetime,
    ILogger<CanonicalDataReconciliationMaintenanceJob> logger)
{
    private readonly object gate = new();
    private Task? runningTask;
    private DateTimeOffset? startedAt;
    private DateTimeOffset? completedAt;
    private string? error;

    public async Task<CanonicalDataReconciliationMaintenanceStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var durableCompletedAt = await GetDurableCompletedAtAsync(cancellationToken);
        lock (gate)
        {
            if (durableCompletedAt.HasValue)
            {
                completedAt = durableCompletedAt;
                error = null;
                return Snapshot("completed");
            }

            if (runningTask is { IsCompleted: false })
            {
                return Snapshot("running");
            }

            return Snapshot(error is null ? "not-run" : "failed");
        }
    }

    public async Task<(bool Started, CanonicalDataReconciliationMaintenanceStatus Status)> StartAsync(
        CancellationToken cancellationToken = default)
    {
        var durableCompletedAt = await GetDurableCompletedAtAsync(cancellationToken);
        lock (gate)
        {
            if (durableCompletedAt.HasValue)
            {
                completedAt = durableCompletedAt;
                error = null;
                return (false, Snapshot("completed"));
            }

            if (runningTask is { IsCompleted: false })
            {
                return (false, Snapshot("running"));
            }

            startedAt = DateTimeOffset.UtcNow;
            completedAt = null;
            error = null;
            runningTask = Task.Run(
                () => RunAsync(applicationLifetime.ApplicationStopping),
                CancellationToken.None);
            return (true, Snapshot("running"));
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var reconciliation = new CanonicalDataReconciliationService(dbContext);
            await reconciliation.RunStartupBackfillAsync(cancellationToken);
            var durableCompletedAt = await reconciliation.GetStartupBackfillCompletedAtAsync(cancellationToken);

            lock (gate)
            {
                completedAt = durableCompletedAt ?? DateTimeOffset.UtcNow;
                error = null;
            }
        }
        catch (OperationCanceledException) when (applicationLifetime.ApplicationStopping.IsCancellationRequested)
        {
            lock (gate)
            {
                error = "Rules Core stopped before corpus reconciliation completed.";
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Historical Rules Core corpus reconciliation failed.");
            lock (gate)
            {
                error = exception.Message;
            }
        }
    }

    private async Task<DateTimeOffset?> GetDurableCompletedAtAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        return await new CanonicalDataReconciliationService(dbContext)
            .GetStartupBackfillCompletedAtAsync(cancellationToken);
    }

    private CanonicalDataReconciliationMaintenanceStatus Snapshot(string state) =>
        new(state, startedAt, completedAt, error);
}
