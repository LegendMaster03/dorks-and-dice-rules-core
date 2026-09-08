using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SourceLayerPostgresCollection
{
    public const string Name = "SourceLayerPostgres";
}

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class SourceLayerIntegrationTests
{
    [Fact]
    public async Task ImportIsIdempotentAndPreservesUnknownFieldsAcrossRevisions()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var options = new DbContextOptionsBuilder<RulesCoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new RulesCoreDbContext(options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        var importer = new Infrastructure.Sources.SourceImportService(db);
        var packageKey = $"test-{Guid.NewGuid():N}";

        var firstRequest = CreateRequest(packageKey, """
            {
              "_meta": { "sources": [{ "json": "TST" }] },
              "skill": [
                {
                  "name": "Arcana",
                  "source": "TST",
                  "ability": "int",
                  "futureField": { "nested": [1, true, "kept"] }
                }
              ]
            }
            """);

        var first = await importer.Import5eToolsDocumentAsync(firstRequest);
        var reorderedSameDocument = CreateRequest(packageKey, """
            {
              "skill": [
                {
                  "futureField": { "nested": [1, true, "kept"] },
                  "ability": "int",
                  "source": "TST",
                  "name": "Arcana"
                }
              ],
              "_meta": { "sources": [{ "json": "TST" }] }
            }
            """);
        var second = await importer.Import5eToolsDocumentAsync(reorderedSameDocument);

        Assert.Single(first.Entities);
        Assert.True(first.Entities[0].CreatedRevision);
        Assert.Equal(1, first.Entities[0].RevisionNumber);
        Assert.False(second.Entities[0].CreatedRevision);
        Assert.Equal(first.Entities[0].Fingerprint, second.Entities[0].Fingerprint);

        var changedRequest = CreateRequest(packageKey, """
            {
              "skill": [
                {
                  "name": "Arcana",
                  "source": "TST",
                  "ability": "int",
                  "futureField": { "nested": [1, true, "changed"] }
                }
              ]
            }
            """);
        var third = await importer.Import5eToolsDocumentAsync(changedRequest);

        Assert.True(third.Entities[0].CreatedRevision);
        Assert.Equal(2, third.Entities[0].RevisionNumber);
        Assert.NotEqual(first.Entities[0].Fingerprint, third.Entities[0].Fingerprint);

        var entityId = first.Entities[0].EntityId;
        var revisions = await db.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => value.SourceEntityId == entityId)
            .OrderBy(value => value.RevisionNumber)
            .ToArrayAsync();

        Assert.Equal(2, revisions.Length);
        using var latest = JsonDocument.Parse(revisions[1].RawJson);
        var futureField = latest.RootElement.GetProperty("futureField");
        Assert.Equal("changed", futureField.GetProperty("nested")[2].GetString());

        var package = await db.SourcePackages.SingleAsync(value => value.Key == packageKey);
        db.SourcePackages.Remove(package);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task PublicReadApiReturnsLatestEntityWithProvenanceAndHidesPrivatePackages()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        Guid publicEntityId;
        Guid privateEntityId;
        string publicPackageKey;
        string privatePackageKey;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
            publicPackageKey = $"public-{Guid.NewGuid():N}";
            privatePackageKey = $"private-{Guid.NewGuid():N}";

            var publicResult = await importer.Import5eToolsDocumentAsync(
                CreateRequest(publicPackageKey, """
                    {
                      "skill": [
                        {
                          "name": "Investigation",
                          "source": "TST",
                          "ability": "int",
                          "unmodeled": { "preserved": true }
                        }
                      ]
                    }
                    "", isPublic: true));
            var privateResult = await importer.Import5eToolsDocumentAsync(
                CreateRequest(privatePackageKey, """
                    {
                      "skill": [
                        {
                          "name": "Private Skill",
                          "source": "PRIVATE",
                          "ability": "wis"
                        }
                      ]
                    }
                    "", isPublic: false));

            publicEntityId = publicResult.Entities[0].EntityId;
            privateEntityId = privateResult.Entities[0].EntityId;
        }

        using var publicResponse = await client.GetAsync($"/api/sources/entities/{publicEntityId}");
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        using var publicJson = JsonDocument.Parse(await publicResponse.Content.ReadAsStringAsync());
        Assert.Equal(publicPackageKey, publicJson.RootElement.GetProperty("packageKey").GetString());
        Assert.Equal("Investigation", publicJson.RootElement.GetProperty("name").GetString());
        Assert.True(publicJson.RootElement
            .GetProperty("document")
            .GetProperty("unmodeled")
            .GetProperty("preserved")
            .GetBoolean());

        using var privateResponse = await client.GetAsync($"/api/sources/entities/{privateEntityId}");
        Assert.Equal(HttpStatusCode.NotFound, privateResponse.StatusCode);

        await using var cleanupScope = factory.Services.CreateAsyncScope();
        var db = cleanupScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        var packages = await db.SourcePackages
            .Where(value => value.Key == publicPackageKey || value.Key == privatePackageKey)
            .ToArrayAsync();
        db.SourcePackages.RemoveRange(packages);
        await db.SaveChangesAsync();
    }

    private static Import5eToolsDocumentRequest CreateRequest(
        string packageKey,
        string json,
        bool isPublic = true) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: "Source Layer Test Package",
            Provider: "integration-test",
            License: "test-only",
            IsPublic: isPublic,
            WorkKey: "test-work",
            WorkDisplayName: "Test Work",
            EditionKey: "test-edition",
            EditionDisplayName: "Test Edition",
            Json: json);
}
