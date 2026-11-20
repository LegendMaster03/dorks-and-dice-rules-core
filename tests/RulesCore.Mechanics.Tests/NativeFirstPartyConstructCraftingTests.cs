using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Native first-party recipe and operating-state integration checks. Resource
/// values are candidate comparisons only: these do not authorize first-party
/// starting character creation, size, or final gameplay balance.
/// </summary>
public sealed class NativeFirstPartyConstructCraftingTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        var location = new DirectoryInfo(AppContext.BaseDirectory);
        while (location is not null)
        {
            var root = Path.Combine(location.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(root, "first-party-001", "native",
                "rules", "creature-origination.json")))
                return NativeCatalogLoader.Load(
                    NativeRulesetLayout.Resolve(root, "first-party-001"));
            location = location.Parent;
        }
        throw new InvalidOperationException("First-party native catalog missing.");
    }

    private static NativeConstructComponentInstance Part(
        string id, string definition, string? materialSlot = null,
        string? material = null, bool enabled = true,
        string? powerSource = null, string[]? links = null) =>
        new(id, "construct-component." + definition,
            materialSlot is null ? null : new Dictionary<string, string>
                { [materialSlot] = "construct-material." + material },
            ConnectedTo: links, Enabled: enabled, PowerSourceId: powerSource);

    private static NativeConstructAssemblyResult Quote(
        NativeCatalogSnapshot catalog, params NativeConstructComponentInstance[] components) =>
        new NativeConstructAssemblyEvaluator(catalog).Evaluate(
            new(components, InitialAllowance: 300));

    [Fact]
    public void MaterialSlotInputIsChargedByItsActualRecipeQuantity()
    {
        var catalog = Catalog();
        var steel = Quote(catalog, Part("frame", "structural-frame", "frame", "steel"));
        var wood = Quote(catalog, Part("frame", "structural-frame", "frame", "wood"));

        Assert.True(steel.IsAuthorized,
            string.Join("; ", steel.Issues.Select(issue => issue.Code)));
        Assert.True(wood.IsAuthorized);
        Assert.Equal(32, steel.KnownConstructionCost);
        Assert.Equal(16, wood.KnownConstructionCost);
        Assert.Equal(8, steel.RequiredConstructionResources!["construct-material.steel"]);
        Assert.Equal(8, wood.RequiredConstructionResources!["construct-material.wood"]);
        Assert.DoesNotContain("construct-material.steel",
            wood.RequiredConstructionResources.Keys);
    }

    [Fact]
    public void ManufacturingIntermediateDoesNotBecomePurchasedResource()
    {
        var catalog = Catalog();
        var actuator = Quote(catalog,
            Part("actuator", "alchemical-actuator", "actuator", "steel",
                enabled: false));
        Assert.True(actuator.IsAuthorized,
            string.Join("; ", actuator.Issues.Select(issue => issue.Code)));
        Assert.Equal(22, actuator.KnownConstructionCost);
        var ingredients = actuator.RequiredConstructionResources!;
        Assert.Equal(3, ingredients["construct-material.steel"]);
        Assert.Equal(2, ingredients["crafting-material.alchemical-reagent"]);
        Assert.False(ingredients.ContainsKey("crafting-material.activated-medium"));
    }

    [Fact]
    public void InstalledPowerAndOpticalSightAreSeparateFromOperatingState()
    {
        var catalog = Catalog();
        NativeConstructComponentInstance[] parts =
        [
            Part("housing", "power-source-housing", "housing", "steel",
                powerSource: "construct-power-source.mechanical-engine",
                links: ["eye"]),
            Part("eye", "crystal-eye", "crystal", "crystal")
        ];

        var powered = Quote(catalog, parts);
        Assert.True(powered.IsAuthorized,
            string.Join("; ", powered.Issues.Select(issue => issue.Code)));
        Assert.Equal(52, powered.KnownConstructionCost);
        Assert.Contains("trait.construct.ordinary-optical-sight",
            powered.Components.Single(x => x.InstanceId == "eye")
                .ActiveOperationalOriginTraitIds!);

        parts[0] = parts[0] with { Enabled = false };
        var offline = Quote(catalog, parts);
        Assert.Equal(powered.KnownConstructionCost, offline.KnownConstructionCost);
        Assert.False(offline.Components.Single(x => x.InstanceId == "eye").Operational);
        Assert.Empty(offline.Components.Single(x => x.InstanceId == "eye")
            .ActiveOperationalOriginTraitIds!);
    }

    [Fact]
    public void HeavyPassiveHardwareIsStillChargedWhenPoweredOff()
    {
        var catalog = Catalog();
        var assembly = Quote(catalog,
            Part("frame", "structural-frame", "frame", "steel"),
            Part("plate-a", "protective-plating", "plating", "steel",
                enabled: false),
            Part("plate-b", "protective-plating", "plating", "steel",
                enabled: false));
        Assert.True(assembly.IsAuthorized,
            string.Join("; ", assembly.Issues.Select(issue => issue.Code)));
        Assert.Equal(80, assembly.KnownConstructionCost);
        Assert.Equal(20,
            assembly.RequiredConstructionResources!["construct-material.steel"]);
        Assert.All(assembly.Components.Where(c => c.InstanceId.StartsWith("plate-")),
            c => Assert.True(c.Installed && !c.Operational));
    }

    [Fact]
    public void NonHumanoidBodyDoesNotRequireStructuralFrame()
    {
        var catalog = Catalog();
        var result = Quote(catalog,
            Part("housing", "power-source-housing", "housing", "wood",
                powerSource: "construct-power-source.solar-crystal",
                links: ["control", "sensor", "drive"]),
            Part("control", "control-module", "controller", "wood"),
            Part("sensor", "sensory-array", "sensor", "crystal"),
            Part("drive", "locomotion-unit", "contact", "wood"));
        Assert.True(result.IsAuthorized,
            string.Join("; ", result.Issues.Select(issue => issue.Code)));
        Assert.Equal(91, result.KnownConstructionCost);
        Assert.All(result.Components, c => Assert.True(c.Installed));
    }

    [Fact]
    public void NoFirstPartyStartingAllowanceMeansNoAuthorizedCharacter()
    {
        var catalog = Catalog();
        var assembly = new NativeConstructAssemblyRequest(
            [Part("frame", "structural-frame", "frame", "steel")],
            InitialAllowance: 220);
        var proposal = new NativeOriginProposal(
            "draft-construct", NativeOriginIntent.Custom,
            new("rule.creature-origination", "construct-assembly"),
            [], [], []);
        var result = new NativeConstructOriginConstructionService(catalog)
            .Evaluate(new(new(proposal), assembly));

        Assert.False(result.IsMechanicallyAuthorized);
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, result.Origin.Status);
        Assert.Contains(result.Origin.Issues,
            issue => issue.Code == "construct-starting-allowance-unresolved");
    }
}
