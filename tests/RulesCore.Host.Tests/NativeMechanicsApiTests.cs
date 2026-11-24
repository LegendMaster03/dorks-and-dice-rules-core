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
            bool realNativeContent = false, bool dm = false,
            INativeSupernaturalHostAuthorityProvider? supernaturalAuthority = null,
            INativeOriginHostBiologicalAuthorityProvider? biologicalAuthority = null,
            INativeOriginHostReconstructionAuthorityProvider? reconstructionAuthority = null,
            INativeLifeExperienceHostAuthorityProvider? lifeExperienceAuthority = null,
            INativeScopedSkillCheckHostAuthorityProvider? scopedCheckAuthority = null,
            INativeOriginHostFeatChoiceAuthorityProvider? featChoiceAuthority = null,
            INativeOriginHostCantripRestAuthorityProvider? cantripRestAuthority = null,
            bool syntheticOpenChoiceCatalog = false,
            INativeConstructHostOperatingAuthorityProvider? constructOperatingAuthority = null,
            INativeConstructHostSolarIntervalAuthorityProvider? solarIntervalAuthority = null,
            INativeConstructHostContextualPowerAuthorityProvider? contextualPowerAuthority = null,
            INativeConstructHostSoulCustodyAuthorityProvider? soulCustodyAuthority = null,
            INativeConstructHostSustenanceAuthorityProvider? sustenanceAuthority = null,
            INativeConstructHostModificationAuthorityProvider? modificationAuthority = null,
            INativeCharacterHostTrainingAuthorityProvider? trainingAuthority = null)
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
            // Use an isolated copy of the real native package for synthetic
            // future-catalog tests. No fixture spell or feat enters production.
            if (syntheticOpenChoiceCatalog)
            {
                if (!realNativeContent) throw new InvalidOperationException("Native source fixture required.");
                var sourceNative = Path.Combine(NativeContentRoot(), "first-party-001", "native");
                var fixtureNative = Path.Combine(package, "native");
                Directory.Delete(fixtureNative, recursive: true);
                foreach (var file in Directory.EnumerateFiles(sourceNative, "*.json", SearchOption.AllDirectories))
                {
                    var target = Path.Combine(fixtureNative, Path.GetRelativePath(sourceNative, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target);
                }
                File.WriteAllText(Path.Combine(fixtureNative, "synthetic-open-choice-catalog.json"), """
                    [
                      {"id":"spell.fixture-cantrip-a","kind":"spell","name":"Fixture Cantrip A",
                       "level":0,"spellListIds":["wizard"]},
                      {"id":"spell.fixture-cantrip-b","kind":"spell","name":"Fixture Cantrip B",
                       "level":0,"spellListIds":["wizard"]},
                      {"id":"spell.fixture-off-list","kind":"spell","name":"Fixture Off List",
                       "level":0,"spellListIds":["cleric"]},
                      {"id":"spell.fixture-leveled","kind":"spell","name":"Fixture Leveled",
                       "level":1,"spellListIds":["wizard"]},
                      {"id":"spell.fixture-unlisted","kind":"spell","name":"Fixture Unlisted","level":0},
                      {"id":"spell.fixture-disabled","kind":"spell","name":"Fixture Disabled",
                       "level":0,"spellListIds":["wizard"],"selectable":false},
                      {"id":"spell.prestidigitation","kind":"spell","name":"Fixture Prestidigitation",
                       "level":0,"spellListIds":["wizard"]},
                      {"id":"feat.fixture-origin","kind":"feat","name":"Fixture Origin","featCategory":"origin"},
                      {"id":"feat.fixture-general","kind":"feat","name":"Fixture General","featCategory":"general"},
                      {"id":"feat.fixture-uncategorized","kind":"feat","name":"Fixture Uncategorized"},
                      {"id":"feat.fixture-disabled","kind":"feat","name":"Fixture Disabled",
                       "featCategory":"origin","selectable":false}
                    ]
                    """);
                // Working-only entries are deliberately invisible to native discovery.
                Directory.CreateDirectory(Path.Combine(package, "working"));
                File.WriteAllText(Path.Combine(package, "working", "candidate.json"), """
                    {"id":"spell.fixture-working","kind":"spell","name":"Working-only spell",
                     "level":0,"spellListIds":["wizard"]}
                    """);
            }
            App = RulesCoreApplication.Create([], builder => {
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                    ["RulesCore:ApiSurface"] = surface,
                    ["Rulesets:ContentRoot"] = realNativeContent && !syntheticOpenChoiceCatalog ? NativeContentRoot() : _root,
                    ["Rulesets:DefaultRuleset"] = "first-party-001",
                    ["ToolHost:BaseUrl"] = "https://site.test"
                });
            }, services => {
                services.AddHttpClient<CharacterSheetMechanicsTicketValidator>()
                    .ConfigurePrimaryHttpMessageHandler(() => new SiteHandler(source, ordinary, dm));
                if (supernaturalAuthority is not null)
                    services.AddSingleton(supernaturalAuthority);
                if (biologicalAuthority is not null)
                    services.AddSingleton(biologicalAuthority);
                if (reconstructionAuthority is not null)
                    services.AddSingleton(reconstructionAuthority);
                if (lifeExperienceAuthority is not null)
                    services.AddSingleton(lifeExperienceAuthority);
                if (scopedCheckAuthority is not null)
                    services.AddSingleton(scopedCheckAuthority);
                if (featChoiceAuthority is not null)
                    services.AddSingleton(featChoiceAuthority);
                if (cantripRestAuthority is not null)
                    services.AddSingleton(cantripRestAuthority);
                if (constructOperatingAuthority is not null)
                    services.AddSingleton(constructOperatingAuthority);
                if (solarIntervalAuthority is not null)
                    services.AddSingleton(solarIntervalAuthority);
                if (contextualPowerAuthority is not null)
                    services.AddSingleton(contextualPowerAuthority);
                if (soulCustodyAuthority is not null)
                    services.AddSingleton(soulCustodyAuthority);
                if (sustenanceAuthority is not null)
                    services.AddSingleton(sustenanceAuthority);
                if (modificationAuthority is not null)
                    services.AddSingleton(modificationAuthority);
                if (trainingAuthority is not null)
                    services.AddSingleton(trainingAuthority);
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


    // This source and authority exist only inside the HTTP test fixture. An
    // authenticated delegation must never register a production entitlement.
    private static readonly Guid SupernaturalActor =
        Guid.Parse("d66a7a7b-bc0f-4677-829d-0ccf40470d2f");
    private static readonly Guid SupernaturalCharacter =
        Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly NativeOriginSourceReference SupernaturalSource =
        new(NativeOriginSourceKind.Native, "fixture.wolf", "synthetic-v1");
    private static readonly NativeOriginDecisionBasis SupernaturalBasis =
        new(NativeOriginAdjudication.Supported,
            NativeRuleReference: "fixture.rule.wolf");

    private sealed class VerifiedSupernaturalTestAuthority :
        INativeSupernaturalHostAuthorityProvider, INativeSupernaturalProgressionAuthority
    {
        private static NativeOriginSourceOccurrence Occurrence(string path) =>
            new(SupernaturalSource, path);

        private static readonly NativeSupernaturalDefinition Definition = new(
            "fixture.definition.wolf", SupernaturalSource,
            new Dictionary<string, string> { ["form"] = "human" },
            [
                new NativeSupernaturalProgressionNode("acquired",
                    Occurrence("nodes.acquired"), OnAcquisition: true),
                new NativeSupernaturalProgressionNode("moonrise",
                    Occurrence("nodes.moonrise"),
                    RequiredEventKeys: ["lunar-event"],
                    Grants: [new NativeSupernaturalGrantDefinition(
                        "moonrise-grant", "fixture.capability.moonrise",
                        Occurrence("grants.moonrise"))]),
                new NativeSupernaturalProgressionNode("second-moonrise",
                    Occurrence("nodes.second-moonrise"),
                    RequiredEventKeys: ["lunar-event"])
            ]);

        public Task<VerifiedSupernaturalHostAuthorization?> VerifyAsync(
            Guid actor, VerifiedSupernaturalOwnershipPlanRequest request,
            CancellationToken cancellationToken)
        {
            // Independently recognized, fixture-scoped events. No transition
            // is minted by echoing an arbitrary supplied event or source.
            NativeSupernaturalTransitionRequest? transition =
                actor != SupernaturalActor ||
                request.CharacterId != SupernaturalCharacter.ToString("D")
                    ? null : request.EventId switch
                    {
                        "wolf-acquire" => new(request.CharacterId, 0,
                            "wolf-acquire", NativeSupernaturalTransitionKind.Acquire,
                            "fixture.wolf.owned", "fixture-proof:acquire",
                            SupernaturalBasis),
                        "wolf-advance" => new(request.CharacterId, 1,
                            "wolf-advance", NativeSupernaturalTransitionKind.Advance,
                            "fixture.wolf.owned", "fixture-proof:advance",
                            SupernaturalBasis, NodeId: "moonrise",
                            RequirementEvidence: new Dictionary<string, string> {
                                ["lunar-event"] = "fixture-proof:moon-1"
                            }),
                        "wolf-replay" => new(request.CharacterId, 2,
                            "wolf-replay", NativeSupernaturalTransitionKind.Advance,
                            "fixture.wolf.owned", "fixture-proof:replay",
                            SupernaturalBasis, NodeId: "second-moonrise",
                            RequirementEvidence: new Dictionary<string, string> {
                                ["lunar-event"] = "fixture-proof:moon-1"
                            }),
                        _ => null
                    };
            VerifiedSupernaturalHostAuthorization? verified = transition is null
                ? null : new(Definition, transition, this);
            return Task.FromResult(verified);
        }

        public bool AuthorizesPrimary(string id, NativeCharacterRequest character) =>
            id == SupernaturalCharacter.ToString("D");
        public bool AuthorizesSheet(string id, NativePersistentCharacterSheet sheet) => true;
        public bool AuthorizesProgression(string id, string sheet,
            NativeSheetEarnedProgression item) => true;
        public bool AuthorizesGrant(string id, string sheet,
            NativeSheetEarnedProgression progression, NativeSheetEarnedGrant grant) => true;
        public bool AuthorizesApplicability(string id, string sheet,
            NativeSheetGrantApplicability decision) => true;
        public bool AuthorizesInfluence(string id, NativeCharacterPersistentInfluence item) => true;
        public bool AuthorizesExpression(string id, NativeCharacterPersistentInfluence item) => true;
        public bool AuthorizesEffect(string id, string influence,
            NativeCharacterInfluenceEffect effect) => true;
        public bool AuthorizesInfluenceProgression(string id, string influence,
            NativeSheetEarnedProgression record) => true;
        public bool AuthorizesInfluenceGrant(string id, string influence,
            NativeSheetEarnedProgression record, NativeSheetEarnedGrant grant) => true;
        public bool AuthorizesInfluenceApplicability(string id, string influence,
            NativeInfluenceGrantApplicability rule) => true;
        public bool AuthorizesJournal(string id, NativeSupernaturalTransitionJournal journal) =>
            id == SupernaturalCharacter.ToString("D");
        public bool AuthorizesDefinition(string id, NativeSupernaturalDefinition definition,
            NativeOriginDecisionBasis basis) => id == SupernaturalCharacter.ToString("D");
        public bool AuthorizesAcquisition(string id, NativeSupernaturalDefinition definition,
            NativeSupernaturalTransitionRequest request) => true;
        public bool AuthorizesAdvancement(string id, NativeCharacterPersistentInfluence influence,
            NativeSupernaturalProgressionNode node,
            NativeSupernaturalTransitionRequest request) => true;
        public bool AuthorizesExpressionChange(string id, NativeCharacterPersistentInfluence influence,
            NativeSupernaturalTransitionRequest request) => false;
        public bool AuthorizesEventRequirement(string id, string influence, string key,
            string evidence, NativeSupernaturalTransitionRequest request) =>
            key == "lunar-event" && evidence == "fixture-proof:moon-1";
        public NativeSupernaturalEligibilityVerdict EvaluateRequirement(string id,
            string influence, string key, NativeSupernaturalTransitionRequest request) =>
            NativeSupernaturalEligibilityVerdict.Unresolved;
    }

    [Fact]
    public async Task VerifiedSupernaturalOwnershipHttpPlansAcquisitionAndAdvanceButRejectsReplayedEvidence()
    {
        const string endpoint = "/api/mechanics/v1/characters/supernatural/ownership/plan";
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true,
            dm: true, supernaturalAuthority: new VerifiedSupernaturalTestAuthority());
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var characterId = SupernaturalCharacter.ToString("D");
        var state = new NativeCharacterState(characterId,
            new NativeCharacterRequest(catalog.RulesetId, catalog.Fingerprint,
                new Dictionary<string, int> { ["strength"] = 10 },
                [new NativeAdvancement("class.demo", 2, "class:demo")]), [], [], []);
        var journal = new NativeSupernaturalTransitionJournal(characterId, 0, [], []);

        object Payload(string operation, string eventId, string evidence,
            NativeCharacterState current, NativeSupernaturalTransitionJournal currentJournal,
            string intent = "fixture-intent") => new
        {
            contractVersion = 2, operation, characterId, eventId, intentId = intent,
            rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
            expectedSupernaturalRevision = currentJournal.Revision,
            expectedLedgerRevision = 0,
            authoritativeInputsFingerprint = "fixture-inputs",
            state = current, ledger = new {
                characterId, revision = 0, instances = Array.Empty<object>(),
                resources = Array.Empty<object>(), processedEventIds = Array.Empty<string>()
            },
            progressionJournal = currentJournal,
            authorizationEvidenceId = evidence,
            verifiedSource = new {
                characterId, operation, eventId,
                authorizationEvidenceId = evidence,
                rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                sourceOccurrence = new NativeOriginSourceOccurrence(
                    SupernaturalSource, "fixture.wolf.acquisition")
            }
        };

        async Task<(HttpStatusCode Code, JsonElement Body)> SendAsync(object payload,
            bool authenticated = true)
        {
            using var request = authenticated
                ? fixture.Authenticated(HttpMethod.Post, endpoint, JsonContent.Create(payload))
                : new HttpRequestMessage(HttpMethod.Post, endpoint) {
                    Content = JsonContent.Create(payload)
                };
            using var response = await fixture.Client.SendAsync(request);
            return (response.StatusCode,
                await response.Content.ReadFromJsonAsync<JsonElement>());
        }

        var acquire = Payload("Acquire", "wolf-acquire", "fixture-proof:acquire",
            state, journal);
        var anonymous = await SendAsync(acquire, authenticated: false);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Code);

        var first = await SendAsync(acquire);
        Assert.True(first.Code == HttpStatusCode.OK,
            "Acquisition HTTP " + (int)first.Code + ": " + first.Body.GetRawText());
        Assert.False(first.Body.GetProperty("committed").GetBoolean());
        Assert.False(first.Body.GetProperty("effectiveMechanicsReady").GetBoolean());
        var planned = first.Body.GetProperty("plan");
        Assert.Equal("planned", planned.GetProperty("status").GetString());
        state = planned.GetProperty("proposedState").Deserialize<NativeCharacterState>(options)!;
        journal = planned.GetProperty("proposedJournal")
            .Deserialize<NativeSupernaturalTransitionJournal>(options)!;
        Assert.Equal(1, journal.Revision);
        Assert.Single(state.PersistentInfluences!);
        Assert.Single(state.PersistentInfluences![0].Progression!);

        var advance = await SendAsync(Payload("Advance", "wolf-advance",
            "fixture-proof:advance", state, journal));
        Assert.Equal(HttpStatusCode.OK, advance.Code);
        planned = advance.Body.GetProperty("plan");
        state = planned.GetProperty("proposedState").Deserialize<NativeCharacterState>(options)!;
        journal = planned.GetProperty("proposedJournal")
            .Deserialize<NativeSupernaturalTransitionJournal>(options)!;
        Assert.Equal(2, journal.Revision);
        Assert.Equal(2, state.PersistentInfluences![0].Progression!.Count);
        Assert.Single(planned.GetProperty("newGrants").EnumerateArray());
        Assert.Contains("fixture-proof:moon-1", journal.ConsumedEligibilityEvidenceIds);

        var replay = await SendAsync(Payload("Advance", "wolf-replay",
            "fixture-proof:replay", state, journal));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, replay.Code);
        Assert.Equal("supernatural_plan_not_committable",
            replay.Body.GetProperty("error").GetString());

        var forged = await SendAsync(Payload("Acquire", "wolf-acquire",
            "forged-proof", new NativeCharacterState(characterId,
                state.PrimaryMechanics, [], [], []),
            new NativeSupernaturalTransitionJournal(characterId, 0, [], [])));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, forged.Code);
        Assert.Equal("supernatural_authority_unverified",
            forged.Body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task SupernaturalOverlapHttpRequiresIndependentAuthorityEvenWithSiteDelegation()
    {
        const string endpoint = "/api/mechanics/v1/characters/supernatural/overlaps/plan";
        await using var fixture = new Fixture();
        // The authorization test must use a valid Site actor GUID. An
        // arbitrary "unit-test" identity is rejected before host authority.
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true, dm: true);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555").ToString("D");
        var payload = new
        {
            contractVersion = 1,
            characterId = id, eventId = "fixture:overlap",
            intentId = "intent:fixture:overlap",
            rulesetId = catalog.RulesetId,
            fingerprint = catalog.Fingerprint,
            expectedSupernaturalRevision = 1,
            expectedLedgerRevision = 0,
            authoritativeInputsFingerprint = "fixture-input-fingerprint",
            state = new NativeCharacterState(id,
                new NativeCharacterRequest(
                    catalog.RulesetId, catalog.Fingerprint,
                    new Dictionary<string, int> { ["strength"] = 10 },
                    [new NativeAdvancement("class.demo", 1, "fixture.class")]),
                [], [], []),
            ledger = new NativeManifestationLedger(id, 0, [], [], []),
            progressionJournal = new NativeSupernaturalTransitionJournal(id, 0, [], []),
            verifiedSource = new
            {
                characterId = id, eventId = "fixture:overlap",
                intentId = "intent:fixture:overlap",
                rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                decisionId = "fixture.decision",
                replacedDecisionId = (string?)null,
                sourceOccurrence = new
                {
                    source = new { kind = 0, id = "fixture.supernatural", version = "v1" },
                    occurrencePath = "fixture.overlap"
                },
                authorizationEvidenceId = "fixture:proof"
            },
            authorizationEvidenceId = "fixture:proof"
        };
        using var anonymous = await fixture.Client.PostAsJsonAsync(endpoint, payload);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var request = fixture.Authenticated(HttpMethod.Post, endpoint,
            JsonContent.Create(payload));
        using var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("overlap_authority_unavailable",
            body.RootElement.GetProperty("error").GetString());

        var malformed = JsonSerializer.SerializeToElement(payload);
        var changed = System.Text.Json.Nodes.JsonNode.Parse(malformed.GetRawText())!;
        changed["contractVersion"] = 7;
        using var invalidRequest = fixture.Authenticated(HttpMethod.Post, endpoint,
            JsonContent.Create(changed));
        using var invalid = await fixture.Client.SendAsync(invalidRequest);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Theory]
    [InlineData("null-contributor")]
    [InlineData("null-candidate")]
    [InlineData("null-manifestation")]
    [InlineData("null-choice")]
    [InlineData("blank-source-id")]
    [InlineData("null-candidate-occurrence")]
    [InlineData("null-candidate-source")]
    [InlineData("null-required-choice")]
    [InlineData("duplicate-required-choice")]
    [InlineData("duplicate-selected-choice")]
    [InlineData("null-selected-option")]
    [InlineData("null-relationship")]
    [InlineData("null-input-role")]
    [InlineData("null-constraint-subject")]
    public async Task IndependentMalformedOriginReview(string scenario)
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true,
            realNativeContent: true);
        var proposal = System.Text.Json.Nodes.JsonNode.Parse("""
          {"id":"independent-review","intent":0,
           "method":{"ruleId":"rule.creature-origination","methodId":"biological"},
           "contributors":[],"candidates":[],"manifestations":[],
           "selectedSpeciesId":"species.orc"}
          """)!;
        var custom = System.Text.Json.Nodes.JsonNode.Parse("""
          {"id":"review-custom","identity":{"kind":1,
           "occurrence":{"source":{"kind":2,"id":"review"},"occurrencePath":"/traits/0"},
           "mechanicalDefinitionId":"review-trait"},"selectionBasis":{"eligibility":0},
           "requiredChoices":[{"id":"duplicate","timing":0,"minimum":0,"maximum":1,
           "occurrence":{"source":{"kind":2,"id":"review"},"occurrencePath":"/choice"},
           "finiteOptions":["a"]}]}
          """)!;
        switch (scenario)
        {
            case "null-contributor": proposal["contributors"]!.AsArray().Add((System.Text.Json.Nodes.JsonNode?)null); break;
            case "null-candidate": proposal["candidates"]!.AsArray().Add((System.Text.Json.Nodes.JsonNode?)null); break;
            case "null-manifestation": proposal["manifestations"]!.AsArray().Add((System.Text.Json.Nodes.JsonNode?)null); break;
            case "blank-source-id": proposal["contributors"]!.AsArray().Add(System.Text.Json.Nodes.JsonNode.Parse("""{"id":"p","role":{"kind":0},"source":{"kind":0,"id":" "}}""")); break;
            case "null-candidate-occurrence": custom["identity"]!["occurrence"] = null; goto default;
            case "null-candidate-source": custom["identity"]!["occurrence"]!["source"] = null; goto default;
            case "null-required-choice": custom["requiredChoices"]!.AsArray().Add((System.Text.Json.Nodes.JsonNode?)null); goto default;
            case "duplicate-required-choice": custom["requiredChoices"]!.AsArray().Add(custom["requiredChoices"]![0]!.DeepClone()); goto default;
            case "null-relationship": proposal["relationships"] = new System.Text.Json.Nodes.JsonArray((System.Text.Json.Nodes.JsonNode?)null); break;
            case "null-input-role": proposal["inputRequirements"] = System.Text.Json.Nodes.JsonNode.Parse("""[{"id":"r","role":null,"minimumCount":0}]"""); break;
            case "null-constraint-subject": proposal["constraints"] = System.Text.Json.Nodes.JsonNode.Parse("""[{"id":"c","kind":5,"subjects":[null],"conditions":[]}]"""); break;
            case "null-choice": proposal["manifestations"] = System.Text.Json.Nodes.JsonNode.Parse("""[{"id":"m","candidateId":"direct:species.orc:/traits/0","choices":[null]}]"""); break;
            case "duplicate-selected-choice": proposal["manifestations"] = System.Text.Json.Nodes.JsonNode.Parse("""[{"id":"m","candidateId":"review-custom","choices":[{"requirementId":"duplicate","selectedOptionIds":["a"]},{"requirementId":"duplicate","selectedOptionIds":["a"]}]}]"""); goto default;
            case "null-selected-option": proposal["manifestations"] = System.Text.Json.Nodes.JsonNode.Parse("""[{"id":"m","candidateId":"review-custom","choices":[{"requirementId":"duplicate","selectedOptionIds":[null]}]}]"""); goto default;
            default:
                proposal["candidates"]!.AsArray().Add(custom);
                if (proposal["manifestations"]!.AsArray().Count == 0)
                    proposal["manifestations"] = System.Text.Json.Nodes.JsonNode.Parse("""[{"id":"m","candidateId":"review-custom","choices":[]}]""");
                break;
        }
        var wire = new { rulesetId = "first-party-001", proposal };
        using var request = fixture.Authenticated(HttpMethod.Post,
            "/api/mechanics/v1/origins/preview", JsonContent.Create(wire));
        using var response = await fixture.Client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode < 500,
            scenario + ": HTTP " + (int)response.StatusCode + " " + text);
        if (scenario == "null-relationship")
        {
            // The transport boundary must refuse a null authored relationship
            // rather than reaching reconciliation sorting or returning a grant.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var rejected = JsonDocument.Parse(text);
            Assert.Equal("invalid_origin_preview_request",
                rejected.RootElement.GetProperty("error").GetString());
            Assert.False(rejected.RootElement.TryGetProperty("result", out _));
        }
        else if (response.IsSuccessStatusCode)
        {
            using var doc = JsonDocument.Parse(text);
            var result = doc.RootElement.GetProperty("result");
            Assert.NotEqual((int)NativeOriginConstructionStatus.Authorized,
                result.GetProperty("status").GetInt32());
            if (scenario == "duplicate-required-choice")
                Assert.Contains(result.GetProperty("issues").EnumerateArray(),
                    issue => issue.GetProperty("code").GetString() ==
                        "duplicate-or-missing-id");
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

        // The direct bootstrap handler must reject malformed keys before
        // first-party choice normalization just like the ordinary preview.
        // Even a valid committed profile cannot justify malformed transport.
        foreach (var malformed in new[]
        {
            proposal with { Manifestations = [
                new NativeOriginManifestation("bad-candidate", null!, Choices: [])] },
            proposal with { Manifestations = [
                new NativeOriginManifestation("bad-choice", "unverified-candidate",
                    Choices: [new NativeOriginChoiceSelection(null!,
                        ["skill.intimidation"])])] }
        })
        {
            using var malformedRequest = fixture.Authenticated(
                HttpMethod.Post, endpoint, JsonContent.Create(new
                {
                    rulesetId = catalog.RulesetId,
                    fingerprint = catalog.Fingerprint,
                    proposal = malformed, committedProfile = origin.Profile, character
                }));
            using var malformedResponse = await fixture.Client.SendAsync(malformedRequest);
            Assert.Equal(HttpStatusCode.BadRequest, malformedResponse.StatusCode);
            var rejected = await malformedResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("trusted_direct_origin_required",
                rejected.GetProperty("error").GetString());
            Assert.False(rejected.TryGetProperty("result", out _));
        }

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
        Assert.True(body.GetProperty("tremorsense").GetProperty("previewOnly").GetBoolean());
        Assert.True(body.GetProperty("innateSpells").GetProperty("previewOnly").GetBoolean());
        Assert.True(body.GetProperty("allocation").GetProperty("previewOnly").GetBoolean());
        Assert.Equal("within-allowance", body.GetProperty("allocation").GetProperty("status").GetString());
        Assert.True(body.GetProperty("passiveEntitlements").GetProperty("previewOnly").GetBoolean());
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

    // Test authority reads a fixture-owned record, not the submitted snapshot.
    private sealed class VerifiedBiologicalTestAuthority : INativeOriginHostBiologicalAuthorityProvider
    {
        public NativeOriginProfile? Parent { get; set; }
        public INativeOriginFeatChoiceAuthority? FeatAuthority { get; set; }
        public Task<INativeOriginFeatChoiceAuthority?> VerifyChoicesAsync(Guid actorId,
            NativeOriginApiEndpoints.VerifiedBiologicalRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(actorId == SupernaturalActor && request.CharacterId == SupernaturalCharacter
                ? FeatAuthority : null);
        public Task<IReadOnlyList<NativeOriginVerifiedManifestedContributor>?> VerifyAsync(
            Guid actorId, Guid characterId, IReadOnlyList<NativeOriginSourceReference> sources,
            string rulesetId, string fingerprint, CancellationToken cancellationToken)
        {
            IReadOnlyList<NativeOriginVerifiedManifestedContributor>? records =
                actorId == SupernaturalActor && characterId == SupernaturalCharacter && sources.Count == 0
                ? [] : actorId == SupernaturalActor && characterId == SupernaturalCharacter &&
                Parent is not null && sources.Count == 1 &&
                sources[0].Id == Parent.Id && sources[0].Version == "revision-1"
                ? [new(Parent, "revision-1")] : null;
            return Task.FromResult(records);
        }
    }

    [Fact]
    public async Task VerifiedBiologicalHttpCanExpressPreviouslyLatentGene()
    {
        await using var fixture = new Fixture();
        var provider = new VerifiedBiologicalTestAuthority();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true,
            realNativeContent: true, dm: true, biologicalAuthority: provider);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var method = new NativeOriginMethodReference("rule.creature-origination", "biological");
        NativeOriginManifestation Express(NativeOriginCandidate candidate, int index) =>
            new("express-" + index, candidate.Id,
                SelectedValue: candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic
                    ? candidate.OfferedValue : null);
        var parentDraft = new NativeOriginProposal("http-parent", NativeOriginIntent.Mixed, method,
            [
                new("p1", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.orc")),
                new("p2", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.orc"))
            ], [], []);
        var engine = new NativeOriginConstructionEngine(catalog);
        var offers = engine.Evaluate(new(parentDraft, AutoManifestDirectDefaults: false))
            .EvaluatedProposal.Candidates;
        var foundation = offers.Where(x => x.ContributorId == "p1" &&
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        var powers = offers.Where(x => x.Identity.MechanicalDefinitionId == "trait.powerful-build")
            .OrderBy(x => x.ContributorId).ToArray();
        Assert.Equal(3, foundation.Length);
        Assert.Equal(2, powers.Length);
        var parent = engine.Evaluate(new(parentDraft with
        {
            Manifestations = foundation.Append(powers[1]).Select(Express).ToArray(),
            BiologicalTransmission = new(foundation.Concat(powers).Select(x => x.Id).ToArray())
        }, AutoManifestDirectDefaults: false));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, parent.Status);
        provider.Parent = parent.Profile!;
        var latent = Assert.Single(provider.Parent.Underlying.BiologicalGenes!,
            gene => gene.SourceCandidateId == powers[0].Id);
        var child = new NativeOriginProposal("http-descendant", NativeOriginIntent.Mixed, method,
            [new("carrier", new(NativeOriginInputKind.BiologicalParent),
                new(NativeOriginSourceKind.ManifestedProfile, provider.Parent.Id, "revision-1"))], [], []);
        const string endpoint = "/api/mechanics/v1/origins/biological/verified-preview";
        async Task<(HttpStatusCode, JsonElement)> Send(NativeOriginProposal proposal,
            Guid characterId, string fingerprint)
        {
            using var request = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new { characterId, rulesetId = catalog.RulesetId, fingerprint, proposal }));
            using var response = await fixture.Client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>());
        }

        var (discoveryStatus, discovery) = await Send(child, SupernaturalCharacter, catalog.Fingerprint);
        Assert.Equal(HttpStatusCode.OK, discoveryStatus);
        var candidates = discovery.GetProperty("result").GetProperty("evaluatedProposal")
            .GetProperty("candidates").Deserialize<NativeOriginCandidate[]>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var inherited = Assert.Single(candidates, x => x.Id == "nested:carrier:gene:" + latent.Id);
        var selected = candidates.Where(x => x.Identity.IntrinsicSlotId is
            "creatureType" or "size" or "walkingSpeedFeet").Append(inherited).ToArray();
        var (status, body) = await Send(child with
        {
            Manifestations = selected.Select(Express).ToArray(),
            BiologicalTransmission = new(selected.Select(x => x.Id).ToArray())
        }, SupernaturalCharacter, catalog.Fingerprint);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.False(body.GetProperty("committed").GetBoolean());
        var result = body.GetProperty("result");
        Assert.Equal((int)NativeOriginConstructionStatus.Authorized, result.GetProperty("status").GetInt32());
        Assert.Equal("within-allowance", result.GetProperty("creationAllocation").GetProperty("status").GetString());
        var profile = result.GetProperty("profile").Deserialize<NativeOriginProfile>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var gene = Assert.Single(profile.Underlying.BiologicalGenes!, x => x.SourceCandidateId == inherited.Id);
        Assert.Equal(provider.Parent.Id, gene.InheritedFrom!.ContributorProfileId);
        Assert.Equal("revision-1", gene.InheritedFrom.ContributorRevision);
        Assert.False(gene.InheritedFrom.SourceWasGrant);

        Assert.Equal(HttpStatusCode.Conflict,
            (await Send(child, SupernaturalCharacter, "stale")).Item1);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await Send(child, Guid.NewGuid(), catalog.Fingerprint)).Item1);
        var stale = child with { Contributors = [child.Contributors[0] with
            { Source = child.Contributors[0].Source with { Version = "revision-0" } }] };
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await Send(stale, SupernaturalCharacter, catalog.Fingerprint)).Item1);
        var forged = child with { Contributors = [child.Contributors[0] with
            { ManifestedProfile = provider.Parent.Underlying }] };
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Send(forged, SupernaturalCharacter, catalog.Fingerprint)).Item1);
    }

    [Fact]
    public async Task VerifiedMixedBiologicalHttpAuthorizesNativeSkillChoices()
    {
        var provider = new VerifiedBiologicalTestAuthority();
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true,
            realNativeContent: true, dm: true, biologicalAuthority: provider);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var draft = new NativeOriginProposal("http-mixed-skill", NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [new("orc", new(NativeOriginInputKind.BiologicalParent),
                new(NativeOriginSourceKind.Native, "species.orc")),
             new("human", new(NativeOriginInputKind.BiologicalParent),
                new(NativeOriginSourceKind.Native, "species.human"))], [], []);
        const string endpoint = "/api/mechanics/v1/origins/biological/verified-preview";
        async Task<JsonElement> Send(NativeOriginProposal proposal)
        {
            using var request = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new { characterId = SupernaturalCharacter, rulesetId = catalog.RulesetId,
                    fingerprint = catalog.Fingerprint, proposal }));
            using var response = await fixture.Client.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, raw);
            using var json = JsonDocument.Parse(raw);
            return json.RootElement.GetProperty("result").Clone();
        }
        var discovery = await Send(draft);
        var offers = discovery.GetProperty("evaluatedProposal").GetProperty("candidates")
            .Deserialize<NativeOriginCandidate[]>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var foundation = offers.Where(x => x.ContributorId == "orc" &&
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        Assert.Equal(3, foundation.Length);
        var skill = Assert.Single(offers, x => x.ContributorId == "human" &&
            x.RequiredChoices?.Any(c => c.OptionCatalogId == "any-native-skill") == true);
        var baseSelections = foundation.Select((x, i) =>
            new NativeOriginManifestation("foundation-" + i, x.Id, SelectedValue: x.OfferedValue)).ToArray();
        var selected = draft with
        {
            Manifestations = [.. baseSelections, new("skill-selection", skill.Id,
                Choices: [new("selection", ["skill.intimidation"])])],
            BiologicalTransmission = new(foundation.Append(skill).Select(x => x.Id).ToArray())
        };
        var result = await Send(selected);
        var grants = result.GetProperty("profile").GetProperty("underlying").GetProperty("inherentGrants");
        var grant = Assert.Single(grants.EnumerateArray(), x => x.GetProperty("id").GetString() == "grant:skill-selection");
        Assert.Equal("skill.intimidation", grant.GetProperty("resolvedChoices")[0].GetProperty("selectedOptionIds")[0].GetString());
        Assert.Equal("native-catalog:" + catalog.RulesetId + ":" + catalog.Fingerprint,
            grant.GetProperty("resolvedChoices")[0].GetProperty("catalogEligibilityEvidenceId").GetString());

        // A latent configuration receives the same membership check without
        // manifesting a Skill grant.
        var latent = await Send(selected with
        {
            Manifestations = baseSelections,
            BiologicalTransmission = selected.BiologicalTransmission! with
            {
                LatentConfigurations = [new(skill.Id, [new("selection", ["skill.intimidation"])])]
            }
        });
        var gene = Assert.Single(latent.GetProperty("profile").GetProperty("underlying")
            .GetProperty("biologicalGenes").EnumerateArray(), x => x.GetProperty("sourceCandidateId").GetString() == skill.Id);
        Assert.Equal("skill.intimidation", gene.GetProperty("resolvedChoices")[0].GetProperty("selectedOptionIds")[0].GetString());
        Assert.DoesNotContain(latent.GetProperty("profile").GetProperty("underlying")
            .GetProperty("inherentGrants").EnumerateArray(), x => x.GetProperty("id").GetString() == "grant:skill-selection");

        var invalid = await Send(selected with
        {
            Manifestations = [.. baseSelections, new("skill-selection", skill.Id,
                Choices: [new("selection", ["competency.disguise"])])]
        });
        Assert.NotEqual((int)NativeOriginConstructionStatus.Authorized, invalid.GetProperty("status").GetInt32());
        Assert.Contains(invalid.GetProperty("issues").EnumerateArray(),
            x => x.GetProperty("code").GetString() == "choice-authorization-unresolved");

        // Carry a saved latent Skill through the authenticated parent path.
        // The descendant selects the existing instance, not a new menu.
        var parent = latent.GetProperty("profile").Deserialize<NativeOriginProfile>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        provider.Parent = parent;
        var child = new NativeOriginProposal("http-mixed-skill-child", NativeOriginIntent.Mixed,
            draft.Method, [new("carrier", new(NativeOriginInputKind.BiologicalParent),
                new(NativeOriginSourceKind.ManifestedProfile, parent.Id, "revision-1"))], [], []);
        var childOffers = (await Send(child)).GetProperty("evaluatedProposal").GetProperty("candidates")
            .Deserialize<NativeOriginCandidate[]>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var inherited = Assert.Single(childOffers, x => x.Id == "nested:carrier:gene:" + gene.GetProperty("id").GetString());
        var childSelected = childOffers.Where(x => x.Identity.IntrinsicSlotId is
            "creatureType" or "size" or "walkingSpeedFeet").Append(inherited).ToArray();
        var childRequest = child with
        {
            Manifestations = childSelected.Select((x, i) =>
                new NativeOriginManifestation("child-" + i, x.Id,
                    SelectedValue: x.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic ? x.OfferedValue : null)).ToArray(),
            BiologicalTransmission = new(childSelected.Select(x => x.Id).ToArray())
        };
        var carried = await Send(childRequest);
        Assert.True(carried.GetProperty("status").GetInt32() == (int)NativeOriginConstructionStatus.Authorized, carried.GetRawText());
        var inheritedGrant = Assert.Single(carried.GetProperty("profile").GetProperty("underlying")
            .GetProperty("inherentGrants").EnumerateArray(), x => x.GetProperty("sourceCandidateIds")
                .EnumerateArray().Any(id => id.GetString() == inherited.Id));
        Assert.Equal("skill.intimidation", inheritedGrant.GetProperty("resolvedChoices")[0]
            .GetProperty("selectedOptionIds")[0].GetString());

        // Simulate an older saved configuration with a now-invalid option.
        // It cannot become an effective grant merely because the record is authenticated.
        var staleConfiguration = parent.Underlying with
        {
            BiologicalGenes = parent.Underlying.BiologicalGenes!.Select(g => g.SourceCandidateId == skill.Id
                ? g with { ResolvedChoices = [g.ResolvedChoices![0] with { SelectedOptionIds = ["competency.disguise"] }] }
                : g).ToArray()
        };
        provider.Parent = parent with { Underlying = staleConfiguration, Current = staleConfiguration };
        var stale = await Send(childRequest);
        Assert.NotEqual((int)NativeOriginConstructionStatus.Authorized, stale.GetProperty("status").GetInt32());
        Assert.Contains(stale.GetProperty("issues").EnumerateArray(),
            x => x.GetProperty("code").GetString() == "inherited-open-choice-unresolved");
    }

    [Fact]
    public async Task VerifiedInheritedFeatRequiresDescendantEligibility()
    {
        var provider = new VerifiedBiologicalTestAuthority();
        var eligibility = new OpenChoiceTestFeatAuthority();
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true,
            realNativeContent: true, dm: true, biologicalAuthority: provider, syntheticOpenChoiceCatalog: true);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var parent = new NativeOriginFirstPartyChoiceEvaluator(catalog).Evaluate(
            OpenChoiceProposal(catalog, "species.human", "feat.fixture-origin"), eligibility);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, parent.Status);
        provider.Parent = parent.Profile!;
        var child = new NativeOriginProposal("fixture-inherited-feat", NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [new("carrier", new(NativeOriginInputKind.BiologicalParent),
                new(NativeOriginSourceKind.ManifestedProfile, provider.Parent.Id, "revision-1"))], [], []);
        async Task<JsonElement> Send(NativeOriginProposal proposal)
        {
            using var request = fixture.Authenticated(HttpMethod.Post,
                "/api/mechanics/v1/origins/biological/verified-preview", JsonContent.Create(new
                {
                    characterId = SupernaturalCharacter, rulesetId = catalog.RulesetId,
                    fingerprint = catalog.Fingerprint, proposal
                }));
            using var response = await fixture.Client.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, raw);
            using var json = JsonDocument.Parse(raw);
            return json.RootElement.GetProperty("result").Clone();
        }
        var discovery = await Send(child);
        var offers = discovery.GetProperty("evaluatedProposal").GetProperty("candidates")
            .Deserialize<NativeOriginCandidate[]>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var feat = Assert.Single(offers, x => x.Identity.MechanicalDefinitionId == "trait.versatile");
        var selected = offers.Where(x => x.Identity.IntrinsicSlotId is
            "creatureType" or "size" or "walkingSpeedFeet").Append(feat).ToArray();
        var proposal = child with
        {
            Manifestations = selected.Select((x, i) => new NativeOriginManifestation("child-" + i,
                x.Id, SelectedValue: x.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic ? x.OfferedValue : null)).ToArray(),
            BiologicalTransmission = new(selected.Select(x => x.Id).ToArray())
        };
        var absent = await Send(proposal);
        Assert.Contains(absent.GetProperty("issues").EnumerateArray(),
            x => x.GetProperty("code").GetString() == "inherited-feat-eligibility-unresolved");
        provider.FeatAuthority = eligibility;
        eligibility.Eligible = false;
        var denied = await Send(proposal);
        Assert.Contains(denied.GetProperty("issues").EnumerateArray(),
            x => x.GetProperty("code").GetString() == "inherited-feat-eligibility-unresolved");
        eligibility.Eligible = true;
        var authorized = await Send(proposal);
        Assert.True(authorized.GetProperty("status").GetInt32() == (int)NativeOriginConstructionStatus.Authorized,
            authorized.GetRawText());
        var grant = Assert.Single(authorized.GetProperty("profile").GetProperty("underlying")
            .GetProperty("inherentGrants").EnumerateArray(), x => x.GetProperty("sourceCandidateIds")
                .EnumerateArray().Any(id => id.GetString() == feat.Id));
        Assert.Equal("feat.fixture-origin", grant.GetProperty("resolvedChoices")[0]
            .GetProperty("selectedOptionIds")[0].GetString());
    }

    private sealed class VerifiedReconstructionTestAuthority : INativeOriginHostReconstructionAuthorityProvider,
        INativeOriginAuthority
    {
        public NativeOriginProfile? Existing { get; set; }
        public NativeTableRuleDecision? Decision { get; set; }
        private static readonly NativeTableDecisionScope Scope = new("character",
            SupernaturalCharacter.ToString("D"));

        public Task<VerifiedPermanentOriginAuthorization?> VerifyAsync(Guid actorId,
            NativeOriginApiEndpoints.VerifiedReconstructionRequest request, CancellationToken cancellationToken)
        {
            VerifiedPermanentOriginAuthorization? result =
                actorId == SupernaturalActor && request.CharacterId == SupernaturalCharacter &&
                request.OriginRevision == 1 && Existing is not null &&
                request.CompletionId == "rite.fixture" && Decision is not null
                ? new(Existing, 1, Scope, this) : null;
            return Task.FromResult(result);
        }

        public bool Authorizes(NativeTableRuleDecision decision, NativeTableDecisionScope scope) =>
            scope == Scope && Decision is not null &&
            JsonElement.DeepEquals(JsonSerializer.SerializeToElement(decision),
                JsonSerializer.SerializeToElement(Decision));
        public bool AuthorizesCatalogChoice(string catalogId, string optionId, string evidenceId) => false;
    }

    [Fact]
    public async Task VerifiedReconstructionHttpPlansReplacementAgainstStoredOrigin()
    {
        await using var fixture = new Fixture();
        var provider = new VerifiedReconstructionTestAuthority();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true,
            realNativeContent: true, dm: true, reconstructionAuthority: provider);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var prior = new NativeOriginConstructionEngine(catalog).Evaluate(new(
            new NativeOriginProposal("http-prior", NativeOriginIntent.Direct,
                new("rule.creature-origination", "biological"), [], [], [], SelectedSpeciesId: "species.orc")));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, prior.Status);
        provider.Existing = prior.Profile!;
        provider.Decision = new("verified-rite", new(SupernaturalActor.ToString("D"), "fixture-rite-proof"),
            new("character", SupernaturalCharacter.ToString("D")), new("rite", "rite.fixture"),
            JsonSerializer.SerializeToElement(new
            {
                allow = true, targetSpeciesId = "species.dragonborn",
                targetAncestryId = "ancestry.dragonborn.red"
            }));
        var target = new NativeOriginProposal("http-rebirth", NativeOriginIntent.Direct,
            new("rule.creature-origination", "rite-of-rebirth"), [], [], [],
            SelectedSpeciesId: "species.dragonborn", SelectedAncestryId: "ancestry.dragonborn.red",
            TableDecisions: [provider.Decision]);
        const string endpoint = "/api/mechanics/v1/origins/reconstruction/plan";
        async Task<(HttpStatusCode, JsonElement)> Send(NativeOriginProposal proposal, long revision)
        {
            using var request = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new
                {
                    characterId = SupernaturalCharacter, originRevision = revision,
                    rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                    completionId = "rite.fixture", targetProposal = proposal
                }));
            using var response = await fixture.Client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>());
        }

        var (status, body) = await Send(target, 1);
        Assert.True(status == HttpStatusCode.OK, body.GetRawText());
        Assert.False(body.GetProperty("committed").GetBoolean());
        var profile = body.GetProperty("result").GetProperty("resultingProfile")
            .Deserialize<NativeOriginProfile>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(provider.Existing.Id, profile.Id);
        Assert.Equal("species.dragonborn", profile.Underlying.SpeciesId);
        Assert.Equal("ancestry.dragonborn.red", profile.Underlying.AncestryId);
        Assert.Equal("species.orc", provider.Existing.Underlying.SpeciesId);
        Assert.Contains(profile.History, item => item.Id == "rite:http-rebirth");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(target, 2)).Item1);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await Send(target with { TableDecisions = [] }, 1)).Item1);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Send(target with { ExistingConfiguration = provider.Existing.Underlying }, 1)).Item1);
    }

    [Fact]
    public async Task RealNativeRockGnomeHttpPreviewRequiresAndProjectsConfiguredChoices()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet",
            ordinary: true, realNativeContent: true);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var proposal = new NativeOriginProposal("http-rock-gnome",
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedAncestryId: "ancestry.gnome.rock");

        async Task<JsonElement> Preview(NativeOriginProposal draft)
        {
            using var request = fixture.Authenticated(HttpMethod.Post,
                "/api/mechanics/v1/origins/preview", JsonContent.Create(new
                {
                    rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                    proposal = draft, characterLevel = 1
                }));
            using var response = await fixture.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        var discovery = await Preview(proposal);
        var result = discovery.GetProperty("result");
        Assert.NotEqual((int)NativeOriginConstructionStatus.Authorized,
            result.GetProperty("status").GetInt32());
        var candidates = result.GetProperty("evaluatedProposal").GetProperty("candidates")
            .Deserialize<NativeOriginCandidate[]>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var ability = Assert.Single(candidates, x => x.Identity.IntrinsicSlotId == "spellcastingAbility");
        var trickster = Assert.Single(candidates, x => x.Identity.MechanicalDefinitionId == "trait.natural-trickster");
        var selected = await Preview(proposal with
        {
            Manifestations = [
                new("ability", ability.Id, Choices: [new(Assert.Single(ability.RequiredChoices!).Id, ["wisdom"])]),
                new("spell", trickster.Id, Choices: [new(Assert.Single(trickster.RequiredChoices!).Id, ["silent-image"])])
            ]
        });
        Assert.Equal((int)NativeOriginConstructionStatus.Authorized,
            selected.GetProperty("result").GetProperty("status").GetInt32());
        Assert.Equal(18, selected.GetProperty("allocation").GetProperty("committedLoad").GetInt32());
        var spell = Assert.Single(selected.GetProperty("innateSpells").GetProperty("spells").EnumerateArray());
        Assert.Equal("silent-image", spell.GetProperty("spellId").GetString());
        Assert.Equal("wisdom", spell.GetProperty("spellcastingAbility").GetString());
        var sense = Assert.Single(selected.GetProperty("tremorsense").GetProperty("senses").EnumerateArray());
        Assert.Equal(60, sense.GetProperty("rangeFeet").GetInt32());
        Assert.Equal(new[] { "earth", "stone" }, sense.GetProperty("contactMaterials")
            .EnumerateArray().Select(x => x.GetString()).ToArray());
    }

    [Fact]
    public async Task LifeExperienceHttpPreviewNeverGrantsUnselectedNestedEquipment()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true, realNativeContent: true);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var endpoint = "/api/mechanics/v1/origins/life-experiences/preview";
        async Task<(HttpStatusCode, JsonElement)> Send(NativeLifeExperienceProposal proposal, string fingerprint)
        {
            using var request = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new { rulesetId = catalog.RulesetId, fingerprint, proposal }));
            using var response = await fixture.Client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>());
        }
        var selections = new Dictionary<string, IReadOnlyList<string>>
        {
            ["upbringing.elf.general/cultural-training"] = ["skill.survival"],
            ["background.spy/spy-field-skills"] = ["skill.deception", "skill.stealth"],
            ["background.spy/spy-tradecraft-competency"] = ["thieves-tools"]
        };
        var proposal = new NativeLifeExperienceProposal("upbringing.elf.wood", "background.spy", selections);
        var (status, body) = await Send(proposal, catalog.Fingerprint);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.False(body.GetProperty("committed").GetBoolean());
        var result = body.GetProperty("result");
        Assert.Equal("needs-choices", result.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("earnedProjection").ValueKind);
        var sources = result.GetProperty("sources").EnumerateArray().ToArray();
        Assert.Equal(3, sources.Length);
        Assert.True(Assert.Single(sources, x => x.GetProperty("sourceId").GetString() ==
            "upbringing.elf.general").GetProperty("inherited").GetBoolean());
        var qualifications = result.GetProperty("candidateEquipment").GetProperty("qualifications")
            .EnumerateArray().Select(x => x.GetProperty("qualificationId").GetString()).ToArray();
        Assert.Contains("equipment.tool.thieves-tools", qualifications);
        Assert.Contains("equipment.weapon.longbow", qualifications);

        selections["background.spy/spy-tradecraft-competency"] = ["forgery"];
        (_, body) = await Send(proposal, catalog.Fingerprint);
        result = body.GetProperty("result");
        Assert.DoesNotContain(result.GetProperty("candidateEquipment").GetProperty("qualifications").EnumerateArray(),
            x => x.GetProperty("qualificationId").GetString() == "equipment.tool.thieves-tools");
        var forgery = Assert.Single(result.GetProperty("candidateGrants").EnumerateArray(),
            x => x.GetProperty("targetId").GetString() == "competency.forgery");
        Assert.Equal(1, forgery.GetProperty("ranks").GetInt32());
        Assert.False(forgery.GetProperty("proficient").GetBoolean());

        selections["background.spy/spy-field-skills"] = ["skill.stealth", "skill.stealth"];
        (_, body) = await Send(proposal, catalog.Fingerprint);
        Assert.Equal("invalid", body.GetProperty("result").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await Send(proposal, "stale")).Item1);
        using var anonymous = await fixture.Client.PostAsJsonAsync(endpoint,
            new { rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint, proposal });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    // Synthetic inventory records and authority are fixture-only. This does
    // not imply that the production ordinary item catalog already exists.
    private sealed class VerifiedLifeExperienceTestAuthority :
        INativeLifeExperienceHostAuthorityProvider, INativeLifeExperienceContextAuthority
    {
        public bool MismatchedInstrument { get; set; }
        public bool ForgedOccurrence { get; set; }
        public bool MagicalItem { get; set; }
        public bool DenyContext { get; set; }
        public Task<VerifiedLifeExperienceHostAuthorization?> VerifyAsync(Guid actorId,
            NativeLifeExperienceEndpoints.VerifiedRequest request, CancellationToken cancellationToken)
        {
            VerifiedLifeExperienceHostAuthorization? result = null;
            if (actorId == SupernaturalActor && request.CharacterId == SupernaturalCharacter &&
                request.OriginRevision == 1 && request.Proposal.UpbringingId == "upbringing.elf.wood" &&
                request.Proposal.BackgroundId == "background.entertainer")
                result = new(request.CharacterId, 1, new NativeLifeExperienceAuthorization(
                    new Dictionary<string, string>
                    {
                        ["upbringing.elf.general"] = "verified-parent",
                        ["upbringing.elf.wood"] = ForgedOccurrence ? "verified-parent" : "verified-upbringing",
                        ["background.entertainer"] = "verified-background"
                    }, this));
            return Task.FromResult(result);
        }

        public bool AuthorizesChoice(NativeLifeExperienceSource source,
            NativeLifeExperienceChoice choice, string optionId) => !DenyContext &&
            choice.Kind == "language" && optionId == "language.fixture";

        public NativeLifeExperienceItemProof? VerifyItem(NativeLifeExperienceSource source,
            NativeLifeExperienceItemIntent intent)
        {
            if (source.SourceId != "background.entertainer" || source.OccurrenceId != "verified-background")
                return null;
            return intent.Key switch
            {
                "background.entertainer/item/0" when
                    intent.ExpectedEquipmentTypeId == "equipment.tool.flute" &&
                    intent.SelectedItemId == "fixture.flute" =>
                    new("fixture.flute", MismatchedInstrument ? "equipment.tool.lute" : "equipment.tool.flute",
                        !MagicalItem, true),
                "background.entertainer/item/0" when intent.ExpectedEquipmentTypeId is null &&
                    intent.SelectedItemId == "fixture.disguise-kit" &&
                    intent.OrdinaryOptions.Contains("Disguise Kit") =>
                    new("fixture.disguise-kit", "equipment.tool.disguise-kit", true, true),
                "background.entertainer/item/1" when intent.SelectedItemId is null =>
                    new("fixture.token", null, true, true),
                "background.entertainer/item/2" when intent.SelectedItemId is null =>
                    new("fixture.costume", null, true, true),
                "background.entertainer/item/3" when intent.SelectedItemId is null =>
                    new("fixture.pouch", null, true, true),
                _ => null
            };
        }
    }

    [Fact]
    public async Task VerifiedLifeExperienceHttpRequiresWholeSelectionAndMatchingOrdinaryInventory()
    {
        await using var fixture = new Fixture();
        var authority = new VerifiedLifeExperienceTestAuthority();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true,
            realNativeContent: true, dm: true, lifeExperienceAuthority: authority);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        const string endpoint = "/api/mechanics/v1/origins/life-experiences/verified-plan";
        var selections = new Dictionary<string, IReadOnlyList<string>>
        {
            ["upbringing.elf.general/cultural-training"] = ["skill.survival"],
            ["background.entertainer/entertainer-performance-specialty"] = ["skill.acrobatics"],
            ["background.entertainer/entertainer-performance-method"] = ["musical-instrument"],
            ["background.entertainer/equipment/entertainer-musical-instrument"] = ["equipment.tool.flute"]
        };
        var items = new Dictionary<string, string> { ["background.entertainer/item/0"] = "fixture.flute" };
        var proposal = new NativeLifeExperienceProposal("upbringing.elf.wood", "background.entertainer", selections, items);
        async Task<(HttpStatusCode Code, JsonElement Body)> Send(long revision = 1)
        {
            using var request = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new
                {
                    characterId = SupernaturalCharacter, originRevision = revision,
                    rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint, proposal
                }));
            using var response = await fixture.Client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>());
        }
        var (status, body) = await Send();
        Assert.True(status == HttpStatusCode.OK, body.GetRawText());
        Assert.False(body.GetProperty("committed").GetBoolean());
        var earned = body.GetProperty("result").GetProperty("earnedProjection");
        var qualification = Assert.Single(earned.GetProperty("equipment").GetProperty("qualifications")
            .EnumerateArray(), x => x.GetProperty("qualificationId").GetString() == "equipment.tool.flute");
        Assert.Equal("verified-background", Assert.Single(qualification.GetProperty("provenance")
            .EnumerateArray()).GetProperty("sourceOccurrenceId").GetString());
        Assert.DoesNotContain(earned.GetProperty("grants").EnumerateArray(),
            x => x.GetProperty("targetId").GetString() == "competency.disguise");
        var first = earned.GetProperty("startingItems")[0];
        Assert.Equal("fixture.flute", first.GetProperty("verifiedItemId").GetString());
        Assert.Equal("equipment.tool.flute", first.GetProperty("expectedEquipmentTypeId").GetString());
        Assert.Equal(15, Assert.Single(earned.GetProperty("currency").EnumerateArray()).GetProperty("quantity").GetInt32());
        Assert.Equal(4, earned.GetProperty("startingItems").GetArrayLength());

        authority.MismatchedInstrument = true;
        (status, body) = await Send();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("result").GetProperty("earnedProjection").ValueKind);
        authority.MismatchedInstrument = false;
        authority.MagicalItem = true;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send()).Code);
        authority.MagicalItem = false;
        authority.ForgedOccurrence = true;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send()).Code);
        authority.ForgedOccurrence = false;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(2)).Code);

        // Switching outer branch invalidates a stale inner equipment choice.
        selections["background.entertainer/entertainer-performance-method"] = ["disguise-kit"];
        items["background.entertainer/item/0"] = "fixture.disguise-kit";
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send()).Code);
        selections.Remove("background.entertainer/equipment/entertainer-musical-instrument");
        (status, body) = await Send();
        Assert.True(status == HttpStatusCode.OK, body.GetRawText());
        earned = body.GetProperty("result").GetProperty("earnedProjection");
        var competency = Assert.Single(earned.GetProperty("competencyGrants").EnumerateArray());
        Assert.Equal("competency.disguise", competency.GetProperty("competencyId").GetString());
        Assert.Equal(1, competency.GetProperty("ranks").GetInt32());
        Assert.False(competency.GetProperty("proficient").GetBoolean());
        Assert.DoesNotContain(earned.GetProperty("equipment").GetProperty("qualifications").EnumerateArray(),
            x => x.GetProperty("qualificationId").GetString()!.StartsWith("equipment.tool.", StringComparison.Ordinal));
        Assert.Equal("fixture.disguise-kit", earned.GetProperty("startingItems")[0].GetProperty("verifiedItemId").GetString());
    }

    private sealed class VerifiedScopedCheckTestAuthority : INativeScopedSkillCheckHostAuthorityProvider
    {
        public Task<VerifiedScopedSkillCheckHostAuthorization?> VerifyAsync(Guid actorId,
            NativeLifeExperienceEndpoints.ScopedCheckRequest request, CancellationToken cancellationToken)
        {
            VerifiedScopedSkillCheckHostAuthorization? result = null;
            if (actorId == SupernaturalActor && request.CharacterId == SupernaturalCharacter &&
                request.OriginRevision == 1 && request.CheckEventId == "fixture.stonework")
                result = new(request.CharacterId, 1, request.CheckEventId,
                    new NativeScopedSkillCheckInput("upbringing.dwarf.general",
                        new Dictionary<string, string> { ["upbringing.dwarf.general"] = "verified-dwarf-upbringing" },
                        "skill.history", "intelligence", ["stonework-history"], 3, 3, 7, true));
            return Task.FromResult(result);
        }
    }

    [Fact]
    public async Task ScopedUpbringingHttpUsesVerifiedEventAndReplacesOrdinaryContribution()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true, realNativeContent: true,
            dm: true, scopedCheckAuthority: new VerifiedScopedCheckTestAuthority());
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        const string endpoint = "/api/mechanics/v1/origins/life-experiences/scoped-check/resolve";
        async Task<(HttpStatusCode, JsonElement)> Send(string checkEventId, string fingerprint)
        {
            using var request = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new { characterId = SupernaturalCharacter, originRevision = 1,
                    rulesetId = catalog.RulesetId, fingerprint, checkEventId }));
            using var response = await fixture.Client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>());
        }
        var (status, body) = await Send("fixture.stonework", catalog.Fingerprint);
        Assert.Equal(HttpStatusCode.OK, status);
        var result = body.GetProperty("result");
        Assert.Equal(10, result.GetProperty("total").GetInt32());
        Assert.Equal(6, result.GetProperty("proficiencyContribution").GetInt32());
        Assert.Equal("verified-dwarf-upbringing", Assert.Single(result.GetProperty("appliedEffects").EnumerateArray())
            .GetProperty("occurrenceId").GetString());
        Assert.False(body.GetProperty("committed").GetBoolean());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send("forged", catalog.Fingerprint)).Item1);
        Assert.Equal(HttpStatusCode.Conflict, (await Send("fixture.stonework", "stale")).Item1);
    }

    private static NativeOriginProposal OpenChoiceProposal(NativeCatalogSnapshot catalog,
        string speciesOrAncestry, string selectedOption)
    {
        var proposal = new NativeOriginProposal("fixture-open-choice-" + speciesOrAncestry,
            NativeOriginIntent.Direct, new("rule.creature-origination", "biological"),
            [], [], [], SelectedSpeciesId: speciesOrAncestry.StartsWith("species.") ? speciesOrAncestry : null,
            SelectedAncestryId: speciesOrAncestry.StartsWith("ancestry.") ? speciesOrAncestry : null);
        var candidates = new NativeOriginSourceDeconstructor(catalog).Deconstruct(proposal).Proposal.Candidates;
        var manifestations = new List<NativeOriginManifestation>();
        foreach (var candidate in candidates.Where(c => c.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible &&
            c.RequiredChoices is { Count: > 0 }))
        {
            var selected = candidate.RequiredChoices!.Select(requirement => new NativeOriginChoiceSelection(
                requirement.Id, [requirement.OptionCatalogId switch
                {
                    "any-native-skill" => "skill.intimidation",
                    "feat:origin" or "spell:wizard" => selectedOption,
                    _ => requirement.Id == "size" ? "medium" : "wisdom"
                }])).ToArray();
            manifestations.Add(new("selected:" + candidate.Id, candidate.Id, Choices: selected));
        }
        return proposal with { Manifestations = manifestations };
    }

    private sealed class OpenChoiceTestFeatAuthority :
        INativeOriginHostFeatChoiceAuthorityProvider, INativeOriginFeatChoiceAuthority
    {
        public bool Eligible { get; set; } = true;
        public Task<VerifiedOriginFeatChoiceAuthority?> VerifyAsync(Guid actorId,
            NativeOriginApiEndpoints.VerifiedDirectChoiceRequest request, CancellationToken cancellationToken)
        {
            VerifiedOriginFeatChoiceAuthority? result = null;
            if (actorId == SupernaturalActor && request.CharacterId == SupernaturalCharacter &&
                request.DraftRevision == 1 && request.Proposal.SelectedSpeciesId == "species.human")
                result = new(request.CharacterId, request.DraftRevision, this);
            return Task.FromResult(result);
        }
        public bool Authorizes(NativeOriginCandidate candidate, string featId) =>
            Eligible && featId == "feat.fixture-origin" &&
            candidate.Identity.MechanicalDefinitionId == "trait.versatile";
    }

    private sealed class OpenChoiceTestRestAuthority : INativeOriginHostCantripRestAuthorityProvider
    {
        public VerifiedOriginCantripRestAuthority? Stored { get; set; }
        public bool Consumed { get; set; }
        public Task<VerifiedOriginCantripRestAuthority?> VerifyAsync(Guid actorId,
            NativeOriginApiEndpoints.CantripRestRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(actorId == SupernaturalActor && !Consumed && Stored is not null &&
                request.CharacterId == Stored.CharacterId && request.RestEventId == Stored.RestEventId &&
                request.OriginRevision == Stored.OriginRevision ? Stored : null);
    }

    [Fact]
    public async Task OpenOriginCatalogDiscoveryDistinguishesUnpublishedCatalogsFromAvailableSkills()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true, realNativeContent: true);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        foreach (var (id, status, authority) in new[]
        {
            ("any-native-skill", "available", false),
            ("spell:wizard", "catalog-unavailable", false),
            ("feat:origin", "catalog-unavailable", true)
        })
        {
            using var request = fixture.Authenticated(HttpMethod.Get,
                "/api/mechanics/v1/origins/choice-catalog?fingerprint=" + catalog.Fingerprint +
                "&catalogId=" + Uri.EscapeDataString(id));
            using var response = await fixture.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var choices = body.GetProperty("choices");
            Assert.Equal(status, choices.GetProperty("status").GetString());
            Assert.Equal(authority, choices.GetProperty("requiresCharacterAuthority").GetBoolean());
            if (status == "catalog-unavailable") Assert.Equal(0, choices.GetProperty("options").GetArrayLength());
        }
        using var stale = fixture.Authenticated(HttpMethod.Get,
            "/api/mechanics/v1/origins/choice-catalog?fingerprint=stale&catalogId=spell%3Awizard");
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.SendAsync(stale)).StatusCode);
    }

    [Fact]
    public async Task NativeOpenChoicesRequireExactMembershipAndIndependentFeatEligibility()
    {
        await using var fixture = new Fixture();
        var authority = new OpenChoiceTestFeatAuthority();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true, realNativeContent: true,
            dm: true, featChoiceAuthority: authority, syntheticOpenChoiceCatalog: true);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var spellOptions = NativeOriginChoiceCatalog.Discover(catalog, "spell:wizard");
        Assert.Equal(new[] { "spell.fixture-cantrip-a", "spell.fixture-cantrip-b", "spell.prestidigitation" },
            spellOptions.Options.Select(option => option.Id).ToArray());
        Assert.Equal("feat.fixture-origin", Assert.Single(NativeOriginChoiceCatalog.Discover(catalog,
            "feat:origin").Options).Id);
        Assert.False(NativeOriginChoiceCatalog.IsMember(catalog, "spell:wizard", "spell.fixture-working"));
        var human = OpenChoiceProposal(catalog, "species.human", "feat.fixture-origin");
        var evaluator = new NativeOriginFirstPartyChoiceEvaluator(catalog);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, evaluator.Evaluate(human).Status);

        async Task<(HttpStatusCode Code, JsonElement Body)> Send(NativeOriginProposal proposal, long revision = 1)
        {
            using var request = fixture.Authenticated(HttpMethod.Post,
                "/api/mechanics/v1/origins/direct/verified-preview", JsonContent.Create(new
                {
                    characterId = SupernaturalCharacter, draftRevision = revision,
                    rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint, proposal
                }));
            using var response = await fixture.Client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>());
        }
        var (code, body) = await Send(human);
        Assert.Equal(HttpStatusCode.OK, code);
        var expected = evaluator.Evaluate(human, authority);
        Assert.True(expected.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", expected.Issues.Select(issue => issue.Code + ":" + issue.Detail)));
        var result = body.GetProperty("result");
        Assert.Empty(result.GetProperty("issues").EnumerateArray());
        Assert.False(body.GetProperty("committed").GetBoolean());
        var grant = Assert.Single(expected.Profile!.Underlying.InherentGrants,
            item => item.Identity.MechanicalDefinitionId == "trait.versatile");
        Assert.Equal("feat.fixture-origin", Assert.Single(Assert.Single(grant.ResolvedChoices!).SelectedOptionIds));
        authority.Eligible = false;
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, evaluator.Evaluate(human, authority).Status);
        authority.Eligible = true;
        var wrong = OpenChoiceProposal(catalog, "species.human", "feat.fixture-general");
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, evaluator.Evaluate(wrong, authority).Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(human, 2)).Code);

        foreach (var wrongSpell in new[] { "spell.fixture-off-list", "spell.fixture-leveled",
            "spell.fixture-unlisted", "spell.fixture-disabled", "spell.fixture-working" })
            Assert.NotEqual(NativeOriginConstructionStatus.Authorized,
                evaluator.Evaluate(OpenChoiceProposal(catalog, "ancestry.elf.high", wrongSpell)).Status);
        var elf = evaluator.Evaluate(OpenChoiceProposal(catalog, "ancestry.elf.high", "spell.fixture-cantrip-a"));
        Assert.True(elf.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", elf.Issues.Select(issue => issue.Code + ":" + issue.Detail)));
        var course = NativeOriginInnateSpellProgressionProjector.Project(catalog, elf, 5);
        Assert.Equal("resolved", course.Status);
        var cantrip = Assert.Single(course.Spells, item => item.TraitId == "trait.arcane-cantrip");
        Assert.Equal("fixture-cantrip-a", cantrip.SpellId);
        Assert.Equal("wisdom", cantrip.SpellcastingAbility);
        Assert.True(cantrip.AtWill);
        Assert.False(cantrip.SlotCasting);
        Assert.Null(cantrip.FreeUsesPerRecharge);

        var learned = new NativeLifeExperienceResolver(catalog).Resolve(new("upbringing.gnome.rock", "background.charlatan",
            new Dictionary<string, IReadOnlyList<string>> { ["upbringing.gnome.rock/prestidigitation"] = ["charisma"] }));
        var learnedSpell = Assert.Single(learned.CandidateGrants, item => item.Kind == "learned-cantrip");
        Assert.Equal("spell.prestidigitation", learnedSpell.TargetId);
        Assert.Equal("charisma", learnedSpell.AuthoredEffect!.Value.GetProperty("spellcastingAbility").GetString());
    }

    [Fact]
    public async Task VerifiedLongRestReplacesOnlyExistingCantripAndRecoversExistingPools()
    {
        await using var fixture = new Fixture();
        var authority = new OpenChoiceTestRestAuthority();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true, realNativeContent: true,
            dm: true, cantripRestAuthority: authority, syntheticOpenChoiceCatalog: true);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var proposal = OpenChoiceProposal(catalog, "ancestry.elf.high", "spell.fixture-cantrip-a");
        var before = new NativeOriginFirstPartyChoiceEvaluator(catalog).Evaluate(proposal);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, before.Status);
        var character = new NativeCharacterProjector(catalog).Project(new NativeCharacterRequest(
            catalog.RulesetId, catalog.Fingerprint, new Dictionary<string, int>
            {
                ["strength"] = 10, ["dexterity"] = 12, ["constitution"] = 14,
                ["intelligence"] = 8, ["wisdom"] = 15, ["charisma"] = 11
            }, [new("class.warlock", 5, "fixture-warlock")]));
        var view = NativeOriginInnateSpellProgressionProjector.ForCharacter(catalog, before, character);
        Assert.Equal("resolved", view.Status);
        var grantId = Assert.Single(view.Spells, spell => spell.TraitId == "trait.arcane-cantrip").GrantId;
        var misty = Assert.Single(view.Spells, spell => spell.SpellId == "misty-step");
        var boot = NativeOriginInnateSpellResourceLedger.Initialize(catalog, before, character);
        Assert.Equal("resolved", boot.Status);
        var spent = NativeOriginInnateSpellResourceLedger.Apply(catalog, before, character, boot.State!,
            new(NativeOriginInnateSpellLedgerActionKind.SpendFreeCast, boot.State!.Revision, misty.GrantId));
        Assert.Equal("resolved", spent.Status);
        Assert.Equal(0, spent.State!.RemainingFreeCasts[misty.GrantId]);
        authority.Stored = new(SupernaturalCharacter, 7, "fixture-long-rest", proposal, before.Profile!, character, spent.State);

        var plan = NativeOriginCantripReplacementPlanner.Plan(catalog, proposal, before.Profile!, character,
            spent.State, spent.State.Revision, grantId, "spell.fixture-cantrip-b");
        Assert.True(plan.Status == "resolved", plan.Reason);
        Assert.Equal(grantId, plan.GrantId);
        Assert.Equal(1, plan.Ledger!.RemainingFreeCasts[misty.GrantId]);
        Assert.Equal(spent.State.Revision + 1, plan.Ledger.Revision);
        Assert.NotEqual(spent.State.ProjectionFingerprint, plan.Ledger.ProjectionFingerprint);
        var after = NativeOriginInnateSpellProgressionProjector.ForCharacter(catalog, plan.Replacement!, character);
        var newCantrip = Assert.Single(after.Spells, spell => spell.GrantId == grantId);
        Assert.Equal("fixture-cantrip-b", newCantrip.SpellId);
        Assert.Equal("wisdom", newCantrip.SpellcastingAbility);
        Assert.Equal(misty, Assert.Single(after.Spells, spell => spell.GrantId == misty.GrantId));
        Assert.NotNull(plan.UpdatedSourceProposal);
        Assert.DoesNotContain(plan.UpdatedSourceProposal!.Manifestations.SelectMany(manifest =>
            manifest.Choices ?? []), choice => choice.CatalogEligibilityEvidenceId is not null);
        var followingRest = NativeOriginCantripReplacementPlanner.Plan(catalog,
            plan.UpdatedSourceProposal, plan.Replacement!.Profile!, character,
            plan.Ledger!, plan.Ledger!.Revision, grantId, "spell.fixture-cantrip-a");
        Assert.True(followingRest.Status == "resolved", followingRest.Reason);
        Assert.Equal(plan.Ledger.Revision + 1, followingRest.Ledger!.Revision);
        Assert.Equal("permitted", NativeOriginInnateSpellResourceLedger.Apply(catalog, plan.Replacement!, character,
            plan.Ledger, new(NativeOriginInnateSpellLedgerActionKind.CheckAtWill, plan.Ledger.Revision, grantId)).Status);
        Assert.Equal("unresolved", NativeOriginInnateSpellResourceLedger.Apply(catalog, before, character,
            plan.Ledger, new(NativeOriginInnateSpellLedgerActionKind.CheckAtWill, plan.Ledger.Revision, grantId)).Status);

        async Task<HttpStatusCode> Send(string selected, string eventId = "fixture-long-rest",
            long revision = 7, long? ledgerRevision = null, string? selectedGrantId = null)
        {
            using var request = fixture.Authenticated(HttpMethod.Post,
                "/api/mechanics/v1/origins/innate-spells/cantrip-replacement/plan", JsonContent.Create(new
                {
                    characterId = SupernaturalCharacter, originRevision = revision,
                    ledgerRevision = ledgerRevision ?? spent.State.Revision,
                    rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint, restEventId = eventId,
                    grantId = selectedGrantId ?? grantId, replacementSpellId = selected
                }));
            using var response = await fixture.Client.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var body = await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.False(body.GetProperty("committed").GetBoolean());
                Assert.Equal("spell.fixture-cantrip-b", body.GetProperty("plan").GetProperty("newSpellId").GetString());
            }
            return response.StatusCode;
        }
        Assert.Equal(HttpStatusCode.OK, await Send("spell.fixture-cantrip-b"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await Send("spell.fixture-off-list"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await Send("spell.fixture-cantrip-a"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await Send("spell.fixture-cantrip-b", eventId: "fixture-short-rest"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await Send("spell.fixture-cantrip-b", revision: 6));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await Send("spell.fixture-cantrip-b", ledgerRevision: 0));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await Send("spell.fixture-cantrip-b", selectedGrantId: misty.GrantId));
        authority.Consumed = true;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await Send("spell.fixture-cantrip-b"));
        Assert.Equal(0, authority.Stored.Ledger.RemainingFreeCasts[misty.GrantId]);
    }

    [Fact]
    public async Task NativeOriginDiscoveryReturnsAuthoredPresetsMethodsAndConstructConfiguration()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true, realNativeContent: true);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        const string root = "/api/mechanics/v1/origins";
        async Task<(HttpStatusCode, JsonElement)> Get(string path, string? fingerprint = null)
        {
            var url = root + path + (path.Contains('?') ? "&" : "?") +
                "fingerprint=" + (fingerprint ?? catalog.Fingerprint);
            using var request = fixture.Authenticated(HttpMethod.Get, url);
            using var response = await fixture.Client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>());
        }

        var (methodStatus, methodBody) = await Get("/methods?creatureTypeId=creature-type.construct");
        Assert.Equal(HttpStatusCode.OK, methodStatus);
        var methods = methodBody.GetProperty("methods").EnumerateArray().ToArray();
        var construct = Assert.Single(methods, x =>
            x.GetProperty("reference").GetProperty("methodId").GetString() == "construct-assembly");
        Assert.Equal("available", construct.GetProperty("defaultAvailability").GetString());
        Assert.Equal("assembly-preview", construct.GetProperty("support").GetString());
        var plant = Assert.Single(methods, x =>
            x.GetProperty("reference").GetProperty("methodId").GetString() == "plant-reproduction");
        Assert.Equal("reference-only", plant.GetProperty("support").GetString());
        Assert.False(construct.TryGetProperty("authoredDefinition", out _));

        var (presetStatus, presetBody) = await Get("/presets");
        Assert.Equal(HttpStatusCode.OK, presetStatus);
        var presets = presetBody.GetProperty("presets").EnumerateArray().ToArray();
        var rock = Assert.Single(presets, x => x.GetProperty("id").GetString() == "ancestry.gnome.rock");
        Assert.True(rock.GetProperty("selectable").GetBoolean());
        Assert.Equal("species.gnome", rock.GetProperty("parentId").GetString());
        Assert.Equal("upbringing.gnome.rock", rock.GetProperty("defaultUpbringingId").GetString());
        var tiefling = Assert.Single(presets, x => x.GetProperty("id").GetString() == "species.tiefling");
        Assert.False(tiefling.GetProperty("selectable").GetBoolean());
        Assert.True(tiefling.GetProperty("ancestrySelectionRequired").GetBoolean());

        var (frameStatus, frameBody) = await Get("/construct/components/construct-component.structural-frame/configuration");
        Assert.Equal(HttpStatusCode.OK, frameStatus);
        var frame = frameBody.GetProperty("configuration");
        Assert.Equal("frame", Assert.Single(frame.GetProperty("materialSlots").EnumerateArray()).GetString());
        var medium = Assert.Single(frame.GetProperty("constructionVariants").EnumerateArray(),
            x => x.GetProperty("id").GetString() == "medium");
        Assert.Equal("medium", medium.GetProperty("bodySize").GetString());
        Assert.Equal("crafting-recipe.construct-component.structural-frame.medium",
            medium.GetProperty("recipeId").GetString());
        Assert.False(frame.GetProperty("powerSourceRequired").GetBoolean());
        Assert.Empty(frame.GetProperty("powerSources").EnumerateArray());

        var (housingStatus, housingBody) = await Get("/construct/components/construct-component.power-source-housing/configuration");
        Assert.Equal(HttpStatusCode.OK, housingStatus);
        var housing = housingBody.GetProperty("configuration");
        Assert.True(housing.GetProperty("powerSourceRequired").GetBoolean());
        Assert.Contains(housing.GetProperty("powerSources").EnumerateArray(),
            x => x.GetProperty("id").GetString() == "construct-power-source.solar-crystal");
        Assert.Equal(HttpStatusCode.NotFound,
            (await Get("/construct/components/species.human/configuration")).Item1);
        Assert.Equal(HttpStatusCode.Conflict, (await Get("/presets", "stale")).Item1);
    }

    [Fact]
    public async Task RealNativeConstructHttpPreviewUsesSharedAssemblyAndOriginEngine()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet",
            ordinary: true, realNativeContent: true);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        string? reference = null;
        while (folder is not null)
        {
            var candidate = Path.Combine(folder.FullName, "data", "reference",
                "construct-basic-starting-build-20261120.json");
            if (File.Exists(candidate)) { reference = candidate; break; }
            folder = folder.Parent;
        }
        Assert.NotNull(reference);
        using var data = JsonDocument.Parse(File.ReadAllText(reference!));
        var assembly = data.RootElement.GetProperty("assembly")
            .Deserialize<NativeConstructAssemblyRequest>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var proposal = new NativeOriginProposal("http-construct",
            NativeOriginIntent.Custom, new("rule.creature-origination", "construct-assembly"),
            [], [], []);
        const string endpoint = "/api/mechanics/v1/origins/construct/preview";

        async Task<HttpResponseMessage> Send(NativeOriginProposal draft,
            NativeConstructAssemblyRequest body, string fingerprint)
        {
            using var request = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new { rulesetId = catalog.RulesetId, fingerprint,
                    proposal = draft, assembly = body }));
            return await fixture.Client.SendAsync(request);
        }

        using var discovery = await Send(proposal, assembly, catalog.Fingerprint);
        Assert.Equal(HttpStatusCode.OK, discovery.StatusCode);
        var first = await discovery.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(first.GetProperty("previewOnly").GetBoolean());
        var offers = first.GetProperty("result").GetProperty("origin")
            .GetProperty("evaluatedProposal").GetProperty("candidates")
            .Deserialize<NativeOriginCandidate[]>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.NotEmpty(offers);
        var manifested = proposal with
        {
            Manifestations = offers.Select((candidate, i) => new NativeOriginManifestation(
                "http-selection-" + i, candidate.Id,
                SelectedValue: candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic
                    ? candidate.OfferedValue : null)).ToArray()
        };
        using var constructed = await Send(manifested, assembly, catalog.Fingerprint);
        Assert.Equal(HttpStatusCode.OK, constructed.StatusCode);
        var result = await constructed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("result").GetProperty("isMechanicallyAuthorized").GetBoolean());
        Assert.Equal(163, result.GetProperty("result").GetProperty("assembly")
            .GetProperty("knownConstructionCost").GetInt64());
        Assert.Equal("resolved", result.GetProperty("movementModes").GetProperty("status").GetString());

        using var stale = await Send(manifested, assembly, "stale");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var forgedAllowance = await Send(manifested,
            assembly with { InitialAllowance = 9999 }, catalog.Fingerprint);
        var forged = await forgedAllowance.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(forged.GetProperty("result").GetProperty("isMechanicallyAuthorized").GetBoolean());
        using var modification = await Send(manifested,
            assembly with { InitialConstruction = false, InitialAllowance = null }, catalog.Fingerprint);
        var modified = await modification.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(modified.GetProperty("result").GetProperty("isMechanicallyAuthorized").GetBoolean());
    }

    private sealed class ConstructOperatingTestAuthority : INativeConstructHostOperatingAuthorityProvider
    {
        public VerifiedConstructOperatingAuthorization? Stored { get; set; }

        public Task<VerifiedConstructOperatingAuthorization?> VerifyAsync(Guid actorId,
            NativeOriginApiEndpoints.VerifiedConstructOperatingRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(actorId == Guid.Parse("d66a7a7b-bc0f-4677-829d-0ccf40470d2f")
                ? Stored : null);
    }

    [Fact]
    public async Task VerifiedConstructOperatingHttpPlansOnlyInstalledComponentTransitions()
    {
        var provider = new ConstructOperatingTestAuthority();
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet",
            ordinary: true, realNativeContent: true, dm: true,
            constructOperatingAuthority: provider);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        string? reference = null;
        while (folder is not null)
        {
            var candidate = Path.Combine(folder.FullName, "data", "reference",
                "construct-basic-starting-build-20261120.json");
            if (File.Exists(candidate)) { reference = candidate; break; }
            folder = folder.Parent;
        }
        Assert.NotNull(reference);
        using var data = JsonDocument.Parse(File.ReadAllText(reference!));
        var assembly = data.RootElement.GetProperty("assembly")
            .Deserialize<NativeConstructAssemblyRequest>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var proposal = new NativeOriginProposal("verified-operating-construct",
            NativeOriginIntent.Custom, new("rule.creature-origination", "construct-assembly"),
            [], [], []);
        var service = new NativeConstructOriginConstructionService(catalog);
        var discovery = service.Evaluate(new(new(proposal), assembly));
        var selected = proposal with
        {
            Manifestations = discovery.Origin.EvaluatedProposal.Candidates.Select((candidate, i) =>
                new NativeOriginManifestation("selection-" + i, candidate.Id,
                    SelectedValue: candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic
                        ? candidate.OfferedValue : null)).ToArray()
        };
        var construction = service.Evaluate(new(new(selected), assembly));
        Assert.True(construction.IsMechanicallyAuthorized);
        var current = new NativeConstructOperatingPreset("current",
            assembly.Components.Select(part => new NativeConstructPresetComponent(
                part.InstanceId, part.DefinitionId, part.Enabled, part.ModeId)).ToArray());
        var target = current with
        {
            Name = "drive off",
            Components = current.Components.Select(part => part.InstanceId == "drive"
                ? part with { Enabled = false } : part).ToArray()
        };
        var characterId = Guid.NewGuid();
        const string endpoint = "/api/mechanics/v1/origins/construct/operating/plan";

        async Task<HttpResponseMessage> Send(NativeConstructOperatingPreset preset,
            long revision = 2, string? fingerprint = null)
        {
            using var request = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new { characterId, originRevision = 1,
                    operatingRevision = revision, rulesetId = catalog.RulesetId,
                    fingerprint = fingerprint ?? catalog.Fingerprint, target = preset }));
            return await fixture.Client.SendAsync(request);
        }

        using var unverified = await Send(target);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unverified.StatusCode);
        provider.Stored = new(characterId, 1, 2, catalog.RulesetId, catalog.Fingerprint,
            construction, current);
        using var stale = await Send(target, revision: 1);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, stale.StatusCode);
        using var staleCatalog = await Send(target, fingerprint: "stale");
        Assert.Equal(HttpStatusCode.Conflict, staleCatalog.StatusCode);

        using var planned = await Send(target);
        Assert.Equal(HttpStatusCode.OK, planned.StatusCode);
        var result = await planned.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(result.GetProperty("committed").GetBoolean());
        Assert.True(result.GetProperty("transition").GetProperty("isTargetValid").GetBoolean());
        Assert.Contains(result.GetProperty("transition").GetProperty("interruptedComponentInstanceIds")
            .EnumerateArray(), item => item.GetString() == "drive");
        Assert.Contains(result.GetProperty("beforeMovement").GetProperty("modes")
            .EnumerateArray(), mode => mode.GetProperty("mode").GetString() == "walk");
        Assert.DoesNotContain(result.GetProperty("afterMovement").GetProperty("modes")
            .EnumerateArray(), mode => mode.GetProperty("mode").GetString() == "walk");

        // A caller cannot install or substitute a component via operating preferences.
        using var forged = await Send(target with
        {
            Components = target.Components.Select(part => part.InstanceId == "drive"
                ? part with { DefinitionId = "construct-component.crystal-eye" } : part).ToArray()
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, forged.StatusCode);

        // A preview does not change the provider's committed operating revision.
        Assert.Equal(2L, provider.Stored.OperatingRevision);
        provider.Stored = provider.Stored with { CurrentPreset = target, OperatingRevision = 3 };
        using var restored = await Send(current, revision: 3);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        var recovery = await restored.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(recovery.GetProperty("afterMovement").GetProperty("modes")
            .EnumerateArray(), mode => mode.GetProperty("mode").GetString() == "walk");
    }

    private sealed class ConstructSolarTestAuthority : INativeConstructHostSolarIntervalAuthorityProvider
    {
        public VerifiedConstructSolarIntervalAuthorization? Stored { get; set; }
        public Task<VerifiedConstructSolarIntervalAuthorization?> VerifyAsync(Guid actorId,
            NativeOriginApiEndpoints.VerifiedConstructSolarIntervalRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(actorId == Guid.Parse("d66a7a7b-bc0f-4677-829d-0ccf40470d2f")
                ? Stored : null);
    }

    [Fact]
    public async Task VerifiedConstructSolarHttpPlansExhaustionAndPreservesDurableState()
    {
        var provider = new ConstructSolarTestAuthority();
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet",
            ordinary: true, realNativeContent: true, dm: true,
            solarIntervalAuthority: provider);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        string? reference = null;
        while (folder is not null)
        {
            var candidate = Path.Combine(folder.FullName, "data", "reference",
                "construct-small-starting-build-20261121.json");
            if (File.Exists(candidate)) { reference = candidate; break; }
            folder = folder.Parent;
        }
        Assert.NotNull(reference);
        using var data = JsonDocument.Parse(File.ReadAllText(reference!));
        var baseAssembly = data.RootElement.GetProperty("assembly")
            .Deserialize<NativeConstructAssemblyRequest>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var assembly = baseAssembly with
        {
            Components = baseAssembly.Components.Select(part => part.InstanceId == "generator"
                ? part with { PowerSourceId = "construct-power-source.solar-crystal" } : part).ToArray()
        };
        var proposal = new NativeOriginProposal("verified-solar-construct",
            NativeOriginIntent.Custom, new("rule.creature-origination", "construct-assembly"),
            [], [], []);
        var service = new NativeConstructOriginConstructionService(catalog);
        var discovery = service.Evaluate(new(new(proposal), assembly));
        var selected = proposal with
        {
            Manifestations = discovery.Origin.EvaluatedProposal.Candidates.Select((candidate, i) =>
                new NativeOriginManifestation("selection-" + i, candidate.Id,
                    SelectedValue: candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic
                        ? candidate.OfferedValue : null)).ToArray()
        };
        var construction = service.Evaluate(new(new(selected), assembly));
        Assert.True(construction.IsMechanicallyAuthorized,
            JsonSerializer.Serialize(construction.Origin.Issues));
        var current = new NativeConstructOperatingPreset("current",
            assembly.Components.Select(part => new NativeConstructPresetComponent(
                part.InstanceId, part.DefinitionId, part.Enabled, part.ModeId)).ToArray());
        var characterId = Guid.NewGuid();
        const string endpoint = "/api/mechanics/v1/origins/construct/solar/interval/plan";

        async Task<HttpResponseMessage> Send(string eventId = "dark-hour", long revision = 4)
        {
            using var request = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new { characterId, originRevision = 1, operatingRevision = 2,
                    solarRevision = revision, rulesetId = catalog.RulesetId,
                    fingerprint = catalog.Fingerprint, intervalEventId = eventId }));
            return await fixture.Client.SendAsync(request);
        }
        using var missing = await Send();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        provider.Stored = new(characterId, 1, 2, 4, catalog.RulesetId, catalog.Fingerprint,
            "dark-hour", construction, current,
            new(new Dictionary<string, long> { ["generator"] = 0 },
                new Dictionary<string, long> { ["generator"] = 0 },
                new Dictionary<string, int> { ["generator"] = 0 }),
            new Dictionary<string, NativeConstructSolarExposure> { ["generator"] = NativeConstructSolarExposure.None }, 60);
        using var stale = await Send(revision: 3);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, stale.StatusCode);
        using var wrongEvent = await Send("unverified-event");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongEvent.StatusCode);

        using var first = await Send();
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(firstBody.GetProperty("committed").GetBoolean());
        var plan = firstBody.GetProperty("plan");
        Assert.True(plan.GetProperty("canAdvance").GetBoolean());
        var next = plan.GetProperty("nextState");
        Assert.Equal(60L, next.GetProperty("consecutiveDeficitMinutes").GetProperty("generator").GetInt64());
        Assert.Equal(1, next.GetProperty("exhaustionEventsIssued").GetProperty("generator").GetInt32());
        Assert.True(next.GetProperty("blocksSolarExhaustionRecovery").GetBoolean());
        Assert.Equal(0, provider.Stored.State.ExhaustionEventsIssued["generator"]);
        // The owning application, not Rules Core, commits and consumes the event.
        provider.Stored = provider.Stored with
        {
            SolarRevision = 5, IntervalEventId = "second-dark-hour",
            State = next.Deserialize<NativeConstructSolarState>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!
        };
        using var replay = await Send();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, replay.StatusCode);
        using var second = await Send("second-dark-hour", 5);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        var secondState = secondBody.GetProperty("plan").GetProperty("nextState");
        Assert.Equal(120L, secondState.GetProperty("consecutiveDeficitMinutes").GetProperty("generator").GetInt64());
        Assert.Equal(1, secondState.GetProperty("exhaustionEventsIssued").GetProperty("generator").GetInt32());
        provider.Stored = provider.Stored with
        {
            SolarRevision = 6, IntervalEventId = "daylight-hour",
            State = secondState.Deserialize<NativeConstructSolarState>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!,
            Exposures = new Dictionary<string, NativeConstructSolarExposure> { ["generator"] = NativeConstructSolarExposure.Direct }
        };
        using var daylight = await Send("daylight-hour", 6);
        Assert.Equal(HttpStatusCode.OK, daylight.StatusCode);
        var daylightBody = await daylight.Content.ReadFromJsonAsync<JsonElement>();
        var recovered = daylightBody.GetProperty("plan").GetProperty("nextState");
        Assert.False(recovered.GetProperty("blocksSolarExhaustionRecovery").GetBoolean());
        Assert.Equal(1, recovered.GetProperty("exhaustionEventsIssued").GetProperty("generator").GetInt32());
    }

    private sealed class ConstructContextualTestAuthority :
        INativeConstructHostContextualPowerAuthorityProvider, INativeConstructHostSoulCustodyAuthorityProvider
    {
        public VerifiedConstructContextualPowerAuthorization? Power { get; set; }
        public VerifiedConstructSoulCustodyAuthorization? Custody { get; set; }
        public Task<VerifiedConstructContextualPowerAuthorization?> VerifyAsync(Guid actorId,
            NativeOriginApiEndpoints.VerifiedConstructContextualPowerRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(actorId == Guid.Parse("d66a7a7b-bc0f-4677-829d-0ccf40470d2f")
                ? Power : null);
        public Task<VerifiedConstructSoulCustodyAuthorization?> VerifyAsync(Guid actorId,
            NativeOriginApiEndpoints.VerifiedConstructSoulCustodyRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(actorId == Guid.Parse("d66a7a7b-bc0f-4677-829d-0ccf40470d2f")
                ? Custody : null);
    }

    private static (NativeConstructOriginConstructionResult Construction, NativeConstructOperatingPreset Preset)
        ContextualConstruct(NativeCatalogSnapshot catalog, string sourceId)
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        string? reference = null;
        while (folder is not null)
        {
            var candidate = Path.Combine(folder.FullName, "data", "reference",
                "construct-small-starting-build-20261121.json");
            if (File.Exists(candidate)) { reference = candidate; break; }
            folder = folder.Parent;
        }
        Assert.NotNull(reference);
        using var data = JsonDocument.Parse(File.ReadAllText(reference!));
        var initial = data.RootElement.GetProperty("assembly").Deserialize<NativeConstructAssemblyRequest>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var assembly = initial with
        {
            Components = initial.Components.Select(part => part.InstanceId == "generator"
                ? part with { PowerSourceId = sourceId } : part).ToArray()
        };
        var proposal = new NativeOriginProposal("verified-contextual-construct",
            NativeOriginIntent.Custom, new("rule.creature-origination", "construct-assembly"),
            [], [], []);
        var service = new NativeConstructOriginConstructionService(catalog);
        var discovery = service.Evaluate(new(new(proposal), assembly));
        var selected = proposal with
        {
            Manifestations = discovery.Origin.EvaluatedProposal.Candidates.Select((candidate, i) =>
                new NativeOriginManifestation("selection-" + i, candidate.Id,
                    SelectedValue: candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic
                        ? candidate.OfferedValue : null)).ToArray()
        };
        var construction = service.Evaluate(new(new(selected), assembly));
        Assert.True(construction.IsMechanicallyAuthorized, JsonSerializer.Serialize(construction.Origin.Issues));
        return (construction, new("current", assembly.Components.Select(part =>
            new NativeConstructPresetComponent(part.InstanceId, part.DefinitionId, part.Enabled, part.ModeId)).ToArray()));
    }

    [Theory]
    [InlineData("soul", "construct-power-source.soul-gem")]
    [InlineData("infernal", "construct-power-source.infernal-engine")]
    public async Task VerifiedConstructContextualPowerPreservesDistinctSourceMechanics(string kind, string sourceId)
    {
        var provider = new ConstructContextualTestAuthority();
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet",
            ordinary: true, realNativeContent: true, dm: true, contextualPowerAuthority: provider);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var (construction, current) = ContextualConstruct(catalog, sourceId);
        var characterId = Guid.NewGuid();
        const string endpoint = "/api/mechanics/v1/origins/construct/contextual-power/resolve";

        async Task<HttpResponseMessage> Send(long revision = 4, string? requestedKind = null)
        {
            using var request = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new { characterId, originRevision = 1, operatingRevision = 2,
                    contextRevision = revision, rulesetId = catalog.RulesetId,
                    fingerprint = catalog.Fingerprint, sourceKind = requestedKind ?? kind }));
            return await fixture.Client.SendAsync(request);
        }
        using var missing = await Send();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        provider.Power = new(characterId, 1, 2, 4, catalog.RulesetId, catalog.Fingerprint,
            kind, construction, current,
            new Dictionary<string, bool> { ["generator"] = kind == "soul" },
            new Dictionary<string, bool> { ["generator"] = kind == "soul" });
        using var stale = await Send(revision: 3);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, stale.StatusCode);
        using var wrong = await Send(requestedKind: kind == "soul" ? "infernal" : "soul");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrong.StatusCode);
        using var resolved = await Send();
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        var body = await resolved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("committed").GetBoolean());
        var projection = body.GetProperty(kind);
        Assert.True(projection.GetProperty("canOperate").GetBoolean());
        var source = Assert.Single(projection.GetProperty("sources").EnumerateArray());
        Assert.Equal(kind == "soul" ? 12 : 8, source.GetProperty("availablePowerOutput").GetInt32());
        if (kind == "soul")
            Assert.True(source.GetProperty("overdriveActive").GetBoolean());
        else
        {
            Assert.True(source.GetProperty("requiresInstabilityAdjudication").GetBoolean());
            Assert.False(source.GetProperty("empowermentActive").GetBoolean());
        }
    }

    [Fact]
    public async Task VerifiedConstructSoulCustodyPlansCaptureAndResolutionWithoutDurableWrites()
    {
        var provider = new ConstructContextualTestAuthority();
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet",
            ordinary: true, realNativeContent: true, dm: true, soulCustodyAuthority: provider);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var (construction, _) = ContextualConstruct(catalog, "construct-power-source.soul-gem");
        var characterId = Guid.NewGuid();
        const string endpoint = "/api/mechanics/v1/origins/construct/soul/custody/plan";

        async Task<HttpResponseMessage> Send(string eventId = "death-1", long revision = 4)
        {
            using var request = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new { characterId, originRevision = 1,
                    custodyRevision = revision, rulesetId = catalog.RulesetId,
                    fingerprint = catalog.Fingerprint, eventId }));
            return await fixture.Client.SendAsync(request);
        }
        using var missing = await Send();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        provider.Custody = new(characterId, 1, 4, catalog.RulesetId, catalog.Fingerprint,
            construction, [], new("death-1", "soul-1", "generator",
                NativeConstructSoulDisposition.Imprisoned,
                new Dictionary<string, int> { ["generator"] = 10 }, true));
        using var stale = await Send(revision: 3);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, stale.StatusCode);
        using var wrongEvent = await Send("forged-event");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongEvent.StatusCode);
        using var captured = await Send();
        Assert.Equal(HttpStatusCode.OK, captured.StatusCode);
        var body = await captured.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("committed").GetBoolean());
        Assert.Empty(provider.Custody.Records);
        var next = body.GetProperty("plan").GetProperty("nextRecords")
            .Deserialize<NativeConstructSoulCustodyRecord[]>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(NativeConstructSoulDisposition.Imprisoned, Assert.Single(next).Disposition);
        provider.Custody = provider.Custody with { Records = next, CustodyRevision = 5 };
        using var duplicate = await Send(revision: 5);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.StatusCode);
        provider.Custody = provider.Custody with
        {
            Event = new("rescue-1", "soul-1", "wrong-core", NativeConstructSoulDisposition.Extracted)
        };
        using var wrongCore = await Send("rescue-1", 5);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongCore.StatusCode);
        provider.Custody = provider.Custody with
        {
            Event = provider.Custody.Event with { CoreInstanceId = "generator" }
        };
        using var extracted = await Send("rescue-1", 5);
        Assert.Equal(HttpStatusCode.OK, extracted.StatusCode);
        var extractedBody = await extracted.Content.ReadFromJsonAsync<JsonElement>();
        var released = extractedBody.GetProperty("plan").GetProperty("nextRecords")
            .Deserialize<NativeConstructSoulCustodyRecord[]>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(NativeConstructSoulDisposition.Extracted, Assert.Single(released).Disposition);
        Assert.Equal(NativeConstructSoulDisposition.Imprisoned, Assert.Single(provider.Custody.Records).Disposition);
    }

    private sealed class ConstructSustenanceTestAuthority : INativeConstructHostSustenanceAuthorityProvider
    {
        public VerifiedConstructSustenanceAuthorization? Stored { get; set; }
        public Task<VerifiedConstructSustenanceAuthorization?> VerifyAsync(Guid actorId,
            NativeOriginApiEndpoints.VerifiedConstructSustenanceRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(actorId == Guid.Parse("d66a7a7b-bc0f-4677-829d-0ccf40470d2f") ? Stored : null);
    }

    [Theory]
    [InlineData("mechanical-day", "construct-power-source.mechanical-engine")]
    [InlineData("infernal-long-rest", "construct-power-source.infernal-engine")]
    public async Task VerifiedConstructSustenancePlansPaidAndMissedEvents(string kind, string sourceId)
    {
        var provider = new ConstructSustenanceTestAuthority();
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true,
            realNativeContent: true, dm: true, sustenanceAuthority: provider);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var (construction, _) = ContextualConstruct(catalog, sourceId);
        var characterId = Guid.NewGuid();
        var mechanical = kind == "mechanical-day";
        const string endpoint = "/api/mechanics/v1/origins/construct/sustenance/plan";
        async Task<HttpResponseMessage> Send(long inventoryRevision = 7, string eventId = "event-1")
        {
            using var request = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new { characterId, originRevision = 1, sustenanceRevision = 4,
                    inventoryRevision, rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                    eventId, eventKind = kind }));
            return await fixture.Client.SendAsync(request);
        }
        using var missing = await Send();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        provider.Stored = new(characterId, 1, 4, 7, catalog.RulesetId, catalog.Fingerprint,
            "event-1", kind, construction, new Dictionary<string, int> { ["generator"] = 0 },
            mechanical ? 0 : null, mechanical ? 3 : null);
        using var stale = await Send(inventoryRevision: 6);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, stale.StatusCode);
        using var wrongEvent = await Send(eventId: "wrong-event");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongEvent.StatusCode);

        using var missed = await Send();
        Assert.Equal(HttpStatusCode.OK, missed.StatusCode);
        var body = await missed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("committed").GetBoolean());
        var plan = body.GetProperty("plan");
        var source = Assert.Single(plan.GetProperty("sources").EnumerateArray());
        Assert.Equal(1, source.GetProperty("requiredUnits").GetInt32());
        Assert.Equal(0, source.GetProperty("proposedDebitUnits").GetInt32());
        Assert.False(source.GetProperty("fullSustenanceProvided").GetBoolean());
        if (mechanical)
        {
            var deprivation = plan.GetProperty("dailyDeprivation");
            Assert.Equal(4, deprivation.GetProperty("consecutiveDaysWithoutFullRation").GetInt32());
            Assert.Equal(1, deprivation.GetProperty("exhaustionLevelsGained").GetInt32());
        }
        else
        {
            Assert.True(source.GetProperty("requiresInstabilityAdjudication").GetBoolean());
            Assert.False(source.GetProperty("empowermentActiveAfter").GetBoolean());
        }
        provider.Stored = provider.Stored with
        {
            ReservedFuelUnits = new Dictionary<string, int> { ["generator"] = 1 }
        };
        using var paid = await Send();
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        var paidBody = await paid.Content.ReadFromJsonAsync<JsonElement>();
        var paidPlan = paidBody.GetProperty("plan");
        var paidSource = Assert.Single(paidPlan.GetProperty("sources").EnumerateArray());
        Assert.Equal(1, paidSource.GetProperty("proposedDebitUnits").GetInt32());
        Assert.True(paidSource.GetProperty("fullSustenanceProvided").GetBoolean());
        Assert.False(paidSource.GetProperty("requiresInstabilityAdjudication").GetBoolean());
        if (mechanical)
            Assert.Equal(0, paidPlan.GetProperty("dailyDeprivation")
                .GetProperty("consecutiveDaysWithoutFullRation").GetInt32());
        else
            Assert.False(paidSource.GetProperty("empowermentActiveAfter").GetBoolean());
        Assert.Equal(1, provider.Stored.ReservedFuelUnits["generator"]);
        provider.Stored = provider.Stored with
        {
            ReservedFuelUnits = new Dictionary<string, int> { ["fabricated"] = 1 }
        };
        using var forged = await Send();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, forged.StatusCode);
    }

    private sealed class ConstructModificationTestAuthority : INativeConstructHostModificationAuthorityProvider
    {
        public VerifiedConstructModificationAuthorization? Stored { get; set; }
        public Task<VerifiedConstructModificationAuthorization?> VerifyAsync(Guid actorId,
            NativeOriginApiEndpoints.VerifiedConstructModificationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(actorId == Guid.Parse("d66a7a7b-bc0f-4677-829d-0ccf40470d2f") ? Stored : null);
    }

    [Fact]
    public async Task VerifiedConstructModificationReusesNativeConstructionBeyondStartingAllowance()
    {
        var provider = new ConstructModificationTestAuthority();
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true,
            realNativeContent: true, dm: true, modificationAuthority: provider);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var (construction, _) = ContextualConstruct(catalog, "construct-power-source.mechanical-engine");
        var profile = construction.Origin.Profile!;
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        string? reference = null;
        while (folder is not null)
        {
            var candidate = Path.Combine(folder.FullName, "data", "reference",
                "construct-small-starting-build-20261121.json");
            if (File.Exists(candidate)) { reference = candidate; break; }
            folder = folder.Parent;
        }
        Assert.NotNull(reference);
        using var data = JsonDocument.Parse(File.ReadAllText(reference!));
        var initial = data.RootElement.GetProperty("assembly").Deserialize<NativeConstructAssemblyRequest>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        // Add independently acquired passive armor at distinct regions. Its
        // material provenance is not an automatic defense grant.
        var additions = Enumerable.Range(0, 3).Select(i => new NativeConstructComponentInstance(
            "plating-" + i, "construct-component.protective-plating",
            new Dictionary<string, string> { ["plating"] = "construct-material.steel" },
            ProtectedRegionId: "hull-" + i)).ToArray();
        var target = initial with
        {
            InitialConstruction = false, InitialAllowance = null,
            Components = [.. initial.Components, .. additions]
        };
        var targetProposal = construction.Origin.EvaluatedProposal with
        {
            Id = "modified-construct", Candidates = []
        };
        var characterId = Guid.NewGuid();
        const string endpoint = "/api/mechanics/v1/origins/construct/modification/plan";
        async Task<HttpResponseMessage> Send(long inventoryRevision = 7, string eventId = "installation-1")
        {
            using var request = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new { characterId, originRevision = 1, operatingRevision = 2,
                    inventoryRevision, rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint, eventId }));
            return await fixture.Client.SendAsync(request);
        }
        using var missing = await Send();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        provider.Stored = new(characterId, 1, 2, 7, catalog.RulesetId, catalog.Fingerprint,
            "installation-1", construction, profile, target, targetProposal);
        using var stale = await Send(inventoryRevision: 6);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, stale.StatusCode);
        using var invalidEvent = await Send(eventId: "unverified");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidEvent.StatusCode);
        using var planned = await Send();
        var raw = await planned.Content.ReadAsStringAsync();
        Assert.True(planned.StatusCode == HttpStatusCode.OK, raw);
        using var result = JsonDocument.Parse(raw);
        var root = result.RootElement;
        Assert.False(root.GetProperty("committed").GetBoolean());
        var plan = root.GetProperty("plan");
        Assert.True(plan.GetProperty("canApply").GetBoolean());
        Assert.Equal(3, plan.GetProperty("addedInstanceIds").GetArrayLength());
        var built = plan.GetProperty("target");
        Assert.True(built.GetProperty("assembly").GetProperty("knownConstructionCost").GetInt64() > 200);
        var next = built.GetProperty("origin").GetProperty("profile");
        Assert.Equal(profile.Id, next.GetProperty("id").GetString());
        Assert.Equal(profile.History.Count + 1, next.GetProperty("history").GetArrayLength());
        Assert.Equal(profile.History.Count, provider.Stored.ExistingProfile.History.Count);
        Assert.Equal(profile.Underlying.InherentGrants.Count,
            next.GetProperty("underlying").GetProperty("inherentGrants").GetArrayLength());
        // The public construction service still refuses post-creation grant issuance.
        Assert.False(new NativeConstructOriginConstructionService(catalog)
            .Evaluate(new(new(targetProposal), target)).IsMechanicallyAuthorized);
    }

    [Fact]
    public async Task VerifiedConstructSourceReplacementRequiresExactLedgerMigration()
    {
        var provider = new ConstructModificationTestAuthority();
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true,
            realNativeContent: true, dm: true, modificationAuthority: provider);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var (construction, _) = ContextualConstruct(catalog, "construct-power-source.mechanical-engine");
        var profile = construction.Origin.Profile!;
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        string? reference = null;
        while (folder is not null)
        {
            var candidate = Path.Combine(folder.FullName, "data", "reference",
                "construct-small-starting-build-20261121.json");
            if (File.Exists(candidate)) { reference = candidate; break; }
            folder = folder.Parent;
        }
        Assert.NotNull(reference);
        using var data = JsonDocument.Parse(File.ReadAllText(reference!));
        var initial = data.RootElement.GetProperty("assembly").Deserialize<NativeConstructAssemblyRequest>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var target = initial with
        {
            InitialConstruction = false, InitialAllowance = null,
            Components = initial.Components.Select(p => p.InstanceId == "generator"
                ? p with { PowerSourceId = "construct-power-source.solar-crystal" } : p).ToArray()
        };
        var proposal = construction.Origin.EvaluatedProposal with { Id = "solar-replacement", Candidates = [] };
        var characterId = Guid.NewGuid();
        const string endpoint = "/api/mechanics/v1/origins/construct/modification/plan";
        async Task<HttpResponseMessage> Send(long? resourceRevision = 8)
        {
            using var request = fixture.Authenticated(HttpMethod.Post, endpoint,
                JsonContent.Create(new { characterId, originRevision = 1, operatingRevision = 2,
                    inventoryRevision = 7, resourceRevision, rulesetId = catalog.RulesetId,
                    fingerprint = catalog.Fingerprint, eventId = "replacement-1" }));
            return await fixture.Client.SendAsync(request);
        }
        provider.Stored = new(characterId, 1, 2, 7, catalog.RulesetId, catalog.Fingerprint,
            "replacement-1", construction, profile, target, proposal);
        using var missing = await Send(null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        var before = new NativeConstructSourceLedgerState(
            new Dictionary<string, NativeConstructSourceState>
            {
                ["generator"] = new("construct-power-source.mechanical-engine")
            }, [], 4);
        var after = new NativeConstructSourceLedgerState(
            new Dictionary<string, NativeConstructSourceState>
            {
                ["generator"] = new("construct-power-source.solar-crystal", 120, 0, 0)
            }, [], 4);
        provider.Stored = provider.Stored with { ResourceRevision = 8, SourceMigration = new(before, after) };
        using var stale = await Send(7);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, stale.StatusCode);
        using var planned = await Send();
        var raw = await planned.Content.ReadAsStringAsync();
        Assert.True(planned.StatusCode == HttpStatusCode.OK, raw);
        using var result = JsonDocument.Parse(raw);
        var plan = result.RootElement.GetProperty("plan");
        var migration = plan.GetProperty("sourceMigration");
        Assert.Equal("generator", Assert.Single(migration.GetProperty("retiredSourceInstanceIds").EnumerateArray()).GetString());
        Assert.Equal("generator", Assert.Single(migration.GetProperty("incomingSourceInstanceIds").EnumerateArray()).GetString());
        Assert.Equal(120L, migration.GetProperty("after").GetProperty("sources").GetProperty("generator")
            .GetProperty("storedSolarPowerUnitMinutes").GetInt64());
        Assert.Equal(4, migration.GetProperty("after").GetProperty("consecutiveDaysWithoutFullMechanicalRation").GetInt32());
        Assert.False(result.RootElement.GetProperty("committed").GetBoolean());
        provider.Stored = provider.Stored with
        {
            SourceMigration = new(before, after with { ConsecutiveDaysWithoutFullMechanicalRation = 0 })
        };
        using var reset = await Send();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reset.StatusCode);
        provider.Stored = provider.Stored with
        {
            SourceMigration = new(before, after with
            {
                Sources = new Dictionary<string, NativeConstructSourceState>
                {
                    ["generator"] = new("construct-power-source.solar-crystal", 99999, 0, 0)
                }
            })
        };
        using var overCapacity = await Send();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, overCapacity.StatusCode);
    }

    private sealed class CharacterTrainingTestAuthority : INativeCharacterHostTrainingAuthorityProvider
    {
        public VerifiedCharacterTrainingAuthorization? Stored { get; set; }
        public Task<VerifiedCharacterTrainingAuthorization?> VerifyAsync(Guid actorId,
            NativeCharacterTrainingEndpoints.Request request, CancellationToken cancellationToken) =>
            Task.FromResult(actorId == SupernaturalActor ? Stored : null);
    }

    [Fact]
    public async Task VerifiedCharacterTrainingCombinesLifeAndIndependentSources()
    {
        var provider = new CharacterTrainingTestAuthority();
        var lifeAuthority = new VerifiedLifeExperienceTestAuthority();
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true,
            realNativeContent: true, dm: true, trainingAuthority: provider);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var proposal = new NativeLifeExperienceProposal("upbringing.elf.wood", "background.entertainer",
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["upbringing.elf.general/cultural-training"] = ["skill.survival"],
                ["background.entertainer/entertainer-performance-specialty"] = ["skill.acrobatics"],
                ["background.entertainer/entertainer-performance-method"] = ["disguise-kit"]
            }, new Dictionary<string, string> { ["background.entertainer/item/0"] = "fixture.disguise-kit" });
        var life = await lifeAuthority.VerifyAsync(SupernaturalActor,
            new(SupernaturalCharacter, 1, catalog.RulesetId, catalog.Fingerprint, proposal), default);
        Assert.NotNull(life);
        var primary = new NativeCharacterRequest(catalog.RulesetId, catalog.Fingerprint,
            new Dictionary<string, int> { ["strength"] = 10, ["dexterity"] = 10, ["constitution"] = 10,
                ["intelligence"] = 10, ["wisdom"] = 10, ["charisma"] = 10 },
            [new("class.rogue", 1, "class-1")]);
        const string endpoint = "/api/mechanics/v1/characters/training/resolve";
        async Task<HttpResponseMessage> Send(long revision = 2)
        {
            using var request = fixture.Authenticated(HttpMethod.Post, endpoint, JsonContent.Create(new
            {
                characterId = SupernaturalCharacter, originRevision = 1, characterRevision = revision,
                rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint
            }));
            return await fixture.Client.SendAsync(request);
        }
        using var missing = await Send();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        provider.Stored = new(SupernaturalCharacter, 1, 2, primary, proposal, life!.Authorization,
            [new("class.rogue", "class-skill-1", "skill.survival")],
            [new("class.rogue", "class-competency-1", "competency.disguise", Proficient: true)],
            new Dictionary<string, string>(), new Dictionary<string, string>(),
            NativeCompetencyDuplicateProficiencyPolicy.AddRank);
        using var stale = await Send(1);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, stale.StatusCode);
        using var pending = await Send();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, pending.StatusCode);
        var pendingBody = await pending.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(pendingBody.GetProperty("result").GetProperty("status").GetString() == "needs-choices",
            pendingBody.GetRawText());
        provider.Stored = provider.Stored with
        {
            SkillAlternatives = new Dictionary<string, string> { ["class-skill-1"] = "skill.stealth" }
        };
        using var completed = await Send();
        var raw = await completed.Content.ReadAsStringAsync();
        Assert.True(completed.StatusCode == HttpStatusCode.OK, raw);
        using var json = JsonDocument.Parse(raw);
        var result = json.RootElement.GetProperty("result");
        Assert.Equal("skill.stealth", Assert.Single(result.GetProperty("skillReplacements")
            .EnumerateArray()).GetProperty("replacementSkillId").GetString());
        var disguise = result.GetProperty("competencies").GetProperty("training").GetProperty("competency.disguise");
        Assert.True(disguise.GetProperty("proficient").GetBoolean());
        Assert.Equal(1, disguise.GetProperty("ranks").GetInt32());
        Assert.False(json.RootElement.GetProperty("committed").GetBoolean());
        provider.Stored = provider.Stored with
        {
            SkillAlternatives = new Dictionary<string, string> { ["class-skill-1"] = "skill.survival" }
        };
        using var duplicate = await Send();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.StatusCode);
    }

    private sealed class CriminalTrainingContext : INativeLifeExperienceContextAuthority
    {
        public bool AuthorizesChoice(NativeLifeExperienceSource source,
            NativeLifeExperienceChoice choice, string optionId) => false;
        public NativeLifeExperienceItemProof? VerifyItem(NativeLifeExperienceSource source,
            NativeLifeExperienceItemIntent intent) =>
            source.SourceId == "background.criminal" ?
                new("fixture." + intent.Key.Replace('/', '.'), null, true, true) : null;
    }

    [Fact]
    public async Task VerifiedCharacterEquipmentReconcilesCriminalAndRogueToolDuplicate()
    {
        var provider = new CharacterTrainingTestAuthority();
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet", ordinary: true,
            realNativeContent: true, dm: true, trainingAuthority: provider);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>().Snapshot!;
        var proposal = new NativeLifeExperienceProposal("upbringing.elf.wood", "background.criminal",
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["upbringing.elf.general/cultural-training"] = ["skill.survival"],
                ["background.criminal/equipment/criminal-gaming-set"] = ["equipment.tool.dice-set"]
            });
        var authorization = new NativeLifeExperienceAuthorization(new Dictionary<string, string>
        {
            ["upbringing.elf.general"] = "verified-parent",
            ["upbringing.elf.wood"] = "verified-upbringing",
            ["background.criminal"] = "verified-background"
        }, new CriminalTrainingContext());
        var primary = new NativeCharacterRequest(catalog.RulesetId, catalog.Fingerprint,
            new Dictionary<string, int> { ["strength"] = 10, ["dexterity"] = 10, ["constitution"] = 10,
                ["intelligence"] = 10, ["wisdom"] = 10, ["charisma"] = 10 },
            [new("class.rogue", 1, "class-1")]);
        provider.Stored = new(SupernaturalCharacter, 1, 2, primary, proposal, authorization, [], [],
            new Dictionary<string, string>(), new Dictionary<string, string>(),
            NativeCompetencyDuplicateProficiencyPolicy.AddRank,
            new(catalog.RulesetId, catalog.Fingerprint, [new("class.rogue", "class-1")]));
        const string endpoint = "/api/mechanics/v1/characters/training/resolve";
        async Task<HttpResponseMessage> Send()
        {
            using var request = fixture.Authenticated(HttpMethod.Post, endpoint, JsonContent.Create(new
            {
                characterId = SupernaturalCharacter, originRevision = 1, characterRevision = 2,
                rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint
            }));
            return await fixture.Client.SendAsync(request);
        }
        using var pending = await Send();
        var raw = await pending.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, pending.StatusCode);
        using var pendingJson = JsonDocument.Parse(raw);
        Assert.True(pendingJson.RootElement.TryGetProperty("equipment", out var discovery), raw);
        Assert.Equal("needs-choices", discovery.GetProperty("status").GetString());
        var choice = Assert.Single(discovery.GetProperty("requiredChoices").EnumerateArray());
        Assert.Equal("equipment.tool.thieves-tools", choice.GetProperty("originalQualificationId").GetString());
        Assert.DoesNotContain(choice.GetProperty("eligibleQualificationIds").EnumerateArray(),
            x => x.GetString() == "equipment.tool.dice-set");
        var provenance = new NativeEquipmentQualificationProvenance("class.rogue", "class-1", "tool-thieves-tools");
        provider.Stored = provider.Stored with
        {
            EquipmentAlternatives = [new(provenance, "equipment.tool.thieves-tools", "equipment.tool.playing-card-set")]
        };
        using var completed = await Send();
        raw = await completed.Content.ReadAsStringAsync();
        Assert.True(completed.StatusCode == HttpStatusCode.OK, raw);
        using var json = JsonDocument.Parse(raw);
        var result = json.RootElement.GetProperty("equipment");
        var qualifications = result.GetProperty("equipment").GetProperty("qualifications").EnumerateArray().ToArray();
        Assert.Single(qualifications, q => q.GetProperty("qualificationId").GetString() == "equipment.tool.thieves-tools");
        Assert.Single(qualifications, q => q.GetProperty("qualificationId").GetString() == "equipment.tool.playing-card-set");
        var shortbow = Assert.Single(qualifications, q => q.GetProperty("qualificationId").GetString() == "equipment.weapon.shortbow");
        Assert.Equal(2, shortbow.GetProperty("provenance").GetArrayLength());
        provider.Stored = provider.Stored with
        {
            EquipmentAlternatives = [new(provenance, "equipment.tool.thieves-tools", "equipment.tool.dice-set")]
        };
        using var covered = await Send();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, covered.StatusCode);
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
            x => x.Identity.MechanicalDefinitionId == "trait.trance");
        var endurance = Assert.Single(offers,
            x => x.Identity.MechanicalDefinitionId ==
                "trait.relentless-endurance");
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
    public async Task ApprovedMixedHttpRejectsMalformedNestedInputsBeforeNativeEvaluation()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync("PublicOnly", "character-sheet",
            ordinary: true, realNativeContent: true, dm: true);
        var catalog = fixture.App.Services.GetRequiredService<NativeCatalogStore>()
            .Snapshot!;
        const string actor = "d66a7a7b-bc0f-4677-829d-0ccf40470d2f";
        const string campaign = "99d41ce8-36fc-43df-b8cd-bb1829b20982";
        const string approval = "approved:malformed-origin-denied";
        var proposal = new NativeOriginProposal("malformed-scoped-mixed",
            NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [
                new("elf", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.elf")),
                new("orc", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.orc"))
            ], [], [],
            TableDecisions: [
                new NativeTableRuleDecision("dm-approve", new(actor, approval),
                    new("campaign", campaign), new("source", "species.orc"),
                    JsonSerializer.SerializeToElement(new { eligibility = "eligible" }))
            ]);

        var malformed = new NativeOriginProposal[]
        {
            proposal with { Manifestations = [
                new NativeOriginManifestation("null-candidate", null!, Choices: [])] },
            proposal with { Manifestations = [
                new NativeOriginManifestation("null-requirement", "offered",
                    Choices: [new NativeOriginChoiceSelection(null!,
                        ["skill.intimidation"])])] },
            proposal with { Contributors = [
                proposal.Contributors[0] with { Role = null! },
                proposal.Contributors[1]
            ] },
            proposal with { Contributors = [
                proposal.Contributors[0],
                proposal.Contributors[1] with {
                    Source = new(NativeOriginSourceKind.Native, null!)
                }
            ] }
        };
        foreach (var invalid in malformed)
        {
            using var request = fixture.Authenticated(HttpMethod.Post,
                "/api/mechanics/v1/origins/evaluate-approved-mixed",
                JsonContent.Create(new
                {
                    rulesetId = catalog.RulesetId,
                    fingerprint = catalog.Fingerprint,
                    characterId = "44eb66e6-3b5c-44e2-aa71-4986d4b346c0",
                    campaignId = campaign,
                    approvalEvidenceId = approval,
                    proposal = invalid
                }));
            using var response = await fixture.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("invalid_approved_mixed_proposal",
                body.GetProperty("error").GetString());
            Assert.False(body.TryGetProperty("result", out _));
            Assert.False(body.TryGetProperty("restSurvival", out _));
        }
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
        foreach (var category in new[] { "species", "ancestry", "skill", "competency", "monster", "item", "feat", "spell",
            "origin-trait", "construct-component", "construct-power-source", "construct-material",
            "construct-design", "construct-enhancement", "upbringing", "background" })
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
