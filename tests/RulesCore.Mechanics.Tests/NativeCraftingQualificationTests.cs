using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeCraftingQualificationTests
{
    [Theory]
    [InlineData(0, "Untrained")]
    [InlineData(1, "Apprentice")]
    [InlineData(2, "Journeyman")]
    [InlineData(3, "Expert")]
    [InlineData(4, "Artisan")]
    [InlineData(5, "Master")]
    [InlineData(6, "Grandmaster")]
    [InlineData(7, "Grandmaster")]
    public void CompetencyRankTitlesAreDirectlyDerivedFromNumericRanks(
        int ranks, string title)
    {
        Assert.Equal(title, NativeCompetencyRankNames.ForRank(ranks));
    }

    [Fact]
    public void InvalidRankHasNoTitle()
    {
        Assert.Null(NativeCompetencyRankNames.ForRank(-1));
    }

    [Theory]
    [InlineData("Common", 1)]
    [InlineData("UNCOMMON", 2)]
    [InlineData("Rare", 3)]
    [InlineData("Very Rare", 4)]
    [InlineData("Very-Rare", 4)]
    [InlineData("Legendary", 5)]
    [InlineData("Artifact", 6)]
    public void RarityRequiresCorrespondingCompetencyRank(
        string rarity, int required)
    {
        var atRank = NativeCraftingQualification.Evaluate(
            new(rarity, required, true, true));
        Assert.Equal("eligible", atRank.Status);
        Assert.True(atRank.Eligible);
        Assert.Equal(required, atRank.MinimumRanks);

        var below = NativeCraftingQualification.Evaluate(
            new(rarity, required - 1, true, true));
        Assert.Equal("ineligible", below.Status);
        Assert.False(below.Eligible);
        Assert.Contains("competency-rank-below-item-rarity", below.Reasons);

        var above = NativeCraftingQualification.Evaluate(
            new(rarity, 6, true, true));
        Assert.True(above.Eligible);
    }

    [Fact]
    public void RankZeroIsValidUntrainedButCannotCraftEvenCommonMagicItems()
    {
        var untrained = NativeCraftingQualification.Evaluate(
            new("Common", 0, true, true));
        Assert.Equal("ineligible", untrained.Status);
        Assert.Equal(1, untrained.MinimumRanks);
        Assert.Contains("competency-rank-below-item-rarity", untrained.Reasons);
    }

    [Theory]
    [InlineData(false, true, "recipe-knowledge-missing")]
    [InlineData(true, false, "required-materials-missing")]
    public void KnowledgeAndMaterialsRemainIndependentRequirements(
        bool knowsRecipe, bool hasMaterials, string reason)
    {
        var result = NativeCraftingQualification.Evaluate(new(
            "Rare", 5, knowsRecipe, hasMaterials));
        Assert.Equal("ineligible", result.Status);
        Assert.Contains(reason, result.Reasons);
    }

    [Fact]
    public void UnresolvedInformationNeverQualifiesAProject()
    {
        var missingKnowledge = NativeCraftingQualification.Evaluate(new(
            "Rare", 3, null, true));
        Assert.Equal("unresolved", missingKnowledge.Status);
        Assert.Null(missingKnowledge.Eligible);

        var unknownRank = NativeCraftingQualification.Evaluate(
            new("Rare", null, true, true));
        Assert.Equal("unresolved", unknownRank.Status);

        var unknownRarity = NativeCraftingQualification.Evaluate(
            new("Varies", 6, true, true));
        Assert.Equal("unresolved", unknownRarity.Status);
        Assert.Null(unknownRarity.MinimumRanks);

        var invalidRank = NativeCraftingQualification.Evaluate(
            new("Rare", -1, true, true));
        Assert.Equal("unresolved", invalidRank.Status);
        Assert.False(NativeCraftingQualification.TryParseRarity("99", out _));
    }

    [Fact]
    public void KnownMissingRequirementIsNeverOverriddenByUnknownInputs()
    {
        var result = NativeCraftingQualification.Evaluate(
            new(null, null, false, null));
        Assert.Equal("ineligible", result.Status);
        Assert.False(result.Eligible);
        Assert.Contains("recipe-knowledge-missing", result.Reasons);
        Assert.Contains("unknown-item-rarity", result.Reasons);
    }

    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(1, 2, false)]
    [InlineData(4, 4, true)]
    [InlineData(6, 5, true)]
    public void InstructorCanNeverTeachBeyondOwnRank(
        int instructor, int target, bool expected)
    {
        var result = NativeCraftingQualification.EvaluateTraining(
            new(instructor, target));
        Assert.Equal(expected, result.Eligible);
        Assert.Equal(expected ? "eligible" : "ineligible", result.Status);
    }

    [Fact]
    public void InvalidTrainingTargetIsNotAutomaticallyAuthorized()
    {
        Assert.Equal("unresolved", NativeCraftingQualification.EvaluateTraining(
            new(null, 1)).Status);
        Assert.Equal("unresolved", NativeCraftingQualification.EvaluateTraining(
            new(5, 0)).Status);
        Assert.Equal("unresolved", NativeCraftingQualification.EvaluateTraining(
            new(5, 7)).Status);
    }

    [Fact]
    public void QualifiedManufacturingDoesNotRunIfPrerequisitesFail()
    {
        using var fixture = new Fixture();
        var resolver = new NativeCraftingResolver(fixture.Catalog);
        var request = new NativeManufacturingRequest(
            "first-party-001", fixture.Catalog.Fingerprint,
            new("Smithing", 3, true),
            D20Roll: 20, TargetDc: 1, ManualAbilityModifier: 2);
        var denied = resolver.ResolveQualifiedManufacturing(
            request, new("Legendary", 3, true, true));
        Assert.Equal("ineligible", denied.Qualification.Status);
        Assert.Null(denied.Manufacturing);

        var allowed = resolver.ResolveQualifiedManufacturing(
            request, new("Rare", 3, true, true));
        Assert.True(allowed.Qualification.Eligible);
        Assert.Equal("completed", allowed.Manufacturing?.Outcome);
        Assert.True(allowed.Manufacturing?.InputsConsumed);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _directory =
            Path.Combine(Path.GetTempPath(), "native-qualified-craft-" +
                Guid.NewGuid().ToString("N"));

        public NativeCatalogSnapshot Catalog { get; }

        public Fixture()
        {
            var folder = Path.Combine(_directory, "first-party-001");
            Directory.CreateDirectory(Path.Combine(folder, "native"));
            File.WriteAllText(Path.Combine(folder, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            File.WriteAllText(Path.Combine(folder, "native", "class.json"),
                """{"id":"class.demo","name":"Demo","kind":"class"}""");
            Catalog = NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(
                _directory, "first-party-001"));
        }

        public void Dispose() => Directory.Delete(_directory, true);
    }
}
