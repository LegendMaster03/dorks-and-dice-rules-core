using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Synthetic numeric fixtures verify composition rules without approving any
/// first-party construction costs, power output, or gameplay characteristics.
/// </summary>
public sealed class NativeConstructCatalogCompositionTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "construct-composition-" + Guid.NewGuid().ToString("N"));

        public NativeCatalogSnapshot Catalog { get; }

        public Fixture()
        {
            var package = Path.Combine(_root, "first-party-001");
            var native = Path.Combine(package, "native");
            Directory.CreateDirectory(native);
            File.WriteAllText(Path.Combine(package, "manifest.json"), """
                {"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,
                 "nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}
                """);
            File.WriteAllText(Path.Combine(native, "components.json"), """
                [
                  {"id":"construct-component.housing","kind":"construct-component",
                   "name":"Housing","constructionCost":2,"powerDemand":0,
                   "powerSourceSlot":true,"provides":["power-housing"],
                   "materialSlots":["shell"]},
                  {"id":"construct-component.arm","kind":"construct-component",
                   "name":"Arm","constructionCost":3,"powerDemand":2,
                   "provides":["manipulation"],"requiresAnyOf":[["power-housing"]],
                   "materialSlots":["frame"]},
                  {"id":"construct-component.plate","kind":"construct-component",
                   "name":"Plate","constructionCost":1,"powerDemand":0,
                   "provides":["enclosure"],"materialSlots":["casing"]}
                ]
                """);
            File.WriteAllText(Path.Combine(native, "materials.json"), """
                [{"id":"construct-material.steel","kind":"construct-material",
                  "name":"Steel","constructionCost":1}]
                """);
            File.WriteAllText(Path.Combine(native, "designs.json"), """
                [
                  {"id":"construct-design.reinforced","kind":"construct-design",
                   "name":"Reinforced","constructionCost":2,"powerDemand":1,
                   "compatibleComponentCapabilities":["manipulation"],
                   "provides":["reinforced-structure"]},
                  {"id":"construct-design.plate","kind":"construct-design",
                   "name":"Plate construction","constructionCost":1,
                   "compatibleComponentCapabilities":["enclosure"]},
                  {"id":"construct-design.unpriced","kind":"construct-design",
                   "name":"Unpriced","compatibleComponentCapabilities":["manipulation"]},
                  {"id":"construct-design.unspecified","kind":"construct-design",
                   "name":"Missing applicability","constructionCost":1}
                ]
                """);
            File.WriteAllText(Path.Combine(native, "enhancements.json"), """
                [
                  {"id":"construct-enhancement.grip","kind":"construct-enhancement",
                   "name":"Specialized Grip","constructionCost":1,"powerDemand":1,
                   "compatibleComponentCapabilities":["manipulation"],
                   "provides":["specialized-grip"]},
                  {"id":"construct-enhancement.storage","kind":"construct-enhancement",
                   "name":"Hidden storage","constructionCost":2,
                   "compatibleComponentCapabilities":["enclosure"],
                   "provides":["concealed-storage"]},
                  {"id":"construct-enhancement.requires-control","kind":"construct-enhancement",
                   "name":"Controlled enhancement","constructionCost":1,
                   "compatibleComponentCapabilities":["manipulation"],
                   "requiresAnyOf":[["remote-control"]]}
                ]
                """);
            File.WriteAllText(Path.Combine(native, "power-sources.json"), """
                [
                  {"id":"construct-power-source.mechanical","kind":"construct-power-source",
                   "name":"Mechanical","constructionCost":4,"powerOutput":5,
                   "compatibleComponentCapabilities":["power-housing"]},
                  {"id":"construct-power-source.weak","kind":"construct-power-source",
                   "name":"Low output","constructionCost":1,"powerOutput":3,
                   "compatibleComponentCapabilities":["power-housing"]},
                  {"id":"construct-power-source.unpriced","kind":"construct-power-source",
                   "name":"Unbalanced","compatibleComponentCapabilities":["power-housing"]}
                ]
                """);
            Catalog = NativeCatalogLoader.Load(
                NativeRulesetLayout.Resolve(_root, "first-party-001"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }

    private static NativeConstructComponentInstance Housing(
        string source = "construct-power-source.mechanical") =>
        new("housing", "construct-component.housing",
            Materials: new Dictionary<string, string>
                { ["shell"] = "construct-material.steel" },
            ConnectedTo: ["arm"], PowerSourceId: source);

    private static NativeConstructComponentInstance Arm(
        string id = "arm",
        string? design = "construct-design.reinforced",
        string[]? enhancements = null) =>
        new(id, "construct-component.arm",
            Materials: new Dictionary<string, string>
                { ["frame"] = "construct-material.steel" },
            DesignId: design,
            EnhancementIds: enhancements ?? ["construct-enhancement.grip"]);

    private static NativeConstructAssemblyResult Evaluate(Fixture fixture,
        IReadOnlyList<NativeConstructComponentInstance> components,
        int? allowance = 30, bool initial = true) =>
        new NativeConstructAssemblyEvaluator(fixture.Catalog).Evaluate(
            new(components, allowance, initial));

    [Fact]
    public void DesignEnhancementMaterialAndPowerSourceHaveIndependentCostsAndCapabilities()
    {
        using var fixture = new Fixture();
        var built = Evaluate(fixture, [Housing(), Arm()]);
        Assert.True(built.IsAuthorized,
            string.Join("; ", built.Issues.Select(x => x.Code)));
        // Housing 2 + steel 1 + source 4; arm 3 + steel 1 +
        // design 2 + enhancement 1 = 14. The source provides 5 output,
        // and the arm uses 2 + 1 + 1 = 4 demand.
        Assert.Equal(14, built.KnownConstructionCost);
        var arm = built.Components.Single(x => x.InstanceId == "arm");
        Assert.True(arm.Operational);
        Assert.Contains("manipulation", arm.ActiveCapabilities);
        Assert.Contains("reinforced-structure", arm.ActiveCapabilities);
        Assert.Contains("specialized-grip", arm.ActiveCapabilities);
    }

    [Fact]
    public void InstalledOptionsCountTowardAllowanceEvenWhenArmIsOff()
    {
        using var fixture = new Fixture();
        var disabled = Arm() with { Enabled = false };
        var built = Evaluate(fixture, [Housing(), disabled], allowance: 13);
        Assert.Equal(14, built.KnownConstructionCost);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, built.Status);
        Assert.Contains(built.Issues, x => x.Code == "construct-allowance-exceeded");
        Assert.False(built.Components.Single(x => x.InstanceId == "arm").Operational);

        var later = Evaluate(fixture, [Housing(), disabled], initial: false);
        Assert.True(later.IsAuthorized);
        Assert.Equal(14, later.KnownConstructionCost);
    }

    [Fact]
    public void SelectedPowerSourceDeterminesOutputInsteadOfClientNumericClaims()
    {
        using var fixture = new Fixture();
        var weak = Evaluate(fixture, [Housing("construct-power-source.weak"), Arm()]);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, weak.Status);
        Assert.Contains(weak.Issues, x => x.Code == "construct-power-overallocated");

        var sufficient = Evaluate(fixture, [Housing(), Arm()]);
        Assert.True(sufficient.IsAuthorized);
    }

    [Fact]
    public void InvalidOptionTypeCompatibilityAndDuplicatesFailClosed()
    {
        using var fixture = new Fixture();
        var wrongDesign = Evaluate(fixture,
            [Housing(), Arm(design: "construct-enhancement.grip")]);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, wrongDesign.Status);
        Assert.Contains(wrongDesign.Issues, x =>
            x.Code == "construct-option-reference-invalid");

        var incompatible = Evaluate(fixture,
            [Housing(), Arm(design: "construct-design.plate")]);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, incompatible.Status);
        Assert.Contains(incompatible.Issues, x =>
            x.Code == "construct-option-incompatible");

        var duplicate = Evaluate(fixture,
            [Housing(), Arm(enhancements:
                ["construct-enhancement.grip", "construct-enhancement.grip"])]);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, duplicate.Status);
        Assert.Contains(duplicate.Issues, x =>
            x.Code == "construct-enhancement-selection-invalid");

        var missingEligibility = Evaluate(fixture,
            [Housing(), Arm(design: "construct-design.unspecified")]);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, missingEligibility.Status);
        Assert.Contains(missingEligibility.Issues, x =>
            x.Code == "construct-option-incompatible");
    }

    [Fact]
    public void SourceSelectionMustMatchTheAuthoredHousingAndBeComplete()
    {
        using var fixture = new Fixture();
        var sourceMissing = Evaluate(fixture,
            [Housing() with { PowerSourceId = null }, Arm()]);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, sourceMissing.Status);
        Assert.Contains(sourceMissing.Issues, x =>
            x.Code == "construct-power-source-required");

        var forged = Evaluate(fixture,
            [Housing("construct-power-source.nonexistent"), Arm()]);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, forged.Status);
        Assert.Contains(forged.Issues, x =>
            x.Code == "construct-power-source-unknown");

        var unexpected = Evaluate(fixture,
            [Housing(), Arm() with { PowerSourceId = "construct-power-source.mechanical" }]);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, unexpected.Status);
        Assert.Contains(unexpected.Issues, x =>
            x.Code == "construct-power-source-unexpected");
    }

    [Fact]
    public void MissingAuthoredPricesOrOutputNeverBecomesFreePower()
    {
        using var fixture = new Fixture();
        var unpricedDesign = Evaluate(fixture,
            [Housing(), Arm(design: "construct-design.unpriced")]);
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, unpricedDesign.Status);
        Assert.Contains(unpricedDesign.Issues, x =>
            x.Code == "construct-option-cost-unresolved");

        var unbalancedSource = Evaluate(fixture,
            [Housing("construct-power-source.unpriced"), Arm()]);
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, unbalancedSource.Status);
        Assert.Contains(unbalancedSource.Issues, x =>
            x.Code == "construct-power-source-output-unresolved");
        Assert.Contains(unbalancedSource.Issues, x =>
            x.Code == "construct-power-source-cost-unresolved");
    }

    [Fact]
    public void EnhancementDependenciesAreReconciledWithOtherConnectedComponents()
    {
        using var fixture = new Fixture();
        var arm = Arm(enhancements: ["construct-enhancement.requires-control"]);
        var noController = Evaluate(fixture, [Housing(), arm]);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, noController.Status);
        Assert.Contains(noController.Issues, x =>
            x.Code == "construct-dependency-unavailable");
        Assert.False(noController.Components.Single(x =>
            x.InstanceId == "arm").Operational);
    }

    [Fact]
    public void SourceCompatibilityIsNotImplicit()
    {
        using var fixture = new Fixture();
        var otherSource = Housing() with { PowerSourceId = "construct-design.reinforced" };
        var rejected = Evaluate(fixture, [otherSource, Arm()]);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, rejected.Status);
        Assert.Contains(rejected.Issues, x =>
            x.Code == "construct-power-source-unknown");
    }
}
