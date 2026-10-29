using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Content;
using RulesCore.Host;
using Xunit;

namespace RulesCore.Host.Tests;

public sealed class NativePublicApiTests
{
    private const string Ruleset = "first-party-001";
    private const string Base = "/api/rulesets/first-party-001/rules";
    private const string NativeDefinition = """
        [
          {"id":"rule.initiative","name":"Initiative","kind":"rule","description":"Native foundational rule"},
          {"id":"condition.prone","name":"Prone","kind":"condition","description":"Native condition"},
          {"id":"rule.defenses","name":"Defenses","kind":"rule","description":"Approved defense rules"},
          {"id":"condition.blinded","name":"Blinded","kind":"condition","description":"Approved condition"}
        ]
        """;

    private sealed class Fixture : IAsyncDisposable
    {
        public WebApplication App { get; }
        public HttpClient Client { get; }
        public string Root { get; }
        public string Fingerprint { get; }
        public NativeCatalogStore Store => App.Services.GetRequiredService<NativeCatalogStore>();

        private Fixture(WebApplication app, HttpClient client, string root, string fingerprint)
        {
            App = app;
            Client = client;
            Root = root;
            Fingerprint = fingerprint;
        }

        public static async Task<Fixture> StartAsync(bool approved = true, string surface = "PublicOnly",
            bool invalidNative = false, bool wrongFingerprint = false, bool missingReview = false)
        {
            var root = Path.Combine(Path.GetTempPath(), "phase3b-" + Guid.NewGuid().ToString("N"));
            var package = Path.Combine(root, Ruleset);
            Directory.CreateDirectory(Path.Combine(package, "native"));
            Directory.CreateDirectory(Path.Combine(package, "working"));
            File.WriteAllText(Path.Combine(package, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","workingRoot":"working/","runtimeContentRoot":"native/"}""");
            File.WriteAllText(Path.Combine(package, "native", "base.json"),
                invalidNative ? "{ broken" : NativeDefinition);
            // Even malformed working content and a matching native ID are never parsed.
            File.WriteAllText(Path.Combine(package, "working", "malformed.json"), "{ broken");
            File.WriteAllText(Path.Combine(package, "working", "shadow.json"),
                """{"id":"rule.initiative","name":"Unapproved Working Rule","kind":"rule"}""");
            var fingerprint = invalidNative ? new string('a', 64) :
                NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(root, Ruleset)).Fingerprint;
            var app = RulesCoreApplication.Create([], builder =>
            {
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RulesCore:ApiSurface"] = surface,
                    ["Rulesets:ContentRoot"] = root,
                    ["Rulesets:DefaultRuleset"] = Ruleset,
                    ["ToolHost:BaseUrl"] = "https://site.test",
                    ["RulesCore:PublicDistribution:Packages:first-party-001:Enabled"] = approved ? "true" : "false",
                    ["RulesCore:PublicDistribution:Packages:first-party-001:ApprovedFingerprint"] =
                        wrongFingerprint ? new string('f', 64) : fingerprint,
                    ["RulesCore:PublicDistribution:Packages:first-party-001:RightsReviewReference"] =
                        missingReview ? "" : "TEST-FIXTURE-NOT-PRODUCTION-APPROVAL",
                    ["RulesCore:PublicDistribution:Packages:first-party-001:AttributionStatement"] =
                        "Original test fixture; not a license assertion for the production catalog"
                });
            });
            await app.StartAsync();
            var server = app.Services.GetRequiredService<IServer>();
            var uri = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new Fixture(app, new HttpClient { BaseAddress = new Uri(uri) }, root, fingerprint);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
            Directory.Delete(Root, recursive: true);
        }
    }

    [Fact]
    public async Task ApprovedSnapshotListsSearchesAndReturnsNativeDefinition()
    {
        await using var f = await Fixture.StartAsync();
        using var listResponse = await f.Client.GetAsync("/api/rulesets");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Contains("no-store", listResponse.Headers.CacheControl!.ToString());
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var ruleset = list.GetProperty("rulesets")[0];
        Assert.Equal(Ruleset, ruleset.GetProperty("id").GetString());
        Assert.Equal(4, ruleset.GetProperty("entityCount").GetInt32());
        Assert.Equal(f.Fingerprint, ruleset.GetProperty("fingerprint").GetString());
        Assert.True(ruleset.GetProperty("capabilities").GetProperty("read").GetBoolean());
        Assert.False(ruleset.GetProperty("capabilities").GetProperty("write").GetBoolean());

        var all = await f.Client.GetFromJsonAsync<JsonElement>(Base);
        Assert.Equal(f.Fingerprint, all.GetProperty("fingerprint").GetString());
        Assert.Equal(4, all.GetProperty("total").GetInt32());
        Assert.Equal(50, all.GetProperty("limit").GetInt32());
        Assert.Equal(new[] { "condition.blinded", "condition.prone", "rule.defenses", "rule.initiative" },
            all.GetProperty("rules").EnumerateArray()
                .Select(r => r.GetProperty("id").GetString()).ToArray());
        Assert.Equal(new[] { "condition", "rule" }, all.GetProperty("categoryFacets")
            .EnumerateArray().Select(r => r.GetProperty("kind").GetString()).ToArray());
        Assert.All(all.GetProperty("categoryFacets").EnumerateArray(),
            facet => Assert.Equal(2, facet.GetProperty("count").GetInt32()));

        var page = await f.Client.GetFromJsonAsync<JsonElement>(Base + "?q=BLIND&category=condition&limit=1");
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        Assert.Equal("condition.blinded", page.GetProperty("rules")[0].GetProperty("id").GetString());

        var detail = await f.Client.GetFromJsonAsync<JsonElement>(Base + "/rule.defenses");
        Assert.Equal(f.Fingerprint, detail.GetProperty("fingerprint").GetString());
        Assert.Equal("Defenses", detail.GetProperty("rule").GetProperty("name").GetString());
        Assert.Equal("rule", detail.GetProperty("rule").GetProperty("kind").GetString());
        Assert.Equal("Approved defense rules", detail.GetProperty("rule")
            .GetProperty("definition").GetProperty("description").GetString());
        Assert.DoesNotContain("SourcePath", detail.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("shadow", all.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(f.Root, detail.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PaginationUnknownCategoriesAndSearchAreDeterministic()
    {
        await using var f = await Fixture.StartAsync();
        var first = await f.Client.GetFromJsonAsync<JsonElement>(Base + "?offset=1&limit=2");
        Assert.Equal(4, first.GetProperty("total").GetInt32());
        Assert.Equal(new[] { "condition.prone", "rule.defenses" },
            first.GetProperty("rules").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToArray());
        var beyond = await f.Client.GetFromJsonAsync<JsonElement>(Base + "?offset=1000000&limit=100");
        Assert.Equal(4, beyond.GetProperty("total").GetInt32());
        Assert.Empty(beyond.GetProperty("rules").EnumerateArray());
        var missingCategory = await f.Client.GetFromJsonAsync<JsonElement>(Base + "?category=monster");
        Assert.Equal(0, missingCategory.GetProperty("total").GetInt32());
        Assert.Equal(2, missingCategory.GetProperty("categoryFacets").GetArrayLength());
        var byId = await f.Client.GetFromJsonAsync<JsonElement>(Base + "?q=RULE.DEFENSES");
        Assert.Equal("rule.defenses", byId.GetProperty("rules")[0].GetProperty("id").GetString());
    }

    [Theory]
    [InlineData("?limit=0")]
    [InlineData("?limit=101")]
    [InlineData("?limit=-1")]
    [InlineData("?limit=1.5")]
    [InlineData("?offset=-1")]
    [InlineData("?offset=1000001")]
    [InlineData("?offset=99999999999999")]
    [InlineData("?limit=1&limit=2")]
    [InlineData("?q=a&q=b")]
    [InlineData("?category=condition&category=rule")]
    [InlineData("?unknown=1")]
    [InlineData("?category=a.b")]
    [InlineData("?limit=")]
    public async Task RejectsInvalidAndDuplicateQueryParameters(string query)
    {
        await using var f = await Fixture.StartAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.GetAsync(Base + query)).StatusCode);
    }

    [Fact]
    public async Task SearchTermAndRouteIdsAreBounded()
    {
        await using var f = await Fixture.StartAsync();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await f.Client.GetAsync(Base + "?q=" + new string('a', 201))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await f.Client.GetAsync("/api/rulesets/Bad-ID/rules")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await f.Client.GetAsync(Base + "/BAD.ID")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await f.Client.GetAsync("/api/rulesets/another-ruleset/rules")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await f.Client.GetAsync(Base + "/rule.unknown")).StatusCode);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task DistributionIsClosedUnlessPackageAndFingerprintAndRightsEvidenceMatch(
        bool approved, bool wrongFingerprint, bool missingReview)
    {
        await using var f = await Fixture.StartAsync(approved, wrongFingerprint: wrongFingerprint,
            missingReview: missingReview);
        var list = await f.Client.GetFromJsonAsync<JsonElement>("/api/rulesets");
        Assert.Empty(list.GetProperty("rulesets").EnumerateArray());
        using var search = await f.Client.GetAsync(Base + "?category=condition");
        using var detail = await f.Client.GetAsync(Base + "/condition.blinded");
        Assert.Equal(HttpStatusCode.NotFound, search.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(await search.Content.ReadAsStringAsync(), await detail.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SurfaceIsolationIsIndependentOfHeadersAndTickets()
    {
        await using var publicHost = await Fixture.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/rulesets");
        request.Headers.TryAddWithoutValidation("X-Dorks-Tool-Auth-Ticket", "spoofed");
        request.Headers.TryAddWithoutValidation("X-Dorks-Tool-Private-Tunnel-Source", "rules-wiki");
        Assert.Equal(HttpStatusCode.OK, (await publicHost.Client.SendAsync(request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await publicHost.Client.GetAsync("/api/private/rulesets")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await publicHost.Client.PostAsync(Base, new StringContent("{}"))).StatusCode);

        await using var privateHost = await Fixture.StartAsync(surface: "PrivateOnly");
        Assert.Equal(HttpStatusCode.NotFound,
            (await privateHost.Client.GetAsync("/api/rulesets")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await privateHost.Client.GetAsync(Base + "/condition.blinded")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await privateHost.Client.GetAsync("/api/private/rulesets")).StatusCode);
    }

    [Fact]
    public async Task MissingOrInvalidSnapshotReturns503AndDoesNotRevealServerPaths()
    {
        await using var f = await Fixture.StartAsync(invalidNative: true);
        var result = await f.Client.GetAsync("/api/rulesets");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
        Assert.DoesNotContain(f.Root, await result.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await f.Client.GetAsync(Base + "/condition.blinded")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await f.Client.GetAsync("/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task RepublishIsAtomicAndContentChangesRevokePreviousFingerprintApproval()
    {
        await using var f = await Fixture.StartAsync();
        var old = f.Store.Snapshot!;
        f.Store.MarkUnavailable("test_unavailable");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await f.Client.GetAsync("/api/rulesets")).StatusCode);
        f.Store.Publish(old);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync(Base)).StatusCode);

        File.WriteAllText(Path.Combine(f.Root, Ruleset, "native", "base.json"),
            NativeDefinition.Replace("Native foundational rule", "Changed foundational rule",
                StringComparison.Ordinal));
        var updated = NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(f.Root, Ruleset));
        Assert.NotEqual(f.Fingerprint, updated.Fingerprint);
        f.Store.Publish(updated);
        var listed = await f.Client.GetFromJsonAsync<JsonElement>("/api/rulesets");
        Assert.Empty(listed.GetProperty("rulesets").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound,
            (await f.Client.GetAsync(Base + "/rule.initiative")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync("/ready")).StatusCode);
        f.Store.Publish(old);
        var responses = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => f.Client.GetAsync(Base)));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        foreach (var response in responses)
        {
            using (response)
            {
                var page = await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal(f.Fingerprint, page.GetProperty("fingerprint").GetString());
                Assert.Equal(4, page.GetProperty("total").GetInt32());
            }
        }
    }
}
