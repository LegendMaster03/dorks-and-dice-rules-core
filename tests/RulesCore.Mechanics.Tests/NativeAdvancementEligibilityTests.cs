using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeAdvancementEligibilityTests
{
    [Fact]
    public void StartingClassAndNextLevelUseNativeProgression()
    {
        using var fixture = new Fixture();
        var service = new NativeAdvancementEligibility(fixture.Catalog);
        Assert.Equal("eligible", service.Evaluate(fixture.Candidate("class.warlock", 0)).Status);
        Assert.Equal("eligible", service.Evaluate(fixture.Candidate("class.warlock", 1)).Status);
        var complete = service.Evaluate(fixture.Candidate("class.warlock", 2));
        Assert.Equal("ineligible", complete.Status);
        Assert.False(complete.Eligible);
    }

    [Fact]
    public void PatronAndPactAreExclusiveAndRequireParentClass()
    {
        using var fixture = new Fixture();
        var service = new NativeAdvancementEligibility(fixture.Catalog);
        Assert.Equal("ineligible", service.Evaluate(fixture.Candidate("patron.warlock.fiend", 0)).Status);
        Assert.Equal("eligible", service.Evaluate(fixture.Candidate("patron.warlock.fiend", 1)).Status);
        var chosen = fixture.Candidate("patron.warlock.fiend", 1,
            ["patron.warlock.other"]);
        Assert.Equal("ineligible", service.Evaluate(chosen).Status);
        Assert.Contains(service.Evaluate(chosen).Requirements,
            requirement => requirement.Key == "exclusive-choice" && requirement.Status == "failed");
    }

    [Fact]
    public void InvocationLevelPactAndElectiveBudgetAreAllChecked()
    {
        using var fixture = new Fixture();
        var service = new NativeAdvancementEligibility(fixture.Catalog);
        Assert.Equal("ineligible", service.Evaluate(fixture.Candidate("invocation.chain-gift", 1)).Status);
        Assert.Equal("ineligible", service.Evaluate(fixture.Candidate("invocation.chain-gift", 2)).Status);
        var affordable = fixture.Candidate("invocation.chain-gift", 2, ["pact.warlock.chain"]);
        Assert.Equal("eligible", service.Evaluate(affordable).Status);
        var overspent = fixture.Candidate("invocation.chain-gift", 2,
            ["pact.warlock.chain", "invocation.basic", "invocation.other"]);
        var outcome = service.Evaluate(overspent);
        Assert.Equal("ineligible", outcome.Status);
        Assert.Contains(outcome.Requirements, r => r.Key == "invocation-budget" && r.Status == "failed");
    }

    [Fact]
    public void ExplicitAndUnmodeledRequirementsNeverBecomeEligible()
    {
        using var fixture = new Fixture();
        var service = new NativeAdvancementEligibility(fixture.Catalog);
        Assert.Equal("unresolved", service.Evaluate(fixture.Candidate("invocation.cantrip", 2)).Status);
        Assert.Equal("unresolved", service.Evaluate(fixture.Candidate("invocation.new-gate", 2)).Status);
        Assert.Equal("unresolved", service.Evaluate(fixture.Candidate("invocation.unknown", 2)).Status);
        Assert.Equal("unresolved", service.Evaluate(fixture.Candidate("invocation.basic", 2,
            ["invocation.unrecognized"])).Status);
        var duplicate = service.Evaluate(fixture.Candidate("invocation.basic", 2,
            ["invocation.basic"]));
        Assert.Equal("ineligible", duplicate.Status);
    }

    [Fact]
    public void StaleVersionAndAmbiguousClassHistoryAreNotApproved()
    {
        using var fixture = new Fixture();
        var service = new NativeAdvancementEligibility(fixture.Catalog);
        Assert.Equal("invalid", service.Evaluate(fixture.Candidate("class.warlock", 1)
            with { Fingerprint = "stale" }).Status);
        var multipleClasses = fixture.Candidate("class.warlock", 1);
        multipleClasses = multipleClasses with { Character = multipleClasses.Character with {
            Advancements = [new("class.warlock", 1, "a"), new("class.secondary", 1, "b")]
        } };
        Assert.Equal("unresolved", service.Evaluate(multipleClasses).Status);
        var duplicateClass = fixture.Candidate("invocation.basic", 2) with { Character =
            fixture.Candidate("invocation.basic", 2).Character with {
                Advancements = [new("class.warlock", 1, "a"), new("class.warlock", 2, "b")]
            } };
        Assert.Equal("unresolved", service.Evaluate(duplicateClass).Status);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "rules-core-eligibility-" + Guid.NewGuid().ToString("N"));
        public NativeCatalogSnapshot Catalog { get; }

        public Fixture()
        {
            var dir = Path.Combine(_root, "first-party-001");
            Directory.CreateDirectory(Path.Combine(dir, "native"));
            File.WriteAllText(Path.Combine(dir, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            File.WriteAllText(Path.Combine(dir, "native", "classes.json"), """
                [
                  {"id":"class.warlock","name":"Warlock","kind":"class","definition":{"progression":[
                    {"level":1,"invocationsElectiveTotal":1},
                    {"level":2,"invocationsElectiveTotal":2}
                  ]}},
                  {"id":"class.secondary","name":"Secondary","kind":"class","definition":{"progression":[
                    {"level":1}
                  ]}}
                ]
                """);
            File.WriteAllText(Path.Combine(dir, "native", "options.json"), """
                [
                  {"id":"patron.warlock.fiend","name":"Fiend","kind":"patron","parentClass":"class.warlock","availableAtWarlockLevel":1},
                  {"id":"patron.warlock.other","name":"Other","kind":"patron","parentClass":"class.warlock","availableAtWarlockLevel":1},
                  {"id":"pact.warlock.chain","name":"Chain","kind":"pact","parentClass":"class.warlock","availableAtWarlockLevel":1},
                  {"id":"invocation.basic","name":"Basic","kind":"invocation","parentClass":"class.warlock","minimumWarlockLevel":1,"cost":1,"repeatable":false},
                  {"id":"invocation.chain-gift","name":"Chain Gift","kind":"invocation","parentClass":"class.warlock","minimumWarlockLevel":2,"requiredPact":"pact.warlock.chain","cost":1,"repeatable":false},
                  {"id":"invocation.other","name":"Other Invocation","kind":"invocation","parentClass":"class.warlock","minimumWarlockLevel":1,"cost":1,"repeatable":false},
                  {"id":"invocation.cantrip","name":"Cantrip","kind":"invocation","parentClass":"class.warlock","minimumWarlockLevel":1,"cost":1,"cantripPrerequisite":"known damaged cantrip"},
                  {"id":"invocation.new-gate","name":"New Gate","kind":"invocation","parentClass":"class.warlock","minimumWarlockLevel":1,"cost":1,"requiredSpellLevel":3}
                ]
                """);
            Catalog = NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(_root, "first-party-001"));
        }

        public NativeEligibilityRequest Candidate(string id, int warlockLevel,
            IReadOnlyList<string>? selected = null)
        {
            var character = new NativeCharacterRequest("first-party-001", Catalog.Fingerprint,
                new Dictionary<string, int> { ["strength"] = 10, ["dexterity"] = 12,
                    ["constitution"] = 10, ["intelligence"] = 10, ["wisdom"] = 10,
                    ["charisma"] = 16 },
                warlockLevel == 0 ? [] : [new NativeAdvancement("class.warlock", warlockLevel, "a")],
                SelectedOptions: selected);
            return new NativeEligibilityRequest("first-party-001", Catalog.Fingerprint, id, character);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}
