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
        ("Sense Motive", "Insight"),
        ("Sleight of Hand", "Sleight of Hand"),
        ("Survival", "Survival")
    ];

    private static readonly (string Source, string Target)[] DirectToolConversions =
    [
        ("Craft (alchemy)", "Alchemist's Supplies"),
        ("Forgery", "Forgery Kit")
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

    [Fact]
    public async Task AlchemistsSuppliesUsesEstablishedCompetencyConceptKey()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var packageKey = $"pcgen-alchemy-key-{token}";

            try
            {
                await new NormalizedSourceImportService(db).ImportAsync(
                    new ImportNormalizedSourceRequest(
                        packageKey,
                        $"PCGen alchemy key fixture {token}",
                        "integration-test",
                        "test-only",
                        true,
                        PcGenRepresentation("3e", $"ALK{token}", ["Alchemy"])));

                var candidates = await new SourceNormalizationService(db).GetCandidatesAsync(
                    $"rules-lawyer-{token}",
                    entityType: "tool",
                    query: "Alchemist's Supplies");

                var candidate = Assert.Single(
                    candidates,
                    value => value.PackageKey == packageKey);
                Assert.Equal("Alchemist's Supplies", candidate.SourceName);
                Assert.Equal("tool.alchemists-supplies", candidate.SuggestedConceptKey);
            }
            finally
            {
                await DeletePackageAsync(db, packageKey);
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
                            .Concat(DirectToolConversions.Select(value => value.Source))
                            .Concat(["Open Lock", "Pick Pocket", "Wilderness Lore", "Alchemy"])
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

                foreach (var (source, target) in DirectToolConversions)
                {
                    var row = await ReadByNativeNameAsync(db, package35, source);
                    Assert.Equal("tool", row.EntityType);
                    Assert.Equal(target, row.NormalizedName);
                    AssertNativeSourceName(row.RawJson, source);
                    AssertExactTranslation(row.ContentJson, source, "tool", target);
                }

                var openLock = await ReadByNativeNameAsync(db, package35, "Open Lock");
                Assert.Equal("skill", openLock.EntityType);
                Assert.Equal("Open Lock", openLock.NormalizedName);
                using (var document = JsonDocument.Parse(openLock.ContentJson))
                {
                    var extension = document.RootElement.GetProperty("_rulesCore");
                    Assert.False(extension.TryGetProperty("exactCompetencyIdentity", out _));
                    var conversion = extension.GetProperty("competencyConversion");
                    Assert.Equal("Thieves' Tools", conversion.GetProperty("targetName").GetString());
                    Assert.Equal("open-lock", conversion.GetProperty("scope").GetString());
                }

                foreach (var source in ExcludedDirectConversions.Concat(["Pick Pocket", "Wilderness Lore", "Alchemy"]))
                {
                    var row = await ReadByNativeNameAsync(db, package35, source);
                    Assert.Equal("skill", row.EntityType);
                    Assert.Equal(source, row.NormalizedName);
                    AssertNoExactTranslation(row.ContentJson);
                }

                AssertExactTranslation(
                    (await ReadByNativeNameAsync(db, package30, "Pick Pocket")).ContentJson,
                    "Pick Pocket",
                    "skill",
                    "Sleight of Hand");
                AssertExactTranslation(
                    (await ReadByNativeNameAsync(db, package30, "Wilderness Lore")).ContentJson,
                    "Wilderness Lore",
                    "skill",
                    "Survival");
                var alchemy = await ReadByNativeNameAsync(db, package30, "Alchemy");
                Assert.Equal("tool", alchemy.EntityType);
                Assert.Equal("Alchemist's Supplies", alchemy.NormalizedName);
                AssertExactTranslation(alchemy.ContentJson, "Alchemy", "tool", "Alchemist's Supplies");
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
                    Assert.Equal("specialized-skill", competency.GetProperty("kind").GetString());
                    Assert.Equal("Knowledge", competency.GetProperty("familyName").GetString());
                    Assert.Equal("the planes", competency.GetProperty("specialty").GetString());
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
                Assert.Equal("tool", alchemy.EntityType);
                Assert.Equal("Alchemist's Supplies", alchemy.NormalizedName);
                using (var document = JsonDocument.Parse(alchemy.ContentJson))
                {
                    var competency = document.RootElement
                        .GetProperty("_rulesCore")
                        .GetProperty("competency");
                    Assert.Equal("tool", competency.GetProperty("kind").GetString());
                    Assert.Equal("Craft", competency.GetProperty("familyName").GetString());
                    Assert.Equal("alchemy", competency.GetProperty("specialty").GetString());
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
                ["Bluff", "Craft (alchemy)", "Open Lock", "Knowledge (the planes)"]);

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
                Assert.Equal("tool", alchemy.EntityType);
                Assert.Equal("Alchemist's Supplies", alchemy.NormalizedName);
                AssertNativeSourceName(alchemy.RawJson, "Craft (alchemy)");
                AssertExactTranslation(
                    alchemy.ContentJson,
                    "Craft (alchemy)",
                    "tool",
                    "Alchemist's Supplies");
                AssertLegacyCompetencyProfile(
                    alchemy.ContentJson,
                    expectedKind: "tool",
                    family: "Craft",
                    specialty: "alchemy");

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
                Assert.Equal("Knowledge (the planes)", knowledge.NormalizedName);
                AssertLegacyCompetencyProfile(
                    knowledge.ContentJson,
                    expectedKind: "specialized-skill",
                    family: "Knowledge",
                    specialty: "the planes");
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
            "legacy-srd-competency-v1",
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
