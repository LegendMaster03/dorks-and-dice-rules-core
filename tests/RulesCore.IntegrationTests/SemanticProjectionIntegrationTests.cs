using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class MechanicalContentIntegrationTests
{
    [Fact]
    public async Task TranslatedContentPreservesNativeRevisionIdentityAndCanonicalSemantics()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<RulesCoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new RulesCoreDbContext(options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..12];
        var packageKey = $"mechanical-content-{token}";
        var publicationKey = $"publication-{token}";
        var importer = new NormalizedSourceImportService(db);

        try
        {
            var first = await importer.ImportAsync(Request(
                packageKey,
                publicationKey,
                token,
                "p. 10",
                "line:10"));
            var second = await importer.ImportAsync(Request(
                packageKey,
                publicationKey,
                token,
                "p. 11",
                "line:11"));

            var firstEntity = Assert.Single(first.Entities);
            var secondEntity = Assert.Single(second.Entities);
            Assert.Equal(firstEntity.EntityId, secondEntity.EntityId);
            Assert.Equal(1, firstEntity.RevisionNumber);
            Assert.Equal(2, secondEntity.RevisionNumber);
            Assert.NotEqual(firstEntity.Fingerprint, secondEntity.Fingerprint);

            var rows = await ReadBindingsAsync(db, firstEntity.EntityId);
            Assert.Equal(2, rows.Count);
            Assert.Equal(rows[0].CanonicalEntityId, rows[1].CanonicalEntityId);
            Assert.Equal(rows[0].SemanticFingerprint, rows[1].SemanticFingerprint);

            var revisions = await db.SourceEntityRevisions
                .AsNoTracking()
                .Where(value => value.SourceEntityId == firstEntity.EntityId)
                .OrderBy(value => value.RevisionNumber)
                .Select(value => new { value.RawJson, value.ContentJson })
                .ToArrayAsync();
            Assert.Contains("p. 10", revisions[0].RawJson, StringComparison.Ordinal);
            Assert.Contains("p. 11", revisions[1].RawJson, StringComparison.Ordinal);
            Assert.DoesNotContain("sourcePage", revisions[0].ContentJson!, StringComparison.Ordinal);
            Assert.DoesNotContain("sourcePage", revisions[1].ContentJson!, StringComparison.Ordinal);
        }
        finally
        {
            await DeletePackageAsync(db, packageKey);
        }
    }

    [Fact]
    public async Task PcGenClassRecordsNormalizeThreeXCharacterProgressionWithoutDiscardingSourceEvidence()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<RulesCoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new RulesCoreDbContext(options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..12];
        var packageKey = $"mechanical-class-{token}";
        var campaign = new SourceRepresentationArtifact(
            "example.pcc",
            Encoding.UTF8.GetBytes("""
                CAMPAIGN:Class Mechanics
                GAMEMODE:35e
                SOURCELONG:Class Mechanics
                SOURCESHORT:CLS
                CLASS:example_classes.lst
                """),
            $"integration:class:{token}#data/35e/example/example.pcc");
        var classes = new SourceRepresentationArtifact(
            "example_classes.lst",
            Encoding.UTF8.GetBytes(string.Join('\n',
            [
                "CLASS:Example Class\tHD:8\tTYPE:Base.PC\tMAXLEVEL:20\tBONUS:COMBAT|BASEAB|classlevel(\"APPLIEDAS=NONEPIC\")*3/4\tBONUS:SAVE|BASE.Fortitude,BASE.Will|classlevel(\"APPLIEDAS=NONEPIC\")/3\tBONUS:SAVE|BASE.Reflex|classlevel(\"APPLIEDAS=NONEPIC\")/2+2",
                "CLASS:Example Class\tSTARTSKILLPTS:4\tCSKILL:Climb|Jump|TYPE.Craft\tSPELLSTAT:INT",
                "1\tABILITY:Special Ability|AUTOMATIC|First Feature",
                "2\tSAB:Second Feature"
            ])),
            $"integration:class:{token}#data/35e/example/example_classes.lst");
        var representation = new PcGenSourceFormatAdapter()
            .TryReadMany([campaign, classes])
            .Single(value => value.Artifact.FileName == "example_classes.lst");
        var imported = await new NormalizedSourceImportService(db).ImportAsync(
            new ImportNormalizedSourceRequest(
                packageKey,
                $"Class mechanics {token}",
                "integration-test",
                "test-only",
                true,
                representation));

        try
        {
            var entity = Assert.Single(imported.Entities);
            Assert.Equal("class", entity.EntityType);
            var revision = await db.SourceEntityRevisions
                .AsNoTracking()
                .SingleAsync(value => value.SourceEntityId == entity.EntityId);
            using var content = JsonDocument.Parse(revision.ContentJson!);
            var root = content.RootElement;
            Assert.Equal(8, root.GetProperty("hd").GetProperty("faces").GetInt32());
            var character = root.GetProperty("_rulesCore").GetProperty("character");
            Assert.Equal(4, character.GetProperty("skillPointsPerLevel").GetInt32());
            Assert.Equal(20, character.GetProperty("maximumLevel").GetInt32());
            Assert.Equal("intelligence", character.GetProperty("spellcastingAbility").GetString());
            Assert.Equal("dnd-3x", character.GetProperty("spellcastingProfile").GetString());
            Assert.Equal("three-quarters", character.GetProperty("baseAttackProgression").GetString());
            var saves = character.GetProperty("saveProgressions");
            Assert.Equal("poor", saves.GetProperty("fortitude").GetString());
            Assert.Equal("good", saves.GetProperty("reflex").GetString());
            Assert.Equal("poor", saves.GetProperty("will").GetString());
            Assert.Contains(
                character.GetProperty("classSkills").EnumerateArray(),
                value => value.GetString() == "TYPE.Craft");
            var features = character.GetProperty("advancementFeatures").EnumerateArray().ToArray();
            Assert.Contains(features, value =>
                value.GetProperty("level").GetInt32() == 1
                && value.GetProperty("name").GetString() == "First Feature");
            Assert.Contains(features, value =>
                value.GetProperty("level").GetInt32() == 2
                && value.GetProperty("name").GetString() == "Second Feature");

            var unmapped = root.GetProperty("_rulesCore")
                .GetProperty("pcgen")
                .GetProperty("unmappedSegments")
                .EnumerateArray()
                .ToArray();
            Assert.Contains(unmapped, value =>
                value.GetProperty("tag").GetString() == "BONUS"
                && value.GetProperty("value").GetString()!.StartsWith("COMBAT|BASEAB|", StringComparison.Ordinal));
        }
        finally
        {
            await DeletePackageAsync(db, packageKey);
        }
    }

    [Fact]
    public async Task FiveEToolsAndRepresentativePcGenRecordsExposeFaithfulFamilyShapes()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<RulesCoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new RulesCoreDbContext(options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..12];
        var fivePackage = $"mechanical-five-{token}";
        var pcgenPackage = $"mechanical-pcgen-{token}";
        var importer = new NormalizedSourceImportService(db);

        try
        {
            await importer.ImportAsync(new ImportNormalizedSourceRequest(
                fivePackage,
                $"5e.tools mechanical fixture {token}",
                "integration-test",
                "test-only",
                false,
                FiveEToolsFixture(token)));
            await importer.ImportAsync(new ImportNormalizedSourceRequest(
                pcgenPackage,
                $"PCGen mechanical fixture {token}",
                "integration-test",
                "test-only",
                false,
                PcGenFixture(token)));

            var five = await LatestContentByTypeAsync(db, fivePackage);
            var pcgen = await LatestContentByTypeAsync(db, pcgenPackage);
            Assert.Equal(new[] { "feat", "item", "monster", "spell" }, five.Keys.Order().ToArray());
            Assert.Equal(new[] { "feat", "item", "monster", "spell" }, pcgen.Keys.Order().ToArray());

            AssertFamilyFields(five["monster"], "name", "source", "size", "type", "speed", "cr", "entries");
            AssertFamilyFields(pcgen["monster"], "name", "source", "size", "type", "speed", "cr", "entries");
            AssertFamilyFields(five["spell"], "name", "source", "level", "school", "entries");
            AssertFamilyFields(pcgen["spell"], "name", "source", "level", "school", "entries");
            AssertFamilyFields(five["feat"], "name", "source", "entries", "repeatable");
            AssertFamilyFields(pcgen["feat"], "name", "source", "entries", "repeatable");
            AssertFamilyFields(five["item"], "name", "source", "entries", "weight", "value");
            AssertFamilyFields(pcgen["item"], "name", "source", "entries", "weight", "value");

            Assert.True(five["monster"].TryGetProperty("futureUpstreamField", out _));
            var monsterContext = pcgen["monster"].GetProperty("_rulesCore").GetProperty("context");
            Assert.Equal("3.5e", monsterContext.GetProperty("edition").GetString());
            Assert.Equal(PcGenSourceFormatAdapter.Format, monsterContext.GetProperty("sourceFormat").GetString());
            Assert.Equal("race", monsterContext.GetProperty("nativeEntityType").GetString());
            Assert.Equal("monster", monsterContext.GetProperty("translatedEntityType").GetString());
            Assert.Equal(1500, pcgen["item"].GetProperty("value").GetInt32());
            Assert.False(pcgen["monster"].TryGetProperty("str", out _));
            Assert.False(pcgen["monster"].TryGetProperty("dex", out _));
            Assert.False(pcgen["monster"].TryGetProperty("ac", out _));
            Assert.False(pcgen["monster"].TryGetProperty("hp", out _));

            var monsterUnmapped = pcgen["monster"]
                .GetProperty("_rulesCore")
                .GetProperty("pcgen")
                .GetProperty("unmappedSegments")
                .EnumerateArray()
                .ToArray();
            Assert.Contains(monsterUnmapped, value =>
                value.GetProperty("tag").GetString() == "BONUS"
                && value.GetProperty("value").GetString() == "STAT|STR|-2");
            Assert.Contains(monsterUnmapped, value =>
                value.GetProperty("tag").GetString() == "BONUS"
                && value.GetProperty("value").GetString() == "COMBAT|AC|1|TYPE=NaturalArmor");
            Assert.Contains(monsterUnmapped, value =>
                value.GetProperty("tag").GetString() == "MONSTERCLASS"
                && value.GetProperty("value").GetString() == "Humanoid:1");

            var featExtension = pcgen["feat"].GetProperty("_rulesCore");
            Assert.Equal(
                "ability",
                featExtension.GetProperty("context").GetProperty("nativeEntityType").GetString());
            var featPrerequisiteGroup = Assert.Single(
                featExtension
                    .GetProperty("character")
                    .GetProperty("prerequisites")
                    .EnumerateArray());
            Assert.Equal(1, featPrerequisiteGroup.GetProperty("matchCount").GetInt32());
            var featRequirement = Assert.Single(
                featPrerequisiteGroup.GetProperty("requirements").EnumerateArray());
            Assert.Equal("ability-score", featRequirement.GetProperty("kind").GetString());
            Assert.Equal("ability.strength.score", featRequirement.GetProperty("targetKey").GetString());
            Assert.Equal(13, featRequirement.GetProperty("value").GetInt32());
            var featUnmapped = featExtension.GetProperty("pcgen").GetProperty("unmappedSegments");
            Assert.Contains(featUnmapped.EnumerateArray(), value =>
                value.GetProperty("tag").GetString() == "PREMULT"
                && value.GetProperty("value").GetString() == "1,[PRESTAT:1,STR=13]");

            var monsterSource = await db.SourceEntities
                .AsNoTracking()
                .SingleAsync(value =>
                    value.SourcePackage.Key == pcgenPackage
                    && value.Name == "PCGen Goblin");
            Assert.Equal("monster", monsterSource.EntityType);
            Assert.StartsWith("pcgen|race|", monsterSource.NativeKey, StringComparison.Ordinal);

            var featSource = await db.SourceEntities
                .AsNoTracking()
                .SingleAsync(value =>
                    value.SourcePackage.Key == pcgenPackage
                    && value.Name == "PCGen Training");
            Assert.Equal("feat", featSource.EntityType);
            Assert.StartsWith("pcgen|ability|", featSource.NativeKey, StringComparison.Ordinal);

            var featRevision = await db.SourceEntityRevisions
                .AsNoTracking()
                .Include(value => value.SourceEntity)
                    .ThenInclude(value => value.SourcePackage)
                .SingleAsync(value =>
                    value.SourceEntity.SourcePackage.Key == pcgenPackage
                    && value.SourceEntity.EntityType == "feat");
            using var mechanicalFeat = JsonDocument.Parse(featRevision.GetMechanicalContentJson());
            var mechanicalExtension = mechanicalFeat.RootElement.GetProperty("_rulesCore");
            Assert.False(mechanicalExtension.TryGetProperty("context", out _));
            Assert.True(mechanicalExtension.TryGetProperty("pcgen", out _));

            var pcgenRaw = featRevision.RawJson;
            Assert.Contains("PREMULT", pcgenRaw, StringComparison.Ordinal);
            Assert.Contains("segments", pcgenRaw, StringComparison.Ordinal);

            var bareFingerprint = CanonicalSourceIdentity.SemanticFingerprint(
                "{\"name\":\"Context Test\",\"entries\":[\"Same mechanic.\"]}");
            var contextualFingerprint = CanonicalSourceIdentity.SemanticFingerprint(
                "{\"name\":\"Context Test\",\"entries\":[\"Same mechanic.\"],\"_rulesCore\":{\"context\":{\"edition\":\"3.5e\",\"sourceFormat\":\"pcgen-data\"}}}");
            Assert.Equal(bareFingerprint, contextualFingerprint);
        }
        finally
        {
            await DeletePackageAsync(db, fivePackage);
            await DeletePackageAsync(db, pcgenPackage);
        }
    }

    private static ImportNormalizedSourceRequest Request(
        string packageKey,
        string publicationKey,
        string token,
        string sourcePage,
        string locator)
    {
        var record = new NormalizedSourceRecord(
            "rule",
            "Projected Rule",
            "PROJ",
            $"rule|{token}",
            $"{{\"name\":\"Projected Rule\",\"effect\":\"Gain a +2 bonus.\",\"sourcePage\":\"{sourcePage}\"}}",
            LocatorKey: locator,
            PublicationLocalKey: publicationKey,
            NativeIdentityJson: "{}")
        {
            ContentJson = "{\"name\":\"Projected Rule\",\"effect\":\"Gain a +2 bonus.\"}"
        };
        return new ImportNormalizedSourceRequest(
            packageKey,
            $"Mechanical content package {token}",
            "integration-test",
            "test-only",
            false,
            new NormalizedSourceRepresentation(
                "projection-test",
                new SourceRepresentationArtifact(
                    $"projection-{token}.json",
                    Encoding.UTF8.GetBytes($"{{\"sourcePage\":\"{sourcePage}\"}}"),
                    $"test:mechanical-content:{token}:{sourcePage}"),
                [record],
                [new NormalizedSourcePublication(
                    publicationKey,
                    $"Mechanical Content Publication {token}",
                    "Integration Test Press",
                    "3.5e",
                    new DateOnly(2006, 1, 1),
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["isbn"] = $"test-mechanical-content-{token}"
                    })]));
    }

    private static NormalizedSourceRepresentation FiveEToolsFixture(string token)
    {
        var records = new[]
        {
            Native("monster", "Reference Goblin", "MM", $"monster|{token}",
                "{\"name\":\"Reference Goblin\",\"source\":\"MM\",\"size\":[\"S\"],\"type\":\"humanoid\",\"ac\":[15],\"hp\":{\"average\":7,\"formula\":\"2d6\"},\"speed\":{\"walk\":30},\"str\":8,\"dex\":14,\"con\":10,\"int\":10,\"wis\":8,\"cha\":8,\"cr\":\"1/4\",\"entries\":[\"A reference creature.\"],\"futureUpstreamField\":{\"preserved\":true}}"),
            Native("spell", "Reference Spark", "PHB", $"spell|{token}",
                "{\"name\":\"Reference Spark\",\"source\":\"PHB\",\"level\":1,\"school\":\"V\",\"entries\":[\"A reference spell.\"]}"),
            Native("feat", "Reference Training", "PHB", $"feat|{token}",
                "{\"name\":\"Reference Training\",\"source\":\"PHB\",\"entries\":[\"A reference feat.\"],\"repeatable\":true}"),
            Native("item", "Reference Blade", "PHB", $"item|{token}",
                "{\"name\":\"Reference Blade\",\"source\":\"PHB\",\"entries\":[\"A reference item.\"],\"weight\":3,\"value\":1500}")
        };
        return new NormalizedSourceRepresentation(
            FiveEToolsSourceFormatAdapter.Format,
            new SourceRepresentationArtifact(
                $"five-{token}.json",
                Encoding.UTF8.GetBytes("{\"fixture\":\"5etools\"}"),
                $"test:mechanical:five:{token}"),
            records);
    }

    private static NormalizedSourceRecord Native(string type, string name, string source, string nativeKey, string rawJson) =>
        new(type, name, source, nativeKey, rawJson, NativeIdentityJson: "{}");

    private static NormalizedSourceRepresentation PcGenFixture(string token)
    {
        const string publicationKey = "pcgen-35e-fixture";
        var records = new[]
        {
            PcGen("race", "PCGen Goblin", "3XTEST", $"pcgen|race|{token}", publicationKey, "data/35e/example/monsters/example_races.lst",
                new[]
                {
                    Seg("SIZE", "S"),
                    Seg("MOVE", "Walk,30"),
                    Seg("BONUS", "COMBAT|AC|1|TYPE=NaturalArmor"),
                    Seg("BONUS", "STAT|STR|-2"),
                    Seg("BONUS", "STAT|DEX|4"),
                    Seg("MONSTERCLASS", "Humanoid:1"),
                    Seg("RACETYPE", "Humanoid"),
                    Seg("TYPE", "Humanoid"),
                    Seg("CR", "1/4"),
                    Seg("DESC", "A translated creature.")
                }),
            PcGen("spell", "PCGen Spark", "3XTEST", $"pcgen|spell|{token}", publicationKey, "data/35e/example/example_spells.lst",
                new[]
                {
                    Seg("TYPE", "Arcane"),
                    Seg("CLASSES", "Sorcerer,Wizard=1"),
                    Seg("SCHOOL", "Evocation"),
                    Seg("COMPS", "V, S"),
                    Seg("DURATION", "Instantaneous"),
                    Seg("DESC", "A translated spell.")
                }),
            PcGen("ability", "PCGen Training", "3XTEST", $"pcgen|ability|{token}", publicationKey, "data/35e/example/example_feats.lst",
                new[]
                {
                    Seg("CATEGORY", "FEAT"),
                    Seg("TYPE", "General"),
                    Seg("MULT", "YES"),
                    Seg("PREMULT", "1,[PRESTAT:1,STR=13]"),
                    Seg("DESC", "A translated feat.")
                }),
            PcGen("item", "PCGen Blade", "3XTEST", $"pcgen|item|{token}", publicationKey, "data/35e/example/example_equip.lst",
                new[] { Seg("TYPE", "Weapon.Melee"), Seg("WT", "3"), Seg("COST", "15"), Seg("DESC", "A translated item.") })
        };
        return new NormalizedSourceRepresentation(
            PcGenSourceFormatAdapter.Format,
            new SourceRepresentationArtifact(
                $"pcgen-{token}.lst",
                Encoding.UTF8.GetBytes("# deterministic PCGen fixture"),
                $"test:mechanical:pcgen:{token}"),
            records,
            [new NormalizedSourcePublication(
                publicationKey,
                "PCGen 3.5e Fixture",
                "Integration Test Press",
                "3.5e",
                new DateOnly(2006, 1, 1))]);
    }

    private static NormalizedSourceRecord PcGen(
        string type,
        string name,
        string source,
        string nativeKey,
        string publicationKey,
        string path,
        IReadOnlyList<(string Tag, string Value)> segments)
    {
        var jsonSegments = segments.Select((value, index) => new
        {
            Index = index,
            value.Tag,
            value.Value,
            Raw = $"{value.Tag}:{value.Value}"
        }).ToArray();
        return new NormalizedSourceRecord(
            type,
            name,
            source,
            nativeKey,
            JsonSerializer.Serialize(new
            {
                format = PcGenSourceFormatAdapter.Format,
                kind = "record",
                path,
                lineNumber = 1,
                rawLine = name,
                name,
                entityType = type,
                segments = jsonSegments
            }),
            PublicationLocalKey: publicationKey,
            NativeIdentityJson: JsonSerializer.Serialize(new { key = nativeKey }));
    }

    private static (string Tag, string Value) Seg(string tag, string value) => (tag, value);

    private static async Task<Dictionary<string, JsonElement>> LatestContentByTypeAsync(
        RulesCoreDbContext db,
        string packageKey)
    {
        var revisions = await db.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => value.SourceEntity.SourcePackage.Key == packageKey)
            .Select(value => new { value.SourceEntity.EntityType, value.ContentJson })
            .ToArrayAsync();
        return revisions.ToDictionary(
            value => value.EntityType,
            value => JsonDocument.Parse(value.ContentJson!).RootElement.Clone(),
            StringComparer.Ordinal);
    }

    private static void AssertFamilyFields(JsonElement content, params string[] fields)
    {
        foreach (var field in fields)
        {
            Assert.True(content.TryGetProperty(field, out _), $"Expected mechanical field '{field}' in {content.GetRawText()}");
        }
    }

    private static async Task DeletePackageAsync(RulesCoreDbContext db, string packageKey)
    {
        var package = await db.SourcePackages.SingleOrDefaultAsync(value => value.Key == packageKey);
        if (package is null) return;
        db.SourcePackages.Remove(package);
        await db.SaveChangesAsync();
    }

    private static async Task<IReadOnlyList<BindingRow>> ReadBindingsAsync(
        RulesCoreDbContext db,
        Guid sourceEntityId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT occurrence.canonical_entity_id, binding.semantic_fingerprint
                FROM source_entity_revision revision
                JOIN source_entity_occurrence_binding binding
                    ON binding.source_entity_revision_id = revision.source_entity_revision_id
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                WHERE revision.source_entity_id = @source_entity_id
                ORDER BY revision.revision_number;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            var rows = new List<BindingRow>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new BindingRow(reader.GetGuid(0), reader.GetString(1)));
            }
            return rows;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record BindingRow(Guid CanonicalEntityId, string SemanticFingerprint);
}
