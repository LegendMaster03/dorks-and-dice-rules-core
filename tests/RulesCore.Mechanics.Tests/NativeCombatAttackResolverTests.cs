using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeCombatAttackResolverTests
{
    [Fact]
    public void OrdinaryAttackHitsOnlyWhenTotalMeetsArmorClass()
    {
        var hit = NativeCombatAttackResolver.Check(18, 5, NativeRollMode.Normal, 13);
        Assert.Equal("resolved", hit.Status);
        Assert.True(hit.Hit);
        Assert.Equal(18, hit.AttackTotal);
        Assert.Equal(18, hit.ArmorClass);
        Assert.Equal(13, hit.NaturalDie);

        var miss = NativeCombatAttackResolver.Check(19, 5, NativeRollMode.Normal, 13);
        Assert.Equal("resolved", miss.Status);
        Assert.False(miss.Hit);
    }

    [Fact]
    public void RollModeUsesExistingDiceResolution()
    {
        var adv = NativeCombatAttackResolver.Check(17, 3, NativeRollMode.Advantage, 4, 15);
        Assert.True(adv.Hit);
        Assert.Equal(15, adv.NaturalDie);
        var disadv = NativeCombatAttackResolver.Check(17, 3, NativeRollMode.Disadvantage, 4, 15);
        Assert.False(disadv.Hit);
        Assert.Equal(4, disadv.NaturalDie);
    }

    [Fact]
    public void RollInputsFailClosed()
    {
        Assert.Equal("invalid-dice",
            NativeCombatAttackResolver.Check(10, 1, NativeRollMode.Normal, 21).Status);
        Assert.Equal("unexpected-die",
            NativeCombatAttackResolver.Check(10, 1, NativeRollMode.Normal, 10, 12).Status);
        Assert.Equal("roll-required",
            NativeCombatAttackResolver.Check(10, 1, NativeRollMode.Advantage, 10).Status);
        Assert.Equal("invalid-input",
            NativeCombatAttackResolver.Check(10, 1, NativeRollMode.Normal, 10,
                criticalThreatMinimum: 1).Status);
        Assert.Equal("invalid-input",
            NativeCombatAttackResolver.Check(10, int.MaxValue,
                NativeRollMode.Normal, 4).Status);
    }

    [Fact]
    public void NaturalTwentyAndOneAreReportedForCriticalAdjudication()
    {
        var twenty = NativeCombatAttackResolver.Check(99, 0, NativeRollMode.Normal, 20);
        Assert.Equal("critical-resolution-required", twenty.Status);
        Assert.True(twenty.Hit);
        var one = NativeCombatAttackResolver.Check(-10, 20, NativeRollMode.Normal, 1);
        Assert.Equal("critical-resolution-required", one.Status);
        Assert.False(one.Hit);
    }

    [Fact]
    public void ExpandedThreatOnlyEscalatesQualifyingHit()
    {
        var hit = NativeCombatAttackResolver.Check(18, 2, NativeRollMode.Normal,
            19, criticalThreatMinimum: 19);
        Assert.Equal("critical-resolution-required", hit.Status);
        Assert.True(hit.Hit);
        var miss = NativeCombatAttackResolver.Check(30, 2, NativeRollMode.Normal,
            19, criticalThreatMinimum: 19);
        Assert.Equal("resolved", miss.Status);
        Assert.False(miss.Hit);
    }

    [Fact]
    public void UnresolvedEmphasisAndInvalidModeDoNotProduceHits()
    {
        Assert.Equal("reroll-required",
            NativeCombatAttackResolver.Check(10, 0, NativeRollMode.Emphasis, 1, 20).Status);
        Assert.Equal("invalid-input",
            NativeCombatAttackResolver.Check(10, 0, (NativeRollMode)99, 10).Status);
    }

    [Theory]
    [InlineData(NativeDefenseProfile.Normal, 19)]
    [InlineData(NativeDefenseProfile.Touch, 13)]
    [InlineData(NativeDefenseProfile.FlatFooted, 16)]
    [InlineData(NativeDefenseProfile.TouchFlatFooted, 10)]
    public void ExistingDefenseProfilesRemainTheSourceOfTruth(
        NativeDefenseProfile profile, int expected)
    {
        var parts = new Dictionary<string, int>
        {
            ["armor"] = 4, ["shield"] = 2, ["dexterity"] = 3
        };
        var ac = NativeDefenses.Evaluate(profile, parts);
        Assert.Equal(expected, ac);
        var hit = NativeCombatAttackResolver.Check(ac, 0, NativeRollMode.Normal, 14);
        Assert.Equal(14 >= expected, hit.Hit);
    }
}
