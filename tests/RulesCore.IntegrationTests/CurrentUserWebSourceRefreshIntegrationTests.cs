using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CurrentUserWebSourceRefreshIntegrationTests
{
    [Fact]
    public async Task ChangedWebSourceKeepsSharedOriginPackageAndCreatesNextRevision()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>()
                .UseNpgsql(connectionString)
                .Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..10];
        var userId = $"origin-refresh-{token}";
        var sourceCode = $"OR{token}".ToUpperInvariant();
        var firstContent = $$"""
            {
              "skill": [
                { "name": "Origin Refresh {{token}}", "source": "{{sourceCode}}", "ability": "int" }
              ]
            }
            """;
        var secondContent = $$"""
            {
              "skill": [
                { "name": "Origin Refresh {{token}}", "source": "{{sourceCode}}", "ability": "wis" }
              ]
            }
            """;

        var handler = new MutableContentHandler(firstContent);
        using var httpClient = new HttpClient(handler);
        var grants = new SourceGrantService(db);
        var sources = new CurrentUserSourceService(
            db,
            new SourceImportService(db),
            grants,
            httpClient);
        var url = $"https://8.8.8.8/origin-refresh-{token}.json";

        Guid? packageId = null;
        try
        {
            var first = await sources.AddAsync(
                userId,
                new AddCurrentUserSourceRequest(
                    CurrentUserSourceKinds.Web,
                    Url: url));

            packageId = first.SourcePackageId;
            handler.Content = secondContent;
            var refreshed = await sources.RefreshAsync(userId, first.Id);

            Assert.NotNull(refreshed);
            Assert.Equal(first.Id, refreshed!.Id);
            Assert.Equal(first.SourcePackageId, refreshed.SourcePackageId);

            var entity = await db.SourceEntities
                .AsNoTracking()
                .SingleAsync(value =>
                    value.SourcePackageId == first.SourcePackageId
                    && value.Name == $"Origin Refresh {token}");
            var revisions = await db.SourceEntityRevisions
                .AsNoTracking()
                .Where(value => value.SourceEntityId == entity.Id)
                .OrderBy(value => value.RevisionNumber)
                .ToArrayAsync();
            Assert.Equal(2, revisions.Length);
            Assert.Equal(1, revisions[0].RevisionNumber);
            Assert.Equal(2, revisions[1].RevisionNumber);
            Assert.NotEqual(revisions[0].Fingerprint, revisions[1].Fingerprint);
        }
        finally
        {
            if (packageId.HasValue)
            {
                await db.SourcePackages
                    .Where(value => value.Id == packageId.Value)
                    .ExecuteDeleteAsync();
            }
        }
    }

    [Fact]
    public async Task DueRegistrationsWithSameUnchangedUrlShareOneProbeAndSkipContentRefresh()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore"))) return;

        await using var factory = new WebApplicationFactory<Program>();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
        var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
        var userSources = new CurrentUserSourceService(db, importer, grants);

        var token = Guid.NewGuid().ToString("N")[..10];
        var sourceCode = $"REFRESH{token}".ToUpperInvariant();
        var content = $$"""
            {
              "skill": [
                { "name": "Refresh Test {{token}}", "source": "{{sourceCode}}", "ability": "int" }
              ]
            }
            """;
        var first = await userSources.AddAsync(
            $"refresh-user-a-{token}",
            new AddCurrentUserSourceRequest(
                CurrentUserSourceKinds.Upload,
                FileName: "refresh.json",
                Json: content));
        var second = await userSources.AddAsync(
            $"refresh-user-b-{token}",
            new AddCurrentUserSourceRequest(
                CurrentUserSourceKinds.Upload,
                FileName: "refresh.json",
                Json: content));

        const string url = "https://8.8.8.8/rules-core-refresh-test";
        const string etag = "\"stable-v1\"";
        var upstreamVersion = $"http:{CanonicalSourceIdentity.Fingerprint($"{etag}\n")}";
        var previousCheck = DateTimeOffset.UtcNow - TimeSpan.FromHours(25);
        var previousRefresh = first.RefreshedAt;

        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($$"""
                UPDATE current_user_source
                SET source_kind = 'web',
                    source_url = {{url}},
                    upstream_version = {{upstreamVersion}},
                    last_checked_at = {{previousCheck}}
                WHERE current_user_source_id IN ({{first.Id}}, {{second.Id}});
                """);

            var handler = new CountingHeadHandler(etag);
            using var httpClient = new HttpClient(handler);
            var refresh = new CurrentUserWebSourceRefreshService(
                db,
                importer,
                grants,
                httpClient);

            var refreshedCount = await refresh.RefreshDueAsync();

            Assert.Equal(0, refreshedCount);
            Assert.Equal(1, handler.RequestCount);

            var state = await ReadRefreshStateAsync(db, [first.Id, second.Id]);
            Assert.Equal(2, state.Count);
            Assert.All(state, value =>
            {
                Assert.Equal(upstreamVersion, value.UpstreamVersion);
                Assert.NotNull(value.LastCheckedAt);
                Assert.True(value.LastCheckedAt > previousCheck);
                Assert.Null(value.LastRefreshError);
            });

            var current = await userSources.ListAsync($"refresh-user-a-{token}");
            Assert.Equal(previousRefresh, Assert.Single(current).RefreshedAt);
        }
        finally
        {
            var packageIds = new[] { first.SourcePackageId, second.SourcePackageId };
            var packages = await db.SourcePackages
                .Where(value => packageIds.Contains(value.Id))
                .ToArrayAsync();
            if (packages.Length > 0)
            {
                db.SourcePackages.RemoveRange(packages);
                await db.SaveChangesAsync();
            }
        }
    }

    private static async Task<IReadOnlyList<RefreshState>> ReadRefreshStateAsync(
        RulesCoreDbContext db,
        IReadOnlyCollection<Guid> sourceIds)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT upstream_version, last_checked_at, last_refresh_error
                FROM current_user_source
                WHERE current_user_source_id = ANY(@ids)
                ORDER BY current_user_source_id;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@ids";
            parameter.Value = sourceIds.ToArray();
            command.Parameters.Add(parameter);

            var rows = new List<RefreshState>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new RefreshState(
                    reader.IsDBNull(0) ? null : reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2)));
            }
            return rows;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private sealed record RefreshState(
        string? UpstreamVersion,
        DateTimeOffset? LastCheckedAt,
        string? LastRefreshError);

    private sealed class MutableContentHandler(string content) : HttpMessageHandler
    {
        public string Content { get; set; } = content;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Content),
                RequestMessage = request
            };
            response.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/json");
            return Task.FromResult(response);
        }
    }

    private sealed class CountingHeadHandler(string etag) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            Assert.Equal(HttpMethod.Head, request.Method);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty),
                RequestMessage = request
            };
            response.Headers.ETag = new EntityTagHeaderValue(etag);
            return Task.FromResult(response);
        }
    }
}
