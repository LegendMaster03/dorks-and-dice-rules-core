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
using RulesCore.Infrastructure.Sources;

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
                var sourceWorker = services.SingleOrDefault(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService)
                    && string.Equals(
                        descriptor.ImplementationType?.Name,
                        "CurrentUserSourceRefreshBackground",
                        StringComparison.Ordinal));
                if (sourceWorker is not null)
                {
                    services.Remove(sourceWorker);
                }
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
            Assert.Equal("queued", job.ProgressStage);
            Assert.Equal(0, job.ProgressCurrent);
            Assert.Null(job.ProgressTotal);
            Assert.Equal("Waiting for background importer", job.ProgressDetail);
            Assert.NotNull(job.ProgressUpdatedAt);

            using var listRequest = HostedRequest(
                HttpMethod.Get,
                "/api/sources/current-user/import-jobs",
                "web-import-ticket");
            using var listResponse = await client.SendAsync(listRequest);
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            var jobs = await listResponse.Content.ReadFromJsonAsync<CurrentUserSourceImportJobView[]>();
            var listed = Assert.Single(jobs!, value => value.Id == job.Id);
            Assert.Equal(job.ProgressStage, listed.ProgressStage);
            Assert.Equal(job.ProgressCurrent, listed.ProgressCurrent);
            Assert.Equal(job.ProgressTotal, listed.ProgressTotal);
            Assert.Equal(job.ProgressDetail, listed.ProgressDetail);
            Assert.Equal(job.ProgressUpdatedAt, listed.ProgressUpdatedAt);

            await using (var progressScope = factory.Services.CreateAsyncScope())
            {
                var progressDb = progressScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var progressJobs = new CurrentUserSourceImportJobService(progressDb);
                var claimed = await progressJobs.ClaimNextAsync();
                Assert.NotNull(claimed);
                Assert.Equal(job.Id, claimed.Id);
                await progressJobs.UpdateProgressAsync(
                    job.Id,
                    new CurrentUserSourceImportProgress(
                        "persisting",
                        100,
                        2631,
                        Detail: null,
                        CurrentItem: "Wreath of the Prism",
                        CurrentItemType: "itemGroup",
                        AdapterFormat: "5etools-json",
                        FilesDiscovered: 824,
                        FilesProcessed: 824,
                        ImportUnitsProcessed: 17,
                        ImportUnitTotal: 824,
                        RecordsDiscovered: 2631,
                        RecordsTranslated: 2631,
                        EntitiesPersisted: 100));
            }

            using var progressListRequest = HostedRequest(
                HttpMethod.Get,
                "/api/sources/current-user/import-jobs",
                "web-import-ticket");
            using var progressListResponse = await client.SendAsync(progressListRequest);
            Assert.Equal(HttpStatusCode.OK, progressListResponse.StatusCode);
            var progressedJobs = await progressListResponse.Content
                .ReadFromJsonAsync<CurrentUserSourceImportJobView[]>();
            var progressed = Assert.Single(progressedJobs!, value => value.Id == job.Id);
            Assert.Equal(CurrentUserSourceImportJobStatuses.Running, progressed.Status);
            Assert.Equal("persisting", progressed.ProgressStage);
            Assert.Equal(100, progressed.ProgressCurrent);
            Assert.Equal(2631, progressed.ProgressTotal);
            Assert.Null(progressed.ProgressDetail);
            Assert.NotNull(progressed.Progress);
            Assert.Equal("Wreath of the Prism", progressed.Progress!.CurrentItem);
            Assert.Equal("itemGroup", progressed.Progress.CurrentItemType);
            Assert.Equal(824, progressed.Progress.FilesDiscovered);
            Assert.Equal(824, progressed.Progress.FilesProcessed);
            Assert.Equal(17, progressed.Progress.ImportUnitsProcessed);
            Assert.Equal(824, progressed.Progress.ImportUnitTotal);
            Assert.Equal(100, progressed.Progress.EntitiesPersisted);

            await using (var gracefulScope = factory.Services.CreateAsyncScope())
            {
                var gracefulDb = gracefulScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var gracefulJobs = new CurrentUserSourceImportJobService(gracefulDb);
                Assert.True(await gracefulJobs.RequeueRunningJobAsync(
                    job.Id,
                    "Web source import was interrupted by service shutdown; waiting to retry"));
            }

            await using (var reclaimScope = factory.Services.CreateAsyncScope())
            {
                var reclaimDb = reclaimScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var reclaimJobs = new CurrentUserSourceImportJobService(reclaimDb);
                var reclaimedForRecovery = await reclaimJobs.ClaimNextAsync();
                Assert.NotNull(reclaimedForRecovery);
                Assert.Equal(job.Id, reclaimedForRecovery.Id);
            }

            await using (var recoveryScope = factory.Services.CreateAsyncScope())
            {
                var recoveryDb = recoveryScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var recoveryJobs = new CurrentUserSourceImportJobService(recoveryDb);
                Assert.Equal(1, await recoveryJobs.RequeueInterruptedRunningJobsAsync());
            }

            using var recoveredListRequest = HostedRequest(
                HttpMethod.Get,
                "/api/sources/current-user/import-jobs",
                "web-import-ticket");
            using var recoveredListResponse = await client.SendAsync(recoveredListRequest);
            Assert.Equal(HttpStatusCode.OK, recoveredListResponse.StatusCode);
            var recoveredJobs = await recoveredListResponse.Content
                .ReadFromJsonAsync<CurrentUserSourceImportJobView[]>();
            var recovered = Assert.Single(recoveredJobs!, value => value.Id == job.Id);
            Assert.Equal(CurrentUserSourceImportJobStatuses.Queued, recovered.Status);
            Assert.Equal("queued", recovered.ProgressStage);
            Assert.Equal(0, recovered.ProgressCurrent);
            Assert.Null(recovered.ProgressTotal);
            Assert.Equal(
                "Previous Web source import was interrupted; waiting to retry",
                recovered.ProgressDetail);

            await using (var retryScope = factory.Services.CreateAsyncScope())
            {
                var retryDb = retryScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var retryJobs = new CurrentUserSourceImportJobService(retryDb);
                var reclaimed = await retryJobs.ClaimNextAsync();
                Assert.NotNull(reclaimed);
                Assert.Equal(job.Id, reclaimed.Id);
                await retryJobs.FailAsync(job.Id, new InvalidOperationException("Fixture terminal failure."));
            }

            using var dismissRequest = HostedRequest(
                HttpMethod.Post,
                "/api/sources/current-user/import-jobs/dismiss",
                "web-import-ticket");
            dismissRequest.Content = JsonContent.Create(
                new DismissCurrentUserSourceImportJobsRequest([job.Id]));
            using var dismissResponse = await client.SendAsync(dismissRequest);
            Assert.Equal(HttpStatusCode.OK, dismissResponse.StatusCode);
            var dismissResult = await dismissResponse.Content
                .ReadFromJsonAsync<DismissCurrentUserSourceImportJobsResult>();
            Assert.NotNull(dismissResult);
            Assert.Equal(1, dismissResult.DismissedCount);

            using var dismissedListRequest = HostedRequest(
                HttpMethod.Get,
                "/api/sources/current-user/import-jobs",
                "web-import-ticket");
            using var dismissedListResponse = await client.SendAsync(dismissedListRequest);
            Assert.Equal(HttpStatusCode.OK, dismissedListResponse.StatusCode);
            var visibleAfterDismiss = await dismissedListResponse.Content
                .ReadFromJsonAsync<CurrentUserSourceImportJobView[]>();
            Assert.DoesNotContain(visibleAfterDismiss!, value => value.Id == job.Id);

            await using (var historyScope = factory.Services.CreateAsyncScope())
            {
                var historyDb = historyScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var retainedHistory = await historyDb.Database.SqlQueryRaw<bool>(
                        """
                        SELECT EXISTS (
                            SELECT 1
                            FROM current_user_source_import_job
                            WHERE current_user_source_import_job_id = {0}
                                AND status = 'failed'
                                AND dismissed_at IS NOT NULL) AS "Value"
                        """,
                        job.Id)
                    .SingleAsync();
                Assert.True(retainedHistory);
            }
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

    [Fact]
    public async Task SourceImportExecutionPolicyAllowsLongRunningDatabaseCommands()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>()
                .UseNpgsql(connectionString)
                .Options);
        db.Database.SetCommandTimeout(30);

        SourceImportExecutionPolicy.Apply(db);

        Assert.Equal(
            SourceImportExecutionPolicy.DatabaseCommandTimeoutSeconds,
            db.Database.GetCommandTimeout().GetValueOrDefault());
    }

    [Fact]
    public async Task ImportJobFailureExplainsDatabaseCommandTimeout()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var userId = $"web-timeout-{Guid.NewGuid():N}";
        await using var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>()
                .UseNpgsql(connectionString)
                .Options);
        var jobs = new CurrentUserSourceImportJobService(db);
        try
        {
            var job = await jobs.QueueWebAddAsync(userId, RegressionSourceUrl);
            await jobs.FailAsync(
                job.Id,
                new InvalidOperationException(
                    "An exception has been raised that is likely due to a transient failure.",
                    new TimeoutException("Timeout during reading attempt")));

            var failed = Assert.Single(
                await jobs.ListAsync(userId),
                value => value.Id == job.Id);
            Assert.Equal(
                "A database operation timed out while importing this source. The import can be retried.",
                failed.Error);
        }
        finally
        {
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
