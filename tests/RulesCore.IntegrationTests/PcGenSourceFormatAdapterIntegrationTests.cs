using System.Text;
using System.Text.Json;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

public sealed class PcGenSourceFormatAdapterIntegrationTests
{
    [Fact]
    public void BatchReadUsesPccMetadataToTypeAndAssociateListEntries()
    {
        var adapter = new PcGenSourceFormatAdapter();
        var campaign = Artifact(
            "data/35e/example/_example.pcc",
            """
            CAMPAIGN:Example Revised Source
            KEY:Example 3.5
            GAMEMODE:35e
            PUBNAMELONG:Example Publisher
            SOURCELONG:Example Revised Rulebook
            SOURCESHORT:EX35
            SOURCEDATE:2006-05-12
            SPELL:spells/example_spells.lst
            """);
        var spells = Artifact(
            "data/35e/example/spells/example_spells.lst",
            """
            SOURCELONG:Example Revised Rulebook	SOURCESHORT:EX35
            Arc Spark	TYPE:Arcane	SCHOOL:Evocation	CUSTOMTAG:One	CUSTOMTAG:Two	DESC:A line Rules Core must preserve.
            Arc Spark	TYPE:Arcane	VARIANT:Greater	DESC:A second same-named native entry.
            """);

        var representations = adapter.TryReadMany([campaign, spells]);

        Assert.Equal(2, representations.Count);
        var campaignRepresentation = representations.Single(value => value.Artifact.FileName == "_example.pcc");
        var listRepresentation = representations.Single(value => value.Artifact.FileName == "example_spells.lst");
        var publication = Assert.Single(campaignRepresentation.Publications!);
        Assert.Equal("Example Revised Rulebook", publication.DisplayName);
        Assert.Equal("Example Publisher", publication.Publisher);
        Assert.Equal("3.5e", publication.GameEdition);
        Assert.Equal(new DateOnly(2006, 5, 12), publication.PublicationDate);
        Assert.Equal("EX35", publication.ExternalIdentifiers!["pcgen-source-short"]);

        Assert.Equal(2, listRepresentation.Records.Count);
        Assert.All(listRepresentation.Records, record =>
        {
            Assert.Equal("spell", record.EntityType);
            Assert.Equal("EX35", record.SourceCode);
            Assert.Equal(publication.LocalKey, record.PublicationLocalKey);
            Assert.StartsWith("pcgen|spell|", record.NativeKey, StringComparison.Ordinal);
        });
        Assert.NotEqual(listRepresentation.Records[0].NativeKey, listRepresentation.Records[1].NativeKey);

        using var parsed = JsonDocument.Parse(listRepresentation.Records[0].RawJson);
        Assert.Equal(
            "Arc Spark\tTYPE:Arcane\tSCHOOL:Evocation\tCUSTOMTAG:One\tCUSTOMTAG:Two\tDESC:A line Rules Core must preserve.",
            parsed.RootElement.GetProperty("rawLine").GetString());
        var customTags = parsed.RootElement.GetProperty("segments")
            .EnumerateArray()
            .Where(value => value.GetProperty("tag").GetString() == "CUSTOMTAG")
            .Select(value => value.GetProperty("value").GetString())
            .ToArray();
        Assert.Equal(["One", "Two"], customTags);
    }

