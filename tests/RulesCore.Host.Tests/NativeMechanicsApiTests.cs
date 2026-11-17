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
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Host.Tests;

public sealed class NativeMechanicsApiTests
{
    private sealed class SiteHandler(string source, bool ordinary = false,
        bool dm = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(JsonSerializer.Serialize(new {
                    contractVersion = 1, toolKey = "rules-core", siteMode = "dorks-and-dice",
                    user = new { id = dm
                        ? "d66a7a7b-bc0f-4677-829d-0ccf40470d2f"
                        : "unit-test" },
                    privateTunnelSourceToolKey = ordinary ? null : source,
                    delegatedFromToolKey = ordinary ? source : null,
                    campaigns = dm ? new object[] {
                        new { id = "99d41ce8-36fc-43df-b8cd-bb1829b20982",
                            role = "DM" }
                    } : Array.Empty<object>()
                }), Encoding.UTF8, "application/json")
            });
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "rc-mechanics-http-" + Guid.NewGuid().ToString("N"));
        public WebApplication App { get; private set; } = null!;
        public HttpClient Client { get; private set; } = null!;
        public async Task StartAsync(string surface, string source, bool ordinary = false,
            bool realNativeContent = false, bool dm = false)
        {
            var package = Path.Combine(_root, "first-party-001");
            Directory.CreateDirectory(Path.Combine(package, "native"));
            File.WriteAllText(Path.Combine(package, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            File.WriteAllText(Path.Combine(package, "native", "class.json"), """
                {"id":"class.demo","name":"Demo","kind":"class","definition":{
                  "progression":[{"level":1,"proficiencyBonus":2,"featureGrants":[]}]}}
                """);
            File.WriteAllText(Path.Combine(package, "native", "equipment.json"), """
                {"equipmentGroups":[
                  {"id":"equipment.group.weapon.martial","name":"Martial","kind":"equipment-group","equipmentCategory":"weapon"}],
                 "equipmentTypes":[
                  {"id":"equipment.weapon.longsword","name":"Longsword","kind":"equipment-type",
                   "equipmentCategory":"weapon","groupIds":["equipment.group.weapon.martial"]},
                  {"id":"equipment.weapon.rapier","name":"Rapier","kind":"equipment-type",
                   "equipmentCategory":"weapon","groupIds":["equipment.group.weapon.martial"]}]}
                """);
            File.WriteAllText(Path.Combine(package, "native", "equipment-feature.json"), """
                {"id":"feature.demo-weapon-training","name":"Training","kind":"feature",
                 "equipmentProficiencyGrants":{"fixed":[
                   {"id":"longsword","targetId":"equipment.weapon.longsword"}]}}
                """);
            File.WriteAllText(Path.Combine(package, "native", "mastery-feature.json"), """
                {"id":"feature.demo-weapon-mastery","name":"Mastery","kind":"feature",
                 "weaponMasteryGrants":{"fixed":[
                   {"id":"longsword","targetId":"equipment.weapon.longsword"}]}}
                """);
            File.WriteAllText(Path.Combine(package, "native", "harvesting.json"), """
                {"id":"procedure.harvesting.loot-tavern-lite",
                 "name":"Harvesting & Crafting Lite","kind":"harvesting-procedure",
                 "source":{"workKey":"loot-tavern.harvesting-crafting-lite",
                   "workDisplayName":"Harvesting & Crafting Lite",
                   "provider":"Loot Tavern","gameEdition":"5e",
                   "releaseKind":"public-release","publicationDate":"2024-07-03",
                   "referenceUri":"https://example.test"},
                 "procedure":{"assessment":{"mechanicKey":"check.harvesting.assessment",
                     "abilityKey":"intelligence","abilityDisplayName":"Intelligence",
                     "defaultRollMode":"normal"},
                   "carving":{"mechanicKey":"check.harvesting.carving",
                     "abilityKey":"dexterity","abilityDisplayName":"Dexterity",
                     "defaultRollMode":"normal"},
                   "totalMechanicKey":"check.harvesting.total",
                   "sameActorRollMode":"disadvantage","componentDcAggregation":"cumulative-in-order",
                   "awardMode":"ordered-prefix","helpers":{"maximumByCreatureSize":
                     {"Tiny":0,"Small":1,"Medium":2,"Large":4,"Huge":6,"Gargantuan":10},
                     "standardHelpActionApplies":false}},
                 "creatureTypes":[{"key":"dragon","displayName":"Dragon",
                   "competencyKey":"competency.survival",
                   "competencyDisplayName":"Survival",
                   "defaultComponents":[{"key":"eye","displayName":"Eye","componentDc":5},
                     {"key":"scale","displayName":"Scale","componentDc":10}]}]}
                """);
            File.WriteAllText(Path.Combine(package, "native", "conditions.json"), """
                {"conditions":[{"id":"condition.frightened","name":"Frightened","kind":"condition",
                  "effects":[{"type":"roll-mode","appliesTo":"attack-roll",
                    "subject":"attacks-by-affected-creature","mode":"disadvantage",
                    "when":{"fearSourceWithinLineOfSight":true}}]}]}
                """);
            // A compact first-party Origin fixture proves that only the
            // authenticated server may populate catalog eligibility evidence.
            File.WriteAllText(Path.Combine(package, "native", "origin-fixture.json"), """
                {"entries":[
                  {"id":"rule.creature-origination","name":"Origination",
                   "kind":"rule","methodDefinitions":[
                     {"id":"biological","name":"Biological"}]},
                  {"id":"rule.low-light-vision","name":"Low-Light Vision","kind":"rule",
                   "effects":[{"actualLightLevel":"dim","perceivedLightLevel":"bright"}]},
                  {"id":"creature-type.humanoid","name":"Humanoid",
                   "kind":"creatureType","parent":"creature",
                   "originationMethods":["biological"]},
                  {"id":"species.human","name":"Human","kind":"species",
                   "selectable":true,"creatureType":"humanoid",
                   "sizeChoice":{"from":["small","medium"],"when":"character-creation"},
                   "walkingSpeedFeet":30},
                  {"id":"species.plain","name":"Plain","kind":"species",
                   "selectable":true,"creatureType":"humanoid",
                   "size":"medium","walkingSpeedFeet":30},
                  {"id":"species.elf","name":"Elf","kind":"species",
                   "selectable":true,"creatureType":"fey","size":"medium",
                   "walkingSpeedFeet":30},
                  {"id":"species.fixture","name":"Fixture Species","kind":"species",
                   "selectable":true,"creatureType":"humanoid",
                   "size":"medium","walkingSpeedFeet":30,"traits":[
                     {"id":"skilled","name":"Skilled",
                      "grantType":"skill-proficiency",
                      "chooseFrom":"any-native-skill","choiceCount":1},
                     {"id":"low-light-vision","name":"Low-Light Vision",
                      "rule":"rule.low-light-vision"}]},
                  {"id":"skill.intimidation","name":"Intimidation","kind":"skill"},
                  {"id":"competency.alchemy","name":"Alchemy","kind":"competency"}
                ]}
                """);
            App = RulesCoreApplication.Create([], builder => {
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                    ["RulesCore:ApiSurface"] = surface,
                    ["Rulesets:ContentRoot"] = realNativeContent ? NativeContentRoot() : _root,
                    ["Rulesets:DefaultRuleset"] = "first-party-001",
                    ["ToolHost:BaseUrl"] = "https://site.test"
                });
            }, services => {
                services.AddHttpClient<CharacterSheetMechanicsTicketValidator>()
                    .ConfigurePrimaryHttpMessageHandler(() => new SiteHandler(source, ordinary, dm));
            });
            await App.StartAsync();
            var server = App.Services.GetRequiredService<IServer>();
            Client = new HttpClient { BaseAddress = new Uri(
                server.Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        }
        // Real-native HTTP tests use the repository's immutable first-party
        // catalog, never an invented miniature Species or trait fixture.
        private static string NativeContentRoot()
        {
            var folder = new DirectoryInfo(AppContext.BaseDirectory);
            while (folder is not null)
            {
                var root = Path.Combine(folder.FullName, "data", "rulesets");
                if (File.Exists(Path.Combine(root, "first-party-001",
                    "native", "rules", "creature-origination.json")))
                    return root;
                folder = folder.Parent;
            }
            throw new InvalidOperationException(
                "Real first-party native ruleset root unavailable.");
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
    public async Task DirectInnateSpellLedgerBootstrapOnlyAcceptsMatchingNativeOriginAndDelegation()
    {
        const string endpoint = "/api/mechanics/v1/origins/innate-spells/ledger/initialize-preview";
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var proposal = new NativeOriginProposal(
            "direct-plain", NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"),
            [], [], [], SelectedSpeciesId: "species.plain");
        var origin = new NativeOriginFirstPartyChoiceEvaluator(catalog)
            .Evaluate(proposal);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, origin.Status);
        Assert.NotNull(origin.Profile);
        var character = new NativeCharacterRequest(catalog.RulesetId, catalog.Fingerprint,
            new Dictionary<string, int>
            {
                ["strength"] = 10, ["dexterity"] = 10,
                ["constitution"] = 10, ["intelligence"] = 10,
                ["wisdom"] = 10, ["charisma"] = 10
            }, [new("class.demo", 1, "class:demo")]);

        var projected = new NativeCharacterProjector(catalog).Project(character);
        Assert.True(projected.Status is "resolved" or "partial",
            "Character: " + JsonSerializer.Serialize(projected.Issues));
        Assert.True(projected.Issues.All(issue => issue.Code is
            "hit-point-input-required" or "movement-input-required" or
            "defense-input-required"),
            "Unexpected character projection: " + JsonSerializer.Serialize(projected.Issues));

        object Valid() => new
        {
            rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
            proposal, committedProfile = origin.Profile, character
        };
        using var anon = await fixture.Client.PostAsJsonAsync(endpoint, Valid());
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);

        using var valid = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Post, endpoint, JsonContent.Create(Valid())));
        Assert.True(valid.StatusCode == HttpStatusCode.OK,
            "Bootstrap failed: " + await valid.Content.ReadAsStringAsync());
        var answer = await valid.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(answer.GetProperty("previewOnly").GetBoolean());
        Assert.True(answer.GetProperty("committedOriginMatched").GetBoolean());
        Assert.Equal("resolved", answer.GetProperty("result")
            .GetProperty("status").GetString());
        Assert.Equal(0, answer.GetProperty("result")
            .GetProperty("state").GetProperty("revision").GetInt32());
        Assert.Empty(answer.GetProperty("result")
            .GetProperty("state").GetProperty("remainingFreeCasts").EnumerateObject());

        using var stale = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Post, endpoint, JsonContent.Create(new
            {
                rulesetId = catalog.RulesetId, fingerprint = "outdated",
                proposal, committedProfile = origin.Profile, character
            })));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        using var forged = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Post, endpoint, JsonContent.Create(new
            {
                rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                proposal, committedProfile = origin.Profile! with { Id = "origin:unrelated" },
                character
            })));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, forged.StatusCode);

        // An otherwise valid Origin must not mask unresolved class levels
        // or injected native option selections in the character kernel.
        using var wrongLevel = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Post, endpoint, JsonContent.Create(new
            {
                rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                proposal, committedProfile = origin.Profile,
                character = character with
                {
                    Advancements = [new NativeAdvancement(
                        "class.demo", 2, "class:demo")]
                }
            })));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongLevel.StatusCode);

        using var wrongOption = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Post, endpoint, JsonContent.Create(new
            {
                rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                proposal, committedProfile = origin.Profile,
                character = character with
                {
                    SelectedOptions = ["option.unverified"]
                }
            })));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongOption.StatusCode);

        using var dmClaim = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Post, endpoint, JsonContent.Create(new
            {
                rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                proposal = proposal with
                {
                    TableDecisions = [new NativeTableRuleDecision(
                        "spoofed", new("self", "self"), new("campaign", "other"),
                        new("source", "species.plain"),
                        JsonSerializer.SerializeToElement(new { eligibility = "eligible" }))]
                },
                committedProfile = origin.Profile, character
            })));
        Assert.Equal(HttpStatusCode.BadRequest, dmClaim.StatusCode);

        await using var wrongSource = new Fixture();
        await wrongSource.StartAsync("PublicOnly", "wiki", ordinary: true);
        using var denied = await wrongSource.Client.SendAsync(wrongSource.Authenticated(
            HttpMethod.Post, endpoint, JsonContent.Create(Valid())));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        await using var privateOnly = new Fixture();
        await privateOnly.StartAsync("PrivateOnly", "character-sheet");
        using var absent = await privateOnly.Client.SendAsync(privateOnly.Authenticated(
            HttpMethod.Post, "/api/private/mechanics/v1/origins/innate-spells/ledger/initialize-preview",
            JsonContent.Create(Valid())));
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
    }

    [Theory]
    [InlineData("species.elf", "trance", 4)]
    [InlineData("species.orc", "relentlessEndurance", 1)]
    public async Task RealNativeDirectHttpPreviewReturnsSourceVerifiedSurvivalEffect(
        string speciesId, string effectKey, int expectedValue)
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet",
            ordinary: true, realNativeContent: true);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>()
            .Snapshot!;
        var proposal = new NativeOriginProposal("http-native-" + speciesId,
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"),
            [], [], [], SelectedSpeciesId: speciesId);
        var construction = new NativeOriginConstructionEngine(catalog).Evaluate(
            new(proposal, AutoManifestDirectDefaults: true));
        Assert.Equal(NativeOriginConstructionStatus.Authorized,
            construction.Status);
        Assert.NotNull(construction.Profile);

        // Send the actual selected native manifestations through authenticated
        // HTTP, not a manufactured projected response.
        using var request = fixture.Authenticated(HttpMethod.Post,
            "/api/mechanics/v1/origins/preview", JsonContent.Create(new
            {
                rulesetId = catalog.RulesetId,
                fingerprint = catalog.Fingerprint,
                proposal = proposal with
                {
                    Manifestations = construction.EvaluatedProposal.Manifestations
                }
            }));
        using var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)NativeOriginConstructionStatus.Authorized,
            body.GetProperty("result").GetProperty("status").GetInt32());
        Assert.True(body.GetProperty("previewOnly").GetBoolean());
        var view = body.GetProperty("restSurvival");
        Assert.Equal("resolved", view.GetProperty("status").GetString());
        Assert.True(view.GetProperty("previewOnly").GetBoolean());
        var effect = view.GetProperty(effectKey);
        Assert.Equal(speciesId, effect.GetProperty("sourceId").GetString());
        Assert.NotEmpty(effect.GetProperty("grantId").GetString()!);
        Assert.StartsWith("/traits/", effect.GetProperty("occurrencePath")
            .GetString());
        if (effectKey == "trance")
        {
            Assert.Equal(expectedValue,
                effect.GetProperty("longRestHours").GetInt32());
            Assert.False(effect.GetProperty("sleepRequired").GetBoolean());
            Assert.Equal(JsonValueKind.Null,
                view.GetProperty("relentlessEndurance").ValueKind);
        }
        else
        {
            Assert.Equal(expectedValue, effect.GetProperty("uses").GetInt32());
            Assert.Equal(1, effect.GetProperty("hitPointsInstead").GetInt32());
            Assert.Equal("long-rest", effect.GetProperty("recharge").GetString());
            Assert.Equal(JsonValueKind.Null,
                view.GetProperty("trance").ValueKind);
        }
    }

    [Fact]
    public async Task RealNativeScopedDmHttpPreviewPreservesSelectedElfOrcEffects()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet",
            ordinary: true, realNativeContent: true, dm: true);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>()
            .Snapshot!;
        const string actor = "d66a7a7b-bc0f-4677-829d-0ccf40470d2f";
        const string campaign = "99d41ce8-36fc-43df-b8cd-bb1829b20982";
        const string approval = "approved:real-native-selected-effects";

        var proposal = new NativeOriginProposal("http-native-scoped-mixed",
            NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [
                new("elf", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.elf")),
                new("orc", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.orc"))
            ], [], []);
        var offers = new NativeOriginConstructionEngine(catalog).Evaluate(
            new(proposal)).EvaluatedProposal.Candidates;
        var physical = offers.Where(x => x.ContributorId == "elf" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        Assert.Equal(3, physical.Length);
        var trance = Assert.Single(offers,
            x => x.Identity.MechanicalDefinitionId == "trait.elf.trance");
        var endurance = Assert.Single(offers,
            x => x.Identity.MechanicalDefinitionId ==
                "trait.orc.relentless-endurance");
        Assert.Equal(NativeOriginEligibility.Eligible,
            trance.SelectionBasis.Eligibility);
        Assert.Equal(NativeOriginEligibility.Eligible,
            endurance.SelectionBasis.Eligibility);
        var manifested = physical.Select((x, i) => new NativeOriginManifestation(
                "physical-" + i, x.Id, SelectedValue: x.OfferedValue))
            .Concat([
                new("chosen-trance", trance.Id),
                new("chosen-endurance", endurance.Id)
            ]).ToArray();

        // The actual Site-introspected actor is the only acceptable source
        // of DM authority. This decision selects the exact Orc source and
        // does not alter the native Endurance payload or mint a new pool.
        var decision = new NativeTableRuleDecision("dm-orc-source",
            new(actor, approval), new("campaign", campaign),
            new("source", "species.orc"),
            JsonSerializer.SerializeToElement(new { eligibility = "eligible" }));
        var submitted = proposal with
        {
            Manifestations = manifested,
            TableDecisions = [decision]
        };
        using var request = fixture.Authenticated(HttpMethod.Post,
            "/api/mechanics/v1/origins/evaluate-approved-mixed",
            JsonContent.Create(new
            {
                rulesetId = catalog.RulesetId,
                fingerprint = catalog.Fingerprint,
                characterId = "44eb66e6-3b5c-44e2-aa71-4986d4b346c0",
                campaignId = campaign,
                approvalEvidenceId = approval,
                proposal = submitted
            }));
        using var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)NativeOriginConstructionStatus.Authorized,
            body.GetProperty("result").GetProperty("status").GetInt32());
        Assert.True(body.GetProperty("previewOnly").GetBoolean());
        var survival = body.GetProperty("restSurvival");
        Assert.Equal("resolved", survival.GetProperty("status").GetString());
        var elf = survival.GetProperty("trance");
        Assert.Equal("species.elf", elf.GetProperty("sourceId").GetString());
        Assert.Equal(4, elf.GetProperty("longRestHours").GetInt32());
        var orc = survival.GetProperty("relentlessEndurance");
        Assert.Equal("species.orc", orc.GetProperty("sourceId").GetString());
        Assert.Equal(1, orc.GetProperty("uses").GetInt32());
        Assert.Equal("long-rest", orc.GetProperty("recharge").GetString());
        Assert.Equal("resolved",
            body.GetProperty("intrinsics").GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null,
            body.GetProperty("result").GetProperty("profile").ValueKind);
    }

    [Fact]
    public async Task OriginPreviewRejectsNullKeysAndContributorRoleBeforeNativeEvaluation()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true);
        const string endpoint = "/api/mechanics/v1/origins/preview";
        var direct = new NativeOriginProposal("null-nested-inputs",
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"),
            [], [], [], SelectedSpeciesId: "species.fixture");

        // JsonContent actually writes these nulls to the authenticated HTTP
        // request. Nonnullable C# annotations do not enforce JSON presence.
        var nullCandidateId = direct with
        {
            Manifestations = [new("missing-candidate-id", null!,
                Choices: [])]
        };
        var nullRequirementId = direct with
        {
            Manifestations = [new("missing-choice-key",
                "direct:species.fixture:/traits/0",
                Choices: [new(null!, ["skill.intimidation"])] )]
        };
        var nullContributorRole = new NativeOriginProposal(
            "missing-parent-role", NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [new("parent", null!,
                new(NativeOriginSourceKind.Native, "species.elf"))],
            [], []);

        foreach (var invalid in new[]
            { nullCandidateId, nullRequirementId, nullContributorRole })
        {
            using var message = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new
                {
                    rulesetId = "first-party-001", proposal = invalid
                }));
            using var response = await fixture.Client.SendAsync(message);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("invalid_origin_preview_request",
                payload.GetProperty("error").GetString());
            Assert.False(payload.TryGetProperty("result", out _));
            Assert.False(payload.TryGetProperty("intrinsics", out _));
        }
    }

    [Fact]
    public async Task OriginPreviewIncludesReadOnlyEffectViewsWithoutCreatingGrants()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true);
        const string endpoint = "/api/mechanics/v1/origins/preview";

        async Task<JsonElement> Preview(NativeOriginProposal proposal)
        {
            using var request = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new
                {
                    rulesetId = "first-party-001", fingerprint = "outdated",
                    proposal
                }));
            using var response = await fixture.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(payload.GetProperty("previewOnly").GetBoolean());
            Assert.Equal(1, payload.GetProperty("contractVersion").GetInt32());
            return payload;
        }

        var plain = await Preview(new("plain-no-effects",
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedSpeciesId: "species.plain"));
        Assert.Equal("resolved", plain.GetProperty("intrinsics")
            .GetProperty("status").GetString());
        Assert.Equal((int)NativeOriginConstructionStatus.Authorized,
            plain.GetProperty("result").GetProperty("status").GetInt32());

        foreach (var key in new[]
            { "physiology", "movementModes", "skillProficiencies", "restSurvival" })
        {
            var effect = plain.GetProperty(key);
            Assert.True(effect.GetProperty("previewOnly").GetBoolean());
            Assert.Equal("none", effect.GetProperty("status").GetString());
            Assert.Empty(effect.GetProperty("unresolvedReasons").EnumerateArray());
        }
        Assert.Empty(plain.GetProperty("movementModes")
            .GetProperty("modes").EnumerateArray());
        Assert.Empty(plain.GetProperty("skillProficiencies")
            .GetProperty("proficiencies").EnumerateArray());
        Assert.Equal(JsonValueKind.Null,
            plain.GetProperty("restSurvival").GetProperty("trance").ValueKind);
        Assert.Equal(JsonValueKind.Null,
            plain.GetProperty("restSurvival")
                .GetProperty("relentlessEndurance").ValueKind);

        // The Human has an unbound native size choice. An incomplete
        // construction may show offered traits, but it publishes no active
        // physiological, skill, movement, or survival result.
        var unfinished = await Preview(new("human-unfinished",
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedSpeciesId: "species.human"));
        Assert.NotEqual((int)NativeOriginConstructionStatus.Authorized,
            unfinished.GetProperty("result").GetProperty("status").GetInt32());
        foreach (var key in new[]
            { "physiology", "movementModes", "skillProficiencies", "restSurvival" })
        {
            var view = unfinished.GetProperty(key);
            Assert.Equal("unresolved", view.GetProperty("status").GetString());
            Assert.True(view.GetProperty("previewOnly").GetBoolean());
            Assert.NotEmpty(view.GetProperty("unresolvedReasons").EnumerateArray());
        }
    }

    [Fact]
    public async Task OriginPreviewRejectsUntrustedBaselineAndDoesNotRequireFixedFingerprint()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true);
        const string endpoint = "/api/mechanics/v1/origins/preview";
        var clean = new NativeOriginProposal(
            "draft-1", NativeOriginIntent.Direct,
            new NativeOriginMethodReference("unknown-method-rule", "direct"),
            [], [], []);

        using var anonymous = await fixture.Client.PostAsJsonAsync(endpoint, new
        {
            rulesetId = "first-party-001", fingerprint = "outdated",
            proposal = clean
        });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var wrongIdentity = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Post, endpoint, JsonContent.Create(new {
                rulesetId = "unrelated-ruleset", fingerprint = "outdated",
                proposal = clean
            })));
        Assert.Equal(HttpStatusCode.Conflict, wrongIdentity.StatusCode);

        using var forgedBaseline = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Post, endpoint, JsonContent.Create(new {
                rulesetId = "first-party-001", fingerprint = "outdated",
                proposal = clean with {
                    ExistingConfiguration = new NativeOriginCurrentConfiguration(
                        "species.human", null, [])
                }
            })));
        Assert.Equal(HttpStatusCode.BadRequest, forgedBaseline.StatusCode);
        var denied = await forgedBaseline.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("trusted_origin_state_required",
            denied.GetProperty("error").GetString());

        // A stateless preview cannot authenticate a manifested biological
        // parent or a previously completed transformation supplied by the caller.
        var untrustedContributor = clean with
        {
            Contributors = [new NativeOriginContributor("claimed-parent",
                new(NativeOriginInputKind.BiologicalParent),
                new(NativeOriginSourceKind.ManifestedProfile, "claimed-parent-state"),
                new NativeOriginCurrentConfiguration("species.human", null, []))]
        };
        var forgedDecision = clean with
        {
            Resolutions = [new NativeOriginResolution("relationship",
                NativeOriginResolutionAction.RetainFirst,
                new(NativeOriginAdjudication.DmAuthorized,
                    TableDecisionId: "unverified-dm-decision"))]
        };
        var forgedRequirement = clean with
        {
            InputRequirements = [new NativeOriginInputRequirement("claimed-requirement",
                new(NativeOriginInputKind.Material), 0,
                TableDecisionId: "claimed-decision")]
        };
        foreach (var unsafeProposal in new[]
            { untrustedContributor, forgedDecision, forgedRequirement })
        {
            using var unsafeRequest = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new
                {
                    rulesetId = "first-party-001", fingerprint = "outdated",
                    proposal = unsafeProposal
                }));
            using var refused = await fixture.Client.SendAsync(unsafeRequest);
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            var refusal = await refused.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("trusted_origin_state_required",
                refusal.GetProperty("error").GetString());
        }

        // First-party option eligibility is verified from the current native
        // catalog. A client must not present even a correctly formatted proof
        // as if it were an independently authenticated entitlement.
        var selfAssertedChoice = clean with
        {
            Manifestations = [new NativeOriginManifestation("choice-1", "unknown",
                Choices: [new NativeOriginChoiceSelection("selection",
                    ["skill.intimidation"], "native-catalog:spoof")])]
        };
        using var forgedChoiceRequest = fixture.Authenticated(HttpMethod.Post, endpoint,
            JsonContent.Create(new
            {
                rulesetId = "first-party-001", fingerprint = "outdated",
                proposal = selfAssertedChoice
            }));
        using var forgedChoice = await fixture.Client.SendAsync(forgedChoiceRequest);
        Assert.Equal(HttpStatusCode.BadRequest, forgedChoice.StatusCode);
        var deniedChoice = await forgedChoice.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("trusted_origin_state_required",
            deniedChoice.GetProperty("error").GetString());

        // An ordinary content update changes the fingerprint, not user permission.
        using var previewRequest = fixture.Authenticated(HttpMethod.Post, endpoint,
            JsonContent.Create(new { rulesetId = "first-party-001",
                fingerprint = "outdated", proposal = clean }));
        using var preview = await fixture.Client.SendAsync(previewRequest);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var content = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(content.GetProperty("previewOnly").GetBoolean());
        Assert.True(content.GetProperty("contentChangedSinceRequest").GetBoolean());
        Assert.Equal("first-party-001", content.GetProperty("rulesetId").GetString());
        Assert.True(content.GetProperty("result").TryGetProperty("issues", out _));
    }

    [Fact]
    public async Task OriginPreviewBoundsUntrustedCollectionsBeforeNativeEvaluation()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true);
        const string endpoint = "/api/mechanics/v1/origins/preview";
        var clean = new NativeOriginProposal(
            "bounded-preview", NativeOriginIntent.Direct,
            new NativeOriginMethodReference("rule.creature-origination", "biological"),
            [], [], []);
        var tooManyContributors = clean with {
            Contributors = Enumerable.Range(0, 33).Select(index =>
                new NativeOriginContributor(
                    "source-" + index,
                    new NativeOriginInputRole(NativeOriginInputKind.BiologicalParent),
                    new NativeOriginSourceReference(
                        NativeOriginSourceKind.Native, "species.fixture"))).ToArray()
        };
        var tooManyManifestations = clean with {
            Manifestations = Enumerable.Range(0, 257).Select(index =>
                new NativeOriginManifestation("offered-" + index, "candidate-" + index))
                .ToArray()
        };
        var tooManyRequirements = clean with {
            InputRequirements = Enumerable.Range(0, 129).Select(index =>
                new NativeOriginInputRequirement(
                    "requirement-" + index,
                    new NativeOriginInputRole(NativeOriginInputKind.Material), 0)).ToArray()
        };
        var tooManyChoices = clean with {
            Manifestations = [new NativeOriginManifestation("choice", "offered",
                Choices: Enumerable.Range(0, 33).Select(index =>
                    new NativeOriginChoiceSelection(
                        "requirement-" + index, ["skill.fixture"])).ToArray())]
        };
        foreach (var oversized in new[] {
            tooManyContributors, tooManyManifestations,
            tooManyRequirements, tooManyChoices
        })
        {
            using var request = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new {
                    rulesetId = "first-party-001", fingerprint = "outdated",
                    proposal = oversized
                }));
            using var response = await fixture.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("origin_request_limits_exceeded",
                body.GetProperty("error").GetString());
        }
        // A normal sized request continues through the existing evaluator.
        using var acceptedRequest = fixture.Authenticated(HttpMethod.Post, endpoint,
            JsonContent.Create(new {
                rulesetId = "first-party-001", fingerprint = "outdated",
                proposal = clean
            }));
        using var accepted = await fixture.Client.SendAsync(acceptedRequest);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task HumanElfPreviewRejectsMalformedIntrinsicChoiceWithoutHttp500()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true);
        const string endpoint = "/api/mechanics/v1/origins/preview";
        var humanElf = new NativeOriginProposal(
            "malformed-human-elf", NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [new("parent-one", new(NativeOriginInputKind.BiologicalParent),
                new(NativeOriginSourceKind.Native, "species.human")),
             new("parent-two", new(NativeOriginInputKind.BiologicalParent),
                new(NativeOriginSourceKind.Native, "species.elf"))],
            [], []);
        var speedId = "contributor:parent-one:species.human:/walkingSpeedFeet";

        async Task<(HttpStatusCode Status, JsonElement Body)> Preview(
            NativeOriginProposal proposal)
        {
            using var message = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new {
                    rulesetId = "first-party-001", fingerprint = "outdated",
                    proposal
                }));
            using var response = await fixture.Client.SendAsync(message);
            return (response.StatusCode,
                await response.Content.ReadFromJsonAsync<JsonElement>());
        }

        // Exact two-forged-requirement reproduction from independent review.
        var bad = humanElf with { Manifestations = [
            new("bad-multiple-choices", speedId, Choices:
                [new("forged-one", ["x"]), new("forged-two", ["y"])])] };
        var outcome = await Preview(bad);
        Assert.Equal(HttpStatusCode.OK, outcome.Status);
        Assert.NotEqual((int)NativeOriginConstructionStatus.Authorized,
            outcome.Body.GetProperty("result").GetProperty("status").GetInt32());
        Assert.Contains(outcome.Body.GetProperty("result").GetProperty("issues")
                .EnumerateArray(),
            issue => issue.GetProperty("severity").GetInt32() ==
                (int)NativeOriginIssueSeverity.Invalid);
        // Invalid proposals may have no reconstructed profile at all. If a
        // partial profile is returned, it still must not contain the grant.
        var malformedProfile = outcome.Body.GetProperty("result").GetProperty("profile");
        if (malformedProfile.ValueKind == JsonValueKind.Object)
            Assert.Empty(malformedProfile.GetProperty("underlying")
                .GetProperty("inherentGrants").EnumerateArray());
        else
            Assert.Equal(JsonValueKind.Null, malformedProfile.ValueKind);

        // A null nested record is a controlled input error at HTTP ingress.
        using var nullRequest = fixture.Authenticated(HttpMethod.Post, endpoint,
            JsonContent.Create(new {
                rulesetId = "first-party-001", proposal = humanElf with {
                    Manifestations = [new NativeOriginManifestation("null", speedId,
                        Choices: [null!])] }
            }));
        using var nullResponse = await fixture.Client.SendAsync(nullRequest);
        Assert.Equal(HttpStatusCode.BadRequest, nullResponse.StatusCode);

        foreach (var selections in new IReadOnlyList<NativeOriginChoiceSelection>[]
            {
                [new("unknown", null!)],
                [new("duplicate", ["x"]), new("duplicate", ["x"])],
                [new("conflict", ["x"]), new("conflict", ["y"])]
            })
        {
            var malformed = await Preview(humanElf with { Manifestations =
                [new NativeOriginManifestation("bad", speedId, Choices: selections)] });
            Assert.Equal(HttpStatusCode.OK, malformed.Status);
            Assert.NotEqual((int)NativeOriginConstructionStatus.Authorized,
                malformed.Body.GetProperty("result").GetProperty("status").GetInt32());
        }

        // Ordinary Human–Elf preview remains available and grants nothing
        // until campaign DM authority is independently verified.
        var ordinary = await Preview(humanElf);
        Assert.Equal(HttpStatusCode.OK, ordinary.Status);
        Assert.True(ordinary.Body.GetProperty("previewOnly").GetBoolean());
        Assert.Empty(ordinary.Body.GetProperty("result").GetProperty("profile")
            .GetProperty("underlying").GetProperty("inherentGrants").EnumerateArray());
    }

    [Fact]
    public async Task NativeChoicePreviewMintsCatalogEvidenceOnlyForRealNativeSkills()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true);
        const string endpoint = "/api/mechanics/v1/origins/preview";
        var proposal = new NativeOriginProposal(
            "fixture-skill", NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [],
            [new NativeOriginManifestation("fixture-selected",
                "direct:species.fixture:/traits/0",
                Choices: [new("selection", ["skill.intimidation"])])],
            SelectedSpeciesId: "species.fixture");

        using var response = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Post, endpoint, JsonContent.Create(new
            {
                rulesetId = "first-party-001",
                fingerprint = "old-contents-are-not-auth",
                proposal
            })));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(document.GetProperty("previewOnly").GetBoolean());
        Assert.True(document.GetProperty("contentChangedSinceRequest").GetBoolean());
        var result = document.GetProperty("result");
        Assert.Equal((int)NativeOriginConstructionStatus.Authorized,
            result.GetProperty("status").GetInt32());
        var proof = result.GetProperty("evaluatedProposal").GetProperty("manifestations")
            .EnumerateArray().Single(x =>
                x.GetProperty("id").GetString() == "fixture-selected")
            .GetProperty("choices")[0]
            .GetProperty("catalogEligibilityEvidenceId").GetString();
        Assert.Equal("native-catalog:first-party-001:" +
            document.GetProperty("fingerprint").GetString(), proof);
        Assert.Equal((int)NativeOriginEventStage.Completed,
            result.GetProperty("profile").GetProperty("history")[0]
                .GetProperty("stage").GetInt32());
        var intrinsics = document.GetProperty("intrinsics");
        Assert.True(intrinsics.GetProperty("previewOnly").GetBoolean());
        Assert.Equal("resolved", intrinsics.GetProperty("status").GetString());
        Assert.Empty(intrinsics.GetProperty("unresolvedSlots").EnumerateArray());
        Assert.Equal("medium", intrinsics.GetProperty("size")
            .GetProperty("value").GetString());
        Assert.Equal(30, intrinsics.GetProperty("walkingSpeedFeet")
            .GetProperty("value").GetInt32());
        Assert.Equal("species.fixture", intrinsics.GetProperty("walkingSpeedFeet")
            .GetProperty("sourceId").GetString());
        Assert.Equal("/walkingSpeedFeet", intrinsics.GetProperty("walkingSpeedFeet")
            .GetProperty("occurrencePath").GetString());
        var vision = document.GetProperty("vision");
        Assert.Equal("resolved", vision.GetProperty("status").GetString());
        Assert.True(vision.GetProperty("previewOnly").GetBoolean());
        Assert.Empty(vision.GetProperty("unresolvedReasons").EnumerateArray());
        var lowLight = vision.GetProperty("lowLightVision");
        Assert.Equal("species.fixture", lowLight.GetProperty("sourceId").GetString());
        Assert.Equal("/traits/1", lowLight.GetProperty("occurrencePath").GetString());
        Assert.Equal("rule.low-light-vision", lowLight.GetProperty("ruleId").GetString());
        Assert.Equal("dim", lowLight.GetProperty("actualLightLevel").GetString());
        Assert.Equal("bright", lowLight.GetProperty("perceivedLightLevel").GetString());


        // An unrelated category can never be approved by the same Skill grant.
        using var wrongCatalog = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Post, endpoint, JsonContent.Create(new
            {
                rulesetId = "first-party-001",
                proposal = proposal with
                {
                    Manifestations = [new NativeOriginManifestation(
                        "fixture-selected", "direct:species.fixture:/traits/0",
                        Choices: [new("selection", ["competency.alchemy"])])]
                }
            })));
        Assert.Equal(HttpStatusCode.OK, wrongCatalog.StatusCode);
        var denied = await wrongCatalog.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual((int)NativeOriginConstructionStatus.Authorized,
            denied.GetProperty("result").GetProperty("status").GetInt32());
        Assert.Equal("unresolved",
            denied.GetProperty("intrinsics").GetProperty("status").GetString());
        Assert.Equal("unresolved",
            denied.GetProperty("vision").GetProperty("status").GetString());
        Assert.True(denied.GetProperty("intrinsics")
            .GetProperty("previewOnly").GetBoolean());

        Assert.Contains(denied.GetProperty("result").GetProperty("issues").EnumerateArray(),
            x => x.GetProperty("code").GetString() == "choice-authorization-unresolved");
    }

    [Theory]
    [InlineData("PublicOnly", false)]
    [InlineData("PrivateOnly", true)]
    public async Task EquipmentQualificationEndpointsEnforceAuthAndResolveCoverage(
        string surface, bool privateIngress)
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync(surface, "character-sheet", ordinary: !privateIngress);
        var prefix = privateIngress ? "/api/private/mechanics/v1" : "/api/mechanics/v1";
        using var info = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, prefix + "/ruleset"));
        Assert.Equal(HttpStatusCode.OK, info.StatusCode);
        var metadata = await info.Content.ReadFromJsonAsync<JsonElement>();
        var fingerprint = metadata.GetProperty("fingerprint").GetString();
        Assert.Equal("authored-scopes-only", metadata.GetProperty("capabilities")
            .GetProperty("equipmentQualifications").GetString());

        var qualifications = new
        {
            rulesetId = "first-party-001", fingerprint,
            sources = new[] { new {
                sourceId = "feature.demo-weapon-training", occurrenceId = "test-entry",
                grantMode = "standard"
            }}
        };
        var url = prefix + "/characters/equipment-proficiencies";
        using var anonymous = await fixture.Client.PostAsJsonAsync(url, qualifications);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var request = fixture.Authenticated(HttpMethod.Post, url,
            JsonContent.Create(qualifications));
        using var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var resolved = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("resolved", resolved.GetProperty("status").GetString());
        Assert.Equal("equipment.weapon.longsword",
            resolved.GetProperty("qualifications")[0].GetProperty("qualificationId").GetString());

        var coverageUrl = url + "/coverage";
        using var coverageRequest = fixture.Authenticated(HttpMethod.Post, coverageUrl,
            JsonContent.Create(new {
                qualifications, equipmentId = "equipment.weapon.longsword"
            }));
        using var coverageResponse = await fixture.Client.SendAsync(coverageRequest);
        Assert.Equal(HttpStatusCode.OK, coverageResponse.StatusCode);
        var coverage = await coverageResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(coverage.GetProperty("coverage").GetProperty("proficient").GetBoolean());
        Assert.Equal("test-entry", coverage.GetProperty("coverage")
            .GetProperty("provenance")[0].GetProperty("sourceOccurrenceId").GetString());

        using var otherRequest = fixture.Authenticated(HttpMethod.Post, coverageUrl,
            JsonContent.Create(new { qualifications,
                equipmentId = "equipment.weapon.rapier" }));
        using var otherResponse = await fixture.Client.SendAsync(otherRequest);
        Assert.Equal(HttpStatusCode.OK, otherResponse.StatusCode);
        var other = await otherResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(other.GetProperty("coverage").GetProperty("proficient").GetBoolean());

        using var staleRequest = fixture.Authenticated(HttpMethod.Post, url,
            JsonContent.Create(new {
                rulesetId = "first-party-001", fingerprint = "stale",
                qualifications.sources
            }));
        using var stale = await fixture.Client.SendAsync(staleRequest);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Theory]
    [InlineData("PublicOnly", false)]
    [InlineData("PrivateOnly", true)]
    public async Task EquipmentTrainingEndpointsResolveSourceBasedTwoBitMasks(
        string surface, bool privateIngress)
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync(surface, "character-sheet", ordinary: !privateIngress);
        var prefix = privateIngress ? "/api/private/mechanics/v1" : "/api/mechanics/v1";
        using var info = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, prefix + "/ruleset"));
        var metadata = await info.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("proficiency-and-mastery-flags",
            metadata.GetProperty("capabilities").GetProperty("equipmentTraining").GetString());
        var fingerprint = metadata.GetProperty("fingerprint").GetString();

        var proficiencies = new {
            rulesetId = "first-party-001", fingerprint,
            sources = new[] { new {
                sourceId = "feature.demo-weapon-training",
                occurrenceId = "proficient", grantMode = "standard"
            }}
        };
        var training = new {
            proficiencies,
            masterySources = new[] { new {
                sourceId = "feature.demo-weapon-mastery", occurrenceId = "mastered"
            }}
        };
        var url = prefix + "/characters/equipment-training";
        using var anonymous = await fixture.Client.PostAsJsonAsync(url, training);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var resolveRequest = fixture.Authenticated(HttpMethod.Post, url,
            JsonContent.Create(training));
        using var resolveResponse = await fixture.Client.SendAsync(resolveRequest);
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);
        var resolved = await resolveResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("resolved", resolved.GetProperty("status").GetString());
        Assert.Equal("equipment.weapon.longsword",
            resolved.GetProperty("masteries")[0].GetProperty("targetId").GetString());

        var coverageUrl = url + "/coverage";
        using var coverageRequest = fixture.Authenticated(HttpMethod.Post, coverageUrl,
            JsonContent.Create(new { training, equipmentId = "equipment.weapon.longsword" }));
        using var coverageResponse = await fixture.Client.SendAsync(coverageRequest);
        Assert.Equal(HttpStatusCode.OK, coverageResponse.StatusCode);
        var covered = await coverageResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, covered.GetProperty("coverage").GetProperty("flags").GetInt32());
        Assert.Equal("mastered", covered.GetProperty("coverage")
            .GetProperty("masteryProvenance")[0].GetProperty("sourceOccurrenceId").GetString());
        Assert.Equal("proficient", covered.GetProperty("coverage")
            .GetProperty("proficiencyProvenance")[0].GetProperty("sourceOccurrenceId").GetString());

        using var otherRequest = fixture.Authenticated(HttpMethod.Post, coverageUrl,
            JsonContent.Create(new { training, equipmentId = "equipment.weapon.rapier" }));
        using var otherResponse = await fixture.Client.SendAsync(otherRequest);
        var other = await otherResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, other.GetProperty("coverage").GetProperty("flags").GetInt32());

        // Mastery alone is a valid raw mask (2), but not usable mastery.
        var emptyProficiencies = new {
            rulesetId = "first-party-001", fingerprint, sources = Array.Empty<object>()
        };
        var masteryOnly = new { proficiencies = emptyProficiencies, training.masterySources };
        using var dormantRequest = fixture.Authenticated(HttpMethod.Post, coverageUrl,
            JsonContent.Create(new { training = masteryOnly,
                equipmentId = "equipment.weapon.longsword" }));
        using var dormantResponse = await fixture.Client.SendAsync(dormantRequest);
        var dormant = await dormantResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, dormant.GetProperty("coverage").GetProperty("flags").GetInt32());

        using var staleRequest = fixture.Authenticated(HttpMethod.Post, url,
            JsonContent.Create(new {
                proficiencies = new {
                    rulesetId = "first-party-001", fingerprint = "stale", proficiencies.sources
                },
                training.masterySources
            }));
        using var staleResponse = await fixture.Client.SendAsync(staleRequest);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
    }

    [Fact]
    public async Task AuthorizedHarvestingUsesNativeSnapshotAndRejectsUnsignedRequests()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true);
        using var metadataResponse = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/mechanics/v1/ruleset"));
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        var metadata = await metadataResponse.Content.ReadFromJsonAsync<JsonElement>();
        var fingerprint = metadata.GetProperty("fingerprint").GetString();
        Assert.Equal("type-monster-manual-edits", metadata.GetProperty("capabilities")
            .GetProperty("harvestingResolution").GetString());
        var url = "/api/mechanics/v1/harvesting?fingerprint=" + fingerprint;
        using var anonymous = await fixture.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var catalogResponse = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, url));
        Assert.Equal(HttpStatusCode.OK, catalogResponse.StatusCode);
        var catalog = await catalogResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Loot Tavern", catalog.GetProperty("source")
            .GetProperty("provider").GetString());
        Assert.Single(catalog.GetProperty("creatureTypes").EnumerateArray());

        var type = new {
            rulesetId = "first-party-001", fingerprint, creatureType = "dragon",
            manualEdits = new { upsertComponents = new[] {
                new { key = "eye", displayName = "Eye", componentDc = 7, quantity = 2 }
            } }
        };
        using var tableResponse = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Post, "/api/mechanics/v1/harvesting/resolve",
            JsonContent.Create(type)));
        Assert.Equal(HttpStatusCode.OK, tableResponse.StatusCode);
        var table = await tableResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(7, table.GetProperty("components")[0].GetProperty("componentDc").GetInt32());
        Assert.Equal("manual", table.GetProperty("components")[0].GetProperty("origin").GetString());

        using var outcomeResponse = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Post, "/api/mechanics/v1/harvesting/outcome",
            JsonContent.Create(new {
                table = type, assessmentResult = 8, carvingResult = 8,
                sameActor = false, harvestOrderComponentKeys = new[] { "eye", "scale" },
                creatureSize = "Medium", helpers = new[] {
                    new { proficiencyBonus = 4, isProficient = true }
                }
            })));
        Assert.Equal(HttpStatusCode.OK, outcomeResponse.StatusCode);
        var outcome = await outcomeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(20, outcome.GetProperty("harvestingResult").GetInt32());
        Assert.Equal(7, outcome.GetProperty("components")[0].GetProperty("harvestDc").GetInt32());
        Assert.Equal(17, outcome.GetProperty("components")[1].GetProperty("harvestDc").GetInt32());
        Assert.True(outcome.GetProperty("components")[1].GetProperty("awarded").GetBoolean());
        using var stale = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/mechanics/v1/harvesting?fingerprint=stale"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task NativeManufacturingChecksAreAuthenticatedAndUseCurrentSnapshot()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true);
        const string endpoint = "/api/mechanics/v1/crafting/manufacturing/resolve";
        using var metadataResponse = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Get, "/api/mechanics/v1/ruleset"));
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        var metadata = await metadataResponse.Content.ReadFromJsonAsync<JsonElement>();
        var fingerprint = metadata.GetProperty("fingerprint").GetString();
        Assert.Equal("manual-competency-subset", metadata.GetProperty("capabilities")
            .GetProperty("manufacturingResolution").GetString());
        var input = new {
            rulesetId = "first-party-001", fingerprint,
            competency = new { displayName = "Smithing", contribution = 3, isQualified = true },
            manualAbilityModifier = 2, d20Roll = 15, otherModifier = 0, targetDc = 20
        };
        using var anonymous = await fixture.Client.PostAsJsonAsync(endpoint, input);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var response = await fixture.Client.SendAsync(fixture.Authenticated(
            HttpMethod.Post, endpoint, JsonContent.Create(input)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("resolved", result.GetProperty("status").GetString());
        Assert.Equal("completed", result.GetProperty("outcome").GetString());
        Assert.Equal(20, result.GetProperty("total").GetInt32());
        Assert.True(result.GetProperty("inputsConsumed").GetBoolean());

        using var staleRequest = fixture.Authenticated(HttpMethod.Post, endpoint,
            JsonContent.Create(new {
                rulesetId = "first-party-001", fingerprint = "stale",
                input.competency, input.manualAbilityModifier,
                input.d20Roll, input.targetDc
            }));
        using var stale = await fixture.Client.SendAsync(staleRequest);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
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
        foreach (var category in new[] { "species", "ancestry", "skill", "competency", "monster", "item", "feat", "spell" })
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
