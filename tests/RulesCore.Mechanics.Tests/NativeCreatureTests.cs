using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeCreatureTests
{
    [Fact]
    public void SourceInitiativeProficiencyDoesNotSilentlyBecomeDexterity()
    {
        var root = Path.Combine(Path.GetTempPath(), "rc-creature-" + Guid.NewGuid().ToString("N"));
        try
        {
            var package = Path.Combine(root, "first-party-001");
            Directory.CreateDirectory(Path.Combine(package, "native"));
            File.WriteAllText(Path.Combine(package, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            File.WriteAllText(Path.Combine(package, "native", "creatures.json"), """
                {"monsters":[
                    {"id":"monster.proficient","name":"Proficient","kind":"monster",
                     "definition":{"dex":9,"initiative":{"proficiency":2},"cr":{"cr":"10"}}},
                    {"id":"monster.normal","name":"Normal","kind":"monster",
                     "definition":{"dex":15,"cr":"1/4"}},
                    {"id":"monster.explicit","name":"Explicit","kind":"monster",
                     "definition":{"dex":15,"initiative":{"bonus":7},"cr":"1"}}
                ]}
                """);
            var snapshot = NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(root, "first-party-001"));
            var projector = new NativeCreatureProjector(snapshot);
            var proficient = projector.Project("monster.proficient");
            Assert.Equal("unsupported", proficient.Values["initiative"].Status);
            Assert.Null(proficient.Values["initiative"].Value);
            Assert.Equal("10", proficient.ChallengeRating);
            Assert.Contains(proficient.Issues, issue => issue.Code == "initiative-profile-unconverted");
            Assert.Equal(2, projector.Project("monster.normal").Values["initiative"].Value);
            Assert.Equal("1/4", projector.Project("monster.normal").ChallengeRating);
            Assert.Equal(7, projector.Project("monster.explicit").Values["initiative"].Value);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void SourceShapedCreatureIsPartialAndDoesNotInventTouchArmorClass()
    {
        var root = Path.Combine(Path.GetTempPath(), "rc-creature-" + Guid.NewGuid().ToString("N"));
        try
        {
            var package = Path.Combine(root, "first-party-001");
            Directory.CreateDirectory(Path.Combine(package, "native"));
            File.WriteAllText(Path.Combine(package, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            File.WriteAllText(Path.Combine(package, "native", "creature.json"), """
                {"id":"monster.sample","name":"Sample","kind":"monster",
                 "definition":{"str":8,"dex":15,"con":10,"int":10,"wis":8,"cha":8,
                               "ac":[15],"hp":{"average":10},"speed":{"walk":30},
                               "action":[{"name":"Source-only attack","entries":["Unconverted"]}]}}
                """);
            var snapshot = NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(root, "first-party-001"));
            var result = new NativeCreatureProjector(snapshot).Project("monster.sample");
            Assert.Equal("partial", result.Status);
            Assert.Equal(15, result.Values["ac.normal"].Value);
            Assert.Equal("unsupported", result.Values["ac.touch"].Status);
            Assert.Equal(10, result.Values["hit-points.maximum"].Value);
            Assert.Contains(result.Issues, issue => issue.Code == "source-conversion-required");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
