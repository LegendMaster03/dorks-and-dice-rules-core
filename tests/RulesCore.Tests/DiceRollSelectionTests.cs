using RulesCore.Domain.Rules;

namespace RulesCore.Tests;

public sealed class DiceRollSelectionTests
{
    [Fact]
    public void NormalUsesOneRoll()
    {
        var result = DiceRollSelector.Select(DiceRollSelectionModes.Normal, [12]);

        Assert.False(result.RequiresChoice);
        Assert.Equal(12, result.SelectedRoll);
        Assert.Equal(0, result.SelectedRollIndex);
    }

    [Fact]
    public void AdvantageRollsTwiceAndTakesTheHigherNumber()
    {
        var result = DiceRollSelector.Select(DiceRollSelectionModes.Advantage, [7, 14]);

        Assert.False(result.RequiresChoice);
        Assert.Equal(14, result.SelectedRoll);
        Assert.Equal(1, result.SelectedRollIndex);
    }

    [Fact]
    public void DisadvantageRollsTwiceAndTakesTheLowerNumber()
    {
        var result = DiceRollSelector.Select(DiceRollSelectionModes.Disadvantage, [7, 14]);

        Assert.False(result.RequiresChoice);
        Assert.Equal(7, result.SelectedRoll);
        Assert.Equal(0, result.SelectedRollIndex);
    }

    [Theory]
    [InlineData(4, 13, 4)]
    [InlineData(19, 2, 19)]
    [InlineData(11, 3, 3)]
    public void EmphasisTakesTheNumberFurthestFromTen(
        int first,
        int second,
        int expected)
    {
        var result = DiceRollSelector.Select(
            DiceRollSelectionModes.Emphasis,
            [first, second]);

        Assert.False(result.RequiresChoice);
        Assert.Equal(expected, result.SelectedRoll);
    }

    [Fact]
    public void EmphasisPreservesAnEqualDistanceTieWithoutInventingATieBreakRule()
    {
        var result = DiceRollSelector.Select(
            DiceRollSelectionModes.Emphasis,
            [7, 13]);

        Assert.True(result.RequiresChoice);
        Assert.Null(result.SelectedRoll);
        Assert.Null(result.SelectedRollIndex);
        Assert.Equal([0, 1], result.CandidateRollIndices);
    }

    [Fact]
    public void CharacterMechanicRollModesShareTheCanonicalDiceVocabulary()
    {
        Assert.Equal(DiceRollSelectionModes.Advantage, CharacterMechanicRollModes.Advantage);
        Assert.Equal(DiceRollSelectionModes.Disadvantage, CharacterMechanicRollModes.Disadvantage);
        Assert.Equal(DiceRollSelectionModes.Emphasis, CharacterMechanicRollModes.Emphasis);
    }
}
