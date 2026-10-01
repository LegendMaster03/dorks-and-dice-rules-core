using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class WikiReferenceTargetedReadModelIntegrationTests
{
    [Fact]
    public async Task TargetedDetailPreservesIdentityVisibilityAndPublicationSemantics()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..10];
            var packageIds = new List<Guid>();
            var canonicalIds = new HashSet<Guid>();
            var campaignId = Guid.NewGuid();
            var conceptKey = $"monster.targeted-detail-{token}";

            try
            {
                var importer = new ReconciledNormalizedSourceImportService(
                    new NormalizedSourceImportService(db),
                    db);
                var name = $"Targeted Detail Fixture {token}";
                var old = await importer.ImportAsync(Request(
                    packageIds,
                    $"targeted-old-{token}",
                    name,
                    "TD3",
                    "3e",
                    true,
                    new DateOnly(2000, 1, 1),
                    7));
                var current = await importer.ImportAsync(Request(
                    packageIds,
                    $"targeted-current-{token}",
                    name,
                    "TD5",
                    "5e",
                    true,
                    new DateOnly(2014, 8, 19),
                    11));
                var privateOnly = await importer.ImportAsync(Request(
                    packageIds,
                    $"targeted-private-{token}",
                    $"Private Targeted Fixture {token}",
                    "TDPRIVATE",
                    "5e",
                    false,
                    new DateOnly(2014, 8, 20),
                    13));
                var provisional = await importer.ImportAsync(Request(
                    packageIds,
                    $"targeted-provisional-{token}",
                    $"Provisional Targeted Fixture {token}",
                    "TDPROVISIONAL",
                    "3.5e",
                    true,
                    new DateOnly(2003, 7, 1),
                    9));
                packageIds.AddRange([old.PackageId, current.PackageId, privateOnly.PackageId, provisional.PackageId]);

                var oldEntity = Assert.Single(old.Entities);
                var currentEntity = Assert.Single(current.Entities);
                var privateEntity = Assert.Single(privateOnly.Entities);
                var provisionalEntity = Assert.Single(provisional.Entities);
                var oldCanonical = await CanonicalEntityIdAsync(db, oldEntity.EntityId);
                var currentCanonical = await CanonicalEntityIdAsync(db, currentEntity.EntityId);
                var privateCanonical = await CanonicalEntityIdAsync(db, privateEntity.EntityId);
                var provisionalCanonical = await CanonicalEntityIdAsync(db, provisionalEntity.EntityId);
                canonicalIds.UnionWith([oldCanonical, currentCanonical, privateCanonical, provisionalCanonical]);
                var oldOccurrence = await OccurrenceIdAsync(db, oldEntity.EntityId);
                var provisionalOccurrence = await ClearCanonicalEntityAsync(db, provisionalEntity.EntityId);

                await RelateAsync(db, oldCanonical, currentCanonical, "revision");
                var oldRevision = await LatestRevisionIdAsync(db, oldEntity.EntityId);
                var currentRevision = await LatestRevisionIdAsync(db, currentEntity.EntityId);

                var globalRules = new GlobalRulesService(db);
                var concept = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(conceptKey, "monster", name),
                    "rules-lawyer");
                await globalRules.BindSourceEntityAsync(
                    concept.Value.Id,
                    new BindRuleConceptSourceRequest(oldEntity.EntityId),
                    "rules-lawyer");
                await globalRules.SetDecisionAsync(
                    concept.Value.Id,
                    new SetGlobalRuleDecisionRequest(
                        currentRevision,
                        "Targeted detail selects the current variation."),
                    "rules-lawyer");
                var globalRevision = await globalRules.PublishAsync("rules-lawyer");

                var campaignRules = new CampaignRulesService(db);
                await campaignRules.SelectBaselineAsync(
                    campaignId,
                    new SelectCampaignRulesetBaselineRequest(globalRevision.Id),
                    "campaign-dm");
                await campaignRules.SetDecisionAsync(
                    campaignId,
                    concept.Value.Id,
                    new SetCampaignRuleDecisionRequest(
                        CampaignRuleDecisionKinds.SelectSource,
                        oldRevision,
                        "Campaign targeted detail selects the older variation."),
                    "campaign-dm");
                await campaignRules.PublishAsync(campaignId, "campaign-dm");

                await new SourceGrantService(db).GrantAsync("granted-reader", privateOnly.PackageId);

                var service = new WikiReferenceCatalogService(db);
                var byConcept = await service.GetGlobalDetailAsync(null, conceptKey);
                Assert.NotNull(byConcept);
                Assert.Equal(2, byConcept!.Variations.Count);
                Assert.Equal(currentRevision, byConcept.Reference.EffectiveVariation.SourceEntityRevisionId);
                Assert.Equal(WikiReferenceResolutionStates.Resolved, byConcept.Reference.ResolutionState);

                var byCanonical = await service.GetGlobalDetailAsync(null, $"canonical:{oldCanonical:N}");
                Assert.NotNull(byCanonical);
                Assert.Equal(byConcept.Reference.ReferenceIdentity, byCanonical!.Reference.ReferenceIdentity);
                Assert.Equal(currentRevision, byCanonical.Reference.EffectiveVariation.SourceEntityRevisionId);

                var byOccurrence = await service.GetGlobalDetailAsync(null, $"occurrence:{oldOccurrence:N}");
                Assert.NotNull(byOccurrence);
                Assert.Equal(byConcept.Reference.ReferenceIdentity, byOccurrence!.Reference.ReferenceIdentity);

                var provisionalDetail = await service.GetGlobalDetailAsync(
                    null,
                    $"occurrence:{provisionalOccurrence:N}");
                Assert.NotNull(provisionalDetail);
                Assert.Single(provisionalDetail!.Variations);
                Assert.Null(provisionalDetail.Variations[0].CanonicalEntityId);

                Assert.Null(await service.GetGlobalDetailAsync(null, $"canonical:{privateCanonical:N}"));
                var grantedPrivate = await service.GetGlobalDetailAsync(
                    "granted-reader",
                    $"canonical:{privateCanonical:N}");
                Assert.NotNull(grantedPrivate);
                Assert.Single(grantedPrivate!.Variations);

                var campaignDetail = await service.GetCampaignDetailAsync(
                    campaignId,
                    "campaign-reader",
                    conceptKey);
                Assert.NotNull(campaignDetail);
                Assert.Equal(oldRevision, campaignDetail!.Reference.EffectiveVariation.SourceEntityRevisionId);
                Assert.Equal(WikiReferenceResolutionStates.CampaignOverride, campaignDetail.Reference.ResolutionState);

                var searched = await service.GetGlobalCatalogAsync(
                    userId: null,
                    entityType: null,
                    categoryMode: WikiReferenceCategoryModes.AnyVariation,
                    query: name,
                    sourceCode: null,
                    packageKey: null,
                    edition: null,
                    limit: 20,
                    offset: 0);
                var searchedReference = Assert.Single(searched.References);
                Assert.Equal(byConcept.Reference.ReferenceIdentity, searchedReference.ReferenceIdentity);
                Assert.Equal(2, searchedReference.CategoryHistory.Single().Editions.Count);

                var sourceFiltered = await service.GetGlobalCatalogAsync(
                    userId: null,
                    entityType: null,
                    categoryMode: WikiReferenceCategoryModes.AnyVariation,
                    query: null,
                    sourceCode: "TD3",
                    packageKey: null,
                    edition: null,
                    limit: 20,
                    offset: 0);
                Assert.Equal(byConcept.Reference.ReferenceIdentity, Assert.Single(sourceFiltered.References).ReferenceIdentity);
            }
            finally
            {
                await CleanupAsync(db, packageIds, canonicalIds);
            }
        }
    }

    private static ImportNormalizedSourceRequest Request(
        ICollection<Guid> packageIds,
        string packageKey,
        string name,
        string sourceCode,
        string edition,
        bool isPublic,
        DateOnly publicationDate,
        int hitPoints)
    {
        var raw = JsonSerializer.Serialize(new
        {
            name,
            source = sourceCode,
            hp = hitPoints,
            marker = $"targeted-{hitPoints}"
        });
        var representation = new NormalizedSourceRepresentation(
            "integration-json",
            new SourceRepresentationArtifact(
                $"{packageKey}.json",
                Encoding.UTF8.GetBytes(raw),
                $"integration:{packageKey}"),
            [new NormalizedSourceRecord(
                "monster",
                name,
                sourceCode,
                $"monster|{sourceCode}|{name}|",
                raw,
                PublicationLocalKey: $"source:{sourceCode}")],
            [new NormalizedSourcePublication(
                $"source:{sourceCode}",
                $"Publication {sourceCode}",
                GameEdition: edition,
                PublicationDate: publicationDate)]);
        return new ImportNormalizedSourceRequest(
            packageKey,
            packageKey,
            "integration-test",
            null,
            isPublic,
            representation);
    }

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
                ORDER BY binding.source_entity_revision_id
                LIMIT 1;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            return (Guid)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Canonical entity binding was missing."));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<Guid> OccurrenceIdAsync(RulesCoreDbContext db, Guid sourceEntityId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT canonical_source_occurrence_id
                FROM source_entity_occurrence_binding
                WHERE source_entity_id = @source_entity_id
                ORDER BY source_entity_revision_id
                LIMIT 1;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            return (Guid)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Source occurrence was missing."));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<Guid> ClearCanonicalEntityAsync(RulesCoreDbContext db, Guid sourceEntityId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE canonical_source_occurrence occurrence
                SET canonical_entity_id = NULL
                FROM source_entity_occurrence_binding binding
                WHERE binding.canonical_source_occurrence_id = occurrence.canonical_source_occurrence_id
                  AND binding.source_entity_id = @source_entity_id
                RETURNING occurrence.canonical_source_occurrence_id;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            return (Guid)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Fixture source occurrence was missing."));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<Guid> LatestRevisionIdAsync(RulesCoreDbContext db, Guid sourceEntityId) =>
        await db.SourceEntityRevisions
            .Where(value => value.SourceEntityId == sourceEntityId)
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => value.Id)
            .FirstAsync();

    private static async Task RelateAsync(
        RulesCoreDbContext db,
        Guid fromId,
        Guid toId,
        string kind)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO canonical_entity_relationship (
                    canonical_entity_relationship_id,
                    from_canonical_entity_id,
                    to_canonical_entity_id,
                    relationship_kind,
                    evidence_kind,
                    confidence,
                    created_at)
                VALUES (@id, @from_id, @to_id, @kind, 'integration-test', 1.0, @created_at)
                ON CONFLICT (from_canonical_entity_id, to_canonical_entity_id, relationship_kind)
                DO NOTHING;
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@from_id", fromId);
            AddParameter(command, "@to_id", toId);
            AddParameter(command, "@kind", kind);
            AddParameter(command, "@created_at", DateTimeOffset.UtcNow);
            await command.ExecuteNonQueryAsync();
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

    private static async Task CleanupAsync(
        RulesCoreDbContext db,
        IReadOnlyCollection<Guid> packageIds,
        IReadOnlyCollection<Guid> canonicalIds)
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

        // ExecuteDelete bypasses the change tracker, while the services above have tracked the
        // publication graph. Clear those stale tracked relationships before deleting source packages.
        db.ChangeTracker.Clear();

        if (packageIds.Count > 0)
        {
            var packages = await db.SourcePackages
                .Where(value => packageIds.Contains(value.Id))
                .ToArrayAsync();
            db.SourcePackages.RemoveRange(packages);
            await db.SaveChangesAsync();
        }

        if (canonicalIds.Count > 0)
        {
            var connection = db.Database.GetDbConnection();
            var openedHere = connection.State != ConnectionState.Open;
            if (openedHere) await connection.OpenAsync();
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    DELETE FROM canonical_entity_relationship
                    WHERE from_canonical_entity_id = ANY(@canonical_ids)
                       OR to_canonical_entity_id = ANY(@canonical_ids);
                    """;
                AddParameter(command, "@canonical_ids", canonicalIds.ToArray());
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                if (openedHere) await connection.CloseAsync();
            }
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
