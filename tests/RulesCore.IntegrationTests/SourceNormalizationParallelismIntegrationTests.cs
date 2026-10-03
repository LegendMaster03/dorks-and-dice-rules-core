using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class SourceNormalizationParallelismIntegrationTests
{
    [Fact]
    public async Task WorkerSkipsLockedEntityAndProcessesDifferentPendingEntity()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var token = Guid.NewGuid().ToString("N")[..12];
        var packageKey = $"normalization-parallel-{token}";
        var sourceShort = $"NP{token}";
        var fileName = $"data/35e/example/example_races_{token}.lst";
        var representation = new PcGenSourceFormatAdapter().TryRead(
            new SourceRepresentationArtifact(
                fileName,
                Encoding.UTF8.GetBytes(string.Join(
                    '\n',
                    $"SOURCELONG:Normalization Parallel Fixture {sourceShort}\tSOURCESHORT:{sourceShort}",
                    "Fine Parallel Fixture\tSIZE:F",
                    "Diminutive Parallel Fixture\tSIZE:D")),
                $"integration:{sourceShort}#{fileName}"))
            ?? throw new InvalidOperationException(
                "PCGen parallel-normalization fixture was not readable.");

        await using var setup = CreateDatabase(connectionString);
        await new RulesCoreSchemaInitializer(setup).InitializeAsync();

        try
        {
            var imported = await new NormalizedSourceImportService(setup).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Normalization Parallel Fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    representation));
            Assert.Equal(2, imported.Entities.Count);

            var revisions = await setup.SourceEntityRevisions
                .Where(value => value.SourceEntity.SourcePackage.Key == packageKey)
                .OrderBy(value => value.ImportedAt)
                .ThenBy(value => value.Id)
                .ToArrayAsync();
            Assert.Equal(2, revisions.Length);
            Assert.NotEqual(revisions[0].SourceEntityId, revisions[1].SourceEntityId);

            foreach (var revision in revisions)
            {
                revision.NormalizationVersion = 0;
                revision.NormalizationAttemptVersion = 0;
                revision.NormalizationAttemptedAt = null;
                revision.NormalizationError = null;
            }
            await setup.SaveChangesAsync();
            setup.ChangeTracker.Clear();

            var lockedRevisionId = revisions[0].Id;
            var lockedEntityId = revisions[0].SourceEntityId;
            var availableRevisionId = revisions[1].Id;

            await using var blocker = CreateDatabase(connectionString);
            await using var blockerTransaction = await blocker.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted);
            var blockerConnection = blocker.Database.GetDbConnection();
            await using (var command = blockerConnection.CreateCommand())
            {
                command.Transaction = blockerTransaction.GetDbTransaction();
                command.CommandText = """
                    SELECT source_entity_id
                    FROM source_entity
                    WHERE source_entity_id = @entity_id
                    FOR UPDATE;
                    """;
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@entity_id";
                parameter.Value = lockedEntityId;
                command.Parameters.Add(parameter);
                Assert.Equal(
                    lockedEntityId,
                    Assert.IsType<Guid>(await command.ExecuteScalarAsync()));
            }

            await using var worker = CreateDatabase(connectionString);
            var registry = new SourceFormatAdapterRegistry(
            [
                new FiveEToolsSourceFormatAdapter(),
                new PcGenSourceFormatAdapter(),
                new PdfSourceFormatAdapter()
            ]);
            var maintenance = new SourceNormalizationMaintenanceService(worker, registry);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var result = await maintenance.ReconcileAsync(
                limit: 1,
                retryFailed: false,
                packageKey: packageKey,
                cancellationToken: timeout.Token);

            Assert.Equal(1, result.AttemptedRevisionCount);
            Assert.Equal(0, result.FailedRevisionCount);
            Assert.Empty(result.Failures);

            await using var verification = CreateDatabase(connectionString);
            var lockedRevision = await verification.SourceEntityRevisions
                .AsNoTracking()
                .SingleAsync(value => value.Id == lockedRevisionId);
            var availableRevision = await verification.SourceEntityRevisions
                .AsNoTracking()
                .SingleAsync(value => value.Id == availableRevisionId);

            Assert.Equal(0, lockedRevision.NormalizationVersion);
            Assert.Equal(SourceNormalizationVersion.Current, availableRevision.NormalizationVersion);

            await blockerTransaction.RollbackAsync();
        }
        finally
        {
            await setup.SourcePackages
                .Where(value => value.Key == packageKey)
                .ExecuteDeleteAsync();
        }
    }

    private static RulesCoreDbContext CreateDatabase(string connectionString) =>
        new(
            new DbContextOptionsBuilder<RulesCoreDbContext>()
                .UseNpgsql(connectionString)
                .Options);
}
