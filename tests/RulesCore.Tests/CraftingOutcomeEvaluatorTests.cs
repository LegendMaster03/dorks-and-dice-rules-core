using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Tests;

public sealed class CraftingOutcomeEvaluatorTests
{
    [Fact]
    public void Manufacturing_uses_resolved_universal_competency()
    {
        var projection = Projection(
            [
                Mechanic(
                    "competency.blacksmithing",
                    "Blacksmithing",
                    8,
                    [
                        Contribution("ability.intelligence.modifier", 4),
                        Contribution("competency.blacksmithing.ranks", 4)
                    ])
            ]);

        var result = CraftingOutcomeEvaluator.ResolveManufacturing(
            projection,
            new ManufacturingResolutionRequest(
                new CharacterRulesProjectionRequest(),
                new CraftingCompetencyInput("competency.blacksmithing"),
                D20Roll: 11,
                TargetDc: 18));

        Assert.Equal(CharacterMechanicRollModes.Normal, result.RollMode);
        Assert.True(result.IsQualified);
        Assert.Equal(4, result.AbilityContribution);
        Assert.Equal(4, result.CompetencyContribution);
        Assert.Equal(19, result.Total);
        Assert.True(result.MeetsTarget);
        Assert.Equal(CraftingOutcomeKinds.Completed, result.Outcome);
        Assert.Equal(1, result.Margin);
        Assert.True(result.InputsConsumed);
        Assert.True(result.ProducesFunctionalOutput);
        Assert.False(result.ManualCompetency);
    }

    [Fact]
    public void Manufacturing_can_use_source_selected_character_ability()
    {
        var projection = Projection(
            [
                Mechanic(
                    "competency.blacksmithing",
                    "Blacksmithing",
                    6,
                    [
                        Contribution("ability.intelligence.modifier", 2),
                        Contribution("competency.blacksmithing.ranks", 4)
                    ]),
                Mechanic(
                    "ability.strength.modifier",
                    "Strength modifier",
                    3,
                    [])
            ]);

        var result = CraftingOutcomeEvaluator.ResolveManufacturing(
            projection,
            new ManufacturingResolutionRequest(
                new CharacterRulesProjectionRequest(),
                new CraftingCompetencyInput("blacksmithing"),
                D20Roll: 10,
                TargetDc: 17,
                AbilityKey: "strength"));

        Assert.Equal(3, result.AbilityContribution);
        Assert.Equal(4, result.CompetencyContribution);
        Assert.Equal(17, result.Total);
        Assert.Equal(CraftingOutcomeKinds.Completed, result.Outcome);
    }

    [Fact]
    public void Manufacturing_can_use_manual_ability_modifier()
    {
        var result = CraftingOutcomeEvaluator.ResolveManufacturing(
            Projection([]),
            new ManufacturingResolutionRequest(
                new CharacterRulesProjectionRequest(),
                new CraftingCompetencyInput(
                    Manual: new CraftingManualCompetencyInput(
                        "Glassblowing",
                        2,
                        true)),
                D20Roll: 10,
                TargetDc: 15,
                ManualAbilityModifier: 3));

        Assert.Equal(3, result.AbilityContribution);
        Assert.Equal(2, result.CompetencyContribution);
        Assert.Equal(15, result.Total);
    }

    [Fact]
    public void Manufacturing_unqualified_without_guidance_uses_disadvantage()
    {
        var projection = Projection(
            [
                Mechanic(
                    "competency.blacksmithing",
                    "Blacksmithing",
                    3,
                    [Contribution("ability.intelligence.modifier", 3)])
            ]);

        var result = CraftingOutcomeEvaluator.ResolveManufacturing(
            projection,
            new ManufacturingResolutionRequest(
                new CharacterRulesProjectionRequest(),
                new CraftingCompetencyInput("blacksmithing")));

        Assert.False(result.IsQualified);
        Assert.Equal(CharacterMechanicRollModes.Disadvantage, result.RollMode);
        Assert.Null(result.Total);
    }

    [Fact]
    public void Manufacturing_manual_entry_is_supported()
    {
        var result = CraftingOutcomeEvaluator.ResolveManufacturing(
            Projection([]),
            new ManufacturingResolutionRequest(
                new CharacterRulesProjectionRequest(),
                new CraftingCompetencyInput(
                    Manual: new CraftingManualCompetencyInput(
                        "Clockmaking",
                        6,
                        true)),
                D20Roll: 10,
                OtherModifier: 1));

        Assert.True(result.ManualCompetency);
        Assert.Equal("Clockmaking", result.CompetencyDisplayName);
        Assert.Equal(17, result.Total);
    }

