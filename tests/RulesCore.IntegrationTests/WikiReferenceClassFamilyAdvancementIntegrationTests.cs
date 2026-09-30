using System.Text.Json;
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
public sealed class WikiReferenceClassFamilyAdvancementIntegrationTests
{
    [Fact]
    public void PrestigeClassFeatureProjectionPreservesExistingClassFeaturePrecedence()
    {
        using var both = JsonDocument.Parse("""
            {
              "classFeatures": ["Legacy Feature|Prestige|SRC|1"],
              "prestigeClassFeatures": ["Prestige Feature|Prestige|SRC|1"]
            }
            """);

        var projected = ClassFamilyFeatureReferenceParser.Project("prestigeClass", both.RootElement);
        var feature = Assert.Single(projected);
        Assert.Equal("Legacy Feature", feature.Name);
        Assert.Equal(1, feature.Level);

        using var prestigeOnly = JsonDocument.Parse("""
            {
              "prestigeClassFeatures": ["Prestige Feature|Prestige|SRC|1"]
            }
            """);

        var fallback = ClassFamilyFeatureReferenceParser.Project("prestigeClass", prestigeOnly.RootElement);
        var fallbackFeature = Assert.Single(fallback);
        Assert.Equal("Prestige Feature", fallbackFeature.Name);
        Assert.Equal(1, fallbackFeature.Level);
    }

    [Fact]
    public async Task NativeFiveXImportsExposeAuthoritativeWikiAdvancementMetadataAndMatchCharacterProjection()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var factory = new WebApplicationFactory<Program>();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        await ResetRulesAsync(db);

        var token = Guid.NewGuid().ToString("N")[..8];
        var actor = $"wiki-advancement-{token}";
        var packageKey = $"wiki-advancement-5e-{token}";
        var package55Key = $"wiki-advancement-55e-{token}";
        var sourceCode = $"P3{token}";
        var source55Code = $"N5{token}";
        var className = $"Phase Three Mage {token}";
        var subclassName = $"Arcane Path {token}";
        var class55Name = $"Revised Mage {token}";
        var importer = new SourceImportService(db);
        var normalization = new SourceNormalizationService(db);
        var globalRules = new GlobalRulesService(db);
        var wiki = new WikiReferenceCatalogService(db);
        var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

