using System.Text.Json;
using RulesCore.Application.Sources;

namespace RulesCore.Tests;

public sealed class LegacySrdCorpusClassificationTests
{
    [Fact]
    public void RacesAndSkillsNormalizeToCrossEditionNames()
    {
        const string races = """
            # RACES

            ## Favored Class
            General favored-class rules.

            ## Race and Languages
            General language rules.

            ## Small Characters
            General size rules.

            ## Humans
            Human racial traits.

            ## Dwarves
            Dwarf racial traits.
            """;

        var raceJson = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            races,
            "https://raw.githubusercontent.com/olimot/srd-v3.5-md/main/basic-rules-and-legal/races.md",
            "SRD35",
            out _);
        using var raceDocument = JsonDocument.Parse(raceJson);
        var raceNames = raceDocument.RootElement.GetProperty("race")
            .EnumerateArray()
            .Select(value => value.GetProperty("name").GetString())
            .ToArray();
        Assert.Equal(new[] { "Human", "Dwarf" }, raceNames);

        const string skills = """
            # SKILLS II

            ## Using Skills
            General rules.

            ## Heal <small>(Wis)</small>
            **Check:** Treat injuries.

            ## Hide <small>(Dex; Armor Check Penalty)</small>
            **Check:** Avoid being seen.
            """;

