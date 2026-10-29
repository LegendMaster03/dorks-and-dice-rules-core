using System.Text.Json;
using RulesCore.Content;
using Xunit;

namespace RulesCore.Host.Tests;

public sealed class NativeCatalogTests
{
    [Fact]
    public void ApprovedPackageLoads35PlayableRecords()
    {
        var catalog = NativeCatalogLoader.Load(RepositoryLocation());
        Assert.Equal("first-party-001", catalog.RulesetId);
        Assert.Equal(1, catalog.SchemaVersion);
        Assert.Equal(35, catalog.Count);
        Assert.Equal(29, catalog.CountByCategory("CONDITION"));
        Assert.Equal(1, catalog.CountByCategory("leveled-condition"));
        Assert.Equal(4, catalog.CountByCategory("rule"));
        Assert.Equal(1, catalog.CountByCategory("ruleChapter"));
        Assert.Equal(64, catalog.Fingerprint.Length);
        Assert.True(catalog.TryGetById("condition.wet", out var wet));
        Assert.Equal("Wet", wet!.Name);
        Assert.Equal("native/conditions.json", wet.SourcePath);
        Assert.Equal(JsonValueKind.Array, wet.Definition.GetProperty("effects").ValueKind);
        Assert.True(catalog.TryGetById("rule.conditions", out var chapter));
        Assert.Equal("native/rules/conditions.json", chapter!.SourcePath);
        Assert.False(catalog.TryGetById("_meta", out _));
        Assert.False(catalog.TryGetById("size.fine", out _));
    }

