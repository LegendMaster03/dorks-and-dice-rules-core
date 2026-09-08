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
public sealed class RulePatchPreviewIntegrationTests
{
    [Fact]
    public async Task GlobalAndCampaignPreviewsShowChangesWithoutPersistingCandidateDecisions()
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
        var previews = new RulePatchPreviewService(db);
        var packageKey = $"preview-{Guid.NewGuid():N}";
        var conceptKey = $"skill.arcana.preview.{Guid.NewGuid():N}";
        var campaignId = Guid.NewGuid();
        Guid packageId = Guid.Empty;

        try
        {
            var imported = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                PackageKey: packageKey,
                PackageDisplayName: "Preview Test Package",
                Provider: "integration-test",
                License: "test-only",
                IsPublic: true,
                WorkKey: "preview-work",
                WorkDisplayName: "Preview Work",
                EditionKey: "preview-edition",
                EditionDisplayName: "Preview Edition",
                Json: """
                    {
                      "skill": [
                        {
                          "name": "Arcana",
                          "source": "TST",
                          "ability": "int",
                          "proficiencies": ["Arcana", "History"]
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

            var globalRequest = new SetGlobalRuleDecisionRequest(
                sourceRevisionId,
                "Preview a global patch before saving it.",
                StructuredPatch: new RuleStructuredPatchRequest(
                    MergePatch: Parse("""{ "ability": "wis", "globalOnly": true }"""),
                    ArrayOperations:
                    [
                        new RuleArrayOperationRequest(
                            RuleArrayOperationKinds.Remove,
                            "/proficiencies",
                            Match: Selector(null, "\"History\"")),
                        new RuleArrayOperationRequest(
                            RuleArrayOperationKinds.Append,
                            "/proficiencies",
                            Value: Parse("\"Religion\""))
                    ]));

            var globalPreview = await previews.PreviewGlobalAsync(
                concept.Id,
                globalRequest,
                "rules-lawyer");
            Assert.NotNull(globalPreview);
            Assert.Equal("global", globalPreview.Scope);
            Assert.Null(globalPreview.CampaignId);
            Assert.Equal(RuleDecisionKinds.JsonRulePatch, globalPreview.DecisionKind);
            Assert.Equal("int", globalPreview.BaseDocument.GetProperty("ability").GetString());
            Assert.Equal("wis", globalPreview.PreviewDocument.GetProperty("ability").GetString());
            Assert.Equal(
                ["Arcana", "Religion"],
                globalPreview.PreviewDocument.GetProperty("proficiencies")
                    .EnumerateArray()
                    .Select(value => value.GetString())
                    .ToArray());
            Assert.Contains(
                globalPreview.Changes,
                change => change.Path == "/ability"
                    && change.ChangeKind == RuleDocumentChangeKinds.Replace);
            Assert.Contains(
                globalPreview.Changes,
                change => change.Path == "/globalOnly"
                    && change.ChangeKind == RuleDocumentChangeKinds.Add);
            Assert.Contains(
                globalPreview.Changes,
                change => change.Path == "/proficiencies"
                    && change.ChangeKind == RuleDocumentChangeKinds.Replace);
            Assert.Equal(0, await db.GlobalRuleDecisions.CountAsync());

            await globalRules.SetDecisionAsync(concept.Id, globalRequest, "rules-lawyer");
            var globalPublication = await globalRules.PublishAsync("rules-lawyer");
            await campaignRules.SelectBaselineAsync(
                campaignId,
                new SelectCampaignRulesetBaselineRequest(globalPublication.Id),
                "campaign-dm");

            var campaignRequest = new SetCampaignRuleDecisionRequest(
                CampaignRuleDecisionKinds.JsonRulePatch,
                SourceEntityRevisionId: null,
                Note: "Preview campaign changes before saving them.",
                StructuredPatch: new RuleStructuredPatchRequest(
                    MergePatch: Parse("""{ "campaignOnly": true }"""),
                    ArrayOperations:
                    [
                        new RuleArrayOperationRequest(
                            RuleArrayOperationKinds.Append,
                            "/proficiencies",
                            Value: Parse("\"Nature\""))
                    ]));

            var campaignPreview = await previews.PreviewCampaignAsync(
                campaignId,
                concept.Id,
                campaignRequest,
                "campaign-dm");
            Assert.NotNull(campaignPreview);
            Assert.Equal("campaign", campaignPreview.Scope);
            Assert.Equal(campaignId, campaignPreview.CampaignId);
            Assert.Equal(CampaignRuleDecisionKinds.JsonRulePatch, campaignPreview.DecisionKind);
            Assert.Equal("wis", campaignPreview.BaseDocument.GetProperty("ability").GetString());
            Assert.True(campaignPreview.BaseDocument.GetProperty("globalOnly").GetBoolean());
            Assert.False(campaignPreview.BaseDocument.TryGetProperty("campaignOnly", out _));
            Assert.True(campaignPreview.PreviewDocument.GetProperty("campaignOnly").GetBoolean());
            Assert.Equal(
                ["Arcana", "Religion", "Nature"],
                campaignPreview.PreviewDocument.GetProperty("proficiencies")
                    .EnumerateArray()
                    .Select(value => value.GetString())
                    .ToArray());
            Assert.Contains(
                campaignPreview.Changes,
                change => change.Path == "/campaignOnly"
                    && change.ChangeKind == RuleDocumentChangeKinds.Add);
            Assert.Equal(0, await db.CampaignRuleDecisions.CountAsync());
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
    public async Task PreviewRequiresIndependentRestrictedSourceGrant()
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
        var grants = new SourceGrantService(db);
        var previews = new RulePatchPreviewService(db);
        var packageKey = $"restricted-preview-{Guid.NewGuid():N}";
        var conceptKey = $"skill.restricted.preview.{Guid.NewGuid():N}";
        Guid packageId = Guid.Empty;

        try
        {
            var imported = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                PackageKey: packageKey,
                PackageDisplayName: "Restricted Preview Package",
                Provider: "integration-test",
                License: "restricted-test",
                IsPublic: false,
                WorkKey: "restricted-work",
                WorkDisplayName: "Restricted Work",
                EditionKey: "restricted-edition",
                EditionDisplayName: "Restricted Edition",
                Json: """
                    {
                      "skill": [
                        { "name": "Restricted Skill", "source": "RST", "ability": "int" }
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
                new CreateRuleConceptRequest(conceptKey, "skill", "Restricted Skill"),
                "rules-lawyer")).Value;
            await globalRules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(sourceEntityId),
                "rules-lawyer");

            var request = new SetGlobalRuleDecisionRequest(
                sourceRevisionId,
                "Restricted preview",
                MergePatch: Parse("""{ "ability": "wis" }"""));

            Assert.Null(await previews.PreviewGlobalAsync(
                concept.Id,
                request,
                "rules-lawyer"));

            await grants.GrantAsync("rules-lawyer", packageId);
            var grantedPreview = await previews.PreviewGlobalAsync(
                concept.Id,
                request,
                "rules-lawyer");
            Assert.NotNull(grantedPreview);
            Assert.Equal("wis", grantedPreview.PreviewDocument.GetProperty("ability").GetString());
            Assert.Equal(0, await db.GlobalRuleDecisions.CountAsync());
        }
        finally
        {
            await db.UserSourceGrants.ExecuteDeleteAsync();
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

    private static RuleArrayItemSelectorRequest Selector(string? key, string json) =>
        new(key, Parse(json));

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
