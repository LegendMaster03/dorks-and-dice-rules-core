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
    public async Task CharacterProjectionUsesOneMaximumContractForExplicitAndPerLevelProgressions()
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
        var tableClassName = $"Finite Table Adept {token}";
        var explicitClassName = $"Finite Explicit Adept {token}";
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
                EditionKey: "5e",
                EditionDisplayName: "5e",
                Json: $$"""
                    {
                      "class": [
                        {
                          "name": "{{tableClassName}}",
                          "source": "{{sourceCode}}",
                          "hd": { "number": 1, "faces": 8 },
                          "classFeatures": [
                            "First Feature|{{tableClassName}}|{{sourceCode}}|1",
                            "Second Feature|{{tableClassName}}|{{sourceCode}}|2"
                          ],
                          "classTableGroups": [
                            {
                              "colLabels": ["1st"],
                              "rowsSpellProgression": [[1], [2]]
                            }
                          ]
                        },
                        {
                          "name": "{{explicitClassName}}",
                          "source": "{{sourceCode}}",
                          "hd": { "number": 1, "faces": 8 },
                          "classFeatures": [
                            "First Feature|{{explicitClassName}}|{{sourceCode}}|1"
                          ],
                          "_rulesCore": {
                            "character": {
                              "maximumLevel": 2
                            }
                          }
                        }
                      ]
                    }
                    """,
                GameEdition: "5e"));

            var classEntities = imported.Entities
                .Where(value => value.EntityType == RuleConceptEntityTypes.Class)
                .ToDictionary(value => value.Name, StringComparer.Ordinal);
            Assert.Equal(2, classEntities.Count);

            var tableEntity = classEntities[tableClassName];
            var explicitEntity = classEntities[explicitClassName];
            var acceptedTable = (await normalization.AcceptAsync(tableEntity.EntityId, actor))!;
            var acceptedExplicit = (await normalization.AcceptAsync(explicitEntity.EntityId, actor))!;
            var revisions = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == tableEntity.EntityId
                    || value.SourceEntityId == explicitEntity.EntityId)
                .ToDictionaryAsync(value => value.SourceEntityId, value => value.Id);

            await globalRules.SetDecisionAsync(
                acceptedTable.Concept.Id,
                new SetGlobalRuleDecisionRequest(revisions[tableEntity.EntityId], "Publish table maximum fixture."),
                actor);
            await globalRules.SetDecisionAsync(
                acceptedExplicit.Concept.Id,
                new SetGlobalRuleDecisionRequest(revisions[explicitEntity.EntityId], "Publish explicit maximum fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            await AssertMaximumContractAsync(projection, acceptedTable.Concept.Key, 2);
            await AssertMaximumContractAsync(projection, acceptedExplicit.Concept.Key, 2);
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

    private static async Task AssertMaximumContractAsync(
        ICharacterRulesProjectionService projection,
        string conceptKey,
        int expectedMaximum)
    {
        var atMaximum = await projection.ResolveGlobalAsync(
            new CharacterRulesProjectionRequest(
                Advancements:
                [
                    new CharacterAdvancementFactInput(
                        conceptKey,
                        expectedMaximum,
                        OccurrenceKey: "class-occurrence")
                ]),
            userId: null);
        var maximum = Assert.Single(atMaximum.Mechanics, value =>
            value.MechanicKey == $"advancement.{conceptKey}.maximum-level");
        Assert.Equal(expectedMaximum, maximum.NumericValue);
        Assert.DoesNotContain(atMaximum.Conflicts, value =>
            value.ConflictKey == $"conflict.{conceptKey}.maximum-level");

        var twoOccurrencesAtMaximum = await projection.ResolveGlobalAsync(
            new CharacterRulesProjectionRequest(
                Advancements:
                [
                    new CharacterAdvancementFactInput(
                        conceptKey,
                        expectedMaximum,
                        OccurrenceKey: "class-occurrence-a"),
                    new CharacterAdvancementFactInput(
                        conceptKey,
                        expectedMaximum,
                        OccurrenceKey: "class-occurrence-b")
                ]),
            userId: null);
        Assert.DoesNotContain(twoOccurrencesAtMaximum.Conflicts, value =>
            value.ConflictKey == $"conflict.{conceptKey}.maximum-level");

        var aboveMaximum = await projection.ResolveGlobalAsync(
            new CharacterRulesProjectionRequest(
                Advancements:
                [
                    new CharacterAdvancementFactInput(
                        conceptKey,
                        expectedMaximum + 1,
                        OccurrenceKey: "class-occurrence")
                ]),
            userId: null);
        var conflict = Assert.Single(aboveMaximum.Conflicts, value =>
            value.ConflictKey == $"conflict.{conceptKey}.maximum-level");
        Assert.Equal("maximum-level", conflict.Kind);
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
