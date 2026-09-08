using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class RuleMergePatchIntegrationTests
{
    [Fact]
    public async Task GlobalAndCampaignMergePatchesComposeWithoutMutatingSource()
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
        var campaignRules = new CampaignRulesService(db);
        var packageKey = $"merge-patch-{Guid.NewGuid():N}";
        var conceptKey = $"skill.arcana.merge-patch.{Guid.NewGuid():N}";
        var campaignId = Guid.NewGuid();
        Guid packageId = Guid.Empty;

        try
        {
            var imported = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                PackageKey: packageKey,
                PackageDisplayName: "Merge Patch Test Package",
                Provider: "integration-test",
                License: "test-only",
                IsPublic: true,
                WorkKey: "merge-work",
                WorkDisplayName: "Merge Work",
                EditionKey: "merge-edition",
                EditionDisplayName: "Merge Edition",
                Json: """
                    {
                      "skill": [
                        {
                          "name": "Arcana",
                          "source": "TST",
                          "ability": "int",
                          "obsolete": true,
                          "nested": { "a": 1, "b": 2, "c": 3 },
                          "tags": ["base"]
                        }
                      ]
                    }
                    """));
            packageId = imported.PackageId;
            var sourceEntityId = imported.Entities.Single().EntityId;
            var sourceRevisionId = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == sourceEntityId)
                .Select(value => value.Id)
                .SingleAsync();

            var concept = (await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(conceptKey, "skill", "Arcana"),
                "rules-lawyer")).Value;
            await globalRules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(sourceEntityId),
                "rules-lawyer");

            using var globalPatchDocument = JsonDocument.Parse("""
                {
                  "ability": "wis",
                  "obsolete": null,
                  "nested": { "b": 20, "d": 4 },
                  "tags": ["global"],
                  "globalOnly": true
                }
                """);
            var firstDecision = await globalRules.SetDecisionAsync(
                concept.Id,
                new SetGlobalRuleDecisionRequest(
                    sourceRevisionId,
                    "Merge selected fields from the Dorks & Dice Rules Layer.",
                    globalPatchDocument.RootElement.Clone()),
                "rules-lawyer");
            Assert.True(firstDecision.Created);
            Assert.Equal(RuleDecisionKinds.JsonMergePatch, firstDecision.Value.DecisionKind);
            Assert.NotNull(firstDecision.Value.PatchFingerprint);

            using var reorderedGlobalPatchDocument = JsonDocument.Parse("""
                {
                  "globalOnly": true,
                  "tags": ["global"],
                  "nested": { "d": 4, "b": 20 },
                  "obsolete": null,
                  "ability": "wis"
                }
                """);
            var repeatedDecision = await globalRules.SetDecisionAsync(
                concept.Id,
                new SetGlobalRuleDecisionRequest(
                    sourceRevisionId,
                    "Merge selected fields from the Dorks & Dice Rules Layer.",
                    reorderedGlobalPatchDocument.RootElement.Clone()),
                "rules-lawyer");
            Assert.False(repeatedDecision.Created);
            Assert.Equal(firstDecision.Value.Id, repeatedDecision.Value.Id);
            Assert.Equal(firstDecision.Value.PatchFingerprint, repeatedDecision.Value.PatchFingerprint);

            var globalPublication = await globalRules.PublishAsync("rules-lawyer");
            var resolvedGlobal = await globalRules.ResolveLatestAsync(conceptKey, userId: null);
            Assert.NotNull(resolvedGlobal);
            Assert.Equal("wis", resolvedGlobal.Document.GetProperty("ability").GetString());
            Assert.False(resolvedGlobal.Document.TryGetProperty("obsolete", out _));
            Assert.Equal(1, resolvedGlobal.Document.GetProperty("nested").GetProperty("a").GetInt32());
            Assert.Equal(20, resolvedGlobal.Document.GetProperty("nested").GetProperty("b").GetInt32());
            Assert.Equal(3, resolvedGlobal.Document.GetProperty("nested").GetProperty("c").GetInt32());
            Assert.Equal(4, resolvedGlobal.Document.GetProperty("nested").GetProperty("d").GetInt32());
            Assert.Equal("global", resolvedGlobal.Document.GetProperty("tags")[0].GetString());
            Assert.True(resolvedGlobal.Document.GetProperty("globalOnly").GetBoolean());
            Assert.Equal(firstDecision.Value.PatchFingerprint, resolvedGlobal.GlobalPatchFingerprint);

            var storedSourceJson = await db.SourceEntityRevisions
                .Where(value => value.Id == sourceRevisionId)
                .Select(value => value.RawJson)
                .SingleAsync();
            using var storedSource = JsonDocument.Parse(storedSourceJson);
            Assert.Equal("int", storedSource.RootElement.GetProperty("ability").GetString());
            Assert.True(storedSource.RootElement.GetProperty("obsolete").GetBoolean());
            Assert.False(storedSource.RootElement.TryGetProperty("globalOnly", out _));

            await campaignRules.SelectBaselineAsync(
                campaignId,
                new SelectCampaignRulesetBaselineRequest(globalPublication.Id),
                "campaign-dm");

            using var campaignPatchDocument = JsonDocument.Parse("""
                {
                  "nested": { "c": null, "d": 40, "e": 5 },
                  "tags": ["campaign"],
                  "campaignOnly": true
                }
                """);
            var campaignDecision = await campaignRules.SetDecisionAsync(
                campaignId,
                concept.Id,
                new SetCampaignRuleDecisionRequest(
                    CampaignRuleDecisionKinds.JsonMergePatch,
                    SourceEntityRevisionId: null,
                    Note: "Apply campaign-specific Arcana changes.",
                    MergePatch: campaignPatchDocument.RootElement.Clone()),
                "campaign-dm");
            Assert.True(campaignDecision.Created);
            Assert.NotNull(campaignDecision.PatchFingerprint);

            await campaignRules.PublishAsync(campaignId, "campaign-dm");
            var resolvedCampaign = await campaignRules.ResolveLatestAsync(campaignId, conceptKey, userId: null);
            Assert.NotNull(resolvedCampaign);
            Assert.Equal(RuleDecisionKinds.JsonMergePatch, resolvedCampaign.GlobalDecisionKind);
            Assert.Equal(CampaignRuleDecisionKinds.JsonMergePatch, resolvedCampaign.EffectiveDecisionKind);
            Assert.Equal("wis", resolvedCampaign.Document.GetProperty("ability").GetString());
            Assert.Equal(1, resolvedCampaign.Document.GetProperty("nested").GetProperty("a").GetInt32());
            Assert.Equal(20, resolvedCampaign.Document.GetProperty("nested").GetProperty("b").GetInt32());
            Assert.False(resolvedCampaign.Document.GetProperty("nested").TryGetProperty("c", out _));
            Assert.Equal(40, resolvedCampaign.Document.GetProperty("nested").GetProperty("d").GetInt32());
            Assert.Equal(5, resolvedCampaign.Document.GetProperty("nested").GetProperty("e").GetInt32());
            Assert.Equal("campaign", resolvedCampaign.Document.GetProperty("tags")[0].GetString());
            Assert.True(resolvedCampaign.Document.GetProperty("globalOnly").GetBoolean());
            Assert.True(resolvedCampaign.Document.GetProperty("campaignOnly").GetBoolean());
            Assert.Equal(campaignDecision.PatchFingerprint, resolvedCampaign.CampaignPatchFingerprint);

            await campaignRules.SetDecisionAsync(
                campaignId,
                concept.Id,
                new SetCampaignRuleDecisionRequest(
                    CampaignRuleDecisionKinds.InheritGlobal,
                    SourceEntityRevisionId: null,
                    Note: "Return to the selected global rule."),
                "campaign-dm");
            await campaignRules.PublishAsync(campaignId, "campaign-dm");

            var inheritedCampaign = await campaignRules.ResolveLatestAsync(campaignId, conceptKey, userId: null);
            Assert.NotNull(inheritedCampaign);
            Assert.Equal(CampaignRuleDecisionKinds.InheritGlobal, inheritedCampaign.EffectiveDecisionKind);
            Assert.Equal("global", inheritedCampaign.Document.GetProperty("tags")[0].GetString());
            Assert.True(inheritedCampaign.Document.GetProperty("nested").TryGetProperty("c", out _));
            Assert.False(inheritedCampaign.Document.TryGetProperty("campaignOnly", out _));
            Assert.Null(inheritedCampaign.CampaignPatchFingerprint);
        }
        finally
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

            if (packageId != Guid.Empty)
            {
                await db.SourcePackages
                    .Where(value => value.Id == packageId)
                    .ExecuteDeleteAsync();
            }
        }
    }

    [Fact]
    public void MergePatchRequiresObjectRoot()
    {
        using var patch = JsonDocument.Parse("[1, 2, 3]");
        var exception = Assert.Throws<ArgumentException>(() =>
            JsonMergePatch.Normalize(patch.RootElement));
        Assert.Contains("object root", exception.Message, StringComparison.Ordinal);
    }
}
