using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Bootstrap;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class BundledSrdMaintenanceIntegrationTests
{
    private static readonly string[] BuiltInPackageKeys =
    ["wotc-srd-ogl", "wotc-srd-cc", "loot-tavern-free", "loot-tavern-licensed", "dorks-and-dice-baseline"];

    [Fact]
    public async Task ReprocessBundledSrdPreservesRulesLayerDecisionAndPatch()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>()
                .UseNpgsql(connectionString)
                .Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        var globalRules = new GlobalRulesService(db);
        var maintenance = new BundledSrdMaintenanceService(db);

        await ResetAsync(db);
        try
        {
            var available = maintenance.List();
            Assert.Equal(4, available.Count);
            Assert.Contains(available, value => value.WorkKey == "srd-3e");
            Assert.Contains(available, value => value.WorkKey == "srd-3-5e");
            Assert.Contains(available, value => value.WorkKey == "srd-5-1");
            Assert.Contains(available, value => value.WorkKey == "srd-5-2-1");

            // Seed only the SRD this test needs. Running the complete baseline bootstrap here used
            // to import all four large bundled corpora before immediately reprocessing one of
            // them, adding several minutes to every CI run without increasing coverage of the
            // preservation invariant being tested.
            var seeded = await maintenance.ReprocessAsync("srd-5-1");
            Assert.True(seeded.ProcessedEntityCount > 0);
            Assert.True(seeded.CreatedNativeRevisionCount > 0);

            var sourceEntity = await db.SourceEntities
                .Where(value => value.SourceCode == "SRD51")
                .OrderBy(value => value.EntityType)
                .ThenBy(value => value.Name)
                .FirstAsync();
            var sourceRevision = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == sourceEntity.Id)
                .OrderByDescending(value => value.RevisionNumber)
                .FirstAsync();

            var conceptKey = $"maintenance.reprocess.{Guid.NewGuid():N}";
            var concept = (await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(
                    conceptKey,
                    sourceEntity.EntityType,
                    $"Maintenance test: {sourceEntity.Name}"),
                "rules-lawyer")).Value;
            await globalRules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(sourceEntity.Id),
                "rules-lawyer");

            using var patchDocument = JsonDocument.Parse("""
                {
                  "rulesCoreMaintenanceTest": true
                }
                """);
            var decision = await globalRules.SetDecisionAsync(
                concept.Id,
                new SetGlobalRuleDecisionRequest(
                    sourceRevision.Id,
                    "Keep this Rules Layer modification while the bundled SRD is reprocessed.",
                    patchDocument.RootElement.Clone()),
                "rules-lawyer");
            Assert.True(decision.Created);
            await globalRules.PublishAsync("rules-lawyer");

            var before = await globalRules.ResolveLatestAsync(conceptKey, userId: null);
            Assert.NotNull(before);
            Assert.True(before.Document.GetProperty("rulesCoreMaintenanceTest").GetBoolean());

            var decisionCountBefore = await db.GlobalRuleDecisions
                .CountAsync(value => value.RuleConceptId == concept.Id);
            var revisionCountBefore = await db.SourceEntityRevisions
                .CountAsync(value => value.SourceEntityId == sourceEntity.Id);
            var patchFingerprintBefore = decision.Value.PatchFingerprint;

            var reprocessed = await maintenance.ReprocessAsync("srd-5-1");

            Assert.True(reprocessed.ProcessedEntityCount > 0);
            Assert.Equal(0, reprocessed.CreatedNativeRevisionCount);
            Assert.Equal(reprocessed.ProcessedEntityCount, reprocessed.PreservedNativeRevisionCount);
            Assert.Equal(0, reprocessed.ReconciliationIssueCount);

            Assert.True(await db.SourceEntities.AnyAsync(value => value.Id == sourceEntity.Id));
            Assert.True(await db.SourceEntityRevisions.AnyAsync(value => value.Id == sourceRevision.Id));
            Assert.Equal(
                revisionCountBefore,
                await db.SourceEntityRevisions.CountAsync(value => value.SourceEntityId == sourceEntity.Id));
            Assert.Equal(
                decisionCountBefore,
                await db.GlobalRuleDecisions.CountAsync(value => value.RuleConceptId == concept.Id));

            var persistedDecision = await db.GlobalRuleDecisions
                .Where(value => value.Id == decision.Value.Id)
                .SingleAsync();
            Assert.Equal(sourceRevision.Id, persistedDecision.SelectedSourceEntityRevisionId);
            Assert.Equal(patchFingerprintBefore, persistedDecision.PatchFingerprint);

            var after = await globalRules.ResolveLatestAsync(conceptKey, userId: null);
            Assert.NotNull(after);
            Assert.True(after.Document.GetProperty("rulesCoreMaintenanceTest").GetBoolean());
            Assert.Equal(patchFingerprintBefore, after.GlobalPatchFingerprint);
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
}
