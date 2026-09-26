using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules;

internal sealed record TravelEnvironmentMechanicCandidate(
    TravelEnvironmentMechanicDefinition Definition,
    ResolvedRuleCatalogItemView Rule,
    IReadOnlyList<CharacterMechanicSourceAttributionView> SourceAttributions);

/// <summary>
/// Projects reviewed source-native travel/environment rules into structured mechanics.
/// This is the only place that knows the exact reviewed SRD source shapes. Consumers receive
/// typed definitions and never parse SRD prose, tables, or edition-specific document formats.
/// </summary>
internal static class TravelEnvironmentProfileFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static IReadOnlyList<TravelEnvironmentMechanicCandidate> Build(
        ResolvedRuleCatalogItemView rule,
        JsonElement document,
        IReadOnlyList<CharacterMechanicSourceAttributionView> attributions)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(attributions);

        var normalized = ReadNormalizedExtension(document);
        if (normalized.Count > 0)
        {
            return normalized
                .Select(value => new TravelEnvironmentMechanicCandidate(value, rule, attributions))
                .ToArray();
        }

        var definitions = BuildReviewedSourceDefinitions(rule, document);
        return definitions
            .Select(value => new TravelEnvironmentMechanicCandidate(value, rule, attributions))
            .ToArray();
    }

    private static IReadOnlyList<TravelEnvironmentMechanicDefinition> ReadNormalizedExtension(
        JsonElement document)
    {
        if (document.ValueKind != JsonValueKind.Object
            || !document.TryGetProperty("_rulesCore", out var extension)
            || extension.ValueKind != JsonValueKind.Object
            || !extension.TryGetProperty("travel", out var travel)
            || travel.ValueKind != JsonValueKind.Object
            || !travel.TryGetProperty("mechanics", out var mechanics)
            || mechanics.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var values = new List<TravelEnvironmentMechanicDefinition>();
        foreach (var mechanic in mechanics.EnumerateArray())
        {
            var parsed = JsonSerializer.Deserialize<TravelEnvironmentMechanicDefinition>(
                mechanic.GetRawText(),
                JsonOptions);
            if (parsed is not null)
            {
                values.Add(parsed);
            }
        }
        return values;
    }

    private static IReadOnlyList<TravelEnvironmentMechanicDefinition> BuildReviewedSourceDefinitions(
        ResolvedRuleCatalogItemView rule,
        JsonElement document)
    {
        var sourceCode = rule.SourceCode?.Trim();
        var sourceName = rule.SourceEntityName.Trim();

        if (string.Equals(sourceCode, "SRD3", StringComparison.Ordinal)
            && string.Equals(sourceName, "Movement", StringComparison.Ordinal)
            && ContainsBody(document, "Terrain and Overland Movement")
            && ContainsBody(document, "Forced March"))
        {
            return BuildThreeEOverland();
        }

        if (string.Equals(sourceCode, "SRD35", StringComparison.Ordinal)
            && string.Equals(sourceName, "Overland Movement", StringComparison.Ordinal)
            && ContainsBody(document, "Terrain and Overland Movement")
            && ContainsBody(document, "Forced March"))
        {
            return BuildThreeFiveOverland();
        }

        if (string.Equals(sourceCode, "SRD35", StringComparison.Ordinal)
            && string.Equals(sourceName, "WILDERNESS, WEATHER, & ENVIRONMENT", StringComparison.Ordinal)
            && ContainsBody(document, "Chance to Get Lost")
            && ContainsBody(document, "Survival DC")
            && ContainsBody(document, "Recognizing that")
            && ContainsBody(document, "Setting a New Course"))
        {
            return BuildThreeFiveNavigation();
        }

        if (string.Equals(sourceCode, "SRD52", StringComparison.Ordinal)
            && string.Equals(sourceName, "Difficult Terrain", StringComparison.Ordinal)
            && ContainsDocumentText(document, "costs 1 extra foot"))
        {
            return [BuildFiveTwoDifficultTerrain()];
        }

        if (string.Equals(sourceCode, "SRD52", StringComparison.Ordinal)
            && string.Equals(sourceName, "High Altitude", StringComparison.Ordinal)
            && ContainsDocumentText(document, "counts as 2 hours")
            && ContainsDocumentText(document, "10,000 feet"))
        {
            return [BuildFiveTwoHighAltitude()];
        }

        return [];
    }

    private static IReadOnlyList<TravelEnvironmentMechanicDefinition> BuildThreeEOverland() =>
    [
        BuildWalkDistanceTable(),
        BuildHustleDistanceTable(),
        BuildTravelDayDurationTable(),
        new(
            "travel.overland.terrain-distance-factor",
            TravelEnvironmentMechanicKinds.DistanceFactor,
            "Terrain and route distance factor",
            TravelEnvironmentResolutionKinds.LookupFactor,
            [
                StringInput("terrain", true, "plains", "scrub-rough", "forest", "jungle", "swamp", "hills", "mountains", "sandy-desert"),
                StringInput("route", true, "highway", "road", "trackless")
            ],
            FactorRows:
            [
                Factor(1m, ("terrain", "plains"), ("route", "highway")),
                Factor(1m, ("terrain", "plains"), ("route", "road")),
                Factor(1m, ("terrain", "plains"), ("route", "trackless")),
                Factor(1m, ("terrain", "scrub-rough"), ("route", "highway")),
                Factor(1m, ("terrain", "scrub-rough"), ("route", "road")),
                Factor(.75m, ("terrain", "scrub-rough"), ("route", "trackless")),
                Factor(1m, ("terrain", "forest"), ("route", "highway")),
                Factor(1m, ("terrain", "forest"), ("route", "road")),
                Factor(.5m, ("terrain", "forest"), ("route", "trackless")),
                Factor(1m, ("terrain", "jungle"), ("route", "highway")),
                Factor(.75m, ("terrain", "jungle"), ("route", "road")),
                Factor(.25m, ("terrain", "jungle"), ("route", "trackless")),
                Factor(1m, ("terrain", "swamp"), ("route", "highway")),
                Factor(.75m, ("terrain", "swamp"), ("route", "road")),
                Factor(.5m, ("terrain", "swamp"), ("route", "trackless")),
                Factor(1m, ("terrain", "hills"), ("route", "highway")),
                Factor(.75m, ("terrain", "hills"), ("route", "road")),
                Factor(.5m, ("terrain", "hills"), ("route", "trackless")),
                Factor(.75m, ("terrain", "mountains"), ("route", "highway")),
                Factor(.5m, ("terrain", "mountains"), ("route", "road")),
                Factor(.25m, ("terrain", "mountains"), ("route", "trackless")),
                Factor(1m, ("terrain", "sandy-desert"), ("route", "highway")),
                Factor(.5m, ("terrain", "sandy-desert"), ("route", "trackless"))
            ],
            FactorSemantic: "distance-multiplier",
            Scale: "overland"),
        BuildForcedMarch(dcPerExtraHour: 1),
        BuildThreeEMountVehicleRates()
    ];

    private static IReadOnlyList<TravelEnvironmentMechanicDefinition> BuildThreeFiveOverland() =>
    [
        BuildWalkDistanceTable(),
        BuildHustleDistanceTable(),
        BuildTravelDayDurationTable(),
        new(
            "travel.overland.terrain-distance-factor",
            TravelEnvironmentMechanicKinds.DistanceFactor,
            "Terrain and route distance factor",
            TravelEnvironmentResolutionKinds.LookupFactor,
            [
                StringInput("terrain", true, "sandy-desert", "forest", "hills", "jungle", "moor", "mountains", "plains", "swamp", "frozen-tundra"),
                StringInput("route", true, "highway", "road-or-trail", "trackless")
            ],
            FactorRows:
            [
                Factor(1m, ("terrain", "sandy-desert"), ("route", "highway")),
                Factor(.5m, ("terrain", "sandy-desert"), ("route", "road-or-trail")),
                Factor(.5m, ("terrain", "sandy-desert"), ("route", "trackless")),
                Factor(1m, ("terrain", "forest"), ("route", "highway")),
                Factor(1m, ("terrain", "forest"), ("route", "road-or-trail")),
                Factor(.5m, ("terrain", "forest"), ("route", "trackless")),
                Factor(1m, ("terrain", "hills"), ("route", "highway")),
                Factor(.75m, ("terrain", "hills"), ("route", "road-or-trail")),
                Factor(.5m, ("terrain", "hills"), ("route", "trackless")),
                Factor(1m, ("terrain", "jungle"), ("route", "highway")),
                Factor(.75m, ("terrain", "jungle"), ("route", "road-or-trail")),
                Factor(.25m, ("terrain", "jungle"), ("route", "trackless")),
                Factor(1m, ("terrain", "moor"), ("route", "highway")),
                Factor(1m, ("terrain", "moor"), ("route", "road-or-trail")),
                Factor(.75m, ("terrain", "moor"), ("route", "trackless")),
                Factor(.75m, ("terrain", "mountains"), ("route", "highway")),
                Factor(.75m, ("terrain", "mountains"), ("route", "road-or-trail")),
                Factor(.5m, ("terrain", "mountains"), ("route", "trackless")),
                Factor(1m, ("terrain", "plains"), ("route", "highway")),
                Factor(1m, ("terrain", "plains"), ("route", "road-or-trail")),
                Factor(.75m, ("terrain", "plains"), ("route", "trackless")),
                Factor(1m, ("terrain", "swamp"), ("route", "highway")),
                Factor(.75m, ("terrain", "swamp"), ("route", "road-or-trail")),
                Factor(.5m, ("terrain", "swamp"), ("route", "trackless")),
                Factor(1m, ("terrain", "frozen-tundra"), ("route", "highway")),
                Factor(.75m, ("terrain", "frozen-tundra"), ("route", "road-or-trail")),
                Factor(.75m, ("terrain", "frozen-tundra"), ("route", "trackless"))
            ],
            FactorSemantic: "distance-multiplier",
            Scale: "overland"),
        BuildForcedMarch(dcPerExtraHour: 2),
        BuildThreeFiveMountVehicleRates(),
        BuildThreeFiveHamperedMovement(),
        BuildThreeFiveDownstreamCurrentSpeedBonus(),
        BuildThreeFiveGuidedFloatDuration()
    ];

    private static TravelEnvironmentMechanicDefinition BuildWalkDistanceTable() =>
        new(
            "travel.overland.walk-distance",
            TravelEnvironmentMechanicKinds.DistanceRate,
            "Overland walking distance",
            TravelEnvironmentResolutionKinds.LookupQuantity,
            [
                IntegerInput("base-speed-feet", true),
                StringInput("period", true, "hour", "day")
            ],
            QuantityRows:
            [
                Quantity(1.5m, "miles", "hour", ("base-speed-feet", "15"), ("period", "hour")),
                Quantity(2m, "miles", "hour", ("base-speed-feet", "20"), ("period", "hour")),
                Quantity(3m, "miles", "hour", ("base-speed-feet", "30"), ("period", "hour")),
                Quantity(4m, "miles", "hour", ("base-speed-feet", "40"), ("period", "hour")),
                Quantity(12m, "miles", "day", ("base-speed-feet", "15"), ("period", "day")),
                Quantity(16m, "miles", "day", ("base-speed-feet", "20"), ("period", "day")),
                Quantity(24m, "miles", "day", ("base-speed-feet", "30"), ("period", "day")),
                Quantity(32m, "miles", "day", ("base-speed-feet", "40"), ("period", "day"))
            ],
            Scale: "overland");

    private static TravelEnvironmentMechanicDefinition BuildHustleDistanceTable() =>
        new(
            "travel.overland.hustle-distance",
            TravelEnvironmentMechanicKinds.DistanceRate,
            "Overland hustling distance",
            TravelEnvironmentResolutionKinds.LookupQuantity,
            [IntegerInput("base-speed-feet", true)],
            QuantityRows:
            [
                Quantity(3m, "miles", "hour", ("base-speed-feet", "15")),
                Quantity(4m, "miles", "hour", ("base-speed-feet", "20")),
                Quantity(6m, "miles", "hour", ("base-speed-feet", "30")),
                Quantity(8m, "miles", "hour", ("base-speed-feet", "40"))
            ],
            Scale: "overland");

    private static TravelEnvironmentMechanicDefinition BuildTravelDayDurationTable() =>
        new(
            "travel.overland.standard-travel-duration",
            TravelEnvironmentMechanicKinds.Duration,
            "Standard travel-day duration",
            TravelEnvironmentResolutionKinds.LookupQuantity,
            [StringInput("travel-mode", true, "land", "rowed-watercraft", "sailing-ship")],
            QuantityRows:
            [
                Quantity(8m, "hours", "day", ("travel-mode", "land")),
                Quantity(10m, "hours", "day", ("travel-mode", "rowed-watercraft")),
                Quantity(24m, "hours", "day", ("travel-mode", "sailing-ship"))
            ],
            Scale: "overland");

    private static TravelEnvironmentMechanicDefinition BuildForcedMarch(int dcPerExtraHour) =>
        new(
            "travel.overland.forced-march-check",
            TravelEnvironmentMechanicKinds.CheckDc,
            "Forced march Constitution check",
            TravelEnvironmentResolutionKinds.LinearCheckDc,
            [IntegerInput("extra-hours", true)],
            LinearCheck: new TravelEnvironmentLinearCheckDefinition(
                10,
                "extra-hours",
                dcPerExtraHour,
                1,
                "constitution",
                null,
                "each-hour-beyond-eight",
                "forced-march-damage"),
            Scale: "overland");

    private static TravelEnvironmentMechanicDefinition BuildThreeEMountVehicleRates() =>
        BuildMountVehicleRates(
        [
            ("light-horse-or-light-warhorse", 6m, 48m),
            ("light-horse-loaded", 4m, 32m),
            ("light-warhorse-loaded", 4m, 32m),
            ("heavy-horse", 5m, 40m),
            ("heavy-horse-loaded", 3.5m, 28m),
            ("heavy-warhorse", 4m, 32m),
            ("heavy-warhorse-loaded", 3m, 24m),
            ("pony-or-warpony", 4m, 32m),
            ("pony-loaded", 3m, 24m),
            ("warpony-loaded", 3m, 24m),
            ("donkey-or-mule", 3m, 24m),
            ("mule-loaded", 2m, 16m),
            ("cart-or-wagon", 2m, 16m),
            ("raft-or-barge", .5m, 5m),
            ("keelboat", 1m, 10m),
            ("rowboat", 1.5m, 15m),
            ("sailing-ship", 2m, 48m),
            ("warship", 2.5m, 60m),
            ("longship", 3m, 72m),
            ("galley", 4m, 96m)
        ]);

    private static TravelEnvironmentMechanicDefinition BuildThreeFiveMountVehicleRates() =>
        BuildMountVehicleRates(
        [
            ("light-horse-or-light-warhorse", 6m, 48m),
            ("light-horse-loaded", 4m, 32m),
            ("light-warhorse-loaded", 4m, 32m),
            ("heavy-horse-or-heavy-warhorse", 5m, 40m),
            ("heavy-horse-loaded", 3.5m, 28m),
            ("heavy-warhorse-loaded", 3.5m, 28m),
            ("pony-or-warpony", 4m, 32m),
            ("pony-loaded", 3m, 24m),
            ("warpony-loaded", 3m, 24m),
            ("donkey-or-mule", 3m, 24m),
            ("donkey-loaded", 2m, 16m),
            ("mule-loaded", 2m, 16m),
            ("riding-dog", 4m, 32m),
            ("riding-dog-loaded", 3m, 24m),
            ("cart-or-wagon", 2m, 16m),
            ("raft-or-barge", .5m, 5m),
            ("keelboat", 1m, 10m),
            ("rowboat", 1.5m, 15m),
            ("sailing-ship", 2m, 48m),
            ("warship", 2.5m, 60m),
            ("longship", 3m, 72m),
            ("galley", 4m, 96m)
        ]);

    private static TravelEnvironmentMechanicDefinition BuildMountVehicleRates(
        IReadOnlyList<(string Key, decimal Hourly, decimal Daily)> rows)
    {
        var modes = rows.Select(value => value.Key).ToArray();
        var quantities = rows
            .SelectMany(value => new[]
            {
                Quantity(value.Hourly, "miles", "hour", ("travel-mode", value.Key), ("period", "hour")),
                Quantity(value.Daily, "miles", "day", ("travel-mode", value.Key), ("period", "day"))
            })
            .ToArray();
        return new TravelEnvironmentMechanicDefinition(
            "travel.overland.mount-vehicle-distance",
            TravelEnvironmentMechanicKinds.DistanceRate,
            "Mount or vehicle travel distance",
            TravelEnvironmentResolutionKinds.LookupQuantity,
            [
                StringInput("travel-mode", true, modes),
                StringInput("period", true, "hour", "day")
            ],
            QuantityRows: quantities,
            Scale: "overland");
    }

    private static TravelEnvironmentMechanicDefinition BuildThreeFiveHamperedMovement() =>
        new(
            "travel.environment.hampered-movement-cost",
            TravelEnvironmentMechanicKinds.MovementCostFactor,
            "Hampered movement cost",
            TravelEnvironmentResolutionKinds.LookupFactor,
            [
                BooleanInput("difficult-terrain", true),
                BooleanInput("obstacle", true),
                BooleanInput("poor-visibility", true)
            ],
            FactorRows:
            [
                Factor(1m, ("difficult-terrain", "False"), ("obstacle", "False"), ("poor-visibility", "False")),
                Factor(2m, ("difficult-terrain", "True"), ("obstacle", "False"), ("poor-visibility", "False")),
                Factor(2m, ("difficult-terrain", "False"), ("obstacle", "True"), ("poor-visibility", "False")),
                Factor(2m, ("difficult-terrain", "False"), ("obstacle", "False"), ("poor-visibility", "True")),
                Factor(4m, ("difficult-terrain", "True"), ("obstacle", "True"), ("poor-visibility", "False")),
                Factor(4m, ("difficult-terrain", "True"), ("obstacle", "False"), ("poor-visibility", "True")),
                Factor(4m, ("difficult-terrain", "False"), ("obstacle", "True"), ("poor-visibility", "True")),
                Factor(8m, ("difficult-terrain", "True"), ("obstacle", "True"), ("poor-visibility", "True"))
            ],
            FactorSemantic: "movement-cost-multiplier",
            Scale: "movement-space");

    private static TravelEnvironmentMechanicDefinition BuildThreeFiveDownstreamCurrentSpeedBonus() =>
        new(
            "travel.water.downstream-current-speed-bonus",
            TravelEnvironmentMechanicKinds.DistanceRate,
            "Typical downstream current speed bonus",
            TravelEnvironmentResolutionKinds.LookupQuantity,
            [],
            QuantityRows:
            [
                Quantity(3m, "miles", "hour")
            ],
            Scale: "overland");

    private static TravelEnvironmentMechanicDefinition BuildThreeFiveGuidedFloatDuration() =>
        new(
            "travel.water.guided-downstream-float-duration",
            TravelEnvironmentMechanicKinds.Duration,
            "Additional guided downstream float duration",
            TravelEnvironmentResolutionKinds.LookupQuantity,
            [
                StringInput("travel-mode", true, "raft-or-barge", "keelboat", "rowboat"),
                BooleanInput("guided", true),
                BooleanInput("traveling-downstream", true)
            ],
            QuantityRows:
            [
                Quantity(14m, "hours", "day", ("travel-mode", "raft-or-barge"), ("guided", "True"), ("traveling-downstream", "True")),
                Quantity(14m, "hours", "day", ("travel-mode", "keelboat"), ("guided", "True"), ("traveling-downstream", "True")),
                Quantity(14m, "hours", "day", ("travel-mode", "rowboat"), ("guided", "True"), ("traveling-downstream", "True"))
            ],
            Scale: "overland");

    private static IReadOnlyList<TravelEnvironmentMechanicDefinition> BuildThreeFiveNavigation()
    {
        var options = new[]
        {
            new TravelEnvironmentCheckOption("moor-or-hill-with-map", 6),
            new TravelEnvironmentCheckOption("mountain-with-map", 8),
            new TravelEnvironmentCheckOption("moor-or-hill-without-map", 10),
            new TravelEnvironmentCheckOption("poor-visibility", 12),
            new TravelEnvironmentCheckOption("mountain-without-map", 12),
            new TravelEnvironmentCheckOption("forest", 15)
        };

        return
        [
            new TravelEnvironmentMechanicDefinition(
                "travel.navigation.avoid-getting-lost",
                TravelEnvironmentMechanicKinds.CheckDc,
                "Avoid getting lost",
                TravelEnvironmentResolutionKinds.MaximumApplicableCheckDc,
                [new TravelEnvironmentInputDefinition(
                    "risk-factors",
                    TravelEnvironmentInputValueKinds.StringList,
                    true,
                    options.Select(value => value.Key).ToArray())],
                MaximumCheck: new TravelEnvironmentMaximumCheckDefinition(
                    "risk-factors",
                    options,
                    null,
                    "skill.survival",
                    "once-per-hour-or-portion",
                    "become-lost"),
                Scale: "local-or-overland"),
            new TravelEnvironmentMechanicDefinition(
                "travel.navigation.recognize-lost",
                TravelEnvironmentMechanicKinds.CheckDc,
                "Recognize that the party is lost",
                TravelEnvironmentResolutionKinds.LinearCheckDc,
                [IntegerInput("random-travel-hours", true)],
                LinearCheck: new TravelEnvironmentLinearCheckDefinition(
                    20,
                    "random-travel-hours",
                    -1,
                    1,
                    null,
                    "skill.survival",
                    "once-per-hour-of-random-travel",
                    "remain-unaware-lost"),
                Scale: "local-or-overland"),
            new TravelEnvironmentMechanicDefinition(
                "travel.navigation.set-new-course",
                TravelEnvironmentMechanicKinds.CheckDc,
                "Set a new course while lost",
                TravelEnvironmentResolutionKinds.LinearCheckDc,
                [IntegerInput("random-travel-hours", true)],
                LinearCheck: new TravelEnvironmentLinearCheckDefinition(
                    15,
                    "random-travel-hours",
                    2,
                    0,
                    null,
                    "skill.survival",
                    "when-setting-new-course",
                    "choose-random-direction"),
                Scale: "local-or-overland")
        ];
    }

    private static TravelEnvironmentMechanicDefinition BuildFiveTwoDifficultTerrain() =>
        new(
            "travel.environment.difficult-terrain-movement-cost",
            TravelEnvironmentMechanicKinds.MovementCostFactor,
            "Difficult terrain movement cost",
            TravelEnvironmentResolutionKinds.ConstantFactor,
            [],
            ConstantFactor: 2m,
            FactorSemantic: "movement-cost-multiplier",
            Scale: "movement-space");

    private static TravelEnvironmentMechanicDefinition BuildFiveTwoHighAltitude() =>
        new(
            "travel.environment.high-altitude-travel-time-cost",
            TravelEnvironmentMechanicKinds.TravelTimeCostFactor,
            "High-altitude travel time cost",
            TravelEnvironmentResolutionKinds.ThresholdFactor,
            [
                IntegerInput("elevation-feet", true),
                BooleanInput("subject-to-high-altitude-travel-cost", true)
            ],
            ThresholdFactor: new TravelEnvironmentThresholdFactorDefinition(
                "elevation-feet",
                10000,
                "subject-to-high-altitude-travel-cost",
                2m),
            FactorSemantic: "travel-time-cost-multiplier",
            Scale: "travel-hour");

    private static TravelEnvironmentInputDefinition IntegerInput(string key, bool required) =>
        new(key, TravelEnvironmentInputValueKinds.Integer, required);

    private static TravelEnvironmentInputDefinition BooleanInput(string key, bool required) =>
        new(key, TravelEnvironmentInputValueKinds.Boolean, required);

    private static TravelEnvironmentInputDefinition StringInput(
        string key,
        bool required,
        params string[] allowed) =>
        new(key, TravelEnvironmentInputValueKinds.String, required, allowed);

    private static TravelEnvironmentInputDefinition StringInput(
        string key,
        bool required,
        IReadOnlyList<string> allowed) =>
        new(key, TravelEnvironmentInputValueKinds.String, required, allowed);

    private static TravelEnvironmentQuantityRow Quantity(
        decimal value,
        string unit,
        string perUnit,
        params (string Key, string Value)[] selectors) =>
        new(
            selectors.Select(value => new TravelEnvironmentSelector(value.Key, value.Value)).ToArray(),
            new TravelEnvironmentQuantity(value, unit, perUnit));

    private static TravelEnvironmentQuantityRow Quantity(
        decimal value,
        string unit,
        string perUnit = null!) =>
        new([], new TravelEnvironmentQuantity(value, unit, perUnit));

    private static TravelEnvironmentFactorRow Factor(
        decimal factor,
        params (string Key, string Value)[] selectors) =>
        new(
            selectors.Select(value => new TravelEnvironmentSelector(value.Key, value.Value)).ToArray(),
            factor);

    private static bool ContainsBody(JsonElement document, string value) =>
        document.ValueKind == JsonValueKind.Object
        && document.TryGetProperty("body", out var body)
        && body.ValueKind == JsonValueKind.String
        && body.GetString()?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;

    private static bool ContainsDocumentText(JsonElement document, string value) =>
        document.GetRawText().Contains(value, StringComparison.OrdinalIgnoreCase);
}