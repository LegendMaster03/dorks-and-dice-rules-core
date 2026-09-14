using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CanonicalBootstrapReconciliationIntegrationTests
{
    [Fact]
    public async Task UnresolvedTrustedIdentityCanBePromotedToConfirmedExactAlias()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var imported = await new NormalizedSourceImportService(db).ImportAsync(SeedRequest(token));
            var canonicalEntityId = await CanonicalEntityIdAsync(db, Assert.Single(imported.Entities).EntityId);
            var aliasValue = $"pcgen|feat|bootstrap|{token}|1";
            var aliasFingerprint = CanonicalSourceIdentity.SemanticFingerprint(
                JsonSerializer.Serialize(new
                {
                    entityType = "feat",
                    segments = new[] { new { Tag = "BONUS", Value = "+2" } }
                }));
            var service = new CanonicalBootstrapReconciliationService(db);

            var unresolved = await service.RecordAsync(new RecordCanonicalBootstrapReconciliationRequest(
                "pcgen-org-pcgen",
                aliasValue,
                aliasFingerprint,
                CanonicalBootstrapReconciliationClassifications.Unresolved,
                CanonicalEntityId: null,
                RelatedCanonicalEntityId: null,
                "manual-review",
                0.5,
                "integration-test",
                "Awaiting independent confirmation."));
            Assert.Null(await new CanonicalEntityAliasStore(db).ResolveAsync(
                "pcgen-org-pcgen",
                aliasValue,
                aliasFingerprint));

            var confirmed = await service.RecordAsync(new RecordCanonicalBootstrapReconciliationRequest(
                "pcgen-org-pcgen",
                aliasValue,
                aliasFingerprint,
                CanonicalBootstrapReconciliationClassifications.ExactIdentity,
                canonicalEntityId,
                RelatedCanonicalEntityId: null,
                "manual-review",
                1.0,
                "integration-test",
                "Confirmed against an independent representation."));

            Assert.Equal(unresolved.Id, confirmed.Id);
            Assert.Equal(CanonicalBootstrapReconciliationClassifications.ExactIdentity, confirmed.Classification);
            Assert.Equal(canonicalEntityId, confirmed.CanonicalEntityId);
            Assert.Equal(
                canonicalEntityId,
                await new CanonicalEntityAliasStore(db).ResolveAsync(
                    "pcgen-org-pcgen",
                    aliasValue,
                    aliasFingerprint));
            Assert.Equal(
                confirmed,
                await service.GetAsync("pcgen-org-pcgen", aliasValue, aliasFingerprint));
        }
    }

    [Fact]
    public async Task NonExactClassificationsPersistWithoutCreatingUnconfirmedTrustedAliases()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var service = new CanonicalBootstrapReconciliationService(db);
            var aliasStore = new CanonicalEntityAliasStore(db);
            var classifications = new[]
            {
                CanonicalBootstrapReconciliationClassifications.Reprint,
                CanonicalBootstrapReconciliationClassifications.Revision,
                CanonicalBootstrapReconciliationClassifications.Rename,
                CanonicalBootstrapReconciliationClassifications.Variant,
                CanonicalBootstrapReconciliationClassifications.SameNameDifferentEntity,
                CanonicalBootstrapReconciliationClassifications.BadSourceData,
                CanonicalBootstrapReconciliationClassifications.ParserError,
                CanonicalBootstrapReconciliationClassifications.Unresolved
            };

            foreach (var classification in classifications)
            {
                var aliasValue = $"pcgen|{classification}|{token}";
                var fingerprint = CanonicalSourceIdentity.Fingerprint($"{classification}:{token}");
                var recorded = await service.RecordAsync(new RecordCanonicalBootstrapReconciliationRequest(
                    "pcgen-org-pcgen-newsources",
                    aliasValue,
                    fingerprint,
                    classification,
                    CanonicalEntityId: null,
                    RelatedCanonicalEntityId: null,
                    "bootstrap-review",
                    0.75,
                    "integration-test"));

                Assert.Equal(classification, recorded.Classification);
                Assert.Null(recorded.CanonicalEntityId);
                Assert.Null(await aliasStore.ResolveAsync(
                    "pcgen-org-pcgen-newsources",
                    aliasValue,
                    fingerprint));
            }
        }
    }

    [Theory]
    [InlineData(CanonicalBootstrapReconciliationClassifications.Reprint)]
    [InlineData(CanonicalBootstrapReconciliationClassifications.Revision)]
    [InlineData(CanonicalBootstrapReconciliationClassifications.Rename)]
    [InlineData(CanonicalBootstrapReconciliationClassifications.Variant)]
    public async Task ConfirmedRelationshipSeedsCandidateAliasAndDirectedCanonicalRelationship(
        string classification)
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var baseToken = $"{token}-base";
            var candidateToken = $"{token}-candidate";
            var importer = new NormalizedSourceImportService(db);
            var baseImport = await importer.ImportAsync(SeedRequest(baseToken));
            var candidateImport = await importer.ImportAsync(SeedRequest(candidateToken));
            var baseCanonicalEntityId = await CanonicalEntityIdAsync(
                db,
                Assert.Single(baseImport.Entities).EntityId);
            var candidateCanonicalEntityId = await CanonicalEntityIdAsync(
                db,
                Assert.Single(candidateImport.Entities).EntityId);
            Assert.NotEqual(baseCanonicalEntityId, candidateCanonicalEntityId);

            var aliasValue = $"pcgen|{classification}|{token}|candidate";
            var fingerprint = CanonicalSourceIdentity.SemanticFingerprint(SeedJson(candidateToken));
            var service = new CanonicalBootstrapReconciliationService(db);
            var recorded = await service.RecordAsync(new RecordCanonicalBootstrapReconciliationRequest(
                "pcgen-org-pcgen",
                aliasValue,
                fingerprint,
                classification,
                candidateCanonicalEntityId,
                baseCanonicalEntityId,
                "manual-bootstrap-confirmation",
                1.0,
                "integration-test"));

            Assert.Equal(candidateCanonicalEntityId, recorded.CanonicalEntityId);
            Assert.Equal(baseCanonicalEntityId, recorded.RelatedCanonicalEntityId);
            Assert.Equal(
                candidateCanonicalEntityId,
                await new CanonicalEntityAliasStore(db).ResolveAsync(
                    "pcgen-org-pcgen",
                    aliasValue,
                    fingerprint));

            var relationship = await ReadRelationshipAsync(
                db,
                baseCanonicalEntityId,
                candidateCanonicalEntityId,
                classification);
            Assert.NotNull(relationship);
            Assert.Equal("manual-bootstrap-confirmation", relationship.Value.EvidenceKind);
            Assert.Equal(1.0, relationship.Value.Confidence);
            Assert.Null(await ReadRelationshipAsync(
                db,
                candidateCanonicalEntityId,
                baseCanonicalEntityId,
                classification));
        }
    }

    [Fact]
    public async Task RelationshipClassificationCanBeCompletedWithoutReclassification()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var baseToken = $"{token}-base";
            var candidateToken = $"{token}-candidate";
            var importer = new NormalizedSourceImportService(db);
            var baseImport = await importer.ImportAsync(SeedRequest(baseToken));
            var candidateImport = await importer.ImportAsync(SeedRequest(candidateToken));
            var baseCanonicalEntityId = await CanonicalEntityIdAsync(
                db,
                Assert.Single(baseImport.Entities).EntityId);
            var candidateCanonicalEntityId = await CanonicalEntityIdAsync(
                db,
                Assert.Single(candidateImport.Entities).EntityId);
            var aliasValue = $"pcgen|revision|pending|{token}";
            var fingerprint = CanonicalSourceIdentity.SemanticFingerprint(SeedJson(candidateToken));
            var service = new CanonicalBootstrapReconciliationService(db);

            var classified = await service.RecordAsync(new RecordCanonicalBootstrapReconciliationRequest(
                "pcgen-org-pcgen-newsources",
                aliasValue,
                fingerprint,
                CanonicalBootstrapReconciliationClassifications.Revision,
                CanonicalEntityId: null,
                RelatedCanonicalEntityId: null,
                "bootstrap-review",
                0.75,
                "integration-test",
                "Classification confirmed; endpoints still being reconciled."));

            var completed = await service.RecordAsync(new RecordCanonicalBootstrapReconciliationRequest(
                "pcgen-org-pcgen-newsources",
                aliasValue,
                fingerprint,
                CanonicalBootstrapReconciliationClassifications.Revision,
                candidateCanonicalEntityId,
                baseCanonicalEntityId,
                "bootstrap-review",
                1.0,
                "integration-test",
                "Canonical endpoints independently confirmed."));

            Assert.Equal(classified.Id, completed.Id);
            Assert.Equal(candidateCanonicalEntityId, completed.CanonicalEntityId);
            Assert.Equal(baseCanonicalEntityId, completed.RelatedCanonicalEntityId);
            Assert.Equal(
                candidateCanonicalEntityId,
                await new CanonicalEntityAliasStore(db).ResolveAsync(
                    "pcgen-org-pcgen-newsources",
                    aliasValue,
                    fingerprint));
            Assert.NotNull(await ReadRelationshipAsync(
                db,
                baseCanonicalEntityId,
                candidateCanonicalEntityId,
                CanonicalBootstrapReconciliationClassifications.Revision));
        }
    }

    [Fact]
    public async Task FinalizedBootstrapDecisionCanNotBeSilentlyReclassified()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var imported = await new NormalizedSourceImportService(db).ImportAsync(SeedRequest(token));
            var canonicalEntityId = await CanonicalEntityIdAsync(db, Assert.Single(imported.Entities).EntityId);
            var aliasValue = $"pcgen|feat|finalized|{token}|1";
            var fingerprint = CanonicalSourceIdentity.Fingerprint($"finalized:{token}");
            var service = new CanonicalBootstrapReconciliationService(db);

            await service.RecordAsync(new RecordCanonicalBootstrapReconciliationRequest(
                "pcgen-org-pcgen",
                aliasValue,
                fingerprint,
                CanonicalBootstrapReconciliationClassifications.ExactIdentity,
                canonicalEntityId,
                RelatedCanonicalEntityId: null,
                "manual-review",
                1.0,
                "integration-test"));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RecordAsync(new RecordCanonicalBootstrapReconciliationRequest(
                    "pcgen-org-pcgen",
                    aliasValue,
                    fingerprint,
                    CanonicalBootstrapReconciliationClassifications.Variant,
                    CanonicalEntityId: null,
                    RelatedCanonicalEntityId: canonicalEntityId,
                    "manual-review",
                    1.0,
                    "integration-test")));
            Assert.Contains("can not be silently replaced", exception.Message, StringComparison.Ordinal);
            Assert.Equal(
                canonicalEntityId,
                await new CanonicalEntityAliasStore(db).ResolveAsync(
                    "pcgen-org-pcgen",
                    aliasValue,
                    fingerprint));
        }
    }

    [Fact]
    public async Task UnregisteredSourceLineageCanNotSeedCanonicalAliasKnowledge()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var service = new CanonicalBootstrapReconciliationService(db);
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.RecordAsync(new RecordCanonicalBootstrapReconciliationRequest(
                    "pcgen-lookalike",
                    "native-key",
                    CanonicalSourceIdentity.Fingerprint("lookalike"),
                    CanonicalBootstrapReconciliationClassifications.Unresolved,
                    CanonicalEntityId: null,
                    RelatedCanonicalEntityId: null,
                    "integration-test",
                    0.5,
                    "integration-test")));
            Assert.Contains("registered trusted source lineage", exception.Message, StringComparison.Ordinal);
        }
    }

    private static ImportNormalizedSourceRequest SeedRequest(string token)
    {
        var rawJson = SeedJson(token);
        const string publicationKey = "bootstrap-seed-publication";
        return new ImportNormalizedSourceRequest(
            $"bootstrap-seed-{token}",
            $"Bootstrap seed {token}",
            "integration-test",
            License: null,
            IsPublic: false,
            new NormalizedSourceRepresentation(
                "bootstrap-seed",
                new SourceRepresentationArtifact(
                    $"bootstrap-{token}.json",
                    Encoding.UTF8.GetBytes(rawJson),
                    $"integration:bootstrap:{token}"),
                [new NormalizedSourceRecord(
                    "feat",
                    $"Bootstrap Seed {token}",
                    SourceCode: "BOOT",
                    NativeKey: $"bootstrap|feat|{token}",
                    RawJson: rawJson,
                    LocatorKey: "entry:1",
                    PublicationLocalKey: publicationKey,
                    SemanticJson: rawJson)],
                [new NormalizedSourcePublication(
                    publicationKey,
                    $"Bootstrap Publication {token}",
                    Publisher: "Integration Test Press",
                    GameEdition: "3.5e")])) ;
    }

    private static string SeedJson(string token) =>
        JsonSerializer.Serialize(new
        {
            name = $"Bootstrap Seed {token}",
            effect = "Gain a +2 bootstrap fixture bonus."
        });

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
                WHERE binding.source_entity_id = @source_entity_id
                    AND occurrence.canonical_entity_id IS NOT NULL
                ORDER BY binding.created_at DESC
                LIMIT 1;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            return (Guid)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Seed source entity did not receive a canonical identity."));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<(string EvidenceKind, double Confidence)?> ReadRelationshipAsync(
        RulesCoreDbContext db,
        Guid fromCanonicalEntityId,
        Guid toCanonicalEntityId,
        string relationshipKind)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT evidence_kind, confidence
                FROM canonical_entity_relationship
                WHERE from_canonical_entity_id = @from_id
                    AND to_canonical_entity_id = @to_id
                    AND relationship_kind = @relationship_kind;
                """;
            AddParameter(command, "@from_id", fromCanonicalEntityId);
            AddParameter(command, "@to_id", toCanonicalEntityId);
            AddParameter(command, "@relationship_kind", relationshipKind);
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync()
                ? (reader.GetString(0), reader.GetDouble(1))
                : null;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<RulesCoreDbContext?> OpenDatabaseAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        return db;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
