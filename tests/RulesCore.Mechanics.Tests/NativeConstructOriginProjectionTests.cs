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
                                       "initialConstructionAllowance":20}]}
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
                  "requiresAnyOf":[["power"]],"provides":["extended-senses"]},
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
