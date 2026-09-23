using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class SourcePackageDeduplicationIntegrationTests
{
    [Fact]
    public async Task LegacyPerUserDuplicatePackagesAreConsolidatedAndRegistrationsAreRepointed()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var firstUserId = $"dedup-user-a-{token}";
            var secondUserId = $"dedup-user-b-{token}";
            var firstKey = $"user-source-legacy-a-{token}";
            var secondKey = $"user-source-legacy-b-{token}";
            var representation = BuildRepresentation(token);
            var importer = new NormalizedSourceImportService(db);
            var grants = new SourceGrantService(db);

            var first = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                firstKey,
                $"Legacy duplicate A {token}",
                "legacy-user-import",
                License: null,
                IsPublic: false,
                representation));
            var second = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                secondKey,
                $"Legacy duplicate B {token}",
                "legacy-user-import",
                License: null,
                IsPublic: false,
                representation));

            Assert.NotEqual(first.PackageId, second.PackageId);
            await grants.GrantAsync(firstUserId, first.PackageId);
            await grants.GrantAsync(secondUserId, second.PackageId);

            var sourceSchema = new CurrentUserSourceService(
                db,
                new SourceImportService(db),
                grants);
            await sourceSchema.ListAsync($"schema-{token}");
            await InsertRegistrationAsync(
                db,
                firstUserId,
                $"Legacy A {token}",
                first.PackageId,
                new string('a', 64));
            await InsertRegistrationAsync(
                db,
                secondUserId,
                $"Legacy B {token}",
                second.PackageId,
                new string('b', 64));

            var firstEntityCount = await db.SourceEntities.CountAsync(
                value => value.SourcePackageId == first.PackageId);
            Assert.True(firstEntityCount > 0);
            Assert.Equal(
                firstEntityCount,
                await db.SourceEntities.CountAsync(
                    value => value.SourcePackageId == second.PackageId));

            var result = await new SourcePackageDeduplicationService(db).ConsolidateAsync();
            Assert.True(result.ConsolidatedPackageCount >= 1);

            var registeredPackages = await db.Database.SqlQueryRaw<Guid>(
                    """
                    SELECT source_package_id AS "Value"
                    FROM current_user_source
                    WHERE user_id IN ({0}, {1})
                    ORDER BY user_id
                    """,
                    firstUserId,
                    secondUserId)
                .ToArrayAsync();
            Assert.Equal(2, registeredPackages.Length);
            Assert.Equal(registeredPackages[0], registeredPackages[1]);

            var sharedPackageId = registeredPackages[0];
            var sharedPackage = await db.SourcePackages
                .AsNoTracking()
                .SingleAsync(value => value.Id == sharedPackageId);
            Assert.StartsWith("user-origin-", sharedPackage.Key);
            Assert.Equal("user-source", sharedPackage.Provider);
            Assert.Equal(
                firstEntityCount,
                await db.SourceEntities.CountAsync(
                    value => value.SourcePackageId == sharedPackageId));
            Assert.Equal(
                2,
                await db.UserSourceGrants.CountAsync(
                    value => value.SourcePackageId == sharedPackageId));

            var representationRow = await db.SourceRepresentations
                .AsNoTracking()
                .SingleAsync(value => value.SourcePackageId == sharedPackageId);
            Assert.Equal(
                1,
                await db.SourceContentBlobs.CountAsync(
                    value => value.Sha256 == representationRow.ContentSha256));

            await db.SourcePackages
                .Where(value => value.Id == sharedPackageId)
                .ExecuteDeleteAsync();
        }
    }

    private static NormalizedSourceRepresentation BuildRepresentation(string token)
    {
        var sourceCode = $"DD{token}".ToUpperInvariant();
        var bytes = Encoding.UTF8.GetBytes(
            $"{{\"name\":\"Shared Fixture {token}\",\"source\":\"{sourceCode}\"}}");
        var artifact = new SourceRepresentationArtifact(
            $"shared-{token}.json",
            bytes,
            $"legacy-shared:{token}",
            MediaType: "application/json");
        var record = new NormalizedSourceRecord(
            "rule",
            $"Shared Fixture {token}",
            sourceCode,
            $"rule|shared-fixture-{token}|{sourceCode}",
            $"{{\"name\":\"Shared Fixture {token}\",\"source\":\"{sourceCode}\"}}");
        return new NormalizedSourceRepresentation(
            FiveEToolsSourceFormatAdapter.Format,
            artifact,
            [record]);
    }

    private static Task InsertRegistrationAsync(
        RulesCoreDbContext db,
        string userId,
        string displayName,
        Guid packageId,
        string originKey) =>
        db.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO current_user_source (
                current_user_source_id,
                user_id,
                source_kind,
                display_name,
                source_url,
                source_package_id,
                origin_key,
                source_codes_json,
                entity_count,
                added_at,
                refreshed_at)
            VALUES (
                {{Guid.NewGuid()}},
                {{userId}},
                'upload',
                {{displayName}},
                NULL,
                {{packageId}},
                {{originKey}},
                '[]'::jsonb,
                1,
                {{DateTimeOffset.UtcNow}},
                {{DateTimeOffset.UtcNow}});
            """);

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
