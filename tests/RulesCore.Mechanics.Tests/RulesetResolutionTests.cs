using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class RulesetResolutionTests
{
    [Theory]
    [InlineData("dnd-5e", 0, false, false, new[] { 15 }, 15, "success")]
    [InlineData("dnd-5-5e", 0, false, false, new[] { 15 }, 15, "success")]
    [InlineData("dnd-5e", 1, false, false, new[] { 7, 19 }, 7, "failure")]
    [InlineData("dnd-5-5e", 1, false, false, new[] { 19 }, 17, "success")]
    [InlineData("dnd-5e", 2, true, false, new[] { 19 }, 19, "success")]
    [InlineData("dnd-5-5e", 2, true, false, new[] { 7, 19 }, 15, "success")]
    [InlineData("dnd-5-5e", 0, true, true, new[] { 15 }, 15, "success")]
    public void D20UsesEditionSpecificExhaustionAndSharedRollSelection(
        string id, int exhaustion, bool advantage, bool disadvantage, int[] dice,
        int total, string outcome)
    {
        var result = RulesetResolution.Evaluate(id,
            new(RulesetResolution.D20AbilityCheck, dice, 0, 15,
                new(advantage, disadvantage, exhaustion)));
        Assert.Equal("resolved", result.Status);
        Assert.Equal((long)total, result.Total);
        Assert.Equal(outcome, result.Outcome);
    }

    [Theory]
    [InlineData(5, "failure")]
    [InlineData(4, "tie")]
    [InlineData(3, "success")]
    [InlineData(1, "success-with-style")]
    public void FateUsesFourSignedDiceAndMargin(int target, string outcome)
    {
        var result = RulesetResolution.Evaluate("fate-condensed",
            new(RulesetResolution.FateRoll, [1, -1, 1, 0], 3, target));
        Assert.Equal("resolved", result.Status);
        Assert.Equal(4L, result.Total);
        Assert.Equal(outcome, result.Outcome);
    }

    [Theory]
    [InlineData("first-party-001", "fate-roll")]
    [InlineData("unknown", "d20-ability-check")]
    [InlineData("fate-condensed", "d20-ability-check")]
    [InlineData("dnd-5e", "character-origins")]
    public void NoFallbackForUnknownOrForeignMechanics(string id, string operation) =>
        Assert.Equal("unsupported", RulesetResolution.Evaluate(id,
            new(operation, [1], 0, 10)).Status);

    [Fact]
    public void RejectsInvalidDiceAndIncompatibleOptions()
    {
        Assert.Equal("invalid_d20_dice", RulesetResolution.Evaluate("dnd-5e",
            new(RulesetResolution.D20AbilityCheck, [0], 0, 10)).Error);
        Assert.Equal("invalid_fate_dice", RulesetResolution.Evaluate("fate-condensed",
            new(RulesetResolution.FateRoll, [1, 2, 0, 0], 0, 0)).Error);
        Assert.Equal("incompatible_options", RulesetResolution.Evaluate("fate-condensed",
            new(RulesetResolution.FateRoll, [1, 0, 0, 0], 0, 0, new())).Error);
        Assert.Equal("actor_unable_to_check", RulesetResolution.Evaluate("dnd-5-5e",
            new(RulesetResolution.D20AbilityCheck, [20], 0, 10, new(Exhaustion: 6))).Error);
    }
}
