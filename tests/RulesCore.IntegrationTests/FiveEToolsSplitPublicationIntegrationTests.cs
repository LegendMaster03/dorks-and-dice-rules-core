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
    public async Task RichCorpusMetadataAndLaterSourceRecordsReuseTheSamePublicationWhenIdentityIsUnambiguous()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var adapter = new FiveEToolsSourceFormatAdapter();
            var importer = new NormalizedSourceImportService(db);
            var packageKey = $"split-bgdia-{Guid.NewGuid():N}";

            var metadata = RequireRepresentation(adapter.TryRead(Artifact(
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
            var entities = RequireRepresentation(adapter.TryRead(Artifact(
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

            var first = await ImportAsync(importer, packageKey, metadata);
            var second = await ImportAsync(importer, packageKey, entities);
            var firstPublication = Assert.Single(first.Publications);
            var secondPublication = Assert.Single(second.Publications);

            Assert.Equal(first.PackageId, second.PackageId);
            Assert.Equal(firstPublication.CanonicalPublicationId, secondPublication.CanonicalPublicationId);
            Assert.Equal(
                "Baldur's Gate: Descent Into Avernus",
                await ReadCanonicalDisplayNameAsync(db, firstPublication.CanonicalPublicationId));
            Assert.Equal(2, await db.SourceEntities.CountAsync(value => value.SourcePackageId == first.PackageId));
        }
    }

    [Fact]
    public async Task SourceCodeFallbackIsUpgradedWhenRichCorpusMetadataArrivesLater()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var adapter = new FiveEToolsSourceFormatAdapter();
            var importer = new NormalizedSourceImportService(db);
            var packageKey = $"split-phb-{Guid.NewGuid():N}";

            var entityRepresentation = RequireRepresentation(adapter.TryRead(Artifact(
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
            var metadataRepresentation = RequireRepresentation(adapter.TryRead(Artifact(
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
            Assert.Equal("PHB", await ReadCanonicalDisplayNameAsync(db, firstPublication.CanonicalPublicationId));

            var second = await ImportAsync(importer, packageKey, metadataRepresentation);
            var secondPublication = Assert.Single(second.Publications);

            Assert.Equal(firstPublication.CanonicalPublicationId, secondPublication.CanonicalPublicationId);
            Assert.Equal(
                "Player's Handbook (2014)",
                await ReadCanonicalDisplayNameAsync(db, firstPublication.CanonicalPublicationId));
        }
    }

    [Fact]
    public async Task ChildAdventureAndParentBookRemainDistinctWhenTheyShareSourceCode()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var adapter = new FiveEToolsSourceFormatAdapter();
            var importer = new NormalizedSourceImportService(db);
            var packageKey = $"split-fraif-{Guid.NewGuid():N}";

            var childRepresentation = RequireRepresentation(adapter.TryRead(Artifact(
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
            var parentRepresentation = RequireRepresentation(adapter.TryRead(Artifact(
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

            var child = Assert.Single((await ImportAsync(importer, packageKey, childRepresentation)).Publications);
            var parent = Assert.Single((await ImportAsync(importer, packageKey, parentRepresentation)).Publications);

            Assert.NotEqual(child.CanonicalPublicationId, parent.CanonicalPublicationId);
            Assert.Equal(
                "The Lost Library of Lethchauntos",
                await ReadCanonicalDisplayNameAsync(db, child.CanonicalPublicationId));
            Assert.Equal(
                "Forgotten Realms: Adventures in Faerûn",
                await ReadCanonicalDisplayNameAsync(db, parent.CanonicalPublicationId));
        }
    }

    [Fact]
    public async Task ContextualSourceCodeDoesNotGloballyMergeDifferentRichPublications()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var importer = new NormalizedSourceImportService(db);
            var packageKey = $"split-contextual-{Guid.NewGuid():N}";

            var first = await ImportAsync(importer, packageKey, Representation(
                "shared-code", "First Real Title", "first.json", "First Rule"));
            var second = await ImportAsync(importer, packageKey, Representation(
                "shared-code", "Different Real Title", "second.json", "Second Rule"));

            Assert.NotEqual(
                Assert.Single(first.Publications).CanonicalPublicationId,
                Assert.Single(second.Publications).CanonicalPublicationId);
        }
    }

    private static SourceRepresentationArtifact Artifact(string fileName, string json) =>
        new(fileName, Encoding.UTF8.GetBytes(json), $"test:{Guid.NewGuid():N}");

    private static NormalizedSourceRepresentation Representation(
        string localKey,
        string displayName,
        string fileName,
        string ruleName)
    {
        var record = new NormalizedSourceRecord(
            "rule",
            ruleName,
            localKey,
            $"rule|{localKey}|{ruleName}",
            $"{{\"name\":\"{ruleName}\",\"source\":\"{localKey}\"}}",
            PublicationLocalKey: localKey);
        var publication = new NormalizedSourcePublication(
            localKey,
            displayName,
            ExternalIdentifiers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["5etools-source-code"] = localKey
            });
        return new NormalizedSourceRepresentation(
            FiveEToolsSourceFormatAdapter.Format,
            Artifact(fileName, "{}"),
            [record],
            [publication]);
    }

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
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        return db;
    }

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
        if (openedHere) await connection.OpenAsync();
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
            if (openedHere) await connection.CloseAsync();
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
