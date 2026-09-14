using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CanonicalSourceIdentityIntegrationTests
{
    [Fact]
    public async Task IndependentPackagesReuseStrongCanonicalPublicationAndOccurrenceWithoutSharingSourceRecords()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var importer = new NormalizedSourceImportService(db);
            var firstKey = $"canonical-first-{Guid.NewGuid():N}";
            var secondKey = $"canonical-second-{Guid.NewGuid():N}";
            const string isbn = "9781402894626";

            var first = await importer.ImportAsync(Request(
                firstKey,
                Representation("first.json", "first-origin", isbn)));
            var second = await importer.ImportAsync(Request(
                secondKey,
                Representation("second.json", "second-origin", isbn)));

            Assert.NotEqual(first.PackageId, second.PackageId);
            var firstEntity = Assert.Single(first.Entities);
            var secondEntity = Assert.Single(second.Entities);
            Assert.NotEqual(firstEntity.EntityId, secondEntity.EntityId);

            var firstCanonical = await ReadCanonicalBindingAsync(db, firstEntity.EntityId);
            var secondCanonical = await ReadCanonicalBindingAsync(db, secondEntity.EntityId);
            Assert.NotNull(firstCanonical);
            Assert.NotNull(secondCanonical);
            Assert.Equal(firstCanonical.Value.PublicationId, secondCanonical.Value.PublicationId);
            Assert.Equal(firstCanonical.Value.OccurrenceId, secondCanonical.Value.OccurrenceId);
            Assert.NotNull(firstCanonical.Value.CanonicalEntityId);
            Assert.Equal(firstCanonical.Value.CanonicalEntityId, secondCanonical.Value.CanonicalEntityId);

            var resolved = await new CanonicalPublicationIdentityService(db).ResolveAsync(
                new CanonicalPublicationEvidence(
                    "Identity Test Book",
                    Publisher: "Example Press",
                    GameEdition: "5e",
                    PublicationDate: new DateOnly(2020, 1, 2),
                    Aliases: new Dictionary<string, string> { ["isbn"] = isbn }));
            Assert.Equal(firstCanonical.Value.PublicationId, resolved.Id);
            Assert.Equal("alias:isbn", resolved.MatchKind);
        }
    }

    [Fact]
    public async Task EquivalentEntitiesAcrossDifferentPublicationsReuseCanonicalEntityButNotOccurrence()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var importer = new NormalizedSourceImportService(db);
            const string body = "{\"name\":\"Shared Mechanic\",\"effect\":\"Gain a +2 bonus on the check.\"}";

            var first = await importer.ImportAsync(Request(
                $"entity-publication-a-{Guid.NewGuid():N}",
                PublicationRepresentation(
                    "publication-a.json",
                    "publication-a",
                    "Publication A",
                    "9781402894626",
                    "Shared Mechanic",
                    body)));
            var second = await importer.ImportAsync(Request(
                $"entity-publication-b-{Guid.NewGuid():N}",
                PublicationRepresentation(
                    "publication-b.json",
                    "publication-b",
                    "Publication B",
                    "9780306406157",
                    "Shared Mechanic",
                    body)));

            var firstBinding = await ReadCanonicalBindingAsync(db, Assert.Single(first.Entities).EntityId);
            var secondBinding = await ReadCanonicalBindingAsync(db, Assert.Single(second.Entities).EntityId);
            Assert.NotNull(firstBinding);
            Assert.NotNull(secondBinding);
            Assert.NotEqual(firstBinding.Value.PublicationId, secondBinding.Value.PublicationId);
            Assert.NotEqual(firstBinding.Value.OccurrenceId, secondBinding.Value.OccurrenceId);
            Assert.NotNull(firstBinding.Value.CanonicalEntityId);
            Assert.Equal(firstBinding.Value.CanonicalEntityId, secondBinding.Value.CanonicalEntityId);
        }
    }

    [Fact]
    public async Task SameNameDifferentMechanicsRemainDifferentCanonicalEntities()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var importer = new NormalizedSourceImportService(db);
            var first = await importer.ImportAsync(Request(
                $"entity-variant-a-{Guid.NewGuid():N}",
                PublicationRepresentation(
                    "variant-a.json",
                    "variant-a",
                    "Variant Publication A",
                    "9781402894626",
                    "Shared Name",
                    "{\"name\":\"Shared Name\",\"effect\":\"Gain a +2 bonus.\"}")));
            var second = await importer.ImportAsync(Request(
                $"entity-variant-b-{Guid.NewGuid():N}",
                PublicationRepresentation(
                    "variant-b.json",
                    "variant-b",
                    "Variant Publication B",
                    "9780306406157",
                    "Shared Name",
                    "{\"name\":\"Shared Name\",\"effect\":\"Gain advantage instead.\"}")));

            var firstBinding = await ReadCanonicalBindingAsync(db, Assert.Single(first.Entities).EntityId);
            var secondBinding = await ReadCanonicalBindingAsync(db, Assert.Single(second.Entities).EntityId);
            Assert.NotNull(firstBinding);
            Assert.NotNull(secondBinding);
            Assert.NotNull(firstBinding.Value.CanonicalEntityId);
            Assert.NotNull(secondBinding.Value.CanonicalEntityId);
            Assert.NotEqual(firstBinding.Value.CanonicalEntityId, secondBinding.Value.CanonicalEntityId);
        }
    }

    [Fact]
    public async Task FormatNeutralContentEvidenceCanAssociateASeparateRepresentation()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var importer = new NormalizedSourceImportService(db);
            var structuredKey = $"canonical-structured-{Guid.NewGuid():N}";
            var extractedKey = $"canonical-extracted-{Guid.NewGuid():N}";

            const string firstJson = "{\"name\":\"First Rule\",\"effect\":\"Unique first mechanical text.\"}";
            const string secondJson = "{\"name\":\"Second Rule\",\"effect\":\"Unique second mechanical text.\"}";
            const string thirdJson = "{\"name\":\"Third Rule\",\"effect\":\"Unique third mechanical text.\"}";
            var publicationKey = "structured-book";
            var structuredRepresentation = new NormalizedSourceRepresentation(
                FiveEToolsSourceFormatAdapter.Format,
                Artifact("structured.json", "structured-origin"),
                [
                    Record(publicationKey, "spell", "First Rule", "first", firstJson, "page:101"),
                    Record(publicationKey, "spell", "Second Rule", "second", secondJson, "page:102"),
                    Record(publicationKey, "spell", "Third Rule", "third", thirdJson, "page:103")
                ],
                [new NormalizedSourcePublication(
                    publicationKey,
                    "Structured Book",
                    Publisher: "Example Press",
                    GameEdition: "5e",
                    PublicationDate: new DateOnly(2020, 1, 2))]);

            var structured = await importer.ImportAsync(Request(structuredKey, structuredRepresentation));
            var knownBinding = await ReadCanonicalBindingAsync(db, structured.Entities[0].EntityId);
            Assert.NotNull(knownBinding);
            Assert.NotNull(knownBinding.Value.CanonicalEntityId);

            const string extractedJson = "{\"name\":\"OCR title variation\",\"effect\":\"Unique first mechanical text.\"}";
            var extractedRepresentation = new NormalizedSourceRepresentation(
                "pdf-extracted-test",
                Artifact("extracted.json", "extracted-origin"),
                [new NormalizedSourceRecord(
                    "spell",
                    "OCR title variation",
                    SourceCode: null,
                    NativeKey: "page-101-rule",
                    RawJson: extractedJson,
                    LocatorKey: "page:101")]);
            var extracted = await importer.ImportAsync(Request(extractedKey, extractedRepresentation));
            var extractedEntity = Assert.Single(extracted.Entities);
            Assert.Null(await ReadCanonicalBindingAsync(db, extractedEntity.EntityId));

            var occurrenceFingerprints = new[]
            {
                CanonicalSourceIdentity.SemanticFingerprint(firstJson),
                CanonicalSourceIdentity.SemanticFingerprint(secondJson),
                CanonicalSourceIdentity.SemanticFingerprint(thirdJson)
            };
            var association = await new CanonicalSourceRepresentationService(db).AssociateSourceEntityAsync(
                extractedEntity.EntityId,
                new CanonicalPublicationEvidence(
                    "OCR did not recover the canonical title",
                    GameEdition: "5e",
                    OccurrenceFingerprints: occurrenceFingerprints),
                new CanonicalSourceOccurrenceEvidence(
                    "spell",
                    "OCR title variation",
                    "page:101",
                    CanonicalSourceIdentity.SemanticFingerprint(extractedJson)),
                "pdf-extracted-test");

            Assert.Equal(knownBinding.Value.PublicationId, association.Publication.Id);
            Assert.Equal("content-overlap", association.PublicationMatchKind);
            Assert.Equal("semantic-fingerprint", association.OccurrenceMatchKind);
            Assert.Equal(knownBinding.Value.OccurrenceId, association.CanonicalOccurrenceId);
            var extractedBinding = await ReadCanonicalBindingAsync(db, extractedEntity.EntityId);
            Assert.NotNull(extractedBinding);
            Assert.Equal(knownBinding.Value.CanonicalEntityId, extractedBinding.Value.CanonicalEntityId);
            Assert.NotEqual(structured.PackageId, extracted.PackageId);
            Assert.NotEqual(structured.Entities[0].EntityId, extractedEntity.EntityId);
        }
    }

    private static ImportNormalizedSourceRequest Request(
        string packageKey,
        NormalizedSourceRepresentation representation) =>
        new(
            packageKey,
            packageKey,
            "integration-test",
            License: null,
            IsPublic: false,
            representation);

    private static NormalizedSourceRepresentation Representation(
        string fileName,
        string origin,
        string isbn)
    {
        const string publicationKey = "identity-book";
        const string rawJson = "{\"name\":\"Identity Test Spell\",\"effect\":\"The same rule-bearing text.\"}";
        return new NormalizedSourceRepresentation(
            FiveEToolsSourceFormatAdapter.Format,
            Artifact(fileName, origin),
            [Record(publicationKey, "spell", "Identity Test Spell", "identity-spell", rawJson, "page:101")],
            [new NormalizedSourcePublication(
                publicationKey,
                "Identity Test Book",
                Publisher: "Example Press",
                GameEdition: "5e",
                PublicationDate: new DateOnly(2020, 1, 2),
                ExternalIdentifiers: new Dictionary<string, string> { ["isbn"] = isbn })]);
    }

    private static NormalizedSourceRepresentation PublicationRepresentation(
        string fileName,
        string origin,
        string publicationName,
        string isbn,
        string entityName,
        string rawJson)
    {
        var publicationKey = $"publication:{isbn}";
        return new NormalizedSourceRepresentation(
            FiveEToolsSourceFormatAdapter.Format,
            Artifact(fileName, origin),
            [Record(publicationKey, "feat", entityName, $"feat:{entityName}", rawJson, "page:42")],
            [new NormalizedSourcePublication(
                publicationKey,
                publicationName,
                Publisher: "Example Press",
                GameEdition: "5e",
                PublicationDate: new DateOnly(2020, 1, 2),
                ExternalIdentifiers: new Dictionary<string, string> { ["isbn"] = isbn })]);
    }

    private static NormalizedSourceRecord Record(
        string publicationKey,
        string entityType,
        string name,
        string nativeKey,
        string rawJson,
        string? locator = null) =>
        new(
            entityType,
            name,
            SourceCode: publicationKey,
            NativeKey: nativeKey,
            RawJson: rawJson,
            LocatorKey: locator,
            PublicationLocalKey: publicationKey);

    private static SourceRepresentationArtifact Artifact(string fileName, string origin) =>
        new(fileName, "{}"u8.ToArray(), $"test:{origin}:{Guid.NewGuid():N}");

    private static async Task<(Guid PublicationId, Guid OccurrenceId, Guid? CanonicalEntityId)?> ReadCanonicalBindingAsync(
        RulesCoreDbContext db,
        Guid sourceEntityId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    occurrence.canonical_publication_id,
                    binding.canonical_source_occurrence_id,
                    occurrence.canonical_entity_id
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                WHERE binding.source_entity_id = @entity_id;
                """;
            AddParameter(command, "@entity_id", sourceEntityId);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return (
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetGuid(2));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
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

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
