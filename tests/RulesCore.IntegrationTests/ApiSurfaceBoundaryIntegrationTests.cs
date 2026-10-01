using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RulesCore.Application.Hosting;
using RulesCore.Web;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class ApiSurfaceBoundaryIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/registrations/rules-core/api/introspect";

    [Fact]
    public async Task PrivateApiRequiresPrivateTunnelWhilePublicApiRemainsAvailable()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var authenticationClient = AuthenticationClient();

        await using var factory = Factory(authenticationClient);
        using var client = Client(factory);

        using (var anonymousInternal = await client.GetAsync("/api/wiki/references?limit=1"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousInternal.StatusCode);
        }

        using (var directRequest = HostedRequest("/api/wiki/references?limit=1", "direct-ticket"))
        using (var directResponse = await client.SendAsync(directRequest))
        {
            Assert.Equal(HttpStatusCode.Forbidden, directResponse.StatusCode);
        }

        using (var delegatedRequest = HostedRequest(
            "/api/wiki/references?limit=1",
            "ordinary-delegation-ticket"))
        using (var delegatedResponse = await client.SendAsync(delegatedRequest))
        {
            Assert.Equal(HttpStatusCode.Forbidden, delegatedResponse.StatusCode);
        }

        using (var privateRequest = HostedRequest(
            "/api/wiki/references?limit=1",
            "private-tunnel-ticket"))
        using (var privateResponse = await client.SendAsync(privateRequest))
        {
            Assert.Equal(HttpStatusCode.OK, privateResponse.StatusCode);
        }

        using (var publicResponse = await client.GetAsync("/api/rules?limit=1"))
        {
            Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        }
    }

    [Fact]
    public async Task SplitIngressModesDoNotExposeTheOtherApiSurface()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var authenticationClient = AuthenticationClient();

        await using (var publicFactory = Factory(
            authenticationClient,
            RulesCoreApiSurfaceMode.PublicOnly))
        using (var publicClient = Client(publicFactory))
        {
            using var publicResponse = await publicClient.GetAsync("/api/rules?limit=1");
            Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);

            using var privateRequest = HostedRequest(
                "/api/wiki/references?limit=1",
                "private-tunnel-ticket");
            using var privateResponse = await publicClient.SendAsync(privateRequest);
            Assert.Equal(HttpStatusCode.NotFound, privateResponse.StatusCode);
        }

        await using (var privateFactory = Factory(
            authenticationClient,
            RulesCoreApiSurfaceMode.PrivateOnly))
        using (var privateClient = Client(privateFactory))
        {
            using var publicResponse = await privateClient.GetAsync("/api/rules?limit=1");
            Assert.Equal(HttpStatusCode.NotFound, publicResponse.StatusCode);

            using var privateRequest = HostedRequest(
                "/api/wiki/references?limit=1",
                "private-tunnel-ticket");
            using var privateResponse = await privateClient.SendAsync(privateRequest);
            Assert.Equal(HttpStatusCode.OK, privateResponse.StatusCode);
        }
    }

    private static FakeToolHostAuthenticationClient AuthenticationClient() =>
        new(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["direct-ticket"] = Context("direct-user"),
            ["ordinary-delegation-ticket"] = Context(
                "delegated-user",
                delegatedFromToolKey: "rules-wiki"),
            ["private-tunnel-ticket"] = Context(
                "private-user",
                privateTunnelSourceToolKey: "rules-wiki")
        });

    private static WebApplicationFactory<Program> Factory(
        IToolHostAuthenticationClient authenticationClient,
        RulesCoreApiSurfaceMode? surfaceMode = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            if (surfaceMode.HasValue)
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        [RulesCoreApiBoundary.ApiSurfaceConfigurationKey] = surfaceMode.Value.ToString()
                    });
                });
            }

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IToolHostAuthenticationClient>();
                services.AddSingleton(authenticationClient);
            });
        });

    private static HttpClient Client(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

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
        string? delegatedFromToolKey = null,
        string? privateTunnelSourceToolKey = null) =>
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
            DelegatedFromToolSlug = delegatedFromToolKey,
            PrivateTunnelSourceToolKey = privateTunnelSourceToolKey,
            PrivateTunnelSourceToolSlug = privateTunnelSourceToolKey
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
