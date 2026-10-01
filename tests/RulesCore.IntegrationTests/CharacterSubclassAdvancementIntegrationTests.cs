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
public sealed class CharacterSubclassAdvancementIntegrationTests
{
    [Fact]
    public async Task CharacterProjectionDerivesSubclassAvailabilityFromEffectiveFeatureLevel()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var factory = new WebApplicationFactory<Program>();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        await ResetRulesAsync(db);

        var token = Guid.NewGuid().ToString("N")[..8];
        var actor = $"subclass-advancement-{token}";
        var packageKey = $"subclass-advancement-{token}";
        var sourceCode = $"SA{token}";
        var className = $"Threshold Mage {token}";
        var subclassName = $"Threshold Path {token}";
        var importer = new SourceImportService(db);
        var normalization = new SourceNormalizationService(db);
        var globalRules = new GlobalRulesService(db);
        var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

        try
        {
            var imported = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                PackageKey: packageKey,
                PackageDisplayName: $"Subclass advancement {token}",
                Provider: "integration-test",
                License: "test-only",
                IsPublic: true,
                WorkKey: $"subclass-advancement-work-{token}",
                WorkDisplayName: $"Subclass Advancement Work {token}",
                EditionKey: "5e",
                EditionDisplayName: "5e",
                Json: $$"""
                    {
                      "class": [
                        {
                          "name": "{{className}}",
                          "source": "{{sourceCode}}",
                          "hd": { "number": 1, "faces": 8 },
                          "classFeatures": [
                            "Class Feature|{{className}}|{{sourceCode}}|1"
                          ]
                        }
                      ],
                      "subclass": [
                        {
                          "name": "{{subclassName}}",
                          "shortName": "Threshold Path",
                          "source": "{{sourceCode}}",
                          "className": "{{className}}",
                          "classSource": "{{sourceCode}}",
                          "subclassFeatures": [
                            "Path Initiate|{{className}}|{{sourceCode}}|{{subclassName}}|{{sourceCode}}|3",
                            "Path Mastery|{{className}}|{{sourceCode}}|{{subclassName}}|{{sourceCode}}|7"
                          ]
                        }
                      ]
                    }
                    """,
                GameEdition: "5e"));

            var classEntity = Assert.Single(imported.Entities, value => value.EntityType == RuleConceptEntityTypes.Class);
            var subclassEntity = Assert.Single(imported.Entities, value => value.EntityType == RuleConceptEntityTypes.Subclass);
            var acceptedClass = (await normalization.AcceptAsync(classEntity.EntityId, actor))!;
            var acceptedSubclass = (await normalization.AcceptAsync(subclassEntity.EntityId, actor))!;
            var revisions = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == classEntity.EntityId
                    || value.SourceEntityId == subclassEntity.EntityId)
                .ToDictionaryAsync(value => value.SourceEntityId, value => value.Id);
            await globalRules.SetDecisionAsync(
                acceptedClass.Concept.Id,
                new SetGlobalRuleDecisionRequest(revisions[classEntity.EntityId], "Publish Class fixture."),
                actor);
            await globalRules.SetDecisionAsync(
                acceptedSubclass.Concept.Id,
                new SetGlobalRuleDecisionRequest(revisions[subclassEntity.EntityId], "Publish Subclass fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            var beforeThreshold = await projection.ResolveGlobalAsync(
                new CharacterRulesProjectionRequest(
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(
                            acceptedClass.Concept.Key,
                            2,
                            OccurrenceKey: "class-occurrence")
                    ]),
                userId: null);
            Assert.DoesNotContain(beforeThreshold.Choices, value =>
                value.Kind == "subclass"
                && value.SourceConceptKey == acceptedClass.Concept.Key);

            var atThreshold = await projection.ResolveGlobalAsync(
                new CharacterRulesProjectionRequest(
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(
                            acceptedClass.Concept.Key,
                            3,
                            OccurrenceKey: "class-occurrence")
                    ]),
                userId: null);
            var subclassChoice = Assert.Single(atThreshold.Choices, value =>
                value.Kind == "subclass"
                && value.SourceConceptKey == acceptedClass.Concept.Key);
            Assert.Equal(CharacterResolutionStates.ChoiceRequired, subclassChoice.State);
            Assert.Contains("class-occurrence", subclassChoice.ChoiceKey, StringComparison.Ordinal);
            var option = Assert.Single(subclassChoice.Options);
            Assert.Equal(acceptedSubclass.Concept.Key, option.ConceptKey);
            Assert.Equal(subclassName, option.DisplayName);

            var resolvedChoice = await projection.ResolveGlobalAsync(
                new CharacterRulesProjectionRequest(
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(
                            acceptedClass.Concept.Key,
                            3,
                            OccurrenceKey: "class-occurrence")
                    ],
                    Choices:
                    [
                        new CharacterRuntimeChoiceInput(
                            subclassChoice.ChoiceKey,
                            acceptedSubclass.Concept.Key)
                    ]),
                userId: null);
            var resolved = Assert.Single(resolvedChoice.Choices, value =>
                value.ChoiceKey == subclassChoice.ChoiceKey);
            Assert.Equal(CharacterResolutionStates.Resolved, resolved.State);
            Assert.Equal(acceptedSubclass.Concept.Key, resolved.SelectedValue);

            var tooEarlyStructuralSelection = await projection.ResolveGlobalAsync(
                new CharacterRulesProjectionRequest(
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(
                            acceptedClass.Concept.Key,
                            2,
                            OccurrenceKey: "class-occurrence"),
                        new CharacterAdvancementFactInput(
                            acceptedSubclass.Concept.Key,
                            2,
                            OccurrenceKey: "subclass-occurrence",
                            ParentConceptKey: acceptedClass.Concept.Key)
                    ]),
                userId: null);
            Assert.Contains(tooEarlyStructuralSelection.Conflicts, value =>
                value.Kind == "subclass-acquisition-level"
                && value.RelatedConceptKeys.Contains(acceptedSubclass.Concept.Key));

            var exaggeratedSubclassLevel = await projection.ResolveGlobalAsync(
                new CharacterRulesProjectionRequest(
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(
                            acceptedClass.Concept.Key,
                            3,
                            OccurrenceKey: "class-occurrence"),
                        new CharacterAdvancementFactInput(
                            acceptedSubclass.Concept.Key,
                            7,
                            OccurrenceKey: "subclass-occurrence",
                            ParentConceptKey: acceptedClass.Concept.Key)
                    ]),
                userId: null);
            Assert.Contains(exaggeratedSubclassLevel.Conflicts, value =>
                value.Kind == "subclass-parent-level-mismatch"
                && value.RelatedConceptKeys.Contains(acceptedSubclass.Concept.Key));
            Assert.Contains(exaggeratedSubclassLevel.Features, value =>
                value.SourceConceptKey == acceptedSubclass.Concept.Key
                && value.DisplayName == "Path Initiate"
                && value.AcquisitionLevel == 3);
            Assert.DoesNotContain(exaggeratedSubclassLevel.Features, value =>
                value.SourceConceptKey == acceptedSubclass.Concept.Key
                && value.DisplayName == "Path Mastery");
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
