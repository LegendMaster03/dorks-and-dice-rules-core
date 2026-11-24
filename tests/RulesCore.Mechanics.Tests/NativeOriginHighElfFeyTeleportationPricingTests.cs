using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Approved High Elf Fey Teleportation increment: one existing Misty Step
/// free use per Short or Long Rest, repriced as ONE enhanced innate spell.
/// The High Elf open Wizard cantrip still prevents a full innate-spell
/// projection/ledger; these are catalog, allocation and recovery primitives.
/// </summary>
public sealed class NativeOriginHighElfFeyTeleportationPricingTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var root = Path.Combine(directory.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(root, "first-party-001", "native",
                "rules", "creature-origination.json")))
                return NativeCatalogLoader.Load(
                    NativeRulesetLayout.Resolve(root, "first-party-001"));
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Native ruleset missing.");
    }

    private static JsonElement Definition(NativeCatalogSnapshot catalog, string id)
    {
        Assert.True(catalog.TryGetById(id, out var entity));
        return entity!.Definition;
    }

    [Fact]
    public void HighElfRetainsSingleMistyStepGrantWithEnhancedRecharge()
    {
        var catalog = Catalog();
        var high = Definition(catalog, "ancestry.elf.high");
        var traits = high.GetProperty("traits").EnumerateArray()
            .Select((_, index) => catalog.OriginTraitDefinitionId("ancestry.elf.high", index)).ToArray();
        Assert.Equal(new[]
        {
            "trait.arcane-cantrip", "trait.detect-magic", "trait.misty-step"
        }, traits);
        var step = Definition(catalog, "trait.misty-step")
            .GetProperty("trait");
        Assert.Equal("misty-step", step.GetProperty("spell").GetString());
        Assert.Equal(5, step.GetProperty("level").GetInt32());
        Assert.Equal(1, step.GetProperty("freeUses").GetInt32());
        Assert.Equal("short-or-long-rest", step.GetProperty("recharge").GetString());
        Assert.True(step.GetProperty("slotCasting").GetBoolean());
        var detect = Definition(catalog, "trait.detect-magic")
            .GetProperty("trait");
        Assert.Equal("long-rest", detect.GetProperty("recharge").GetString());
        Assert.Equal(3, detect.GetProperty("level").GetInt32());
    }

    [Fact]
    public void RevisedCompleteInnateSpellPriceBringsHighElfToSeventeen()
    {
        var catalog = Catalog();
        var price = NativeOriginFirstPartyBiologicalPricing.Create();
        Assert.Equal(NativeOriginFirstPartyBiologicalPricing.Revision, price.Revision);
        var byMember = price.Prices.SelectMany(x =>
                x.MemberKeys.Select(key => (Key: key, Entry: x)))
            .ToDictionary(x => x.Key, x => x.Entry, StringComparer.Ordinal);

        var baseElf = Definition(catalog, "species.elf");
        var highElf = Definition(catalog, "ancestry.elf.high");
        var baseCost = baseElf.GetProperty("traits").EnumerateArray()
            .Select((_, index) => catalog.OriginTraitDefinitionId("species.elf", index))
            .Sum(id => byMember["trait:" + id].Load);
        var ancestryCost = highElf.GetProperty("traits").EnumerateArray()
            .Select((_, index) => catalog.OriginTraitDefinitionId("ancestry.elf.high", index))
            .Sum(id => byMember["trait:" + id].Load);
        var abilityCost = byMember["intrinsic:spellcastingAbility"].Load;
        Assert.Equal(7, baseCost);
        Assert.Equal(9, ancestryCost);
        Assert.Equal(1, abilityCost);
        Assert.Equal(17, baseCost + ancestryCost + abilityCost);
        Assert.Equal(5, byMember["trait:trait.misty-step"].Load);
        Assert.Equal(2, byMember["trait:trait.detect-magic"].Load);
        Assert.Equal(2, byMember["trait:trait.arcane-cantrip"].Load);
    }

    [Fact]
    public void SharedRecoveryPrimitiveRefillsEnhancedSpellOnBothRestTypes()
    {
        var step = new NativeResource("origin-spell-free:misty-step",
            1, 0, "short-or-long-rest");
        var shortRest = NativeResourceResolution.Recover(step, 0,
            NativeRecoveryEvent.ShortRest);
        Assert.Equal("resolved", shortRest.Status);
        Assert.Equal(1, shortRest.Current);
        Assert.Equal(1, shortRest.Maximum);
        var longRest = NativeResourceResolution.Recover(step, 0,
            NativeRecoveryEvent.LongRest);
        Assert.Equal("resolved", longRest.Status);
        Assert.Equal(1, longRest.Current);

        var detect = new NativeResource("origin-spell-free:detect-magic",
            1, 0, "long-rest");
        var nonRecovery = NativeResourceResolution.Recover(detect, 0,
            NativeRecoveryEvent.ShortRest);
        Assert.Equal("resolved", nonRecovery.Status);
        Assert.Equal(0, nonRecovery.Current);
        Assert.Equal(1, NativeResourceResolution.Recover(detect, 0,
            NativeRecoveryEvent.LongRest).Current);
    }

}
