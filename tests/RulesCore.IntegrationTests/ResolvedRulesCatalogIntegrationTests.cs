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
public sealed class ResolvedRulesCatalogIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task CatalogsHideInaccessibleRulesAndAllowCampaignPlayersToBrowsePublishedRules()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var campaignId = Guid.NewGuid();
        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["granted-ticket"] = Context("granted-reader", campaignId: null, campaignRole: null),
            ["ungranted-ticket"] = Context("ungranted-reader", campaignId: null, campaignRole: null),
            ["player-granted-ticket"] = Context("player-granted", campaignId, "Player"),
            ["player-ungranted-ticket"] = Context("player-ungranted", campaignId, "Player"),
            ["outsider-ticket"] = Context("outsider", campaignId: null, campaignRole: null)
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var token = Guid.NewGuid().ToString("N")[..10];
        var publicPackageKey = $"browser-public-{token}";
        var privatePackageKey = $"browser-private-{token}";
        var publicConceptKey = $"skill.browser-public-{token}";
        var privateConceptKey = $"skill.browser-private-{token}";
        Guid publicPackageId = Guid.Empty;
        Guid privatePackageId = Guid.Empty;

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

                var publicImport = await importer.Import5eToolsDocumentAsync(SourceRequest(
                    publicPackageKey,
                    $"Browser Public {token}",
                    "PUB",
                    isPublic: true));
                publicPackageId = publicImport.PackageId;
                var publicEntityId = publicImport.Entities.Single().EntityId;
                var publicRevisionId = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == publicEntityId)
                    .Select(value => value.Id)
                    .SingleAsync();

                var privateImport = await importer.Import5eToolsDocumentAsync(SourceRequest(
                    privatePackageKey,
                    $"Browser Private {token}",
                    "PRV",
                    isPublic: false));
                privatePackageId = privateImport.PackageId;
                var privateEntityId = privateImport.Entities.Single().EntityId;
                var privateRevisionId = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == privateEntityId)
                    .Select(value => value.Id)
                    .SingleAsync();

                await grants.GrantAsync("granted-reader", privatePackageId);
                await grants.GrantAsync("player-granted", privatePackageId);

                var publicConcept = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(publicConceptKey, "skill", $"Browser Public {token}"),
                    "rules-lawyer");
                await globalRules.BindSourceEntityAsync(
                    publicConcept.Value.Id,
                    new BindRuleConceptSourceRequest(publicEntityId),
                    "rules-lawyer");
                await globalRules.SetDecisionAsync(
                    publicConcept.Value.Id,
                    new SetGlobalRuleDecisionRequest(publicRevisionId, "Public browser rule."),
                    "rules-lawyer");

                var privateConcept = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(privateConceptKey, "skill", $"Browser Private {token}"),
                    "rules-lawyer");
                await globalRules.BindSourceEntityAsync(
                    privateConcept.Value.Id,
                    new BindRuleConceptSourceRequest(privateEntityId),
                    "rules-lawyer");
                await globalRules.SetDecisionAsync(
                    privateConcept.Value.Id,
                    new SetGlobalRuleDecisionRequest(privateRevisionId, "Private browser rule."),
                    "rules-lawyer");

                globalRevision = await globalRules.PublishAsync("rules-lawyer");
                await campaignRules.SelectBaselineAsync(
                    campaignId,
                    new SelectCampaignRulesetBaselineRequest(globalRevision.Id),
                    "campaign-dm");
                await campaignRules.PublishAsync(campaignId, "campaign-dm");
            }

            using (var directResponse = await client.GetAsync($"/api/rules?q={token}"))
            {
                Assert.Equal(HttpStatusCode.OK, directResponse.StatusCode);
                var raw = await directResponse.Content.ReadAsStringAsync();
                Assert.DoesNotContain("\"document\"", raw, StringComparison.OrdinalIgnoreCase);
                var catalog = await directResponse.Content.ReadFromJsonAsync<ResolvedRulesCatalogView>();
                Assert.NotNull(catalog);
                Assert.Equal(globalRevision.RevisionNumber, catalog.RevisionNumber);
                var rule = Assert.Single(catalog.Rules);
                Assert.Equal(publicConceptKey, rule.ConceptKey);
                Assert.Equal(publicPackageKey, rule.PackageKey);
            }

            using (var grantedRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/rules?q={token}",
                       "granted-ticket"))
            using (var grantedResponse = await client.SendAsync(grantedRequest))
            {
                Assert.Equal(HttpStatusCode.OK, grantedResponse.StatusCode);
                Assert.Equal("no-store", grantedResponse.Headers.CacheControl?.ToString());
                var catalog = (await grantedResponse.Content
                    .ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                Assert.Equal(2, catalog.Rules.Count);
                Assert.Contains(catalog.Rules, value => value.ConceptKey == publicConceptKey);
                Assert.Contains(catalog.Rules, value => value.ConceptKey == privateConceptKey);
            }

            using (var firstPageRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/rules?q={token}&limit=1&offset=0",
                       "granted-ticket"))
            using (var firstPageResponse = await client.SendAsync(firstPageRequest))
            using (var secondPageRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/rules?q={token}&limit=1&offset=1",
                       "granted-ticket"))
            using (var secondPageResponse = await client.SendAsync(secondPageRequest))
            {
                Assert.Equal(HttpStatusCode.OK, firstPageResponse.StatusCode);
                Assert.Equal(HttpStatusCode.OK, secondPageResponse.StatusCode);
                var firstPage = (await firstPageResponse.Content.ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                var secondPage = (await secondPageResponse.Content.ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                var first = Assert.Single(firstPage.Rules);
                var second = Assert.Single(secondPage.Rules);
                Assert.NotEqual(first.RuleConceptId, second.RuleConceptId);
                Assert.Equal(new[] { publicConceptKey, privateConceptKey }.OrderBy(value => value), new[] { first.ConceptKey, second.ConceptKey }.OrderBy(value => value));
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
                var rule = Assert.Single(catalog.Rules);
                Assert.Equal(publicConceptKey, rule.ConceptKey);
            }

            using (var anonymousCampaign = await client.GetAsync(
                       $"/api/campaigns/{campaignId}/rules?q={token}"))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymousCampaign.StatusCode);
            }

            using (var outsiderRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules?q={token}",
                       "outsider-ticket"))
            using (var outsiderResponse = await client.SendAsync(outsiderRequest))
            {
                Assert.Equal(HttpStatusCode.NotFound, outsiderResponse.StatusCode);
            }

            using (var playerGrantedRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules?q={token}",
                       "player-granted-ticket"))
            using (var playerGrantedResponse = await client.SendAsync(playerGrantedRequest))
            {
                Assert.Equal(HttpStatusCode.OK, playerGrantedResponse.StatusCode);
                var raw = await playerGrantedResponse.Content.ReadAsStringAsync();
                Assert.DoesNotContain("\"document\"", raw, StringComparison.OrdinalIgnoreCase);
                var catalog = await playerGrantedResponse.Content
                    .ReadFromJsonAsync<ResolvedRulesCatalogView>();
                Assert.NotNull(catalog);
                Assert.Equal("campaign", catalog.Scope);
                Assert.Equal(campaignId, catalog.CampaignId);
                Assert.Equal(2, catalog.Rules.Count);
                Assert.All(catalog.Rules, value => Assert.False(value.HasCampaignOverride));
            }

            using (var campaignFirstPageRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules?q={token}&limit=1&offset=0",
                       "player-granted-ticket"))
            using (var campaignFirstPageResponse = await client.SendAsync(campaignFirstPageRequest))
            using (var campaignSecondPageRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules?q={token}&limit=1&offset=1",
                       "player-granted-ticket"))
            using (var campaignSecondPageResponse = await client.SendAsync(campaignSecondPageRequest))
            {
                Assert.Equal(HttpStatusCode.OK, campaignFirstPageResponse.StatusCode);
                Assert.Equal(HttpStatusCode.OK, campaignSecondPageResponse.StatusCode);
                var firstPage = (await campaignFirstPageResponse.Content.ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                var secondPage = (await campaignSecondPageResponse.Content.ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                Assert.NotEqual(Assert.Single(firstPage.Rules).RuleConceptId, Assert.Single(secondPage.Rules).RuleConceptId);
            }

            using (var playerUngrantedRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules?q={token}",
                       "player-ungranted-ticket"))
            using (var playerUngrantedResponse = await client.SendAsync(playerUngrantedRequest))
            {
                Assert.Equal(HttpStatusCode.OK, playerUngrantedResponse.StatusCode);
                var catalog = (await playerUngrantedResponse.Content
                    .ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                var rule = Assert.Single(catalog.Rules);
                Assert.Equal(publicConceptKey, rule.ConceptKey);
            }

            using (var filteredRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/rules?entityType=spell&q={token}",
                       "granted-ticket"))
            using (var filteredResponse = await client.SendAsync(filteredRequest))
            {
                Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
                var catalog = (await filteredResponse.Content
                    .ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                Assert.Empty(catalog.Rules);
            }
        }
        finally
        {
            await CleanupAsync(factory, publicPackageId, privatePackageId);
        }
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
        string? campaignRole) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: "dorks-and-dice",
            User: new ToolHostUserContext(userId, userId),
            GlobalRoles: [],
            Campaigns: campaignId is not null && campaignRole is not null
                ? [new ToolHostCampaignContext(campaignId.Value, "Browser Campaign", campaignRole)]
                : []);

    private static Import5eToolsDocumentRequest SourceRequest(
        string packageKey,
        string entityName,
        string sourceCode,
        bool isPublic) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: $"Package {packageKey}",
            Provider: "integration-test",
            License: "test-only",
            IsPublic: isPublic,
            WorkKey: "browser-work",
            WorkDisplayName: "Browser Work",
            EditionKey: "browser-edition",
            EditionDisplayName: "Browser Edition",
            Json: $$"""
                {
                  "skill": [
                    {
                      "name": "{{entityName}}",
                      "source": "{{sourceCode}}",
                      "ability": "int",
                      "secretMarker": "source-document-only"
                    }
                  ]
                }
                """);

    private static async Task CleanupAsync(
        WebApplicationFactory<Program> factory,
        Guid publicPackageId,
        Guid privatePackageId)
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

        var packageIds = new[] { publicPackageId, privatePackageId }
            .Where(value => value != Guid.Empty)
            .ToArray();
        if (packageIds.Length > 0)
        {
            var packages = await db.SourcePackages
                .Where(value => packageIds.Contains(value.Id))
                .ToArrayAsync();
            db.SourcePackages.RemoveRange(packages);
            await db.SaveChangesAsync();
        }
    }

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
