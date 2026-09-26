using System.Collections;
using System.Reflection;
using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.IntegrationTests;

public sealed class TravelEnvironmentSourceProjectionIntegrationTests
{
    [Fact]
    public void ReviewedThreeEAndThreeFiveOverlandProfilesPreserveMechanicalDifferences()
    {
        var threeE = Definitions(
            Rule("rule.movement.3e", "Movement", "SRD3"),
            """
            {"name":"Movement","source":"SRD3","body":"Overland Movement. Table: Terrain and Overland Movement. Forced March. Hampered Movement. Downstream current is typically 3 miles per hour. A raft or barge and keelboat can float an additional 14 hours if guided."}
            """);
        var threeFive = Definitions(
            Rule("rule.overland-movement.3-5e", "Overland Movement", "SRD35"),
            """
            {"name":"Overland Movement","source":"SRD35","body":"Table: Terrain and Overland Movement. Forced March. Hampered Movement. If going downstream, add the speed of the current. Rafts, barges, keelboats, and rowboats can float an additional 14 hours, if someone can guide it."}
            """);

        Assert.Contains(threeE, value => value.MechanicKey == "travel.overland.walk-distance");
        Assert.Contains(threeFive, value => value.MechanicKey == "travel.overland.walk-distance");
        Assert.Contains(threeE, value => value.MechanicKey == "travel.overland.hustle-distance");
        Assert.Contains(threeFive, value => value.MechanicKey == "travel.overland.hustle-distance");

        var hustle = threeFive.Single(value => value.MechanicKey == "travel.overland.hustle-distance");
        var hustleResult = TravelEnvironmentMechanicEvaluator.Evaluate(
            hustle,
            new TravelEnvironmentResolutionInput(
                IntegerInputs: new Dictionary<string, int> { ["base-speed-feet"] = 30 }));
        Assert.Equal(6m, hustleResult.Quantity?.Value);
        Assert.Equal("miles", hustleResult.Quantity?.Unit);
        Assert.Equal("hour", hustleResult.Quantity?.PerUnit);

        var threeEForcedMarch = threeE.Single(value => value.MechanicKey == "travel.overland.forced-march-check");
        var threeFiveForcedMarch = threeFive.Single(value => value.MechanicKey == "travel.overland.forced-march-check");

        var input = new TravelEnvironmentResolutionInput(
            IntegerInputs: new Dictionary<string, int> { ["extra-hours"] = 1 });
        Assert.Equal(11, TravelEnvironmentMechanicEvaluator.Evaluate(threeEForcedMarch, input).Check?.Dc);
        Assert.Equal(12, TravelEnvironmentMechanicEvaluator.Evaluate(threeFiveForcedMarch, input).Check?.Dc);

        var threeFiveTerrain = threeFive.Single(value => value.MechanicKey == "travel.overland.terrain-distance-factor");
        var mountainRoad = TravelEnvironmentMechanicEvaluator.Evaluate(
            threeFiveTerrain,
            new TravelEnvironmentResolutionInput(
                StringInputs: new Dictionary<string, string>
                {
                    ["terrain"] = "mountains",
                    ["route"] = "road-or-trail"
                }));
        Assert.Equal(.75m, mountainRoad.Factor);

        var threeEHampered = threeE.Single(value => value.MechanicKey == "travel.environment.hampered-movement");
        var threeFiveHampered = threeFive.Single(value => value.MechanicKey == "travel.environment.hampered-movement");
        Assert.Equal(TravelEnvironmentMechanicKinds.DistanceFactor, threeEHampered.Kind);
        Assert.Equal("distance-multiplier", threeEHampered.FactorSemantic);
        Assert.Equal(TravelEnvironmentMechanicKinds.MovementCostFactor, threeFiveHampered.Kind);
        Assert.Equal("movement-cost-multiplier", threeFiveHampered.FactorSemantic);

        var threeEHamperedResult = TravelEnvironmentMechanicEvaluator.Evaluate(
            threeEHampered,
            new TravelEnvironmentResolutionInput(
                BooleanInputs: new Dictionary<string, bool>
                {
                    ["poor-visibility"] = true
                },
                StringInputs: new Dictionary<string, string>
                {
                    ["obstruction"] = "moderate",
                    ["surface"] = "bad"
                }));
        Assert.Equal(.1875m, threeEHamperedResult.Factor);

        var threeFiveHamperedResult = TravelEnvironmentMechanicEvaluator.Evaluate(
            threeFiveHampered,
            new TravelEnvironmentResolutionInput(
                BooleanInputs: new Dictionary<string, bool>
                {
                    ["difficult-terrain"] = true,
                    ["obstacle"] = true,
                    ["poor-visibility"] = false
                }));
        Assert.Equal(4m, threeFiveHamperedResult.Factor);

        var threeECurrent = threeE.Single(value => value.MechanicKey == "travel.water.downstream-current-speed-bonus");
        var threeFiveCurrent = threeFive.Single(value => value.MechanicKey == "travel.water.downstream-current-speed-bonus");
        Assert.Equal(3m, TravelEnvironmentMechanicEvaluator.Evaluate(threeECurrent).Quantity?.Value);
        Assert.Equal(3m, TravelEnvironmentMechanicEvaluator.Evaluate(threeFiveCurrent).Quantity?.Value);
        Assert.Equal("miles", TravelEnvironmentMechanicEvaluator.Evaluate(threeFiveCurrent).Quantity?.Unit);
        Assert.Equal("hour", TravelEnvironmentMechanicEvaluator.Evaluate(threeFiveCurrent).Quantity?.PerUnit);

        var threeEFloat = threeE.Single(value => value.MechanicKey == "travel.water.guided-downstream-float-duration");
        var threeFiveFloat = threeFive.Single(value => value.MechanicKey == "travel.water.guided-downstream-float-duration");
        var threeEModes = threeEFloat.Inputs.Single(value => value.Key == "travel-mode").AllowedValues!;
        var threeFiveModes = threeFiveFloat.Inputs.Single(value => value.Key == "travel-mode").AllowedValues!;
        Assert.DoesNotContain("rowboat", threeEModes);
        Assert.Contains("rowboat", threeFiveModes);

        var guidedFloatResult = TravelEnvironmentMechanicEvaluator.Evaluate(
            threeFiveFloat,
            new TravelEnvironmentResolutionInput(
                BooleanInputs: new Dictionary<string, bool>
                {
                    ["guided"] = true,
                    ["traveling-downstream"] = true
                },
                StringInputs: new Dictionary<string, string>
                {
                    ["travel-mode"] = "rowboat"
                }));
        Assert.Equal(14m, guidedFloatResult.Quantity?.Value);
        Assert.Equal("hours", guidedFloatResult.Quantity?.Unit);
        Assert.Equal("day", guidedFloatResult.Quantity?.PerUnit);

        var unguidedFloatResult = TravelEnvironmentMechanicEvaluator.Evaluate(
            threeFiveFloat,
            new TravelEnvironmentResolutionInput(
                BooleanInputs: new Dictionary<string, bool>
                {
                    ["guided"] = false,
                    ["traveling-downstream"] = true
                },
                StringInputs: new Dictionary<string, string>
                {
                    ["travel-mode"] = "rowboat"
                }));
        Assert.Equal(TravelEnvironmentEvaluationStates.NotApplicable, unguidedFloatResult.State);
    }

