using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class ThreeXSourceNormalizationRegressionTests
{
    [Fact]
    public async Task PcGenRaceWithMonsterClassRemainsRaceAfterNormalizedImport()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var adapter = new PcGenSourceFormatAdapter();
            var campaignPath = "data/35e/wizards_of_the_coast/rsrd/rsrd.pcc";
            var racePath = "data/35e/wizards_of_the_coast/rsrd/monsters/rsrd_races.lst";
            var representations = adapter.TryReadMany(
            [
                Artifact(
                    campaignPath,
                    """
                    CAMPAIGN:3.5 RSRD
                    GAMEMODE:35e
                    PUBNAMELONG:Wizards of the Coast
                    SOURCELONG:Revised (v.3.5) System Reference Document
                    SOURCESHORT:RSRD
                    SOURCEDATE:2003-07
                    RACE:monsters/rsrd_races.lst
                    """),
                Artifact(
                    racePath,
                    """
                    Goblin	FAVCLASS:Rogue	STARTFEATS:1	SIZE:S	MOVE:Walk,30	BONUS:STAT|STR|-2	BONUS:STAT|DEX|2	BONUS:STAT|CHA|-2	MONSTERCLASS:Humanoid:1	RACETYPE:Humanoid	RACESUBTYPE:Goblinoid	TYPE:Humanoid	CR:1/2
                    """)
            ]);

            var representation = Assert.Single(
                representations,
                value => string.Equals(value.Artifact.FileName, "rsrd_races.lst", StringComparison.Ordinal));
            var native = Assert.Single(representation.Records);
            Assert.Equal("race", native.EntityType);

            var result = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    $"pcgen-race-normalization-{token}",
                    "PCGen 3.5 race normalization regression",
                    "PCGen",
                    License: null,
                    IsPublic: false,
                    representation));

            var imported = Assert.Single(result.Entities);
            Assert.Equal("race", imported.EntityType);

            var entity = await db.SourceEntities
                .AsNoTracking()
                .SingleAsync(value => value.Id == imported.EntityId);
            Assert.Equal("race", entity.EntityType);

            var revision = await db.SourceEntityRevisions
                .AsNoTracking()
                .Where(value => value.SourceEntityId == imported.EntityId)
                .OrderByDescending(value => value.RevisionNumber)
                .FirstAsync();
            Assert.NotNull(revision.ContentJson);
            using var document = JsonDocument.Parse(revision.ContentJson!);
            Assert.Equal("Goblin", document.RootElement.GetProperty("name").GetString());
            Assert.Equal(30, document.RootElement.GetProperty("speed").GetInt32());
            var ability = Assert.Single(document.RootElement.GetProperty("ability").EnumerateArray());
            Assert.Equal(-2, ability.GetProperty("str").GetInt32());
            Assert.Equal(2, ability.GetProperty("dex").GetInt32());
            Assert.Equal(-2, ability.GetProperty("cha").GetInt32());
            Assert.False(document.RootElement.TryGetProperty("cr", out _));
            Assert.False(document.RootElement.TryGetProperty("type", out _));

            var unmapped = document.RootElement
                .GetProperty("_rulesCore")
                .GetProperty("pcgen")
                .GetProperty("unmappedSegments")
                .EnumerateArray()
                .ToArray();
            Assert.Contains(unmapped, value =>
                string.Equals(value.GetProperty("tag").GetString(), "MONSTERCLASS", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(unmapped, value =>
                string.Equals(value.GetProperty("tag").GetString(), "RACETYPE", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(unmapped, value =>
                string.Equals(value.GetProperty("tag").GetString(), "CR", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task LegacySrdMonsterHtmlTableNormalizesStatsWithoutExposingRawMarkupAsDescription()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            const string body = """
                <table data-debug="no-caption" class="full-width-table"><tbody>
                <tr><td></td><td>Goblin, 1st-Level Warrior</td></tr>
                <tr><td></td><td>Small Humanoid (Goblinoid)</td></tr>
                <tr><th>Hit Dice:</th><td>1d8+1 (5 hp)</td></tr>
                <tr><th>Initiative:</th><td>+1</td></tr>
                <tr><th>Speed:</th><td>30 ft. (6 squares)</td></tr>
                <tr><th>Armor Class:</th><td>15 (+1 size, +1 Dex, +2 leather armor, +1 light shield), touch 12, flat-footed 14</td></tr>
                <tr><th>Base Attack/Grapple:</th><td>+1/-3</td></tr>
                <tr><th>Abilities:</th><td>Str 11, Dex 13, Con 12, Int 10, Wis 9, Cha 6</td></tr>
                <tr><th>Saves:</th><td>Fort +3, Ref +1, Will -1</td></tr>
                <tr><th>Challenge Rating:</th><td>1/3</td></tr>
                </tbody></table>
                A goblin stands 3 to 3-1/2 feet tall and weighs 40 to 45 pounds.
                ### Combat
                Goblins favor ambushes and overwhelming numbers.
                """;
            var json = JsonSerializer.Serialize(new
            {
                monster = new[]
                {
                    new
                    {
                        name = "Goblin",
                        source = "SRD35",
                        uniqueId = "monster-goblin",
                        documentUri = "https://example.invalid/srd35/monsters/goblin",
                        body
                    }
                }
            });
            var representation = new LegacySrdSourceFormatAdapter().TryRead(
                new SourceRepresentationArtifact(
                    "srd-3-5e.json",
                    Encoding.UTF8.GetBytes(json),
                    $"test:legacy-srd35-html-monster:{Guid.NewGuid():N}"));

            Assert.NotNull(representation);
            var token = Guid.NewGuid().ToString("N")[..12];
            var result = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    $"legacy-srd-html-normalization-{token}",
                    "Legacy SRD HTML normalization regression",
                    "Rules Core",
                    License: null,
                    IsPublic: false,
                    representation!));

            var imported = Assert.Single(result.Entities);
            Assert.Equal("monster", imported.EntityType);
            var revision = await db.SourceEntityRevisions
                .AsNoTracking()
                .Where(value => value.SourceEntityId == imported.EntityId)
                .OrderByDescending(value => value.RevisionNumber)
                .FirstAsync();
            Assert.NotNull(revision.ContentJson);

            using var document = JsonDocument.Parse(revision.ContentJson!);
            var root = document.RootElement;
            Assert.Equal("S", Assert.Single(root.GetProperty("size").EnumerateArray()).GetString());
            Assert.Equal("humanoid", root.GetProperty("type").GetString());
            Assert.Equal(30, root.GetProperty("speed").GetProperty("walk").GetInt32());
            Assert.Equal(15, Assert.Single(root.GetProperty("ac").EnumerateArray()).GetInt32());
            Assert.Equal(5, root.GetProperty("hp").GetProperty("average").GetInt32());
            Assert.Equal("1d8+1", root.GetProperty("hp").GetProperty("formula").GetString());
            Assert.Equal(11, root.GetProperty("str").GetInt32());
            Assert.Equal(13, root.GetProperty("dex").GetInt32());
            Assert.Equal(12, root.GetProperty("con").GetInt32());
            Assert.Equal(10, root.GetProperty("int").GetInt32());
            Assert.Equal(9, root.GetProperty("wis").GetInt32());
            Assert.Equal(6, root.GetProperty("cha").GetInt32());
            Assert.Equal("1/3", root.GetProperty("cr").GetString());

            var entries = root.GetProperty("entries").EnumerateArray()
                .Select(value => value.GetString() ?? string.Empty)
                .ToArray();
            var readable = string.Join("\n", entries);
            Assert.DoesNotContain("<table", readable, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<tr", readable, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("A goblin stands", readable, StringComparison.Ordinal);
            Assert.Contains("### Combat", readable, StringComparison.Ordinal);

            var threeX = root
                .GetProperty("_rulesCore")
                .GetProperty("threeX");
            Assert.Equal(1, threeX.GetProperty("baseAttackBonus").GetInt32());
            Assert.Equal(-3, threeX.GetProperty("grapple").GetInt32());
            Assert.Equal(3, threeX.GetProperty("saves").GetProperty("fortitude").GetInt32());
            Assert.Equal(1, threeX.GetProperty("saves").GetProperty("reflex").GetInt32());
            Assert.Equal(-1, threeX.GetProperty("saves").GetProperty("will").GetInt32());
            Assert.Contains("<table", threeX.GetProperty("sourceBody").GetString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private static SourceRepresentationArtifact Artifact(string path, string text) =>
        new(
            Path.GetFileName(path),
            Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal)),
            $"test:three-x-normalization#{path}",
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
