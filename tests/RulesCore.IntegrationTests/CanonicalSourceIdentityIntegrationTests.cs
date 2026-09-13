using System.Data.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CanonicalSourceIdentityIntegrationTests
{
    [Fact]
    public async Task IndependentPackagesCanShareCanonicalPublicationAndOccurrenceWithoutSharingSourceRecords()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore"))) return;

        await using var factory = new WebApplicationFactory<Program>();
        await using var scope = factory.Services.CreateAsyncScope();
        var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        var identity = new CanonicalSourceIdentityService(db);

        var token = Guid.NewGuid().ToString("N")[..10];
        var sourceCode = $"CANON{token}".ToUpperInvariant();
        var firstPackageKey = $"canonical-first-{token}";
        var secondPackageKey = $"canonical-second-{token}";
        Guid? canonicalPublicationId = null;

        var json = $$"""
            {
              "spell": [
                {
                  "name": "Identity Test Spell",
                  "source": "{{sourceCode}}",
                  "page": 101,
                  "level": 3,
                  "school": "V",
                  "entries": ["The same rule-bearing text appears in both representations."]
                }
              ]
            }
            """;

        try
        {
            var first = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                firstPackageKey,
                "First account representation",
                "first-provider",
                License: null,
                IsPublic: false,
                WorkKey: "first-work",
                WorkDisplayName: "First representation of the book",
                EditionKey: "release",
                EditionDisplayName: "Release",
                Json: json,
                GameEdition: "5e"));

            var second = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                secondPackageKey,
                "Second account representation",
                "second-provider",
                License: null,
                IsPublic: false,
                WorkKey: "second-work",
                WorkDisplayName: "Second representation of the book",
                EditionKey: "release",
                EditionDisplayName: "Release",
                Json: json,
                GameEdition: "5e"));

            Assert.NotEqual(first.PackageId, second.PackageId);
            var firstEntity = Assert.Single(first.Entities);
            var secondEntity = Assert.Single(second.Entities);
            Assert.NotEqual(firstEntity.EntityId, secondEntity.EntityId);

            Assert.Equal(1, await identity.IndexPackageAsync(first.PackageId));
            Assert.Equal(1, await identity.IndexPackageAsync(second.PackageId));

            var firstCanonical = await ReadCanonicalBindingAsync(db, firstEntity.EntityId);
            var secondCanonical = await ReadCanonicalBindingAsync(db, secondEntity.EntityId);
            Assert.NotNull(firstCanonical);
            Assert.NotNull(secondCanonical);
            Assert.Equal(firstCanonical.Value.PublicationId, secondCanonical.Value.PublicationId);
            Assert.Equal(firstCanonical.Value.OccurrenceId, secondCanonical.Value.OccurrenceId);
            canonicalPublicationId = firstCanonical.Value.PublicationId;

            var byAlias = await identity.ResolvePublicationAsync(new CanonicalPublicationEvidence(
                "A third representation does not need the package-owned work name",
                Aliases: new Dictionary<string, string>
                {
                    ["5etools-source-code"] = sourceCode
                }));
            Assert.Equal(canonicalPublicationId, byAlias.Id);
            Assert.Equal("alias:5etools-source-code", byAlias.MatchKind);
        }
        finally
        {
            var packages = await db.SourcePackages
                .Where(value => value.Key == firstPackageKey || value.Key == secondPackageKey)
                .ToArrayAsync();
            if (packages.Length > 0)
            {
                db.SourcePackages.RemoveRange(packages);
                await db.SaveChangesAsync();
            }

            if (canonicalPublicationId is Guid publicationId)
            {
                var connection = db.Database.GetDbConnection();
                var openedHere = connection.State != System.Data.ConnectionState.Open;
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

    private static async Task<(Guid PublicationId, Guid OccurrenceId)?> ReadCanonicalBindingAsync(
        RulesCoreDbContext db,
        Guid sourceEntityId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    occurrence.canonical_publication_id,
                    binding.canonical_source_occurrence_id
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                WHERE binding.source_entity_id = @entity_id;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@entity_id";
            parameter.Value = sourceEntityId;
            command.Parameters.Add(parameter);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return (reader.GetGuid(0), reader.GetGuid(1));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }
}
