using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class ThreeXBulkTranslationIntegrationTests
{
    [Fact]
    public async Task LegacyStraightforwardFamiliesProjectIntoSharedContentFields()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var json = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["spell"] = new[]
                {
                    LegacyRecord(
                        "Acid Arrow",
                        "spell-acid-arrow",
                        "Conjuration (Creation) [Acid]\n**Level:** Sor/Wiz 2\n**Components:** V, S, M\n**Casting Time:** 1 standard action\n**Range:** Long\n**Duration:** Instantaneous\n**Saving Throw:** None\n**Spell Resistance:** No")
                },
                ["power"] = new[]
                {
                    LegacyRecord(
                        "Adapt Body",
                        "power-adapt-body",
                        "Psychometabolism\n**Level:** Psion/wilder 5\n**Manifesting Time:** 1 standard action\n**Range:** Personal\n**Duration:** 1 hour/level\n**Power Points:** 9")
                },
                ["race"] = new[]
                {
                    LegacyRecord(
                        "Dwarf",
                        "race-dwarf",
                        "+2 Constitution, -2 Charisma.\n**Size:** Medium\nBase land speed is 20 feet.\n**Favored Class:** Fighter")
                },
                ["item"] = new[]
                {
                    LegacyRecord(
                        "Boots of Elvenkind",
                        "item-boots",
                        "**Weight:** 1 lb.\n**Price:** 2,500 gp.\n**Caster Level:** 5th")
                },
                ["feat"] = new[]
                {
                    LegacyRecord(
                        "Armor Skin",
                        "feat-armor-skin",
                        "**Prerequisite:** Character level 21st.\n**Benefit:** Gain a +1 natural armor bonus.",
                        "Armor Skin [Epic]")
                },
                ["domain"] = new[]
                {
                    LegacyRecord(
                        "Sun Domain",
                        "domain-sun",
                        "**Granted Power:** Greater turning once per day.")
                },
                ["class"] = new[]
                {
                    LegacyRecord(
                        "Ranger",
                        "class-ranger",
                        "**Alignment:** Any.\n**Hit Die:** d8.\n**Skill Points at Each Additional Level:** 4 + Int modifier.\n**Class Skills:** Climb, Heal, Hide, Survival")
                }
            });

            var representation = new LegacySrdSourceFormatAdapter().TryRead(
                new SourceRepresentationArtifact(
                    "srd-3-5e.json",
                    Encoding.UTF8.GetBytes(json),
                    $"test:three-x-bulk-legacy:{token}"));
            Assert.NotNull(representation);

            var result = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    $"three-x-bulk-legacy-{token}",
                    "3.x bulk legacy fixture",
                    "integration-test",
                    "test-only",
                    false,
                    representation!));

            Assert.Equal(7, result.Entities.Count);

            using var spell = await ReadLatestContentAsync(db, result, "spell");
            Assert.Equal(2, spell.RootElement.GetProperty("level").GetInt32());
            Assert.Equal("C", spell.RootElement.GetProperty("school").GetString());
            Assert.True(spell.RootElement.GetProperty("components").GetProperty("v").GetBoolean());
            Assert.True(spell.RootElement.GetProperty("components").GetProperty("s").GetBoolean());
            Assert.Equal(
                "instant",
                Assert.Single(spell.RootElement.GetProperty("duration").EnumerateArray())
                    .GetProperty("type").GetString());
            var spellThreeX = spell.RootElement.GetProperty("_rulesCore").GetProperty("threeX");
            Assert.Equal("Sor/Wiz 2", spellThreeX.GetProperty("spellLevels").GetString());
            Assert.Equal("Long", spellThreeX.GetProperty("range").GetString());

            using var power = await ReadLatestContentAsync(db, result, "power");
            Assert.Equal(5, power.RootElement.GetProperty("level").GetInt32());
            var powerThreeX = power.RootElement.GetProperty("_rulesCore").GetProperty("threeX");
            Assert.Equal("Psychometabolism", powerThreeX.GetProperty("discipline").GetString());
            Assert.Equal(9, powerThreeX.GetProperty("powerPoints").GetInt32());
            Assert.Equal("1 standard action", powerThreeX.GetProperty("manifestingTime").GetString());

            using var race = await ReadLatestContentAsync(db, result, "race");
            var racialAbility = Assert.Single(race.RootElement.GetProperty("ability").EnumerateArray());
            Assert.Equal(2, racialAbility.GetProperty("con").GetInt32());
            Assert.Equal(-2, racialAbility.GetProperty("cha").GetInt32());
            Assert.Equal("M", Assert.Single(race.RootElement.GetProperty("size").EnumerateArray()).GetString());
            Assert.Equal(20, race.RootElement.GetProperty("speed").GetInt32());

            using var item = await ReadLatestContentAsync(db, result, "item");
            Assert.Equal(1m, item.RootElement.GetProperty("weight").GetDecimal());
            Assert.Equal(250000, item.RootElement.GetProperty("value").GetInt64());

            using var feat = await ReadLatestContentAsync(db, result, "feat");
            Assert.Equal("Epic", feat.RootElement.GetProperty("category").GetString());
            Assert.Equal(
                "Gain a +1 natural armor bonus.",
                feat.RootElement.GetProperty("_rulesCore").GetProperty("threeX").GetProperty("benefit").GetString());

            using var domain = await ReadLatestContentAsync(db, result, "domain");
            Assert.Equal(
                "Greater turning once per day.",
                domain.RootElement.GetProperty("_rulesCore").GetProperty("threeX").GetProperty("grantedPower").GetString());

            using var characterClass = await ReadLatestContentAsync(db, result, "class");
            Assert.Equal(1, characterClass.RootElement.GetProperty("hd").GetProperty("number").GetInt32());
            Assert.Equal(8, characterClass.RootElement.GetProperty("hd").GetProperty("faces").GetInt32());
            var character = characterClass.RootElement.GetProperty("_rulesCore").GetProperty("character");
            Assert.Equal(4, character.GetProperty("skillPointsPerLevel").GetInt32());
            Assert.Contains(
                "Survival",
                character.GetProperty("classSkills").EnumerateArray().Select(value => value.GetString()));
        }
    }

    [Fact]
    public async Task PcGenSupplementalFamiliesReceiveStructuredSemanticFields()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var root = $"data/35e/example/{token}";
            var adapter = new PcGenSourceFormatAdapter();
            var representations = adapter.TryReadMany(
            [
                Artifact(
                    $"{root}/fixture.pcc",
                    """
                    CAMPAIGN:3.5 Bulk Fixture
                    GAMEMODE:35e
                    SOURCELONG:3.5 Bulk Fixture
                    SOURCESHORT:BULK
                    DEITY:deities.lst
                    DOMAIN:domains.lst
                    LANGUAGE:languages.lst
                    TEMPLATE:templates.lst
                    WEAPONPROF:weaponprofs.lst
                    """),
                Artifact(
                    $"{root}/deities.lst",
                    "Pelor\tDOMAINS:Good,Healing,Strength,Sun\tDEITYWEAP:Mace\tALIGN:NG\tFACT:Title|The Shining One\tFACT:Symbol|Sun disk\tFACTSET:Pantheon|Greyhawk"),
                Artifact(
                    $"{root}/domains.lst",
                    "Sun\tDESC:Sun domain\tCSKILL:Knowledge (religion)\tSPELLS:Searing Light"),
                Artifact(
                    $"{root}/languages.lst",
                    "Draconic\tTYPE:Exotic\tSCRIPT:Draconic"),
                Artifact(
                    $"{root}/templates.lst",
                    "Celestial\tCR:1\tLEVELADJUSTMENT:2\tRACETYPE:Outsider"),
                Artifact(
                    $"{root}/weaponprofs.lst",
                    "Longsword\tTYPE:Martial")
            ]);

            var packageKey = $"three-x-bulk-pcgen-{token}";
            var importer = new NormalizedSourceImportService(db);
            var importedIds = new List<Guid>();
            foreach (var representation in representations.Where(value => value.Records.Any(record =>
                         !record.EntityType.StartsWith("pcgen-", StringComparison.OrdinalIgnoreCase))))
            {
                var result = await importer.ImportAsync(
                    new ImportNormalizedSourceRequest(
                        packageKey,
                        "3.x bulk PCGen fixture",
                        "integration-test",
                        "test-only",
                        false,
                        representation));
                importedIds.AddRange(result.Entities.Select(value => value.EntityId));
            }

            using var deity = await ReadLatestContentAsync(db, importedIds, "deity");
            Assert.Equal(
                new[] { "N", "G" },
                deity.RootElement.GetProperty("alignment").EnumerateArray().Select(value => value.GetString()).ToArray());
            Assert.Contains("Healing", deity.RootElement.GetProperty("domains").EnumerateArray().Select(value => value.GetString()));
            Assert.Equal("Greyhawk", deity.RootElement.GetProperty("pantheon").GetString());
            Assert.Equal("The Shining One", deity.RootElement.GetProperty("title").GetString());
            Assert.Equal("Sun disk", deity.RootElement.GetProperty("symbol").GetString());
            Assert.Equal(
                "Mace",
                deity.RootElement.GetProperty("_rulesCore").GetProperty("threeX").GetProperty("favoredWeapon").GetString());

            using var language = await ReadLatestContentAsync(db, importedIds, "language");
            Assert.Equal("exotic", language.RootElement.GetProperty("type").GetString());
            Assert.Equal("Draconic", language.RootElement.GetProperty("script").GetString());

            using var domain = await ReadLatestContentAsync(db, importedIds, "domain");
            var domainThreeX = domain.RootElement.GetProperty("_rulesCore").GetProperty("threeX");
            Assert.Equal("Searing Light", domainThreeX.GetProperty("spellAccess").GetString());
            Assert.Contains(
                "Knowledge (religion)",
                domainThreeX.GetProperty("classSkills").EnumerateArray().Select(value => value.GetString()));

            using var template = await ReadLatestContentAsync(db, importedIds, "template");
            var templateThreeX = template.RootElement.GetProperty("_rulesCore").GetProperty("threeX");
            Assert.Equal("1", templateThreeX.GetProperty("challengeRatingAdjustment").GetString());
            Assert.Equal("2", templateThreeX.GetProperty("levelAdjustment").GetString());
            Assert.Equal("Outsider", templateThreeX.GetProperty("creatureTypeAdjustment").GetString());

            using var proficiency = await ReadLatestContentAsync(db, importedIds, "weapon-proficiency");
            var proficiencyThreeX = proficiency.RootElement.GetProperty("_rulesCore").GetProperty("threeX");
            Assert.Equal("weapon", proficiencyThreeX.GetProperty("proficiencyKind").GetString());
            Assert.Equal("Martial", proficiencyThreeX.GetProperty("proficiencyType").GetString());
        }
    }

    [Fact]
    public void SourceBodyIsProvenanceNotRuleBearingContent()
    {
        const string content = """
            {
              "name":"Fixture",
              "source":"SRD35",
              "_rulesCore":{
                "context":{"edition":"3.5e","sourceFormat":"legacy-srd-snapshot"},
                "threeX":{"powerPoints":9,"sourceBody":"Exact source text"}
              }
            }
            """;

        using var mechanical = JsonDocument.Parse(RulesMechanicalContent.ForRules(content));
        var extension = mechanical.RootElement.GetProperty("_rulesCore");
        Assert.False(extension.TryGetProperty("context", out _));
        var threeX = extension.GetProperty("threeX");
        Assert.Equal(9, threeX.GetProperty("powerPoints").GetInt32());
        Assert.False(threeX.TryGetProperty("sourceBody", out _));
    }

    private static object LegacyRecord(
        string name,
        string uniqueId,
        string body,
        string? originalHeading = null) =>
        new
        {
            name,
            source = "SRD35",
            uniqueId,
            documentUri = $"https://example.invalid/{uniqueId}",
            body,
            originalHeading
        };

    private static async Task<JsonDocument> ReadLatestContentAsync(
        RulesCoreDbContext db,
        NormalizedSourceImportResult result,
        string entityType) =>
        await ReadLatestContentAsync(db, result.Entities.Select(value => value.EntityId), entityType);

    private static async Task<JsonDocument> ReadLatestContentAsync(
        RulesCoreDbContext db,
        IEnumerable<Guid> entityIds,
        string entityType)
    {
        var ids = entityIds.Distinct().ToArray();
        var entityId = await db.SourceEntities
            .AsNoTracking()
            .Where(value => ids.Contains(value.Id) && value.EntityType == entityType)
            .Select(value => value.Id)
            .SingleAsync();
        var json = await db.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => value.SourceEntityId == entityId)
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => value.ContentJson)
            .FirstAsync();
        Assert.False(string.IsNullOrWhiteSpace(json));
        return JsonDocument.Parse(json!);
    }

    private static SourceRepresentationArtifact Artifact(string path, string text) =>
        new(
            Path.GetFileName(path),
            Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal)),
            $"test:three-x-bulk#{path}",
            SourceUri: $"https://example.invalid/{path}",
            MediaType: "text/plain");

    private static async Task<RulesCoreDbContext?> OpenDatabaseAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        return db;
    }
}
