using System.Text;
using System.Text.Json;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Tests;

public sealed class WikiReferenceLegacyThreeXProjectionTests
{
    [Fact]
    public void LegacySrdSpellFieldsProjectIntoWikiBrowserMetadata()
    {
        const string source = """
            {
              "spell": [
                {
                  "name": "Fixture Bolt",
                  "source": "SRD35",
                  "uniqueId": "fixture-bolt",
                  "documentUri": "https://example.invalid/srd35/spells/fixture-bolt",
                  "body": "School: Evocation\nLevel: Sorcerer/Wizard 3\nComponents: V, S, M\nCasting Time: 1 standard action\nRange: Long (400 ft. + 40 ft./level)\nDuration: Instantaneous\nSaving Throw: Reflex half\nSpell Resistance: Yes"
                }
              ]
            }
            """;

        var representation = new LegacySrdSourceFormatAdapter().TryRead(
            new SourceRepresentationArtifact(
                "srd-3-5e.json",
                Encoding.UTF8.GetBytes(source),
                "test:wiki-phase-4-legacy-spell"));

        Assert.NotNull(representation);
        var record = Assert.Single(representation!.Records);
        Assert.NotNull(record.ContentJson);
        using var document = JsonDocument.Parse(record.ContentJson!);
        var fields = WikiReferenceBrowserProjection.Project("spell", document.RootElement);

        AssertField(fields, "school", "Evocation");
        AssertField(fields, "castingTime", "1 standard action");
        AssertField(fields, "range", "Long (400 ft. + 40 ft./level)");
        AssertField(fields, "components", "V, S, M");
        AssertField(fields, "duration", "Instantaneous");
        AssertField(fields, "spellList", "Sorcerer/Wizard 3");
        AssertField(fields, "savingThrow", "Reflex half");
        AssertField(fields, "spellResistance", "Yes");
    }

    [Fact]
    public void LegacySrdItemFieldsProjectIntoWikiBrowserMetadata()
    {
        const string source = """
            {
              "item": [
                {
                  "name": "Fixture Lens",
                  "source": "SRD35",
                  "uniqueId": "fixture-lens",
                  "documentUri": "https://example.invalid/srd35/items/fixture-lens",
                  "body": "Type: Wondrous Item\nPrice: 12,000 gp\nWeight: 1 lb."
                }
              ]
            }
            """;

        var representation = new LegacySrdSourceFormatAdapter().TryRead(
            new SourceRepresentationArtifact(
                "srd-3-5e.json",
                Encoding.UTF8.GetBytes(source),
                "test:wiki-phase-4-legacy-item"));

        Assert.NotNull(representation);
        var record = Assert.Single(representation!.Records);
        Assert.NotNull(record.ContentJson);
        using var document = JsonDocument.Parse(record.ContentJson!);
        var fields = WikiReferenceBrowserProjection.Project("item", document.RootElement);

        AssertField(fields, "type", "Wondrous Item");
        AssertField(fields, "value", "12,000 gp");
        AssertField(fields, "weight", "1 lb.");
    }

    private static void AssertField(
        IReadOnlyList<RulesCore.Application.Rules.ResolvedRuleBrowserFieldView> fields,
        string key,
        string expected)
    {
        var field = Assert.Single(fields, value => value.Key == key);
        Assert.Equal(expected, field.Value);
    }
}
