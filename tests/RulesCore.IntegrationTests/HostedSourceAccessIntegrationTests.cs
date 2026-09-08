using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RulesCore.Application.Hosting;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class HostedSourceAccessIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task HostedGrantUnlocksPrivateSourceWithoutBroadeningOtherUsers()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["granted-ticket"] = Context("user-granted", "Granted User"),
            ["other-ticket"] = Context("user-other", "Other User")
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        Guid packageId;
        Guid entityId;
        string packageKey;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
            var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
            packageKey = $"private-grant-{Guid.NewGuid():N}";
            var result = await importer.Import5eToolsDocumentAsync(CreateRequest(packageKey));
            packageId = result.PackageId;
            entityId = result.Entities[0].EntityId;
            await grants.GrantAsync("user-granted", packageId);

            Assert.True(await grants.HasGrantAsync("user-granted", packageId));
            Assert.False(await grants.HasGrantAsync("user-other", packageId));
        }

        using (var anonymousResponse = await client.GetAsync($"/api/sources/entities/{entityId}"))
        {
            Assert.Equal(HttpStatusCode.NotFound, anonymousResponse.StatusCode);
        }

        using (var grantedRequest = HostedRequest(HttpMethod.Get, $"/api/sources/entities/{entityId}", "granted-ticket"))
        using (var grantedResponse = await client.SendAsync(grantedRequest))
        {
            Assert.Equal(HttpStatusCode.OK, grantedResponse.StatusCode);
            Assert.Equal("no-store", grantedResponse.Headers.CacheControl?.ToString());
        }

        using (var otherRequest = HostedRequest(HttpMethod.Get, $"/api/sources/entities/{entityId}", "other-ticket"))
        using (var otherResponse = await client.SendAsync(otherRequest))
        {
            Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
        }

        using (var packagesRequest = HostedRequest(HttpMethod.Get, "/api/sources", "granted-ticket"))
        using (var packagesResponse = await client.SendAsync(packagesRequest))
        {
            Assert.Equal(HttpStatusCode.OK, packagesResponse.StatusCode);
            using var packages = JsonDocument.Parse(await packagesResponse.Content.ReadAsStringAsync());
            Assert.Contains(
                packages.RootElement.EnumerateArray(),
                package => package.GetProperty("key").GetString() == packageKey
                    && !package.GetProperty("isPublic").GetBoolean());
        }

        await using var cleanupScope = factory.Services.CreateAsyncScope();
        var db = cleanupScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        var packageToDelete = await db.SourcePackages.FindAsync(packageId);
        Assert.NotNull(packageToDelete);
        db.SourcePackages.Remove(packageToDelete);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task HostedSessionUsesRedeemedContextAndRejectsIncompleteOrInvalidTickets()
    {
        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["valid-ticket"] = Context("user-123", "Rules Lawyer")
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using (var request = HostedRequest(HttpMethod.Get, "/api/integration/session", "valid-ticket"))
        using (var response = await client.SendAsync(request))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("rules-core", body.RootElement.GetProperty("toolSlug").GetString());
            Assert.Equal("dorks-and-dice", body.RootElement.GetProperty("siteMode").GetString());
            Assert.Equal("user-123", body.RootElement.GetProperty("user").GetProperty("id").GetString());
            Assert.Contains(
                body.RootElement.GetProperty("globalRoles").EnumerateArray(),
                role => role.GetString() == "Rules Lawyer");
            Assert.Equal("DM", body.RootElement.GetProperty("campaigns")[0].GetProperty("role").GetString());
        }

        using (var directResponse = await client.GetAsync("/api/integration/session"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, directResponse.StatusCode);
        }

        using (var incomplete = new HttpRequestMessage(HttpMethod.Get, "/api/integration/session"))
        {
            incomplete.Headers.Add(ToolHostAuthenticationHeaders.Ticket, "valid-ticket");
            using var response = await client.SendAsync(incomplete);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using (var invalid = HostedRequest(HttpMethod.Get, "/api/integration/session", "invalid-ticket"))
        using (var response = await client.SendAsync(invalid))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

    private static ToolHostAuthenticationContext Context(string userId, string displayName) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: "dorks-and-dice",
            User: new ToolHostUserContext(userId, displayName),
            GlobalRoles: ["Rules Lawyer"],
            Campaigns:
            [
                new ToolHostCampaignContext(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    "Test Campaign",
                    "DM")
            ]);

    private static Import5eToolsDocumentRequest CreateRequest(string packageKey) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: "Private Grant Test Package",
            Provider: "integration-test",
            License: "test-only",
            IsPublic: false,
            WorkKey: "private-work",
            WorkDisplayName: "Private Work",
            EditionKey: "private-edition",
            EditionDisplayName: "Private Edition",
            Json: """
                {
                  "skill": [
                    {
                      "name": "Private Arcana",
                      "source": "PRIVATE",
                      "ability": "int"
                    }
                  ]
                }
                """);

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
