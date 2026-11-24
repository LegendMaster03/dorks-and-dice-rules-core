using RulesCore.Content;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Approved Infernal Tiefling Flames of Phlegethos biological increment.
/// This tests catalog shape and complete origin allocation, not execution
/// of spell damage dice, attack retaliation or dynamic light in gameplay.
/// </summary>
public sealed class NativeOriginInfernalFlamesPricingTests
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
    public void FlamesHaveBothOriginalMechanicalBenefitsWithoutAbilityIncrease()
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("trait.flames-of-phlegethos",
            out var entity));
        var effect = entity!.Definition.GetProperty("trait");
        Assert.False(effect.TryGetProperty("abilityScoreIncrease", out _));
        Assert.False(effect.TryGetProperty("uses", out _));
        Assert.False(effect.TryGetProperty("requiredCharacterLevel", out _));

        var damageReroll = effect.GetProperty("fireSpellDamageReroll");
        Assert.Equal("spell-you-cast",
            damageReroll.GetProperty("source").GetString());
        Assert.Equal("fire",
            damageReroll.GetProperty("damageType").GetString());
        Assert.Equal(1,
            damageReroll.GetProperty("triggerDieResult").GetInt32());
        Assert.Equal(1,
            damageReroll.GetProperty("rerollsPerDie").GetInt32());
        Assert.True(damageReroll.GetProperty("useNewResult").GetBoolean());

        var flames = effect.GetProperty("protectiveFlames");
        Assert.Equal("cast-spell-that-deals-fire-damage",
            flames.GetProperty("trigger").GetString());
        Assert.True(flames.GetProperty("optional").GetBoolean());
        Assert.Equal("end-of-your-next-turn",
            flames.GetProperty("endsAt").GetString());
        Assert.Equal(30, flames.GetProperty("brightLightFeet").GetInt32());
        Assert.Equal(30, flames.GetProperty("dimLightAdditionalFeet").GetInt32());
        Assert.False(flames.GetProperty("harmsSelfOrPossessions").GetBoolean());

        var retaliation = flames.GetProperty("retaliation");
        Assert.Equal("creature-within-5-feet-hits-you-with-melee-attack-while-flames-active",
            retaliation.GetProperty("trigger").GetString());
        Assert.Equal(5, retaliation.GetProperty("rangeFeet").GetInt32());
        Assert.Equal("1d4", retaliation.GetProperty("damageDice").GetString());
        Assert.Equal("fire", retaliation.GetProperty("damageType").GetString());
        Assert.Equal("none", retaliation.GetProperty("actionCost").GetString());
    }

}
