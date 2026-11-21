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
    [InlineData("climbing-attachment", "climbing", "climb", 20)]
    [InlineData("flight-propulsion", "flying", "fly", 30)]
    [InlineData("excavation-drive", "burrowing", "burrow", 15)]
    [InlineData("aquatic-propulsion", "swimming", "swim", 20)]
    public void MovementComponentsReferToCanonicalModeCandidates(
        string componentSlug, string traitName, string mode, int speedFeet)
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
        Assert.Equal(speedFeet, effect.GetProperty("speedFeet").GetInt32());
    }

    [Fact]
    public void FlightBuildHasManufacturingCostAndOperationalMovement()
    {
        var catalog = Catalog();
        var cwd = new DirectoryInfo(AppContext.BaseDirectory);
        while (cwd is not null && !File.Exists(Path.Combine(cwd.FullName,
            "data", "reference", "construct-basic-starting-build-20261120.json")))
            cwd = cwd.Parent;
        Assert.NotNull(cwd);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            cwd!.FullName, "data", "reference",
            "construct-basic-starting-build-20261120.json")));
        var basis = JsonSerializer.Deserialize<NativeConstructAssemblyRequest>(
            json.RootElement.GetProperty("assembly").GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var flight = new NativeConstructComponentInstance(
            "flight", "construct-component.flight-propulsion",
            Materials: new Dictionary<string, string>
                { ["propulsor"] = "construct-material.wood" },
            ConnectedTo: ["generator"]);
        var assembly = basis with
        {
            Components = basis.Components.Select(x =>
                x.InstanceId == "drive" ? x with { Enabled = false } : x)
                .Append(flight).ToArray()
        };
        var assessment = new NativeConstructAssemblyEvaluator(catalog).Evaluate(assembly);
        Assert.True(assessment.IsAuthorized,
            string.Join("; ", assessment.Issues.Select(x => x.Code)));
        Assert.Equal(183, assessment.KnownConstructionCost);
        Assert.Equal(8, Assert.Single(assessment.PowerNetworks!).RequestedDemand);

        var proposal = new NativeOriginProposal(
            "playtest-flying-construct", NativeOriginIntent.Custom,
            new("rule.creature-origination", "construct-assembly"), [], [], []);
        var service = new NativeConstructOriginConstructionService(catalog);
        var draft = service.Evaluate(new(new(proposal), assembly));
        var manifestations = draft.Origin.EvaluatedProposal.Candidates
            .Select((candidate, i) => new NativeOriginManifestation(
                "playtest-flight-" + i, candidate.Id,
                SelectedValue: candidate.Identity.Kind ==
                    NativeOriginCharacteristicKind.Intrinsic
                    ? candidate.OfferedValue : null)).ToArray();
        var built = service.Evaluate(new(new(proposal with
            { Manifestations = manifestations }), assembly));
        Assert.True(built.IsMechanicallyAuthorized,
            string.Join("; ", built.Origin.Issues.Select(x => x.Code)));

        var flightMode = NativeOriginMovementModeProjector
            .ProjectConstruct(catalog, built);
        Assert.Equal("resolved", flightMode.Status);
        var fly = Assert.Single(flightMode.Modes, mode => mode.Mode == "fly");
        Assert.Equal(30, fly.SpeedFeet);
        Assert.Equal("construct-component.flight-propulsion", fly.SourceId);

        var disabled = NativeOriginMovementModeProjector.ProjectConstruct(
            catalog, built, new Dictionary<string, bool> { ["flight"] = false });
        Assert.DoesNotContain(disabled.Modes, mode => mode.Mode == "fly");
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
    public void AdditionalMaterialsHavePlaytestPricesAndPhysicalMass()
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

        // Newly selectable materials must participate in the standard quote
        // and retained-mass accounting, rather than being treated as free.
        var plated = new NativeConstructComponentInstance("glass-plating",
            "construct-component.protective-plating",
            Materials: new Dictionary<string, string>
                { ["plating"] = "construct-material.glass" });
        var assessment = new NativeConstructAssemblyEvaluator(catalog).Evaluate(
            new NativeConstructAssemblyRequest([plated], InitialAllowance: 200));
        Assert.True(assessment.IsAuthorized,
            string.Join("; ", assessment.Issues.Select(x => x.Code)));
        Assert.Equal(24, assessment.KnownConstructionCost);
        Assert.Equal(30, assessment.KnownRetainedMaterialMassPounds);
        Assert.True(assessment.IsBodyMassComplete);
    }

    [Theory]
    [InlineData("operator-station")]
    [InlineData("deployable-unit-bay")]
    public void AdvancedControlAndDeploymentResolveManufacturingRecipes(string slug)
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("construct-component." + slug,
            out var component));
        var materialSlot = Assert.Single(component!.Definition
            .GetProperty("materialSlots").EnumerateArray()).GetString()!;
        var installed = new NativeConstructComponentInstance(slug,
            component.Id, Materials: new Dictionary<string, string>
                { [materialSlot] = "construct-material.steel" });
        var recipeId = component.Definition.GetProperty("craftingRecipeId").GetString()!;
        Assert.True(catalog.TryGetById(recipeId, out var recipe));
        Assert.Equal(component.Id,
            recipe!.Definition.GetProperty("producesDefinitionId").GetString());
        Assert.True(component.Definition.GetProperty("powerDemand").GetInt32() >= 0);
        var assessment = new NativeConstructAssemblyEvaluator(catalog).Evaluate(
            new NativeConstructAssemblyRequest([installed], InitialAllowance: 200));
        Assert.DoesNotContain(assessment.Issues, issue =>
            issue.Code is "crafting-quote-recipe-unresolved" or
                "construct-power-demand-unresolved");
    }
    [Fact]
    public void SolarFirstPartySourceAuthorsApprovedOutputCollectionAndBattery()
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("construct-power-source.solar-crystal",
            out var source));
        Assert.Equal("construct-power-source", source!.Category);
        var solar = source.Definition;
        Assert.Equal(8, solar.GetProperty("powerOutput").GetInt32());
        Assert.Equal(12, solar.GetProperty("overdrivePowerOutput").GetInt32());
        Assert.Equal(50, solar.GetProperty("overdriveIncreasePercent").GetInt32());
        Assert.Equal(96, solar.GetProperty("storageCapacityPowerUnitHours").GetInt32());
        var collection = solar.GetProperty("solarCollectionPower");
        Assert.Equal(0, collection.GetProperty("none").GetInt32());
        Assert.Equal(8, collection.GetProperty("indirect").GetInt32());
        Assert.Equal(16, collection.GetProperty("direct").GetInt32());
        Assert.Equal(24, collection.GetProperty("concentrated").GetInt32());
        Assert.Equal("dm-confirmed-concentrated-sunlight",
            solar.GetProperty("overdriveCondition").GetString());
        Assert.True(catalog.TryGetById(
            solar.GetProperty("craftingRecipeId").GetString()!, out var recipe));
        Assert.Equal(source.Id,
            recipe!.Definition.GetProperty("producesDefinitionId").GetString());
        Assert.True(catalog.TryGetById("construct-power-source.mechanical-engine",
            out var mechanical));
        Assert.Equal(8, mechanical!.Definition.GetProperty("powerOutput").GetInt32());
        Assert.Equal(1, mechanical.Definition
            .GetProperty("dailySustenance").GetProperty("unitsPerDay").GetInt32());
    }

    [Fact]
    public void SolarReferenceBuildChargesAndSupportsNormalOvernightOperation()
    {
        var catalog = Catalog();
        var cwd = new DirectoryInfo(AppContext.BaseDirectory);
        while (cwd is not null && !File.Exists(Path.Combine(cwd.FullName,
            "data", "reference", "construct-basic-starting-build-20261120.json")))
            cwd = cwd.Parent;
        Assert.NotNull(cwd);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            cwd!.FullName, "data", "reference",
            "construct-basic-starting-build-20261120.json")));
        var basic = JsonSerializer.Deserialize<NativeConstructAssemblyRequest>(
            json.RootElement.GetProperty("assembly").GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var solarAssembly = basic with
        {
            Components = basic.Components.Select(x =>
                x.InstanceId == "generator"
                    ? x with { PowerSourceId = "construct-power-source.solar-crystal" }
                    : x).ToArray()
        };
        var initial = new NativeConstructAssemblyEvaluator(catalog).Evaluate(solarAssembly);
        Assert.True(initial.IsAuthorized,
            string.Join("; ", initial.Issues.Select(x => x.Code)));
        Assert.Equal(7, Assert.Single(initial.PowerNetworks!).RequestedDemand);
        Assert.Equal(8, Assert.Single(initial.PowerNetworks!).AvailableOutput);

        var proposal = new NativeOriginProposal(
            "solar-first-party-reference", NativeOriginIntent.Custom,
            new("rule.creature-origination", "construct-assembly"), [], [], []);
        var service = new NativeConstructOriginConstructionService(catalog);
        var draft = service.Evaluate(new(new(proposal), solarAssembly));
        var manifestations = draft.Origin.EvaluatedProposal.Candidates
            .Select((candidate, i) => new NativeOriginManifestation(
                "solar-reference-" + i, candidate.Id,
                SelectedValue: candidate.Identity.Kind ==
                    NativeOriginCharacteristicKind.Intrinsic
                    ? candidate.OfferedValue : null)).ToArray();
        var built = service.Evaluate(new(new(proposal with
            { Manifestations = manifestations }), solarAssembly));
        Assert.True(built.IsMechanicallyAuthorized,
            string.Join("; ", built.Origin.Issues.Select(x => x.Code)));
        var session = built.OperatingSession!;

        var dark = new Dictionary<string, NativeConstructSolarExposure>
            { ["generator"] = NativeConstructSolarExposure.None };
        var full = new Dictionary<string, long> { ["generator"] = 96 * 60 };
        var overnight = session.PreviewSolarOperation(full, dark, 12 * 60);
        Assert.True(overnight.CanAdvance);
        Assert.Equal((96 - 7 * 12) * 60,
            Assert.Single(overnight.Sources).AfterPowerUnitMinutes);

        var tooLong = session.PreviewSolarOperation(full, dark, 14 * 60);
        Assert.False(tooLong.CanAdvance);

        var sun = new Dictionary<string, NativeConstructSolarExposure>
            { ["generator"] = NativeConstructSolarExposure.Direct };
        var charging = session.PreviewSolarOperation(
            new Dictionary<string, long> { ["generator"] = 0 },
            sun, 10 * 60 + 40);
        Assert.True(charging.CanAdvance);
        Assert.Equal(96 * 60,
            Assert.Single(charging.Sources).AfterPowerUnitMinutes);
    }

}
