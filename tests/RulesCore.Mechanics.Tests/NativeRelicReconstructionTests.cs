using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeRelicReconstructionTests
{
    private static NativeRelicReconstructionRequest Relic(
        bool magical = false, string? rarity = null, int? rank = 3) =>
        new("source:dwarf", magical, rarity, rank,
            [new("source:dwarf", magical, 6)],
            true, true, true);

    [Fact]
    public void SameOriginFragmentsAllowOrdinaryRelicReconstruction()
    {
        var result = NativeRelicReconstruction.Evaluate(Relic());
        Assert.Equal("eligible", result.Status);
        Assert.True(result.CanAttempt);
        Assert.Equal(6, result.MatchingFragments);
        Assert.Equal(6, result.RequiredFragments);
    }

    [Fact]
    public void WrongOriginOrFragmentKindCannotCompleteRelic()
    {
        var relic = Relic() with {
            AvailableFragments = [
                new("source:dwarf", false, 3),
                new("source:orc", false, 3),
                new("source:dwarf", true, 3)
            ]
        };
        var result = NativeRelicReconstruction.Evaluate(relic);
        Assert.Equal("ineligible", result.Status);
        Assert.Equal(3, result.MatchingFragments);
        Assert.Contains("insufficient-matching-fragments", result.Reasons);
    }

    [Fact]
    public void MagicRelicRequiresItsItemRarityRankAndDesign()
    {
        var denied = NativeRelicReconstruction.Evaluate(
            Relic(true, "Legendary", 3));
        Assert.Equal("ineligible", denied.Status);
        Assert.Contains("competency-rank-below-item-rarity", denied.Reasons);

        var unknownRecipe = NativeRelicReconstruction.Evaluate(
            Relic(true, "Rare", 3) with { HasRequiredKnowledge = null });
        Assert.Equal("unresolved", unknownRecipe.Status);

        var allowed = NativeRelicReconstruction.Evaluate(
            Relic(true, "Rare", 3));
        Assert.True(allowed.CanAttempt);
    }

    [Fact]
    public void SourceCanSpecifyDifferentFragmentCount()
    {
        var result = NativeRelicReconstruction.Evaluate(
            Relic() with {
                RequiredFragments = 8,
                AvailableFragments = [new("source:dwarf", false, 8)]
            });
        Assert.True(result.CanAttempt);
        Assert.Equal(8, result.RequiredFragments);
    }

    [Fact]
    public void LackOfHistoricalMethodOrFacilitiesBlocksRestoration()
    {
        var noSkill = NativeRelicReconstruction.Evaluate(
            Relic() with { HasQualifiedHistoricalMethod = false });
        Assert.Equal("ineligible", noSkill.Status);

        var unknownFacilities = NativeRelicReconstruction.Evaluate(
            Relic() with { HasRequiredFacilities = null });
        Assert.Equal("unresolved", unknownFacilities.Status);
    }

    [Fact]
    public void OrdinaryRelicAssemblyRequiresSuccessfulHistoryCheck()
    {
        var passed = NativeRelicAssembly.Resolve(new(Relic(), HistoryCheckTotal: 15));
        Assert.Equal("resolved", passed.Status);
        Assert.True(passed.Assembled);
        Assert.Equal(15, passed.DifficultyClass);

        var failed = NativeRelicAssembly.Resolve(new(Relic(), HistoryCheckTotal: 14));
        Assert.Equal("resolved", failed.Status);
        Assert.False(failed.Assembled);
        Assert.Contains("failed-history-check", failed.Reasons);
    }

    [Fact]
    public void FailedAssemblyCannotBeRepeatedWithoutNewMatchingFragment()
    {
        var blocked = NativeRelicAssembly.Resolve(new(
            Relic(), HistoryCheckTotal: 19, PreviousAttemptFailed: true,
            AdditionalMatchingFragmentsSinceFailure: 0));
        Assert.Equal("ineligible", blocked.Status);
        Assert.Null(blocked.Assembled);

        var unknown = NativeRelicAssembly.Resolve(new(
            Relic(), HistoryCheckTotal: 20, PreviousAttemptFailed: true));
        Assert.Equal("unresolved", unknown.Status);

        var allowed = NativeRelicAssembly.Resolve(new(
            Relic() with {
                AvailableFragments = [new("source:dwarf", false, 7)]
            }, HistoryCheckTotal: 15, PreviousAttemptFailed: true,
            AdditionalMatchingFragmentsSinceFailure: 1));
        Assert.True(allowed.Assembled);
    }

    [Fact]
    public void MagicFragmentsUseRarityQualificationWithoutHistoryRoll()
    {
        var result = NativeRelicAssembly.Resolve(new(
            Relic(true, "Rare", 3)));
        Assert.Equal("resolved", result.Status);
        Assert.True(result.Assembled);
        Assert.Null(result.DifficultyClass);

        var rejected = NativeRelicAssembly.Resolve(new(
            Relic(true, "Legendary", 3)));
        Assert.Equal("ineligible", rejected.Status);
        Assert.Null(rejected.Assembled);
    }

    [Fact]
    public void InvalidHistoryDcDoesNotResolveAssembly()
    {
        var invalid = NativeRelicAssembly.Resolve(new(
            Relic(), HistoryCheckTotal: 15, HistoryDifficultyClass: 0));
        Assert.Equal("unresolved", invalid.Status);
        Assert.Null(invalid.Assembled);
    }

    [Fact]
    public void InvalidFragmentDataIsNotTreatedAsAvailableLoot()
    {
        var bad = NativeRelicReconstruction.Evaluate(
            Relic() with { AvailableFragments = [new("source:dwarf", false, -1)] });
        Assert.Equal("unresolved", bad.Status);
        Assert.Null(bad.CanAttempt);
    }
}
