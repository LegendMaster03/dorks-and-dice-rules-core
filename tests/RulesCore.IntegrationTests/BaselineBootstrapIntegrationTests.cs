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
        var hostedSources = new HostedSourceService(db, importer);
        var bootstrapper = new RulesCoreBaselineBootstrapper(db, importer, globalRules);

        await ResetAsync(db, hostedSources);
        try
        {
            var first = await bootstrapper.EnsureAsync();

            Assert.True(first.RulesBaselineApplied);
            Assert.NotNull(first.PublishedRuleset);
            Assert.Equal(1, first.PublishedRuleset!.RevisionNumber);
            Assert.Equal(6, first.PublishedRuleset.EntryCount);
            Assert.Equal(6, first.HouseRuleSourceEntityCount);

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
            Assert.True(packages.Single(value => value.Key == "loot-tavern-free").IsPublic);
            Assert.False(packages.Single(value => value.Key == "loot-tavern-licensed").IsPublic);

            var oglWorks = await db.SourceWorks
                .AsNoTracking()
                .Where(value => value.SourcePackageId == packages.Single(package => package.Key == "wotc-srd-ogl").Id)
                .Select(value => value.Key)
                .OrderBy(value => value)
                .ToArrayAsync();
            Assert.Equal(new[] { "srd-3-5e", "srd-3e" }, oglWorks);

            var ccWorks = await db.SourceWorks
                .AsNoTracking()
                .Where(value => value.SourcePackageId == packages.Single(package => package.Key == "wotc-srd-cc").Id)
                .Select(value => value.Key)
                .OrderBy(value => value)
                .ToArrayAsync();
            Assert.Equal(new[] { "srd-5-1", "srd-5-2-1" }, ccWorks);

            var definitions = await hostedSources.ListAsync(includeDisabled: true);
            var builtIns = definitions
                .Where(value => value.Key.StartsWith("builtin-wotc-srd-", StringComparison.Ordinal))
                .OrderBy(value => value.Key)
                .ToArray();
            Assert.Equal(2, builtIns.Length);
            var srd51 = builtIns.Single(value => value.Key == "builtin-wotc-srd-5-1");
            var srd52 = builtIns.Single(value => value.Key == "builtin-wotc-srd-5-2-1");
            Assert.Equal(new[] { "SRD51" }, srd51.IncludedSourceCodes);
            Assert.Equal(new[] { "SRD52" }, srd52.IncludedSourceCodes);
            Assert.Contains(srd51.Resources, value => value.Uri.EndsWith("/feats.json", StringComparison.Ordinal));
            Assert.DoesNotContain(srd52.Resources, value => value.Uri.EndsWith("/feats.json", StringComparison.Ordinal));

            var housePackage = packages.Single(value => value.Key == "dorks-and-dice-baseline");
            var houseEntities = await db.SourceEntities
                .AsNoTracking()
                .Where(value => value.SourceEdition.SourceWork.SourcePackageId == housePackage.Id)
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

            var editedDefinition = await hostedSources.SetAsync(
                srd52.Key,
                ToRequest(srd52) with
                {
                    IsEnabled = false,
                    Note = "Rules Lawyer intentionally disabled automatic SRD 5.2.1 refresh."
                },
                "rules-lawyer");
            Assert.True(editedDefinition.CreatedRevision);
            Assert.Equal(2, editedDefinition.RevisionNumber);

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

            var second = await bootstrapper.EnsureAsync();
            Assert.False(second.RulesBaselineApplied);
            Assert.Null(second.PublishedRuleset);
            Assert.Equal(1, await db.RulesetRevisions.CountAsync());
            Assert.Equal(2, await db.GlobalRuleDecisions.CountAsync(
                value => value.RuleConceptId == healingConcept.Id));

            var preservedDefinition = await hostedSources.GetAsync(srd52.Id);
            Assert.NotNull(preservedDefinition);
            Assert.False(preservedDefinition!.IsEnabled);
            Assert.Equal(2, preservedDefinition.RevisionNumber);
            Assert.Equal(
                "Rules Lawyer intentionally disabled automatic SRD 5.2.1 refresh.",
                preservedDefinition.Note);

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
            await ResetAsync(db, hostedSources);
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
            definition.Resources
                .Select(value => new HostedSourceResourceRequest(value.Kind, value.Uri))
                .ToArray(),
            definition.IsEnabled,
            definition.Note);

    private static async Task ResetAsync(
        RulesCoreDbContext db,
        HostedSourceService hostedSources)
    {
        _ = await hostedSources.ListAsync(includeDisabled: true);

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
            WHERE definition_key LIKE 'builtin-wotc-srd-%';
            """);

        var packages = await db.SourcePackages
            .Where(value => BuiltInPackageKeys.Contains(value.Key))
            .ToArrayAsync();
        db.SourcePackages.RemoveRange(packages);
        await db.SaveChangesAsync();
    }
}
