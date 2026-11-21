using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Checks native catalog composition and deliberately incomplete numeric authoring
/// for the new physical component families. These checks do not approve balance.
/// </summary>
public sealed class NativeConstructComponentExpansionTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var root = Path.Combine(dir.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(root, "first-party-001", "native",
                "rules", "creature-origination.json")))
                return NativeCatalogLoader.Load(
                    NativeRulesetLayout.Resolve(root, "first-party-001"));
        }
        throw new InvalidOperationException("First-party native data unavailable.");
    }

    [Fact]
    public void ExpandedComponentsResolveWithFunctionalCategories()
    {
        var catalog = Catalog();
        var cases = new (string Id, string Category)[]
        {
            ("construct-component.climbing-attachment", "locomotion"),
            ("construct-component.flight-propulsion", "locomotion"),
            ("construct-component.excavation-drive", "locomotion"),
            ("construct-component.aquatic-propulsion", "locomotion"),
            ("construct-component.projectile-launcher", "weapon-systems"),
            ("construct-component.substance-projector", "utility"),
            ("construct-component.energy-discharge-unit", "weapon-systems"),
            ("construct-component.energy-accumulator", "power"),
            ("construct-component.protective-field-projector", "defense"),
            ("construct-component.spell-storage-module", "utility"),
            ("construct-component.repair-assembly", "utility"),
            ("construct-component.restraint-mechanism", "manipulation"),
            ("construct-component.external-control-interface", "control"),
        };
        foreach (var (id, category) in cases)
        {
            Assert.True(catalog.TryGetById(id, out var component), id);
            Assert.Equal("construct-component", component!.Category);
            Assert.Equal(category,
                component.Definition.GetProperty("componentCategory").GetString());
            Assert.Equal("native/origination/construct-assembly/components/" +
                category + "/" + id["construct-component.".Length..] + ".json",
                component.SourcePath);
            Assert.NotEmpty(component.Definition.GetProperty("provides").EnumerateArray());
            Assert.NotEmpty(component.Definition.GetProperty("materialSlots").EnumerateArray());
        }
    }

    [Theory]
    [InlineData("climbing-attachment", "climbing", "climb")]
    [InlineData("flight-propulsion", "flying", "fly")]
    [InlineData("excavation-drive", "burrowing", "burrow")]
    [InlineData("aquatic-propulsion", "swimming", "swim")]
    public void MovementComponentsReferToCanonicalModeCandidates(
        string componentSlug, string traitName, string mode)
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("construct-component." + componentSlug,
            out var part));
        var refs = part!.Definition.GetProperty("operationalOriginTraitIds");
        var id = Assert.Single(refs.EnumerateArray()).GetString();
        Assert.Equal("trait.construct." + traitName + "-movement", id);
        Assert.True(catalog.TryGetById(id!, out var canonical));
        Assert.Equal("origin-trait", canonical!.Category);
        var effect = canonical.Definition.GetProperty("trait");
        Assert.Equal(mode, effect.GetProperty("movementMode").GetString());
        Assert.True(effect.GetProperty("requiresOperationalComponent").GetBoolean());
        Assert.False(effect.TryGetProperty("speedFeet", out _));
    }

    [Fact]
    public void UnbalancedFlightAssemblyIsUnresolvedRatherThanFree()
    {
        var catalog = Catalog();
        var part = new NativeConstructComponentInstance(
            "flight", "construct-component.flight-propulsion",
            Materials: new Dictionary<string, string>
            { ["propulsor"] = "construct-material.steel" });
        var result = new NativeConstructAssemblyEvaluator(catalog).Evaluate(
            new NativeConstructAssemblyRequest([part], InitialAllowance: 200));
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, result.Status);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "crafting-quote-recipe-unresolved");
        Assert.Contains(result.Issues, issue =>
            issue.Code == "construct-power-demand-unresolved");
    }
}
