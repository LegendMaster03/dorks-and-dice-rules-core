using System.Text.Json;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Tests;

public sealed class LegacySrdEpicClassificationTests
{
    [Fact]
    public void LevelLessEpicSpellDescriptionsRemainSpells()
    {
        const string markdown = """
            # EPIC SPELLS

            ## Epic Spell Descriptions
            General format rules for epic spells.

            ## Animus Blast

            Evocation [Cold]

            **Spellcraft DC:** 50
            **Components:** V, S
            **Casting Time:** 1 standard action
            **Range:** 300 ft.
            **Area:** 20-ft.-radius hemisphere burst
            **Duration:** Instantaneous
            **Saving Throw:** Reflex half
            **Spell Resistance:** Yes
            **To Develop:** 450,000 gp; 9 days; 18,000 XP.
            """;

        var json = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            markdown,
            "https://raw.githubusercontent.com/olimot/srd-v3.5-md/main/epic/epic-spells.md",
            "SRD35",
            out _);

        using var document = JsonDocument.Parse(json);
        var spells = document.RootElement.GetProperty("spell")
            .EnumerateArray()
            .ToArray();
        var spell = Assert.Single(spells);
        Assert.Equal("Animus Blast", spell.GetProperty("name").GetString());
        Assert.False(document.RootElement.TryGetProperty("rule", out var rules)
                     && rules.EnumerateArray().Any(value =>
                         string.Equals(
                             value.GetProperty("name").GetString(),
                             "Animus Blast",
                             StringComparison.Ordinal)));
    }

    [Fact]
    public void PreviouslyGenericEpicSpellBackfillsToSpellAndReceivesEpicTierMetadata()
    {
        const string raw = """
            {
              "name":"Animus Blast",
              "source":"SRD35",
              "uniqueId":"epic-spell-animus-blast",
              "documentUri":"https://raw.githubusercontent.com/olimot/srd-v3.5-md/main/epic/epic-spells.md",
              "headingLevel":2,
              "originalHeading":"Animus Blast",
              "body":"Spellcraft DC: 50. Casting Time: 1 standard action. Range: 300 ft. To Develop: 450,000 gp."
            }
            """;
        const string content = """
            {"name":"Animus Blast","source":"SRD35","_rulesCore":{"context":{"sourceFormat":"legacy-srd-snapshot","nativeEntityType":"rule","edition":"3.5e"}}}
            """;
        var record = new NormalizedSourceRecord(
            "rule",
            "Animus Blast",
            "SRD35",
            "test|epic-spell|animus-blast",
            raw)
        {
            ContentJson = content
        };
        var representation = new NormalizedSourceRepresentation(
            LegacySrdSourceFormatAdapter.Format,
            new SourceRepresentationArtifact(
                "srd-3-5e.json",
                [],
                "test:epic-spell"),
            [record]);

        var normalized = EpicContentNormalizationPolicy.Apply(representation, record);

        Assert.Equal("spell", normalized.EntityType);
        Assert.Equal("Animus Blast", normalized.Name);
        Assert.True(EpicContentNormalizationPolicy.IsReviewedIdentityMigration(
            "rule",
            "Animus Blast",
            normalized));
        using var document = JsonDocument.Parse(normalized.ContentJson!);
        var epic = document.RootElement.GetProperty("_rulesCore").GetProperty("epic");
        Assert.Equal("epic", epic.GetProperty("tier").GetString());
        Assert.Equal("content", epic.GetProperty("kind").GetString());

        var tier = Assert.Single(
            RuleBrowserSummaryProjector.Project("spell", document.RootElement)
                .Where(value => value.Key == "tier"));
        Assert.Equal("Epic", tier.Value);
    }
}
