using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class EmbeddedModuleAssetsIntegrationTests
{
    [Fact]
    public async Task AuthoringModuleAssetsAreServedFromTheRulesCoreHost()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var app = await GetAssetAsync(client, "/app.js", "javascript");
        Assert.Contains("./api.js", app, StringComparison.Ordinal);
        Assert.Contains("./authoring.js", app, StringComparison.Ordinal);
        Assert.Contains("rules-core.css", app, StringComparison.Ordinal);

        var api = await GetAssetAsync(client, "/api.js", "javascript");
        Assert.Contains("/upstream", api, StringComparison.Ordinal);
        Assert.Contains("saveGlobalDecision", api, StringComparison.Ordinal);

        var authoring = await GetAssetAsync(client, "/authoring.js", "javascript");
        Assert.Contains("Global Rules", authoring, StringComparison.Ordinal);
        Assert.Contains("Campaign Rules", authoring, StringComparison.Ordinal);
        Assert.Contains("Preview", authoring, StringComparison.Ordinal);
        Assert.Contains("Save decision", authoring, StringComparison.Ordinal);

        await GetAssetAsync(client, "/ui.js", "javascript");
        await GetAssetAsync(client, "/rules-core.css", "text/css");
    }

    private static async Task<string> GetAssetAsync(
        HttpClient client,
        string path,
        string expectedContentTypeFragment)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            expectedContentTypeFragment,
            response.Content.Headers.ContentType?.MediaType ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        return await response.Content.ReadAsStringAsync();
    }
}
