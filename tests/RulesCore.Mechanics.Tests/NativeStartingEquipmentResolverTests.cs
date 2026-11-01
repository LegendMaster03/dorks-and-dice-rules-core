using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeStartingEquipmentResolverTests
{
    [Fact]
    public void MissingExecutableGrantDataDoesNotProduceAnEmptySuccess()
    {
        using var fixture = new Fixture(false, false);
        var result = new NativeStartingEquipmentResolver(fixture.Catalog)
            .Resolve(fixture.Request());
        Assert.Equal("unavailable", result.Status);
        Assert.Empty(result.Grants);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "native-equipment-not-reconciled");
    }

    [Fact]
    public void PackageSelectionMustBeExplicitAndUsesStableGrantProvenance()
    {
        using var fixture = new Fixture(true, false);
        var resolver = new NativeStartingEquipmentResolver(fixture.Catalog);
        var unresolved = resolver.Resolve(fixture.Request());
        Assert.Equal("needs-choices", unresolved.Status);
        Assert.Empty(unresolved.Grants); // No partial fixed grants before choice.
        var choice = Assert.Single(unresolved.Choices);
        Assert.Equal("starting-equipment.00000000-0000-0000-0000-000000000001",
            choice.ChoiceKey);
        Assert.Contains(choice.Options, item => item.Id == "gold");

        var selected = resolver.Resolve(fixture.Request(
            new Dictionary<string, string> { [choice.ChoiceKey] = "gold" }));
        Assert.Equal("resolved", selected.Status);
        Assert.Empty(selected.Issues);
        Assert.Single(selected.Choices);
        Assert.Equal("starting-equipment.00000000-0000-0000-0000-000000000001",
            selected.Choices[0].ChoiceKey);
        Assert.Collection(selected.Grants,
            grant => {
                Assert.Equal("item", grant.Kind);
                Assert.Equal("item.demo.knife", grant.TargetKey);
                Assert.Equal(1, grant.Quantity);
                Assert.Contains("native.00000000-0000-0000-0000-000000000001.", grant.GrantKey);
            },
            grant => {
                Assert.Equal("currency", grant.Kind);
                Assert.Equal("gp", grant.TargetKey);
                Assert.Equal(50, grant.Quantity);
            });
    }

    [Fact]
    public void InvalidOrUnresolvedNativeEquipmentNeverMaterializesPartialGrants()
    {
        using var fixture = new Fixture(true, true);
        var result = new NativeStartingEquipmentResolver(fixture.Catalog).Resolve(
            fixture.Request(new Dictionary<string, string> {
                ["starting-equipment.00000000-0000-0000-0000-000000000001"] = "gold"
            }));
        Assert.Equal("unavailable", result.Status);
        Assert.Empty(result.Grants);
        Assert.Contains(result.Issues, issue => issue.Code == "native-item-unavailable");
    }

    [Fact]
    public void SelectedUnknownPackageAndStaleFingerprintFailClosed()
    {
        using var fixture = new Fixture(true, false);
        var resolver = new NativeStartingEquipmentResolver(fixture.Catalog);
        var invalidChoice = resolver.Resolve(fixture.Request(
            new Dictionary<string, string> {
                ["starting-equipment.00000000-0000-0000-0000-000000000001"] = "unknown"
            }));
        Assert.Equal("unavailable", invalidChoice.Status);
        Assert.Empty(invalidChoice.Grants);
        var version = resolver.Resolve(fixture.Request() with {
            Fingerprint = new string('0', 64)
        });
        Assert.Equal("invalid", version.Status);
        Assert.Empty(version.Grants);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _path =
            Path.Combine(Path.GetTempPath(), "rules-core-equipment-" + Guid.NewGuid().ToString("N"));
        public NativeCatalogSnapshot Catalog { get; }

        public Fixture(bool approved, bool missingItem)
        {
            var package = Path.Combine(_path, "first-party-001");
            Directory.CreateDirectory(Path.Combine(package, "native"));
            File.WriteAllText(Path.Combine(package, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            var equipment = approved
                ? """
                  "startingEquipment":{"materialization":{
                    "fixedGrants":[{"id":"knife","kind":"item","itemId":"item.demo.knife","quantity":1}],
                    "packages":[
                      {"id":"gold","name":"50 gold pieces","grants":[
                        {"id":"gold","kind":"currency","currencyKey":"gp","quantity":50}]},
                      {"id":"kit","name":"Travel kit","grants":[
                        {"id":"pack","kind":"item","itemId":"item.demo.knife","quantity":2}]}
                    ]
                  }},
                  """
                : """
                  "startingEquipment":{"chooseOnePackage":["host-SRD-package"]},
                  """;
            File.WriteAllText(Path.Combine(package, "native", "class.json"),
                "{\"id\":\"class.demo\",\"name\":\"Demo\",\"kind\":\"class\",\"definition\":{" +
                equipment + "\"progression\":[{\"level\":1,\"proficiencyBonus\":2,\"featureGrants\":[]}]}}");
            if (!missingItem)
                File.WriteAllText(Path.Combine(package, "native", "items.json"),
                    """{"id":"item.demo.knife","name":"Demo Knife","kind":"item"}""");
            Catalog = NativeCatalogLoader.Load(
                NativeRulesetLayout.Resolve(_path, "first-party-001"));
        }

        public NativeStartingEquipmentRequest Request(
            IReadOnlyDictionary<string, string>? selected = null) => new(
                "first-party-001", Catalog.Fingerprint,
                [new NativeAdvancement("class.demo", 1,
                    "00000000-0000-0000-0000-000000000001")], selected);
        public void Dispose() => Directory.Delete(_path, true);
    }
}
