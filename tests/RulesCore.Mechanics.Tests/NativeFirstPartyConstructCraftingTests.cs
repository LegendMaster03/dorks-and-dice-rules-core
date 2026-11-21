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
        Assert.Equal(56, steel.KnownRetainedMaterialMassPounds);
        Assert.Equal(16, wood.KnownRetainedMaterialMassPounds);
        Assert.Empty(steel.UnresolvedRetainedMassMaterials!);
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
        // Plating remains physically installed (and contributes to measured
        // retained mass) even when its operational state is disabled.
        Assert.Equal(140, assembly.KnownRetainedMaterialMassPounds);
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

    [Theory]
    [InlineData("small", 4, 16)]
    [InlineData("medium", 8, 32)]
    [InlineData("large", 16, 64)]
    public void BodySizeComesFromVerifiedFrameRecipeVariant(
        string size, long quantity, long cost)
    {
        var catalog = Catalog();
        var frame = Part("body", "structural-frame", "frame", "steel")
            with { ConstructionVariantId = size };
        var result = Quote(catalog, frame);
        Assert.True(result.IsAuthorized,
            string.Join("; ", result.Issues.Select(x => x.Code)));
        Assert.Equal(size, result.DerivedCreatureSize);
        Assert.Equal(quantity,
            result.RequiredConstructionResources!["construct-material.steel"]);
        Assert.Equal(quantity * 7, result.KnownRetainedMaterialMassPounds);
        Assert.Equal(cost, result.KnownConstructionCost);
    }

    [Fact]
    public void SizedCasingCanRepresentBodyWithoutMandatoryFrame()
    {
        var catalog = Catalog();
        var body = Part("body", "external-casing", "casing", "wood")
            with { ConstructionVariantId = "small" };
        var result = Quote(catalog, body);
        Assert.True(result.IsAuthorized);
        Assert.Equal("small", result.DerivedCreatureSize);
        Assert.Equal(4, result.KnownConstructionCost);
    }

    [Fact]
    public void UnsizedHardwareDoesNotBecomeAnIndependentSizeChoice()
    {
        var catalog = Catalog();
        var result = Quote(catalog,
            Part("body", "structural-frame", "frame", "steel"));
        Assert.True(result.IsAuthorized);
        Assert.Null(result.DerivedCreatureSize);
    }

    [Fact]
    public void ContradictoryBodyComponentsCannotSilentlySelectSize()
    {
        var catalog = Catalog();
        var result = Quote(catalog,
            Part("frame", "structural-frame", "frame", "steel") with
            { ConstructionVariantId = "large" },
            Part("case", "external-casing", "casing", "wood") with
            { ConstructionVariantId = "small" });

        Assert.Equal(NativeOriginConstructionStatus.Conflicting, result.Status);
        Assert.Null(result.DerivedCreatureSize);
        Assert.Contains(result.Issues, x => x.Code == "construct-body-size-conflict");
        Assert.Equal(68, result.KnownConstructionCost);
    }

    [Fact]
    public void UnsupportedManufacturingVariantDoesNotBecomeFreeOrAuthorizeSize()
    {
        var catalog = Catalog();
        var result = Quote(catalog,
            Part("frame", "structural-frame", "frame", "steel") with
            { ConstructionVariantId = "gargantuan" });

        Assert.Equal(NativeOriginConstructionStatus.Invalid, result.Status);
        Assert.Null(result.DerivedCreatureSize);
        Assert.Contains(result.Issues, x =>
            x.Code == "construct-construction-variant-invalid");
    }

    [Fact]
    public void HeavyInstalledSurfacesRemainPhysicalWithoutAssumedLocomotionCapacity()
    {
        var catalog = Catalog();
        var parts = new List<NativeConstructComponentInstance>
        {
            Part("housing", "power-source-housing", "housing", "steel",
                powerSource: "construct-power-source.mechanical-engine",
                links: ["drive", "plate-0", "plate-1", "plate-2",
                        "plate-3", "plate-4", "plate-5"]),
            Part("drive", "locomotion-unit", "contact", "steel")
        };
        for (var index = 0; index < 6; index++)
            parts.Add(Part("plate-" + index, "protective-plating",
                "plating", "steel", enabled: false));

        var result = Quote(catalog, [.. parts]);
        Assert.True(result.IsAuthorized);
        Assert.NotNull(result.PhysicalLoad);
        Assert.Equal("unresolved", result.PhysicalLoad.Status);
        Assert.Null(result.PhysicalLoad.VerifiedSupportCapacityPounds);
        Assert.All(parts, part =>
            Assert.Contains(result.Components, state =>
                state.InstanceId == part.InstanceId && state.Installed));
        Assert.Equal(6, result.PhysicalSurfaces.Count(x =>
            x.Role == "protective-surface"));
        Assert.All(result.PhysicalSurfaces.Where(x =>
            x.Role == "protective-surface"), surface =>
        {
            Assert.Equal("construct-material.steel", surface.MaterialDefinitionId);
            Assert.Equal("rigid", surface.Rigidity);
            Assert.False(surface.Operational);
        });
    }

    [Fact]
    public void LoadSharingMetadataDoesNotInventNumericalCapacity()
    {
        var catalog = Catalog();
        var components = new List<NativeConstructComponentInstance>
        {
            Part("housing", "power-source-housing", "housing", "steel",
                powerSource: "construct-power-source.mechanical-engine",
                links: ["drive-a", "drive-b"]),
            Part("drive-a", "locomotion-unit", "contact", "steel"),
            Part("drive-b", "locomotion-unit", "contact", "steel")
        };
        var result = Quote(catalog, [.. components]);
        Assert.True(result.IsAuthorized);
        Assert.Equal("unresolved", result.PhysicalLoad?.Status);
        Assert.Null(result.PhysicalLoad?.VerifiedSupportCapacityPounds);
        Assert.Equal(2, result.PhysicalLoadSupports.Count);
        Assert.All(result.PhysicalLoadSupports, x =>
        {
            Assert.True(x.Operating);
            Assert.True(x.SharesLoad);
            Assert.Null(x.MaximumGrossLoadPounds);
        });
    }

    [Fact]
    public void SteelHardwareIsNotAutomaticallyDefensiveArmor()
    {
        var catalog = Catalog();
        var result = Quote(catalog, Part("eye", "crystal-eye", "crystal", "steel",
            enabled: false));
        Assert.True(result.IsAuthorized);
        Assert.Empty(result.PhysicalSurfaces);
    }

    [Fact]
    public void UnpoweredProtectiveMaterialRemainsVisibleWithoutArmorGrant()
    {
        var catalog = Catalog();
        var result = Quote(catalog, Part("plating", "protective-plating",
            "plating", "cloth", enabled: false));
        var layer = Assert.Single(result.PhysicalSurfaces);
        Assert.Equal("protective-surface", layer.Role);
        Assert.Equal("construct-material.cloth", layer.MaterialDefinitionId);
        Assert.Equal("flexible", layer.Rigidity);
        Assert.False(layer.Operational);
        Assert.Empty(result.Components.Single().ActiveOperationalOriginTraitIds!);
    }

    [Fact]
    public void OfflineDriveRemainsInstalledWithoutClaimingLoadSupport()
    {
        var catalog = Catalog();
        var parts = new[]
        {
            Part("housing", "power-source-housing", "housing", "steel",
                enabled: false,
                powerSource: "construct-power-source.mechanical-engine",
                links: ["drive"]),
            Part("drive", "locomotion-unit", "contact", "wood")
        };
        var result = Quote(catalog, parts);
        Assert.All(result.Components, x => Assert.True(x.Installed));
        var drive = Assert.Single(result.PhysicalLoadSupports);
        Assert.False(drive.Operating);
        Assert.Null(drive.MaximumGrossLoadPounds);
    }

    [Fact]
    public void ActiveDriveWithUnauthoredCapacityCannotProveMovement()
    {
        var catalog = Catalog();
        var result = Quote(catalog,
            Part("housing", "power-source-housing", "housing", "wood",
                powerSource: "construct-power-source.mechanical-engine",
                links: ["drive"]),
            Part("drive", "locomotion-unit", "contact", "wood"));
        Assert.True(result.IsAuthorized);
        Assert.Equal("unresolved", result.PhysicalLoad?.Status);
        Assert.Null(Assert.Single(result.PhysicalLoadSupports).MaximumGrossLoadPounds);
    }

    [Fact]
    public void InstalledPlatesIdentifyLocalCoverageWithoutInventingArmor()
    {
        var catalog = Catalog();
        var parts = new[]
        {
            Part("plate-a", "protective-plating", "plating", "steel",
                enabled: false) with { ProtectedRegionId = "shell" },
            Part("plate-b", "protective-plating", "plating", "steel",
                enabled: false) with { ProtectedRegionId = "shell" },
            Part("plate-c", "protective-plating", "plating", "cloth",
                enabled: false) with { ProtectedRegionId = "limb" }
        };
        var result = Quote(catalog, parts);
        Assert.True(result.IsAuthorized);
        var shell = NativePhysicalProtectionEvaluator.Evaluate(
            "shell", result.PhysicalSurfaces);
        var limb = NativePhysicalProtectionEvaluator.Evaluate(
            "limb", result.PhysicalSurfaces);
        var uncovered = NativePhysicalProtectionEvaluator.Evaluate(
            "control-node", result.PhysicalSurfaces);
        Assert.Equal("present", shell.Status);
        Assert.Equal(2, shell.ContributingSurfaces.Count);
        Assert.Equal("present", limb.Status);
        Assert.Equal("none", uncovered.Status);
        Assert.All(result.Components, state =>
            Assert.Empty(state.ActiveOperationalOriginTraitIds!));
    }

    [Fact]
    public void CasingCanNotDeclareProtectiveCoverageWithoutAuthoredCapability()
    {
        var catalog = Catalog();
        var result = Quote(catalog,
            Part("casing", "external-casing", "casing", "steel")
                with { ProtectedRegionId = "shell" });
        Assert.Equal(NativeOriginConstructionStatus.Invalid, result.Status);
        Assert.Contains(result.Issues,
            issue => issue.Code == "construct-protection-region-invalid");
        Assert.Null(Assert.Single(result.PhysicalSurfaces).ProtectedRegionId);
    }

    [Fact]
    public void RepeatedPlatesDoNotAuthorizeAutomaticStallOrStackedDamageReduction()
    {
        var catalog = Catalog();
        var parts = new List<NativeConstructComponentInstance>
        {
            Part("housing", "power-source-housing", "housing", "steel",
                powerSource: "construct-power-source.mechanical-engine",
                links: ["drive", "plate-0", "plate-1", "plate-2",
                        "plate-3", "plate-4", "plate-5"]),
            Part("drive", "locomotion-unit", "contact", "steel")
        };
        for (var i = 0; i < 6; i++)
            parts.Add(Part("plate-" + i, "protective-plating",
                "plating", "steel", enabled: false)
                with { ProtectedRegionId = "outer-shell" });

        var built = Quote(catalog, [.. parts]);
        Assert.True(built.IsAuthorized);
        Assert.Equal("unresolved", built.PhysicalLoad?.Status);
        Assert.Null(built.PhysicalLoad?.VerifiedSupportCapacityPounds);
        Assert.Equal(6, built.PhysicalSurfaces.Count(x =>
            x.ProtectedRegionId == "outer-shell"));
        var coverage = NativePhysicalProtectionEvaluator.Evaluate(
            "outer-shell", built.PhysicalSurfaces);
        Assert.Equal("present", coverage.Status);
        Assert.Equal(6, coverage.ContributingSurfaces.Count);
    }

    [Fact]
    public void CertifiedSingleFrameHasCompleteProductMassButNoMovementWithoutDrive()
    {
        var catalog = Catalog();
        var build = Quote(catalog,
            Part("frame", "structural-frame", "frame", "steel"));
        Assert.True(build.IsAuthorized);
        Assert.Equal(56, build.KnownRetainedMaterialMassPounds);
        Assert.True(build.IsBodyMassComplete);
        Assert.Empty(build.ProductsWithUncertifiedMass!);
    }

    [Fact]
    public void UncertifiedAlchemicalActuatorKeepsWholeBodyMassUnresolved()
    {
        var catalog = Catalog();
        var build = Quote(catalog,
            Part("actuator", "alchemical-actuator", "actuator", "steel",
                enabled: false));
        Assert.True(build.IsAuthorized);
        Assert.Equal(21, build.KnownRetainedMaterialMassPounds);
        Assert.False(build.IsBodyMassComplete);
        Assert.Contains("construct-component.alchemical-actuator",
            build.ProductsWithUncertifiedMass!);
    }

    [Fact]
    public void CertifiedBodyAndDriveStillNeedApprovedCapacityAndInventoryEvidence()
    {
        var catalog = Catalog();
        var build = Quote(catalog,
            Part("housing", "power-source-housing", "housing", "wood",
                powerSource: "construct-power-source.mechanical-engine",
                links: ["drive"]),
            Part("drive", "locomotion-unit", "contact", "wood"));

        Assert.True(build.IsAuthorized);
        Assert.True(build.IsBodyMassComplete);
        Assert.Empty(build.ProductsWithUncertifiedMass!);
        Assert.Equal(45, build.KnownRetainedMaterialMassPounds);
        Assert.Equal("unresolved", build.PhysicalLoad?.Status);
        var support = Assert.Single(build.PhysicalLoadSupports);
        Assert.Equal("drive", support.SourceId);
        Assert.True(support.Operating);
        Assert.Null(support.MaximumGrossLoadPounds);

        // Even a hypothetical complete inventory payload cannot create an
        // unauthored locomotion capacity or an automatic speed rule.
        var bodyMass = build.KnownRetainedMaterialMassPounds!.Value;
        foreach (var payload in new long[] { 0, 250, 300 })
        {
            var assessed = NativePhysicalLoadEvaluator.Evaluate(
                bodyMass, true, payload, build.PhysicalLoadSupports);
            Assert.Equal("unresolved", assessed.Status);
            Assert.Null(assessed.VerifiedSupportCapacityPounds);
        }
        Assert.All(build.Components, part => Assert.True(part.Installed));
    }

}
