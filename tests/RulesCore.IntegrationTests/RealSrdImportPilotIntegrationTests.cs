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
using RulesCore.Web;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class RealSrdImportPilotIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task RealSrdActionPairSurvivesPartitioningAndCrossEditionDetection()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "real-srd",
            "real-srd-actions-pilot.json");
        var aggregateJson = await File.ReadAllTextAsync(fixturePath);

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["dev-ticket"] = Context("real-srd-pilot-dev", ["Dev"]),
            ["rules-ticket"] = Context("real-srd-pilot-rules", ["Rules Lawyer"])
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"real-srd-pilot-{token}";
        Guid packageId = Guid.Empty;
        Guid conceptId = Guid.Empty;

        try
        {
            SourceImportResult srd51;
            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/source-admin/import",
                       "dev-ticket",
                       ImportRequest(
                           packageKey,
                           token,
                           "srd-5-1",
                           "System Reference Document 5.1",
                           "5e",
                           aggregateJson,
                           ["SRD51"])))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                srd51 = (await response.Content.ReadFromJsonAsync<SourceImportResult>())!;
                packageId = srd51.PackageId;
                Assert.Equal("5e", srd51.GameEdition);
                var attack = Assert.Single(srd51.Entities);
                Assert.Equal("Attack", attack.Name);
                Assert.Equal("SRD51", attack.SourceCode);
            }

            SourceImportResult srd52;
            using (var request = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/source-admin/import",
                       "dev-ticket",
                       ImportRequest(
                           packageKey,
                           token,
                           "srd-5-2-1",
                           "System Reference Document 5.2.1",
                           "5.5e",
                           aggregateJson,
                           ["SRD52"])))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                srd52 = (await response.Content.ReadFromJsonAsync<SourceImportResult>())!;
                Assert.Equal(packageId, srd52.PackageId);
                Assert.Equal("5.5e", srd52.GameEdition);
                var attack = Assert.Single(srd52.Entities);
                Assert.Equal("Attack", attack.Name);
                Assert.Equal("SRD52", attack.SourceCode);
            }

            var srd51EntityId = Assert.Single(srd51.Entities).EntityId;
            var srd52EntityId = Assert.Single(srd52.Entities).EntityId;

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var oldRaw = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == srd51EntityId)
                    .Select(value => value.RawJson)
                    .SingleAsync();
                var newRaw = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == srd52EntityId)
                    .Select(value => value.RawJson)
                    .SingleAsync();

                Assert.Contains("Attack|SRD52", oldRaw, StringComparison.Ordinal);
                Assert.Contains("{@book Making an Attack|srd51|9|making an attack}", oldRaw, StringComparison.Ordinal);
                Assert.Contains("{@variantrule Unarmed Strike|srd52}", newRaw, StringComparison.Ordinal);
                Assert.Contains("Moving Between Attacks", newRaw, StringComparison.Ordinal);
                Assert.DoesNotContain("SRD51", newRaw, StringComparison.Ordinal);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var rules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                conceptId = (await rules.CreateConceptAsync(
                    new CreateRuleConceptRequest(
                        $"action.attack-real-srd-{token}",
                        "action",
                        "Attack"),
                    "real-srd-pilot-rules")).Value.Id;
                await rules.BindSourceEntityAsync(
                    conceptId,
                    new BindRuleConceptSourceRequest(srd52EntityId),
                    "real-srd-pilot-rules");
            }

            using (var request = HostedRequest(
                       HttpMethod.Get,
                       $"/api/global/rules/versioning/entities/{srd51EntityId}/candidates",
                       "rules-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var detection = (await response.Content.ReadFromJsonAsync<SourceVersionDetectionView>())!;
                var candidate = Assert.Single(
                    detection.Candidates,
                    value => value.Candidate.SourceEntityId == srd52EntityId);
                Assert.Equal(100, candidate.Confidence);
                Assert.Contains(
                    candidate.Reasons,
                    value => value.Contains("Explicit source metadata", StringComparison.Ordinal));
                Assert.Contains(
                    candidate.Reasons,
                    value => value.Contains("Cross-edition candidate: 5e -> 5.5e", StringComparison.Ordinal));
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                Assert.False(await db.RuleConceptSourceBindings.AnyAsync(
                    value => value.RuleConceptId == conceptId && value.SourceEntityId == srd51EntityId));
                Assert.True(await db.RuleConceptSourceBindings.AnyAsync(
                    value => value.RuleConceptId == conceptId && value.SourceEntityId == srd52EntityId));
            }
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            if (conceptId != Guid.Empty)
            {
                await db.RuleConceptSourceBindings
                    .Where(value => value.RuleConceptId == conceptId)
                    .ExecuteDeleteAsync();
                await db.RuleConcepts
                    .Where(value => value.Id == conceptId)
                    .ExecuteDeleteAsync();
            }
            if (packageId != Guid.Empty)
            {
                var package = await db.SourcePackages.FindAsync(packageId);
                if (package is not null)
                {
                    db.SourcePackages.Remove(package);
                    await db.SaveChangesAsync();
                }
            }
        }
    }

    private static SourceAdminImportRequest ImportRequest(
        string packageKey,
        string token,
        string workKey,
        string workDisplayName,
        string gameEdition,
        string json,
        IReadOnlyList<string> includedSourceCodes) =>
        new(
            packageKey,
            $"Real SRD Pilot {token}",
            "CoolFireGiant/hewnhero-srd",
            "CC-BY-4.0",
            true,
            workKey,
            workDisplayName,
            "cc-release",
            "CC release",
            json,
            gameEdition,
            "srd",
            null,
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
        T body)
    {
        var request = HostedRequest(method, path, ticket);
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
