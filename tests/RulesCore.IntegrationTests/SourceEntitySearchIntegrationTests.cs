using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RulesCore.Application.Hosting;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class SourceEntitySearchIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task SearchFiltersAccessibleEntitiesWithoutLeakingRestrictedPackagesOrDocuments()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["granted-ticket"] = Context("search-granted", "Granted Search User"),
            ["other-ticket"] = Context("search-other", "Other Search User")
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        Guid publicPackageId;
        Guid privatePackageId;
        string publicPackageKey;
        string privatePackageKey;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
            var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();

            publicPackageKey = $"source-search-public-{Guid.NewGuid():N}";
            privatePackageKey = $"source-search-private-{Guid.NewGuid():N}";
            var publicResult = await importer.Import5eToolsDocumentAsync(CreateRequest(
                publicPackageKey,
                "Public Search Package",
                "Public Search Edition",
                "Arcana Search Public",
                isPublic: true));
            var privateResult = await importer.Import5eToolsDocumentAsync(CreateRequest(
                privatePackageKey,
                "Private Search Package",
                "Private Search Edition",
                "Arcana Search Private",
                isPublic: false));

            publicPackageId = publicResult.PackageId;
            privatePackageId = privateResult.PackageId;
            await grants.GrantAsync("search-granted", privatePackageId);
        }

        using (var anonymous = await client.GetAsync("/api/sources/entities?entityType=skill&q=Arcana%20Search&limit=25"))
        {
            Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
            using var body = JsonDocument.Parse(await anonymous.Content.ReadAsStringAsync());
            var items = body.RootElement.EnumerateArray().ToArray();
            var item = Assert.Single(items);
            Assert.Equal(publicPackageKey, item.GetProperty("packageKey").GetString());
            Assert.Equal("Arcana Search Public", item.GetProperty("name").GetString());
            Assert.False(item.TryGetProperty("document", out _));
            Assert.True(item.GetProperty("latestRevisionNumber").GetInt32() >= 1);
        }

        using (var grantedRequest = HostedRequest(
            HttpMethod.Get,
            "/api/sources/entities?entityType=skill&q=Arcana%20Search&limit=25",
            "granted-ticket"))
        using (var granted = await client.SendAsync(grantedRequest))
        {
            Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
            Assert.Equal("no-store", granted.Headers.CacheControl?.ToString());
            using var body = JsonDocument.Parse(await granted.Content.ReadAsStringAsync());
            var packageKeys = body.RootElement
                .EnumerateArray()
                .Select(item => item.GetProperty("packageKey").GetString())
                .ToArray();
            Assert.Contains(publicPackageKey, packageKeys);
            Assert.Contains(privatePackageKey, packageKeys);
        }

        using (var otherRequest = HostedRequest(
            HttpMethod.Get,
            "/api/sources/entities?entityType=skill&q=Arcana%20Search&limit=25",
            "other-ticket"))
        using (var other = await client.SendAsync(otherRequest))
        {
            Assert.Equal(HttpStatusCode.OK, other.StatusCode);
            using var body = JsonDocument.Parse(await other.Content.ReadAsStringAsync());
            var packageKeys = body.RootElement
                .EnumerateArray()
                .Select(item => item.GetProperty("packageKey").GetString())
                .ToArray();
            Assert.Contains(publicPackageKey, packageKeys);
            Assert.DoesNotContain(privatePackageKey, packageKeys);
        }

        using (var editionRequest = HostedRequest(
            HttpMethod.Get,
            "/api/sources/entities?q=Private%20Search%20Edition&limit=25",
            "granted-ticket"))
        using (var editionResponse = await client.SendAsync(editionRequest))
        {
            Assert.Equal(HttpStatusCode.OK, editionResponse.StatusCode);
            using var body = JsonDocument.Parse(await editionResponse.Content.ReadAsStringAsync());
            var item = Assert.Single(body.RootElement.EnumerateArray().ToArray());
            Assert.Equal(privatePackageKey, item.GetProperty("packageKey").GetString());
        }

        using (var wrongType = await client.GetAsync("/api/sources/entities?entityType=spell&q=Arcana%20Search"))
        {
            Assert.Equal(HttpStatusCode.OK, wrongType.StatusCode);
            using var body = JsonDocument.Parse(await wrongType.Content.ReadAsStringAsync());
            Assert.Empty(body.RootElement.EnumerateArray().ToArray());
        }

        await using var cleanupScope = factory.Services.CreateAsyncScope();
        var db = cleanupScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        var packages = await db.SourcePackages
            .Where(value => value.Id == publicPackageId || value.Id == privatePackageId)
            .ToArrayAsync();
        db.SourcePackages.RemoveRange(packages);
        await db.SaveChangesAsync();
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
            Campaigns: []);

    private static Import5eToolsDocumentRequest CreateRequest(
        string packageKey,
        string packageDisplayName,
        string editionDisplayName,
        string entityName,
        bool isPublic) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: packageDisplayName,
            Provider: "integration-test",
            License: "test-only",
            IsPublic: isPublic,
            WorkKey: "search-work",
            WorkDisplayName: "Search Work",
            EditionKey: "search-edition",
            EditionDisplayName: editionDisplayName,
            Json: $$"""
                {
                  "skill": [
                    {
                      "name": "{{entityName}}",
                      "source": "SEARCH",
                      "ability": "int",
                      "notInSummary": { "preserved": true }
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
