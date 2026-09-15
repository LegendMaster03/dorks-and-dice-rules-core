using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class PcGenSkillNormalizationIntegrationTests
{
    [Fact]
    public async Task ReviewedDirectConversionsNormalizeToLaterCompetenciesWithoutBroadeningScopedTools()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<RulesCoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new RulesCoreDbContext(options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..12];
        var package35 = $"pcgen-normalize-35-{token}";
        var package30 = $"pcgen-normalize-30-{token}";
        var userId = $"pcgen-normalizer-{token}";
        var importer = new NormalizedSourceImportService(db);

        try
        {
            var imported35 = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                package35,
                $"PCGen 3.5 normalization fixture {token}",
                "integration-test",
                "test-only",
                false,
                ParseFixture(token, "3.5e", "35", ["Bluff", "Sleight of Hand", "Craft (alchemy)", "Open Lock"])));
            var imported30 = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                package30,
                $"PCGen 3.0 normalization fixture {token}",
                "integration-test",
                "test-only",
                false,
                ParseFixture(token, "3e", "30", ["Pick Pocket"])));

            var grants = new SourceGrantService(db);
            await grants.GrantAsync(userId, imported35.PackageId);
            await grants.GrantAsync(userId, imported30.PackageId);

            var normalization = new SourceNormalizationService(db);
            var bluffCandidate = Assert.Single(await normalization.GetCandidatesAsync(userId, query: "Bluff"));
            Assert.Equal("Bluff", bluffCandidate.Name);
            Assert.Equal("skill.deception", bluffCandidate.SuggestedConceptKey);

            var craftCandidate = Assert.Single(await normalization.GetCandidatesAsync(userId, query: "Craft (alchemy)"));
            Assert.Equal("skill", craftCandidate.EntityType);
            Assert.Equal("tool.alchemist-s-supplies", craftCandidate.SuggestedConceptKey);

            var openLockCandidate = Assert.Single(await normalization.GetCandidatesAsync(userId, query: "Open Lock"));
            Assert.Equal("skill.open-lock", openLockCandidate.SuggestedConceptKey);
            Assert.DoesNotContain("thieves", openLockCandidate.SuggestedConceptKey, StringComparison.OrdinalIgnoreCase);

            var sleight = await normalization.AcceptAsync(
                EntityId(imported35, "Sleight of Hand"),
                userId);
            Assert.NotNull(sleight);
            Assert.Equal("skill.sleight-of-hand", sleight!.Concept.Key);
            Assert.Equal("skill", sleight.Concept.EntityType);
            Assert.Equal("Sleight of Hand", sleight.Concept.DisplayName);

            var pickPocket = await normalization.AcceptAsync(
                EntityId(imported30, "Pick Pocket"),
                userId);
            Assert.NotNull(pickPocket);
            Assert.Equal(sleight.Concept.Id, pickPocket!.Concept.Id);
            Assert.Equal("skill.sleight-of-hand", pickPocket.Concept.Key);

            var bluff = await normalization.AcceptAsync(EntityId(imported35, "Bluff"), userId);
            Assert.NotNull(bluff);
            Assert.Equal("skill.deception", bluff!.Concept.Key);
            Assert.Equal("skill", bluff.Concept.EntityType);
            Assert.Equal("Deception", bluff.Concept.DisplayName);

            var craft = await normalization.AcceptAsync(EntityId(imported35, "Craft (alchemy)"), userId);
            Assert.NotNull(craft);
            Assert.Equal("tool.alchemist-s-supplies", craft!.Concept.Key);
            Assert.Equal("tool", craft.Concept.EntityType);
            Assert.Equal("Alchemist's Supplies", craft.Concept.DisplayName);

            var openLock = await normalization.AcceptAsync(EntityId(imported35, "Open Lock"), userId);
            Assert.NotNull(openLock);
            Assert.Equal("skill.open-lock", openLock!.Concept.Key);
            Assert.Equal("skill", openLock.Concept.EntityType);
            Assert.Equal("Open Lock", openLock.Concept.DisplayName);
        }
        finally
        {
            await DeletePackageAsync(db, package35);
            await DeletePackageAsync(db, package30);
        }
    }

    private static NormalizedSourceRepresentation ParseFixture(
        string token,
        string edition,
        string suffix,
        IReadOnlyList<string> names)
    {
        var editionPath = edition == "3e" ? "3e" : "35e";
        var shortCode = $"SN{suffix}{token}";
        var lines = new List<string>
        {
            $"SOURCELONG:PCGen {edition} Normalization Fixture {token}\tSOURCESHORT:{shortCode}"
        };
        lines.AddRange(names.Select(name => $"{name}\tKEYSTAT:INT"));
        var path = $"data/{editionPath}/example/normalization_skills.lst";
        var representation = new PcGenSourceFormatAdapter().TryRead(new SourceRepresentationArtifact(
            $"normalization-skills-{suffix}-{token}.lst",
            Encoding.UTF8.GetBytes(string.Join('\n', lines)),
            $"test:pcgen-skill-normalization:{suffix}:{token}#{path}"));
        Assert.NotNull(representation);
        Assert.Equal(names.Count, representation!.Records.Count);
        return representation;
    }

    private static Guid EntityId(NormalizedSourceImportResult imported, string name) =>
        imported.Entities.Single(value => value.Name == name).EntityId;

    private static async Task DeletePackageAsync(RulesCoreDbContext db, string packageKey)
    {
        var package = await db.SourcePackages.SingleOrDefaultAsync(value => value.Key == packageKey);
        if (package is null) return;
        db.SourcePackages.Remove(package);
        await db.SaveChangesAsync();
    }
}
