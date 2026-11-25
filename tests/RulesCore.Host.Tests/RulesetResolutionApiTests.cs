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
        Assert.Equal(350, firstPage.GetProperty("total").GetInt32());
        Assert.Equal(350, secondPage.GetProperty("total").GetInt32());
        Assert.Empty(firstPage.GetProperty("rules").EnumerateArray().Select(x => x.GetProperty("id").GetString())
            .Intersect(secondPage.GetProperty("rules").EnumerateArray().Select(x => x.GetProperty("id").GetString())));
        var seahorse = await client.GetFromJsonAsync<JsonElement>(animals + "/monster.seahorse");
        Assert.Equal(0, seahorse.GetProperty("rule").GetProperty("definition")
            .GetProperty("challenge").GetProperty("xp").GetInt32());
        var owl = await client.GetFromJsonAsync<JsonElement>(animals + "/monster.giant-owl");
        Assert.Equal("celestial", owl.GetProperty("rule").GetProperty("definition").GetProperty("type").GetString());

        var oldMonsters = await client.GetFromJsonAsync<JsonElement>("/api/rulesets/dnd-5e/rules?category=monster&limit=1");
        Assert.Equal(331, oldMonsters.GetProperty("total").GetInt32());
        var dragon = await client.GetFromJsonAsync<JsonElement>(animals + "/monster.adult-black-dragon");
        var dragonDefinition = dragon.GetProperty("rule").GetProperty("definition");
        Assert.Contains("Acid Arrow|SRD52", dragonDefinition.GetProperty("spellcasting").GetRawText());
        Assert.DoesNotContain("Melf", dragonDefinition.GetRawText());
        var spirit = await client.GetFromJsonAsync<JsonElement>(animals + "/monster.draconic-spirit");
        Assert.Equal("Summon Dragon|SRD52", spirit.GetProperty("rule").GetProperty("definition")
            .GetProperty("summonedBySpell").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(
            "/api/rulesets/dnd-5e/rules/monster.draconic-spirit")).StatusCode);

        // Concrete equipment, generic magic templates and weapon rules remain
        // distinct and edition-scoped; recovered embedded stat blocks are accessible.
        // Legacy published disease variant and source-listed equipment configurations.
        var diseasedRat = await client.GetFromJsonAsync<JsonElement>("/api/rulesets/dnd-5e/rules/monster.giant-rat-diseased");
        var ratDefinition = diseasedRat.GetProperty("rule").GetProperty("definition");
        Assert.Equal("1/8", ratDefinition.GetProperty("cr").GetString());
        Assert.Contains("contract a disease", ratDefinition.GetProperty("action").GetRawText());
        var bardingCases = new[]
        {
            ("chain-mail", "chain-mail", 16),
            ("leather", "leather-armor", 12),
            ("plate", "plate-armor", 18),
            ("ring-mail", "ring-mail", 14),
            ("scale-mail", "scale-mail", 15),
            ("splint", "splint-armor", 17),
            ("studded-leather", "studded-leather-armor", 13),
        };
        foreach (var (variant, armor, ac) in bardingCases)
        {
            var id = "monster.warhorse-" + variant + "-barding";
            var horse = await client.GetFromJsonAsync<JsonElement>("/api/rulesets/dnd-5e/rules/" + id);
            var definition = horse.GetProperty("rule").GetProperty("definition");
            Assert.Equal("monster.warhorse", definition.GetProperty("variantOf").GetString());
            Assert.Equal(ac, definition.GetProperty("ac")[0].GetProperty("ac").GetInt32());
            Assert.Equal("baseitem." + armor, definition.GetProperty("barding").GetProperty("baseArmorId").GetString());
            Assert.Equal(4, definition.GetProperty("barding").GetProperty("costMultiplier").GetInt32());
            Assert.Equal(2, definition.GetProperty("barding").GetProperty("weightMultiplier").GetInt32());
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(animals + "/" + id)).StatusCode);
        }

        // All finite summon choices preserve their parent and edition, not a fixed cast level.
        var summonCases = new[]
        {
            ("animated-object-huge", "animated-object"),
            ("animated-object-large", "animated-object"),
            ("animated-object-medium-or-smaller", "animated-object"),
            ("draconic-spirit-acid", "draconic-spirit"),
            ("draconic-spirit-cold", "draconic-spirit"),
            ("draconic-spirit-fire", "draconic-spirit"),
            ("draconic-spirit-lightning", "draconic-spirit"),
            ("draconic-spirit-poison", "draconic-spirit"),
            ("giant-insect-centipede", "giant-insect"),
            ("giant-insect-spider", "giant-insect"),
            ("giant-insect-wasp", "giant-insect"),
            ("otherworldly-steed-celestial", "otherworldly-steed"),
            ("otherworldly-steed-fey", "otherworldly-steed"),
            ("otherworldly-steed-fiend", "otherworldly-steed"),
        };
        foreach (var (variant, parent) in summonCases)
        {
            var summoned = await client.GetFromJsonAsync<JsonElement>(animals + "/monster." + variant);
            var definition = summoned.GetProperty("rule").GetProperty("definition");
            Assert.Equal("monster." + parent, definition.GetProperty("variantOf").GetString());
            Assert.Contains("monster." + parent, definition.GetProperty("catalogReferences").GetRawText());
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/rulesets/dnd-5e/rules/monster." + variant)).StatusCode);
        }
        var mediumAnimated = await client.GetFromJsonAsync<JsonElement>(animals + "/monster.animated-object-medium-or-smaller");
        var mediumDefinition = mediumAnimated.GetProperty("rule").GetProperty("definition");
        Assert.Equal(3, mediumDefinition.GetProperty("size").GetArrayLength());
        Assert.Equal(5, mediumDefinition.GetProperty("conditionImmune").GetArrayLength());
        Assert.DoesNotContain("your spellcasting ability modifier", mediumDefinition.GetProperty("action").GetRawText());
        var wasp = await client.GetFromJsonAsync<JsonElement>(animals + "/monster.giant-insect-wasp");
        var waspDefinition = wasp.GetProperty("rule").GetProperty("definition");
        Assert.Equal(40, waspDefinition.GetProperty("speed").GetProperty("fly").GetInt32());
        Assert.False(waspDefinition.TryGetProperty("bonus", out _));
        Assert.DoesNotContain("Web Bolt", waspDefinition.GetProperty("action").GetRawText());
        var fireSpirit = await client.GetFromJsonAsync<JsonElement>(animals + "/monster.draconic-spirit-fire");
        var fireDefinition = fireSpirit.GetProperty("rule").GetProperty("definition");
        Assert.Equal(5, fireDefinition.GetProperty("resist").GetArrayLength());
        Assert.Equal("fire", fireDefinition.GetProperty("variantSelection").GetProperty("sharedResistance").GetString());

        var modernItems = await client.GetFromJsonAsync<JsonElement>(animals + "?category=item&limit=1");
        Assert.Equal(630, modernItems.GetProperty("total").GetInt32());
        // Concrete numbered items retain the original edition-local equipment and template.
        foreach (var prefix in new[] { "/api/rulesets/dnd-5e/rules", animals })
        {
            var blade = await client.GetFromJsonAsync<JsonElement>(prefix + "/item.1-longsword");
            var bladeDefinition = blade.GetProperty("rule").GetProperty("definition");
            Assert.Equal("baseitem.longsword", bladeDefinition.GetProperty("baseItemId").GetString());
            Assert.Equal("magic-variant.1-weapon", bladeDefinition.GetProperty("variantOf").GetString());
            Assert.Equal("+1", bladeDefinition.GetProperty("bonusWeapon").GetString());
            Assert.Equal("1d8", bladeDefinition.GetProperty("dmg1").GetString());
            Assert.DoesNotContain("{=bonusWeapon}", bladeDefinition.GetProperty("entries").GetRawText());
            Assert.Equal(prefix == animals, bladeDefinition.TryGetProperty("mastery", out _));
            var plate = await client.GetFromJsonAsync<JsonElement>(prefix + "/item.3-plate-armor");
            var plateDefinition = plate.GetProperty("rule").GetProperty("definition");
            Assert.Equal(18, plateDefinition.GetProperty("ac").GetInt32());
            Assert.Equal("+3", plateDefinition.GetProperty("bonusAc").GetString());
            Assert.Equal("legendary", plateDefinition.GetProperty("rarity").GetString());
            var shield = await client.GetFromJsonAsync<JsonElement>(prefix + "/item.2-shield");
            var shieldDefinition = shield.GetProperty("rule").GetProperty("definition");
            Assert.Equal(2, shieldDefinition.GetProperty("ac").GetInt32());
            Assert.Equal("+2", shieldDefinition.GetProperty("bonusAc").GetString());
            var arrow = await client.GetFromJsonAsync<JsonElement>(prefix + "/item.1-arrow");
            var arrowDefinition = arrow.GetProperty("rule").GetProperty("definition");
            Assert.Equal(prefix == animals, arrowDefinition.GetProperty("entries").GetRawText().Contains("equivalent in value to a potion", StringComparison.Ordinal));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(prefix + "/item.1-arrows-20")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(prefix + "/item.1-net")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(animals + "/item.1-musket")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/rulesets/dnd-5e/rules/item.1-musket")).StatusCode);
        var modernWeapon = await client.GetFromJsonAsync<JsonElement>(animals + "/baseitem.musket");
        Assert.Equal("Slow|SRD52", modernWeapon.GetProperty("rule").GetProperty("definition")
            .GetProperty("mastery")[0].GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(
            "/api/rulesets/dnd-5e/rules/baseitem.musket")).StatusCode);
        var cube = await client.GetFromJsonAsync<JsonElement>(animals + "/item.cube-of-force");
        var cubeDefinition = cube.GetProperty("rule").GetProperty("definition");
        Assert.Contains("tiny hut|srd52", cubeDefinition.GetProperty("attachedSpells").GetRawText());
        Assert.DoesNotContain("leomund", cubeDefinition.GetProperty("attachedSpells").GetRawText());
        var ethereal = await client.GetFromJsonAsync<JsonElement>(animals + "/item.plate-armor-of-etherealness");
        Assert.Equal(2, ethereal.GetProperty("rule").GetProperty("definition").GetProperty("eligibleArmor").GetArrayLength());
        var fly = await client.GetFromJsonAsync<JsonElement>(animals + "/monster.giant-fly");
        Assert.Equal(0, fly.GetProperty("rule").GetProperty("definition").GetProperty("challenge").GetProperty("xp").GetInt32());
        var oldAssassin = await client.GetFromJsonAsync<JsonElement>(
            "/api/rulesets/dnd-5e/rules/monster.assassin");
        Assert.Contains("baseitem.studded-leather-armor", oldAssassin.GetProperty("rule").GetProperty("definition").GetRawText());

        // Character creation categories and edition-specific payloads stay isolated.
        const string oldRules = "/api/rulesets/dnd-5e/rules";
        var oldRaces = await client.GetFromJsonAsync<JsonElement>(oldRules + "?category=race&limit=1");
        var modernSpecies = await client.GetFromJsonAsync<JsonElement>(animals + "?category=species&limit=1");
        Assert.Equal(9, oldRaces.GetProperty("total").GetInt32());
        Assert.Equal(9, modernSpecies.GetProperty("total").GetInt32());
        var oldHuman = await client.GetFromJsonAsync<JsonElement>(oldRules + "/race.human");
        Assert.Equal(1, oldHuman.GetProperty("rule").GetProperty("definition").GetProperty("ability")[0].GetProperty("str").GetInt32());
        var modernHuman = await client.GetFromJsonAsync<JsonElement>(animals + "/species.human");
        Assert.False(modernHuman.GetProperty("rule").GetProperty("definition").TryGetProperty("ability", out _));
        Assert.True(modernHuman.GetProperty("rule").GetProperty("definition").TryGetProperty("feats", out _));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(oldRules + "/species.human")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(animals + "/race.human")).StatusCode);
        var hill = await client.GetFromJsonAsync<JsonElement>(oldRules + "/subrace.dwarf.hill");
        Assert.Contains("race.dwarf", hill.GetProperty("rule").GetProperty("definition").GetProperty("catalogReferences").GetRawText());
        var oldBackgrounds = await client.GetFromJsonAsync<JsonElement>(oldRules + "?category=background&limit=1");
        var modernBackgrounds = await client.GetFromJsonAsync<JsonElement>(animals + "?category=background&limit=1");
        Assert.Equal(1, oldBackgrounds.GetProperty("total").GetInt32());
        Assert.Equal(4, modernBackgrounds.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/rulesets/fate-condensed/rules/species.human")).StatusCode);

        // Class progression links identify the edition-local feature at its acquisition level.
        foreach (var id in ids.Take(2))
        {
            var prefix = "/api/rulesets/" + id + "/rules";
            var classes = await client.GetFromJsonAsync<JsonElement>(prefix + "?category=class&limit=1");
            var subclasses = await client.GetFromJsonAsync<JsonElement>(prefix + "?category=subclass&limit=1");
            Assert.Equal(12, classes.GetProperty("total").GetInt32());
            Assert.Equal(12, subclasses.GetProperty("total").GetInt32());
            var warlock = await client.GetFromJsonAsync<JsonElement>(prefix + "/class.warlock");
            Assert.Contains("class-feature.warlock.pact-magic.1", warlock.GetProperty("rule").GetProperty("definition").GetProperty("catalogReferences").GetRawText());
            var pact = await client.GetFromJsonAsync<JsonElement>(prefix + "/class-feature.warlock.pact-magic.1");
            Assert.Equal(1, pact.GetProperty("rule").GetProperty("definition").GetProperty("level").GetInt32());
            if (id == "dnd-5-5e")
                Assert.DoesNotContain("Witch Bolt", pact.GetProperty("rule").GetProperty("definition").GetProperty("entries").GetRawText());
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/rulesets/fate-condensed/rules/class.warlock")).StatusCode);

        // Legacy optional features and modern feats must retain their edition-specific semantics.
        // Legacy publisher corrections must be visible through the public catalog,
        // including the structured grant choices and their descriptive tables.
        var oldPact = await client.GetFromJsonAsync<JsonElement>(oldRules + "/class-feature.warlock.pact-magic.1");
        var oldPactEntries = oldPact.GetProperty("rule").GetProperty("definition").GetProperty("entries").GetRawText();
        Assert.Contains("{@spell thunderwave}", oldPactEntries);
        Assert.DoesNotContain("witch bolt", oldPactEntries);
        var oldLand = await client.GetFromJsonAsync<JsonElement>(oldRules + "/subclass.druid.land");
        var oldLandGrants = oldLand.GetProperty("rule").GetProperty("definition").GetProperty("additionalSpells");
        Assert.Equal(7, oldLandGrants.GetArrayLength());
        Assert.DoesNotContain("Underdark", oldLandGrants.GetRawText());
        Assert.DoesNotContain("Melf", oldLandGrants.GetRawText());
        var oldLandTable = await client.GetFromJsonAsync<JsonElement>(oldRules + "/subclass-feature.druid.land.circle-spells.2");
        Assert.DoesNotContain("Underdark", oldLandTable.GetProperty("rule").GetProperty("definition").GetProperty("entries").GetRawText());
        var oldTwinned = await client.GetFromJsonAsync<JsonElement>(oldRules + "/optional-feature.twinned-spell");
        Assert.DoesNotContain("chromatic orb", oldTwinned.GetProperty("rule").GetProperty("definition").GetProperty("entries").GetRawText());
        var oldDeities = await client.GetFromJsonAsync<JsonElement>(oldRules + "?category=deity&limit=1");
        Assert.Equal(67, oldDeities.GetProperty("total").GetInt32());
        var anubis = await client.GetFromJsonAsync<JsonElement>(oldRules + "/deity.egyptian.anubis");
        Assert.Equal("Death", Assert.Single(anubis.GetProperty("rule").GetProperty("definition").GetProperty("domains").EnumerateArray()).GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(animals + "/deity.egyptian.anubis")).StatusCode);

        var legacyFeats = await client.GetFromJsonAsync<JsonElement>(oldRules + "?category=feat&limit=1");
        var modernFeats = await client.GetFromJsonAsync<JsonElement>(animals + "?category=feat&limit=1");
        Assert.Equal(1, legacyFeats.GetProperty("total").GetInt32());
        Assert.Equal(17, modernFeats.GetProperty("total").GetInt32());
        var magicInitiate = await client.GetFromJsonAsync<JsonElement>(animals + "/feat.magic-initiate");
        var magicDefinition = magicInitiate.GetProperty("rule").GetProperty("definition");
        Assert.Equal("O", magicDefinition.GetProperty("category").GetString());
        Assert.Equal(3, magicDefinition.GetProperty("additionalSpells").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(oldRules + "/feat.magic-initiate")).StatusCode);
        var modernChain = await client.GetFromJsonAsync<JsonElement>(animals + "/optional-feature.pact-of-the-chain");
        var chain = modernChain.GetProperty("rule").GetProperty("definition");
        Assert.DoesNotContain("Slaad Tadpole", chain.GetProperty("entries").GetRawText());
        Assert.DoesNotContain("appendix B", chain.GetProperty("entries").GetRawText());
        Assert.Equal(JsonValueKind.Array, chain.GetProperty("additionalSpells")[0].GetProperty("innate").GetProperty("_").ValueKind);
        var oldChain = await client.GetFromJsonAsync<JsonElement>(oldRules + "/optional-feature.pact-of-the-chain");
        Assert.True(oldChain.GetProperty("rule").GetProperty("definition").GetProperty("additionalSpells")[0].GetProperty("innate").GetProperty("_").TryGetProperty("ritual", out _));

        // Supplementary actions, glossary and contagions preserve edition-specific categories.
        var oldCoreActions = await client.GetFromJsonAsync<JsonElement>(oldRules + "?category=action&limit=1");
        var newCoreActions = await client.GetFromJsonAsync<JsonElement>(animals + "?category=action&limit=1");
        Assert.Equal(10, oldCoreActions.GetProperty("total").GetInt32());
        Assert.Equal(12, newCoreActions.GetProperty("total").GetInt32());
        var glossary = await client.GetFromJsonAsync<JsonElement>(animals + "?category=glossary&limit=1");
        Assert.Equal(114, glossary.GetProperty("total").GetInt32());
        var grapple = await client.GetFromJsonAsync<JsonElement>(oldRules + "/attack-option.grapple");
        var grappleDefinition = grapple.GetProperty("rule").GetProperty("definition");
        Assert.True(grappleDefinition.GetProperty("replacesAttack").GetBoolean());
        Assert.False(grappleDefinition.TryGetProperty("time", out _));
        Assert.Contains("action.attack", grappleDefinition.GetProperty("catalogReferences").GetRawText());
        var modernShield = await client.GetFromJsonAsync<JsonElement>(animals + "/equipment-option.don-or-doff-a-shield");
        Assert.Equal("action.utilize", modernShield.GetProperty("rule").GetProperty("definition").GetProperty("actionId").GetString());
        var madness = await client.GetFromJsonAsync<JsonElement>(oldRules + "/rule.madness");
        Assert.DoesNotContain("Sanity", madness.GetProperty("rule").GetProperty("definition").GetProperty("entries").GetRawText());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(oldRules + "/disease.cackle-fever")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(animals + "/disease.cackle-fever")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(animals + "/magical-contagion.cackle-fever")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(oldRules + "/magical-contagion.cackle-fever")).StatusCode);

        // Recover missing publisher group identities without duplicating the concrete focus items.
        foreach (var (focus, formCount) in new[] { ("arcane-focus", 5), ("druidic-focus", 4), ("holy-symbol", 3) })
        {
            var group = await client.GetFromJsonAsync<JsonElement>(oldRules + "/item-group." + focus);
            Assert.Equal(formCount, group.GetProperty("rule").GetProperty("definition").GetProperty("items").GetArrayLength());
        }
        var mastery = await client.GetFromJsonAsync<JsonElement>(animals + "/rule.weapon-mastery-properties");
        Assert.Equal(8, mastery.GetProperty("rule").GetProperty("definition").GetProperty("properties").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(oldRules + "/rule.weapon-mastery-properties")).StatusCode);

        // A vehicle profile belongs to its existing magic item, not a second item identity.
        foreach (var prefix in new[] { oldRules, animals })
        {
            var crab = await client.GetFromJsonAsync<JsonElement>(prefix + "/item.apparatus-of-the-crab");
            var crabDefinition = crab.GetProperty("rule").GetProperty("definition");
            var profile = crabDefinition.GetProperty("vehicleProfile");
            Assert.Equal(20, profile.GetProperty("ac").GetInt32());
            Assert.Equal(200, profile.GetProperty("hp").GetInt32());
            Assert.Equal(1, profile.GetProperty("capCrew").GetInt32());
            Assert.Equal(1, profile.GetProperty("capPassenger").GetInt32());
            var restriction = profile.GetProperty("speed").GetProperty("swim").GetProperty("condition").GetString()!;
            Assert.Equal(prefix == oldRules, restriction.Contains("and tail", StringComparison.Ordinal));
            Assert.DoesNotContain("Kwalish", crabDefinition.GetProperty("entries").GetRawText());
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(prefix + "/vehicle.apparatus-of-the-crab")).StatusCode);
        }

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
