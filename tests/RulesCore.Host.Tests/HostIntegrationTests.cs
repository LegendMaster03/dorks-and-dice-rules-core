using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Host;
using Xunit;

namespace RulesCore.Host.Tests;

public sealed class HostIntegrationTests
{
    [Theory]
    [InlineData("PublicOnly")]
    [InlineData("PrivateOnly")]
    public async Task EachSurfaceStartsHealthyButUnreadyAndDoesNotExposeApis(string surface)
    {
        await using var app = RulesCoreApplication.Create([], builder =>
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RulesCore:ApiSurface"] = surface,
                ["Rulesets:ContentRoot"] = Path.Combine(Path.GetTempPath(), "absent-rules-core-data"),
                ["Rulesets:DefaultRuleset"] = "first-party-001",
                ["ConnectionStrings:RulesCore"] = "Host=invalid.invalid;Port=1"
            });
        });

        await app.StartAsync();

        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Contains("\"alive\"", await health.Content.ReadAsStringAsync());

        var readiness = await client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
        Assert.Contains("content_unavailable", await readiness.Content.ReadAsStringAsync());

        // Both endpoint families remain absent, regardless of configured mode.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/rules")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/wiki/references")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/integration/session")).StatusCode);

        await app.StopAsync();
        Assert.True(app.Lifetime.ApplicationStopped.IsCancellationRequested);
    }

    [Theory]
    [InlineData("RulesCore:ApiSurface", "Combined")]
    [InlineData("RulesCore:ApiSurface", "Compatibility")]
    [InlineData("RulesCore:ApiSurface", "0")]
    [InlineData("Rulesets:ContentRoot", " ")]
    [InlineData("Rulesets:DefaultRuleset", "../working")]
    [InlineData("Rulesets:DefaultRuleset", "First-Party")]
    public void InvalidConfigurationFailsBeforeStartup(string key, string value)
    {
        Assert.Throws<ArgumentException>(() =>
            RulesCoreApplication.Create([], builder =>
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [key] = value
                })));
    }
}
