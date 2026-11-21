using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>Physical layering proposals are not automatic character armor grants.</summary>
public sealed class NativePhysicalProtectionEvaluatorTests
{
    private static NativePhysicalSurface Layer(string id, string region,
        string material = "construct-material.steel", int? potential = 2,
        bool operating = false) =>
        new(id, "construct-component.protective-plating", "plating", material,
            "protective-surface", "rigid", "noncombustible", operating,
            region, potential);

    [Fact]
    public void OnlyLayersCoveringTheQueriedRegionContribute()
    {
        var view = NativePhysicalProtectionEvaluator.Evaluate("shell",
            [Layer("front-a", "shell"), Layer("front-b", "shell"),
             Layer("arm", "arm")]);
        Assert.Equal("candidate", view.Status);
        Assert.Equal(4, view.KnownCandidateFlatDamageReduction);
        Assert.Equal(2, view.ContributingSurfaces.Count);
        Assert.All(view.ContributingSurfaces, x => Assert.False(x.Operational));
    }

    [Fact]
    public void OtherRegionsOrNonprotectiveSteelDoNotOfferFreeArmor()
    {
        var view = NativePhysicalProtectionEvaluator.Evaluate("shell",
            [Layer("arm", "arm"),
             new("eye", "construct-component.crystal-eye", "crystal",
                 "construct-material.steel", "other", "rigid", "noncombustible",
                 false, "shell", 99)]);
        Assert.Equal("none", view.Status);
        Assert.Equal(0, view.KnownCandidateFlatDamageReduction);
    }

    [Fact]
    public void UnknownMaterialPropertiesCannotBeCountedAsZero()
    {
        var view = NativePhysicalProtectionEvaluator.Evaluate("shell",
            [Layer("a", "shell"), Layer("b", "shell", potential: null)]);
        Assert.Equal("unresolved", view.Status);
    }

    [Fact]
    public void OneCompositeComponentIsNotSilentlyCountedAsTwoLayers()
    {
        var view = NativePhysicalProtectionEvaluator.Evaluate("shell",
            [Layer("shared", "shell"),
             new("shared", "construct-component.protective-plating", "lining",
                 "construct-material.cloth", "protective-surface", "flexible",
                 "combustible", false, "shell", 0)]);
        Assert.Equal("unresolved", view.Status);
    }
}
