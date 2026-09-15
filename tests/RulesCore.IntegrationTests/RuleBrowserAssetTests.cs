namespace RulesCore.IntegrationTests;

public sealed class RuleBrowserAssetTests
{
    [Fact]
    public void BrowserConsumesToolRelativeRoutingContract()
    {
        var content = ReadWebAsset("rules-browser.js");

        Assert.Contains("toolRoute", content, StringComparison.Ordinal);
        Assert.Contains("toolBasePath", content, StringComparison.Ordinal);
        Assert.Contains("browserLink", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MonsterRendererIsRegisteredAsSpecializedRuleRenderer()
    {
        var content = ReadWebAsset("rule-renderers.js");

        Assert.Contains("[\"monster\", renderMonster]", content, StringComparison.Ordinal);
        Assert.Contains("Legendary Actions", content, StringComparison.Ordinal);
        Assert.Contains("Ability Scores", content, StringComparison.Ordinal);
        Assert.Contains("abilityDatum(\"Save\"", content, StringComparison.Ordinal);
        Assert.Contains("hasAbilitySaveModel", content, StringComparison.Ordinal);
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
