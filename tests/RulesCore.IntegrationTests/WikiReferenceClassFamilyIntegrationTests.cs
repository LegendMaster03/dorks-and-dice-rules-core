using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Domain.Rules;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class WikiReferenceClassFamilyIntegrationTests
{
    [Fact]
    public async Task SourceOnlyClassFamilyNavigationUsesNativeIdentityAndPreservesAccessScope()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<RulesCoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new RulesCoreDbContext(options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        await ResetRulesAsync(db);

        var token = Guid.NewGuid().ToString("N")[..12];
        var userId = $"class-family-user-{token}";
        var classPackageKey = $"wiki-class-family-class-{token}";
        var publicSubclassPackageKey = $"wiki-class-family-public-subclass-{token}";
        var privateSubclassPackageKey = $"wiki-class-family-private-subclass-{token}";
        var importer = new SourceImportService(db);

        try
        {
            var classes = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                PackageKey: classPackageKey,
                PackageDisplayName: "Class family classes",
                Provider: "integration-test",
                License: "test-only",
                IsPublic: true,
                WorkKey: "class-family-base",
                WorkDisplayName: "Class Family Base",
                EditionKey: "5e",
                EditionDisplayName: "5e",
                Json: """
                    {
                      "class": [
                        { "name": "Wizard", "source": "BASE", "hd": { "number": 1, "faces": 6 } },
                        { "name": "Fighter", "source": "BASE", "hd": { "number": 1, "faces": 10 } }
                      ]
                    }
                    """,
                GameEdition: "5e"));

            var publicSubclasses = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                PackageKey: publicSubclassPackageKey,
                PackageDisplayName: "Class family public subclasses",
                Provider: "integration-test",
                License: "test-only",
                IsPublic: true,
                WorkKey: "class-family-expansion",
                WorkDisplayName: "Class Family Expansion",
                EditionKey: "5e",
                EditionDisplayName: "5e",
                Json: """
                    {
                      "subclass": [
                        {
                          "name": "Shared School",
                          "shortName": "Shared",
                          "source": "EXP-WIZ",
                          "className": "Wizard",
                          "classSource": "BASE"
                        },
                        {
                          "name": "Shared School",
                          "shortName": "Shared",
                          "source": "EXP-FTR",
                          "className": "Fighter",
                          "classSource": "BASE"
                        }
                      ]
                    }
                    """,
                GameEdition: "5e"));

            var privateSubclasses = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                PackageKey: privateSubclassPackageKey,
                PackageDisplayName: "Class family private subclasses",
                Provider: "integration-test",
                License: "test-only",
                IsPublic: false,
                WorkKey: "class-family-private",
                WorkDisplayName: "Class Family Private",
                EditionKey: "5e",
                EditionDisplayName: "5e",
                Json: """
                    {
                      "subclass": [
                        {
                          "name": "Private School",
                          "shortName": "Private",
                          "source": "PRIVATE",
                          "className": "Wizard",
                          "classSource": "BASE"
                        }
                      ]
                    }
                    """,
                GameEdition: "5e"));

            var wiki = new WikiReferenceCatalogService(db);
            var family = new WikiReferenceClassFamilyService(db);
            var wizardCatalog = await wiki.GetGlobalCatalogAsync(
                userId: null,
                entityType: RuleConceptEntityTypes.Class,
                categoryMode: WikiReferenceCategoryModes.AnyVariation,
                query: "Wizard",
                sourceCode: null,
                packageKey: null,
                edition: null,
                limit: 20,
                offset: 0);
            var wizard = Assert.Single(wizardCatalog.References, value => value.DisplayName == "Wizard");
            Assert.Null(wizard.RuleConceptId);

            var anonymousFamily = await family.GetGlobalAsync(null, wizard.ReferenceIdentity);
            Assert.NotNull(anonymousFamily);
            Assert.Equal("global", anonymousFamily!.Scope);
            var publicWizardChild = Assert.Single(anonymousFamily.Subclasses);
            Assert.Equal("Shared School", publicWizardChild.DisplayName);
            Assert.Null(publicWizardChild.RuleConceptId);

            var publicWizardChildDetail = await wiki.GetGlobalDetailAsync(
                userId: null,
                publicWizardChild.ReferenceIdentity);
            Assert.NotNull(publicWizardChildDetail);
            Assert.Contains(
                publicWizardChildDetail!.Variations,
                value => value.SourceCode == "EXP-WIZ");
            Assert.DoesNotContain(
                publicWizardChildDetail.Variations,
                value => value.SourceCode == "EXP-FTR");

            var childFamily = await family.GetGlobalAsync(null, publicWizardChild.ReferenceIdentity);
            Assert.NotNull(childFamily);
            var parent = Assert.Single(childFamily!.ParentClasses);
            Assert.Equal(wizard.ReferenceIdentity, parent.ReferenceIdentity);
            Assert.Equal("Wizard", parent.DisplayName);
            Assert.Single(childFamily.Subclasses);
            Assert.Equal(publicWizardChild.ReferenceIdentity, childFamily.Subclasses[0].ReferenceIdentity);

            db.UserSourceGrants.Add(new UserSourceGrant
            {
                Id = Guid.NewGuid(),
                SourcePackageId = privateSubclasses.PackageId,
                UserId = userId,
                GrantedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();

            var grantedFamily = await family.GetGlobalAsync(userId, wizard.ReferenceIdentity);
            Assert.NotNull(grantedFamily);
            Assert.Equal(2, grantedFamily!.Subclasses.Count);
            Assert.Contains(grantedFamily.Subclasses, value => value.DisplayName == "Shared School");
            Assert.Contains(grantedFamily.Subclasses, value => value.DisplayName == "Private School");

            var campaignId = Guid.NewGuid();
            var campaignFamily = await family.GetCampaignAsync(campaignId, userId, wizard.ReferenceIdentity);
            Assert.NotNull(campaignFamily);
            Assert.Equal("campaign", campaignFamily!.Scope);
            Assert.Equal(campaignId, campaignFamily.CampaignId);
            Assert.Equal(2, campaignFamily.Subclasses.Count);

            var importedSourceIds = classes.Entities
                .Concat(publicSubclasses.Entities)
                .Concat(privateSubclasses.Entities)
                .Select(value => value.EntityId)
                .ToArray();
            Assert.Empty(await db.RuleConceptSourceBindings
                .Where(value => value.SourceEntityId.HasValue
                    && importedSourceIds.Contains(value.SourceEntityId.Value))
                .ToArrayAsync());
        }
        finally
        {
            await ResetRulesAsync(db);
            db.ChangeTracker.Clear();
            var packageKeys = new[]
            {
                classPackageKey,
                publicSubclassPackageKey,
                privateSubclassPackageKey
            };
            var packages = await db.SourcePackages
                .Where(value => packageKeys.Contains(value.Key))
                .ToArrayAsync();
            db.SourcePackages.RemoveRange(packages);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task RuleConceptBackedClassFamilyNavigationStillResolvesReferenceIdentities()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<RulesCoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new RulesCoreDbContext(options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        await ResetRulesAsync(db);

        var token = Guid.NewGuid().ToString("N")[..12];
        var packageKey = $"wiki-class-family-normalized-{token}";
        var actor = $"rules-lawyer-{token}";
        var importer = new SourceImportService(db);
        var normalization = new SourceNormalizationService(db);
        var globalRules = new GlobalRulesService(db);

        try
        {
            var imported = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                PackageKey: packageKey,
                PackageDisplayName: "Normalized class family",
                Provider: "integration-test",
                License: "test-only",
                IsPublic: true,
                WorkKey: "normalized-class-family",
                WorkDisplayName: "Normalized Class Family",
                EditionKey: "5e",
                EditionDisplayName: "5e",
                Json: """
                    {
                      "class": [
                        { "name": "Wizard", "source": "BASE", "hd": { "number": 1, "faces": 6 } }
                      ],
                      "subclass": [
                        {
                          "name": "School of Evocation",
                          "shortName": "Evocation",
                          "source": "EXP",
                          "className": "Wizard",
                          "classSource": "BASE"
                        }
                      ]
                    }
                    """,
                GameEdition: "5e"));

            var classEntity = Assert.Single(imported.Entities, value => value.EntityType == "class");
            var subclassEntity = Assert.Single(imported.Entities, value => value.EntityType == "subclass");
            var acceptedClass = (await normalization.AcceptAsync(classEntity.EntityId, actor))!;
            var acceptedSubclass = (await normalization.AcceptAsync(subclassEntity.EntityId, actor))!;

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
                new SetGlobalRuleDecisionRequest(classRevisionId, "Publish class family parent."),
                actor);
            await globalRules.SetDecisionAsync(
                acceptedSubclass.Concept.Id,
                new SetGlobalRuleDecisionRequest(subclassRevisionId, "Publish class family child."),
                actor);
            await globalRules.PublishAsync(actor);

            var family = new WikiReferenceClassFamilyService(db);
            var classView = await family.GetGlobalAsync(null, acceptedClass.Concept.Key);
            Assert.NotNull(classView);
            var child = Assert.Single(classView!.Subclasses);
            Assert.Equal(acceptedSubclass.Concept.Id, child.RuleConceptId);
            Assert.Equal(acceptedSubclass.Concept.Key, child.ReferenceIdentity);

            var subclassView = await family.GetGlobalAsync(null, acceptedSubclass.Concept.Key);
            Assert.NotNull(subclassView);
            var parent = Assert.Single(subclassView!.ParentClasses);
            Assert.Equal(acceptedClass.Concept.Id, parent.RuleConceptId);
            Assert.Equal(acceptedClass.Concept.Key, parent.ReferenceIdentity);
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
