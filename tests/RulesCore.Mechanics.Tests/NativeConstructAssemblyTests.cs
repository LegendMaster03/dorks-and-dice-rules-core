using Xunit;
using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeConstructAssemblyTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "construct-assembly-" + Guid.NewGuid().ToString("N"));

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
                  {"id":"construct-component.frame","kind":"construct-component",
                   "name":"Frame","constructionCost":2,"powerDemand":0,
                   "provides":["structure"],"materialSlots":["body"]},
                  {"id":"construct-component.core","kind":"construct-component",
                   "name":"Power Core","constructionCost":3,"powerDemand":0,
                   "powerOutput":8,"provides":["power"],
                   "shutdownConsequence":"critical-control-loss"},
                  {"id":"construct-component.circulation","kind":"construct-component",
                   "name":"Circulation","constructionCost":1,"powerDemand":2,
                   "provides":["circulation"],"requiresAnyOf":[["power"]]},
                  {"id":"construct-component.arm","kind":"construct-component",
                   "name":"Arm","constructionCost":3,"powerDemand":3,
                   "provides":["manipulation"],
                   "requiresAnyOf":[["circulation"],["structure"]]},
                  {"id":"construct-component.weapon","kind":"construct-component",
                   "name":"Weapon","constructionCost":4,"powerDemand":5,
                   "provides":["integrated-attack"],"requiresAnyOf":[["manipulation"]]},
                  {"id":"construct-component.unknown","kind":"construct-component",
                   "name":"Unbalanced Prototype","provides":["special"]}
                ]
                """);
            File.WriteAllText(Path.Combine(native, "materials.json"), """
                [{"id":"construct-material.steel","kind":"construct-material","name":"Steel","constructionCost":1}]
                """);
            Catalog = NativeCatalogLoader.Load(
                NativeRulesetLayout.Resolve(_root, "first-party-001"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }

    private static NativeConstructComponentInstance Part(string instanceId, string definition,
        bool enabled = true, string[]? connectedTo = null, bool frame = false) =>
        new(instanceId, "construct-component." + definition,
            frame ? new Dictionary<string, string> { ["body"] = "construct-material.steel" } : null,
            connectedTo, enabled);

    private static NativeConstructComponentInstance[] Parts(bool weaponEnabled = true,
        bool coreEnabled = true) =>
        [
            Part("frame", "frame", connectedTo: ["core"], frame: true),
            Part("core", "core", coreEnabled, ["circulation"]),
            Part("circulation", "circulation", connectedTo: ["arm"]),
            Part("arm", "arm", connectedTo: ["weapon"]),
            Part("weapon", "weapon", weaponEnabled)
        ];

    private static NativeConstructAssemblyResult Evaluate(Fixture fixture,
        IReadOnlyList<NativeConstructComponentInstance> components,
        int? allowance = 20, bool initial = true) =>
        new NativeConstructAssemblyEvaluator(fixture.Catalog).Evaluate(
            new(components, allowance, initial));

    private static bool Operational(NativeConstructAssemblyResult result, string id) =>
        result.Components.Single(x => x.InstanceId == id).Operational;

    [Fact]
    public void InstalledDemandMayExceedOutputButActiveAllocationCannot()
    {
        using var fixture = new Fixture();
        var overloaded = Evaluate(fixture, Parts());
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, overloaded.Status);
        Assert.Equal(14, overloaded.KnownConstructionCost);
        Assert.Equal(5, overloaded.Components.Count);
        Assert.Contains(overloaded.Issues, x => x.Code == "construct-power-overallocated");
        Assert.All(overloaded.Components, x => Assert.True(x.Installed));
        Assert.True(Operational(overloaded, "frame"));
        Assert.False(Operational(overloaded, "weapon"));

        var allocated = Evaluate(fixture, Parts(weaponEnabled: false));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, allocated.Status);
        Assert.True(Operational(allocated, "arm"));
        Assert.False(Operational(allocated, "weapon"));
        Assert.Equal(overloaded.KnownConstructionCost, allocated.KnownConstructionCost);
    }

    [Fact]
    public void CriticalShutdownPropagatesDependenciesWithoutRemovingInstalledParts()
    {
        using var fixture = new Fixture();
        var result = Evaluate(fixture, Parts(weaponEnabled: false, coreEnabled: false));
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, result.Status);
        Assert.True(Operational(result, "frame"));
        Assert.False(Operational(result, "circulation"));
        Assert.False(Operational(result, "arm"));
        Assert.Contains(result.ShutdownConsequences, x => x.InstanceId == "core" &&
            x.ConsequenceId == "critical-control-loss");
        Assert.Equal(5, result.Components.Count);
    }

    [Fact]
    public void StartingAllowanceIsDistinctFromLaterModification()
    {
        using var fixture = new Fixture();
        var initial = Evaluate(fixture, Parts(weaponEnabled: false), allowance: 10);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, initial.Status);
        Assert.Contains(initial.Issues, x => x.Code == "construct-allowance-exceeded");
        var upgraded = Evaluate(fixture, Parts(weaponEnabled: false),
            allowance: 10, initial: false);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, upgraded.Status);
        Assert.Equal(14, upgraded.KnownConstructionCost);
    }

    [Fact]
    public void NoMandatoryHumanoidFrameOrFixedComponentCount()
    {
        using var fixture = new Fixture();
        var frameOnly = Evaluate(fixture,
            [Part("root", "frame", frame: true)], allowance: 3);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, frameOnly.Status);
        var multiple = Evaluate(fixture,
            [Part("a", "frame", frame: true),
             Part("b", "frame", frame: true),
             Part("c", "frame", frame: true)], allowance: 9);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, multiple.Status);
    }

    [Fact]
    public void ConnectivityMustBeExplicitForFunctionalDependencies()
    {
        using var fixture = new Fixture();
        var disconnected = Evaluate(fixture,
        [
            Part("frame", "frame", frame: true, connectedTo: ["arm"]),
            Part("core", "core", connectedTo: ["circulation"]),
            Part("circulation", "circulation"),
            Part("arm", "arm")
        ]);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, disconnected.Status);
        Assert.False(Operational(disconnected, "arm"));
        Assert.Contains(disconnected.Issues, x => x.Code == "construct-dependency-unavailable");
    }

    [Fact]
    public void MissingBalanceAndUnrecognizedMaterialsDoNotBecomeFreeBenefits()
    {
        using var fixture = new Fixture();
        var unknown = Evaluate(fixture, [Part("unknown", "unknown")]);
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, unknown.Status);
        Assert.Contains(unknown.Issues, x => x.Code == "construct-cost-unresolved");
        Assert.Contains(unknown.Issues, x => x.Code == "construct-power-demand-unresolved");

        var badMaterial = new NativeConstructComponentInstance("frame",
            "construct-component.frame",
            new Dictionary<string, string> { ["body"] = "construct-material.fake" });
        var invalid = Evaluate(fixture, [badMaterial]);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, invalid.Status);
        Assert.Contains(invalid.Issues, x => x.Code == "construct-material-unknown");
    }
}