    [Fact]
    public void ReviewedThreeFiveNavigationProjectsLostStateChecksWithoutOwningLostState()
    {
        var definitions = Definitions(
            Rule("rule.wilderness.3-5e", "WILDERNESS, WEATHER, & ENVIRONMENT", "SRD35"),
            """
            {"name":"WILDERNESS, WEATHER, & ENVIRONMENT","source":"SRD35","body":"### Getting Lost. Chance to Get Lost. Survival DC. Recognizing that You’re Lost. Setting a New Course."}
            """);

        Assert.Equal(3, definitions.Count);

        var navigation = definitions.Single(value => value.MechanicKey == "travel.navigation.avoid-getting-lost");
        var navigationResult = TravelEnvironmentMechanicEvaluator.Evaluate(
            navigation,
            new TravelEnvironmentResolutionInput(
                StringListInputs: new Dictionary<string, IReadOnlyList<string>>
                {
                    ["risk-factors"] = ["mountain-with-map", "poor-visibility", "forest"]
                }));

        Assert.Equal(15, navigationResult.Check?.Dc);
        Assert.Equal("skill.survival", navigationResult.Check?.CompetencyConceptKey);
        Assert.Equal("once-per-hour-or-portion", navigationResult.Check?.Cadence);

        var recognize = definitions.Single(value => value.MechanicKey == "travel.navigation.recognize-lost");
        var missingRecognitionInput = TravelEnvironmentMechanicEvaluator.Evaluate(recognize);
        Assert.Equal(TravelEnvironmentEvaluationStates.InputRequired, missingRecognitionInput.State);
        Assert.Equal(["random-travel-hours"], missingRecognitionInput.MissingInputKeys);

        var recognitionResult = TravelEnvironmentMechanicEvaluator.Evaluate(
            recognize,
            new TravelEnvironmentResolutionInput(
                IntegerInputs: new Dictionary<string, int> { ["random-travel-hours"] = 3 }));
        Assert.Equal(17, recognitionResult.Check?.Dc);
        Assert.Equal("skill.survival", recognitionResult.Check?.CompetencyConceptKey);
        Assert.Equal("once-per-hour-of-random-travel", recognitionResult.Check?.Cadence);
        Assert.Equal("remain-unaware-lost", recognitionResult.Check?.FailureConsequenceKey);

        var setCourse = definitions.Single(value => value.MechanicKey == "travel.navigation.set-new-course");
        var setCourseResult = TravelEnvironmentMechanicEvaluator.Evaluate(
            setCourse,
            new TravelEnvironmentResolutionInput(
                IntegerInputs: new Dictionary<string, int> { ["random-travel-hours"] = 3 }));
        Assert.Equal(21, setCourseResult.Check?.Dc);
        Assert.Equal("skill.survival", setCourseResult.Check?.CompetencyConceptKey);
        Assert.Equal("choose-random-direction", setCourseResult.Check?.FailureConsequenceKey);
    }

