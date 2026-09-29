using RulesCore.Domain.Rules;

namespace RulesCore.Tests;

public sealed class RuleConceptEntityTypeNormalizationTests
{
    [Theory]
    [InlineData("race", RuleConceptEntityTypes.Species)]
    [InlineData("RACE", RuleConceptEntityTypes.Species)]
    [InlineData("species", RuleConceptEntityTypes.Species)]
    [InlineData("SPECIES", RuleConceptEntityTypes.Species)]
    [InlineData("subrace", RuleConceptEntityTypes.Subspecies)]
    [InlineData("SUBRACE", RuleConceptEntityTypes.Subspecies)]
    [InlineData("subspecies", RuleConceptEntityTypes.Subspecies)]
    [InlineData("SUBSPECIES", RuleConceptEntityTypes.Subspecies)]
    public void LegacySpeciesTerminologyNormalizesToCanonicalApiNames(string input, string expected)
    {
        Assert.Equal(expected, RuleConceptEntityTypes.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ra\nce")]
    [InlineData("sub\0race")]
    public void MalformedEntityTypesAreRejected(string input)
    {
        Assert.Throws<ArgumentException>(() => RuleConceptEntityTypes.Normalize(input));
    }

    [Fact]
    public void OversizedEntityTypeIsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            RuleConceptEntityTypes.Normalize(new string('x', 121)));
    }

    [Fact]
    public void UnknownEntityTypeIsTrimmedAndCanonicalizedForStableComparison()
    {
        Assert.Equal("customtype", RuleConceptEntityTypes.Normalize("  CustomType  "));
    }
}
