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

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class ResolvedRulesCatalogEditionMetadataIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task CatalogUsesCanonicalPublicationEditionMetadataWithoutFormatFallback()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var campaignId = Guid.NewGuid();
        var authenticationClient = new FakeToolHostAuthenticationClient(
            new Dictionary<string, ToolHostAuthenticationContext>
            {
                ["granted-ticket"] = Context("granted-reader", campaignId: null, campaignRole: null),
                ["ungranted-ticket"] = Context("ungranted-reader", campaignId: null, campaignRole: null),
                ["player-granted-ticket"] = Context("player-granted", campaignId, "Player"),
                ["lawyer-ticket"] = Context(
                    "rules-lawyer",
                    campaignId: null,
                    campaignRole: null,
                    globalRoles: ["Rules Lawyer"])
            });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var token = Guid.NewGuid().ToString("N")[..10];
        var fixtures = new[]
        {
            new EditionFixture("e5", "E5", "5e", true),
            new EditionFixture("e55", "E55", "5.5e", true),
            new EditionFixture("e3", "E3", "3e", true),
            new EditionFixture("e35", "E35", "3.5e", false),
            new EditionFixture("custom", "CUSTOM", null, true)
        };
        var packageIds = new List<Guid>();

        try
        {
            PublishedRulesetRevisionView globalRevision;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var campaignRules = scope.ServiceProvider.GetRequiredService<ICampaignRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

                foreach (var fixture in fixtures)
                {
                    var packageKey = $"catalog-edition-{fixture.Key}-{token}";
                    var entityName = $"Catalog Edition {fixture.SourceCode} {token}";
                    var imported = await importer.Import5eToolsDocumentAsync(SourceRequest(
                        packageKey,
                        entityName,
                        fixture.SourceCode,
                        fixture.GameEdition,
                        fixture.IsPublic));
                    packageIds.Add(imported.PackageId);

                    if (!fixture.IsPublic)
                    {
                        await grants.GrantAsync("rules-lawyer", imported.PackageId);
                        await grants.GrantAsync("granted-reader", imported.PackageId);
                        await grants.GrantAsync("player-granted", imported.PackageId);
                    }

                    var sourceEntityId = imported.Entities.Single().EntityId;
                    var sourceRevisionId = await db.SourceEntityRevisions
                        .Where(value => value.SourceEntityId == sourceEntityId)
                        .Select(value => value.Id)
                        .SingleAsync();
                    var concept = await globalRules.CreateConceptAsync(
                        new CreateRuleConceptRequest(
                            $"skill.catalog-edition-{fixture.Key}-{token}",
                            "skill",
                            entityName),
                        "rules-lawyer");
                    await globalRules.BindSourceEntityAsync(
                        concept.Value.Id,
                        new BindRuleConceptSourceRequest(sourceEntityId),
                        "rules-lawyer");
                    await globalRules.SetDecisionAsync(
                        concept.Value.Id,
                        new SetGlobalRuleDecisionRequest(sourceRevisionId, "Catalog edition metadata fixture."),
                        "rules-lawyer");
                }

                globalRevision = await globalRules.PublishAsync("rules-lawyer");
                await campaignRules.SelectBaselineAsync(
                    campaignId,
                    new SelectCampaignRulesetBaselineRequest(globalRevision.Id),
                    "campaign-dm");
                await campaignRules.PublishAsync(campaignId, "campaign-dm");
            }

            using (var anonymousResponse = await client.GetAsync($"/api/rules?q={token}"))
            {
                Assert.Equal(HttpStatusCode.OK, anonymousResponse.StatusCode);
                var catalog = (await anonymousResponse.Content
                    .ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                Assert.Equal(4, catalog.TotalCount);
                Assert.Equal(4, catalog.Rules.Count);
                Assert.DoesNotContain(catalog.Rules, value => value.SourceCode == "E35");
                Assert.Equal(4, catalog.SourceFacets.Count);
                Assert.All(catalog.SourceFacets, value => Assert.Equal(1, value.Count));
                AssertCatalogEditions(catalog.Rules, includePrivate: false);
            }

            ResolvedRulesCatalogView grantedCatalog;
            using (var grantedRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/rules?q={token}",
                       "granted-ticket"))
            using (var grantedResponse = await client.SendAsync(grantedRequest))
            {
                Assert.Equal(HttpStatusCode.OK, grantedResponse.StatusCode);
                grantedCatalog = (await grantedResponse.Content
                    .ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                Assert.Equal(5, grantedCatalog.TotalCount);
                Assert.Equal(5, grantedCatalog.Rules.Count);
                Assert.Equal(5, grantedCatalog.SourceFacets.Count);
                Assert.All(grantedCatalog.SourceFacets, value => Assert.Equal(1, value.Count));
                AssertCatalogEditions(grantedCatalog.Rules, includePrivate: true);
            }

            var pagedRules = new List<ResolvedRuleCatalogItemView>();
            for (var offset = 0; offset < 5; offset += 2)
            {
                using var request = HostedRequest(
                    HttpMethod.Get,
                    $"/api/rules?q={token}&limit=2&offset={offset}",
                    "granted-ticket");
                using var response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var page = (await response.Content.ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                Assert.Equal(5, page.TotalCount);
                if (offset == 0)
                {
                    Assert.Equal(5, page.SourceFacets.Count);
                }
                else
                {
                    Assert.Empty(page.SourceFacets);
                    Assert.Empty(page.EntityTypeFacets);
                }
                pagedRules.AddRange(page.Rules);
            }
            Assert.Equal(5, pagedRules.Count);
            Assert.Equal(5, pagedRules.Select(value => value.RuleConceptId).Distinct().Count());
            AssertCatalogEditions(pagedRules, includePrivate: true);

            using (var campaignRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules?q={token}",
                       "player-granted-ticket"))
            using (var campaignResponse = await client.SendAsync(campaignRequest))
            {
                Assert.Equal(HttpStatusCode.OK, campaignResponse.StatusCode);
                var campaignCatalog = (await campaignResponse.Content
                    .ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                Assert.Equal("campaign", campaignCatalog.Scope);
                Assert.Equal(campaignId, campaignCatalog.CampaignId);
                Assert.Equal(5, campaignCatalog.TotalCount);
                Assert.Equal(5, campaignCatalog.SourceFacets.Count);
                AssertCatalogEditions(campaignCatalog.Rules, includePrivate: true);
            }

            using (var ungrantedRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/rules?q={token}",
                       "ungranted-ticket"))
            using (var ungrantedResponse = await client.SendAsync(ungrantedRequest))
            {
                Assert.Equal(HttpStatusCode.OK, ungrantedResponse.StatusCode);
                var catalog = (await ungrantedResponse.Content
                    .ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                Assert.Equal(4, catalog.TotalCount);
                Assert.DoesNotContain(catalog.Rules, value => value.SourceCode == "E35");
            }
        }
        finally
        {
            await CleanupAsync(factory, packageIds);
        }
    }

    private static void AssertCatalogEditions(
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        bool includePrivate)
    {
        Assert.Equal("5e", rules.Single(value => value.SourceCode == "E5").EditionKey);
        Assert.Equal("5e", rules.Single(value => value.SourceCode == "E5").EditionDisplayName);
        Assert.Equal("5.5e", rules.Single(value => value.SourceCode == "E55").EditionDisplayName);
        Assert.Equal("3e", rules.Single(value => value.SourceCode == "E3").EditionDisplayName);
        if (includePrivate)
        {
            Assert.Equal("3.5e", rules.Single(value => value.SourceCode == "E35").EditionDisplayName);
        }
        var custom = rules.Single(value => value.SourceCode == "CUSTOM");
        Assert.Equal(string.Empty, custom.EditionKey);
        Assert.Equal(string.Empty, custom.EditionDisplayName);
        Assert.DoesNotContain(rules, value =>
            string.Equals(value.EditionKey, "5etools-json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value.EditionDisplayName, "5etools-json", StringComparison.OrdinalIgnoreCase));
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IToolHostAuthenticationClient authenticationClient) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IToolHostAuthenticationClient>();
                services.AddSingleton(authenticationClient);
            });
        });

    private static HttpRequestMessage HostedRequest(HttpMethod method, string path, string ticket)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(ToolHostAuthenticationHeaders.Ticket, ticket);
        request.Headers.Add(ToolHostAuthenticationHeaders.IntrospectionPath, IntrospectionPath);
        return request;
    }

    private static ToolHostAuthenticationContext Context(
        string userId,
        Guid? campaignId,
        string? campaignRole,
        IReadOnlyList<string>? globalRoles = null) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: "dorks-and-dice",
            User: new ToolHostUserContext(userId, userId),
            GlobalRoles: globalRoles ?? [],
            Campaigns: campaignId is not null && campaignRole is not null
                ? [new ToolHostCampaignContext(campaignId.Value, "Edition Metadata Campaign", campaignRole)]
                : []);

    private static Import5eToolsDocumentRequest SourceRequest(
        string packageKey,
        string entityName,
        string sourceCode,
        string? gameEdition,
        bool isPublic) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: $"Package {packageKey}",
            Provider: "integration-test",
            License: "test-only",
            IsPublic: isPublic,
            WorkKey: $"work-{packageKey}",
            WorkDisplayName: $"Work {packageKey}",
            EditionKey: gameEdition ?? "custom-edition",
            EditionDisplayName: gameEdition ?? "Custom Edition",
            Json: $$"""
                {
                  "skill": [
                    {
                      "name": "{{entityName}}",
                      "source": "{{sourceCode}}",
                      "ability": "int"
                    }
                  ]
                }
                """,
            GameEdition: gameEdition,
            ReleaseKind: "published",
            PublicationDate: new DateOnly(2024, 1, 1));

    private static async Task CleanupAsync(
        WebApplicationFactory<Program> factory,
        IReadOnlyCollection<Guid> packageIds)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

        await db.CampaignRulesetRevisionEntries.ExecuteDeleteAsync();
        await db.CampaignRulesetRevisions.ExecuteDeleteAsync();
        await db.CampaignRuleDecisions.ExecuteDeleteAsync();
        await db.CampaignRulesetSelections.ExecuteDeleteAsync();
        await db.RulesetRevisionEntries.ExecuteDeleteAsync();
        await db.RulesetRevisions.ExecuteDeleteAsync();
        await db.GlobalRuleDecisions.ExecuteDeleteAsync();
        await db.RuleConceptSourceBindings.ExecuteDeleteAsync();
        await db.RuleConcepts.ExecuteDeleteAsync();

        if (packageIds.Count > 0)
        {
            var packages = await db.SourcePackages
                .Where(value => packageIds.Contains(value.Id))
                .ToArrayAsync();
            db.SourcePackages.RemoveRange(packages);
            await db.SaveChangesAsync();
        }
    }

    private sealed record EditionFixture(
        string Key,
        string SourceCode,
        string? GameEdition,
        bool IsPublic);

    private sealed class FakeToolHostAuthenticationClient(
        IReadOnlyDictionary<string, ToolHostAuthenticationContext> contexts)
        : IToolHostAuthenticationClient
    {
        public Task<ToolHostAuthenticationContext?> RedeemAsync(
            string ticket,
            string introspectionPath,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(introspectionPath, IntrospectionPath, StringComparison.Ordinal))
            {
                return Task.FromResult<ToolHostAuthenticationContext?>(null);
            }

            contexts.TryGetValue(ticket, out var context);
            return Task.FromResult(context);
        }
    }
}
