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
public sealed class RuleArrayPatchIntegrationTests
{
    [Fact]
    public async Task StructuredPatchesComposeArrayOperationsWithoutMutatingSource()
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
        var packageKey = $"array-patch-{Guid.NewGuid():N}";
        var conceptKey = $"skill.arcana.array-patch.{Guid.NewGuid():N}";
        var campaignId = Guid.NewGuid();
        Guid packageId = Guid.Empty;

        try
        {
            var imported = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                PackageKey: packageKey,
                PackageDisplayName: "Array Patch Test Package",
                Provider: "integration-test",
                License: "test-only",
                IsPublic: true,
                WorkKey: "array-work",
                WorkDisplayName: "Array Work",
                EditionKey: "array-edition",
                EditionDisplayName: "Array Edition",
                Json: """
                    {
                      "skill": [
                        {
                          "name": "Arcana",
                          "source": "TST",
                          "ability": "int",
                          "proficiencies": ["Arcana", "History"],
                          "traits": [
                            { "name": "Alpha", "rank": 1 },
                            { "name": "Beta", "rank": 2 },
                            { "name": "Gamma", "rank": 3 }
                          ],
                          "a/b": { "~list": ["base"] }
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

            var globalPatch = new RuleStructuredPatchRequest(
                MergePatch: Parse("""{ "globalOnly": true, "ability": "wis" }"""),
                ArrayOperations:
                [
                    new RuleArrayOperationRequest(
                        RuleArrayOperationKinds.Append,
                        "/proficiencies",
                        Value: Parse("\"Religion\"")),
                    new RuleArrayOperationRequest(
                        RuleArrayOperationKinds.Remove,
                        "/proficiencies",
                        Match: Selector(null, "\"History\"")),
                    new RuleArrayOperationRequest(
                        RuleArrayOperationKinds.ReplaceByKey,
                        "/traits",
                        Value: Parse("""{ "name": "Beta", "rank": 20, "source": "global" }"""),
                        Match: Selector("name", "\"Beta\"")),
                    new RuleArrayOperationRequest(
                        RuleArrayOperationKinds.InsertBefore,
                        "/traits",
                        Value: Parse("""{ "name": "BeforeGamma", "rank": 25 }"""),
                        Anchor: Selector("name", "\"Gamma\"")),
                    new RuleArrayOperationRequest(
                        RuleArrayOperationKinds.InsertAfter,
                        "/traits",
                        Value: Parse("""{ "name": "AfterGamma", "rank": 35 }"""),
                        Anchor: Selector("name", "\"Gamma\"")),
                    new RuleArrayOperationRequest(
                        RuleArrayOperationKinds.Append,
                        "/a~1b/~0list",
                        Value: Parse("\"escaped\""))
                ]);

            var firstDecision = await globalRules.SetDecisionAsync(
                concept.Id,
                new SetGlobalRuleDecisionRequest(
                    sourceRevisionId,
                    "Merge fields and edit arrays without replacing them wholesale.",
                    StructuredPatch: globalPatch),
                "rules-lawyer");
            Assert.True(firstDecision.Created);
            Assert.Equal(RuleDecisionKinds.JsonRulePatch, firstDecision.Value.DecisionKind);
            Assert.NotNull(firstDecision.Value.StructuredPatch);
            Assert.Null(firstDecision.Value.MergePatch);

            var reorderedMergeGlobalPatch = globalPatch with
            {
                MergePatch = Parse("""{ "ability": "wis", "globalOnly": true }""")
            };
            var repeatedDecision = await globalRules.SetDecisionAsync(
                concept.Id,
                new SetGlobalRuleDecisionRequest(
                    sourceRevisionId,
                    "Merge fields and edit arrays without replacing them wholesale.",
                    StructuredPatch: reorderedMergeGlobalPatch),
                "rules-lawyer");
            Assert.False(repeatedDecision.Created);
            Assert.Equal(firstDecision.Value.Id, repeatedDecision.Value.Id);
            Assert.Equal(firstDecision.Value.PatchFingerprint, repeatedDecision.Value.PatchFingerprint);

            var globalPublication = await globalRules.PublishAsync("rules-lawyer");
            var resolvedGlobal = await globalRules.ResolveLatestAsync(conceptKey, userId: null);
            Assert.NotNull(resolvedGlobal);
            Assert.Equal("wis", resolvedGlobal.Document.GetProperty("ability").GetString());
            Assert.True(resolvedGlobal.Document.GetProperty("globalOnly").GetBoolean());
            Assert.Equal(
                ["Arcana", "Religion"],
                resolvedGlobal.Document.GetProperty("proficiencies")
                    .EnumerateArray()
                    .Select(value => value.GetString())
                    .ToArray());
            Assert.Equal(
                ["Alpha", "Beta", "BeforeGamma", "Gamma", "AfterGamma"],
                resolvedGlobal.Document.GetProperty("traits")
                    .EnumerateArray()
                    .Select(value => value.GetProperty("name").GetString())
                    .ToArray());
            Assert.Equal(
                20,
                resolvedGlobal.Document.GetProperty("traits")[1].GetProperty("rank").GetInt32());
            Assert.Equal(
                "escaped",
                resolvedGlobal.Document.GetProperty("a/b").GetProperty("~list")[1].GetString());
            Assert.Equal(firstDecision.Value.PatchFingerprint, resolvedGlobal.GlobalPatchFingerprint);
            Assert.NotNull(resolvedGlobal.GlobalStructuredPatch);
            Assert.Null(resolvedGlobal.GlobalMergePatch);

            var storedSourceJson = await db.SourceEntityRevisions
                .Where(value => value.Id == sourceRevisionId)
                .Select(value => value.RawJson)
                .SingleAsync();
            using var storedSource = JsonDocument.Parse(storedSourceJson);
            Assert.Equal("int", storedSource.RootElement.GetProperty("ability").GetString());
            Assert.Equal(
                ["Arcana", "History"],
                storedSource.RootElement.GetProperty("proficiencies")
                    .EnumerateArray()
                    .Select(value => value.GetString())
                    .ToArray());
            Assert.Equal(2, storedSource.RootElement.GetProperty("traits")[1].GetProperty("rank").GetInt32());
            Assert.Single(storedSource.RootElement.GetProperty("a/b").GetProperty("~list").EnumerateArray());

            await campaignRules.SelectBaselineAsync(
                campaignId,
                new SelectCampaignRulesetBaselineRequest(globalPublication.Id),
                "campaign-dm");

            var campaignPatch = new RuleStructuredPatchRequest(
                MergePatch: Parse("""{ "campaignOnly": true }"""),
                ArrayOperations:
                [
                    new RuleArrayOperationRequest(
                        RuleArrayOperationKinds.Remove,
                        "/proficiencies",
                        Match: Selector(null, "\"Arcana\"")),
                    new RuleArrayOperationRequest(
                        RuleArrayOperationKinds.Append,
                        "/proficiencies",
                        Value: Parse("\"Nature\"")),
                    new RuleArrayOperationRequest(
                        RuleArrayOperationKinds.InsertBefore,
                        "/traits",
                        Value: Parse("""{ "name": "CampaignFirst", "rank": 24 }"""),
                        Anchor: Selector("name", "\"BeforeGamma\"")),
                    new RuleArrayOperationRequest(
                        RuleArrayOperationKinds.ReplaceByKey,
                        "/traits",
                        Value: Parse("""{ "name": "Gamma", "rank": 30, "source": "campaign" }"""),
                        Match: Selector("name", "\"Gamma\""))
                ]);

            var campaignDecision = await campaignRules.SetDecisionAsync(
                campaignId,
                concept.Id,
                new SetCampaignRuleDecisionRequest(
                    CampaignRuleDecisionKinds.JsonRulePatch,
                    SourceEntityRevisionId: null,
                    Note: "Apply campaign-specific array edits.",
                    StructuredPatch: campaignPatch),
                "campaign-dm");
            Assert.True(campaignDecision.Created);
            Assert.NotNull(campaignDecision.StructuredPatch);

            await campaignRules.PublishAsync(campaignId, "campaign-dm");
            var resolvedCampaign = await campaignRules.ResolveLatestAsync(
                campaignId,
                conceptKey,
                userId: null);
            Assert.NotNull(resolvedCampaign);
            Assert.Equal(RuleDecisionKinds.JsonRulePatch, resolvedCampaign.GlobalDecisionKind);
            Assert.Equal(CampaignRuleDecisionKinds.JsonRulePatch, resolvedCampaign.EffectiveDecisionKind);
            Assert.True(resolvedCampaign.Document.GetProperty("globalOnly").GetBoolean());
            Assert.True(resolvedCampaign.Document.GetProperty("campaignOnly").GetBoolean());
            Assert.Equal(
                ["Religion", "Nature"],
                resolvedCampaign.Document.GetProperty("proficiencies")
                    .EnumerateArray()
                    .Select(value => value.GetString())
                    .ToArray());
            Assert.Equal(
                ["Alpha", "Beta", "CampaignFirst", "BeforeGamma", "Gamma", "AfterGamma"],
                resolvedCampaign.Document.GetProperty("traits")
                    .EnumerateArray()
                    .Select(value => value.GetProperty("name").GetString())
                    .ToArray());
            Assert.Equal(
                30,
                resolvedCampaign.Document.GetProperty("traits")[4].GetProperty("rank").GetInt32());
            Assert.Equal(campaignDecision.PatchFingerprint, resolvedCampaign.CampaignPatchFingerprint);
            Assert.NotNull(resolvedCampaign.CampaignStructuredPatch);
            Assert.Null(resolvedCampaign.CampaignMergePatch);
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
    public void StructuredPatchRejectsAmbiguousSelectors()
    {
        var source = Parse("""
            {
              "items": [
                { "name": "Duplicate", "value": 1 },
                { "name": "Duplicate", "value": 2 }
              ]
            }
            """);
        var patch = JsonRulePatch.Normalize(new RuleStructuredPatchRequest(
            ArrayOperations:
            [
                new RuleArrayOperationRequest(
                    RuleArrayOperationKinds.ReplaceByKey,
                    "/items",
                    Value: Parse("""{ "name": "Duplicate", "value": 3 }"""),
                    Match: Selector("name", "\"Duplicate\""))
            ]));

        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonRulePatch.Apply(source, patch.Json));
        Assert.Contains("exactly one matching item", exception.Message, StringComparison.Ordinal);
    }

    private static RuleArrayItemSelectorRequest Selector(string? key, string json) =>
        new(key, Parse(json));

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
