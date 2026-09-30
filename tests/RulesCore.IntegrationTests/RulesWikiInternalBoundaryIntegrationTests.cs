using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RulesCore.Application.Hosting;

namespace RulesCore.IntegrationTests;

public sealed class RulesWikiInternalBoundaryIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task PrivateWikiSurfaceRequiresRulesWikiDelegationWhilePublicConsumerRouteRemainsAvailable()
    {
        var authenticationClient = new FakeAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["wiki"] = Context("wiki-user", delegatedFrom: "rules-wiki"),
            ["character-sheet"] = Context("sheet-user", delegatedFrom: "character-sheet"),
            ["direct-core"] = Context("core-user", delegatedFrom: null)
        });
        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using (var deniedShared = HostedRequest("/internal/wiki/shared/api/rules", "character-sheet"))
        using (var deniedSharedResponse = await client.SendAsync(deniedShared))
        {
            Assert.Equal(HttpStatusCode.NotFound, deniedSharedResponse.StatusCode);
        }

        using (var directShared = HostedRequest("/internal/wiki/shared/api/rules", "direct-core"))
        using (var directSharedResponse = await client.SendAsync(directShared))
        {
            Assert.Equal(HttpStatusCode.NotFound, directSharedResponse.StatusCode);
        }

        using (var allowedShared = HostedRequest("/internal/wiki/shared/api/rules", "wiki"))
        using (var allowedSharedResponse = await client.SendAsync(allowedShared))
        {
            Assert.NotEqual(HttpStatusCode.NotFound, allowedSharedResponse.StatusCode);
        }

        using (var deniedReference = HostedRequest("/api/wiki/references", "character-sheet"))
        using (var deniedReferenceResponse = await client.SendAsync(deniedReference))
        {
            Assert.Equal(HttpStatusCode.NotFound, deniedReferenceResponse.StatusCode);
        }

        using (var allowedReference = HostedRequest("/api/wiki/references", "wiki"))
        using (var allowedReferenceResponse = await client.SendAsync(allowedReference))
        {
            Assert.NotEqual(HttpStatusCode.NotFound, allowedReferenceResponse.StatusCode);
        }

        using (var publicConsumer = HostedRequest("/api/rules", "character-sheet"))
        using (var publicConsumerResponse = await client.SendAsync(publicConsumer))
        {
            Assert.NotEqual(HttpStatusCode.NotFound, publicConsumerResponse.StatusCode);
        }
    }

    [Fact]
    public async Task SharedWikiAdapterRejectsUnallowlistedCorePathEvenForRulesWiki()
    {
        var authenticationClient = new FakeAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["wiki"] = Context("wiki-user", delegatedFrom: "rules-wiki")
        });
        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var request = HostedRequest("/internal/wiki/shared/api/integration/session", "wiki");
        using var response = await client.SendAsync(request);

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

    private static HttpRequestMessage HostedRequest(string path, string ticket)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(ToolHostAuthenticationHeaders.Ticket, ticket);
        request.Headers.Add(ToolHostAuthenticationHeaders.IntrospectionPath, IntrospectionPath);
        return request;
    }

    private static ToolHostAuthenticationContext Context(string userId, string? delegatedFrom) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: "dorks-and-dice",
            User: new ToolHostUserContext(userId, userId),
            GlobalRoles: [],
            Campaigns: [])
        {
            ToolKey = "rules-core",
            DelegatedFromToolKey = delegatedFrom,
            DelegatedFromToolSlug = delegatedFrom
        };

    private sealed class FakeAuthenticationClient(
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
