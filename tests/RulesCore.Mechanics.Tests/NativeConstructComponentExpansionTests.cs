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
            ("construct-component.operator-station", "control"),
            ("construct-component.deployable-unit-bay", "storage-and-transport"),
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

    [Fact]
    public void NewDesignsAndEnhancementsMatchExistingComponentCapabilities()
    {
        var catalog = Catalog();
        var componentCapabilities = catalog.Search(category: "construct-component", limit: 100)
            .Items.SelectMany(x => x.Definition.GetProperty("provides").EnumerateArray()
                .Select(v => v.GetString()!))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var id in new[]
        {
            "construct-design.wheeled-drive",
            "construct-design.tracked-drive",
            "construct-design.multi-legged-drive",
            "construct-design.rolling-body",
            "construct-design.compliant-manipulator",
            "construct-design.layered-plating",
            "construct-design.distributed-control",
            "construct-design.multi-operator-control"
        })
            VerifyOption(id, "construct-design");

        foreach (var id in new[]
        {
            "construct-enhancement.vibration-sensing",
            "construct-enhancement.active-ranging",
            "construct-enhancement.magical-sensing",
            "construct-enhancement.target-acquisition",
            "construct-enhancement.clawed-grip",
            "construct-enhancement.precision-control",
            "construct-enhancement.interchangeable-tool-coupling",
            "construct-enhancement.hazard-adapted-lining",
            "construct-enhancement.energy-absorption-interface",
            "construct-enhancement.coverage-adaptation",
            "construct-enhancement.programmed-instructions",
            "construct-enhancement.supervisory-link",
            "construct-enhancement.remote-command-receiver",
            "construct-enhancement.occupant-protection",
            "construct-enhancement.compartment-access",
            "construct-enhancement.environmental-support",
            "construct-enhancement.operator-controls"
        })
            VerifyOption(id, "construct-enhancement");

        void VerifyOption(string id, string kind)
        {
            Assert.True(catalog.TryGetById(id, out var option), id);
            Assert.Equal(kind, option!.Category);
            var capabilities = option.Definition
                .GetProperty("compatibleComponentCapabilities")
                .EnumerateArray().Select(x => x.GetString()!).ToArray();
            Assert.Contains(capabilities, componentCapabilities.Contains);
            Assert.NotEmpty(option.Definition.GetProperty("provides").EnumerateArray());
        }
    }

    [Fact]
    public void AdditionalMaterialsAreRealUnpricedDefinitions()
    {
        var catalog = Catalog();
        foreach (var slug in new[]
        {
            "clay", "terracotta", "ceramic", "glass", "bone",
            "preserved-tissue", "amber", "wax", "paper", "wicker"
        })
        {
            Assert.True(catalog.TryGetById("construct-material." + slug,
                out var material), slug);
            Assert.Equal("construct-material", material!.Category);
            Assert.Equal(JsonValueKind.Object,
                material.Definition.GetProperty("physicalProfile").ValueKind);
        }

        // A genuinely selected but still unpriced material is not free.
        var plated = new NativeConstructComponentInstance("glass-plating",
            "construct-component.protective-plating",
            Materials: new Dictionary<string, string>
                { ["plating"] = "construct-material.glass" });
        var assessment = new NativeConstructAssemblyEvaluator(catalog).Evaluate(
            new NativeConstructAssemblyRequest([plated], InitialAllowance: 200));
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, assessment.Status);
        Assert.Contains(assessment.Issues, issue =>
            issue.Code == "crafting-quote-resource-price-unresolved");
        Assert.Contains("construct-material.glass",
            assessment.UnresolvedRetainedMassMaterials ?? []);
    }

    [Theory]
    [InlineData("operator-station")]
    [InlineData("deployable-unit-bay")]
    public void AdvancedControlAndDeploymentRequireAuthoredProduction(string slug)
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("construct-component." + slug,
            out var component));
        var materialSlot = Assert.Single(component!.Definition
            .GetProperty("materialSlots").EnumerateArray()).GetString()!;
        var installed = new NativeConstructComponentInstance(slug,
            component.Id, Materials: new Dictionary<string, string>
                { [materialSlot] = "construct-material.steel" });
        var assessment = new NativeConstructAssemblyEvaluator(catalog).Evaluate(
            new NativeConstructAssemblyRequest([installed], InitialAllowance: 200));
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, assessment.Status);
        Assert.Contains(assessment.Issues, issue =>
            issue.Code == "crafting-quote-recipe-unresolved");
        Assert.Contains(assessment.Issues, issue =>
            issue.Code == "construct-power-demand-unresolved");
    }
}
