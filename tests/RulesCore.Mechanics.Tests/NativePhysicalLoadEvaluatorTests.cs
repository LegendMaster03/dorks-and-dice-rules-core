using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// General gross-load math, independent of Construct Origins, body topology,
/// equipment persistence or a chosen universal movement penalty policy.
/// </summary>
public sealed class NativePhysicalLoadEvaluatorTests
{
    [Fact]
    public void IncompleteBodyMassCanStillProveSupportIsOverloaded()
    {
        var value = NativePhysicalLoadEvaluator.Evaluate(
            knownMinimumBodyMassPounds: 325, bodyMassComplete: false,
            carriedPayloadMassPounds: null,
            [new("drive", 300, true)]);
        Assert.Equal("overloaded", value.Status);
        Assert.Equal(false, value.WithinSupportCapacity);
        Assert.Equal(25, value.MinimumDeficitPounds);
        Assert.Equal(325, value.KnownMinimumGrossLoadPounds);
    }

    [Fact]
    public void ApparentHeadroomWithUnknownAdditionalMassMustStayUnresolved()
    {
        var value = NativePhysicalLoadEvaluator.Evaluate(
            140, bodyMassComplete: false, carriedPayloadMassPounds: 10,
            [new("drive", 300, true)]);
        Assert.Equal("unresolved", value.Status);
        Assert.Null(value.WithinSupportCapacity);
        Assert.Equal(150, value.KnownMinimumGrossLoadPounds);
        Assert.Equal(150, value.MaximumPossibleHeadroomPounds);
    }

    [Fact]
    public void CompleteGrossLoadSupportsAUniversalCapacityDecision()
    {
        var supported = NativePhysicalLoadEvaluator.Evaluate(
            200, bodyMassComplete: true, carriedPayloadMassPounds: 75,
            [new("drive", 300, true)]);
        Assert.Equal("supported", supported.Status);
        Assert.Equal(true, supported.WithinSupportCapacity);
        Assert.Equal(25, supported.MaximumPossibleHeadroomPounds);

        var overloaded = NativePhysicalLoadEvaluator.Evaluate(
            200, bodyMassComplete: true, carriedPayloadMassPounds: 150,
            [new("drive", 300, true)]);
        Assert.Equal("overloaded", overloaded.Status);
        Assert.Equal(50, overloaded.MinimumDeficitPounds);
    }

    [Fact]
    public void ExplicitlySharingActiveSupportsCanCombineMaximumCapacity()
    {
        var combined = NativePhysicalLoadEvaluator.Evaluate(
            500, bodyMassComplete: true, carriedPayloadMassPounds: 0,
            [new("left", 300, true, SharesLoad: true),
             new("right", 300, true, SharesLoad: true)]);
        Assert.Equal("supported", combined.Status);
        Assert.Equal(600, combined.VerifiedSupportCapacityPounds);

        var noAuthorization = NativePhysicalLoadEvaluator.Evaluate(
            500, bodyMassComplete: true, carriedPayloadMassPounds: 0,
            [new("left", 300, true, SharesLoad: true),
             new("right", 300, true)]);
        Assert.Equal("unresolved", noAuthorization.Status);
        Assert.Null(noAuthorization.VerifiedSupportCapacityPounds);
    }

    [Fact]
    public void InactiveAndUnresolvedSupportsAreNotAssumedAvailable()
    {
        var inactive = NativePhysicalLoadEvaluator.Evaluate(
            350, bodyMassComplete: false, carriedPayloadMassPounds: 0,
            [new("drive-a", 300, true),
             new("drive-b", 1000, false)]);
        Assert.Equal("overloaded", inactive.Status);
        Assert.Equal(300, inactive.VerifiedSupportCapacityPounds);

        var unknown = NativePhysicalLoadEvaluator.Evaluate(
            350, bodyMassComplete: false, carriedPayloadMassPounds: 0,
            [new("drive-a", 300, true, SharesLoad: true),
             new("drive-b", null, true, SharesLoad: true)]);
        Assert.Equal("unresolved", unknown.Status);
        Assert.Null(unknown.WithinSupportCapacity);
    }

    [Fact]
    public void MissingPayloadNeverBecomesCompleteCapacityProof()
    {
        var value = NativePhysicalLoadEvaluator.Evaluate(
            75, bodyMassComplete: true, carriedPayloadMassPounds: null,
            [new("drive", 300, true)]);
        Assert.Equal("unresolved", value.Status);
        Assert.Equal(75, value.KnownMinimumGrossLoadPounds);
    }

    [Fact]
    public void CapacityInputMustBeNonnegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NativePhysicalLoadEvaluator.Evaluate(-1, true, 0, []));
        Assert.Throws<ArgumentException>(() =>
            NativePhysicalLoadEvaluator.Evaluate(5, true, 0,
                [new("invalid", -4, true)]));
    }
}
