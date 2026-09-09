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
        Assert.Contains("./rules-browser.js", app, StringComparison.Ordinal);
        Assert.Contains("./campaign-baseline-authoring.js", app, StringComparison.Ordinal);
        Assert.Contains("./concept-source-authoring.js", app, StringComparison.Ordinal);
        Assert.Contains("./source-access-admin.js", app, StringComparison.Ordinal);
        Assert.Contains("./source-admin.js", app, StringComparison.Ordinal);
        Assert.Contains("./source-normalization.js", app, StringComparison.Ordinal);
        Assert.Contains("rules-core.css", app, StringComparison.Ordinal);

        var api = await GetAssetAsync(client, "/api.js", "javascript");
        Assert.Contains("/upstream", api, StringComparison.Ordinal);
        Assert.Contains("getGlobalRulesCatalog", api, StringComparison.Ordinal);
        Assert.Contains("getCampaignRulesCatalog", api, StringComparison.Ordinal);
        Assert.Contains("getResolvedGlobalRule", api, StringComparison.Ordinal);
        Assert.Contains("getResolvedCampaignRule", api, StringComparison.Ordinal);
        Assert.Contains("saveGlobalDecision", api, StringComparison.Ordinal);
        Assert.Contains("searchSourceEntities", api, StringComparison.Ordinal);
        Assert.Contains("createGlobalConcept", api, StringComparison.Ordinal);
        Assert.Contains("bindGlobalConceptSource", api, StringComparison.Ordinal);
        Assert.Contains("importSourceDocument", api, StringComparison.Ordinal);
        Assert.Contains("getSourceAdministrationPackages", api, StringComparison.Ordinal);
        Assert.Contains("grantCurrentUserSourcePackage", api, StringComparison.Ordinal);
        Assert.Contains("revokeCurrentUserSourcePackage", api, StringComparison.Ordinal);
        Assert.Contains("getSourceNormalizationCandidates", api, StringComparison.Ordinal);
        Assert.Contains("acceptSourceNormalization", api, StringComparison.Ordinal);
        Assert.Contains("getCampaignBaselineCandidates", api, StringComparison.Ordinal);
        Assert.Contains("previewCampaignBaseline", api, StringComparison.Ordinal);

        var rulesBrowser = await GetAssetAsync(client, "/rules-browser.js", "javascript");
        Assert.Contains("Rules Browser", rulesBrowser, StringComparison.Ordinal);
        Assert.Contains("Browse published rules that this account may access", rulesBrowser, StringComparison.Ordinal);
        Assert.Contains("Resolved rule document", rulesBrowser, StringComparison.Ordinal);
        Assert.Contains("Campaign override", rulesBrowser, StringComparison.Ordinal);

        var authoring = await GetAssetAsync(client, "/authoring.js", "javascript");
        Assert.Contains("Global Rules", authoring, StringComparison.Ordinal);
        Assert.Contains("Campaign Rules", authoring, StringComparison.Ordinal);
        Assert.Contains("Preview", authoring, StringComparison.Ordinal);
        Assert.Contains("Save decision", authoring, StringComparison.Ordinal);

        var campaignBaselineAuthoring = await GetAssetAsync(
            client,
            "/campaign-baseline-authoring.js",
            "javascript");
        Assert.Contains("Global baseline", campaignBaselineAuthoring, StringComparison.Ordinal);
        Assert.Contains("Preview migration", campaignBaselineAuthoring, StringComparison.Ordinal);
        Assert.Contains("Select baseline", campaignBaselineAuthoring, StringComparison.Ordinal);

        var conceptSourceAuthoring = await GetAssetAsync(
            client,
            "/concept-source-authoring.js",
            "javascript");
        Assert.Contains("Create rule concept", conceptSourceAuthoring, StringComparison.Ordinal);
        Assert.Contains("Source bindings", conceptSourceAuthoring, StringComparison.Ordinal);
        Assert.Contains("Find sources", conceptSourceAuthoring, StringComparison.Ordinal);

        var sourceNormalization = await GetAssetAsync(
            client,
            "/source-normalization.js",
            "javascript");
        Assert.Contains("Normalize imported sources", sourceNormalization, StringComparison.Ordinal);
        Assert.Contains("Suggestions never apply automatically", sourceNormalization, StringComparison.Ordinal);
        Assert.Contains("Create + bind", sourceNormalization, StringComparison.Ordinal);
        Assert.Contains("Bind to concept", sourceNormalization, StringComparison.Ordinal);

        var sourceAdmin = await GetAssetAsync(client, "/source-admin.js", "javascript");
        Assert.Contains("Source Administration", sourceAdmin, StringComparison.Ordinal);
        Assert.Contains("Import source document", sourceAdmin, StringComparison.Ordinal);
        Assert.Contains("Dev control-plane operation", sourceAdmin, StringComparison.Ordinal);

        var sourceAccessAdmin = await GetAssetAsync(
            client,
            "/source-access-admin.js",
            "javascript");
        Assert.Contains("Current account source access", sourceAccessAdmin, StringComparison.Ordinal);
        Assert.Contains("Grant my account", sourceAccessAdmin, StringComparison.Ordinal);
        Assert.Contains("Revoke my account", sourceAccessAdmin, StringComparison.Ordinal);
        Assert.Contains("only the current authenticated account", sourceAccessAdmin, StringComparison.Ordinal);

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
