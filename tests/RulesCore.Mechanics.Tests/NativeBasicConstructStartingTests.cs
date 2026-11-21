using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// One first-party beginner assembly exercises ordinary simultaneous functions,
/// recipe-derived points, and 2014 starvation-equivalent daily engine fuel.
/// The candidate physical and cost numbers remain playtest values.
/// </summary>
public sealed class NativeBasicConstructStartingTests
{
    private static string DataRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "rulesets",
                "first-party-001", "native", "rules",
                "creature-origination.json")))
                return candidate;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("First-party native data missing.");
    }

    private static (NativeCatalogSnapshot Catalog, NativeConstructAssemblyRequest Assembly)
        Reference()
    {
        var root = DataRoot();
        var catalog = NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(
            Path.Combine(root, "rulesets"), "first-party-001"));
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            root, "reference", "construct-basic-starting-build-20261120.json")));
        var assembly = JsonSerializer.Deserialize<NativeConstructAssemblyRequest>(
            data.RootElement.GetProperty("assembly").GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return (catalog, assembly ?? throw new InvalidOperationException(
            "Invalid first-party reference assembly."));
    }

    private static NativeConstructOriginConstructionResult Construct(
        NativeCatalogSnapshot catalog, NativeConstructAssemblyRequest assembly)
    {
        var service = new NativeConstructOriginConstructionService(catalog);
        var proposal = new NativeOriginProposal(
            "basic-construct-playtest", NativeOriginIntent.Custom,
            new("rule.creature-origination", "construct-assembly"),
            [], [], []);
        var draft = service.Evaluate(new(new(proposal), assembly));
        Assert.True(draft.Assembly.IsAuthorized,
            string.Join("; ", draft.Assembly.Issues.Select(x => x.Code)));
        var manifests = draft.Origin.EvaluatedProposal.Candidates
            .Select((candidate, i) => new NativeOriginManifestation(
                "basic-selection-" + i, candidate.Id,
                SelectedValue: candidate.Identity.Kind ==
                    NativeOriginCharacteristicKind.Intrinsic
                    ? candidate.OfferedValue : null))
            .ToArray();
        return service.Evaluate(new(new(proposal with
        {
            Manifestations = manifests
        }), assembly));
    }

    [Fact]
    public void ReferenceBodyFitsTwoHundredPointsAndRunsAllOrdinaryEquipment()
    {
        var (catalog, assembly) = Reference();
        Assert.Equal(200, assembly.InitialAllowance);
        var assessed = new NativeConstructAssemblyEvaluator(catalog)
            .Evaluate(assembly);
        Assert.True(assessed.IsAuthorized,
            string.Join("; ", assessed.Issues.Select(x => x.Code)));
        Assert.Equal(163, assessed.KnownConstructionCost);
        Assert.Equal("medium", assessed.DerivedCreatureSize);
        var network = Assert.Single(assessed.PowerNetworks!);
        Assert.Equal(8, network.AvailableOutput);
        Assert.Equal(7, network.RequestedDemand);
        Assert.False(network.IsOverallocated);
        Assert.All(assessed.Components, x => Assert.True(x.Operational));
        foreach (var (instance, trait) in new[]
        {
            ("control", "trait.construct.ordinary-communication"),
            ("drive", "trait.construct.ordinary-ground-movement"),
            ("eye", "trait.construct.ordinary-optical-sight"),
            ("manipulator", "trait.construct.physical-manipulation")
        })
            Assert.Contains(trait, assessed.Components.Single(x =>
                x.InstanceId == instance).ActiveOperationalOriginTraitIds!);
    }

    [Fact]
    public void StartingBuildManifestationsUseSharedOriginEngine()
    {
        var (catalog, assembly) = Reference();
        var built = Construct(catalog, assembly);
        Assert.True(built.IsMechanicallyAuthorized,
            string.Join("; ", built.Origin.Issues.Select(x => x.Code)));
        Assert.Equal("medium", built.Origin.Profile!.Underlying.InherentGrants
            .Single(x => x.Identity.IntrinsicSlotId == "size")
            .EffectiveValue!.Value.GetString());
        foreach (var id in new[]
        {
            "trait.construct.ordinary-communication",
            "trait.construct.ordinary-ground-movement",
            "trait.construct.ordinary-optical-sight",
            "trait.construct.physical-manipulation"
        })
            Assert.Contains(built.Origin.Profile.Current.InherentGrants,
                x => x.Identity.MechanicalDefinitionId == id);
    }

    [Fact]
    public void BasicEngineSubstitutesFuelForFoodWithoutNewSurvivalRules()
    {
        var (catalog, assembly) = Reference();
        var built = Construct(catalog, assembly);
        Assert.True(built.IsMechanicallyAuthorized,
            string.Join("; ", built.Origin.Issues.Select(x => x.Code)));
        Assert.Contains(assembly.Components, x =>
            x.InstanceId == "generator" &&
            x.PowerSourceId == "construct-power-source.mechanical-engine");

        Assert.True(catalog.TryGetById("construct-power-source.mechanical-engine",
            out var engine));
        Assert.NotNull(engine);
        var daily = engine!.Definition.GetProperty("dailySustenance");
        var fuel = new NativeDailyRationRequirement(
            daily.GetProperty("resourceKind").GetString()!,
            daily.GetProperty("unitsPerDay").GetInt32());
        var food = new NativeDailyRationRequirement("ordinary-food-ration", 1);
        Assert.Equal("mechanical-engine-fuel-charge", fuel.ResourceKind);
        Assert.Equal(1, fuel.UnitsPerDay);

        // Both characters pass through the same general survival evaluator.
        // With Constitution +1, four days can pass without nourishment.
        var foodFourthDay = NativeDailyRationEvaluator.AssessDay(food, 0, 1, 3);
        var fuelFourthDay = NativeDailyRationEvaluator.AssessDay(fuel, 0, 1, 3);
        Assert.Equal(foodFourthDay, fuelFourthDay);
        Assert.Equal(0, fuelFourthDay.ExhaustionLevelsGained);

        var foodFifthDay = NativeDailyRationEvaluator.AssessDay(food, 0, 1, 4);
        var fuelFifthDay = NativeDailyRationEvaluator.AssessDay(fuel, 0, 1, 4);
        Assert.Equal(foodFifthDay, fuelFifthDay);
        Assert.Equal("condition.exhaustion", fuelFifthDay.AppliedConditionId);
        Assert.Equal(1, fuelFifthDay.ExhaustionLevelsGained);

        var fed = NativeDailyRationEvaluator.AssessDay(food, 1, 1, 5);
        var refueled = NativeDailyRationEvaluator.AssessDay(fuel, 1, 1, 5);
        Assert.Equal(fed, refueled);
        Assert.Equal(0, refueled.ConsecutiveDaysWithoutFullRation);
        Assert.Equal(0, refueled.ExhaustionLevelsGained);

        // Missing a day's ration does not independently shut down the engine.
        Assert.Equal(8, Assert.Single(built.OperatingSession!
            .EvaluateConfiguration().Assembly.PowerNetworks!).AvailableOutput);
    }

    [Fact]
    public void DailyRationEvaluationIsSharedAndDoesNotAddExtraRestCosts()
    {
        var minimum = NativeDailyRationEvaluator.AssessDay(-5, 0, false);
        Assert.Equal(1, minimum.DaysAllowedWithoutRation);
        Assert.Equal(0, minimum.ExhaustionLevelsGained);
        Assert.Equal(1, NativeDailyRationEvaluator.AssessDay(-5, 1, false)
            .ExhaustionLevelsGained);
        Assert.Equal(1, NativeDailyRationEvaluator.AssessDay(0, 3, false)
            .ExhaustionLevelsGained);
        Assert.Equal(0, NativeDailyRationEvaluator.AssessDay(0, 5, true)
            .ConsecutiveDaysWithoutFullRation);
        Assert.Throws<ArgumentException>(() =>
            NativeDailyRationEvaluator.AssessDay(
                new NativeDailyRationRequirement("", 1), 1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NativeDailyRationEvaluator.AssessDay(
                new NativeDailyRationRequirement("fuel", 1), -1, 0, 0));
    }
}
