using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class LegacyCrossEditionResolutionIntegrationTests
{
    [Fact]
    public async Task RulesLawyerCanResolveBetweenThreeEAndThreeFiveSourcesExplicitly()
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
        await ResetRulesAsync(db);

        var packageKey = $"legacy-resolver-{Guid.NewGuid():N}";
        var conceptKey = $"feat.power-attack.legacy.{Guid.NewGuid():N}";
        var importer = new SourceImportService(db);
        var globalRules = new GlobalRulesService(db);

        try
        {
            var (threeEntity, threeRevisionId, threeFiveEntity, threeFiveRevisionId) =
                await ImportPowerAttackPairAsync(db, importer, packageKey);

            var concept = (await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(conceptKey, "feat", "Power Attack"),
                "rules-lawyer")).Value;
            await globalRules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(threeEntity.EntityId),
                "rules-lawyer");
            await globalRules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(threeFiveEntity.EntityId),
                "rules-lawyer");

            await globalRules.SetDecisionAsync(
                concept.Id,
                new SetGlobalRuleDecisionRequest(
                    threeFiveRevisionId,
                    "Use the 3.5e Power Attack wording for this global rule."),
                "rules-lawyer");
            var firstPublication = await globalRules.PublishAsync("rules-lawyer");
            Assert.Equal(1, firstPublication.RevisionNumber);

            var resolvedThreeFive = await globalRules.ResolveLatestAsync(conceptKey, null);
            Assert.NotNull(resolvedThreeFive);
            Assert.Equal("SRD35", resolvedThreeFive!.SourceCode);
            Assert.Equal("srd-3-5e", resolvedThreeFive.WorkKey);
            Assert.Equal("3.5e SRD", resolvedThreeFive.EditionDisplayName);
            Assert.Contains(
                "3.5e rule text",
                resolvedThreeFive.Document.GetProperty("body").GetString(),
                StringComparison.Ordinal);

            await globalRules.SetDecisionAsync(
                concept.Id,
                new SetGlobalRuleDecisionRequest(
                    threeRevisionId,
                    "Switch explicitly to the 3e Power Attack wording."),
                "rules-lawyer");
            var secondPublication = await globalRules.PublishAsync("rules-lawyer");
            Assert.Equal(2, secondPublication.RevisionNumber);

            var resolvedThree = await globalRules.ResolveLatestAsync(conceptKey, null);
            Assert.NotNull(resolvedThree);
            Assert.Equal("SRD3", resolvedThree!.SourceCode);
            Assert.Equal("srd-3e", resolvedThree.WorkKey);
            Assert.Equal("3e SRD", resolvedThree.EditionDisplayName);
            Assert.Contains(
                "3e rule text",
                resolvedThree.Document.GetProperty("body").GetString(),
                StringComparison.Ordinal);
        }
        finally
        {
            await CleanupAsync(db, packageKey);
        }
    }

    [Fact]
    public async Task RulesLawyerCanConsolidateThreeEIntoThreeFiveWithProvenance()
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
        await ResetRulesAsync(db);

        var packageKey = $"legacy-consolidation-{Guid.NewGuid():N}";
        var conceptKey = $"feat.power-attack.consolidated.{Guid.NewGuid():N}";
        var importer = new SourceImportService(db);
        var globalRules = new GlobalRulesService(db);

        try
        {
            var (threeEntity, threeRevisionId, threeFiveEntity, threeFiveRevisionId) =
                await ImportPowerAttackPairAsync(db, importer, packageKey);

            var concept = (await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(conceptKey, "feat", "Power Attack"),
                "rules-lawyer")).Value;
            await globalRules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(threeEntity.EntityId),
                "rules-lawyer");
            await globalRules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(threeFiveEntity.EntityId),
                "rules-lawyer");

            using var patchDocument = JsonDocument.Parse("""
                {
                  "crossEditionCompatibility": {
                    "baseEdition": "3.5e",
                    "incorporatedEdition": "3e",
                    "strategy": "additive-reviewed"
                  }
                }
                """);
            var decision = await globalRules.SetDecisionAsync(
                concept.Id,
                new SetGlobalRuleDecisionRequest(
                    threeFiveRevisionId,
                    "Use 3.5e as the base while retaining reviewed compatible 3e material.",
                    MergePatch: patchDocument.RootElement.Clone(),
                    Contributions:
                    [
                        new RuleConsolidationContributionRequest(
                            threeRevisionId,
                            RuleConsolidationContributionKinds.Incorporated,
                            "3e Power Attack was reviewed and incorporated into the hybrid adjudication.")
                    ]),
                "rules-lawyer");
            Assert.True(decision.Created);

            var publication = await globalRules.PublishAsync("rules-lawyer");
            Assert.Equal(1, publication.RevisionNumber);

            var resolved = await globalRules.ResolveLatestAsync(conceptKey, null);
            Assert.NotNull(resolved);
            Assert.Equal("SRD35", resolved!.SourceCode);
            Assert.Equal("3.5e", resolved.Document
                .GetProperty("crossEditionCompatibility")
                .GetProperty("baseEdition")
                .GetString());
            Assert.Equal("3e", resolved.Document
                .GetProperty("crossEditionCompatibility")
                .GetProperty("incorporatedEdition")
                .GetString());

            var resolvedContribution = Assert.Single(resolved.Contributions);
            Assert.Equal(threeRevisionId, resolvedContribution.SourceEntityRevisionId);
            Assert.Equal("SRD3", resolvedContribution.SourceCode);
            Assert.Equal("srd-3e", resolvedContribution.WorkKey);
            Assert.Equal("3e", resolvedContribution.GameEdition);
            Assert.Equal(
                RuleConsolidationContributionKinds.Incorporated,
                resolvedContribution.ContributionKind);

            var versioning = new SourceVersioningService(db);
            var authoring = new GlobalRulesAuthoringService(db);
            var consolidation = new RuleConsolidationService(db, authoring, versioning);
            var view = await consolidation.GetAsync(concept.Id, "rules-lawyer");
            Assert.NotNull(view);
            var contribution = Assert.Single(view!.LatestContributions);
            Assert.Equal(threeRevisionId, contribution.SourceEntityRevisionId);
            Assert.Equal("SRD3", contribution.SourceCode);
            Assert.Equal("3e", contribution.GameEdition);
            Assert.Equal(RuleConsolidationContributionKinds.Incorporated, contribution.ContributionKind);
        }
        finally
        {
            await CleanupAsync(db, packageKey);
        }
    }

    private static async Task<(
        ImportedSourceEntity ThreeEntity,
        Guid ThreeRevisionId,
        ImportedSourceEntity ThreeFiveEntity,
        Guid ThreeFiveRevisionId)> ImportPowerAttackPairAsync(
        RulesCoreDbContext db,
        SourceImportService importer,
        string packageKey)
    {
        var threeJson = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            """
                <html><body>
                <h1>Feats</h1>
                <h2>Power Attack [General]</h2>
                <p><strong>Prerequisite:</strong> Str 13.</p>
                <p><strong>Benefit:</strong> 3e rule text: trade attack bonus for damage.</p>
                </body></html>
                """,
            "https://www.dragon.ee/30srd/feats.htm",
            "SRD3",
            out _);
        var threeFiveJson = LegacySrdDocumentInspector.ConvertToCanonicalJson(
            """
                # FEATS

                ## Feat Descriptions

                ### Power Attack <small>[General]</small>

                **Prerequisite:** Str 13.

                **Benefit:** 3.5e rule text: subtract from melee attack rolls and add to melee damage rolls.
                """,
            "https://example.test/basic-rules-and-legal/feats.md",
            "SRD35",
            out _);

        var threeImport = await importer.Import5eToolsDocumentAsync(
            LegacyImportRequest(packageKey, "srd-3e", "3e SRD", "3e", threeJson));
        var threeFiveImport = await importer.Import5eToolsDocumentAsync(
            LegacyImportRequest(packageKey, "srd-3-5e", "3.5e SRD", "3.5e", threeFiveJson));

        var threeEntity = Assert.Single(
            threeImport.Entities,
            value => value.EntityType == "feat" && value.Name == "Power Attack");
        var threeFiveEntity = Assert.Single(
            threeFiveImport.Entities,
            value => value.EntityType == "feat" && value.Name == "Power Attack");
        return (
            threeEntity,
            await LatestRevisionIdAsync(db, threeEntity.EntityId),
            threeFiveEntity,
            await LatestRevisionIdAsync(db, threeFiveEntity.EntityId));
    }

    private static Import5eToolsDocumentRequest LegacyImportRequest(
        string packageKey,
        string workKey,
        string editionDisplayName,
        string gameEdition,
        string json) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: "Legacy Resolver Integration Package",
            Provider: "integration-test",
            License: "OGL-1.0a",
            IsPublic: true,
            WorkKey: workKey,
            WorkDisplayName: editionDisplayName,
            EditionKey: "original",
            EditionDisplayName: editionDisplayName,
            Json: json,
            GameEdition: gameEdition,
            ReleaseKind: "srd",
            PublicationDate: null);

    private static Task<Guid> LatestRevisionIdAsync(RulesCoreDbContext db, Guid entityId) =>
        db.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => value.SourceEntityId == entityId)
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => value.Id)
            .FirstAsync();

    private static async Task CleanupAsync(RulesCoreDbContext db, string packageKey)
    {
        await ResetRulesAsync(db);
        db.ChangeTracker.Clear();
        var packages = await db.SourcePackages
            .Where(value => value.Key == packageKey)
            .ToArrayAsync();
        db.SourcePackages.RemoveRange(packages);
        await db.SaveChangesAsync();
    }

    private static async Task ResetRulesAsync(RulesCoreDbContext db)
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
        db.ChangeTracker.Clear();
    }
}
