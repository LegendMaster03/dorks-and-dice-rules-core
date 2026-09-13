using System.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class FiveEToolsPublicationMetadataReconciliationIntegrationTests
{
    [Fact]
    public async Task BookMetadataUpgradesCanonicalPublicationForCrossFormatLookup()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore"))) return;

        await using var factory = new WebApplicationFactory<Program>();
        await using var scope = factory.Services.CreateAsyncScope();
        var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

        var token = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var packageKey = $"book-metadata-{token.ToLowerInvariant()}";
        var sourceCode = $"B{token}";
        Guid? packageId = null;
        Guid? canonicalPublicationId = null;

        try
        {
            var imported = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                packageKey,
                "Book metadata test",
                "integration-test",
                License: null,
                IsPublic: false,
                WorkKey: sourceCode.ToLowerInvariant(),
                WorkDisplayName: sourceCode,
                EditionKey: "current",
                EditionDisplayName: "Current",
                Json: $$"""
                    {
                      "book": [
                        {
                          "name": "Cross Format Handbook {{token}}",
                          "id": "{{sourceCode}}",
                          "source": "{{sourceCode}}",
                          "published": "2014-08-19"
                        }
                      ],
                      "spell": [
                        {
                          "name": "Metadata Test Spell {{token}}",
                          "source": "{{sourceCode}}",
                          "level": 3,
                          "entries": ["Test content."]
                        }
                      ]
                    }
                    """,
                GameEdition: "5e"));
            packageId = imported.PackageId;

            await new CanonicalPublicationPublisherService(db)
                .ReconcilePackageAsync(imported.PackageId);

            var spell = imported.Entities.Single(value => value.EntityType == "spell");
            var canonical = await ReadCanonicalPublicationAsync(db, spell.EntityId);
            Assert.NotNull(canonical);
            canonicalPublicationId = canonical.Value.PublicationId;
            Assert.Equal($"Cross Format Handbook {token}", canonical.Value.DisplayName);
            Assert.Equal(new DateOnly(2014, 8, 19), canonical.Value.PublicationDate);

            var resolvedWithoutFiveEToolsAlias = await new CanonicalPublicationIdentityService(db).ResolveAsync(
                new CanonicalPublicationEvidence(
                    $"Cross Format Handbook {token}",
                    Publisher: null,
                    GameEdition: "5e",
                    PublicationDate: new DateOnly(2014, 8, 19)));

            Assert.Equal(canonicalPublicationId, resolvedWithoutFiveEToolsAlias.Id);
            Assert.Equal("bibliographic", resolvedWithoutFiveEToolsAlias.MatchKind);
        }
        finally
        {
            if (packageId is Guid id)
            {
                var package = await db.SourcePackages.SingleOrDefaultAsync(value => value.Id == id);
                if (package is not null)
                {
                    db.SourcePackages.Remove(package);
                    await db.SaveChangesAsync();
                }
            }

            if (canonicalPublicationId is Guid publicationId)
            {
                var connection = db.Database.GetDbConnection();
                var openedHere = connection.State != ConnectionState.Open;
                if (openedHere) await connection.OpenAsync();
                try
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = "DELETE FROM canonical_publication WHERE canonical_publication_id = @id;";
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "@id";
                    parameter.Value = publicationId;
                    command.Parameters.Add(parameter);
                    await command.ExecuteNonQueryAsync();
                }
                finally
                {
                    if (openedHere) await connection.CloseAsync();
                }
            }
        }
    }

    private static async Task<(Guid PublicationId, string DisplayName, DateOnly? PublicationDate)?> ReadCanonicalPublicationAsync(
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
                SELECT publication.canonical_publication_id, publication.display_name, publication.publication_date
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                JOIN canonical_publication publication
                    ON publication.canonical_publication_id = occurrence.canonical_publication_id
                WHERE binding.source_entity_id = @entity_id;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@entity_id";
            parameter.Value = sourceEntityId;
            command.Parameters.Add(parameter);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return (
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetFieldValue<DateOnly>(2));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }
}
