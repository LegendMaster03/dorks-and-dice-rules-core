using System.Text.Json;
using RulesCore.Application.Sources;

namespace RulesCore.Tests;

public sealed class LegacySrdDocumentInspectorTests
{
    [Fact]
    public void MarkdownFeatAndSpellHeadingsBecomeResolverFriendlyEntities()
    {
        const string feats = """
            This material is Open Game Content.

            # FEATS

            ## Feat Descriptions

            ### Power Attack <small>[General]</small>

            **Prerequisite:** Str 13.

            **Benefit:** Trade attack bonus for damage.

            ### Cleave <small>[General]</small>

            **Prerequisite:** Power Attack.
            """;

        var json = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            feats,
            "https://example.test/basic-rules-and-legal/feats.md",
            "SRD35",
            out var count);

        using var document = JsonDocument.Parse(json);
        var powerAttack = document.RootElement.GetProperty("feat")
            .EnumerateArray()
            .Single(value => value.GetProperty("name").GetString() == "Power Attack");

        Assert.True(count >= 4);
        Assert.Equal("SRD35", powerAttack.GetProperty("source").GetString());
        Assert.Contains("Str 13", powerAttack.GetProperty("body").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("legacy-", powerAttack.GetProperty("uniqueId").GetString(), StringComparison.Ordinal);

        const string spells = """
            # SPELLS (A-B)

            ## Acid Arrow

            Conjuration (Creation) [Acid]

            **Level:** Sor/Wiz 2
            **Components:** V, S, M
            """;
        var spellJson = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            spells,
            "https://example.test/spells/spells-a-b.md",
            "SRD35",
            out _);
        using var spellDocument = JsonDocument.Parse(spellJson);
        var acidArrow = Assert.Single(spellDocument.RootElement.GetProperty("spell").EnumerateArray());
        Assert.Equal("Acid Arrow", acidArrow.GetProperty("name").GetString());
    }

    [Fact]
    public void ThreeEHtmlClassAndMonsterBlocksPreserveEditionIdentity()
    {
        const string barbarian = """
            <html><body>
            <h1>Barbarian</h1>
            <p>Alignment: Any nonlawful</p>
            <p>Hit Die: d12</p>
            </body></html>
            """;
        var first = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            barbarian,
            "https://www.dragon.ee/30srd/barbarian.htm",
            "SRD3",
            out _);
        var second = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            barbarian,
            "https://www.dragon.ee/30srd/barbarian.htm",
            "SRD3",
            out _);

        using var firstDocument = JsonDocument.Parse(first);
        using var secondDocument = JsonDocument.Parse(second);
        var classEntity = Assert.Single(firstDocument.RootElement.GetProperty("class").EnumerateArray());
        var repeatedClass = Assert.Single(secondDocument.RootElement.GetProperty("class").EnumerateArray());
        Assert.Equal("Barbarian", classEntity.GetProperty("name").GetString());
        Assert.Equal("SRD3", classEntity.GetProperty("source").GetString());
        Assert.Equal(
            classEntity.GetProperty("uniqueId").GetString(),
            repeatedClass.GetProperty("uniqueId").GetString());

        const string monsters = """
            <html><body><h1>SRD Monsters</h1>
            Merfolk<br>SizeAndType: Medium-Size Humanoid (Aquatic)<br>HitDice: 1d8+1 (5)<br>
            Minotaur<br>SizeAndType: Large Monstrous Humanoid<br>HitDice: 6d8+12 (39)<br>
            </body></html>
            """;
        var monsterJson = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            monsters,
            "https://www.dragon.ee/30srd/monsters_m.html",
            "SRD3",
            out _);
        using var monsterDocument = JsonDocument.Parse(monsterJson);
        var names = monsterDocument.RootElement.GetProperty("monster")
            .EnumerateArray()
            .Select(value => value.GetProperty("name").GetString())
            .ToArray();
        Assert.Contains("Merfolk", names);
        Assert.Contains("Minotaur", names);
    }

    [Fact]
    public void HtmlIndexOnlyReturnsSameCorpusHtmlDocuments()
    {
        const string html = """
            <a href="feats.htm">Feats</a>
            <a href="spells/spellsa.html#acid-arrow">Spells</a>
            <a href="https://example.com/outside.htm">Outside</a>
            <a href="style.css">Styles</a>
            <a href="javascript:void(0)">Script</a>
            """;

        var references = LegacySrdDocumentInspector.ResolveHtmlIndexReferences(
            html,
            new Uri("https://www.dragon.ee/30srd/"));

        Assert.Equal(2, references.Count);
        Assert.Contains(references, value => value.AbsoluteUri == "https://www.dragon.ee/30srd/feats.htm");
        Assert.Contains(references, value => value.AbsoluteUri.StartsWith(
            "https://www.dragon.ee/30srd/spells/spellsa.html",
            StringComparison.Ordinal));
    }
}
