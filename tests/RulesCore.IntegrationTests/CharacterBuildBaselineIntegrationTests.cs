using Microsoft.EntityFrameworkCore;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Bootstrap;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CharacterBuildBaselineIntegrationTests
{
    private static readonly string[] BuiltInPackageKeys =
    ["wotc-srd-ogl", "wotc-srd-cc", "loot-tavern-free", "loot-tavern-licensed", "dorks-and-dice-baseline"];

    [Fact]
    public async Task FreshBaselinePublishesCanonicalBuilderCatalogAndSpeciesRelationships()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>()
                .UseNpgsql(connectionString)
                .Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        var importer = new SourceImportService(db);
        var globalRules = new GlobalRulesService(db);
        var bootstrapper = new RulesCoreBaselineBootstrapper(db, importer, globalRules);

        await ResetAsync(db);
        try
        {
            var result = await bootstrapper.EnsureAsync();
            Assert.True(result.RulesBaselineApplied);
            Assert.NotNull(result.PublishedRuleset);

            var latestRevisionId = await db.RulesetRevisions
                .OrderByDescending(value => value.RevisionNumber)
                .Select(value => value.Id)
                .FirstAsync();
            var publishedEntityTypes = (await db.RulesetRevisionEntries
                    .AsNoTracking()
                    .Where(value => value.RulesetRevisionId == latestRevisionId)
                    .Select(value => value.RuleConcept.EntityType)
                    .Distinct()
                    .ToArrayAsync())
                .ToHashSet(StringComparer.Ordinal);

            foreach (var expected in new[]
                     {
                         RuleConceptEntityTypes.Species,
                         RuleConceptEntityTypes.Subspecies,
                         "background",
                         RuleConceptEntityTypes.Class,
                         RuleConceptEntityTypes.Subclass,
                         "feat",
                         "item",
                         "spell"
                     })
            {
                Assert.Contains(expected, publishedEntityTypes);
            }
            Assert.DoesNotContain("race", publishedEntityTypes);
            Assert.DoesNotContain("subrace", publishedEntityTypes);

            Assert.True(await CountCanonicalParentSpeciesRelationshipsAsync(db) > 0);

            var catalog = new ResolvedRulesCatalogService(db);
            var subspeciesCatalog = await catalog.GetGlobalAsync(
                userId: null,
                entityType: RuleConceptEntityTypes.Subspecies);
            Assert.NotEmpty(subspeciesCatalog.Rules);

            var parentSpeciesRelationships = subspeciesCatalog.Rules
                .SelectMany(value => value.Relationships)
                .Where(value => string.Equals(
                    value.Kind,
                    RuleConceptRelationshipKinds.ParentSpecies,
                    StringComparison.Ordinal))
                .ToArray();
            Assert.NotEmpty(parentSpeciesRelationships);
            Assert.All(
                parentSpeciesRelationships,
                relationship => Assert.Equal(
                    RuleConceptEntityTypes.Species,
                    relationship.RelatedEntityType));

            var catalogSubspecies = subspeciesCatalog.Rules.First(value =>
                value.Relationships.Any(relationship => string.Equals(
                    relationship.Kind,
                    RuleConceptRelationshipKinds.ParentSpecies,
                    StringComparison.Ordinal)));
            var catalogParent = Assert.Single(catalogSubspecies.Relationships.Where(relationship =>
                string.Equals(
                    relationship.Kind,
                    RuleConceptRelationshipKinds.ParentSpecies,
                    StringComparison.Ordinal)));
            var directRules = new RelationshipAwareGlobalRulesService(globalRules, db);
            var directSubspecies = await directRules.ResolveLatestAsync(
                catalogSubspecies.ConceptKey,
                userId: null);
            Assert.NotNull(directSubspecies);
            var directParent = Assert.Single(directSubspecies!.Relationships!, relationship =>
                string.Equals(
                    relationship.Kind,
                    RuleConceptRelationshipKinds.ParentSpecies,
                    StringComparison.Ordinal));
            Assert.Equal(catalogParent.RelatedConceptKey, directParent.RelatedConceptKey);
            Assert.Equal(RuleConceptEntityTypes.Species, directParent.RelatedEntityType);
        }
        finally
        {
            await ResetAsync(db);
        }
    }

    private static async Task<int> CountCanonicalParentSpeciesRelationshipsAsync(
        RulesCoreDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM rule_concept_relationship relationship
                JOIN rule_concept child
                  ON child.rule_concept_id = relationship.from_rule_concept_id
                JOIN rule_concept parent
                  ON parent.rule_concept_id = relationship.to_rule_concept_id
                WHERE relationship.relationship_kind = 'parent-species'
                  AND child.entity_type = 'subspecies'
                  AND parent.entity_type = 'species';
                """;
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task ResetAsync(RulesCoreDbContext db)
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
        await db.Database.ExecuteSqlRawAsync("""
            DO $$
            BEGIN
                IF to_regclass('public.hosted_source_definition') IS NOT NULL THEN
                    DELETE FROM hosted_source_definition
                    WHERE definition_key IN (
                        'builtin-wotc-srd-3e',
                        'builtin-wotc-srd-3-5e',
                        'builtin-wotc-srd-5-1',
                        'builtin-wotc-srd-5-2-1');
                END IF;
            END
            $$;
            """);
        db.ChangeTracker.Clear();

        var packages = await db.SourcePackages
            .Where(value => BuiltInPackageKeys.Contains(value.Key))
            .ToArrayAsync();
        db.SourcePackages.RemoveRange(packages);
        await db.SaveChangesAsync();
    }
}
