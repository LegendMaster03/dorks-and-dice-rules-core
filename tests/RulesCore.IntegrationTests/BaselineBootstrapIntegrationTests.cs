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
    private static readonly string[] BuiltInPackageKeys =
    [
        "wotc-srd-ogl",
        "wotc-srd-cc",
        "loot-tavern-free",
        "loot-tavern-licensed",
        "dorks-and-dice-baseline"
    ];

    [Fact]
    public async Task FreshDatabaseGetsSourceRegistryAndSettledRulesWithoutOverwritingLaterChanges()
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
        var importer = new SourceImportService(db);
        var globalRules = new GlobalRulesService(db);
        var hostedSources = new LegacyAwareHostedSourceService(db, importer);
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
            Assert.True(packages.Single(value => value.Key == "wotc-srd-ogl").IsPublic);
            Assert.Equal("OGL-1.0a", packages.Single(value => value.Key == "wotc-srd-ogl").License);
            Assert.True(packages.Single(value => value.Key == "wotc-srd-cc").IsPublic);
            Assert.Equal("CC-BY-4.0", packages.Single(value => value.Key == "wotc-srd-cc").License);

    var anonymousSourceSearch = new SourceEntitySearchService(db);
    foreach (var sourceCode in new[] { "SRD3", "SRD35", "SRD51", "SRD52" })
    {
        var visible = await anonymousSourceSearch.SearchAccessibleAsync(
            userId: null,
            query: sourceCode,
            limit: 1);
        Assert.NotEmpty(visible);
    }
            Assert.True(packages.Single(value => value.Key == "loot-tavern-free").IsPublic);
            Assert.False(packages.Single(value => value.Key == "loot-tavern-licensed").IsPublic);

            var oglPackageId = packages.Single(value => value.Key == "wotc-srd-ogl").Id;
            var ccPackageId = packages.Single(value => value.Key == "wotc-srd-cc").Id;
            var housePackageId = packages.Single(value => value.Key == "dorks-and-dice-baseline").Id;

            var oglWorks = await db.SourceWorks
                .AsNoTracking()
                .Where(value => value.SourcePackageId == oglPackageId)
                .Select(value => value.Key)
                .OrderBy(value => value)
                .ToArrayAsync();
            Assert.Equal(new[] { "srd-3-5e", "srd-3e" }, oglWorks);

            var ccWorks = await db.SourceWorks
                .AsNoTracking()
                .Where(value => value.SourcePackageId == ccPackageId)
                .Select(value => value.Key)
                .OrderBy(value => value)
                .ToArrayAsync();
            Assert.Equal(new[] { "srd-5-1", "srd-5-2-1" }, ccWorks);

            var authorities = await ReadAuthorityReferencesAsync(db);
            Assert.Equal(4, authorities.Count);
            Assert.Contains(authorities, value =>
                value.WorkKey == "srd-3e"
                && value.EditionKey == "original"
                && value.Uri == "https://web.archive.org/web/20080209011829/http://www.opengamingfoundation.org/srd.html"
                && value.MediaType == "text/html");
            Assert.Contains(authorities, value =>
                value.WorkKey == "srd-3-5e"
                && value.EditionKey == "original"
                && value.Uri == "https://web.archive.org/web/20160328013113/http://www.wizards.com/d20/files/v35/SRD.zip"
                && value.MediaType == "application/zip");
            Assert.Contains(authorities, value =>
                value.WorkKey == "srd-5-1"
                && value.EditionKey == "5.1"
                && value.Uri == "https://media.wizards.com/2023/downloads/dnd/SRD_CC_v5.1.pdf"
                && value.MediaType == "application/pdf");
            Assert.Contains(authorities, value =>
                value.WorkKey == "srd-5-2-1"
                && value.EditionKey == "5.2.1"
                && value.Uri == "https://media.dndbeyond.com/compendium-images/srd/5.2/SRD_CC_v5.2.1.pdf"
                && value.MediaType == "application/pdf");

            var definitions = await hostedSources.ListAsync(includeDisabled: true);
            var srd3 = Assert.Single(definitions, value => value.Key == "builtin-wotc-srd-3e");
            var srd35 = Assert.Single(definitions, value => value.Key == "builtin-wotc-srd-3-5e");
            var srd51 = Assert.Single(definitions, value => value.Key == "builtin-wotc-srd-5-1");
            var srd52 = Assert.Single(definitions, value => value.Key == "builtin-wotc-srd-5-2-1");
            Assert.Equal(HostedSourceFormatKinds.LegacySrdText, srd3.FormatKind);
            Assert.Equal(HostedSourceFormatKinds.LegacySrdText, srd35.FormatKind);
            Assert.Equal(new[] { "SRD3" }, srd3.IncludedSourceCodes);
            Assert.Equal(new[] { "SRD35" }, srd35.IncludedSourceCodes);
            Assert.Single(srd3.Resources);
            Assert.Equal(HostedSourceResourceKinds.HtmlIndex, srd3.Resources[0].Kind);
            Assert.Equal("https://www.dragon.ee/30srd/", srd3.Resources[0].Uri);
            Assert.Equal(7, srd35.Resources.Count);
            Assert.All(srd35.Resources, value => Assert.Equal(HostedSourceResourceKinds.GitHubTree, value.Kind));
            Assert.Contains(srd35.Resources, value => value.Uri.EndsWith("/basic-rules-and-legal", StringComparison.Ordinal));
            Assert.Contains(srd35.Resources, value => value.Uri.EndsWith("/spells", StringComparison.Ordinal));
            Assert.True(srd51.IsPublic);
            Assert.True(srd52.IsPublic);
            Assert.Equal(new[] { "SRD51" }, srd51.IncludedSourceCodes);
            Assert.Equal(new[] { "SRD52" }, srd52.IncludedSourceCodes);
            Assert.Contains(srd51.Resources, value => value.Uri.EndsWith("/spells/spells-srd51.json", StringComparison.Ordinal));
            Assert.Contains(srd52.Resources, value => value.Uri.EndsWith("/spells/spells-srd52.json", StringComparison.Ordinal));
            Assert.Contains(srd51.Resources, value => value.Uri.EndsWith("/bestiary/bestiary-srd51.json", StringComparison.Ordinal));
            Assert.Contains(srd52.Resources, value => value.Uri.EndsWith("/bestiary/bestiary-srd52.json", StringComparison.Ordinal));
            Assert.Contains(srd52.Resources, value => value.Uri.EndsWith("/feats.json", StringComparison.Ordinal));

            var houseEntities = await db.SourceEntities
                .AsNoTracking()
                .Where(value => value.SourceEdition.SourceWork.SourcePackageId == housePackageId)
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

            var revisedSrd52 = await hostedSources.SetAsync(
                srd52.Key,
                ToRequest(srd52) with
                {
                    IsEnabled = false,
                    Note = "Rules Lawyer intentionally disabled automatic SRD 5.2.1 refresh."
                },
                "rules-lawyer");
            Assert.Equal(2, revisedSrd52.RevisionNumber);

            var sourceRevisionCountBeforeRepeat = await db.SourceEntityRevisions.CountAsync();
            var second = await bootstrapper.EnsureAsync();
            Assert.Equal(sourceRevisionCountBeforeRepeat, await db.SourceEntityRevisions.CountAsync());
            Assert.False(second.RulesBaselineApplied);
            Assert.Null(second.PublishedRuleset);
            Assert.Equal(1, await db.RulesetRevisions.CountAsync());
            Assert.Equal(2, await db.GlobalRuleDecisions.CountAsync(
                value => value.RuleConceptId == healingConcept.Id));
            Assert.Equal(4, (await ReadAuthorityReferencesAsync(db)).Count);

            var preservedSrd52 = await hostedSources.GetAsync(srd52.Id);
            Assert.NotNull(preservedSrd52);
            Assert.False(preservedSrd52!.IsEnabled);
            Assert.Equal(2, preservedSrd52.RevisionNumber);

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

    private static SetHostedSourceDefinitionRequest ToRequest(HostedSourceDefinitionView definition) =>
        new(
            definition.DisplayName,
            definition.FormatKind,
            definition.PackageKey,
            definition.PackageDisplayName,
            definition.Provider,
            definition.License,
            definition.IsPublic,
            definition.WorkKey,
            definition.WorkDisplayName,
            definition.EditionKey,
            definition.EditionDisplayName,
            definition.GameEdition,
            definition.ReleaseKind,
            definition.PublicationDate,
            definition.IncludedSourceCodes,
            definition.Resources.Select(value => new HostedSourceResourceRequest(value.Kind, value.Uri)).ToArray(),
            definition.IsEnabled,
            definition.Note);

    private static async Task<IReadOnlyList<AuthorityReferenceRow>> ReadAuthorityReferencesAsync(
        RulesCoreDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT w.work_key, e.edition_key, r.uri, r.media_type
                FROM source_edition_authority_reference r
                JOIN source_edition e ON e.source_edition_id = r.source_edition_id
                JOIN source_work w ON w.source_work_id = e.source_work_id
                ORDER BY w.work_key, e.edition_key, r.uri;
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
            if (openedHere)
            {
                await connection.CloseAsync();
            }
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
            CREATE TABLE IF NOT EXISTS source_edition_authority_reference (
                source_edition_authority_reference_id uuid NOT NULL,
                source_edition_id uuid NOT NULL,
                authority_kind varchar(80) NOT NULL,
                uri varchar(2000) NOT NULL,
                media_type varchar(200) NOT NULL,
                note varchar(2000) NULL,
                created_at timestamp with time zone NOT NULL,
                CONSTRAINT pk_source_edition_authority_reference PRIMARY KEY (source_edition_authority_reference_id),
                CONSTRAINT fk_source_edition_authority_reference_edition FOREIGN KEY (source_edition_id)
                    REFERENCES source_edition(source_edition_id) ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS hosted_source_definition (
                hosted_source_definition_id uuid NOT NULL,
                definition_key varchar(200) NOT NULL,
                created_at timestamp with time zone NOT NULL,
                CONSTRAINT pk_hosted_source_definition PRIMARY KEY (hosted_source_definition_id));
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
        string WorkKey,
        string EditionKey,
        string Uri,
        string MediaType);
}