    [Fact]
    public void Enchanting_uses_spellcasting_ability_and_nonability_competency_contribution()
    {
        var projection = Projection(
            [
                Mechanic(
                    "competency.arcana",
                    "Arcana",
                    7,
                    [
                        Contribution("ability.intelligence.modifier", 3),
                        Contribution("proficiency.standard", 4)
                    ]),
                Mechanic(
                    "ability.charisma.modifier",
                    "Charisma modifier",
                    5,
                    [])
            ],
            [
                new CharacterSpellcastingView(
                    "spellcasting.sorcerer",
                    "Sorcerer Spellcasting",
                    "resolved",
                    "charisma",
                    null,
                    null,
                    null,
                    [],
                    [],
                    Provenance())
            ]);

        var result = CraftingOutcomeEvaluator.ResolveEnchanting(
            projection,
            new EnchantingResolutionRequest(
                new CharacterRulesProjectionRequest(),
                CreatureType: "aberration",
                D20Roll: 9,
                TargetDc: 17));

        Assert.Equal("competency.arcana", result.CompetencyKey);
        Assert.Equal(5, result.AbilityContribution);
        Assert.Equal(4, result.CompetencyContribution);
        Assert.Equal(18, result.Total);
        Assert.True(result.MeetsTarget);
        Assert.Equal(CraftingOutcomeKinds.Completed, result.Outcome);
        Assert.Equal(1, result.Margin);
        Assert.Equal(0, result.FlawCount);
        Assert.True(result.InputsConsumed);
        Assert.True(result.ProducesFunctionalOutput);
    }

    [Theory]
    [InlineData(4, "completed-with-flaws", -4, 1, true)]
    [InlineData(0, "completed-with-flaws", -8, 2, true)]
    [InlineData(-4, "completed-with-flaws", -12, 3, true)]
    [InlineData(-5, "destroyed", -13, null, false)]
    public void Enchanting_classifies_failed_check_outcomes(
        int otherModifier,
        string expectedOutcome,
        int expectedMargin,
        int? expectedFlaws,
        bool producesOutput)
    {
        var projection = Projection(
            [
                Mechanic(
                    "competency.arcana",
                    "Arcana",
                    4,
                    [Contribution("proficiency.standard", 4)]),
                Mechanic(
                    "ability.intelligence.modifier",
                    "Intelligence modifier",
                    3,
                    [])
            ],
            [
                new CharacterSpellcastingView(
                    "spellcasting.wizard",
                    "Wizard Spellcasting",
                    "resolved",
                    "intelligence",
                    null,
                    null,
                    null,
                    [],
                    [],
                    Provenance())
            ]);

        var result = CraftingOutcomeEvaluator.ResolveEnchanting(
            projection,
            new EnchantingResolutionRequest(
                new CharacterRulesProjectionRequest(),
                CreatureType: "aberration",
                D20Roll: 6,
                OtherModifier: otherModifier,
                TargetDc: 21));

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(expectedMargin, result.Margin);
        Assert.Equal(expectedFlaws, result.FlawCount);
        Assert.True(result.InputsConsumed);
        Assert.Equal(producesOutput, result.ProducesFunctionalOutput);
    }

    [Fact]
    public void Manufacturing_failure_consumes_inputs_without_functional_output()
    {
        var projection = Projection(
            [
                Mechanic(
                    "competency.blacksmithing",
                    "Blacksmithing",
                    4,
                    [Contribution("competency.blacksmithing.ranks", 4)])
            ]);

        var result = CraftingOutcomeEvaluator.ResolveManufacturing(
            projection,
            new ManufacturingResolutionRequest(
                new CharacterRulesProjectionRequest(),
                new CraftingCompetencyInput("blacksmithing"),
                D20Roll: 5,
                TargetDc: 15));

        Assert.Equal(CraftingOutcomeKinds.Failed, result.Outcome);
        Assert.Equal(-6, result.Margin);
        Assert.True(result.InputsConsumed);
        Assert.False(result.ProducesFunctionalOutput);
    }

    private static CharacterRulesProjectionView Projection(
        IReadOnlyList<CharacterResolvedMechanicView> mechanics,
        IReadOnlyList<CharacterSpellcastingView>? spellcasting = null) =>
        new(
            "global",
            null,
            1,
            DateTimeOffset.UtcNow,
            mechanics,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            spellcasting ?? [],
            [],
            [],
            [],
            [],
            []);

    private static CharacterResolvedMechanicView Mechanic(
        string key,
        string displayName,
        int value,
        IReadOnlyList<CharacterMechanicContributionView> contributions) =>
        new(
            key,
            key.StartsWith("competency.", StringComparison.Ordinal)
                ? "competency"
                : "ability",
            displayName,
            "resolved",
            value,
            null,
            null,
            [],
            [],
            [],
            [],
            contributions,
            Provenance());

    private static CharacterMechanicContributionView Contribution(
        string key,
        int value) =>
        new(
            key,
            key,
            "add",
            value,
            null,
            null);

    private static CharacterMechanicProvenanceView Provenance() =>
        new([], [], []);
}
