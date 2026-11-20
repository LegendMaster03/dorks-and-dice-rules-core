using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Synthetic native recipes demonstrate first-party resource quotation without
/// approving real ingredient quantities, prices, or Construct starting budgets.
/// </summary>
public sealed class NativeConstructRecipeCostTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "construct-recipe-quote-" + Guid.NewGuid().ToString("N"));
        public NativeCatalogSnapshot Catalog { get; }

        public Fixture(string variant = "")
        {
            var package = Path.Combine(_root, "first-party-001");
            var native = Path.Combine(package, "native");
            Directory.CreateDirectory(native);
            File.WriteAllText(Path.Combine(package, "manifest.json"), """
                {"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,
                 "nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}
                """);
            var rule = """
                {"id":"rule.creature-origination","kind":"rule","name":"Origination",
                 "methodDefinitions":[{"id":"construct-assembly","name":"Assembly",
                  "initialConstructionAllowance":20,
                  "initialConstructionCostModel":"recipe-inputs",
                  "startingResourceUnitCosts":{
                   "construct-material.steel":2,"construct-material.wood":1,
                   "crafting-material.ore":2,"crafting-material.reagent":1,
                   "crafting-material.crystal":2}}]}
                """;
            if (variant == "unpriced-ore")
                rule = rule.Replace("\"crafting-material.ore\":2,", "");
            File.WriteAllText(Path.Combine(native, "rule.json"), rule);
            File.WriteAllText(Path.Combine(native, "competencies.json"), """
                [{"id":"competency.smithing","kind":"competency","name":"Smithing"}]
                """);
            File.WriteAllText(Path.Combine(native, "resources.json"), """
                [{"id":"crafting-material.ore","kind":"crafting-material","name":"Ore"},
                 {"id":"crafting-material.reagent","kind":"crafting-material","name":"Reagent"},
                 {"id":"crafting-material.crystal","kind":"crafting-material","name":"Crystal"},
                 {"id":"crafting-material.alloy","kind":"crafting-material","name":"Alloy"}]
                """);
            File.WriteAllText(Path.Combine(native, "materials.json"), """
                [{"id":"construct-material.steel","kind":"construct-material",
                  "name":"Steel","constructionCost":999},
                 {"id":"construct-material.wood","kind":"construct-material","name":"Wood"}]
                """);
            var components = """
                [{"id":"construct-component.frame","kind":"construct-component",
                  "name":"Frame","constructionCost":999,"powerDemand":0,
                  "provides":["structural-support"],"materialSlots":["frame"],
                  "craftingRecipeId":"crafting-recipe.frame"},
                 {"id":"construct-component.actuator","kind":"construct-component",
                  "name":"Actuator","powerDemand":2,"provides":["actuation"],
                  "craftingRecipeId":"crafting-recipe.actuator"},
                 {"id":"construct-component.housing","kind":"construct-component",
                  "name":"Housing","powerDemand":0,"powerSourceSlot":true,
                  "provides":["power-housing"],"materialSlots":["housing"],
                  "craftingRecipeId":"crafting-recipe.housing"}]
                """;
            if (variant == "missing-recipe")
                components = components.Replace(
                    "\"craftingRecipeId\":\"crafting-recipe.frame\"", "");
            // Do not leave an invalid trailing comma after removing the field.
            if (variant == "missing-recipe")
                components = components.Replace("\"materialSlots\":[\"frame\"],\n                  }",
                    "\"materialSlots\":[\"frame\"]}");
            File.WriteAllText(Path.Combine(native, "components.json"), components);
            File.WriteAllText(Path.Combine(native, "designs.json"), """
                [{"id":"construct-design.reinforced","kind":"construct-design",
                  "name":"Reinforced","compatibleComponentCapabilities":["structural-support"],
                  "craftingRecipeId":"crafting-recipe.reinforced"}]
                """);
            File.WriteAllText(Path.Combine(native, "enhancements.json"), """
                [{"id":"construct-enhancement.grip","kind":"construct-enhancement",
                  "name":"Grip","compatibleComponentCapabilities":["actuation"],
                  "craftingRecipeId":"crafting-recipe.grip"}]
                """);
            File.WriteAllText(Path.Combine(native, "power-sources.json"), """
                [{"id":"construct-power-source.crystal","kind":"construct-power-source",
                  "name":"Crystal","compatibleComponentCapabilities":["power-housing"],
                  "powerOutput":4,"craftingRecipeId":"crafting-recipe.crystal"}]
                """);
            var recipes = """
                [{"id":"crafting-recipe.frame","kind":"crafting-recipe","name":"Frame",
                  "producesDefinitionId":"construct-component.frame",
                  "inputs":[{"materialSlot":"frame","amount":3}]},
                 {"id":"crafting-recipe.actuator","kind":"crafting-recipe","name":"Actuator",
                  "producesDefinitionId":"construct-component.actuator",
                  "stages":[
                   {"stepId":"forge","competencyId":"competency.smithing",
                    "minimumRanks":2,"dependencies":["smelt"],
                    "inputs":[{"materialId":"crafting-material.alloy","amount":1},
                              {"materialId":"crafting-material.reagent","amount":1}],
                    "outputs":[]},
                   {"stepId":"smelt","competencyId":"competency.smithing",
                    "minimumRanks":1,"dependencies":[],
                    "inputs":[{"materialId":"crafting-material.ore","amount":2}],
                    "outputs":[{"materialId":"crafting-material.alloy","amount":1}]}
                  ]},
                 {"id":"crafting-recipe.housing","kind":"crafting-recipe","name":"Housing",
                  "producesDefinitionId":"construct-component.housing",
                  "inputs":[{"materialSlot":"housing","amount":1}]},
                 {"id":"crafting-recipe.reinforced","kind":"crafting-recipe","name":"Reinforced",
                  "producesDefinitionId":"construct-design.reinforced",
                  "inputs":[{"materialId":"construct-material.steel","amount":1}]},
                 {"id":"crafting-recipe.grip","kind":"crafting-recipe","name":"Grip",
                  "producesDefinitionId":"construct-enhancement.grip",
                  "inputs":[{"materialId":"crafting-material.reagent","amount":1}]},
                 {"id":"crafting-recipe.crystal","kind":"crafting-recipe","name":"Crystal",
                  "producesDefinitionId":"construct-power-source.crystal",
                  "inputs":[{"materialId":"crafting-material.crystal","amount":1}]}
                ]
                """;
            if (variant == "wrong-product")
                recipes = recipes.Replace(
                    "\"producesDefinitionId\":\"construct-component.frame\"",
                    "\"producesDefinitionId\":\"construct-component.housing\"");
            if (variant == "cyclic-stages")
                recipes = recipes.Replace(
                    "\"stepId\":\"smelt\",\"competencyId\":\"competency.smithing\",\n                    \"minimumRanks\":1,\"dependencies\":[]",
                    "\"stepId\":\"smelt\",\"competencyId\":\"competency.smithing\",\n                    \"minimumRanks\":1,\"dependencies\":[\"forge\"]");
            File.WriteAllText(Path.Combine(native, "recipes.json"), recipes);
            Catalog = NativeCatalogLoader.Load(
                NativeRulesetLayout.Resolve(_root, "first-party-001"));
        }
        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }

    private static NativeConstructComponentInstance[] Parts(bool actuatorEnabled = false,
        bool secondFrame = false, string material = "construct-material.steel")
    {
        var parts = new List<NativeConstructComponentInstance>
        {
            new("frame", "construct-component.frame",
                Materials: new Dictionary<string, string> { ["frame"] = material },
                DesignId: "construct-design.reinforced"),
            new("actuator", "construct-component.actuator",
                Enabled: actuatorEnabled,
                EnhancementIds: ["construct-enhancement.grip"]),
            new("housing", "construct-component.housing",
                Materials: new Dictionary<string, string>
                    { ["housing"] = "construct-material.wood" },
                PowerSourceId: "construct-power-source.crystal")
        };
        if (secondFrame)
            parts.Add(new("frame2", "construct-component.frame",
                Materials: new Dictionary<string, string>
                    { ["frame"] = "construct-material.steel" }));
        return [.. parts];
    }

    private static NativeConstructAssemblyResult Evaluate(Fixture fixture,
        bool secondFrame = false, string material = "construct-material.steel",
        bool initial = true, int? allowance = 20) =>
        new NativeConstructAssemblyEvaluator(fixture.Catalog).Evaluate(
            new(Parts(secondFrame: secondFrame, material: material),
                InitialAllowance: allowance, InitialConstruction: initial));

    [Fact]
    public void NativeRecipesPriceSelectedIngredientsWithoutDoubleCountingIntermediateAlloy()
    {
        using var fixture = new Fixture();
        var result = Evaluate(fixture);
        Assert.True(result.IsAuthorized,
            string.Join("; ", result.Issues.Select(x => x.Code)));
        // Frame 3 steel (6), actuator 2 ore + reagent (5),
        // housing 1 wood (1), design 1 steel (2),
        // enhancement 1 reagent (1), source 1 crystal (2) = 17.
        // One intermediate alloy is created and consumed within the recipe,
        // not charged as an additional purchased ingredient.
        Assert.Equal(17, result.KnownConstructionCost);
        Assert.False(result.Components.Single(x => x.InstanceId == "actuator").Operational);
    }

    [Fact]
    public void AdditionalInstalledFrameCountsEvenWithoutNewPoweredDemand()
    {
        using var fixture = new Fixture();
        var result = Evaluate(fixture, secondFrame: true);
        Assert.Equal(23, result.KnownConstructionCost);
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, result.Status);
        Assert.Contains(result.Issues, x => x.Code == "construct-allowance-exceeded");
    }

    [Fact]
    public void SelectedComponentMaterialChangesRecipeInputs()
    {
        using var fixture = new Fixture();
        var result = Evaluate(fixture, material: "construct-material.wood");
        Assert.True(result.IsAuthorized);
        Assert.Equal(14, result.KnownConstructionCost);
    }

    [Theory]
    [InlineData("missing-recipe", "crafting-quote-recipe-unresolved",
        NativeOriginConstructionStatus.Unresolved)]
    [InlineData("unpriced-ore", "crafting-quote-resource-price-unresolved",
        NativeOriginConstructionStatus.Unresolved)]
    [InlineData("wrong-product", "crafting-quote-product-mismatch",
        NativeOriginConstructionStatus.Invalid)]
    [InlineData("cyclic-stages", "crafting-quote-stage-structure-invalid",
        NativeOriginConstructionStatus.Invalid)]
    public void MissingOrForgedRecipeEvidenceFailsClosed(
        string variant, string expectedIssue, NativeOriginConstructionStatus expected)
    {
        using var fixture = new Fixture(variant);
        var result = Evaluate(fixture);
        Assert.Equal(expected, result.Status);
        Assert.Contains(result.Issues, x => x.Code == expectedIssue);
    }

    [Fact]
    public void LaterModificationDoesNotReapplyStartingRecipeBudget()
    {
        using var fixture = new Fixture();
        var result = Evaluate(fixture, secondFrame: true, initial: false);
        Assert.True(result.IsAuthorized);
        Assert.Equal(0, result.KnownConstructionCost);
    }
}
