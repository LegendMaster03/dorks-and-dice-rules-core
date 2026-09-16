using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class FiveEToolsNativeIdentityIntegrationTests
{
    [Fact]
    public void DirectClassIdentityDoesNotTreatEditionAsNativeIdentity()
    {
        var adapter = new FiveEToolsSourceFormatAdapter();
        var classic = Require(adapter.TryRead(Artifact(
            "class-sorcerer-classic.json",
            """
            {
              "class": [
                {"name":"Sorcerer","source":"PHB","edition":"classic","page":99}
              ]
            }
            """)));
        var unmarked = Require(adapter.TryRead(Artifact(
            "class-sorcerer-unmarked.json",
            """
            {
              "class": [
                {"name":"Sorcerer","source":"PHB","page":99}
              ]
            }
            """)));

        var first = Assert.Single(classic.Records);
        var second = Assert.Single(unmarked.Records);
        Assert.Equal("class|PHB|Sorcerer|", first.NativeKey);
        Assert.Equal(first.NativeKey, second.NativeKey);
        Assert.Equal(first.NativeIdentityJson, second.NativeIdentityJson);
        Assert.DoesNotContain("edition", first.NativeIdentityJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"edition\":\"classic\"", first.RawJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ClassFeatureIdentityUsesTheNativeClassAndLevelUidFields()
    {
        var representation = Require(new FiveEToolsSourceFormatAdapter().TryRead(Artifact(
            "class-features.json",
            """
            {
              "classFeature": [
                {
                  "name":"Ability Score Improvement",
                  "source":"PHB",
                  "className":"Sorcerer",
                  "classSource":"PHB",
                  "level":4
                },
                {
                  "name":"Ability Score Improvement",
                  "source":"PHB",
                  "className":"Sorcerer",
                  "classSource":"PHB",
                  "level":8
                }
              ]
            }
            """)));

        Assert.Equal(2, representation.Records.Count);
        Assert.NotEqual(representation.Records[0].NativeKey, representation.Records[1].NativeKey);
        Assert.Contains("\"className\":\"Sorcerer\"", representation.Records[0].NativeIdentityJson, StringComparison.Ordinal);
        Assert.Contains("\"level\":\"4\"", representation.Records[0].NativeIdentityJson, StringComparison.Ordinal);
        Assert.Contains("\"level\":\"8\"", representation.Records[1].NativeIdentityJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingLegacyFiveEToolsIdentityMetadataIsCorrectedWithoutCreatingNativeIdentity()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();

        var token = Guid.NewGuid().ToString("N");
        var packageKey = $"fiveetools-native-identity-{token}";
        var importer = new NormalizedSourceImportService(db);
        var oldRaw = "{\"name\":\"Sorcerer\",\"source\":\"PHB\",\"edition\":\"classic\"}";
        var oldRepresentation = new NormalizedSourceRepresentation(
            FiveEToolsSourceFormatAdapter.Format,
            new SourceRepresentationArtifact(
                "old-sorcerer.json",
                Encoding.UTF8.GetBytes(oldRaw),
                $"integration:fiveetools-old:{token}"),
            [new NormalizedSourceRecord(
                "class",
                "Sorcerer",
                "PHB",
                "class|PHB|Sorcerer|",
                oldRaw,
                NativeIdentityJson: "{\"source\":\"PHB\",\"id\":null,\"uniqueId\":null,\"parentSource\":null,\"edition\":\"classic\"}")]);

        var first = await importer.ImportAsync(new ImportNormalizedSourceRequest(
            packageKey,
            "5e.tools native identity test",
            "5e.tools",
            null,
            false,
            oldRepresentation));
        var originalEntity = Assert.Single(first.Entities);

        var currentRepresentation = Require(new FiveEToolsSourceFormatAdapter().TryRead(Artifact(
            "current-sorcerer.json",
            """
            {"class":[{"name":"Sorcerer","source":"PHB","edition":"classic","page":99}]}
            """,
            $"integration:fiveetools-current:{token}")));
        var second = await importer.ImportAsync(new ImportNormalizedSourceRequest(
            packageKey,
            "5e.tools native identity test",
            "5e.tools",
            null,
            false,
            currentRepresentation));

        var currentEntity = Assert.Single(second.Entities);
        Assert.Equal(originalEntity.EntityId, currentEntity.EntityId);
        var persisted = await db.SourceEntities.AsNoTracking().SingleAsync(value => value.Id == currentEntity.EntityId);
        Assert.Equal(Assert.Single(currentRepresentation.Records).NativeIdentityJson, persisted.NativeIdentityJson);
        Assert.DoesNotContain("edition", persisted.NativeIdentityJson, StringComparison.OrdinalIgnoreCase);

        var latest = await db.SourceEntityRevisions.AsNoTracking()
            .Where(value => value.SourceEntityId == currentEntity.EntityId)
            .OrderByDescending(value => value.RevisionNumber)
            .FirstAsync();
        Assert.Equal(latest.RawJson, latest.ContentJson);
    }

    private static SourceRepresentationArtifact Artifact(
        string fileName,
        string json,
        string? originIdentity = null) =>
        new(
            fileName,
            Encoding.UTF8.GetBytes(json.Replace("\r\n", "\n", StringComparison.Ordinal)),
            originIdentity ?? $"integration:fiveetools:{Guid.NewGuid():N}",
            MediaType: "application/json");

    private static T Require<T>(T? value) where T : class
    {
        Assert.NotNull(value);
        return value!;
    }
}
