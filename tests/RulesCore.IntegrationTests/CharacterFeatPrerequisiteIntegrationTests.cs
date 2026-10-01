using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CharacterFeatPrerequisiteIntegrationTests
{
    [Fact]
    public async Task CharacterProjectionEvaluatesNormalizedFeatPossessionPrerequisite()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var factory = new WebApplicationFactory<Program>();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        await ResetRulesAsync(db);

        var token = Guid.NewGuid().ToString("N")[..8];
        var actor = $"feat-prerequisite-{token}";
        var packageKey = $"feat-prerequisite-{token}";
        var featName = $"Required Feat {token}";
        var prestigeName = $"Feat Prestige {token}";
        var importer = new NormalizedSourceImportService(db);
        var normalization = new SourceNormalizationService(db);
        var globalRules = new GlobalRulesService(db);
        var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

        try
        {
            var featContent = $$"""
                {
                  "name": "{{featName}}",
                  "entries": ["A prerequisite fixture feat."]
                }
                """;
            var prestigeContent = $$"""
                {
                  "name": "{{prestigeName}}",
                  "_rulesCore": {
                    "character": {
                      "prerequisitesComplete": true,
                      "prerequisites": [
                        {
                          "key": "prerequisite.feat",
                          "matchCount": 1,
                          "requirements": [
                            {
                              "kind": "feat",
                              "operator": ">=",
                              "value": 1,
                              "targetName": "{{featName}}"
                            }
                          ]
                        }
                      ]
                    }
                  }
                }
                """;
            var representation = new NormalizedSourceRepresentation(
                "projection-test",
                new SourceRepresentationArtifact(
                    $"feat-prerequisite-{token}.json",
                    Encoding.UTF8.GetBytes("{}"),
                    $"test:feat-prerequisite:{token}"),
                [
                    new NormalizedSourceRecord(
                        "feat",
                        featName,
                        "TEST",
                        $"feat|{token}",
                        featContent)
                    {
                        ContentJson = featContent
                    },
                    new NormalizedSourceRecord(
                        "prestigeClass",
                        prestigeName,
                        "TEST",
                        $"prestige-class|{token}",
                        prestigeContent)
                    {
                        ContentJson = prestigeContent
                    }
                ]);
            var imported = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                packageKey,
                $"Feat prerequisite {token}",
                "integration-test",
                "test-only",
                true,
                representation));

            var featEntity = Assert.Single(imported.Entities, value => value.EntityType == "feat");
            var prestigeEntity = Assert.Single(imported.Entities, value => value.EntityType == "prestigeClass");
            var acceptedFeat = (await normalization.AcceptAsync(featEntity.EntityId, actor))!;
            var acceptedPrestige = (await normalization.AcceptAsync(prestigeEntity.EntityId, actor))!;
            var revisions = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == featEntity.EntityId
                    || value.SourceEntityId == prestigeEntity.EntityId)
                .ToDictionaryAsync(value => value.SourceEntityId, value => value.Id);

            await globalRules.SetDecisionAsync(
                acceptedFeat.Concept.Id,
                new SetGlobalRuleDecisionRequest(revisions[featEntity.EntityId], "Publish required feat fixture."),
                actor);
            await globalRules.SetDecisionAsync(
                acceptedPrestige.Concept.Id,
                new SetGlobalRuleDecisionRequest(revisions[prestigeEntity.EntityId], "Publish Prestige Class fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            var missing = await projection.ResolveGlobalAsync(
                new CharacterRulesProjectionRequest(
                    SelectedConcepts:
                    [
                        new CharacterSelectedConceptInput(acceptedPrestige.Concept.Key)
                    ]),
                userId: null);
            var missingPrerequisite = Assert.Single(missing.Prerequisites, value =>
                value.ConceptKey == acceptedPrestige.Concept.Key);
            Assert.False(missingPrerequisite.Satisfied);
            var missingFeat = Assert.Single(missingPrerequisite.Requirements);
            Assert.Equal("feat", missingFeat.Kind);
            Assert.False(missingFeat.Satisfied);
            Assert.Contains(missing.Conflicts, value =>
                value.ConflictKey == $"conflict.prerequisite.{acceptedPrestige.Concept.Key}"
                && value.Kind == "prerequisite-unsatisfied");

            var owned = await projection.ResolveGlobalAsync(
                new CharacterRulesProjectionRequest(
                    SelectedConcepts:
                    [
                        new CharacterSelectedConceptInput(acceptedPrestige.Concept.Key),
                        new CharacterSelectedConceptInput(acceptedFeat.Concept.Key)
                    ]),
                userId: null);
            var ownedPrerequisite = Assert.Single(owned.Prerequisites, value =>
                value.ConceptKey == acceptedPrestige.Concept.Key);
            Assert.True(ownedPrerequisite.Satisfied);
            var ownedFeat = Assert.Single(ownedPrerequisite.Requirements);
            Assert.Equal("feat", ownedFeat.Kind);
            Assert.True(ownedFeat.Satisfied);
            Assert.DoesNotContain(owned.Conflicts, value =>
                value.ConflictKey == $"conflict.prerequisite.{acceptedPrestige.Concept.Key}");
        }
        finally
        {
            await ResetRulesAsync(db);
            db.ChangeTracker.Clear();
            var package = await db.SourcePackages.SingleOrDefaultAsync(value => value.Key == packageKey);
            if (package is not null)
            {
                db.SourcePackages.Remove(package);
                await db.SaveChangesAsync();
            }
        }
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
