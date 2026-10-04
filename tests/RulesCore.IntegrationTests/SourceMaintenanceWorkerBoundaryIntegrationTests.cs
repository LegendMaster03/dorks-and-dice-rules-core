using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RulesCore.Application.Sources;
using RulesCore.Web;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class SourceMaintenanceWorkerBoundaryIntegrationTests
{
    [Fact]
    public async Task PrivateOnlyIngressKeepsSourceMaintenanceWorkerActive()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore")))
        {
            return;
        }

        await using var factory = Factory(RulesCoreApiSurfaceMode.PrivateOnly);
        using var client = factory.CreateClient();
        using var readiness = await client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);

        var worker = SourceWorker(factory);
        Assert.NotNull(worker.ExecuteTask);
        await Task.Delay(TimeSpan.FromMilliseconds(750));
        Assert.False(
            worker.ExecuteTask!.IsCompleted,
            "The private Rules Core ingress should participate in normalization maintenance instead of exiting its worker.");
    }

    [Fact]
    public async Task PublicAndPrivateIngressWorkersRemainActiveTogether()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore")))
        {
            return;
        }

        await using var publicFactory = Factory(RulesCoreApiSurfaceMode.PublicOnly);
        using var publicClient = publicFactory.CreateClient();
        using var publicReadiness = await publicClient.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.OK, publicReadiness.StatusCode);

        await using var privateFactory = Factory(RulesCoreApiSurfaceMode.PrivateOnly);
        using var privateClient = privateFactory.CreateClient();
        using var privateReadiness = await privateClient.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.OK, privateReadiness.StatusCode);

        var publicWorker = SourceWorker(publicFactory);
        var privateWorker = SourceWorker(privateFactory);
        Assert.NotNull(publicWorker.ExecuteTask);
        Assert.NotNull(privateWorker.ExecuteTask);

        await Task.Delay(TimeSpan.FromSeconds(1));

        Assert.False(
            publicWorker.ExecuteTask!.IsCompleted,
            "The public Rules Core maintenance worker should remain active.");
        Assert.False(
            privateWorker.ExecuteTask!.IsCompleted,
            "The private Rules Core maintenance worker should remain active so both processes can normalize in parallel.");
    }

    [Fact]
    public async Task AutomaticNormalizationUsesBoundedBatch()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore")))
        {
            return;
        }

        var maintenance = new RecordingNormalizationMaintenanceService();
        await using var factory = Factory(
            RulesCoreApiSurfaceMode.PrivateOnly,
            services =>
            {
                services.RemoveAll<ISourceNormalizationMaintenanceService>();
                services.AddSingleton<ISourceNormalizationMaintenanceService>(maintenance);
            });
        using var client = factory.CreateClient();
        using var readiness = await client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);

        var observedLimit = await maintenance.FirstLimit.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(25, observedLimit);
    }

    private static WebApplicationFactory<Program> Factory(
        RulesCoreApiSurfaceMode mode,
        Action<IServiceCollection>? configureServices = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [RulesCoreApiBoundary.ApiSurfaceConfigurationKey] = mode.ToString()
                });
            });
            if (configureServices is not null)
            {
                builder.ConfigureServices(configureServices);
            }
        });

    private static BackgroundService SourceWorker(WebApplicationFactory<Program> factory) =>
        Assert.IsAssignableFrom<BackgroundService>(
            factory.Services.GetServices<IHostedService>().Single(service =>
                string.Equals(
                    service.GetType().Name,
                    "CurrentUserSourceRefreshBackground",
                    StringComparison.Ordinal)));

    private sealed class RecordingNormalizationMaintenanceService
        : ISourceNormalizationMaintenanceService
    {
        public TaskCompletionSource<int> FirstLimit { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<SourceNormalizationStatusView> GetStatusAsync(
            string? packageKey = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SourceNormalizationStatusView(
                SourceNormalizationVersion.Current,
                RevisionCount: 0,
                CurrentRevisionCount: 0,
                PendingRevisionCount: 0,
                FailedRevisionCount: 0));

        public Task<SourceNormalizationRunView> ReconcileAsync(
            int limit = 25,
            bool retryFailed = false,
            string? packageKey = null,
            CancellationToken cancellationToken = default)
        {
            FirstLimit.TrySetResult(limit);
            return Task.FromResult(new SourceNormalizationRunView(
                SourceNormalizationVersion.Current,
                AttemptedRevisionCount: 0,
                UpdatedContentCount: 0,
                UnchangedContentCount: 0,
                CanonicalReassociationCount: 0,
                FailedRevisionCount: 0,
                RemainingPendingRevisionCount: 0,
                Failures: Array.Empty<SourceNormalizationFailureView>()));
        }
    }
}
