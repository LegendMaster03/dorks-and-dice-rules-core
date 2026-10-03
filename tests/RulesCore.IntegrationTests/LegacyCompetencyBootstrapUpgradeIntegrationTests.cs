using Microsoft.EntityFrameworkCore;
using RulesCore.Infrastructure.Bootstrap;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class LegacyCompetencyBootstrapUpgradeIntegrationTests
{
    private static readonly string[] BuiltInPackageKeys =
    ["wotc-srd-ogl", "wotc-srd-cc", "loot-tavern-free", "loot-tavern-licensed", "dorks-and-dice-baseline"];

    [Fact]
    public async Task BootstrapPreservesReviewedLegacyCrossTypeBindingsAndCreatesCorrectedConcepts()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>()
                .UseNpgsql(connectionString)
                .Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        var importer = new SourceImportService(db);
        var globalRules = new GlobalRulesService(db);
        var bootstrapper = new RulesCoreBaselineBootstrapper(db, importer, globalRules);

        await ResetAsync(db);
        try
        {
            var initial = await bootstrapper.EnsureAsync();
            Assert.True(initial.RulesBaselineApplied);

            var migrations = new[]
            {
                new LegacyBindingFixture(
                    "skill.alchemy",
                    "tool.alchemists-supplies",
                    "Alchemist's Supplies"),
                new LegacyBindingFixture(
                    "skill.forgery",
                    "tool.forgery-kit",
                    "Forgery Kit")
            };

            var preserved = new List<PreservedLegacyBinding>();
            foreach (var migration in migrations)
            {
                var concept = await db.RuleConcepts
                    .SingleAsync(value => value.Key == migration.CorrectedKey);
                var bindingIds = await db.RuleConceptSourceBindings
                    .Where(value => value.RuleConceptId == concept.Id)
                    .Select(value => value.Id)
                    .OrderBy(value => value)
                    .ToArrayAsync();
                var canonicalIds = await db.RuleConceptSourceBindings
                    .Where(value => value.RuleConceptId == concept.Id)
                    .Select(value => value.CanonicalEntityId)
                    .Distinct()
                    .OrderBy(value => value)
                    .ToArrayAsync();
                var decisionIds = await db.GlobalRuleDecisions
                    .Where(value => value.RuleConceptId == concept.Id)
                    .Select(value => value.Id)
                    .OrderBy(value => value)
                    .ToArrayAsync();

                Assert.NotEmpty(bindingIds);
                Assert.NotEmpty(canonicalIds);
                Assert.NotEmpty(decisionIds);

                preserved.Add(new PreservedLegacyBinding(
                    migration,
                    concept.Id,
                    bindingIds,
                    canonicalIds,
                    decisionIds));

                // Recreate the production upgrade state left by the earlier reviewed direct
                // cross-type normalization. The source/canonical lineage has already moved to
                // the corrected skill facet, while the existing Rule Concept identity must stay
                // stable for compatibility with persisted Character references and decisions.
                concept.Key = migration.LegacyKey;
                concept.EntityType = "tool";
                concept.DisplayName = migration.LegacyDisplayName;
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var upgraded = await bootstrapper.EnsureAsync();
            Assert.True(upgraded.RulesBaselineApplied);
            Assert.NotNull(upgraded.PublishedRuleset);

            foreach (var prior in preserved)
            {
                var legacy = await db.RuleConcepts
                    .AsNoTracking()
                    .SingleAsync(value => value.Key == prior.Fixture.LegacyKey);
                Assert.Equal(prior.ConceptId, legacy.Id);
                Assert.Equal("tool", legacy.EntityType);
                Assert.Equal(prior.Fixture.LegacyDisplayName, legacy.DisplayName);

                Assert.Equal(
                    prior.BindingIds,
                    await db.RuleConceptSourceBindings
                        .AsNoTracking()
                        .Where(value => value.RuleConceptId == legacy.Id)
                        .Select(value => value.Id)
                        .OrderBy(value => value)
                        .ToArrayAsync());
                Assert.Equal(
                    prior.DecisionIds,
                    await db.GlobalRuleDecisions
                        .AsNoTracking()
                        .Where(value => value.RuleConceptId == legacy.Id)
                        .Select(value => value.Id)
                        .OrderBy(value => value)
                        .ToArrayAsync());

                var corrected = await db.RuleConcepts
                    .AsNoTracking()
                    .SingleAsync(value => value.Key == prior.Fixture.CorrectedKey);
                Assert.NotEqual(legacy.Id, corrected.Id);
                Assert.Equal("skill", corrected.EntityType);

                var correctedCanonicalIds = await db.RuleConceptSourceBindings
                    .AsNoTracking()
                    .Where(value => value.RuleConceptId == corrected.Id)
                    .Select(value => value.CanonicalEntityId)
                    .Distinct()
                    .ToArrayAsync();
                Assert.NotEmpty(correctedCanonicalIds);
                Assert.True(correctedCanonicalIds.Intersect(prior.CanonicalIds).Any());
                Assert.True(await db.GlobalRuleDecisions
                    .AsNoTracking()
                    .AnyAsync(value => value.RuleConceptId == corrected.Id));
            }

            var conceptCount = await db.RuleConcepts.CountAsync();
            var bindingCount = await db.RuleConceptSourceBindings.CountAsync();
            var decisionCount = await db.GlobalRuleDecisions.CountAsync();
            var rulesetRevisionCount = await db.RulesetRevisions.CountAsync();

            var rerun = await bootstrapper.EnsureAsync();
            Assert.False(rerun.RulesBaselineApplied);
            Assert.Null(rerun.PublishedRuleset);
            Assert.Equal(conceptCount, await db.RuleConcepts.CountAsync());
            Assert.Equal(bindingCount, await db.RuleConceptSourceBindings.CountAsync());
            Assert.Equal(decisionCount, await db.GlobalRuleDecisions.CountAsync());
            Assert.Equal(rulesetRevisionCount, await db.RulesetRevisions.CountAsync());
        }
        finally
        {
            await ResetAsync(db);
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
            DO $$
            BEGIN
                IF to_regclass('public.hosted_source_definition') IS NOT NULL THEN
                    DELETE FROM hosted_source_definition
                    WHERE definition_key IN ('builtin-wotc-srd-3e','builtin-wotc-srd-3-5e','builtin-wotc-srd-5-1','builtin-wotc-srd-5-2-1');
                END IF;
            END
            $$;
            """);
        db.ChangeTracker.Clear();
        var packages = await db.SourcePackages
            .Where(value => BuiltInPackageKeys.Contains(value.Key))
            .ToArrayAsync();
        db.SourcePackages.RemoveRange(packages);
        await db.SaveChangesAsync();
    }

    private sealed record LegacyBindingFixture(
        string CorrectedKey,
        string LegacyKey,
        string LegacyDisplayName);

    private sealed record PreservedLegacyBinding(
        LegacyBindingFixture Fixture,
        Guid ConceptId,
        Guid[] BindingIds,
        Guid[] CanonicalIds,
        Guid[] DecisionIds);
}
