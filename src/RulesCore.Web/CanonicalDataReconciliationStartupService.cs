using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Web;

/// <summary>
/// Runs the versioned canonical-data backfill once per database. Database serialization inside the
/// reconciliation service prevents the public and private Rules Core ingress processes from doing
/// the same full-corpus work concurrently during deployment.
/// </summary>
public sealed class CanonicalDataReconciliationStartupService(IServiceScopeFactory scopeFactory)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        await new CanonicalDataReconciliationService(dbContext)
            .RunStartupBackfillAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
