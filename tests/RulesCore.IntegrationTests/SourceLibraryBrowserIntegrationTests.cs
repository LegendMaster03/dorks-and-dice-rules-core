using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class SourceLibraryBrowserIntegrationTests
{
    [Fact]
    public void SourceLibraryRoutesKeepSourceAndPublishedIdentitiesSeparate()
    {
        var publicationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var entityId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var publication = SourceLibraryRoutes.ForPublication(publicationId);
        var collection = SourceLibraryRoutes.ForCollection(publicationId, "monster");
        var entity = SourceLibraryRoutes.ForEntity(publicationId, "monster", entityId);
        var unknown = SourceLibraryRoutes.ForCollection(publicationId, "customRuleType");

        Assert.Equal("/library/11111111-1111-1111-1111-111111111111", publication.ToolRelativePath);
        Assert.Equal("/library/11111111-1111-1111-1111-111111111111/monsters", collection.ToolRelativePath);
        Assert.Equal("/library/11111111-1111-1111-1111-111111111111/monsters/22222222-2222-2222-2222-222222222222", entity.ToolRelativePath);
        Assert.Equal("source-entity:22222222-2222-2222-2222-222222222222", entity.RouteIdentity);
        Assert.Contains("/types/customRuleType", unknown.ToolRelativePath, StringComparison.Ordinal);
        Assert.False(entity.ToolRelativePath.StartsWith("/monsters/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SourceLibraryBrowserGroupsPublicationsAndPreservesAccessRules()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var options = new DbContextOptionsBuilder<RulesCoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new RulesCoreDbContext(options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..10];
        var publicPackageKey = $"library-public-{token}";
        var privatePackageKey = $"library-private-{token}";
        var owner = $"library-owner-{token}";
        var sourceCode = $"LIB{token}";
        var privateSourceCode = $"PRIV{token}";

        try
        {
            var importer = new SourceImportService(db);
            var publicImport = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                publicPackageKey,
                "Rules Library public fixture",
                "integration-test",
                "test-only",
                true,
                "library-public-work",
                "Rules Library Test Book",
                "first-printing",
                "First Printing",
                JsonSerializer.Serialize(new
                {
                    monster = new[]
                    {
                        new
                        {
                            name = "Library Dragon",
                            source = sourceCode,
                            ac = new[] { 18 },
                            hp = new { average = 95, formula = "10d10+40" },
                            str = 20,
                            dex = 12,
                            con = 18,
                            @int = 10,
                            wis = 14,
                            cha = 16
                        }
                    },
                    spell = new[]
                    {
                        new { name = "Library Spark", source = sourceCode, level = 1, entries = new[] { "A test spell." } }
                    }
                }),
                "5e",
                "published",
                new DateOnly(2024, 1, 1)));

            var privateImport = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                privatePackageKey,
                "Rules Library private fixture",
                "integration-test",
                "test-only",
                false,
                "library-private-work",
                "Private Test Book",
                "original",
                "Original",
                JsonSerializer.Serialize(new
                {
                    feat = new[]
                    {
                        new { name = "Private Test Feat", source = privateSourceCode, entries = new[] { "Private text." } }
                    }
                }),
                "5e",
                "published",
                new DateOnly(2024, 2, 1)));

            await new SourceGrantService(db).GrantAsync(owner, privateImport.PackageId);

            var browser = new SourceLibraryBrowserService(db);
            var anonymousPublications = await browser.GetPublicationsAsync(null);
            var ownerPublications = await browser.GetPublicationsAsync(owner);

            var publicPublication = Assert.Single(
                anonymousPublications,
                value => value.PublicationId == publicImport.EditionId);
            Assert.Equal("Rules Library Test Book", publicPublication.DisplayName);
            Assert.Equal(2, publicPublication.EntityCount);
            Assert.Contains(publicPublication.Categories, value => value.EntityType == "monster" && value.Count == 1);
            Assert.Contains(publicPublication.Categories, value => value.EntityType == "spell" && value.Count == 1);
            Assert.DoesNotContain(anonymousPublications, value => value.PublicationId == privateImport.EditionId);
            Assert.Contains(ownerPublications, value => value.PublicationId == privateImport.EditionId);

            var monsters = await browser.SearchAsync(
                userId: null,
                publicationId: publicImport.EditionId,
                entityType: "monster",
                query: "Dragon",
                limit: 10,
                offset: 0);
            var monster = Assert.Single(monsters);
            Assert.StartsWith($"/library/{publicImport.EditionId:D}/monsters/", monster.BrowserLink.ToolRelativePath, StringComparison.Ordinal);

            var detail = await browser.GetEntityAsync(publicImport.EditionId, monster.EntityId, null);
            Assert.NotNull(detail);
            Assert.Equal("Library Dragon", detail!.Name);
            Assert.Equal(publicImport.EditionId, detail.PublicationId);
            Assert.Equal(monster.BrowserLink.ToolRelativePath, detail.BrowserLink.ToolRelativePath);

            Assert.Null(await browser.GetEntityAsync(privateImport.EditionId, privateImport.Entities[0].EntityId, null));
            Assert.NotNull(await browser.GetEntityAsync(privateImport.EditionId, privateImport.Entities[0].EntityId, owner));
        }
        finally
        {
            var packages = await db.SourcePackages
                .Where(value => value.Key == publicPackageKey || value.Key == privatePackageKey)
                .ToArrayAsync();
            if (packages.Length > 0)
            {
                db.SourcePackages.RemoveRange(packages);
                await db.SaveChangesAsync();
            }
        }
    }
}
