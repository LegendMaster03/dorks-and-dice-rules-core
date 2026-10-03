using System.Text.Json;
using RulesCore.Application.Rules;

namespace RulesCore.Tests;

public sealed class ClassFamilyFeatureReferenceParserTests
{
    [Fact]
    public void SidekickClassUsesClassFeatureShape()
    {
        using var document = JsonDocument.Parse("""
            {
              "classFeatures": [
                "Helpful|Expert Sidekick|TCE|1",
                { "classFeature": "Coordinated Strike|Expert Sidekick|TCE|6" }
              ]
            }
            """);

        var features = ClassFamilyFeatureReferenceParser.Project(
            "sidekickClass",
            document.RootElement);

        Assert.Collection(
            features,
            feature => Assert.Equal(("Helpful", (int?)1), (feature.Name, feature.Level)),
            feature => Assert.Equal(("Coordinated Strike", (int?)6), (feature.Name, feature.Level)));
    }
}
