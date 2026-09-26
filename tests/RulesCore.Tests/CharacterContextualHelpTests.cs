using RulesCore.Domain.Rules;

namespace RulesCore.Tests;

public sealed class CharacterContextualHelpTests
{
    [Theory]
    [InlineData("defense.ac.touch")]
    [InlineData("defense.ac.flat-footed")]
    [InlineData("combat.base-attack-bonus")]
    [InlineData("combat.grapple")]
    [InlineData("defense.damage-reduction")]
    [InlineData("defense.spell-resistance")]
    [InlineData("resource.nonlethal-damage")]
    [InlineData("defense.miss-chance")]
    [InlineData("competency.armor-check-penalty")]
    [InlineData("competency.class-skill")]
    [InlineData("competency.trained-only")]
    public void UncommonMechanicsExposeProminentHelp(string topicKey)
    {
        var help = KnownCharacterContextualHelp.FindByTopicKey(topicKey);

        Assert.NotNull(help);
        Assert.Equal(CharacterHelpProminence.Prominent, help!.Prominence);
        Assert.False(string.IsNullOrWhiteSpace(help.ShortText));
    }

    [Theory]
    [InlineData("defense.ac.total")]
    [InlineData("health.maximum-hp")]
    [InlineData("combat.initiative")]
    [InlineData("proficiency.standard")]
    [InlineData("ability.strength.score")]
    [InlineData("ability.dexterity.modifier")]
    [InlineData("save.strength")]
    public void CommonMechanicsCanExposeNonProminentHelp(string topicKey)
    {
        var help = KnownCharacterContextualHelp.FindByTopicKey(topicKey);

        Assert.NotNull(help);
        Assert.Equal(CharacterHelpProminence.Standard, help!.Prominence);
    }

    [Fact]
    public void TouchTargetRemainsIndependentFromRollSelection()
    {
        var context = CharacterAttackResolutionSemantics.Create(
            "defense.ac.touch",
            CharacterMechanicRollModes.Normal,
            ["state.touch-attack"]);

        Assert.Equal("defense.ac.touch", context.TargetDefenseKey);
        Assert.Equal(CharacterMechanicRollModes.Normal, context.RollMode);
        Assert.Equal(["state.touch-attack"], context.TargetStateKeys);
    }

    [Fact]
    public void FlatFootedTargetIsNotInferredFromAdvantage()
    {
        var context = CharacterAttackResolutionSemantics.Create(
            "defense.ac.total",
            CharacterMechanicRollModes.Advantage,
            ["state.unseen-attacker"]);

        Assert.Equal("defense.ac.total", context.TargetDefenseKey);
        Assert.Equal(CharacterMechanicRollModes.Advantage, context.RollMode);
        Assert.DoesNotContain(
            "flat-footed",
            context.TargetStateKeys,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExplicitFlatFootedTargetRemainsFlatFooted()
    {
        var context = CharacterAttackResolutionSemantics.Create(
            "defense.ac.flat-footed",
            CharacterMechanicRollModes.Normal,
            ["state.flat-footed"]);

        Assert.Equal("defense.ac.flat-footed", context.TargetDefenseKey);
        Assert.Equal(["state.flat-footed"], context.TargetStateKeys);
    }

    [Fact]
    public void DeniedDexterityStateRemainsDistinctFromFlatFootedState()
    {
        var context = CharacterAttackResolutionSemantics.Create(
            "defense.ac.total",
            CharacterMechanicRollModes.Normal,
            ["state.denied-dexterity-defense"]);

        Assert.Equal(["state.denied-dexterity-defense"], context.TargetStateKeys);
        Assert.DoesNotContain(
            "state.flat-footed",
            context.TargetStateKeys,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void RollSelectionWithoutDefenseTargetDoesNotInventOne()
    {
        var context = CharacterAttackResolutionSemantics.Create(
            targetDefenseKey: null,
            CharacterMechanicRollModes.Advantage,
            targetStateKeys: []);

        Assert.Null(context.TargetDefenseKey);
        Assert.Equal(CharacterMechanicRollModes.Advantage, context.RollMode);
    }
}
