using RulesCore.Domain.Rules;

namespace RulesCore.Tests;

public sealed class CharacterMechanicResolutionTests
{
    [Fact]
    public void DependencyPlannerProducesStableTopologicalOrder()
    {
        var plan = CharacterDependencyPlanner.Plan(
        [
            new CharacterDependencyNode("attack", ["ability.modifier", "proficiency"]),
            new CharacterDependencyNode("ability.modifier", ["ability.score"]),
            new CharacterDependencyNode("proficiency", ["level"]),
            new CharacterDependencyNode("level", []),
            new CharacterDependencyNode("ability.score", [])
        ]);

        Assert.True(plan.CanEvaluate);
        Assert.Empty(plan.MissingDependencyKeys);
        Assert.Empty(plan.CyclicNodeKeys);
        Assert.True(Array.IndexOf(plan.EvaluationOrder.ToArray(), "ability.score") < Array.IndexOf(plan.EvaluationOrder.ToArray(), "ability.modifier"));
        Assert.True(Array.IndexOf(plan.EvaluationOrder.ToArray(), "level") < Array.IndexOf(plan.EvaluationOrder.ToArray(), "proficiency"));
        Assert.True(Array.IndexOf(plan.EvaluationOrder.ToArray(), "ability.modifier") < Array.IndexOf(plan.EvaluationOrder.ToArray(), "attack"));
        Assert.True(Array.IndexOf(plan.EvaluationOrder.ToArray(), "proficiency") < Array.IndexOf(plan.EvaluationOrder.ToArray(), "attack"));
    }

    [Fact]
    public void DependencyPlannerReportsMissingDependenciesWithoutTreatingThemAsZero()
    {
        var plan = CharacterDependencyPlanner.Plan(
        [
            new CharacterDependencyNode("save.fortitude", ["ability.constitution.modifier", "save.fortitude.base"])
        ]);

        Assert.False(plan.CanEvaluate);
        Assert.Equal(
            new[] { "ability.constitution.modifier", "save.fortitude.base" },
            plan.MissingDependencyKeys);
        Assert.Empty(plan.CyclicNodeKeys);
    }

    [Fact]
    public void DependencyPlannerDetectsCycles()
    {
        var plan = CharacterDependencyPlanner.Plan(
        [
            new CharacterDependencyNode("a", ["b"]),
            new CharacterDependencyNode("b", ["c"]),
            new CharacterDependencyNode("c", ["a"])
        ]);

        Assert.False(plan.CanEvaluate);
        Assert.Empty(plan.MissingDependencyKeys);
        Assert.Equal(new[] { "a", "b", "c" }, plan.CyclicNodeKeys);
    }

    [Theory]
    [InlineData(1, -5)]
    [InlineData(8, -1)]
    [InlineData(9, -1)]
    [InlineData(10, 0)]
    [InlineData(11, 0)]
    [InlineData(12, 1)]
    [InlineData(18, 4)]
    public void StandardAbilityModifierUsesDndFloorSemantics(int score, int expected)
    {
        Assert.Equal(expected, StandardDndCharacterMath.AbilityModifier(score));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(9, 4)]
    [InlineData(13, 5)]
    [InlineData(17, 6)]
    [InlineData(20, 6)]
    public void StandardProficiencyProgressionIsRulesCoreOwned(int level, int expected)
    {
        Assert.Equal(expected, StandardDndCharacterMath.ProficiencyBonusForCharacterLevel(level));
    }


    [Theory]
    [InlineData(1, "full", 1)]
    [InlineData(5, "full", 5)]
    [InlineData(1, "three-quarters", 0)]
    [InlineData(5, "three-quarters", 3)]
    [InlineData(8, "three-quarters", 6)]
    [InlineData(1, "half", 0)]
    [InlineData(5, "half", 2)]
    public void ThreeXBaseAttackProgressionsUseClassLevelTables(
        int level,
        string progression,
        int expected)
    {
        Assert.Equal(
            expected,
            ThreeXClassProgressionMath.BaseAttackBonus(level, progression));
    }

    [Theory]
    [InlineData(1, "good", 2)]
    [InlineData(2, "good", 3)]
    [InlineData(5, "good", 4)]
    [InlineData(1, "poor", 0)]
    [InlineData(3, "poor", 1)]
    [InlineData(5, "poor", 1)]
    [InlineData(6, "poor", 2)]
    public void ThreeXSaveProgressionsUseClassLevelTables(
        int level,
        string progression,
        int expected)
    {
        Assert.Equal(
            expected,
            ThreeXClassProgressionMath.BaseSave(level, progression));
    }

    [Fact]
    public void ResolutionStatesDoNotConflateUnknownWithFalseOrZero()
    {
        Assert.Contains(CharacterResolutionStates.Resolved, CharacterResolutionStates.All);
        Assert.Contains(CharacterResolutionStates.MissingCharacterInput, CharacterResolutionStates.All);
        Assert.Contains(CharacterResolutionStates.MissingCapability, CharacterResolutionStates.All);
        Assert.Contains(CharacterResolutionStates.ChoiceRequired, CharacterResolutionStates.All);
        Assert.Contains(CharacterResolutionStates.RollRequired, CharacterResolutionStates.All);
        Assert.Contains(CharacterResolutionStates.SourceUnavailable, CharacterResolutionStates.All);
        Assert.Contains(CharacterResolutionStates.NotApplicable, CharacterResolutionStates.All);
        Assert.Contains(CharacterResolutionStates.Conflict, CharacterResolutionStates.All);
        Assert.Equal(9, CharacterResolutionStates.All.Count);
    }
}
