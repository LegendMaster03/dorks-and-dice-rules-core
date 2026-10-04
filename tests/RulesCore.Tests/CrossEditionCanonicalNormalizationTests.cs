using System.Text;
using System.Text.Json;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Tests;

public sealed class CrossEditionCanonicalNormalizationTests
{
    [Fact]
    public void LegacySrdSpellPopulatesCanonicalSpellContractAndPreservesSourceFields()
    {
        const string source = """
            {
              "spell": [
                {
                  "name": "Fixture Bolt",
                  "source": "SRD35",
                  "uniqueId": "fixture-bolt",
                  "documentUri": "https://example.invalid/srd35/spells/fixture-bolt",
                  "body": "School: Evocation [Electricity]\nSubschool: Creation\nLevel: Sorcerer/Wizard 3, Warmage 3\nComponents: V, S, M, F\nMaterial Component: A copper wire\nFocus: A glass rod\nCasting Time: 1 standard action\nRange: Long (400 ft. + 40 ft./level)\nDuration: Instantaneous\nTarget: One creature\nSaving Throw: Reflex half\nSpell Resistance: Yes"
                }
              ]
            }
            """;

        var representation = new LegacySrdSourceFormatAdapter().TryRead(
            Artifact("srd-3-5e.json", source, "legacy-spell"));
        Assert.NotNull(representation);
        var native = Assert.Single(representation!.Records);
        var normalized = NormalizedSourceImportService.TranslateAndNormalizeRecord(
            representation,
            native);

        using var document = JsonDocument.Parse(normalized.ContentJson!);
        var rulesCore = document.RootElement.GetProperty("_rulesCore");
        var spell = rulesCore.GetProperty("spell");

        Assert.Equal("evocation", spell.GetProperty("school").GetString());
        Assert.Contains(
            spell.GetProperty("subschools").EnumerateArray(),
            value => value.GetString() == "Creation");
        Assert.Contains(
            spell.GetProperty("descriptors").EnumerateArray(),
            value => value.GetString() == "Electricity");
        Assert.Contains(
            spell.GetProperty("lists").EnumerateArray(),
            value => value.GetProperty("name").GetString() == "Wizard"
                && value.GetProperty("level").GetInt32() == 3);
        Assert.Contains(
            spell.GetProperty("lists").EnumerateArray(),
            value => value.GetProperty("name").GetString() == "Sorcerer"
                && value.GetProperty("level").GetInt32() == 3);
        Assert.Equal(
            "1 standard action",
            Assert.Single(spell.GetProperty("castingTime").EnumerateArray())
                .GetProperty("text").GetString());
        Assert.Equal(
            "Long (400 ft. + 40 ft./level)",
            spell.GetProperty("range").GetProperty("text").GetString());
        var components = spell.GetProperty("components");
        Assert.True(components.GetProperty("verbal").GetBoolean());
        Assert.True(components.GetProperty("somatic").GetBoolean());
        Assert.Equal("A copper wire", components.GetProperty("material").GetString());
        Assert.Equal("A glass rod", components.GetProperty("focus").GetString());
        Assert.Equal("Reflex half", spell.GetProperty("savingThrow").GetString());
        Assert.Equal("Yes", spell.GetProperty("spellResistance").GetString());

        Assert.Equal(
            "Evocation [Electricity]",
            rulesCore.GetProperty("threeX").GetProperty("fields").GetProperty("School").GetString());
    }

