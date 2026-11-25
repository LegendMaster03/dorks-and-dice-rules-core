using RulesCore.Content;

namespace RulesCore.Host;

/// <summary>
/// Initializes a fully validated catalog before HTTP traffic is accepted.
/// Content failures preserve process liveness and keep readiness false.
/// </summary>
internal sealed class NativeCatalogInitializer(
    RulesCoreSettings settings,
    NativeCatalogRegistry registry,
    ILogger<NativeCatalogInitializer> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var location in settings.AdditionalRulesets.Prepend(settings.Ruleset))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Load(location);
        }
        return Task.CompletedTask;
    }

    private void Load(NativeRulesetLocation location)
    {
        var store = registry.GetStore(location.RulesetId);
        try
        {
            var snapshot = NativeCatalogLoader.Load(location);
            store.Publish(snapshot);
            logger.LogInformation(
                "Native rules catalog ready: ruleset {RulesetId}, records {Count}, fingerprint {Fingerprint}",
                snapshot.RulesetId, snapshot.Count, snapshot.Fingerprint);
        }
        catch (NativeCatalogLoadException ex)
        {
            store.MarkUnavailable(ex.ReasonCode);
            logger.LogWarning(
                "Native rules catalog unavailable: ruleset {RulesetId}, reason {ReasonCode}",
                location.RulesetId, ex.ReasonCode);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
