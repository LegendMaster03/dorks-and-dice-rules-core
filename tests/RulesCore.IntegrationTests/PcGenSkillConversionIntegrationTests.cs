using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class PcGenSkillConversionIntegrationTests
{
    private static readonly (string Source, string Target)[] DirectSkillConversions =
    [
        ("Bluff", "Deception"),
        ("Diplomacy", "Persuasion"),
        ("Handle Animal", "Animal Handling"),
        ("Heal", "Medicine"),
        ("Intimidate", "Intimidation"),
        ("Knowledge (arcana)", "Arcana"),
        ("Knowledge (history)", "History"),
        ("Knowledge (nature)", "Nature"),
        ("Knowledge (religion)", "Religion"),
        ("Knowledge (Psionics)", "Psionics"),
        ("Knowledge (the planes)", "The planes"),
        ("Sense Motive", "Insight"),
        ("Sleight of Hand", "Sleight of Hand"),
        ("Survival", "Survival")
    ];

    private static readonly (string Source, string Target, string IdentityKey, string IdentityName)[] SharedToolFacets =
    [
        ("Craft (alchemy)", "Alchemist's Supplies", "alchemy", "Alchemy"),
        ("Craft (calligraphy)", "Calligrapher's Supplies", "calligraphy", "Calligraphy"),
        ("Craft (carpentry)", "Carpenter's Tools", "carpentry", "Carpentry"),
        ("Craft (cobbling)", "Cobbler's Tools", "cobbling", "Cobbling"),
        ("Craft (gemcutting)", "Jeweler's Tools", "gemcutting", "Gemcutting"),
        ("Craft (leatherworking)", "Leatherworker's Tools", "leatherworking", "Leatherworking"),
        ("Craft (painting)", "Painter's Supplies", "painting", "Painting"),
        ("Craft (pottery)", "Potter's Tools", "pottery", "Pottery"),
        ("Craft (stonemasonry)", "Mason's Tools", "stonemasonry", "Stonemasonry"),
        ("Craft (weaving)", "Weaver's Tools", "weaving", "Weaving"),
        ("Forgery", "Forgery Kit", "forgery", "Forgery")
    ];

    private static readonly string[] ExcludedDirectConversions =
    [
        "Perform (dance)",
        "Balance",
        "Hide",
        "Move Silently",
        "Listen",
        "Spot",
        "Ride",
        "Spellcraft",
        "Craft (blacksmithing)"
    ];

    [Theory]
    [InlineData("Glassblower's Tools", "glassblowing", "Glassblowing")]
    [InlineData("Cartographer's Tools", "cartography", "Cartography")]
    [InlineData("Poisoner's Kit", "poisoning", "Poisoning")]
    [InlineData("Thieves' Tools", "thieves-tools", "Thieves' Tools")]
    public void LaterToolOnlyCompetenciesReceiveStandaloneUniversalIdentity(
        string toolName,
        string identityKey,
        string identityName)
    {
        var profile = BuildCompetencyProfileForTest(
            Guid.NewGuid(),
            "tool",
            toolName,
            JsonSerializer.Serialize(new { name = toolName }),
            "5e");

        Assert.NotNull(profile);
        Assert.Equal(identityKey, profile!.IdentityKey);
        Assert.Equal(identityName, profile.IdentityName);
        Assert.Equal("tool", profile.FacetType);
        Assert.False(profile.SupportsRanks);
        Assert.True(profile.SupportsTrainingState);
        Assert.Equal($"competency.{identityKey}.training", profile.SharedTrainingKey);
    }

    [Fact]
    public void SmithsToolsRemainIndependentAndRelatedToMultipleCraftSpecialties()
    {
        var profile = BuildCompetencyProfileForTest(
            Guid.NewGuid(),
            "tool",
            "Smith's Tools",
            JsonSerializer.Serialize(new { name = "Smith's Tools" }),
            "5.5e");

        Assert.NotNull(profile);
        Assert.Equal("smithing", profile!.IdentityKey);
        Assert.Equal("Smithing", profile.IdentityName);
        Assert.False(profile.SupportsRanks);
        Assert.Equal(
            ["Armorsmithing", "Blacksmithing", "Weaponsmithing"],
            profile.RelatedCompetencies!
                .Select(value => value.TargetName)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
        Assert.All(
            profile.RelatedCompetencies!,
            value =>
            {
                Assert.Equal("related-competency", value.Kind);
                Assert.Equal("competency", value.TargetType);
                Assert.False(value.SharesTrainingState);
            });
    }

    [Fact]
    public async Task SpecializedCompetencyFamiliesAreTaxonomyWhileKnowledgeNormalizesDirectly()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var packageKey = $"pcgen-competency-families-{token}";

            try
            {
                await new NormalizedSourceImportService(db).ImportAsync(
                    new ImportNormalizedSourceRequest(
                        packageKey,
                        $"PCGen competency family fixture {token}",
                        "integration-test",
                        "test-only",
                        true,
                        PcGenRepresentation(
                            "35e",
                            $"FAM{token}",
                            [
                                "Craft",
                                "Craft (blacksmithing)",
                                "Perform",
                                "Perform (dance)",
                                "Profession",
                                "Profession (sailor)",
                                "Knowledge (arcana)"
                            ])));

                foreach (var family in new[] { "Craft", "Perform", "Profession" })
                {
                    var row = await ReadByNativeNameAsync(db, packageKey, family);
                    using var document = JsonDocument.Parse(row.ContentJson);
                    var competency = document.RootElement
                        .GetProperty("_rulesCore")
                        .GetProperty("competency");
                    Assert.Equal("skill", competency.GetProperty("kind").GetString());
                    Assert.Equal(family, competency.GetProperty("familyName").GetString());
                    Assert.True(competency.GetProperty("isFamily").GetBoolean());
                    Assert.False(competency.TryGetProperty("specialty", out _));
                    Assert.True(competency.GetProperty("supportsRanks").GetBoolean());
                    Assert.True(competency.GetProperty("supportsClassSkillState").GetBoolean());
                }

                foreach (var (name, family, specialty) in new[]
                         {
                             ("Craft (blacksmithing)", "Craft", "blacksmithing"),
                             ("Perform (dance)", "Perform", "dance"),
                             ("Profession (sailor)", "Profession", "sailor")
                         })
                {
                    var row = await ReadByNativeNameAsync(db, packageKey, name);
                    using var document = JsonDocument.Parse(row.ContentJson);
                    var competency = document.RootElement
                        .GetProperty("_rulesCore")
                        .GetProperty("competency");
                    Assert.Equal(
                        "specialized-skill",
                        competency.GetProperty("kind").GetString());
                    Assert.Equal(family, competency.GetProperty("familyName").GetString());
                    Assert.Equal(specialty, competency.GetProperty("specialty").GetString());
                    Assert.False(competency.TryGetProperty("isFamily", out _));
                    Assert.True(competency.GetProperty("supportsRanks").GetBoolean());
                    Assert.True(competency.GetProperty("supportsClassSkillState").GetBoolean());
                }

                var knowledge = await ReadByNativeNameAsync(
                    db,
                    packageKey,
                    "Knowledge (arcana)");
                Assert.Equal("skill", knowledge.EntityType);
                Assert.Equal("Arcana", knowledge.NormalizedName);
                using (var document = JsonDocument.Parse(knowledge.ContentJson))
                {
                    var competency = document.RootElement
                        .GetProperty("_rulesCore")
                        .GetProperty("competency");
                    Assert.Equal("skill", competency.GetProperty("kind").GetString());
                    Assert.False(competency.TryGetProperty("familyName", out _));
                    Assert.False(competency.TryGetProperty("specialty", out _));
                    Assert.False(competency.TryGetProperty("isFamily", out _));
                }
            }
            finally
            {
                await DeletePackageAsync(db, packageKey);
            }
        }
    }

    [Fact]
    public async Task SharedAlchemyFacetsKeepSeparateCanonicalIdentities()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var skillPackage = $"pcgen-alchemy-skill-{token}";
            var toolPackage = $"fivee-alchemy-tool-{token}";

            try
            {
                var skillImport = await new NormalizedSourceImportService(db).ImportAsync(
                    new ImportNormalizedSourceRequest(
                        skillPackage,
                        $"PCGen alchemy skill fixture {token}",
                        "integration-test",
                        "test-only",
                        true,
                        PcGenRepresentation("35e", $"ALK{token}", ["Craft (alchemy)"])));
                var toolImport = await new NormalizedSourceImportService(db).ImportAsync(
                    new ImportNormalizedSourceRequest(
                        toolPackage,
                        $"5e alchemy tool fixture {token}",
                        "integration-test",
                        "test-only",
                        true,
                        FiveEToolRepresentation(token, "Alchemist's Supplies")));

                var skillEntity = Assert.Single(skillImport.Entities);
                Assert.Equal("skill", skillEntity.EntityType);
                Assert.Equal("Craft (alchemy)", skillEntity.Name);
                var toolEntity = Assert.Single(toolImport.Entities);
                Assert.Equal("tool", toolEntity.EntityType);
                Assert.Equal("Alchemist's Supplies", toolEntity.Name);

                Assert.NotEqual(
                    await ReadCanonicalEntityIdAsync(db, skillEntity.EntityId),
                    await ReadCanonicalEntityIdAsync(db, toolEntity.EntityId));

                var skillCandidate = Assert.Single(
                    await new SourceNormalizationService(db).GetCandidatesAsync(
                        $"rules-lawyer-{token}",
                        entityType: "skill",
                        query: "Craft (alchemy)"),
                    value => value.PackageKey == skillPackage);
                Assert.Equal("skill.craft-alchemy", skillCandidate.SuggestedConceptKey);

                var toolCandidate = Assert.Single(
                    await new SourceNormalizationService(db).GetCandidatesAsync(
                        $"rules-lawyer-{token}",
                        entityType: "tool",
                        query: "Alchemist's Supplies"),
                    value => value.PackageKey == toolPackage);
                Assert.Equal("tool.alchemists-supplies", toolCandidate.SuggestedConceptKey);

                var skillRow = await ReadByNativeNameAsync(
                    db,
                    skillPackage,
                    "Craft (alchemy)");
                AssertSharedFacet(
                    skillRow.ContentJson,
                    "Craft (alchemy)",
                    "Alchemist's Supplies",
                    "alchemy",
                    "Alchemy");
            }
            finally
            {
                await DeletePackageAsync(db, skillPackage);
                await DeletePackageAsync(db, toolPackage);
            }
        }
    }

    [Fact]
    public async Task ApprovedMappingsAreImporterTranslationsAndPreserveNativePcGenEvidence()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var package35 = $"pcgen-skill-translation-35-{token}";
            var package30 = $"pcgen-skill-translation-30-{token}";
            var importer = new NormalizedSourceImportService(db);

            try
            {
                await importer.ImportAsync(new ImportNormalizedSourceRequest(
                    package35,
                    $"PCGen 3.5 skill translation fixture {token}",
                    "integration-test",
                    "test-only",
                    false,
                    PcGenRepresentation(
                        "35e",
                        $"S35{token}",
                        DirectSkillConversions.Select(value => value.Source)
                            .Concat(SharedToolFacets.Select(value => value.Source))
                            .Concat(["Open Lock", "Disable Device", "Disguise", "Pick Pocket", "Wilderness Lore", "Alchemy"])
                            .Concat(ExcludedDirectConversions)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray())));

                await importer.ImportAsync(new ImportNormalizedSourceRequest(
                    package30,
                    $"PCGen 3.0 skill translation fixture {token}",
                    "integration-test",
                    "test-only",
                    false,
                    PcGenRepresentation(
                        "3e",
                        $"S30{token}",
                        ["Pick Pocket", "Wilderness Lore", "Alchemy"])));

                foreach (var (source, target) in DirectSkillConversions)
                {
                    var row = await ReadByNativeNameAsync(db, package35, source);
                    Assert.Equal("skill", row.EntityType);
                    Assert.Equal(target, row.NormalizedName);
                    AssertNativeSourceName(row.RawJson, source);
                    AssertExactTranslation(row.ContentJson, source, "skill", target);
                }

                foreach (var (source, target, identityKey, identityName) in SharedToolFacets)
                {
                    var row = await ReadByNativeNameAsync(db, package35, source);
                    Assert.Equal("skill", row.EntityType);
                    Assert.Equal(source, row.NormalizedName);
                    AssertNativeSourceName(row.RawJson, source);
                    AssertSharedFacet(
                        row.ContentJson,
                        source,
                        target,
                        identityKey,
                        identityName);
                }

                var openLock = await ReadByNativeNameAsync(db, package35, "Open Lock");
                AssertScopedCompetency(
                    openLock.ContentJson,
                    "Open Lock",
                    "Thieves' Tools",
                    "open-lock");

                var disableDevice = await ReadByNativeNameAsync(db, package35, "Disable Device");
                AssertScopedCompetency(
                    disableDevice.ContentJson,
                    "Disable Device",
                    "Thieves' Tools",
                    "disable-device");

                var disguise = await ReadByNativeNameAsync(db, package35, "Disguise");
                AssertRelatedCompetency(
                    disguise.ContentJson,
                    "Disguise",
                    "skill",
                    "Disguise Kit",
                    "tool");

                foreach (var source in ExcludedDirectConversions.Concat(["Pick Pocket", "Wilderness Lore", "Alchemy"]))
                {
                    var row = await ReadByNativeNameAsync(db, package35, source);
                    Assert.Equal("skill", row.EntityType);
                    Assert.Equal(source, row.NormalizedName);
                    AssertNoExactTranslation(row.ContentJson);
                }

                var pickPocket = await ReadByNativeNameAsync(db, package30, "Pick Pocket");
                AssertNativeSourceName(pickPocket.RawJson, "Pick Pocket");
                AssertExactTranslation(
                    pickPocket.ContentJson,
                    "Pick Pocket",
                    "skill",
                    "Sleight of Hand");

                var wildernessLore = await ReadByNativeNameAsync(
                    db,
                    package30,
                    "Wilderness Lore");
                AssertNativeSourceName(wildernessLore.RawJson, "Wilderness Lore");
                AssertExactTranslation(
                    wildernessLore.ContentJson,
                    "Wilderness Lore",
                    "skill",
                    "Survival");
                var alchemy = await ReadByNativeNameAsync(db, package30, "Alchemy");
                Assert.Equal("skill", alchemy.EntityType);
                Assert.Equal("Alchemy", alchemy.NormalizedName);
                AssertSharedFacet(
                    alchemy.ContentJson,
                    "Alchemy",
                    "Alchemist's Supplies",
                    "alchemy",
                    "Alchemy");
            }
            finally
            {
                await DeletePackageAsync(db, package35);
                await DeletePackageAsync(db, package30);
            }
        }
    }

    [Fact]
    public async Task BluffAndDeceptionShareCanonicalIdentityAndDoNotRequireSecondRulesLawyerBinding()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var fivePackage = $"exact-deception-5e-{token}";
            var pcgenPackage = $"exact-bluff-35-{token}";
            var importer = new NormalizedSourceImportService(db);
            AcceptedSourceNormalizationView? accepted = null;

            try
            {
                var fiveImport = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                    fivePackage,
                    $"5e Deception fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    FiveEDeceptionRepresentation(token)));
                var fiveEntity = Assert.Single(fiveImport.Entities);
                Assert.Equal("skill", fiveEntity.EntityType);
                Assert.Equal("Deception", fiveEntity.Name);

                var normalization = new SourceNormalizationService(db);
                accepted = await normalization.AcceptAsync(fiveEntity.EntityId, $"rules-lawyer-{token}");
                Assert.NotNull(accepted);
                Assert.Equal("skill.deception", accepted!.Concept.Key);

                var pcgenImport = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                    pcgenPackage,
                    $"3.5 Bluff fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    PcGenRepresentation("35e", $"B35{token}", ["Bluff"])));
                var bluffEntity = Assert.Single(pcgenImport.Entities);
                Assert.Equal("skill", bluffEntity.EntityType);
                Assert.Equal("Deception", bluffEntity.Name);

                Assert.Equal(
                    await ReadCanonicalEntityIdAsync(db, fiveEntity.EntityId),
                    await ReadCanonicalEntityIdAsync(db, bluffEntity.EntityId));

                var candidates = await normalization.GetCandidatesAsync(
                    $"rules-lawyer-{token}",
                    entityType: "skill",
                    query: "Deception",
                    limit: 100);
                Assert.DoesNotContain(candidates, value => value.SourceEntityId == bluffEntity.EntityId);

                var bluffRow = await ReadByNativeNameAsync(db, pcgenPackage, "Bluff");
                AssertNativeSourceName(bluffRow.RawJson, "Bluff");
                AssertExactTranslation(bluffRow.ContentJson, "Bluff", "skill", "Deception");
            }
            finally
            {
                if (accepted?.CreatedBinding == true)
                {
                    var binding = await db.RuleConceptSourceBindings
                        .SingleOrDefaultAsync(value => value.Id == accepted.Binding.Id);
                    if (binding is not null)
                    {
                        db.RuleConceptSourceBindings.Remove(binding);
                        await db.SaveChangesAsync();
                    }
                }
                if (accepted?.CreatedConcept == true)
                {
                    var concept = await db.RuleConcepts
                        .SingleOrDefaultAsync(value => value.Id == accepted.Concept.Id);
                    if (concept is not null)
                    {
                        db.RuleConcepts.Remove(concept);
                        await db.SaveChangesAsync();
                    }
                }
                await DeletePackageAsync(db, fivePackage);
                await DeletePackageAsync(db, pcgenPackage);
            }
        }
    }

    [Fact]
    public async Task PersistedLegacyCrossTypeFacetKeepsStableRuleConceptReferenceAfterReimport()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var packageKey = $"pcgen-shared-facet-migration-{token}";
            var actor = $"rules-lawyer-{token}";
            var representation = PcGenRepresentation(
                "35e",
                $"SFM{token}",
                ["Craft (alchemy)"]);
            var importer = new NormalizedSourceImportService(db);
            AcceptedSourceNormalizationView? accepted = null;

            try
            {
                var first = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                    packageKey,
                    $"PCGen shared facet migration fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    representation));
                var source = Assert.Single(first.Entities);
                Assert.Equal("skill", source.EntityType);
                Assert.Equal("Craft (alchemy)", source.Name);

                var entity = await db.SourceEntities.SingleAsync(value => value.Id == source.EntityId);
                var revisionId = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == source.EntityId)
                    .Select(value => value.Id)
                    .SingleAsync();

                // Reconstruct the previously persisted cross-type normalization. This is derived
                // Rules Core metadata only; the native key, source code, raw evidence, and revision
                // identity remain unchanged.
                entity.EntityType = "tool";
                entity.Name = "Alchemist's Supplies";
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();

                var normalization = new SourceNormalizationService(db);
                accepted = await normalization.AcceptAsync(source.EntityId, actor);
                Assert.NotNull(accepted);
                Assert.Equal("tool.alchemists-supplies", accepted!.Concept.Key);
                var stableConceptId = accepted.Concept.Id;
                var stableBindingId = accepted.Binding.Id;

                var globalRules = new GlobalRulesService(db);
                await globalRules.SetDecisionAsync(
                    stableConceptId,
                    new SetGlobalRuleDecisionRequest(
                        revisionId,
                        "Legacy Character compatibility fixture."),
                    actor);
                await globalRules.PublishAsync(actor);

                var reimport = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                    packageKey,
                    $"PCGen shared facet migration fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    representation));
                var migrated = Assert.Single(reimport.Entities);
                Assert.Equal(source.EntityId, migrated.EntityId);
                Assert.Equal("skill", migrated.EntityType);
                Assert.Equal("Craft (alchemy)", migrated.Name);

                var persistedEntity = await db.SourceEntities
                    .AsNoTracking()
                    .SingleAsync(value => value.Id == source.EntityId);
                Assert.Equal("skill", persistedEntity.EntityType);
                Assert.Equal("Craft (alchemy)", persistedEntity.Name);

                var migratedRevisionId = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == source.EntityId)
                    .Select(value => value.Id)
                    .SingleAsync();
                Assert.Equal(revisionId, migratedRevisionId);

                var binding = await db.RuleConceptSourceBindings
                    .AsNoTracking()
                    .SingleAsync(value => value.Id == stableBindingId);
                Assert.Equal(stableConceptId, binding.RuleConceptId);
                Assert.Equal(source.EntityId, binding.SourceEntityId);
                var concept = await db.RuleConcepts
                    .AsNoTracking()
                    .SingleAsync(value => value.Id == stableConceptId);
                Assert.Equal("tool.alchemists-supplies", concept.Key);

                var postMigrationCandidates = await normalization.GetCandidatesAsync(
                    actor,
                    entityType: "skill",
                    query: "Craft (alchemy)");
                Assert.DoesNotContain(
                    postMigrationCandidates,
                    value => value.SourceEntityId == source.EntityId);
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    normalization.AcceptAsync(source.EntityId, actor));

                var catalog = await new CharacterMechanicsConsumerService(db)
                    .GetGlobalAsync(userId: null);
                var legacyMechanic = Assert.Single(
                    catalog.Mechanics,
                    value => value.MechanicKey == "competency.tool.alchemists-supplies");
                Assert.NotNull(legacyMechanic.Competency);
                Assert.Equal("alchemy", legacyMechanic.Competency!.IdentityKey);
                Assert.Equal("Alchemy", legacyMechanic.Competency.IdentityName);
                Assert.Equal(
                    "competency.alchemy.training",
                    legacyMechanic.Competency.SharedTrainingKey);
                var profile = Assert.Single(legacyMechanic.Competency.Profiles);
                Assert.Equal("skill", profile.FacetType);
                Assert.True(profile.SupportsRanks);
                Assert.True(profile.SupportsClassSkillState);
            }
            finally
            {
                if (accepted is not null)
                {
                    var rulesetRevisionIds = await db.RulesetRevisionEntries
                        .Where(value => value.RuleConceptId == accepted.Concept.Id)
                        .Select(value => value.RulesetRevisionId)
                        .Distinct()
                        .ToArrayAsync();
                    await db.RulesetRevisionEntries
                        .Where(value => value.RuleConceptId == accepted.Concept.Id)
                        .ExecuteDeleteAsync();
                    if (rulesetRevisionIds.Length > 0)
                    {
                        await db.RulesetRevisions
                            .Where(value => rulesetRevisionIds.Contains(value.Id)
                                && !value.Entries.Any())
                            .ExecuteDeleteAsync();
                    }
                    await db.GlobalRuleDecisions
                        .Where(value => value.RuleConceptId == accepted.Concept.Id)
                        .ExecuteDeleteAsync();
                }
                if (accepted?.CreatedBinding == true)
                {
                    await db.RuleConceptSourceBindings
                        .Where(value => value.Id == accepted.Binding.Id)
                        .ExecuteDeleteAsync();
                }
                if (accepted?.CreatedConcept == true)
                {
                    await db.RuleConcepts
                        .Where(value => value.Id == accepted.Concept.Id)
                        .ExecuteDeleteAsync();
                }
                db.ChangeTracker.Clear();
                await DeletePackageAsync(db, packageKey);
            }
        }
    }

    [Fact]
    public async Task PcGenCompetencySemanticsAreNormalizedWithoutDiscardingNativeEvidence()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var packageKey = $"pcgen-competency-metadata-{token}";
            var sourceShort = $"PCM{token}";
            var fileName = $"data/35e/example/competency_metadata_skills_{token}.lst";
            var text = string.Join('\n',
            [
                $"SOURCELONG:Competency Metadata Fixture {token}\tSOURCESHORT:{sourceShort}",
                "Knowledge (the planes)\tKEYSTAT:INT\tUSEUNTRAINED:NO\tACHECK:NO",
                "Craft (alchemy)\tKEYSTAT:INT\tUSEUNTRAINED:YES\tACHECK:NO"
            ]);
            var representation = new PcGenSourceFormatAdapter().TryRead(
                new SourceRepresentationArtifact(
                    fileName,
                    Encoding.UTF8.GetBytes(text),
                    $"integration:competency-metadata:{token}#{fileName}"))
                ?? throw new InvalidOperationException("PCGen competency metadata fixture was not readable.");

            try
            {
                await new NormalizedSourceImportService(db).ImportAsync(
                    new ImportNormalizedSourceRequest(
                        packageKey,
                        $"PCGen competency metadata {token}",
                        "integration-test",
                        "test-only",
                        true,
                        representation));

                var knowledge = await ReadByNativeNameAsync(db, packageKey, "Knowledge (the planes)");
                using (var document = JsonDocument.Parse(knowledge.ContentJson))
                {
                    var extension = document.RootElement.GetProperty("_rulesCore");
                    var competency = extension.GetProperty("competency");
                    Assert.Equal("dnd-3x", competency.GetProperty("profileKey").GetString());
                    Assert.Equal("skill", competency.GetProperty("kind").GetString());
                    Assert.False(competency.TryGetProperty("familyName", out _));
                    Assert.False(competency.TryGetProperty("specialty", out _));
                    Assert.Equal("intelligence", competency.GetProperty("governingAbilityKey").GetString());
                    Assert.True(competency.GetProperty("supportsRanks").GetBoolean());
                    Assert.True(competency.GetProperty("supportsClassSkillState").GetBoolean());
                    Assert.True(competency.GetProperty("supportsTrainingState").GetBoolean());
                    Assert.True(competency.GetProperty("trainedOnly").GetBoolean());
                    Assert.False(competency.GetProperty("armorCheckPenaltyApplies").GetBoolean());
                    Assert.Equal(
                        "competency.skill-ranks",
                        Assert.Single(competency.GetProperty("requiredCapabilityKeys").EnumerateArray())
                            .GetString());

                    var tags = extension.GetProperty("pcgen")
                        .GetProperty("unmappedSegments")
                        .EnumerateArray()
                        .Select(value => value.GetProperty("tag").GetString())
                        .ToArray();
                    Assert.Contains("KEYSTAT", tags);
                    Assert.Contains("USEUNTRAINED", tags);
                    Assert.Contains("ACHECK", tags);
                }

                var alchemy = await ReadByNativeNameAsync(db, packageKey, "Craft (alchemy)");
                Assert.Equal("skill", alchemy.EntityType);
                Assert.Equal("Craft (alchemy)", alchemy.NormalizedName);
                using (var document = JsonDocument.Parse(alchemy.ContentJson))
                {
                    var competency = document.RootElement
                        .GetProperty("_rulesCore")
                        .GetProperty("competency");
                    Assert.Equal("specialized-skill", competency.GetProperty("kind").GetString());
                    Assert.Equal("Craft", competency.GetProperty("familyName").GetString());
                    Assert.Equal("alchemy", competency.GetProperty("specialty").GetString());
                    Assert.Equal("skill", competency.GetProperty("facetType").GetString());
                    Assert.Equal("alchemy", competency.GetProperty("identityKey").GetString());
                    Assert.Equal("Alchemy", competency.GetProperty("identityName").GetString());
                    Assert.Equal(
                        "competency.alchemy.training",
                        competency.GetProperty("sharedTrainingKey").GetString());
                    Assert.True(competency.GetProperty("supportsRanks").GetBoolean());
                    Assert.True(competency.GetProperty("supportsClassSkillState").GetBoolean());
                }
            }
            finally
            {
                await DeletePackageAsync(db, packageKey);
            }
        }
    }

    [Fact]
    public async Task ReviewedLegacySrdUsesTheSameDirectCompetencyConversionsAndMetadata()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var packageKey = $"legacy-srd-competency-{token}";
            var representation = LegacySrdRepresentation(
                token,
                "SRD35",
                [
                    "Bluff",
                    "Craft (alchemy)",
                    "Craft (blacksmithing)",
                    "Open Lock",
                    "Knowledge (the planes)",
                    "Perform (dance)",
                    "Profession (sailor)"
                ]);

            try
            {
                await new NormalizedSourceImportService(db).ImportAsync(
                    new ImportNormalizedSourceRequest(
                        packageKey,
                        $"Legacy SRD competency fixture {token}",
                        "integration-test",
                        "test-only",
                        true,
                        representation));

                var bluff = await ReadByNativeNameAsync(db, packageKey, "Bluff");
                Assert.Equal("skill", bluff.EntityType);
                Assert.Equal("Deception", bluff.NormalizedName);
                AssertNativeSourceName(bluff.RawJson, "Bluff");
                AssertExactTranslation(bluff.ContentJson, "Bluff", "skill", "Deception");
                AssertLegacyCompetencyProfile(
                    bluff.ContentJson,
                    expectedKind: "skill",
                    family: null,
                    specialty: null);

                var alchemy = await ReadByNativeNameAsync(db, packageKey, "Craft (alchemy)");
                Assert.Equal("skill", alchemy.EntityType);
                Assert.Equal("Craft (alchemy)", alchemy.NormalizedName);
                AssertNativeSourceName(alchemy.RawJson, "Craft (alchemy)");
                AssertSharedFacet(
                    alchemy.ContentJson,
                    "Craft (alchemy)",
                    "Alchemist's Supplies",
                    "alchemy",
                    "Alchemy");
                AssertLegacyCompetencyProfile(
                    alchemy.ContentJson,
                    expectedKind: "specialized-skill",
                    family: "Craft",
                    specialty: "alchemy");

                var craft = await ReadByNativeNameAsync(
                    db,
                    packageKey,
                    "Craft (blacksmithing)");
                Assert.Equal("skill", craft.EntityType);
                Assert.Equal("Craft (blacksmithing)", craft.NormalizedName);
                AssertLegacyCompetencyProfile(
                    craft.ContentJson,
                    expectedKind: "specialized-skill",
                    family: "Craft",
                    specialty: "blacksmithing");

                var openLock = await ReadByNativeNameAsync(db, packageKey, "Open Lock");
                Assert.Equal("skill", openLock.EntityType);
                Assert.Equal("Open Lock", openLock.NormalizedName);
                using (var document = JsonDocument.Parse(openLock.ContentJson))
                {
                    var extension = document.RootElement.GetProperty("_rulesCore");
                    var conversion = extension.GetProperty("competencyConversion");
                    Assert.Equal("Thieves' Tools", conversion.GetProperty("targetName").GetString());
                    Assert.Equal("open-lock", conversion.GetProperty("scope").GetString());
                }
                AssertLegacyCompetencyProfile(
                    openLock.ContentJson,
                    expectedKind: "skill",
                    family: null,
                    specialty: null);

                var knowledge = await ReadByNativeNameAsync(
                    db,
                    packageKey,
                    "Knowledge (the planes)");
                Assert.Equal("skill", knowledge.EntityType);
                Assert.Equal("The planes", knowledge.NormalizedName);
                AssertLegacyCompetencyProfile(
                    knowledge.ContentJson,
                    expectedKind: "skill",
                    family: null,
                    specialty: null);

                var perform = await ReadByNativeNameAsync(
                    db,
                    packageKey,
                    "Perform (dance)");
                Assert.Equal("skill", perform.EntityType);
                Assert.Equal("Perform (dance)", perform.NormalizedName);
                AssertLegacyCompetencyProfile(
                    perform.ContentJson,
                    expectedKind: "specialized-skill",
                    family: "Perform",
                    specialty: "dance");

                var profession = await ReadByNativeNameAsync(
                    db,
                    packageKey,
                    "Profession (sailor)");
                Assert.Equal("skill", profession.EntityType);
                Assert.Equal("Profession (sailor)", profession.NormalizedName);
                AssertLegacyCompetencyProfile(
                    profession.ContentJson,
                    expectedKind: "specialized-skill",
                    family: "Profession",
                    specialty: "sailor");
            }
            finally
            {
                await DeletePackageAsync(db, packageKey);
            }
        }
    }

    [Fact]
    public async Task ReviewedLegacySrdCanUpgradePreviouslyPersistedNormalizedNameWithoutNewNativeRevision()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var packageKey = $"legacy-srd-competency-upgrade-{token}";
            var representation = LegacySrdRepresentation(token, "SRD35", ["Bluff"]);
            var importer = new NormalizedSourceImportService(db);

            try
            {
                await importer.ImportAsync(new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Legacy SRD competency upgrade fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    representation));

                var entity = await db.SourceEntities
                    .Include(value => value.Revisions)
                    .SingleAsync(value => value.SourcePackage.Key == packageKey);
                var sourceEntityId = entity.Id;
                var sourceRevisionId = Assert.Single(entity.Revisions).Id;

                // Reconstruct the pre-convergence persisted metadata while leaving the immutable
                // native key, RawJson, revision, and canonical/Rules Layer references intact.
                entity.Name = "Bluff";
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();

                await importer.ImportAsync(new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Legacy SRD competency upgrade fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    representation));

                var upgraded = await db.SourceEntities
                    .Include(value => value.Revisions)
                    .SingleAsync(value => value.Id == sourceEntityId);
                Assert.Equal("Deception", upgraded.Name);
                Assert.Equal("skill", upgraded.EntityType);
                Assert.Equal(sourceRevisionId, Assert.Single(upgraded.Revisions).Id);
            }
            finally
            {
                await DeletePackageAsync(db, packageKey);
            }
        }
    }


    [Fact]
    public async Task PcGenSizeTranslationPromotesAllUniversalCodesAndPreservesUnknownSourceValues()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var packageKey = $"pcgen-size-normalization-{token}";
            var sourceShort = $"SZ{token}";
            var fileName = $"data/35e/example/example_races_{token}.lst";
            var expected = new[]
            {
                ("Fine Fixture", "F", "Fine"),
                ("Diminutive Fixture", "D", "Diminutive"),
                ("Tiny Fixture", "T", "Tiny"),
                ("Small Fixture", "S", "Small"),
                ("Medium Fixture", "M", "Medium"),
                ("Large Fixture", "L", "Large"),
                ("Huge Fixture", "H", "Huge"),
                ("Gargantuan Fixture", "G", "Gargantuan"),
                ("Colossal Fixture", "C", "Colossal")
            };

            var text = string.Join('\n',
                new[] { $"SOURCELONG:Size Translation Fixture {sourceShort}\tSOURCESHORT:{sourceShort}" }
                    .Concat(expected.Select(value => $"{value.Item1}\tSIZE:{value.Item2}"))
                    .Concat(["Unknown Size Fixture\tSIZE:X"]));
            var representation = new PcGenSourceFormatAdapter().TryRead(
                new SourceRepresentationArtifact(
                    fileName,
                    Encoding.UTF8.GetBytes(text),
                    $"integration:{sourceShort}#{fileName}"))
                ?? throw new InvalidOperationException("PCGen size fixture was not readable.");

            try
            {
                await new NormalizedSourceImportService(db).ImportAsync(
                    new ImportNormalizedSourceRequest(
                        packageKey,
                        $"PCGen size normalization fixture {token}",
                        "integration-test",
                        "test-only",
                        true,
                        representation));

                var rows = await db.SourceEntityRevisions
                    .AsNoTracking()
                    .Include(value => value.SourceEntity)
                        .ThenInclude(value => value.SourcePackage)
                    .Where(value => value.SourceEntity.SourcePackage.Key == packageKey)
                    .ToArrayAsync();

                foreach (var (name, code, semanticName) in expected)
                {
                    var revision = Assert.Single(
                        rows,
                        value => string.Equals(
                            value.SourceEntity.Name,
                            name,
                            StringComparison.Ordinal));
                    Assert.NotNull(revision.ContentJson);
                    using var document = JsonDocument.Parse(revision.ContentJson!);
                    var size = Assert.Single(
                        document.RootElement.GetProperty("size").EnumerateArray().ToArray());
                    Assert.Equal(code, size.GetString());
                    Assert.Equal(
                        semanticName,
                        RulesCore.Domain.Rules.UniversalSizeCategories.Normalize(code));
                }

                var unknown = Assert.Single(
                    rows,
                    value => string.Equals(
                        value.SourceEntity.Name,
                        "Unknown Size Fixture",
                        StringComparison.Ordinal));
                Assert.NotNull(unknown.ContentJson);
                using var unknownDocument = JsonDocument.Parse(unknown.ContentJson!);
                Assert.False(unknownDocument.RootElement.TryGetProperty("size", out _));
                var unmapped = unknownDocument.RootElement
                    .GetProperty("_rulesCore")
                    .GetProperty("pcgen")
                    .GetProperty("unmappedSegments")
                    .EnumerateArray()
                    .ToArray();
                Assert.Contains(
                    unmapped,
                    value => value.GetProperty("tag").GetString() == "SIZE"
                        && value.GetProperty("value").GetString() == "X");
            }
            finally
            {
                await DeletePackageAsync(db, packageKey);
            }
        }
    }

    private static RulesCore.Application.Rules.CharacterCompetencyProfileView? BuildCompetencyProfileForTest(
        Guid sourceEntityRevisionId,
        string entityType,
        string? sourceEntityName,
        string? mechanicalJson,
        string? gameEdition)
    {
        var factoryType = typeof(RulesCoreDbContext).Assembly.GetType(
            "RulesCore.Infrastructure.Rules.CharacterCompetencyProfileFactory",
            throwOnError: true)!;
        var method = factoryType.GetMethod(
            "BuildProfile",
            System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Competency profile factory method was not found.");
        return method.Invoke(
            null,
            [
                sourceEntityRevisionId,
                entityType,
                sourceEntityName,
                mechanicalJson,
                gameEdition,
                null
            ]) as RulesCore.Application.Rules.CharacterCompetencyProfileView;
    }

    private static NormalizedSourceRepresentation LegacySrdRepresentation(
        string token,
        string sourceCode,
        IReadOnlyList<string> names)
    {
        var records = names.Select((name, index) => new
        {
            name,
            source = sourceCode,
            uniqueId = $"{token}-{index}",
            documentUri = $"https://example.invalid/{token}/{index}",
            body = $"Reviewed legacy competency fixture for {name}."
        }).ToArray();
        var json = JsonSerializer.Serialize(new { skill = records });
        return new LegacySrdSourceFormatAdapter().TryRead(
            new SourceRepresentationArtifact(
                "srd-3-5e.json",
                Encoding.UTF8.GetBytes(json),
                $"integration:legacy-srd-competency:{token}"))
            ?? throw new InvalidOperationException(
                "Legacy SRD competency fixture was not readable.");
    }

    private static void AssertLegacyCompetencyProfile(
        string contentJson,
        string expectedKind,
        string? family,
        string? specialty)
    {
        using var document = JsonDocument.Parse(contentJson);
        var extension = document.RootElement.GetProperty("_rulesCore");
        var context = extension.GetProperty("context");
        Assert.Equal(
            "legacy-srd-competency-v2",
            context.GetProperty("competencyNormalizationVersion").GetString());
        var competency = extension.GetProperty("competency");
        Assert.Equal("dnd-3x", competency.GetProperty("profileKey").GetString());
        Assert.Equal(expectedKind, competency.GetProperty("kind").GetString());
        Assert.True(competency.GetProperty("supportsRanks").GetBoolean());
        Assert.True(competency.GetProperty("supportsClassSkillState").GetBoolean());
        Assert.True(competency.GetProperty("supportsTrainingState").GetBoolean());
        Assert.Equal("ranked-skill", competency.GetProperty("evaluationProfileKey").GetString());
        Assert.True(competency.GetProperty("canEvaluate").GetBoolean());
        if (family is null)
        {
            Assert.False(competency.TryGetProperty("familyName", out _));
            Assert.False(competency.TryGetProperty("specialty", out _));
        }
        else
        {
            Assert.Equal(family, competency.GetProperty("familyName").GetString());
            Assert.Equal(specialty, competency.GetProperty("specialty").GetString());
        }
    }

    private static NormalizedSourceRepresentation PcGenRepresentation(
        string editionPath,
        string sourceShort,
        IReadOnlyList<string> names)
    {
        var fileName = $"data/{editionPath}/example/example_skills.lst";
        var text = string.Join('\n',
            new[] { $"SOURCELONG:Skill Translation Fixture {sourceShort}\tSOURCESHORT:{sourceShort}" }
                .Concat(names.Select(name => $"{name}\tKEYSTAT:CHA")));
        return new PcGenSourceFormatAdapter().TryRead(new SourceRepresentationArtifact(
            fileName,
            Encoding.UTF8.GetBytes(text),
            $"integration:{sourceShort}#{fileName}"))
            ?? throw new InvalidOperationException("PCGen skill fixture was not readable.");
    }

    private static NormalizedSourceRepresentation FiveEToolRepresentation(
        string token,
        string name)
    {
        var source = $"T5{token}";
        var raw = JsonSerializer.Serialize(new
        {
            name,
            source,
            entries = new[] { $"Reviewed 5e tool fixture for {name}." }
        });
        return new NormalizedSourceRepresentation(
            FiveEToolsSourceFormatAdapter.Format,
            new SourceRepresentationArtifact(
                $"tools-{token}.json",
                Encoding.UTF8.GetBytes(raw),
                $"integration:5e-tool:{token}"),
            [new NormalizedSourceRecord(
                "tool",
                name,
                source,
                $"tool|{name}|{source}",
                raw,
                PublicationLocalKey: source)],
            [new NormalizedSourcePublication(
                source,
                $"5e Tool Fixture {token}",
                "Integration Test Press",
                "5e",
                new DateOnly(2014, 8, 19))]);
    }

    private static NormalizedSourceRepresentation FiveEDeceptionRepresentation(string token)
    {
        var source = $"D5{token}";
        var raw = JsonSerializer.Serialize(new
        {
            name = "Deception",
            source,
            entries = new[] { "A deliberately different 5e description from the 3.x Bluff record." }
        });
        return new NormalizedSourceRepresentation(
            FiveEToolsSourceFormatAdapter.Format,
            new SourceRepresentationArtifact(
                $"skills-{token}.json",
                Encoding.UTF8.GetBytes(raw),
                $"integration:5e-deception:{token}"),
            [new NormalizedSourceRecord(
                "skill",
                "Deception",
                source,
                $"skill|Deception|{source}",
                raw,
                PublicationLocalKey: source)],
            [new NormalizedSourcePublication(
                source,
                $"5e Deception Fixture {token}",
                "Integration Test Press",
                "5e",
                new DateOnly(2014, 8, 19))]);
    }

    private static async Task<SourceRow> ReadByNativeNameAsync(
        RulesCoreDbContext db,
        string packageKey,
        string nativeName)
    {
        var rows = await db.SourceEntityRevisions
            .AsNoTracking()
            .Include(value => value.SourceEntity)
                .ThenInclude(value => value.SourcePackage)
            .Where(value => value.SourceEntity.SourcePackage.Key == packageKey)
            .ToArrayAsync();

        var matches = rows.Where(value =>
        {
            using var document = JsonDocument.Parse(value.RawJson);
            return document.RootElement.TryGetProperty("name", out var name)
                && string.Equals(name.GetString(), nativeName, StringComparison.Ordinal);
        }).ToArray();
        var revision = Assert.Single(matches);
        Assert.NotNull(revision.ContentJson);
        return new SourceRow(
            revision.SourceEntity.EntityType,
            revision.SourceEntity.Name,
            revision.RawJson,
            revision.ContentJson!);
    }

    private static void AssertNativeSourceName(string rawJson, string expected)
    {
        using var document = JsonDocument.Parse(rawJson);
        Assert.Equal(expected, document.RootElement.GetProperty("name").GetString());
    }

    private static void AssertExactTranslation(
        string contentJson,
        string sourceName,
        string targetType,
        string targetName)
    {
        using var document = JsonDocument.Parse(contentJson);
        var root = document.RootElement;
        Assert.Equal(targetName, root.GetProperty("name").GetString());
        var extension = root.GetProperty("_rulesCore");
        var conversion = extension.GetProperty("competencyConversion");
        Assert.Equal(sourceName, conversion.GetProperty("sourceName").GetString());
        Assert.Equal(targetType, conversion.GetProperty("targetType").GetString());
        Assert.Equal(targetName, conversion.GetProperty("targetName").GetString());
        Assert.False(extension.TryGetProperty("exactCompetencyIdentity", out _));
    }

    private static void AssertSharedFacet(
        string contentJson,
        string sourceName,
        string targetName,
        string identityKey,
        string identityName)
    {
        using var document = JsonDocument.Parse(contentJson);
        var root = document.RootElement;
        Assert.Equal(sourceName, root.GetProperty("name").GetString());
        var extension = root.GetProperty("_rulesCore");
        var conversion = extension.GetProperty("competencyConversion");
        Assert.Equal(
            "shared-competency-facet",
            conversion.GetProperty("relationship").GetString());
        Assert.Equal("skill", conversion.GetProperty("sourceType").GetString());
        Assert.Equal(sourceName, conversion.GetProperty("sourceName").GetString());
        Assert.Equal("tool", conversion.GetProperty("targetType").GetString());
        Assert.Equal(targetName, conversion.GetProperty("targetName").GetString());
        Assert.True(conversion.GetProperty("mechanicalNamePreserved").GetBoolean());
        Assert.Equal(identityKey, conversion.GetProperty("sharedCompetencyKey").GetString());
        Assert.Equal(identityName, conversion.GetProperty("sharedCompetencyName").GetString());

        var competency = extension.GetProperty("competency");
        Assert.Equal(identityKey, competency.GetProperty("identityKey").GetString());
        Assert.Equal(identityName, competency.GetProperty("identityName").GetString());
        Assert.Equal(
            $"competency.{identityKey}.training",
            competency.GetProperty("sharedTrainingKey").GetString());
        Assert.Equal("skill", competency.GetProperty("facetType").GetString());
        Assert.True(competency.GetProperty("supportsRanks").GetBoolean());
        Assert.True(competency.GetProperty("supportsClassSkillState").GetBoolean());
        Assert.False(extension.TryGetProperty("exactCompetencyIdentity", out _));
    }

    private static void AssertScopedCompetency(
        string contentJson,
        string sourceName,
        string targetName,
        string scope)
    {
        using var document = JsonDocument.Parse(contentJson);
        var root = document.RootElement;
        Assert.Equal(sourceName, root.GetProperty("name").GetString());
        var extension = root.GetProperty("_rulesCore");
        var conversion = extension.GetProperty("competencyConversion");
        Assert.Equal("direct-cross-type", conversion.GetProperty("relationship").GetString());
        Assert.Equal(targetName, conversion.GetProperty("targetName").GetString());
        Assert.Equal(scope, conversion.GetProperty("scope").GetString());
        Assert.True(conversion.GetProperty("mechanicalNamePreserved").GetBoolean());
        Assert.False(conversion.TryGetProperty("sharedCompetencyKey", out _));
    }

    private static void AssertRelatedCompetency(
        string contentJson,
        string sourceName,
        string sourceType,
        string targetName,
        string targetType)
    {
        using var document = JsonDocument.Parse(contentJson);
        var root = document.RootElement;
        Assert.Equal(sourceName, root.GetProperty("name").GetString());
        var extension = root.GetProperty("_rulesCore");
        var conversion = extension.GetProperty("competencyConversion");
        Assert.Equal("related-competency", conversion.GetProperty("relationship").GetString());
        Assert.Equal(sourceType, conversion.GetProperty("sourceType").GetString());
        Assert.Equal(sourceName, conversion.GetProperty("sourceName").GetString());
        Assert.Equal(targetType, conversion.GetProperty("targetType").GetString());
        Assert.Equal(targetName, conversion.GetProperty("targetName").GetString());
        Assert.True(conversion.GetProperty("mechanicalNamePreserved").GetBoolean());
        Assert.Equal("skill", extension.GetProperty("competency").GetProperty("kind").GetString());
        Assert.False(extension.TryGetProperty("exactCompetencyIdentity", out _));
    }

    private static void AssertNoExactTranslation(string contentJson)
    {
        using var document = JsonDocument.Parse(contentJson);
        Assert.False(document.RootElement.GetProperty("_rulesCore").TryGetProperty("exactCompetencyIdentity", out _));
    }

    private static async Task<Guid> ReadCanonicalEntityIdAsync(RulesCoreDbContext db, Guid sourceEntityId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT occurrence.canonical_entity_id
                FROM source_entity_revision revision
                JOIN source_entity_occurrence_binding binding
                    ON binding.source_entity_revision_id = revision.source_entity_revision_id
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                WHERE revision.source_entity_id = @source_entity_id
                    AND occurrence.canonical_entity_id IS NOT NULL
                ORDER BY revision.revision_number DESC
                LIMIT 1;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            return (Guid)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Canonical entity was not resolved."));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task DeletePackageAsync(RulesCoreDbContext db, string packageKey)
    {
        var package = await db.SourcePackages.SingleOrDefaultAsync(value => value.Key == packageKey);
        if (package is null) return;
        db.SourcePackages.Remove(package);
        await db.SaveChangesAsync();
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

    private sealed record SourceRow(
        string EntityType,
        string NormalizedName,
        string RawJson,
        string ContentJson);
}
