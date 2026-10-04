using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Tests;

public sealed class WikiReferenceBrowserFieldNormalizerTests
{
    [Theory]
    [InlineData("G", "General")]
    [InlineData("EB", "Epic Boon")]
    [InlineData("DG", "Dark Gift")]
    public void FeatCategoryCodesBecomeHumanReadable(string sourceValue, string expected)
    {
        var normalized = WikiReferenceBrowserFieldNormalizer.Normalize(Catalog(
            "feat",
            new ResolvedRuleBrowserFieldView("category", "Category", sourceValue)));

        Assert.Equal(expected, Field(normalized, "category"));
    }

    [Theory]
    [InlineData("M", "Melee Weapon")]
    [InlineData("RD|XDMG", "Rod")]
    [InlineData("SCF", "Spellcasting Focus")]
    public void ItemTypeCodesBecomeHumanReadable(string sourceValue, string expected)
    {
        var normalized = WikiReferenceBrowserFieldNormalizer.Normalize(Catalog(
            "item",
            new ResolvedRuleBrowserFieldView("type", "Type", sourceValue)));

        Assert.Equal(expected, Field(normalized, "type"));
    }

    [Fact]
    public void UnknownValuesRemainSourceFaithful()
    {
        var normalized = WikiReferenceBrowserFieldNormalizer.Normalize(Catalog(
            "item",
            new ResolvedRuleBrowserFieldView("type", "Type", "Custom Relic")));

        Assert.Equal("Custom Relic", Field(normalized, "type"));
    }

    private static string Field(WikiReferenceCatalogView catalog, string key) =>
        Assert.Single(Assert.Single(catalog.References).BrowserFields, field => field.Key == key).Value;

    private static WikiReferenceCatalogView Catalog(
        string category,
        ResolvedRuleBrowserFieldView field)
    {
        var variation = new WikiReferenceVariationSummaryView(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "Fixture",
            category,
            "XPHB",
            "fixture-package",
            "Fixture Package",
            Guid.NewGuid(),
            "fixture-publication",
            "Fixture Publication",
            "5.5e",
            "5.5e",
            new DateOnly(2024, 9, 17),
            true);
        var reference = new WikiReferenceItemView(
            "fixture",
            null,
            null,
            "Fixture",
            category,
            category,
            "5.5e",
            "5.5e",
            WikiReferenceResolutionStates.UnresolvedFallback,
            false,
            variation,
            variation,
            [],
            [field],
            []);
        return new WikiReferenceCatalogView(
            "global",
            null,
            null,
            null,
            1,
            WikiReferenceCategoryModes.AnyVariation,
            [],
            [],
            [],
            [],
            [reference]);
    }
}
