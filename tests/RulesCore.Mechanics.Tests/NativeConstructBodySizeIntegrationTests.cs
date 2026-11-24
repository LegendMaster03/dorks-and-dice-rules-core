using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Synthetic complete Origin case: a selected physical manufacturing variant,
/// not an unrelated method-wide size choice, grants a creature-size intrinsic.
/// </summary>
public sealed class NativeConstructBodySizeIntegrationTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "construct-sized-origin-" + Guid.NewGuid().ToString("N"));
        public NativeCatalogSnapshot Catalog { get; }

        public Fixture()
        {
            var package = Path.Combine(_root, "first-party-001");
            var native = Path.Combine(package, "native");
            Directory.CreateDirectory(native);
            File.WriteAllText(Path.Combine(package, "manifest.json"), """
                {"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,
                 "nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}
                """);
            File.WriteAllText(Path.Combine(native, "rule.json"), """
                {"id":"rule.creature-origination","kind":"rule","name":"Origination",
                 "methodDefinitions":[{"id":"construct-assembly","name":"Assembly",
                    "initialConstructionAllowance":100,
                    "initialConstructionCostModel":"recipe-inputs",
                    "startingResourceUnitCosts":{"construct-material.steel":3},
                    "constructIntrinsics":{"creatureType":"construct"}}]}
                """);
            File.WriteAllText(Path.Combine(native, "types.json"), """
                {"id":"creature-type.construct","kind":"creatureType",
                 "name":"Construct","parent":"creature",
                 "originationMethods":["construct-assembly"]}
                """);
            File.WriteAllText(Path.Combine(native, "materials.json"), """
                {"id":"construct-material.steel","kind":"construct-material",
                 "name":"Steel","massPoundsPerUnit":7}
                """);
            File.WriteAllText(Path.Combine(native, "components.json"), """
                {"id":"construct-component.shell","kind":"construct-component",
                 "name":"Body shell","powerDemand":0,
                 "provides":["enclosure"],"materialSlots":["casing"],
                 "definesBodySize":true,
                 "craftingRecipeId":"crafting-recipe.shell-default",
                 "constructionVariants":[
                    {"id":"medium","craftingRecipeId":"crafting-recipe.shell-medium",
                     "bodySize":"medium"}]}
                """);
            File.WriteAllText(Path.Combine(native, "recipes.json"), """
                [{"id":"crafting-recipe.shell-default","kind":"crafting-recipe",
                  "name":"Default shell","producesDefinitionId":"construct-component.shell",
                  "inputs":[{"materialSlot":"casing","amount":2,"retainedInProduct":true}]},
                 {"id":"crafting-recipe.shell-medium","kind":"crafting-recipe",
                  "name":"Medium body shell","producesDefinitionId":"construct-component.shell",
                  "inputs":[{"materialSlot":"casing","amount":7,"retainedInProduct":true}]}]
                """);
            Catalog = NativeCatalogLoader.Load(
                NativeRulesetLayout.Resolve(_root, "first-party-001"));
        }
        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }

    private static NativeConstructAssemblyRequest Assembly(string? variant = "medium") =>
        new([
            new("shell", "construct-component.shell",
                Materials: new Dictionary<string, string>
                    { ["casing"] = "construct-material.steel" },
                ConstructionVariantId: variant)
        ], InitialAllowance: 100);

    private static NativeOriginProposal Proposal() =>
        new("body-size-synthetic", NativeOriginIntent.Custom,
            new("rule.creature-origination", "construct-assembly"),
            [], [], []);

    [Fact]
    public void ManufacturingVariantAuthorizesTypeAndSizeThroughSharedOrigin()
    {
        using var fixture = new Fixture();
        var service = new NativeConstructOriginConstructionService(fixture.Catalog);
        var assembly = Assembly();
        var draft = service.Evaluate(new(new(Proposal()), assembly));

        Assert.True(draft.Assembly.IsAuthorized,
            string.Join("; ", draft.Assembly.Issues.Select(issue => issue.Code)));
        Assert.Equal("medium", draft.Assembly.DerivedCreatureSize);
        Assert.Equal(21, draft.Assembly.KnownConstructionCost);
        Assert.Equal(49, draft.Assembly.KnownRetainedMaterialMassPounds);
        var sizeOffer = Assert.Single(draft.Origin.EvaluatedProposal.Candidates,
            candidate => candidate.Identity.IntrinsicSlotId == "size");
        Assert.Equal("construct-component.shell",
            sizeOffer.Identity.Occurrence.Source.Id);
        Assert.Contains("instance/shell", sizeOffer.Identity.Occurrence.OccurrencePath);

        var manifests = new NativeOriginManifestation[]
        {
            new("type", "construct:intrinsic:creatureType",
                SelectedValue: JsonSerializer.SerializeToElement("construct")),
            new("size", "construct:intrinsic:size",
                SelectedValue: JsonSerializer.SerializeToElement("medium"))
        };
        var completed = service.Evaluate(new(new(Proposal() with
        {
            Manifestations = manifests
        }), assembly));
        Assert.True(completed.IsMechanicallyAuthorized,
            string.Join("; ", completed.Origin.Issues.Select(issue => issue.Code)));
        Assert.Equal("medium", completed.Origin.Profile!.Underlying.InherentGrants
            .Single(grant => grant.Identity.IntrinsicSlotId == "size")
            .EffectiveValue!.Value.GetString());
    }

    [Fact]
    public void AForgedSizeSelectionIsNotAuthorizedByTheManufacturedPart()
    {
        using var fixture = new Fixture();
        var manifestations = new NativeOriginManifestation[]
        {
            new("type", "construct:intrinsic:creatureType",
                SelectedValue: JsonSerializer.SerializeToElement("construct")),
            new("size", "construct:intrinsic:size",
                SelectedValue: JsonSerializer.SerializeToElement("large"))
        };
        var invalid = new NativeConstructOriginConstructionService(fixture.Catalog)
            .Evaluate(new(new(Proposal() with
            {
                Manifestations = manifestations
            }), Assembly()));
        Assert.False(invalid.IsMechanicallyAuthorized);
    }

    [Fact]
    public void UnsizedShellDoesNotSynthesizeCreatureSize()
    {
        using var fixture = new Fixture();
        var draft = new NativeConstructOriginConstructionService(fixture.Catalog)
            .Evaluate(new(new(Proposal()), Assembly(null)));
        Assert.True(draft.Assembly.IsAuthorized);
        Assert.Null(draft.Assembly.DerivedCreatureSize);
        Assert.DoesNotContain(draft.Origin.EvaluatedProposal.Candidates,
            candidate => candidate.Identity.IntrinsicSlotId == "size");
    }
}
