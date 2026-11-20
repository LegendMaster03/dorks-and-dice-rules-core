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
    public void InvalidFragmentDataIsNotTreatedAsAvailableLoot()
    {
        var bad = NativeRelicReconstruction.Evaluate(
            Relic() with { AvailableFragments = [new("source:dwarf", false, -1)] });
        Assert.Equal("unresolved", bad.Status);
        Assert.Null(bad.CanAttempt);
    }
}
