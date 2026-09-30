using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RulesCore.Application.Hosting;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class ApiSurfaceBoundaryIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task PrivateApiRequiresRulesWikiDelegationWhilePublicApiRemainsAvailable()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var authenticationClient = new FakeToolHostAuthenticationClient(
            new Dictionary<string, ToolHostAuthenticationContext>
            {
                ["direct-ticket"] = Context("direct-user"),
                ["other-tool-ticket"] = Context("other-tool-user", "character-sheet"),
                ["rules-wiki-ticket"] = Context("wiki-user", "rules-wiki")
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

        using (var anonymousInternal = await client.GetAsync("/api/wiki/references?limit=1"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousInternal.StatusCode);
        }

        using (var directRequest = HostedRequest("/api/wiki/references?limit=1", "direct-ticket"))
        using (var directResponse = await client.SendAsync(directRequest))
        {
            Assert.Equal(HttpStatusCode.Forbidden, directResponse.StatusCode);
        }

        using (var otherToolRequest = HostedRequest("/api/wiki/references?limit=1", "other-tool-ticket"))
        using (var otherToolResponse = await client.SendAsync(otherToolRequest))
        {
            Assert.Equal(HttpStatusCode.Forbidden, otherToolResponse.StatusCode);
        }

        using (var wikiRequest = HostedRequest("/api/wiki/references?limit=1", "rules-wiki-ticket"))
        using (var wikiResponse = await client.SendAsync(wikiRequest))
        {
            Assert.Equal(HttpStatusCode.OK, wikiResponse.StatusCode);
        }

        using (var publicResponse = await client.GetAsync("/api/rules?limit=1"))
        {
            Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        }
    }

    private static HttpRequestMessage HostedRequest(string path, string ticket)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation(ToolHostAuthenticationHeaders.Ticket, ticket);
        request.Headers.TryAddWithoutValidation(
            ToolHostAuthenticationHeaders.IntrospectionPath,
            IntrospectionPath);
        return request;
    }

    private static ToolHostAuthenticationContext Context(
        string userId,
        string? delegatedFromToolKey = null) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: "dorks-and-dice",
            User: new ToolHostUserContext(userId, userId),
            GlobalRoles: ["Rules Lawyer"],
            Campaigns: [])
        {
            ToolKey = "rules-core",
            ScopedRoles = ["Rules Lawyer"],
            DelegatedFromToolKey = delegatedFromToolKey,
            DelegatedFromToolSlug = delegatedFromToolKey
        };

    private sealed class FakeToolHostAuthenticationClient(
        IReadOnlyDictionary<string, ToolHostAuthenticationContext> contexts)
        : IToolHostAuthenticationClient
    {
        public Task<ToolHostAuthenticationContext?> RedeemAsync(
            string ticket,
            string introspectionPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(contexts.GetValueOrDefault(ticket));
    }
}
