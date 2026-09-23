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
public sealed class CharacterProjectionEndpointIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task ProjectionEndpointsPreserveGlobalAndCampaignConsumerContracts()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore")))
        {
            return;
        }

        var campaignId = Guid.NewGuid();
        var projection = new RecordingCharacterRulesProjectionService();
        var authenticationClient = new FakeToolHostAuthenticationClient(
            new Dictionary<string, ToolHostAuthenticationContext>
            {
                ["player-ticket"] = Context("player", campaignId, "Player"),
                ["outsider-ticket"] = Context("outsider", campaignId: null, campaignRole: null)
            });

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICharacterRulesProjectionService>();
                services.AddSingleton<ICharacterRulesProjectionService>(projection);
                services.RemoveAll<IToolHostAuthenticationClient>();
                services.AddSingleton<IToolHostAuthenticationClient>(authenticationClient);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var globalRequest = new CharacterRulesProjectionRequest(
            BaseAbilityScores: new Dictionary<string, int>
            {
                ["strength"] = 14
            },
            SelectedConcepts:
            [
                new CharacterSelectedConceptInput("class.contract-fixture")
            ],
            RequestedMechanicKeys:
            [
                "defense.ac.total"
            ]);

        using (var response = await client.PostAsJsonAsync(
                   "/api/rules/character-mechanics/resolve",
                   globalRequest))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

            var view = await response.Content.ReadFromJsonAsync<CharacterRulesProjectionView>();
            Assert.NotNull(view);
            Assert.Equal("global", view.Scope);
            Assert.Null(view.CampaignId);
        }

        Assert.NotNull(projection.LastGlobalRequest);
        Assert.Equal(14, projection.LastGlobalRequest!.BaseAbilityScores!["strength"]);
        Assert.Equal(
            "class.contract-fixture",
            Assert.Single(projection.LastGlobalRequest.SelectedConcepts!).ConceptKey);
        Assert.Equal(
            "defense.ac.total",
            Assert.Single(projection.LastGlobalRequest.RequestedMechanicKeys!));
        Assert.Null(projection.LastGlobalUserId);

        using (var anonymousResponse = await client.PostAsJsonAsync(
                   $"/api/campaigns/{campaignId}/rules/character-mechanics/resolve",
                   new CharacterRulesProjectionRequest()))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        }

        using (var outsiderRequest = HostedRequest(
                   HttpMethod.Post,
                   $"/api/campaigns/{campaignId}/rules/character-mechanics/resolve",
                   "outsider-ticket"))
        {
            outsiderRequest.Content = JsonContent.Create(new CharacterRulesProjectionRequest());
            using var outsiderResponse = await client.SendAsync(outsiderRequest);
            Assert.Equal(HttpStatusCode.NotFound, outsiderResponse.StatusCode);
        }

        var campaignRequest = new CharacterRulesProjectionRequest(
            CapabilityKeys:
            [
                "save.fortitude"
            ],
            IntegerFacts: new Dictionary<string, int>
            {
                ["combat.grapple.size-modifier"] = 1
            });

        using (var playerRequest = HostedRequest(
                   HttpMethod.Post,
                   $"/api/campaigns/{campaignId}/rules/character-mechanics/resolve",
                   "player-ticket"))
        {
            playerRequest.Content = JsonContent.Create(campaignRequest);
            using var playerResponse = await client.SendAsync(playerRequest);

            Assert.Equal(HttpStatusCode.OK, playerResponse.StatusCode);
            Assert.Equal("no-store", playerResponse.Headers.CacheControl?.ToString());

            var view = await playerResponse.Content.ReadFromJsonAsync<CharacterRulesProjectionView>();
            Assert.NotNull(view);
            Assert.Equal("campaign", view.Scope);
            Assert.Equal(campaignId, view.CampaignId);
        }

        Assert.Equal(campaignId, projection.LastCampaignId);
        Assert.Equal("player", projection.LastCampaignUserId);
        Assert.NotNull(projection.LastCampaignRequest);
        Assert.Equal(
            "save.fortitude",
            Assert.Single(projection.LastCampaignRequest!.CapabilityKeys!));
        Assert.Equal(
            1,
            projection.LastCampaignRequest.IntegerFacts!["combat.grapple.size-modifier"]);
    }

    private static CharacterRulesProjectionView Projection(string scope, Guid? campaignId) =>
        new(
            scope,
            campaignId,
            RevisionNumber: 7,
            PublishedAt: DateTimeOffset.Parse("2026-09-23T00:00:00Z"),
            Mechanics: [],
            Capabilities: [],
            Grants: [],
            Effects: [],
            Movement: [],
            Qualifications: [],
            Actions: [],
            Features: [],
            Resources: [],
            Spellcasting: [],
            Procedures: [],
            Choices: [],
            Prerequisites: [],
            Conflicts: [],
            Equipment: []);

    private static HttpRequestMessage HostedRequest(
        HttpMethod method,
        string path,
        string ticket)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(ToolHostAuthenticationHeaders.Ticket, ticket);
        request.Headers.Add(
            ToolHostAuthenticationHeaders.IntrospectionPath,
            IntrospectionPath);
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
                ? [new ToolHostCampaignContext(
                    campaignId.Value,
                    "Projection Contract Campaign",
                    campaignRole)]
                : []);

    private sealed class RecordingCharacterRulesProjectionService
        : ICharacterRulesProjectionService
    {
        public CharacterRulesProjectionRequest? LastGlobalRequest { get; private set; }
        public string? LastGlobalUserId { get; private set; }
        public Guid? LastCampaignId { get; private set; }
        public CharacterRulesProjectionRequest? LastCampaignRequest { get; private set; }
        public string? LastCampaignUserId { get; private set; }

        public Task<CharacterRulesProjectionView> ResolveGlobalAsync(
            CharacterRulesProjectionRequest request,
            string? userId,
            CancellationToken cancellationToken = default)
        {
            LastGlobalRequest = request;
            LastGlobalUserId = userId;
            return Task.FromResult(Projection("global", campaignId: null));
        }

        public Task<CharacterRulesProjectionView> ResolveCampaignAsync(
            Guid campaignId,
            CharacterRulesProjectionRequest request,
            string userId,
            CancellationToken cancellationToken = default)
        {
            LastCampaignId = campaignId;
            LastCampaignRequest = request;
            LastCampaignUserId = userId;
            return Task.FromResult(Projection("campaign", campaignId));
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
            if (!string.Equals(
                    introspectionPath,
                    IntrospectionPath,
                    StringComparison.Ordinal))
            {
                return Task.FromResult<ToolHostAuthenticationContext?>(null);
            }

            contexts.TryGetValue(ticket, out var context);
            return Task.FromResult(context);
        }
    }
}
