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
    public async Task IdenticalPrivateImportsShareOnePhysicalContentBlobAcrossUsers()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var legacyImporter = new SourceImportService(db);
            var grants = new SourceGrantService(db);
            var sources = new CurrentUserSourceService(db, legacyImporter, grants);
            var firstUserId = $"blob-owner-a-{Guid.NewGuid():N}";
            var secondUserId = $"blob-owner-b-{Guid.NewGuid():N}";
            var bytes = BuildPdf(
                "Title: Shared Physical Fixture",
                "Publisher: Table Press",
                "The same physical bytes must not be stored twice.");

            var first = await sources.AddAsync(
                firstUserId,
                new AddCurrentUserSourceRequest(
                    CurrentUserSourceKinds.Upload,
                    FileName: "first-name.pdf")
                {
                    ContentBase64 = Convert.ToBase64String(bytes)
                });
            var second = await sources.AddAsync(
                secondUserId,
                new AddCurrentUserSourceRequest(
                    CurrentUserSourceKinds.Upload,
                    FileName: "second-name.pdf")
                {
                    ContentBase64 = Convert.ToBase64String(bytes)
                });

            Assert.NotEqual(first.SourcePackageId, second.SourcePackageId);
            Assert.True(await grants.HasGrantAsync(firstUserId, first.SourcePackageId));
            Assert.False(await grants.HasGrantAsync(firstUserId, second.SourcePackageId));
            Assert.True(await grants.HasGrantAsync(secondUserId, second.SourcePackageId));
            Assert.False(await grants.HasGrantAsync(secondUserId, first.SourcePackageId));

            var firstRepresentation = await db.SourceRepresentations
                .AsNoTracking()
                .SingleAsync(value => value.SourcePackageId == first.SourcePackageId);
            var secondRepresentation = await db.SourceRepresentations
                .AsNoTracking()
                .SingleAsync(value => value.SourcePackageId == second.SourcePackageId);

            Assert.NotEqual(firstRepresentation.Id, secondRepresentation.Id);
            Assert.Equal(firstRepresentation.ContentSha256, secondRepresentation.ContentSha256);
            Assert.Equal(
                1,
                await db.SourceContentBlobs.CountAsync(
                    value => value.Sha256 == firstRepresentation.ContentSha256));
            Assert.Equal(bytes, await ReadRepresentationBytesAsync(db, first.SourcePackageId));
            Assert.Equal(bytes, await ReadRepresentationBytesAsync(db, second.SourcePackageId));

            await db.SourcePackages
                .Where(value => value.Id == first.SourcePackageId)
                .ExecuteDeleteAsync();

            Assert.Equal(
                1,
                await db.SourceContentBlobs.CountAsync(
                    value => value.Sha256 == secondRepresentation.ContentSha256));
            Assert.Equal(bytes, await ReadRepresentationBytesAsync(db, second.SourcePackageId));
        }
    }

    [Fact]
    public async Task CurrentUserPdfUploadUsesByteSafeAdapterPipelineAndOnlyGrantsUploader()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
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
                    FileName: "uploaded-homebrew.pdf")
                {
                    ContentBase64 = Convert.ToBase64String(bytes)
                });

            Assert.Equal(CurrentUserSourceKinds.Upload, added.Kind);
            Assert.True(added.EntityCount >= 1);
            Assert.True(await grants.HasGrantAsync(userId, added.SourcePackageId));
            Assert.False(await grants.HasGrantAsync(otherUserId, added.SourcePackageId));

            var ownerPackages = await new SourceCatalogService(db).GetAccessiblePackagesAsync(userId);
            var otherPackages = await new SourceCatalogService(db).GetAccessiblePackagesAsync(otherUserId);
            Assert.Contains(ownerPackages, value => value.Id == added.SourcePackageId);
            Assert.DoesNotContain(otherPackages, value => value.Id == added.SourcePackageId);

            Assert.Equal(bytes, await ReadRepresentationBytesAsync(db, added.SourcePackageId));
        }
    }

    [Fact]
    public async Task ConflictingPublisherEvidenceIsRecordedWithoutOverwritingCanonicalPublisher()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var importer = new NormalizedSourceImportService(db);
            const string isbn = "9798675309001";
            var first = await importer.ImportAsync(BuildImport(
                $"publisher-a-{Guid.NewGuid():N}", "Original Press", isbn, "rule-a"));
            var second = await importer.ImportAsync(BuildImport(
                $"publisher-b-{Guid.NewGuid():N}", "Conflicting Press", isbn, "rule-b"));

            var firstPublication = Assert.Single(first.Publications);
            var secondPublication = Assert.Single(second.Publications);
            Assert.Equal(firstPublication.CanonicalPublicationId, secondPublication.CanonicalPublicationId);
            Assert.Equal(
                "Original Press",
                await ReadCanonicalPublisherAsync(db, firstPublication.CanonicalPublicationId));

            var secondRepresentationId = await ReadRepresentationIdAsync(db, second.PackageId);
            var conflict = await ReadPublisherConflictAsync(db, firstPublication.CanonicalPublicationId);
            Assert.Equal(secondRepresentationId, conflict.SourceRepresentationId);
            Assert.Equal("Original Press", conflict.CanonicalValue);
            Assert.Equal("Conflicting Press", conflict.ObservedValue);

            Assert.NotEqual(first.PackageId, second.PackageId);
            Assert.Equal(1, await db.SourceRepresentations.CountAsync(value => value.SourcePackageId == first.PackageId));
            Assert.Equal(1, await db.SourceRepresentations.CountAsync(value => value.SourcePackageId == second.PackageId));
        }
    }

    [Fact]
    public async Task LateRepresentationFailureRollsBackEntireNormalizedImport()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var importer = new NormalizedSourceImportService(db);
            var packageKey = $"atomic-{Guid.NewGuid():N}";
            var request = BuildImport(packageKey, "Atomic Press", "9798675309018", "atomic-rule");
            var invalidRequest = request with
            {
                Representation = request.Representation with { FormatKey = new string('x', 81) }
            };

            await Assert.ThrowsAsync<ArgumentException>(() => importer.ImportAsync(invalidRequest));
            db.ChangeTracker.Clear();
            Assert.False(await db.SourcePackages.AnyAsync(value => value.Key == packageKey));
        }
    }

    private static ImportNormalizedSourceRequest BuildImport(
        string packageKey,
        string publisher,
        string isbn,
        string ruleName)
    {
        var localKey = "same-publication";
        var artifact = new SourceRepresentationArtifact(
            $"{packageKey}.json",
            "{}"u8.ToArray(),
            $"test:{Guid.NewGuid():N}");
        var record = new NormalizedSourceRecord(
            "rule",
            ruleName,
            localKey,
            $"same-publication|{ruleName}",
            $"{{\"name\":\"{ruleName}\",\"text\":\"mechanic {ruleName}\"}}",
            PublicationLocalKey: localKey);
        var publication = new NormalizedSourcePublication(
            localKey,
            "Shared Publication",
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
                [record],
                [publication]));
    }

    private static async Task<RulesCoreDbContext?> OpenDatabaseAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);
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

    private static Task<byte[]> ReadRepresentationBytesAsync(RulesCoreDbContext db, Guid packageId) =>
        ReadScalarAsync<byte[]>(db,
            """
            SELECT blob.content_bytes
            FROM source_representation representation
            JOIN source_content_blob blob
                ON blob.content_sha256 = representation.content_sha256
            WHERE representation.source_package_id = @id
            ORDER BY representation.imported_at DESC
            LIMIT 1;
            """,
            packageId);

    private static Task<Guid> ReadRepresentationIdAsync(RulesCoreDbContext db, Guid packageId) =>
        ReadScalarAsync<Guid>(db,
            "SELECT source_representation_id FROM source_representation WHERE source_package_id = @id ORDER BY imported_at DESC LIMIT 1;",
            packageId);

    private static async Task<string?> ReadCanonicalPublisherAsync(RulesCoreDbContext db, Guid publicationId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT publisher FROM canonical_publication WHERE canonical_publication_id = @id;";
            AddParameter(command, "@id", publicationId);
            var result = await command.ExecuteScalarAsync();
            return result is null or DBNull ? null : (string)result;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<T> ReadScalarAsync<T>(RulesCoreDbContext db, string sql, Guid id)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            AddParameter(command, "@id", id);
            return (T)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Expected stored value was not found."));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<(Guid? SourceRepresentationId, string? CanonicalValue, string? ObservedValue)>
        ReadPublisherConflictAsync(RulesCoreDbContext db, Guid publicationId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT source_representation_id, canonical_value, observed_value
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
                reader.IsDBNull(0) ? null : reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2));
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
