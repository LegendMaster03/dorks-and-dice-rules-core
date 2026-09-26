using RulesCore.Domain.Rules;

namespace RulesCore.Tests;

public sealed class TravelEnvironmentMechanicsTests
{
    [Fact]
    public void LookupQuantityRequiresInputsAndResolvesExactRate()
    {
        var definition = new TravelEnvironmentMechanicDefinition(
            "travel.test.rate",
            TravelEnvironmentMechanicKinds.DistanceRate,
            "Test rate",
            TravelEnvironmentResolutionKinds.LookupQuantity,
            [
                new("speed", TravelEnvironmentInputValueKinds.Integer, true),
                new("period", TravelEnvironmentInputValueKinds.String, true, ["hour", "day"])
            ],
            QuantityRows:
            [
                new(
                    [
                        new("speed", "30"),
                        new("period", "hour")
                    ],
                    new(3m, "miles", "hour"))
            ]);

        var missing = TravelEnvironmentMechanicEvaluator.Evaluate(definition);
        Assert.Equal(TravelEnvironmentEvaluationStates.InputRequired, missing.State);
        Assert.Equal(["period", "speed"], missing.MissingInputKeys);

        var resolved = TravelEnvironmentMechanicEvaluator.Evaluate(
            definition,
            new TravelEnvironmentResolutionInput(
                IntegerInputs: new Dictionary<string, int> { ["speed"] = 30 },
                StringInputs: new Dictionary<string, string> { ["period"] = "hour" }));

        Assert.Equal(TravelEnvironmentEvaluationStates.Resolved, resolved.State);
        Assert.Equal(3m, resolved.Quantity?.Value);
        Assert.Equal("miles", resolved.Quantity?.Unit);
        Assert.Equal("hour", resolved.Quantity?.PerUnit);
    }

    [Fact]
    public void LinearCheckUsesSourceDefinedProgression()
    {
        var definition = new TravelEnvironmentMechanicDefinition(
            "travel.test.forced-march",
            TravelEnvironmentMechanicKinds.CheckDc,
            "Forced march",
            TravelEnvironmentResolutionKinds.LinearCheckDc,
            [new("extra-hours", TravelEnvironmentInputValueKinds.Integer, true)],
            LinearCheck: new(
                10,
                "extra-hours",
                2,
                1,
                "constitution",
                null,
                "each-hour",
                "damage"));

        var resolved = TravelEnvironmentMechanicEvaluator.Evaluate(
            definition,
            new TravelEnvironmentResolutionInput(
                IntegerInputs: new Dictionary<string, int> { ["extra-hours"] = 3 }));

        Assert.Equal(TravelEnvironmentEvaluationStates.Resolved, resolved.State);
        Assert.Equal(16, resolved.Check?.Dc);
        Assert.Equal("constitution", resolved.Check?.AbilityKey);
    }

    [Fact]
    public void MaximumApplicableCheckUsesHighestSuppliedOption()
    {
        var definition = new TravelEnvironmentMechanicDefinition(
            "travel.test.navigation",
            TravelEnvironmentMechanicKinds.CheckDc,
            "Navigation",
            TravelEnvironmentResolutionKinds.MaximumApplicableCheckDc,
            [new(
                "risk-factors",
                TravelEnvironmentInputValueKinds.StringList,
                true,
                ["mountain-with-map", "poor-visibility", "forest"])],
            MaximumCheck: new(
                "risk-factors",
                [
                    new("mountain-with-map", 8),
                    new("poor-visibility", 12),
                    new("forest", 15)
                ],
                null,
                "skill.survival",
                "per-hour",
                "become-lost"));

        var resolved = TravelEnvironmentMechanicEvaluator.Evaluate(
            definition,
            new TravelEnvironmentResolutionInput(
                StringListInputs: new Dictionary<string, IReadOnlyList<string>>
                {
                    ["risk-factors"] = ["mountain-with-map", "poor-visibility"]
                }));

        Assert.Equal(12, resolved.Check?.Dc);
        Assert.Equal("skill.survival", resolved.Check?.CompetencyConceptKey);
    }

    [Fact]
    public void ThresholdFactorDoesNotGuessApplicability()
    {
        var definition = new TravelEnvironmentMechanicDefinition(
            "travel.test.altitude",
            TravelEnvironmentMechanicKinds.TravelTimeCostFactor,
            "Altitude",
            TravelEnvironmentResolutionKinds.ThresholdFactor,
            [
                new("elevation-feet", TravelEnvironmentInputValueKinds.Integer, true),
                new("subject-to-effect", TravelEnvironmentInputValueKinds.Boolean, true)
            ],
            ThresholdFactor: new("elevation-feet", 10000, "subject-to-effect", 2m));

        var missing = TravelEnvironmentMechanicEvaluator.Evaluate(
            definition,
            new TravelEnvironmentResolutionInput(
                IntegerInputs: new Dictionary<string, int> { ["elevation-feet"] = 12000 }));

        Assert.Equal(TravelEnvironmentEvaluationStates.InputRequired, missing.State);
        Assert.Equal(["subject-to-effect"], missing.MissingInputKeys);

        var exempt = TravelEnvironmentMechanicEvaluator.Evaluate(
            definition,
            new TravelEnvironmentResolutionInput(
                IntegerInputs: new Dictionary<string, int> { ["elevation-feet"] = 12000 },
                BooleanInputs: new Dictionary<string, bool> { ["subject-to-effect"] = false }));

        Assert.Equal(TravelEnvironmentEvaluationStates.NotApplicable, exempt.State);
    }
}
