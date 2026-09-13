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
public sealed class CurrentUserSourceIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task SignedInAccountCanAddPrivateSourceWithoutElevatedRole()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore"))) return;

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["user-ticket"] = Context("ordinary-user", "dorks-and-dice"),
            ["other-ticket"] = Context("other-user", "dorks-and-dice"),
            ["wrong-mode-ticket"] = Context("wrong-mode", "professional")
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var payload = new AddCurrentUserSourceRequest(
            CurrentUserSourceKinds.Upload,
            FileName: "personal-source.json",
            Json: """
                {
                  "skill": [
                    { "name": "Private Arcana", "source": "BOOKA", "ability": "int" },
                    { "name": "Private Survival", "source": "BOOKB", "ability": "wis" }
                  ]
                }
                """);

        using (var anonymous = await client.PostAsJsonAsync("/api/sources/current-user", payload))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        using (var wrongModeRequest = HostedJsonRequest(
            "/api/sources/current-user",
            "wrong-mode-ticket",
            payload))
        using (var wrongModeResponse = await client.SendAsync(wrongModeRequest))
        {
            Assert.Equal(HttpStatusCode.Forbidden, wrongModeResponse.StatusCode);
        }

        CurrentUserSourceView added;
        using (var request = HostedJsonRequest("/api/sources/current-user", "user-ticket", payload))
        using (var response = await client.SendAsync(request))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            added = (await response.Content.ReadFromJsonAsync<CurrentUserSourceView>())!;
            Assert.Equal(CurrentUserSourceKinds.Upload, added.Kind);
            Assert.Equal("personal-source.json", added.DisplayName);
            Assert.Equal(2, added.SourceCodeCount);
            Assert.Equal(2, added.EntityCount);
            Assert.Contains("BOOKA", added.SourceCodes);
            Assert.Contains("BOOKB", added.SourceCodes);
        }

        try
        {
            using (var request = HostedRequest(HttpMethod.Get, "/api/sources/current-user", "user-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var sources = await response.Content.ReadFromJsonAsync<CurrentUserSourceView[]>();
                Assert.Contains(sources!, value => value.Id == added.Id);
            }

            using (var request = HostedRequest(
                HttpMethod.Get,
                "/api/sources/entities?q=Private%20Arcana&limit=20",
                "user-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var entities = await response.Content.ReadFromJsonAsync<SourceEntitySummary[]>();
                Assert.Contains(entities!, value => value.Name == "Private Arcana");
            }

            using (var request = HostedRequest(
                HttpMethod.Get,
                "/api/sources/entities?q=Private%20Arcana&limit=20",
                "other-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var entities = await response.Content.ReadFromJsonAsync<SourceEntitySummary[]>();
                Assert.DoesNotContain(entities!, value => value.Name == "Private Arcana");
            }

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            Assert.True(await db.UserSourceGrants.AnyAsync(value =>
                value.UserId == "ordinary-user"
                && value.SourcePackageId == added.SourcePackageId));
            Assert.False(await db.UserSourceGrants.AnyAsync(value =>
                value.UserId == "other-user"
                && value.SourcePackageId == added.SourcePackageId));
        }
        finally
        {
            await using var cleanupScope = factory.Services.CreateAsyncScope();
            var db = cleanupScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var package = await db.SourcePackages.SingleOrDefaultAsync(value => value.Id == added.SourcePackageId);
            if (package is not null)
            {
                db.SourcePackages.Remove(package);
                await db.SaveChangesAsync();
            }
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

    private static HttpRequestMessage HostedJsonRequest(
        string path,
        string ticket,
        AddCurrentUserSourceRequest request)
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

    private static ToolHostAuthenticationContext Context(string userId, string mode) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: mode,
            User: new ToolHostUserContext(userId, userId),
            GlobalRoles: [],
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
