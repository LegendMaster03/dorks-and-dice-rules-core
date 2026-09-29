using System.Data;
using System.Data.Common;
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
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class WikiReferenceCatalogIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task ReferenceCatalogUsesAccessibleCanonicalHistoryWithoutChangingResolvedConsumerContract()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var campaignId = Guid.NewGuid();
        var authenticationClient = new FakeToolHostAuthenticationClient(
            new Dictionary<string, ToolHostAuthenticationContext>
            {
                ["granted-ticket"] = Context("granted-reader"),
                ["ungranted-ticket"] = Context("ungranted-reader"),
                ["campaign-ticket"] = Context("campaign-reader", campaignId, "Player"),
                ["lawyer-ticket"] = Context("rules-lawyer", globalRoles: ["Rules Lawyer"])
            });
        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var token = Guid.NewGuid().ToString("N")[..10];
        var packageIds = new List<Guid>();
        var canonicalIds = new HashSet<Guid>();
        Guid crossConceptId = Guid.Empty;
        Guid prestigeRevisionId = Guid.Empty;
        Guid subclass5RevisionId = Guid.Empty;
        Guid subclass55RevisionId = Guid.Empty;

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

                var open = await ImportAsync(importer, packageIds,
                    $"wiki-open-{token}", "skill", $"Open Lore {token}", "OPEN", "3e", true,
                    new DateOnly(2000, 1, 1), "int");
                var restricted = await ImportAsync(importer, packageIds,
                    $"wiki-restricted-{token}", "skill", $"Restricted Lore {token}", "PRIVATE", "5e", false,
                    new DateOnly(2014, 1, 1), "wis");
                await grants.GrantAsync("granted-reader", restricted.PackageId);

                var prestige = await ImportAsync(importer, packageIds,
                    $"wiki-prestige-{token}", "prestigeClass", $"Arcane Fixture {token}", "THREEFIVE", "3.5e", true,
                    new DateOnly(2003, 7, 1), "legacy");
                var subclass5 = await ImportAsync(importer, packageIds,
                    $"wiki-subclass5-{token}", "subclass", $"Arcane Fixture {token}", "FIVE", "5e", true,
                    new DateOnly(2014, 8, 1), "2014");
                var subclass55 = await ImportAsync(importer, packageIds,
                    $"wiki-subclass55-{token}", "subclass", $"Arcane Fixture {token}", "FIVEFIVE", "5.5e", true,
                    new DateOnly(2024, 9, 1), "2024");

                var race = await ImportAsync(importer, packageIds,
                    $"wiki-race-{token}", "race", $"Elf Fixture {token}", "RACE5", "5e", true,
                    new DateOnly(2014, 8, 1), "race");
                var species = await ImportAsync(importer, packageIds,
                    $"wiki-species-{token}", "species", $"Elf Fixture {token}", "SPECIES55", "5.5e", true,
                    new DateOnly(2024, 9, 1), "species");
                var subrace = await ImportAsync(importer, packageIds,
                    $"wiki-subrace-{token}", "subrace", $"High Fixture {token}", "SUBRACE5", "5e", true,
                    new DateOnly(2014, 8, 1), "subrace");
                var subspecies = await ImportAsync(importer, packageIds,
                    $"wiki-subspecies-{token}", "subspecies", $"High Fixture {token}", "SUBSPECIES55", "5.5e", true,
                    new DateOnly(2024, 9, 1), "subspecies");

                var variantA = await ImportAsync(importer, packageIds,
                    $"wiki-variant-a-{token}", "feat", $"Twin Fixture {token}", "VARA", "5e", true,
                    new DateOnly(2014, 8, 1), "alpha");
                var variantB = await ImportAsync(importer, packageIds,
                    $"wiki-variant-b-{token}", "feat", $"Twin Fixture {token}", "VARB", "5e", true,
                    new DateOnly(2014, 9, 1), "beta");

                var openCanonical = await GetCanonicalEntityIdAsync(db, open.EntityId);
                var restrictedCanonical = await GetCanonicalEntityIdAsync(db, restricted.EntityId);
                var prestigeCanonical = await GetCanonicalEntityIdAsync(db, prestige.EntityId);
                var subclass5Canonical = await GetCanonicalEntityIdAsync(db, subclass5.EntityId);
                var subclass55Canonical = await GetCanonicalEntityIdAsync(db, subclass55.EntityId);
                var raceCanonical = await GetCanonicalEntityIdAsync(db, race.EntityId);
                var speciesCanonical = await GetCanonicalEntityIdAsync(db, species.EntityId);
                var subraceCanonical = await GetCanonicalEntityIdAsync(db, subrace.EntityId);
                var subspeciesCanonical = await GetCanonicalEntityIdAsync(db, subspecies.EntityId);
                var variantACanonical = await GetCanonicalEntityIdAsync(db, variantA.EntityId);
                var variantBCanonical = await GetCanonicalEntityIdAsync(db, variantB.EntityId);
                canonicalIds.UnionWith([
                    openCanonical, restrictedCanonical, prestigeCanonical, subclass5Canonical, subclass55Canonical,
                    raceCanonical, speciesCanonical, subraceCanonical, subspeciesCanonical,
                    variantACanonical, variantBCanonical
                ]);

                await RelateAsync(db, prestigeCanonical, subclass5Canonical, "revision");
                await RelateAsync(db, subclass5Canonical, subclass55Canonical, "revision");
                await RelateAsync(db, raceCanonical, speciesCanonical, "rename");
                await RelateAsync(db, subraceCanonical, subspeciesCanonical, "rename");
                await RelateAsync(db, variantACanonical, variantBCanonical, "variant");

                prestigeRevisionId = await GetRevisionIdAsync(db, prestige.EntityId);
                subclass5RevisionId = await GetRevisionIdAsync(db, subclass5.EntityId);
                subclass55RevisionId = await GetRevisionIdAsync(db, subclass55.EntityId);

                var concept = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(
                        $"subclass.arcane-fixture-{token}",
                        RuleConceptEntityTypes.Subclass,
                        $"Arcane Fixture {token}"),
                    "rules-lawyer");
                crossConceptId = concept.Value.Id;
                await globalRules.BindSourceEntityAsync(
                    crossConceptId,
                    new BindRuleConceptSourceRequest(subclass5.EntityId),
                    "rules-lawyer");
                await globalRules.BindSourceEntityAsync(
                    crossConceptId,
                    new BindRuleConceptSourceRequest(subclass55.EntityId),
                    "rules-lawyer");
                await globalRules.SetDecisionAsync(
                    crossConceptId,
                    new SetGlobalRuleDecisionRequest(subclass5RevisionId, "Global fixture chooses 5e."),
                    "rules-lawyer");
                globalRevision = await globalRules.PublishAsync("rules-lawyer");

                await campaignRules.SelectBaselineAsync(
                    campaignId,
                    new SelectCampaignRulesetBaselineRequest(globalRevision.Id),
                    "campaign-dm");
                await campaignRules.SetDecisionAsync(
                    campaignId,
                    crossConceptId,
                    new SetCampaignRuleDecisionRequest(
                        CampaignRuleDecisionKinds.SelectSource,
                        subclass55RevisionId,
                        "Campaign fixture chooses 5.5e."),
                    "campaign-dm");
                await campaignRules.PublishAsync(campaignId, "campaign-dm");
            }

            using (var anonymous = await client.GetAsync($"/api/wiki/references?q={token}&limit=100"))
            {
                Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
                var catalog = (await anonymous.Content.ReadFromJsonAsync<WikiReferenceCatalogView>())!;
                Assert.DoesNotContain(catalog.References, value => value.DisplayName.Contains("Restricted Lore", StringComparison.Ordinal));
                Assert.Contains(catalog.References, value => value.DisplayName.Contains("Open Lore", StringComparison.Ordinal));
                Assert.Contains(catalog.EditionFacets, value => value.Value == "3e");
                Assert.Contains(catalog.EditionFacets, value => value.Value == "3.5e");
                Assert.Contains(catalog.EditionFacets, value => value.Value == "5e");
                Assert.Contains(catalog.EditionFacets, value => value.Value == "5.5e");
            }

            using (var grantedRequest = HostedRequest(HttpMethod.Get, $"/api/wiki/references?q={token}&limit=100", "granted-ticket"))
            using (var grantedResponse = await client.SendAsync(grantedRequest))
            {
                Assert.Equal(HttpStatusCode.OK, grantedResponse.StatusCode);
                var catalog = (await grantedResponse.Content.ReadFromJsonAsync<WikiReferenceCatalogView>())!;
                Assert.Contains(catalog.References, value => value.DisplayName.Contains("Restricted Lore", StringComparison.Ordinal));
                Assert.Contains(catalog.PackageFacets, value => value.Value.Contains("wiki-restricted", StringComparison.Ordinal));
                Assert.Equal(2, catalog.References.Count(value => value.DisplayName == $"Twin Fixture {token}"));
            }

            using (var ungrantedRequest = HostedRequest(HttpMethod.Get, $"/api/wiki/references?q={token}&limit=100", "ungranted-ticket"))
            using (var ungrantedResponse = await client.SendAsync(ungrantedRequest))
            {
                Assert.Equal(HttpStatusCode.OK, ungrantedResponse.StatusCode);
                var catalog = (await ungrantedResponse.Content.ReadFromJsonAsync<WikiReferenceCatalogView>())!;
                Assert.DoesNotContain(catalog.References, value => value.DisplayName.Contains("Restricted Lore", StringComparison.Ordinal));
                Assert.DoesNotContain(catalog.PackageFacets, value => value.Value.Contains("wiki-restricted", StringComparison.Ordinal));
            }

            WikiReferenceItemView crossReference;
            using (var crossResponse = await client.GetAsync(
                       $"/api/wiki/references?entityType=prestigeClass&categoryMode=any&q={Uri.EscapeDataString($"Arcane Fixture {token}")}"))
            {
                Assert.Equal(HttpStatusCode.OK, crossResponse.StatusCode);
                var catalog = (await crossResponse.Content.ReadFromJsonAsync<WikiReferenceCatalogView>())!;
                crossReference = Assert.Single(catalog.References);
                Assert.Equal("subclass", crossReference.EffectiveCategory);
                Assert.Equal("5e", crossReference.EffectiveEditionKey);
                Assert.Equal(WikiReferenceResolutionStates.Resolved, crossReference.ResolutionState);
                Assert.Contains(crossReference.CategoryHistory, value => value.Category == "prestigeClass");
                Assert.Contains(crossReference.CategoryHistory, value => value.Category == "subclass");
            }

            using (var effectivePrestige = await client.GetAsync(
                       $"/api/wiki/references?entityType=prestigeClass&categoryMode=effective&q={Uri.EscapeDataString($"Arcane Fixture {token}")}"))
            {
                var catalog = (await effectivePrestige.Content.ReadFromJsonAsync<WikiReferenceCatalogView>())!;
                Assert.Empty(catalog.References);
            }
            using (var effectiveSubclass = await client.GetAsync(
                       $"/api/wiki/references?entityType=subclass&categoryMode=effective&q={Uri.EscapeDataString($"Arcane Fixture {token}")}"))
            {
                var catalog = (await effectiveSubclass.Content.ReadFromJsonAsync<WikiReferenceCatalogView>())!;
                Assert.Single(catalog.References);
            }

            var encodedReference = Uri.EscapeDataString(crossReference.ReferenceIdentity);
            using (var detailResponse = await client.GetAsync($"/api/wiki/references/{encodedReference}"))
            {
                Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
                var detail = (await detailResponse.Content.ReadFromJsonAsync<WikiReferenceDetailView>())!;
                Assert.Equal(3, detail.Variations.Count);
                Assert.Contains(detail.Variations, value => value.NativeEntityType == "prestigeClass" && value.EditionKey == "3.5e");
                Assert.Contains(detail.Variations, value => value.NativeEntityType == "subclass" && value.EditionKey == "5e");
                Assert.Contains(detail.Variations, value => value.NativeEntityType == "subclass" && value.EditionKey == "5.5e");
                Assert.Equal(subclass5RevisionId, detail.Reference.EffectiveVariation.SourceEntityRevisionId);
            }

            using (var comparisonResponse = await client.PostAsJsonAsync(
                       "/api/wiki/references/comparison",
                       new WikiReferenceComparisonRequest(
                           crossReference.ReferenceIdentity,
                           prestigeRevisionId,
                           subclass5RevisionId)))
            {
                Assert.Equal(HttpStatusCode.OK, comparisonResponse.StatusCode);
                var comparison = await comparisonResponse.Content.ReadFromJsonAsync<RuleSemanticComparisonView>();
                Assert.NotNull(comparison);
            }

            using (var speciesResponse = await client.GetAsync(
                       $"/api/wiki/references?entityType=species&categoryMode=any&q={token}"))
            {
                var catalog = (await speciesResponse.Content.ReadFromJsonAsync<WikiReferenceCatalogView>())!;
                var elf = Assert.Single(catalog.References.Where(value => value.DisplayName == $"Elf Fixture {token}"));
                Assert.Contains(elf.CategoryHistory, value => value.Category == "species");
                Assert.Equal("5.5e", elf.EffectiveEditionKey);
            }
            using (var raceAliasResponse = await client.GetAsync(
                       $"/api/wiki/references?entityType=race&categoryMode=any&q={token}"))
            {
                var catalog = (await raceAliasResponse.Content.ReadFromJsonAsync<WikiReferenceCatalogView>())!;
                Assert.Single(catalog.References.Where(value => value.DisplayName == $"Elf Fixture {token}"));
            }
            using (var subspeciesResponse = await client.GetAsync(
                       $"/api/wiki/references?entityType=subspecies&categoryMode=any&q={token}"))
            {
                var catalog = (await subspeciesResponse.Content.ReadFromJsonAsync<WikiReferenceCatalogView>())!;
                Assert.Single(catalog.References.Where(value => value.DisplayName == $"High Fixture {token}"));
            }
            using (var subraceAliasResponse = await client.GetAsync(
                       $"/api/wiki/references?entityType=subrace&categoryMode=any&q={token}"))
            {
                var catalog = (await subraceAliasResponse.Content.ReadFromJsonAsync<WikiReferenceCatalogView>())!;
                Assert.Single(catalog.References.Where(value => value.DisplayName == $"High Fixture {token}"));
            }

            int decisionsBeforeFallbackRead;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                decisionsBeforeFallbackRead = await scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>()
                    .GlobalRuleDecisions.CountAsync();
            }
            using (var fallbackResponse = await client.GetAsync(
                       $"/api/wiki/references?q={Uri.EscapeDataString($"Open Lore {token}")}"))
            {
                var catalog = (await fallbackResponse.Content.ReadFromJsonAsync<WikiReferenceCatalogView>())!;
                var openReference = Assert.Single(catalog.References);
                Assert.Equal(WikiReferenceResolutionStates.UnresolvedFallback, openReference.ResolutionState);
                Assert.Equal("3e", openReference.EffectiveEditionKey);
                using var detailResponse = await client.GetAsync(
                    $"/api/wiki/references/{Uri.EscapeDataString(openReference.ReferenceIdentity)}");
                Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
            }
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var decisionsAfter = await scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>()
                    .GlobalRuleDecisions.CountAsync();
                Assert.Equal(decisionsBeforeFallbackRead, decisionsAfter);
            }

            using (var consumerResponse = await client.GetAsync(
                       $"/api/rules?q={Uri.EscapeDataString($"Open Lore {token}")}"))
            {
                Assert.Equal(HttpStatusCode.OK, consumerResponse.StatusCode);
                var consumerCatalog = (await consumerResponse.Content.ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                Assert.Empty(consumerCatalog.Rules);
            }

            using (var campaignRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/wiki/references?q={Uri.EscapeDataString($"Arcane Fixture {token}")}",
                       "campaign-ticket"))
            using (var campaignResponse = await client.SendAsync(campaignRequest))
            {
                Assert.Equal(HttpStatusCode.OK, campaignResponse.StatusCode);
                var catalog = (await campaignResponse.Content.ReadFromJsonAsync<WikiReferenceCatalogView>())!;
                var reference = Assert.Single(catalog.References);
                Assert.Equal(WikiReferenceResolutionStates.CampaignOverride, reference.ResolutionState);
                Assert.True(reference.HasCampaignOverride);
                Assert.Equal(subclass55RevisionId, reference.EffectiveVariation.SourceEntityRevisionId);
                Assert.Equal("5.5e", reference.EffectiveEditionKey);
            }
            using (var overridesRequest = HostedRequest(
                       HttpMethod.Get,
                       $"/api/campaigns/{campaignId}/wiki/references?overridesOnly=true&q={token}",
                       "campaign-ticket"))
            using (var overridesResponse = await client.SendAsync(overridesRequest))
            {
                Assert.Equal(HttpStatusCode.OK, overridesResponse.StatusCode);
                var catalog = (await overridesResponse.Content.ReadFromJsonAsync<WikiReferenceCatalogView>())!;
                var reference = Assert.Single(catalog.References);
                Assert.Equal(crossReference.ReferenceIdentity, reference.ReferenceIdentity);
            }

            using (var deniedMutation = HostedRequest(
                       HttpMethod.Put,
                       $"/api/global/rules/concepts/{crossConceptId}/decision",
                       "granted-ticket"))
            {
                deniedMutation.Content = JsonContent.Create(new SetGlobalRuleDecisionRequest(
                    subclass5RevisionId,
                    "Reader must not mutate."));
                using var response = await client.SendAsync(deniedMutation);
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
        }
        finally
        {
            await CleanupAsync(factory, packageIds, canonicalIds);
        }
    }

    private static async Task<(Guid PackageId, Guid EntityId)> ImportAsync(
        ISourceImportService importer,
        ICollection<Guid> packageIds,
        string packageKey,
        string entityType,
        string entityName,
        string sourceCode,
        string edition,
        bool isPublic,
        DateOnly publicationDate,
        string marker)
    {
        var request = new Import5eToolsDocumentRequest(
            PackageKey: packageKey,
            PackageDisplayName: $"Package {packageKey}",
            Provider: "integration-test",
            License: "test-only",
            IsPublic: isPublic,
            WorkKey: $"work-{packageKey}",
            WorkDisplayName: $"Work {packageKey}",
            EditionKey: edition,
            EditionDisplayName: edition,
            Json: $$"""
                {
                  "{{entityType}}": [
                    {
                      "name": "{{entityName}}",
                      "source": "{{sourceCode}}",
                      "entries": ["{{marker}}"]
                    }
                  ]
                }
                """,
            GameEdition: edition,
            ReleaseKind: "published",
            PublicationDate: publicationDate);
        var imported = await importer.Import5eToolsDocumentAsync(request);
        packageIds.Add(imported.PackageId);
        var entity = Assert.Single(imported.Entities);
        return (imported.PackageId, entity.EntityId);
    }

    private static async Task<Guid> GetRevisionIdAsync(RulesCoreDbContext db, Guid sourceEntityId) =>
        await db.SourceEntityRevisions
            .Where(value => value.SourceEntityId == sourceEntityId)
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => value.Id)
            .FirstAsync();

    private static async Task<Guid> GetCanonicalEntityIdAsync(RulesCoreDbContext db, Guid sourceEntityId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT occurrence.canonical_entity_id
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                  ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                WHERE binding.source_entity_id = @source_entity_id
                  AND occurrence.canonical_entity_id IS NOT NULL
                ORDER BY binding.source_entity_revision_id
                LIMIT 1;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            var value = await command.ExecuteScalarAsync();
            return value is Guid id ? id : throw new InvalidOperationException("Canonical entity was not created for fixture source entity.");
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task RelateAsync(
        RulesCoreDbContext db,
        Guid fromId,
        Guid toId,
        string kind)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO canonical_entity_relationship (
                    canonical_entity_relationship_id,
                    from_canonical_entity_id,
                    to_canonical_entity_id,
                    relationship_kind,
                    evidence_kind,
                    confidence,
                    created_at)
                VALUES (@id, @from_id, @to_id, @kind, 'integration-test', 1.0, @created_at)
                ON CONFLICT (from_canonical_entity_id, to_canonical_entity_id, relationship_kind)
                DO NOTHING;
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@from_id", fromId);
            AddParameter(command, "@to_id", toId);
            AddParameter(command, "@kind", kind);
            AddParameter(command, "@created_at", DateTimeOffset.UtcNow);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(IToolHostAuthenticationClient authenticationClient) =>
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
        Guid? campaignId = null,
        string? campaignRole = null,
        IReadOnlyList<string>? globalRoles = null) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: "dorks-and-dice",
            User: new ToolHostUserContext(userId, userId),
            GlobalRoles: globalRoles ?? [],
            Campaigns: campaignId is not null && campaignRole is not null
                ? [new ToolHostCampaignContext(campaignId.Value, "Wiki Reference Campaign", campaignRole)]
                : []);

    private static async Task CleanupAsync(
        WebApplicationFactory<Program> factory,
        IReadOnlyCollection<Guid> packageIds,
        IReadOnlyCollection<Guid> canonicalIds)
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

        if (packageIds.Count > 0)
        {
            var packages = await db.SourcePackages.Where(value => packageIds.Contains(value.Id)).ToArrayAsync();
            db.SourcePackages.RemoveRange(packages);
            await db.SaveChangesAsync();
        }

        if (canonicalIds.Count > 0)
        {
            var connection = db.Database.GetDbConnection();
            var openedHere = connection.State != ConnectionState.Open;
            if (openedHere) await connection.OpenAsync();
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    DELETE FROM canonical_entity_relationship
                    WHERE from_canonical_entity_id = ANY(@canonical_ids)
                       OR to_canonical_entity_id = ANY(@canonical_ids);
                    """;
                AddParameter(command, "@canonical_ids", canonicalIds.ToArray());
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                if (openedHere) await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
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
