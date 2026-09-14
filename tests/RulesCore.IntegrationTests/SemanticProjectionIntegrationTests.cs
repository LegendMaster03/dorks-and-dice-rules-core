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
    public async Task FiveEToolsAndPcGenExposeCommonMechanicalFamilyShapes()
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

            AssertFamilyFields(five["monster"], "name", "source", "size", "type", "ac", "hp", "speed", "str", "dex", "cr");
            AssertFamilyFields(pcgen["monster"], "name", "source", "size", "type", "ac", "hp", "speed", "str", "dex", "cr");
            AssertFamilyFields(five["spell"], "name", "source", "level", "school", "entries");
            AssertFamilyFields(pcgen["spell"], "name", "source", "level", "school", "entries");
            AssertFamilyFields(five["feat"], "name", "source", "entries", "repeatable");
            AssertFamilyFields(pcgen["feat"], "name", "source", "entries", "repeatable");
            AssertFamilyFields(five["item"], "name", "source", "entries", "weight", "value");
            AssertFamilyFields(pcgen["item"], "name", "source", "entries", "weight", "value");

            Assert.True(five["monster"].TryGetProperty("futureUpstreamField", out _));
            Assert.Equal("3.5e", pcgen["monster"].GetProperty("_rulesCore").GetProperty("edition").GetString());
            var unmapped = pcgen["feat"].GetProperty("_rulesCore").GetProperty("pcgen").GetProperty("unmappedSegments");
            Assert.Contains(unmapped.EnumerateArray(), value =>
                value.GetProperty("tag").GetString() == "PREMULT"
                && value.GetProperty("value").GetString() == "1,[PRESTAT:1,STR=13]");

            var pcgenRaw = await db.SourceEntityRevisions
                .AsNoTracking()
                .Where(value => value.SourceEntity.SourcePackage.Key == pcgenPackage && value.SourceEntity.EntityType == "feat")
                .Select(value => value.RawJson)
                .SingleAsync();
            Assert.Contains("PREMULT", pcgenRaw, StringComparison.Ordinal);
            Assert.Contains("segments", pcgenRaw, StringComparison.Ordinal);
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
                "{\"name\":\"Reference Goblin\",\"source\":\"MM\",\"size\":[\"S\"],\"type\":\"humanoid\",\"ac\":[15],\"hp\":{\"average\":7,\"formula\":\"2d6\"},\"speed\":{\"walk\":30},\"str\":8,\"dex\":14,\"con\":10,\"int\":10,\"wis\":8,\"cha\":8,\"cr\":\"1/4\",\"futureUpstreamField\":{\"preserved\":true}}"),
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
            PcGen("monster", "PCGen Goblin", "3XTEST", $"pcgen|monster|{token}", publicationKey,
                new[] { Seg("SIZE", "S"), Seg("TYPE", "Humanoid"), Seg("AC", "15"), Seg("HP", "7"), Seg("HD", "2d6"), Seg("MOVE", "30"), Seg("STR", "8"), Seg("DEX", "14"), Seg("CON", "10"), Seg("INT", "10"), Seg("WIS", "8"), Seg("CHA", "8"), Seg("CR", "1/4"), Seg("DESC", "A translated creature.") }),
            PcGen("spell", "PCGen Spark", "3XTEST", $"pcgen|spell|{token}", publicationKey,
                new[] { Seg("LEVEL", "1"), Seg("SCHOOL", "Evocation"), Seg("COMPS", "V,S"), Seg("DURATION", "Instantaneous"), Seg("DESC", "A translated spell.") }),
            PcGen("feat", "PCGen Training", "3XTEST", $"pcgen|feat|{token}", publicationKey,
                new[] { Seg("MULT", "YES"), Seg("PREMULT", "1,[PRESTAT:1,STR=13]"), Seg("DESC", "A translated feat.") }),
            PcGen("item", "PCGen Blade", "3XTEST", $"pcgen|item|{token}", publicationKey,
                new[] { Seg("WT", "3"), Seg("COST", "15 gp"), Seg("DESC", "A translated item.") })
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
                path = $"data/35e/{type}.lst",
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
