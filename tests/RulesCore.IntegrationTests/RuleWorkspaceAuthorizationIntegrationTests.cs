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
public sealed class RuleWorkspaceAuthorizationIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task WorkspaceScopesExposeIndependentGlobalAndCampaignAuthority()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore"))) return;

        var ownedCampaignId = Guid.NewGuid();
        var memberCampaignId = Guid.NewGuid();
        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["mixed-ticket"] = new(
                1,
                "rules-core",
                RulesAuthority.DorksAndDiceMode,
                new ToolHostUserContext("mixed-user", "Mixed User"),
                [RulesAuthority.RulesLawyerRole],
                [
                    new ToolHostCampaignContext(ownedCampaignId, "Owned Campaign", RulesAuthority.CampaignDmRole),
                    new ToolHostCampaignContext(memberCampaignId, "Member Campaign", "Player")
                ])
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = HostedRequest(HttpMethod.Get, "/api/workspace/scopes", "mixed-ticket");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var view = await response.Content.ReadFromJsonAsync<RuleWorkspaceScopesView>();
        Assert.NotNull(view);
        Assert.Contains(view.Scopes, scope => scope.Kind == "global" && scope.CanAdjudicate);
        Assert.Contains(view.Scopes, scope => scope.CampaignId == ownedCampaignId && scope.CanAdjudicate);
        Assert.Contains(view.Scopes, scope => scope.CampaignId == memberCampaignId && !scope.CanAdjudicate);
    }

    [Fact]
    public async Task ComparisonRechecksRequestedScopeServerSide()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore"))) return;

        var ownedCampaignId = Guid.NewGuid();
        var memberCampaignId = Guid.NewGuid();
        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["campaign-ticket"] = new(
                1,
                "rules-core",
                RulesAuthority.DorksAndDiceMode,
                new ToolHostUserContext("campaign-user", "Campaign User"),
                [],
                [
                    new ToolHostCampaignContext(ownedCampaignId, "Owned Campaign", RulesAuthority.CampaignDmRole),
                    new ToolHostCampaignContext(memberCampaignId, "Member Campaign", "Player")
                ])
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var payload = new RuleSemanticComparisonRequest(
            new RuleAdjudicationScopeRequest(RuleAdjudicationScopeKinds.Campaign, memberCampaignId),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
        using var request = HostedRequest(HttpMethod.Post, "/api/workspace/comparison", "campaign-ticket");
        request.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SourceComparisonIsReadableWithoutAdjudicationAuthority()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore"))) return;

        var authenticationClient = new FakeToolHostAuthenticationClient(
            new Dictionary<string, ToolHostAuthenticationContext>());
        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var payload = new RuleSourceComparisonRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
        using var response = await client.PostAsJsonAsync("/api/rules/comparison", payload);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(IToolHostAuthenticationClient authenticationClient) =>
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
