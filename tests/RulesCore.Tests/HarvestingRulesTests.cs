using RulesCore.Domain.Rules;

namespace RulesCore.Tests;

public sealed class HarvestingRulesTests
{
    [Fact]
    public void PublicCreatureTypeCatalogUsesUniversalCompetencyConcepts()
    {
        var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["aberration"] = "competency.arcana",
            ["beast"] = "competency.survival",
            ["celestial"] = "competency.religion",
            ["construct"] = "competency.investigation",
            ["dragon"] = "competency.survival",
            ["elemental"] = "competency.arcana",
            ["fey"] = "competency.arcana",
            ["fiend"] = "competency.religion",
            ["giant"] = "competency.medicine",
            ["humanoid"] = "competency.medicine",
            ["monstrosity"] = "competency.survival",
            ["ooze"] = "competency.nature",
            ["plant"] = "competency.nature",
            ["undead"] = "competency.medicine"
        };

        Assert.Equal(expected.Count, KnownHarvestingRules.CreatureTypes.Count);
        foreach (var (creatureType, competencyKey) in expected)
        {
            var definition = KnownHarvestingRules.FindCreatureType(creatureType);
            Assert.NotNull(definition);
            Assert.Equal(competencyKey, definition!.CompetencyKey);
            Assert.NotEmpty(definition.BaseComponents);
            Assert.All(definition.BaseComponents, component => Assert.True(component.ComponentDc > 0));
        }
    }

    [Fact]
    public void ProcedureContractKeepsWorkflowSemanticsInRulesCore()
    {
        Assert.Equal("check.harvesting.assessment", KnownHarvestingRules.AssessmentMechanicKey);
        Assert.Equal("intelligence", KnownHarvestingRules.AssessmentAbilityKey);
        Assert.Equal("check.harvesting.carving", KnownHarvestingRules.CarvingMechanicKey);
        Assert.Equal("dexterity", KnownHarvestingRules.CarvingAbilityKey);
        Assert.Equal("check.harvesting.total", KnownHarvestingRules.TotalMechanicKey);
        Assert.Equal("cumulative-in-order", KnownHarvestingRules.ComponentDcAggregation);
        Assert.Equal("ordered-prefix", KnownHarvestingRules.AwardMode);
        Assert.Equal(0, KnownHarvestingRules.HelperLimitsByCreatureSize["Tiny"]);
        Assert.Equal(2, KnownHarvestingRules.HelperLimitsByCreatureSize["Medium"]);
        Assert.Equal(10, KnownHarvestingRules.HelperLimitsByCreatureSize["Gargantuan"]);
    }

    [Fact]
    public void DefaultHarvestablesRetainSourceComponentDcWithoutInventingCreatureSpecificParts()
    {
        var dragon = Required("dragon");
        Assert.Contains(
            dragon.BaseComponents,
            value => value.Key == "breath-sac" && value.ComponentDc == 25);
        Assert.Contains(
            dragon.BaseComponents,
            value => value.Key == "pouch-of-scales" && value.ComponentDc == 15);

        var ooze = Required("ooze");
        Assert.Contains(
            ooze.BaseComponents,
            value => value.Key == "phial-of-acid" && value.ComponentDc == 5);
        Assert.Contains(
            ooze.BaseComponents,
            value => value.Key == "membrane" && value.ComponentDc == 20);
    }

    [Fact]
    public void CreatureOverridesCanRemoveDefaultsModifyDefaultsAndAddUniqueParts()
    {
        var resolved = KnownHarvestingRules.ResolveComponents(
            Required("dragon"),
            new HarvestingTableEdits(
                ["egg", "pouch-of-claws"],
                [
                    new HarvestingComponentEdit(
                        "pouch-of-scales",
                        DisplayName: "Ancient scales",
                        ComponentDc: 20,
                        Quantity: 3),
                    new HarvestingComponentEdit(
                        "storm-gland",
                        DisplayName: "Storm gland",
                        ComponentDc: 25,
                        Quantity: 1)
                ]));

        Assert.DoesNotContain(resolved, value => value.Key == "egg");
        Assert.DoesNotContain(resolved, value => value.Key == "pouch-of-claws");

        var scales = Assert.Single(resolved, value => value.Key == "pouch-of-scales");
        Assert.Equal("Ancient scales", scales.DisplayName);
        Assert.Equal(20, scales.ComponentDc);
        Assert.Equal(3, scales.Quantity);
        Assert.Equal(HarvestingComponentOrigins.Creature, scales.Origin);

        var unique = Assert.Single(resolved, value => value.Key == "storm-gland");
        Assert.Equal(25, unique.ComponentDc);
        Assert.Equal(HarvestingComponentOrigins.Creature, unique.Origin);
    }

    [Fact]
    public void ManualEditsApplyAfterCreatureOverridesWithoutMutatingTheBaseCatalog()
    {
        var dragon = Required("dragon");
        var resolved = KnownHarvestingRules.ResolveComponents(
            dragon,
            new HarvestingTableEdits(
                [],
                [new HarvestingComponentEdit("heart", ComponentDc: 25)]),
            new HarvestingTableEdits(
                ["breath-sac"],
                [
                    new HarvestingComponentEdit(
                        "heart",
                        DisplayName: "Damaged heart",
                        ComponentDc: 10),
                    new HarvestingComponentEdit(
                        "custom-part",
                        DisplayName: "Custom part",
                        ComponentDc: 15)
                ]));

        var heart = Assert.Single(resolved, value => value.Key == "heart");
        Assert.Equal("Damaged heart", heart.DisplayName);
        Assert.Equal(10, heart.ComponentDc);
        Assert.Equal(HarvestingComponentOrigins.Manual, heart.Origin);
        Assert.DoesNotContain(resolved, value => value.Key == "breath-sac");
        Assert.Contains(resolved, value =>
            value.Key == "custom-part"
            && value.Origin == HarvestingComponentOrigins.Manual);

        Assert.Equal(
            20,
            Assert.Single(dragon.BaseComponents, value => value.Key == "heart").ComponentDc);
        Assert.Contains(dragon.BaseComponents, value => value.Key == "breath-sac");
    }

    [Fact]
    public void NewHarvestableRequiresNameAndComponentDc()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            KnownHarvestingRules.ResolveComponents(
                Required("beast"),
                manualEdits: new HarvestingTableEdits(
                    [],
                    [new HarvestingComponentEdit("unique-part")])));

        Assert.Contains("displayName", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("componentDc", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HarvestingCreatureTypeDefinition Required(string key) =>
        KnownHarvestingRules.FindCreatureType(key)
        ?? throw new InvalidOperationException($"Missing harvesting creature type '{key}'.");
}
