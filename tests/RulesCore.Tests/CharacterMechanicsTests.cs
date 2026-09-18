using RulesCore.Domain.Rules;

namespace RulesCore.Tests;

public sealed class CharacterMechanicsTests
{
    [Fact]
    public void KnownCatalogSeparatesGenericThreeXAndLootTavernApplicability()
    {
        var generic = Required("check.competency");
        Assert.Equal(CharacterMechanicApplicabilityKinds.Always, generic.Applicability.Kind);

        var fortitude = Required("save.fortitude");
        Assert.Equal(CharacterMechanicApplicabilityKinds.RulesetEdition, fortitude.Applicability.Kind);
        Assert.Equal(new[] { "3e", "3.5e" }, fortitude.Applicability.EditionKeys);

        var harvesting = Required("check.harvesting.total");
        Assert.Equal(CharacterMechanicApplicabilityKinds.AccessibleSource, harvesting.Applicability.Kind);
        Assert.Equal(KnownCharacterMechanics.LootTavernPackageKey, harvesting.Applicability.SourcePackageKey);
        Assert.NotNull(harvesting.Source);
        Assert.True(harvesting.Source!.PresentationRequired);
        Assert.True(harvesting.Source.ReferenceLinkRequired);
        Assert.Equal("Loot Tavern Free Releases", harvesting.Source.PackageDisplayName);
        Assert.Equal(KnownCharacterMechanics.LootTavernReferenceKey, harvesting.Source.WorkKey);
        Assert.Equal("Harvesting & Crafting Lite", harvesting.Source.WorkDisplayName);
        Assert.Equal("5e", harvesting.Source.GameEdition);
        Assert.Equal(new DateOnly(2024, 7, 3), harvesting.Source.PublicationDate);
        Assert.Equal(
            "https://www.patreon.com/LootTavern/posts/helianas-and-to-107406117",
            harvesting.Source.ReferenceUri);
    }

    [Fact]
    public void GenericCompetencyCheckUsesResolvedInputsWithoutOwningCharacterState()
    {
        var definition = Required("check.competency");
        var result = CharacterMechanicEvaluator.Evaluate(
            definition,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["d20Roll"] = 12,
                ["abilityModifier"] = 3,
                ["competencyModifier"] = 2,
                ["targetDc"] = 17
            },
            new Dictionary<string, bool>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["abilityKey"] = "intelligence",
                ["competencyKey"] = "skill.arcana"
            });

        Assert.Equal(17, result.Value);
        Assert.Equal(17, result.Target);
        Assert.True(result.MeetsTarget);
        Assert.True(result.RequirementsSatisfied);
    }

    [Fact]
    public void HarvestingCombinesComponentResultsAndReportsSameActorRollRule()
    {
        var definition = Required("check.harvesting.total");
        var result = CharacterMechanicEvaluator.Evaluate(
            definition,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["assessmentResult"] = 14,
                ["carvingResult"] = 12,
                ["targetDc"] = 25
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["sameActor"] = true
            },
            new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Equal(26, result.Value);
        Assert.True(result.MeetsTarget);
        var rollRule = Assert.Single(result.AppliedRollRules);
        Assert.Equal(CharacterMechanicRollModes.Disadvantage, rollRule.RollMode);
        Assert.Equal(
            new[] { "check.harvesting.assessment", "check.harvesting.carving" },
            rollRule.TargetMechanicKeys);
    }

    [Fact]
    public void ManufacturingDoesNotTurnMissingToolProficiencyIntoMissingProficiencyData()
    {
        var definition = Required("check.crafting.manufacturing");
        var result = CharacterMechanicEvaluator.Evaluate(
            definition,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["d20Roll"] = 11,
                ["abilityModifier"] = 2,
                ["proficiencyModifier"] = 0
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["hasToolProficiency"] = false
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["toolKey"] = "tool.example",
                ["abilityKey"] = "dexterity"
            });

        Assert.Equal(13, result.Value);
        Assert.True(result.RequirementsSatisfied);
        var rollRule = Assert.Single(result.AppliedRollRules);
        Assert.Equal(CharacterMechanicRollModes.Disadvantage, rollRule.RollMode);
    }

    [Fact]
    public void EnchantingReportsSpellcastingRequirementSeparatelyFromCheckArithmetic()
    {
        var definition = Required("check.crafting.enchanting");
        var result = CharacterMechanicEvaluator.Evaluate(
            definition,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["d20Roll"] = 10,
                ["spellcastingAbilityModifier"] = 4,
                ["competencyModifier"] = 3
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["hasSpellcastingAbility"] = false
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["creatureTypeCompetencyKey"] = "skill.example"
            });

        Assert.Equal(17, result.Value);
        Assert.False(result.RequirementsSatisfied);
        Assert.Equal(new[] { "hasSpellcastingAbility" }, result.UnsatisfiedRequirementKeys);
    }



    [Fact]
    public void GrappleUsesTheThreeXSpecificSizeModifierInput()
    {
        var definition = Required("combat.grapple");

        Assert.Contains(
            definition.Inputs,
            value => value.Key == "grappleSizeModifier"
                && value.ValueKind == CharacterMechanicInputValueKinds.Integer);
        Assert.DoesNotContain(
            definition.Inputs,
            value => value.Key == "sizeModifier");
    }

    [Fact]
    public void SkillRanksIdentifyTheCompetencyWhoseRanksAreBeingSupplied()
    {
        var definition = Required("competency.skill-ranks");

        Assert.Contains(
            definition.Inputs,
            value => value.Key == "competencyKey"
                && value.ValueKind == CharacterMechanicInputValueKinds.String
                && value.Origin == CharacterMechanicInputOrigins.SourceInput
                && value.Required);
        Assert.Contains(
            definition.Inputs,
            value => value.Key == "value"
                && value.ValueKind == CharacterMechanicInputValueKinds.Integer
                && value.Origin == CharacterMechanicInputOrigins.CharacterState
                && value.Required);
    }

    [Fact]
    public void ThreeXTouchArmorClassUsesOnlyCallerResolvedApplicableContributions()
    {
        var definition = Required("defense.ac.touch");
        var result = CharacterMechanicEvaluator.Evaluate(
            definition,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["dexterityContribution"] = 3,
                ["sizeModifier"] = 1,
                ["deflectionBonus"] = 2,
                ["dodgeContribution"] = 1
            },
            new Dictionary<string, bool>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Equal(17, result.Value);
    }

    private static CharacterMechanicDefinition Required(string key) =>
        KnownCharacterMechanics.FindByKey(key)
        ?? throw new Xunit.Sdk.XunitException($"Known mechanic '{key}' was not registered.");
}
