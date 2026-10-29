using RulesCore.Content;

namespace RulesCore.Host;

/// <summary>
/// Initializes a fully validated catalog before HTTP traffic is accepted.
/// Content failures preserve process liveness and keep readiness false.
/// </summary>
internal sealed class NativeCatalogInitializer(
    RulesCoreSettings settings,
    NativeCatalogStore store,
    ILogger<NativeCatalogInitializer> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = NativeCatalogLoader.Load(settings.Ruleset);
            store.Publish(snapshot);
            logger.LogInformation(
                "Native rules catalog ready: ruleset {RulesetId}, records {Count}, fingerprint {Fingerprint}",
                snapshot.RulesetId, snapshot.Count, snapshot.Fingerprint);
        }
        catch (NativeCatalogLoadException ex)
        {
            store.MarkUnavailable(ex.ReasonCode);
            logger.LogWarning(
                "Native rules catalog unavailable: {ReasonCode}", ex.ReasonCode);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
