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
public sealed class SourceFormatAdapterIntegrationTests
{
    [Fact]
    public void PdfAdapterRetainsReadablePagesAsUnclassifiedFragmentsWithBibliographicEvidence()
    {
        var bytes = BuildPdf(
            [
                "Title: Third Party Compendium",
                "Publisher: Example Press",
                "System: D&D 5e",
                "Publication Date: 2026-05-01",
                "ISBN: 978-1-4028-9462-6",
                "This paragraph is readable source material."
            ],
            ["A second page must remain available even when it is not classified."]);
        var adapter = new PdfSourceFormatAdapter();

        var result = adapter.TryRead(new SourceRepresentationArtifact(
            "third-party-compendium.pdf",
            bytes,
            "upload:test-pdf"));

        Assert.NotNull(result);
        Assert.Equal(PdfSourceFormatAdapter.Format, result.FormatKey);
        var publication = Assert.Single(result.Publications);
        Assert.Equal("Third Party Compendium", publication.DisplayName);
        Assert.Equal("Example Press", publication.Publisher);
        Assert.Equal("D&D 5e", publication.GameEdition);
        Assert.Equal(new DateOnly(2026, 5, 1), publication.PublicationDate);
        Assert.Equal("9781402894626", publication.ExternalIdentifiers!["isbn"]);
        Assert.Equal(2, publication.Records.Count);
        Assert.All(publication.Records, record => Assert.Equal("source-fragment", record.EntityType));
        Assert.Equal("page:1", publication.Records[0].LocatorKey);
        Assert.Equal("page:2", publication.Records[1].LocatorKey);
        Assert.Contains("readable source material", publication.Records[0].RawJson, StringComparison.Ordinal);
        Assert.Contains("second page", publication.Records[1].RawJson, StringComparison.Ordinal);
    }

