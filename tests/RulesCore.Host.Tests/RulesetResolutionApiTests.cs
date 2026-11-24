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

public sealed class RulesetResolutionApiTests
{
    private sealed class SiteHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    contractVersion = 1, toolKey = "rules-core", siteMode = "dorks-and-dice",
                    user = new { id = "unit-test" }, delegatedFromToolKey = "character-sheet"
                })
            });
    }

    [Fact]
    public async Task FourPackageHostDispatchesWithoutCrossRulesetFallback()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RulesCore.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var root = Path.Combine(dir!.FullName, "data", "rulesets");
        string[] ids = ["dnd-5e", "dnd-5-5e", "fate-condensed"];
        var fingerprints = ids.ToDictionary(id => id,
            id => NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(root, id)).Fingerprint);
        var config = new Dictionary<string, string?>
        {
            ["Rulesets:ContentRoot"] = root,
            ["Rulesets:DefaultRuleset"] = "first-party-001",
            ["RulesCore:ApiSurface"] = "PublicOnly",
            ["ToolHost:BaseUrl"] = "https://site.test"
        };
        for (var i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            config["Rulesets:AdditionalRulesets:" + i] = id;
            var prefix = "RulesCore:PublicDistribution:Packages:" + id + ":";
            config[prefix + "Enabled"] = "true";
            config[prefix + "ApprovedFingerprint"] = fingerprints[id];
            config[prefix + "RightsReviewReference"] = "TEST-FIXTURE-NOT-PRODUCTION-APPROVAL";
            config[prefix + "AttributionStatement"] = "Integration test only";
        }
        await using var app = RulesCoreApplication.Create([], builder =>
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(config);
        }, services => services.AddHttpClient<CharacterSheetMechanicsTicketValidator>()
            .ConfigurePrimaryHttpMessageHandler(() => new SiteHandler()));
        await app.StartAsync();
        var uri = app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(uri) };
        var registry = app.Services.GetRequiredService<NativeCatalogRegistry>();
        Assert.Equal(4, registry.RulesetIds.Count);
        Assert.All(registry.RulesetIds, id => Assert.True(registry.GetStore(id).Current().IsReady));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/ready/all")).StatusCode);
        foreach (var id in ids)
        {
            var ready = await client.GetFromJsonAsync<JsonElement>("/ready/rulesets/" + id);
            Assert.Equal(fingerprints[id], ready.GetProperty("fingerprint").GetString());
        }
        var listing = await client.GetFromJsonAsync<JsonElement>("/api/rulesets");
        Assert.Equal(ids.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            listing.GetProperty("rulesets").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToArray());

        const string path = "/api/mechanics/v1/rulesets/";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path + ids[0] + "/capabilities")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Dorks-Tool-Auth-Ticket", "signed-in-site-ticket");
        client.DefaultRequestHeaders.Add("X-Dorks-Tool-Auth-Introspection-Path",
            "/tool-host/registrations/rules-core/api/introspect");
        foreach (var id in ids)
        {
            var capabilities = await client.GetFromJsonAsync<JsonElement>(path + id + "/capabilities");
            Assert.False(capabilities.GetProperty("complete").GetBoolean());
        }
        async Task<HttpResponseMessage> Resolve(string id, string fingerprint, object resolution) =>
            await client.PostAsJsonAsync(path + id + "/resolution", new { fingerprint, resolution });

        using var old = await Resolve(ids[0], fingerprints[ids[0]],
            new { operation = "d20-ability-check", dice = new[] { 5, 15 }, modifier = 2, target = 10,
                  d20 = new { exhaustion = 1 } });
        using var modern = await Resolve(ids[1], fingerprints[ids[1]],
            new { operation = "d20-ability-check", dice = new[] { 17 }, modifier = 2, target = 10,
                  d20 = new { exhaustion = 1 } });
        using var fate = await Resolve(ids[2], fingerprints[ids[2]],
            new { operation = "fate-roll", dice = new[] { 1, 0, -1, 1 }, modifier = 3, target = 1 });
        foreach (var (response, total) in new[] { (old, 7), (modern, 17), (fate, 4) })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(total, body.GetProperty("result").GetProperty("total").GetInt32());
        }
        using var unsupported = await Resolve(ids[2], fingerprints[ids[2]],
            new { operation = "d20-ability-check", dice = new[] { 10 }, modifier = 0, target = 10 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unsupported.StatusCode);
        using var wrong = await Resolve(ids[1], fingerprints[ids[0]],
            new { operation = "d20-ability-check", dice = new[] { 10 }, modifier = 0, target = 10 });
        Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + "unknown/capabilities")).StatusCode);
        registry.GetStore(ids[0]).MarkUnavailable("invalid_native_json");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/ready/all")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/ready/rulesets/" + ids[0])).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/ready/rulesets/" + ids[1])).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/ready/rulesets/unknown")).StatusCode);
    }
}
