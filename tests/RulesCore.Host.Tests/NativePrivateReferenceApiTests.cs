using Microsoft.AspNetCore.Builder;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Host;
using RulesCore.Content;
using Xunit;

namespace RulesCore.Host.Tests;

public sealed class NativePrivateReferenceApiTests
{
    private sealed class SiteHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(handler(request));
    }

    private static HttpResponseMessage Identity(string? source = "rules-wiki", string mode = "dorks-and-dice",
        string toolKey = "rules-core", string? userId = "u-1") =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                contractVersion = 1,
                toolKey,
                siteMode = mode,
                user = new { id = userId },
                privateTunnelSourceToolKey = source
            }), Encoding.UTF8, "application/json")
        };

    private sealed class TestHost : IAsyncDisposable
    {
        public WebApplication App { get; }
        public HttpClient Client { get; }
        public string Root { get; }

        private TestHost(WebApplication app, HttpClient client, string root)
        {
            App = app;
            Client = client;
            Root = root;
        }

        public static async Task<TestHost> StartAsync(HttpMessageHandler site, string surface = "PrivateOnly",
            bool foreign = false, string? referenceAccess = null, bool publicApproval = true)
        {
            var root = Path.Combine(Path.GetTempPath(), "phase3a-" + Guid.NewGuid().ToString("N"));
            var package = Path.Combine(root, "first-party-001");
            Directory.CreateDirectory(Path.Combine(package, "native"));
            Directory.CreateDirectory(Path.Combine(package, "working"));
            File.WriteAllText(Path.Combine(package, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","workingRoot":"working/","runtimeContentRoot":"native/","name":"First-party test","reviewReference":"private-marker"}""");
            File.WriteAllText(Path.Combine(package, "native", "base.json"),
                """{"id":"rule.native","name":"Native Rule","kind":"rule","description":"Approved definition"}""");
            File.WriteAllText(Path.Combine(package, "working", "unfinished.json"),
                """{"id":"rule.unfinished","name":"Unfinished Rule","kind":"rule"}""");

            var additional = new Dictionary<string, string?>();
            if (foreign)
            {
                foreach (var (id, index) in new[] { ("dnd-5e", 0), ("dnd-5-5e", 1), ("fate-condensed", 2), ("restricted-test", 3) })
                {
                    var location = NativeRulesetLayout.Resolve(root, id);
                    Directory.CreateDirectory(location.NativeDirectory);
                    File.WriteAllText(Path.Combine(root, id, "manifest.json"),
                        $$"""{"id":"{{id}}","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","workingRoot":"working/","runtimeContentRoot":"native/","name":"{{id}} test","license":"CC-BY-4.0","licenseUrl":"https://creativecommons.org/licenses/by/4.0/","attribution":"{{id}} attribution","originalSources":["https://example.org/source","file:///private/file"],"reviewReference":"private-marker"}""");
                    File.WriteAllText(Path.Combine(location.NativeDirectory, "base.json"),
                        $$"""{"id":"rule.native","name":"{{id}} rule","kind":"rule"}""");
                    additional["Rulesets:AdditionalRulesets:" + index] = id;
                    if (index == 0 && publicApproval)
                    {
                        var prefix = "RulesCore:PublicDistribution:Packages:" + id + ":";
                        additional[prefix + "Enabled"] = "true";
                        additional[prefix + "ApprovedFingerprint"] = NativeCatalogLoader.Load(location).Fingerprint;
                        additional[prefix + "RightsReviewReference"] = "TEST-FIXTURE";
                        additional[prefix + "AttributionStatement"] = "Test fixture only";
                    }
                }
            }
            var app = RulesCoreApplication.Create([], builder =>
            {
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RulesCore:ApiSurface"] = surface,
                    ["Rulesets:ContentRoot"] = root,
                    ["Rulesets:DefaultRuleset"] = "first-party-001",
                    ["ToolHost:BaseUrl"] = "https://site.test"
                });
                if (referenceAccess is not null)
                    additional["RulesCore:AuthenticatedReferenceAccess:AdditionalRulesetsCsv"] = referenceAccess;
                builder.Configuration.AddInMemoryCollection(additional);
            }, services =>
            {
                if (surface == "PrivateOnly")
                    services.AddHttpClient<PrivateReferenceTicketValidator>()
                        .ConfigurePrimaryHttpMessageHandler(() => site);
                else
                    services.AddHttpClient<CharacterSheetMechanicsTicketValidator>()
                        .ConfigurePrimaryHttpMessageHandler(() => site);
            });
            await app.StartAsync();
            var server = app.Services.GetRequiredService<IServer>();
            var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new TestHost(app, new HttpClient { BaseAddress = new Uri(address) }, root);
        }

        public HttpRequestMessage TicketRequest(string path, string ticket = "fresh-site-ticket",
            string introspectionPath = PrivateReferenceTicketValidator.IntrospectionPath)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add(PrivateReferenceTicketValidator.TicketHeader, ticket);
            request.Headers.Add(PrivateReferenceTicketValidator.PathHeader, introspectionPath);
            return request;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
            Directory.Delete(Root, recursive: true);
        }
    }


    [Fact]
    public async Task OriginReferenceReadsUseActualExpandedSnapshotAndExactFingerprint()
    {
        await using var host = await TestHost.StartAsync(new SiteHandler(_ => Identity()), foreign: true);
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "RulesCore.slnx")))
            root = root.Parent;
        Assert.NotNull(root);
        var catalog = NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(
            Path.Combine(root!.FullName, "data", "rulesets"), "first-party-001"));
        host.App.Services.GetRequiredService<NativeCatalogRegistry>().GetStore("first-party-001").Publish(catalog);
        const string prefix = "/api/private/rulesets/first-party-001/origins/";
        async Task<JsonElement> Read(string path)
        {
            using var response = await host.Client.SendAsync(host.TicketRequest(
                prefix + path + "?fingerprint=" + Uri.EscapeDataString(catalog.Fingerprint)));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(catalog.Fingerprint, body.GetProperty("fingerprint").GetString());
            Assert.Equal("first-party-001", body.GetProperty("rulesetId").GetString());
            Assert.Equal(1, body.GetProperty("contractVersion").GetInt32());
            return body;
        }
        var tree = await Read("hierarchy/biological");
        Assert.True(tree.GetProperty("complete").GetBoolean());
        var dragonborn = tree.GetProperty("rows").EnumerateArray().Single(
            row => row.GetProperty("id").GetString() == "species.dragonborn");
        Assert.False(dragonborn.GetProperty("selectable").GetBoolean());
        var lineage = (await Read("source/ancestry.dragonborn.chromatic.red")).GetProperty("lineage");
        Assert.Equal(new[] { "species.dragonborn", "ancestry.dragonborn.chromatic",
            "ancestry.dragonborn.chromatic.red" },
            lineage.EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToArray());
        var occurrence = lineage[0].GetProperty("traitOccurrences")[2];
        Assert.Equal("trait.breath-weapon", occurrence.GetProperty("canonicalTraitId").GetString());
        Assert.Equal("/traits/2", occurrence.GetProperty("path").GetString());
        Assert.Equal("leaf", occurrence.GetProperty("trait").GetProperty("originBindings")
            .GetProperty("damageType").GetProperty("lookup").GetString());
        var wood = await Read("source/ancestry.elf.wood");
        Assert.False(wood.GetProperty("effectiveProjectionAvailable").GetBoolean());
        Assert.Equal(30, wood.GetProperty("lineage")[0].GetProperty("definition").GetProperty("walkingSpeedFeet").GetInt32());
        Assert.Equal(35, wood.GetProperty("lineage")[1].GetProperty("definition").GetProperty("walkingSpeedFeet").GetInt32());
        Assert.True(wood.GetProperty("lineage")[1].GetProperty("definition").GetProperty("overridesAncestorWalkingSpeed").GetBoolean());
        var culture = (await Read("source/upbringing.elf.wood")).GetProperty("lineage");
        Assert.Equal("upbringing.elf.general", culture[0].GetProperty("id").GetString());
        Assert.False(culture[0].GetProperty("definition").GetProperty("selectable").GetBoolean());
        var reverse = await Read("trait-sources/trait.low-light-vision");
        Assert.Contains(reverse.GetProperty("sources").EnumerateArray(),
            x => x.GetProperty("sourceId").GetString() == "species.elf" &&
                x.GetProperty("path").GetString() == "/traits/0");
        var component = await Read("component/construct-component.locomotion-unit");
        Assert.Equal("locomotion", component.GetProperty("componentCategory").GetString());
        Assert.Contains(component.GetProperty("recipes").EnumerateArray(),
            x => x.GetProperty("id").GetString() == "crafting-recipe.construct-component.locomotion-unit");
        Assert.Contains((await Read("trait-sources/trait.construct.ordinary-ground-movement"))
            .GetProperty("sources").EnumerateArray(), x =>
                x.GetProperty("sourceId").GetString() == "construct-component.locomotion-unit" &&
                x.GetProperty("relation").GetString() == "construct-operational-reference");
        using var stale = await host.Client.SendAsync(host.TicketRequest(prefix + "hierarchy/biological?fingerprint=stale"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var missing = await host.Client.SendAsync(host.TicketRequest(prefix + "source/species.missing?fingerprint=" + catalog.Fingerprint));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var foreign = await host.Client.SendAsync(host.TicketRequest(
            "/api/private/rulesets/dnd-5e/origins/hierarchy/biological?fingerprint=" + catalog.Fingerprint));
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await host.Client.GetAsync(prefix + "hierarchy/biological?fingerprint=" + catalog.Fingerprint)).StatusCode);
    }

    [Fact]
    public async Task PrivateReadsUseRedeemedIdentityAndOnlyNativeSnapshot()
    {
        var calls = 0;
        await using var host = await TestHost.StartAsync(new SiteHandler(request =>
        {
            calls++;
            Assert.Equal("/tool-host/registrations/rules-core/api/introspect", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("fresh-site-ticket", request.Headers.Authorization.Parameter);
            return Identity();
        }));
        using var listResponse = await host.Client.SendAsync(host.TicketRequest("/api/private/rulesets"));
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = (await listResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rulesets")[0];
        Assert.Equal(1, list.GetProperty("entityCount").GetInt32());
        var metadata = list.GetProperty("metadata");
        Assert.Equal("first-party-001", metadata.GetProperty("rulesetId").GetString());
        Assert.Equal("First-party test", metadata.GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Null, metadata.GetProperty("licenseIdentifier").ValueKind);
        Assert.DoesNotContain("private-marker", list.ToString());
        var fingerprint = list.GetProperty("fingerprint").GetString();

        using var browseResponse = await host.Client.SendAsync(host.TicketRequest(
            "/api/private/rulesets/first-party-001/rules?q=NATIVE&category=rule&limit=10&offset=0"));
        Assert.Equal(HttpStatusCode.OK, browseResponse.StatusCode);
        var browse = await browseResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, browse.GetProperty("total").GetInt32());
        Assert.Equal(fingerprint, browse.GetProperty("fingerprint").GetString());
        Assert.Equal("rule.native", browse.GetProperty("rules")[0].GetProperty("id").GetString());
        Assert.Equal("rule", browse.GetProperty("categoryFacets")[0].GetProperty("kind").GetString());
        Assert.DoesNotContain("unfinished", browse.ToString(), StringComparison.OrdinalIgnoreCase);

        using var detailResponse = await host.Client.SendAsync(host.TicketRequest(
            "/api/private/rulesets/first-party-001/rules/rule.native"));
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(fingerprint, detail.GetProperty("fingerprint").GetString());
        Assert.Equal("Approved definition", detail.GetProperty("rule").GetProperty("definition")
            .GetProperty("description").GetString());
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task WikiReadsOnlyFirstPartyAndIndependentlyApprovedForeignPackages()
    {
        await using var host = await TestHost.StartAsync(new SiteHandler(_ => Identity()), foreign: true);
        using var listing = await host.Client.SendAsync(host.TicketRequest("/api/private/rulesets"));
        Assert.Equal(HttpStatusCode.OK, listing.StatusCode);
        var body = await listing.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "dnd-5e", "first-party-001" },
            body.GetProperty("rulesets").EnumerateArray().Select(item => item.GetProperty("id").GetString()).ToArray());
        var foreignMetadata = body.GetProperty("rulesets")[0].GetProperty("metadata");
        Assert.Equal("dnd-5e", foreignMetadata.GetProperty("rulesetId").GetString());
        Assert.Equal("dnd-5e attribution", foreignMetadata.GetProperty("attributionStatement").GetString());
        Assert.Equal("CC-BY-4.0", foreignMetadata.GetProperty("licenseIdentifier").GetString());
        Assert.Equal("https://creativecommons.org/licenses/by/4.0/", foreignMetadata.GetProperty("licenseUrl").GetString());
        Assert.Single(foreignMetadata.GetProperty("originalSources").EnumerateArray());
        Assert.DoesNotContain("private-marker", body.ToString());
        Assert.DoesNotContain("dnd-5-5e", body.ToString());
        Assert.DoesNotContain("TEST-FIXTURE", body.ToString());
        using var detail = await host.Client.SendAsync(host.TicketRequest("/api/private/rulesets/dnd-5e/rules/rule.native"));
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var rule = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("dnd-5e rule", rule.GetProperty("rule").GetProperty("name").GetString());
        using var denied = await host.Client.SendAsync(host.TicketRequest("/api/private/rulesets/dnd-5-5e/rules/rule.native"));
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    [Theory]
    [InlineData("PrivateOnly", "/api/private/rulesets")]
    [InlineData("PublicOnly", "/api/consumer/v1/rulesets")]
    public async Task ExplicitAuthenticatedReferenceAccessDoesNotApprovePublication(string surface, string route)
    {
        await using var host = await TestHost.StartAsync(
            new SiteHandler(_ => Identity(source: surface == "PrivateOnly" ? "rules-wiki" : null)),
            surface, foreign: true, referenceAccess: "dnd-5e,dnd-5-5e,fate-condensed", publicApproval: false);
        using var listing = await host.Client.SendAsync(host.TicketRequest(route));
        Assert.Equal(HttpStatusCode.OK, listing.StatusCode);
        var body = await listing.Content.ReadFromJsonAsync<JsonElement>();
        var descriptors = body.GetProperty("rulesets").EnumerateArray().ToArray();
        Assert.Equal(new[] { "dnd-5-5e", "dnd-5e", "fate-condensed", "first-party-001" },
            descriptors.Select(item => item.GetProperty("id").GetString()).ToArray());
        foreach (var descriptor in descriptors)
        {
            var id = descriptor.GetProperty("id").GetString();
            var fingerprint = descriptor.GetProperty("fingerprint").GetString();
            using var page = await host.Client.SendAsync(host.TicketRequest($"{route}/{id}/rules"));
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            var search = await page.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(id, search.GetProperty("rulesetId").GetString());
            Assert.Equal(fingerprint, search.GetProperty("fingerprint").GetString());
            using var result = await host.Client.SendAsync(host.TicketRequest($"{route}/{id}/rules/rule.native"));
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var detail = await result.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(id, detail.GetProperty("rulesetId").GetString());
            Assert.Equal(fingerprint, detail.GetProperty("fingerprint").GetString());
            Assert.Equal(id == "first-party-001" ? "Native Rule" : $"{id} rule",
                detail.GetProperty("rule").GetProperty("name").GetString());
            Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync($"{route}/{id}/rules")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync($"{route}/{id}/rules/rule.native")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/rulesets/{id}/rules")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/rulesets/{id}/rules/rule.native")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync(route)).StatusCode);
        Assert.True(host.App.Services.GetRequiredService<NativeCatalogRegistry>().GetStore("restricted-test").Current().IsReady);
        foreach (var id in new[] { "restricted-test", "unknown" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.SendAsync(host.TicketRequest($"{route}/{id}/rules"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.SendAsync(host.TicketRequest($"{route}/{id}/rules/rule.native"))).StatusCode);
        }
        if (surface == "PublicOnly")
        {
            var anonymous = await host.Client.GetFromJsonAsync<JsonElement>("/api/rulesets");
            Assert.Empty(anonymous.GetProperty("rulesets").EnumerateArray());
        }
        else
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/api/rulesets")).StatusCode);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("dnd-5e,")]
    [InlineData("dnd-5e,dnd-5e")]
    [InlineData("first-party-001")]
    [InlineData("DND-5E")]
    public void MalformedReferenceAllowlistFailsClosed(string csv)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RulesCore:AuthenticatedReferenceAccess:AdditionalRulesetsCsv"] = csv
        }).Build();
        Assert.Throws<ArgumentException>(() => new NativeReferenceAccessPolicy(configuration));
    }

    [Theory]
    [InlineData("different-tool", "dorks-and-dice", HttpStatusCode.Forbidden)]
    [InlineData("", "dorks-and-dice", HttpStatusCode.Forbidden)]
    [InlineData("rules-wiki", "development", HttpStatusCode.Forbidden)]
    public async Task IncorrectPrivateProvenanceOrModeIsDenied(string source, string mode, HttpStatusCode expected)
    {
        await using var host = await TestHost.StartAsync(new SiteHandler(_ => Identity(source, mode)));
        using var response = await host.Client.SendAsync(host.TicketRequest("/api/private/rulesets"));
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task BadAndMissingTicketsFailClosedAndHealthStaysAvailable()
    {
        await using var host = await TestHost.StartAsync(new SiteHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/private/rulesets")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await host.Client.SendAsync(host.TicketRequest("/api/private/rulesets"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.SendAsync(host.TicketRequest(
            "/api/private/rulesets", introspectionPath: "/tool-host/registrations/other/api/introspect"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/ready")).StatusCode);
    }

    [Fact]
    public async Task PublicIngressDoesNotMapPrivateApi()
    {
        await using var host = await TestHost.StartAsync(new SiteHandler(_ => Identity()), "PublicOnly");
        Assert.Equal(HttpStatusCode.NotFound,
            (await host.Client.SendAsync(host.TicketRequest("/api/private/rulesets"))).StatusCode);
    }

    [Theory]
    [InlineData("/api/private/rulesets/first-party-001/rules?limit=101", HttpStatusCode.BadRequest)]
    [InlineData("/api/private/rulesets/first-party-001/rules?limit=0", HttpStatusCode.BadRequest)]
    [InlineData("/api/private/rulesets/first-party-001/rules?offset=-1", HttpStatusCode.BadRequest)]
    [InlineData("/api/private/rulesets/first-party-001/rules?limit=2&limit=3", HttpStatusCode.BadRequest)]
    [InlineData("/api/private/rulesets/first-party-001/rules/rule.missing", HttpStatusCode.NotFound)]
    [InlineData("/api/private/rulesets/unknown/rules", HttpStatusCode.NotFound)]
    public async Task InvalidOrMissingRecordsAreBounded(string path, HttpStatusCode expected)
    {
        await using var host = await TestHost.StartAsync(new SiteHandler(_ => Identity()));
        Assert.Equal(expected, (await host.Client.SendAsync(host.TicketRequest(path))).StatusCode);
    }
    [Fact]
    public async Task ForgedSourceHeaderCanNotReplaceSiteIssuedProvenance()
    {
        await using var host = await TestHost.StartAsync(new SiteHandler(_ => Identity(source: "other-tool")));
        using var request = host.TicketRequest("/api/private/rulesets");
        request.Headers.TryAddWithoutValidation("X-Dorks-Tool-Private-Tunnel-Source", "rules-wiki");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task WrongTargetOrMissingStableUserIsDenied()
    {
        await using var wrongTarget = await TestHost.StartAsync(new SiteHandler(_ => Identity(toolKey: "character-sheet")));
        Assert.Equal(HttpStatusCode.Unauthorized, (await wrongTarget.Client.SendAsync(
            wrongTarget.TicketRequest("/api/private/rulesets"))).StatusCode);
        await using var missingUser = await TestHost.StartAsync(new SiteHandler(_ => Identity(userId: null)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await missingUser.Client.SendAsync(
            missingUser.TicketRequest("/api/private/rulesets"))).StatusCode);
    }

    [Fact]
    public async Task MissingCatalogFailsClosedAfterAuthentication()
    {
        await using var host = await TestHost.StartAsync(new SiteHandler(_ => Identity()));
        host.App.Services.GetRequiredService<RulesCore.Content.NativeCatalogStore>()
            .MarkUnavailable("native_content_invalid");
        using var response = await host.Client.SendAsync(host.TicketRequest("/api/private/rulesets"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain(host.Root, await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await host.Client.GetAsync("/ready")).StatusCode);
    }

}
