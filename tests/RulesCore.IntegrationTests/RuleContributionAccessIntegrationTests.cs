using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class RuleContributionAccessIntegrationTests
{
    [Fact]
    public async Task AppliedRestrictedContributionRequiresGrantButCampaignReplacementDoesNot()
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

        var token = Guid.NewGuid().ToString("N");
        var publicPackageKey = $"contribution-public-{token}";
        var restrictedPackageKey = $"contribution-restricted-{token}";
        var conceptKey = $"feat.contribution-access-{token}";
        var campaignId = Guid.NewGuid();
        var importer = new SourceImportService(db);
        var grants = new SourceGrantService(db);
        var globalRules = new GlobalRulesService(db);
        var campaignRules = new CampaignRulesService(db);

        try
        {
            var publicImport = await importer.Import5eToolsDocumentAsync(
                ImportRequest(
                    publicPackageKey,
                    isPublic: true,
                    workKey: "public-work",
                    sourceCode: "PUBLIC",
                    marker: "public-base"));
            var restrictedImport = await importer.Import5eToolsDocumentAsync(
                ImportRequest(
                    restrictedPackageKey,
                    isPublic: false,
                    workKey: "restricted-work",
                    sourceCode: "RESTRICTED",
                    marker: "restricted-contribution"));

            var publicEntity = Assert.Single(publicImport.Entities);
            var restrictedEntity = Assert.Single(restrictedImport.Entities);
            var publicRevisionId = await LatestRevisionIdAsync(db, publicEntity.EntityId);
            var restrictedRevisionId = await LatestRevisionIdAsync(db, restrictedEntity.EntityId);

            var concept = (await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(conceptKey, "feat", "Contribution Access"),
                "rules-lawyer")).Value;
            await globalRules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(publicEntity.EntityId),
                "rules-lawyer");
            await globalRules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(restrictedEntity.EntityId),
                "rules-lawyer");

            await grants.GrantAsync("rules-lawyer", restrictedImport.PackageId);
            using var patch = JsonDocument.Parse("{\"hybrid\":true}");
            await globalRules.SetDecisionAsync(
                concept.Id,
                new SetGlobalRuleDecisionRequest(
                    publicRevisionId,
                    "Public base with a restricted incorporated contribution.",
                    MergePatch: patch.RootElement.Clone(),
                    Contributions:
                    [
                        new RuleConsolidationContributionRequest(
                            restrictedRevisionId,
                            RuleConsolidationContributionKinds.Incorporated,
                            "Restricted material contributes to this adjudication.")
                    ]),
                "rules-lawyer");
            var globalPublication = await globalRules.PublishAsync("rules-lawyer");

            Assert.Null(await globalRules.ResolveLatestAsync(conceptKey, null));
            Assert.Null(await globalRules.ResolveLatestAsync(conceptKey, "reader"));

            await grants.GrantAsync("reader", restrictedImport.PackageId);
            var granted = await globalRules.ResolveLatestAsync(conceptKey, "reader");
            Assert.NotNull(granted);
            Assert.True(granted!.Document.GetProperty("hybrid").GetBoolean());
            var contribution = Assert.Single(granted.Contributions);
            Assert.Equal(restrictedRevisionId, contribution.SourceEntityRevisionId);
            Assert.Equal(restrictedPackageKey, contribution.PackageKey);
            Assert.Equal("RESTRICTED", contribution.SourceCode);
            Assert.Equal(RuleConsolidationContributionKinds.Incorporated, contribution.ContributionKind);

            await campaignRules.SelectBaselineAsync(
                campaignId,
                new SelectCampaignRulesetBaselineRequest(globalPublication.Id),
                "dm");
            await campaignRules.SetDecisionAsync(
                campaignId,
                concept.Id,
                new SetCampaignRuleDecisionRequest(
                    CampaignRuleDecisionKinds.SelectSource,
                    publicRevisionId,
                    "Campaign explicitly replaces the consolidated global rule with the public source."),
                "dm");
            await campaignRules.PublishAsync(campaignId, "dm");

            var campaignResolved = await campaignRules.ResolveLatestAsync(
                campaignId,
                conceptKey,
                "campaign-reader-without-restricted-grant");
            Assert.NotNull(campaignResolved);
            Assert.Empty(campaignResolved!.GlobalContributions);
            Assert.Equal("PUBLIC", campaignResolved.SourceCode);
            Assert.Equal("public-base", campaignResolved.Document.GetProperty("marker").GetString());
            Assert.False(campaignResolved.Document.TryGetProperty("hybrid", out _));
        }
        finally
        {
            await ResetRulesAsync(db);
            db.ChangeTracker.Clear();
            var packages = await db.SourcePackages
                .Where(value => value.Key == publicPackageKey || value.Key == restrictedPackageKey)
                .ToArrayAsync();
            db.SourcePackages.RemoveRange(packages);
            await db.SaveChangesAsync();
        }
    }

    private static Import5eToolsDocumentRequest ImportRequest(
        string packageKey,
        bool isPublic,
        string workKey,
        string sourceCode,
        string marker) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: packageKey,
            Provider: "integration-test",
            License: "test-only",
            IsPublic: isPublic,
            WorkKey: workKey,
            WorkDisplayName: workKey,
            EditionKey: "original",
            EditionDisplayName: "Original",
            Json: $$"""
                {
                  "feat": [
                    {
                      "name": "Contribution Access",
                      "source": "{{sourceCode}}",
                      "marker": "{{marker}}"
                    }
                  ]
                }
                """,
            GameEdition: "3.5e",
            ReleaseKind: "srd",
            PublicationDate: null);

    private static Task<Guid> LatestRevisionIdAsync(RulesCoreDbContext db, Guid entityId) =>
        db.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => value.SourceEntityId == entityId)
            .Select(value => value.Id)
            .SingleAsync();

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
