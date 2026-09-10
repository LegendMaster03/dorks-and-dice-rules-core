using System.Net;
using System.Net.Http.Json;
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
public sealed class SourceAccessAdministrationIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task DevCanExplicitlyGrantAndRevokeOnlyCurrentAccountSourceAccess()
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
            ["wrong-mode-dev-ticket"] = Context("source-dev", "professional", ["Dev"])
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var publicPackageKey = $"access-public-{Guid.NewGuid():N}";
        var privatePackageKey = $"access-private-{Guid.NewGuid():N}";
        Guid publicPackageId = Guid.Empty;
        Guid privatePackageId = Guid.Empty;
        Guid privateEntityId = Guid.Empty;

        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var publicImport = await importer.Import5eToolsDocumentAsync(
                    CreateRequest(publicPackageKey, isPublic: true, "Public Access Skill"));
                var privateImport = await importer.Import5eToolsDocumentAsync(
                    CreateRequest(privatePackageKey, isPublic: false, "Restricted Access Skill"));

                publicPackageId = publicImport.PackageId;
                privatePackageId = privateImport.PackageId;
                privateEntityId = privateImport.Entities.Single().EntityId;
            }

            using (var anonymousResponse = await client.GetAsync("/api/source-admin/packages"))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
            }

            using (var rulesRequest = HostedRequest(
                       HttpMethod.Get,
                       "/api/source-admin/packages",
                       "rules-lawyer-ticket"))
            using (var rulesResponse = await client.SendAsync(rulesRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, rulesResponse.StatusCode);
            }

            using (var wrongModeRequest = HostedRequest(
                       HttpMethod.Get,
                       "/api/source-admin/packages",
                       "wrong-mode-dev-ticket"))
            using (var wrongModeResponse = await client.SendAsync(wrongModeRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, wrongModeResponse.StatusCode);
            }

            IReadOnlyList<SourceAdministrationPackageView> packages;
            using (var devRequest = HostedRequest(
                       HttpMethod.Get,
                       "/api/source-admin/packages",
                       "dev-ticket"))
            using (var devResponse = await client.SendAsync(devRequest))
            {
                Assert.Equal(HttpStatusCode.OK, devResponse.StatusCode);
                Assert.Equal("no-store", devResponse.Headers.CacheControl?.ToString());
                packages = (await devResponse.Content
                    .ReadFromJsonAsync<IReadOnlyList<SourceAdministrationPackageView>>())!;
            }

            var publicPackage = packages.Single(value => value.Id == publicPackageId);
            var privatePackage = packages.Single(value => value.Id == privatePackageId);
            Assert.True(publicPackage.IsPublic);
            Assert.False(publicPackage.CurrentUserHasGrant);
            Assert.False(privatePackage.IsPublic);
            Assert.False(privatePackage.CurrentUserHasGrant);

            using (var beforeGrantRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/sources/entities/{privateEntityId}",
                       "dev-ticket"))
            using (var beforeGrantResponse = await client.SendAsync(beforeGrantRequest))
            {
                Assert.Equal(HttpStatusCode.NotFound, beforeGrantResponse.StatusCode);
            }

            SourceAdministrationGrantMutationView firstGrant;
            using (var grantRequest = HostedRequest(
                       HttpMethod.Post,
                       $"/api/source-admin/packages/{privatePackageId}/current-user-grant",
                       "dev-ticket"))
            using (var grantResponse = await client.SendAsync(grantRequest))
            {
                Assert.Equal(HttpStatusCode.OK, grantResponse.StatusCode);
                Assert.Equal("no-store", grantResponse.Headers.CacheControl?.ToString());
                firstGrant = (await grantResponse.Content
                    .ReadFromJsonAsync<SourceAdministrationGrantMutationView>())!;
            }
            Assert.True(firstGrant.Changed);
            Assert.True(firstGrant.Package.CurrentUserHasGrant);
            Assert.Equal(privatePackageId, firstGrant.Package.Id);

            using (var repeatGrantRequest = HostedRequest(
                       HttpMethod.Post,
                       $"/api/source-admin/packages/{privatePackageId}/current-user-grant",
                       "dev-ticket"))
            using (var repeatGrantResponse = await client.SendAsync(repeatGrantRequest))
            {
                Assert.Equal(HttpStatusCode.OK, repeatGrantResponse.StatusCode);
                var repeated = (await repeatGrantResponse.Content
                    .ReadFromJsonAsync<SourceAdministrationGrantMutationView>())!;
                Assert.False(repeated.Changed);
                Assert.True(repeated.Package.CurrentUserHasGrant);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var grants = await db.UserSourceGrants
                    .Where(value => value.SourcePackageId == privatePackageId)
                    .ToArrayAsync();
                var grant = Assert.Single(grants);
                Assert.Equal("source-dev", grant.UserId);
            }

            using (var afterGrantRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/sources/entities/{privateEntityId}",
                       "dev-ticket"))
            using (var afterGrantResponse = await client.SendAsync(afterGrantRequest))
            {
                Assert.Equal(HttpStatusCode.OK, afterGrantResponse.StatusCode);
            }

            using (var publicGrantRequest = HostedRequest(
                       HttpMethod.Post,
                       $"/api/source-admin/packages/{publicPackageId}/current-user-grant",
                       "dev-ticket"))
            using (var publicGrantResponse = await client.SendAsync(publicGrantRequest))
            {
                Assert.Equal(HttpStatusCode.Conflict, publicGrantResponse.StatusCode);
            }

            using (var missingGrantRequest = HostedRequest(
                       HttpMethod.Post,
                       $"/api/source-admin/packages/{Guid.NewGuid()}/current-user-grant",
                       "dev-ticket"))
            using (var missingGrantResponse = await client.SendAsync(missingGrantRequest))
            {
                Assert.Equal(HttpStatusCode.NotFound, missingGrantResponse.StatusCode);
            }

            using (var revokeRequest = HostedRequest(
                       HttpMethod.Delete,
                       $"/api/source-admin/packages/{privatePackageId}/current-user-grant",
                       "dev-ticket"))
            using (var revokeResponse = await client.SendAsync(revokeRequest))
            {
                Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
                var revoked = (await revokeResponse.Content
                    .ReadFromJsonAsync<SourceAdministrationGrantMutationView>())!;
                Assert.True(revoked.Changed);
                Assert.False(revoked.Package.CurrentUserHasGrant);
            }

            using (var repeatRevokeRequest = HostedRequest(
                       HttpMethod.Delete,
                       $"/api/source-admin/packages/{privatePackageId}/current-user-grant",
                       "dev-ticket"))
            using (var repeatRevokeResponse = await client.SendAsync(repeatRevokeRequest))
            {
                Assert.Equal(HttpStatusCode.OK, repeatRevokeResponse.StatusCode);
                var repeated = (await repeatRevokeResponse.Content
                    .ReadFromJsonAsync<SourceAdministrationGrantMutationView>())!;
                Assert.False(repeated.Changed);
                Assert.False(repeated.Package.CurrentUserHasGrant);
            }

            using (var afterRevokeRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/sources/entities/{privateEntityId}",
                       "dev-ticket"))
            using (var afterRevokeResponse = await client.SendAsync(afterRevokeRequest))
            {
                Assert.Equal(HttpStatusCode.NotFound, afterRevokeResponse.StatusCode);
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
            WorkKey: "source-access-work",
            WorkDisplayName: "Source Access Work",
            EditionKey: "source-access-edition",
            EditionDisplayName: "Source Access Edition",
            Json: $$"""
                {
                  "skill": [
                    {
                      "name": "{{entityName}}",
                      "source": "ACCESS",
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