        var skillJson = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            skills,
            "https://raw.githubusercontent.com/olimot/srd-v3.5-md/main/basic-rules-and-legal/skills-ii.md",
            "SRD35",
            out _);
        using var skillDocument = JsonDocument.Parse(skillJson);
        var skillNames = skillDocument.RootElement.GetProperty("skill")
            .EnumerateArray()
            .Select(value => value.GetProperty("name").GetString())
            .ToArray();
        Assert.Equal(new[] { "Heal", "Hide" }, skillNames);
    }

    [Fact]
    public void PsionicClassSectionsDoNotBecomeFalseClasses()
    {
        const string markdown = """
            # PSIONIC CLASSES

            ## The Power Point Reserve
            General reserve rules.

            ## Abilities and Manifesters
            General manifester rules.

            ## Random Starting Gold
            Starting wealth table.

            ## Psion
            **Alignment:** Any.
            **Hit Die:** d4.

            ## Psychic Warrior
            **Alignment:** Any.
            **Hit Die:** d8.
            """;

        var json = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            markdown,
            "https://raw.githubusercontent.com/olimot/srd-v3.5-md/main/psionics/psionic-classes.md",
            "SRD35",
            out _);
        using var document = JsonDocument.Parse(json);
        var classNames = document.RootElement.GetProperty("class")
            .EnumerateArray()
            .Select(value => value.GetProperty("name").GetString())
            .ToArray();

        Assert.Equal(new[] { "Psion", "Psychic Warrior" }, classNames);
    }

    [Fact]
    public void PsionicPowersAndDivineDomainsUseDedicatedEntityTypes()
    {
        const string powers = """
            # PSIONIC POWERS (A-C)

            ## Adapt Body
            Psychometabolism
            **Level:** Psion/wilder 5
            **Manifesting Time:** 1 standard action
            **Power Points:** 9

            ## Affinity Field
            Psychometabolism
            **Level:** Psion/wilder 9
            **Manifesting Time:** 1 standard action
            **Power Points:** 17
            """;

        var powerJson = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            powers,
            "https://raw.githubusercontent.com/olimot/srd-v3.5-md/main/psionics/psionic-powers-a-c.md",
            "SRD35",
            out _);
        using var powerDocument = JsonDocument.Parse(powerJson);
        Assert.Equal(
            new[] { "Adapt Body", "Affinity Field" },
            powerDocument.RootElement.GetProperty("power")
                .EnumerateArray()
                .Select(value => value.GetProperty("name").GetString())
                .ToArray());

        const string domains = """
            # DOMAINS AND SPELLS

            ## Artifice Domain
            **Granted Power:** Gain a bonus on Craft checks.

            ### Artifice Domain Spells
            Spell list.

            ## Charm Domain
            **Granted Power:** Boost Charisma once per day.
            """;

        var domainJson = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            domains,
            "https://raw.githubusercontent.com/olimot/srd-v3.5-md/main/divine/divine-domains-and-spells.md",
            "SRD35",
            out _);
        using var domainDocument = JsonDocument.Parse(domainJson);
        Assert.Equal(
            new[] { "Artifice Domain", "Charm Domain" },
            domainDocument.RootElement.GetProperty("domain")
                .EnumerateArray()
                .Select(value => value.GetProperty("name").GetString())
                .ToArray());
    }

    [Fact]
    public void MagicItemDocumentsExtractHeadingAndPricedStrongLabelEntries()
    {
        const string markdown = """
            # MAGIC ITEMS

            ## Wondrous Items
            General rules.

            ### Boots of Elvenkind
            These boots help the wearer move quietly.
            Faint illusion; CL 5th; Price 2,500 gp.

            **Activation:** Usually use activated.

            **Gauntlets of Ogre Power:** These gauntlets grant great strength.
            Faint transmutation; CL 6th; Craft Wondrous Item; Price 4,000 gp.
            """;

        var markdownJson = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            markdown,
            "https://raw.githubusercontent.com/olimot/srd-v3.5-md/main/magic-items/magic-items-v-wondrous-items.md",
            "SRD35",
            out _);
        using var markdownDocument = JsonDocument.Parse(markdownJson);
        var markdownItems = markdownDocument.RootElement.GetProperty("item")
            .EnumerateArray()
            .Select(value => value.GetProperty("name").GetString())
            .ToArray();
        Assert.Contains("Boots of Elvenkind", markdownItems);
        Assert.Contains("Gauntlets of Ogre Power", markdownItems);
        Assert.DoesNotContain("Activation", markdownItems);

        const string html = """
            <html><body>
            <h1>Magic Items (Wondrous Items)</h1>
            <h2>Wondrous Items</h2>
            <h3>Boots of Elvenkind</h3>
            <p>These boots help the wearer move quietly. Market Price: 2,000 gp.</p>
            <p><strong>Gauntlets of Ogre Power:</strong> These gauntlets grant great strength.</p>
            <p>Caster Level: 6th; Market Price: 4,000 gp.</p>
            </body></html>
            """;

        var htmlJson = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            html,
            "https://www.dragon.ee/30srd/wondrous_items.htm",
            "SRD3",
            out _);
        using var htmlDocument = JsonDocument.Parse(htmlJson);
        var htmlItems = htmlDocument.RootElement.GetProperty("item")
            .EnumerateArray()
            .Select(value => value.GetProperty("name").GetString())
            .ToArray();
        Assert.Contains("Boots of Elvenkind", htmlItems);
        Assert.Contains("Gauntlets of Ogre Power", htmlItems);
    }

    [Fact]
    public void SpellListPagesDoNotCreateFalseSpellEntities()
    {
        const string spellList = """
            <html><body>
            <h1>Bard Spells</h1>
            <h2>0-Level Bard Spells</h2>
            <p>Dancing lights, daze, detect magic.</p>
            </body></html>
            """;
        var listJson = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            spellList,
            "https://www.dragon.ee/30srd/bardspells.htm",
            "SRD3",
            out _);
        using var listDocument = JsonDocument.Parse(listJson);
        Assert.False(listDocument.RootElement.TryGetProperty("spell", out _));

        const string spellDescription = """
            <html><body>
            <h1>Spells A</h1>
            <h2>Acid Arrow</h2>
            <p>Conjuration (Creation) [Acid]</p>
            <p>Level: Sor/Wiz 2</p>
            <p>Components: V, S, M</p>
            <p>Range: Long</p>
            </body></html>
            """;
        var descriptionJson = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            spellDescription,
            "https://www.dragon.ee/30srd/spellsa.htm",
            "SRD3",
            out _);
        using var descriptionDocument = JsonDocument.Parse(descriptionJson);
        Assert.Equal(
            "Acid Arrow",
            Assert.Single(descriptionDocument.RootElement.GetProperty("spell").EnumerateArray())
                .GetProperty("name").GetString());
    }
    [Fact]
    public void MonsterIntroductionsAndFamiliesDoNotBecomeFalseMonsters()
    {
        const string monsters = """
            # MONSTERS (INTRO-A)

            ## Reading the Entries
            General monster-format guidance.

            ## Achaierai
            <table><tr><th>Hit Dice:</th><td>6d8+12</td></tr><tr><th>Armor Class:</th><td>20</td></tr><tr><th>Challenge Rating:</th><td>5</td></tr></table>

            ### Combat
            Combat tactics.

            ## Angel
            Angels are a family of celestials.

            ### Angel, Astral Deva
            <table><tr><th>Hit Dice:</th><td>12d8+48</td></tr><tr><th>Armor Class:</th><td>29</td></tr><tr><th>Challenge Rating:</th><td>14</td></tr></table>

            #### Combat
            Combat tactics.
            """;

        var json = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            monsters,
            "https://raw.githubusercontent.com/olimot/srd-v3.5-md/main/monsters/monsters-intro-a.md",
            "SRD35",
            out _);
        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.GetProperty("monster")
            .EnumerateArray()
            .Select(value => value.GetProperty("name").GetString())
            .ToArray();

        Assert.Equal(new[] { "Achaierai", "Angel, Astral Deva" }, names);
        Assert.DoesNotContain("Reading the Entries", names);
        Assert.DoesNotContain("Angel", names);
    }

    [Fact]
    public void EpicFeatSectionsAndDivineAbilityPlaceholdersAreNotFalseEntities()
    {
        const string epicFeats = """
            # EPIC FEATS

            ## Acquiring Epic Feats
            General rules for gaining epic feats.

            ### Divine Feats
            General category text.

            ## Feats

            ### Armor Skin <small>[Epic]</small>
            **Benefit:** The character gains a +1 natural armor bonus.
            """;

        var featJson = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            epicFeats,
            "https://raw.githubusercontent.com/olimot/srd-v3.5-md/main/epic/epic-feats.md",
            "SRD35",
            out _);
        using var featDocument = JsonDocument.Parse(featJson);
        var featNames = featDocument.RootElement.GetProperty("feat")
            .EnumerateArray()
            .Select(value => value.GetProperty("name").GetString())
            .ToArray();
        Assert.Equal(new[] { "Armor Skin" }, featNames);

        const string divine = """
            # SALIENT DIVINE ABILITIES

            ## Salient Divine Ability Descriptions
            Format guidance.

            ## Ability Name
            **Prerequisite:** Divine rank 1.
            **Benefit:** Placeholder benefit text.

            ## Alter Form
            **Prerequisite:** Alter Size salient divine ability.
            **Benefit:** The deity can alter its form.
            **Suggested Portfolio Elements:** Nature.
            """;

        var divineJson = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            divine,
            "https://raw.githubusercontent.com/olimot/srd-v3.5-md/main/divine/divine-abilities-and-feats.md",
            "SRD35",
            out _);
        using var divineDocument = JsonDocument.Parse(divineJson);
        var abilityNames = divineDocument.RootElement.GetProperty("divineAbility")
            .EnumerateArray()
            .Select(value => value.GetProperty("name").GetString())
            .ToArray();
        Assert.Equal(new[] { "Alter Form" }, abilityNames);
        Assert.False(divineDocument.RootElement.TryGetProperty("feat", out _));
    }

    [Fact]
    public void MagicItemPriceModifiersDoNotMasqueradeAsStandaloneItems()
    {
        const string markdown = """
            # PSIONIC ITEMS

            **Psychic:** A psychic weapon scales with its wielder.
            Strong clairsentience; ML 17th; Price +35,000 gp.

            **Cognizance Crystal:** Stores power points for later use.
            Faint psychokinesis; ML 1st; Price 1,000 gp.
            """;

        var json = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            markdown,
            "https://raw.githubusercontent.com/olimot/srd-v3.5-md/main/psionics/psionic-items.md",
            "SRD35",
            out _);
        using var document = JsonDocument.Parse(json);
        var itemNames = document.RootElement.GetProperty("item")
            .EnumerateArray()
            .Select(value => value.GetProperty("name").GetString())
            .ToArray();

        Assert.Equal(new[] { "Cognizance Crystal" }, itemNames);
        Assert.DoesNotContain("Psychic", itemNames);
    }

}