    [Fact]
    public void SearchIsStableCaseInsensitiveAndBounded()
    {
        var catalog = NativeCatalogLoader.Load(RepositoryLocation());
        var all = catalog.Search(limit: 100);
        Assert.Equal(35, all.Total);
        Assert.Equal(
            all.Items.Select(x => x.Id).OrderBy(x => x, StringComparer.Ordinal),
            all.Items.Select(x => x.Id));
        Assert.Equal(catalog.Search("condition.", limit: 100).Items.Select(x => x.Id),
            catalog.Search("CONDITION.", limit: 100).Items.Select(x => x.Id));
        Assert.Equal(30, catalog.Search("CONDITION.").Total);
        Assert.Equal(29, catalog.Search(category: "condition").Total);
        Assert.Equal(1, catalog.Search(category: "LEVELED-CONDITION").Total);
        Assert.Equal(0, catalog.Search("never-present").Total);
        Assert.Equal(0, catalog.Search(category: "never-present").Total);
        var first = catalog.Search("condition", offset: 0, limit: 4);
        var second = catalog.Search("condition", offset: 4, limit: 4);
        Assert.Equal(4, first.Items.Length);
        Assert.Equal(4, second.Items.Length);
        Assert.Empty(first.Items.Select(e => e.Id).Intersect(second.Items.Select(e => e.Id)));
        Assert.Equal(first.Total, second.Total);
        Assert.Empty(catalog.Search(offset: 9999).Items);
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.Search(offset: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.Search(limit: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.Search(limit: 101));
    }

    [Fact]
    public void SemanticFingerprintIgnoresWhitespacePropertyOrderAndFileNames()
    {
        using var f = new Fixture();
        f.Native("a.json", """{"id":"rule.alpha","name":"Alpha","kind":"rule","nested":{"a":1,"b":2}}""");
        var initial = f.Load();
        File.Delete(Path.Combine(f.Location.NativeDirectory, "a.json"));
        f.Native("b.json", """
            {"nested":{"b":2,"a":1},"kind":"rule","name":"Alpha","id":"rule.alpha"}
            """);
        Assert.Equal(initial.Fingerprint, f.Load().Fingerprint);
        f.Native("b.json", """{"nested":{"a":2,"b":2},"kind":"rule","name":"Alpha","id":"rule.alpha"}""");
        Assert.NotEqual(initial.Fingerprint, f.Load().Fingerprint);
    }

    [Fact]
    public void WorkingIsNotReadAsCatalogOrFallback()
    {
        using var f = new Fixture();
        f.Native("live.json", """{"id":"rule.live","name":"Live","kind":"rule"}""");
        f.Working("draft.json", """{"id":"rule.draft","name":"Draft","kind":"rule"}""");
        var catalog = f.Load();
        Assert.Equal(1, catalog.Count);
        Assert.False(catalog.TryGetById("rule.draft", out _));
        f.Working("draft.json", "{ bad json");
        Assert.Equal(catalog.Fingerprint, f.Load().Fingerprint);
        File.Delete(Path.Combine(f.Location.NativeDirectory, "live.json"));
        Assert.Equal("content_unavailable", Failure(f).ReasonCode);
        Assert.True(catalog.TryGetById("rule.live", out _));
    }

    [Fact]
    public void MetadataIsExcludedFromPlayableRecords()
    {
        using var f = new Fixture();
        f.Native("catalog.json", """
            {"_meta":{"note":"not a rule"},"entries":[
              {"id":"meta.import","name":"Import","kind":"metadata"},
              {"id":"rule.live","name":"Live","kind":"rule"}]}
            """);
        Assert.Equal(1, f.Load().Count);
        Assert.False(f.Load().TryGetById("meta.import", out _));
    }

    [Theory]
    [InlineData("{ invalid")]
    [InlineData("""{"id":"rule.a","kind":"rule","name":"A","name":"B"}""")]
    [InlineData("""{"id":"rule.a","name":"A"}""")]
    [InlineData("""{"id":"rule.a","kind":"rule"}""")]
    [InlineData("""{"id":"../working","kind":"rule","name":"A"}""")]
    [InlineData("""{"entries":[{"id":"rule.a","kind":"rule","name":"A"},42]}""")]
    [InlineData("""{"entries":[{"id":"rule.a","kind":"rule","name":"A"}],"note":"invalid"}""")]
    public void MalformedOrStructurallyInvalidContentFails(string body)
    {
        using var f = new Fixture();
        f.Native("bad.json", body);
        Assert.Equal("invalid_native_json", Failure(f).ReasonCode);
    }

    [Fact]
    public void DuplicateIdsInDifferentFilesAndConsolidatedCatalogsFail()
    {
        using var f = new Fixture();
        f.Native("a.json", """{"id":"rule.same","name":"A","kind":"rule"}""");
        f.Native("b.json", """{"entries":[{"id":"rule.same","name":"B","kind":"rule"}]}""");
        Assert.Equal("duplicate_entity_id", Failure(f).ReasonCode);
        File.Delete(Path.Combine(f.Location.NativeDirectory, "a.json"));
        f.Native("b.json", """[{"id":"rule.same","name":"A","kind":"rule"},{"id":"rule.same","name":"B","kind":"rule"}]""");
        Assert.Equal("duplicate_entity_id", Failure(f).ReasonCode);
    }

    [Fact]
    public void InvalidManifestMissingContentAndConditionReferencesFail()
    {
        using var f = new Fixture();
        f.Native("live.json", """{"id":"rule.live","name":"Live","kind":"rule"}""");
        f.Manifest(Fixture.ValidManifest.Replace("first-party-001", "other-package"));
        Assert.Equal("invalid_manifest", Failure(f).ReasonCode);
        f.Manifest(Fixture.ValidManifest);
        f.Native("conditions.json", """
            {"conditions":[{"id":"condition.fake","name":"Fake","kind":"condition",
            "appliesConditions":["condition.nonexistent"]}]}
            """);
        Assert.Equal("invalid_native_json", Failure(f).ReasonCode);
        File.Delete(Path.Combine(f.Location.NativeDirectory, "conditions.json"));
        Directory.Delete(f.Location.NativeDirectory, recursive: true);
        Assert.Equal("content_unavailable", Failure(f).ReasonCode);
    }

    [Fact]
    public void ForgedNativePathAndFilesystemLinkEscapeFail()
    {
        using var f = new Fixture();
        f.Native("live.json", """{"id":"rule.live","name":"Live","kind":"rule"}""");
        var forged = f.Location with { NativeDirectory =
            Path.Combine(f.Location.ContentRoot, "first-party-001", "working") };
        Assert.Equal("unsafe_content_path",
            Assert.Throws<NativeCatalogLoadException>(() => NativeCatalogLoader.Load(forged)).ReasonCode);
        if (OperatingSystem.IsWindows()) return;
        f.Working("draft.json", """{"id":"rule.draft","name":"Draft","kind":"rule"}""");
        File.CreateSymbolicLink(
            Path.Combine(f.Location.NativeDirectory, "redirect.json"),
            Path.Combine(f.Location.ContentRoot, "first-party-001", "working", "draft.json"));
        Assert.Equal("unsafe_content_path", Failure(f).ReasonCode);
    }

    [Fact]
    public async Task ConcurrentReadsAndAtomicReplacementDoNotPublishPartialCatalogs()
    {
        using var f = new Fixture();
        f.Native("one.json", """{"id":"rule.one","name":"One","kind":"rule"}""");
        var first = f.Load();
        f.Native("two.json", """{"id":"rule.two","name":"Two","kind":"rule"}""");
        var second = f.Load();
        var store = new NativeCatalogStore();
        Assert.False(store.Current().IsReady);
        store.Publish(first);
        var readers = Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                var view = store.Snapshot;
                Assert.NotNull(view);
                Assert.True(view!.Count is 1 or 2);
                Assert.True(view.TryGetById("rule.one", out _));
                Assert.Equal(view.Count, view.Search(limit: 10).Total);
            }
        })).ToArray();
        for (var i = 0; i < 100; i++) store.Publish(i % 2 == 0 ? second : first);
        await Task.WhenAll(readers);
        store.Publish(second);
        f.Native("corrupt.json", "{ bad");
        Assert.Equal("invalid_native_json", Failure(f).ReasonCode);
        Assert.Same(second, store.Snapshot);
        store.MarkUnavailable("native_content_invalid");
        Assert.False(store.Current().IsReady);
        store.Publish(first);
        Assert.True(store.Current().IsReady);
    }

    private static NativeCatalogLoadException Failure(Fixture f) =>
        Assert.Throws<NativeCatalogLoadException>(() => f.Load());

    private static NativeRulesetLocation RepositoryLocation()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "RulesCore.slnx")))
                return NativeRulesetLayout.Resolve(
                    Path.Combine(dir.FullName, "data", "rulesets"), "first-party-001");
        }
        throw new InvalidOperationException("The test requires the repository checkout.");
    }

    private sealed class Fixture : IDisposable
    {
        public const string ValidManifest =
            """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","workingRoot":"working/","runtimeContentRoot":"native/"}""";

        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "rules-core-" + Guid.NewGuid().ToString("N"));
        public NativeRulesetLocation Location { get; }

        public Fixture()
        {
            Location = NativeRulesetLayout.Resolve(Path.Combine(_root, "rulesets"), "first-party-001");
            Directory.CreateDirectory(Location.NativeDirectory);
            Directory.CreateDirectory(Path.Combine(Location.ContentRoot, "first-party-001", "working"));
            Manifest(ValidManifest);
        }
        public void Manifest(string json) =>
            File.WriteAllText(Path.Combine(Location.ContentRoot, "first-party-001", "manifest.json"), json);
        public void Native(string path, string json)
        {
            var full = Path.Combine(Location.NativeDirectory, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, json);
        }
        public void Working(string path, string json)
        {
            var full = Path.Combine(Location.ContentRoot, "first-party-001", "working", path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, json);
        }
        public NativeCatalogSnapshot Load() => NativeCatalogLoader.Load(Location);
        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
