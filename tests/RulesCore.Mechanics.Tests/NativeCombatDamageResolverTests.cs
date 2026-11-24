using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeCombatDamageResolverTests
{
    private static NativeCombatDamageRequest Request(
        int incoming, int percent = 0, IReadOnlyList<int>? dr = null,
        int? hp = 20, int? max = 20) =>
        new("first-party-001", "fixture-fingerprint",
            incoming, percent, dr, hp, max);

    [Fact]
    public void PercentageAppliesBeforeSummedDamageReduction()
    {
        var result = NativeCombatDamageResolver.Resolve(
            Request(20, 50, [2, 3]));
        Assert.Equal("resolved", result.Status);
        Assert.Equal(10, result.ModifiedDamage);
        Assert.Equal(5, result.AppliedDamageReduction);
        Assert.Equal(5, result.FinalDamage);
        Assert.Equal(15, result.ProposedHitPoints);
        Assert.False(result.AtZeroHitPoints);
    }

    [Fact]
    public void ImmunityAndVulnerabilityUseSignedPercentSemantics()
    {
        var immune = NativeCombatDamageResolver.Resolve(Request(10, 100));
        Assert.Equal(0, immune.FinalDamage);
        Assert.Equal(20, immune.ProposedHitPoints);
        var vulnerable = NativeCombatDamageResolver.Resolve(Request(10, -100));
        Assert.Equal(20, vulnerable.FinalDamage);
        Assert.Equal(0, vulnerable.ProposedHitPoints);
        Assert.True(vulnerable.AtZeroHitPoints);
    }

    [Fact]
    public void FlatDamageReductionNeverProducesNegativeDamage()
    {
        var result = NativeCombatDamageResolver.Resolve(Request(4, 0, [7, 8]));
        Assert.Equal(4, result.AppliedDamageReduction);
        Assert.Equal(0, result.FinalDamage);
        Assert.Equal(20, result.ProposedHitPoints);
    }

    [Fact]
    public void FractionalDamageReturnsUnresolvedWithoutGuessingRounding()
    {
        var result = NativeCombatDamageResolver.Resolve(Request(3, 25));
        Assert.Equal("rounding-unresolved", result.Status);
        Assert.Null(result.FinalDamage);
        Assert.Null(result.ProposedHitPoints);
    }

    [Fact]
    public void HealthCanBeOmittedForDamageOnlyCalculation()
    {
        var result = NativeCombatDamageResolver.Resolve(
            Request(9, 0, [2], hp: null, max: null));
        Assert.Equal("resolved", result.Status);
        Assert.Equal(7, result.FinalDamage);
        Assert.Null(result.ProposedHitPoints);
        Assert.Null(result.AtZeroHitPoints);
    }

    [Fact]
    public void InvalidInputsAndOverflowFailClosed()
    {
        Assert.Equal("invalid-input",
            NativeCombatDamageResolver.Resolve(Request(-1)).Status);
        Assert.Equal("invalid-input",
            NativeCombatDamageResolver.Resolve(Request(10, 101)).Status);
        Assert.Equal("invalid-input",
            NativeCombatDamageResolver.Resolve(Request(10, 0, [-1])).Status);
        Assert.Equal("invalid-input",
            NativeCombatDamageResolver.Resolve(Request(10, hp: 21, max: 20)).Status);
        Assert.Equal("invalid-input",
            NativeCombatDamageResolver.Resolve(Request(10, hp: null, max: 20)).Status);
        Assert.Equal("invalid-input",
            NativeCombatDamageResolver.Resolve(Request(int.MaxValue, -100)).Status);
    }

    [Fact]
    public void DamageProposalDoesNotMutateCallerState()
    {
        var input = Request(8, 0, [2], hp: 10, max: 20);
        var result = NativeCombatDamageResolver.Resolve(input);
        Assert.Equal(4, result.ProposedHitPoints);
        Assert.Equal(10, input.CurrentHitPoints);
        Assert.Equal(20, input.MaximumHitPoints);
    }
}
