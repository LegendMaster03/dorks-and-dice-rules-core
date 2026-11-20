using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeCraftingProcessTests
{
    private static NativeCraftingProcessStep Step(
        string name, string competency, int required, int operatorRank,
        IReadOnlyList<string>? after = null, bool completed = false,
        bool? hasInputs = true, bool? hasFacilities = true) =>
        new(name, competency, required, operatorRank, after ?? [],
            [new("source:ore", 2)], [new("source:bar", 1)],
            true, hasInputs, hasFacilities, completed);

    [Fact]
    public void StagesCanUseDifferentCompetenciesWithoutDuplicateProgression()
    {
        var plan = new[] {
            Step("smelt", "competency.smithing", 2, 3),
            Step("imbue", "competency.alchemy", 3, 3, ["smelt"]),
            Step("forge", "competency.smithing", 4, 4, ["imbue"])
        };
        var result = NativeCraftingProcess.Evaluate(plan);
        Assert.Equal("ready", result.Status);
        Assert.Equal("ready", result.Steps[0].Status);
        Assert.Equal("blocked", result.Steps[1].Status);
        Assert.Equal("blocked", result.Steps[2].Status);

        var progressed = NativeCraftingProcess.Evaluate([
            plan[0] with { Completed = true },
            plan[1],
            plan[2]
        ]);
        Assert.Equal("ready", progressed.Steps[1].Status);
        Assert.Equal("blocked", progressed.Steps[2].Status);
    }

    [Fact]
    public void MissingMaterialsOrFacilitiesBlockAnIndividualStage()
    {
        var result = NativeCraftingProcess.Evaluate([
            Step("refine", "competency.smithing", 2, 2, hasInputs: false),
            Step("assemble", "competency.tinkering", 1, 2, hasFacilities: false)
        ]);
        Assert.Equal("blocked", result.Status);
        Assert.Contains("materials-missing", result.Steps[0].Reasons);
        Assert.Contains("facilities-missing", result.Steps[1].Reasons);
    }

    [Fact]
    public void UnknownFactsDoNotMakeStageReady()
    {
        var result = NativeCraftingProcess.Evaluate([
            Step("refine", "competency.smithing", 2, 2, hasInputs: null)
        ]);
        Assert.Equal("unresolved", result.Status);
        Assert.Equal("unresolved", result.Steps[0].Status);
    }

    [Fact]
    public void AStageNeedsTheRequiredCompetencyRank()
    {
        var result = NativeCraftingProcess.Evaluate([
            Step("refine", "competency.smithing", 3, 0)
        ]);
        Assert.Equal("blocked", result.Status);
        Assert.Contains("performer-rank-too-low", result.Steps[0].Reasons);
    }

    [Fact]
    public void CyclesAndUnknownStageDependenciesAreRejected()
    {
        Assert.Equal("invalid", NativeCraftingProcess.Evaluate([
            Step("one", "competency.smithing", 1, 1, ["two"]),
            Step("two", "competency.tinkering", 1, 1, ["one"])
        ]).Status);
        Assert.Equal("invalid", NativeCraftingProcess.Evaluate([
            Step("one", "competency.smithing", 1, 1, ["missing"])
        ]).Status);
    }

    [Fact]
    public void CompletedStageCannotDependOnUnfinishedStage()
    {
        var result = NativeCraftingProcess.Evaluate([
            Step("ore", "competency.smithing", 1, 1),
            Step("alloy", "competency.alchemy", 2, 2, ["ore"], completed: true)
        ]);
        Assert.Equal("invalid", result.Status);
        Assert.Contains("completed-stage-has-unfinished-dependency", result.Issues);
        Assert.Empty(result.Steps);
    }

    [Fact]
    public void InvalidResourceQuantitiesAndDuplicateMaterialsAreRejected()
    {
        var example = Step("one", "competency.smithing", 1, 1);
        Assert.Equal("invalid", NativeCraftingProcess.Evaluate([
            example with { Inputs = [new("source:ore", 0)] }
        ]).Status);
        Assert.Equal("invalid", NativeCraftingProcess.Evaluate([
            example with { Inputs = [new("source:ore", 1), new("source:ore", 2)] }
        ]).Status);
    }

    [Fact]
    public void CompletedStagesDoNotGrantMaterialsOrModifyInventory()
    {
        var result = NativeCraftingProcess.Evaluate([
            Step("smelt", "competency.smithing", 1, 1, completed: true)
        ]);
        Assert.Equal("completed", result.Status);
        Assert.Equal("completed", result.Steps[0].Status);
        // Results intentionally contain no changed inventory or output grants.
        Assert.Empty(result.Issues);
    }
}
