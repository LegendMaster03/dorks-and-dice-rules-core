using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeArchaeologyExcavationTests
{
    private static NativeExcavationRequest Dig(
        int die, int total, int dc, int? fragments = null) =>
        new("source:dwarf", true, true, 60, die, total, dc, fragments);

    [Fact]
    public void OrdinarySuccessProducesOnlyTheExternallyRolledFragmentCount()
    {
        var result = NativeArchaeologyExcavation.Evaluate(Dig(12, 15, 15, 4));
        Assert.Equal("resolved", result.Status);
        Assert.Equal(NativeExcavationFind.Fragments, result.Find);
        Assert.Equal(4, result.FragmentQuantity);

        var withoutRoll = NativeArchaeologyExcavation.Evaluate(Dig(12, 15, 15));
        Assert.Equal("unresolved", withoutRoll.Status);
    }

    [Fact]
    public void StrongSuccessAndNaturalTwentyHaveDistinctFindKinds()
    {
        var relic = NativeArchaeologyExcavation.Evaluate(Dig(16, 20, 15));
        Assert.Equal(NativeExcavationFind.CompleteRelic, relic.Find);
        Assert.Equal(0, relic.FragmentQuantity);

        var magical = NativeArchaeologyExcavation.Evaluate(Dig(20, 22, 15));
        Assert.Equal(NativeExcavationFind.MagicalFragment, magical.Find);
        Assert.Equal(1, magical.FragmentQuantity);
    }

    [Fact]
    public void FailedCheckDoesNotGrantFragments()
    {
        var result = NativeArchaeologyExcavation.Evaluate(Dig(5, 9, 15));
        Assert.Equal(NativeExcavationFind.Nothing, result.Find);
        Assert.Equal(0, result.FragmentQuantity);
    }

    [Fact]
    public void ExcavationRequiresDedicatedDigSiteAndTime()
    {
        var notSite = NativeArchaeologyExcavation.Evaluate(
            Dig(20, 23, 15) with { IsDesignatedDigSite = false });
        Assert.Equal("ineligible", notSite.Status);
        Assert.Null(notSite.Find);

        var notEnoughTime = NativeArchaeologyExcavation.Evaluate(
            Dig(20, 23, 15) with { MinutesSpent = 10 });
        Assert.Equal("ineligible", notEnoughTime.Status);
        Assert.Null(notEnoughTime.Find);
    }

    [Fact]
    public void InvalidDiceAndUnknownAuthorityFailClosed()
    {
        Assert.Equal("unresolved", NativeArchaeologyExcavation.Evaluate(
            Dig(21, 30, 15)).Status);
        Assert.Equal("unresolved", NativeArchaeologyExcavation.Evaluate(
            Dig(12, 16, 15) with { IsDesignatedDigSite = null }).Status);
        Assert.Equal("ineligible", NativeArchaeologyExcavation.Evaluate(
            Dig(12, 16, 15) with { HasQualifiedHistoricalMethod = false }).Status);
    }
}
