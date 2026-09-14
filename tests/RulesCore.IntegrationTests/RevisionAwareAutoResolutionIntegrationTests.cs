using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class RevisionAwareAutoResolutionIntegrationTests
{
    [Fact]
    public async Task AutoResolutionUsesRevisionDescendantOfBoundCanonicalEntity()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<RulesCoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new RulesCoreDbContext(options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..10];
        var oldPackageKey = $"revision-auto-old-{token}";
        var newPackageKey = $"revision-auto-new-{token}";
        var conceptKey = $"monster.revision-auto-{token}";

        try
        {
            var importer = new SourceImportService(db);
            var rules = new GlobalRulesService(db);

            var oldImport = await importer.Import5eToolsDocumentAsync(Request(
                oldPackageKey,
                "Old revision-aware source",
                "3e",
                $"OLD{token}",
                new DateOnly(2000, 8, 1),
                hitPoints: 20));
            var newImport = await importer.Import5eToolsDocumentAsync(Request(
                newPackageKey,
                "New revision-aware source",
                "3.5e",
                $"NEW{token}",
                new DateOnly(2003, 7, 1),
                hitPoints: 24));

            var oldEntity = Assert.Single(oldImport.Entities);
            var newEntity = Assert.Single(newImport.Entities);
            var concept = (await rules.CreateConceptAsync(
                new CreateRuleConceptRequest(conceptKey, "monster", "Revision-Aware Monster"),
                "rules-lawyer")).Value;

            await rules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(oldEntity.EntityId),
                "rules-lawyer");
            await rules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(newEntity.EntityId),
                "rules-lawyer");

            Assert.False(await db.GlobalRuleDecisions.AnyAsync(value => value.RuleConceptId == concept.Id));

            var revisedOldImport = await importer.Import5eToolsDocumentAsync(Request(
                oldPackageKey,
                "Old revision-aware source",
                "3e",
                $"OLD{token}",
                new DateOnly(2000, 8, 1),
                hitPoints: 24));
            var revisedOldEntity = Assert.Single(revisedOldImport.Entities);
            Assert.Equal(oldEntity.EntityId, revisedOldEntity.EntityId);
            Assert.Equal(2, revisedOldEntity.RevisionNumber);

            var result = await RuleAutoResolutionService.TryResolveAsync(
                db,
                concept.Id,
                "rules-lawyer");

            Assert.True(result.Eligible);
            Assert.True(result.Applied);
            var decision = await db.GlobalRuleDecisions
                .AsNoTracking()
                .SingleAsync(value => value.RuleConceptId == concept.Id);
            Assert.StartsWith("Auto-resolved:", decision.Note);
        }
        finally
        {
            var concept = await db.RuleConcepts.SingleOrDefaultAsync(value => value.Key == conceptKey);
            if (concept is not null)
            {
                db.RuleConcepts.Remove(concept);
                await db.SaveChangesAsync();
            }

            var packages = await db.SourcePackages
                .Where(value => value.Key == oldPackageKey || value.Key == newPackageKey)
                .ToArrayAsync();
            if (packages.Length > 0)
            {
                db.SourcePackages.RemoveRange(packages);
                await db.SaveChangesAsync();
            }
        }
    }

    private static Import5eToolsDocumentRequest Request(
        string packageKey,
        string packageDisplayName,
        string gameEdition,
        string sourceCode,
        DateOnly publicationDate,
        int hitPoints) =>
        new(
            packageKey,
            packageDisplayName,
            "integration-test",
            "test-only",
            true,
            $"{packageKey}-work",
            packageDisplayName,
            "release",
            packageDisplayName,
            JsonSerializer.Serialize(new
            {
                monster = new object[]
                {
                    new
                    {
                        name = "Revision-Aware Monster",
                        source = sourceCode,
                        size = new[] { "M" },
                        hp = new { average = hitPoints, formula = "4d8 + 2" },
                        trait = new[]
                        {
                            new
                            {
                                name = "Shared Trait",
                                entries = new[] { "Shared rule text." }
                            }
                        }
                    }
                }
            }),
            gameEdition,
            "published",
            publicationDate);
}
