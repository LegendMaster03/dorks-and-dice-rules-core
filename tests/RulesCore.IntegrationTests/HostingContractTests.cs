using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RulesCore.IntegrationTests;

public sealed class HostingContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HostingContractTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task HealthEndpointIsAvailable()
    {
        using var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EmbeddedModuleEntryPointIsAvailable()
    {
        using var response = await _client.GetAsync("/app.js");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("tool-root", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StandaloneMetadataDoesNotRedirect()
    {
        using var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
