using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
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
                Assert.Equal(nativeRawJson, current.RawJson);
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
