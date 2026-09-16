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

    [Fact]
    public void GitPathsThatDifferOnlyByCaseRemainDistinctCampaigns()
    {
        var upper = CampaignArtifact(
            "data/35e/Case/Book.pcc",
            "Upper-case path campaign");
        var lower = CampaignArtifact(
            "data/35e/case/book.pcc",
            "Lower-case path campaign");

        var representations = new PcGenSourceFormatAdapter().TryReadMany([upper, lower]);

        Assert.Equal(2, representations.Count);
        var records = representations.Select(value => Assert.Single(value.Records)).ToArray();
        Assert.Contains(records, value => value.Name == "Upper-case path campaign");
        Assert.Contains(records, value => value.Name == "Lower-case path campaign");
        Assert.NotEqual(records[0].NativeKey, records[1].NativeKey);
    }

    private static SourceRepresentationArtifact CampaignArtifact(string path, string campaignName) =>
        new(
            path,
            Encoding.UTF8.GetBytes($"CAMPAIGN:{campaignName}\nGAMEMODE:35e\n"),
            $"test:https://example.invalid/repository#{path}",
            SourceUri: $"https://example.invalid/{path}",
            MediaType: "text/plain");
}
