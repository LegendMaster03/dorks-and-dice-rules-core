using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeCraftingQualificationTests
{
    [Theory]
    [InlineData("Common", NativeCompetencyMasteryTier.Apprentice)]
    [InlineData("UNCOMMON", NativeCompetencyMasteryTier.Journeyman)]
    [InlineData("Rare", NativeCompetencyMasteryTier.Expert)]
    [InlineData("Very Rare", NativeCompetencyMasteryTier.Artisan)]
    [InlineData("Very-Rare", NativeCompetencyMasteryTier.Artisan)]
    [InlineData("Legendary", NativeCompetencyMasteryTier.Master)]
    [InlineData("Artifact", NativeCompetencyMasteryTier.Grandmaster)]
    public void RarityRequiresCorrespondingMastery(
        string rarity, NativeCompetencyMasteryTier required)
    {
        var atTier = NativeCraftingQualification.Evaluate(
            new(rarity, required, true, true));
        Assert.Equal("eligible", atTier.Status);
        Assert.True(atTier.Eligible);
        Assert.Equal(required, atTier.MinimumTier);

        if (required == NativeCompetencyMasteryTier.Apprentice) return;
        var below = NativeCraftingQualification.Evaluate(
            new(rarity, required - 1, true, true));
        Assert.Equal("ineligible", below.Status);
        Assert.False(below.Eligible);
        Assert.Contains("competency-tier-below-item-rarity", below.Reasons);

        var above = NativeCraftingQualification.Evaluate(
            new(rarity, NativeCompetencyMasteryTier.Grandmaster, true, true));
        Assert.True(above.Eligible);
    }

    [Theory]
    [InlineData(false, true, "recipe-knowledge-missing")]
    [InlineData(true, false, "required-materials-missing")]
    public void KnowledgeAndMaterialsRemainIndependentRequirements(
        bool knowsRecipe, bool hasMaterials, string reason)
    {
        var result = NativeCraftingQualification.Evaluate(new(
            "Rare", NativeCompetencyMasteryTier.Master, knowsRecipe, hasMaterials));
        Assert.Equal("ineligible", result.Status);
        Assert.Contains(reason, result.Reasons);
    }

    [Fact]
    public void UnresolvedInformationNeverQualifiesAProject()
    {
        var missingKnowledge = NativeCraftingQualification.Evaluate(new(
            "Rare", NativeCompetencyMasteryTier.Expert, null, true));
        Assert.Equal("unresolved", missingKnowledge.Status);
        Assert.Null(missingKnowledge.Eligible);

        var unranked = NativeCraftingQualification.Evaluate(
            new("Rare", null, true, true));
        Assert.Equal("unresolved", unranked.Status);

        var unknownRarity = NativeCraftingQualification.Evaluate(
            new("Varies", NativeCompetencyMasteryTier.Grandmaster, true, true));
        Assert.Equal("unresolved", unknownRarity.Status);
        Assert.Null(unknownRarity.MinimumTier);

        var invalidTier = NativeCraftingQualification.Evaluate(
            new("Rare", (NativeCompetencyMasteryTier)100, true, true));
        Assert.Equal("unresolved", invalidTier.Status);
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
    [InlineData(NativeCompetencyMasteryTier.Apprentice, NativeCompetencyMasteryTier.Journeyman, false)]
    [InlineData(NativeCompetencyMasteryTier.Artisan, NativeCompetencyMasteryTier.Artisan, true)]
    [InlineData(NativeCompetencyMasteryTier.Grandmaster, NativeCompetencyMasteryTier.Master, true)]
    public void InstructorCanNeverTeachBeyondOwnMastery(
        NativeCompetencyMasteryTier instructor,
        NativeCompetencyMasteryTier target, bool expected)
    {
        var result = NativeCraftingQualification.EvaluateTraining(
            new(instructor, target));
        Assert.Equal(expected, result.Eligible);
        Assert.Equal(expected ? "eligible" : "ineligible", result.Status);
    }

    [Fact]
    public void UnknownInstructorOrTargetDoesNotAuthorizeTraining()
    {
        Assert.Equal("unresolved", NativeCraftingQualification.EvaluateTraining(
            new(null, NativeCompetencyMasteryTier.Apprentice)).Status);
        Assert.Equal("unresolved", NativeCraftingQualification.EvaluateTraining(
            new(NativeCompetencyMasteryTier.Master,
                (NativeCompetencyMasteryTier)99)).Status);
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
            request, new("Legendary", NativeCompetencyMasteryTier.Expert, true, true));
        Assert.Equal("ineligible", denied.Qualification.Status);
        Assert.Null(denied.Manufacturing);

        var allowed = resolver.ResolveQualifiedManufacturing(
            request, new("Rare", NativeCompetencyMasteryTier.Expert, true, true));
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
