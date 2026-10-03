using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class ClassTaxonomyCanonicalBackfillIntegrationTests
{
    [Fact]
    public async Task TranslationBackfillMovesMultipleRevisionsOutOfStaleClassCanonicalCategory()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var packageKey = $"class-taxonomy-history-{token}";
            try
            {
                var firstRepresentation = SidekickRepresentation(token, "first");
                var firstImport = await ImportAsync(db, packageKey, token, firstRepresentation);
                var importedEntity = Assert.Single(firstImport.Entities);
                Assert.Equal(RuleConceptEntityTypes.SidekickClass, importedEntity.EntityType);

                var secondRepresentation = SidekickRepresentation(token, "second");
                var secondImport = await ImportAsync(db, packageKey, token, secondRepresentation);
                Assert.Equal(importedEntity.EntityId, Assert.Single(secondImport.Entities).EntityId);

                var entity = await db.SourceEntities
                    .SingleAsync(value => value.Id == importedEntity.EntityId);
                var revisions = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == entity.Id)
                    .OrderBy(value => value.RevisionNumber)
                    .ToArrayAsync();
                Assert.Equal(2, revisions.Length);

                var publication = Assert.Single(firstRepresentation.Publications!);
                var canonicalService = new CanonicalSourceRepresentationService(db);
                foreach (var revision in revisions)
                {
                    var fingerprint = await ReadBindingFingerprintAsync(db, revision.Id);
                    await canonicalService.AssociateSourceEntityAsync(
                        entity.Id,
                        revision.Id,
                        new CanonicalPublicationEvidence(
                            publication.DisplayName,
                            publication.Publisher,
                            publication.GameEdition,
                            publication.PublicationDate,
                            OccurrenceFingerprints: [fingerprint]),
                        new CanonicalSourceOccurrenceEvidence(
                            RuleConceptEntityTypes.Class,
                            entity.Name,
                            revision.LocatorKey,
                            fingerprint),
                        FiveEToolsSourceFormatAdapter.Format,
                        canonicalAliases: null,
                        allowTranslationOnlyReassociation: true);
                }

                foreach (var revision in revisions)
                {
                    Assert.Equal(
                        RuleConceptEntityTypes.Class,
                        await ReadBoundCanonicalEntityTypeAsync(db, revision.Id));
                    revision.NormalizationVersion = SourceNormalizationVersion.Current - 1;
                    revision.NormalizationAttemptVersion = SourceNormalizationVersion.Current - 1;
                    revision.NormalizationAttemptedAt = null;
                    revision.NormalizationError = null;
                }
                entity.EntityType = RuleConceptEntityTypes.Class;
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();

                var maintenance = new SourceNormalizationMaintenanceService(
                    db,
                    new SourceFormatAdapterRegistry(
                    [
                        new FiveEToolsSourceFormatAdapter(),
                        new PcGenSourceFormatAdapter(),
                        new PdfSourceFormatAdapter()
                    ]));
                var result = await maintenance.ReconcileAsync(
                    limit: 10,
                    retryFailed: false,
                    packageKey: packageKey);

                Assert.Equal(2, result.AttemptedRevisionCount);
                Assert.Equal(0, result.FailedRevisionCount);
                Assert.Equal(2, result.CanonicalReassociationCount);
                Assert.Equal(
                    RuleConceptEntityTypes.SidekickClass,
                    (await db.SourceEntities
                        .AsNoTracking()
                        .SingleAsync(value => value.Id == entity.Id))
                    .EntityType);
                Assert.Equal(
                    2,
                    await db.SourceEntityRevisions.CountAsync(value => value.SourceEntityId == entity.Id));

                var repairedRevisions = await db.SourceEntityRevisions
                    .AsNoTracking()
                    .Where(value => value.SourceEntityId == entity.Id)
                    .OrderBy(value => value.RevisionNumber)
                    .ToArrayAsync();
                foreach (var revision in repairedRevisions)
                {
                    Assert.Equal(SourceNormalizationVersion.Current, revision.NormalizationVersion);
                    Assert.Equal(
                        CanonicalSourceIdentity.NormalizeIdentityPart(RuleConceptEntityTypes.SidekickClass),
                        await ReadBoundCanonicalEntityTypeAsync(db, revision.Id));
                }
            }
            finally
            {
                await db.SourcePackages
                    .Where(value => value.Key == packageKey)
                    .ExecuteDeleteAsync();
            }
        }
    }

    private static NormalizedSourceRepresentation SidekickRepresentation(string token, string revisionLabel)
    {
        var json = $$"""
            {
              "_meta": {
                "edition": "classic",
                "sources": [
                  {"json":"TCE","full":"Tasha's Cauldron of Everything","dateReleased":"2020-11-17"}
                ]
              },
              "class": [
                {
                  "name":"Expert Sidekick",
                  "source":"TCE",
                  "isSidekick":true,
                  "entries":["taxonomy fixture {{revisionLabel}}"]
                }
              ]
            }
            """;
        return new FiveEToolsSourceFormatAdapter().TryRead(
            new SourceRepresentationArtifact(
                $"class-sidekick-{revisionLabel}.json",
                Encoding.UTF8.GetBytes(json),
                $"admin:class-taxonomy-history:{token}:{revisionLabel}",
                "https://raw.githubusercontent.com/5etools-mirror-3/5etools-src/main/data/class/class-sidekick.json",
                "application/json"))
            ?? throw new InvalidOperationException("The sidekick fixture was not readable.");
    }

    private static Task<NormalizedSourceImportResult> ImportAsync(
        RulesCoreDbContext db,
        string packageKey,
        string token,
        NormalizedSourceRepresentation representation) =>
        new NormalizedSourceImportService(db).ImportAsync(
            new ImportNormalizedSourceRequest(
                packageKey,
                $"Class taxonomy history fixture {token}",
                "integration-test",
                "test-only",
                false,
                representation));

    private static async Task<string> ReadBindingFingerprintAsync(
        RulesCoreDbContext db,
        Guid revisionId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT semantic_fingerprint
                FROM source_entity_occurrence_binding
                WHERE source_entity_revision_id = @revision_id;
                """;
            AddParameter(command, "@revision_id", revisionId);
            return (string)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("The source revision is not canonically bound."));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<string> ReadBoundCanonicalEntityTypeAsync(
        RulesCoreDbContext db,
        Guid revisionId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT entity.entity_type
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                  ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                JOIN canonical_entity entity
                  ON entity.canonical_entity_id = occurrence.canonical_entity_id
                WHERE binding.source_entity_revision_id = @revision_id;
                """;
            AddParameter(command, "@revision_id", revisionId);
            return (string)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("The source revision is not canonically bound."));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static async Task<RulesCoreDbContext?> OpenDatabaseAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>()
                .UseNpgsql(connectionString)
                .Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        return db;
    }
}
