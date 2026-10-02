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
public sealed class CharacterAdvancementEligibilityEndpointIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task EligibilityEndpointsPreserveGlobalAndCampaignScopes()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore")))
        {
            return;
        }

        var campaignId = Guid.NewGuid();
        var eligibility = new RecordingEligibilityService();
        var authenticationClient = new FakeToolHostAuthenticationClient(
            new Dictionary<string, ToolHostAuthenticationContext>
            {
                ["player-ticket"] = Context("player", campaignId, "Player"),
                ["outsider-ticket"] = Context("outsider", null, null)
            });

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICharacterAdvancementEligibilityService>();
                services.AddSingleton<ICharacterAdvancementEligibilityService>(eligibility);
                services.RemoveAll<IToolHostAuthenticationClient>();
                services.AddSingleton<IToolHostAuthenticationClient>(authenticationClient);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var request = new CharacterAdvancementEligibilityRequest(
            "subclass.contract-fixture",
            new CharacterRulesProjectionRequest(
                Advancements:
                [
                    new CharacterAdvancementFactInput(
                        "class.contract-fixture",
                        3,
                        "class-occurrence")
                ]),
            ParentAdvancementOccurrenceKey: "class-occurrence");

        using (var globalResponse = await client.PostAsJsonAsync(
                   "/api/rules/character-advancement/eligibility",
                   request))
        {
            Assert.Equal(HttpStatusCode.OK, globalResponse.StatusCode);
            Assert.Equal("no-store", globalResponse.Headers.CacheControl?.ToString());
            var view = await globalResponse.Content
                .ReadFromJsonAsync<CharacterAdvancementEligibilityView>();
            Assert.NotNull(view);
            Assert.Equal("global", view.Scope);
            Assert.True(view.Eligible);
        }

        Assert.NotNull(eligibility.LastGlobalRequest);
        Assert.Equal("subclass.contract-fixture", eligibility.LastGlobalRequest!.CandidateConceptKey);
        Assert.Equal("class-occurrence", eligibility.LastGlobalRequest.ParentAdvancementOccurrenceKey);
        Assert.Null(eligibility.LastGlobalUserId);

        using (var anonymous = await client.PostAsJsonAsync(
                   $"/api/campaigns/{campaignId}/rules/character-advancement/eligibility",
                   request))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        using (var outsiderRequest = HostedRequest(
                   $"/api/campaigns/{campaignId}/rules/character-advancement/eligibility",
                   "outsider-ticket",
                   request))
        using (var outsider = await client.SendAsync(outsiderRequest))
        {
            Assert.Equal(HttpStatusCode.NotFound, outsider.StatusCode);
        }

        using (var playerRequest = HostedRequest(
                   $"/api/campaigns/{campaignId}/rules/character-advancement/eligibility",
                   "player-ticket",
                   request))
        using (var player = await client.SendAsync(playerRequest))
        {
            Assert.Equal(HttpStatusCode.OK, player.StatusCode);
            Assert.Equal("no-store", player.Headers.CacheControl?.ToString());
            var view = await player.Content
                .ReadFromJsonAsync<CharacterAdvancementEligibilityView>();
            Assert.NotNull(view);
            Assert.Equal("campaign", view.Scope);
            Assert.Equal(campaignId, view.CampaignId);
        }

        Assert.Equal(campaignId, eligibility.LastCampaignId);
        Assert.Equal("player", eligibility.LastCampaignUserId);
        Assert.NotNull(eligibility.LastCampaignRequest);
    }

    private static HttpRequestMessage HostedRequest(
        string path,
        string ticket,
        CharacterAdvancementEligibilityRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(ToolHostAuthenticationHeaders.Ticket, ticket);
        message.Headers.Add(
            ToolHostAuthenticationHeaders.IntrospectionPath,
            IntrospectionPath);
        return message;
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
                ? [new ToolHostCampaignContext(campaignId.Value, "Eligibility Campaign", campaignRole)]
                : []);

    private sealed class RecordingEligibilityService : ICharacterAdvancementEligibilityService
    {
        public CharacterAdvancementEligibilityRequest? LastGlobalRequest { get; private set; }
        public string? LastGlobalUserId { get; private set; }
        public Guid? LastCampaignId { get; private set; }
        public CharacterAdvancementEligibilityRequest? LastCampaignRequest { get; private set; }
        public string? LastCampaignUserId { get; private set; }

        public Task<CharacterAdvancementEligibilityView?> EvaluateGlobalAsync(
            CharacterAdvancementEligibilityRequest request,
            string? userId,
            CancellationToken cancellationToken = default)
        {
            LastGlobalRequest = request;
            LastGlobalUserId = userId;
            return Task.FromResult<CharacterAdvancementEligibilityView?>(View("global", null));
        }

        public Task<CharacterAdvancementEligibilityView?> EvaluateCampaignAsync(
            Guid campaignId,
            CharacterAdvancementEligibilityRequest request,
            string userId,
            CancellationToken cancellationToken = default)
        {
            LastCampaignId = campaignId;
            LastCampaignRequest = request;
            LastCampaignUserId = userId;
            return Task.FromResult<CharacterAdvancementEligibilityView?>(View("campaign", campaignId));
        }

        private static CharacterAdvancementEligibilityView View(string scope, Guid? campaignId) =>
            new(
                scope,
                campaignId,
                "subclass.contract-fixture",
                "Contract Fixture Path",
                "subclass",
                CharacterAdvancementEligibilityStates.Eligible,
                true,
                null,
                null,
                []);
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
