using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class PrivateCanonicalRuleReuseIntegrationTests
{
    [Fact]
    public async Task EquivalentPrivateImportsReuseCanonicalRuleIdentityWithoutSharingSourceContent()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..12];
        var userA = $"private-a-{token}";
        var userB = $"private-b-{token}";
        var packageAKey = $"private-canonical-a-{token}";
        var packageBKey = $"private-canonical-b-{token}";
        var conceptKey = $"feat.private-canonical-{token}";
        var entityName = $"Private Canonical Test {token}";
        Guid? publishedRulesetId = null;
        Guid? conceptId = null;

        try
        {
            var importer = new NormalizedSourceImportService(db);
            var grants = new SourceGrantService(db);
            var rules = new GlobalRulesService(db);
            var catalog = new SourceCatalogService(db);

            var importedA = await importer.ImportAsync(Request(
                packageAKey,
                "Private package A",
                entityName,
                $"PA{token}",
                "Private publication A"));
            await grants.GrantAsync(userA, importedA.PackageId);
            var entityA = Assert.Single(importedA.Entities);
            var revisionAId = await LatestRevisionIdAsync(db, entityA.EntityId);

            var concept = (await rules.CreateConceptAsync(
                new CreateRuleConceptRequest(conceptKey, "feat", entityName),
                userA)).Value;
            conceptId = concept.Id;
            var bindingA = await rules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(entityA.EntityId),
                userA);
            Assert.True(bindingA.Created);

            await rules.SetDecisionAsync(
                concept.Id,
                new SetGlobalRuleDecisionRequest(revisionAId, "Private canonical acceptance fixture."),
                userA);
            var published = await rules.PublishAsync(userA);
            publishedRulesetId = published.Id;

            Assert.Null(await catalog.GetLatestAccessibleEntityAsync(entityA.EntityId, userB));
            Assert.Null(await rules.ResolveLatestAsync(conceptKey, userB));

            var importedB = await importer.ImportAsync(Request(
                packageBKey,
                "Private package B",
                entityName,
                $"PB{token}",
                "Independently acquired publication B"));
            await grants.GrantAsync(userB, importedB.PackageId);
            var entityB = Assert.Single(importedB.Entities);

            Assert.NotEqual(importedA.PackageId, importedB.PackageId);
            Assert.NotEqual(entityA.EntityId, entityB.EntityId);
            Assert.Null(await catalog.GetLatestAccessibleEntityAsync(entityA.EntityId, userB));
            Assert.NotNull(await catalog.GetLatestAccessibleEntityAsync(entityB.EntityId, userB));

            var canonicalA = await CanonicalEntityIdAsync(db, entityA.EntityId);
            var canonicalB = await CanonicalEntityIdAsync(db, entityB.EntityId);
            Assert.NotEqual(Guid.Empty, canonicalA);
            Assert.Equal(canonicalA, canonicalB);

            var binding = Assert.Single(await db.RuleConceptSourceBindings
                .AsNoTracking()
                .Where(value => value.RuleConceptId == concept.Id)
                .ToArrayAsync());
            Assert.Equal(canonicalA, binding.CanonicalEntityId);
            Assert.Equal(entityA.EntityId, binding.SourceEntityId);

            var resolvedForB = await rules.ResolveLatestAsync(conceptKey, userB);
            Assert.NotNull(resolvedForB);
            Assert.Equal(entityB.EntityId, resolvedForB!.SourceEntityId);
            Assert.NotEqual(entityA.EntityId, resolvedForB.SourceEntityId);
            Assert.Equal(entityName, resolvedForB.SourceEntityName);
            Assert.Equal("You gain one test benefit.",
                resolvedForB.Document.GetProperty("entries")[0].GetString());

            var resolvedForA = await rules.ResolveLatestAsync(conceptKey, userA);
            Assert.NotNull(resolvedForA);
            Assert.Equal(entityA.EntityId, resolvedForA!.SourceEntityId);
        }
        finally
        {
            if (publishedRulesetId.HasValue)
            {
                await db.RulesetRevisionEntries
                    .Where(value => value.RulesetRevisionId == publishedRulesetId.Value)
                    .ExecuteDeleteAsync();
                await db.RulesetRevisions
                    .Where(value => value.Id == publishedRulesetId.Value)
                    .ExecuteDeleteAsync();
            }
            if (conceptId.HasValue)
            {
                await db.GlobalRuleDecisions
                    .Where(value => value.RuleConceptId == conceptId.Value)
                    .ExecuteDeleteAsync();
                await db.RuleConceptSourceBindings
                    .Where(value => value.RuleConceptId == conceptId.Value)
                    .ExecuteDeleteAsync();
                await db.RuleConcepts
                    .Where(value => value.Id == conceptId.Value)
                    .ExecuteDeleteAsync();
            }

            var packages = await db.SourcePackages
                .Where(value => value.Key == packageAKey || value.Key == packageBKey)
                .ToArrayAsync();
            if (packages.Length > 0)
            {
                db.SourcePackages.RemoveRange(packages);
                await db.SaveChangesAsync();
            }
        }
    }

    private static ImportNormalizedSourceRequest Request(
        string packageKey,
        string packageDisplayName,
        string entityName,
        string sourceCode,
        string publicationName)
    {
        var rawJson = JsonSerializer.Serialize(new
        {
            name = entityName,
            source = sourceCode,
            entries = new[] { "You gain one test benefit." }
        });
        const string publicationKey = "private-publication";
        return new ImportNormalizedSourceRequest(
            packageKey,
            packageDisplayName,
            "integration-test",
            License: "test-only",
            IsPublic: false,
            new NormalizedSourceRepresentation(
                FiveEToolsSourceFormatAdapter.Format,
                new SourceRepresentationArtifact(
                    $"{packageKey}.json",
                    System.Text.Encoding.UTF8.GetBytes(rawJson),
                    $"integration:{packageKey}"),
                [new NormalizedSourceRecord(
                    "feat",
                    entityName,
                    sourceCode,
                    NativeKey: $"feat|{sourceCode}|{entityName}",
                    RawJson: rawJson,
                    PublicationLocalKey: publicationKey)],
                [new NormalizedSourcePublication(
                    publicationKey,
                    publicationName,
                    Publisher: "Private Test Press",
                    GameEdition: "5e")])) ;
    }

    private static async Task<Guid> LatestRevisionIdAsync(RulesCoreDbContext db, Guid sourceEntityId) =>
        await db.SourceEntityRevisions
            .Where(value => value.SourceEntityId == sourceEntityId)
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => value.Id)
            .FirstAsync();

    private static async Task<Guid> CanonicalEntityIdAsync(RulesCoreDbContext db, Guid sourceEntityId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT occurrence.canonical_entity_id
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                WHERE binding.source_entity_id = @source_entity_id;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            var value = await command.ExecuteScalarAsync();
            return value is Guid id ? id : Guid.Empty;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
