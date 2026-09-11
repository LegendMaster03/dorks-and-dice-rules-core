using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class LegacyNormalizationIntegrationTests
{
    [Fact]
    public async Task ThreeEAndThreeFiveRaceNamesConvergeOnOneAdvisoryConcept()
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

        var packageKey = $"legacy-normalization-{Guid.NewGuid():N}";
        var importer = new SourceImportService(db);
        var normalizer = new SourceNormalizationService(db);

        try
        {
            var threeJson = LegacySrdDocumentInspector.ConvertToCanonicalJson(
                """
                    <html><body>
                    <h1>Dwarf</h1>
                    <p>3e dwarf racial traits.</p>
                    </body></html>
                    """,
                "https://www.dragon.ee/30srd/dwarf.htm",
                "SRD3",
                out _);
            var threeFiveJson = LegacySrdDocumentInspector.ConvertToCanonicalJson(
                """
                    # RACES

                    ## Dwarves
                    3.5e dwarf racial traits.
                    """,
                "https://raw.githubusercontent.com/olimot/srd-v3.5-md/main/basic-rules-and-legal/races.md",
                "SRD35",
                out _);

            var threeImport = await importer.Import5eToolsDocumentAsync(
                LegacyImportRequest(packageKey, "srd-3e", "3e SRD", "3e", threeJson));
            var threeFiveImport = await importer.Import5eToolsDocumentAsync(
                LegacyImportRequest(packageKey, "srd-3-5e", "3.5e SRD", "3.5e", threeFiveJson));

            var threeEntity = Assert.Single(
                threeImport.Entities,
                value => value.EntityType == "race" && value.Name == "Dwarf");
            var threeFiveEntity = Assert.Single(
                threeFiveImport.Entities,
                value => value.EntityType == "race" && value.Name == "Dwarf");

            var candidates = await normalizer.GetCandidatesAsync(
                "rules-lawyer",
                entityType: "race",
                query: "Dwarf");
            Assert.Equal(2, candidates.Count);
            Assert.All(candidates, value =>
            {
                Assert.Equal("race.dwarf", value.SuggestedConceptKey);
                Assert.Equal(SourceNormalizationSuggestionKinds.NewConcept, value.SuggestionKind);
            });

            var acceptedThree = await normalizer.AcceptAsync(threeEntity.EntityId, "rules-lawyer");
            Assert.NotNull(acceptedThree);
            Assert.True(acceptedThree!.CreatedConcept);
            Assert.True(acceptedThree.CreatedBinding);
            Assert.Equal("race.dwarf", acceptedThree.Concept.Key);

            var remaining = await normalizer.GetCandidatesAsync(
                "rules-lawyer",
                entityType: "race",
                query: "Dwarf");
            var threeFiveCandidate = Assert.Single(remaining);
            Assert.Equal(threeFiveEntity.EntityId, threeFiveCandidate.SourceEntityId);
            Assert.Equal(SourceNormalizationSuggestionKinds.ExistingConcept, threeFiveCandidate.SuggestionKind);
            Assert.Equal(acceptedThree.Concept.Id, threeFiveCandidate.SuggestedConceptId);

            var acceptedThreeFive = await normalizer.AcceptAsync(threeFiveEntity.EntityId, "rules-lawyer");
            Assert.NotNull(acceptedThreeFive);
            Assert.False(acceptedThreeFive!.CreatedConcept);
            Assert.True(acceptedThreeFive.CreatedBinding);
            Assert.Equal(acceptedThree.Concept.Id, acceptedThreeFive.Concept.Id);

            Assert.Equal(2, await db.RuleConceptSourceBindings.CountAsync(
                value => value.RuleConceptId == acceptedThree.Concept.Id));
            Assert.Equal(0, await db.GlobalRuleDecisions.CountAsync(
                value => value.RuleConceptId == acceptedThree.Concept.Id));
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

    private static Import5eToolsDocumentRequest LegacyImportRequest(
        string packageKey,
        string workKey,
        string editionDisplayName,
        string gameEdition,
        string json) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: "Legacy Normalization Integration Package",
            Provider: "integration-test",
            License: "OGL-1.0a",
            IsPublic: true,
            WorkKey: workKey,
            WorkDisplayName: editionDisplayName,
            EditionKey: "original",
            EditionDisplayName: editionDisplayName,
            Json: json,
            GameEdition: gameEdition,
            ReleaseKind: "srd",
            PublicationDate: null);

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
