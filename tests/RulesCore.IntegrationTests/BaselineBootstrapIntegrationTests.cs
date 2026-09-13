using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Bootstrap;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class BaselineBootstrapIntegrationTests
{
    private static readonly string[] BuiltInPackageKeys =
    ["wotc-srd-ogl", "wotc-srd-cc", "loot-tavern-free", "loot-tavern-licensed", "dorks-and-dice-baseline"];

    [Fact]
    public async Task FreshDatabaseGetsPublicSrdsAndSettledRulesWithoutOverwritingLaterChanges()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        var importer = new SourceImportService(db);
        var globalRules = new GlobalRulesService(db);
        var bootstrapper = new RulesCoreBaselineBootstrapper(db, importer, globalRules);

        await ResetAsync(db);
        try
        {
            var first = await bootstrapper.EnsureAsync();
            Assert.True(first.RulesBaselineApplied);
            Assert.NotNull(first.PublishedRuleset);
            Assert.Equal(1, first.PublishedRuleset!.RevisionNumber);
            Assert.Equal(6, first.PublishedRuleset.EntryCount);
            Assert.Equal(6, first.HouseRuleSourceEntityCount);
            Assert.Equal(4, first.SourceAuthorityReferenceCount);
            Assert.Equal(4, first.HostedSourceDefinitionCount);

            var packages = await db.SourcePackages
                .AsNoTracking()
                .Where(value => BuiltInPackageKeys.Contains(value.Key))
                .ToArrayAsync();
            Assert.Equal(5, packages.Length);
            var ogl = packages.Single(value => value.Key == "wotc-srd-ogl");
            var cc = packages.Single(value => value.Key == "wotc-srd-cc");
            var house = packages.Single(value => value.Key == "dorks-and-dice-baseline");
            Assert.True(ogl.IsPublic);
            Assert.True(cc.IsPublic);
            Assert.Equal("OGL-1.0a", ogl.License);
            Assert.Equal("CC-BY-4.0", cc.License);
            Assert.True(packages.Single(value => value.Key == "loot-tavern-free").IsPublic);
            Assert.False(packages.Single(value => value.Key == "loot-tavern-licensed").IsPublic);

            var anonymousSearch = new SourceEntitySearchService(db);
            foreach (var sourceCode in new[] { "SRD3", "SRD35", "SRD51", "SRD52" })
            {
                var visible = await anonymousSearch.SearchAccessibleAsync(
                    userId: null,
                    query: sourceCode,
                    limit: 1);
                Assert.NotEmpty(visible);
            }

            var oglCodes = await db.SourceEntities
                .Where(value => value.SourcePackageId == ogl.Id)
                .Select(value => value.SourceCode)
                .Distinct()
                .ToArrayAsync();
            Assert.Contains("SRD3", oglCodes);
            Assert.Contains("SRD35", oglCodes);
            var ccCodes = await db.SourceEntities
                .Where(value => value.SourcePackageId == cc.Id)
                .Select(value => value.SourceCode)
                .Distinct()
                .ToArrayAsync();
            Assert.Contains("SRD51", ccCodes);
            Assert.Contains("SRD52", ccCodes);

            Assert.Equal(4, await CountAuthorityReferencesAsync(db));
            var houseEntities = await db.SourceEntities
                .Where(value => value.SourcePackageId == house.Id)
                .ToArrayAsync();
            Assert.Equal(6, houseEntities.Length);
            Assert.All(houseEntities, value => Assert.Equal("DDBASE", value.SourceCode));

            Assert.Equal(6, await db.RuleConcepts.CountAsync());
            Assert.Equal(6, await db.RuleConceptSourceBindings.CountAsync());
            Assert.Equal(6, await db.GlobalRuleDecisions.CountAsync());
            Assert.Equal(1, await db.RulesetRevisions.CountAsync());

            var healing = await globalRules.ResolveLatestAsync("house.healing-potion-use", null);
            Assert.Equal("maximum possible healing",
                healing!.Document.GetProperty("action").GetProperty("healing").GetString());

            var concept = await db.RuleConcepts.SingleAsync(value => value.Key == "house.healing-potion-use");
            var selectedRevision = await db.GlobalRuleDecisions
                .Where(value => value.RuleConceptId == concept.Id)
                .OrderByDescending(value => value.DecisionNumber)
                .Select(value => value.SelectedSourceEntityRevisionId)
                .FirstAsync();
            await globalRules.SetDecisionAsync(
                concept.Id,
                new SetGlobalRuleDecisionRequest(selectedRevision, "User-maintained baseline decision."),
                "rules-lawyer");

            var revisionCount = await db.SourceEntityRevisions.CountAsync();
            var second = await bootstrapper.EnsureAsync();
            Assert.False(second.RulesBaselineApplied);
            Assert.Null(second.PublishedRuleset);
            Assert.Equal(revisionCount, await db.SourceEntityRevisions.CountAsync());
            Assert.Equal(1, await db.RulesetRevisions.CountAsync());
            Assert.Equal(2, await db.GlobalRuleDecisions.CountAsync(value => value.RuleConceptId == concept.Id));
        }
        finally
        {
            await ResetAsync(db);
        }
    }

    private static async Task<int> CountAuthorityReferencesAsync(RulesCoreDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM source_package_authority_reference;";
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task ResetAsync(RulesCoreDbContext db)
    {
        await db.CampaignRulesetRevisionEntries.ExecuteDeleteAsync();
        await db.CampaignRulesetRevisions.ExecuteDeleteAsync();
        await db.CampaignRuleDecisions.ExecuteDeleteAsync();
        await db.CampaignRulesetSelections.ExecuteDeleteAsync();
        await db.RulesetRevisionEntries.ExecuteDeleteAsync();
        await db.RulesetRevisions.ExecuteDeleteAsync();
        await db.GlobalRuleDecisions.ExecuteDeleteAsync();
        await db.RuleConceptSourceBindings.ExecuteDeleteAsync();
        await db.RuleConcepts.ExecuteDeleteAsync();
        await db.Database.ExecuteSqlRawAsync("""
            DELETE FROM hosted_source_definition
            WHERE definition_key IN ('builtin-wotc-srd-3e','builtin-wotc-srd-3-5e','builtin-wotc-srd-5-1','builtin-wotc-srd-5-2-1');
            """);
        db.ChangeTracker.Clear();
        var packages = await db.SourcePackages.Where(value => BuiltInPackageKeys.Contains(value.Key)).ToArrayAsync();
        db.SourcePackages.RemoveRange(packages);
        await db.SaveChangesAsync();
    }
}
