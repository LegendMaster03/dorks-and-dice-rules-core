using System.Text.Json;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

public sealed class FiveEToolsFoundryOverlayTests
{
    // Complete PHB Sorcerer records extracted without field changes from
    // 5etools-mirror-3/5etools-src at e5d052071b635f58cc8006e9727053eaf78ea8f9.
    internal static SourceRepresentationArtifact Artifact(string fileName) => new(
        $"data/class/{fileName}",
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "FiveEToolsFoundry", fileName)),
        $"web:test#{fileName}");

    [Fact]
    public void RealSorcererDefinitionIsNotReplacedByFoundryCompanionData()
    {
        var definition = Artifact("class-sorcerer.json");
        var overlay = Artifact("foundry.json");
        var adapter = new FiveEToolsSourceFormatAdapter();

        // Demonstrate why interpreting the companion as a standalone definition
        // produces the exact immutable-identity collision seen in production.
        var original = Assert.Single(adapter.TryRead(definition)!.Records);
        var misclassified = Assert.Single(adapter.TryRead(overlay with { FileName = "upload.json" })!.Records);
        Assert.Equal("class|PHB|Sorcerer|", original.NativeKey);
        Assert.Equal(original.NativeKey, misclassified.NativeKey);
        Assert.NotEqual(original.NativeIdentityJson, misclassified.NativeIdentityJson);

        var registry = new SourceFormatAdapterRegistry([adapter]);
        var accepted = Assert.Single(registry.TryReadMany([definition, overlay]));
        Assert.Equal(definition, accepted.Artifact);
        var record = Assert.Single(accepted.Records);
        Assert.Equal(original.RawJson, record.RawJson);
        using var identity = JsonDocument.Parse(record.NativeIdentityJson!);
        Assert.Equal("classic", identity.RootElement.GetProperty("edition").GetString());
        Assert.Null(adapter.TryRead(overlay));
    }

    [Theory]
    [InlineData("data/class/foundry.json")]
    [InlineData("data/bestiary/foundry.json")]
    [InlineData("data/spells/foundry.json")]
    [InlineData("data/foundry-actions.json")]
    [InlineData("data/foundry-feats.json")]
    [InlineData("data/foundry-items.json")]
    [InlineData("data/foundry-optionalfeatures.json")]
    [InlineData("data/foundry-psionics.json")]
    [InlineData("data/foundry-races.json")]
    [InlineData("data/foundry-rewards.json")]
    [InlineData("data/foundry-vehicles.json")]
    [InlineData("snapshot\\data\\class\\foundry.json")]
    public void FoundryCompanionPathsAreNotStandaloneSiteDefinitions(string path)
    {
        Assert.Null(FiveEToolsSchemaContract.GetSiteSchemaId(path));
        Assert.Null(new FiveEToolsSourceFormatAdapter().TryRead(Artifact("foundry.json") with { FileName = path }));
    }

    [Theory]
    [InlineData("data/class/class-sorcerer.json", "class/class.json")]
    [InlineData("data/spells/spells-phb.json", "spells/spells.json")]
    [InlineData("data/bestiary/bestiary-mm.json", "bestiary/bestiary.json")]
    [InlineData("data/feats.json", "feats.json")]
    [InlineData("data/generated/bookref-quick.json", "generated/bookref-quick.json")]
    [InlineData("data/generated/gendata-spell-source-lookup.json", "generated/gendata-spell-source-lookup.json")]
    public void ExistingDefinitionAndGeneratedAllowlistPathsRemainSupported(string path, string schema)
    {
        Assert.Equal(schema, FiveEToolsSchemaContract.GetSiteSchemaId(path));
    }
}
