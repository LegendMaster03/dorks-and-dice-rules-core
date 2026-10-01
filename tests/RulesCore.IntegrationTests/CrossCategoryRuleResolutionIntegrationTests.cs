using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CrossCategoryRuleResolutionIntegrationTests
{
    [Fact]
    public async Task AuthoritativeHistoryAllowsResolutionAcrossMechanicalCategoriesWithoutWeakeningBindings()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var token = Guid.NewGuid().ToString("N")[..10];
        var campaignId = Guid.NewGuid();
        var inheritedCampaignId = Guid.NewGuid();
        var packageIds = new List<Guid>();
        var canonicalIds = new HashSet<Guid>();

        Guid conceptId = Guid.Empty;
        string conceptKey = string.Empty;
        string displayName = string.Empty;
        Guid prestigeRevisionId = Guid.Empty;
        Guid subclass5RevisionId = Guid.Empty;
        Guid subclass55RevisionId = Guid.Empty;
        Guid fallbackConceptId = Guid.Empty;
        string fallbackKey = string.Empty;
        PublishedRulesetRevisionView subclassGlobalRevision = null!;

        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var campaignRules = scope.ServiceProvider.GetRequiredService<ICampaignRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

                displayName = $"Arcane Transition Fixture {token}";
                conceptKey = $"prestige-class.arcane-transition-{token}";

                var prestige = await ImportAsync(
                    importer, packageIds,
                    $"cross-prestige-{token}",
                    RuleConceptEntityTypes.PrestigeClass,
                    displayName,
                    "CROSS35",
                    "3.5e",
                    new DateOnly(2003, 7, 1),
                    "prestige");
                var subclass5 = await ImportAsync(
                    importer, packageIds,
                    $"cross-subclass5-{token}",
                    RuleConceptEntityTypes.Subclass,
                    displayName,
                    "CROSS5",
                    "5e",
                    new DateOnly(2014, 8, 1),
                    "subclass-2014");
                var subclass55 = await ImportAsync(
                    importer, packageIds,
                    $"cross-subclass55-{token}",
                    RuleConceptEntityTypes.Subclass,
                    displayName,
                    "CROSS55",
                    "5.5e",
                    new DateOnly(2024, 9, 1),
                    "subclass-2024");
                var unrelated = await ImportAsync(
                    importer, packageIds,
                    $"cross-unrelated-{token}",
                    RuleConceptEntityTypes.Subclass,
                    $"Unrelated Transition Fixture {token}",
                    "UNRELATED",
                    "5e",
                    new DateOnly(2014, 8, 2),
                    "unrelated");
                var variantOnly = await ImportAsync(
                    importer, packageIds,
                    $"cross-variant-{token}",
                    RuleConceptEntityTypes.Subclass,
                    displayName,
                    "VARIANT",
                    "5e",
                    new DateOnly(2014, 8, 3),
                    "variant");
                var reprintOnly = await ImportAsync(
                    importer, packageIds,
                    $"cross-reprint-{token}",
                    RuleConceptEntityTypes.Subclass,
                    displayName,
                    "REPRINT",
                    "5e",
                    new DateOnly(2014, 8, 4),
                    "reprint");

                var prestigeCanonical = await GetCanonicalEntityIdAsync(db, prestige.EntityId);
                var subclass5Canonical = await GetCanonicalEntityIdAsync(db, subclass5.EntityId);
                var subclass55Canonical = await GetCanonicalEntityIdAsync(db, subclass55.EntityId);
                var unrelatedCanonical = await GetCanonicalEntityIdAsync(db, unrelated.EntityId);
                var variantCanonical = await GetCanonicalEntityIdAsync(db, variantOnly.EntityId);
                var reprintCanonical = await GetCanonicalEntityIdAsync(db, reprintOnly.EntityId);
                canonicalIds.UnionWith([
                    prestigeCanonical,
                    subclass5Canonical,
                    subclass55Canonical,
                    unrelatedCanonical,
                    variantCanonical,
                    reprintCanonical
                ]);

                // Revision and rename are authoritative same-history evidence. Variant and reprint are not.
                await RelateAsync(db, prestigeCanonical, subclass5Canonical, "revision");
                await RelateAsync(db, subclass5Canonical, subclass55Canonical, "rename");
                await RelateAsync(db, prestigeCanonical, variantCanonical, "variant");
                await RelateAsync(db, prestigeCanonical, reprintCanonical, "reprint");

                prestigeRevisionId = await GetRevisionIdAsync(db, prestige.EntityId);
                subclass5RevisionId = await GetRevisionIdAsync(db, subclass5.EntityId);
                subclass55RevisionId = await GetRevisionIdAsync(db, subclass55.EntityId);
                var unrelatedRevisionId = await GetRevisionIdAsync(db, unrelated.EntityId);
                var variantRevisionId = await GetRevisionIdAsync(db, variantOnly.EntityId);
                var reprintRevisionId = await GetRevisionIdAsync(db, reprintOnly.EntityId);

                var concept = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(
                        conceptKey,
                        RuleConceptEntityTypes.PrestigeClass,
                        displayName),
                    "rules-lawyer");
                conceptId = concept.Value.Id;
                await globalRules.BindSourceEntityAsync(
                    conceptId,
                    new BindRuleConceptSourceRequest(prestige.EntityId),
                    "rules-lawyer");

                // Direct concept bindings remain type-coherent even when canonical history allows
                // the related variation to participate in resolution.
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    globalRules.BindSourceEntityAsync(
                        conceptId,
                        new BindRuleConceptSourceRequest(subclass5.EntityId),
                        "rules-lawyer"));
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    globalRules.BindSourceEntityAsync(
                        conceptId,
                        new BindRuleConceptSourceRequest(unrelated.EntityId),
                        "rules-lawyer"));

                // Unrelated identities and variant/reprint relationships do not grant
                // authoritative same-history resolution semantics.
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    globalRules.SetDecisionAsync(
                        conceptId,
                        new SetGlobalRuleDecisionRequest(unrelatedRevisionId, "Must reject unrelated source."),
                        "rules-lawyer"));
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    globalRules.SetDecisionAsync(
                        conceptId,
                        new SetGlobalRuleDecisionRequest(variantRevisionId, "Must reject variant-only source."),
                        "rules-lawyer"));
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    globalRules.SetDecisionAsync(
                        conceptId,
                        new SetGlobalRuleDecisionRequest(reprintRevisionId, "Must reject reprint-only source."),
                        "rules-lawyer"));

                await globalRules.SetDecisionAsync(
                    conceptId,
                    new SetGlobalRuleDecisionRequest(
                        prestigeRevisionId,
                        "Global fixture initially selects the 3.5e Prestige Class variation."),
                    "rules-lawyer");
                await globalRules.PublishAsync("rules-lawyer");

                // An unresolved second history exercises deterministic fallback independently of
                // the published cross-category concept.
                var fallbackPrestige = await ImportAsync(
                    importer, packageIds,
                    $"fallback-prestige-{token}",
                    RuleConceptEntityTypes.PrestigeClass,
                    $"Fallback Transition Fixture {token}",
                    "FALLBACK35",
                    "3.5e",
                    new DateOnly(2003, 7, 1),
                    "fallback-prestige");
                var fallbackSubclass = await ImportAsync(
                    importer, packageIds,
                    $"fallback-subclass-{token}",
                    RuleConceptEntityTypes.Subclass,
                    $"Fallback Transition Fixture {token}",
                    "FALLBACK5",
                    "5e",
                    new DateOnly(2014, 8, 1),
                    "fallback-subclass");
                var fallbackPrestigeCanonical = await GetCanonicalEntityIdAsync(db, fallbackPrestige.EntityId);
                var fallbackSubclassCanonical = await GetCanonicalEntityIdAsync(db, fallbackSubclass.EntityId);
                canonicalIds.UnionWith([fallbackPrestigeCanonical, fallbackSubclassCanonical]);
                await RelateAsync(db, fallbackPrestigeCanonical, fallbackSubclassCanonical, "revision");

                fallbackKey = $"prestige-class.fallback-transition-{token}";
                var fallbackConcept = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(
                        fallbackKey,
                        RuleConceptEntityTypes.PrestigeClass,
                        $"Fallback Transition Fixture {token}"),
                    "rules-lawyer");
                fallbackConceptId = fallbackConcept.Value.Id;
                await globalRules.BindSourceEntityAsync(
                    fallbackConceptId,
                    new BindRuleConceptSourceRequest(fallbackPrestige.EntityId),
                    "rules-lawyer");
            }

            // The initial published rule genuinely resolves through the consumer API as a Prestige Class.
            using (var initialResponse = await client.GetAsync($"/api/rules/{Uri.EscapeDataString(conceptKey)}"))
            {
                Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);
                var resolved = (await initialResponse.Content.ReadFromJsonAsync<ResolvedRuleView>())!;
                Assert.Equal(RuleConceptEntityTypes.PrestigeClass, resolved.EntityType);
                Assert.Equal(prestigeRevisionId, resolved.SourceEntityRevisionId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var wiki = new WikiReferenceCatalogService(db);

                var initialWiki = await wiki.GetGlobalCatalogAsync(
                    userId: null,
                    entityType: RuleConceptEntityTypes.PrestigeClass,
                    categoryMode: WikiReferenceCategoryModes.Effective,
                    query: displayName,
                    sourceCode: null,
                    packageKey: null,
                    edition: null,
                    limit: 20,
                    offset: 0);
                var initialReference = Assert.Single(initialWiki.References);
                Assert.Equal(conceptId, initialReference.RuleConceptId);
                Assert.Equal(RuleConceptEntityTypes.PrestigeClass, initialReference.EffectiveCategory);
                Assert.Contains(initialReference.CategoryHistory, value => value.Category == RuleConceptEntityTypes.Subclass);

                // The same Rules Layer concept selects a mechanically distinct 5e variation solely
                // because canonical revision history proves it is the evolving logical concept.
                await globalRules.SetDecisionAsync(
                    conceptId,
                    new SetGlobalRuleDecisionRequest(
                        subclass5RevisionId,
                        "Global fixture switches to the 5e Subclass variation."),
                    "rules-lawyer");
                subclassGlobalRevision = await globalRules.PublishAsync("rules-lawyer");

                var effectiveSubclass = await wiki.GetGlobalCatalogAsync(
                    userId: null,
                    entityType: RuleConceptEntityTypes.Subclass,
                    categoryMode: WikiReferenceCategoryModes.Effective,
                    query: displayName,
                    sourceCode: null,
                    packageKey: null,
                    edition: null,
                    limit: 20,
                    offset: 0);
                var switchedReference = Assert.Single(
                    effectiveSubclass.References.Where(value => value.RuleConceptId == conceptId));
                Assert.Equal(conceptId, switchedReference.RuleConceptId);
                Assert.Equal(WikiReferenceResolutionStates.Resolved, switchedReference.ResolutionState);
                Assert.Equal(RuleConceptEntityTypes.Subclass, switchedReference.EffectiveCategory);
                Assert.Equal("5e", switchedReference.EffectiveEditionKey);
                Assert.Contains(switchedReference.CategoryHistory, value => value.Category == RuleConceptEntityTypes.PrestigeClass);

                var effectivePrestige = await wiki.GetGlobalCatalogAsync(
                    userId: null,
                    entityType: RuleConceptEntityTypes.PrestigeClass,
                    categoryMode: WikiReferenceCategoryModes.Effective,
                    query: displayName,
                    sourceCode: null,
                    packageKey: null,
                    edition: null,
                    limit: 20,
                    offset: 0);
                Assert.Empty(effectivePrestige.References);

                var historicalPrestige = await wiki.GetGlobalCatalogAsync(
                    userId: null,
                    entityType: RuleConceptEntityTypes.PrestigeClass,
                    categoryMode: WikiReferenceCategoryModes.AnyVariation,
                    query: displayName,
                    sourceCode: null,
                    packageKey: null,
                    edition: null,
                    limit: 20,
                    offset: 0);
                Assert.Single(historicalPrestige.References);
            }

            // Both the single-rule and catalog resolved consumer APIs expose the 5e effective category.
            using (var switchedResponse = await client.GetAsync($"/api/rules/{Uri.EscapeDataString(conceptKey)}"))
            {
                Assert.Equal(HttpStatusCode.OK, switchedResponse.StatusCode);
                var resolved = (await switchedResponse.Content.ReadFromJsonAsync<ResolvedRuleView>())!;
                Assert.Equal(RuleConceptEntityTypes.Subclass, resolved.EntityType);
                Assert.Equal(subclass5RevisionId, resolved.SourceEntityRevisionId);
            }
            using (var subclassCatalogResponse = await client.GetAsync(
                       $"/api/rules?entityType=subclass&q={Uri.EscapeDataString(displayName)}&limit=20"))
            {
                Assert.Equal(HttpStatusCode.OK, subclassCatalogResponse.StatusCode);
                var catalog = (await subclassCatalogResponse.Content.ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                var resolved = Assert.Single(catalog.Rules.Where(value => value.RuleConceptId == conceptId));
                Assert.Equal(RuleConceptEntityTypes.Subclass, resolved.EntityType);
                Assert.Contains(catalog.EntityTypeFacets, value =>
                    value.EntityType == RuleConceptEntityTypes.Subclass);
            }
            using (var prestigeCatalogResponse = await client.GetAsync(
                       $"/api/rules?entityType=prestigeClass&q={Uri.EscapeDataString(displayName)}&limit=20"))
            {
                Assert.Equal(HttpStatusCode.OK, prestigeCatalogResponse.StatusCode);
                var catalog = (await prestigeCatalogResponse.Content.ReadFromJsonAsync<ResolvedRulesCatalogView>())!;
                Assert.DoesNotContain(catalog.Rules, value => value.RuleConceptId == conceptId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
                var campaignRules = scope.ServiceProvider.GetRequiredService<ICampaignRulesService>();
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var wiki = new WikiReferenceCatalogService(db);

                // Deterministic unresolved fallback follows the newest applicable variation and does
                // not persist a decision merely because it was read.
                var fallbackResolved = await globalRules.ResolveLatestAsync(fallbackKey, userId: null);
                Assert.NotNull(fallbackResolved);
                Assert.Equal(RuleResolutionStates.UnresolvedFallback, fallbackResolved!.DecisionKind);
                Assert.Equal(RuleConceptEntityTypes.Subclass, fallbackResolved.EntityType);
                Assert.False(await db.GlobalRuleDecisions.AnyAsync(value => value.RuleConceptId == fallbackConceptId));

                await campaignRules.SelectBaselineAsync(
                    campaignId,
                    new SelectCampaignRulesetBaselineRequest(subclassGlobalRevision.Id),
                    "campaign-dm");
                await campaignRules.SetDecisionAsync(
                    campaignId,
                    conceptId,
                    new SetCampaignRuleDecisionRequest(
                        CampaignRuleDecisionKinds.SelectSource,
                        prestigeRevisionId,
                        "Campaign override crosses back to the 3.5e Prestige Class variation."),
                    "campaign-dm");
                await campaignRules.PublishAsync(campaignId, "campaign-dm");

                var prestigeOverride = await campaignRules.ResolveLatestAsync(
                    campaignId,
                    conceptKey,
                    "campaign-reader");
                Assert.NotNull(prestigeOverride);
                Assert.Equal(RuleConceptEntityTypes.PrestigeClass, prestigeOverride!.EntityType);
                Assert.Equal(prestigeRevisionId, prestigeOverride.SourceEntityRevisionId);

                var campaignPrestigeWiki = await wiki.GetCampaignCatalogAsync(
                    campaignId,
                    "campaign-reader",
                    RuleConceptEntityTypes.PrestigeClass,
                    WikiReferenceCategoryModes.Effective,
                    displayName,
                    sourceCode: null,
                    packageKey: null,
                    edition: null,
                    limit: 20,
                    offset: 0);
                var prestigeReference = Assert.Single(campaignPrestigeWiki.References);
                Assert.Equal(WikiReferenceResolutionStates.CampaignOverride, prestigeReference.ResolutionState);
                Assert.Equal(RuleConceptEntityTypes.PrestigeClass, prestigeReference.EffectiveCategory);

                // The same campaign can move forward through the rename-supported history to 5.5e.
                await campaignRules.SetDecisionAsync(
                    campaignId,
                    conceptId,
                    new SetCampaignRuleDecisionRequest(
                        CampaignRuleDecisionKinds.SelectSource,
                        subclass55RevisionId,
                        "Campaign override selects the 5.5e Subclass variation."),
                    "campaign-dm");
                await campaignRules.PublishAsync(campaignId, "campaign-dm");

                var subclassOverride = await campaignRules.ResolveLatestAsync(
                    campaignId,
                    conceptKey,
                    "campaign-reader");
                Assert.NotNull(subclassOverride);
                Assert.Equal(RuleConceptEntityTypes.Subclass, subclassOverride!.EntityType);
                Assert.Equal(subclass55RevisionId, subclassOverride.SourceEntityRevisionId);

                var campaignSubclassWiki = await wiki.GetCampaignCatalogAsync(
                    campaignId,
                    "campaign-reader",
                    RuleConceptEntityTypes.Subclass,
                    WikiReferenceCategoryModes.Effective,
                    displayName,
                    sourceCode: null,
                    packageKey: null,
                    edition: null,
                    limit: 20,
                    offset: 0);
                var subclassReference = Assert.Single(
                    campaignSubclassWiki.References.Where(value => value.RuleConceptId == conceptId));
                Assert.Equal(WikiReferenceResolutionStates.CampaignOverride, subclassReference.ResolutionState);
                Assert.Equal(RuleConceptEntityTypes.Subclass, subclassReference.EffectiveCategory);
                Assert.Equal("5.5e", subclassReference.EffectiveEditionKey);

                // A separate campaign that inherits the same pinned global baseline remains on the
                // baseline's 5e source revision and does not inherit another campaign's override.
                await campaignRules.SelectBaselineAsync(
                    inheritedCampaignId,
                    new SelectCampaignRulesetBaselineRequest(subclassGlobalRevision.Id),
                    "inherited-dm");
                await campaignRules.PublishAsync(inheritedCampaignId, "inherited-dm");
                var inherited = await campaignRules.ResolveLatestAsync(
                    inheritedCampaignId,
                    conceptKey,
                    "inherited-reader");
                Assert.NotNull(inherited);
                Assert.Equal(RuleConceptEntityTypes.Subclass, inherited!.EntityType);
                Assert.Equal(subclass5RevisionId, inherited.SourceEntityRevisionId);
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
        DateOnly publicationDate,
        string marker)
    {
        var request = new Import5eToolsDocumentRequest(
            PackageKey: packageKey,
            PackageDisplayName: $"Package {packageKey}",
            Provider: "integration-test",
            License: "test-only",
            IsPublic: true,
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

    private static Task<Guid> GetRevisionIdAsync(RulesCoreDbContext db, Guid sourceEntityId) =>
        db.SourceEntityRevisions
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
            return value is Guid id
                ? id
                : throw new InvalidOperationException("Canonical entity was not created for fixture source entity.");
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
            var packages = await db.SourcePackages
                .Where(value => packageIds.Contains(value.Id))
                .ToArrayAsync();
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
}