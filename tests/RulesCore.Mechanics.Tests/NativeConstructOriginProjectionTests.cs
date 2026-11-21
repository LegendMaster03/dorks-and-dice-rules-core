using System.Text.Json;
using System.Text.Json.Nodes;
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

        public Fixture(bool overlappingHousingTrait = false,
            bool overlappingModeDesignTrait = false)
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
                                       "initialConstructionAllowance":20,
                                       "constructIntrinsics":{"creatureType":"construct",
                                           "sizeChoice":{"from":["small","medium","large"]}}}],
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
                  "name":"Steel","constructionCost":1,
                  "installedOriginTraitIds":["trait.fixture.material-trait"]}]
                """);
            File.WriteAllText(Path.Combine(native, "components.json"), """
                [
                 {"id":"construct-component.power","kind":"construct-component",
                  "name":"Power","constructionCost":3,"powerDemand":0,"powerOutput":4,
                  "provides":["power"],"requiresContinuousPower":true,
                  "shutdownConsequence":"core-unpowered"},
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
                 {"id":"construct-component.multi","kind":"construct-component",
                  "name":"Multifunctional Tool","constructionCost":2,"powerDemand":0,
                  "operationalOriginTraitIds":["trait.fixture.tether","trait.fixture.blast"],
                  "operatingModes":[
                    {"id":"stowed","powerDemand":0,"provides":[],
                     "operationalOriginTraitIds":[]},
                    {"id":"tether","powerDemand":2,"provides":["tether-control"],
                     "operationalOriginTraitIds":["trait.fixture.tether"]},
                    {"id":"blast","powerDemand":4,"provides":["blast-port"],
                     "operationalOriginTraitIds":["trait.fixture.blast"]}
                  ]},
                 {"id":"construct-component.tether-dependent","kind":"construct-component",
                  "name":"Tether Receiver","constructionCost":1,"powerDemand":1,
                  "requiresAnyOf":[["tether-control"]],
                  "provides":["tether-receiver"]},
                 {"id":"construct-component.invalid-mode","kind":"construct-component",
                  "name":"Forged Mode","constructionCost":1,"powerDemand":0,
                  "operationalOriginTraitIds":["trait.fixture.tether"],
                  "operatingModes":[
                    {"id":"forged","powerDemand":0,"provides":["arbitrary"],
                     "operationalOriginTraitIds":["trait.fixture.blast"]}
                  ]},
                 {"id":"construct-component.fuel-housing","kind":"construct-component",
                  "name":"Fuel Housing","constructionCost":2,"powerDemand":0,
                  "powerSourceSlot":true,"provides":["power-housing"]},
                 {"id":"construct-component.solar-load","kind":"construct-component",
                  "name":"Solar powered load","constructionCost":1,"powerDemand":9,
                  "requiresAnyOf":[["power-housing"]]},
                 {"id":"construct-component.fuel-load","kind":"construct-component",
                  "name":"Fuel Powered Sensor","constructionCost":1,"powerDemand":3,
                  "requiresAnyOf":[["power-housing"]],
                  "requiresContinuousPower":true,
                  "shutdownConsequence":"critical-sensor-loss",
                  "operationalOriginTraitIds":["trait.fixture.extended-senses"]},
                 {"id":"construct-component.unknown","kind":"construct-component",
                  "name":"Bad reference","constructionCost":1,"powerDemand":0,
                  "installedOriginTraitIds":["trait.fixture.missing"]},
                 {"id":"construct-component.choice","kind":"construct-component",
                  "name":"Unbound choice","constructionCost":1,"powerDemand":0,
                  "installedOriginTraitIds":["trait.fixture.choice"]},
                 {"id":"construct-component.aptitude","kind":"construct-component",
                  "name":"Aptitude Module","constructionCost":1,"powerDemand":0,
                  "installedOriginTraitIds":["trait.fixture.ability-choice",
                                             "trait.fixture.aptitude-dependent"]}
                ]
                """);
            File.WriteAllText(Path.Combine(native, "designs.json"), """
                [{"id":"construct-design.reinforced","kind":"construct-design",
                  "name":"Reinforced Plate","constructionCost":1,
                  "compatibleComponentCapabilities":["protection"],
                  "installedOriginTraitIds":["trait.fixture.design-trait"]}]
                """);
            File.WriteAllText(Path.Combine(native, "enhancements.json"), """
                [{"id":"construct-enhancement.active-plate","kind":"construct-enhancement",
                  "name":"Active Plate","constructionCost":1,"powerDemand":1,
                  "compatibleComponentCapabilities":["protection"],
                  "operationalOriginTraitIds":["trait.fixture.enhancement-active"]}]
                """);
            File.WriteAllText(Path.Combine(native, "power-sources.json"), """
                [
                 {"id":"construct-power-source.solar-crystal",
                  "kind":"construct-power-source","name":"Solar collector",
                  "constructionCost":1,"powerOutput":8,
                  "overdrivePowerOutput":12,"overdriveIncreasePercent":50,
                  "compatibleComponentCapabilities":["power-housing"],
                  "operatingPrinciple":"solar-recharge",
                  "solarCollectionPower":{"none":0,"indirect":8,
                    "direct":16,"concentrated":24},
                  "storageCapacityPowerUnitHours":96,
                  "overdriveCondition":"dm-confirmed-concentrated-sunlight"},
                 {"id":"construct-power-source.finite","kind":"construct-power-source",
                  "name":"Finite generator","constructionCost":1,"powerOutput":4,
                  "compatibleComponentCapabilities":["power-housing"],
                  "resourceKind":"fuel-unit","resourceCapacity":5,
                  "resourceUsePerStep":2,
                  "resourceTransferOut":true,"resourceTransferIn":true},
                 {"id":"construct-power-source.trait-generator",
                  "kind":"construct-power-source","name":"Trait generator",
                  "constructionCost":1,"powerOutput":4,
                  "compatibleComponentCapabilities":["power-housing"],
                  "installedOriginTraitIds":["trait.fixture.source-passive"],
                  "operationalOriginTraitIds":["trait.fixture.source-active"]},
                 {"id":"construct-power-source.sealed","kind":"construct-power-source",
                  "name":"Sealed reservoir","constructionCost":1,"powerOutput":4,
                  "compatibleComponentCapabilities":["power-housing"],
                  "resourceKind":"fuel-unit","resourceCapacity":5,
                  "resourceUsePerStep":2},
                 {"id":"construct-power-source.other-kind","kind":"construct-power-source",
                  "name":"Other kind","constructionCost":1,"powerOutput":4,
                  "compatibleComponentCapabilities":["power-housing"],
                  "resourceKind":"charge-unit","resourceCapacity":5,
                  "resourceUsePerStep":2,
                  "resourceTransferOut":true,"resourceTransferIn":true},
                 {"id":"construct-power-source.invalid-transfer","kind":"construct-power-source",
                  "name":"Invalid authored port","constructionCost":1,"powerOutput":4,
                  "compatibleComponentCapabilities":["power-housing"],
                  "resourceKind":"fuel-unit","resourceCapacity":5,
                  "resourceUsePerStep":2,"resourceTransferIn":"yes"},
                 {"id":"construct-power-source.unknown-endurance",
                  "kind":"construct-power-source","name":"Unbalanced endurance",
                  "constructionCost":1,"powerOutput":4,
                  "compatibleComponentCapabilities":["power-housing"]}
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
                 {"id":"trait.fixture.tether","kind":"origin-trait",
                  "name":"Tether Handling",
                  "trait":{"id":"tether","name":"Tether Handling",
                           "mechanicalEffect":"fixture.tether"}},
                 {"id":"trait.fixture.blast","kind":"origin-trait",
                  "name":"Blast Configuration",
                  "trait":{"id":"blast","name":"Blast Configuration",
                           "mechanicalEffect":"fixture.blast"}},
                 {"id":"trait.fixture.sensor-guard","kind":"origin-trait",
                  "name":"Sensor Guard",
                  "trait":{"id":"sensor-guard","name":"Sensor Guard",
                           "mechanicalEffect":"fixture.sensor-guard"}},
                 {"id":"trait.fixture.choice","kind":"origin-trait",
                  "name":"A Choice",
                  "trait":{"id":"choice","name":"A Choice",
                           "chooseFrom":"fixture:any","choiceCount":1}},
                 {"id":"trait.fixture.ability-choice","kind":"origin-trait",
                  "name":"Aptitude","trait":{"id":"aptitude","name":"Aptitude",
                   "mechanicalEffect":"fixture.aptitude",
                   "abilityChoice":{"from":["intelligence","wisdom"],"perCharacter":true}}},
                 {"id":"trait.fixture.aptitude-dependent","kind":"origin-trait",
                  "name":"Aptitude Dependent",
                  "trait":{"id":"aptitude-dependent",
                   "mechanicalEffect":"fixture.aptitude-dependent",
                   "originBindings":{"aptitude":{"lookup":"manifested-trait-choice",
                    "scope":"contributor","mechanicalDefinitionId":"trait.fixture.ability-choice",
                    "choiceRequirementId":"abilityChoice"}}}},
                 {"id":"trait.fixture.material-trait","kind":"origin-trait",
                  "name":"Material Trait","trait":{"id":"steel","mechanicalEffect":"fixture.steel"}},
                 {"id":"trait.fixture.design-trait","kind":"origin-trait",
                  "name":"Design Trait","trait":{"id":"reinforced","mechanicalEffect":"fixture.design"}},
                 {"id":"trait.fixture.enhancement-active","kind":"origin-trait",
                  "name":"Active Enhancement","trait":{"id":"active-plate",
                   "mechanicalEffect":"fixture.enhancement"}},
                 {"id":"trait.fixture.source-passive","kind":"origin-trait",
                  "name":"Source Passive","trait":{"id":"source-passive",
                   "mechanicalEffect":"fixture.source-passive"}},
                 {"id":"trait.fixture.source-active","kind":"origin-trait",
                  "name":"Source Active","trait":{"id":"source-active",
                   "mechanicalEffect":"fixture.source-active"}}
                ]
                """);
            // CA-01 fixtures deliberately author overlapping canonical traits.
            // Only a manifested candidate is authorized for runtime use.
            if (overlappingHousingTrait || overlappingModeDesignTrait)
            {
                var componentPath = Path.Combine(native, "components.json");
                var components = JsonNode.Parse(File.ReadAllText(componentPath))!.AsArray();
                if (overlappingHousingTrait)
                {
                    var housing = components.Single(node =>
                        node!["id"]!.GetValue<string>() ==
                        "construct-component.fuel-housing")!;
                    housing["operationalOriginTraitIds"] =
                        new JsonArray(JsonValue.Create("trait.fixture.source-active"));
                }
                if (overlappingModeDesignTrait)
                {
                    var multi = components.Single(node =>
                        node!["id"]!.GetValue<string>() ==
                        "construct-component.multi")!;
                    multi["provides"] = new JsonArray(JsonValue.Create("tool-base"));
                    var designPath = Path.Combine(native, "designs.json");
                    var designs = JsonNode.Parse(File.ReadAllText(designPath))!.AsArray();
                    designs.Add(JsonNode.Parse("""
                        {"id":"construct-design.mode-addon","kind":"construct-design",
                         "name":"Mode Addon","constructionCost":1,
                         "compatibleComponentCapabilities":["tool-base"],
                         "operationalOriginTraitIds":["trait.fixture.tether"]}
                        """));
                    File.WriteAllText(designPath, designs.ToJsonString());
                }
                File.WriteAllText(componentPath, components.ToJsonString());
            }
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
        bool enabled = true, string[]? links = null, string? mode = null) =>
        new(id, "construct-component." + kind,
            kind == "plate" ? new Dictionary<string, string>
                { ["surface"] = "construct-material.steel" } : null, links, enabled, mode);

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
    public void OpenCatalogChoicesRequireExplicitSelectionAndTrustedEligibility()
    {
        using var fixture = new Fixture();
        var result = Evaluate(fixture, assembly:
            new([Part("choice", "choice")], InitialAllowance: 20));
        var candidate = result.Origin.EvaluatedProposal.Candidates.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.choice");
        var requirement = Assert.Single(candidate.RequiredChoices!);
        Assert.Equal("fixture:any", requirement.OptionCatalogId);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, result.Origin.Status);
        Assert.Contains(result.Origin.Issues, x =>
            x.Code == "required-origin-selection-missing");

        var selected = Evaluate(fixture, proposal: Proposal() with
        {
            Manifestations = [new("choice-selected", candidate.Id, Choices:
                [new("selection", ["forged-option"])] )]
        }, assembly: new([Part("choice", "choice")], InitialAllowance: 20));
        Assert.False(selected.IsMechanicallyAuthorized);
        Assert.Contains(selected.Origin.Issues, x =>
            x.Code == "choice-authorization-unresolved");
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


    private static NativeConstructAssemblyRequest ModularToolAssembly(
        string? mode = "stowed", bool includeDependent = false,
        bool includeScanner = false) =>
        new NativeConstructAssemblyRequest(
        [
            Part("power", "power", links: ["tool", "dependent", "scanner"]),
            Part("tool", "multi", mode: mode),
            Part("dependent", "tether-dependent", enabled: includeDependent),
            Part("scanner", "scanner", enabled: includeScanner)
        ], InitialAllowance: 20);

    [Fact]
    public void MutuallyExclusiveModesSwitchPoweredTraitsWithoutChangingPermanentOrigin()
    {
        using var fixture = new Fixture();
        var constructed = Evaluate(fixture,
            assembly: ModularToolAssembly());
        Assert.True(constructed.IsMechanicallyAuthorized,
            string.Join("; ", constructed.Origin.Issues.Select(x => x.Code)));
        var profile = constructed.Origin.Profile!;
        var serialized = JsonSerializer.Serialize(profile);
        var tether = profile.Current.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.tether");
        var blast = profile.Current.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.blast");

        var stowed = constructed.OperatingSession!.EvaluateConfiguration();
        Assert.True(stowed.IsOperatingConfigurationValid);
        Assert.Contains(tether.Id, stowed.InactiveOriginGrantIds);
        Assert.Contains(blast.Id, stowed.InactiveOriginGrantIds);
        Assert.Equal("stowed", stowed.Assembly.Components.Single(x =>
            x.InstanceId == "tool").ModeId);

        var deployed = constructed.OperatingSession.EvaluateConfiguration(
            requestedModes: new Dictionary<string, string> { ["tool"] = "tether" });
        Assert.True(deployed.IsOperatingConfigurationValid);
        Assert.Contains(tether.Id, deployed.ApplicableOriginGrantIds);
        Assert.Contains(blast.Id, deployed.InactiveOriginGrantIds);
        Assert.Contains("tether-control", deployed.Assembly.Components
            .Single(x => x.InstanceId == "tool").ActiveCapabilities);

        var firing = constructed.OperatingSession.EvaluateConfiguration(
            requestedModes: new Dictionary<string, string> { ["tool"] = "blast" });
        Assert.True(firing.IsOperatingConfigurationValid);
        Assert.Contains(blast.Id, firing.ApplicableOriginGrantIds);
        Assert.Contains(tether.Id, firing.InactiveOriginGrantIds);
        Assert.Contains("blast-port", firing.Assembly.Components
            .Single(x => x.InstanceId == "tool").ActiveCapabilities);

        var disabled = constructed.OperatingSession.EvaluateConfiguration(
            new Dictionary<string, bool> { ["tool"] = false },
            new Dictionary<string, string> { ["tool"] = "blast" });
        Assert.True(disabled.IsOperatingConfigurationValid);
        Assert.Contains(blast.Id, disabled.InactiveOriginGrantIds);
        Assert.Equal(serialized, JsonSerializer.Serialize(profile));
    }

    [Fact]
    public void ModePowerDemandAndDependencyProvidersChangeTogether()
    {
        using var fixture = new Fixture();
        var constructed = Evaluate(fixture,
            assembly: ModularToolAssembly());
        Assert.True(constructed.IsMechanicallyAuthorized);

        var tether = constructed.OperatingSession!.EvaluateConfiguration(
            new Dictionary<string, bool> { ["dependent"] = true },
            new Dictionary<string, string> { ["tool"] = "tether" });
        Assert.True(tether.IsOperatingConfigurationValid);
        Assert.True(tether.Assembly.Components.Single(x =>
            x.InstanceId == "dependent").Operational);

        var noTether = constructed.OperatingSession.EvaluateConfiguration(
            new Dictionary<string, bool> { ["dependent"] = true },
            new Dictionary<string, string> { ["tool"] = "blast" });
        Assert.False(noTether.IsOperatingConfigurationValid);
        Assert.Contains(noTether.Issues, x =>
            x.Code == "construct-dependency-unavailable");
        Assert.False(noTether.Assembly.Components.Single(x =>
            x.InstanceId == "dependent").Operational);

        var stowedScanner = constructed.OperatingSession.EvaluateConfiguration(
            new Dictionary<string, bool> { ["scanner"] = true });
        Assert.True(stowedScanner.IsOperatingConfigurationValid);

        var overloaded = constructed.OperatingSession.EvaluateConfiguration(
            new Dictionary<string, bool> { ["scanner"] = true },
            new Dictionary<string, string> { ["tool"] = "tether" });
        Assert.False(overloaded.IsOperatingConfigurationValid);
        Assert.Contains(overloaded.Issues, x =>
            x.Code == "construct-power-overallocated");
    }

    [Fact]
    public void InvalidOperatingModesAndUndeclaredModeTraitsFailClosed()
    {
        using var fixture = new Fixture();
        var original = Evaluate(fixture, assembly: ModularToolAssembly());
        Assert.True(original.IsMechanicallyAuthorized);
        var session = original.OperatingSession!;

        var unknown = session.EvaluateConfiguration(
            requestedModes: new Dictionary<string, string> { ["tool"] = "hacked" });
        Assert.False(unknown.IsOperatingConfigurationValid);
        Assert.Contains(unknown.Issues, x => x.Code == "construct-mode-unknown");
        var blast = original.Origin.Profile!.Current.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.blast");
        Assert.Contains(blast.Id, unknown.InactiveOriginGrantIds);

        var absent = Evaluate(fixture, assembly: ModularToolAssembly(mode: null));
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, absent.Assembly.Status);
        Assert.Contains(absent.Assembly.Issues, x =>
            x.Code == "construct-mode-selection-required");
        Assert.Null(absent.Origin.Profile);

        var unexpected = Evaluate(fixture, assembly:
            new([Part("plain", "power", mode: "firing")], InitialAllowance: 20));
        Assert.Equal(NativeOriginConstructionStatus.Invalid, unexpected.Assembly.Status);
        Assert.Contains(unexpected.Assembly.Issues, x =>
            x.Code == "construct-mode-unexpected");

        var forged = Evaluate(fixture, assembly:
            new([Part("fake", "invalid-mode", mode: "forged")], InitialAllowance: 20));
        Assert.Equal(NativeOriginConstructionStatus.Invalid, forged.Assembly.Status);
        Assert.Contains(forged.Assembly.Issues, x =>
            x.Code == "construct-mode-traits-invalid");
        Assert.Null(forged.Origin.Profile);
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

    [Fact]
    public void PriorityPreviewShedsOnlyOptionalLoadsWithoutChangingOriginEntitlements()
    {
        using var fixture = new Fixture();
        var constructed = Evaluate(fixture, assembly: Assembly(scannerEnabled: false));
        Assert.True(constructed.IsMechanicallyAuthorized);
        var profile = constructed.Origin.Profile!;
        var original = JsonSerializer.Serialize(profile);
        var scan = profile.Current.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.extended-senses");
        var synergy = profile.Current.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.sensor-guard");

        var preview = constructed.OperatingSession!.PreviewPriorityShedding(
            ["overload"],
            requestedEnabled: new Dictionary<string, bool>
                { ["scanner"] = true, ["overload"] = true });
        Assert.True(preview.IsSatisfied,
            string.Join("; ", preview.PlanningIssues.Select(x => x.Code)));
        Assert.Equal(new[] { "overload" }, preview.ProposedShedInstanceIds);
        Assert.Empty(preview.InoperativeContinuousPowerInstanceIds);
        Assert.True(preview.Evaluation.Assembly.Components.Single(x =>
            x.InstanceId == "scanner").Operational);
        Assert.False(preview.Evaluation.Assembly.Components.Single(x =>
            x.InstanceId == "overload").Operational);
        Assert.Contains(scan.Id, preview.Evaluation.ApplicableOriginGrantIds);
        Assert.Contains(synergy.Id, preview.Evaluation.ApplicableOriginGrantIds);
        Assert.Equal(original, JsonSerializer.Serialize(profile));

        var later = constructed.OperatingSession.EvaluateConfiguration();
        Assert.True(later.IsOperatingConfigurationValid);
        Assert.False(later.Assembly.Components.Single(x =>
            x.InstanceId == "scanner").RequestedEnabled);
    }

    [Fact]
    public void PriorityPreviewRejectsProtectedDuplicateAndUnknownShutdowns()
    {
        using var fixture = new Fixture();
        var constructed = Evaluate(fixture);
        Assert.True(constructed.IsMechanicallyAuthorized);
        var session = constructed.OperatingSession!;

        var protectedLoad = session.PreviewPriorityShedding(["power"]);
        Assert.False(protectedLoad.IsSatisfied);
        Assert.Empty(protectedLoad.ProposedShedInstanceIds);
        Assert.Contains(protectedLoad.PlanningIssues, x => x.Code == "construct-priority-protected");

        var unknown = session.PreviewPriorityShedding(["forged"]);
        Assert.False(unknown.IsSatisfied);
        Assert.Contains(unknown.PlanningIssues, x => x.Code == "construct-priority-selection-invalid");

        var duplicate = session.PreviewPriorityShedding(["scanner", "scanner"]);
        Assert.False(duplicate.IsSatisfied);
        Assert.Contains(duplicate.PlanningIssues, x => x.Code == "construct-priority-selection-invalid");

        // Operators may still deliberately turn off critical components.
        var manual = session.EvaluateAllocation(
            new Dictionary<string, bool> { ["power"] = false });
        Assert.Contains(manual.Assembly.ShutdownConsequences, x =>
            x.InstanceId == "power" && x.ConsequenceId == "core-unpowered");
        Assert.True(manual.Assembly.Components.Single(x =>
            x.InstanceId == "power").RequiresContinuousPower);
    }

    [Fact]
    public void PriorityPreviewReportsInsufficientCapacityAndContinuousLoadFailure()
    {
        using var fixture = new Fixture();
        var constructed = Evaluate(fixture);
        Assert.True(constructed.IsMechanicallyAuthorized);
        var session = constructed.OperatingSession!;

        var insufficient = session.PreviewPriorityShedding([],
            requestedEnabled: new Dictionary<string, bool>
                { ["scanner"] = true, ["overload"] = true });
        Assert.False(insufficient.IsSatisfied);
        Assert.Empty(insufficient.ProposedShedInstanceIds);
        Assert.Contains(insufficient.PlanningIssues, x =>
            x.Code == "construct-priority-capacity-insufficient");

        var manualOff = session.PreviewPriorityShedding([],
            requestedEnabled: new Dictionary<string, bool>
                { ["power"] = false, ["scanner"] = false });
        Assert.False(manualOff.IsSatisfied);
        Assert.Equal(new[] { "power" }, manualOff.InoperativeContinuousPowerInstanceIds);
        Assert.Contains(manualOff.PlanningIssues, x =>
            x.Code == "construct-continuous-power-unavailable");
    }

    [Fact]
    public void PriorityPreviewRespectsModeChangesAndDoesNotAcceptFabricatedModes()
    {
        using var fixture = new Fixture();
        var constructed = Evaluate(fixture, assembly: ModularToolAssembly());
        Assert.True(constructed.IsMechanicallyAuthorized);
        var tether = constructed.Origin.Profile!.Current.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.tether");
        var planned = constructed.OperatingSession!.PreviewPriorityShedding(
            ["scanner"], requestedEnabled:
                new Dictionary<string, bool> { ["scanner"] = true },
            requestedModes: new Dictionary<string, string> { ["tool"] = "tether" });
        Assert.True(planned.IsSatisfied);
        Assert.Equal(new[] { "scanner" }, planned.ProposedShedInstanceIds);
        Assert.Equal("tether", planned.Evaluation.Assembly.Components.Single(x =>
            x.InstanceId == "tool").ModeId);
        Assert.Contains(tether.Id, planned.Evaluation.ApplicableOriginGrantIds);

        var forged = constructed.OperatingSession.PreviewPriorityShedding(
            ["scanner"], requestedModes:
                new Dictionary<string, string> { ["tool"] = "forged" });
        Assert.False(forged.IsSatisfied);
        Assert.Empty(forged.ProposedShedInstanceIds);
        Assert.Contains(forged.Evaluation.Issues, x => x.Code == "construct-mode-unknown");
    }


    [Fact]
    public void PresetsRoundTripAndRevalidateWithoutReconstructingOrigin()
    {
        using var fixture = new Fixture();
        var constructed = Evaluate(fixture, assembly: Assembly(scannerEnabled: false));
        Assert.True(constructed.IsMechanicallyAuthorized);
        var session = constructed.OperatingSession!;
        var originalProfile = JsonSerializer.Serialize(constructed.Origin.Profile);
        var dormant = session.CapturePreset("Idle");
        var scanning = session.CapturePreset("Scanning",
            requestedEnabled: new Dictionary<string, bool> { ["scanner"] = true });
        Assert.NotNull(dormant.Preset);
        Assert.NotNull(scanning.Preset);
        Assert.True(scanning.Evaluation.IsOperatingConfigurationValid);

        // Serialized presets contain instance/definition identities and modes,
        // never a trusted installation snapshot or a grant authority token.
        var serialized = JsonSerializer.Serialize(scanning.Preset);
        var replay = JsonSerializer.Deserialize<NativeConstructOperatingPreset>(serialized);
        var evaluated = session.EvaluatePreset(replay);
        Assert.True(evaluated.IsOperatingConfigurationValid);
        var scan = constructed.Origin.Profile!.Current.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.extended-senses");
        var synergy = constructed.Origin.Profile.Current.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.sensor-guard");

        var transition = session.PreviewPresetTransition(dormant.Preset, replay);
        Assert.True(transition.IsTargetValid);
        Assert.Equal(new[] { "scanner" }, transition.RestoredComponentInstanceIds);
        Assert.Contains(scan.Id, transition.NewlyApplicableOriginGrantIds);
        Assert.Contains(synergy.Id, transition.NewlyApplicableOriginGrantIds);
        Assert.Empty(transition.NewlyTriggeredShutdownConsequences);
        Assert.Equal(originalProfile, JsonSerializer.Serialize(constructed.Origin.Profile));
        Assert.False(session.EvaluateConfiguration().Assembly.Components
            .Single(x => x.InstanceId == "scanner").RequestedEnabled);
    }

    [Fact]
    public void TransitionReportsCriticalRecoveryWithoutUndoingPastConsequences()
    {
        using var fixture = new Fixture();
        var constructed = Evaluate(fixture);
        Assert.True(constructed.IsMechanicallyAuthorized);
        var session = constructed.OperatingSession!;
        var running = session.CapturePreset("Running").Preset!;
        var powerOut = running with
        {
            Name = "Power Outage",
            Components = running.Components.Select(x =>
                x.InstanceId == "power" ? x with { Enabled = false } : x).ToArray()
        };
        var lost = session.PreviewPresetTransition(running, powerOut);
        Assert.False(lost.IsTargetValid);
        Assert.Contains("power", lost.InterruptedContinuousPowerInstanceIds);
        Assert.Contains(lost.NewlyTriggeredShutdownConsequences, x =>
            x.InstanceId == "power" && x.ConsequenceId == "core-unpowered");

        var recovered = session.PreviewPresetTransition(powerOut, running);
        Assert.True(recovered.IsTargetValid);
        Assert.Equal(new[] { "power" }, recovered.RestoredContinuousPowerInstanceIds);
        Assert.Empty(recovered.NewlyTriggeredShutdownConsequences);
        Assert.Contains(recovered.Before.Operating.Assembly.ShutdownConsequences, x =>
            x.InstanceId == "power" && x.ConsequenceId == "core-unpowered");
    }

    [Fact]
    public void PresetsRejectStaleDefinitionsMissingEntriesAndFabricatedModes()
    {
        using var fixture = new Fixture();
        var constructed = Evaluate(fixture, assembly: ModularToolAssembly());
        Assert.True(constructed.IsMechanicallyAuthorized);
        var session = constructed.OperatingSession!;
        var saved = session.CapturePreset("Ready").Preset!;

        var forged = saved with
        {
            Components = saved.Components.Select(x => x.InstanceId == "tool"
                ? x with { DefinitionId = "construct-component.power" } : x).ToArray()
        };
        var rejected = session.EvaluatePreset(forged);
        Assert.False(rejected.IsStructurallyValid);
        Assert.Empty(rejected.Operating.ApplicableOriginGrantIds);
        Assert.Contains(rejected.PresetIssues, x =>
            x.Code == "construct-preset-instance-invalid");

        var missing = saved with { Components = saved.Components.Skip(1).ToArray() };
        Assert.False(session.EvaluatePreset(missing).IsStructurallyValid);

        var duplicate = saved with
        {
            Components = saved.Components.Select((x, i) =>
                i == 1 ? x with { InstanceId = saved.Components[0].InstanceId } : x)
                .ToArray()
        };
        Assert.False(session.EvaluatePreset(duplicate).IsStructurallyValid);

        var fabricatedMode = saved with
        {
            Components = saved.Components.Select(x => x.InstanceId == "tool"
                ? x with { ModeId = "fabricated" } : x).ToArray()
        };
        var denied = session.EvaluatePreset(fabricatedMode);
        Assert.True(denied.IsStructurallyValid);
        Assert.False(denied.IsOperatingConfigurationValid);
        Assert.Contains(denied.Operating.Issues, x => x.Code == "construct-mode-unknown");
        var transition = session.PreviewPresetTransition(saved, forged);
        Assert.False(transition.IsTargetValid);
        Assert.Empty(transition.RestoredComponentInstanceIds);
        Assert.Empty(transition.NewlyApplicableOriginGrantIds);
    }

    [Fact]
    public void ModePresetsSwitchTraitsOnlyThroughNativeOperatingSession()
    {
        using var fixture = new Fixture();
        var constructed = Evaluate(fixture, assembly: ModularToolAssembly());
        Assert.True(constructed.IsMechanicallyAuthorized);
        var session = constructed.OperatingSession!;
        var stowed = session.CapturePreset("Stowed").Preset!;
        var tether = session.CapturePreset("Tether",
            requestedModes: new Dictionary<string, string> { ["tool"] = "tether" })
            .Preset!;
        var blast = session.CapturePreset("Blast",
            requestedModes: new Dictionary<string, string> { ["tool"] = "blast" })
            .Preset!;
        var tetherGrant = constructed.Origin.Profile!.Current.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.tether");
        var blastGrant = constructed.Origin.Profile.Current.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.blast");

        var deployed = session.PreviewPresetTransition(stowed, tether);
        Assert.True(deployed.IsTargetValid);
        Assert.Contains(tetherGrant.Id, deployed.NewlyApplicableOriginGrantIds);
        Assert.DoesNotContain(blastGrant.Id, deployed.NewlyApplicableOriginGrantIds);

        var switched = session.PreviewPresetTransition(tether, blast);
        Assert.True(switched.IsTargetValid);
        Assert.Contains(blastGrant.Id, switched.NewlyApplicableOriginGrantIds);
        Assert.Contains(tetherGrant.Id, switched.NewlyInactiveOriginGrantIds);
        Assert.Equal("blast", switched.After.Operating.Assembly.Components
            .Single(x => x.InstanceId == "tool").ModeId);
        Assert.Equal("stowed", session.EvaluateConfiguration().Assembly.Components
            .Single(x => x.InstanceId == "tool").ModeId);

        var unbalanced = session.CapturePreset("Too Much",
            requestedEnabled: new Dictionary<string, bool> { ["scanner"] = true },
            requestedModes: new Dictionary<string, string> { ["tool"] = "tether" });
        Assert.Null(unbalanced.Preset);
        Assert.False(unbalanced.Evaluation.IsOperatingConfigurationValid);
    }


    private static NativeConstructAssemblyRequest FuelAssembly(
        string source = "construct-power-source.finite") =>
        new([
            Part("generator", "fuel-housing", links: ["sensor"]) with
                { PowerSourceId = source },
            Part("sensor", "fuel-load")
        ], InitialAllowance: 20);

    private sealed class TestSolarAuthority(bool approved = true) :
        INativeConstructSolarIntervalAuthority
    {
        public bool AuthorizeInterval(
            IReadOnlyDictionary<string, NativeConstructSolarExposure> exposures,
            int elapsedMinutes, long expectedRevision) =>
            approved && elapsedMinutes > 0 &&
            exposures.ContainsKey("generator");
    }

    private static NativeConstructAssemblyRequest SolarOverdriveAssembly() =>
        new([
            Part("generator", "fuel-housing", links: ["load"]) with
                { PowerSourceId = "construct-power-source.solar-crystal" },
            Part("load", "solar-load", enabled: false)
        ], InitialAllowance: 20);

    [Fact]
    public void SolarPowerUsesElapsedEnergyAndChargesOnlyAvailableCapacity()
    {
        using var fixture = new Fixture();
        var built = Evaluate(fixture, assembly: FuelAssembly(
            "construct-power-source.solar-crystal"));
        Assert.True(built.IsMechanicallyAuthorized);
        var session = built.OperatingSession!;
        var genericAllocation = session.EvaluateConfiguration();
        Assert.False(genericAllocation.IsOperatingConfigurationValid);
        Assert.Empty(genericAllocation.ApplicableOriginGrantIds);
        Assert.Contains(genericAllocation.Issues, issue =>
            issue.Code == "construct-solar-operating-context-required");
        Assert.False(genericAllocation.Assembly.Components.Single(x =>
            x.InstanceId == "generator").SourceSupplyingPower);
        Assert.Equal(0, genericAllocation.Assembly.PowerNetworks!.Single().AvailableOutput);
        var stored = new Dictionary<string, long> { ["generator"] = 60 };
        var direct = new Dictionary<string, NativeConstructSolarExposure>
            { ["generator"] = NativeConstructSolarExposure.Direct };
        var charged = session.PreviewSolarOperation(stored, direct, 60);
        Assert.True(charged.CanAdvance);
        var source = Assert.Single(charged.Sources);
        Assert.Equal(8, source.AvailableOutput);
        Assert.Equal(16, source.CollectionPower);
        Assert.Equal(3, source.DeliveredPower);
        Assert.Equal(60, source.BeforePowerUnitMinutes);
        Assert.Equal(840, source.AfterPowerUnitMinutes);
        Assert.Equal(96L * 60, source.CapacityPowerUnitMinutes);

        var full = session.PreviewSolarOperation(
            new Dictionary<string, long> { ["generator"] = 96 * 60 },
            direct, 60);
        Assert.True(full.CanAdvance);
        Assert.Equal(96 * 60,
            Assert.Single(full.Sources).AfterPowerUnitMinutes);

        var night = session.PreviewSolarOperation(
            new Dictionary<string, long> { ["generator"] = 96 * 60 },
            new Dictionary<string, NativeConstructSolarExposure>
                { ["generator"] = NativeConstructSolarExposure.None }, 60);
        Assert.True(night.CanAdvance);
        Assert.Equal(96 * 60 - 3 * 60,
            Assert.Single(night.Sources).AfterPowerUnitMinutes);
    }

    [Fact]
    public void SolarOverdriveRequiresConcentratedSunlightNotJustFullBattery()
    {
        using var fixture = new Fixture();
        var built = Evaluate(fixture, assembly: SolarOverdriveAssembly());
        Assert.True(built.IsMechanicallyAuthorized);
        var session = built.OperatingSession!;
        var stored = new Dictionary<string, long> { ["generator"] = 96 * 60 };
        var enabled = new Dictionary<string, bool> { ["load"] = true };
        NativeConstructSolarOperationPreview EvaluateLight(NativeConstructSolarExposure light) =>
            session.PreviewSolarOperation(stored,
                new Dictionary<string, NativeConstructSolarExposure>
                    { ["generator"] = light }, 60, enabled);

        var direct = EvaluateLight(NativeConstructSolarExposure.Direct);
        Assert.False(direct.CanAdvance);
        Assert.Contains(direct.Evaluation!.Assembly.Issues,
            x => x.Code == "construct-power-overallocated");
        var night = EvaluateLight(NativeConstructSolarExposure.None);
        Assert.False(night.CanAdvance);
        var concentrated = EvaluateLight(NativeConstructSolarExposure.Concentrated);
        Assert.True(concentrated.CanAdvance);
        var source = Assert.Single(concentrated.Sources);
        Assert.True(source.OverdriveActive);
        Assert.Equal(12, source.AvailableOutput);
        Assert.Equal(9, source.DeliveredPower);
        Assert.Equal(24, source.CollectionPower);
    }

    [Fact]
    public void SolarBatteryCannotRunLongerThanItsStoredEnergyAllows()
    {
        using var fixture = new Fixture();
        var built = Evaluate(fixture, assembly: FuelAssembly(
            "construct-power-source.solar-crystal"));
        Assert.True(built.IsMechanicallyAuthorized);
        var session = built.OperatingSession!;
        var noLight = new Dictionary<string, NativeConstructSolarExposure>
            { ["generator"] = NativeConstructSolarExposure.None };
        var stored = new Dictionary<string, long> { ["generator"] = 180 };
        var shortInterval = session.PreviewSolarOperation(stored, noLight, 60);
        Assert.True(shortInterval.CanAdvance);
        Assert.Equal(0, Assert.Single(shortInterval.Sources).AfterPowerUnitMinutes);
        var longer = session.PreviewSolarOperation(stored, noLight, 61);
        Assert.False(longer.CanAdvance);
        var empty = session.PreviewSolarOperation(
            new Dictionary<string, long> { ["generator"] = 0 }, noLight, 60);
        Assert.False(empty.CanAdvance);
        Assert.False(empty.Evaluation!.Assembly.Components
            .Single(x => x.InstanceId == "generator").SourceSupplyingPower);
        Assert.Equal(0, empty.Evaluation.Assembly.PowerNetworks!.Single().AvailableOutput);
        Assert.All(empty.Evaluation.Assembly.Components,
            state => Assert.True(state.Installed));
    }

    [Fact]
    public void SolarLedgerRequiresTrustedExposureAndRejectsStaleCommits()
    {
        using var fixture = new Fixture();
        var built = Evaluate(fixture, assembly: FuelAssembly(
            "construct-power-source.solar-crystal"));
        Assert.True(built.IsMechanicallyAuthorized);
        var session = built.OperatingSession!;
        var opened = session.OpenTrustedSolarLedger(
            new Dictionary<string, long> { ["generator"] = 0 });
        Assert.Empty(opened.Issues);
        var ledger = Assert.IsType<NativeConstructSolarEnergyLedger>(opened.Ledger);
        var direct = new Dictionary<string, NativeConstructSolarExposure>
            { ["generator"] = NativeConstructSolarExposure.Direct };
        var denied = ledger.TryCommitInterval(0, direct, 60,
            new TestSolarAuthority(false));
        Assert.False(denied.Committed);
        Assert.Equal(0, ledger.Snapshot().Revision);
        var credited = ledger.TryCommitInterval(0, direct, 60,
            new TestSolarAuthority());
        Assert.True(credited.Committed);
        Assert.Equal(780, credited.Snapshot.StoredPowerUnitMinutes["generator"]);
        var replay = ledger.TryCommitInterval(0, direct, 60,
            new TestSolarAuthority());
        Assert.False(replay.Committed);
        Assert.Equal(1, ledger.Snapshot().Revision);
        Assert.Equal(780, ledger.Snapshot().StoredPowerUnitMinutes["generator"]);

        var night = new Dictionary<string, NativeConstructSolarExposure>
            { ["generator"] = NativeConstructSolarExposure.None };
        var spent = ledger.TryCommitInterval(1, night, 60,
            new TestSolarAuthority());
        Assert.True(spent.Committed);
        Assert.Equal(600, spent.Snapshot.StoredPowerUnitMinutes["generator"]);
    }

    [Fact]
    public void SolarPreviewsRejectUntrustedSourcesAndInventedBatteryBalances()
    {
        using var fixture = new Fixture();
        var built = Evaluate(fixture, assembly: FuelAssembly(
            "construct-power-source.solar-crystal"));
        Assert.True(built.IsMechanicallyAuthorized);
        var session = built.OperatingSession!;
        var direct = new Dictionary<string, NativeConstructSolarExposure>
            { ["generator"] = NativeConstructSolarExposure.Direct };
        foreach (var bad in new long[] { -1, 96 * 60 + 1 })
        {
            var preview = session.PreviewSolarOperation(
                new Dictionary<string, long> { ["generator"] = bad },
                direct, 60);
            Assert.False(preview.CanAdvance);
            Assert.Contains(preview.Issues,
                issue => issue.Code == "construct-solar-balance-invalid");
        }
        Assert.False(session.PreviewSolarOperation(
            new Dictionary<string, long> { ["generator"] = 0 },
            direct, 0).CanAdvance);
        var fake = session.PreviewSolarOperation(
            new Dictionary<string, long> { ["generator"] = 0,
                ["fabricated"] = 100 }, direct, 60);
        Assert.False(fake.CanAdvance);
        Assert.Contains(fake.Issues,
            issue => issue.Code == "construct-solar-instance-invalid");
    }

    [Fact]
    public void ResourceUseIsNativeBoundedAndLastAvailableStepCanFinish()
    {
        using var fixture = new Fixture();
        var built = Evaluate(fixture, assembly: FuelAssembly());
        Assert.True(built.IsMechanicallyAuthorized);
        var session = built.OperatingSession!;
        var origin = JsonSerializer.Serialize(built.Origin.Profile);
        var first = session.PreviewResourceStep(
            new Dictionary<string, int> { ["generator"] = 5 });
        Assert.True(first.CanAdvance);
        var balance = Assert.Single(first.Sources);
        Assert.Equal("fuel-unit", balance.ResourceKind);
        Assert.Equal(2, balance.Consumed);
        Assert.Equal(3, balance.After);
        Assert.Equal(true, first.Evaluation!.Assembly.Components.Single(x =>
            x.InstanceId == "generator").SourceSupplyingPower);
        var final = session.PreviewResourceStep(
            new Dictionary<string, int> { ["generator"] = 2 });
        Assert.True(final.CanAdvance);
        Assert.Equal(0, Assert.Single(final.Sources).After);
        Assert.Equal(origin, JsonSerializer.Serialize(built.Origin.Profile));
    }

    [Fact]
    public void ExhaustedSourceStopsSupplyAndSuppressesPoweredEntitlements()
    {
        using var fixture = new Fixture();
        var built = Evaluate(fixture, assembly: FuelAssembly());
        Assert.True(built.IsMechanicallyAuthorized);
        var step = built.OperatingSession!.PreviewResourceStep(
            new Dictionary<string, int> { ["generator"] = 1 });
        Assert.False(step.CanAdvance);
        Assert.Equal(new[] { "generator" }, step.UnavailablePowerSourceInstanceIds);
        Assert.Equal(0, Assert.Single(step.Sources).Consumed);
        Assert.Equal(1, Assert.Single(step.Sources).After);
        Assert.Equal(false, step.Evaluation!.Assembly.Components.Single(x =>
            x.InstanceId == "generator").SourceSupplyingPower);
        Assert.False(step.Evaluation.Assembly.Components.Single(x =>
            x.InstanceId == "sensor").Operational);
        Assert.All(step.Evaluation.Assembly.Components, c => Assert.True(c.Installed));
        Assert.Contains(step.Evaluation.Assembly.ShutdownConsequences, x =>
            x.InstanceId == "sensor" && x.ConsequenceId == "critical-sensor-loss");
        var grant = built.Origin.Profile!.Current.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.extended-senses");
        Assert.Contains(grant.Id, step.Evaluation.InactiveOriginGrantIds);
    }

    [Fact]
    public void DisabledSourceNeverConsumesResources()
    {
        using var fixture = new Fixture();
        var built = Evaluate(fixture, assembly: FuelAssembly());
        Assert.True(built.IsMechanicallyAuthorized);
        var step = built.OperatingSession!.PreviewResourceStep(
            new Dictionary<string, int> { ["generator"] = 4 },
            new Dictionary<string, bool>
                { ["generator"] = false, ["sensor"] = false });
        Assert.True(step.CanAdvance);
        Assert.Equal(4, Assert.Single(step.Sources).After);
        Assert.Equal(0, Assert.Single(step.Sources).Consumed);
        Assert.Equal(false, step.Evaluation!.Assembly.Components.Single(x =>
            x.InstanceId == "generator").SourceSupplyingPower);
    }

    [Fact]
    public void InvalidResourceBalancesAndUnbalancedSourcesFailClosed()
    {
        using var fixture = new Fixture();
        var built = Evaluate(fixture, assembly: FuelAssembly());
        Assert.True(built.IsMechanicallyAuthorized);
        var session = built.OperatingSession!;
        foreach (var balances in new[]
        {
            new Dictionary<string, int>(),
            new Dictionary<string, int> { ["generator"] = -1 },
            new Dictionary<string, int> { ["generator"] = 6 },
            new Dictionary<string, int> { ["generator"] = 3, ["fake"] = 7 }
        })
        {
            var invalid = session.PreviewResourceStep(balances);
            Assert.False(invalid.CanAdvance);
            Assert.Null(invalid.Evaluation);
            Assert.Empty(invalid.Sources);
            Assert.Contains(invalid.Issues, x =>
                x.Severity == NativeOriginIssueSeverity.Invalid);
        }
        var unknown = Evaluate(fixture, assembly: FuelAssembly(
            "construct-power-source.unknown-endurance"));
        Assert.True(unknown.IsMechanicallyAuthorized);
        var unresolved = unknown.OperatingSession!.PreviewResourceStep(
            new Dictionary<string, int> { ["generator"] = 3 });
        Assert.False(unresolved.CanAdvance);
        Assert.Null(unresolved.Evaluation);
        Assert.Contains(unresolved.Issues, x =>
            x.Code == "construct-resource-model-unresolved");
    }

    [Fact]
    public void UnavailableSourceMustReferenceAnInstalledSourceHousing()
    {
        using var fixture = new Fixture();
        var evaluator = new NativeConstructAssemblyEvaluator(fixture.Catalog);
        var invalid = evaluator.Evaluate(FuelAssembly() with
        {
            UnavailablePowerSourceInstanceIds = ["sensor"]
        });
        Assert.Equal(NativeOriginConstructionStatus.Invalid, invalid.Status);
        Assert.Contains(invalid.Issues, x =>
            x.Code == "construct-unavailable-source-invalid");
        var outage = evaluator.Evaluate(FuelAssembly() with
        {
            UnavailablePowerSourceInstanceIds = ["generator"]
        });
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, outage.Status);
        Assert.Equal(0, Assert.Single(outage.PowerNetworks!).AvailableOutput);
        Assert.Contains(outage.Issues, x =>
            x.Code == "construct-power-overallocated");
    }


    [Fact]
    public void ResourceStepCannotReenableAnAlreadyUnavailableNativeSource()
    {
        using var fixture = new Fixture();
        var baseline = FuelAssembly();
        var approvedOutage = baseline with
        {
            Components = baseline.Components.Select(x => x.InstanceId == "sensor"
                ? x with { Enabled = false } : x).ToArray(),
            UnavailablePowerSourceInstanceIds = ["generator"]
        };
        var built = Evaluate(fixture, assembly: approvedOutage);
        Assert.True(built.IsMechanicallyAuthorized);
        var step = built.OperatingSession!.PreviewResourceStep(
            new Dictionary<string, int> { ["generator"] = 5 },
            new Dictionary<string, bool> { ["sensor"] = true });
        Assert.False(step.CanAdvance);
        Assert.Contains("generator", step.UnavailablePowerSourceInstanceIds);
        Assert.Equal(false, step.Evaluation!.Assembly.Components.Single(x =>
            x.InstanceId == "generator").SourceSupplyingPower);
        Assert.Equal(0, step.Evaluation.Assembly.PowerNetworks!.Single().AvailableOutput);
        Assert.Equal(0, Assert.Single(step.Sources).Consumed);
        Assert.Equal(5, Assert.Single(step.Sources).After);
    }

    [Fact]
    public void FiniteResourcePreviewDoesNotClaimUnlimitedEnduranceWithoutSource()
    {
        using var fixture = new Fixture();
        var built = Evaluate(fixture);
        Assert.True(built.IsMechanicallyAuthorized);
        var step = built.OperatingSession!.PreviewResourceStep(
            new Dictionary<string, int>());
        Assert.False(step.CanAdvance);
        Assert.Null(step.Evaluation);
        Assert.Contains(step.Issues, x =>
            x.Code == "construct-resource-model-unresolved");
    }


    private static NativeConstructAssemblyRequest DualReservoirAssembly(
        string donorSource = "construct-power-source.finite",
        bool connected = true) =>
        new([
            Part("generator", "fuel-housing", links: ["sensor"]) with
                { PowerSourceId = "construct-power-source.finite" },
            Part("sensor", "fuel-load"),
            Part("donor", "fuel-housing", enabled: false,
                links: connected ? ["generator"] : null) with
                { PowerSourceId = donorSource }
        ], InitialAllowance: 20);

    [Fact]
    public void ReservoirTransferConservesResourcesAndCanRestoreNextStep()
    {
        using var fixture = new Fixture();
        var built = Evaluate(fixture, assembly: DualReservoirAssembly());
        Assert.True(built.IsMechanicallyAuthorized);
        var session = built.OperatingSession!;
        var initial = new Dictionary<string, int>
            { ["generator"] = 1, ["donor"] = 4 };
        var profile = JsonSerializer.Serialize(built.Origin.Profile);

        var depleted = session.PreviewResourceStep(initial);
        Assert.False(depleted.CanAdvance);

        var transfer = session.PreviewResourceTransfer(initial,
            "donor", "generator", 2);
        Assert.True(transfer.CanTransfer);
        Assert.Equal(5, transfer.Sources.Sum(x => x.Before));
        Assert.Equal(5, transfer.Sources.Sum(x => x.After));
        Assert.Equal(0, transfer.Sources.Sum(x => x.Delta));
        Assert.Equal(3, transfer.Sources.Single(x => x.InstanceId == "generator").After);
        Assert.Equal(2, transfer.Sources.Single(x => x.InstanceId == "donor").After);
        Assert.True(transfer.NextStep!.CanAdvance);
        Assert.Equal(1, transfer.NextStep.Sources
            .Single(x => x.InstanceId == "generator").After);
        Assert.Equal(2, transfer.NextStep.Sources
            .Single(x => x.InstanceId == "donor").After);
        Assert.Equal(profile, JsonSerializer.Serialize(built.Origin.Profile));

        // Previewing a transfer changes neither this caller snapshot nor
        // the authorized operating session.
        Assert.Equal(1, initial["generator"]);
        Assert.False(session.PreviewResourceStep(initial).CanAdvance);
    }

    [Fact]
    public void TransferRejectsOverCapacityUnderflowAndFabricatedInputs()
    {
        using var fixture = new Fixture();
        var built = Evaluate(fixture, assembly: DualReservoirAssembly());
        Assert.True(built.IsMechanicallyAuthorized);
        var session = built.OperatingSession!;
        var initial = new Dictionary<string, int>
            { ["generator"] = 4, ["donor"] = 2 };

        foreach (var (source, destination, amount) in new[]
        {
            ("donor", "generator", 2), // destination has room for one
            ("donor", "generator", 3), // donor has only two
            ("donor", "generator", 0),
            ("generator", "generator", 1),
            ("forged", "generator", 1)
        })
        {
            var preview = session.PreviewResourceTransfer(initial,
                source, destination, amount);
            Assert.False(preview.CanTransfer);
            Assert.Null(preview.NextStep);
            Assert.Empty(preview.Sources);
            Assert.NotEmpty(preview.Issues);
        }
        var forgedBalance = session.PreviewResourceTransfer(
            new Dictionary<string, int>
                { ["generator"] = 1, ["donor"] = 4, ["extra"] = 9 },
            "donor", "generator", 2);
        Assert.False(forgedBalance.CanTransfer);
        Assert.Contains(forgedBalance.Issues, x =>
            x.Code == "construct-resource-instance-invalid");
    }

    [Fact]
    public void TransferRequiresAuthoringAndPhysicalContact()
    {
        using var fixture = new Fixture();
        var balance = new Dictionary<string, int>
            { ["generator"] = 1, ["donor"] = 4 };
        var disconnected = Evaluate(fixture,
            assembly: DualReservoirAssembly(connected: false));
        Assert.True(disconnected.IsMechanicallyAuthorized);
        var noContact = disconnected.OperatingSession!.PreviewResourceTransfer(
            balance, "donor", "generator", 2);
        Assert.False(noContact.CanTransfer);
        Assert.Contains(noContact.Issues, x =>
            x.Code == "construct-resource-transfer-disconnected");

        var other = Evaluate(fixture,
            assembly: DualReservoirAssembly("construct-power-source.other-kind"));
        Assert.True(other.IsMechanicallyAuthorized);
        var mismatch = other.OperatingSession!.PreviewResourceTransfer(
            balance, "donor", "generator", 2);
        Assert.False(mismatch.CanTransfer);
        Assert.Contains(mismatch.Issues, x =>
            x.Code == "construct-resource-transfer-kind-invalid");

        var sealedDonor = Evaluate(fixture,
            assembly: DualReservoirAssembly("construct-power-source.sealed"));
        Assert.True(sealedDonor.IsMechanicallyAuthorized);
        var blocked = sealedDonor.OperatingSession!.PreviewResourceTransfer(
            balance, "donor", "generator", 2);
        Assert.False(blocked.CanTransfer);
        Assert.Contains(blocked.Issues, x =>
            x.Code == "construct-resource-transfer-port-unavailable");

        var malformed = Evaluate(fixture,
            assembly: DualReservoirAssembly("construct-power-source.invalid-transfer"));
        Assert.True(malformed.IsMechanicallyAuthorized);
        var denied = malformed.OperatingSession!.PreviewResourceTransfer(
            balance, "donor", "generator", 2);
        Assert.False(denied.CanTransfer);
        Assert.Contains(denied.Issues, x =>
            x.Code == "construct-resource-transfer-flag-invalid");
    }

    [Fact]
    public void ResourceTransferDoesNotClearPreviouslyAuthorizedSourceOutages()
    {
        using var fixture = new Fixture();
        var assembly = DualReservoirAssembly();
        var trustedOutage = assembly with
        {
            Components = assembly.Components.Select(x => x.InstanceId == "sensor"
                ? x with { Enabled = false } : x).ToArray(),
            UnavailablePowerSourceInstanceIds = ["generator"]
        };
        var built = Evaluate(fixture, assembly: trustedOutage);
        Assert.True(built.IsMechanicallyAuthorized);
        var transfer = built.OperatingSession!.PreviewResourceTransfer(
            new Dictionary<string, int> { ["generator"] = 1, ["donor"] = 4 },
            "donor", "generator", 2,
            requestedEnabled: new Dictionary<string, bool> { ["sensor"] = true });
        Assert.True(transfer.CanTransfer);
        Assert.False(transfer.NextStep!.CanAdvance);
        Assert.Equal(false, transfer.NextStep.Evaluation!.Assembly.Components
            .Single(x => x.InstanceId == "generator").SourceSupplyingPower);
        Assert.Contains("generator", transfer.NextStep.UnavailablePowerSourceInstanceIds);
        Assert.Equal(0, transfer.NextStep.Sources.Sum(x => x.Consumed));
    }


    [Fact]
    public void TrustedResourceLedgerCommitsConsumptionAndRejectsStaleReplays()
    {
        using var fixture = new Fixture();
        var built = Evaluate(fixture, assembly: FuelAssembly());
        Assert.True(built.IsMechanicallyAuthorized);
        var originalProfile = JsonSerializer.Serialize(built.Origin.Profile);
        var opened = built.OperatingSession!.OpenTrustedResourceLedger(
            new Dictionary<string, int> { ["generator"] = 5 });
        Assert.Empty(opened.Issues);
        var ledger = Assert.IsType<NativeConstructResourceLedger>(opened.Ledger);
        var detached = ledger.Snapshot();
        var mutable = Assert.IsType<Dictionary<string, int>>(detached.Balances);
        mutable["generator"] = 0;
        Assert.Equal(5, ledger.Snapshot().Balances["generator"]);

        var first = ledger.TryCommitStep(0);
        Assert.True(first.Committed);
        Assert.Equal("operating-step", first.Receipt!.Operation);
        Assert.Equal(0, first.Receipt.PreviousRevision);
        Assert.Equal(1, first.Receipt.NewRevision);
        Assert.Equal(3, first.Snapshot.Balances["generator"]);
        Assert.Equal(2, Assert.Single(first.Receipt.Changes).Before -
            Assert.Single(first.Receipt.Changes).After);

        var duplicate = ledger.TryCommitStep(0);
        Assert.False(duplicate.Committed);
        Assert.Equal(1, duplicate.Snapshot.Revision);
        Assert.Null(duplicate.Receipt);
        Assert.Contains(duplicate.Issues, x =>
            x.Code == "construct-resource-ledger-revision-conflict");

        var second = ledger.TryCommitStep(1);
        Assert.True(second.Committed);
        // Five initial units minus two authored two-unit steps leaves one.
        Assert.Equal(1, second.Snapshot.Balances["generator"]);
        var exhausted = ledger.TryCommitStep(2);
        Assert.False(exhausted.Committed);
        Assert.Equal(2, exhausted.Snapshot.Revision);
        Assert.Equal(1, exhausted.Snapshot.Balances["generator"]);
        Assert.Equal(originalProfile, JsonSerializer.Serialize(built.Origin.Profile));
    }

    [Fact]
    public void TrustedLedgerTransferCommitsWithoutSpendingTheNextStep()
    {
        using var fixture = new Fixture();
        var built = Evaluate(fixture, assembly: DualReservoirAssembly());
        Assert.True(built.IsMechanicallyAuthorized);
        var opened = built.OperatingSession!.OpenTrustedResourceLedger(
            new Dictionary<string, int> { ["generator"] = 1, ["donor"] = 4 });
        var ledger = Assert.IsType<NativeConstructResourceLedger>(opened.Ledger);

        var transfer = ledger.TryCommitTransfer(0, "donor", "generator", 2);
        Assert.True(transfer.Committed);
        Assert.Equal("reservoir-transfer", transfer.Receipt!.Operation);
        Assert.Equal(5, transfer.Receipt.Changes.Sum(x => x.Before));
        Assert.Equal(5, transfer.Receipt.Changes.Sum(x => x.After));
        Assert.Equal(3, transfer.Snapshot.Balances["generator"]);
        Assert.Equal(2, transfer.Snapshot.Balances["donor"]);
        Assert.True(transfer.Transfer!.NextStep!.CanAdvance);

        // The next operating step consumes fuel only when committed.
        var step = ledger.TryCommitStep(1);
        Assert.True(step.Committed);
        Assert.Equal(1, step.Snapshot.Balances["generator"]);
        Assert.Equal(2, step.Snapshot.Balances["donor"]);
        Assert.Equal(2, ledger.Snapshot().Revision);
    }

    [Fact]
    public void LedgerRejectsInvalidInitialBalancesAndNeverMintsResources()
    {
        using var fixture = new Fixture();
        var built = Evaluate(fixture, assembly: DualReservoirAssembly());
        Assert.True(built.IsMechanicallyAuthorized);
        var session = built.OperatingSession!;
        foreach (var balances in new[]
        {
            new Dictionary<string, int> { ["generator"] = 5 },
            new Dictionary<string, int> { ["generator"] = 1, ["donor"] = 6 },
            new Dictionary<string, int> { ["generator"] = 1, ["donor"] = 4,
                ["fabricated"] = 2 }
        })
        {
            var rejected = session.OpenTrustedResourceLedger(balances);
            Assert.Null(rejected.Ledger);
            Assert.NotEmpty(rejected.Issues);
        }

        var opened = session.OpenTrustedResourceLedger(
            new Dictionary<string, int> { ["generator"] = 4, ["donor"] = 2 });
        var ledger = Assert.IsType<NativeConstructResourceLedger>(opened.Ledger);
        var invalid = ledger.TryCommitTransfer(0, "donor", "generator", 2);
        Assert.False(invalid.Committed);
        Assert.Null(invalid.Receipt);
        Assert.Equal(0, invalid.Snapshot.Revision);
        Assert.Equal(6, invalid.Snapshot.Balances.Values.Sum());

        var forged = ledger.TryCommitTransfer(0, "fabricated", "generator", 1);
        Assert.False(forged.Committed);
        Assert.Equal(0, ledger.Snapshot().Revision);
        Assert.Equal(6, ledger.Snapshot().Balances.Values.Sum());
    }

    [Fact]
    public void LedgerPermitsConservedTransfersDuringOutagesWithoutReactivation()
    {
        using var fixture = new Fixture();
        var assembly = DualReservoirAssembly();
        var approvedOutage = assembly with
        {
            Components = assembly.Components.Select(x => x.InstanceId == "sensor"
                ? x with { Enabled = false } : x).ToArray(),
            UnavailablePowerSourceInstanceIds = ["generator"]
        };
        var built = Evaluate(fixture, assembly: approvedOutage);
        Assert.True(built.IsMechanicallyAuthorized);
        var ledger = Assert.IsType<NativeConstructResourceLedger>(
            built.OperatingSession!.OpenTrustedResourceLedger(
                new Dictionary<string, int> { ["generator"] = 1, ["donor"] = 4 }).Ledger);
        var transferred = ledger.TryCommitTransfer(0, "donor", "generator", 2);
        Assert.True(transferred.Committed);
        // The sensor is deliberately off in this next-step preview, so the
        // configuration itself is valid. However, the approved outage still
        // suppresses the source even after a successful reservoir transfer.
        Assert.True(transferred.Transfer!.NextStep!.CanAdvance);
        Assert.Equal(false, transferred.Transfer.NextStep.Evaluation!.Assembly
            .Components.Single(x => x.InstanceId == "generator").SourceSupplyingPower);
        Assert.Equal(3, ledger.Snapshot().Balances["generator"]);

        // Additional fuel can not clear an outage owned by trusted state.
        var powering = ledger.TryCommitStep(1,
            requestedEnabled: new Dictionary<string, bool> { ["sensor"] = true });
        Assert.False(powering.Committed);
        Assert.Equal(1, ledger.Snapshot().Revision);
        Assert.Equal(3, ledger.Snapshot().Balances["generator"]);
    }

    [Fact]
    public void ConcurrentRevisionAttemptsCommitAtMostOnce()
    {
        using var fixture = new Fixture();
        var built = Evaluate(fixture, assembly: FuelAssembly());
        Assert.True(built.IsMechanicallyAuthorized);
        var ledger = Assert.IsType<NativeConstructResourceLedger>(
            built.OperatingSession!.OpenTrustedResourceLedger(
                new Dictionary<string, int> { ["generator"] = 5 }).Ledger);
        var outcomes = Enumerable.Range(0, 12)
            .AsParallel()
            .Select(_ => ledger.TryCommitStep(0)).ToArray();
        Assert.Single(outcomes, x => x.Committed);
        Assert.Equal(1, ledger.Snapshot().Revision);
        Assert.Equal(3, ledger.Snapshot().Balances["generator"]);
    }

    private static NativeConstructAssemblyRequest CompleteSyntheticConstruct() =>
        new([
            Part("power", "power", links: ["plate", "scanner", "generator", "aptitude"]),
            Part("plate", "plate") with
            {
                DesignId = "construct-design.reinforced",
                EnhancementIds = ["construct-enhancement.active-plate"]
            },
            Part("scanner", "scanner"),
            Part("aptitude", "aptitude"),
            new NativeConstructComponentInstance("generator",
                "construct-component.fuel-housing",
                PowerSourceId: "construct-power-source.trait-generator")
        ], InitialAllowance: 20);

    [Fact]
    public void CompleteCustomConstructUsesSharedIntrinsicsChoicesAndInstalledOptions()
    {
        using var fixture = new Fixture();
        var assembly = CompleteSyntheticConstruct();
        var discovery = Evaluate(fixture, Proposal(NativeOriginIntent.Custom), assembly);
        Assert.True(discovery.Assembly.IsAuthorized,
            string.Join("; ", discovery.Assembly.Issues.Select(x => x.Code)));
        Assert.Equal(14, discovery.Assembly.KnownConstructionCost);
        var candidates = discovery.Origin.EvaluatedProposal.Candidates;
        string Candidate(string id) => candidates.Single(x =>
            x.Identity.MechanicalDefinitionId == id).Id;

        var manifestations = new NativeOriginManifestation[]
        {
            new("type", "construct:intrinsic:creatureType",
                SelectedValue: JsonSerializer.SerializeToElement("construct")),
            new("size", "construct:intrinsic:size", Choices:
                [new("size", ["large"])]),
            new("plate", Candidate("trait.fixture.passive-plate")),
            new("steel", Candidate("trait.fixture.material-trait")),
            new("reinforcement", Candidate("trait.fixture.design-trait")),
            new("active-plate", Candidate("trait.fixture.enhancement-active")),
            new("core", Candidate("trait.fixture.source-passive")),
            new("core-active", Candidate("trait.fixture.source-active")),
            new("aptitude", Candidate("trait.fixture.ability-choice"), Choices:
                [new("abilityChoice", ["wisdom"])]),
            new("dependent", Candidate("trait.fixture.aptitude-dependent"))
        };
        var originProposal = Proposal(NativeOriginIntent.Custom) with
        {
            Manifestations = manifestations
        };
        var completed = Evaluate(fixture, originProposal, assembly);
        Assert.True(completed.IsMechanicallyAuthorized,
            string.Join("; ", completed.Origin.Issues.Select(x => x.Code)));
        var profile = completed.Origin.Profile!;
        Assert.Equal(NativeOriginEventStage.Completed, profile.History.Single(x =>
            x.Id == "construction:construct-origin-test").Stage);
        Assert.Equal("construct", profile.Underlying.InherentGrants.Single(x =>
            x.Identity.IntrinsicSlotId == "creatureType").EffectiveValue!.Value.GetString());
        Assert.Equal("large", profile.Underlying.InherentGrants.Single(x =>
            x.Identity.IntrinsicSlotId == "size").EffectiveValue!.Value.GetString());
        var aptitude = profile.Underlying.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.ability-choice");
        Assert.Equal("wisdom", Assert.Single(aptitude.ResolvedChoices!).
            SelectedOptionIds.Single());
        var dependent = profile.Underlying.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.aptitude-dependent");
        Assert.Equal("wisdom", dependent.Parameters!["aptitude"].GetString());
        Assert.Equal(Candidate("trait.fixture.ability-choice"),
            dependent.ParameterSources!["aptitude"]);

        foreach (var (definition, sourceId) in new[]
        {
            ("trait.fixture.material-trait", "construct-material.steel"),
            ("trait.fixture.design-trait", "construct-design.reinforced"),
            ("trait.fixture.enhancement-active", "construct-enhancement.active-plate"),
            ("trait.fixture.source-passive", "construct-power-source.trait-generator")
        })
            Assert.Equal(sourceId, profile.Underlying.InherentGrants.Single(x =>
                x.Identity.MechanicalDefinitionId == definition)
                .Identity.Occurrence.Source.Id);

        var initialGrants = JsonSerializer.Serialize(profile.Underlying.InherentGrants);
        var off = completed.OperatingSession!.EvaluateAllocation(
            new Dictionary<string, bool>
            {
                ["plate"] = false,
                ["generator"] = false
            });
        Assert.True(off.IsOperatingConfigurationValid,
            string.Join("; ", off.Issues.Select(x => x.Code)));
        var poweredPlate = profile.Underlying.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.enhancement-active");
        var poweredSource = profile.Underlying.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.source-active");
        var passiveMaterial = profile.Underlying.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.material-trait");
        Assert.Contains(poweredPlate.Id, off.InactiveOriginGrantIds);
        Assert.Contains(poweredSource.Id, off.InactiveOriginGrantIds);
        Assert.Contains(passiveMaterial.Id, off.ApplicableOriginGrantIds);
        Assert.Equal(initialGrants, JsonSerializer.Serialize(profile.Underlying.InherentGrants));
    }

    [Fact]
    public void ConstructIntrinsicAndTraitChoicesRejectUnauthorizedSelections()
    {
        using var fixture = new Fixture();
        var assembly = CompleteSyntheticConstruct();
        var discovered = Evaluate(fixture, Proposal(NativeOriginIntent.Custom), assembly);
        var ability = discovered.Origin.EvaluatedProposal.Candidates.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.ability-choice");
        var invalid = Evaluate(fixture, Proposal(NativeOriginIntent.Custom) with
        {
            Manifestations =
            [
                new("type", "construct:intrinsic:creatureType",
                SelectedValue: JsonSerializer.SerializeToElement("construct")),
                new("size", "construct:intrinsic:size", Choices:
                    [new("size", ["giant"])]),
                new("ability", ability.Id, Choices:
                    [new("abilityChoice", ["charisma"])])
            ]
        }, assembly);
        Assert.False(invalid.IsMechanicallyAuthorized);
        Assert.Equal(NativeOriginConstructionStatus.Invalid, invalid.Origin.Status);
        Assert.Contains(invalid.Origin.Issues, x => x.Code == "dependent-choice-invalid");

        var missing = Evaluate(fixture, Proposal(NativeOriginIntent.Custom) with
        {
            Manifestations =
            [
                new("type", "construct:intrinsic:creatureType",
                SelectedValue: JsonSerializer.SerializeToElement("construct")),
                new("size", "construct:intrinsic:size"),
                new("ability", ability.Id)
            ]
        }, assembly);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, missing.Origin.Status);
        Assert.Contains(missing.Origin.Issues, x =>
            x.Code == "dependent-choice-incomplete");
    }

    [Fact]
    public void IdenticalConstructInstancesCannotBorrowAnothersBoundAptitude()
    {
        using var fixture = new Fixture();
        var assembly = new NativeConstructAssemblyRequest(
            [Part("apt-a", "aptitude"), Part("apt-b", "aptitude")],
            InitialAllowance: 20);
        var proposal = Proposal(NativeOriginIntent.Custom) with
        {
            Manifestations =
            [
                new("type", "construct:intrinsic:creatureType",
                    SelectedValue: JsonSerializer.SerializeToElement("construct")),
                new("size", "construct:intrinsic:size", Choices:
                    [new("size", ["medium"])]),
                new("aptitude-a", "construct:apt-a:trait:0", Choices:
                    [new("abilityChoice", ["wisdom"])]),
                // The dependent on apt-b has no selected aptitude on apt-b.
                new("dependent-b", "construct:apt-b:trait:1")
            ]
        };
        var result = Evaluate(fixture, proposal, assembly);
        Assert.False(result.IsMechanicallyAuthorized);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, result.Origin.Status);
        Assert.Contains(result.Origin.Issues, x =>
            x.Code == "origin-binding-choice-missing");
        Assert.DoesNotContain(result.Origin.Profile?.Underlying.InherentGrants ?? [],
            x => x.Identity.MechanicalDefinitionId == "trait.fixture.aptitude-dependent");
    }

    private static NativeOriginProposal CustomWithOperationalTrait(
        string grantId, string candidateId) =>
        Proposal(NativeOriginIntent.Custom) with
        {
            Manifestations =
            [
                new("type", "construct:intrinsic:creatureType",
                    SelectedValue: JsonSerializer.SerializeToElement("construct")),
                new("size", "construct:intrinsic:size", Choices:
                    [new("size", ["large"])]),
                new(grantId, candidateId)
            ]
        };

    [Fact]
    public void Ca01UnavailablePowerSourceCannotBeKeptActiveByUnselectedHousingTrait()
    {
        using var fixture = new Fixture(overlappingHousingTrait: true);
        var assembly = new NativeConstructAssemblyRequest(
            [new NativeConstructComponentInstance("hub",
                "construct-component.fuel-housing",
                PowerSourceId: "construct-power-source.trait-generator")],
            InitialAllowance: 20);
        var issued = Evaluate(fixture, CustomWithOperationalTrait(
            "source", "construct:hub:power-source:operational-trait:0"), assembly);
        Assert.True(issued.IsMechanicallyAuthorized,
            string.Join("; ", issued.Origin.Issues.Select(x => x.Code)));
        var profile = issued.Origin.Profile!;
        var serialized = JsonSerializer.Serialize(profile);
        var selected = profile.Underlying.InherentGrants.Single(x => x.Id == "grant:source");
        Assert.Equal("construct-power-source.trait-generator",
            selected.Identity.Occurrence.Source.Id);
        Assert.Equal("/operationalOriginTraitIds/0/power-source/instance/hub",
            selected.Identity.Occurrence.OccurrencePath);
        Assert.Equal(new[] { "construct:hub:power-source:operational-trait:0" },
            selected.SourceCandidateIds);

        var online = issued.OperatingSession!.EvaluateConfiguration();
        Assert.True(online.IsOperatingConfigurationValid);
        Assert.Contains(selected.Id, online.ApplicableOriginGrantIds);
        var sourceOut = issued.OperatingSession.EvaluateConfiguration(
            unavailablePowerSourceInstanceIds: ["hub"]);
        Assert.True(sourceOut.IsOperatingConfigurationValid);
        var housing = sourceOut.Assembly.Components.Single(x => x.InstanceId == "hub");
        Assert.True(housing.Operational);
        Assert.Equal(false, housing.SourceSupplyingPower);
        Assert.Contains("trait.fixture.source-active",
            housing.ActiveOperationalOriginTraitIds!);
        Assert.DoesNotContain(selected.Id, sourceOut.ApplicableOriginGrantIds);
        Assert.Contains(selected.Id, sourceOut.InactiveOriginGrantIds);
        Assert.Equal(serialized, JsonSerializer.Serialize(profile));

        // Independent selection of the housing occurrence remains valid.
        var housingIssued = Evaluate(fixture, CustomWithOperationalTrait(
            "housing", "construct:hub:operational-trait:0"), assembly);
        Assert.True(housingIssued.IsMechanicallyAuthorized);
        var housingOut = housingIssued.OperatingSession!.EvaluateConfiguration(
            unavailablePowerSourceInstanceIds: ["hub"]);
        Assert.True(housingOut.IsOperatingConfigurationValid);
        Assert.Contains("grant:housing", housingOut.ApplicableOriginGrantIds);
    }

    [Fact]
    public void Ca01ComponentModeCannotBeKeptActiveByUnselectedDesignTrait()
    {
        using var fixture = new Fixture(overlappingModeDesignTrait: true);
        var assembly = new NativeConstructAssemblyRequest(
        [
            Part("battery", "power", links: ["tool"]),
            Part("tool", "multi", mode: "tether") with
            {
                DesignId = "construct-design.mode-addon"
            }
        ], InitialAllowance: 20);
        var issued = Evaluate(fixture, CustomWithOperationalTrait(
            "tool", "construct:tool:operational-trait:0"), assembly);
        Assert.True(issued.IsMechanicallyAuthorized,
            string.Join("; ", issued.Origin.Issues.Select(x => x.Code)));
        var profile = issued.Origin.Profile!;
        var serialized = JsonSerializer.Serialize(profile);
        var selected = profile.Underlying.InherentGrants.Single(x => x.Id == "grant:tool");
        Assert.Equal("construct-component.multi", selected.Identity.Occurrence.Source.Id);
        Assert.Equal("/operationalOriginTraitIds/0/instance/tool",
            selected.Identity.Occurrence.OccurrencePath);
        Assert.Equal(new[] { "construct:tool:operational-trait:0" },
            selected.SourceCandidateIds);

        var tethered = issued.OperatingSession!.EvaluateConfiguration();
        Assert.True(tethered.IsOperatingConfigurationValid);
        Assert.Contains(selected.Id, tethered.ApplicableOriginGrantIds);
        var stowed = issued.OperatingSession.EvaluateConfiguration(
            requestedModes: new Dictionary<string, string> { ["tool"] = "stowed" });
        Assert.True(stowed.IsOperatingConfigurationValid);
        var tool = stowed.Assembly.Components.Single(x => x.InstanceId == "tool");
        Assert.True(tool.Operational);
        Assert.Equal("stowed", tool.ModeId);
        Assert.Contains("trait.fixture.tether",
            tool.ActiveOperationalOriginTraitIds!);
        Assert.DoesNotContain(selected.Id, stowed.ApplicableOriginGrantIds);
        Assert.Contains(selected.Id, stowed.InactiveOriginGrantIds);
        Assert.Equal(serialized, JsonSerializer.Serialize(profile));

        // Independent selection of the design occurrence survives stowing.
        var designIssued = Evaluate(fixture, CustomWithOperationalTrait(
            "design", "construct:tool:design:operational-trait:0"), assembly);
        Assert.True(designIssued.IsMechanicallyAuthorized);
        var designStowed = designIssued.OperatingSession!.EvaluateConfiguration(
            requestedModes: new Dictionary<string, string> { ["tool"] = "stowed" });
        Assert.True(designStowed.IsOperatingConfigurationValid);
        Assert.Contains("grant:design", designStowed.ApplicableOriginGrantIds);
    }

}
