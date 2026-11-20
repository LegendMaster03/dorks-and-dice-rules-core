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
                  {"id":"construct-component.assembly","kind":"construct-component",
                   "name":"Composite assembly","constructionCost":2,"powerDemand":0,
                   "provides":["assembly"],"materialSlots":["body"],
                   "mountInterfaces":["structural"],
                   "acceptsMountInterfaces":["structural","motion","cover"]},
                  {"id":"construct-component.segment","kind":"construct-component",
                   "name":"Segment","constructionCost":1,"powerDemand":0,
                   "provides":["segment"],"materialSlots":["body"],
                   "mountInterfaces":["structural"]},
                  {"id":"construct-component.motor","kind":"construct-component",
                   "name":"Motor","constructionCost":2,"powerDemand":2,
                   "provides":["actuation"],"requiresAnyOf":[["power"]],
                   "mountInterfaces":["motion"]},
                  {"id":"construct-component.skin","kind":"construct-component",
                   "name":"Casing","constructionCost":1,"powerDemand":0,
                   "provides":["enclosure"],"materialSlots":["body"],
                   "mountInterfaces":["cover"]},
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
        int? allowance = 20, bool initial = true,
        IReadOnlyList<NativeConstructPowerConnection>? powerConnections = null) =>
        new NativeConstructAssemblyEvaluator(fixture.Catalog).Evaluate(
            new(components, allowance, initial, powerConnections));

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

    [Fact]
    public void ExplicitPowerWiringIsSeparateFromFunctionalConnectivity()
    {
        using var fixture = new Fixture();
        var assembly = Parts(weaponEnabled: false);
        // Legacy calls continue to treat every physical link as a power path.
        Assert.Equal(NativeOriginConstructionStatus.Authorized,
            Evaluate(fixture, assembly).Status);

        var wired = Evaluate(fixture, assembly, powerConnections:
            [new("core", "circulation"), new("circulation", "arm")]);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, wired.Status);
        Assert.True(Operational(wired, "arm"));

        // An explicitly empty wiring map does not borrow power just because
        // functional and structural connections exist.
        var unwired = Evaluate(fixture, assembly, powerConnections: []);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, unwired.Status);
        Assert.True(Operational(unwired, "core"));
        Assert.False(Operational(unwired, "circulation"));
        Assert.False(Operational(unwired, "arm"));
        Assert.Contains(unwired.Issues, x => x.Code == "construct-power-overallocated");
    }

    [Fact]
    public void PowerLinksMustFollowDirectPhysicalConnectionsAndBeUnique()
    {
        using var fixture = new Fixture();
        var assembly = Parts(weaponEnabled: false);
        var skipped = Evaluate(fixture, assembly, powerConnections:
            [new("core", "arm")]);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, skipped.Status);
        Assert.Contains(skipped.Issues, x => x.Code == "construct-power-link-invalid");

        var repeated = Evaluate(fixture, assembly, powerConnections:
            [new("core", "circulation"), new("circulation", "core")]);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, repeated.Status);
        Assert.Contains(repeated.Issues, x => x.Code == "construct-power-link-duplicate");
    }

    [Fact]
    public void PowerFailuresInvalidateDependentsOnAnotherPowerNetwork()
    {
        using var fixture = new Fixture();
        var assembly = new[]
        {
            Part("core", "core", connectedTo: ["circulation"]),
            Part("circulation", "circulation", connectedTo: ["arm"]),
            Part("frame", "frame", connectedTo: ["arm"], frame: true),
            Part("arm", "arm", connectedTo: ["backup"]),
            Part("backup", "core")
        };
        var shared = Evaluate(fixture, assembly);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, shared.Status);

        // The backup powers the arm, but circulation has no power path.
        // The arm cannot keep using the unpowered circulation capability.
        var separated = Evaluate(fixture, assembly, powerConnections:
            [new("arm", "backup")]);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, separated.Status);
        Assert.True(Operational(separated, "backup"));
        Assert.True(Operational(separated, "frame"));
        Assert.False(Operational(separated, "circulation"));
        Assert.False(Operational(separated, "arm"));
        Assert.Contains(separated.Issues, x =>
            x.Code == "construct-dependency-unavailable" &&
            x.Path == "components.arm");
    }


    private static NativeConstructComponentInstance Mounted(string id, string kind,
        string parent, string mountInterface, bool material = false) =>
        new(id, "construct-component." + kind,
            Materials: material
                ? new Dictionary<string, string>
                    { ["body"] = "construct-material.steel" } : null,
            ParentInstanceId: parent, MountInterfaceId: mountInterface);

    [Fact]
    public void CompositePartContainsIndependentlyConfigurableSubcomponents()
    {
        using var fixture = new Fixture();
        var parts = new[]
        {
            new NativeConstructComponentInstance("assembly", "construct-component.assembly",
                Materials: new Dictionary<string, string>
                    { ["body"] = "construct-material.steel" },
                ConnectedTo: ["core"]),
            Mounted("segment", "segment", "assembly", "structural", material: true),
            Mounted("motor1", "motor", "assembly", "motion"),
            Mounted("motor2", "motor", "assembly", "motion"),
            Mounted("skin", "skin", "assembly", "cover", material: true),
            Part("core", "core")
        };

        // Two motors on one assembly are allowed. Every subcomponent has
        // independent costs/materials without a mandatory anatomy or root.
        var result = Evaluate(fixture, parts);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Equal(14, result.KnownConstructionCost);
        Assert.All(new[] { "motor1", "motor2", "skin", "segment" },
            id => Assert.True(Operational(result, id)));

        var wiring = Evaluate(fixture, parts, powerConnections:
            [new("assembly", "core"), new("assembly", "motor1"),
             new("assembly", "motor2")]);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, wiring.Status);
        Assert.True(Operational(wiring, "motor1"));
        Assert.True(Operational(wiring, "motor2"));

        var noMotorPower = Evaluate(fixture, parts, powerConnections:
            [new("assembly", "core")]);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, noMotorPower.Status);
        Assert.False(Operational(noMotorPower, "motor1"));
        Assert.False(Operational(noMotorPower, "motor2"));
        Assert.True(Operational(noMotorPower, "skin"));
    }

    [Fact]
    public void MountInterfacesRejectUnknownParentsAndIncompatibleAttachments()
    {
        using var fixture = new Fixture();
        var parent = new NativeConstructComponentInstance("assembly",
            "construct-component.assembly",
            Materials: new Dictionary<string, string>
                { ["body"] = "construct-material.steel" });

        var missing = Evaluate(fixture,
            [parent, Mounted("part", "segment", "absent", "structural", material: true)]);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, missing.Status);
        Assert.Contains(missing.Issues, x => x.Code == "construct-mount-parent-invalid");

        var wrong = Evaluate(fixture,
            [parent, Mounted("part", "motor", "assembly", "cover")]);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, wrong.Status);
        Assert.Contains(wrong.Issues, x =>
            x.Code == "construct-mount-interface-incompatible");

        var absent = Evaluate(fixture,
            [parent, Mounted("part", "motor", "assembly", "")]);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, absent.Status);
        Assert.Contains(absent.Issues, x => x.Code == "construct-mount-interface-required");

        var stray = Evaluate(fixture,
            [parent with { MountInterfaceId = "structural" }]);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, stray.Status);
        Assert.Contains(stray.Issues, x => x.Code == "construct-mount-parent-required");
    }

    [Fact]
    public void NestedCompositionIsAllowedButCyclicContainmentIsNot()
    {
        using var fixture = new Fixture();
        var root = new NativeConstructComponentInstance("root",
            "construct-component.assembly",
            Materials: new Dictionary<string, string>
                { ["body"] = "construct-material.steel" });
        var child = Mounted("child", "assembly", "root", "structural",
            material: true);
        var grandchild = Mounted("grandchild", "segment", "child",
            "structural", material: true);

        var nested = Evaluate(fixture, [root, child, grandchild]);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, nested.Status);
        Assert.Equal(8, nested.KnownConstructionCost);

        var cycle = Evaluate(fixture,
            [root with { ParentInstanceId = "child", MountInterfaceId = "structural" },
             child]);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, cycle.Status);
        Assert.Contains(cycle.Issues, x => x.Code == "construct-mount-cycle");
    }


    [Fact]
    public void PowerNetworkLedgerSeparatesRequestedAndOperationalLoads()
    {
        using var fixture = new Fixture();
        var allocated = Evaluate(fixture, Parts(weaponEnabled: false));
        var network = Assert.Single(allocated.PowerNetworks!);
        Assert.Equal("arm", network.Id);
        Assert.Equal(5, network.ComponentInstanceIds.Count);
        Assert.Equal(5, network.RequestedDemand);
        Assert.Equal(8, network.AvailableOutput);
        Assert.Equal(5, network.OperationalDemand);
        Assert.Equal(8, network.OperationalOutput);
        Assert.Equal(3, network.RequestedHeadroom);
        Assert.Equal(false, network.IsOverallocated);
        Assert.False(network.HasUnresolvedDemand);
        Assert.False(network.HasUnresolvedOutput);

        var overloaded = Evaluate(fixture, Parts());
        var conflict = Assert.Single(overloaded.PowerNetworks!);
        Assert.Equal(10, conflict.RequestedDemand);
        Assert.Equal(8, conflict.AvailableOutput);
        Assert.Equal(-2, conflict.RequestedHeadroom);
        Assert.Equal(true, conflict.IsOverallocated);
        Assert.Equal(0, conflict.OperationalDemand);
        Assert.Equal(8, conflict.OperationalOutput);

        var separate = Evaluate(fixture, Parts(weaponEnabled: false),
            powerConnections: []);
        Assert.Equal(5, separate.PowerNetworks!.Count);
        var circulation = separate.PowerNetworks.Single(x =>
            x.Id == "circulation");
        Assert.Equal(2, circulation.RequestedDemand);
        Assert.Equal(0, circulation.AvailableOutput);
        Assert.Equal(-2, circulation.RequestedHeadroom);
        Assert.Equal(true, circulation.IsOverallocated);
    }

}