    [Fact]
    public void PdfAdapterRejectsPdfWithoutUsableTextLayer()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.A4);
        var bytes = builder.Build();

        var result = new PdfSourceFormatAdapter().TryRead(new SourceRepresentationArtifact(
            "scan-only.pdf",
            bytes,
            "upload:scan-only"));

        Assert.Null(result);
    }

    [Fact]
    public async Task NewThirdPartyPdfEstablishesPublicationAndPreservesRepresentationBytes()
    {
        var db = await OpenDatabaseAsync();
        if (db is null)
        {
            return;
        }
        await using (db)
        {
            var bytes = BuildPdf(
                [
                    "Title: The Clockwork Bestiary",
                    "Publisher: Small Forge Games",
                    "Publication Date: 2026-04-03",
                    "ISBN: 978-1-2345-6789-7",
                    "Clockwork creatures use the following optional rules."
                ],
                ["This page is intentionally left as generic source evidence."]);
            var representation = Assert.NotNull(new PdfSourceFormatAdapter().TryRead(
                new SourceRepresentationArtifact(
                    "clockwork-bestiary.pdf",
                    bytes,
                    $"upload:{Guid.NewGuid():N}")));
            var packageKey = $"pdf-new-{Guid.NewGuid():N}";

            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    "Clockwork Bestiary upload",
                    "user-upload",
                    License: null,
                    IsPublic: false,
                    representation));

            var publication = Assert.Single(imported.Publications);
            Assert.NotEqual(Guid.Empty, publication.CanonicalPublicationId);
            Assert.Equal(2, publication.EntityCount);
            Assert.All(imported.Entities, entity => Assert.Equal("source-fragment", entity.EntityType));

            var stored = await ReadRepresentationAsync(db, imported.PackageId);
            Assert.Equal(PdfSourceFormatAdapter.Format, stored.FormatKey);
            Assert.Equal(bytes, stored.Bytes);
            Assert.Equal(bytes.LongLength, stored.Length);
            Assert.Equal(2, await db.SourceEntities.CountAsync(
                value => value.SourceEdition.SourceWork.SourcePackageId == imported.PackageId));
        }
    }

    [Fact]
    public async Task MatchingPdfUsesExistingCanonicalPublicationWithoutSharingPackageGrant()
    {
        var db = await OpenDatabaseAsync();
        if (db is null)
        {
            return;
        }
        await using (db)
        {
            var importer = new NormalizedSourceImportService(db);
            var aggregatePackageKey = $"aggregate-{Guid.NewGuid():N}";
            var aggregate = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                aggregatePackageKey,
                "Aggregate structured source",
                "test-structured",
                License: null,
                IsPublic: false,
                new NormalizedSourceRepresentation(
                    FiveEToolsSourceFormatAdapter.Format,
                    new SourceRepresentationArtifact(
                        "aggregate.json",
                        "{}"u8.ToArray(),
                        $"test:{Guid.NewGuid():N}"),
                    [
                        Publication(
                            "known-book",
                            "Known Book",
                            "Known Press",
                            "9781402894626",
                            "spell",
                            "Shared Rule",
                            "known-book|shared-rule",
                            "{\"name\":\"Shared Rule\",\"effect\":\"same mechanic\"}"),
                        Publication(
                            "unrelated-book",
                            "Unrelated Book",
                            "Other Press",
                            "9781234567897",
                            "spell",
                            "Unrelated Rule",
                            "unrelated-book|rule",
                            "{\"name\":\"Unrelated Rule\",\"effect\":\"unrelated\"}")
                    ])));
            await new SourceGrantService(db).GrantAsync("account-a", aggregate.PackageId);

            var pdfBytes = BuildPdf(
                [
                    "Title: Known Book",
                    "Publisher: Known Press",
                    "ISBN: 978-1-4028-9462-6",
                    "This PDF is a separate physical representation."
                ]);
            var pdfRepresentation = Assert.NotNull(new PdfSourceFormatAdapter().TryRead(
                new SourceRepresentationArtifact(
                    "known-book.pdf",
                    pdfBytes,
                    $"upload:{Guid.NewGuid():N}")));
            var pdfImport = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                $"pdf-account-b-{Guid.NewGuid():N}",
                "Known Book PDF",
                "user-upload",
                License: null,
                IsPublic: false,
                pdfRepresentation));
            await new SourceGrantService(db).GrantAsync("account-b", pdfImport.PackageId);

            var aggregateKnown = aggregate.Publications.Single(value => value.DisplayName == "Known Book");
            var pdfKnown = Assert.Single(pdfImport.Publications);
            Assert.Equal(aggregateKnown.CanonicalPublicationId, pdfKnown.CanonicalPublicationId);

            var accountBCatalog = await new SourceCatalogService(db).GetAccessiblePackagesAsync("account-b");
            Assert.Contains(accountBCatalog, value => value.Id == pdfImport.PackageId);
            Assert.DoesNotContain(accountBCatalog, value => value.Id == aggregate.PackageId);
            Assert.False(await new SourceGrantService(db).HasGrantAsync("account-b", aggregate.PackageId));

            var representations = await CountRepresentationsAsync(
                db,
                aggregateKnown.CanonicalPublicationId);
            Assert.True(representations >= 2);
        }
    }

    [Fact]
    public async Task SameTitleWithoutStrongEvidenceDoesNotSilentlyMergeDifferentPdfContent()
    {
        var db = await OpenDatabaseAsync();
        if (db is null)
        {
            return;
        }
        await using (db)
        {
            var importer = new NormalizedSourceImportService(db);
            var first = await ImportPdfAsync(
                db,
                importer,
                "Echoes",
                BuildPdf(["Title: Echoes", "Rule text unique to the first publication."]));
            var second = await ImportPdfAsync(
                db,
                importer,
                "Echoes",
                BuildPdf(["Title: Echoes", "Completely different rules from another publication."]));

            Assert.NotEqual(
                Assert.Single(first.Publications).CanonicalPublicationId,
                Assert.Single(second.Publications).CanonicalPublicationId);
        }
    }

    private static NormalizedSourcePublication Publication(
        string key,
        string title,
        string publisher,
        string isbn,
        string entityType,
        string name,
        string naturalKey,
        string rawJson) =>
        new(
            key,
            title,
            [new NormalizedSourceRecord(entityType, name, key, naturalKey, rawJson)],
            Publisher: publisher,
            ExternalIdentifiers: new Dictionary<string, string> { ["isbn"] = isbn });

    private static async Task<NormalizedSourceImportResult> ImportPdfAsync(
        RulesCoreDbContext db,
        NormalizedSourceImportService importer,
        string displayName,
        byte[] bytes)
    {
        var representation = Assert.NotNull(new PdfSourceFormatAdapter().TryRead(
            new SourceRepresentationArtifact(
                $"{displayName}.pdf",
                bytes,
                $"upload:{Guid.NewGuid():N}")));
        return await importer.ImportAsync(new ImportNormalizedSourceRequest(
            $"pdf-{Guid.NewGuid():N}",
            $"{displayName} PDF",
            "user-upload",
            License: null,
            IsPublic: false,
            representation));
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

    private static byte[] BuildPdf(params string[][] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var lines in pages)
        {
            var page = builder.AddPage(PageSize.A4);
            var y = 760d;
            foreach (var line in lines)
            {
                page.AddText(line, 11, new PdfPoint(36, y), font);
                y -= 18;
            }
        }
        return builder.Build();
    }

    private static async Task<(string FormatKey, long Length, byte[] Bytes)> ReadRepresentationAsync(
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
                SELECT format_key, content_length, content_bytes
                FROM source_representation
                WHERE source_package_id = @package_id
                ORDER BY imported_at DESC
                LIMIT 1;
                """;
            AddParameter(command, "@package_id", packageId);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            return (reader.GetString(0), reader.GetInt64(1), (byte[])reader.GetValue(2));
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<int> CountRepresentationsAsync(
        RulesCoreDbContext db,
        Guid canonicalPublicationId)
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
                SELECT COUNT(DISTINCT source_representation_id)
                FROM source_representation_publication
                WHERE canonical_publication_id = @publication_id;
                """;
            AddParameter(command, "@publication_id", canonicalPublicationId);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
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
