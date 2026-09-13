using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class AdditiveAutoResolutionIntegrationTests
{
    [Fact]
    public async Task CrossEditionAdditionsSelectTheNonDestructiveSemanticSuperset()
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
        var oldPackageKey = $"additive-old-{token}";
        var newPackageKey = $"additive-new-{token}";
        var newerSupersetConceptKey = $"monster.newer-superset-{token}";
        var olderSupersetConceptKey = $"monster.older-superset-{token}";
        var conflictConceptKey = $"monster.conflict-{token}";
        var packageKeys = new[] { oldPackageKey, newPackageKey };
        var conceptKeys = new[]
        {
            newerSupersetConceptKey,
            olderSupersetConceptKey,
            conflictConceptKey
        };

        try
        {
            var importer = new SourceImportService(db);
            var rules = new GlobalRulesService(db);

            var oldImport = await importer.Import5eToolsDocumentAsync(MonsterRequest(
                oldPackageKey,
                "Old monster edition",
                "old-monsters",
                "5e",
                $"OLD{token}",
                new DateOnly(2014, 8, 19),
                newerSupersetTraits:
                [
                    Trait("Shared Trait", "Shared rule text.")
                ],
                olderSupersetTraits:
                [
                    Trait("Shared Trait", "Shared rule text."),
                    Trait("Legacy Ability", "Compatible ability retained from the older edition.")
                ],
                conflictHitPoints: 20));

            var newImport = await importer.Import5eToolsDocumentAsync(MonsterRequest(
                newPackageKey,
                "New monster edition",
                "new-monsters",
                "5.5e",
                $"NEW{token}",
                new DateOnly(2024, 9, 17),
                newerSupersetTraits:
                [
                    Trait("Shared Trait", "Shared rule text."),
                    Trait("Modern Ability", "Compatible ability added by the newer edition.")
                ],
                olderSupersetTraits:
                [
                    Trait("Shared Trait", "Shared rule text.")
                ],
                conflictHitPoints: 24));

            var oldNewerSuperset = oldImport.Entities.Single(value => value.Name == "Newer Superset Monster");
            var newNewerSuperset = newImport.Entities.Single(value => value.Name == "Newer Superset Monster");
            var oldOlderSuperset = oldImport.Entities.Single(value => value.Name == "Older Superset Monster");
            var newOlderSuperset = newImport.Entities.Single(value => value.Name == "Older Superset Monster");
            var oldConflict = oldImport.Entities.Single(value => value.Name == "Conflicting Monster");
            var newConflict = newImport.Entities.Single(value => value.Name == "Conflicting Monster");

            var newerSupersetConcept = (await rules.CreateConceptAsync(
                new CreateRuleConceptRequest(newerSupersetConceptKey, "monster", "Newer Superset Monster"),
                "rules-lawyer")).Value;
            await rules.BindSourceEntityAsync(
                newerSupersetConcept.Id,
                new BindRuleConceptSourceRequest(oldNewerSuperset.EntityId),
                "rules-lawyer");
            await rules.BindSourceEntityAsync(
                newerSupersetConcept.Id,
                new BindRuleConceptSourceRequest(newNewerSuperset.EntityId),
                "rules-lawyer");

            var newerDecision = await db.GlobalRuleDecisions
                .AsNoTracking()
                .SingleAsync(value => value.RuleConceptId == newerSupersetConcept.Id);
            var newNewerRevisionId = await LatestRevisionIdAsync(db, newNewerSuperset.EntityId);
            Assert.Equal(newNewerRevisionId, newerDecision.SelectedSourceEntityRevisionId);
            Assert.Equal(RuleDecisionKinds.SelectSource, newerDecision.DecisionKind);
            Assert.Contains("non-destructive additions", newerDecision.Note, StringComparison.OrdinalIgnoreCase);

            var olderSupersetConcept = (await rules.CreateConceptAsync(
                new CreateRuleConceptRequest(olderSupersetConceptKey, "monster", "Older Superset Monster"),
                "rules-lawyer")).Value;
            await rules.BindSourceEntityAsync(
                olderSupersetConcept.Id,
                new BindRuleConceptSourceRequest(oldOlderSuperset.EntityId),
                "rules-lawyer");
            await rules.BindSourceEntityAsync(
                olderSupersetConcept.Id,
                new BindRuleConceptSourceRequest(newOlderSuperset.EntityId),
                "rules-lawyer");

            var olderDecision = await db.GlobalRuleDecisions
                .AsNoTracking()
                .SingleAsync(value => value.RuleConceptId == olderSupersetConcept.Id);
            var oldOlderRevisionId = await LatestRevisionIdAsync(db, oldOlderSuperset.EntityId);
            Assert.Equal(oldOlderRevisionId, olderDecision.SelectedSourceEntityRevisionId);
            Assert.Contains("non-destructive additions", olderDecision.Note, StringComparison.OrdinalIgnoreCase);

            var repeatedResolution = await RuleAutoResolutionService.TryResolveAsync(
                db,
                olderSupersetConcept.Id,
                "rules-lawyer");
            Assert.True(repeatedResolution.Eligible);
            Assert.False(repeatedResolution.Applied);
            Assert.Equal(1, await db.GlobalRuleDecisions.CountAsync(
                value => value.RuleConceptId == olderSupersetConcept.Id));

            var conflictConcept = (await rules.CreateConceptAsync(
                new CreateRuleConceptRequest(conflictConceptKey, "monster", "Conflicting Monster"),
                "rules-lawyer")).Value;
            await rules.BindSourceEntityAsync(
                conflictConcept.Id,
                new BindRuleConceptSourceRequest(oldConflict.EntityId),
                "rules-lawyer");
            await rules.BindSourceEntityAsync(
                conflictConcept.Id,
                new BindRuleConceptSourceRequest(newConflict.EntityId),
                "rules-lawyer");

            var conflictResolution = await RuleAutoResolutionService.TryResolveAsync(
                db,
                conflictConcept.Id,
                "rules-lawyer");
            Assert.False(conflictResolution.Eligible);
            Assert.False(conflictResolution.Applied);
            Assert.Contains("manual adjudication", conflictResolution.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.False(await db.GlobalRuleDecisions.AnyAsync(
                value => value.RuleConceptId == conflictConcept.Id));
        }
        finally
        {
            var concepts = await db.RuleConcepts
                .Where(value => conceptKeys.Contains(value.Key))
                .ToArrayAsync();
            if (concepts.Length > 0)
            {
                db.RuleConcepts.RemoveRange(concepts);
                await db.SaveChangesAsync();
            }

            var packages = await db.SourcePackages
                .Where(value => packageKeys.Contains(value.Key))
                .ToArrayAsync();
            if (packages.Length > 0)
            {
                db.SourcePackages.RemoveRange(packages);
                await db.SaveChangesAsync();
            }
        }
    }

    private static async Task<Guid> LatestRevisionIdAsync(RulesCoreDbContext db, Guid sourceEntityId) =>
        await db.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => value.SourceEntityId == sourceEntityId)
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => value.Id)
            .FirstAsync();

    private static object Trait(string name, string text) => new
    {
        name,
        entries = new[] { text }
    };

    private static Import5eToolsDocumentRequest MonsterRequest(
        string packageKey,
        string packageName,
        string workKey,
        string gameEdition,
        string sourceCode,
        DateOnly publicationDate,
        object[] newerSupersetTraits,
        object[] olderSupersetTraits,
        int conflictHitPoints) =>
        new(
            packageKey,
            packageName,
            "integration-test",
            "test-only",
            true,
            workKey,
            packageName,
            "release",
            packageName,
            JsonSerializer.Serialize(new
            {
                monster = new object[]
                {
                    new
                    {
                        name = "Newer Superset Monster",
                        source = sourceCode,
                        size = new[] { "M" },
                        hp = new { average = 20, formula = "4d8 + 2" },
                        trait = newerSupersetTraits
                    },
                    new
                    {
                        name = "Older Superset Monster",
                        source = sourceCode,
                        size = new[] { "M" },
                        hp = new { average = 20, formula = "4d8 + 2" },
                        trait = olderSupersetTraits
                    },
                    new
                    {
                        name = "Conflicting Monster",
                        source = sourceCode,
                        size = new[] { "M" },
                        hp = new { average = conflictHitPoints, formula = "4d8 + 2" },
                        trait = new[]
                        {
                            Trait("Shared Trait", "Shared rule text.")
                        }
                    }
                }
            }),
            gameEdition,
            "published",
            publicationDate);
}