    [Fact]
    public void ReviewedFiveTwoEnvironmentProfilesKeepCostSemanticsDistinct()
    {
        var difficultTerrain = Assert.Single(Definitions(
            Rule("rule.difficult-terrain.5-5e", "Difficult Terrain", "SRD52"),
            """
            {"name":"Difficult Terrain","source":"SRD52","entries":["every foot of movement in that space costs 1 extra foot"]}
            """));
        var altitude = Assert.Single(Definitions(
            Rule("rule.high-altitude.5-5e", "High Altitude", "SRD52"),
            """
            {"name":"High Altitude","source":"SRD52","entries":["Traveling at altitudes of 10,000 feet or higher: each hour counts as 2 hours."]}
            """));

        Assert.Equal("movement-cost-multiplier", difficultTerrain.FactorSemantic);
        Assert.Equal(
            2m,
            TravelEnvironmentMechanicEvaluator.Evaluate(difficultTerrain).Factor);

        var altitudeResult = TravelEnvironmentMechanicEvaluator.Evaluate(
            altitude,
            new TravelEnvironmentResolutionInput(
                IntegerInputs: new Dictionary<string, int> { ["elevation-feet"] = 12000 },
                BooleanInputs: new Dictionary<string, bool>
                {
                    ["subject-to-high-altitude-travel-cost"] = true
                }));
        Assert.Equal("travel-time-cost-multiplier", altitude.FactorSemantic);
        Assert.Equal(2m, altitudeResult.Factor);
    }

    private static IReadOnlyList<TravelEnvironmentMechanicDefinition> Definitions(
        ResolvedRuleCatalogItemView rule,
        string json)
    {
        var type = typeof(RulesCore.Infrastructure.Rules.ResolvedRulesCatalogService).Assembly.GetType(
            "RulesCore.Infrastructure.Rules.TravelEnvironmentProfileFactory",
            throwOnError: true)!;
        var method = type.GetMethod(
            "Build",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TravelEnvironmentProfileFactory.Build is unavailable.");
        using var document = JsonDocument.Parse(json);
        var result = method.Invoke(
            null,
            [
                rule,
                document.RootElement.Clone(),
                Array.Empty<CharacterMechanicSourceAttributionView>()
            ]) as IEnumerable
            ?? throw new InvalidOperationException("Travel source projection returned no enumerable result.");

        var definitions = new List<TravelEnvironmentMechanicDefinition>();
        foreach (var candidate in result)
        {
            var definition = candidate?.GetType().GetProperty("Definition")?.GetValue(candidate)
                as TravelEnvironmentMechanicDefinition;
            Assert.NotNull(definition);
            definitions.Add(definition!);
        }
        return definitions;
    }

    private static ResolvedRuleCatalogItemView Rule(
        string conceptKey,
        string sourceEntityName,
        string sourceCode) =>
        new(
            Guid.NewGuid(),
            conceptKey,
            "rule",
            sourceEntityName,
            "select-source",
            false,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            sourceEntityName,
            sourceCode,
            "wotc-srd-test",
            "Reviewed SRD",
            "dnd",
            "D&D",
            [],
            []);
}
