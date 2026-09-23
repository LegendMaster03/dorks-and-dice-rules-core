using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CurrentUserSourceDeduplicationRecoveryIntegrationTests
{
    [Fact]
    public async Task InterruptedLegacyWebPackageIsAdoptedAndReusedOnRetry()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var userId = $"legacy-resume-{token}";
            const string sourceUrl = "https://1.1.1.1/source.json";
            const string originIdentity = "web:https://1.1.1.1/source.json";
            var legacyKey =
                $"user-source-{Fingerprint($"{userId}\n{originIdentity}")[..24]}";
            var bytes = Encoding.UTF8.GetBytes(
                $$"""
                {
                  "spell": [
                    {
                      "name": "Resume Fixture {{token}}",
                      "source": "PHB",
                      "level": 1,
                      "school": "A",
                      "time": [{ "number": 1, "unit": "action" }],
                      "range": { "type": "point", "distance": { "type": "feet", "amount": 30 } },
                      "components": { "v": true },
                      "duration": [{ "type": "instant" }],
                      "entries": ["Recovery fixture."]
                    }
                  ]
                }
                """);
            var artifact = new SourceRepresentationArtifact(
                "source.json",
                bytes,
                originIdentity,
                sourceUrl,
                "application/json");
            var representation = new FiveEToolsSourceFormatAdapter().TryRead(artifact)
                ?? throw new InvalidOperationException("Recovery fixture did not parse.");

            var legacy = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    legacyKey,
                    $"Interrupted legacy source {token}",
                    "legacy-web",
                    License: null,
                    IsPublic: false,
                    representation));

            var legacyRepresentationId = await db.SourceRepresentations
                .Where(value => value.SourcePackageId == legacy.PackageId)
                .Select(value => value.Id)
                .SingleAsync();
            var legacyEntityIds = await db.SourceEntities
                .Where(value => value.SourcePackageId == legacy.PackageId)
                .Select(value => value.Id)
                .OrderBy(value => value)
                .ToArrayAsync();
            Assert.NotEmpty(legacyEntityIds);

            var registry = new SourceFormatAdapterRegistry([
                new FiveEToolsSourceFormatAdapter(),
                new PcGenSourceFormatAdapter(),
                new PdfSourceFormatAdapter()
            ]);
            using var client = new HttpClient(new StaticSourceHandler(bytes));
            var grants = new SourceGrantService(db);
            var service = new CurrentUserSourceService(
                db,
                new NormalizedSourceImportService(db),
                registry,
                grants,
                client);

            var resumed = await service.AddAsync(
                userId,
                new AddCurrentUserSourceRequest(
                    CurrentUserSourceKinds.Web,
                    Url: sourceUrl));

            Assert.Equal(legacy.PackageId, resumed.SourcePackageId);
            var package = await db.SourcePackages
                .AsNoTracking()
                .SingleAsync(value => value.Id == legacy.PackageId);
            Assert.Equal(
                CurrentUserSourceService.SharedPackageKey(originIdentity),
                package.Key);
            Assert.Equal("user-source", package.Provider);
            Assert.True(await grants.HasGrantAsync(userId, legacy.PackageId));

            Assert.Equal(
                legacyRepresentationId,
                await db.SourceRepresentations
                    .Where(value => value.SourcePackageId == legacy.PackageId)
                    .Select(value => value.Id)
                    .SingleAsync());
            Assert.Equal(
                legacyEntityIds,
                await db.SourceEntities
                    .Where(value => value.SourcePackageId == legacy.PackageId)
                    .Select(value => value.Id)
                    .OrderBy(value => value)
                    .ToArrayAsync());

            await db.SourcePackages
                .Where(value => value.Id == legacy.PackageId)
                .ExecuteDeleteAsync();
        }
    }

    private static string Fingerprint(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

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

    private sealed class StaticSourceHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
                {
                    Headers =
                    {
                        ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                            "application/json")
                    }
                },
                RequestMessage = request
            });
    }
}
