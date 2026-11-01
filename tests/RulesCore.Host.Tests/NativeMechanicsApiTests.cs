using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Host;
using Xunit;

namespace RulesCore.Host.Tests;

public sealed class NativeMechanicsApiTests
{
    private sealed class SiteHandler(string source, bool ordinary = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(JsonSerializer.Serialize(new {
                    contractVersion = 1, toolKey = "rules-core", siteMode = "dorks-and-dice",
                    user = new { id = "unit-test" },
                    privateTunnelSourceToolKey = ordinary ? null : source,
                    delegatedFromToolKey = ordinary ? source : null
                }), Encoding.UTF8, "application/json")
            });
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "rc-mechanics-http-" + Guid.NewGuid().ToString("N"));
        public WebApplication App { get; private set; } = null!;
        public HttpClient Client { get; private set; } = null!;
        public async Task StartAsync(string surface, string source, bool ordinary = false)
        {
            var package = Path.Combine(_root, "first-party-001");
            Directory.CreateDirectory(Path.Combine(package, "native"));
            File.WriteAllText(Path.Combine(package, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            File.WriteAllText(Path.Combine(package, "native", "class.json"), """
                {"id":"class.demo","name":"Demo","kind":"class","definition":{
                  "progression":[{"level":1,"proficiencyBonus":2,"featureGrants":[]}]}}
                """);
            File.WriteAllText(Path.Combine(package, "native", "conditions.json"), """
                {"conditions":[{"id":"condition.frightened","name":"Frightened","kind":"condition",
                  "effects":[{"type":"roll-mode","appliesTo":"attack-roll",
                    "subject":"attacks-by-affected-creature","mode":"disadvantage",
                    "when":{"fearSourceWithinLineOfSight":true}}]}]}
                """);
            App = RulesCoreApplication.Create([], builder => {
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                    ["RulesCore:ApiSurface"] = surface,
                    ["Rulesets:ContentRoot"] = _root,
                    ["Rulesets:DefaultRuleset"] = "first-party-001",
                    ["ToolHost:BaseUrl"] = "https://site.test"
                });
            }, services => {
                services.AddHttpClient<CharacterSheetMechanicsTicketValidator>()
                    .ConfigurePrimaryHttpMessageHandler(() => new SiteHandler(source, ordinary));
            });
            await App.StartAsync();
            var server = App.Services.GetRequiredService<IServer>();
            Client = new HttpClient { BaseAddress = new Uri(
                server.Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        }
        public HttpRequestMessage Authenticated(HttpMethod method, string path, HttpContent? body = null)
        {
            var request = new HttpRequestMessage(method, path) { Content = body };
            request.Headers.Add("X-Dorks-Tool-Auth-Ticket", "test-site-ticket");
            request.Headers.Add("X-Dorks-Tool-Auth-Introspection-Path",
                "/tool-host/registrations/rules-core/api/introspect");
            return request;
        }
        public async ValueTask DisposeAsync()
        {
            Client?.Dispose();
            if (App is not null) await App.DisposeAsync();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task AuthorizedCharacterSheetCanPinAndProjectSnapshot()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PrivateOnly", "character-sheet");
        using var info = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/private/mechanics/v1/ruleset"));
        Assert.Equal(HttpStatusCode.OK, info.StatusCode);
        var metadata = await info.Content.ReadFromJsonAsync<JsonElement>();
        var fingerprint = metadata.GetProperty("fingerprint").GetString();
        using var request = fixture.Authenticated(HttpMethod.Post,
            "/api/private/mechanics/v1/characters/project", JsonContent.Create(new {
                rulesetId = "first-party-001", fingerprint,
                abilityScores = new { strength = 10, dexterity = 16, constitution = 12,
                    intelligence = 10, wisdom = 10, charisma = 10 },
                advancements = new[] { new { classId = "class.demo", level = 1, occurrenceId = "a" } }
            }));
        using var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("native-character-project", response.Headers.GetValues("Server-Timing").Single());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("values").GetProperty("proficiency.bonus")
            .GetProperty("value").GetInt32());
        Assert.Equal(3, body.GetProperty("values").GetProperty("initiative")
            .GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task InvalidCharacterMagnitudeIsBadRequestRatherThanVersionConflictOrServerError()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true);
        using var info = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/mechanics/v1/ruleset"));
        var metadata = await info.Content.ReadFromJsonAsync<JsonElement>();
        var fingerprint = metadata.GetProperty("fingerprint").GetString();
        using var oversized = fixture.Authenticated(HttpMethod.Post,
            "/api/mechanics/v1/characters/project", JsonContent.Create(new {
                rulesetId = "first-party-001", fingerprint,
                abilityScores = new { strength = 10, dexterity = 10, constitution = 10,
                    intelligence = 10, wisdom = 10, charisma = 10 },
                advancements = new[] { new { classId = "class.demo", level = 1, occurrenceId = "a" } },
                defenseContributions = new { armor = int.MaxValue }
            }));
        using var invalidResponse = await fixture.Client.SendAsync(oversized);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        var invalid = await invalidResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid", invalid.GetProperty("status").GetString());
        using var stale = fixture.Authenticated(HttpMethod.Post,
            "/api/mechanics/v1/characters/project", JsonContent.Create(new {
                rulesetId = "first-party-001", fingerprint = "stale",
                abilityScores = new { strength = 10, dexterity = 10, constitution = 10,
                    intelligence = 10, wisdom = 10, charisma = 10 },
                advancements = new[] { new { classId = "class.demo", level = 1, occurrenceId = "a" } }
            }));
        using var conflict = await fixture.Client.SendAsync(stale);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task AuthorizedRollModeEvaluatesEventFactsAndRejectsMissingFacts()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PrivateOnly", "character-sheet");
        using var metadataResponse = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/private/mechanics/v1/ruleset"));
        var metadata = await metadataResponse.Content.ReadFromJsonAsync<JsonElement>();
        var fingerprint = metadata.GetProperty("fingerprint").GetString();
        var uri = "/api/private/mechanics/v1/conditions/roll-mode";
        var roll = new { appliesTo = "attack-roll", subject = "attacks-by-affected-creature" };
        using var request = fixture.Authenticated(HttpMethod.Post, uri, JsonContent.Create(new {
            rulesetId = "first-party-001", fingerprint, conditionIds = new[] { "condition.frightened" },
            roll, facts = new { flags = new { fearSourceWithinLineOfSight = true } }
        }));
        using var resolvedResponse = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, resolvedResponse.StatusCode);
        var resolved = await resolvedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("resolved", resolved.GetProperty("status").GetString());
        Assert.Equal(2, resolved.GetProperty("mode").GetInt32());

        using var incompleteRequest = fixture.Authenticated(HttpMethod.Post, uri, JsonContent.Create(new {
            rulesetId = "first-party-001", fingerprint,
            conditionIds = new[] { "condition.frightened" }, roll
        }));
        using var incomplete = await fixture.Client.SendAsync(incompleteRequest);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, incomplete.StatusCode);
        var unresolved = await incomplete.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unsupported", unresolved.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CharacterSheetCanSearchNativeCatalogAndBatchResolveStableIds()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PrivateOnly", "character-sheet");
        using var metadataResponse = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/private/mechanics/v1/ruleset"));
        var metadata = await metadataResponse.Content.ReadFromJsonAsync<JsonElement>();
        var fingerprint = metadata.GetProperty("fingerprint").GetString();
        using var search = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/private/mechanics/v1/catalog/rules?" +
                "fingerprint=" + fingerprint + "&category=class&q=demo&limit=10&offset=0"));
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var page = await search.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        Assert.Equal("class.demo", page.GetProperty("rules")[0].GetProperty("id").GetString());
        Assert.False(page.GetProperty("rules")[0].TryGetProperty("sourcePath", out _));

        using var request = fixture.Authenticated(HttpMethod.Post,
            "/api/private/mechanics/v1/catalog/resolve", JsonContent.Create(new {
                rulesetId = "first-party-001", fingerprint,
                ids = new[] { "class.demo", "class.missing" }
            }));
        using var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var details = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("partial", details.GetProperty("status").GetString());
        Assert.Equal("class.demo", details.GetProperty("rules")[0].GetProperty("id").GetString());
        Assert.Equal("class.missing", details.GetProperty("missingIds")[0].GetString());
        Assert.False(details.GetProperty("rules")[0].TryGetProperty("sourcePath", out _));
        Assert.Equal("class.demo", details.GetProperty("rules")[0]
            .GetProperty("definition").GetProperty("id").GetString());
    }

    [Fact]
    public async Task CatalogRequestsRejectStaleFingerprintsAndInvalidIds()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PrivateOnly", "character-sheet");
        using var stale = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/private/mechanics/v1/catalog/rules?fingerprint=stale"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var invalidRequest = fixture.Authenticated(HttpMethod.Post,
            "/api/private/mechanics/v1/catalog/resolve", JsonContent.Create(new {
                rulesetId = "first-party-001", fingerprint = "stale", ids = new[] { "../working" }
            }));
        using var invalid = await fixture.Client.SendAsync(invalidRequest);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var staleBody = fixture.Authenticated(HttpMethod.Post,
            "/api/private/mechanics/v1/catalog/resolve", JsonContent.Create(new {
                rulesetId = "first-party-001", fingerprint = "stale", ids = new[] { "class.demo" }
            }));
        using var mismatch = await fixture.Client.SendAsync(staleBody);
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
    }

    [Fact]
    public async Task WikiTicketCannotReadCharacterSheetNativeCatalog()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PrivateOnly", "rules-wiki");
        using var response = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/private/mechanics/v1/catalog/rules?fingerprint=anything"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WikiTicketCannotInvokeConditionRollMechanics()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PrivateOnly", "rules-wiki");
        using var response = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Post, "/api/private/mechanics/v1/conditions/roll-mode",
            JsonContent.Create(new { rulesetId = "first-party-001", fingerprint = "invalid",
                conditionIds = Array.Empty<string>(), roll = new {
                    appliesTo = "attack-roll", subject = "affected-creature" } })));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WikiTicketCannotInvokeCharacterSheetMechanics()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PrivateOnly", "rules-wiki");
        using var response = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/private/mechanics/v1/ruleset"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OrdinaryCharacterSheetDelegationCanProjectThroughPublicIngress()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true);
        using var metadataResponse = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/mechanics/v1/ruleset"));
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        var metadata = await metadataResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(metadata.GetProperty("capabilities").GetProperty("nativeCatalogSearch").GetBoolean());
        var fingerprint = metadata.GetProperty("fingerprint").GetString();
        using var request = fixture.Authenticated(HttpMethod.Post,
            "/api/mechanics/v1/characters/project", JsonContent.Create(new {
                rulesetId = "first-party-001", fingerprint,
                abilityScores = new { strength = 10, dexterity = 16, constitution = 12,
                    intelligence = 10, wisdom = 10, charisma = 10 },
                advancements = new[] { new { classId = "class.demo", level = 1, occurrenceId = "a" } }
            }));
        using var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var projection = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, projection.GetProperty("values").GetProperty("proficiency.bonus")
            .GetProperty("value").GetInt32());
    }

    [Theory]
    [InlineData("rules-wiki")]
    [InlineData("block-initiative")]
    public async Task OtherOrdinaryToolsCannotReadCharacterSheetMechanics(string source)
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", source, ordinary: true);
        using var response = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/mechanics/v1/ruleset"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PrivateOnlyHasNoOrdinaryMechanicsRoute()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PrivateOnly", "character-sheet");
        using var response = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/mechanics/v1/ruleset"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OrdinaryDelegationListsOnlySafeNativeOptionsAndChecksAdvancement()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true);
        using var metadataResponse = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/mechanics/v1/ruleset"));
        var metadata = await metadataResponse.Content.ReadFromJsonAsync<JsonElement>();
        var fingerprint = metadata.GetProperty("fingerprint").GetString();
        Assert.True(metadata.GetProperty("capabilities").GetProperty("nativeOptionSearch").GetBoolean());

        using var search = await fixture.Client.SendAsync(fixture.Authenticated(HttpMethod.Get,
            "/api/mechanics/v1/catalog/options?fingerprint=" + fingerprint + "&category=class&q=demo"));
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var result = await search.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, result.GetProperty("total").GetInt32());
        Assert.Equal("class.demo", result.GetProperty("options")[0].GetProperty("id").GetString());
        Assert.False(result.GetProperty("options")[0].TryGetProperty("definition", out _));
        Assert.False(result.GetProperty("options")[0].TryGetProperty("sourcePath", out _));
        // Skill and Competency summaries are safe identities only, not raw
        // native definitions or private content. Empty result sets are valid.
        foreach (var category in new[] { "skill", "competency", "monster", "item" })
        {
            using var catalogSearch = await fixture.Client.SendAsync(fixture.Authenticated(
                HttpMethod.Get, "/api/mechanics/v1/catalog/options?fingerprint=" +
                    fingerprint + "&category=" + category));
            Assert.Equal(HttpStatusCode.OK, catalogSearch.StatusCode);
            var catalogPage = await catalogSearch.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(category, catalogPage.GetProperty("category").GetString());
            foreach (var item in catalogPage.GetProperty("options").EnumerateArray())
            {
                Assert.False(item.TryGetProperty("definition", out _));
                Assert.False(item.TryGetProperty("sourcePath", out _));
            }
        }

        using var check = fixture.Authenticated(HttpMethod.Post,
            "/api/mechanics/v1/advancements/eligibility", JsonContent.Create(new {
                rulesetId = "first-party-001", fingerprint, candidateId = "class.demo",
                character = new { rulesetId = "first-party-001", fingerprint,
                    abilityScores = new { strength = 10, dexterity = 12, constitution = 10,
                        intelligence = 10, wisdom = 10, charisma = 14 },
                    advancements = Array.Empty<object>() }
            }));
        using var response = await fixture.Client.SendAsync(check);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var eligibility = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("eligible", eligibility.GetProperty("status").GetString());
        Assert.True(eligibility.GetProperty("eligible").GetBoolean());
    }

    [Fact]
    public async Task NativeOptionSummarySearchRejectsUnsafeAndStaleQueries()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true);
        using var noCategory = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/mechanics/v1/catalog/options?fingerprint=any"));
        Assert.Equal(HttpStatusCode.BadRequest, noCategory.StatusCode);
        using var deniedKind = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/mechanics/v1/catalog/options?fingerprint=any&category=source"));
        Assert.Equal(HttpStatusCode.BadRequest, deniedKind.StatusCode);
        using var stale = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/mechanics/v1/catalog/options?fingerprint=stale&category=class"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task OrdinaryPublicIngressDoesNotExposeRawNativeCatalog()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true);
        using var response = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Post, "/api/mechanics/v1/catalog/resolve", JsonContent.Create(new {
                rulesetId = "first-party-001", fingerprint = "any", ids = new[] { "class.demo" }
            })));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousPublicMechanicsRequestIsRejected()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true);
        using var response = await fixture.Client.GetAsync("/api/mechanics/v1/ruleset");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PublicIngressHasNoMechanicsRoutes()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet");
        using var response = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/private/mechanics/v1/ruleset"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
