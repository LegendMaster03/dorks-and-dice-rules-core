using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Fixture-only native traits exercise the trusted procedure adapter. They are
/// not published Construct species, balance costs or playable first-party traits.
/// </summary>
public sealed class NativeConstructOriginProjectionTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "construct-origin-projection-" + Guid.NewGuid().ToString("N"));

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
            File.WriteAllText(Path.Combine(native, "rule.json"), """
                {"id":"rule.creature-origination","kind":"rule","name":"Origination",
                 "methodDefinitions":[{"id":"construct-assembly","name":"Construct Assembly",
                                       "initialConstructionAllowance":20}],
                 "originSynergies":[
                   {"id":"sensor-guard","resultTraitId":"trait.fixture.sensor-guard",
                    "inputs":[
                     {"anyOf":[{"mechanicalDefinitionId":"trait.fixture.passive-plate"}],"count":1},
                     {"anyOf":[{"mechanicalDefinitionId":"trait.fixture.extended-senses"}],"count":1}
                    ]}
                 ]}
                """);
            File.WriteAllText(Path.Combine(native, "type.json"), """
                {"id":"creature-type.construct","kind":"creatureType","name":"Construct",
                 "parent":"creature","originationMethods":["construct-assembly"]}
                """);
            File.WriteAllText(Path.Combine(native, "species.json"), """
                {"id":"species.fixture","kind":"species","name":"Fixture","selectable":true,
                 "creatureType":"construct","size":"medium","walkingSpeedFeet":0,
                 "traits":[]}
                """);
            File.WriteAllText(Path.Combine(native, "materials.json"), """
                [{"id":"construct-material.steel","kind":"construct-material",
                  "name":"Steel","constructionCost":1}]
                """);
            File.WriteAllText(Path.Combine(native, "components.json"), """
                [
                 {"id":"construct-component.power","kind":"construct-component",
                  "name":"Power","constructionCost":3,"powerDemand":0,"powerOutput":4,
                  "provides":["power"]},
                 {"id":"construct-component.plate","kind":"construct-component",
                  "name":"Plate","constructionCost":2,"powerDemand":0,
                  "materialSlots":["surface"],"provides":["protection"],
                  "installedOriginTraitIds":["trait.fixture.passive-plate"]},
                 {"id":"construct-component.scanner","kind":"construct-component",
                  "name":"Scanner","constructionCost":2,"powerDemand":3,
                  "requiresAnyOf":[["power"]],"provides":["extended-senses"],
                  "operationalOriginTraitIds":["trait.fixture.extended-senses"]},
                 {"id":"construct-component.overload","kind":"construct-component",
                  "name":"Overload","constructionCost":1,"powerDemand":5,
                  "requiresAnyOf":[["power"]],"provides":["burst"]},
                 {"id":"construct-component.unknown","kind":"construct-component",
                  "name":"Bad reference","constructionCost":1,"powerDemand":0,
                  "installedOriginTraitIds":["trait.fixture.missing"]},
                 {"id":"construct-component.choice","kind":"construct-component",
                  "name":"Unbound choice","constructionCost":1,"powerDemand":0,
                  "installedOriginTraitIds":["trait.fixture.choice"]}
                ]
                """);
            File.WriteAllText(Path.Combine(native, "trait.json"), """
                [
                 {"id":"trait.fixture.passive-plate","kind":"origin-trait",
                  "name":"Passive Plate",
                  "trait":{"id":"passive-plate","name":"Passive Plate",
                           "mechanicalEffect":"fixture.passive-protection"}},
                 {"id":"trait.fixture.extended-senses","kind":"origin-trait",
                  "name":"Extended Senses",
                  "trait":{"id":"extended-senses","name":"Extended Senses",
                           "mechanicalEffect":"fixture.extended-senses"}},
                 {"id":"trait.fixture.sensor-guard","kind":"origin-trait",
                  "name":"Sensor Guard",
                  "trait":{"id":"sensor-guard","name":"Sensor Guard",
                           "mechanicalEffect":"fixture.sensor-guard"}},
                 {"id":"trait.fixture.choice","kind":"origin-trait",
                  "name":"A Choice",
                  "trait":{"id":"choice","name":"A Choice",
                           "chooseFrom":"fixture:any","choiceCount":1}}
                ]
                """);
            Catalog = NativeCatalogLoader.Load(
                NativeRulesetLayout.Resolve(_root, "first-party-001"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }

    private static NativeOriginProposal Proposal(NativeOriginIntent intent = NativeOriginIntent.Direct) =>
        new("construct-origin-test", intent,
            new("rule.creature-origination", "construct-assembly"), [], [], [],
            SelectedSpeciesId: intent == NativeOriginIntent.Direct ? "species.fixture" : null);

    private static NativeConstructComponentInstance Part(string id, string kind,
        bool enabled = true, string[]? links = null) =>
        new(id, "construct-component." + kind,
            kind == "plate" ? new Dictionary<string, string>
                { ["surface"] = "construct-material.steel" } : null, links, enabled);

    private static NativeConstructAssemblyRequest Assembly(bool plateEnabled = true,
        bool scannerEnabled = false, bool overloadEnabled = false) =>
        new([
            Part("power", "power", links: ["plate", "scanner", "overload"]),
            Part("plate", "plate", plateEnabled),
            Part("scanner", "scanner", scannerEnabled),
            Part("overload", "overload", overloadEnabled)
        ], InitialAllowance: 20);

    private static NativeConstructOriginConstructionResult Evaluate(Fixture fixture,
        NativeOriginProposal? proposal = null, NativeConstructAssemblyRequest? assembly = null) =>
        new NativeConstructOriginConstructionService(fixture.Catalog).Evaluate(
            new(new(proposal ?? Proposal()), assembly ?? Assembly()));

    private static NativeOriginEffectiveGrant PassiveGrant(
        NativeConstructOriginConstructionResult result) =>
        result.Origin.Profile!.Underlying.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.passive-plate");

    [Fact]
    public void TrustedNativeComponentProjectsCanonicalGrantThroughSharedOriginEngine()
    {
        using var fixture = new Fixture();
        var outcome = Evaluate(fixture);
        Assert.True(outcome.IsMechanicallyAuthorized,
            string.Join("; ", outcome.Origin.Issues.Select(x => x.Code)));
        var grant = PassiveGrant(outcome);
        Assert.Equal("construct-component.plate", grant.Identity.Occurrence.Source.Id);
        Assert.Equal(NativeOriginSourceKind.Native, grant.Identity.Occurrence.Source.Kind);
        Assert.Equal("/installedOriginTraitIds/0/instance/plate",
            grant.Identity.Occurrence.OccurrencePath);
        Assert.Equal(NativeOriginAdjudication.Supported, grant.Authorization!.Adjudication);
        Assert.NotEmpty(grant.SourceCandidateIds);
        Assert.Equal(NativeOriginEventStage.Completed,
            outcome.Origin.Profile!.History.Single(x =>
                x.Id == "construction:construct-origin-test").Stage);
    }

    [Fact]
    public void PassiveGrantSurvivesOperationalReallocationWithoutOriginReconstruction()
    {
        using var fixture = new Fixture();
        var active = Evaluate(fixture, assembly: Assembly(scannerEnabled: true));
        var idle = Evaluate(fixture, assembly: Assembly(scannerEnabled: false,
            plateEnabled: false));
        Assert.True(active.IsMechanicallyAuthorized);
        Assert.True(idle.IsMechanicallyAuthorized);
        Assert.False(idle.Assembly.Components.Single(x =>
            x.InstanceId == "plate").Operational);
        Assert.Equal(JsonSerializer.Serialize(active.Origin.Profile!.Underlying.InherentGrants),
            JsonSerializer.Serialize(idle.Origin.Profile!.Underlying.InherentGrants));
    }

    [Fact]
    public void AnUntrustedNativeOfferCannotBypassProcedureValidation()
    {
        using var fixture = new Fixture();
        var issued = Evaluate(fixture);
        var stolen = issued.Origin.EvaluatedProposal.Candidates.Single(x =>
            x.Id == "construct:plate:trait:0");
        var forged = Proposal() with
        {
            Candidates = [stolen],
            Manifestations = [new("forged", stolen.Id)]
        };
        var normal = new NativeOriginConstructionEngine(fixture.Catalog)
            .Evaluate(new(forged));
        Assert.Equal(NativeOriginConstructionStatus.Invalid, normal.Status);
        Assert.Contains(normal.Issues, x => x.Code == "candidate-not-offered");
        Assert.Null(normal.Profile);
    }

    [Fact]
    public void AlteredClientOfferCannotReplaceCanonicalConstructMechanics()
    {
        using var fixture = new Fixture();
        var issued = Evaluate(fixture);
        var canonical = issued.Origin.EvaluatedProposal.Candidates.Single(x =>
            x.Id == "construct:plate:trait:0");
        var forged = Proposal() with
        {
            Candidates = [canonical with
            {
                OfferedValue = JsonSerializer.SerializeToElement(
                    new { id = "forged-trait", damage = "unlimited" })
            }]
        };
        var attempt = Evaluate(fixture, proposal: forged);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, attempt.Origin.Status);
        Assert.Contains(attempt.Origin.Issues, x =>
            x.Code == "candidate-native-evidence-mismatch");
        Assert.Null(attempt.Origin.Profile);
    }

    [Fact]
    public void OverallocatedOrUnbalancedAssembliesNeverIssueTrustedOriginProfiles()
    {
        using var fixture = new Fixture();
        var exceeded = Evaluate(fixture,
            assembly: Assembly(overloadEnabled: true));
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, exceeded.Assembly.Status);
        Assert.Null(exceeded.Origin.Profile);
        Assert.Contains(exceeded.Origin.Issues, x =>
            x.Code == "construct-power-overallocated");

        var unbalanced = Evaluate(fixture, assembly:
            new([Part("bad", "unknown")], InitialAllowance: 20));
        Assert.Equal(NativeOriginConstructionStatus.Invalid, unbalanced.Origin.Status);
        Assert.Null(unbalanced.Origin.Profile);
        Assert.Contains(unbalanced.Origin.Issues, x =>
            x.Code == "construct-origin-trait-reference-invalid");
    }

    [Fact]
    public void ChoiceBearingTraitsRemainUnresolvedRatherThanSilentlyGranted()
    {
        using var fixture = new Fixture();
        var result = Evaluate(fixture, assembly:
            new([Part("choice", "choice")], InitialAllowance: 20));
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, result.Origin.Status);
        Assert.Null(result.Origin.Profile);
        Assert.Contains(result.Origin.Issues, x =>
            x.Code == "construct-origin-trait-binding-deferred");
    }

    [Fact]
    public void CustomConstructionWithoutPhysicalFoundationCannotBePlayable()
    {
        using var fixture = new Fixture();
        var proposal = Proposal(NativeOriginIntent.Custom);
        var discovered = Evaluate(fixture, proposal);
        var candidate = discovered.Origin.EvaluatedProposal.Candidates.Single(x =>
            x.Id == "construct:plate:trait:0");
        var attempted = Evaluate(fixture, proposal with
        {
            Manifestations = [new("chosen-plate", candidate.Id)]
        });
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, attempted.Origin.Status);
        Assert.Contains(attempted.Origin.Issues, x =>
            x.Code == "construct-foundation-incomplete");
        Assert.Equal(NativeOriginEventStage.Pending,
            attempted.Origin.Profile!.History.Single(x =>
                x.Id == "construction:construct-origin-test").Stage);
    }


    [Fact]
    public void ForgedStartingAllowanceAndGameplayModificationPreviewCannotIssueGrants()
    {
        using var fixture = new Fixture();
        var inflated = Evaluate(fixture, assembly:
            new([Part("plate", "plate")], InitialAllowance: int.MaxValue));
        Assert.Equal(NativeOriginConstructionStatus.Invalid, inflated.Origin.Status);
        Assert.Contains(inflated.Origin.Issues, x =>
            x.Code == "construct-starting-allowance-unverified");
        Assert.Null(inflated.Origin.Profile);

        var later = Evaluate(fixture, assembly:
            new([Part("plate", "plate")], InitialConstruction: false));
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, later.Origin.Status);
        Assert.Contains(later.Origin.Issues, x =>
            x.Code == "construct-modification-authorization-deferred");
        Assert.Null(later.Origin.Profile);
    }


    [Fact]
    public void PoweredTraitIsAuthorizedAtInstallationButChangesApplicabilityWithoutReconstruction()
    {
        using var fixture = new Fixture();
        var constructed = Evaluate(fixture, assembly: Assembly(scannerEnabled: false));
        Assert.True(constructed.IsMechanicallyAuthorized);
        Assert.NotNull(constructed.OperatingSession);
        var profile = constructed.Origin.Profile!;
        var originalProfile = JsonSerializer.Serialize(profile);
        var scan = profile.Current.InherentGrants.Single(g =>
            g.Identity.MechanicalDefinitionId == "trait.fixture.extended-senses");
        var protection = PassiveGrant(constructed);
        var derived = profile.Current.InherentGrants.Single(g =>
            g.Identity.MechanicalDefinitionId == "trait.fixture.sensor-guard");
        Assert.NotNull(derived.Synergy);

        var inactive = constructed.OperatingSession!.EvaluateAllocation(
            new Dictionary<string, bool>());
        Assert.True(inactive.IsOperatingConfigurationValid);
        Assert.Contains(protection.Id, inactive.ApplicableOriginGrantIds);
        Assert.Contains(scan.Id, inactive.InactiveOriginGrantIds);
        Assert.Contains(derived.Id, inactive.InactiveOriginGrantIds);

        var active = constructed.OperatingSession.EvaluateAllocation(
            new Dictionary<string, bool> { ["scanner"] = true });
        Assert.True(active.IsOperatingConfigurationValid);
        Assert.Contains(scan.Id, active.ApplicableOriginGrantIds);
        Assert.Contains(protection.Id, active.ApplicableOriginGrantIds);
        Assert.Contains(derived.Id, active.ApplicableOriginGrantIds);
        Assert.Equal(originalProfile, JsonSerializer.Serialize(profile));

        var offAgain = constructed.OperatingSession.EvaluateAllocation(
            new Dictionary<string, bool> { ["scanner"] = false });
        Assert.Contains(scan.Id, offAgain.InactiveOriginGrantIds);
        Assert.Contains(derived.Id, offAgain.InactiveOriginGrantIds);
        Assert.Equal(originalProfile, JsonSerializer.Serialize(profile));
    }

    [Fact]
    public void LosingPowerSuppressesDependentTraitsAndSynergiesWithoutRemovingInstalledComponents()
    {
        using var fixture = new Fixture();
        var constructed = Evaluate(fixture, assembly: Assembly(scannerEnabled: false));
        Assert.True(constructed.IsMechanicallyAuthorized);
        var powerOff = constructed.OperatingSession!.EvaluateAllocation(
            new Dictionary<string, bool> { ["power"] = false, ["scanner"] = true });
        Assert.False(powerOff.IsOperatingConfigurationValid);
        Assert.Contains(powerOff.Issues, x => x.Code == "construct-dependency-unavailable");
        Assert.Equal(4, powerOff.Assembly.Components.Count);
        Assert.All(powerOff.Assembly.Components, state => Assert.True(state.Installed));
        Assert.DoesNotContain(constructed.Origin.Profile!.Current.InherentGrants
            .Where(g => g.Identity.MechanicalDefinitionId == "trait.fixture.extended-senses")
            .Select(g => g.Id), id => powerOff.ApplicableOriginGrantIds.Contains(id));

        var overloaded = constructed.OperatingSession.EvaluateAllocation(
            new Dictionary<string, bool> { ["scanner"] = true, ["overload"] = true });
        Assert.False(overloaded.IsOperatingConfigurationValid);
        Assert.Contains(overloaded.Issues, x => x.Code == "construct-power-overallocated");
        var scan = constructed.Origin.Profile.Current.InherentGrants.Single(g =>
            g.Identity.MechanicalDefinitionId == "trait.fixture.extended-senses");
        Assert.Contains(scan.Id, overloaded.InactiveOriginGrantIds);
        Assert.Contains(PassiveGrant(constructed).Id, overloaded.ApplicableOriginGrantIds);
    }

    [Fact]
    public void RuntimeAllocationCannotIntroduceComponentsOrChangeInstalledCatalog()
    {
        using var fixture = new Fixture();
        var assembly = Assembly(scannerEnabled: false);
        var constructed = Evaluate(fixture, assembly: assembly);
        Assert.True(constructed.IsMechanicallyAuthorized);

        var injected = constructed.OperatingSession!.EvaluateAllocation(
            new Dictionary<string, bool> { ["extra-weapon"] = true });
        Assert.False(injected.IsOperatingConfigurationValid);
        Assert.Contains(injected.Issues,
            x => x.Code == "construct-operating-instance-unknown");
        Assert.Empty(injected.ApplicableOriginGrantIds);

        // The session owns a deep-enough configuration snapshot: changing a
        // caller-owned material dictionary can not change authorized content.
        var materials = Assert.IsType<Dictionary<string, string>>(
            assembly.Components[1].Materials);
        materials["surface"] = "construct-material.forged";
        var same = constructed.OperatingSession.EvaluateAllocation(
            new Dictionary<string, bool> { ["scanner"] = true });
        Assert.True(same.IsOperatingConfigurationValid);
        Assert.DoesNotContain(same.Issues, x =>
            x.Code == "construct-material-unknown");
    }

    [Fact]
    public void MultipleInstancesGetDistinctNativeCandidatesWithoutForcedAnatomy()
    {
        using var fixture = new Fixture();
        var assembly = new NativeConstructAssemblyRequest(
        [
            Part("plate-a", "plate"),
            Part("plate-b", "plate")
        ], InitialAllowance: 20);
        var result = Evaluate(fixture, assembly: assembly);
        // Two physical instances are valid. A duplicate canonical gameplay
        // effect still requires an authored reconciliation/stacking decision.
        Assert.True(result.Assembly.IsAuthorized);
        Assert.Equal(2, result.Origin.EvaluatedProposal.Candidates.Count(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.passive-plate"));
        Assert.Equal(2, result.Origin.EvaluatedProposal.Candidates
            .Where(x => x.Identity.MechanicalDefinitionId == "trait.fixture.passive-plate")
            .Select(x => x.Identity.Occurrence.OccurrencePath)
            .Distinct(StringComparer.Ordinal).Count());
    }
}
