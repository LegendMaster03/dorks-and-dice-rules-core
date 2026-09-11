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
using RulesCore.Web;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class MixedSourceAggregateImportIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task MixedSourceAggregateCanBePartitionedIntoDistinctLogicalReleases()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["dev-ticket"] = Context("aggregate-import-dev", ["Dev"])
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"aggregate-source-{token}";
        Guid packageId = Guid.Empty;

        const string aggregateJson = """
            {
              "_meta": { "sources": ["SRD51", "SRD52"] },
              "action": [
                {
                  "name": "Attack",
                  "source": "SRD51",
                  "page": 192,
                  "reprintedAs": ["Attack|SRD52"],
                  "time": [{ "number": 1, "unit": "action" }],
                  "entries": ["Legacy attack text."]
                },
                {
                  "name": "Attack",
                  "source": "SRD52",
                  "page": 361,
                  "time": [{ "number": 1, "unit": "action" }],
                  "entries": [
                    "Updated attack text.",
                    {
                      "type": "entries",
                      "name": "Moving Between Attacks",
                      "entries": ["Nested rule text."]
                    }
                  ]
                },
                {
                  "name": "Magic",
                  "source": "SRD52",
                  "page": 367,
                  "time": [{ "number": 1, "unit": "action" }],
                  "entries": ["Updated magic action text."]
                }
              ]
            }
            """;

        try
        {
            var unfiltered = Request(
                packageKey,
                token,
                "aggregate-probe",
                "Aggregate probe",
                "mixed",
                "Mixed aggregate",
                "5e",
                aggregateJson,
                includedSourceCodes: null);
            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/source-admin/import/preview",
                       "dev-ticket",
                       unfiltered))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var preview = (await response.Content.ReadFromJsonAsync<SourceImportPreviewResult>())!;
                Assert.Equal(3, preview.EntityCount);
                Assert.Contains(
                    preview.Warnings,
                    value => value.Contains("multiple source codes", StringComparison.OrdinalIgnoreCase));
            }

            var srd51 = Request(
                packageKey,
                token,
                "srd-5-1",
                "System Reference Document 5.1",
                "original",
                "SRD 5.1 release",
                "5e",
                aggregateJson,
                ["srd51"]);
            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/source-admin/import/preview",
                       "dev-ticket",
                       srd51))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var preview = (await response.Content.ReadFromJsonAsync<SourceImportPreviewResult>())!;
                var entity = Assert.Single(preview.Entities);
                Assert.Equal("Attack", entity.Name);
                Assert.Equal("SRD51", entity.SourceCode);
                Assert.Contains(
                    preview.Warnings,
                    value => value.Contains("partition active", StringComparison.OrdinalIgnoreCase));
            }

            SourceImportResult srd51Import;
            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/source-admin/import",
                       "dev-ticket",
                       srd51))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                srd51Import = (await response.Content.ReadFromJsonAsync<SourceImportResult>())!;
                packageId = srd51Import.PackageId;
                Assert.Equal("5e", srd51Import.GameEdition);
                Assert.Equal("SRD51", Assert.Single(srd51Import.Entities).SourceCode);
            }

            var srd52 = Request(
                packageKey,
                token,
                "srd-5-2-1",
                "System Reference Document 5.2.1",
                "current",
                "SRD 5.2.1 release",
                "5.5e",
                aggregateJson,
                ["SRD52"]);
            SourceImportResult srd52Import;
            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/source-admin/import",
                       "dev-ticket",
                       srd52))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                srd52Import = (await response.Content.ReadFromJsonAsync<SourceImportResult>())!;
                Assert.Equal(srd51Import.PackageId, srd52Import.PackageId);
                Assert.Equal("5.5e", srd52Import.GameEdition);
                Assert.Equal(2, srd52Import.Entities.Count);
                Assert.All(srd52Import.Entities, value => Assert.Equal("SRD52", value.SourceCode));
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var works = await db.SourceWorks
                    .Where(value => value.SourcePackageId == packageId)
                    .OrderBy(value => value.Key)
                    .ToArrayAsync();
                Assert.Equal(2, works.Length);
                Assert.Contains(works, value => value.Key == "srd-5-1");
                Assert.Contains(works, value => value.Key == "srd-5-2-1");

                var srd52AttackId = srd52Import.Entities.Single(value => value.Name == "Attack").EntityId;
                var rawJson = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == srd52AttackId)
                    .Select(value => value.RawJson)
                    .SingleAsync();
                using var raw = JsonDocument.Parse(rawJson);
                Assert.Equal("SRD52", raw.RootElement.GetProperty("source").GetString());
                Assert.Equal(
                    "Moving Between Attacks",
                    raw.RootElement.GetProperty("entries")[1].GetProperty("name").GetString());
            }

            var missingFilter = Request(
                packageKey,
                token,
                "unused",
                "Unused",
                "unused",
                "Unused",
                "5e",
                aggregateJson,
                ["DOES-NOT-EXIST"]);
            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/source-admin/import/preview",
                       "dev-ticket",
                       missingFilter))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
        }
        finally
        {
            if (packageId != Guid.Empty)
            {
                await using var scope = factory.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var package = await db.SourcePackages.FindAsync(packageId);
                if (package is not null)
                {
                    db.SourcePackages.Remove(package);
                    await db.SaveChangesAsync();
                }
            }
        }
    }

    private static SourceAdminImportRequest Request(
        string packageKey,
        string token,
        string workKey,
        string workDisplayName,
        string releaseKey,
        string releaseDisplayName,
        string gameEdition,
        string json,
        IReadOnlyList<string>? includedSourceCodes) =>
        new(
            packageKey,
            $"Aggregate Source {token}",
            "integration-test",
            "test-only",
            true,
            workKey,
            workDisplayName,
            releaseKey,
            releaseDisplayName,
            json,
            gameEdition,
            "srd",
            new DateOnly(2025, 5, 1),
            includedSourceCodes);

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

    private static HttpRequestMessage HostedJsonRequest<T>(
        HttpMethod method,
        string path,
        string ticket,
        T body)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(ToolHostAuthenticationHeaders.Ticket, ticket);
        request.Headers.Add(ToolHostAuthenticationHeaders.IntrospectionPath, IntrospectionPath);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private static ToolHostAuthenticationContext Context(
        string userId,
        IReadOnlyList<string> globalRoles) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: "dorks-and-dice",
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