    [Fact]
    public void CampaignGameModesMapThreeEAndThirtyFiveEWithoutInventingExactPartialDates()
    {
        var adapter = new PcGenSourceFormatAdapter();
        var threeE = Require(adapter.TryRead(Artifact(
            "data/3e/wizards/srd/srd.pcc",
            """
            CAMPAIGN:3.0 SRD
            GAMEMODE:3e
            PUBNAMELONG:Wizards of the Coast
            SOURCELONG:System Reference Document
            SOURCESHORT:SRD
            SOURCEDATE:2000-01
            """)));
        var thirtyFiveE = Require(adapter.TryRead(Artifact(
            "data/35e/wizards/rsrd/rsrd.pcc",
            """
            CAMPAIGN:3.5 RSRD
            GAMEMODE:35e
            PUBNAMELONG:Wizards of the Coast
            SOURCELONG:Revised System Reference Document
            SOURCESHORT:RSRD
            SOURCEDATE:2003-07
            """)));

        Assert.Equal("3e", Assert.Single(threeE.Publications!).GameEdition);
        Assert.Equal("3.5e", Assert.Single(thirtyFiveE.Publications!).GameEdition);
        Assert.Null(Assert.Single(threeE.Publications!).PublicationDate);
        Assert.Null(Assert.Single(thirtyFiveE.Publications!).PublicationDate);
        Assert.Contains("2000-01", threeE.MetadataJson, StringComparison.Ordinal);
        Assert.Contains("2003-07", thirtyFiveE.MetadataJson, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedListReferencedByDifferentPublicationsRemainsUnassociatedWhenEvidenceIsAmbiguous()
    {
        var adapter = new PcGenSourceFormatAdapter();
        var first = Artifact(
            "data/35e/shared/first.pcc",
            """
            CAMPAIGN:First Book
            GAMEMODE:35e
            SOURCELONG:First Book
            SOURCESHORT:FIRST
            SPELL:shared.lst
            """);
        var second = Artifact(
            "data/35e/shared/second.pcc",
            """
            CAMPAIGN:Second Book
            GAMEMODE:35e
            SOURCELONG:Second Book
            SOURCESHORT:SECOND
            SPELL:shared.lst
            """);
        var shared = Artifact(
            "data/35e/shared/shared.lst",
            "Shared Spell\tTYPE:Arcane\tDESC:Ambiguous publication evidence.\n");

        var representations = adapter.TryReadMany([first, second, shared]);
        var listRepresentation = representations.Single(value => value.Artifact.FileName == "shared.lst");
        var record = Assert.Single(listRepresentation.Records);

        Assert.Equal("spell", record.EntityType);
        Assert.Null(record.PublicationLocalKey);
        Assert.Null(record.SourceCode);
        Assert.Empty(listRepresentation.Publications!);
    }

    [Fact]
    public void MonsterManualTwoShapedFixturePreservesSourcePageOutsideSemanticProjection()
    {
        var adapter = new PcGenSourceFormatAdapter();
        var campaign = Artifact(
            "data/35e/wizards_of_the_coast/monster_manual_2/monster_manual_2.pcc",
            """
            CAMPAIGN:Monster Manual II
            GAMEMODE:35e
            PUBNAMELONG:Wizards of the Coast
            SOURCELONG:Monster Manual II
            SOURCESHORT:MM2
            SOURCEDATE:2002-09
            RACE:mm2_races.lst
            """);
        var races = Artifact(
            "data/35e/wizards_of_the_coast/monster_manual_2/mm2_races.lst",
            "Abeil (Vassal)\tTYPE:Monstrous Humanoid\tSIZE:M\tSOURCEPAGE:p.22\tDESC:Representative fixture.\n");

        var representations = adapter.TryReadMany([campaign, races]);
        var publication = Assert.Single(representations.Single(value => value.Artifact.FileName == "monster_manual_2.pcc").Publications!);
        var record = Assert.Single(representations.Single(value => value.Artifact.FileName == "mm2_races.lst").Records);

        Assert.Equal("3.5e", publication.GameEdition);
        Assert.Null(publication.PublicationDate);
        Assert.Equal("MM2", record.SourceCode);
        Assert.Equal("race", record.EntityType);
        Assert.Contains("SOURCEPAGE:p.22", record.RawJson, StringComparison.Ordinal);
        Assert.NotNull(record.SemanticJson);
        Assert.DoesNotContain("SOURCEPAGE", record.SemanticJson!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Representative fixture.", record.SemanticJson!, StringComparison.Ordinal);
    }

    [Fact]
    public void DeferredCopyModifyAndForgetOperationsArePreservedButNotCanonicalizedAsRules()
    {
        var adapter = new PcGenSourceFormatAdapter();
        var campaign = Artifact(
            "data/35e/example/example.pcc",
            """
            CAMPAIGN:Operation Book
            GAMEMODE:35e
            SOURCELONG:Operation Book
            SOURCESHORT:OPS
            SKILL:operations_skills.lst
            """);
        var list = Artifact(
            "data/35e/example/operations_skills.lst",
            """
            Camel.COPY=Camel (Old Nag)
            Balance.MOD	BONUS:SKILL|Balance|1
            Knowledge (Local).FORGET
            """);

        var representation = adapter.TryReadMany([campaign, list])
            .Single(value => value.Artifact.FileName == "operations_skills.lst");

        Assert.Equal(3, representation.Records.Count);
        Assert.All(representation.Records, record =>
        {
            Assert.Equal("pcgen-operation", record.EntityType);
            Assert.Null(record.PublicationLocalKey);
            Assert.Null(record.SemanticJson);
        });
        var operations = representation.Records
            .Select(record => JsonDocument.Parse(record.RawJson))
            .ToArray();
        try
        {
            Assert.Equal("copy", operations[0].RootElement.GetProperty("operation").GetString());
            Assert.Equal("Camel", operations[0].RootElement.GetProperty("target").GetString());
            Assert.Equal("Camel (Old Nag)", operations[0].RootElement.GetProperty("copyName").GetString());
            Assert.Equal("modify", operations[1].RootElement.GetProperty("operation").GetString());
            Assert.Equal("forget", operations[2].RootElement.GetProperty("operation").GetString());
        }
        finally
        {
            foreach (var operation in operations) operation.Dispose();
        }
    }

    [Fact]
    public void UnsupportedClassFamilyIsImportedAsLosslessFragmentsInsteadOfGuessedEntities()
    {
        var adapter = new PcGenSourceFormatAdapter();
        var campaign = Artifact(
            "data/35e/example/example.pcc",
            """
            CAMPAIGN:Class Book
            GAMEMODE:35e
            SOURCELONG:Class Book
            SOURCESHORT:CLS
            CLASS:example_classes.lst
            """);
        var classes = Artifact(
            "data/35e/example/example_classes.lst",
            """
            CLASS:Example Class	HD:8	TYPE:Base.PC
            CLASS:Example Class	STARTSKILLPTS:4
            """);

        var representation = adapter.TryReadMany([campaign, classes])
            .Single(value => value.Artifact.FileName == "example_classes.lst");

        Assert.Equal(2, representation.Records.Count);
        Assert.All(representation.Records, record =>
        {
            Assert.Equal("pcgen-fragment", record.EntityType);
            Assert.Null(record.PublicationLocalKey);
            Assert.Contains("unsupported-family", record.RawJson, StringComparison.Ordinal);
        });
    }

    private static SourceRepresentationArtifact Artifact(string path, string content) =>
        new(
            Path.GetFileName(path),
            Encoding.UTF8.GetBytes(content.Replace("\r\n", "\n", StringComparison.Ordinal)),
            $"test:https://example.invalid/repository#{path}",
            SourceUri: $"https://example.invalid/{path}",
            MediaType: "text/plain");

    private static T Require<T>(T? value) where T : class
    {
        Assert.NotNull(value);
        return value!;
    }
}
