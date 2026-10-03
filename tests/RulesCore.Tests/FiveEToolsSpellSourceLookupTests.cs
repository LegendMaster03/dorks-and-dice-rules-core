using System.Text;
using System.Text.Json;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Tests;

public sealed class FiveEToolsSpellSourceLookupTests
{
    [Fact]
    public void GeneratedSpellSourceLookupBecomesSpellCompanionEvidence()
    {
        const string source = """
            {
              "xphb": {
                "fireball": {
                  "class": {
                    "XPHB": {
                      "Sorcerer": true,
                      "Wizard": true
                    }
                  },
                  "classVariant": {
                    "XPHB": {
                      "Bard": {
                        "definedInSources": ["XPHB"]
                      }
                    }
                  }
                }
              }
            }
            """;
        var artifact = new SourceRepresentationArtifact(
            "data/generated/gendata-spell-source-lookup.json",
            Encoding.UTF8.GetBytes(source),
            "test:spell-source-lookup");

        var representation = new FiveEToolsCompanionSourceFormatAdapter().TryRead(artifact);

        Assert.NotNull(representation);
        Assert.Empty(representation!.Records);
        var companion = Assert.Single(representation.CompanionContents);
        Assert.Equal(FiveEToolsCompanionSourceFormatAdapter.SpellSourceLookupCompanionKind, companion.CompanionKind);
        Assert.Equal("fireball", companion.Name);
        Assert.Equal("xphb", companion.SourceCode);
        var target = Assert.Single(companion.Targets);
        Assert.Equal("spell", target.EntityType);
        Assert.Equal("generated-spell-source-lookup", target.EvidenceKind);

        var names = WikiReferenceSpellAccessEnrichmentService.ReadClassNames(companion.RawJson);
        Assert.Equal(["Bard", "Sorcerer", "Wizard"], names);

        using var payload = JsonDocument.Parse(companion.RawJson);
        Assert.True(payload.RootElement.TryGetProperty("access", out _));
    }

    [Fact]
    public void MalformedOrNonClassLookupPayloadDoesNotInventClassAccess()
    {
        Assert.Empty(WikiReferenceSpellAccessEnrichmentService.ReadClassNames("{}"));
        Assert.Empty(WikiReferenceSpellAccessEnrichmentService.ReadClassNames("{\"access\":{\"feat\":{\"XPHB\":{\"Magic Initiate\":true}}}}"));
        Assert.Empty(WikiReferenceSpellAccessEnrichmentService.ReadClassNames("not-json"));
    }
}
