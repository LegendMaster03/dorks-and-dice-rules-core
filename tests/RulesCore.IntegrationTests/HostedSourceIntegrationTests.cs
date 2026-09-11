using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class HostedSourceIntegrationTests
{
    [Fact]
    public async Task DirectHostedSourceIsPersistentIdempotentMatchableAndRefreshesImmutableRevisions()
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

        var packageKey = $"hosted-direct-{Guid.NewGuid():N}";
        var definitionKey = $"hosted-direct-{Guid.NewGuid():N}";
        const string sourceUri = "https://1.1.1.1/feats.json";
        var documents = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [sourceUri] = """
                {
                  "feat": [
                    { "name": "Hosted Feat", "source": "SRD51", "entries": ["v1"] },
                    { "name": "Other Feat", "source": "XGE", "entries": ["not selected"] }
                  ]
                }
                """
        };
        using var client = CreateClient(documents);
        var importer = new SourceImportService(db);
        var service = new HostedSourceService(db, importer, client);

        try
        {
            var request = DefinitionRequest(
                packageKey,
                ["SRD51"],
                [new HostedSourceResourceRequest(HostedSourceResourceKinds.DirectJson, sourceUri)]);

            var created = await service.SetAsync(definitionKey, request, "rules-lawyer");
            Assert.True(created.CreatedRevision);
            Assert.Equal(1, created.RevisionNumber);
            Assert.Equal(sourceUri, Assert.Single(created.Resources).Uri);

            var unchangedDefinition = await service.SetAsync(definitionKey, request, "rules-lawyer");
            Assert.False(unchangedDefinition.CreatedRevision);
            Assert.Equal(1, unchangedDefinition.RevisionNumber);

            var match = Assert.Single(await service.FindMatchesAsync(["SRD51"]));
            Assert.True(match.ExactSourceCodeMatch);
            Assert.Equal(definitionKey, match.DefinitionKey);

            var preview = await service.PreviewAsync(created.Id);
            Assert.Single(preview.Documents);
            Assert.Equal(1, preview.Preview.EntityCount);
            Assert.Equal(1, preview.Preview.NewEntityCount);
            Assert.Equal(0, preview.Preview.NewRevisionCount);

            var firstRefresh = await service.RefreshAsync(created.Id);
            var firstEntity = Assert.Single(firstRefresh.Import.Entities);
            Assert.True(firstEntity.CreatedRevision);
            Assert.Equal(1, firstEntity.RevisionNumber);

            var repeatedRefresh = await service.RefreshAsync(created.Id);
            var repeatedEntity = Assert.Single(repeatedRefresh.Import.Entities);
            Assert.False(repeatedEntity.CreatedRevision);
            Assert.Equal(1, repeatedEntity.RevisionNumber);

            documents[sourceUri] = """
                {
                  "feat": [
                    { "name": "Hosted Feat", "source": "SRD51", "entries": ["v2"] },
                    { "name": "Other Feat", "source": "XGE", "entries": ["not selected"] }
                  ]
                }
                """;

            var changedPreview = await service.PreviewAsync(created.Id);
            Assert.Equal(0, changedPreview.Preview.NewEntityCount);
            Assert.Equal(1, changedPreview.Preview.NewRevisionCount);

            var secondRefresh = await service.RefreshAsync(created.Id);
            var secondEntity = Assert.Single(secondRefresh.Import.Entities);
            Assert.True(secondEntity.CreatedRevision);
            Assert.Equal(2, secondEntity.RevisionNumber);

            var revisedDefinition = await service.SetAsync(
                definitionKey,
                request with { Note = "canonical source reviewed" },
                "rules-lawyer");
            Assert.True(revisedDefinition.CreatedRevision);
            Assert.Equal(2, revisedDefinition.RevisionNumber);
            Assert.Equal("canonical source reviewed", revisedDefinition.Note);
        }
        finally
        {
            await CleanupAsync(db, packageKey, definitionKey);
        }
    }

    [Fact]
    public async Task JsonIndexExpandsChildDocumentsAndAppliesLogicalSourceCodeFilter()
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

        var packageKey = $"hosted-index-{Guid.NewGuid():N}";
        var definitionKey = $"hosted-index-{Guid.NewGuid():N}";
        const string indexUri = "https://1.1.1.1/spells/index.json";
        const string phbUri = "https://1.1.1.1/spells/spells-phb.json";
        const string xgeUri = "https://1.1.1.1/spells/spells-xge.json";
        var documents = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [indexUri] = "{\"PHB\":\"spells-phb.json\",\"XGE\":\"spells-xge.json\"}",
            [phbUri] = "{\"spell\":[{\"name\":\"Indexed Spell\",\"source\":\"PHB\",\"level\":1}]}",
            [xgeUri] = "{\"spell\":[{\"name\":\"Ignored Spell\",\"source\":\"XGE\",\"level\":1}]}"
        };
        using var client = CreateClient(documents);
        var service = new HostedSourceService(db, new SourceImportService(db), client);

        try
        {
            var definition = await service.SetAsync(
                definitionKey,
                DefinitionRequest(
                    packageKey,
                    ["PHB"],
                    [new HostedSourceResourceRequest(HostedSourceResourceKinds.JsonIndex, indexUri)]),
                "rules-lawyer");

            var preview = await service.PreviewAsync(definition.Id);
            var resolved = Assert.Single(preview.Documents);
            Assert.Equal(HostedSourceResourceKinds.JsonIndex, resolved.ResourceKind);
            Assert.Equal(phbUri, resolved.ResolvedUri);
            Assert.Equal(1, resolved.SelectedEntityCount);
            Assert.Equal(1, preview.Preview.EntityCount);
            Assert.Equal("PHB", Assert.Single(preview.Preview.Entities).SourceCode);
        }
        finally
        {
            await CleanupAsync(db, packageKey, definitionKey);
        }
    }

    [Fact]
    public async Task GitHubTreeEnumeratesJsonFilesBelowConfiguredPath()
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

        var packageKey = $"hosted-tree-{Guid.NewGuid():N}";
        var definitionKey = $"hosted-tree-{Guid.NewGuid():N}";
        const string treeUri = "https://github.com/example/rules/tree/main/data";
        const string treeApiUri = "https://api.github.com/repos/example/rules/git/trees/main?recursive=1";
        const string rawUri = "https://raw.githubusercontent.com/example/rules/main/data/feats.json";
        var documents = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [treeApiUri] = """
                {
                  "truncated": false,
                  "tree": [
                    { "path": "data/feats.json", "type": "blob" },
                    { "path": "data/readme.txt", "type": "blob" },
                    { "path": "outside/ignored.json", "type": "blob" }
                  ]
                }
                """,
            [rawUri] = "{\"feat\":[{\"name\":\"Tree Feat\",\"source\":\"TREE\"}]}"
        };
        using var client = CreateClient(documents);
        var service = new HostedSourceService(db, new SourceImportService(db), client);

        try
        {
            var definition = await service.SetAsync(
                definitionKey,
                DefinitionRequest(
                    packageKey,
                    ["TREE"],
                    [new HostedSourceResourceRequest(HostedSourceResourceKinds.GitHubTree, treeUri)]),
                "rules-lawyer");

            var preview = await service.PreviewAsync(definition.Id);
            var resolved = Assert.Single(preview.Documents);
            Assert.Equal(HostedSourceResourceKinds.GitHubTree, resolved.ResourceKind);
            Assert.Equal(rawUri, resolved.ResolvedUri);
            Assert.Equal(1, preview.Preview.EntityCount);
        }
        finally
        {
            await CleanupAsync(db, packageKey, definitionKey);
        }
    }

    private static SetHostedSourceDefinitionRequest DefinitionRequest(
        string packageKey,
        IReadOnlyList<string> sourceCodes,
        IReadOnlyList<HostedSourceResourceRequest> resources) =>
        new(
            DisplayName: $"Hosted {packageKey}",
            FormatKind: HostedSourceFormatKinds.FiveEToolsJson,
            PackageKey: packageKey,
            PackageDisplayName: $"Package {packageKey}",
            Provider: "integration-test",
            License: "test-only",
            IsPublic: true,
            WorkKey: "hosted-work",
            WorkDisplayName: "Hosted Work",
            EditionKey: "hosted-release",
            EditionDisplayName: "Hosted Release",
            GameEdition: "5e",
            ReleaseKind: "published",
            PublicationDate: new DateOnly(2026, 9, 10),
            IncludedSourceCodes: sourceCodes,
            Resources: resources);

    private static HttpClient CreateClient(IReadOnlyDictionary<string, string> documents) =>
        new(new StubHandler(documents))
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

    private static async Task CleanupAsync(
        RulesCoreDbContext db,
        string packageKey,
        string definitionKey)
    {
        var packages = await db.SourcePackages
            .Where(value => value.Key == packageKey)
            .ToArrayAsync();
        db.SourcePackages.RemoveRange(packages);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM hosted_source_definition WHERE definition_key = {0};",
            definitionKey);
    }

    private sealed class StubHandler(IReadOnlyDictionary<string, string> documents)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (!documents.TryGetValue(uri, out var content))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    RequestMessage = request,
                    Content = new StringContent("not found", Encoding.UTF8, "text/plain")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
        }
    }
}
