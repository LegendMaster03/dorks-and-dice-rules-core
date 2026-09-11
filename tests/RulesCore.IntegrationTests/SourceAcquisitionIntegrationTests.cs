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
public sealed class SourceAcquisitionIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task AcquisitionHistoryIsCurrentUserScopedAndIndependentFromSourceGrants()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["dev-ticket"] = Context("acquisition-dev", "dorks-and-dice", ["Dev"]),
            ["other-dev-ticket"] = Context("other-acquisition-dev", "dorks-and-dice", ["Dev"]),
            ["rules-ticket"] = Context("acquisition-rules", "dorks-and-dice", ["Rules Lawyer"]),
            ["wrong-mode-ticket"] = Context("acquisition-dev", "professional", ["Dev"])
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var packageKey = $"acquisition-private-{Guid.NewGuid():N}";
        Guid packageId = Guid.Empty;
        Guid entityId = Guid.Empty;

        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var imported = await importer.Import5eToolsDocumentAsync(CreateRequest(packageKey));
                packageId = imported.PackageId;
                entityId = imported.Entities.Single().EntityId;
            }

            using (var anonymous = await client.GetAsync("/api/source-admin/acquisitions"))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            }

            using (var rulesRequest = HostedRequest(HttpMethod.Get, "/api/source-admin/acquisitions", "rules-ticket"))
            using (var rulesResponse = await client.SendAsync(rulesRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, rulesResponse.StatusCode);
            }

            using (var wrongModeRequest = HostedRequest(HttpMethod.Get, "/api/source-admin/acquisitions", "wrong-mode-ticket"))
            using (var wrongModeResponse = await client.SendAsync(wrongModeRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, wrongModeResponse.StatusCode);
            }

            using (var beforeGrantRequest = HostedRequest(HttpMethod.Get, $"/api/sources/entities/{entityId}", "dev-ticket"))
            using (var beforeGrantResponse = await client.SendAsync(beforeGrantRequest))
            {
                Assert.Equal(HttpStatusCode.NotFound, beforeGrantResponse.StatusCode);
            }

            var acquiredAt = DateTimeOffset.UtcNow.AddDays(-30);
            SourceAcquisitionMutationView recorded;
            using (var recordRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/source-admin/packages/{packageId}/current-user-acquisitions",
                       "dev-ticket",
                       new RecordCurrentUserSourceAcquisitionRequest(
                           SourceAcquisitionKinds.PhysicalCopy,
                           "Shelf copy",
                           acquiredAt)))
            using (var recordResponse = await client.SendAsync(recordRequest))
            {
                Assert.Equal(HttpStatusCode.OK, recordResponse.StatusCode);
                Assert.Equal("no-store", recordResponse.Headers.CacheControl?.ToString());
                recorded = (await recordResponse.Content
                    .ReadFromJsonAsync<SourceAcquisitionMutationView>())!;
            }

            Assert.True(recorded.Changed);
            Assert.Equal(packageId, recorded.Acquisition.SourcePackageId);
            Assert.Equal(SourceAcquisitionKinds.PhysicalCopy, recorded.Acquisition.AcquisitionKind);
            Assert.Equal("Shelf copy", recorded.Acquisition.Reference);
            Assert.False(recorded.Acquisition.IsVoided);

            using (var stillNoGrantRequest = HostedRequest(HttpMethod.Get, $"/api/sources/entities/{entityId}", "dev-ticket"))
            using (var stillNoGrantResponse = await client.SendAsync(stillNoGrantRequest))
            {
                Assert.Equal(HttpStatusCode.NotFound, stillNoGrantResponse.StatusCode);
            }

            using (var listRequest = HostedRequest(HttpMethod.Get, "/api/source-admin/acquisitions", "dev-ticket"))
            using (var listResponse = await client.SendAsync(listRequest))
            {
                Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
                var acquisitions = (await listResponse.Content
                    .ReadFromJsonAsync<IReadOnlyList<SourceAcquisitionView>>())!;
                var acquisition = Assert.Single(acquisitions.Where(value => value.Id == recorded.Acquisition.Id));
                Assert.Equal("Shelf copy", acquisition.Reference);
                Assert.False(acquisition.IsVoided);
            }

            using (var otherListRequest = HostedRequest(HttpMethod.Get, "/api/source-admin/acquisitions", "other-dev-ticket"))
            using (var otherListResponse = await client.SendAsync(otherListRequest))
            {
                Assert.Equal(HttpStatusCode.OK, otherListResponse.StatusCode);
                var acquisitions = (await otherListResponse.Content
                    .ReadFromJsonAsync<IReadOnlyList<SourceAcquisitionView>>())!;
                Assert.DoesNotContain(acquisitions, value => value.Id == recorded.Acquisition.Id);
            }

            using (var otherVoidRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/source-admin/acquisitions/{recorded.Acquisition.Id}/void",
                       "other-dev-ticket",
                       new VoidCurrentUserSourceAcquisitionRequest("Not mine")))
            using (var otherVoidResponse = await client.SendAsync(otherVoidRequest))
            {
                Assert.Equal(HttpStatusCode.NotFound, otherVoidResponse.StatusCode);
            }

            using (var invalidKindRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/source-admin/packages/{packageId}/current-user-acquisitions",
                       "dev-ticket",
                       new RecordCurrentUserSourceAcquisitionRequest("unverified-magic", null, null)))
            using (var invalidKindResponse = await client.SendAsync(invalidKindRequest))
            {
                Assert.Equal(HttpStatusCode.BadRequest, invalidKindResponse.StatusCode);
            }

            using (var grantRequest = HostedRequest(
                       HttpMethod.Post,
                       $"/api/source-admin/packages/{packageId}/current-user-grant",
                       "dev-ticket"))
            using (var grantResponse = await client.SendAsync(grantRequest))
            {
                Assert.Equal(HttpStatusCode.OK, grantResponse.StatusCode);
            }

            using (var afterGrantRequest = HostedRequest(HttpMethod.Get, $"/api/sources/entities/{entityId}", "dev-ticket"))
            using (var afterGrantResponse = await client.SendAsync(afterGrantRequest))
            {
                Assert.Equal(HttpStatusCode.OK, afterGrantResponse.StatusCode);
            }

            SourceAcquisitionMutationView voided;
            using (var voidRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/source-admin/acquisitions/{recorded.Acquisition.Id}/void",
                       "dev-ticket",
                       new VoidCurrentUserSourceAcquisitionRequest("Duplicate record")))
            using (var voidResponse = await client.SendAsync(voidRequest))
            {
                Assert.Equal(HttpStatusCode.OK, voidResponse.StatusCode);
                voided = (await voidResponse.Content
                    .ReadFromJsonAsync<SourceAcquisitionMutationView>())!;
            }
            Assert.True(voided.Changed);
            Assert.True(voided.Acquisition.IsVoided);
            Assert.Equal("Duplicate record", voided.Acquisition.VoidReason);

            using (var repeatVoidRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/source-admin/acquisitions/{recorded.Acquisition.Id}/void",
                       "dev-ticket",
                       new VoidCurrentUserSourceAcquisitionRequest("Ignored")))
            using (var repeatVoidResponse = await client.SendAsync(repeatVoidRequest))
            {
                Assert.Equal(HttpStatusCode.OK, repeatVoidResponse.StatusCode);
                var repeated = (await repeatVoidResponse.Content
                    .ReadFromJsonAsync<SourceAcquisitionMutationView>())!;
                Assert.False(repeated.Changed);
                Assert.Equal("Duplicate record", repeated.Acquisition.VoidReason);
            }

            using (var afterVoidRequest = HostedRequest(HttpMethod.Get, $"/api/sources/entities/{entityId}", "dev-ticket"))
            using (var afterVoidResponse = await client.SendAsync(afterVoidRequest))
            {
                Assert.Equal(HttpStatusCode.OK, afterVoidResponse.StatusCode);
            }

            using (var revokeRequest = HostedRequest(
                       HttpMethod.Delete,
                       $"/api/source-admin/packages/{packageId}/current-user-grant",
                       "dev-ticket"))
            using (var revokeResponse = await client.SendAsync(revokeRequest))
            {
                Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
            }

            using (var afterRevokeRequest = HostedRequest(HttpMethod.Get, $"/api/sources/entities/{entityId}", "dev-ticket"))
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
                .Where(value => value.Key == packageKey)
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

    private static HttpRequestMessage HostedJsonRequest<T>(
        HttpMethod method,
        string path,
        string ticket,
        T payload)
    {
        var request = HostedRequest(method, path, ticket);
        request.Content = JsonContent.Create(payload);
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

    private static Import5eToolsDocumentRequest CreateRequest(string packageKey) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: $"Package {packageKey}",
            Provider: "integration-test",
            License: "test-only",
            IsPublic: false,
            WorkKey: "acquisition-work",
            WorkDisplayName: "Acquisition Work",
            EditionKey: "acquisition-edition",
            EditionDisplayName: "Acquisition Edition",
            Json: """
                {
                  "skill": [
                    {
                      "name": "Acquisition Skill",
                      "source": "ACQ",
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
