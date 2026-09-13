using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class FiveEToolsSplitPublicationIntegrationTests
{
    [Fact]
    public async Task RichTitleThenSourceCodeFallbackDoesNotFailSplitPublicationImport()
    {
        var db = await OpenDatabaseAsync();
        if (db is null)
        {
            return;
        }
        await using (db)
        {
            var adapter = new FiveEToolsSourceFormatAdapter();
            var importer = new NormalizedSourceImportService(db);
            var packageKey = $"split-bgdia-{Guid.NewGuid():N}";

            var metadataRepresentation = RequireRepresentation(adapter.TryRead(
                Artifact(
                    "adventures.json",
                    """
                    {
                      "adventure": [
                        {
                          "name": "Baldur's Gate: Descent Into Avernus",
                          "id": "BGDIA",
                          "source": "BGDIA",
                          "published": "2019-09-17"
                        }
                      ]
                    }
                    """)));
            var entityRepresentation = RequireRepresentation(adapter.TryRead(
                Artifact(
                    "bestiary-bgdia.json",
                    """
                    {
                      "monster": [
                        {
                          "name": "Amrik Vanthampur",
                          "source": "BGDIA",
                          "ac": [12],
                          "hp": { "average": 66, "formula": "12d8 + 12" }
                        }
                      ]
                    }
                    """)));

            var first = await ImportAsync(importer, packageKey, metadataRepresentation);
            var second = await ImportAsync(importer, packageKey, entityRepresentation);

            var firstPublication = Assert.Single(first.Publications);
            var secondPublication = Assert.Single(second.Publications);
            Assert.Equal(first.PackageId, second.PackageId);
            Assert.Equal(firstPublication.WorkId, secondPublication.WorkId);
            Assert.Equal(firstPublication.CanonicalPublicationId, secondPublication.CanonicalPublicationId);
            Assert.Equal("Baldur's Gate: Descent Into Avernus", secondPublication.DisplayName);
            Assert.Equal(
                "Baldur's Gate: Descent Into Avernus",
                await ReadWorkDisplayNameAsync(db, firstPublication.WorkId));
            Assert.Equal(
                "Baldur's Gate: Descent Into Avernus",
                await ReadCanonicalDisplayNameAsync(db, firstPublication.CanonicalPublicationId));
        }
    }

    [Fact]
    public async Task SourceCodeFallbackIsUpgradedWhenPublicationMetadataArrivesLater()
    {
        var db = await OpenDatabaseAsync();
        if (db is null)
        {
            return;
        }
        await using (db)
        {
            var adapter = new FiveEToolsSourceFormatAdapter();
            var importer = new NormalizedSourceImportService(db);
            var packageKey = $"split-phb-{Guid.NewGuid():N}";

            var entityRepresentation = RequireRepresentation(adapter.TryRead(
                Artifact(
                    "bestiary-phb.json",
                    """
                    {
                      "monster": [
                        {
                          "name": "Example Creature",
                          "source": "PHB",
                          "ac": [10],
                          "hp": { "average": 4, "formula": "1d8" }
                        }
                      ]
                    }
                    """)));
            var metadataRepresentation = RequireRepresentation(adapter.TryRead(
                Artifact(
                    "books.json",
                    """
                    {
                      "book": [
                        {
                          "name": "Player's Handbook (2014)",
                          "id": "PHB",
                          "source": "PHB",
                          "published": "2014-08-19"
                        }
                      ]
                    }
                    """)));

            var first = await ImportAsync(importer, packageKey, entityRepresentation);
            var firstPublication = Assert.Single(first.Publications);
            Assert.Equal("PHB", firstPublication.DisplayName);
            Assert.Equal(
                "PHB",
                await ReadCanonicalDisplayNameAsync(db, firstPublication.CanonicalPublicationId));

            var second = await ImportAsync(importer, packageKey, metadataRepresentation);
            var secondPublication = Assert.Single(second.Publications);

            Assert.Equal(firstPublication.WorkId, secondPublication.WorkId);
            Assert.Equal(firstPublication.CanonicalPublicationId, secondPublication.CanonicalPublicationId);
            Assert.Equal("Player's Handbook (2014)", secondPublication.DisplayName);
            Assert.Equal(
                "Player's Handbook (2014)",
                await ReadWorkDisplayNameAsync(db, firstPublication.WorkId));
            Assert.Equal(
                "Player's Handbook (2014)",
                await ReadCanonicalDisplayNameAsync(db, firstPublication.CanonicalPublicationId));
        }
    }

    [Fact]
    public async Task ChildAdventureMetadataDoesNotRenameParentPublication()
    {
        var db = await OpenDatabaseAsync();
        if (db is null)
        {
            return;
        }
        await using (db)
        {
            var adapter = new FiveEToolsSourceFormatAdapter();
            var importer = new NormalizedSourceImportService(db);
            var packageKey = $"split-fraif-{Guid.NewGuid():N}";

            var childAdventureRepresentation = RequireRepresentation(adapter.TryRead(
                Artifact(
                    "adventures.json",
                    """
                    {
                      "adventure": [
                        {
                          "name": "The Lost Library of Lethchauntos",
                          "id": "FRAiF-TLLoL",
                          "source": "FRAiF",
                          "parentSource": "FRAiF",
                          "published": "2025-11-11"
                        }
                      ]
                    }
                    """)));
            var parentBookRepresentation = RequireRepresentation(adapter.TryRead(
                Artifact(
                    "books.json",
                    """
                    {
                      "book": [
                        {
                          "name": "Forgotten Realms: Adventures in Faerûn",
                          "id": "FRAiF",
                          "source": "FRAiF",
                          "published": "2025-11-11"
                        }
                      ]
                    }
                    """)));

            var child = await ImportAsync(importer, packageKey, childAdventureRepresentation);
            var childPublication = Assert.Single(child.Publications);
            Assert.Equal("FRAiF", childPublication.DisplayName);

            var parent = await ImportAsync(importer, packageKey, parentBookRepresentation);
            var parentPublication = Assert.Single(parent.Publications);

            Assert.Equal(childPublication.WorkId, parentPublication.WorkId);
            Assert.Equal(childPublication.CanonicalPublicationId, parentPublication.CanonicalPublicationId);
            Assert.Equal("Forgotten Realms: Adventures in Faerûn", parentPublication.DisplayName);
            Assert.Equal(
                "Forgotten Realms: Adventures in Faerûn",
                await ReadWorkDisplayNameAsync(db, parentPublication.WorkId));
            Assert.Equal(
                "Forgotten Realms: Adventures in Faerûn",
                await ReadCanonicalDisplayNameAsync(db, parentPublication.CanonicalPublicationId));
        }
    }

    [Fact]
    public async Task DifferentRichTitlesForSamePackageLocalKeyStillConflict()
    {
        var db = await OpenDatabaseAsync();
        if (db is null)
        {
            return;
        }
        await using (db)
        {
            var importer = new NormalizedSourceImportService(db);
            var packageKey = $"split-conflict-{Guid.NewGuid():N}";
            var first = Representation(
                "shared-code",
                "First Real Title",
                "first.json",
                "First Rule");
            var second = Representation(
                "shared-code",
                "Different Real Title",
                "second.json",
                "Second Rule");

            await ImportAsync(importer, packageKey, first);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => ImportAsync(importer, packageKey, second));

            Assert.Contains("already registered as 'First Real Title'", exception.Message, StringComparison.Ordinal);
        }
    }

    private static SourceRepresentationArtifact Artifact(string fileName, string json) =>
        new(
            fileName,
            Encoding.UTF8.GetBytes(json),
            $"test:{Guid.NewGuid():N}");

    private static NormalizedSourceRepresentation Representation(
        string localKey,
        string displayName,
        string fileName,
        string ruleName) =>
        new(
            FiveEToolsSourceFormatAdapter.Format,
            Artifact(fileName, "{}"),
            [new NormalizedSourcePublication(
                localKey,
                displayName,
                [new NormalizedSourceRecord(
                    "rule",
                    ruleName,
                    localKey,
                    $"rule|{localKey}|{ruleName}",
                    $"{{\"name\":\"{ruleName}\",\"source\":\"{localKey}\"}}")],
                ExternalIdentifiers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["5etools-source-code"] = localKey
                })]);

    private static NormalizedSourceRepresentation RequireRepresentation(
        NormalizedSourceRepresentation? representation)
    {
        Assert.NotNull(representation);
        return representation!;
    }

    private static Task<NormalizedSourceImportResult> ImportAsync(
        NormalizedSourceImportService importer,
        string packageKey,
        NormalizedSourceRepresentation representation) =>
        importer.ImportAsync(new ImportNormalizedSourceRequest(
            packageKey,
            "Split 5e.tools source",
            "5etools-test",
            License: null,
            IsPublic: false,
            representation));

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

    private static Task<string?> ReadWorkDisplayNameAsync(RulesCoreDbContext db, Guid workId) =>
        ReadScalarStringAsync(
            db,
            "SELECT display_name FROM source_work WHERE source_work_id = @id;",
            workId);

    private static Task<string?> ReadCanonicalDisplayNameAsync(
        RulesCoreDbContext db,
        Guid publicationId) =>
        ReadScalarStringAsync(
            db,
            "SELECT display_name FROM canonical_publication WHERE canonical_publication_id = @id;",
            publicationId);

    private static async Task<string?> ReadScalarStringAsync(
        RulesCoreDbContext db,
        string sql,
        Guid id)
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
            command.CommandText = sql;
            AddParameter(command, "@id", id);
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

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
