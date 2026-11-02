using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeHarvestingResolutionTests
{
    [Fact]
    public void NativeCatalogProvidesSourceAttributionAndAuthoredTypeComponents()
    {
        using var fixture = new Fixture();
        var catalog = new NativeHarvestingResolver(fixture.Catalog)
            .GetCatalog(fixture.Catalog.Fingerprint);
        Assert.Equal("first-party-001", catalog.RulesetId);
        Assert.Equal("Loot Tavern", catalog.Source.Provider);
        var dragon = Assert.Single(catalog.CreatureTypes);
        Assert.Equal("dragon", dragon.Key);
        Assert.Equal(["eye", "scale", "heart"],
            dragon.DefaultComponents.Select(c => c.Key));
    }

    [Fact]
    public void EditsPreserveExistingOrderAndOverrideOriginWithoutInventingComponents()
    {
        using var fixture = new Fixture();
        var resolver = new NativeHarvestingResolver(fixture.Catalog);
        var table = resolver.Resolve(fixture.Request() with {
            ManualEdits = new(["eye"], [
                new("scale", Quantity: 3, ComponentDc: 7),
                new("breath-sac", "Breath sac", 25, 1)
            ])
        });
        Assert.True(table.ManualEditsApplied);
        Assert.Equal(["scale", "heart", "breath-sac"], table.Components.Select(c => c.Key));
        Assert.Equal([7, 20, 25], table.Components.Select(c => c.ComponentDc));
        Assert.Equal(["manual", "base", "manual"], table.Components.Select(c => c.Origin));
        Assert.Equal(3, table.Components[0].Quantity);
        Assert.Throws<ArgumentException>(() => resolver.Resolve(fixture.Request() with {
            ManualEdits = new(UpsertComponents: [new("new")])
        }));
    }

    [Fact]
    public void NativeBestiaryDerivesTypeSizeAndCreatureSpecificEdits()
    {
        using var fixture = new Fixture();
        var table = new NativeHarvestingResolver(fixture.Catalog)
            .Resolve(fixture.Request() with {
                CreatureType = null, CreatureConceptKey = "monster.demo-dragon"
            });
        Assert.Equal("monster.demo-dragon", table.CreatureConceptKey);
        Assert.Equal("Dragon Test", table.CreatureDisplayName);
        Assert.Equal("Huge", table.CreatureSize);
        Assert.True(table.CreatureOverridesApplied);
        Assert.Equal(["eye", "heart"], table.Components.Select(c => c.Key));
        Assert.Equal(11, table.Components[0].ComponentDc);
        Assert.Equal("creature", table.Components[0].Origin);
    }

    [Fact]
    public void CumulativeDcsHelpersAndSameActorMatchPriorOutcome()
    {
        using var fixture = new Fixture();
        var resolver = new NativeHarvestingResolver(fixture.Catalog);
        var outcome = resolver.Evaluate(new(fixture.Request(), 8, 8, true,
            ["eye", "scale", "heart"], "Medium", [
                new(4, true), new(5, false)
            ]));
        Assert.Equal(22, outcome.HarvestingResult);
        Assert.Equal(6, outcome.HelperContribution);
        Assert.Equal(2, outcome.HelperCount);
        Assert.Equal("disadvantage", outcome.AssessmentRollMode);
        Assert.Equal("disadvantage", outcome.CarvingRollMode);
        Assert.Equal([5, 15, 35], outcome.Components.Select(c => c.HarvestDc));
        Assert.Equal([true, true, false], outcome.Components.Select(c => c.Awarded));
    }

    [Fact]
    public void InvalidHelpersOrderAndVersionRejectInsteadOfAwardingComponents()
    {
        using var fixture = new Fixture();
        var resolver = new NativeHarvestingResolver(fixture.Catalog);
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(
            fixture.Request() with { Fingerprint = "stale" }));
        Assert.Throws<ArgumentException>(() => resolver.Evaluate(new(
            fixture.Request(), 8, 8, false, ["eye", "eye"])));
        Assert.Throws<KeyNotFoundException>(() => resolver.Evaluate(new(
            fixture.Request(), 8, 8, false, ["invalid"])));
        Assert.Throws<ArgumentException>(() => resolver.Evaluate(new(
            fixture.Request(), 8, 8, false, ["eye"], "Tiny", [new(4, true)])));
        Assert.Throws<ArgumentException>(() => resolver.Evaluate(new(
            fixture.Request(), 8, 8, false, ["eye"], "Medium", [
                new(4, true, ParticipatedForEntireDuration: false)
            ])));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root =
            Path.Combine(Path.GetTempPath(), "native-harvesting-" + Guid.NewGuid().ToString("N"));
        public NativeCatalogSnapshot Catalog { get; }

        public Fixture()
        {
            var directory = Path.Combine(_root, "first-party-001");
            Directory.CreateDirectory(Path.Combine(directory, "native"));
            File.WriteAllText(Path.Combine(directory, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            File.WriteAllText(Path.Combine(directory, "native", "harvesting.json"), """
                {
                  "id": "procedure.harvesting.loot-tavern-lite",
                  "name": "Harvesting & Crafting Lite",
                  "kind": "harvesting-procedure",
                  "source": {
                    "workKey": "loot-tavern.harvesting-crafting-lite",
                    "workDisplayName": "Harvesting & Crafting Lite",
                    "provider": "Loot Tavern", "gameEdition": "5e",
                    "releaseKind": "public-release", "publicationDate": "2024-07-03",
                    "referenceUri": "https://example.test/loot-tavern"
                  },
                  "procedure": {
                    "assessment": {"mechanicKey":"check.harvesting.assessment","abilityKey":"intelligence","abilityDisplayName":"Intelligence","defaultRollMode":"normal"},
                    "carving": {"mechanicKey":"check.harvesting.carving","abilityKey":"dexterity","abilityDisplayName":"Dexterity","defaultRollMode":"normal"},
                    "totalMechanicKey":"check.harvesting.total","sameActorRollMode":"disadvantage",
                    "componentDcAggregation":"cumulative-in-order","awardMode":"ordered-prefix",
                    "helpers":{"maximumByCreatureSize":{"Tiny":0,"Small":1,"Medium":2,"Large":4,"Huge":6,"Gargantuan":10},"standardHelpActionApplies":false}
                  },
                  "creatureTypes": [{
                    "key":"dragon","displayName":"Dragon","competencyKey":"competency.survival",
                    "competencyDisplayName":"Survival",
                    "defaultComponents":[
                      {"key":"eye","displayName":"Eye","componentDc":5},
                      {"key":"scale","displayName":"Scale","componentDc":10},
                      {"key":"heart","displayName":"Heart","componentDc":20}
                    ]
                  }]
                }
                """);
            File.WriteAllText(Path.Combine(directory, "native", "dragon.json"), """
                {"id":"monster.demo-dragon","name":"Dragon Test","kind":"monster",
                 "definition":{"type":{"type":"dragon"},"size":["H"],
                   "_rulesCore":{"harvesting":{
                     "removeComponents":["scale"],
                     "upsertComponents":[{"key":"eye","componentDc":11}]
                   }}}}
                """);
            Catalog = NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(
                _root, "first-party-001"));
        }
        public NativeHarvestingTableRequest Request() =>
            new("first-party-001", Catalog.Fingerprint, CreatureType: "dragon");
        public void Dispose() => Directory.Delete(_root, true);
    }
}
