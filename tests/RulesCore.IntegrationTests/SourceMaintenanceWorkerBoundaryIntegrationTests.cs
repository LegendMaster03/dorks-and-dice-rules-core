using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

    private static WebApplicationFactory<Program> Factory(RulesCoreApiSurfaceMode mode) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [RulesCoreApiBoundary.ApiSurfaceConfigurationKey] = mode.ToString()
                });
            });
        });

    private static BackgroundService SourceWorker(WebApplicationFactory<Program> factory) =>
        Assert.IsAssignableFrom<BackgroundService>(
            factory.Services.GetServices<IHostedService>().Single(service =>
                string.Equals(
                    service.GetType().Name,
                    "CurrentUserSourceRefreshBackground",
                    StringComparison.Ordinal)));
}
