using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class SubclassRuleConceptRelationshipIntegrationTests
{
    [Fact]
    public async Task NormalizedSubclassPublishesParentClassRelationshipWithoutCollapsingIntoClass()
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

        var token = Guid.NewGuid().ToString("N")[..12];
        var packageKey = $"subclass-relationship-{token}";
        var actor = $"rules-lawyer-{token}";
        var importer = new SourceImportService(db);
        var normalization = new SourceNormalizationService(db);
        var globalRules = new GlobalRulesService(db);
        var catalog = new ResolvedRulesCatalogService(db);

        try
        {
            var imported = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                PackageKey: packageKey,
                PackageDisplayName: "Subclass relationship fixture",
                Provider: "integration-test",
                License: "test-only",
                IsPublic: true,
                WorkKey: "fixture-work",
                WorkDisplayName: "Fixture Work",
                EditionKey: "5e",
                EditionDisplayName: "5e",
                Json: """
                    {
                      "class": [
                        {
                          "name": "Wizard",
                          "source": "TST",
                          "hd": { "number": 1, "faces": 6 }
                        }
                      ],
                      "subclass": [
                        {
                          "name": "School of Evocation",
                          "shortName": "Evocation",
                          "source": "TST",
                          "className": "Wizard",
                          "classSource": "TST"
                        }
                      ]
                    }
                    """,
                GameEdition: "5e"));

            var classEntity = Assert.Single(imported.Entities, value => value.EntityType == "class");
            var subclassEntity = Assert.Single(imported.Entities, value => value.EntityType == "subclass");

            var acceptedClass = await normalization.AcceptAsync(classEntity.EntityId, actor);
            Assert.NotNull(acceptedClass);
            Assert.Equal("class.wizard", acceptedClass!.Concept.Key);
            Assert.Equal(RuleConceptEntityTypes.Class, acceptedClass.Concept.EntityType);

            var acceptedSubclass = await normalization.AcceptAsync(subclassEntity.EntityId, actor);
            Assert.NotNull(acceptedSubclass);
            Assert.Equal("subclass.wizard.school-of-evocation", acceptedSubclass!.Concept.Key);
            Assert.Equal(RuleConceptEntityTypes.Subclass, acceptedSubclass.Concept.EntityType);
            Assert.NotEqual(acceptedClass.Concept.Id, acceptedSubclass.Concept.Id);

            var classRevisionId = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == classEntity.EntityId)
                .Select(value => value.Id)
                .SingleAsync();
            var subclassRevisionId = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == subclassEntity.EntityId)
                .Select(value => value.Id)
                .SingleAsync();

            await globalRules.SetDecisionAsync(
                acceptedClass.Concept.Id,
                new SetGlobalRuleDecisionRequest(classRevisionId, "Publish fixture Class."),
                actor);
            await globalRules.SetDecisionAsync(
                acceptedSubclass.Concept.Id,
                new SetGlobalRuleDecisionRequest(subclassRevisionId, "Publish fixture Subclass."),
                actor);
            await globalRules.PublishAsync(actor);

            var resolved = await catalog.GetGlobalAsync(
                userId: null,
                entityType: RuleConceptEntityTypes.Subclass,
                query: "Evocation");
            AssertPublishedSubclassRelationship(
                resolved,
                acceptedSubclass.Concept.Id,
                acceptedClass.Concept.Id);

            // Simulate upgrading a database whose Class/Subclass bindings predate the explicit
            // relationship contract. The first resolved-catalog read must reconstruct the stable
            // relationship without asking a Rules Lawyer to re-accept the source.
            await db.Database.ExecuteSqlRawAsync("""
                DELETE FROM rule_concept_relationship;
                DELETE FROM rule_concept_relationship_backfill
                WHERE backfill_key = 'subclass-parent-v1';
                """);

            var backfilled = await catalog.GetGlobalAsync(
                userId: null,
                entityType: RuleConceptEntityTypes.Subclass,
                query: "Evocation");
            AssertPublishedSubclassRelationship(
                backfilled,
                acceptedSubclass.Concept.Id,
                acceptedClass.Concept.Id);
        }
        finally
        {
            await ResetRulesAsync(db);
            db.ChangeTracker.Clear();
            var packages = await db.SourcePackages
                .Where(value => value.Key == packageKey)
                .ToArrayAsync();
            db.SourcePackages.RemoveRange(packages);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task LegacyPrestigeClassRemainsDistinctInResolvedCatalog()
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

        var token = Guid.NewGuid().ToString("N")[..12];
        var packageKey = $"prestige-class-{token}";
        var actor = $"rules-lawyer-{token}";
        var importer = new SourceImportService(db);
        var normalization = new SourceNormalizationService(db);
        var globalRules = new GlobalRulesService(db);
        var catalog = new ResolvedRulesCatalogService(db);

        try
        {
            var legacyJson = LegacySrdDocumentInspector.ConvertToCanonicalJson(
                """
                    <html><body>
                    <h1>Arcane Archer</h1>
                    <p>A prestige class fixture.</p>
                    </body></html>
                    """,
                "https://www.dragon.ee/30srd/arcane_archer.htm",
                "SRD3",
                out _);
            var imported = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                PackageKey: packageKey,
                PackageDisplayName: "Prestige Class fixture",
                Provider: "integration-test",
                License: "test-only",
                IsPublic: true,
                WorkKey: "srd-3e",
                WorkDisplayName: "3e SRD",
                EditionKey: "original",
                EditionDisplayName: "3e",
                Json: legacyJson,
                GameEdition: "3e"));

            var prestigeEntity = Assert.Single(
                imported.Entities,
                value => value.EntityType == RuleConceptEntityTypes.PrestigeClass);
            var accepted = await normalization.AcceptAsync(prestigeEntity.EntityId, actor);
            Assert.NotNull(accepted);
            Assert.Equal("prestigeclass.arcane-archer", accepted!.Concept.Key);
            Assert.Equal(RuleConceptEntityTypes.PrestigeClass, accepted.Concept.EntityType);

            var revisionId = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == prestigeEntity.EntityId)
                .Select(value => value.Id)
                .SingleAsync();
            await globalRules.SetDecisionAsync(
                accepted.Concept.Id,
                new SetGlobalRuleDecisionRequest(revisionId, "Publish fixture Prestige Class."),
                actor);
            await globalRules.PublishAsync(actor);

            var prestigeCatalog = await catalog.GetGlobalAsync(
                userId: null,
                entityType: RuleConceptEntityTypes.PrestigeClass,
                query: "Arcane Archer");
            var prestigeClass = Assert.Single(prestigeCatalog.Rules);
            Assert.Equal(RuleConceptEntityTypes.PrestigeClass, prestigeClass.EntityType);
            Assert.Equal("prestigeclass.arcane-archer", prestigeClass.ConceptKey);
            Assert.Empty(prestigeClass.Relationships);

            var ordinaryClassCatalog = await catalog.GetGlobalAsync(
                userId: null,
                entityType: RuleConceptEntityTypes.Class,
                query: "Arcane Archer");
            Assert.Empty(ordinaryClassCatalog.Rules);
        }
        finally
        {
            await ResetRulesAsync(db);
            db.ChangeTracker.Clear();
            var packages = await db.SourcePackages
                .Where(value => value.Key == packageKey)
                .ToArrayAsync();
            db.SourcePackages.RemoveRange(packages);
            await db.SaveChangesAsync();
        }
    }

    [Theory]
    [InlineData("class", RuleConceptEntityTypes.Class)]
    [InlineData("SUBCLASS", RuleConceptEntityTypes.Subclass)]
    [InlineData("prestigeClass", RuleConceptEntityTypes.PrestigeClass)]
    [InlineData("PRESTIGECLASS", RuleConceptEntityTypes.PrestigeClass)]
    public void FirstClassAdvancementEntityTypesHaveStableApiNames(string input, string expected)
    {
        Assert.Equal(expected, RuleConceptEntityTypes.Normalize(input));
    }

    private static void AssertPublishedSubclassRelationship(
        ResolvedRulesCatalogView resolved,
        Guid subclassConceptId,
        Guid classConceptId)
    {
        var subclass = Assert.Single(resolved.Rules);
        Assert.Equal(subclassConceptId, subclass.RuleConceptId);
        Assert.Equal(RuleConceptEntityTypes.Subclass, subclass.EntityType);

        var parent = Assert.Single(subclass.Relationships);
        Assert.Equal(RuleConceptRelationshipKinds.ParentClass, parent.Kind);
        Assert.Equal(classConceptId, parent.RelatedRuleConceptId);
        Assert.Equal("class.wizard", parent.RelatedConceptKey);
        Assert.Equal(RuleConceptEntityTypes.Class, parent.RelatedEntityType);
        Assert.Equal("Wizard", parent.RelatedDisplayName);
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
