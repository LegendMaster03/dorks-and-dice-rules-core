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
public sealed class ResolvedRulesCatalogIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task CatalogsHideInaccessibleRulesAndAllowCampaignPlayersToBrowsePublishedRules()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var campaignId = Guid.NewGuid();
        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["granted-ticket"] = Context("granted-reader", campaignId: null, campaignRole: null),
            ["ungranted-ticket"] = Context("ungranted-reader", campaignId: null, campaignRole: null),
            ["player-granted-ticket"] = Context("player-granted", campaignId, "Player"),
            ["player-ungranted-ticket"] = Context("player-ungranted", campaignId, "Player"),
            ["outsider-ticket"] = Context("outsider", campaignId: null, campaignRole: null)
        });

        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var token = Guid.NewGuid().ToString("N")[..10];
        var publicPackageKey = $"browser-public-{token}";
        var privatePackageKey = $"browser-private-{token}";
        var publicConceptKey = $"skill.browser-public-{token}";
        var privateConceptKey = $"skill.browser-private-{token}";
        Guid publicPackageId = Guid.Empty;
        Guid privatePackageId = Guid.Empty;

        try
        {
            PublishedRulesetRevisionView globalRevision;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var campaignRules = scope.ServiceProvider.GetRequiredService<ICampaignRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

                var publicImport = await importer.Import5eToolsDocumentAsync(SourceRequest(
                    publicPackageKey,
                    $"Browser Public {token}",
                    "PUB",
                    isPublic: true));
                publicPackageId = publicImport.PackageId;
                var publicEntityId = publicImport.Entities.Single().EntityId;
                var publicRevisionId = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == publicEntityId)
                    .Select(value => value.Id)
                    .SingleAsync();

                var privateImport = await importer.Import5eToolsDocumentAsync(SourceRequest(
                    privatePackageKey,
                    $"Browser Private {token}",
                    "PRV",
                    isPublic: false));
                privatePackageId = privateImport.PackageId;
                var privateEntityId = privateImport.Entities.Single().EntityId;
                var privateRevisionId = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == privateEntityId)
                    .Select(value => value.Id)
                    .SingleAsync();

                await grants.GrantAsync("rules-lawyer", privatePackageId);
                await grants.GrantAsync("granted-reader", privatePackageId);
                await grants.GrantAsync("player-granted", privatePackageId);

                var publicConcept = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(publicConceptKey, "skill", $"Browser Public {token}"),
                    "rules-lawyer");
                await globalRules.BindSourceEntityAsync(
                    publicConcept.Value.Id,
                    new BindRuleConceptSourceRequest(publicEntityId),
                    "rules-lawyer");
                await globalRules.SetDecisionAsync(
                    publicConcept.Value.Id,
                    new SetGlobalRuleDecisionRequest(publicRevisionId, "Public browser rule."),
                    "rules-lawyer");

                var privateConcept = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(privateConceptKey, "skill", $"Browser Private {token}"),
                    "rules-lawyer");
                await globalRules.BindSourceEntityAsync(
                    privateConcept.Value.Id,
                    new BindRuleConceptSourceRequest(privateEntityId),
                    "rules-lawyer");
                await globalRules.SetDecisionAsync(
                    privateConcept.Value.Id,
                    new SetGlobalRuleDecisionRequest(privateRevisionId, "Private browser rule."),
                    "rules-lawyer");

                globalRevision = await globalRules.PublishAsync("rules-lawyer");
                await campaignRules.SelectBaselineAsync(
                    campaignId,
                    new SelectCampaignRulesetBaselineRequest(globalRevision.Id),
                    "campaign-dm");
                await campaignRules.PublishAsync(campaignId, "campaign-dm");
            }

            using (var directResponse = await client.GetAsync($"/api/rules?q={token}"))
            {
                Assert.Equal(HttpStatusCode.OK, directResponse.StatusCode);
                var raw = await directResponse.Content.ReadAsStringAsync();
                Assert.DoesNotContain("\"document\"", raw, StringComparison.OrdinalIgnoreCase);
                var catalog = await directResponse.Content.ReadFromJsonAsync<ResolvedRulesCatalogView>();
                Assert.NotNull(catalog);
                Assert.Equal(globalRevision.RevisionNumber, catalog.RevisionNumber);
                Assert.Equal(1, catalog.TotalCount);
                var publicFacet = Assert.Single(catalog.SourceFacets);
                Assert.Equal("PUB", publicFacet.SourceCode);
                Assert.Equal(1, publicFacet.Count);
                var rule = Assert.Single(catalog.Rules);
                Assert.Equal(publicConceptKey, rule.ConceptKey);
                Assert.Equal(publicPackageKey, rule.PackageKey);
                var ability = Assert.Single(rule.BrowserFields);
                Assert.Equal("ability", ability.Key);
                Assert.Equal("Ability", ability.Label);
                Assert.Equal("INT", ability.Value);
            }

            using (var publicVersions = await client.GetAsync(
                       $"/api/rules/{Uri.EscapeDataString(publicConceptKey)}/versions"))
            {
                Assert.Equal(HttpStatusCode.OK, publicVersions.StatusCode);
                Assert.Equal("no-store", publicVersions.Headers.CacheControl?.ToString());
                var versions = await publicVersions.Content.ReadFromJsonAsync<RuleConceptVersionsView>();
                Assert.NotNull(versions);
                Assert.Equal(publicConceptKey, versions.ConceptKey);
                var source = Assert.Single(versions.Versions);
                Assert.Equal(publicPackageKey, source.PackageKey);
                Assert.True(source.Document.TryGetProperty("secretMarker", out var marker));
                Assert.Equal("source-document-only", marker.GetString());
            }

            using (var deniedPrivateVersionsRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/rules/{Uri.EscapeDataString(privateConceptKey)}/versions",
                       "ungranted-ticket"))
            using (var deniedPrivateVersionsResponse = await client.SendAsync(deniedPrivateVersionsRequest))
            {
                Assert.Equal(HttpStatusCode.NotFound, deniedPrivateVersionsResponse.StatusCode);
            }

            using (var grantedPrivateVersionsRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/rules/{Uri.EscapeDataString(privateConceptKey)}/versions",
                       "granted-ticket"))
            using (var grantedPrivateVersionsResponse = await client.SendAsync(grantedPrivateVersionsRequest))
            {
                Assert.Equal(HttpStatusCode.OK, grantedPrivateVersionsResponse.StatusCode);
                var versions = await grantedPrivateVersionsResponse.Content
                    .ReadFromJsonAsync<RuleConceptVersionsView>();
                Assert.NotNull(versions);
                var source = Assert.Single(versions.Versions);
                Assert.Equal(privatePackageKey, source.PackageKey);
            }

            using (var grantedRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/rules?q={token}",
                       "granted-ticket"))
            using (var grantedResponse = await client.SendAsync(grantedRequest))
            {
                Assert.Equal(HttpStatusCode.OK, grantedResponse.StatusCode);
                Assert.Equal("no-store", grantedResponse.Headers.CacheControl?.ToString());
                var catalog = (await grantedResponse.Content
                    .ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                Assert.Equal(2, catalog.TotalCount);
                Assert.Equal(2, catalog.Rules.Count);
                Assert.Equal(2, catalog.SourceFacets.Count);
                Assert.Contains(catalog.SourceFacets, value => value.SourceCode == "PUB" && value.Count == 1);
                Assert.Contains(catalog.SourceFacets, value => value.SourceCode == "PRV" && value.Count == 1);
                Assert.Contains(catalog.Rules, value => value.ConceptKey == publicConceptKey);
                Assert.Contains(catalog.Rules, value => value.ConceptKey == privateConceptKey);
            }

            using (var sourceFilteredRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/rules?q={token}&source=PRV",
                       "granted-ticket"))
            using (var sourceFilteredResponse = await client.SendAsync(sourceFilteredRequest))
            {
                Assert.Equal(HttpStatusCode.OK, sourceFilteredResponse.StatusCode);
                var catalog = (await sourceFilteredResponse.Content
                    .ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                Assert.Equal(1, catalog.TotalCount);
                Assert.Equal(privateConceptKey, Assert.Single(catalog.Rules).ConceptKey);
                Assert.Equal(2, catalog.SourceFacets.Count);
            }

            using (var firstPageRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/rules?q={token}&limit=1&offset=0",
                       "granted-ticket"))
            using (var firstPageResponse = await client.SendAsync(firstPageRequest))
            using (var secondPageRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/rules?q={token}&limit=1&offset=1",
                       "granted-ticket"))
            using (var secondPageResponse = await client.SendAsync(secondPageRequest))
            {
                Assert.Equal(HttpStatusCode.OK, firstPageResponse.StatusCode);
                Assert.Equal(HttpStatusCode.OK, secondPageResponse.StatusCode);
                var firstPage = (await firstPageResponse.Content.ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                var secondPage = (await secondPageResponse.Content.ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                Assert.Equal(2, firstPage.TotalCount);
                Assert.Equal(2, secondPage.TotalCount);
                var first = Assert.Single(firstPage.Rules);
                var second = Assert.Single(secondPage.Rules);
                Assert.NotEqual(first.RuleConceptId, second.RuleConceptId);
                Assert.Equal(new[] { publicConceptKey, privateConceptKey }.OrderBy(value => value), new[] { first.ConceptKey, second.ConceptKey }.OrderBy(value => value));
            }

            using (var ungrantedRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/rules?q={token}",
                       "ungranted-ticket"))
            using (var ungrantedResponse = await client.SendAsync(ungrantedRequest))
            {
                Assert.Equal(HttpStatusCode.OK, ungrantedResponse.StatusCode);
                var catalog = (await ungrantedResponse.Content
                    .ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                var rule = Assert.Single(catalog.Rules);
                Assert.Equal(publicConceptKey, rule.ConceptKey);
            }

            using (var anonymousCampaign = await client.GetAsync(
                       $"/api/campaigns/{campaignId}/rules?q={token}"))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymousCampaign.StatusCode);
            }

            using (var outsiderRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules?q={token}",
                       "outsider-ticket"))
            using (var outsiderResponse = await client.SendAsync(outsiderRequest))
            {
                Assert.Equal(HttpStatusCode.NotFound, outsiderResponse.StatusCode);
            }

            using (var playerGrantedRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules?q={token}",
                       "player-granted-ticket"))
            using (var playerGrantedResponse = await client.SendAsync(playerGrantedRequest))
            {
                Assert.Equal(HttpStatusCode.OK, playerGrantedResponse.StatusCode);
                var raw = await playerGrantedResponse.Content.ReadAsStringAsync();
                Assert.DoesNotContain("\"document\"", raw, StringComparison.OrdinalIgnoreCase);
                var catalog = await playerGrantedResponse.Content
                    .ReadFromJsonAsync<ResolvedRulesCatalogView>();
                Assert.NotNull(catalog);
                Assert.Equal("campaign", catalog.Scope);
                Assert.Equal(campaignId, catalog.CampaignId);
                Assert.Equal(2, catalog.TotalCount);
                Assert.Equal(2, catalog.Rules.Count);
                Assert.All(catalog.Rules, value => Assert.False(value.HasCampaignOverride));
            }

            using (var overridesOnlyRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules?q={token}&overridesOnly=true",
                       "player-granted-ticket"))
            using (var overridesOnlyResponse = await client.SendAsync(overridesOnlyRequest))
            {
                Assert.Equal(HttpStatusCode.OK, overridesOnlyResponse.StatusCode);
                var catalog = (await overridesOnlyResponse.Content
                    .ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                Assert.Equal(0, catalog.TotalCount);
                Assert.Empty(catalog.Rules);
            }

            using (var campaignFirstPageRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules?q={token}&limit=1&offset=0",
                       "player-granted-ticket"))
            using (var campaignFirstPageResponse = await client.SendAsync(campaignFirstPageRequest))
            using (var campaignSecondPageRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules?q={token}&limit=1&offset=1",
                       "player-granted-ticket"))
            using (var campaignSecondPageResponse = await client.SendAsync(campaignSecondPageRequest))
            {
                Assert.Equal(HttpStatusCode.OK, campaignFirstPageResponse.StatusCode);
                Assert.Equal(HttpStatusCode.OK, campaignSecondPageResponse.StatusCode);
                var firstPage = (await campaignFirstPageResponse.Content.ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                var secondPage = (await campaignSecondPageResponse.Content.ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                Assert.Equal(2, firstPage.TotalCount);
                Assert.Equal(2, secondPage.TotalCount);
                Assert.NotEqual(Assert.Single(firstPage.Rules).RuleConceptId, Assert.Single(secondPage.Rules).RuleConceptId);
            }

            using (var playerUngrantedRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/rules?q={token}",
                       "player-ungranted-ticket"))
            using (var playerUngrantedResponse = await client.SendAsync(playerUngrantedRequest))
            {
                Assert.Equal(HttpStatusCode.OK, playerUngrantedResponse.StatusCode);
                var catalog = (await playerUngrantedResponse.Content
                    .ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                var rule = Assert.Single(catalog.Rules);
                Assert.Equal(publicConceptKey, rule.ConceptKey);
            }

            using (var filteredRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/rules?entityType=spell&q={token}",
                       "granted-ticket"))
            using (var filteredResponse = await client.SendAsync(filteredRequest))
            {
                Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
                var catalog = (await filteredResponse.Content
                    .ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                Assert.Equal(0, catalog.TotalCount);
                Assert.Empty(catalog.Rules);
            }
        }
        finally
        {
            await CleanupAsync(factory, publicPackageId, privatePackageId);
        }
    }

    [Fact]
    public async Task CatalogProjectsFamilySpecificBrowserFieldsWithoutExposingDocuments()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var authenticationClient = new FakeToolHostAuthenticationClient(
            new Dictionary<string, ToolHostAuthenticationContext>());
        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"browser-fields-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

                var imported = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                    PackageKey: packageKey,
                    PackageDisplayName: $"Browser Fields {token}",
                    Provider: "integration-test",
                    License: "test-only",
                    IsPublic: true,
                    WorkKey: "browser-fields-work",
                    WorkDisplayName: "Browser Fields Work",
                    EditionKey: "browser-fields-edition",
                    EditionDisplayName: "Browser Fields Edition",
                    Json: """
                        {
                          "monster": [
                            {
                              "name": "Browser Dragon __TOKEN__",
                              "source": "BROWSE",
                              "size": ["L"],
                              "type": "dragon",
                              "cr": "5"
                            }
                          ],
                          "spell": [
                            {
                              "name": "Browser Burst __TOKEN__",
                              "source": "BROWSE",
                              "level": 3,
                              "school": "V"
                            }
                          ],
                          "class": [
                            {
                              "name": "Browser Adept __TOKEN__",
                              "source": "BROWSE",
                              "hd": { "number": 1, "faces": 8 }
                            }
                          ],
                          "race": [
                            {
                              "name": "Browser Folk __TOKEN__",
                              "source": "BROWSE",
                              "size": ["M"],
                              "ability": [{ "dex": 2, "wis": 1 }]
                            }
                          ]
                        }
                        """.Replace("__TOKEN__", token, StringComparison.Ordinal)));
                packageId = imported.PackageId;

                foreach (var entity in imported.Entities)
                {
                    var source = await db.SourceEntities
                        .AsNoTracking()
                        .SingleAsync(value => value.Id == entity.EntityId);
                    var revisionId = await db.SourceEntityRevisions
                        .Where(value => value.SourceEntityId == entity.EntityId)
                        .Select(value => value.Id)
                        .SingleAsync();
                    var conceptKey = $"{source.EntityType}.browser-fields-{token}-{source.EntityType}";
                    var concept = await globalRules.CreateConceptAsync(
                        new CreateRuleConceptRequest(conceptKey, source.EntityType, source.Name),
                        "rules-lawyer");
                    await globalRules.BindSourceEntityAsync(
                        concept.Value.Id,
                        new BindRuleConceptSourceRequest(entity.EntityId),
                        "rules-lawyer");
                    await globalRules.SetDecisionAsync(
                        concept.Value.Id,
                        new SetGlobalRuleDecisionRequest(revisionId, "Browser field fixture."),
                        "rules-lawyer");
                }

                await globalRules.PublishAsync("rules-lawyer");
            }

            using var response = await client.GetAsync($"/api/rules?q={token}&limit=20");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var raw = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("\"document\"", raw, StringComparison.OrdinalIgnoreCase);
            var catalog = (await response.Content.ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
            Assert.Equal(4, catalog.TotalCount);
            Assert.Equal(4, catalog.Rules.Count);

            var monster = catalog.Rules.Single(value => value.EntityType == "monster");
            Assert.Equal("Dragon", BrowserField(monster, "type"));
            Assert.Equal("5", BrowserField(monster, "cr"));
            Assert.Equal("Large", BrowserField(monster, "size"));

            var spell = catalog.Rules.Single(value => value.EntityType == "spell");
            Assert.Equal("3rd", BrowserField(spell, "level"));
            Assert.Equal("Evocation", BrowserField(spell, "school"));

            var characterClass = catalog.Rules.Single(value => value.EntityType == "class");
            Assert.Equal("d8", BrowserField(characterClass, "hitDie"));

            var race = catalog.Rules.Single(value => value.EntityType == "race");
            Assert.Equal("DEX +2, WIS +1", BrowserField(race, "ability"));
            Assert.Equal("Medium", BrowserField(race, "size"));
        }
        finally
        {
            await CleanupAsync(factory, packageId, Guid.Empty);
        }
    }

    [Fact]
    public async Task LibraryVersionReadsDoNotExposeUnpublishedConceptBindings()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var authenticationClient = new FakeToolHostAuthenticationClient(
            new Dictionary<string, ToolHostAuthenticationContext>());
        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"browser-unpublished-{token}";
        var conceptKey = $"skill.browser-unpublished-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            Guid conceptId;
            Guid revisionId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

                var imported = await importer.Import5eToolsDocumentAsync(SourceRequest(
                    packageKey,
                    $"Unpublished Skill {token}",
                    "UNPUB",
                    isPublic: true));
                packageId = imported.PackageId;
                var sourceEntityId = imported.Entities.Single().EntityId;
                revisionId = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == sourceEntityId)
                    .Select(value => value.Id)
                    .SingleAsync();

                var concept = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(conceptKey, "skill", $"Unpublished Skill {token}"),
                    "rules-lawyer");
                conceptId = concept.Value.Id;
                await globalRules.BindSourceEntityAsync(
                    conceptId,
                    new BindRuleConceptSourceRequest(sourceEntityId),
                    "rules-lawyer");
            }

            using (var versionsResponse = await client.GetAsync(
                       $"/api/rules/{Uri.EscapeDataString(conceptKey)}/versions"))
            {
                Assert.Equal(HttpStatusCode.NotFound, versionsResponse.StatusCode);
            }

            using var comparisonResponse = await client.PostAsJsonAsync(
                "/api/rules/comparison",
                new RuleSourceComparisonRequest(conceptId, revisionId, revisionId));
            Assert.Equal(HttpStatusCode.NotFound, comparisonResponse.StatusCode);
        }
        finally
        {
            await CleanupAsync(factory, packageId, Guid.Empty);
        }
    }

    [Fact]
    public async Task LibraryVersionsExposeBoundSourceVariantsAndSemanticDiffs()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var authenticationClient = new FakeToolHostAuthenticationClient(
            new Dictionary<string, ToolHostAuthenticationContext>());
        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var token = Guid.NewGuid().ToString("N")[..10];
        var firstPackageKey = $"browser-version-a-{token}";
        var secondPackageKey = $"browser-version-b-{token}";
        var conceptKey = $"skill.browser-version-{token}";
        Guid firstPackageId = Guid.Empty;
        Guid secondPackageId = Guid.Empty;

        try
        {
            Guid firstRevisionId;
            Guid secondRevisionId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

                var firstImport = await importer.Import5eToolsDocumentAsync(SourceRequest(
                    firstPackageKey,
                    $"Versioned Skill {token}",
                    "VER-A",
                    isPublic: true,
                    ability: "int"));
                firstPackageId = firstImport.PackageId;
                var firstEntityId = firstImport.Entities.Single().EntityId;
                firstRevisionId = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == firstEntityId)
                    .Select(value => value.Id)
                    .SingleAsync();

                var secondImport = await importer.Import5eToolsDocumentAsync(SourceRequest(
                    secondPackageKey,
                    $"Versioned Skill {token}",
                    "VER-B",
                    isPublic: true,
                    ability: "wis"));
                secondPackageId = secondImport.PackageId;
                var secondEntityId = secondImport.Entities.Single().EntityId;
                secondRevisionId = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == secondEntityId)
                    .Select(value => value.Id)
                    .SingleAsync();

                var concept = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(conceptKey, "skill", $"Versioned Skill {token}"),
                    "rules-lawyer");
                await globalRules.BindSourceEntityAsync(
                    concept.Value.Id,
                    new BindRuleConceptSourceRequest(firstEntityId),
                    "rules-lawyer");
                await globalRules.BindSourceEntityAsync(
                    concept.Value.Id,
                    new BindRuleConceptSourceRequest(secondEntityId),
                    "rules-lawyer");
                await globalRules.SetDecisionAsync(
                    concept.Value.Id,
                    new SetGlobalRuleDecisionRequest(firstRevisionId, "Version browser fixture."),
                    "rules-lawyer");
                await globalRules.PublishAsync("rules-lawyer");
            }

            using (var versionsResponse = await client.GetAsync(
                       $"/api/rules/{Uri.EscapeDataString(conceptKey)}/versions"))
            {
                Assert.Equal(HttpStatusCode.OK, versionsResponse.StatusCode);
                var versions = await versionsResponse.Content.ReadFromJsonAsync<RuleConceptVersionsView>();
                Assert.NotNull(versions);
                Assert.Equal(2, versions.Versions.Count);
                Assert.Contains(versions.Versions, value => value.SourceCode == "VER-A");
                Assert.Contains(versions.Versions, value => value.SourceCode == "VER-B");
            }

            var comparisonRequest = new RuleSourceComparisonRequest(
                (await client.GetFromJsonAsync<RuleConceptVersionsView>(
                    $"/api/rules/{Uri.EscapeDataString(conceptKey)}/versions"))!.RuleConceptId,
                firstRevisionId,
                secondRevisionId);
            using var comparisonResponse = await client.PostAsJsonAsync(
                "/api/rules/comparison",
                comparisonRequest);
            Assert.Equal(HttpStatusCode.OK, comparisonResponse.StatusCode);
            var comparison = await comparisonResponse.Content
                .ReadFromJsonAsync<RuleSemanticComparisonView>();
            Assert.NotNull(comparison);
            Assert.True(comparison.ContradictionCount > 0);
            Assert.Contains(
                comparison.Differences,
                difference => difference.Path == "$.ability" && difference.RequiresDecision);
        }
        finally
        {
            await CleanupAsync(factory, firstPackageId, secondPackageId);
        }
    }

    private static string BrowserField(ResolvedRuleCatalogItemView rule, string key) =>
        rule.BrowserFields.Single(value => value.Key == key).Value;

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

    private static ToolHostAuthenticationContext Context(
        string userId,
        Guid? campaignId,
        string? campaignRole) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: "dorks-and-dice",
            User: new ToolHostUserContext(userId, userId),
            GlobalRoles: [],
            Campaigns: campaignId is not null && campaignRole is not null
                ? [new ToolHostCampaignContext(campaignId.Value, "Browser Campaign", campaignRole)]
                : []);

    private static Import5eToolsDocumentRequest SourceRequest(
        string packageKey,
        string entityName,
        string sourceCode,
        bool isPublic,
        string ability = "int") =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: $"Package {packageKey}",
            Provider: "integration-test",
            License: "test-only",
            IsPublic: isPublic,
            WorkKey: "browser-work",
            WorkDisplayName: "Browser Work",
            EditionKey: "browser-edition",
            EditionDisplayName: "Browser Edition",
            Json: $$"""
                {
                  "skill": [
                    {
                      "name": "{{entityName}}",
                      "source": "{{sourceCode}}",
                      "ability": "{{ability}}",
                      "secretMarker": "source-document-only"
                    }
                  ]
                }
                """);

    private static async Task CleanupAsync(
        WebApplicationFactory<Program> factory,
        Guid publicPackageId,
        Guid privatePackageId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

        await db.CampaignRulesetRevisionEntries.ExecuteDeleteAsync();
        await db.CampaignRulesetRevisions.ExecuteDeleteAsync();
        await db.CampaignRuleDecisions.ExecuteDeleteAsync();
        await db.CampaignRulesetSelections.ExecuteDeleteAsync();
        await db.RulesetRevisionEntries.ExecuteDeleteAsync();
        await db.RulesetRevisions.ExecuteDeleteAsync();
        await db.GlobalRuleDecisions.ExecuteDeleteAsync();
        await db.RuleConceptSourceBindings.ExecuteDeleteAsync();
        await db.RuleConcepts.ExecuteDeleteAsync();

        var packageIds = new[] { publicPackageId, privatePackageId }
            .Where(value => value != Guid.Empty)
            .ToArray();
        if (packageIds.Length > 0)
        {
            var packages = await db.SourcePackages
                .Where(value => packageIds.Contains(value.Id))
                .ToArrayAsync();
            db.SourcePackages.RemoveRange(packages);
            await db.SaveChangesAsync();
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
