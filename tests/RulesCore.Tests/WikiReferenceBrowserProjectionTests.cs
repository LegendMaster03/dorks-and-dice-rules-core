using System.Text.Json;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Tests;

public sealed class WikiReferenceBrowserProjectionTests
{
    [Fact]
    public void SpellProjectionExposesStructuredFiveEReferenceFields()
    {
        using var document = JsonDocument.Parse("""
            {
              "name": "Arc Spark",
              "level": 1,
              "school": "V",
              "time": [{ "number": 1, "unit": "action" }],
              "range": { "type": "point", "distance": { "type": "feet", "amount": 60 } },
              "components": { "v": true, "s": true, "m": "a copper wire" },
              "duration": [{ "type": "timed", "duration": { "type": "minute", "amount": 1 }, "concentration": true }],
              "meta": { "ritual": true },
              "classes": { "fromClassList": [{ "name": "Wizard" }, { "name": "Sorcerer" }] }
            }
            """);

        var fields = ByKey(WikiReferenceBrowserProjection.Project("spell", document.RootElement));

        Assert.Equal("1st", fields["level"]);
        Assert.Equal("Evocation", fields["school"]);
        Assert.Equal("1 action", fields["castingTime"]);
        Assert.Equal("60 ft.", fields["range"]);
        Assert.Equal("V, S, M", fields["components"]);
        Assert.Contains("Concentration", fields["duration"]);
        Assert.Equal("Yes", fields["concentration"]);
        Assert.Equal("Yes", fields["ritual"]);
        Assert.Contains("Wizard", fields["spellList"]);
        Assert.Contains("Sorcerer", fields["spellList"]);
    }

    [Fact]
    public void ThreeXClassProjectionPreservesProgressionAndSkillPointSemantics()
    {
        using var document = JsonDocument.Parse("""
            {
              "name": "Arcane Adept",
              "hd": { "number": 1, "faces": 6 },
              "_rulesCore": {
                "character": {
                  "baseAttackProgression": "half",
                  "saveProgressions": {
                    "fortitude": "poor",
                    "reflex": "poor",
                    "will": "good"
                  },
                  "skillPointsPerLevel": 4,
                  "classSkills": ["Concentration", "Knowledge (arcana)", "Spellcraft"],
                  "spellcastingProfile": "dnd-3x"
                }
              }
            }
            """);

        var fields = ByKey(WikiReferenceBrowserProjection.Project("prestigeClass", document.RootElement));

        Assert.Equal("d6", fields["hitDie"]);
        Assert.Equal("half", fields["bab"]);
        Assert.Equal("poor", fields["fortitude"]);
        Assert.Equal("poor", fields["reflex"]);
        Assert.Equal("good", fields["will"]);
        Assert.Equal("4", fields["skillPoints"]);
        Assert.Contains("Spellcraft", fields["classSkills"]);
        Assert.Equal("dnd-3x", fields["spellcasting"]);
    }

    [Fact]
    public void RankedCompetencyProjectionUsesNormalizedRulesCoreMetadata()
    {
        using var document = JsonDocument.Parse("""
            {
              "name": "Craft (alchemy)",
              "_rulesCore": {
                "competency": {
                  "kind": "specialized-skill",
                  "familyName": "Craft",
                  "specialty": "alchemy",
                  "governingAbilityKey": "intelligence",
                  "supportsRanks": true,
                  "supportsClassSkillState": true,
                  "trainedOnly": false,
                  "armorCheckPenaltyApplies": false
                }
              }
            }
            """);

        var fields = ByKey(WikiReferenceBrowserProjection.Project("skill", document.RootElement));

        Assert.Equal("INT", fields["ability"]);
        Assert.Equal("Craft", fields["family"]);
        Assert.Equal("alchemy", fields["specialty"]);
        Assert.Equal("Yes", fields["ranks"]);
        Assert.Equal("Yes", fields["classSkill"]);
        Assert.Equal("No", fields["trainedOnly"]);
        Assert.Equal("No", fields["armorCheckPenalty"]);
    }

    [Fact]
    public void ItemAndFeatProjectionDoNotInventFiveEMechanics()
    {
        using var itemDocument = JsonDocument.Parse("""
            {
              "name": "Legacy Blade",
              "type": "weapon",
              "weight": 4,
              "value": 1200,
              "enhancementBonus": 2,
              "charges": 3
            }
            """);
        using var featDocument = JsonDocument.Parse("""
            {
              "name": "Exacting Study",
              "_rulesCore": {
                "character": {
                  "prerequisites": [{
                    "matchCount": 1,
                    "requirements": [{ "kind": "skill-ranks", "targetName": "Spellcraft", "operator": ">=", "value": 8 }]
                  }]
                }
              }
            }
            """);

        var itemFields = ByKey(WikiReferenceBrowserProjection.Project("item", itemDocument.RootElement));
        var featFields = ByKey(WikiReferenceBrowserProjection.Project("feat", featDocument.RootElement));

        Assert.Equal("12 gp", itemFields["value"]);
        Assert.Equal("4 lb.", itemFields["weight"]);
        Assert.Equal("2", itemFields["enhancement"]);
        Assert.Equal("3", itemFields["charges"]);
        Assert.DoesNotContain("rarity", itemFields.Keys);
        Assert.DoesNotContain("attunement", itemFields.Keys);
        Assert.Contains("Spellcraft", featFields["prerequisite"]);
    }

    private static Dictionary<string, string> ByKey(
        IReadOnlyList<RulesCore.Application.Rules.ResolvedRuleBrowserFieldView> fields) =>
        fields.ToDictionary(value => value.Key, value => value.Value, StringComparer.OrdinalIgnoreCase);
}
