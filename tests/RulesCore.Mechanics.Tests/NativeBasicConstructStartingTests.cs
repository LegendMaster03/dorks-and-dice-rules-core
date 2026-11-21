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
    public void FuelDeprivationUsesConstitutionAndDoesNotInstantlyRemovePower()
    {
        var (catalog, assembly) = Reference();
        var built = Construct(catalog, assembly);
        Assert.True(built.IsMechanicallyAuthorized,
            string.Join("; ", built.Origin.Issues.Select(x => x.Code)));
        var session = Assert.IsType<NativeConstructOperatingSession>(
            built.OperatingSession);
        // Constitution +1 gives four permitted consecutive days without
        // a daily ration, followed by one Exhaustion level per further day.
        var fourthDay = session.PreviewDailyFuel("generator", 1, 3, false);
        Assert.Empty(fourthDay.Issues);
        Assert.Equal(0, fourthDay.ProposedFuelChargesUsed);
        Assert.Equal(4, fourthDay.Deprivation!.ConsecutiveDaysWithoutFullRation);
        Assert.Equal(0, fourthDay.Deprivation.ExhaustionLevelsGained);

        var fifthDay = session.PreviewDailyFuel("generator", 1, 4, false);
        Assert.Equal("condition.exhaustion",
            fifthDay.Deprivation!.AppliedConditionId);
        Assert.Equal(1, fifthDay.Deprivation.ExhaustionLevelsGained);

        var supplied = session.PreviewDailyFuel("generator", 1, 5, true);
        Assert.Empty(supplied.Issues);
        Assert.Equal(1, supplied.ProposedFuelChargesUsed);
        Assert.Equal(0, supplied.Deprivation!.ConsecutiveDaysWithoutFullRation);
        Assert.Equal(0, supplied.Deprivation.ExhaustionLevelsGained);

        // Deprivation reports a condition consequence; unlike finite energy
        // exhaustion it does not suppress the source's immediate output.
        Assert.Equal(8, Assert.Single(session.EvaluateConfiguration()
            .Assembly.PowerNetworks!).AvailableOutput);
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
    }
}
