using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RulesCore.IntegrationTests;

public sealed class RuleBrowserAssetTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public RuleBrowserAssetTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task BrowserConsumesToolRelativeRoutingContract()
    {
        using var response = await _client.GetAsync("/rules-browser.js");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("toolRoute", content, StringComparison.Ordinal);
        Assert.Contains("toolBasePath", content, StringComparison.Ordinal);
        Assert.Contains("browserLink", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MonsterRendererIsRegisteredAsSpecializedRuleRenderer()
    {
        using var response = await _client.GetAsync("/rule-renderers.js");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("[\"monster\", renderMonster]", content, StringComparison.Ordinal);
        Assert.Contains("Legendary Actions", content, StringComparison.Ordinal);
        Assert.Contains("Saving Throws", content, StringComparison.Ordinal);
    }
}
