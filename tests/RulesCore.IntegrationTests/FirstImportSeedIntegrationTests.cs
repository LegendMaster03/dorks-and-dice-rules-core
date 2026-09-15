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
public sealed class FirstImportSeedIntegrationTests
{
    [Fact]
    public async Task FirstTrustedPcGenImportSeedsCanonicalAliasForLaterPrivateImports()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var sourceUri = $"https://raw.githubusercontent.com/PCGen/pcgen/master/data/35e/wizards_of_the_coast/seed_{token}_spells.lst";
            var text = string.Join('\n',
            [
                $"SOURCELONG:Seed Fixture {token}\tSOURCESHORT:S35{token}",
                $"Arc Spark {token}\tTYPE:Arcane\tSCHOOL:Evocation\tDESC:Representative source text."
            ]);

            NormalizedSourceRepresentation Build(string originIdentity) =>
                new PcGenSourceFormatAdapter().TryRead(new SourceRepresentationArtifact(
                    $"seed_{token}_spells.lst",
                    Encoding.UTF8.GetBytes(text),
                    originIdentity,
                    sourceUri,
                    "text/plain"))
                ?? throw new InvalidOperationException("PCGen seed fixture was not readable.");

            await AssertFirstImportSeedsAndSecondReusesAsync(
                db,
                Build,
                "pcgen-org-pcgen",
                $"pcgen-seed-a-{token}",
                $"pcgen-seed-b-{token}");
        }
    }

    [Fact]
    public async Task FirstTrustedFiveEToolsImportSeedsCanonicalAliasForLaterPrivateImports()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var sourceCode = $"S5{token}";
            var sourceUri = $"https://raw.githubusercontent.com/5etools-mirror-3/5etools-src/main/data/spells/seed-{token}.json";
            var json = JsonSerializer.Serialize(new
            {
                _meta = new
                {
                    edition = "classic",
                    sources = new[]
                    {
                        new
                        {
                            json = sourceCode,
                            full = $"Seed Publication {token}",
                            dateReleased = "2014-08-19"
                        }
                    }
                },
                spell = new[]
                {
                    new
                    {
                        name = $"Seed Spell {token}",
                        source = sourceCode,
                        level = 1,
                        school = "A",
                        entries = new[] { "Representative source text." }
                    }
                }
            });

            NormalizedSourceRepresentation Build(string originIdentity) =>
                new FiveEToolsSourceFormatAdapter().TryRead(new SourceRepresentationArtifact(
                    $"seed-{token}.json",
                    Encoding.UTF8.GetBytes(json),
                    originIdentity,
                    sourceUri,
                    "application/json"))
                ?? throw new InvalidOperationException("5e.tools seed fixture was not readable.");

            await AssertFirstImportSeedsAndSecondReusesAsync(
                db,
                Build,
                "5etools-mirror-3-5etools-src",
                $"5etools-seed-a-{token}",
                $"5etools-seed-b-{token}");
        }
    }

    private static async Task AssertFirstImportSeedsAndSecondReusesAsync(
        RulesCoreDbContext db,
        Func<string, NormalizedSourceRepresentation> buildRepresentation,
        string aliasScheme,
        string firstPackageKey,
        string secondPackageKey)
    {
        var importer = new NormalizedSourceImportService(db);
        var firstRepresentation = buildRepresentation($"integration:first-seed:{Guid.NewGuid():N}");
        var firstRecord = Assert.Single(firstRepresentation.Records);
        var first = await importer.ImportAsync(new ImportNormalizedSourceRequest(
            firstPackageKey,
            firstPackageKey,
            "integration-test",
            License: null,
            IsPublic: false,
            firstRepresentation));
        var firstEntity = Assert.Single(first.Entities);
        var firstBinding = await ReadLatestBindingAsync(db, firstEntity.EntityId);
        Assert.NotNull(firstBinding);

        Assert.Equal(
            firstBinding.Value.CanonicalEntityId,
            await new CanonicalEntityAliasStore(db).ResolveAsync(
                aliasScheme,
                firstRecord.NativeKey,
                firstBinding.Value.SemanticFingerprint));

        var grants = new SourceGrantService(db);
        await grants.GrantAsync("seed-user-a", first.PackageId);

        var secondRepresentation = buildRepresentation($"integration:second-seed:{Guid.NewGuid():N}");
        Assert.Equal(firstRecord.NativeKey, Assert.Single(secondRepresentation.Records).NativeKey);
        var second = await importer.ImportAsync(new ImportNormalizedSourceRequest(
            secondPackageKey,
            secondPackageKey,
            "integration-test",
            License: null,
            IsPublic: false,
            secondRepresentation));
        var secondEntity = Assert.Single(second.Entities);
        var secondBinding = await ReadLatestBindingAsync(db, secondEntity.EntityId);
        Assert.NotNull(secondBinding);

        Assert.NotEqual(first.PackageId, second.PackageId);
        Assert.NotEqual(firstEntity.EntityId, secondEntity.EntityId);
        Assert.Equal(firstBinding.Value.CanonicalEntityId, secondBinding.Value.CanonicalEntityId);
        Assert.EndsWith(":strong-alias", secondBinding.Value.MatchKind, StringComparison.Ordinal);

        await grants.GrantAsync("seed-user-b", second.PackageId);
        var catalog = new SourceCatalogService(db);
        Assert.NotNull(await catalog.GetLatestAccessibleEntityAsync(firstEntity.EntityId, "seed-user-a"));
        Assert.Null(await catalog.GetLatestAccessibleEntityAsync(secondEntity.EntityId, "seed-user-a"));
        Assert.NotNull(await catalog.GetLatestAccessibleEntityAsync(secondEntity.EntityId, "seed-user-b"));
        Assert.Null(await catalog.GetLatestAccessibleEntityAsync(firstEntity.EntityId, "seed-user-b"));
    }

    private static async Task<(Guid CanonicalEntityId, string SemanticFingerprint, string MatchKind)?> ReadLatestBindingAsync(
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
                SELECT occurrence.canonical_entity_id, binding.semantic_fingerprint, binding.match_kind
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
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync()
                ? (reader.GetGuid(0), reader.GetString(1), reader.GetString(2))
                : null;
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
