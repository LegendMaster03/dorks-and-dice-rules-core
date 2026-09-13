using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class NormalizedSourceProvenanceIntegrationTests
{
    [Fact]
    public async Task CurrentUserPdfUploadUsesByteSafeAdapterPipelineAndOnlyGrantsUploader()
    {
        var db = await OpenDatabaseAsync();
        if (db is null)
        {
            return;
        }
        await using (db)
        {
            var legacyImporter = new SourceImportService(db);
            var grants = new SourceGrantService(db);
            var sources = new CurrentUserSourceService(db, legacyImporter, grants);
            var userId = $"pdf-owner-{Guid.NewGuid():N}";
            var otherUserId = $"pdf-other-{Guid.NewGuid():N}";
            var bytes = BuildPdf(
                "Title: Uploaded Homebrew",
                "Publisher: Table Press",
                "ISBN: 978-0-3064-0615-7",
                "This page must survive byte-safe upload ingestion.");

            var added = await sources.AddAsync(
                userId,
                new AddCurrentUserSourceRequest(
                    CurrentUserSourceKinds.Upload,
                    FileName: "uploaded-homebrew.pdf",
                    ContentBase64: Convert.ToBase64String(bytes)));

            Assert.Equal(CurrentUserSourceKinds.Upload, added.Kind);
            Assert.True(added.EntityCount >= 1);
            Assert.True(await grants.HasGrantAsync(userId, added.SourcePackageId));
            Assert.False(await grants.HasGrantAsync(otherUserId, added.SourcePackageId));

            var ownerPackages = await new SourceCatalogService(db).GetAccessiblePackagesAsync(userId);
            var otherPackages = await new SourceCatalogService(db).GetAccessiblePackagesAsync(otherUserId);
            Assert.Contains(ownerPackages, value => value.Id == added.SourcePackageId);
            Assert.DoesNotContain(otherPackages, value => value.Id == added.SourcePackageId);

            var storedBytes = await ReadRepresentationBytesAsync(db, added.SourcePackageId);
            Assert.Equal(bytes, storedBytes);
        }
    }

    [Fact]
    public async Task ConflictingPublisherEvidenceIsRetainedWithoutOverwritingCanonicalPublisher()
    {
        var db = await OpenDatabaseAsync();
        if (db is null)
        {
            return;
        }
        await using (db)
        {
            var importer = new NormalizedSourceImportService(db);
            const string isbn = "9780306406157";
            var first = await importer.ImportAsync(BuildImport(
                $"publisher-a-{Guid.NewGuid():N}",
                "Original Press",
                isbn,
                "rule-a"));
            var second = await importer.ImportAsync(BuildImport(
                $"publisher-b-{Guid.NewGuid():N}",
                "Conflicting Press",
                isbn,
                "rule-b"));

            var firstPublication = Assert.Single(first.Publications);
            var secondPublication = Assert.Single(second.Publications);
            Assert.Equal(firstPublication.CanonicalPublicationId, secondPublication.CanonicalPublicationId);

            var canonicalPublisher = await ReadCanonicalPublisherAsync(
                db,
                firstPublication.CanonicalPublicationId);
            Assert.Equal("Original Press", canonicalPublisher);

            var firstPublisher = await ReadEditionPublisherAsync(db, firstPublication.EditionId);
            var secondPublisher = await ReadEditionPublisherAsync(db, secondPublication.EditionId);
            Assert.Equal("Original Press", firstPublisher);
            Assert.Equal("Conflicting Press", secondPublisher);

            var conflict = await ReadPublisherConflictAsync(
                db,
                firstPublication.CanonicalPublicationId);
            Assert.Equal("Original Press", conflict.CanonicalValue);
            Assert.Equal("Conflicting Press", conflict.ObservedValue);
        }
    }

    private static ImportNormalizedSourceRequest BuildImport(
        string packageKey,
        string publisher,
        string isbn,
        string ruleName)
    {
        var artifact = new SourceRepresentationArtifact(
            $"{packageKey}.json",
            "{}"u8.ToArray(),
            $"test:{Guid.NewGuid():N}");
        var publication = new NormalizedSourcePublication(
            "same-publication",
            "Shared Publication",
            [new NormalizedSourceRecord(
                "rule",
                ruleName,
                "same-publication",
                $"same-publication|{ruleName}",
                $"{{\"name\":\"{ruleName}\",\"text\":\"mechanic {ruleName}\"}}")],
            Publisher: publisher,
            ExternalIdentifiers: new Dictionary<string, string> { ["isbn"] = isbn });
        return new ImportNormalizedSourceRequest(
            packageKey,
            $"{publisher} representation",
            "test",
            License: null,
            IsPublic: false,
            new NormalizedSourceRepresentation(
                FiveEToolsSourceFormatAdapter.Format,
                artifact,
                [publication]));
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

    private static byte[] BuildPdf(params string[] lines)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var y = 760d;
        foreach (var line in lines)
        {
            page.AddText(line, 11, new PdfPoint(36, y), font);
            y -= 18;
        }
        return builder.Build();
    }

    private static async Task<byte[]> ReadRepresentationBytesAsync(
        RulesCoreDbContext db,
        Guid packageId)
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
                SELECT content_bytes
                FROM source_representation
                WHERE source_package_id = @package_id
                ORDER BY imported_at DESC
                LIMIT 1;
                """;
            AddParameter(command, "@package_id", packageId);
            return (byte[])(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Representation was not stored."));
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<string?> ReadCanonicalPublisherAsync(
        RulesCoreDbContext db,
        Guid publicationId) =>
        await ReadScalarStringAsync(
            db,
            "SELECT publisher FROM canonical_publication WHERE canonical_publication_id = @id;",
            publicationId);

    private static async Task<string?> ReadEditionPublisherAsync(
        RulesCoreDbContext db,
        Guid editionId) =>
        await ReadScalarStringAsync(
            db,
            "SELECT publisher FROM source_edition_publisher WHERE source_edition_id = @id;",
            editionId);

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

    private static async Task<(string? CanonicalValue, string? ObservedValue)> ReadPublisherConflictAsync(
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
                SELECT canonical_value, observed_value
                FROM canonical_publication_evidence_conflict
                WHERE canonical_publication_id = @publication_id
                    AND field_name = 'publisher'
                ORDER BY recorded_at DESC
                LIMIT 1;
                """;
            AddParameter(command, "@publication_id", publicationId);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            return (
                reader.IsDBNull(0) ? null : reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1));
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
