using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Bootstrap;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class BaselineBootstrapIntegrationTests
{
    private const string BootstrapActor = "rules-core-bootstrap";

    private static readonly string[] BuiltInPackageKeys =
    ["wotc-srd-ogl", "wotc-srd-cc", "loot-tavern-free", "loot-tavern-licensed", "dorks-and-dice-baseline"];

    private static readonly string[] LegacyBuiltInHostedKeys =
    ["builtin-wotc-srd-3e", "builtin-wotc-srd-3-5e", "builtin-wotc-srd-5-1", "builtin-wotc-srd-5-2-1"];

    [Fact]
    public async Task FreshDatabaseGetsPublicSrdsAndSettledRulesWithoutOverwritingLaterChanges()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        var importer = new SourceImportService(db);
        var globalRules = new GlobalRulesService(db);
        var bootstrapper = new RulesCoreBaselineBootstrapper(db, importer, globalRules);
        var hostedService = new HostedSourceService(db, importer);

        await ResetAsync(db);
        try
        {
            // Simulate an installation upgraded from the old bootstrap path. Only an exact,
            // untouched historical seed is stale bootstrap state and may be retired.
            var historicalSeed = HistoricalHostedDefinition("builtin-wotc-srd-5-1");
            await hostedService.SetAsync(
                "builtin-wotc-srd-5-1",
                historicalSeed,
                BootstrapActor);

            var first = await bootstrapper.EnsureAsync();
            Assert.True(first.RulesBaselineApplied);
            Assert.NotNull(first.PublishedRuleset);
            Assert.Equal(1, first.PublishedRuleset!.RevisionNumber);
            Assert.True(first.PublishedRuleset.EntryCount > 6);
            Assert.Equal(6, first.HouseRuleSourceEntityCount);
            Assert.Equal(4, first.SourceAuthorityReferenceCount);
            Assert.Equal(0, first.HostedSourceDefinitionCount);

            var hosted = await hostedService.ListAsync(includeDisabled: true);
            Assert.DoesNotContain(hosted, value => LegacyBuiltInHostedKeys.Contains(value.Key));

            var packages = await db.SourcePackages
                .AsNoTracking()
                .Where(value => BuiltInPackageKeys.Contains(value.Key))
                .ToArrayAsync();
            Assert.Equal(5, packages.Length);
            var ogl = packages.Single(value => value.Key == "wotc-srd-ogl");
            var cc = packages.Single(value => value.Key == "wotc-srd-cc");
            var house = packages.Single(value => value.Key == "dorks-and-dice-baseline");
            Assert.True(ogl.IsPublic);
            Assert.True(cc.IsPublic);
            Assert.Equal("OGL-1.0a", ogl.License);
            Assert.Equal("CC-BY-4.0", cc.License);
            Assert.True(packages.Single(value => value.Key == "loot-tavern-free").IsPublic);
            Assert.False(packages.Single(value => value.Key == "loot-tavern-licensed").IsPublic);

            var anonymousSearch = new SourceEntitySearchService(db);
            foreach (var sourceCode in new[] { "SRD3", "SRD35", "SRD51", "SRD52" })
            {
                var visible = await anonymousSearch.SearchAccessibleAsync(
                    userId: null,
                    query: sourceCode,
                    limit: 1);
                Assert.NotEmpty(visible);
            }

            var oglCodes = await db.SourceEntities
                .Where(value => value.SourcePackageId == ogl.Id)
                .Select(value => value.SourceCode)
                .Distinct()
                .ToArrayAsync();
            Assert.Contains("SRD3", oglCodes);
            Assert.Contains("SRD35", oglCodes);
            var ccCodes = await db.SourceEntities
                .Where(value => value.SourcePackageId == cc.Id)
                .Select(value => value.SourceCode)
                .Distinct()
                .ToArrayAsync();
            Assert.Contains("SRD51", ccCodes);
            Assert.Contains("SRD52", ccCodes);

            Assert.Equal(4, await CountAuthorityReferencesAsync(db));
            var houseEntities = await db.SourceEntities
                .Where(value => value.SourcePackageId == house.Id)
                .ToArrayAsync();
            Assert.Equal(6, houseEntities.Length);
            Assert.All(houseEntities, value => Assert.Equal("DDBASE", value.SourceCode));

            var competencyConceptCount = await db.RuleConcepts.CountAsync(value =>
                value.EntityType == "skill" || value.EntityType == "tool");
            Assert.True(competencyConceptCount > 0);
            Assert.Equal(
                first.PublishedRuleset.EntryCount,
                await db.RuleConcepts.CountAsync());
            Assert.True(
                await db.RuleConceptSourceBindings.CountAsync()
                >= await db.RuleConcepts.CountAsync());
            Assert.Equal(
                first.PublishedRuleset.EntryCount,
                await db.GlobalRuleDecisions.CountAsync());
            Assert.Equal(1, await db.RulesetRevisions.CountAsync());

            var healing = await globalRules.ResolveLatestAsync("house.healing-potion-use", null);
            Assert.Equal("maximum possible healing",
                healing!.Document.GetProperty("action").GetProperty("healing").GetString());

            var concept = await db.RuleConcepts.SingleAsync(value => value.Key == "house.healing-potion-use");
            var selectedRevision = await db.GlobalRuleDecisions
                .Where(value => value.RuleConceptId == concept.Id)
                .OrderByDescending(value => value.DecisionNumber)
                .Select(value => value.SelectedSourceEntityRevisionId)
                .FirstAsync();
            await globalRules.SetDecisionAsync(
                concept.Id,
                new SetGlobalRuleDecisionRequest(selectedRevision, "User-maintained baseline decision."),
                "rules-lawyer");

            // A revision-1 definition under a reserved key is still deliberate configuration
            // when any content differs from the historical bootstrap seed. Do not delete it
            // merely because it was created by the old bootstrap actor.
            var modifiedBootstrap = await hostedService.SetAsync(
                "builtin-wotc-srd-5-1",
                historicalSeed with { Note = "Locally modified bootstrap-owned source policy" },
                BootstrapActor);
            Assert.Equal(1, modifiedBootstrap.RevisionNumber);

            var revisionCount = await db.SourceEntityRevisions.CountAsync();
            var second = await bootstrapper.EnsureAsync();
            Assert.False(second.RulesBaselineApplied);
            Assert.Null(second.PublishedRuleset);
            Assert.Equal(1, second.HostedSourceDefinitionCount);
            Assert.Equal(revisionCount, await db.SourceEntityRevisions.CountAsync());
            Assert.Equal(1, await db.RulesetRevisions.CountAsync());
            Assert.Equal(2, await db.GlobalRuleDecisions.CountAsync(value => value.RuleConceptId == concept.Id));

            var preservedFirstRevision = await hostedService.ListAsync(includeDisabled: true);
            var retainedBootstrap = Assert.Single(
                preservedFirstRevision.Where(value => value.Key == "builtin-wotc-srd-5-1"));
            Assert.Equal(1, retainedBootstrap.RevisionNumber);
            Assert.Equal(BootstrapActor, retainedBootstrap.CreatedByUserId);
            Assert.Equal("Locally modified bootstrap-owned source policy", retainedBootstrap.Note);

            // A later Rules Lawyer revision is also deliberate policy and must remain intact.
            var revised = await hostedService.SetAsync(
                "builtin-wotc-srd-5-1",
                historicalSeed with { Note = "Rules Lawyer retained source policy" },
                "rules-lawyer");
            Assert.Equal(2, revised.RevisionNumber);

            var third = await bootstrapper.EnsureAsync();
            Assert.False(third.RulesBaselineApplied);
            Assert.Null(third.PublishedRuleset);
            Assert.Equal(1, third.HostedSourceDefinitionCount);
            Assert.Equal(revisionCount, await db.SourceEntityRevisions.CountAsync());
            Assert.Equal(1, await db.RulesetRevisions.CountAsync());
            Assert.Equal(2, await db.GlobalRuleDecisions.CountAsync(value => value.RuleConceptId == concept.Id));

            var preserved = await hostedService.ListAsync(includeDisabled: true);
            var retained = Assert.Single(preserved.Where(value => value.Key == "builtin-wotc-srd-5-1"));
            Assert.Equal(2, retained.RevisionNumber);
            Assert.Equal("rules-lawyer", retained.CreatedByUserId);
            Assert.Equal("Rules Lawyer retained source policy", retained.Note);
        }
        finally
        {
            await ResetAsync(db);
        }
    }

    [Fact]
    public async Task FreshBaselinePublishesNormalizedReviewedCompetencyCorpusToCharacterMechanics()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        var importer = new SourceImportService(db);
        var globalRules = new GlobalRulesService(db);
        var bootstrapper = new RulesCoreBaselineBootstrapper(db, importer, globalRules);

        await ResetAsync(db);
        try
        {
            var first = await bootstrapper.EnsureAsync();
            Assert.True(first.RulesBaselineApplied);
            Assert.NotNull(first.PublishedRuleset);

            var expectedConceptKeys = await ReadReviewedCompetencyConceptKeysAsync(db);
            Assert.NotEmpty(expectedConceptKeys);

            var latestRevisionId = await db.RulesetRevisions
                .OrderByDescending(value => value.RevisionNumber)
                .Select(value => value.Id)
                .FirstAsync();
            var publishedCompetencyKeys = (await db.RulesetRevisionEntries
                    .AsNoTracking()
                    .Where(value => value.RulesetRevisionId == latestRevisionId
                        && (value.RuleConcept.EntityType == "skill"
                            || value.RuleConcept.EntityType == "tool"))
                    .Select(value => value.RuleConcept.Key)
                    .ToArrayAsync())
                .ToHashSet(StringComparer.Ordinal);
            Assert.Equal(
                expectedConceptKeys.OrderBy(value => value, StringComparer.Ordinal),
                publishedCompetencyKeys.OrderBy(value => value, StringComparer.Ordinal));

            Assert.Contains("skill.deception", publishedCompetencyKeys);
            Assert.DoesNotContain("skill.bluff", publishedCompetencyKeys);
            Assert.Contains("skill.medicine", publishedCompetencyKeys);
            Assert.DoesNotContain("skill.heal", publishedCompetencyKeys);
            Assert.Contains("skill.insight", publishedCompetencyKeys);
            Assert.DoesNotContain("skill.sense-motive", publishedCompetencyKeys);
            Assert.Contains("skill.arcana", publishedCompetencyKeys);
            Assert.DoesNotContain("skill.knowledge-arcana", publishedCompetencyKeys);
            Assert.Contains("skill.sleight-of-hand", publishedCompetencyKeys);
            Assert.Contains("tool.alchemists-supplies", publishedCompetencyKeys);
            Assert.DoesNotContain("skill.craft-alchemy", publishedCompetencyKeys);

            foreach (var retained in new[]
                     {
                         "skill.search",
                         "skill.spellcraft",
                         "skill.disable-device",
                         "skill.use-magic-device",
                         "skill.use-rope"
                     })
            {
                Assert.Contains(retained, publishedCompetencyKeys);
            }

            var mechanics = new CharacterMechanicsConsumerService(db);
            var catalog = await mechanics.GetGlobalAsync(userId: null);
            var competencyMechanics = catalog.Mechanics
                .Where(value => value.Competency is not null)
                .ToArray();
            Assert.NotEmpty(competencyMechanics);
            Assert.Equal(
                expectedConceptKeys.OrderBy(value => value, StringComparer.Ordinal),
                competencyMechanics
                    .Select(value => value.ConceptKey!)
                    .ToHashSet(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal));

            foreach (var definition in KnownMechanicalRelationships.All)
            {
                Assert.Contains(definition.Parent.ConceptKey, publishedCompetencyKeys);
                foreach (var component in definition.Components)
                {
                    Assert.Contains(component.ConceptKey, publishedCompetencyKeys);
                }

                var parent = Assert.Single(
                    competencyMechanics,
                    value => value.ConceptKey == definition.Parent.ConceptKey);
                var relationship = Assert.Single(
                    parent.Relationships,
                    value => value.RelationshipKey == definition.Key);
                Assert.True(relationship.CanResolve);
                Assert.Equal(
                    MechanicalRelationshipResolutionKinds.DeriveParent,
                    relationship.EffectiveResolutionKind);
            }

            var competencyProfiles = competencyMechanics
                .SelectMany(value => value.Competency!.Profiles)
                .ToArray();
            Assert.Contains(
                competencyProfiles,
                value => value.FamilyName == "Knowledge"
                    && !string.IsNullOrWhiteSpace(value.Specialty));

            // The checked-in SRD3/SRD35 snapshots have generic Craft, Perform, and Profession
            // entries. SRD3 has Alchemy, which converges directly to Alchemist's Supplies, but
            // that native record is not a Craft (...) specialty. Do not fabricate specialty
            // metadata that is absent from the reviewed source corpus. Translation coverage
            // below verifies all four parenthetical specialty families independently.
            Assert.Contains("skill.craft", publishedCompetencyKeys);
            Assert.Contains("skill.perform", publishedCompetencyKeys);
            Assert.Contains("skill.profession", publishedCompetencyKeys);

            var search = Assert.Single(
                competencyMechanics,
                value => value.ConceptKey == "skill.search");
            Assert.Contains(search.Competency!.Profiles, value => value.SupportsRanks);
            Assert.Contains(search.Competency.Profiles, value => value.SupportsClassSkillState);

            var deception = Assert.Single(
                competencyMechanics,
                value => value.ConceptKey == "skill.deception");
            Assert.Contains(deception.Competency!.Profiles, value => value.SupportsTrainingState);
            Assert.Contains(deception.Competency.Profiles, value => value.SupportsRanks);

            var alchemistsSupplies = Assert.Single(
                competencyMechanics,
                value => value.ConceptKey == "tool.alchemists-supplies");
            Assert.Contains(
                alchemistsSupplies.Competency!.Profiles,
                value => value.SupportsRanks && value.SupportsClassSkillState);

            var psionicSourceNames = await db.SourceEntities
                .AsNoTracking()
                .Where(value => (value.SourcePackage.Key == "wotc-srd-ogl"
                        || value.SourcePackage.Key == "wotc-srd-cc")
                    && value.EntityType == "skill"
                    && (value.Name.Contains("Psion")
                        || value.Name == "Autohypnosis"
                        || value.Name == "Psicraft"))
                .Select(value => value.Name)
                .Distinct()
                .ToArrayAsync();
            if (psionicSourceNames.Length > 0)
            {
                var psionicKeys = await ReadReviewedCompetencyConceptKeysAsync(
                    db,
                    psionicSourceNames);
                Assert.NotEmpty(psionicKeys);
                Assert.True(psionicKeys.All(publishedCompetencyKeys.Contains));
            }
        }
        finally
        {
            await ResetAsync(db);
        }
    }

    [Fact]
    public async Task ExistingSixRuleInstallationGetsIncrementalCompetencyRevisionIdempotently()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        var importer = new SourceImportService(db);
        var globalRules = new GlobalRulesService(db);
        var bootstrapper = new RulesCoreBaselineBootstrapper(db, importer, globalRules);

        await ResetAsync(db);
        try
        {
            // Hydrate the reviewed Source Layer once, then reduce only the Rules Layer to the
            // historical production shape: the six Dorks & Dice house rules in revision 1.
            await bootstrapper.EnsureAsync();
            var competencyConceptIds = await db.RuleConcepts
                .Where(value => value.EntityType == "skill" || value.EntityType == "tool")
                .Select(value => value.Id)
                .ToArrayAsync();
            await db.RulesetRevisionEntries.ExecuteDeleteAsync();
            await db.RulesetRevisions.ExecuteDeleteAsync();
            await db.GlobalRuleDecisions
                .Where(value => competencyConceptIds.Contains(value.RuleConceptId))
                .ExecuteDeleteAsync();
            await db.RuleConceptSourceBindings
                .Where(value => competencyConceptIds.Contains(value.RuleConceptId))
                .ExecuteDeleteAsync();
            await db.RuleConcepts
                .Where(value => competencyConceptIds.Contains(value.Id))
                .ExecuteDeleteAsync();
            db.ChangeTracker.Clear();

            var legacyRevision = await globalRules.PublishAsync("legacy-production-bootstrap");
            Assert.True(legacyRevision.CreatedRevision);
            Assert.Equal(1, legacyRevision.RevisionNumber);
            Assert.Equal(6, legacyRevision.EntryCount);

            var legacyEntries = await db.RulesetRevisionEntries
                .AsNoTracking()
                .Where(value => value.RulesetRevisionId == legacyRevision.Id)
                .OrderBy(value => value.RuleConceptId)
                .Select(value => new
                {
                    value.RuleConceptId,
                    value.GlobalRuleDecisionId,
                    value.SourceEntityRevisionId
                })
                .ToArrayAsync();
            var legacyDecisionCount = await db.GlobalRuleDecisions.CountAsync();

            var upgraded = await bootstrapper.EnsureAsync();
            Assert.True(upgraded.RulesBaselineApplied);
            Assert.NotNull(upgraded.PublishedRuleset);
            Assert.Equal(2, upgraded.PublishedRuleset!.RevisionNumber);
            Assert.True(upgraded.PublishedRuleset.EntryCount > 6);
            Assert.Equal(legacyDecisionCount, 6);

            var preservedEntries = await db.RulesetRevisionEntries
                .AsNoTracking()
                .Where(value => value.RulesetRevisionId == legacyRevision.Id)
                .OrderBy(value => value.RuleConceptId)
                .Select(value => new
                {
                    value.RuleConceptId,
                    value.GlobalRuleDecisionId,
                    value.SourceEntityRevisionId
                })
                .ToArrayAsync();
            Assert.Equal(legacyEntries, preservedEntries);

            var conceptCount = await db.RuleConcepts.CountAsync();
            var bindingCount = await db.RuleConceptSourceBindings.CountAsync();
            var decisionCount = await db.GlobalRuleDecisions.CountAsync();
            Assert.True(decisionCount > legacyDecisionCount);
            Assert.Equal(2, await db.RulesetRevisions.CountAsync());

            var rerun = await bootstrapper.EnsureAsync();
            Assert.False(rerun.RulesBaselineApplied);
            Assert.Null(rerun.PublishedRuleset);
            Assert.Equal(conceptCount, await db.RuleConcepts.CountAsync());
            Assert.Equal(bindingCount, await db.RuleConceptSourceBindings.CountAsync());
            Assert.Equal(decisionCount, await db.GlobalRuleDecisions.CountAsync());
            Assert.Equal(2, await db.RulesetRevisions.CountAsync());
        }
        finally
        {
            await ResetAsync(db);
        }
    }

    [Fact]
    public async Task BootstrapPreservesRulesLawyerCompetencyDecisionWithoutAdvancingIt()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        var importer = new SourceImportService(db);
        var globalRules = new GlobalRulesService(db);
        var bootstrapper = new RulesCoreBaselineBootstrapper(db, importer, globalRules);

        await ResetAsync(db);
        try
        {
            await bootstrapper.EnsureAsync();
            var search = await db.RuleConcepts.SingleAsync(value => value.Key == "skill.search");
            var currentDecision = await db.GlobalRuleDecisions
                .Where(value => value.RuleConceptId == search.Id)
                .OrderByDescending(value => value.DecisionNumber)
                .FirstAsync();

            var human = await globalRules.SetDecisionAsync(
                search.Id,
                new SetGlobalRuleDecisionRequest(
                    currentDecision.SelectedSourceEntityRevisionId,
                    "Rules Lawyer-authored competency decision."),
                "rules-lawyer");
            Assert.True(human.Created);
            await globalRules.PublishAsync("rules-lawyer");
            var revisionCount = await db.RulesetRevisions.CountAsync();

            var rerun = await bootstrapper.EnsureAsync();
            Assert.False(rerun.RulesBaselineApplied);
            Assert.Null(rerun.PublishedRuleset);

            var latest = await db.GlobalRuleDecisions
                .Where(value => value.RuleConceptId == search.Id)
                .OrderByDescending(value => value.DecisionNumber)
                .FirstAsync();
            Assert.Equal(human.Value.Id, latest.Id);
            Assert.Equal(human.Value.DecisionNumber, latest.DecisionNumber);
            Assert.Equal("rules-lawyer", latest.CreatedByUserId);
            Assert.Equal(
                2,
                await db.GlobalRuleDecisions.CountAsync(value => value.RuleConceptId == search.Id));
            Assert.Equal(revisionCount, await db.RulesetRevisions.CountAsync());
        }
        finally
        {
            await ResetAsync(db);
        }
    }

    private static async Task<HashSet<string>> ReadReviewedCompetencyConceptKeysAsync(
        RulesCoreDbContext db,
        IReadOnlyCollection<string>? restrictNames = null)
    {
        var names = restrictNames?.ToArray();
        var query = db.SourceEntities
            .AsNoTracking()
            .Where(value => (value.SourcePackage.Key == "wotc-srd-ogl"
                    || value.SourcePackage.Key == "wotc-srd-cc")
                && (value.SourceCode == "SRD3"
                    || value.SourceCode == "SRD35"
                    || value.SourceCode == "SRD51"
                    || value.SourceCode == "SRD52")
                && (value.EntityType == "skill" || value.EntityType == "tool"));
        if (names is not null)
        {
            query = query.Where(value => names.Contains(value.Name));
        }

        var sources = await query
            .Select(value => new
            {
                value.EntityType,
                value.Name,
                value.NativeIdentityJson
            })
            .ToArrayAsync();
        var method = typeof(SourceNormalizationService).GetMethod(
            "BuildSuggestedConceptKey",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "SourceNormalizationService.BuildSuggestedConceptKey is unavailable.");

        return sources
            .Select(value => (string)(method.Invoke(
                null,
                [value.EntityType, value.Name, value.NativeIdentityJson])
                ?? throw new InvalidOperationException("Suggested concept key was null.")))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static SetHostedSourceDefinitionRequest HistoricalHostedDefinition(string definitionKey)
    {
        var type = typeof(RulesCoreBaselineBootstrapper).Assembly.GetType(
            "RulesCore.Infrastructure.Bootstrap.BuiltInSrdHostedSources",
            throwOnError: true)!;
        var field = type.GetField("Definitions", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Historical hosted SRD definitions are unavailable.");
        var definitions = (IEnumerable)(field.GetValue(null)
            ?? throw new InvalidOperationException("Historical hosted SRD definitions are unavailable."));

        foreach (var definition in definitions)
        {
            if (definition is null) continue;
            var definitionType = definition.GetType();
            var key = definitionType.GetProperty("Key")?.GetValue(definition) as string;
            if (!string.Equals(key, definitionKey, StringComparison.Ordinal)) continue;
            return (SetHostedSourceDefinitionRequest)(definitionType.GetProperty("Request")?.GetValue(definition)
                ?? throw new InvalidOperationException($"Historical hosted source '{definitionKey}' has no request."));
        }

        throw new InvalidOperationException($"Historical hosted source '{definitionKey}' was not found.");
    }

    private static async Task<int> CountAuthorityReferencesAsync(RulesCoreDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM source_package_authority_reference;";
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
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
            DELETE FROM hosted_source_definition
            WHERE definition_key IN ('builtin-wotc-srd-3e','builtin-wotc-srd-3-5e','builtin-wotc-srd-5-1','builtin-wotc-srd-5-2-1');
            """);
        db.ChangeTracker.Clear();
        var packages = await db.SourcePackages.Where(value => BuiltInPackageKeys.Contains(value.Key)).ToArrayAsync();
        db.SourcePackages.RemoveRange(packages);
        await db.SaveChangesAsync();
    }
}
