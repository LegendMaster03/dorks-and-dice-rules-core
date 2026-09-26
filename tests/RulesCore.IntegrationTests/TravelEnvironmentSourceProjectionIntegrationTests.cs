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
            {"name":"Movement","source":"SRD3","body":"Overland Movement. Table: Terrain and Overland Movement. Forced March."}
            """);
        var threeFive = Definitions(
            Rule("rule.overland-movement.3-5e", "Overland Movement", "SRD35"),
            """
            {"name":"Overland Movement","source":"SRD35","body":"Table: Terrain and Overland Movement. Forced March."}
            """);

        Assert.Contains(threeE, value => value.MechanicKey == "travel.overland.walk-distance");
        Assert.Contains(threeFive, value => value.MechanicKey == "travel.overland.walk-distance");

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
    }

    [Fact]
    public void ReviewedThreeFiveNavigationProjectsSurvivalDcAndCadence()
    {
        var definitions = Definitions(
            Rule("rule.wilderness.3-5e", "WILDERNESS, WEATHER, & ENVIRONMENT", "SRD35"),
            """
            {"name":"WILDERNESS, WEATHER, & ENVIRONMENT","source":"SRD35","body":"### Getting Lost. Chance to Get Lost. Survival DC."}
            """);
        var navigation = Assert.Single(definitions);

        var result = TravelEnvironmentMechanicEvaluator.Evaluate(
            navigation,
            new TravelEnvironmentResolutionInput(
                StringListInputs: new Dictionary<string, IReadOnlyList<string>>
                {
                    ["risk-factors"] = ["mountain-with-map", "poor-visibility", "forest"]
                }));

        Assert.Equal("travel.navigation.avoid-getting-lost", navigation.MechanicKey);
        Assert.Equal(15, result.Check?.Dc);
        Assert.Equal("skill.survival", result.Check?.CompetencyConceptKey);
        Assert.Equal("once-per-hour-or-portion", result.Check?.Cadence);
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
        var type = typeof(Program).Assembly.GetType(
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
