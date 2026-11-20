using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeCraftingRecoveryTests
{
    private static NativeRecoveryQualificationRequest Disenchant(
        string rarity, int ranks) =>
        new(NativeRecoveryOperation.Disenchant, rarity, ranks,
            null, true, true, true, true, true, false,
            MinutesSpent: 60);

    [Theory]
    [InlineData("Common", 2)]
    [InlineData("Uncommon", 3)]
    [InlineData("Rare", 4)]
    [InlineData("Very Rare", 5)]
    public void StandardDisenchantingIsRarityGated(
        string rarity, int minimum)
    {
        var allowed = NativeCraftingRecovery.Evaluate(
            Disenchant(rarity, minimum));
        Assert.True(allowed.Eligible);
        Assert.Equal(minimum, allowed.MinimumRanks);
        var denied = NativeCraftingRecovery.Evaluate(
            Disenchant(rarity, minimum - 1));
        Assert.Equal("ineligible", denied.Status);
        Assert.Contains("operator-rank-too-low", denied.Reasons);
    }

    [Theory]
    [InlineData("Legendary")]
    [InlineData("Artifact")]
    public void OrdinaryDisenchantingExcludesExceptionalRarities(string rarity)
    {
        var result = NativeCraftingRecovery.Evaluate(Disenchant(rarity, 6));
        Assert.False(result.Eligible);
        Assert.Contains("rarity-excluded-from-ordinary-disenchanting",
            result.Reasons);
    }

    [Fact]
    public void ExceptionalRecoveryMustHaveAnExplicitSourceDefinedProcedure()
    {
        var unresolved = NativeCraftingRecovery.Evaluate(
            Disenchant("Artifact", 6) with {
                ExceptionalDisenchantingAuthorized = true
            });
        Assert.Equal("unresolved", unresolved.Status);

        var authorized = NativeCraftingRecovery.Evaluate(
            Disenchant("Artifact", 6) with {
                ExceptionalDisenchantingAuthorized = true,
                SourceMinimumRanks = 6
            });
        Assert.True(authorized.Eligible);
    }

    [Fact]
    public void OtherPeoplesAttunedItemsAndNonPermanentEffectsCannotBeDisenchanted()
    {
        var attuned = NativeCraftingRecovery.Evaluate(
            Disenchant("Rare", 4) with { IsAttunedToAnother = true });
        Assert.Equal("ineligible", attuned.Status);
        Assert.Contains("item-attuned-to-another", attuned.Reasons);

        var temporary = NativeCraftingRecovery.Evaluate(
            Disenchant("Rare", 4) with { IsPermanent = false });
        Assert.Equal("ineligible", temporary.Status);
    }

    [Fact]
    public void DisenchantingRequiresOneHourButOrdinarySalvageDoesNot()
    {
        var tooShort = NativeCraftingRecovery.Evaluate(
            Disenchant("Common", 2) with { MinutesSpent = 59 });
        Assert.Equal("ineligible", tooShort.Status);
        Assert.Contains("disenchanting-time-insufficient", tooShort.Reasons);

        var unknown = NativeCraftingRecovery.Evaluate(
            Disenchant("Common", 2) with { MinutesSpent = null });
        Assert.Equal("unresolved", unknown.Status);
        Assert.Contains("disenchanting-time-unresolved", unknown.Reasons);

        var salvage = NativeCraftingRecovery.Evaluate(new(
            NativeRecoveryOperation.Salvage, null, 2, 2,
            true, true, true, true));
        Assert.True(salvage.Eligible);
    }

    [Fact]
    public void ExceptionalRulesUseTheirOwnAuthoredMinimumRank()
    {
        var authorized = NativeCraftingRecovery.Evaluate(
            Disenchant("Legendary", 5) with {
                ExceptionalDisenchantingAuthorized = true,
                SourceMinimumRanks = 5
            });
        Assert.True(authorized.Eligible);
        Assert.Equal(5, authorized.MinimumRanks);
    }

    [Fact]
    public void SalvagingUsesSourceDefinedRequirementsWithoutGuessingAYield()
    {
        var unknown = NativeCraftingRecovery.Evaluate(
            new(NativeRecoveryOperation.Salvage, null, 3, null,
                true, true, true, true));
        Assert.Equal("unresolved", unknown.Status);

        var allowed = NativeCraftingRecovery.Evaluate(
            new(NativeRecoveryOperation.Salvage, null, 3, 2,
                true, true, true, true));
        Assert.True(allowed.Eligible);
        Assert.Equal(2, allowed.MinimumRanks);

        var noOutput = NativeCraftingRecovery.Evaluate(
            new(NativeRecoveryOperation.Salvage, null, 3, 2,
                true, true, false, true));
        Assert.Equal("ineligible", noOutput.Status);
        Assert.Contains("defined-recovery-output-missing", noOutput.Reasons);
    }

    [Fact]
    public void UnknownInputsAndEquipmentNeverAuthorizeRecovery()
    {
        var unknown = NativeCraftingRecovery.Evaluate(
            Disenchant("Rare", 4) with { HasAccessibleItem = null });
        Assert.Equal("unresolved", unknown.Status);

        var missing = NativeCraftingRecovery.Evaluate(
            Disenchant("Rare", 4) with { HasRequiredEquipment = false });
        Assert.Equal("ineligible", missing.Status);
    }
}
