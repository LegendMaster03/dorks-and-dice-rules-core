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
using RulesCore.Content;
using RulesCore.Host;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Host.Tests;

public sealed class NativeCombatHttpTests
{
    private sealed class SiteHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    contractVersion = 1,
                    toolKey = "rules-core",
                    siteMode = "dorks-and-dice",
                    user = new { id = "combat-test-user" },
                    delegatedFromToolKey = "character-sheet",
                    privateTunnelSourceToolKey = (string?)null
                }), Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(),
            "native-combat-http-" + Guid.NewGuid().ToString("N"));

        public WebApplication App { get; private set; } = null!;
        public HttpClient Client { get; private set; } = null!;
        public NativeCatalogSnapshot Catalog =>
            App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;

        public async Task StartAsync()
        {
            var package = Path.Combine(root, "first-party-001");
            Directory.CreateDirectory(Path.Combine(package, "native"));
            File.WriteAllText(Path.Combine(package, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            File.WriteAllText(Path.Combine(package, "native", "class.json"),
                """{"id":"class.demo","name":"Demo","kind":"class","definition":{}}""");
            App = RulesCoreApplication.Create([], builder =>
            {
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["RulesCore:ApiSurface"] = "PublicOnly",
                        ["Rulesets:ContentRoot"] = root,
                        ["Rulesets:DefaultRuleset"] = "first-party-001",
                        ["ToolHost:BaseUrl"] = "https://site.test"
                    });
            }, services =>
            {
                services.AddHttpClient<CharacterSheetMechanicsTicketValidator>()
                    .ConfigurePrimaryHttpMessageHandler(() => new SiteHandler());
            });
            await App.StartAsync();
            var server = App.Services.GetRequiredService<IServer>();
            var url = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            Client = new HttpClient { BaseAddress = new Uri(url) };
        }

        public Task<HttpResponseMessage> Post(string endpoint, object payload, bool authorized = true)
        {
            var req = new HttpRequestMessage(HttpMethod.Post,
                "/api/mechanics/v1/experimental/combat/" + endpoint)
            {
                Content = JsonContent.Create(payload)
            };
            if (authorized)
            {
                req.Headers.Add("X-Dorks-Tool-Auth-Ticket", "signed-in-site-ticket");
                req.Headers.Add("X-Dorks-Tool-Auth-Introspection-Path",
                    "/tool-host/registrations/rules-core/api/introspect");
            }
            return Client.SendAsync(req);
        }

        public async ValueTask DisposeAsync()
        {
            Client?.Dispose();
            if (App is not null) await App.DisposeAsync();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("action-budget")]
    [InlineData("attack-check")]
    [InlineData("damage-preview")]
    public async Task CombatExperimentsRequireExistingDelegation(string endpoint)
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync();
        object request = endpoint switch
        {
            "action-budget" => new
            {
                rulesetId = fixture.Catalog.RulesetId,
                fingerprint = fixture.Catalog.Fingerprint,
                policyId = NativeActionBudgetExperiment.Independent,
                command = "initialize"
            },
            "attack-check" => new NativeCombatAttackRequest(
                fixture.Catalog.RulesetId, fixture.Catalog.Fingerprint,
                "melee", 2, 10, null, NativeDefenseProfile.Normal,
                new Dictionary<string, int> { ["armor"] = 2 }),
            _ => new NativeCombatDamageRequest(
                fixture.Catalog.RulesetId, fixture.Catalog.Fingerprint, 10)
        };
        using var reply = await fixture.Post(endpoint, request, authorized: false);
        Assert.Equal(HttpStatusCode.Unauthorized, reply.StatusCode);
    }

    [Theory]
    [InlineData(NativeActionBudgetExperiment.Independent)]
    [InlineData(NativeActionBudgetExperiment.SharedEndTurn)]
    public async Task ActionBudgetsAreExplicitAndFingerprintBound(string policy)
    {
        await using var f = new Fixture();
        await f.StartAsync();
        using var init = await f.Post("action-budget", new
        {
            rulesetId = f.Catalog.RulesetId,
            fingerprint = f.Catalog.Fingerprint,
            policyId = policy,
            command = "initialize"
        });
        Assert.Equal(HttpStatusCode.OK, init.StatusCode);
        var body = await init.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("resolved", body.GetProperty("status").GetString());
        var state = body.GetProperty("state");
        Assert.True(state.GetProperty("bonusActionAvailable").GetBoolean());

        using var wrong = await f.Post("action-budget", new
        {
            rulesetId = f.Catalog.RulesetId, fingerprint = "old",
            policyId = policy, command = "start-turn", state
        });
        Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);

        using var changed = await f.Post("action-budget", new
        {
            rulesetId = f.Catalog.RulesetId,
            fingerprint = f.Catalog.Fingerprint,
            policyId = policy == NativeActionBudgetExperiment.Independent
                ? NativeActionBudgetExperiment.SharedEndTurn
                : NativeActionBudgetExperiment.Independent,
            command = "start-turn",
            state
        });
        Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);
    }

    [Fact]
    public async Task NormalAttackPreviewUsesNativeDefenseAndDoesNotApplyDamage()
    {
        await using var f = new Fixture();
        await f.StartAsync();
        using var response = await f.Post("attack-check",
            new NativeCombatAttackRequest(
                f.Catalog.RulesetId, f.Catalog.Fingerprint,
                "melee", 5, 13, null, NativeDefenseProfile.Normal,
                new Dictionary<string, int>
                {
                    ["armor"] = 4, ["shield"] = 2, ["dexterity"] = 3
                }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var check = body.GetProperty("result");
        Assert.Equal("resolved", check.GetProperty("status").GetString());
        Assert.False(check.GetProperty("hit").GetBoolean());
        Assert.Equal(19, check.GetProperty("armorClass").GetInt32());
        Assert.Equal(18, check.GetProperty("attackTotal").GetInt32());
    }

    [Fact]
    public async Task DamagePreviewIsNoncommittingAndRejectsUnknownRounding()
    {
        await using var f = new Fixture();
        await f.StartAsync();
        var input = new NativeCombatDamageRequest(f.Catalog.RulesetId,
            f.Catalog.Fingerprint, 20, 50, [2, 3], 20, 20);
        using var resolved = await f.Post("damage-preview", input);
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        var body = await resolved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(15,
            body.GetProperty("result").GetProperty("proposedHitPoints").GetInt32());
        Assert.Equal(20, input.CurrentHitPoints);

        using var fractional = await f.Post("damage-preview",
            input with { IncomingDamage = 3, DamageModificationPercent = 25 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, fractional.StatusCode);
        var pending = await fractional.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("rounding-unresolved",
            pending.GetProperty("result").GetProperty("status").GetString());
    }
}
