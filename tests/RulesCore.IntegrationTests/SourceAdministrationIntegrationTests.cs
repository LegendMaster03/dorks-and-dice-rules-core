using System.Net;
using System.Net.Http.Json;
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
public sealed class SourceAdministrationIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task ImportRequiresDorksModeDevAndDoesNotGrantRestrictedSourceAccess()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["dev-ticket"] = Context("source-dev", "dorks-and-dice", ["Dev"]),
            ["rules-lawyer-ticket"] = Context("rules-lawyer", "dorks-and-dice", ["Rules Lawyer"]),
            ["wrong-mode-dev-ticket"] = Context("wrong-mode-dev", "professional", ["Dev"])
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var publicPackageKey = $"admin-public-{Guid.NewGuid():N}";
        var privatePackageKey = $"admin-private-{Guid.NewGuid():N}";
        try
        {
            var publicRequest = CreateRequest(publicPackageKey, isPublic: true, "Public Admin Skill");

            using (var anonymous = await client.PostAsJsonAsync("/api/source-admin/import", publicRequest))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            }

            using (var rulesLawyerRequest = HostedJsonRequest(
                "/api/source-admin/import",
                "rules-lawyer-ticket",
                publicRequest))
            using (var rulesLawyerResponse = await client.SendAsync(rulesLawyerRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, rulesLawyerResponse.StatusCode);
            }

            using (var wrongModeRequest = HostedJsonRequest(
                "/api/source-admin/import",
                "wrong-mode-dev-ticket",
                publicRequest))
            using (var wrongModeResponse = await client.SendAsync(wrongModeRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, wrongModeResponse.StatusCode);
            }

            Guid publicEntityId;
            using (var devRequest = HostedJsonRequest(
                "/api/source-admin/import",
                "dev-ticket",
                publicRequest))
            using (var devResponse = await client.SendAsync(devRequest))
            {
                Assert.Equal(HttpStatusCode.OK, devResponse.StatusCode);
                Assert.Equal("no-store", devResponse.Headers.CacheControl?.ToString());
                using var body = JsonDocument.Parse(await devResponse.Content.ReadAsStringAsync());
                var entity = Assert.Single(body.RootElement.GetProperty("entities").EnumerateArray().ToArray());
                publicEntityId = entity.GetProperty("entityId").GetGuid();
                Assert.True(entity.GetProperty("createdRevision").GetBoolean());
                Assert.Equal(1, entity.GetProperty("revisionNumber").GetInt32());
            }

            using (var repeatRequest = HostedJsonRequest(
                "/api/source-admin/import",
                "dev-ticket",
                publicRequest))
            using (var repeatResponse = await client.SendAsync(repeatRequest))
            {
                Assert.Equal(HttpStatusCode.OK, repeatResponse.StatusCode);
                using var body = JsonDocument.Parse(await repeatResponse.Content.ReadAsStringAsync());
                var entity = Assert.Single(body.RootElement.GetProperty("entities").EnumerateArray().ToArray());
                Assert.False(entity.GetProperty("createdRevision").GetBoolean());
                Assert.Equal(1, entity.GetProperty("revisionNumber").GetInt32());
            }

            using (var publicRead = await client.GetAsync($"/api/sources/entities/{publicEntityId}"))
            {
                Assert.Equal(HttpStatusCode.OK, publicRead.StatusCode);
            }

            var privateRequest = CreateRequest(privatePackageKey, isPublic: false, "Restricted Admin Skill");
            Guid privatePackageId;
            Guid privateEntityId;
            using (var devRequest = HostedJsonRequest(
                "/api/source-admin/import",
                "dev-ticket",
                privateRequest))
            using (var devResponse = await client.SendAsync(devRequest))
            {
                Assert.Equal(HttpStatusCode.OK, devResponse.StatusCode);
                using var body = JsonDocument.Parse(await devResponse.Content.ReadAsStringAsync());
                privatePackageId = body.RootElement.GetProperty("packageId").GetGuid();
                privateEntityId = Assert.Single(body.RootElement.GetProperty("entities").EnumerateArray().ToArray())
                    .GetProperty("entityId")
                    .GetGuid();
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                Assert.False(await db.UserSourceGrants
                    .AnyAsync(value => value.SourcePackageId == privatePackageId));
            }

            using (var directPrivateRead = await client.GetAsync($"/api/sources/entities/{privateEntityId}"))
            {
                Assert.Equal(HttpStatusCode.NotFound, directPrivateRead.StatusCode);
            }

            using (var hostedPrivateReadRequest = HostedRequest(
                HttpMethod.Get,
                $"/api/sources/entities/{privateEntityId}",
                "dev-ticket"))
            using (var hostedPrivateReadResponse = await client.SendAsync(hostedPrivateReadRequest))
            {
                Assert.Equal(HttpStatusCode.NotFound, hostedPrivateReadResponse.StatusCode);
            }
        }
        finally
        {
            await using var cleanupScope = factory.Services.CreateAsyncScope();
            var db = cleanupScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var packages = await db.SourcePackages
                .Where(value => value.Key == publicPackageKey || value.Key == privatePackageKey)
                .ToArrayAsync();
            db.SourcePackages.RemoveRange(packages);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task InvalidSourceDocumentReturnsBadRequestInsteadOfServerError()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["dev-ticket"] = Context("source-dev", "dorks-and-dice", ["Dev"])
        });
        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var invalid = CreateRequest($"invalid-{Guid.NewGuid():N}", isPublic: true, "Invalid");
        invalid = invalid with { Json = "{not-json" };

        using var request = HostedJsonRequest("/api/source-admin/import", "dev-ticket", invalid);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    private static HttpRequestMessage HostedJsonRequest(
        string path,
        string ticket,
        Import5eToolsDocumentRequest request)
    {
        var message = HostedRequest(HttpMethod.Post, path, ticket);
        message.Content = JsonContent.Create(request);
        return message;
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
        string mode,
        IReadOnlyList<string> roles) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: mode,
            User: new ToolHostUserContext(userId, userId),
            GlobalRoles: roles,
            Campaigns: []);

    private static Import5eToolsDocumentRequest CreateRequest(
        string packageKey,
        bool isPublic,
        string entityName) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: $"Package {packageKey}",
            Provider: "integration-test",
            License: "test-only",
            IsPublic: isPublic,
            WorkKey: "admin-work",
            WorkDisplayName: "Admin Work",
            EditionKey: "admin-edition",
            EditionDisplayName: "Admin Edition",
            Json: $$"""
                {
                  "skill": [
                    {
                      "name": "{{entityName}}",
                      "source": "ADMIN",
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
