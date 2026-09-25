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
        Assert.False(result.ManualCompetency);
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
