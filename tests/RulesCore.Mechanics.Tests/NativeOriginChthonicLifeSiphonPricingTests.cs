using RulesCore.Content;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Approved Chthonic Tiefling life-siphon origin grant and trial allocation.
/// The catalog and price test does not simulate damage, restorative resource
/// spending, actual combat turn ordering, or Hit Point change execution.
/// </summary>
public sealed class NativeOriginChthonicLifeSiphonPricingTests
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


    [Fact]
    public void ChthonicSiphonRequiresDeliveredNecroticDamageAndLimitsRecovery()
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("trait.chthonic-life-siphon",
            out var entity));
        var trait = entity!.Definition.GetProperty("trait");
        Assert.True(trait.GetProperty("oncePerTurn").GetBoolean());
        Assert.False(trait.TryGetProperty("abilityScoreIncrease", out _));
        Assert.False(trait.TryGetProperty("requiredCharacterLevel", out _));
        var siphon = trait.GetProperty("lifeSiphon");
        Assert.Equal("deal-necrotic-damage-to-another-creature",
            siphon.GetProperty("trigger").GetString());
        Assert.Equal("necrotic", siphon.GetProperty("damageType").GetString());
        Assert.Equal("necrotic-damage-actually-taken",
            siphon.GetProperty("damageMeasured").GetString());
        Assert.True(siphon.GetProperty("requiresPositiveDamage").GetBoolean());
        var healing = siphon.GetProperty("healing");
        Assert.Equal("self", healing.GetProperty("recipient").GetString());
        Assert.Equal("proficiency-bonus",
            healing.GetProperty("amount").GetString());
        Assert.Equal("necrotic-damage-actually-taken",
            healing.GetProperty("maximum").GetString());
        Assert.True(healing.GetProperty("obeysNormalHitPointMaximum").GetBoolean());

        var uses = trait.GetProperty("uses");
        Assert.Equal("proficiency-bonus", uses.GetProperty("value").GetString());
        Assert.Equal("long-rest", uses.GetProperty("recharge").GetString());
    }

}
