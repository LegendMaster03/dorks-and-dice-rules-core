using System.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class GlobalSourceDispositionIntegrationTests
{
    [Fact]
    public async Task IgnoringPackageSuppressesGlobalNormalizationWithoutRevokingSourceAccess()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore"))) return;

        await using var factory = new WebApplicationFactory<Program>();
        await using var scope = factory.Services.CreateAsyncScope();
        var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
        var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
        var catalog = scope.ServiceProvider.GetRequiredService<ISourceCatalogService>();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        var normalization = new SourceNormalizationService(db);
        var disposition = new GlobalSourceDispositionService(db);

        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"ignored-homebrew-{token}";
        var userId = $"ignore-user-{token}";
        Guid? packageId = null;
        Guid? canonicalPublicationId = null;

        try
        {
            var imported = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                packageKey,
                "Large homebrew collection",
                "integration-test",
                License: null,
                IsPublic: false,
                WorkKey: "homebrew-work",
                WorkDisplayName: "Homebrew work",
                EditionKey: "current",
                EditionDisplayName: "Current",
                Json: $$"""
                    {
                      "spell": [
                        {
                          "name": "Irrelevant Homebrew {{token}}",
                          "source": "HB{{token}}",
                          "level": 1,
                          "entries": ["Integration test content."]
                        }
                      ]
                    }
                    """,
                GameEdition: "5e",
                ReleaseKind: SourceReleaseKinds.Other,
                Publisher: "Homebrew Publisher"));
            packageId = imported.PackageId;
            await grants.GrantAsync(userId, imported.PackageId);

            var canonical = await ReadCanonicalPublisherAsync(db, imported.Entities.Single().EntityId);
            Assert.NotNull(canonical);
            canonicalPublicationId = canonical.Value.PublicationId;
            Assert.Equal("Homebrew Publisher", canonical.Value.Publisher);

            var before = await normalization.GetCandidatesPageAsync(userId, query: token, limit: 20);
            var candidate = Assert.Single(before);
            Assert.Equal(imported.PackageId, candidate.SourcePackageId);

            var ignored = await disposition.SetIgnoredAsync(
                imported.PackageId,
                new SetGlobalSourceIgnoredRequest(true, "Not relevant to the global ruleset."),
                "rules-lawyer");
            Assert.NotNull(ignored);
            Assert.Equal("Homebrew Publisher", imported.Publisher);

            var ignoredView = Assert.Single(
                await disposition.GetIgnoredAsync(),
                value => value.SourcePackageId == imported.PackageId);
            Assert.Equal("Not relevant to the global ruleset.", ignoredView.Reason);

            Assert.Empty(await normalization.GetCandidatesPageAsync(userId, query: token, limit: 20));

            var accessiblePackages = await catalog.GetAccessiblePackagesAsync(userId);
            Assert.Contains(accessiblePackages, value => value.Id == imported.PackageId);
            Assert.True(await grants.HasGrantAsync(userId, imported.PackageId));

            await disposition.SetIgnoredAsync(
                imported.PackageId,
                new SetGlobalSourceIgnoredRequest(false),
                "rules-lawyer");

            Assert.Single(await normalization.GetCandidatesPageAsync(userId, query: token, limit: 20));
            Assert.DoesNotContain(
                await disposition.GetIgnoredAsync(),
                value => value.SourcePackageId == imported.PackageId);
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

    private static async Task<(Guid PublicationId, string? Publisher)?> ReadCanonicalPublisherAsync(
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
                SELECT publication.canonical_publication_id, publication.publisher
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
            return (reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }
}
