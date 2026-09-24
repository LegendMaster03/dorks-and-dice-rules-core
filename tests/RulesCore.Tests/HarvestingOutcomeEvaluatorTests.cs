using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Tests;

public sealed class HarvestingOutcomeEvaluatorTests
{
    [Fact]
    public void CumulativeHarvestDcsFollowTheExplicitPartyOrder()
    {
        var table = Table(
            "Medium",
            [
                Component("teeth", "Teeth", 10),
                Component("eye-one", "Eye", 5),
                Component("eye-two", "Eye", 5),
                Component("breath-sac", "Breath sac", 25),
                Component("essence", "Essence", 30)
            ]);

        var result = HarvestingOutcomeEvaluator.Evaluate(
            table,
            Request(
                table,
                assessmentResult: 12,
                carvingResult: 18,
                order: ["teeth", "eye-one", "eye-two", "breath-sac", "essence"]));

        Assert.Equal([10, 15, 20, 45, 75], result.Components.Select(value => value.HarvestDc));
        Assert.Equal(30, result.HarvestingResult);
        Assert.Equal(
            [true, true, true, false, false],
            result.Components.Select(value => value.Awarded));
    }

    [Fact]
    public void HelpersUseFullOrHalfProficiencyAndRespectCreatureSizeLimit()
    {
        var table = Table("Medium", [Component("hide", "Hide", 20)]);
        var result = HarvestingOutcomeEvaluator.Evaluate(
            table,
            Request(
                table,
                assessmentResult: 8,
                carvingResult: 8,
                order: ["hide"],
                helpers:
                [
                    new HarvestingHelperInput(4, true),
                    new HarvestingHelperInput(5, false)
                ]));

        Assert.Equal(2, result.HelperCount);
        Assert.Equal(6, result.HelperContribution);
        Assert.Equal(22, result.HarvestingResult);
        Assert.True(Assert.Single(result.Components).Awarded);

        Assert.Throws<InvalidOperationException>(() =>
            HarvestingOutcomeEvaluator.Evaluate(
                table,
                Request(
                    table,
                    assessmentResult: 8,
                    carvingResult: 8,
                    order: ["hide"],
                    helpers:
                    [
                        new HarvestingHelperInput(2, true),
                        new HarvestingHelperInput(2, true),
                        new HarvestingHelperInput(2, true)
                    ])));
    }

    [Fact]
    public void SameActorChangesBothCheckModesWithoutRecalculatingEnteredResults()
    {
        var table = Table("Small", [Component("bone", "Bone", 10)]);
        var result = HarvestingOutcomeEvaluator.Evaluate(
            table,
            Request(
                table,
                assessmentResult: 11,
                carvingResult: 12,
                sameActor: true,
                order: ["bone"]));

        Assert.Equal(CharacterMechanicRollModes.Disadvantage, result.AssessmentRollMode);
        Assert.Equal(CharacterMechanicRollModes.Disadvantage, result.CarvingRollMode);
        Assert.Equal(23, result.HarvestingResult);
    }

    [Fact]
    public void TypeOnlyResolutionRequiresSizeOnlyWhenHelpersArePresent()
    {
        var table = Table(null, [Component("sap", "Sap", 5)]);

        var noHelpers = HarvestingOutcomeEvaluator.Evaluate(
            table,
            Request(table, 5, 5, ["sap"]));
        Assert.Null(noHelpers.CreatureSize);

        Assert.Throws<ArgumentException>(() =>
            HarvestingOutcomeEvaluator.Evaluate(
                table,
                Request(
                    table,
                    5,
                    5,
                    ["sap"],
                    helpers: [new HarvestingHelperInput(2, true)])));

        var withSize = HarvestingOutcomeEvaluator.Evaluate(
            table,
            Request(
                table,
                5,
                5,
                ["sap"],
                creatureSize: "M",
                helpers: [new HarvestingHelperInput(2, true)]));
        Assert.Equal("Medium", withSize.CreatureSize);
    }

    [Fact]
    public void HarvestOrderRejectsUnknownOrDuplicateComponents()
    {
        var table = Table("Medium", [Component("eye", "Eye", 5)]);

        Assert.Throws<KeyNotFoundException>(() =>
            HarvestingOutcomeEvaluator.Evaluate(
                table,
                Request(table, 5, 5, ["missing"])));

        Assert.Throws<ArgumentException>(() =>
            HarvestingOutcomeEvaluator.Evaluate(
                table,
                Request(table, 5, 5, ["eye", "eye"])));
    }

    [Fact]
    public void HelpersMustBeEligibleHarvestingHelpers()
    {
        var table = Table("Medium", [Component("eye", "Eye", 5)]);

        Assert.Throws<InvalidOperationException>(() =>
            HarvestingOutcomeEvaluator.Evaluate(
                table,
                Request(
                    table,
                    5,
                    5,
                    ["eye"],
                    helpers:
                    [
                        new HarvestingHelperInput(
                            2,
                            true,
                            ParticipatedForEntireDuration: false)
                    ])));

        Assert.Throws<InvalidOperationException>(() =>
            HarvestingOutcomeEvaluator.Evaluate(
                table,
                Request(
                    table,
                    5,
                    5,
                    ["eye"],
                    helpers:
                    [
                        new HarvestingHelperInput(
                            2,
                            true,
                            IsAssessmentParticipant: true)
                    ])));
    }

    private static HarvestingOutcomeRequest Request(
        HarvestingResolvedTableView table,
        int assessmentResult,
        int carvingResult,
        IReadOnlyList<string> order,
        bool sameActor = false,
        string? creatureSize = null,
        IReadOnlyList<HarvestingHelperInput>? helpers = null) =>
        new(
            new HarvestingTableResolutionRequest(CreatureType: table.CreatureType),
            assessmentResult,
            carvingResult,
            sameActor,
            order,
            creatureSize,
            helpers);

    private static HarvestingResolvedTableView Table(
        string? size,
        IReadOnlyList<HarvestingComponentView> components) =>
        new(
            new HarvestingSourceView(
                KnownHarvestingRules.WorkKey,
                KnownHarvestingRules.WorkDisplayName,
                KnownHarvestingRules.Provider,
                KnownHarvestingRules.GameEdition,
                KnownHarvestingRules.ReleaseKind,
                KnownHarvestingRules.PublicationDate,
                KnownHarvestingRules.ReferenceUri),
            "dragon",
            "Dragon",
            "competency.survival",
            "Survival",
            components,
            CreatureSize: size);

    private static HarvestingComponentView Component(
        string key,
        string name,
        int dc) =>
        new(key, name, dc);
}
