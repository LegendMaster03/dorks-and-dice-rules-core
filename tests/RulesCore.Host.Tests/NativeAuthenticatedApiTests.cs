using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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

public sealed class NativeAuthenticatedApiTests
{
    private const string Ruleset = "first-party-001";
    private const string Catalog = "/api/consumer/v1/rulesets";
    private const string Rules = Catalog + "/first-party-001/rules";
    private const string Definition = """
        [{"id":"monster.goblin","name":"Goblin","kind":"monster",
          "definition":{"hp":{"average":7},"ac":[15],"dex":14}},
         {"id":"condition.prone","name":"Prone","kind":"condition"}]
        """;

    private sealed class SiteHandler(string? source = null, string mode = "dorks-and-dice") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/tool-host/registrations/rules-core/api/introspect",
                request.RequestUri?.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("signed-in-site-ticket", request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    contractVersion = 1, toolKey = "rules-core", siteMode = mode,
                    user = new { id = "d66a7a7b-bc0f-4677-829d-0ccf40470d2f" },
                    delegatedFromToolKey = source,
                    privateTunnelSourceToolKey = (string?)null
                }), Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public WebApplication App { get; }
        public HttpClient Client { get; }
        public string Root { get; }
        public NativeCatalogStore Store => App.Services.GetRequiredService<NativeCatalogStore>();

        private Fixture(WebApplication app, HttpClient client, string root)
        {
            App = app;
            Client = client;
            Root = root;
        }

        public static async Task<Fixture> StartAsync(
            string surface = "PublicOnly", string? source = null, string mode = "dorks-and-dice")
        {
            var root = Path.Combine(Path.GetTempPath(), "native-consumer-" + Guid.NewGuid().ToString("N"));
            var package = Path.Combine(root, Ruleset);
            Directory.CreateDirectory(Path.Combine(package, "native"));
            File.WriteAllText(Path.Combine(package, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","workingRoot":"working/","runtimeContentRoot":"native/"}""");
            File.WriteAllText(Path.Combine(package, "native", "base.json"), Definition);
            var app = RulesCoreApplication.Create([], builder =>
            {
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RulesCore:ApiSurface"] = surface,
                    ["Rulesets:ContentRoot"] = root,
                    ["Rulesets:DefaultRuleset"] = Ruleset,
                    ["ToolHost:BaseUrl"] = "https://site.test"
                    // No public distribution approval: internal access must not depend on it.
                });
            }, services =>
            {
                services.AddHttpClient<CharacterSheetMechanicsTicketValidator>()
                    .ConfigurePrimaryHttpMessageHandler(() => new SiteHandler(source, mode));
            });
            await app.StartAsync();
            var server = app.Services.GetRequiredService<IServer>();
            var uri = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new Fixture(app, new HttpClient { BaseAddress = new Uri(uri) }, root);
        }

        public HttpRequestMessage Authorized(string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Dorks-Tool-Auth-Ticket", "signed-in-site-ticket");
            request.Headers.Add("X-Dorks-Tool-Auth-Introspection-Path",
                "/tool-host/registrations/rules-core/api/introspect");
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
    public async Task SignedInSiteUserCanReadLiveCatalogWithoutDistributionApproval()
    {
        await using var f = await Fixture.StartAsync();
        var publicListing = await f.Client.GetFromJsonAsync<JsonElement>("/api/rulesets");
        Assert.Empty(publicListing.GetProperty("rulesets").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound,
            (await f.Client.GetAsync("/api/rulesets/first-party-001/rules")).StatusCode);

        using var listingResponse = await f.Client.SendAsync(f.Authorized(Catalog));
        Assert.Equal(HttpStatusCode.OK, listingResponse.StatusCode);
        Assert.Contains("no-store", listingResponse.Headers.CacheControl!.ToString());
        var listing = await listingResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Ruleset, listing.GetProperty("rulesets")[0].GetProperty("id").GetString());
        Assert.Equal(f.Store.Snapshot!.Fingerprint,
            listing.GetProperty("rulesets")[0].GetProperty("fingerprint").GetString());

        using var searchResponse = await f.Client.SendAsync(
            f.Authorized(Rules + "?category=monster&q=goblin&limit=8&offset=0"));
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        var search = await searchResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("monster.goblin", search.GetProperty("rules")[0].GetProperty("id").GetString());

        using var detailResponse = await f.Client.SendAsync(
            f.Authorized(Rules + "/monster.goblin"));
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(7, detail.GetProperty("rule").GetProperty("definition")
            .GetProperty("definition").GetProperty("hp").GetProperty("average").GetInt32());
        Assert.DoesNotContain("sourcePath", detail.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ContentChangesDoNotRevokeSignedInCatalogAccess()
    {
        await using var f = await Fixture.StartAsync();
        var before = f.Store.Snapshot!.Fingerprint;
        File.WriteAllText(Path.Combine(f.Root, Ruleset, "native", "base.json"),
            Definition.Replace("Goblin", "Goblin Warrior", StringComparison.Ordinal));
        var updated = NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(f.Root, Ruleset));
        Assert.NotEqual(before, updated.Fingerprint);
        f.Store.Publish(updated);

        using var response = await f.Client.SendAsync(f.Authorized(Rules + "/monster.goblin"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Goblin Warrior", detail.GetProperty("rule").GetProperty("name").GetString());
        Assert.Equal(updated.Fingerprint, detail.GetProperty("fingerprint").GetString());
        var publicListing = await f.Client.GetFromJsonAsync<JsonElement>("/api/rulesets");
        Assert.Empty(publicListing.GetProperty("rulesets").EnumerateArray());
    }

    [Fact]
    public async Task AnonymousAndDelegatedTicketsCannotReadDirectConsumerCatalog()
    {
        await using var f = await Fixture.StartAsync();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await f.Client.GetAsync(Catalog)).StatusCode);
        using var spoofed = new HttpRequestMessage(HttpMethod.Get, Catalog);
        spoofed.Headers.Add("X-Dorks-Tool-Auth-Ticket", "signed-in-site-ticket");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await f.Client.SendAsync(spoofed)).StatusCode);

        await using var delegated = await Fixture.StartAsync(source: "character-sheet");
        using var denied = await delegated.Client.SendAsync(delegated.Authorized(Catalog));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        await using var wrongMode = await Fixture.StartAsync(mode: "another-mode");
        using var deniedMode = await wrongMode.Client.SendAsync(wrongMode.Authorized(Catalog));
        Assert.Equal(HttpStatusCode.Forbidden, deniedMode.StatusCode);
    }

    [Fact]
    public async Task PrivateIngressHasNoBrowserConsumerCatalog()
    {
        await using var f = await Fixture.StartAsync(surface: "PrivateOnly");
        using var response = await f.Client.SendAsync(f.Authorized(Catalog));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
