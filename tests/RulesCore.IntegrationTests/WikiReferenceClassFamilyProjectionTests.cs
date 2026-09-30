using System.Text.Json;
using RulesCore.Application.Rules;

namespace RulesCore.IntegrationTests;

public sealed class WikiReferenceClassFamilyProjectionTests
{
    [Fact]
    public void VariationProjectsClassFeatureLevelsFromNormalizedReferences()
    {
        using var document = JsonDocument.Parse("""
            {
              "classFeatures": [
                "Fighting Style|PHB|Fighter|1",
                { "classFeature": "Action Surge|PHB|Fighter|2" },
                { "name": "Explicit Feature", "level": 4 },
                "Unresolved Feature"
              ]
            }
            """);

        var variation = Variation("class", document.RootElement.Clone());

        Assert.Collection(
            variation.AdvancementFeatures,
            feature => Assert.Equal(("Fighting Style", 1, "Fighting Style|PHB|Fighter|1"),
                (feature.Name, feature.Level, feature.FeatureReference)),
            feature => Assert.Equal(("Action Surge", 2, "Action Surge|PHB|Fighter|2"),
                (feature.Name, feature.Level, feature.FeatureReference)),
            feature => Assert.Equal(("Explicit Feature", 4, (string?)null),
                (feature.Name, feature.Level, feature.FeatureReference)),
            feature => Assert.Equal(("Unresolved Feature", (int?)null, "Unresolved Feature"),
                (feature.Name, feature.Level, feature.FeatureReference)));
    }

    [Fact]
    public void VariationUsesSubclassReferenceLevelPosition()
    {
        using var document = JsonDocument.Parse("""
            {
              "subclassFeatures": [
                "Improved Critical|PHB|Fighter|PHB|Champion|3"
              ]
            }
            """);

        var variation = Variation("subclass", document.RootElement.Clone());

        var feature = Assert.Single(variation.AdvancementFeatures);
        Assert.Equal("Improved Critical", feature.Name);
        Assert.Equal(3, feature.Level);
        Assert.Equal("Improved Critical|PHB|Fighter|PHB|Champion|3", feature.FeatureReference);
    }

    [Fact]
    public void PrestigeClassRemainsIndependentAndSupportsPrestigeFeatureCollection()
    {
        using var document = JsonDocument.Parse("""
            {
              "prestigeClassFeatures": [
                { "name": "Arcane Advancement", "level": 1 }
              ]
            }
            """);

        var variation = Variation("prestigeClass", document.RootElement.Clone());

        var feature = Assert.Single(variation.AdvancementFeatures);
        Assert.Equal("Arcane Advancement", feature.Name);
        Assert.Equal(1, feature.Level);
    }

    [Fact]
    public void AdvancementProjectionIsSerializedAsAnAdditiveWikiReferenceContract()
    {
        using var document = JsonDocument.Parse("""
            { "classFeatures": ["Second Wind|PHB|Fighter|1"] }
            """);
        var variation = Variation("class", document.RootElement.Clone());

        var json = JsonSerializer.Serialize(
            variation,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"advancementFeatures\"", json, StringComparison.Ordinal);
        Assert.Contains("\"level\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"Second Wind\"", json, StringComparison.Ordinal);
    }

    private static WikiReferenceVariationView Variation(string category, JsonElement document) =>
        new(
            CanonicalEntityId: Guid.NewGuid(),
            SourceEntityId: Guid.NewGuid(),
            SourceEntityRevisionId: Guid.NewGuid(),
            SourceRevisionNumber: 1,
            Name: "Example",
            Category: category,
            SourceCode: "TEST",
            PackageKey: "test",
            PackageDisplayName: "Test",
            PublicationId: Guid.NewGuid(),
            PublicationKey: "test.publication",
            PublicationDisplayName: "Test Publication",
            EditionKey: "5e",
            EditionDisplayName: "5e",
            PublicationDate: new DateOnly(2024, 1, 1),
            IsEffective: true,
            Document: document);
}
