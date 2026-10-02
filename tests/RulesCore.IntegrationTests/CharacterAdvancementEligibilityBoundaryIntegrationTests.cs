using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RulesCore.Application.Rules;
using RulesCore.Web;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CharacterAdvancementEligibilityBoundaryIntegrationTests
{
    [Fact]
    public async Task GlobalEligibilityEndpointRemainsAvailableOnPublicOnlyIngress()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore")))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [RulesCoreApiBoundary.ApiSurfaceConfigurationKey] =
                        RulesCoreApiSurfaceMode.PublicOnly.ToString()
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICharacterAdvancementEligibilityService>();
                services.AddSingleton<ICharacterAdvancementEligibilityService>(
                    new AlwaysEligibleService());
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.PostAsJsonAsync(
            "/api/rules/character-advancement/eligibility",
            new CharacterAdvancementEligibilityRequest(
                "subclass.public-contract",
                new CharacterRulesProjectionRequest()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class AlwaysEligibleService : ICharacterAdvancementEligibilityService
    {
        public Task<CharacterAdvancementEligibilityView?> EvaluateGlobalAsync(
            CharacterAdvancementEligibilityRequest request,
            string? userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CharacterAdvancementEligibilityView?>(new(
                "global",
                null,
                request.CandidateConceptKey,
                "Public Contract",
                "subclass",
                CharacterAdvancementEligibilityStates.Eligible,
                true,
                null,
                null,
                []));

        public Task<CharacterAdvancementEligibilityView?> EvaluateCampaignAsync(
            Guid campaignId,
            CharacterAdvancementEligibilityRequest request,
            string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CharacterAdvancementEligibilityView?>(new(
                "campaign",
                campaignId,
                request.CandidateConceptKey,
                "Public Contract",
                "subclass",
                CharacterAdvancementEligibilityStates.Eligible,
                true,
                null,
                null,
                []));
    }
}
