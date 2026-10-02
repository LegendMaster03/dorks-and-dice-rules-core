using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class ThreeXPcGenNormalizationCleanupIntegrationTests
{
    [Fact]
    public async Task TranslatedRaceSyntaxLeavesUntranslatedMechanicsOnlyInPcGenFallback()
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
                    CAMPAIGN:3.5 Cleanup Fixture
                    GAMEMODE:35e
                    SOURCELONG:3.5 Cleanup Fixture
                    SOURCESHORT:CLEAN
                    RACE:races.lst
                    """),
                Artifact(
                    $"{root}/races.lst",
                    "Goblin\tFAVCLASS:Rogue\tSIZE:S\tMOVE:Walk,30\tVISION:Darkvision (60')\tLANGBONUS:Common\tBONUS:STAT|DEX|2\tBONUS:SKILL|Move Silently|4|TYPE=Racial\tRACETYPE:Humanoid\tRACESUBTYPE:Goblinoid\tCR:1/2")
            ]);
            var representation = Assert.Single(
                representations,
                value => string.Equals(value.Artifact.FileName, "races.lst", StringComparison.Ordinal));

            var result = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    $"three-x-cleanup-{token}",
                    "3.x cleanup fixture",
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

            using var content = JsonDocument.Parse(json!);
            var unmapped = content.RootElement
                .GetProperty("_rulesCore")
                .GetProperty("pcgen")
                .GetProperty("unmappedSegments")
                .EnumerateArray()
                .ToArray();
            var only = Assert.Single(unmapped);
            Assert.Equal("BONUS", only.GetProperty("tag").GetString());
            Assert.Equal("SKILL|Move Silently|4|TYPE=Racial", only.GetProperty("value").GetString());
        }
    }

    [Fact]
    public async Task FullyTranslatedSupplementalFamilyDropsPcGenFallbackSyntax()
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
                    CAMPAIGN:3.5 Language Cleanup Fixture
                    GAMEMODE:35e
                    SOURCELONG:3.5 Language Cleanup Fixture
                    SOURCESHORT:LCLEAN
                    LANGUAGE:languages.lst
                    """),
                Artifact(
                    $"{root}/languages.lst",
                    "Draconic\tTYPE:Exotic\tSCRIPT:Draconic")
            ]);
            var representation = Assert.Single(
                representations,
                value => string.Equals(value.Artifact.FileName, "languages.lst", StringComparison.Ordinal));

            var result = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    $"three-x-language-cleanup-{token}",
                    "3.x language cleanup fixture",
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

            using var content = JsonDocument.Parse(json!);
            Assert.Equal("exotic", content.RootElement.GetProperty("type").GetString());
            Assert.Equal("Draconic", content.RootElement.GetProperty("script").GetString());
            var extension = content.RootElement.GetProperty("_rulesCore");
            Assert.False(extension.TryGetProperty("pcgen", out _));
        }
    }

    private static SourceRepresentationArtifact Artifact(string path, string text) =>
        new(
            Path.GetFileName(path),
            Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal)),
            $"test:three-x-cleanup#{path}",
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
