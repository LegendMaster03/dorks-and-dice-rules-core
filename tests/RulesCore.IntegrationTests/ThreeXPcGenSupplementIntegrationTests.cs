using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class ThreeXPcGenSupplementIntegrationTests
{
    [Fact]
    public async Task PcGenRaceProjectsDarkvisionAndPreservesChassisMechanics()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var root = $"data/35e/example/{token}";
            var representations = new PcGenSourceFormatAdapter().TryReadMany(
            [
                Artifact(
                    $"{root}/fixture.pcc",
                    """
                    CAMPAIGN:3.5 Race Supplement Fixture
                    GAMEMODE:35e
                    SOURCELONG:3.5 Race Supplement Fixture
                    SOURCESHORT:RACEFIX
                    RACE:races.lst
                    """),
                Artifact(
                    $"{root}/races.lst",
                    "Goblin\tFAVCLASS:Rogue\tSTARTFEATS:1\tSIZE:S\tMOVE:Walk,30\tUNENCUMBEREDMOVE:HeavyLoad|HeavyArmor\tFACE:5\tREACH:5\tLANGBONUS:Common\tBONUS:STAT|STR|-2\tBONUS:STAT|DEX|2\tBONUS:VAR|DarkvisionRange|60|TYPE=Base\tBONUS:SAVE|Fortitude,Reflex,Will|1|TYPE=Racial\tAUTO:LANG|Goblin\tLEGS:2\tHANDS:2\tDEFINESTAT:MINVALUE|INT|3\tRACETYPE:Humanoid\tRACESUBTYPE:Goblinoid\tTYPE:Humanoid.PC.Base\tTEMPLATE:CHOOSE:Normal Goblin|Goblin Cavalry\tXTRASKILLPTSPERLVL:1\tCR:1/2")
            ]);
            var representation = Assert.Single(
                representations,
                value => string.Equals(value.Artifact.FileName, "races.lst", StringComparison.Ordinal));

            var result = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    $"three-x-race-supplement-{token}",
                    "3.x race supplement fixture",
                    "integration-test",
                    "test-only",
                    false,
                    representation));
            var imported = Assert.Single(result.Entities);
            var json = await db.SourceEntityRevisions
                .AsNoTracking()
                .Where(value => value.SourceEntityId == imported.EntityId)
                .OrderByDescending(value => value.RevisionNumber)
                .Select(value => value.ContentJson)
                .FirstAsync();
            Assert.False(string.IsNullOrWhiteSpace(json));

            using var document = JsonDocument.Parse(json!);
            var rootElement = document.RootElement;
            Assert.Equal(60, rootElement.GetProperty("darkvision").GetInt32());
            Assert.Equal(30, rootElement.GetProperty("speed").GetInt32());
            var ability = Assert.Single(rootElement.GetProperty("ability").EnumerateArray());
            Assert.Equal(-2, ability.GetProperty("str").GetInt32());
            Assert.Equal(2, ability.GetProperty("dex").GetInt32());

            var threeX = rootElement.GetProperty("_rulesCore").GetProperty("threeX");
            Assert.Equal("Rogue", threeX.GetProperty("favoredClass").GetString());
            Assert.Equal("Humanoid", threeX.GetProperty("raceType").GetString());
            Assert.Contains(
                "Goblinoid",
                threeX.GetProperty("raceSubtypes").EnumerateArray().Select(value => value.GetString()));
            Assert.Contains(
                "PC",
                threeX.GetProperty("pcgenTypes").EnumerateArray().Select(value => value.GetString()));
            Assert.Contains(
                "Goblin",
                threeX.GetProperty("automaticLanguages").EnumerateArray().Select(value => value.GetString()));
            Assert.Contains(
                "Common",
                threeX.GetProperty("bonusLanguages").EnumerateArray().Select(value => value.GetString()));
            Assert.Equal("1/2", threeX.GetProperty("racialChallengeRating").GetString());
            Assert.Equal(1, threeX.GetProperty("startingFeats").GetInt32());
            Assert.Equal(2, threeX.GetProperty("legs").GetInt32());
            Assert.Equal(2, threeX.GetProperty("hands").GetInt32());
            Assert.Equal(1, threeX.GetProperty("extraSkillPointsPerLevel").GetInt32());
            Assert.Equal(3, threeX.GetProperty("abilityMinimums").GetProperty("int").GetInt32());
            Assert.Contains(
                "HeavyArmor",
                threeX.GetProperty("unencumberedMovement").EnumerateArray().Select(value => value.GetString()));
            Assert.Contains(
                "Goblin Cavalry",
                threeX.GetProperty("templateChoices").EnumerateArray().Select(value => value.GetString()));
            var saveBonus = Assert.Single(threeX.GetProperty("racialSaveBonuses").EnumerateArray());
            Assert.Equal("1", saveBonus.GetProperty("value").GetString());
            Assert.Equal("Racial", saveBonus.GetProperty("type").GetString());
            Assert.False(rootElement.TryGetProperty("cr", out _));
            Assert.False(rootElement.TryGetProperty("type", out _));
        }
    }

    [Fact]
    public async Task PcGenSpellKeepsEditionSpecificCastingFieldsAlongsideSharedFields()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var root = $"data/35e/example/{token}";
            var representations = new PcGenSourceFormatAdapter().TryReadMany(
            [
                Artifact(
                    $"{root}/fixture.pcc",
                    """
                    CAMPAIGN:3.5 Spell Supplement Fixture
                    GAMEMODE:35e
                    SOURCELONG:3.5 Spell Supplement Fixture
                    SOURCESHORT:SPELLFIX
                    SPELL:spells.lst
                    """),
                Artifact(
                    $"{root}/spells.lst",
                    "Acid Arrow\tSCHOOL:Conjuration\tSUBSCHOOL:Creation\tDESCRIPTOR:Acid\tCLASSES:Sorcerer,Wizard=2\tCOMPS:V,S,M\tCASTTIME:1 standard action\tRANGE:Long\tTARGETAREA:One creature\tDURATION:1 round/level\tSAVEINFO:None\tSPELLRES:No")
            ]);
            var representation = Assert.Single(
                representations,
                value => string.Equals(value.Artifact.FileName, "spells.lst", StringComparison.Ordinal));

            var result = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    $"three-x-spell-supplement-{token}",
                    "3.x spell supplement fixture",
                    "integration-test",
                    "test-only",
                    false,
                    representation));
            var imported = Assert.Single(result.Entities);
            var json = await db.SourceEntityRevisions
                .AsNoTracking()
                .Where(value => value.SourceEntityId == imported.EntityId)
                .OrderByDescending(value => value.RevisionNumber)
                .Select(value => value.ContentJson)
                .FirstAsync();
            Assert.False(string.IsNullOrWhiteSpace(json));

            using var document = JsonDocument.Parse(json!);
            Assert.Equal(2, document.RootElement.GetProperty("level").GetInt32());
            Assert.Equal("C", document.RootElement.GetProperty("school").GetString());
            Assert.True(document.RootElement.GetProperty("components").GetProperty("v").GetBoolean());
            Assert.True(document.RootElement.GetProperty("components").GetProperty("s").GetBoolean());

            var threeX = document.RootElement.GetProperty("_rulesCore").GetProperty("threeX");
            Assert.Equal("V,S,M", threeX.GetProperty("components").GetString());
            Assert.Equal("1 standard action", threeX.GetProperty("castingTime").GetString());
            Assert.Equal("Long", threeX.GetProperty("range").GetString());
            Assert.Equal("One creature", threeX.GetProperty("targetArea").GetString());
            Assert.Equal("1 round/level", threeX.GetProperty("duration").GetString());
            Assert.Equal("None", threeX.GetProperty("savingThrow").GetString());
            Assert.Equal("No", threeX.GetProperty("spellResistance").GetString());
            Assert.Equal("Creation", threeX.GetProperty("subschool").GetString());
            Assert.Equal("Acid", threeX.GetProperty("descriptor").GetString());
        }
    }

    private static SourceRepresentationArtifact Artifact(string path, string text) =>
        new(
            Path.GetFileName(path),
            Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal)),
            $"test:three-x-pcgen-supplement#{path}",
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
