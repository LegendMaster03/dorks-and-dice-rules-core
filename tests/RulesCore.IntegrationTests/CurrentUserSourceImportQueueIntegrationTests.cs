using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RulesCore.Application.Hosting;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CurrentUserSourceImportQueueIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";
    private const string RegressionSourceUrl =
        "https://github.com/5etools-mirror-3/5etools-src/tree/main/data";

    [Fact]
    public async Task LargeGitHubWebSourceIsQueuedWithoutWaitingForRemoteImport()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore"))) return;

        var userId = $"web-import-{Guid.NewGuid():N}";
        var authenticationClient = new FakeToolHostAuthenticationClient(
            new ToolHostAuthenticationContext(
                ContractVersion: 1,
                ToolSlug: "rules-core",
                SiteMode: "dorks-and-dice",
                User: new ToolHostUserContext(userId, userId),
                GlobalRoles: [],
                Campaigns: []));

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IToolHostAuthenticationClient>();
                services.RemoveAll<IHostedService>();
                services.AddSingleton<IToolHostAuthenticationClient>(authenticationClient);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        try
        {
            using var request = HostedRequest(HttpMethod.Post, "/api/sources/current-user", "web-import-ticket");
            request.Content = JsonContent.Create(new AddCurrentUserSourceRequest(
                CurrentUserSourceKinds.Web,
                Url: RegressionSourceUrl));

            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            var job = await response.Content.ReadFromJsonAsync<CurrentUserSourceImportJobView>();
            Assert.NotNull(job);
            Assert.Equal(CurrentUserSourceImportJobOperations.Add, job.Operation);
            Assert.Equal(CurrentUserSourceKinds.Web, job.Kind);
            Assert.Equal(CurrentUserSourceImportJobStatuses.Queued, job.Status);
            Assert.Equal(RegressionSourceUrl, job.Url);
            Assert.Null(job.CurrentUserSourceId);
            Assert.Null(job.Error);

            using var listRequest = HostedRequest(
                HttpMethod.Get,
                "/api/sources/current-user/import-jobs",
                "web-import-ticket");
            using var listResponse = await client.SendAsync(listRequest);
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            var jobs = await listResponse.Content.ReadFromJsonAsync<CurrentUserSourceImportJobView[]>();
            Assert.Contains(jobs!, value => value.Id == job.Id);
        }
        finally
        {
            await using var cleanupScope = factory.Services.CreateAsyncScope();
            var db = cleanupScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($$"""
                DELETE FROM current_user_source_import_job
                WHERE user_id = {{userId}};
                """);
        }
    }

    private static HttpRequestMessage HostedRequest(HttpMethod method, string path, string ticket)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(ToolHostAuthenticationHeaders.Ticket, ticket);
        request.Headers.Add(ToolHostAuthenticationHeaders.IntrospectionPath, IntrospectionPath);
        return request;
    }

    private sealed class FakeToolHostAuthenticationClient(ToolHostAuthenticationContext context)
        : IToolHostAuthenticationClient
    {
        public Task<ToolHostAuthenticationContext?> RedeemAsync(
            string ticket,
            string introspectionPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ToolHostAuthenticationContext?>(
                string.Equals(ticket, "web-import-ticket", StringComparison.Ordinal)
                && string.Equals(introspectionPath, IntrospectionPath, StringComparison.Ordinal)
                    ? context
                    : null);
    }
}
