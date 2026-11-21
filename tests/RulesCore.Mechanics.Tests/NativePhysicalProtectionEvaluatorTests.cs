using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>Coverage evidence never implies damage reduction or armor grants.</summary>
public sealed class NativePhysicalProtectionEvaluatorTests
{
    private static NativePhysicalSurface Layer(string id, string region,
        string material = "construct-material.steel", bool operating = false) =>
        new(id, "construct-component.protective-plating", "plating", material,
            "protective-surface", "rigid", "noncombustible", operating, region);

    [Fact]
    public void OnlySourcesCoveringTheQueriedRegionAreReturned()
    {
        var view = NativePhysicalProtectionEvaluator.Evaluate("shell",
            [Layer("front-a", "shell"), Layer("front-b", "shell"),
             Layer("arm", "arm")]);
        Assert.Equal("present", view.Status);
        Assert.Equal(2, view.ContributingSurfaces.Count);
        Assert.All(view.ContributingSurfaces, x => Assert.False(x.Operational));
    }

    [Fact]
    public void NonprotectiveSteelOrOtherRegionsDoNotImplyArmor()
    {
        var view = NativePhysicalProtectionEvaluator.Evaluate("shell",
            [Layer("arm", "arm"),
             new("eye", "construct-component.crystal-eye", "crystal",
                 "construct-material.steel", "other", "rigid", "noncombustible",
                 false, "shell")]);
        Assert.Equal("none", view.Status);
        Assert.Empty(view.ContributingSurfaces);
    }

    [Fact]
    public void MultipleSeparatePlatesAreNotSummedIntoDamageReduction()
    {
        var view = NativePhysicalProtectionEvaluator.Evaluate("shell",
            [Layer("a", "shell"), Layer("b", "shell")]);
        Assert.Equal("present", view.Status);
        Assert.Equal(2, view.ContributingSurfaces.Count);
        // The contract exposes material, source and coverage evidence only.
        Assert.All(view.ContributingSurfaces,
            source => Assert.Equal("construct-material.steel", source.MaterialDefinitionId));
    }

    [Fact]
    public void OneCompositeComponentNeedsSeparateCoverageRules()
    {
        var view = NativePhysicalProtectionEvaluator.Evaluate("shell",
            [Layer("shared", "shell"),
             new("shared", "construct-component.protective-plating", "lining",
                 "construct-material.cloth", "protective-surface", "flexible",
                 "combustible", false, "shell")]);
        Assert.Equal("unresolved", view.Status);
    }
}
