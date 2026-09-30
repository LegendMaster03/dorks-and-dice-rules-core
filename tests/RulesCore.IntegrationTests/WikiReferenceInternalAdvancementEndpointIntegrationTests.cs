using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class WikiReferenceInternalAdvancementEndpointIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task PrivateWikiEndpointReturnsAuthoritativeFiveXAcquisitionLevelsWithoutConceptCreation()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var authenticationClient = new FakeAuthenticationClient();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IToolHostAuthenticationClient>();
                services.AddSingleton<IToolHostAuthenticationClient>(authenticationClient);
            });
        });
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        await ResetRulesAsync(db);

        var token = Guid.NewGuid().ToString("N")[..8];
        var packageKey = $"wiki-internal-advancement-{token}";
        var package55Key = $"wiki-internal-advancement-55-{token}";
        var source = $"WIA{token}";
        var source55 = $"WI5{token}";
        var className = $"Internal Mage {token}";
        var subclassName = $"Internal Path {token}";
        var class55Name = $"Internal Revised Mage {token}";

        try
        {
            var importer = new SourceImportService(db);
            var imported = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                PackageKey: packageKey,
                PackageDisplayName: packageKey,
                Provider: "integration-test",
                License: "test-only",
                IsPublic: true,
                WorkKey: $"work-{packageKey}",
                WorkDisplayName: packageKey,
                EditionKey: "5e",
                EditionDisplayName: "5e",
                Json: $$"""
                    {
                      "class": [{
                        "name": "{{className}}",
                        "source": "{{source}}",
                        "classFeatures": [
                          "Internal Study|{{className}}|{{source}}|1",
                          { "classFeature": "Internal Focus|{{className}}|{{source}}|3" },
                          "Unresolved Internal Feature"
                        ]
                      }],
                      "subclass": [{
                        "name": "{{subclassName}}",
                        "shortName": "Internal Path",
                        "source": "{{source}}",
                        "className": "{{className}}",
                        "classSource": "{{source}}",
                        "subclassFeatures": [
                          "Internal Initiate|{{className}}|{{source}}|{{subclassName}}|{{source}}|3",
                          "Internal Adept|{{className}}|{{source}}|{{subclassName}}|{{source}}|7"
                        ]
                      }]
                    }
                    """,
                GameEdition: "5e"));

            var imported55 = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                PackageKey: package55Key,
                PackageDisplayName: package55Key,
                Provider: "integration-test",
                License: "test-only",
                IsPublic: true,
                WorkKey: $"work-{package55Key}",
                WorkDisplayName: package55Key,
                EditionKey: "5.5e",
                EditionDisplayName: "5.5e",
                Json: $$"""
                    {
                      "class": [{
                        "name": "{{class55Name}}",
                        "source": "{{source55}}",
                        "classFeatures": ["Revised Internal Study|{{class55Name}}|{{source55}}|2"]
                      }]
                    }
                    """,
                GameEdition: "5.5e"));

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var classDetail = await FetchDetailAsync(client, "class", className);
            Assert.Collection(
                classDetail.EffectiveAdvancementFeatures,
                feature => Assert.Equal(("Internal Study", (int?)1), (feature.Name, feature.Level)),
                feature => Assert.Equal(("Internal Focus", (int?)3), (feature.Name, feature.Level)),
                feature => Assert.Equal(("Unresolved Internal Feature", (int?)null), (feature.Name, feature.Level)));

            var subclassDetail = await FetchDetailAsync(client, "subclass", subclassName);
            Assert.Collection(
                subclassDetail.EffectiveAdvancementFeatures,
                feature => Assert.Equal(("Internal Initiate", (int?)3), (feature.Name, feature.Level)),
                feature => Assert.Equal(("Internal Adept", (int?)7), (feature.Name, feature.Level)));

            var class55Detail = await FetchDetailAsync(client, "class", class55Name);
            Assert.Equal("5.5e", class55Detail.Reference.EffectiveEditionKey);
            Assert.Collection(
                class55Detail.EffectiveAdvancementFeatures,
                feature => Assert.Equal(("Revised Internal Study", (int?)2), (feature.Name, feature.Level)));

            var importedIds = imported.Entities.Concat(imported55.Entities).Select(value => value.EntityId).ToArray();
            Assert.Empty(await db.RuleConceptSourceBindings
                .Where(value => value.SourceEntityId.HasValue && importedIds.Contains(value.SourceEntityId.Value))
                .ToArrayAsync());
            Assert.Empty(await db.RuleConcepts.ToArrayAsync());
        }
        finally
        {
            await ResetRulesAsync(db);
            db.ChangeTracker.Clear();
            var packages = await db.SourcePackages
                .Where(value => value.Key == packageKey || value.Key == package55Key)
                .ToArrayAsync();
            db.SourcePackages.RemoveRange(packages);
            await db.SaveChangesAsync();
        }
    }

    private static async Task<WikiReferenceDetailView> FetchDetailAsync(
        HttpClient client,
        string entityType,
        string name)
    {
        using var catalogRequest = HostedRequest(
            $"/api/wiki/references?entityType={Uri.EscapeDataString(entityType)}&q={Uri.EscapeDataString(name)}");
        using var catalogResponse = await client.SendAsync(catalogRequest);
        Assert.Equal(HttpStatusCode.OK, catalogResponse.StatusCode);
        var catalog = (await catalogResponse.Content.ReadFromJsonAsync<WikiReferenceCatalogView>())!;
        var reference = Assert.Single(catalog.References, value => value.DisplayName == name);

        using var detailRequest = HostedRequest(
            $"/api/wiki/references/{Uri.EscapeDataString(reference.ReferenceIdentity)}");
        using var detailResponse = await client.SendAsync(detailRequest);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        return (await detailResponse.Content.ReadFromJsonAsync<WikiReferenceDetailView>())!;
    }

    private static HttpRequestMessage HostedRequest(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(ToolHostAuthenticationHeaders.Ticket, "wiki-ticket");
        request.Headers.Add(ToolHostAuthenticationHeaders.IntrospectionPath, IntrospectionPath);
        return request;
    }

    private static async Task ResetRulesAsync(RulesCoreDbContext db)
    {
        await db.CampaignRulesetRevisionEntries.ExecuteDeleteAsync();
        await db.CampaignRulesetRevisions.ExecuteDeleteAsync();
        await db.CampaignRuleDecisions.ExecuteDeleteAsync();
        await db.CampaignRulesetSelections.ExecuteDeleteAsync();
        await db.RulesetRevisionEntries.ExecuteDeleteAsync();
        await db.RulesetRevisions.ExecuteDeleteAsync();
        await db.GlobalRuleDecisions.ExecuteDeleteAsync();
        await db.RuleConceptSourceBindings.ExecuteDeleteAsync();
        await db.RuleConcepts.ExecuteDeleteAsync();
        db.ChangeTracker.Clear();
    }

    private sealed class FakeAuthenticationClient : IToolHostAuthenticationClient
    {
        public Task<ToolHostAuthenticationContext?> RedeemAsync(
            string ticket,
            string introspectionPath,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(ticket, "wiki-ticket", StringComparison.Ordinal)
                || !string.Equals(introspectionPath, IntrospectionPath, StringComparison.Ordinal))
            {
                return Task.FromResult<ToolHostAuthenticationContext?>(null);
            }

            ToolHostAuthenticationContext context = new(
                ContractVersion: 1,
                ToolSlug: "rules-core",
                SiteMode: "dorks-and-dice",
                User: new ToolHostUserContext("wiki-reader", "Wiki Reader"),
                GlobalRoles: [],
                Campaigns: [])
            {
                ToolKey = "rules-core",
                DelegatedFromToolKey = "rules-wiki",
                DelegatedFromToolSlug = "rules-wiki"
            };
            return Task.FromResult<ToolHostAuthenticationContext?>(context);
        }
    }
}
