namespace RulesCore.IntegrationTests;

public sealed class RuleBrowserAssetTests
{
    [Fact]
    public void BrowserConsumesToolRelativeRoutingContract()
    {
        var content = ReadWebAsset("rules-browser.js");

        Assert.Contains("registerToolRouteHandler", content, StringComparison.Ordinal);
        Assert.Contains("navigateToolRoute", content, StringComparison.Ordinal);
        Assert.Contains("browserLink?.toolRelativePath", content, StringComparison.Ordinal);
        Assert.Contains("routeParts", content, StringComparison.Ordinal);
        Assert.Contains("withQuery", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MonsterRendererIsRegisteredAsSpecializedRuleRenderer()
    {
        var content = ReadWebAsset("rule-renderers.js");

        Assert.Contains("renderers.set(type, renderMonster)", content, StringComparison.Ordinal);
        Assert.Contains("Legendary Actions", content, StringComparison.Ordinal);
        Assert.Contains("Saving Throws", content, StringComparison.Ordinal);
        Assert.Contains("Chaos Threshold", content, StringComparison.Ordinal);
    }

    private static string ReadWebAsset(string filename)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "dorks-and-dice-rules-core.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate the Rules Core repository root.");
        }

        var path = Path.Combine(directory.FullName, "src", "RulesCore.Web", "wwwroot", filename);
        return File.ReadAllText(path);
    }
}
