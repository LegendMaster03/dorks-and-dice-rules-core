using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
public sealed class RulePreviewEndpointIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task PreviewEndpointsEnforceChangeAuthorityAndDoNotPersistCandidates()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var campaignId = Guid.NewGuid();
        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["rules-lawyer-ticket"] = Context(
                "rules-lawyer",
                "Rules Lawyer",
                "dorks-and-dice",
                [RulesAuthority.RulesLawyerRole]),
            ["ordinary-ticket"] = Context(
                "ordinary-user",
                "Ordinary User",
                "dorks-and-dice",
                []),
            ["dm-ticket"] = Context(
                "campaign-dm",
                "Campaign DM",
                "dorks-and-dice",
                [],
                campaignId,
                RulesAuthority.CampaignDmRole),
            ["player-ticket"] = Context(
                "campaign-player",
                "Campaign Player",
                "dorks-and-dice",
                [],
                campaignId,
                "Player"),
            ["outsider-ticket"] = Context(
                "outsider",
                "Outsider",
                "dorks-and-dice",
                [])
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var packageKey = $"preview-http-{Guid.NewGuid():N}";
        var conceptKey = $"skill.arcana.preview-http.{Guid.NewGuid():N}";
        Guid packageId = Guid.Empty;

        try
        {
            Guid conceptId;
            Guid sourceRevisionId;
            PublishedRulesetRevisionView globalPublication;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

                var imported = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                    PackageKey: packageKey,
                    PackageDisplayName: "Preview HTTP Package",
                    Provider: "integration-test",
                    License: "test-only",
                    IsPublic: true,
                    WorkKey: "preview-http-work",
                    WorkDisplayName: "Preview HTTP Work",
                    EditionKey: "preview-http-edition",
                    EditionDisplayName: "Preview HTTP Edition",
                    Json: """
                        {
                          "skill": [
                            {
                              "name": "Arcana",
                              "source": "TST",
                              "ability": "int",
                              "proficiencies": ["Arcana"]
                            }
                          ]
                        }
                        """));
                packageId = imported.PackageId;
                var sourceEntityId = imported.Entities.Single().EntityId;
                sourceRevisionId = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == sourceEntityId)
                    .Select(value => value.Id)
                    .SingleAsync();

                conceptId = (await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(conceptKey, "skill", "Arcana"),
                    "rules-lawyer")).Value.Id;
                await globalRules.BindSourceEntityAsync(
                    conceptId,
                    new BindRuleConceptSourceRequest(sourceEntityId),
                    "rules-lawyer");
            }

            var globalPreviewRequest = new SetGlobalRuleDecisionRequest(
                sourceRevisionId,
                "Preview through the hosted API.",
                StructuredPatch: new RuleStructuredPatchRequest(
                    MergePatch: Parse("""{ "ability": "wis" }"""),
                    ArrayOperations:
                    [
                        new RuleArrayOperationRequest(
                            RuleArrayOperationKinds.Append,
                            "/proficiencies",
                            Value: Parse("\"Religion\""))
                    ]));

            using (var anonymousRequest = new HttpRequestMessage(
                       HttpMethod.Post,
                       $"/api/global/rules/concepts/{conceptId}/preview")
                   {
                       Content = JsonContent.Create(globalPreviewRequest)
                   })
            using (var response = await client.SendAsync(anonymousRequest))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            using (var ordinaryRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/concepts/{conceptId}/preview",
                       "ordinary-ticket",
                       globalPreviewRequest))
            using (var response = await client.SendAsync(ordinaryRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            using (var rulesLawyerRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/concepts/{conceptId}/preview",
                       "rules-lawyer-ticket",
                       globalPreviewRequest))
            using (var response = await client.SendAsync(rulesLawyerRequest))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
                var preview = (await response.Content.ReadFromJsonAsync<RulePatchPreviewView>())!;
                Assert.Equal("global", preview.Scope);
                Assert.Equal("wis", preview.PreviewDocument.GetProperty("ability").GetString());
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var campaignRules = scope.ServiceProvider.GetRequiredService<ICampaignRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                Assert.Equal(0, await db.GlobalRuleDecisions.CountAsync());

                await globalRules.SetDecisionAsync(
                    conceptId,
                    globalPreviewRequest,
                    "rules-lawyer");
                globalPublication = await globalRules.PublishAsync("rules-lawyer");
                await campaignRules.SelectBaselineAsync(
                    campaignId,
                    new SelectCampaignRulesetBaselineRequest(globalPublication.Id),
                    "campaign-dm");
            }

            var campaignPreviewRequest = new SetCampaignRuleDecisionRequest(
                CampaignRuleDecisionKinds.JsonMergePatch,
                SourceEntityRevisionId: null,
                Note: "Preview campaign override through the hosted API.",
                MergePatch: Parse("""{ "campaignOnly": true }"""));
            var campaignPreviewPath =
                $"/api/campaigns/{campaignId}/rules/concepts/{conceptId}/preview";

            using (var playerRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       campaignPreviewPath,
                       "player-ticket",
                       campaignPreviewRequest))
            using (var response = await client.SendAsync(playerRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            using (var outsiderRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       campaignPreviewPath,
                       "outsider-ticket",
                       campaignPreviewRequest))
            using (var response = await client.SendAsync(outsiderRequest))
            {
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }

            using (var dmRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       campaignPreviewPath,
                       "dm-ticket",
                       campaignPreviewRequest))
            using (var response = await client.SendAsync(dmRequest))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var preview = (await response.Content.ReadFromJsonAsync<RulePatchPreviewView>())!;
                Assert.Equal("campaign", preview.Scope);
                Assert.True(preview.PreviewDocument.GetProperty("campaignOnly").GetBoolean());
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                Assert.Equal(0, await db.CampaignRuleDecisions.CountAsync());
            }
        }
        finally
        {
            await CleanupAsync(factory, packageId);
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

    private static HttpRequestMessage HostedJsonRequest<T>(
        HttpMethod method,
        string path,
        string ticket,
        T body)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add(ToolHostAuthenticationHeaders.Ticket, ticket);
        request.Headers.Add(ToolHostAuthenticationHeaders.IntrospectionPath, IntrospectionPath);
        return request;
    }

    private static ToolHostAuthenticationContext Context(
        string userId,
        string displayName,
        string siteMode,
        IReadOnlyList<string> globalRoles,
        Guid? campaignId = null,
        string? campaignRole = null) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: siteMode,
            User: new ToolHostUserContext(userId, displayName),
            GlobalRoles: globalRoles,
            Campaigns: campaignId is not null && campaignRole is not null
                ? [new ToolHostCampaignContext(campaignId.Value, "Preview Campaign", campaignRole)]
                : []);

    private static async Task CleanupAsync(
        WebApplicationFactory<Program> factory,
        Guid packageId)
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
        db.ChangeTracker.Clear();

        if (packageId != Guid.Empty)
        {
            await db.SourcePackages
                .Where(value => value.Id == packageId)
                .ExecuteDeleteAsync();
        }
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
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
