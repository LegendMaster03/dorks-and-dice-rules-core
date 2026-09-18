using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CharacterMechanicsConsumerIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task ConsumerEndpointsExposeGenericMechanicsAndPreserveCampaignReadAuthority()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var campaignId = Guid.NewGuid();
        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["player-ticket"] = Context("player", campaignId, "Player"),
            ["outsider-ticket"] = Context("outsider", campaignId: null, campaignRole: null)
        });

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IToolHostAuthenticationClient>();
                services.AddSingleton<IToolHostAuthenticationClient>(authenticationClient);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using (var globalResponse = await client.GetAsync("/api/rules/mechanics?includeUnavailable=true"))
        {
            Assert.Equal(HttpStatusCode.OK, globalResponse.StatusCode);
            var catalog = await globalResponse.Content.ReadFromJsonAsync<CharacterMechanicsCatalogView>();
            Assert.NotNull(catalog);
            Assert.Contains(catalog.Mechanics, value => value.MechanicKey == "check.competency");
            Assert.Contains(catalog.Mechanics, value => value.MechanicKey == "save.fortitude");
        }

        using (var evaluationResponse = await client.PostAsJsonAsync(
                   "/api/rules/mechanics/check.competency/evaluate",
                   new CharacterMechanicEvaluationRequest(
                       IntegerInputs: new Dictionary<string, int>
                       {
                           ["d20Roll"] = 12,
                           ["abilityModifier"] = 3,
                           ["competencyModifier"] = 2,
                           ["targetDc"] = 17
                       },
                       StringInputs: new Dictionary<string, string>
                       {
                           ["abilityKey"] = "intelligence",
                           ["competencyKey"] = "skill.arcana"
                       })))
        {
            Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
            var evaluation = await evaluationResponse.Content.ReadFromJsonAsync<CharacterMechanicEvaluationView>();
            Assert.NotNull(evaluation);
            Assert.Equal(17, evaluation.Value);
            Assert.True(evaluation.MeetsTarget);
        }

        using (var anonymousCampaign = await client.GetAsync(
                   $"/api/campaigns/{campaignId}/rules/mechanics"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousCampaign.StatusCode);
        }

        using (var outsiderRequest = HostedRequest(
                   HttpMethod.Get,
                   $"/api/campaigns/{campaignId}/rules/mechanics",
                   "outsider-ticket"))
        using (var outsiderResponse = await client.SendAsync(outsiderRequest))
        {
            Assert.Equal(HttpStatusCode.NotFound, outsiderResponse.StatusCode);
        }

        using (var playerRequest = HostedRequest(
                   HttpMethod.Get,
                   $"/api/campaigns/{campaignId}/rules/mechanics?includeUnavailable=true",
                   "player-ticket"))
        using (var playerResponse = await client.SendAsync(playerRequest))
        {
            Assert.Equal(HttpStatusCode.OK, playerResponse.StatusCode);
            Assert.Equal("no-store", playerResponse.Headers.CacheControl?.ToString());
            var catalog = await playerResponse.Content.ReadFromJsonAsync<CharacterMechanicsCatalogView>();
            Assert.NotNull(catalog);
            Assert.Equal("campaign", catalog.Scope);
            Assert.Equal(campaignId, catalog.CampaignId);
            Assert.Contains(catalog.Mechanics, value => value.MechanicKey == "check.competency");
        }
    }

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
            SiteMode: RulesAuthority.DorksAndDiceMode,
            User: new ToolHostUserContext(userId, userId),
            GlobalRoles: [],
            Campaigns: campaignId is not null && campaignRole is not null
                ? [new ToolHostCampaignContext(campaignId.Value, "Mechanics Campaign", campaignRole)]
                : []);

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