        try
        {
            var imported = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                PackageKey: packageKey,
                PackageDisplayName: $"Wiki advancement 5e {token}",
                Provider: "integration-test",
                License: "test-only",
                IsPublic: true,
                WorkKey: $"wiki-advancement-work-{token}",
                WorkDisplayName: $"Wiki Advancement Work {token}",
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
                            "Arcane Study|{{className}}|{{sourceCode}}|1",
                            { "classFeature": "Focused Study|{{className}}|{{sourceCode}}|3" },
                            "Unresolved Class Feature"
                          ]
                        }
                      ],
                      "subclass": [
                        {
                          "name": "{{subclassName}}",
                          "shortName": "Arcane Path",
                          "source": "{{sourceCode}}",
                          "className": "{{className}}",
                          "classSource": "{{sourceCode}}",
                          "subclassFeatures": [
                            "Path Initiate|{{className}}|{{sourceCode}}|{{subclassName}}|{{sourceCode}}|3",
                            { "subclassFeature": "Path Adept|{{className}}|{{sourceCode}}|{{subclassName}}|{{sourceCode}}|7" },
                            "Unresolved Subclass Feature"
                          ]
                        }
                      ]
                    }
                    """,
                GameEdition: "5e"));

            var imported55 = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                PackageKey: package55Key,
                PackageDisplayName: $"Wiki advancement 5.5e {token}",
                Provider: "integration-test",
                License: "test-only",
                IsPublic: true,
                WorkKey: $"wiki-advancement-work-55-{token}",
                WorkDisplayName: $"Wiki Advancement Work 5.5e {token}",
                EditionKey: "5.5e",
                EditionDisplayName: "5.5e",
                Json: $$"""
                    {
                      "class": [
                        {
                          "name": "{{class55Name}}",
                          "source": "{{source55Code}}",
                          "hd": { "number": 1, "faces": 8 },
                          "classFeatures": [
                            "Revised Study|{{class55Name}}|{{source55Code}}|1",
                            "Revised Mastery|{{class55Name}}|{{source55Code}}|5"
                          ]
                        }
                      ]
                    }
                    """,
                GameEdition: "5.5e"));

            var classEntity = Assert.Single(imported.Entities, value => value.EntityType == RuleConceptEntityTypes.Class);
            var subclassEntity = Assert.Single(imported.Entities, value => value.EntityType == RuleConceptEntityTypes.Subclass);
            var class55Entity = Assert.Single(imported55.Entities, value => value.EntityType == RuleConceptEntityTypes.Class);

            var classReference = await FindReferenceAsync(wiki, RuleConceptEntityTypes.Class, className);
            Assert.Null(classReference.RuleConceptId);
            var classDetail = await wiki.GetGlobalDetailAsync(null, classReference.ReferenceIdentity);
            Assert.NotNull(classDetail);
            var classVariation = Assert.Single(classDetail!.Variations);
            Assert.Collection(
                classVariation.AdvancementFeatures,
                feature => Assert.Equal(("Arcane Study", (int?)1), (feature.Name, feature.Level)),
                feature => Assert.Equal(("Focused Study", (int?)3), (feature.Name, feature.Level)),
                feature => Assert.Equal(("Unresolved Class Feature", (int?)null), (feature.Name, feature.Level)));
            Assert.Equal(
                classVariation.AdvancementFeatures.Select(value => (value.Name, value.Level)),
                classDetail.EffectiveAdvancementFeatures.Select(value => (value.Name, value.Level)));

            var subclassReference = await FindReferenceAsync(wiki, RuleConceptEntityTypes.Subclass, subclassName);
            Assert.Null(subclassReference.RuleConceptId);
            var subclassDetail = await wiki.GetGlobalDetailAsync(null, subclassReference.ReferenceIdentity);
            Assert.NotNull(subclassDetail);
            var subclassVariation = Assert.Single(subclassDetail!.Variations);
            Assert.Collection(
                subclassVariation.AdvancementFeatures,
                feature => Assert.Equal(("Path Initiate", (int?)3), (feature.Name, feature.Level)),
                feature => Assert.Equal(("Path Adept", (int?)7), (feature.Name, feature.Level)),
                feature => Assert.Equal(("Unresolved Subclass Feature", (int?)null), (feature.Name, feature.Level)));
            Assert.Equal(
                subclassVariation.AdvancementFeatures.Select(value => (value.Name, value.Level)),
                subclassDetail.EffectiveAdvancementFeatures.Select(value => (value.Name, value.Level)));

            var class55Reference = await FindReferenceAsync(wiki, RuleConceptEntityTypes.Class, class55Name);
            Assert.Null(class55Reference.RuleConceptId);
            var class55Detail = await wiki.GetGlobalDetailAsync(null, class55Reference.ReferenceIdentity);
            Assert.NotNull(class55Detail);
            Assert.Equal("5.5e", class55Detail!.Reference.EffectiveEditionKey);
            Assert.Collection(
                Assert.Single(class55Detail.Variations).AdvancementFeatures,
                feature => Assert.Equal(("Revised Study", (int?)1), (feature.Name, feature.Level)),
                feature => Assert.Equal(("Revised Mastery", (int?)5), (feature.Name, feature.Level)));

            var importedSourceIds = imported.Entities
                .Concat(imported55.Entities)
                .Select(value => value.EntityId)
                .ToArray();
            Assert.Empty(await db.RuleConceptSourceBindings
                .Where(value => value.SourceEntityId.HasValue
                    && importedSourceIds.Contains(value.SourceEntityId.Value))
                .ToArrayAsync());
            Assert.Empty(await db.RuleConcepts.ToArrayAsync());

            var serialized = JsonSerializer.Serialize(
                classDetail,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.Contains("\"advancementFeatures\"", serialized, StringComparison.Ordinal);
            Assert.Contains("\"effectiveAdvancementFeatures\"", serialized, StringComparison.Ordinal);
            Assert.Contains("\"level\":null", serialized, StringComparison.Ordinal);

            var acceptedClass = (await normalization.AcceptAsync(classEntity.EntityId, actor))!;
            var acceptedSubclass = (await normalization.AcceptAsync(subclassEntity.EntityId, actor))!;
            var revisions = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == classEntity.EntityId
                    || value.SourceEntityId == subclassEntity.EntityId)
                .ToDictionaryAsync(value => value.SourceEntityId, value => value.Id);
            await globalRules.SetDecisionAsync(
                acceptedClass.Concept.Id,
                new SetGlobalRuleDecisionRequest(revisions[classEntity.EntityId], "Publish class advancement parity fixture."),
                actor);
            await globalRules.SetDecisionAsync(
                acceptedSubclass.Concept.Id,
                new SetGlobalRuleDecisionRequest(revisions[subclassEntity.EntityId], "Publish subclass advancement parity fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            var character = await projection.ResolveGlobalAsync(
                new CharacterRulesProjectionRequest(
                    BaseAbilityScores: new Dictionary<string, int>
                    {
                        ["strength"] = 10,
                        ["dexterity"] = 12,
                        ["constitution"] = 12,
                        ["intelligence"] = 16,
                        ["wisdom"] = 12,
                        ["charisma"] = 10
                    },
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(acceptedClass.Concept.Key, 7),
                        new CharacterAdvancementFactInput(acceptedSubclass.Concept.Key, 7)
                    ]),
                userId: null);

            AssertCharacterFeatureMatchesWiki(character, classVariation.AdvancementFeatures, "Arcane Study");
            AssertCharacterFeatureMatchesWiki(character, classVariation.AdvancementFeatures, "Focused Study");
            AssertCharacterFeatureMatchesWiki(character, subclassVariation.AdvancementFeatures, "Path Initiate");
            AssertCharacterFeatureMatchesWiki(character, subclassVariation.AdvancementFeatures, "Path Adept");
        }
        finally
        {
            await ResetRulesAsync(db);
            db.ChangeTracker.Clear();
            var packageKeys = new[] { packageKey, package55Key };
            var packages = await db.SourcePackages
                .Where(value => packageKeys.Contains(value.Key))
                .ToArrayAsync();
            db.SourcePackages.RemoveRange(packages);
            await db.SaveChangesAsync();
        }
    }

    private static async Task<WikiReferenceItemView> FindReferenceAsync(
        WikiReferenceCatalogService wiki,
        string entityType,
        string name)
    {
        var catalog = await wiki.GetGlobalCatalogAsync(
            userId: null,
            entityType: entityType,
            categoryMode: WikiReferenceCategoryModes.AnyVariation,
            query: name,
            sourceCode: null,
            packageKey: null,
            edition: null,
            limit: 20,
            offset: 0);
        return Assert.Single(catalog.References, value => value.DisplayName == name);
    }

    private static void AssertCharacterFeatureMatchesWiki(
        CharacterRulesProjectionView character,
        IReadOnlyList<WikiReferenceAdvancementFeatureView> wikiFeatures,
        string name)
    {
        var wikiFeature = Assert.Single(wikiFeatures, value => value.Name == name);
        var characterFeature = Assert.Single(
            character.Features,
            value => value.DisplayName == name
                && value.State == CharacterResolutionStates.Resolved);
        Assert.Equal(wikiFeature.Level, characterFeature.AcquisitionLevel);
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
