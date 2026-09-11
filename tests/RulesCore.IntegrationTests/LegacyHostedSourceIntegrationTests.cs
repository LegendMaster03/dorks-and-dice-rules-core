using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class LegacyHostedSourceIntegrationTests
{
    [Fact]
    public async Task ThreeEHtmlIndexNormalizesAndImportsFeatEntities()
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

        var packageKey = $"legacy-3e-{Guid.NewGuid():N}";
        var definitionKey = $"legacy-3e-{Guid.NewGuid():N}";
        const string indexUri = "https://1.1.1.1/30srd/";
        const string featsUri = "https://1.1.1.1/30srd/feats.htm";
        var documents = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [indexUri] = "<a href=\"feats.htm\">Feat Descriptions</a>",
            [featsUri] = """
                <html><body>
                <h1>Feats</h1>
                <h2>Power Attack [General]</h2>
                <p>Prerequisite: Str 13.</p>
                <p>Benefit: Trade attack bonus for damage.</p>
                </body></html>
                """
        };
        using var client = CreateClient(documents);
        var service = new LegacyAwareHostedSourceService(db, new SourceImportService(db), client);

        try
        {
            var definition = await service.SetAsync(
                definitionKey,
                LegacyDefinition(
                    packageKey,
                    "3e",
                    "SRD3",
                    [new HostedSourceResourceRequest(HostedSourceResourceKinds.HtmlIndex, indexUri)]),
                "rules-lawyer");

            Assert.Equal(HostedSourceFormatKinds.LegacySrdText, definition.FormatKind);
            var preview = await service.PreviewAsync(definition.Id);
            var powerAttack = Assert.Single(
                preview.Preview.Entities,
                value => value.EntityType == "feat" && value.Name == "Power Attack");
            Assert.Equal("SRD3", powerAttack.SourceCode);

            var refreshed = await service.RefreshAsync(definition.Id);
            var imported = Assert.Single(
                refreshed.Import.Entities,
                value => value.EntityType == "feat" && value.Name == "Power Attack");
            Assert.True(imported.CreatedRevision);

            var entity = await db.SourceEntities
                .AsNoTracking()
                .SingleAsync(value => value.Id == imported.EntityId);
            Assert.Equal("SRD3", entity.SourceCode);
        }
        finally
        {
            await CleanupAsync(db, packageKey, definitionKey);
        }
    }

    [Fact]
    public async Task ThreeFiveGitHubMarkdownTreeNormalizesAndImportsFeatEntities()
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

        var packageKey = $"legacy-35-{Guid.NewGuid():N}";
        var definitionKey = $"legacy-35-{Guid.NewGuid():N}";
        const string treeUri = "https://github.com/example/srd/tree/main/basic-rules-and-legal";
        const string treeApiUri = "https://api.github.com/repos/example/srd/git/trees/main?recursive=1";
        const string rawUri = "https://raw.githubusercontent.com/example/srd/main/basic-rules-and-legal/feats.md";
        var documents = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [treeApiUri] = """
                {
                  "truncated": false,
                  "tree": [
                    { "path": "basic-rules-and-legal/feats.md", "type": "blob" },
                    { "path": "README.md", "type": "blob" }
                  ]
                }
                """,
            [rawUri] = """
                This material is Open Game Content.

                # FEATS

                ## Feat Descriptions

                ### Power Attack <small>[General]</small>

                **Prerequisite:** Str 13.

                **Benefit:** Trade attack bonus for damage.
                """
        };
        using var client = CreateClient(documents);
        var service = new LegacyAwareHostedSourceService(db, new SourceImportService(db), client);

        try
        {
            var definition = await service.SetAsync(
                definitionKey,
                LegacyDefinition(
                    packageKey,
                    "3.5e",
                    "SRD35",
                    [new HostedSourceResourceRequest(HostedSourceResourceKinds.GitHubTree, treeUri)]),
                "rules-lawyer");

            var preview = await service.PreviewAsync(definition.Id);
            Assert.Single(preview.Documents);
            Assert.Equal(rawUri, preview.Documents[0].ResolvedUri);
            var powerAttack = Assert.Single(
                preview.Preview.Entities,
                value => value.EntityType == "feat" && value.Name == "Power Attack");
            Assert.Equal("SRD35", powerAttack.SourceCode);

            var refreshed = await service.RefreshAsync(definition.Id);
            Assert.Contains(
                refreshed.Import.Entities,
                value => value.EntityType == "feat"
                    && value.Name == "Power Attack"
                    && value.SourceCode == "SRD35");
        }
        finally
        {
            await CleanupAsync(db, packageKey, definitionKey);
        }
    }

    private static SetHostedSourceDefinitionRequest LegacyDefinition(
        string packageKey,
        string gameEdition,
        string sourceCode,
        IReadOnlyList<HostedSourceResourceRequest> resources) =>
        new(
            DisplayName: $"Legacy {gameEdition} integration source",
            FormatKind: HostedSourceFormatKinds.LegacySrdText,
            PackageKey: packageKey,
            PackageDisplayName: $"Legacy {gameEdition} integration package",
            Provider: "integration-test",
            License: "OGL-1.0a",
            IsPublic: true,
            WorkKey: "srd-work",
            WorkDisplayName: "SRD Work",
            EditionKey: "original",
            EditionDisplayName: $"{gameEdition} SRD",
            GameEdition: gameEdition,
            ReleaseKind: "srd",
            PublicationDate: null,
            IncludedSourceCodes: [sourceCode],
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
        db.ChangeTracker.Clear();
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
                Content = new StringContent(content, Encoding.UTF8, "text/plain")
            });
        }
    }
}
