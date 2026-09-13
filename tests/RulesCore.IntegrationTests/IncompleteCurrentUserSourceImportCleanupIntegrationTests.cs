using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class IncompleteCurrentUserSourceImportCleanupIntegrationTests
{
    private const string SourceUrl =
        "https://github.com/5etools-mirror-3/5etools-src/tree/main/data";

    [Fact]
    public async Task RetryRemovesUngrantedPartialPackageAndItsOrphanCanonicalMetadata()
    {
        var db = await OpenDatabaseAsync();
        if (db is null)
        {
            return;
        }

        await using (db)
        {
            var userId = $"cleanup-{Guid.NewGuid():N}";
            var packageKey = WebPackageKey(userId, SourceUrl);
            var importer = new NormalizedSourceImportService(db);

            var stale = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                packageKey,
                $"Web source {SourceUrl}",
                "github.com",
                License: null,
                IsPublic: false,
                Representation(
                    "FRAiF",
                    "The Lost Library of Lethchauntos",
                    "stale-child.json",
                    "The Lost Library of Lethchauntos")));
            var stalePublication = Assert.Single(stale.Publications);
            Assert.True(await CanonicalExistsAsync(db, stalePublication.CanonicalPublicationId));
            Assert.False(await db.UserSourceGrants.AnyAsync(
                value => value.SourcePackageId == stale.PackageId));

            var removed = await new IncompleteCurrentUserSourceImportCleanupService(db)
                .CleanupWebAddAsync(userId, SourceUrl);

            Assert.True(removed);
            Assert.False(await db.SourcePackages.AnyAsync(value => value.Id == stale.PackageId));
            Assert.False(await CanonicalExistsAsync(db, stalePublication.CanonicalPublicationId));

            var retry = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                packageKey,
                $"Web source {SourceUrl}",
                "github.com",
                License: null,
                IsPublic: false,
                Representation(
                    "FRAiF",
                    "Forgotten Realms: Adventures in Faerûn",
                    "books.json",
                    "Forgotten Realms: Adventures in Faerûn")));

            var retryPublication = Assert.Single(retry.Publications);
            Assert.Equal("Forgotten Realms: Adventures in Faerûn", retryPublication.DisplayName);
            Assert.Equal(
                "Forgotten Realms: Adventures in Faerûn",
                await ReadCanonicalDisplayNameAsync(db, retryPublication.CanonicalPublicationId));
        }
    }

    private static NormalizedSourceRepresentation Representation(
        string sourceCode,
        string displayName,
        string fileName,
        string recordName) =>
        new(
            FiveEToolsSourceFormatAdapter.Format,
            new SourceRepresentationArtifact(
                fileName,
                "{}"u8.ToArray(),
                $"test:{Guid.NewGuid():N}"),
            [new NormalizedSourcePublication(
                sourceCode,
                displayName,
                [new NormalizedSourceRecord(
                    "adventure",
                    recordName,
                    sourceCode,
                    $"adventure|{sourceCode}|{recordName}",
                    $"{{\"name\":\"{recordName}\",\"source\":\"{sourceCode}\"}}")],
                ExternalIdentifiers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["5etools-source-code"] = sourceCode
                })]);

    private static string WebPackageKey(string userId, string url)
    {
        var uri = new Uri(url, UriKind.Absolute);
        var builder = new UriBuilder(uri)
        {
            Host = uri.Host.ToLowerInvariant(),
            Fragment = string.Empty
        };
        var originIdentity = $"web:{builder.Uri.AbsoluteUri}";
        var bytes = Encoding.UTF8.GetBytes($"{userId}\n{originIdentity}");
        var fingerprint = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return $"user-source-{fingerprint[..24]}";
    }

    private static async Task<bool> CanonicalExistsAsync(
        RulesCoreDbContext db,
        Guid publicationId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync();
        }
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT EXISTS (
                    SELECT 1
                    FROM canonical_publication
                    WHERE canonical_publication_id = @id);
                """;
            AddParameter(command, "@id", publicationId);
            return Convert.ToBoolean(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<string?> ReadCanonicalDisplayNameAsync(
        RulesCoreDbContext db,
        Guid publicationId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync();
        }
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT display_name
                FROM canonical_publication
                WHERE canonical_publication_id = @id;
                """;
            AddParameter(command, "@id", publicationId);
            var value = await command.ExecuteScalarAsync();
            return value is null or DBNull ? null : (string)value;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
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
        var options = new DbContextOptionsBuilder<RulesCoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var db = new RulesCoreDbContext(options);
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
