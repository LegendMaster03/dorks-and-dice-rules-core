using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class PcGenSkillConversionIntegrationTests
{
    private static readonly (string Source, string Target)[] DirectSkillConversions =
    [
        ("Bluff", "Deception"),
        ("Diplomacy", "Persuasion"),
        ("Handle Animal", "Animal Handling"),
        ("Heal", "Medicine"),
        ("Intimidate", "Intimidation"),
        ("Knowledge (arcana)", "Arcana"),
        ("Knowledge (history)", "History"),
        ("Knowledge (nature)", "Nature"),
        ("Knowledge (religion)", "Religion"),
        ("Sense Motive", "Insight"),
        ("Sleight of Hand", "Sleight of Hand"),
        ("Survival", "Survival")
    ];

    private static readonly (string Source, string Target)[] DirectToolConversions =
    [
        ("Craft (alchemy)", "Alchemist's Supplies"),
        ("Forgery", "Forgery Kit")
    ];

    private static readonly string[] ExcludedDirectConversions =
    [
        "Perform (dance)",
        "Balance",
        "Hide",
        "Move Silently",
        "Listen",
        "Spot",
        "Ride",
        "Spellcraft",
        "Craft (blacksmithing)"
    ];

    [Fact]
    public async Task ApprovedDirectConversionsAreExplicitAndSourceIdentityRemainsNative()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<RulesCoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new RulesCoreDbContext(options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..12];
        var package35 = $"pcgen-skill-conversions-35-{token}";
        var package30 = $"pcgen-skill-conversions-30-{token}";
        var importer = new NormalizedSourceImportService(db);

        try
        {
            await importer.ImportAsync(new ImportNormalizedSourceRequest(
                package35,
                $"PCGen 3.5 skill conversion fixture {token}",
                "integration-test",
                "test-only",
                false,
                Fixture(token, "3.5e", "35", Build35SkillNames())));
            await importer.ImportAsync(new ImportNormalizedSourceRequest(
                package30,
                $"PCGen 3.0 skill conversion fixture {token}",
                "integration-test",
                "test-only",
                false,
                Fixture(token, "3e", "30", ["Pick Pocket", "Wilderness Lore", "Alchemy"])));

            foreach (var (source, target) in DirectSkillConversions)
            {
                var row = await ReadSkillAsync(db, package35, source);
                Assert.Equal("skill", row.EntityType);
                Assert.Contains(source, row.RawJson, StringComparison.Ordinal);
                AssertConversion(row.ContentJson, source, target, "skill", "direct-equivalence", scope: null);
            }

            foreach (var (source, target) in DirectToolConversions)
            {
                var row = await ReadSkillAsync(db, package35, source);
                Assert.Equal("skill", row.EntityType);
                Assert.Contains(source, row.RawJson, StringComparison.Ordinal);
                AssertConversion(row.ContentJson, source, target, "tool", "direct-cross-type", scope: null);
            }

            var openLock = await ReadSkillAsync(db, package35, "Open Lock");
            using (var document = JsonDocument.Parse(openLock.ContentJson))
            {
                var root = document.RootElement;
                Assert.Equal("Open Lock", root.GetProperty("name").GetString());
                var conversion = root.GetProperty("_rulesCore").GetProperty("competencyConversion");
                Assert.Equal("direct-cross-type", conversion.GetProperty("relationship").GetString());
                Assert.Equal("skill", conversion.GetProperty("sourceType").GetString());
                Assert.Equal("Open Lock", conversion.GetProperty("sourceName").GetString());
                Assert.Equal("tool", conversion.GetProperty("targetType").GetString());
                Assert.Equal("Thieves' Tools", conversion.GetProperty("targetName").GetString());
                Assert.Equal("open-lock", conversion.GetProperty("scope").GetString());
                Assert.True(conversion.GetProperty("mechanicalNamePreserved").GetBoolean());
            }

            foreach (var source in ExcludedDirectConversions)
            {
                var row = await ReadSkillAsync(db, package35, source);
                AssertNoConversion(row.ContentJson, source);
            }

            foreach (var source in new[] { "Pick Pocket", "Wilderness Lore", "Alchemy" })
            {
                var row = await ReadSkillAsync(db, package35, source);
                AssertNoConversion(row.ContentJson, source);
            }

            AssertConversion(
                (await ReadSkillAsync(db, package30, "Pick Pocket")).ContentJson,
                "Pick Pocket",
                "Sleight of Hand",
                "skill",
                "direct-equivalence",
                scope: null);
            AssertConversion(
                (await ReadSkillAsync(db, package30, "Wilderness Lore")).ContentJson,
                "Wilderness Lore",
                "Survival",
                "skill",
                "direct-equivalence",
                scope: null);
            AssertConversion(
                (await ReadSkillAsync(db, package30, "Alchemy")).ContentJson,
                "Alchemy",
                "Alchemist's Supplies",
                "tool",
                "direct-cross-type",
                scope: null);

            var bluff = await db.SourceEntityRevisions
                .AsNoTracking()
                .Include(value => value.SourceEntity)
                    .ThenInclude(value => value.SourcePackage)
                .SingleAsync(value =>
                    value.SourceEntity.SourcePackage.Key == package35
                    && value.SourceEntity.Name == "Bluff");
            using var mechanical = JsonDocument.Parse(bluff.GetMechanicalContentJson());
            var mechanicalExtension = mechanical.RootElement.GetProperty("_rulesCore");
            Assert.False(mechanicalExtension.TryGetProperty("context", out _));
            Assert.Equal(
                "Deception",
                mechanicalExtension.GetProperty("competencyConversion").GetProperty("targetName").GetString());
        }
        finally
        {
            await DeletePackageAsync(db, package35);
            await DeletePackageAsync(db, package30);
        }
    }

    private static IReadOnlyList<string> Build35SkillNames() =>
        DirectSkillConversions.Select(value => value.Source)
            .Concat(DirectToolConversions.Select(value => value.Source))
            .Concat(["Open Lock", "Pick Pocket", "Wilderness Lore", "Alchemy"])
            .Concat(ExcludedDirectConversions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static NormalizedSourceRepresentation Fixture(
        string token,
        string edition,
        string suffix,
        IReadOnlyList<string> names)
    {
        var editionPath = edition == "3e" ? "3e" : "35e";
        var shortCode = $"SK{suffix}{token}";
        var lines = new List<string>
        {
            $"SOURCELONG:PCGen {edition} Skill Fixture {token}\tSOURCESHORT:{shortCode}"
        };
        lines.AddRange(names.Select(name => $"{name}\tKEYSTAT:INT"));
        var path = $"data/{editionPath}/example/example_skills.lst";
        var artifact = new SourceRepresentationArtifact(
            $"skills-{suffix}-{token}.lst",
            Encoding.UTF8.GetBytes(string.Join('\n', lines)),
            $"test:pcgen-skill-conversions:{suffix}:{token}#{path}");
        var representation = new PcGenSourceFormatAdapter().TryRead(artifact);
        Assert.NotNull(representation);
        Assert.Equal(names.Count, representation!.Records.Count);
        Assert.All(representation.Records, record => Assert.Equal("skill", record.EntityType));
        Assert.Equal(edition, Assert.Single(representation.Publications!).GameEdition);
        return representation;
    }

    private static async Task<SkillRow> ReadSkillAsync(
        RulesCoreDbContext db,
        string packageKey,
        string sourceName)
    {
        var revision = await db.SourceEntityRevisions
            .AsNoTracking()
            .Include(value => value.SourceEntity)
                .ThenInclude(value => value.SourcePackage)
            .SingleAsync(value =>
                value.SourceEntity.SourcePackage.Key == packageKey
                && value.SourceEntity.Name == sourceName);
        Assert.NotNull(revision.ContentJson);
        return new SkillRow(
            revision.SourceEntity.EntityType,
            revision.RawJson,
            revision.ContentJson!);
    }

    private static void AssertConversion(
        string contentJson,
        string sourceName,
        string targetName,
        string targetType,
        string relationship,
        string? scope)
    {
        using var document = JsonDocument.Parse(contentJson);
        var root = document.RootElement;
        Assert.Equal(targetName, root.GetProperty("name").GetString());
        var extension = root.GetProperty("_rulesCore");
        var context = extension.GetProperty("context");
        Assert.Equal(sourceName, context.GetProperty("nativeName").GetString());
        Assert.True(context.TryGetProperty("edition", out _));
        var conversion = extension.GetProperty("competencyConversion");
        Assert.Equal(relationship, conversion.GetProperty("relationship").GetString());
        Assert.Equal("skill", conversion.GetProperty("sourceType").GetString());
        Assert.Equal(sourceName, conversion.GetProperty("sourceName").GetString());
        Assert.Equal(targetType, conversion.GetProperty("targetType").GetString());
        Assert.Equal(targetName, conversion.GetProperty("targetName").GetString());
        if (scope is null)
        {
            Assert.False(conversion.TryGetProperty("scope", out _));
        }
        else
        {
            Assert.Equal(scope, conversion.GetProperty("scope").GetString());
        }
    }

    private static void AssertNoConversion(string contentJson, string sourceName)
    {
        using var document = JsonDocument.Parse(contentJson);
        var root = document.RootElement;
        Assert.Equal(sourceName, root.GetProperty("name").GetString());
        var extension = root.GetProperty("_rulesCore");
        Assert.False(extension.TryGetProperty("competencyConversion", out _));
        Assert.False(extension.GetProperty("context").TryGetProperty("nativeName", out _));
    }

    private static async Task DeletePackageAsync(RulesCoreDbContext db, string packageKey)
    {
        var package = await db.SourcePackages.SingleOrDefaultAsync(value => value.Key == packageKey);
        if (package is null) return;
        db.SourcePackages.Remove(package);
        await db.SaveChangesAsync();
    }

    private sealed record SkillRow(string EntityType, string RawJson, string ContentJson);
}
