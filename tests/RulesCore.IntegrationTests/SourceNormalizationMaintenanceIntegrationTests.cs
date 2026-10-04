using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class SourceNormalizationMaintenanceIntegrationTests
{
    [Fact]
    public async Task StoredNativeRevisionCanBeBackfilledWithoutCreatingANewSourceRevision()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var packageKey = $"normalization-backfill-{token}";
            var sourceShort = $"NB{token}";
            var fileName = $"data/35e/example/example_races_{token}.lst";
            var representation = new PcGenSourceFormatAdapter().TryRead(
                new SourceRepresentationArtifact(
                    fileName,
                    Encoding.UTF8.GetBytes(string.Join(
                        '\n',
                        $"SOURCELONG:Normalization Backfill Fixture {sourceShort}\tSOURCESHORT:{sourceShort}",
                        "Fine Fixture\tSIZE:F")),
                    $"integration:{sourceShort}#{fileName}"))
                ?? throw new InvalidOperationException(
                    "PCGen normalization fixture was not readable.");

            try
            {
                var imported = await new NormalizedSourceImportService(db).ImportAsync(
                    new ImportNormalizedSourceRequest(
                        packageKey,
                        $"Normalization Backfill Fixture {token}",
                        "integration-test",
                        "test-only",
                        true,
                        representation));
                var importedEntity = Assert.Single(imported.Entities);

                var revision = await db.SourceEntityRevisions
                    .Include(value => value.SourceEntity)
                    .SingleAsync(value => value.SourceEntityId == importedEntity.EntityId);
                Assert.Equal(SourceNormalizationVersion.Current, revision.NormalizationVersion);
                Assert.NotNull(revision.ContentJson);

                var revisionId = revision.Id;
                var nativeFingerprint = revision.Fingerprint;
                var nativeRawJson = revision.RawJson;
                var revisionNumber = revision.RevisionNumber;

                var staleContent = JsonNode.Parse(revision.ContentJson!)?.AsObject()
                    ?? throw new InvalidOperationException("Fixture content was not a JSON object.");
                staleContent.Remove("size");
                revision.ContentJson = staleContent.ToJsonString();
                revision.NormalizationVersion = 0;
                revision.NormalizationAttemptVersion = 0;
                revision.NormalizationAttemptedAt = null;
                revision.NormalizationError = null;
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();

                var registry = new SourceFormatAdapterRegistry(
                [
                    new FiveEToolsSourceFormatAdapter(),
                    new PcGenSourceFormatAdapter(),
                    new PdfSourceFormatAdapter()
                ]);
                var maintenance = new SourceNormalizationMaintenanceService(db, registry);

                var before = await maintenance.GetStatusAsync(packageKey);
                Assert.Equal(1, before.PendingRevisionCount);
                Assert.Equal(0, before.FailedRevisionCount);

                var result = await maintenance.ReconcileAsync(
                    limit: 10,
                    retryFailed: false,
                    packageKey: packageKey);
                Assert.Equal(1, result.AttemptedRevisionCount);
                Assert.Equal(1, result.UpdatedContentCount);
                Assert.Equal(0, result.UnchangedContentCount);
                Assert.Equal(0, result.FailedRevisionCount);
                Assert.Equal(0, result.RemainingPendingRevisionCount);
                Assert.Empty(result.Failures);

                var current = await db.SourceEntityRevisions
                    .AsNoTracking()
                    .SingleAsync(value => value.Id == revisionId);
                Assert.Equal(revisionNumber, current.RevisionNumber);
                Assert.Equal(nativeFingerprint, current.Fingerprint);
                Assert.True(
                    JsonNode.DeepEquals(
                        JsonNode.Parse(nativeRawJson),
                        JsonNode.Parse(current.RawJson)),
                    "Backfill changed the immutable native RawJson document.");
                Assert.Equal(SourceNormalizationVersion.Current, current.NormalizationVersion);
                Assert.Equal(
                    SourceNormalizationVersion.Current,
                    current.NormalizationAttemptVersion);
                Assert.NotNull(current.NormalizationAttemptedAt);
                Assert.Null(current.NormalizationError);
                Assert.NotNull(current.ContentJson);

                using var document = JsonDocument.Parse(current.ContentJson!);
                var size = Assert.Single(
                    document.RootElement.GetProperty("size").EnumerateArray().ToArray());
                Assert.Equal("F", size.GetString());

                Assert.Equal(
                    1,
                    await db.SourceEntityRevisions.CountAsync(
                        value => value.SourceEntityId == importedEntity.EntityId));

                var repeated = await maintenance.ReconcileAsync(
                    limit: 10,
                    retryFailed: false,
                    packageKey: packageKey);
                Assert.Equal(0, repeated.AttemptedRevisionCount);
            }
            finally
            {
                await db.SourcePackages
                    .Where(value => value.Key == packageKey)
                    .ExecuteDeleteAsync();
            }
        }
    }

    [Fact]
    public async Task EpicContinuationIdentityIsBackfilledWithoutChangingNativeRevisionHistory()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var packageKey = $"epic-normalization-backfill-{token}";
            var uniqueId = $"epic-barbarian-{token}";
            var sourceJson = JsonSerializer.Serialize(new
            {
                @class = new[]
                {
                    new
                    {
                        name = "Epic Barbarian",
                        source = "SRD35",
                        uniqueId,
                        documentUri = "https://raw.githubusercontent.com/olimot/srd-v3.5-md/main/epic/epic-classes.md",
                        headingLevel = 2,
                        originalHeading = "Epic Barbarian",
                        body = "Hit Die: d12. Skill Points at Each Additional Level: 4 + Int modifier."
                    }
                }
            });
            var representation = new LegacySrdSourceFormatAdapter().TryRead(
                new SourceRepresentationArtifact(
                    "srd-3-5e.json",
                    Encoding.UTF8.GetBytes(sourceJson),
                    $"integration:epic-backfill-{token}"))
                ?? throw new InvalidOperationException(
                    "Legacy SRD epic normalization fixture was not readable.");

            try
            {
                var imported = await new NormalizedSourceImportService(db).ImportAsync(
                    new ImportNormalizedSourceRequest(
                        packageKey,
                        $"Epic Normalization Backfill Fixture {token}",
                        "integration-test",
                        "test-only",
                        true,
                        representation));
                var importedEntity = Assert.Single(imported.Entities);

                var revision = await db.SourceEntityRevisions
                    .Include(value => value.SourceEntity)
                    .SingleAsync(value => value.SourceEntityId == importedEntity.EntityId);
                Assert.Equal(RuleConceptEntityTypes.ClassProgression, revision.SourceEntity.EntityType);
                Assert.Equal("Barbarian", revision.SourceEntity.Name);

                var revisionId = revision.Id;
                var revisionNumber = revision.RevisionNumber;
                var nativeFingerprint = revision.Fingerprint;
                var nativeRawJson = revision.RawJson;

                revision.SourceEntity.EntityType = RuleConceptEntityTypes.Class;
                revision.SourceEntity.Name = "Epic Barbarian";
                revision.NormalizationVersion = 1;
                revision.NormalizationAttemptVersion = 1;
                revision.NormalizationAttemptedAt = null;
                revision.NormalizationError = null;
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();

                var registry = new SourceFormatAdapterRegistry(
                [
                    new FiveEToolsSourceFormatAdapter(),
                    new PcGenSourceFormatAdapter(),
                    new PdfSourceFormatAdapter()
                ]);
                var maintenance = new SourceNormalizationMaintenanceService(db, registry);
                var result = await maintenance.ReconcileAsync(
                    limit: 10,
                    retryFailed: false,
                    packageKey: packageKey);

                Assert.Equal(1, result.AttemptedRevisionCount);
                Assert.Equal(0, result.FailedRevisionCount);
                Assert.Empty(result.Failures);

                var currentRevision = await db.SourceEntityRevisions
                    .AsNoTracking()
                    .SingleAsync(value => value.Id == revisionId);
                var currentEntity = await db.SourceEntities
                    .AsNoTracking()
                    .SingleAsync(value => value.Id == importedEntity.EntityId);

                Assert.Equal(RuleConceptEntityTypes.ClassProgression, currentEntity.EntityType);
                Assert.Equal("Barbarian", currentEntity.Name);
                Assert.Equal(revisionNumber, currentRevision.RevisionNumber);
                Assert.Equal(nativeFingerprint, currentRevision.Fingerprint);
                Assert.True(
                    JsonNode.DeepEquals(
                        JsonNode.Parse(nativeRawJson),
                        JsonNode.Parse(currentRevision.RawJson)),
                    "Epic identity backfill changed the immutable native RawJson document.");
                Assert.Equal(SourceNormalizationVersion.Current, currentRevision.NormalizationVersion);

                using var content = JsonDocument.Parse(currentRevision.ContentJson!);
                var epic = content.RootElement.GetProperty("_rulesCore").GetProperty("epic");
                Assert.Equal("epic", epic.GetProperty("tier").GetString());
                Assert.Equal("progression", epic.GetProperty("kind").GetString());
                Assert.Equal(
                    "Barbarian",
                    epic.GetProperty("continuationOf").GetProperty("name").GetString());

                Assert.Equal(
                    1,
                    await db.SourceEntityRevisions.CountAsync(
                        value => value.SourceEntityId == importedEntity.EntityId));
            }
            finally
            {
                await db.SourcePackages
                    .Where(value => value.Key == packageKey)
                    .ExecuteDeleteAsync();
            }
        }
    }

    [Fact]
    public async Task GeneratedSpellLookupEvidenceIsRehydratedDuringNormalizationReplay()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var packageKey = $"spell-lookup-replay-{token}";
            var spellName = $"Replay Spark {token}";
            var spellArtifact = new SourceRepresentationArtifact(
                $"spell-replay-{token}.json",
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    spell = new[]
                    {
                        new { name = spellName, source = "XPHB", level = 3, school = "V" }
                    }
                })),
                $"integration:spell-replay-{token}:spell");
            var lookupArtifact = new SourceRepresentationArtifact(
                "gendata-spell-source-lookup.json",
                Encoding.UTF8.GetBytes($$"""
                    {
                      "xphb": {
                        "{{spellName.ToLowerInvariant()}}": {
                          "class": {
                            "XPHB": {
                              "Wizard": true
                            }
                          }
                        }
                      }
                    }
                    """),
                $"integration:spell-replay-{token}:lookup");
            var representations = new FiveEToolsCompanionSourceFormatAdapter()
                .TryReadMany([spellArtifact, lookupArtifact]);
            var spellRepresentation = Assert.Single(representations.Where(value => value.Records.Count == 1));
            var lookupRepresentation = Assert.Single(representations.Where(value => value.CompanionContents.Count == 1));

            try
            {
                var importer = new ReconciledNormalizedSourceImportService(
                    new NormalizedSourceImportService(db),
                    db);
                ImportNormalizedSourceRequest Request(NormalizedSourceRepresentation representation) =>
                    new(
                        packageKey,
                        $"Spell lookup replay {token}",
                        "integration-test",
                        "test-only",
                        true,
                        representation);

                var imported = await importer.ImportAsync(Request(spellRepresentation));
                await importer.ImportAsync(Request(lookupRepresentation));
                var importedEntity = Assert.Single(imported.Entities);

                var revision = await db.SourceEntityRevisions
                    .SingleAsync(value => value.SourceEntityId == importedEntity.EntityId);
                var revisionId = revision.Id;
                var revisionNumber = revision.RevisionNumber;
                var nativeFingerprint = revision.Fingerprint;

                var staleContent = JsonNode.Parse(revision.ContentJson!)?.AsObject()
                    ?? throw new InvalidOperationException("Spell fixture content was not a JSON object.");
                var canonicalSpell = staleContent["_rulesCore"]?["spell"]?.AsObject()
                    ?? throw new InvalidOperationException("Spell fixture did not receive canonical spell mechanics.");
                Assert.True(canonicalSpell.Remove("lists"));
                revision.ContentJson = staleContent.ToJsonString();
                revision.NormalizationVersion = 0;
                revision.NormalizationAttemptVersion = 0;
                revision.NormalizationAttemptedAt = null;
                revision.NormalizationError = null;
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();

                var registry = new SourceFormatAdapterRegistry(
                [
                    new FiveEToolsCompanionSourceFormatAdapter(),
                    new PcGenSourceFormatAdapter(),
                    new PdfSourceFormatAdapter()
                ]);
                var maintenance = new SourceNormalizationMaintenanceService(db, registry);
                var result = await maintenance.ReconcileAsync(
                    limit: 10,
                    retryFailed: false,
                    packageKey: packageKey);

                Assert.Equal(1, result.AttemptedRevisionCount);
                Assert.Equal(1, result.UpdatedContentCount);
                Assert.Equal(0, result.FailedRevisionCount);
                Assert.Empty(result.Failures);

                var current = await db.SourceEntityRevisions
                    .AsNoTracking()
                    .SingleAsync(value => value.Id == revisionId);
                Assert.Equal(revisionNumber, current.RevisionNumber);
                Assert.Equal(nativeFingerprint, current.Fingerprint);
                Assert.Equal(SourceNormalizationVersion.Current, current.NormalizationVersion);

                using var document = JsonDocument.Parse(current.ContentJson!);
                var spell = document.RootElement
                    .GetProperty("_rulesCore")
                    .GetProperty("spell");
                var wizard = Assert.Single(spell.GetProperty("lists").EnumerateArray().Where(value =>
                    value.GetProperty("name").GetString() == "Wizard"));
                Assert.Equal("class", wizard.GetProperty("kind").GetString());
                Assert.Equal(3, wizard.GetProperty("level").GetInt32());
                Assert.Equal("generated-spell-source-lookup", wizard.GetProperty("evidence").GetString());

                Assert.Equal(
                    1,
                    await db.SourceEntityRevisions.CountAsync(
                        value => value.SourceEntityId == importedEntity.EntityId));
            }
            finally
            {
                await db.SourcePackages
                    .Where(value => value.Key == packageKey)
                    .ExecuteDeleteAsync();
            }
        }
    }

    private static async Task<RulesCoreDbContext?> OpenDatabaseAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>()
                .UseNpgsql(connectionString)
                .Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        return db;
    }
}
