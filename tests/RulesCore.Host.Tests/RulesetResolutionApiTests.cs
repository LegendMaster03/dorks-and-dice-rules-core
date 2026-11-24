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

        // Exercise real, edition-specific bestiary entities through the same
        // running four-package host, not just synthetic catalog fixtures.
        foreach (var id in ids.Take(2))
        {
            var prefix = "/api/rulesets/" + id + "/rules";
            var results = await client.GetFromJsonAsync<JsonElement>(
                prefix + "?category=monster&q=Avatar%20of%20Death&limit=1");
            Assert.Equal(1, results.GetProperty("total").GetInt32());
            Assert.Equal(fingerprints[id], results.GetProperty("fingerprint").GetString());
            Assert.Equal("monster.avatar-of-death",
                results.GetProperty("rules")[0].GetProperty("id").GetString());
            var detail = await client.GetFromJsonAsync<JsonElement>(prefix + "/monster.avatar-of-death");
            Assert.Equal(id, detail.GetProperty("rulesetId").GetString());
            var definition = detail.GetProperty("rule").GetProperty("definition");
            Assert.Equal(id == "dnd-5-5e" ? 2 : 1, definition.GetProperty("action").GetArrayLength());
            Assert.Equal(id == "dnd-5-5e", definition.GetProperty("conditionImmune")
                .EnumerateArray().Any(x => x.GetString() == "exhaustion"));
            var localAcolyte = id == "dnd-5e" ? "acolyte" : "priest-acolyte";
            var foreignAcolyte = id == "dnd-5e" ? "priest-acolyte" : "acolyte";
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(prefix + "/monster." + localAcolyte)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(prefix + "/monster." + foreignAcolyte)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(
            "/api/rulesets/fate-condensed/rules/monster.avatar-of-death")).StatusCode);

        // Real publisher-derived catalog scale and structured fields.
        const string animals = "/api/rulesets/dnd-5-5e/rules";
        var firstPage = await client.GetFromJsonAsync<JsonElement>(animals + "?category=monster&limit=10");
        var secondPage = await client.GetFromJsonAsync<JsonElement>(animals + "?category=monster&limit=10&offset=10");
        Assert.Equal(97, firstPage.GetProperty("total").GetInt32());
        Assert.Equal(97, secondPage.GetProperty("total").GetInt32());
        Assert.Empty(firstPage.GetProperty("rules").EnumerateArray().Select(x => x.GetProperty("id").GetString())
            .Intersect(secondPage.GetProperty("rules").EnumerateArray().Select(x => x.GetProperty("id").GetString())));
        var seahorse = await client.GetFromJsonAsync<JsonElement>(animals + "/monster.seahorse");
        Assert.Equal(0, seahorse.GetProperty("rule").GetProperty("definition")
            .GetProperty("challenge").GetProperty("xp").GetInt32());
        var owl = await client.GetFromJsonAsync<JsonElement>(animals + "/monster.giant-owl");
        Assert.Equal("celestial", owl.GetProperty("rule").GetProperty("definition").GetProperty("type").GetString());

        // The foreign catalogs expose original typed spell fields and entries,
        // not just publisher layout strings.
        foreach (var id in ids.Take(2))
        {
            var spell = await client.GetFromJsonAsync<JsonElement>(
                "/api/rulesets/" + id + "/rules/spell.barkskin");
            var definition = spell.GetProperty("rule").GetProperty("definition");
            Assert.Equal(id == "dnd-5e" ? "action" : "bonus",
                definition.GetProperty("time")[0].GetProperty("unit").GetString());
            Assert.Equal(JsonValueKind.Object, definition.GetProperty("range").ValueKind);
            Assert.Equal(JsonValueKind.Object, definition.GetProperty("components").ValueKind);
            Assert.NotEmpty(definition.GetProperty("entries").EnumerateArray());
        }

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
