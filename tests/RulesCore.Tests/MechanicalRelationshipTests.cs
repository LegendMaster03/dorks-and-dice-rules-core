using RulesCore.Domain.Rules;

namespace RulesCore.Tests;

public sealed class MechanicalRelationshipTests
{
    [Fact]
    public void KnownCompositeSkillsPreserveTheReviewedManyToOneStructure()
    {
        var expected = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["skill.stealth"] = ["skill.hide", "skill.move-silently"],
            ["skill.perception"] = ["skill.listen", "skill.spot"],
            ["skill.athletics"] = ["skill.climb", "skill.jump", "skill.swim"],
            ["skill.acrobatics"] = ["skill.balance", "skill.tumble"]
        };

        Assert.Equal(expected.Count, KnownMechanicalRelationships.All.Count);
        foreach (var definition in KnownMechanicalRelationships.All)
        {
            Assert.Equal(MechanicalRelationshipKinds.CompositeSkill, definition.Kind);
            Assert.Equal(MechanicalRelationshipCompositionKinds.ArithmeticMean, definition.Composition);
            Assert.Equal(MechanicalRelationshipDirections.ComponentsToParent, definition.Direction);
            Assert.True(expected.TryGetValue(definition.Parent.ConceptKey, out var components));
            Assert.Equal(components, definition.Components.Select(value => value.ConceptKey).ToArray());
            Assert.DoesNotContain(
                definition.Components,
                value => string.Equals(value.ConceptKey, definition.Parent.ConceptKey, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ChildModifiersAffectDerivedParentButParentModifiersDoNotFlowBackToChildren()
    {
        var stealth = Assert.Single(
            KnownMechanicalRelationships.FindAllByConceptKey("skill.stealth"));
        var baseValues = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["skill.hide"] = 9,
            ["skill.move-silently"] = 3
        };

        var initial = CompositeCompetencyEvaluator.Evaluate(stealth, baseValues, []);
        Assert.Equal(6, initial.ParentValue);
        Assert.Equal(9, initial.EffectiveComponentValues["skill.hide"]);
        Assert.Equal(3, initial.EffectiveComponentValues["skill.move-silently"]);

        var childModified = CompositeCompetencyEvaluator.Evaluate(
            stealth,
            baseValues,
            [new CompetencyModifier("skill.hide", 2)]);
        Assert.Equal(7, childModified.ParentValue);
        Assert.Equal(11, childModified.EffectiveComponentValues["skill.hide"]);
        Assert.Equal(3, childModified.EffectiveComponentValues["skill.move-silently"]);

        var parentModified = CompositeCompetencyEvaluator.Evaluate(
            stealth,
            baseValues,
            [new CompetencyModifier("skill.stealth", 2)]);
        Assert.Equal(8, parentModified.ParentValue);
        Assert.Equal(2, parentModified.ExplicitParentModifier);
        Assert.Equal(9, parentModified.EffectiveComponentValues["skill.hide"]);
        Assert.Equal(3, parentModified.EffectiveComponentValues["skill.move-silently"]);
    }

    [Fact]
    public void ArithmeticMeanTruncatesTowardZero()
    {
        var stealth = Assert.Single(
            KnownMechanicalRelationships.FindAllByConceptKey("skill.stealth"));
        var evaluation = CompositeCompetencyEvaluator.Evaluate(
            stealth,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["skill.hide"] = -2,
                ["skill.move-silently"] = -1
            },
            []);

        Assert.Equal(-1, evaluation.ParentValue);
    }

    [Theory]
    [InlineData("skill.search")]
    [InlineData("skill.escape-artist")]
    [InlineData("skill.spellcraft")]
    [InlineData("skill.gather-information")]
    [InlineData("skill.ride")]
    [InlineData("skill.use-rope")]
    [InlineData("skill.concentration")]
    [InlineData("skill.use-magic-device")]
    public void AmbiguousOrDeferredSkillsAreNotAutomaticallyAttached(string conceptKey)
    {
        Assert.Empty(KnownMechanicalRelationships.FindAllByConceptKey(conceptKey));
    }
}
