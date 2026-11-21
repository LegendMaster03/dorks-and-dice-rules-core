using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>Exercises source-authored movement tiers independently of character creation.</summary>
public sealed class NativePhysicalMovementEvaluatorTests
{
    private static NativePhysicalMovementLoadPolicy Policy() => new("walk", 30, 15, 75, 0);

    [Fact]
    public void CompleteGrossLoadResolvesNormalAndReducedSpeed()
    {
        var sources = new[] { new NativePhysicalMovementSource("drive", true, Policy()) };
        var light = NativePhysicalLoadEvaluator.Evaluate(200, true, 0,
            [new("drive", 300, true)]);
        var heavy = NativePhysicalLoadEvaluator.Evaluate(225, true, 0,
            [new("drive", 300, true)]);
        var normal = NativePhysicalMovementEvaluator.Evaluate(light, sources);
        var reduced = NativePhysicalMovementEvaluator.Evaluate(heavy, sources);
        Assert.Equal("normal", normal.Status);
        Assert.Equal(30, normal.SpeedFeet);
        Assert.Equal("loaded", reduced.Status);
        Assert.Equal(15, reduced.SpeedFeet);
    }

    [Fact]
    public void ProvenOverloadCanStallOnlyTheAuthoredMovementMode()
    {
        var load = NativePhysicalLoadEvaluator.Evaluate(500, false, null,
            [new("drive", 300, true)]);
        var result = NativePhysicalMovementEvaluator.Evaluate(load,
            [new("drive", true, Policy())]);
        Assert.Equal("stalled", result.Status);
        Assert.Equal("walk", result.Mode);
        Assert.Equal(0, result.SpeedFeet);
    }

    [Theory]
    [InlineData(220, 30)]
    [InlineData(240, 15)]
    public void MinimumMassOnlyGivesUpperBoundRatherThanUsableSpeed(long weight, int max)
    {
        var load = NativePhysicalLoadEvaluator.Evaluate(weight, false, null,
            [new("drive", 300, true)]);
        var result = NativePhysicalMovementEvaluator.Evaluate(load,
            [new("drive", true, Policy())]);
        Assert.Equal("unresolved", result.Status);
        Assert.Null(result.SpeedFeet);
        Assert.Equal(max, result.UpperBoundSpeedFeet);
    }

    [Fact]
    public void SharedSupportsDoNotDoubleSpeed()
    {
        var load = NativePhysicalLoadEvaluator.Evaluate(400, true, 0,
            [new("a", 300, true, true), new("b", 300, true, true)]);
        var result = NativePhysicalMovementEvaluator.Evaluate(load,
            [new("a", true, Policy()), new("b", true, Policy())]);
        Assert.Equal("normal", result.Status);
        Assert.Equal(30, result.SpeedFeet);
        Assert.Equal(new[] { "a", "b" }, result.SourceIds);
    }

    [Fact]
    public void IncompatibleModesCannotSilentlyShareOneSpeed()
    {
        var load = NativePhysicalLoadEvaluator.Evaluate(450, true, 0,
            [new("legs", 300, true, true), new("wings", 300, true, true)]);
        var result = NativePhysicalMovementEvaluator.Evaluate(load,
            [new("legs", true, Policy()),
             new("wings", true, new("fly", 40, 20, 75, 0))]);
        Assert.Equal("unresolved", result.Status);
        Assert.Null(result.SpeedFeet);
    }

    [Fact]
    public void UnpoweredOrUnverifiedSourcesDoNotGrantMovement()
    {
        Assert.Equal("unavailable", NativePhysicalMovementEvaluator.Evaluate(null,
            [new("drive", false, Policy())]).Status);
        var load = NativePhysicalLoadEvaluator.Evaluate(100, true, 0,
            [new("drive", 300, true)]);
        Assert.Equal("unresolved", NativePhysicalMovementEvaluator.Evaluate(load,
            [new("drive", true, null)]).Status);
        Assert.Equal("unresolved", NativePhysicalMovementEvaluator.Evaluate(load,
            [new("drive", true, new("walk", 20, 30, 75, 0))]).Status);
    }
}
