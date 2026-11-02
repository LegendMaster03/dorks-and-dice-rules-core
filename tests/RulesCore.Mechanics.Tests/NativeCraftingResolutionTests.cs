using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeCraftingResolutionTests
{
    [Theory]
    [InlineData(15, 3, 2, 0, 20, "completed", 0, true)]
    [InlineData(5, 3, 2, 0, 20, "failed", -10, false)]
    [InlineData(19, 1, 0, 0, 20, "completed", 0, true)]
    public void ManufacturingMatchesPriorAcceptedOutcome(
        int die, int competency, int ability, int other, int dc,
        string expected, int margin, bool functional)
    {
        using var fixture = new Fixture();
        var result = new NativeCraftingResolver(fixture.Catalog).ResolveManufacturing(
            fixture.Request() with {
                Competency = new("Woodcarving", competency, true),
                ManualAbilityModifier = ability,
                D20Roll = die, OtherModifier = other, TargetDc = dc
            });
        Assert.Equal("resolved", result.Status);
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(margin, result.Margin);
        Assert.Equal(functional, result.ProducesFunctionalOutput);
        Assert.True(result.InputsConsumed);
        Assert.Equal(functional, result.MeetsTarget);
    }

    [Fact]
    public void UnqualifiedCraftingRequiresDisadvantageWithoutQualifiedGuidance()
    {
        using var fixture = new Fixture();
        var resolver = new NativeCraftingResolver(fixture.Catalog);
        var without = resolver.ResolveManufacturing(fixture.Request());
        Assert.Equal("resolved", without.Status);
        Assert.Equal("disadvantage", without.RollMode);
        Assert.Equal("pending", without.Outcome);
        Assert.Null(without.Total);
        Assert.False(without.InputsConsumed);
        Assert.Equal("normal", resolver.ResolveManufacturing(fixture.Request()
            with { HasQualifiedGuidance = true }).RollMode);
        Assert.Equal("normal", resolver.ResolveManufacturing(fixture.Request()
            with { Competency = new("Smithing", 3, true) }).RollMode);
    }

    [Fact]
    public void MissingManualAbilityDoesNotGuessAnAbilityContribution()
    {
        using var fixture = new Fixture();
        var result = new NativeCraftingResolver(fixture.Catalog)
            .ResolveManufacturing(fixture.Request() with {
                ManualAbilityModifier = null, D20Roll = 20, TargetDc = 1
            });
        Assert.Equal("unsupported", result.Status);
        Assert.Equal("manual-ability-required", result.Reason);
        Assert.Null(result.Total);
        Assert.False(result.InputsConsumed);
    }

    [Fact]
    public void SnapshotAndDiceAreValidatedWithoutConsumingMaterials()
    {
        using var fixture = new Fixture();
        var resolver = new NativeCraftingResolver(fixture.Catalog);
        var stale = resolver.ResolveManufacturing(fixture.Request() with {
            Fingerprint = new string('0', 64)
        });
        Assert.Equal("unsupported", stale.Status);
        Assert.Equal("ruleset-version-mismatch", stale.Reason);
        var badDie = resolver.ResolveManufacturing(fixture.Request() with { D20Roll = 21 });
        Assert.Equal("unsupported", badDie.Status);
        Assert.Equal("invalid-manufacturing-input", badDie.Reason);
        Assert.False(badDie.InputsConsumed);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _directory =
            Path.Combine(Path.GetTempPath(), "native-craft-" + Guid.NewGuid().ToString("N"));
        public NativeCatalogSnapshot Catalog { get; }

        public Fixture()
        {
            var folder = Path.Combine(_directory, "first-party-001");
            Directory.CreateDirectory(Path.Combine(folder, "native"));
            File.WriteAllText(Path.Combine(folder, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            File.WriteAllText(Path.Combine(folder, "native", "class.json"),
                """{"id":"class.demo","name":"Demo","kind":"class"}""");
            Catalog = NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(
                _directory, "first-party-001"));
        }
        public NativeManufacturingRequest Request() => new(
            "first-party-001", Catalog.Fingerprint,
            new("Smithing", 3, false),
            ManualAbilityModifier: 2);

        public void Dispose() => Directory.Delete(_directory, true);
    }
}