    [Fact]
    public void GeneratedFiveEToolsSpellLookupFeedsCanonicalSpellListsWithoutChangingCompanionOwnership()
    {
        const string spellJson = """
            {
              "spell": [
                {
                  "name": "Fireball",
                  "source": "XPHB",
                  "level": 3,
                  "school": "V",
                  "time": [{ "number": 1, "unit": "action" }],
                  "range": { "type": "point", "distance": { "type": "feet", "amount": 150 } },
                  "components": { "v": true, "s": true, "m": "a tiny ball of bat guano and sulfur" },
                  "duration": [{ "type": "instant" }],
                  "entries": ["A bright streak flashes."]
                }
              ]
            }
            """;
        const string lookupJson = """
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

        var representations = new FiveEToolsCompanionSourceFormatAdapter().TryReadMany(
        [
            Artifact("spells-xphb.json", spellJson, "five-spell"),
            Artifact(
                "data/generated/gendata-spell-source-lookup.json",
                lookupJson,
                "five-spell-lookup")
        ]);

        var spellRepresentation = Assert.Single(
            representations,
            value => value.Records.Any(record =>
                string.Equals(record.EntityType, "spell", StringComparison.OrdinalIgnoreCase)));
        var lookupRepresentation = Assert.Single(
            representations,
            value => value.CompanionContents.Any(companion =>
                companion.CompanionKind == FiveEToolsCompanionSourceFormatAdapter.SpellSourceLookupCompanionKind));

        Assert.Empty(spellRepresentation.CompanionContents);
        Assert.Single(spellRepresentation.NormalizationCompanionEvidence);
        Assert.Single(lookupRepresentation.CompanionContents);

        var record = Assert.Single(spellRepresentation.Records);
        var normalized = NormalizedSourceImportService.TranslateAndNormalizeRecord(
            spellRepresentation,
            record);
        using var document = JsonDocument.Parse(normalized.ContentJson!);
        var spell = document.RootElement.GetProperty("_rulesCore").GetProperty("spell");
        var lists = spell.GetProperty("lists").EnumerateArray().ToArray();

        Assert.Contains(lists, value =>
            value.GetProperty("name").GetString() == "Wizard"
            && value.GetProperty("level").GetInt32() == 3
            && value.GetProperty("evidence").GetString() == "generated-spell-source-lookup");
        Assert.Contains(lists, value =>
            value.GetProperty("name").GetString() == "Sorcerer"
            && value.GetProperty("level").GetInt32() == 3);
        Assert.Contains(lists, value =>
            value.GetProperty("name").GetString() == "Bard"
            && value.GetProperty("kind").GetString() == "class-variant");
    }

    [Fact]
    public void LegacySrdItemNormalizesValueWeightAndEditionSpecificMechanics()
    {
        const string source = """
            {
              "item": [
                {
                  "name": "Fixture Lens",
                  "source": "SRD35",
                  "uniqueId": "fixture-lens",
                  "documentUri": "https://example.invalid/srd35/items/fixture-lens",
                  "body": "Type: Wondrous Item\nPrice: 12,000 gp\nWeight: 1 lb.\nCharges: 3\nEnhancement Bonus: +2"
                }
              ]
            }
            """;

        var representation = new LegacySrdSourceFormatAdapter().TryRead(
            Artifact("srd-3-5e.json", source, "legacy-item"));
        Assert.NotNull(representation);
        var normalized = NormalizedSourceImportService.TranslateAndNormalizeRecord(
            representation!,
            Assert.Single(representation!.Records));

        using var document = JsonDocument.Parse(normalized.ContentJson!);
        var rulesCore = document.RootElement.GetProperty("_rulesCore");
        var item = rulesCore.GetProperty("item");

        Assert.Equal("Wondrous Item", item.GetProperty("type").GetString());
        Assert.Equal(1_200_000m, item.GetProperty("value").GetProperty("copperPieces").GetDecimal());
        Assert.Equal(1m, item.GetProperty("weight").GetProperty("pounds").GetDecimal());
        Assert.Equal(3, item.GetProperty("charges").GetInt32());
        Assert.Equal(2, item.GetProperty("enhancementBonus").GetInt32());
        Assert.False(item.TryGetProperty("rarity", out _));
        Assert.False(item.TryGetProperty("attunement", out _));
        Assert.Equal(
            "12,000 gp",
            rulesCore.GetProperty("threeX").GetProperty("fields").GetProperty("Price").GetString());
    }

    [Fact]
    public void LegacySrdClassAndPrestigeClassShareCanonicalCharacterPaths()
    {
        const string source = """
            {
              "class": [
                {
                  "name": "Arcane Adept",
                  "source": "SRD35",
                  "uniqueId": "arcane-adept",
                  "documentUri": "https://example.invalid/srd35/classes/arcane-adept",
                  "body": "Hit Die: d6\nBase Attack Progression: Poor\nFortitude Save: Poor\nReflex Save: Poor\nWill Save: Good\nSkill Points at Each Level: 4 + Int modifier\nClass Skills: Concentration, Knowledge (arcana), Spellcraft"
                }
              ],
              "prestigeClass": [
                {
                  "name": "Exacting Savant",
                  "source": "SRD35",
                  "uniqueId": "exacting-savant",
                  "documentUri": "https://example.invalid/srd35/prestige/exacting-savant",
                  "body": "Hit Die: d8\nRequirements: Spellcraft 8 ranks\nSkill Points at Each Level: 2 + Int modifier\nClass Skills: Knowledge (arcana), Spellcraft"
                }
              ]
            }
            """;

        var representation = new LegacySrdSourceFormatAdapter().TryRead(
            Artifact("srd-3-5e.json", source, "legacy-class"));
        Assert.NotNull(representation);

        var normalized = representation!.Records
            .Select(record => NormalizedSourceImportService.TranslateAndNormalizeRecord(
                representation,
                record))
            .ToDictionary(value => value.EntityType, StringComparer.OrdinalIgnoreCase);

        using var classDocument = JsonDocument.Parse(normalized["class"].ContentJson!);
        var classCharacter = classDocument.RootElement
            .GetProperty("_rulesCore")
            .GetProperty("character");
        Assert.Equal(6, classCharacter.GetProperty("hitDie").GetProperty("faces").GetInt32());
        Assert.Equal("half", classCharacter.GetProperty("baseAttackProgression").GetString());
        Assert.Equal("poor", classCharacter.GetProperty("saveProgressions").GetProperty("fortitude").GetString());
        Assert.Equal("good", classCharacter.GetProperty("saveProgressions").GetProperty("will").GetString());
        Assert.Equal(4, classCharacter.GetProperty("skillPointsPerLevel").GetInt32());
        Assert.Contains(
            classCharacter.GetProperty("classSkills").EnumerateArray(),
            value => value.GetString() == "Spellcraft");

        using var prestigeDocument = JsonDocument.Parse(normalized["prestigeClass"].ContentJson!);
        var prestigeCharacter = prestigeDocument.RootElement
            .GetProperty("_rulesCore")
            .GetProperty("character");
        Assert.Equal(8, prestigeCharacter.GetProperty("hitDie").GetProperty("faces").GetInt32());
        Assert.Equal(2, prestigeCharacter.GetProperty("skillPointsPerLevel").GetInt32());
        Assert.Equal("Spellcraft 8 ranks", prestigeCharacter.GetProperty("prerequisiteText").GetString());
    }

    [Fact]
    public void PcGenClassRetainsExistingStructuredProgressionAndAddsCanonicalHitDie()
    {
        var campaign = Artifact(
            "example.pcc",
            """
            CAMPAIGN:Class Mechanics
            GAMEMODE:35e
            SOURCELONG:Class Mechanics
            SOURCESHORT:CLS
            CLASS:example_classes.lst
            """,
            "pcgen-class-campaign");
        var classes = Artifact(
            "example_classes.lst",
            string.Join('\n',
            [
                "CLASS:Example Class\tHD:8\tTYPE:Base.PC\tMAXLEVEL:20\tBONUS:COMBAT|BASEAB|classlevel(\"APPLIEDAS=NONEPIC\")*3/4\tBONUS:SAVE|BASE.Fortitude,BASE.Will|classlevel(\"APPLIEDAS=NONEPIC\")/3\tBONUS:SAVE|BASE.Reflex|classlevel(\"APPLIEDAS=NONEPIC\")/2+2",
                "CLASS:Example Class\tSTARTSKILLPTS:4\tCSKILL:Climb|Jump|TYPE.Craft\tSPELLSTAT:INT"
            ]),
            "pcgen-class-list");

        var representation = new PcGenSourceFormatAdapter()
            .TryReadMany([campaign, classes])
            .Single(value => value.Artifact.FileName == "example_classes.lst");
        var normalized = NormalizedSourceImportService.TranslateAndNormalizeRecord(
            representation,
            Assert.Single(representation.Records));

        using var document = JsonDocument.Parse(normalized.ContentJson!);
        var character = document.RootElement.GetProperty("_rulesCore").GetProperty("character");
        Assert.Equal(8, character.GetProperty("hitDie").GetProperty("faces").GetInt32());
        Assert.Equal("three-quarters", character.GetProperty("baseAttackProgression").GetString());
        Assert.Equal(4, character.GetProperty("skillPointsPerLevel").GetInt32());
        Assert.Equal("intelligence", character.GetProperty("spellcastingAbility").GetString());
        Assert.Contains(
            document.RootElement.GetProperty("_rulesCore")
                .GetProperty("pcgen")
                .GetProperty("unmappedSegments")
                .EnumerateArray(),
            value => value.GetProperty("tag").GetString() == "BONUS");
    }

    [Fact]
    public void LegacyRankedSkillKeepsCompetencySemanticsAndRawEvidence()
    {
        const string source = """
            {
              "skill": [
                {
                  "name": "Craft (alchemy)",
                  "source": "SRD35",
                  "uniqueId": "craft-alchemy",
                  "documentUri": "https://example.invalid/srd35/skills/craft-alchemy",
                  "body": "Key Ability: Int\nTrained Only: No\nArmor Check Penalty: No"
                }
              ]
            }
            """;

        var representation = new LegacySrdSourceFormatAdapter().TryRead(
            Artifact("srd-3-5e.json", source, "legacy-skill"));
        Assert.NotNull(representation);
        var normalized = NormalizedSourceImportService.TranslateAndNormalizeRecord(
            representation!,
            Assert.Single(representation!.Records));

        using var document = JsonDocument.Parse(normalized.ContentJson!);
        var rulesCore = document.RootElement.GetProperty("_rulesCore");
        var competency = rulesCore.GetProperty("competency");
        Assert.Equal("Craft", competency.GetProperty("familyName").GetString());
        Assert.Equal("alchemy", competency.GetProperty("specialty").GetString());
        Assert.True(competency.GetProperty("supportsRanks").GetBoolean());
        Assert.True(competency.GetProperty("supportsClassSkillState").GetBoolean());
        Assert.Equal("intelligence", competency.GetProperty("governingAbilityKey").GetString());
        Assert.True(rulesCore.GetProperty("threeX").TryGetProperty("fields", out _));
    }

    [Fact]
    public void ModernAndLegacyItemsUseTheSameCanonicalTypePath()
    {
        const string modern = """
            {
              "item": [
                {
                  "name": "Modern Lens",
                  "source": "XDMG",
                  "type": "W",
                  "rarity": "rare",
                  "reqAttune": true,
                  "weight": 1,
                  "value": 1200000,
                  "entries": ["A modern fixture."]
                }
              ]
            }
            """;
        const string legacy = """
            {
              "item": [
                {
                  "name": "Legacy Lens",
                  "source": "SRD35",
                  "uniqueId": "legacy-lens",
                  "documentUri": "https://example.invalid/srd35/items/legacy-lens",
                  "body": "Type: Wondrous Item\nPrice: 12,000 gp\nWeight: 1 lb."
                }
              ]
            }
            """;

        var modernRepresentation = new FiveEToolsSourceFormatAdapter().TryRead(
            Artifact("items-xdmg.json", modern, "modern-item"));
        var legacyRepresentation = new LegacySrdSourceFormatAdapter().TryRead(
            Artifact("srd-3-5e.json", legacy, "legacy-item-comparison"));
        Assert.NotNull(modernRepresentation);
        Assert.NotNull(legacyRepresentation);

        var modernNormalized = NormalizedSourceImportService.TranslateAndNormalizeRecord(
            modernRepresentation!,
            Assert.Single(modernRepresentation!.Records));
        var legacyNormalized = NormalizedSourceImportService.TranslateAndNormalizeRecord(
            legacyRepresentation!,
            Assert.Single(legacyRepresentation!.Records));

        using var modernDocument = JsonDocument.Parse(modernNormalized.ContentJson!);
        using var legacyDocument = JsonDocument.Parse(legacyNormalized.ContentJson!);
        var modernItem = modernDocument.RootElement.GetProperty("_rulesCore").GetProperty("item");
        var legacyItem = legacyDocument.RootElement.GetProperty("_rulesCore").GetProperty("item");

        Assert.Equal("Wondrous Item", modernItem.GetProperty("type").GetString());
        Assert.Equal(
            modernItem.GetProperty("type").GetString(),
            legacyItem.GetProperty("type").GetString());
        Assert.Equal(
            modernItem.GetProperty("value").GetProperty("copperPieces").GetDecimal(),
            legacyItem.GetProperty("value").GetProperty("copperPieces").GetDecimal());
        Assert.True(modernItem.GetProperty("attunement").GetBoolean());
        Assert.False(legacyItem.TryGetProperty("attunement", out _));
    }

    [Fact]
    public void RaceMechanicsProjectIntoCanonicalSpeciesContractWithoutMonsterFabrication()
    {
        var campaign = Artifact(
            "rsrd.pcc",
            """
            CAMPAIGN:3.5 RSRD
            GAMEMODE:35e
            PUBNAMELONG:Wizards of the Coast
            SOURCELONG:Revised (v.3.5) System Reference Document
            SOURCESHORT:RSRD
            SOURCEDATE:2003-07
            RACE:rsrd_races.lst
            """,
            "pcgen-race-campaign");
        var races = Artifact(
            "rsrd_races.lst",
            "Goblin\tFAVCLASS:Rogue\tSTARTFEATS:1\tSIZE:S\tMOVE:Walk,30\tBONUS:STAT|STR|-2\tBONUS:STAT|DEX|2\tBONUS:STAT|CHA|-2\tRACETYPE:Humanoid\tRACESUBTYPE:Goblinoid",
            "pcgen-race-list");

        var representation = new PcGenSourceFormatAdapter()
            .TryReadMany([campaign, races])
            .Single(value => value.Artifact.FileName == "rsrd_races.lst");
        var normalized = NormalizedSourceImportService.TranslateAndNormalizeRecord(
            representation,
            Assert.Single(representation.Records));

        using var document = JsonDocument.Parse(normalized.ContentJson!);
        var root = document.RootElement;
        var species = root.GetProperty("_rulesCore").GetProperty("species");
        Assert.Contains(species.GetProperty("sizes").EnumerateArray(), value => value.GetString() == "Small");
        Assert.Equal(30m, species.GetProperty("speed").GetProperty("walk").GetDecimal());
        Assert.Equal(-2, species.GetProperty("abilityAdjustments").GetProperty("str").GetInt32());
        Assert.Equal(2, species.GetProperty("abilityAdjustments").GetProperty("dex").GetInt32());
        Assert.False(root.TryGetProperty("cr", out _));
        Assert.False(root.TryGetProperty("hp", out _));
    }

    private static SourceRepresentationArtifact Artifact(
        string fileName,
        string content,
        string identity) =>
        new(
            fileName,
            Encoding.UTF8.GetBytes(content),
            $"test:phase-4-1b:{identity}");
}
