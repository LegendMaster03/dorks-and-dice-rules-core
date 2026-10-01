using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CharacterAdvancementMaximumIntegrationTests
{
    [Fact]
    public async Task CharacterProjectionDerivesFiniteMaximumFromEffectivePerLevelTable()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var factory = new WebApplicationFactory<Program>();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        await ResetRulesAsync(db);

        var token = Guid.NewGuid().ToString("N")[..8];
        var actor = $"advancement-maximum-{token}";
        var packageKey = $"advancement-maximum-{token}";
        var sourceCode = $"AM{token}";
        var className = $"Finite Adept {token}";
        var importer = new SourceImportService(db);
        var normalization = new SourceNormalizationService(db);
        var globalRules = new GlobalRulesService(db);
        var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

        try
        {
            var imported = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                PackageKey: packageKey,
                PackageDisplayName: $"Advancement maximum {token}",
                Provider: "integration-test",
                License: "test-only",
                IsPublic: true,
                WorkKey: $"advancement-maximum-work-{token}",
                WorkDisplayName: $"Advancement Maximum Work {token}",
                EditionKey: "fixture",
                EditionDisplayName: "Fixture",
                Json: $$"""
                    {
                      "class": [
                        {
                          "name": "{{className}}",
                          "source": "{{sourceCode}}",
                          "hd": { "number": 1, "faces": 8 },
                          "classFeatures": [
                            "First Feature|{{className}}|{{sourceCode}}|1",
                            "Second Feature|{{className}}|{{sourceCode}}|2"
                          ],
                          "classTableGroups": [
                            {
                              "colLabels": ["Progression"],
                              "rows": [[1], [2]]
                            }
                          ]
                        }
                      ]
                    }
                    """,
                GameEdition: "fixture"));

            var classEntity = Assert.Single(imported.Entities, value =>
                value.EntityType == RuleConceptEntityTypes.Class);
            var acceptedClass = (await normalization.AcceptAsync(classEntity.EntityId, actor))!;
            var revisionId = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == classEntity.EntityId)
                .Select(value => value.Id)
                .SingleAsync();
            await globalRules.SetDecisionAsync(
                acceptedClass.Concept.Id,
                new SetGlobalRuleDecisionRequest(revisionId, "Publish finite Class fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            var atMaximum = await projection.ResolveGlobalAsync(
                new CharacterRulesProjectionRequest(
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(
                            acceptedClass.Concept.Key,
                            2,
                            OccurrenceKey: "class-occurrence")
                    ]),
                userId: null);
            var maximum = Assert.Single(atMaximum.Mechanics, value =>
                value.MechanicKey == $"advancement.{acceptedClass.Concept.Key}.maximum-level");
            Assert.Equal(2, maximum.NumericValue);
            Assert.DoesNotContain(atMaximum.Conflicts, value =>
                value.ConflictKey == $"conflict.{acceptedClass.Concept.Key}.maximum-level");

            var aboveMaximum = await projection.ResolveGlobalAsync(
                new CharacterRulesProjectionRequest(
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(
                            acceptedClass.Concept.Key,
                            3,
                            OccurrenceKey: "class-occurrence")
                    ]),
                userId: null);
            Assert.Contains(aboveMaximum.Conflicts, value =>
                value.ConflictKey == $"conflict.{acceptedClass.Concept.Key}.maximum-level"
                && value.Kind == "maximum-level");
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
