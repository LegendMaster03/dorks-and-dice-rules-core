using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class ReferenceHistoryAndCompanionContentIntegrationTests
{
    [Fact]
    public async Task ChangedMechanicsAndCompatibleCategoryMigrationsShareOneReferenceHistory()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..10];
            var importer = new ReconciledNormalizedSourceImportService(new NormalizedSourceImportService(db), db);
            var goblinName = $"Goblin {token}";
            var goblin3 = await importer.ImportAsync(Request(
                $"goblin-3e-{token}", "monster", goblinName, $"G3{token}", "3e", true, 7));
            var goblin5 = await importer.ImportAsync(Request(
                $"goblin-5e-{token}", "monster", goblinName, $"G5{token}", "5e", true, 11));

            var goblin3Canonical = await CanonicalEntityIdAsync(db, Assert.Single(goblin3.Entities).EntityId);
            var goblin5Canonical = await CanonicalEntityIdAsync(db, Assert.Single(goblin5.Entities).EntityId);
            Assert.NotEqual(goblin3Canonical, goblin5Canonical);

            var goblinCatalog = await CatalogAsync(db, null, goblinName);
            var goblinReference = Assert.Single(goblinCatalog.References.Where(value => value.DisplayName == goblinName));
            var goblinDetail = await new WikiReferenceCatalogService(db)
                .GetGlobalDetailAsync(null, goblinReference.ReferenceIdentity);
            Assert.NotNull(goblinDetail);
            Assert.Equal(2, goblinDetail!.Variations.Count);
            Assert.Contains(goblinDetail.Variations, value => value.EditionKey == "3e");
            Assert.Contains(goblinDetail.Variations, value => value.EditionKey == "5e");

            var className = $"Arcane Fixture {token}";
            await importer.ImportAsync(Request(
                $"prestige-{token}", "prestigeClass", className, $"PC{token}", "3.5e", true, 1));
            await importer.ImportAsync(Request(
                $"subclass-{token}", "subclass", className, $"SC{token}", "5e", true, 2));
            var classCatalog = await CatalogAsync(db, null, className);
            var classReference = Assert.Single(classCatalog.References.Where(value => value.DisplayName == className));
            var classDetail = await new WikiReferenceCatalogService(db)
                .GetGlobalDetailAsync(null, classReference.ReferenceIdentity);
            Assert.NotNull(classDetail);
            Assert.Equal(2, classDetail!.Variations.Count);
            Assert.Contains(classDetail.Variations, value => value.Category == "prestigeClass");
            Assert.Contains(classDetail.Variations, value => value.Category == "subclass");
        }
    }

    [Fact]
    public async Task RestrictedMiddleVariationPreservesAccessibleLogicalHistoryWithoutLeakingSource()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..10];
            var importer = new NormalizedSourceImportService(db);
            var name = $"Hidden Bridge {token}";
            var firstSource = $"HB3{token}";
            var middleSource = $"HB35{token}";
            var lastSource = $"HB5{token}";

            var first = await importer.ImportAsync(Request(
                $"hidden-bridge-first-{token}", "monster", name, firstSource, "3e", true, 7));
            var middle = await importer.ImportAsync(Request(
                $"hidden-bridge-middle-{token}", "monster", name, middleSource, "3.5e", false, 9));
            var last = await importer.ImportAsync(Request(
                $"hidden-bridge-last-{token}", "monster", name, lastSource, "5e", true, 12));

            var firstCanonical = await CanonicalEntityIdAsync(db, Assert.Single(first.Entities).EntityId);
            var middleCanonical = await CanonicalEntityIdAsync(db, Assert.Single(middle.Entities).EntityId);
            var lastCanonical = await CanonicalEntityIdAsync(db, Assert.Single(last.Entities).EntityId);
            await RelateHistoryAsync(db, firstCanonical, middleCanonical);
            await RelateHistoryAsync(db, middleCanonical, lastCanonical);

            var anonymousCatalog = await CatalogAsync(db, null, name);
            var anonymousReference = Assert.Single(
                anonymousCatalog.References.Where(value => value.DisplayName == name));
            var anonymousDetail = await new WikiReferenceCatalogService(db)
                .GetGlobalDetailAsync(null, anonymousReference.ReferenceIdentity);
            Assert.NotNull(anonymousDetail);
            Assert.Equal(2, anonymousDetail!.Variations.Count);
            Assert.Contains(anonymousDetail.Variations, value => value.SourceCode == firstSource);
            Assert.Contains(anonymousDetail.Variations, value => value.SourceCode == lastSource);
            Assert.DoesNotContain(anonymousDetail.Variations, value => value.SourceCode == middleSource);

            await new SourceGrantService(db).GrantAsync("hidden-history-reader", middle.PackageId);
            var grantedCatalog = await CatalogAsync(db, "hidden-history-reader", name);
            var grantedReference = Assert.Single(
                grantedCatalog.References.Where(value => value.DisplayName == name));
            Assert.Equal(anonymousReference.ReferenceIdentity, grantedReference.ReferenceIdentity);

            var grantedDetail = await new WikiReferenceCatalogService(db)
                .GetGlobalDetailAsync("hidden-history-reader", grantedReference.ReferenceIdentity);
            Assert.NotNull(grantedDetail);
            Assert.Equal(3, grantedDetail!.Variations.Count);
            Assert.Contains(grantedDetail.Variations, value => value.SourceCode == middleSource);
        }
    }

    [Fact]
    public async Task ExplicitHomonymSeparationSurvivesRepeatedBackfillAndPreservesRulesDecision()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..10];
            var baseImporter = new NormalizedSourceImportService(db);
            var name = $"Demogorgon {token}";
            var traditional = await baseImporter.ImportAsync(Request(
                $"demogorgon-dnd-{token}", "monster", name, $"DND{token}", "3.5e", true, 200));
            var stranger = await baseImporter.ImportAsync(Request(
                $"demogorgon-st-{token}", "monster", name, $"ST{token}", "5e", true, 80));
            var traditionalEntity = Assert.Single(traditional.Entities);
            var strangerEntity = Assert.Single(stranger.Entities);
            var traditionalCanonical = await CanonicalEntityIdAsync(db, traditionalEntity.EntityId);
            var strangerCanonical = await CanonicalEntityIdAsync(db, strangerEntity.EntityId);
            Assert.NotEqual(traditionalCanonical, strangerCanonical);

            var traditionalRevision = await LatestRevisionIdAsync(db, traditionalEntity.EntityId);
            var rules = new GlobalRulesService(db);
            var concept = await rules.CreateConceptAsync(
                new CreateRuleConceptRequest($"monster.demogorgon-{token}", "monster", name),
                "integration-rules-lawyer");
            await rules.BindSourceEntityAsync(
                concept.Value.Id,
                new BindRuleConceptSourceRequest(traditionalEntity.EntityId),
                "integration-rules-lawyer");
            await rules.SetDecisionAsync(
                concept.Value.Id,
                new SetGlobalRuleDecisionRequest(traditionalRevision, "Traditional Demogorgon remains the published anchor."),
                "integration-rules-lawyer");
            var published = await rules.PublishAsync("integration-rules-lawyer");

            await new CanonicalBootstrapReconciliationService(db).RecordAsync(
                new RecordCanonicalBootstrapReconciliationRequest(
                    "5etools-mirror-3-5etools-src",
                    $"demogorgon-separation-{token}",
                    CanonicalSourceIdentity.Fingerprint($"demogorgon-separation-{token}"),
                    CanonicalBootstrapReconciliationClassifications.SameNameDifferentEntity,
                    traditionalCanonical,
                    strangerCanonical,
                    "authoritative-homonym-separation",
                    1.0,
                    "integration-test",
                    "Traditional D&D and Stranger Things Demogorgon are distinct identities."));

            var reconciliation = new CanonicalDataReconciliationService(db);
            await reconciliation.ReconcileExistingCorpusAsync();
            var firstEdges = await AutomaticHistoryEdgeCountAsync(db, traditionalCanonical, strangerCanonical);
            await reconciliation.ReconcileExistingCorpusAsync();
            var secondEdges = await AutomaticHistoryEdgeCountAsync(db, traditionalCanonical, strangerCanonical);

            Assert.Equal(0, firstEdges);
            Assert.Equal(firstEdges, secondEdges);
            var catalog = await CatalogAsync(db, null, name);
            Assert.Equal(2, catalog.References.Count(value => value.DisplayName == name));

            var decision = await db.GlobalRuleDecisions
                .AsNoTracking()
                .SingleAsync(value => value.RuleConceptId == concept.Value.Id);
            Assert.Equal(traditionalRevision, decision.SelectedSourceEntityRevisionId);
            Assert.True(published.RevisionNumber > 0);
        }
    }

    [Fact]
    public async Task FluffImportsAsAuthorizedCompanionContentAndLegacyStandaloneRowsAreBackfilled()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..10];
            var publicSource = $"PUB{token}";
            var privateSource = $"PRV{token}";
            var goblinName = $"Goblin Fluff {token}";
            var publicRepresentation = ReadFiveETools(
                $"public-{token}.json",
                publicSource,
                goblinName,
                includeRaceAndSpell: true,
                hitPoints: 7);

            Assert.DoesNotContain(publicRepresentation.Records, value =>
                value.EntityType.EndsWith("Fluff", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(3, publicRepresentation.CompanionContents.Count);
            Assert.Contains(publicRepresentation.CompanionContents, value => value.CompanionKind == "monsterFluff");
            Assert.Contains(publicRepresentation.CompanionContents, value => value.CompanionKind == "raceFluff");
            Assert.Contains(publicRepresentation.CompanionContents, value => value.CompanionKind == "spellFluff");

            var importer = new ReconciledNormalizedSourceImportService(new NormalizedSourceImportService(db), db);
            var publicImport = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                $"fluff-public-{token}",
                $"Fluff Public {token}",
                "integration-test",
                null,
                true,
                publicRepresentation));
            Assert.DoesNotContain(
                await db.SourceEntities.Where(value => value.SourcePackageId == publicImport.PackageId).ToArrayAsync(),
                value => value.EntityType.EndsWith("Fluff", StringComparison.OrdinalIgnoreCase));

            var publicCatalog = await CatalogAsync(db, null, goblinName);
            var publicReference = Assert.Single(publicCatalog.References.Where(value => value.DisplayName == goblinName));
            var publicCompanions = await new WikiReferenceCompanionContentService(db)
                .GetGlobalAsync(null, publicReference.ReferenceIdentity);
            Assert.NotNull(publicCompanions);
            var monsterFluff = Assert.Single(publicCompanions!.Contents.Where(value => value.CompanionKind == "monsterFluff"));
            Assert.Equal(publicSource, monsterFluff.SourceCode);
            Assert.True(monsterFluff.Content.TryGetProperty("entries", out _));
            Assert.True(monsterFluff.Content.TryGetProperty("images", out _));

            var raceName = $"Elf Fluff {token}";
            var spellName = $"Light Fluff {token}";
            var raceReference = Assert.Single((await CatalogAsync(db, null, raceName)).References.Where(value => value.DisplayName == raceName));
            var spellReference = Assert.Single((await CatalogAsync(db, null, spellName)).References.Where(value => value.DisplayName == spellName));
            Assert.Contains((await new WikiReferenceCompanionContentService(db).GetGlobalAsync(null, raceReference.ReferenceIdentity))!.Contents,
                value => value.CompanionKind == "raceFluff");
            Assert.Contains((await new WikiReferenceCompanionContentService(db).GetGlobalAsync(null, spellReference.ReferenceIdentity))!.Contents,
                value => value.CompanionKind == "spellFluff");

            var privateRepresentation = ReadFiveETools(
                $"private-{token}.json",
                privateSource,
                goblinName,
                includeRaceAndSpell: false,
                hitPoints: 12);
            var privateImport = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                $"fluff-private-{token}",
                $"Fluff Private {token}",
                "integration-test",
                null,
                false,
                privateRepresentation));

            var anonymousReference = Assert.Single((await CatalogAsync(db, null, goblinName)).References.Where(value => value.DisplayName == goblinName));
            var anonymousCompanions = (await new WikiReferenceCompanionContentService(db)
                .GetGlobalAsync(null, anonymousReference.ReferenceIdentity))!;
            Assert.DoesNotContain(anonymousCompanions.Contents, value => value.SourceCode == privateSource);

            await new SourceGrantService(db).GrantAsync("fluff-reader", privateImport.PackageId);
            var grantedReference = Assert.Single((await CatalogAsync(db, "fluff-reader", goblinName)).References.Where(value => value.DisplayName == goblinName));
            var grantedCompanions = (await new WikiReferenceCompanionContentService(db)
                .GetGlobalAsync("fluff-reader", grantedReference.ReferenceIdentity))!;
            Assert.Contains(grantedCompanions.Contents, value => value.SourceCode == privateSource);

            var legacyName = $"Legacy Goblin Fluff {token}";
            var legacyTarget = await baseImportAsync(db, Request(
                $"legacy-target-{token}", "monster", legacyName, $"LEG{token}", "5e", true, 9));
            var legacyFluff = await baseImportAsync(db, LegacyFluffRequest(
                $"legacy-fluff-{token}", legacyName, $"LEG{token}"));
            var legacyFluffEntityId = Assert.Single(legacyFluff.Entities).EntityId;
            Assert.True(await HasCanonicalBindingAsync(db, legacyFluffEntityId));

            await new CanonicalDataReconciliationService(db).ReconcileExistingCorpusAsync();
            Assert.False(await HasCanonicalBindingAsync(db, legacyFluffEntityId));
            var legacyReference = Assert.Single((await CatalogAsync(db, null, legacyName)).References.Where(value => value.DisplayName == legacyName));
            var legacyCompanions = (await new WikiReferenceCompanionContentService(db)
                .GetGlobalAsync(null, legacyReference.ReferenceIdentity))!;
            Assert.Contains(legacyCompanions.Contents, value => value.CompanionKind == "monsterFluff");
            Assert.NotNull(Assert.Single(legacyTarget.Entities));
        }
    }

    [Fact]
    public async Task CompanionAttachmentCanCrossPackagesWithoutInheritingCompanionVisibility()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..10];
            var sourceCode = $"XPK{token}";
            var name = $"Cross Package Fluff {token}";
            var importer = new ReconciledNormalizedSourceImportService(new NormalizedSourceImportService(db), db);

            await importer.ImportAsync(Request(
                $"cross-package-mechanics-{token}",
                "monster",
                name,
                sourceCode,
                "5e",
                true,
                13));

            var companionOnly = ReadFiveETools(
                $"cross-package-fluff-{token}.json",
                sourceCode,
                name,
                includeRaceAndSpell: false,
                hitPoints: 13) with
            {
                Records = []
            };
            var companionImport = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                $"cross-package-fluff-{token}",
                $"Cross Package Fluff {token}",
                "integration-test",
                null,
                false,
                companionOnly));
            Assert.Empty(await db.SourceEntities
                .Where(value => value.SourcePackageId == companionImport.PackageId)
                .ToArrayAsync());

            var anonymousReference = Assert.Single(
                (await CatalogAsync(db, null, name)).References.Where(value => value.DisplayName == name));
            var anonymousCompanions = await new WikiReferenceCompanionContentService(db)
                .GetGlobalAsync(null, anonymousReference.ReferenceIdentity);
            Assert.NotNull(anonymousCompanions);
            Assert.Empty(anonymousCompanions!.Contents);

            await new SourceGrantService(db).GrantAsync("cross-package-reader", companionImport.PackageId);
            var grantedReference = Assert.Single(
                (await CatalogAsync(db, "cross-package-reader", name)).References.Where(value => value.DisplayName == name));
            Assert.Equal(anonymousReference.ReferenceIdentity, grantedReference.ReferenceIdentity);
            var grantedCompanions = await new WikiReferenceCompanionContentService(db)
                .GetGlobalAsync("cross-package-reader", grantedReference.ReferenceIdentity);
            Assert.NotNull(grantedCompanions);
            var fluff = Assert.Single(grantedCompanions!.Contents);
            Assert.Equal("monsterFluff", fluff.CompanionKind);
            Assert.Equal(sourceCode, fluff.SourceCode);
        }
    }

    [Fact]
    public void CategoryCompatibilityIsExplicitAndDoesNotMakeUnrelatedTypesInterchangeable()
    {
        Assert.True(CanonicalReferenceHistoryPolicy.AreCategoriesCompatible("race", "species"));
        Assert.True(CanonicalReferenceHistoryPolicy.AreCategoriesCompatible("subrace", "subspecies"));
        Assert.True(CanonicalReferenceHistoryPolicy.AreCategoriesCompatible("prestigeClass", "subclass"));
        Assert.True(CanonicalReferenceHistoryPolicy.AreCategoriesCompatible("monster", "monster"));
        Assert.False(CanonicalReferenceHistoryPolicy.AreCategoriesCompatible("monster", "spell"));
        Assert.False(CanonicalReferenceHistoryPolicy.AreCategoriesCompatible("class", "subclass"));
    }

    private static async Task<NormalizedSourceImportResult> baseImportAsync(
        RulesCoreDbContext db,
        ImportNormalizedSourceRequest request) =>
        await new NormalizedSourceImportService(db).ImportAsync(request);

    private static ImportNormalizedSourceRequest Request(
        string packageKey,
        string entityType,
        string name,
        string sourceCode,
        string edition,
        bool isPublic,
        int mechanicValue)
    {
        var raw = JsonSerializer.Serialize(new
        {
            name,
            source = sourceCode,
            hp = mechanicValue,
            marker = $"mechanics-{mechanicValue}"
        });
        var representation = new NormalizedSourceRepresentation(
            "integration-json",
            new SourceRepresentationArtifact(
                $"{packageKey}.json",
                Encoding.UTF8.GetBytes(raw),
                $"integration:{packageKey}"),
            [new NormalizedSourceRecord(
                entityType,
                name,
                sourceCode,
                $"{entityType}|{sourceCode}|{name}|",
                raw,
                PublicationLocalKey: $"source:{sourceCode}")],
            [new NormalizedSourcePublication(
                $"source:{sourceCode}",
                $"Publication {sourceCode}",
                GameEdition: edition,
                PublicationDate: edition switch
                {
                    "3e" => new DateOnly(2000, 1, 1),
                    "3.5e" => new DateOnly(2003, 7, 1),
                    "5e" => new DateOnly(2014, 8, 19),
                    "5.5e" => new DateOnly(2024, 9, 17),
                    _ => null
                })]);
        return new ImportNormalizedSourceRequest(
            packageKey,
            packageKey,
            "integration-test",
            null,
            isPublic,
            representation);
    }

    private static ImportNormalizedSourceRequest LegacyFluffRequest(
        string packageKey,
        string name,
        string sourceCode)
    {
        var raw = JsonSerializer.Serialize(new
        {
            name,
            source = sourceCode,
            entries = new[] { "Legacy lore retained by migration." },
            images = new[] { new { type = "image", href = new { type = "internal", path = "legacy/goblin.webp" } } }
        });
        var representation = new NormalizedSourceRepresentation(
            FiveEToolsSourceFormatAdapter.Format,
            new SourceRepresentationArtifact(
                $"{packageKey}.json",
                Encoding.UTF8.GetBytes(raw),
                $"integration:{packageKey}"),
            [new NormalizedSourceRecord(
                "monsterFluff",
                name,
                sourceCode,
                $"monsterFluff|{sourceCode}|{name}|",
                raw,
                PublicationLocalKey: $"source:{sourceCode}")],
            [new NormalizedSourcePublication(
                $"source:{sourceCode}",
                $"Legacy Publication {sourceCode}",
                GameEdition: "5e",
                PublicationDate: new DateOnly(2014, 8, 19))]);
        return new ImportNormalizedSourceRequest(
            packageKey,
            packageKey,
            "integration-test",
            null,
            true,
            representation);
    }

    private static NormalizedSourceRepresentation ReadFiveETools(
        string fileName,
        string sourceCode,
        string goblinName,
        bool includeRaceAndSpell,
        int hitPoints)
    {
        var token = goblinName.Split(' ').Last();
        var raceName = $"Elf Fluff {token}";
        var spellName = $"Light Fluff {token}";
        var body = new Dictionary<string, object?>
        {
            ["_meta"] = new
            {
                edition = "classic",
                sources = new[]
                {
                    new { json = sourceCode, full = $"Fixture {sourceCode}", dateReleased = "2014-08-19" }
                }
            },
            ["monster"] = new[]
            {
                new { name = goblinName, source = sourceCode, hp = new { average = hitPoints, formula = "2d6" } }
            },
            ["monsterFluff"] = new[]
            {
                new
                {
                    name = goblinName,
                    source = sourceCode,
                    entries = new[] { $"Lore for {goblinName}." },
                    images = new[] { new { type = "image", href = new { type = "internal", path = $"bestiary/{sourceCode}/goblin.webp" } } }
                }
            }
        };
        if (includeRaceAndSpell)
        {
            body["race"] = new[] { new { name = raceName, source = sourceCode, size = new[] { "M" }, speed = 30 } };
            body["raceFluff"] = new[] { new { name = raceName, source = sourceCode, entries = new[] { "Race lore." } } };
            body["spell"] = new[] { new { name = spellName, source = sourceCode, level = 0, school = "E", entries = new[] { "Spell mechanics." } } };
            body["spellFluff"] = new[] { new { name = spellName, source = sourceCode, entries = new[] { "Spell lore." } } };
        }

        var json = JsonSerializer.Serialize(body);
        return new FiveEToolsCompanionSourceFormatAdapter().TryRead(new SourceRepresentationArtifact(
            fileName,
            Encoding.UTF8.GetBytes(json),
            $"integration:{fileName}",
            $"https://raw.githubusercontent.com/5etools-mirror-3/5etools-src/main/data/{fileName}",
            "application/json"))
            ?? throw new InvalidOperationException("5e.tools fixture was not readable.");
    }

    private static Task<WikiReferenceCatalogView> CatalogAsync(
        RulesCoreDbContext db,
        string? userId,
        string query) =>
        new WikiReferenceCatalogService(db).GetGlobalCatalogAsync(
            userId,
            entityType: null,
            categoryMode: WikiReferenceCategoryModes.AnyVariation,
            query,
            sourceCode: null,
            packageKey: null,
            edition: null,
            limit: 100,
            offset: 0);

    private static async Task<Guid> CanonicalEntityIdAsync(RulesCoreDbContext db, Guid sourceEntityId)
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
                JOIN source_entity_revision revision
                    ON revision.source_entity_revision_id = binding.source_entity_revision_id
                WHERE binding.source_entity_id = @source_entity_id
                  AND occurrence.canonical_entity_id IS NOT NULL
                ORDER BY revision.revision_number DESC
                LIMIT 1;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            return (Guid)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Canonical entity binding was missing."));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<Guid> LatestRevisionIdAsync(RulesCoreDbContext db, Guid sourceEntityId) =>
        await db.SourceEntityRevisions
            .Where(value => value.SourceEntityId == sourceEntityId)
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => value.Id)
            .FirstAsync();

    private static async Task<bool> HasCanonicalBindingAsync(RulesCoreDbContext db, Guid sourceEntityId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT EXISTS (
                    SELECT 1
                    FROM source_entity_occurrence_binding
                    WHERE source_entity_id = @source_entity_id);
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            return Convert.ToBoolean(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task RelateHistoryAsync(
        RulesCoreDbContext db,
        Guid fromCanonicalEntityId,
        Guid toCanonicalEntityId)
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
                VALUES (
                    @id,
                    @from_id,
                    @to_id,
                    'revision',
                    'integration-hidden-bridge',
                    1.0,
                    @created_at)
                ON CONFLICT (from_canonical_entity_id, to_canonical_entity_id, relationship_kind)
                DO NOTHING;
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@from_id", fromCanonicalEntityId);
            AddParameter(command, "@to_id", toCanonicalEntityId);
            AddParameter(command, "@created_at", DateTimeOffset.UtcNow);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<int> AutomaticHistoryEdgeCountAsync(
        RulesCoreDbContext db,
        Guid left,
        Guid right)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM canonical_entity_relationship
                WHERE evidence_kind = 'normalized-name-compatible-category'
                  AND ((from_canonical_entity_id = @left AND to_canonical_entity_id = @right)
                    OR (from_canonical_entity_id = @right AND to_canonical_entity_id = @left));
                """;
            AddParameter(command, "@left", left);
            AddParameter(command, "@right", right);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<RulesCoreDbContext?> OpenDatabaseAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        return db;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
