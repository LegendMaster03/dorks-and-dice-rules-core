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
    public void DuplicateRegistrationIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new NativeCatalogRegistry(
            "first-party-001", new NativeCatalogStore(), ["first-party-001"]));
    }
}
