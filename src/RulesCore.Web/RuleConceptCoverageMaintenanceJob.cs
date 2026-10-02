using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Web;

public sealed record RuleConceptCoverageMaintenanceStatus(
    string State,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Error,
    RuleConceptCoverageRepairResult? Result);

/// <summary>
/// Runs the owner-triggered RuleConcept coverage repair outside the browser request lifetime.
/// The repair itself is protected by a PostgreSQL advisory transaction lock, so duplicate starts
/// across Rules Core processes serialize safely.
/// </summary>
public sealed class RuleConceptCoverageMaintenanceJob(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime applicationLifetime,
    ILogger<RuleConceptCoverageMaintenanceJob> logger)
{
    private readonly object gate = new();
    private Task? runningTask;
    private DateTimeOffset? startedAt;
    private DateTimeOffset? completedAt;
    private string? error;
    private RuleConceptCoverageRepairResult? result;

    public Task<RuleConceptCoverageMaintenanceStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (runningTask is { IsCompleted: false })
            {
                return Task.FromResult(Snapshot("running"));
            }

            if (error is not null)
            {
                return Task.FromResult(Snapshot("failed"));
            }

            return Task.FromResult(Snapshot(completedAt.HasValue ? "completed" : "not-run"));
        }
    }

    public Task<(bool Started, RuleConceptCoverageMaintenanceStatus Status)> StartAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            throw new ArgumentException("Actor user ID can not be blank.", nameof(actorUserId));
        }
        var actor = actorUserId.Trim();

        lock (gate)
        {
            if (runningTask is { IsCompleted: false })
            {
                return Task.FromResult((false, Snapshot("running")));
            }

            startedAt = DateTimeOffset.UtcNow;
            completedAt = null;
            error = null;
            result = null;
            runningTask = Task.Run(
                () => RunAsync(actor, applicationLifetime.ApplicationStopping),
                CancellationToken.None);
            return Task.FromResult((true, Snapshot("running")));
        }
    }

    private async Task RunAsync(string actorUserId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var repair = new RuleConceptCoverageRepairService(dbContext);
            var repairResult = await repair.RepairAsync(actorUserId, cancellationToken);

            lock (gate)
            {
                result = repairResult;
                completedAt = DateTimeOffset.UtcNow;
                error = null;
            }
        }
        catch (OperationCanceledException) when (applicationLifetime.ApplicationStopping.IsCancellationRequested)
        {
            lock (gate)
            {
                error = "Rules Core stopped before RuleConcept coverage repair completed.";
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Rules Core RuleConcept coverage repair failed.");
            lock (gate)
            {
                error = exception.Message;
            }
        }
    }

    private RuleConceptCoverageMaintenanceStatus Snapshot(string state) =>
        new(state, startedAt, completedAt, error, result);
}
