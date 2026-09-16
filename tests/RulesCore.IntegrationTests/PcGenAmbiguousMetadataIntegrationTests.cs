using System.Text;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

public sealed class PcGenAmbiguousMetadataIntegrationTests
{
    [Fact]
    public void MultipleEmbeddedSourceValuesRemainLosslessAndUnassociated()
    {
        var artifact = new SourceRepresentationArtifact(
            "mixed_spells.lst",
            Encoding.UTF8.GetBytes(string.Join('\n',
            [
                "SOURCELONG:First Book",
                "SOURCELONG:Second Book",
                "SOURCESHORT:FIRST",
                "SOURCESHORT:SECOND",
                "Arc Spark\tTYPE:Arcane\tSCHOOL:Evocation\tDESC:Representative source text."
            ])),
            "test:https://example.invalid/repository#data/35e/example/mixed_spells.lst",
            SourceUri: "https://example.invalid/data/35e/example/mixed_spells.lst",
            MediaType: "text/plain");

        var representation = new PcGenSourceFormatAdapter().TryRead(artifact);

        Assert.NotNull(representation);
        Assert.Contains("\"embeddedSourceMetadataAmbiguous\":true", representation!.MetadataJson, StringComparison.Ordinal);
        Assert.Empty(representation.Publications!);
        var record = Assert.Single(representation.Records);
        Assert.Equal("spell", record.EntityType);
        Assert.Null(record.SourceCode);
        Assert.Null(record.PublicationLocalKey);
        Assert.Contains("Arc Spark", record.RawJson, StringComparison.Ordinal);
    }
}
