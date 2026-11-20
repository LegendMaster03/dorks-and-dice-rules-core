using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeRecipeLearningTests
{
    [Theory]
    [InlineData(NativeRecipeAcquisitionKind.FundamentalAdvancement)]
    [InlineData(NativeRecipeAcquisitionKind.Research)]
    [InlineData(NativeRecipeAcquisitionKind.Discovery)]
    [InlineData(NativeRecipeAcquisitionKind.Treasure)]
    [InlineData(NativeRecipeAcquisitionKind.QuestReward)]
    [InlineData(NativeRecipeAcquisitionKind.RenownReward)]
    [InlineData(NativeRecipeAcquisitionKind.Purchase)]
    public void AuthorizedSourcePathsCanTeachKnownRecipe(NativeRecipeAcquisitionKind path)
    {
        var result = NativeRecipeLearning.Evaluate(
            new("competency.smithing", "source:recipe:blade", 4, 3,
                true, path, true));
        Assert.Equal("eligible", result.Status);
        Assert.True(result.CanLearn);
    }

    [Fact]
    public void SpecialRecipesAreNotAutomaticallyAwardedByRank()
    {
        var result = NativeRecipeLearning.Evaluate(
            new("competency.smithing", "source:secret", 6, 5,
                false, NativeRecipeAcquisitionKind.FundamentalAdvancement, true));
        Assert.Equal("ineligible", result.Status);
        Assert.Contains("specialized-recipe-not-learned-automatically", result.Reasons);
    }

    [Fact]
    public void RankDoesNotBypassUnauthorizedKnowledgeOrSpecialization()
    {
        var noSource = NativeRecipeLearning.Evaluate(
            new("competency.smithing", "source:secret", 6, 4,
                false, NativeRecipeAcquisitionKind.Discovery, false));
        Assert.Equal("ineligible", noSource.Status);

        var noSpecialization = NativeRecipeLearning.Evaluate(
            new("competency.tinkering", "source:gadget", 6, 3,
                false, NativeRecipeAcquisitionKind.Treasure, true,
                RequiredSpecializationId: "specialization.clockwork",
                KnownSpecializationIds: ["specialization.explosives"]));
        Assert.Equal("ineligible", noSpecialization.Status);
        Assert.Contains("required-specialization-missing", noSpecialization.Reasons);

        var specialized = NativeRecipeLearning.Evaluate(
            new("competency.tinkering", "source:gadget", 6, 3,
                false, NativeRecipeAcquisitionKind.Treasure, true,
                RequiredSpecializationId: "specialization.clockwork",
                KnownSpecializationIds: ["specialization.clockwork"]));
        Assert.True(specialized.CanLearn);
    }

    [Fact]
    public void TrainingRequiresTeacherWhoKnowsRecipeAndHasEnoughRank()
    {
        var noviceTrainer = NativeRecipeLearning.Evaluate(
            new("competency.alchemy", "source:elixir", 6, 4,
                false, NativeRecipeAcquisitionKind.Trainer, true, 3, true));
        Assert.Equal("ineligible", noviceTrainer.Status);
        Assert.Contains("instructor-rank-below-recipe-requirement", noviceTrainer.Reasons);

        var unknowingTrainer = NativeRecipeLearning.Evaluate(
            new("competency.alchemy", "source:elixir", 6, 4,
                false, NativeRecipeAcquisitionKind.Trainer, true, 6, false));
        Assert.Equal("ineligible", unknowingTrainer.Status);

        var goodTrainer = NativeRecipeLearning.Evaluate(
            new("competency.alchemy", "source:elixir", 4, 4,
                false, NativeRecipeAcquisitionKind.Trainer, true, 4, true));
        Assert.True(goodTrainer.CanLearn);
    }

    [Fact]
    public void LearnerBelowRankCannotLearnRecipeEvenFromMaster()
    {
        var result = NativeRecipeLearning.Evaluate(
            new("competency.alchemy", "source:elixir", 2, 4,
                false, NativeRecipeAcquisitionKind.Trainer, true, 6, true));
        Assert.Equal("ineligible", result.Status);
        Assert.Contains("learner-rank-below-recipe-requirement", result.Reasons);
    }

    [Fact]
    public void MissingAuthorizationCannotBeInferredFromRecipeName()
    {
        var result = NativeRecipeLearning.Evaluate(
            new("competency.cooking", "source:meal", 3, 1,
                true, NativeRecipeAcquisitionKind.FundamentalAdvancement, null));
        Assert.Equal("unresolved", result.Status);
        Assert.Null(result.CanLearn);
    }

    [Fact]
    public void SourceDefinedSpecializationIsCheckedForCraftingToo()
    {
        var denied = NativeCraftingQualification.Evaluate(
            new("Rare", 6, true, true,
                "specialization.clockwork", ["specialization.explosives"]));
        Assert.Equal("ineligible", denied.Status);
        Assert.Contains("required-specialization-missing", denied.Reasons);

        var unknown = NativeCraftingQualification.Evaluate(
            new("Rare", 6, true, true, "specialization.clockwork"));
        Assert.Equal("unresolved", unknown.Status);

        var allowed = NativeCraftingQualification.Evaluate(
            new("Rare", 3, true, true,
                "specialization.clockwork", ["specialization.clockwork"]));
        Assert.True(allowed.Eligible);

        // The same generic craft gate serves Cooking, Alchemy, Smithing, etc.
        Assert.True(NativeCraftingQualification.Evaluate(
            new("Uncommon", 2, true, true)).Eligible);
    }
}
