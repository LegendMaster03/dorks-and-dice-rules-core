using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class SourcePagingAndAutoResolutionIntegrationTests
{
    [Fact]
    public async Task SourcePagingAndCrossEditionNoChangeResolutionAreDeterministic()
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
        var pagingPackageKey = $"paging-{token}";
        var oldPackageKey = $"autoresolve-old-{token}";
        var newPackageKey = $"autoresolve-new-{token}";
        var unchangedConceptKey = $"feat.unchanged-{token}";
        var changedConceptKey = $"feat.changed-{token}";
        var packageKeys = new[] { pagingPackageKey, oldPackageKey, newPackageKey };
        var conceptKeys = new[] { unchangedConceptKey, changedConceptKey };

        try
        {
            var importer = new SourceImportService(db);
            var search = new SourceEntitySearchService(db);
            var rules = new GlobalRulesService(db);

            await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                pagingPackageKey,
                "Paging fixture",
                "integration-test",
                "test-only",
                true,
                "paging-work",
                "Paging work",
                "release",
                "Paging release",
                JsonSerializer.Serialize(new
                {
                    rule = new[]
                    {
                        new { name = "Alpha", source = $"PAGE{token}" },
                        new { name = "Bravo", source = $"PAGE{token}" },
                        new { name = "Charlie", source = $"PAGE{token}" }
                    }
                }),
                "5e",
                "published",
                new DateOnly(2024, 1, 1)));

            var pageOne = await search.SearchAccessiblePageAsync(
                userId: null,
                query: $"PAGE{token}",
                limit: 2,
                offset: 0);
            var pageTwo = await search.SearchAccessiblePageAsync(
                userId: null,
                query: $"PAGE{token}",
                limit: 2,
                offset: 2);

            Assert.Equal(2, pageOne.Count);
            Assert.Single(pageTwo);
            Assert.Empty(pageOne.Select(value => value.EntityId).Intersect(pageTwo.Select(value => value.EntityId)));
            Assert.Equal(new[] { "Alpha", "Bravo" }, pageOne.Select(value => value.Name));
            Assert.Equal("Charlie", pageTwo[0].Name);

            var oldImport = await importer.Import5eToolsDocumentAsync(AutoResolutionRequest(
                oldPackageKey,
                "Old edition fixture",
                "old-work",
                "Old edition",
                "old-release",
                "5e",
                $"OLD{token}",
                new DateOnly(2014, 8, 19),
                unchangedText: "You gain advantage on the attack roll.",
                changedText: "You gain a +2 bonus to the attack roll."));
            var newImport = await importer.Import5eToolsDocumentAsync(AutoResolutionRequest(
                newPackageKey,
                "New edition fixture",
                "new-work",
                "New edition",
                "new-release",
                "5.5e",
                $"NEW{token}",
                new DateOnly(2024, 9, 17),
                unchangedText: "You gain advantage on the attack roll.",
                changedText: "You gain advantage on the attack roll."));

            var oldUnchanged = oldImport.Entities.Single(value => value.Name == "Unchanged Rule");
            var newUnchanged = newImport.Entities.Single(value => value.Name == "Renamed Unchanged Rule");
            var oldChanged = oldImport.Entities.Single(value => value.Name == "Changed Rule");
            var newChanged = newImport.Entities.Single(value => value.Name == "Changed Rule");

            var unchangedConcept = (await rules.CreateConceptAsync(
                new CreateRuleConceptRequest(unchangedConceptKey, "feat", "Unchanged Rule"),
                "rules-lawyer")).Value;
            await rules.BindSourceEntityAsync(
                unchangedConcept.Id,
                new BindRuleConceptSourceRequest(oldUnchanged.EntityId),
                "rules-lawyer");
            await rules.BindSourceEntityAsync(
                unchangedConcept.Id,
                new BindRuleConceptSourceRequest(newUnchanged.EntityId),
                "rules-lawyer");

            var unchangedResolution = await RuleAutoResolutionService.TryResolveAsync(
                db,
                unchangedConcept.Id,
                "rules-lawyer");
            Assert.True(unchangedResolution.Eligible);
            Assert.True(unchangedResolution.Applied);
            Assert.NotNull(unchangedResolution.DecisionId);

            var unchangedDecision = await db.GlobalRuleDecisions
                .AsNoTracking()
                .SingleAsync(value => value.RuleConceptId == unchangedConcept.Id);
            Assert.Equal(RuleDecisionKinds.SelectSource, unchangedDecision.DecisionKind);
            Assert.StartsWith("Auto-resolved:", unchangedDecision.Note);

            var repeatedResolution = await RuleAutoResolutionService.TryResolveAsync(
                db,
                unchangedConcept.Id,
                "rules-lawyer");
            Assert.True(repeatedResolution.Eligible);
            Assert.False(repeatedResolution.Applied);
            Assert.Equal(1, await db.GlobalRuleDecisions.CountAsync(
                value => value.RuleConceptId == unchangedConcept.Id));

            var changedConcept = (await rules.CreateConceptAsync(
                new CreateRuleConceptRequest(changedConceptKey, "feat", "Changed Rule"),
                "rules-lawyer")).Value;
            await rules.BindSourceEntityAsync(
                changedConcept.Id,
                new BindRuleConceptSourceRequest(oldChanged.EntityId),
                "rules-lawyer");
            await rules.BindSourceEntityAsync(
                changedConcept.Id,
                new BindRuleConceptSourceRequest(newChanged.EntityId),
                "rules-lawyer");

            var changedResolution = await RuleAutoResolutionService.TryResolveAsync(
                db,
                changedConcept.Id,
                "rules-lawyer");
            Assert.False(changedResolution.Eligible);
            Assert.False(changedResolution.Applied);
            Assert.Contains("manual adjudication", changedResolution.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.False(await db.GlobalRuleDecisions.AnyAsync(
                value => value.RuleConceptId == changedConcept.Id));
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

    private static Import5eToolsDocumentRequest AutoResolutionRequest(
        string packageKey,
        string packageName,
        string workKey,
        string workName,
        string editionKey,
        string gameEdition,
        string sourceCode,
        DateOnly publicationDate,
        string unchangedText,
        string changedText) =>
        new(
            packageKey,
            packageName,
            "integration-test",
            "test-only",
            true,
            workKey,
            workName,
            editionKey,
            workName,
            JsonSerializer.Serialize(new
            {
                feat = new object[]
                {
                    new
                    {
                        name = gameEdition == "5e" ? "Unchanged Rule" : "Renamed Unchanged Rule",
                        source = sourceCode,
                        page = gameEdition == "5e" ? 10 : 110,
                        edition = gameEdition == "5e" ? "classic" : "one",
                        basicRules = gameEdition == "5e",
                        basicRules2024 = gameEdition != "5e",
                        entries = new[] { unchangedText }
                    },
                    new
                    {
                        name = "Changed Rule",
                        source = sourceCode,
                        page = gameEdition == "5e" ? 20 : 120,
                        edition = gameEdition == "5e" ? "classic" : "one",
                        entries = new[] { changedText }
                    }
                }
            }),
            gameEdition,
            "published",
            publicationDate);
}