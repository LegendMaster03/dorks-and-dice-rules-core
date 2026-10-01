using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Web;

/// <summary>
/// One-shot startup reconciliation for data-model corrections that must also apply to an existing
/// corpus. Each operation is idempotent so repeated process starts do not manufacture new history.
/// </summary>
public sealed class CanonicalDataReconciliationStartupService(IServiceScopeFactory scopeFactory)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        await new CanonicalDataReconciliationService(dbContext)
            .ReconcileExistingCorpusAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
