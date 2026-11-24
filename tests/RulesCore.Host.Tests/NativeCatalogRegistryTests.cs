using RulesCore.Content;
using Xunit;

namespace RulesCore.Host.Tests;

public sealed class NativeCatalogRegistryTests
{
    [Fact]
    public void IndependentPackagesPreserveIdentityFingerprintAndReadiness()
    {
        var root = Path.Combine(Path.GetTempPath(), "rules-core-" + Guid.NewGuid().ToString("N"));
        try
        {
            var ids = new[] { "first-party-001", "dnd-5e", "dnd-5-5e", "fate-condensed" };
            var defaultStore = new NativeCatalogStore();
            var registry = new NativeCatalogRegistry(ids[0], defaultStore, ids.Skip(1));
            foreach (var id in ids)
            {
                var location = NativeRulesetLayout.Resolve(root, id);
                Directory.CreateDirectory(location.NativeDirectory);
                File.WriteAllText(Path.Combine(root, id, "manifest.json"),
                    $$"""{"id":"{{id}}","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","workingRoot":"working/","runtimeContentRoot":"native/"}""");
                File.WriteAllText(Path.Combine(location.NativeDirectory, "rules.json"),
                    """{"id":"rule.shared","kind":"rule","name":"Shared"}""");
                registry.GetStore(id).Publish(NativeCatalogLoader.Load(location));
            }

            Assert.Same(defaultStore, registry.GetStore(ids[0]));
            Assert.Equal(ids.Length, registry.RulesetIds.Count);
            var fingerprints = ids.Select(id => registry.GetStore(id).Snapshot!.Fingerprint).ToArray();
            Assert.Equal(ids.Length, fingerprints.Distinct().Count());
            foreach (var id in ids)
            {
                var store = registry.GetStore(id);
                Assert.True(store.Current().IsReady);
                Assert.True(store.Snapshot!.TryGetById("rule.shared", out var entity));
                Assert.Equal(id, entity!.RulesetId);
                Assert.Equal(0, store.Snapshot.CountByCategory("class"));
            }

            var changed = NativeRulesetLayout.Resolve(root, ids[1]);
            File.WriteAllText(Path.Combine(changed.NativeDirectory, "rules.json"),
                """{"id":"rule.shared","kind":"rule","name":"Changed"}""");
            registry.GetStore(ids[1]).Publish(NativeCatalogLoader.Load(changed));
            Assert.NotEqual(fingerprints[1], registry.GetStore(ids[1]).Snapshot!.Fingerprint);
            registry.GetStore(ids[1]).MarkUnavailable("invalid_native_json");
            Assert.Null(registry.GetStore(ids[1]).Snapshot);
            Assert.False(registry.GetStore(ids[1]).Current().IsReady);
            foreach (var i in new[] { 0, 2, 3 })
            {
                Assert.True(registry.GetStore(ids[i]).Current().IsReady);
                Assert.Equal(fingerprints[i], registry.GetStore(ids[i]).Snapshot!.Fingerprint);
            }
            Assert.False(registry.TryGetStore("unknown", out _));
            Assert.Throws<KeyNotFoundException>(() => registry.GetStore("unknown"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RepositoryPackagesLoadSeparately()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RulesCore.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var root = Path.Combine(dir!.FullName, "data", "rulesets");
        string[] ids = ["first-party-001", "dnd-5e", "dnd-5-5e", "fate-condensed"];
        var registry = new NativeCatalogRegistry(ids[0], new NativeCatalogStore(ids[0]), ids.Skip(1));
        foreach (var id in ids)
            registry.GetStore(id).Publish(NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(root, id)));
        Assert.All(ids, id => Assert.True(registry.GetStore(id).Current().IsReady));
        Assert.Equal(4, ids.Select(id => registry.GetStore(id).Snapshot!.Fingerprint).Distinct().Count());
        var old = registry.GetStore(ids[1]).Snapshot!;
        var modern = registry.GetStore(ids[2]).Snapshot!;
        Assert.True(old.TryGetById("condition.exhaustion", out var a));
        Assert.True(modern.TryGetById("condition.exhaustion", out var b));
        Assert.NotEqual(a!.Definition.GetRawText(), b!.Definition.GetRawText());
        Assert.Equal(401, old.CountByCategory("rulebook-page"));
        Assert.Equal(360, modern.CountByCategory("rulebook-page"));
        foreach (var catalog in new[] { old, modern })
        {
            Assert.Equal(18, catalog.CountByCategory("skill"));
            Assert.Equal(15, catalog.CountByCategory("condition"));
        }
        Assert.Throws<ArgumentException>(() => registry.GetStore(ids[1]).Publish(modern));
        Assert.Equal(127, registry.GetStore(ids[3]).Snapshot!.Count);
        Assert.Equal(0, registry.GetStore(ids[3]).Snapshot!.CountByCategory("class"));
    }

    [Theory]
    [InlineData("rule.target", true)]
    [InlineData("rule.missing", false)]
    public void ExplicitReferenceNeverResolvesAcrossPackages(string target, bool valid)
    {
        var root = Path.Combine(Path.GetTempPath(), "rules-core-ref-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var id in new[] { "package-a", "package-b" })
            {
                var location = NativeRulesetLayout.Resolve(root, id);
                Directory.CreateDirectory(location.NativeDirectory);
                File.WriteAllText(Path.Combine(root, id, "manifest.json"),
                    $$"""{"id":"{{id}}","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","workingRoot":"working/","runtimeContentRoot":"native/"}""");
                File.WriteAllText(Path.Combine(location.NativeDirectory, "target.json"),
                    id == "package-a"
                        ? """{"id":"rule.target","kind":"rule","name":"Local"}"""
                        : """{"id":"rule.missing","kind":"rule","name":"Foreign"}""");
            }
            var first = NativeRulesetLayout.Resolve(root, "package-a");
            File.WriteAllText(Path.Combine(first.NativeDirectory, "source.json"),
                $$"""{"id":"rule.source","kind":"rule","name":"Source","catalogReferences":["{{target}}"]}""");
            if (valid)
                Assert.Equal(2, NativeCatalogLoader.Load(first).Count);
            else
                Assert.Throws<NativeCatalogLoadException>(() => NativeCatalogLoader.Load(first));
            Assert.Equal(1, NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(root, "package-b")).Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DeclaredMarkdownSourceRejectsMissingParentWithoutImposingRulesOnOtherEntities(bool valid)
    {
        var root = Path.Combine(Path.GetTempPath(), "rules-core-md-" + Guid.NewGuid().ToString("N"));
        const string commit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        try
        {
            var location = NativeRulesetLayout.Resolve(root, "markdown-test");
            Directory.CreateDirectory(location.NativeDirectory);
            File.WriteAllText(Path.Combine(root, "markdown-test", "manifest.json"),
                """{"id":"markdown-test","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","workingRoot":"working/","runtimeContentRoot":"native/","markdownImport":{"sectionRecords":2,"commit":"COMMIT_PLACEHOLDER"}}""".Replace("COMMIT_PLACEHOLDER", commit));
            File.WriteAllText(Path.Combine(location.NativeDirectory, "root.json"),
                """{"id":"section.root","kind":"chapter","name":"Root","headingLevel":1,"parentId":null,"markdown":"# Root","source":{"commit":"COMMIT_PLACEHOLDER"}}""".Replace("COMMIT_PLACEHOLDER", commit));
            var parent = valid ? "section.root" : "section.missing";
            File.WriteAllText(Path.Combine(location.NativeDirectory, "child.json"),
                """{"id":"section.root.child","kind":"rule","name":"Child","headingLevel":2,"parentId":"PARENT_PLACEHOLDER","markdown":"## Child","source":{"commit":"COMMIT_PLACEHOLDER"}}""".Replace("COMMIT_PLACEHOLDER", commit).Replace("PARENT_PLACEHOLDER", parent));
            File.WriteAllText(Path.Combine(location.NativeDirectory, "skill.json"),
                """{"id":"skill.example","kind":"skill","name":"Example"}""");
            if (valid)
                Assert.Equal(3, NativeCatalogLoader.Load(location).Count);
            else
                Assert.Throws<NativeCatalogLoadException>(() => NativeCatalogLoader.Load(location));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DuplicateRegistrationIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new NativeCatalogRegistry(
            "first-party-001", new NativeCatalogStore(), ["first-party-001"]));
    }
}
