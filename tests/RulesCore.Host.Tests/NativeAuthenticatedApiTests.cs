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
using RulesCore.Mechanics;
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

    private sealed class SiteHandler(string? source = null, string mode = "dorks-and-dice",
        bool dm = false) : HttpMessageHandler
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
                    privateTunnelSourceToolKey = (string?)null,
                    campaigns = dm ? new[]
                    {
                        new { id = "99d41ce8-36fc-43df-b8cd-bb1829b20982", name = "Test", role = "DM" }
                    } : Array.Empty<object>()
                }), Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class OriginVerifier(
        Guid characterId, bool authorize = true)
        : INativeOriginHostUnderlyingAuthorityProvider
    {
        public int Calls { get; private set; }

        public Task<bool> VerifyAsync(Guid actorId, Guid id, long revision,
            NativeOriginProfile profile, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(authorize &&
                actorId == Guid.Parse("d66a7a7b-bc0f-4677-829d-0ccf40470d2f") &&
                id == characterId && revision == 2 &&
                profile.Id == "origin:verified");
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
            string surface = "PublicOnly", string? source = null, string mode = "dorks-and-dice",
            bool dm = false,
            INativeOriginHostUnderlyingAuthorityProvider? originVerifier = null,
            bool foreign = false)
        {
            var root = Path.Combine(Path.GetTempPath(), "native-consumer-" + Guid.NewGuid().ToString("N"));
            var package = Path.Combine(root, Ruleset);
            Directory.CreateDirectory(Path.Combine(package, "native"));
            File.WriteAllText(Path.Combine(package, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","workingRoot":"working/","runtimeContentRoot":"native/"}""");
            File.WriteAllText(Path.Combine(package, "native", "base.json"), Definition);
            var additional = new Dictionary<string, string?>();
            if (foreign)
            {
                foreach (var (id, index) in new[] { ("dnd-5e", 0), ("dnd-5-5e", 1) })
                {
                    var location = NativeRulesetLayout.Resolve(root, id);
                    Directory.CreateDirectory(location.NativeDirectory);
                    File.WriteAllText(Path.Combine(root, id, "manifest.json"),
                        $$"""{"id":"{{id}}","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","workingRoot":"working/","runtimeContentRoot":"native/"}""");
                    File.WriteAllText(Path.Combine(location.NativeDirectory, "base.json"),
                        $$"""{"id":"monster.goblin","name":"{{id}} goblin","kind":"monster"}""");
                    additional["Rulesets:AdditionalRulesets:" + index] = id;
                    if (index == 0)
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
                    ["Rulesets:DefaultRuleset"] = Ruleset,
                    ["ToolHost:BaseUrl"] = "https://site.test"
                    // No first-party public distribution approval: internal access must not depend on it.
                });
                builder.Configuration.AddInMemoryCollection(additional);
            }, services =>
            {
                services.AddHttpClient<CharacterSheetMechanicsTicketValidator>()
                    .ConfigurePrimaryHttpMessageHandler(() => new SiteHandler(source, mode, dm));
                if (originVerifier is not null)
                    services.AddSingleton(originVerifier);
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
    public async Task VerifiedOriginUnderlyingRequiresIndependentSourceAndKeepsCurrentDistinct()
    {
        var id = Guid.NewGuid();
        var verified = new OriginVerifier(id);
        await using var fixture = await Fixture.StartAsync(
            source: "character-sheet", originVerifier: verified);
        var catalog = fixture.Store.Snapshot!;
        var underlying = new NativeOriginCurrentConfiguration(
            "species.human", null, []);
        var current = new NativeOriginCurrentConfiguration(
            "species.elf", null, []);
        var profile = new NativeOriginProfile(
            "origin:verified", underlying, current, [], [],
            new NativeOriginDecisionBasis(NativeOriginAdjudication.Supported),
            EffectiveConfigurationBasis: new NativeOriginDecisionBasis(
                NativeOriginAdjudication.Supported,
                NativeRuleReference: "rule.creature-origination"));

        async Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(
            NativeOriginProfile submitted, long revision, string fingerprint)
        {
            using var request = fixture.Authorized(
                "/api/mechanics/v1/origins/underlying/verify");
            request.Method = HttpMethod.Post;
            request.Content = JsonContent.Create(new
            {
                characterId = id, originRevision = revision,
                rulesetId = Ruleset, fingerprint, profile = submitted
            });
            using var response = await fixture.Client.SendAsync(request);
            return (response.StatusCode,
                await response.Content.ReadFromJsonAsync<JsonElement>());
        }

        var mismatch = await SendAsync(profile, 2, "obsolete");
        Assert.Equal(HttpStatusCode.Conflict, mismatch.Status);
        Assert.Equal(0, verified.Calls);

        var altered = await SendAsync(profile with { Id = "origin:forged" },
            2, catalog.Fingerprint);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, altered.Status);
        Assert.Equal("origin_authority_unverified",
            altered.Body.GetProperty("error").GetString());

        var accepted = await SendAsync(profile, 2, catalog.Fingerprint);
        Assert.Equal(HttpStatusCode.OK, accepted.Status);
        Assert.Equal("partial", accepted.Body.GetProperty("status").GetString());
        Assert.False(accepted.Body.GetProperty("effectiveMechanicsReady").GetBoolean());
        Assert.Equal("species.human",
            accepted.Body.GetProperty("underlying").GetProperty("speciesId").GetString());
        Assert.Equal("species.elf",
            accepted.Body.GetProperty("current").GetProperty("speciesId").GetString());
        Assert.Equal(2, verified.Calls);
    }

    [Fact]
    public async Task VerifiedOriginUnderlyingRejectsDelegationWithoutHostAuthority()
    {
        await using var fixture = await Fixture.StartAsync(source: "character-sheet");
        using var request = fixture.Authorized(
            "/api/mechanics/v1/origins/underlying/verify");
        request.Method = HttpMethod.Post;
        request.Content = JsonContent.Create(new
        {
            characterId = Guid.NewGuid(), originRevision = 1,
            rulesetId = Ruleset,
            fingerprint = fixture.Store.Snapshot!.Fingerprint,
            profile = new NativeOriginProfile(
                "origin:unverified",
                new NativeOriginCurrentConfiguration("species.human", null, []),
                new NativeOriginCurrentConfiguration("species.human", null, []),
                [], [], new NativeOriginDecisionBasis(
                    NativeOriginAdjudication.Supported))
        });
        using var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("origin_authority_unavailable",
            body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task SignedInUserReadsApprovedForeignButNotUnpublishedForeignCatalogs()
    {
        await using var fixture = await Fixture.StartAsync(foreign: true);
        using var listing = await fixture.Client.SendAsync(fixture.Authorized(Catalog));
        Assert.Equal(HttpStatusCode.OK, listing.StatusCode);
        var body = await listing.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "dnd-5e", Ruleset },
            body.GetProperty("rulesets").EnumerateArray().Select(item => item.GetProperty("id").GetString()).ToArray());
        using var foreign = await fixture.Client.SendAsync(fixture.Authorized(Catalog + "/dnd-5e/rules/monster.goblin"));
        Assert.Equal(HttpStatusCode.OK, foreign.StatusCode);
        var detail = await foreign.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("dnd-5e goblin", detail.GetProperty("rule").GetProperty("name").GetString());
        using var denied = await fixture.Client.SendAsync(fixture.Authorized(Catalog + "/dnd-5-5e/rules/monster.goblin"));
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
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
    public async Task ApprovedMixedEvaluationRequiresSiteDelegatedDmRoleAndValidatedRequest()
    {
        const string path = "/api/mechanics/v1/origins/evaluate-approved-mixed";
        const string campaignId = "99d41ce8-36fc-43df-b8cd-bb1829b20982";
        var requestBody = new
        {
            rulesetId = Ruleset,
            characterId = "44eb66e6-3b5c-44e2-aa71-4986d4b346c0",
            campaignId,
            approvalEvidenceId = "approval:123",
            proposal = new
            {
                id = "mixed-1", intent = 1,
                method = new { ruleId = "rule.creature-origination", methodId = "biological" },
                contributors = Array.Empty<object>(),
                candidates = Array.Empty<object>(),
                manifestations = Array.Empty<object>()
            }
        };

        static async Task<HttpResponseMessage> SendAsync(
            Fixture fixture, string endpoint, object body)
        {
            using var request = fixture.Authorized(endpoint);
            request.Method = HttpMethod.Post;
            request.Content = JsonContent.Create(body);
            return await fixture.Client.SendAsync(request);
        }

        await using var player = await Fixture.StartAsync(source: "character-sheet");
        using var playerResponse = await SendAsync(player, path, requestBody);
        Assert.Equal(HttpStatusCode.Forbidden, playerResponse.StatusCode);
        Assert.Contains("campaign_dm_authority_required",
            await playerResponse.Content.ReadAsStringAsync());

        await using var direct = await Fixture.StartAsync(dm: true);
        using var directResponse = await SendAsync(direct, path, requestBody);
        Assert.Equal(HttpStatusCode.Forbidden, directResponse.StatusCode);

        await using var dm = await Fixture.StartAsync(source: "character-sheet", dm: true);
        using var malformedResponse = await SendAsync(dm, path, requestBody);
        Assert.Equal(HttpStatusCode.BadRequest, malformedResponse.StatusCode);
        Assert.Contains("invalid_approved_mixed_proposal",
            await malformedResponse.Content.ReadAsStringAsync());

        using var wrongCampaignResponse = await SendAsync(dm, path,
            new
            {
                requestBody.rulesetId, requestBody.characterId,
                campaignId = "d18df570-4574-4d10-872e-351a88843a27",
                requestBody.approvalEvidenceId, requestBody.proposal
            });
        Assert.Equal(HttpStatusCode.Forbidden, wrongCampaignResponse.StatusCode);
    }


    [Fact]
    public async Task DelegatedPrimaryStateEvaluationIsReadOnlyAndDeniesUnverifiedForms()
    {
        const string path = "/api/mechanics/v1/characters/effective-state-baseline";
        var characterId = Guid.NewGuid().ToString("D");

        await using var delegated = await Fixture.StartAsync(source: "character-sheet");
        object Body(long ledgerRevision = 0, object[]? alternate = null,
            object[]? events = null, string? fingerprint = null) => new
        {
            state = new
            {
                characterId,
                primaryMechanics = new
                {
                    rulesetId = Ruleset,
                    fingerprint = fingerprint ?? delegated.Store.Snapshot!.Fingerprint,
                    abilityScores = new Dictionary<string, int>
                    {
                        ["strength"] = 12, ["dexterity"] = 12,
                        ["constitution"] = 12, ["intelligence"] = 12,
                        ["wisdom"] = 12, ["charisma"] = 12
                    },
                    advancements = Array.Empty<object>()
                },
                alternateSheets = alternate ?? Array.Empty<object>(),
                persistentInfluences = Array.Empty<object>()
            },
            ledger = new
            {
                characterId, revision = ledgerRevision,
                instances = Array.Empty<object>(),
                resources = Array.Empty<object>(),
                processedEventIds = events ?? Array.Empty<object>()
            }
        };

        static async Task<HttpResponseMessage> Send(Fixture f, string path, object body)
        {
            using var request = f.Authorized(path);
            request.Method = HttpMethod.Post;
            request.Content = JsonContent.Create(body);
            return await f.Client.SendAsync(request);
        }

        using var allowed = await Send(delegated, path, Body());
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var output = await allowed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("primary-only", output.GetProperty("supportedScope").GetString());
        Assert.False(output.GetProperty("effectiveMechanicsReady").GetBoolean());
        Assert.Equal("partial", output.GetProperty("status").GetString());

        // A delegated tool ticket does not attest to arbitrary supernatural
        // acquisitions or a claimed processed transformation event.
        using var claimedLedger = await Send(delegated, path,
            Body(1, events: ["forged:wolf:activation"]));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, claimedLedger.StatusCode);
        Assert.Contains("supernatural_authority_unavailable",
            await claimedLedger.Content.ReadAsStringAsync());

        using var staleCatalog = await Send(delegated, path,
            Body(fingerprint: new string('0', 64)));
        Assert.Equal(HttpStatusCode.Conflict, staleCatalog.StatusCode);

        await using var notDelegated = await Fixture.StartAsync();
        using var rejectedDirect = await Send(notDelegated, path, Body());
        Assert.Equal(HttpStatusCode.Forbidden, rejectedDirect.StatusCode);

        using var rejectedAnonymous = await delegated.Client.PostAsJsonAsync(path, Body());
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedAnonymous.StatusCode);
    }


    [Fact]
    public async Task ManifestationPlanRequiresIndependentAuthorityNotJustDelegationOrEvidenceLabels()
    {
        const string path = "/api/mechanics/v1/characters/manifestations/plan";
        var id = Guid.NewGuid().ToString("D");
        await using var delegated = await Fixture.StartAsync(source: "character-sheet");
        var fingerprint = delegated.Store.Snapshot!.Fingerprint;
        object Body(string? catalog = null, long expectedRevision = 0,
            string? characterId = null) => new
        {
            state = new
            {
                characterId = id,
                primaryMechanics = new
                {
                    rulesetId = Ruleset, fingerprint = catalog ?? fingerprint,
                    abilityScores = new Dictionary<string, int>(),
                    advancements = Array.Empty<object>()
                },
                alternateSheets = new[]
                {
                    new
                    {
                        id = "wolf", parentId = "@primary",
                        definitionId = "forged.wolf",
                        definitionSource = new { kind = 0, id = "test.source", version = "v1" },
                        acquisitionEvidenceId = "receipt:asserted-by-caller"
                    }
                },
                persistentInfluences = Array.Empty<object>()
            },
            ledger = new
            {
                characterId = id, revision = 0,
                instances = Array.Empty<object>(),
                resources = Array.Empty<object>(),
                processedEventIds = Array.Empty<string>()
            },
            transition = new
            {
                characterId = characterId ?? id, expectedRevision,
                eventId = "activate:wolf",
                steps = new[]
                {
                    new
                    {
                        action = 0, targetInstanceId = "manifest.wolf",
                        ruleOccurrence = new
                        {
                            source = new { kind = 0, id = "test.source", version = "v1" },
                            occurrencePath = "forms.wolf"
                        },
                        basis = new { adjudication = 0, nativeRuleReference = "test.rule" },
                        authorizationEvidenceId = "receipt:claimed",
                        newInstance = new
                        {
                            characterId = id, id = "manifest.wolf", sheetId = "wolf",
                            definitionId = "test.wolf",
                            definitionOccurrence = new
                            {
                                source = new { kind = 0, id = "test.source", version = "v1" },
                                occurrencePath = "forms.wolf"
                            },
                            sourceInfluenceId = (string?)null,
                            activationEventId = "activate:wolf",
                            activationEvidenceId = "receipt:claimed",
                            activationBasis = new { adjudication = 0, nativeRuleReference = "test.rule" },
                            lifecycle = 0, contribution = 0,
                            activationCaptures = new Dictionary<string, int>(),
                            endingConditions = Array.Empty<string>(),
                            resourcePolicies = Array.Empty<object>(),
                            lastEventId = "activate:wolf"
                        }
                    }
                }
            }
        };

        static async Task<HttpResponseMessage> Send(Fixture f, string path, object body)
        {
            using var message = f.Authorized(path);
            message.Method = HttpMethod.Post;
            message.Content = JsonContent.Create(body);
            return await f.Client.SendAsync(message);
        }

        using var missingSourceAuthority = await Send(delegated, path, Body());
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            missingSourceAuthority.StatusCode);
        Assert.Contains("supernatural_authority_unavailable",
            await missingSourceAuthority.Content.ReadAsStringAsync());

        // Structural failures must be rejected before any authority is called,
        // even if the sender holds an authenticated Character Sheet ticket.
        var missingSheets = JsonSerializer.SerializeToNode(Body())!;
        missingSheets["state"]!["alternateSheets"] = null;
        using var incompleteState = await Send(delegated, path, missingSheets);
        Assert.Equal(HttpStatusCode.BadRequest, incompleteState.StatusCode);
        Assert.Contains("invalid_character_scope",
            await incompleteState.Content.ReadAsStringAsync());

        var missingJournal = JsonSerializer.SerializeToNode(Body())!;
        missingJournal["ledger"]!["processedEventIds"] = null;
        using var incompleteLedger = await Send(delegated, path, missingJournal);
        Assert.Equal(HttpStatusCode.BadRequest, incompleteLedger.StatusCode);

        var malformedPolicies = JsonSerializer.SerializeToNode(Body())!;
        malformedPolicies["transition"]!["steps"]![0]!["newInstance"]!
            ["resourcePolicies"] = new System.Text.Json.Nodes.JsonArray((System.Text.Json.Nodes.JsonNode?)null);
        using var invalidPolicy = await Send(delegated, path, malformedPolicies);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPolicy.StatusCode);
        Assert.Contains("invalid_character_scope",
            await invalidPolicy.Content.ReadAsStringAsync());

        var missingSteps = JsonSerializer.SerializeToNode(Body())!;
        missingSteps["transition"]!["steps"] = new System.Text.Json.Nodes.JsonArray();
        using var incompleteTransition = await Send(delegated, path, missingSteps);
        Assert.Equal(HttpStatusCode.BadRequest, incompleteTransition.StatusCode);

        var noncanonicalId = JsonSerializer.SerializeToNode(Body())!;
        noncanonicalId["state"]!["characterId"] = id.ToUpperInvariant();
        noncanonicalId["ledger"]!["characterId"] = id.ToUpperInvariant();
        noncanonicalId["transition"]!["characterId"] = id.ToUpperInvariant();
        using var ambiguousScope = await Send(delegated, path, noncanonicalId);
        Assert.Equal(HttpStatusCode.BadRequest, ambiguousScope.StatusCode);

        using var staleContent = await Send(delegated, path,
            Body(catalog: new string('a', 64)));
        Assert.Equal(HttpStatusCode.Conflict, staleContent.StatusCode);

        using var staleRevision = await Send(delegated, path,
            Body(expectedRevision: 1));
        Assert.Equal(HttpStatusCode.Conflict, staleRevision.StatusCode);

        using var foreignCharacter = await Send(delegated, path,
            Body(characterId: Guid.NewGuid().ToString("D")));
        Assert.Equal(HttpStatusCode.BadRequest, foreignCharacter.StatusCode);

        await using var direct = await Fixture.StartAsync();
        using var notDelegated = await Send(direct, path, Body());
        Assert.Equal(HttpStatusCode.Forbidden, notDelegated.StatusCode);
    }

    [Fact]
    public async Task PrivateIngressHasNoBrowserConsumerCatalog()
    {
        await using var f = await Fixture.StartAsync(surface: "PrivateOnly");
        using var response = await f.Client.SendAsync(f.Authorized(Catalog));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
