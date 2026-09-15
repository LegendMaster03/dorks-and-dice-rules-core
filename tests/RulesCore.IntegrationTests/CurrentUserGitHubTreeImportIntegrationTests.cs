using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CurrentUserGitHubTreeImportIntegrationTests
{
    private const string CommitSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string TreeSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string SourceUrl = "https://github.com/snapshot-owner/snapshot-repo/tree/main/data";

    [Fact]
    public async Task GitHubTreeImportPinsCommitAndPreservesRepositoryPaths()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore"))) return;

        await using var factory = new WebApplicationFactory<Program>();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        var importer = scope.ServiceProvider.GetRequiredService<INormalizedSourceImportService>();
        var adapters = scope.ServiceProvider.GetRequiredService<ISourceFormatAdapterRegistry>();
        var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();

        var handler = new GitHubSnapshotHandler();
        using var httpClient = new HttpClient(handler);
        var service = new CurrentUserSourceService(
            db,
            importer,
            adapters,
            grants,
            httpClient);
        var userId = $"github-snapshot-{Guid.NewGuid():N}";

        CurrentUserSourceView? source = null;
        try
        {
            source = await service.AddAsync(
                userId,
                new AddCurrentUserSourceRequest(
                    CurrentUserSourceKinds.Web,
                    Url: SourceUrl));

            Assert.Equal(1, source.EntityCount);
            Assert.Equal(["PHB"], source.SourceCodes);

            var representation = Assert.Single(await db.SourceRepresentations
                .AsNoTracking()
                .Where(value => value.SourcePackageId == source.SourcePackageId)
                .ToArrayAsync());
            Assert.Equal("data/class/class-sorcerer.json", representation.FileName);
            Assert.Equal(
                $"https://raw.githubusercontent.com/snapshot-owner/snapshot-repo/{CommitSha}/data/class/class-sorcerer.json",
                representation.SourceUri);

            Assert.Contains(
                handler.Requests,
                value => value.AbsolutePath == "/repos/snapshot-owner/snapshot-repo/commits/main");
            Assert.Contains(
                handler.Requests,
                value => value.AbsolutePath == $"/repos/snapshot-owner/snapshot-repo/git/trees/{TreeSha}");
            Assert.Contains(
                handler.Requests,
                value => value.AbsolutePath == $"/snapshot-owner/snapshot-repo/{CommitSha}/data/class/class-sorcerer.json");
            Assert.Contains(
                handler.Requests,
                value => value.AbsolutePath == $"/snapshot-owner/snapshot-repo/{CommitSha}/data/generated/generated-class.json");
            Assert.DoesNotContain(
                handler.Requests,
                value => value.Host == "raw.githubusercontent.com"
                    && value.AbsolutePath.Contains("/main/", StringComparison.Ordinal));
        }
        finally
        {
            if (source is not null)
            {
                var package = await db.SourcePackages
                    .SingleOrDefaultAsync(value => value.Id == source.SourcePackageId);
                if (package is not null)
                {
                    db.SourcePackages.Remove(package);
                    await db.SaveChangesAsync();
                }
            }
        }
    }

    private sealed class GitHubSnapshotHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? throw new InvalidOperationException("Request URI is required.");
            Requests.Add(uri);

            if (uri.Host == "api.github.com"
                && uri.AbsolutePath == "/repos/snapshot-owner/snapshot-repo/commits/main")
            {
                return Json(request, $$"""
                    {
                      "sha": "{{CommitSha}}",
                      "commit": {
                        "tree": {
                          "sha": "{{TreeSha}}"
                        }
                      }
                    }
                    """);
            }

            if (uri.Host == "api.github.com"
                && uri.AbsolutePath == $"/repos/snapshot-owner/snapshot-repo/git/trees/{TreeSha}")
            {
                return Json(request, """
                    {
                      "truncated": false,
                      "tree": [
                        {
                          "path": "data/class/class-sorcerer.json",
                          "type": "blob"
                        },
                        {
                          "path": "data/generated/generated-class.json",
                          "type": "blob"
                        }
                      ]
                    }
                    """);
            }

            if (uri.Host == "raw.githubusercontent.com"
                && uri.AbsolutePath == $"/snapshot-owner/snapshot-repo/{CommitSha}/data/class/class-sorcerer.json")
            {
                return Json(request, """
                    {
                      "class": [
                        {
                          "name": "Sorcerer",
                          "source": "PHB",
                          "edition": "classic",
                          "page": 99
                        }
                      ]
                    }
                    """);
            }

            if (uri.Host == "raw.githubusercontent.com"
                && uri.AbsolutePath == $"/snapshot-owner/snapshot-repo/{CommitSha}/data/generated/generated-class.json")
            {
                return Json(request, """
                    {
                      "class": [
                        {
                          "name": "Sorcerer",
                          "source": "PHB",
                          "page": 99
                        }
                      ]
                    }
                    """);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("not found"),
                RequestMessage = request
            });
        }

        private static Task<HttpResponseMessage> Json(
            HttpRequestMessage request,
            string json) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
                RequestMessage = request
            });
    }
}
