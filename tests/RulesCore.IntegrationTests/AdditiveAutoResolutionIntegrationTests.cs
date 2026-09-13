using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class AdditiveAutoResolutionIntegrationTests
{
    [Fact]
    public async Task CrossEditionAdditionsBuildNonDestructiveResults()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<RulesCoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new RulesCoreDbContext(options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..10];
        var oldPackageKey = $"additive-old-{token}";
        var newPackageKey = $"additive-new-{token}";
        var newerKey = $"monster.newer-superset-{token}";
        var olderKey = $"monster.older-superset-{token}";
        var unionKey = $"monster.additive-union-{token}";
        var conflictKey = $"monster.conflict-{token}";
        var conceptKeys = new[] { newerKey, olderKey, unionKey, conflictKey };

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
                newerTraits: [Trait("Shared Trait", "Shared rule text.")],
                olderTraits:
                [
                    Trait("Shared Trait", "Shared rule text."),
                    Trait("Legacy Ability", "Compatible ability retained from the older edition.")
                ],
                unionTraits:
                [
                    Trait("Shared Trait", "Shared rule text."),
                    Trait("Legacy Ability", "Compatible older ability.")
                ],
                conflictHp: 20));

            var newImport = await importer.Import5eToolsDocumentAsync(MonsterRequest(
                newPackageKey,
                "New monster edition",
                "new-monsters",
                "5.5e",
                $"NEW{token}",
                new DateOnly(2024, 9, 17),
                newerTraits:
                [
                    Trait("Shared Trait", "Shared rule text."),
                    Trait("Modern Ability", "Compatible ability added by the newer edition.")
                ],
                olderTraits: [Trait("Shared Trait", "Shared rule text.")],
                unionTraits:
                [
                    Trait("Shared Trait", "Shared rule text."),
                    Trait("Modern Ability", "Compatible newer ability.")
                ],
                conflictHp: 24));

            var newerDecision = await BindPairAndGetDecisionAsync(
                db,
                rules,
                newerKey,
                "Newer Superset Monster",
                Find(oldImport, "Newer Superset Monster"),
                Find(newImport, "Newer Superset Monster"));
            Assert.Equal(RuleDecisionKinds.SelectSource, newerDecision.DecisionKind);
            Assert.Equal(
                await LatestRevisionIdAsync(db, Find(newImport, "Newer Superset Monster").EntityId),
                newerDecision.SelectedSourceEntityRevisionId);
            Assert.StartsWith("Auto-resolved-additive:", newerDecision.Note);

            var olderDecision = await BindPairAndGetDecisionAsync(
                db,
                rules,
                olderKey,
                "Older Superset Monster",
                Find(oldImport, "Older Superset Monster"),
                Find(newImport, "Older Superset Monster"));
            Assert.Equal(RuleDecisionKinds.SelectSource, olderDecision.DecisionKind);
            Assert.Equal(
                await LatestRevisionIdAsync(db, Find(oldImport, "Older Superset Monster").EntityId),
                olderDecision.SelectedSourceEntityRevisionId);
            Assert.StartsWith("Auto-resolved-additive:", olderDecision.Note);

            var repeatedResolution = await RuleAutoResolutionService.TryResolveAsync(
                db,
                olderDecision.RuleConceptId,
                "rules-lawyer");
            Assert.True(repeatedResolution.Eligible);
            Assert.False(repeatedResolution.Applied);
            Assert.Equal(1, await db.GlobalRuleDecisions.CountAsync(
                value => value.RuleConceptId == olderDecision.RuleConceptId));

            var unionDecision = await BindPairAndGetDecisionAsync(
                db,
                rules,
                unionKey,
                "Additive Union Monster",
                Find(oldImport, "Additive Union Monster"),
                Find(newImport, "Additive Union Monster"));
            Assert.Equal(RuleDecisionKinds.JsonMergePatch, unionDecision.DecisionKind);
            Assert.NotNull(unionDecision.PatchJson);
            Assert.Equal(
                await LatestRevisionIdAsync(db, Find(newImport, "Additive Union Monster").EntityId),
                unionDecision.SelectedSourceEntityRevisionId);
            Assert.StartsWith("Auto-resolved-additive:", unionDecision.Note);

            var unionBase = await db.SourceEntityRevisions
                .AsNoTracking()
                .SingleAsync(value => value.Id == unionDecision.SelectedSourceEntityRevisionId);
            var resolvedUnion = JsonMergePatch.Apply(unionBase.RawJson, unionDecision.PatchJson);
            var traitNames = resolvedUnion.GetProperty("trait")
                .EnumerateArray()
                .Select(value => value.GetProperty("name").GetString())
                .ToArray();
            Assert.Contains("Shared Trait", traitNames);
            Assert.Contains("Legacy Ability", traitNames);
            Assert.Contains("Modern Ability", traitNames);

            var conflictOld = Find(oldImport, "Conflicting Monster");
            var conflictNew = Find(newImport, "Conflicting Monster");
            var conflictConcept = (await rules.CreateConceptAsync(
                new CreateRuleConceptRequest(conflictKey, "monster", "Conflicting Monster"),
                "rules-lawyer")).Value;
            await rules.BindSourceEntityAsync(
                conflictConcept.Id,
                new BindRuleConceptSourceRequest(conflictOld.EntityId),
                "rules-lawyer");
            await rules.BindSourceEntityAsync(
                conflictConcept.Id,
                new BindRuleConceptSourceRequest(conflictNew.EntityId),
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
                .Where(value => value.Key == oldPackageKey || value.Key == newPackageKey)
                .ToArrayAsync();
            if (packages.Length > 0)
            {
                db.SourcePackages.RemoveRange(packages);
                await db.SaveChangesAsync();
            }
        }
    }

    private static ImportedSourceEntity Find(SourceImportResult import, string name) =>
        import.Entities.Single(value => value.Name == name);

    private static async Task<GlobalRuleDecision> BindPairAndGetDecisionAsync(
        RulesCoreDbContext db,
        GlobalRulesService rules,
        string conceptKey,
        string displayName,
        ImportedSourceEntity oldSource,
        ImportedSourceEntity newSource)
    {
        var concept = (await rules.CreateConceptAsync(
            new CreateRuleConceptRequest(conceptKey, "monster", displayName),
            "rules-lawyer")).Value;
        await rules.BindSourceEntityAsync(
            concept.Id,
            new BindRuleConceptSourceRequest(oldSource.EntityId),
            "rules-lawyer");
        await rules.BindSourceEntityAsync(
            concept.Id,
            new BindRuleConceptSourceRequest(newSource.EntityId),
            "rules-lawyer");
        return await db.GlobalRuleDecisions
            .AsNoTracking()
            .SingleAsync(value => value.RuleConceptId == concept.Id);
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
        object[] newerTraits,
        object[] olderTraits,
        object[] unionTraits,
        int conflictHp) =>
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
                        trait = newerTraits
                    },
                    new
                    {
                        name = "Older Superset Monster",
                        source = sourceCode,
                        size = new[] { "M" },
                        hp = new { average = 20, formula = "4d8 + 2" },
                        trait = olderTraits
                    },
                    new
                    {
                        name = "Additive Union Monster",
                        source = sourceCode,
                        size = new[] { "M" },
                        hp = new { average = 20, formula = "4d8 + 2" },
                        trait = unionTraits
                    },
                    new
                    {
                        name = "Conflicting Monster",
                        source = sourceCode,
                        size = new[] { "M" },
                        hp = new { average = conflictHp, formula = "4d8 + 2" },
                        trait = new[] { Trait("Shared Trait", "Shared rule text.") }
                    }
                }
            }),
            gameEdition,
            "published",
            publicationDate);
}
