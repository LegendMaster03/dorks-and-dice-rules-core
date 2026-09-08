using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
public sealed class GlobalRulesLayerIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task RulesLawyerCanPublishPinnedSourceRevisionAndReimportDoesNotFloatRuleset()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["lawyer-ticket"] = Context("rules-lawyer", "Rules Lawyer", "dorks-and-dice", ["Rules Lawyer"]),
            ["nonlawyer-ticket"] = Context("ordinary-user", "Ordinary User", "dorks-and-dice", []),
            ["wrong-mode-ticket"] = Context("rules-lawyer", "Rules Lawyer", "professional", ["Rules Lawyer"])
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var packageKey = $"global-rules-public-{Guid.NewGuid():N}";
        var conceptKey = $"skill.arcana.{Guid.NewGuid():N}";
        Guid packageId = Guid.Empty;

        try
        {
            Guid sourceEntityId;
            Guid sourceRevision1Id;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var imported = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(packageKey, isPublic: true, versionMarker: "one"));
                packageId = imported.PackageId;
                sourceEntityId = imported.Entities.Single().EntityId;
                sourceRevision1Id = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == sourceEntityId && value.RevisionNumber == 1)
                    .Select(value => value.Id)
                    .SingleAsync();
            }

            using (var anonymousCreate = new HttpRequestMessage(HttpMethod.Post, "/api/global/rules/concepts")
            {
                Content = JsonContent.Create(new CreateRuleConceptRequest(conceptKey, "skill", "Arcana"))
            })
            using (var anonymousResponse = await client.SendAsync(anonymousCreate))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
            }

            using (var nonlawyerCreate = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/global/rules/concepts",
                       "nonlawyer-ticket",
                       new CreateRuleConceptRequest(conceptKey, "skill", "Arcana")))
            using (var nonlawyerResponse = await client.SendAsync(nonlawyerCreate))
            {
                Assert.Equal(HttpStatusCode.Forbidden, nonlawyerResponse.StatusCode);
            }

            using (var wrongModeCreate = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/global/rules/concepts",
                       "wrong-mode-ticket",
                       new CreateRuleConceptRequest(conceptKey, "skill", "Arcana")))
            using (var wrongModeResponse = await client.SendAsync(wrongModeCreate))
            {
                Assert.Equal(HttpStatusCode.Forbidden, wrongModeResponse.StatusCode);
            }

            RuleConceptView concept;
            using (var createRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/global/rules/concepts",
                       "lawyer-ticket",
                       new CreateRuleConceptRequest(conceptKey, "skill", "Arcana")))
            using (var createResponse = await client.SendAsync(createRequest))
            {
                Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
                concept = (await createResponse.Content.ReadFromJsonAsync<RuleConceptView>())!;
                Assert.Equal(conceptKey, concept.Key);
                Assert.Equal("rules-lawyer", concept.CreatedByUserId);
            }

            using (var bindRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/concepts/{concept.Id}/bindings",
                       "lawyer-ticket",
                       new BindRuleConceptSourceRequest(sourceEntityId)))
            using (var bindResponse = await client.SendAsync(bindRequest))
            {
                Assert.Equal(HttpStatusCode.Created, bindResponse.StatusCode);
                var bindingJson = await bindResponse.Content.ReadAsStringAsync();
                using var bindingDocument = JsonDocument.Parse(bindingJson);
                Assert.Equal(sourceEntityId, bindingDocument.RootElement.GetProperty("sourceEntityId").GetGuid());
                Assert.False(bindingDocument.RootElement.TryGetProperty("sourceEntityName", out _));
                Assert.False(bindingDocument.RootElement.TryGetProperty("sourceCode", out _));
            }

            using (var decisionRequest = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/global/rules/concepts/{concept.Id}/decision",
                       "lawyer-ticket",
                       new SetGlobalRuleDecisionRequest(sourceRevision1Id, "Use the imported Arcana implementation.")))
            using (var decisionResponse = await client.SendAsync(decisionRequest))
            {
                Assert.Equal(HttpStatusCode.OK, decisionResponse.StatusCode);
                var decision = await decisionResponse.Content
                    .ReadFromJsonAsync<RuleMutationResult<GlobalRuleDecisionView>>();
                Assert.NotNull(decision);
                Assert.True(decision.Created);
                Assert.Equal(1, decision.Value.DecisionNumber);
                Assert.Equal(sourceRevision1Id, decision.Value.SourceEntityRevisionId);
            }

            PublishedRulesetRevisionView firstPublication;
            using (var publishRequest = HostedRequest(HttpMethod.Post, "/api/global/rules/publish", "lawyer-ticket"))
            using (var publishResponse = await client.SendAsync(publishRequest))
            {
                Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
                firstPublication = (await publishResponse.Content.ReadFromJsonAsync<PublishedRulesetRevisionView>())!;
                Assert.Equal(1, firstPublication.RevisionNumber);
                Assert.True(firstPublication.CreatedRevision);
                Assert.Equal(1, firstPublication.EntryCount);
            }

            using (var resolvedResponse = await client.GetAsync($"/api/rules/{conceptKey}"))
            {
                Assert.Equal(HttpStatusCode.OK, resolvedResponse.StatusCode);
                var resolved = (await resolvedResponse.Content.ReadFromJsonAsync<ResolvedRuleView>())!;
                Assert.Equal(1, resolved.RulesetRevisionNumber);
                Assert.Equal(1, resolved.SourceRevisionNumber);
                Assert.Equal("one", resolved.Document.GetProperty("versionMarker").GetString());
            }

            Guid sourceRevision2Id;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var imported = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(packageKey, isPublic: true, versionMarker: "two"));
                Assert.Equal(2, imported.Entities.Single().RevisionNumber);
                sourceRevision2Id = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == sourceEntityId && value.RevisionNumber == 2)
                    .Select(value => value.Id)
                    .SingleAsync();
            }

            using (var stillPinnedResponse = await client.GetAsync($"/api/rules/{conceptKey}"))
            {
                Assert.Equal(HttpStatusCode.OK, stillPinnedResponse.StatusCode);
                var resolved = (await stillPinnedResponse.Content.ReadFromJsonAsync<ResolvedRuleView>())!;
                Assert.Equal(1, resolved.RulesetRevisionNumber);
                Assert.Equal(sourceRevision1Id, resolved.SourceEntityRevisionId);
                Assert.Equal("one", resolved.Document.GetProperty("versionMarker").GetString());
            }

            using (var decisionRequest = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/global/rules/concepts/{concept.Id}/decision",
                       "lawyer-ticket",
                       new SetGlobalRuleDecisionRequest(sourceRevision2Id, "Adopt the new source revision.")))
            using (var decisionResponse = await client.SendAsync(decisionRequest))
            {
                Assert.Equal(HttpStatusCode.OK, decisionResponse.StatusCode);
                var decision = await decisionResponse.Content
                    .ReadFromJsonAsync<RuleMutationResult<GlobalRuleDecisionView>>();
                Assert.NotNull(decision);
                Assert.True(decision.Created);
                Assert.Equal(2, decision.Value.DecisionNumber);
            }

            PublishedRulesetRevisionView secondPublication;
            using (var publishRequest = HostedRequest(HttpMethod.Post, "/api/global/rules/publish", "lawyer-ticket"))
            using (var publishResponse = await client.SendAsync(publishRequest))
            {
                Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
                secondPublication = (await publishResponse.Content.ReadFromJsonAsync<PublishedRulesetRevisionView>())!;
                Assert.Equal(2, secondPublication.RevisionNumber);
                Assert.True(secondPublication.CreatedRevision);
                Assert.NotEqual(firstPublication.Fingerprint, secondPublication.Fingerprint);
            }

            using (var resolvedResponse = await client.GetAsync($"/api/rules/{conceptKey}"))
            {
                Assert.Equal(HttpStatusCode.OK, resolvedResponse.StatusCode);
                var resolved = (await resolvedResponse.Content.ReadFromJsonAsync<ResolvedRuleView>())!;
                Assert.Equal(2, resolved.RulesetRevisionNumber);
                Assert.Equal(sourceRevision2Id, resolved.SourceEntityRevisionId);
                Assert.Equal(2, resolved.SourceRevisionNumber);
                Assert.Equal("two", resolved.Document.GetProperty("versionMarker").GetString());
            }

            using (var repeatPublishRequest = HostedRequest(
                       HttpMethod.Post,
                       "/api/global/rules/publish",
                       "lawyer-ticket"))
            using (var repeatPublishResponse = await client.SendAsync(repeatPublishRequest))
            {
                Assert.Equal(HttpStatusCode.OK, repeatPublishResponse.StatusCode);
                var repeated = (await repeatPublishResponse.Content
                    .ReadFromJsonAsync<PublishedRulesetRevisionView>())!;
                Assert.Equal(2, repeated.RevisionNumber);
                Assert.False(repeated.CreatedRevision);
                Assert.Equal(secondPublication.Id, repeated.Id);
            }
        }
        finally
        {
            await CleanupAsync(factory, packageId);
        }
    }

    [Fact]
    public async Task SourceGrantAndRulesLawyerAuthorityRemainIndependent()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["lawyer-ticket"] = Context("rules-lawyer", "Rules Lawyer", "dorks-and-dice", ["Rules Lawyer"]),
            ["reader-ticket"] = Context("granted-reader", "Granted Reader", "dorks-and-dice", [])
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var packageKey = $"global-rules-private-{Guid.NewGuid():N}";
        var conceptKey = $"skill.private-arcana.{Guid.NewGuid():N}";
        Guid packageId = Guid.Empty;

        try
        {
            Guid sourceEntityId;
            Guid sourceRevisionId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var imported = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(packageKey, isPublic: false, versionMarker: "restricted"));
                packageId = imported.PackageId;
                sourceEntityId = imported.Entities.Single().EntityId;
                sourceRevisionId = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == sourceEntityId)
                    .Select(value => value.Id)
                    .SingleAsync();
                Assert.False(await grants.HasGrantAsync("rules-lawyer", packageId));
            }

            RuleConceptView concept;
            using (var createRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/global/rules/concepts",
                       "lawyer-ticket",
                       new CreateRuleConceptRequest(conceptKey, "skill", "Private Arcana")))
            using (var createResponse = await client.SendAsync(createRequest))
            {
                Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
                concept = (await createResponse.Content.ReadFromJsonAsync<RuleConceptView>())!;
            }

            using (var bindRequest = HostedJsonRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/concepts/{concept.Id}/bindings",
                       "lawyer-ticket",
                       new BindRuleConceptSourceRequest(sourceEntityId)))
            using (var bindResponse = await client.SendAsync(bindRequest))
            {
                Assert.Equal(HttpStatusCode.Created, bindResponse.StatusCode);
                var body = await bindResponse.Content.ReadAsStringAsync();
                Assert.DoesNotContain("Private Arcana", body, StringComparison.Ordinal);
                Assert.DoesNotContain("PRIVATE", body, StringComparison.Ordinal);
            }

            using (var decisionRequest = HostedJsonRequest(
                       HttpMethod.Put,
                       $"/api/global/rules/concepts/{concept.Id}/decision",
                       "lawyer-ticket",
                       new SetGlobalRuleDecisionRequest(sourceRevisionId, "Select restricted implementation metadata.")))
            using (var decisionResponse = await client.SendAsync(decisionRequest))
            {
                Assert.Equal(HttpStatusCode.OK, decisionResponse.StatusCode);
            }

            using (var publishRequest = HostedRequest(HttpMethod.Post, "/api/global/rules/publish", "lawyer-ticket"))
            using (var publishResponse = await client.SendAsync(publishRequest))
            {
                Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
            }

            using (var lawyerRead = HostedRequest(HttpMethod.Get, $"/api/rules/{conceptKey}", "lawyer-ticket"))
            using (var lawyerReadResponse = await client.SendAsync(lawyerRead))
            {
                Assert.Equal(HttpStatusCode.NotFound, lawyerReadResponse.StatusCode);
            }

            using (var anonymousReadResponse = await client.GetAsync($"/api/rules/{conceptKey}"))
            {
                Assert.Equal(HttpStatusCode.NotFound, anonymousReadResponse.StatusCode);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
                await grants.GrantAsync("granted-reader", packageId);
                Assert.True(await grants.HasGrantAsync("granted-reader", packageId));
            }

            using (var readerRead = HostedRequest(HttpMethod.Get, $"/api/rules/{conceptKey}", "reader-ticket"))
            using (var readerReadResponse = await client.SendAsync(readerRead))
            {
                Assert.Equal(HttpStatusCode.OK, readerReadResponse.StatusCode);
                var resolved = (await readerReadResponse.Content.ReadFromJsonAsync<ResolvedRuleView>())!;
                Assert.Equal("restricted", resolved.Document.GetProperty("versionMarker").GetString());
            }

            using (var readerMutation = HostedJsonRequest(
                       HttpMethod.Post,
                       "/api/global/rules/concepts",
                       "reader-ticket",
                       new CreateRuleConceptRequest($"forbidden.{Guid.NewGuid():N}", "skill", "Forbidden")))
            using (var readerMutationResponse = await client.SendAsync(readerMutation))
            {
                Assert.Equal(HttpStatusCode.Forbidden, readerMutationResponse.StatusCode);
            }
        }
        finally
        {
            await CleanupAsync(factory, packageId);
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
        T body)
    {
        var request = HostedRequest(method, path, ticket);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private static ToolHostAuthenticationContext Context(
        string userId,
        string displayName,
        string siteMode,
        IReadOnlyList<string> globalRoles) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: siteMode,
            User: new ToolHostUserContext(userId, displayName),
            GlobalRoles: globalRoles,
            Campaigns: []);

    private static Import5eToolsDocumentRequest SourceRequest(
        string packageKey,
        bool isPublic,
        string versionMarker) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: "Global Rules Integration Package",
            Provider: "integration-test",
            License: "test-only",
            IsPublic: isPublic,
            WorkKey: "rules-work",
            WorkDisplayName: "Rules Work",
            EditionKey: "rules-edition",
            EditionDisplayName: "Rules Edition",
            Json: $$"""
                {
                  "skill": [
                    {
                      "name": "Arcana",
                      "source": "TST",
                      "ability": "int",
                      "versionMarker": "{{versionMarker}}"
                    }
                  ]
                }
                """);

    private static async Task CleanupAsync(
        WebApplicationFactory<Program> factory,
        Guid packageId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

        await db.RulesetRevisionEntries.ExecuteDeleteAsync();
        await db.RulesetRevisions.ExecuteDeleteAsync();
        await db.GlobalRuleDecisions.ExecuteDeleteAsync();
        await db.RuleConceptSourceBindings.ExecuteDeleteAsync();
        await db.RuleConcepts.ExecuteDeleteAsync();

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
