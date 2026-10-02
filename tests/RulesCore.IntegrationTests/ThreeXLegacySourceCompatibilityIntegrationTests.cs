using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class ThreeXLegacySourceCompatibilityIntegrationTests
{
    [Fact]
    public async Task ReviewedSrdUnicodeMinusRacialAdjustmentsNormalizeToSignedAbilityBonuses()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var json = JsonSerializer.Serialize(new
            {
                race = new[]
                {
                    new
                    {
                        name = "Dwarf",
                        source = "SRD35",
                        uniqueId = "race-dwarf-unicode-minus",
                        documentUri = "https://example.invalid/races/dwarf",
                        body = "- +2 Constitution, –2 Charisma.\n- Medium: As Medium creatures, dwarves have no special bonuses or penalties due to their size.\n- Dwarf base land speed is 20 feet."
                    }
                }
            });
            var representation = new LegacySrdSourceFormatAdapter().TryRead(
                new SourceRepresentationArtifact(
                    "srd-3-5e.json",
                    Encoding.UTF8.GetBytes(json),
                    $"test:three-x-legacy-unicode:{token}"));
            Assert.NotNull(representation);

            var result = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    $"three-x-legacy-unicode-{token}",
                    "3.x legacy Unicode fixture",
                    "integration-test",
                    "test-only",
                    false,
                    representation!));
            var imported = Assert.Single(result.Entities);
            var contentJson = await db.SourceEntityRevisions
                .AsNoTracking()
                .Where(value => value.SourceEntityId == imported.EntityId)
                .OrderByDescending(value => value.RevisionNumber)
                .Select(value => value.ContentJson)
                .FirstAsync();
            Assert.False(string.IsNullOrWhiteSpace(contentJson));

            using var content = JsonDocument.Parse(contentJson!);
            var ability = Assert.Single(content.RootElement.GetProperty("ability").EnumerateArray());
            Assert.Equal(2, ability.GetProperty("con").GetInt32());
            Assert.Equal(-2, ability.GetProperty("cha").GetInt32());
            Assert.Equal("M", Assert.Single(content.RootElement.GetProperty("size").EnumerateArray()).GetString());
            Assert.Equal(20, content.RootElement.GetProperty("speed").GetInt32());
        }
    }

    [Fact]
    public async Task ReviewedThreeEHtmlSpellFieldsReplayThroughBulkNormalization()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            const string body = """
                <p>Conjuration (Creation) [Acid]</p>
                <p>Level: Sor/Wiz 2</p>
                <p>Components: V, S, M</p>
                <p>Casting Time: 1 standard action</p>
                <p>Range: Long</p>
                <p>Duration: Instantaneous</p>
                <p>Saving Throw: None</p>
                <p>Spell Resistance: No</p>
                """;
            var json = JsonSerializer.Serialize(new
            {
                spell = new[]
                {
                    new
                    {
                        name = "Acid Arrow",
                        source = "SRD3",
                        uniqueId = "spell-acid-arrow-html",
                        documentUri = "https://example.invalid/spellsa.htm#acid-arrow",
                        body
                    }
                }
            });
            var representation = new LegacySrdSourceFormatAdapter().TryRead(
                new SourceRepresentationArtifact(
                    "srd-3e.json",
                    Encoding.UTF8.GetBytes(json),
                    $"test:three-x-legacy-html:{token}"));
            Assert.NotNull(representation);

            var result = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    $"three-x-legacy-html-{token}",
                    "3.x legacy HTML fixture",
                    "integration-test",
                    "test-only",
                    false,
                    representation!));
            var imported = Assert.Single(result.Entities);
            var contentJson = await db.SourceEntityRevisions
                .AsNoTracking()
                .Where(value => value.SourceEntityId == imported.EntityId)
                .OrderByDescending(value => value.RevisionNumber)
                .Select(value => value.ContentJson)
                .FirstAsync();
            Assert.False(string.IsNullOrWhiteSpace(contentJson));

            using var content = JsonDocument.Parse(contentJson!);
            Assert.Equal(2, content.RootElement.GetProperty("level").GetInt32());
            Assert.Equal("C", content.RootElement.GetProperty("school").GetString());
            Assert.True(content.RootElement.GetProperty("components").GetProperty("v").GetBoolean());
            Assert.True(content.RootElement.GetProperty("components").GetProperty("s").GetBoolean());
            Assert.Equal(
                "instant",
                Assert.Single(content.RootElement.GetProperty("duration").EnumerateArray())
                    .GetProperty("type").GetString());

            var threeX = content.RootElement.GetProperty("_rulesCore").GetProperty("threeX");
            Assert.Equal("1 standard action", threeX.GetProperty("castingTime").GetString());
            Assert.Equal("Long", threeX.GetProperty("range").GetString());
            Assert.Equal("None", threeX.GetProperty("savingThrow").GetString());
            Assert.Equal("No", threeX.GetProperty("spellResistance").GetString());
        }
    }

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
