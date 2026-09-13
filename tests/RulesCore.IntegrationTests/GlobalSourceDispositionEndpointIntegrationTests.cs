using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class GlobalSourceDispositionEndpointIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task OnlyGlobalRulesLawyerCanIgnoreAndRestoreSourcePackage()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore"))) return;

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["rules-lawyer"] = Context("rules-lawyer", [RulesAuthority.RulesLawyerRole]),
            ["plain-user"] = Context("plain-user", [])
        });

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IToolHostAuthenticationClient>();
                services.AddSingleton<IToolHostAuthenticationClient>(authenticationClient);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var packageKey = $"ignore-endpoint-{Guid.NewGuid():N}";
        Guid packageId = Guid.Empty;
        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var imported = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                    packageKey,
                    "Endpoint ignore package",
                    "integration-test",
                    License: null,
                    IsPublic: true,
                    WorkKey: "work",
                    WorkDisplayName: "Work",
                    EditionKey: "edition",
                    EditionDisplayName: "Edition",
                    Json: """{"feat":[{"name":"Endpoint Ignore Test","source":"TEST"}]}"""));
                packageId = imported.PackageId;
            }

            using (var anonymous = new HttpRequestMessage(
                       HttpMethod.Put,
                       $"/api/global/rules/normalization/packages/{packageId}/ignored"))
            {
                anonymous.Content = JsonContent.Create(new SetGlobalSourceIgnoredRequest(true));
                using var response = await client.SendAsync(anonymous);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            using (var plain = HostedRequest(
                       HttpMethod.Put,
                       $"/api/global/rules/normalization/packages/{packageId}/ignored",
                       "plain-user"))
            {
                plain.Content = JsonContent.Create(new SetGlobalSourceIgnoredRequest(true));
                using var response = await client.SendAsync(plain);
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            using (var ignore = HostedRequest(
                       HttpMethod.Put,
                       $"/api/global/rules/normalization/packages/{packageId}/ignored",
                       "rules-lawyer"))
            {
                ignore.Content = JsonContent.Create(new SetGlobalSourceIgnoredRequest(
                    true,
                    "User homebrew is not relevant globally."));
                using var response = await client.SendAsync(ignore);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            using (var list = HostedRequest(
                       HttpMethod.Get,
                       "/api/global/rules/normalization/ignored-packages",
                       "rules-lawyer"))
            using (var response = await client.SendAsync(list))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var ignored = (await response.Content.ReadFromJsonAsync<IReadOnlyList<GlobalIgnoredSourceView>>())!;
                var entry = Assert.Single(ignored, value => value.SourcePackageId == packageId);
                Assert.Equal("User homebrew is not relevant globally.", entry.Reason);
            }

            using (var restore = HostedRequest(
                       HttpMethod.Put,
                       $"/api/global/rules/normalization/packages/{packageId}/ignored",
                       "rules-lawyer"))
            {
                restore.Content = JsonContent.Create(new SetGlobalSourceIgnoredRequest(false));
                using var response = await client.SendAsync(restore);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            using (var list = HostedRequest(
                       HttpMethod.Get,
                       "/api/global/rules/normalization/ignored-packages",
                       "rules-lawyer"))
            using (var response = await client.SendAsync(list))
            {
                var ignored = (await response.Content.ReadFromJsonAsync<IReadOnlyList<GlobalIgnoredSourceView>>())!;
                Assert.DoesNotContain(ignored, value => value.SourcePackageId == packageId);
            }
        }
        finally
        {
            if (packageId != Guid.Empty)
            {
                await using var scope = factory.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var package = await db.SourcePackages.SingleOrDefaultAsync(value => value.Id == packageId);
                if (package is not null)
                {
                    db.SourcePackages.Remove(package);
                    await db.SaveChangesAsync();
                }
            }
        }
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
        IReadOnlyList<string> globalRoles) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: RulesAuthority.DorksAndDiceMode,
            User: new ToolHostUserContext(userId, userId),
            GlobalRoles: globalRoles,
            Campaigns: []);

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