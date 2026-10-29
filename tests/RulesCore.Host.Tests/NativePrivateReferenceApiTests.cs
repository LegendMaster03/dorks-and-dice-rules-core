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
using Xunit;

namespace RulesCore.Host.Tests;

public sealed class NativePrivateReferenceApiTests
{
    private sealed class SiteHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(handler(request));
    }

    private static HttpResponseMessage Identity(string source = "rules-wiki", string mode = "dorks-and-dice",
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

        public static async Task<TestHost> StartAsync(HttpMessageHandler site, string surface = "PrivateOnly")
        {
            var root = Path.Combine(Path.GetTempPath(), "phase3a-" + Guid.NewGuid().ToString("N"));
            var package = Path.Combine(root, "first-party-001");
            Directory.CreateDirectory(Path.Combine(package, "native"));
            Directory.CreateDirectory(Path.Combine(package, "working"));
            File.WriteAllText(Path.Combine(package, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","workingRoot":"working/","runtimeContentRoot":"native/"}""");
            File.WriteAllText(Path.Combine(package, "native", "base.json"),
                """{"id":"rule.native","name":"Native Rule","kind":"rule","description":"Approved definition"}""");
            File.WriteAllText(Path.Combine(package, "working", "unfinished.json"),
                """{"id":"rule.unfinished","name":"Unfinished Rule","kind":"rule"}""");

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
            }, services =>
            {
                if (surface == "PrivateOnly")
                    services.AddHttpClient<PrivateReferenceTicketValidator>()
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
