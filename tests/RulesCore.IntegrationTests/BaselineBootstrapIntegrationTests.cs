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
    [
        "wotc-srd-ogl",
        "wotc-srd-cc",
        "loot-tavern-free",
        "loot-tavern-licensed",
        "dorks-and-dice-baseline"
    ];

    [Fact]
    public async Task FreshDatabaseGetsPublicSrdsAndSettledRulesWithoutOverwritingLaterChanges()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);
        await using (db)
        {
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
                    .OrderBy(value => value.Key)
                    .ToArrayAsync();
                Assert.Equal(5, packages.Length);

                var ogl = packages.Single(value => value.Key == "wotc-srd-ogl");
                var cc = packages.Single(value => value.Key == "wotc-srd-cc");
                var house = packages.Single(value => value.Key == "dorks-and-dice-baseline");
                Assert.True(ogl.IsPublic);
                Assert.Equal("OGL-1.0a", ogl.License);
                Assert.True(cc.IsPublic);
                Assert.Equal("CC-BY-4.0", cc.License);
                Assert.True(packages.Single(value => value.Key == "loot-tavern-free").IsPublic);
                Assert.False(packages.Single(value => value.Key == "loot-tavern-licensed").IsPublic);

                var anonymousSearch = new SourceEntitySearchService(db);
                foreach (var sourceCode in new[] { "SRD3", "SRD35", "SRD51", "SRD52" })
                {
                    var visible = await anonymousSearch.SearchAccessibleAsync(null, sourceCode, 1);
                    Assert.NotEmpty(visible);
                }

                var oglCodes = await db.SourceEntities
                    .AsNoTracking()
                    .Where(value => value.SourcePackageId == ogl.Id)
                    .Select(value => value.SourceCode)
                    .Distinct()
                    .ToArrayAsync();
                Assert.Contains("SRD3", oglCodes);
                Assert.Contains("SRD35", oglCodes);

                var ccCodes = await db.SourceEntities
                    .AsNoTracking()
                    .Where(value => value.SourcePackageId == cc.Id)
                    .Select(value => value.SourceCode)
                    .Distinct()
                    .ToArrayAsync();
                Assert.Contains("SRD51", ccCodes);
                Assert.Contains("SRD52", ccCodes);

                var authorities = await ReadAuthorityReferencesAsync(db);
                Assert.Equal(4, authorities.Count);
                Assert.Contains(authorities, value =>
                    value.PackageKey == "wotc-srd-ogl"
                    && value.PublicationKey == "srd-3e"
                    && value.Uri == "https://web.archive.org/web/20080209011829/http://www.opengamingfoundation.org/srd.html"
                    && value.MediaType == "text/html");
                Assert.Contains(authorities, value =>
                    value.PackageKey == "wotc-srd-ogl"
                    && value.PublicationKey == "srd-3-5e"
                    && value.Uri == "https://web.archive.org/web/20160328013113/http://www.wizards.com/d20/files/v35/SRD.zip"
                    && value.MediaType == "application/zip");
                Assert.Contains(authorities, value =>
                    value.PackageKey == "wotc-srd-cc"
                    && value.PublicationKey == "srd-5-1"
                    && value.Uri == "https://media.wizards.com/2023/downloads/dnd/SRD_CC_v5.1.pdf");
                Assert.Contains(authorities, value =>
                    value.PackageKey == "wotc-srd-cc"
                    && value.PublicationKey == "srd-5-2-1"
                    && value.Uri == "https://media.dndbeyond.com/compendium-images/srd/5.2/SRD_CC_v5.2.1.pdf");

                var houseEntities = await db.SourceEntities
                    .AsNoTracking()
                    .Where(value => value.SourcePackageId == house.Id)
                    .OrderBy(value => value.Name)
                    .ToArrayAsync();
                Assert.Equal(6, houseEntities.Length);
                Assert.All(houseEntities, value => Assert.Equal("DDBASE", value.SourceCode));

                Assert.Equal(6, await db.RuleConcepts.CountAsync());
                Assert.Equal(6, await db.RuleConceptSourceBindings.CountAsync());
                Assert.Equal(6, await db.GlobalRuleDecisions.CountAsync());
                Assert.Equal(1, await db.RulesetRevisions.CountAsync());

                var healing = await globalRules.ResolveLatestAsync("house.healing-potion-use", null);
                Assert.NotNull(healing);
                Assert.Equal(
                    "maximum possible healing",
                    healing!.Document.GetProperty("action").GetProperty("healing").GetString());

                var healingConcept = await db.RuleConcepts
                    .SingleAsync(value => value.Key == "house.healing-potion-use");
                var currentHealingDecision = await db.GlobalRuleDecisions
                    .AsNoTracking()
                    .Where(value => value.RuleConceptId == healingConcept.Id)
                    .OrderByDescending(value => value.DecisionNumber)
                    .FirstAsync();
                await globalRules.SetDecisionAsync(
                    healingConcept.Id,
                    new SetGlobalRuleDecisionRequest(
                        currentHealingDecision.SelectedSourceEntityRevisionId,
                        "Rules Lawyer retained the baseline implementation with a user-maintained note."),
                    "rules-lawyer");

                var revisionCountBeforeRepeat = await db.SourceEntityRevisions.CountAsync();
                var second = await bootstrapper.EnsureAsync();
                Assert.Equal(revisionCountBeforeRepeat, await db.SourceEntityRevisions.CountAsync());
                Assert.False(second.RulesBaselineApplied);
                Assert.Null(second.PublishedRuleset);
                Assert.Equal(1, await db.RulesetRevisions.CountAsync());
                Assert.Equal(2, await db.GlobalRuleDecisions.CountAsync(
                    value => value.RuleConceptId == healingConcept.Id));
                Assert.Equal(4, (await ReadAuthorityReferencesAsync(db)).Count);

                var latestHealingDecision = await db.GlobalRuleDecisions
                    .AsNoTracking()
                    .Where(value => value.RuleConceptId == healingConcept.Id)
                    .OrderByDescending(value => value.DecisionNumber)
                    .FirstAsync();
                Assert.Equal("rules-lawyer", latestHealingDecision.CreatedByUserId);
                Assert.Equal(
                    "Rules Lawyer retained the baseline implementation with a user-maintained note.",
                    latestHealingDecision.Note);
            }
            finally
            {
                await ResetAsync(db);
            }
        }
    }

    private static async Task<IReadOnlyList<AuthorityReferenceRow>> ReadAuthorityReferencesAsync(
        RulesCoreDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT p.package_key, r.publication_key, r.uri, r.media_type
                FROM source_package_authority_reference r
                JOIN source_package p ON p.source_package_id = r.source_package_id
                ORDER BY p.package_key, r.publication_key, r.uri;
                """;
            var results = new List<AuthorityReferenceRow>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new AuthorityReferenceRow(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3)));
            }
            return results;
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
            WHERE definition_key IN (
                'builtin-wotc-srd-3e',
                'builtin-wotc-srd-3-5e',
                'builtin-wotc-srd-5-1',
                'builtin-wotc-srd-5-2-1');
            """);

        db.ChangeTracker.Clear();
        var packages = await db.SourcePackages
            .Where(value => BuiltInPackageKeys.Contains(value.Key))
            .ToArrayAsync();
        db.SourcePackages.RemoveRange(packages);
        await db.SaveChangesAsync();
    }

    private sealed record AuthorityReferenceRow(
        string PackageKey,
        string PublicationKey,
        string Uri,
        string MediaType);
}
