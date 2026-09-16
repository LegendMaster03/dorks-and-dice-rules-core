using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Bootstrap;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class BaselineBootstrapIntegrationTests
{
    private const string BootstrapActor = "rules-core-bootstrap";

    private static readonly string[] BuiltInPackageKeys =
    ["wotc-srd-ogl", "wotc-srd-cc", "loot-tavern-free", "loot-tavern-licensed", "dorks-and-dice-baseline"];

    private static readonly string[] LegacyBuiltInHostedKeys =
    ["builtin-wotc-srd-3e", "builtin-wotc-srd-3-5e", "builtin-wotc-srd-5-1", "builtin-wotc-srd-5-2-1"];

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
        var hostedService = new HostedSourceService(db, importer);

        await ResetAsync(db);
        try
        {
            // Simulate an installation upgraded from the old bootstrap path. Only an exact,
            // untouched historical seed is stale bootstrap state and may be retired.
            var historicalSeed = HistoricalHostedDefinition("builtin-wotc-srd-5-1");
            await hostedService.SetAsync(
                "builtin-wotc-srd-5-1",
                historicalSeed,
                BootstrapActor);

            var first = await bootstrapper.EnsureAsync();
            Assert.True(first.RulesBaselineApplied);
            Assert.NotNull(first.PublishedRuleset);
            Assert.Equal(1, first.PublishedRuleset!.RevisionNumber);
            Assert.Equal(6, first.PublishedRuleset.EntryCount);
            Assert.Equal(6, first.HouseRuleSourceEntityCount);
            Assert.Equal(4, first.SourceAuthorityReferenceCount);
            Assert.Equal(0, first.HostedSourceDefinitionCount);

            var hosted = await hostedService.ListAsync(includeDisabled: true);
            Assert.DoesNotContain(hosted, value => LegacyBuiltInHostedKeys.Contains(value.Key));

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

            // A revision-1 definition under a reserved key is still deliberate configuration
            // when any content differs from the historical bootstrap seed. Do not delete it
            // merely because it was created by the old bootstrap actor.
            var modifiedBootstrap = await hostedService.SetAsync(
                "builtin-wotc-srd-5-1",
                historicalSeed with { Note = "Locally modified bootstrap-owned source policy" },
                BootstrapActor);
            Assert.Equal(1, modifiedBootstrap.RevisionNumber);

            var revisionCount = await db.SourceEntityRevisions.CountAsync();
            var second = await bootstrapper.EnsureAsync();
            Assert.False(second.RulesBaselineApplied);
            Assert.Null(second.PublishedRuleset);
            Assert.Equal(1, second.HostedSourceDefinitionCount);
            Assert.Equal(revisionCount, await db.SourceEntityRevisions.CountAsync());
            Assert.Equal(1, await db.RulesetRevisions.CountAsync());
            Assert.Equal(2, await db.GlobalRuleDecisions.CountAsync(value => value.RuleConceptId == concept.Id));

            var preservedFirstRevision = await hostedService.ListAsync(includeDisabled: true);
            var retainedBootstrap = Assert.Single(
                preservedFirstRevision.Where(value => value.Key == "builtin-wotc-srd-5-1"));
            Assert.Equal(1, retainedBootstrap.RevisionNumber);
            Assert.Equal(BootstrapActor, retainedBootstrap.CreatedByUserId);
            Assert.Equal("Locally modified bootstrap-owned source policy", retainedBootstrap.Note);

            // A later Rules Lawyer revision is also deliberate policy and must remain intact.
            var revised = await hostedService.SetAsync(
                "builtin-wotc-srd-5-1",
                historicalSeed with { Note = "Rules Lawyer retained source policy" },
                "rules-lawyer");
            Assert.Equal(2, revised.RevisionNumber);

            var third = await bootstrapper.EnsureAsync();
            Assert.False(third.RulesBaselineApplied);
            Assert.Null(third.PublishedRuleset);
            Assert.Equal(1, third.HostedSourceDefinitionCount);
            Assert.Equal(revisionCount, await db.SourceEntityRevisions.CountAsync());
            Assert.Equal(1, await db.RulesetRevisions.CountAsync());
            Assert.Equal(2, await db.GlobalRuleDecisions.CountAsync(value => value.RuleConceptId == concept.Id));

            var preserved = await hostedService.ListAsync(includeDisabled: true);
            var retained = Assert.Single(preserved.Where(value => value.Key == "builtin-wotc-srd-5-1"));
            Assert.Equal(2, retained.RevisionNumber);
            Assert.Equal("rules-lawyer", retained.CreatedByUserId);
            Assert.Equal("Rules Lawyer retained source policy", retained.Note);
        }
        finally
        {
            await ResetAsync(db);
        }
    }

    private static SetHostedSourceDefinitionRequest HistoricalHostedDefinition(string definitionKey)
    {
        var type = typeof(RulesCoreBaselineBootstrapper).Assembly.GetType(
            "RulesCore.Infrastructure.Bootstrap.BuiltInSrdHostedSources",
            throwOnError: true)!;
        var field = type.GetField("Definitions", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Historical hosted SRD definitions are unavailable.");
        var definitions = (IEnumerable)(field.GetValue(null)
            ?? throw new InvalidOperationException("Historical hosted SRD definitions are unavailable."));

        foreach (var definition in definitions)
        {
            if (definition is null) continue;
            var definitionType = definition.GetType();
            var key = definitionType.GetProperty("Key")?.GetValue(definition) as string;
            if (!string.Equals(key, definitionKey, StringComparison.Ordinal)) continue;
            return (SetHostedSourceDefinitionRequest)(definitionType.GetProperty("Request")?.GetValue(definition)
                ?? throw new InvalidOperationException($"Historical hosted source '{definitionKey}' has no request."));
        }

        throw new InvalidOperationException($"Historical hosted source '{definitionKey}' was not found.");
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
