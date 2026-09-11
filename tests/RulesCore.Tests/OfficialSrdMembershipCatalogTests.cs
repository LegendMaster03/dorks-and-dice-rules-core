using System.Text.Json;
using RulesCore.Application.Sources;

namespace RulesCore.Tests;

public sealed class OfficialSrdMembershipCatalogTests
{
    [Fact]
    public void Srd52FeatFilterKeepsPdfConfirmedFeatAndRejectsMalformedDuplicate()
    {
        const string json = """
            {
              "feat": [
                {
                  "name": "Magic Initiate",
                  "source": "SRD52",
                  "page": 168,
                  "reprintedAs": ["Magic Initiate|SRD52"]
                },
                {
                  "name": "Magic Initiate",
                  "source": "SRD52",
                  "page": 201,
                  "basicRules2024": true,
                  "category": "O"
                },
                {
                  "name": "Skilled",
                  "source": "SRD52",
                  "basicRules2024": true,
                  "category": "O"
                },
                {
                  "name": "Not In The SRD PDF",
                  "source": "SRD52",
                  "basicRules2024": true
                }
              ]
            }
            """;

        var filtered = FiveEToolsDocumentInspector.FilterBySourceCodes(
            json,
            "5.2.1",
            ["SRD52"],
            out var count);

        Assert.Equal(2, count);
        using var document = JsonDocument.Parse(filtered);
        var feats = document.RootElement.GetProperty("feat").EnumerateArray().ToArray();
        Assert.Equal(2, feats.Length);
        var magicInitiate = Assert.Single(feats, value => value.GetProperty("name").GetString() == "Magic Initiate");
        Assert.True(magicInitiate.GetProperty("basicRules2024").GetBoolean());
        Assert.Contains(feats, value => value.GetProperty("name").GetString() == "Skilled");
    }

    [Fact]
    public void Srd51ManualSectionsMatchOfficialPdfMembership()
    {
        Assert.Equal(
            new[] { "Acolyte" },
            OfficialSrdMembershipCatalog.GetConfirmedNames("SRD51", "background").OrderBy(value => value));
        Assert.Equal(
            new[] { "Grappler" },
            OfficialSrdMembershipCatalog.GetConfirmedNames("SRD51", "feat").OrderBy(value => value));
        Assert.Equal(
            new[]
            {
                "Dragonborn", "Dwarf", "Elf", "Gnome", "Half-Elf", "Half-Orc", "Halfling", "Human", "Tiefling"
            },
            OfficialSrdMembershipCatalog.GetConfirmedNames("SRD51", "race").OrderBy(value => value));
    }

    [Fact]
    public void Srd52ManualSectionsMatchOfficialPdfMembership()
    {
        Assert.Equal(
            new[] { "Acolyte", "Criminal", "Sage", "Soldier" },
            OfficialSrdMembershipCatalog.GetConfirmedNames("SRD52", "background").OrderBy(value => value));
        Assert.Equal(17, OfficialSrdMembershipCatalog.GetConfirmedNames("SRD52", "feat").Count);
        Assert.Equal(
            new[] { "Dragonborn", "Dwarf", "Elf", "Gnome", "Goliath", "Halfling", "Human", "Orc", "Tiefling" },
            OfficialSrdMembershipCatalog.GetConfirmedNames("SRD52", "race").OrderBy(value => value));
    }

    [Fact]
    public void UnconstrainedEntityTypesContinueToUseSourceCodeFiltering()
    {
        const string json = """
            {
              "spell": [
                { "name": "Example SRD spell", "source": "SRD52" },
                { "name": "Other spell", "source": "OTHER" }
              ]
            }
            """;

        var filtered = FiveEToolsDocumentInspector.FilterBySourceCodes(
            json,
            "5.2.1",
            ["SRD52"],
            out var count);

        Assert.Equal(1, count);
        using var document = JsonDocument.Parse(filtered);
        Assert.Equal(
            "Example SRD spell",
            Assert.Single(document.RootElement.GetProperty("spell").EnumerateArray()).GetProperty("name").GetString());
    }
}
