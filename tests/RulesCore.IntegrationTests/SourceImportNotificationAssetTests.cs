using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class SourceImportNotificationAssetTests
{
    [Fact]
    public async Task SourceAddAssetExplainsImportStateAndSupportsDismissal()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/source-add.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "javascript",
            response.Content.Headers.ContentType?.MediaType ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

        var source = await response.Content.ReadAsStringAsync();
        Assert.Contains("Import complete", source, StringComparison.Ordinal);
        Assert.Contains("Dismiss finished", source, StringComparison.Ordinal);
        Assert.Contains("Dismiss notification", source, StringComparison.Ordinal);
        Assert.Contains("Dismiss hides finished notifications only", source, StringComparison.Ordinal);
        Assert.Contains("does not cancel imports", source, StringComparison.Ordinal);
        Assert.Contains("Last update", source, StringComparison.Ordinal);
        Assert.Contains("rules-core:dismissed-import-jobs:v1", source, StringComparison.Ordinal);
        Assert.Contains("window.localStorage", source, StringComparison.Ordinal);
    }
}
