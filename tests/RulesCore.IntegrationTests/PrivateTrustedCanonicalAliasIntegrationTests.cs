using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class PrivateTrustedCanonicalAliasIntegrationTests
{
    [Fact]
    public async Task TrustedAliasReusesCanonicalIdentityWithoutExposingAnotherUsersPrivateSource()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..12];
        var userA = $"trusted-private-a-{token}";
        var userB = $"trusted-private-b-{token}";
        var packageAKey = $"trusted-private-a-{token}";
        var packageBKey = $"trusted-private-b-{token}";
        var conceptKey = $"feat.trusted-private-{token}";
        var entityName = $"Trusted Private Rule {token}";
        var aliasValue = $"pcgen|feat|trusted-private|{token}|1";
        Guid? publishedRulesetId = null;
        Guid? conceptId = null;

        try
        {
            var importer = new NormalizedSourceImportService(db);
            var grants = new SourceGrantService(db);
            var catalog = new SourceCatalogService(db);
            var rules = new GlobalRulesService(db);

            var semanticA = JsonSerializer.Serialize(new
            {
                name = entityName,
                mechanic = new { bonus = 2 }
            });
            var rawA = JsonSerializer.Serialize(new
            {
                name = entityName,
                source = "PRIVATE-A",
                entries = new[] { "User A private source body." },
                privateMarker = "user-a-private-copy"
            });
            var importedA = await importer.ImportAsync(Request(
                packageAKey,
                "fixture-private-a",
                entityName,
                $"fixture|feat|{token}",
                rawA,
                semanticA,
                sourceUri: null));
            await grants.GrantAsync(userA, importedA.PackageId);
            var entityA = Assert.Single(importedA.Entities);
            var revisionA = await LatestRevisionAsync(db, entityA.EntityId);
            var bindingA = await CanonicalBindingAsync(db, entityA.EntityId);

            var concept = (await rules.CreateConceptAsync(
                new CreateRuleConceptRequest(conceptKey, "feat", entityName),
                userA)).Value;
            conceptId = concept.Id;
            Assert.True((await rules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(entityA.EntityId),
                userA)).Created);
            await rules.SetDecisionAsync(
                concept.Id,
                new SetGlobalRuleDecisionRequest(revisionA.RevisionId, "Trusted-alias privacy fixture."),
                userA);
            var published = await rules.PublishAsync(userA);
            publishedRulesetId = published.Id;

            Assert.Null(await catalog.GetLatestAccessibleEntityAsync(entityA.EntityId, userB));
            Assert.False(await grants.HasGrantAsync(userB, importedA.PackageId));
            Assert.Null(await rules.ResolveLatestAsync(conceptKey, userB));

            var semanticB = JsonSerializer.Serialize(new
            {
                entityType = "feat",
                segments = new[]
                {
                    new { Tag = "BONUS", Value = "COMBAT|DAMAGE|2" }
                }
            });
            Assert.NotEqual(
                CanonicalSourceIdentity.SemanticFingerprint(semanticA),
                CanonicalSourceIdentity.SemanticFingerprint(semanticB));

            await new CanonicalBootstrapReconciliationService(db).RecordAsync(
                new RecordCanonicalBootstrapReconciliationRequest(
                    "pcgen-org-pcgen",
                    aliasValue,
                    CanonicalSourceIdentity.SemanticFingerprint(semanticB),
                    CanonicalBootstrapReconciliationClassifications.ExactIdentity,
                    bindingA.CanonicalEntityId,
                    RelatedCanonicalEntityId: null,
                    "integration-bootstrap-confirmation",
                    1.0,
                    "integration-test"));

            var rawB = JsonSerializer.Serialize(new
            {
                format = "pcgen-data",
                name = entityName,
                raw = "BONUS:COMBAT|DAMAGE|2",
                pcgenMarker = "user-b-own-copy"
            });
            var importedB = await importer.ImportAsync(Request(
                packageBKey,
                PcGenSourceFormatAdapter.Format,
                entityName,
                aliasValue,
                rawB,
                semanticB,
                sourceUri: "https://raw.githubusercontent.com/PCGen/pcgen/master/data/35e/example/example_feats.lst"));
            await grants.GrantAsync(userB, importedB.PackageId);
            var entityB = Assert.Single(importedB.Entities);
            var revisionB = await LatestRevisionAsync(db, entityB.EntityId);
            var bindingB = await CanonicalBindingAsync(db, entityB.EntityId);

            Assert.NotEqual(importedA.PackageId, importedB.PackageId);
            Assert.NotEqual(entityA.EntityId, entityB.EntityId);
            Assert.NotEqual(revisionA.RevisionId, revisionB.RevisionId);
            Assert.NotEqual(revisionA.RepresentationId, revisionB.RepresentationId);
            Assert.Equal(bindingA.CanonicalEntityId, bindingB.CanonicalEntityId);
            Assert.NotEqual(bindingA.SemanticFingerprint, bindingB.SemanticFingerprint);
            Assert.EndsWith(":strong-alias", bindingB.MatchKind, StringComparison.Ordinal);

            Assert.Null(await catalog.GetLatestAccessibleEntityAsync(entityA.EntityId, userB));
            Assert.NotNull(await catalog.GetLatestAccessibleEntityAsync(entityB.EntityId, userB));
            Assert.False(await grants.HasGrantAsync(userA, importedB.PackageId));

            var resolvedForB = await rules.ResolveLatestAsync(conceptKey, userB);
            Assert.NotNull(resolvedForB);
            Assert.Equal(entityB.EntityId, resolvedForB!.SourceEntityId);
            Assert.Equal("user-b-own-copy", resolvedForB.Document.GetProperty("pcgenMarker").GetString());
            Assert.False(resolvedForB.Document.TryGetProperty("privateMarker", out _));

            var resolvedForA = await rules.ResolveLatestAsync(conceptKey, userA);
            Assert.NotNull(resolvedForA);
            Assert.Equal(entityA.EntityId, resolvedForA!.SourceEntityId);
            Assert.Equal("user-a-private-copy", resolvedForA.Document.GetProperty("privateMarker").GetString());
            Assert.False(resolvedForA.Document.TryGetProperty("pcgenMarker", out _));
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

            db.ChangeTracker.Clear();
            await db.SourcePackages
                .Where(value => value.Key == packageAKey || value.Key == packageBKey)
                .ExecuteDeleteAsync();
        }
    }

    private static ImportNormalizedSourceRequest Request(
        string packageKey,
        string formatKey,
        string entityName,
        string nativeKey,
        string rawJson,
        string semanticJson,
        string? sourceUri)
    {
        var publicationKey = $"publication-{entityName}";
        return new ImportNormalizedSourceRequest(
            packageKey,
            $"Private trusted alias package {packageKey}",
            "integration-test",
            License: null,
            IsPublic: false,
            new NormalizedSourceRepresentation(
                formatKey,
                new SourceRepresentationArtifact(
                    $"{packageKey}.data",
                    Encoding.UTF8.GetBytes(rawJson),
                    $"integration:{packageKey}",
                    SourceUri: sourceUri,
                    MediaType: "text/plain"),
                [new NormalizedSourceRecord(
                    "feat",
                    entityName,
                    SourceCode: "PRIVATE",
                    NativeKey: nativeKey,
                    RawJson: rawJson,
                    LocatorKey: "entry:1",
                    PublicationLocalKey: publicationKey,
                    SemanticJson: semanticJson)],
                [new NormalizedSourcePublication(
                    publicationKey,
                    $"Private Trusted Alias Publication {entityName}",
                    Publisher: "Integration Test Press",
                    GameEdition: "3.5e")]));
    }

    private static async Task<(Guid RevisionId, Guid RepresentationId)> LatestRevisionAsync(
        RulesCoreDbContext db,
        Guid sourceEntityId)
    {
        var revision = await db.SourceEntityRevisions
            .Where(value => value.SourceEntityId == sourceEntityId)
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => new
            {
                RevisionId = value.Id,
                RepresentationId = value.SourceRepresentationId
            })
            .FirstAsync();
        return (revision.RevisionId, revision.RepresentationId);
    }

    private static async Task<(Guid CanonicalEntityId, string SemanticFingerprint, string MatchKind)> CanonicalBindingAsync(
        RulesCoreDbContext db,
        Guid sourceEntityId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT occurrence.canonical_entity_id, binding.semantic_fingerprint, binding.match_kind
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                JOIN source_entity_revision revision
                    ON revision.source_entity_revision_id = binding.source_entity_revision_id
                WHERE binding.source_entity_id = @source_entity_id
                    AND occurrence.canonical_entity_id IS NOT NULL
                ORDER BY revision.revision_number DESC
                LIMIT 1;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("Source entity did not receive a canonical binding.");
            }
            return (reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
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
