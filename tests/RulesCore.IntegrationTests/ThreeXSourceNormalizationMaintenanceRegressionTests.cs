using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class ThreeXSourceNormalizationMaintenanceRegressionTests
{
    [Fact]
    public async Task MaintenanceReclassifiesLegacyPcGenMonsterProjectionWithoutJoiningMonsterAndRaceHistory()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var packageKey = $"pcgen-race-maintenance-{token}";
            var campaignPath = $"data/35e/example/{token}/fixture.pcc";
            var racePath = $"data/35e/example/{token}/monsters/fixture_races.lst";
            var adapter = new PcGenSourceFormatAdapter();
            var representations = adapter.TryReadMany(
            [
                Artifact(
                    campaignPath,
                    """
                    CAMPAIGN:3.5 Maintenance Fixture
                    GAMEMODE:35e
                    PUBNAMELONG:Integration Test Press
                    SOURCELONG:3.5 Maintenance Fixture
                    SOURCESHORT:T35M
                    SOURCEDATE:2003-07
                    RACE:monsters/fixture_races.lst
                    """),
                Artifact(
                    racePath,
                    """
                    Goblin	FAVCLASS:Rogue	STARTFEATS:1	SIZE:S	MOVE:Walk,30	BONUS:STAT|STR|-2	BONUS:STAT|DEX|2	BONUS:STAT|CHA|-2	MONSTERCLASS:Humanoid:1	RACETYPE:Humanoid	RACESUBTYPE:Goblinoid	TYPE:Humanoid	CR:1/2
                    """)
            ]);
            var representation = Assert.Single(
                representations,
                value => string.Equals(
                    value.Artifact.FileName,
                    "fixture_races.lst",
                    StringComparison.Ordinal));

            try
            {
                var imported = await new NormalizedSourceImportService(db).ImportAsync(
                    new ImportNormalizedSourceRequest(
                        packageKey,
                        $"PCGen race maintenance fixture {token}",
                        "integration-test",
                        "test-only",
                        true,
                        representation));
                var importedEntity = Assert.Single(imported.Entities);
                Assert.Equal("race", importedEntity.EntityType);

                var entity = await db.SourceEntities
                    .SingleAsync(value => value.Id == importedEntity.EntityId);
                var revision = await db.SourceEntityRevisions
                    .SingleAsync(value => value.SourceEntityId == entity.Id);
                var correctedRaceCanonicalId = await ReadBoundCanonicalEntityAsync(
                    db,
                    revision.Id);

                const string staleMonsterContent = """
                    {
                      "name":"Goblin",
                      "source":"T35M",
                      "size":["S"],
                      "type":"humanoid",
                      "speed":30,
                      "cr":"1/2",
                      "_rulesCore":{
                        "context":{
                          "edition":"3.5e",
                          "sourceFormat":"pcgen-data",
                          "nativeEntityType":"race",
                          "translatedEntityType":"monster"
                        }
                      }
                    }
                    """;
                var staleFingerprint = CanonicalSourceIdentity.SemanticFingerprint(
                    staleMonsterContent);
                var publication = Assert.Single(representation.Publications!);
                await new CanonicalSourceRepresentationService(db).AssociateSourceEntityAsync(
                    entity.Id,
                    revision.Id,
                    new CanonicalPublicationEvidence(
                        publication.DisplayName,
                        publication.Publisher,
                        publication.GameEdition,
                        publication.PublicationDate,
                        publication.ExternalIdentifiers,
                        [staleFingerprint]),
                    new CanonicalSourceOccurrenceEvidence(
                        "monster",
                        "Goblin",
                        revision.LocatorKey,
                        staleFingerprint),
                    representation.FormatKey,
                    canonicalAliases: null,
                    allowTranslationOnlyReassociation: true);
                var staleMonsterCanonicalId = await ReadBoundCanonicalEntityAsync(
                    db,
                    revision.Id);
                Assert.NotEqual(correctedRaceCanonicalId, staleMonsterCanonicalId);

                entity.EntityType = "monster";
                revision.ContentJson = staleMonsterContent;
                revision.NormalizationVersion = 1;
                revision.NormalizationAttemptVersion = 1;
                revision.NormalizationAttemptedAt = DateTimeOffset.UtcNow;
                revision.NormalizationError = null;
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();

                var registry = new SourceFormatAdapterRegistry(
                [
                    new FiveEToolsSourceFormatAdapter(),
                    new PcGenSourceFormatAdapter(),
                    new PdfSourceFormatAdapter()
                ]);
                var maintenance = new SourceNormalizationMaintenanceService(db, registry);
                var result = await maintenance.ReconcileAsync(
                    limit: 10,
                    retryFailed: false,
                    packageKey: packageKey);

                Assert.Equal(1, result.AttemptedRevisionCount);
                Assert.Equal(1, result.UpdatedContentCount);
                Assert.Equal(1, result.CanonicalReassociationCount);
                Assert.Equal(0, result.FailedRevisionCount);
                Assert.Empty(result.Failures);

                var repairedEntity = await db.SourceEntities
                    .AsNoTracking()
                    .SingleAsync(value => value.Id == entity.Id);
                Assert.Equal("race", repairedEntity.EntityType);

                var repairedRevision = await db.SourceEntityRevisions
                    .AsNoTracking()
                    .SingleAsync(value => value.Id == revision.Id);
                Assert.Equal(SourceNormalizationVersion.Current, repairedRevision.NormalizationVersion);
                Assert.Contains("\"ability\"", repairedRevision.ContentJson!, StringComparison.Ordinal);
                Assert.DoesNotContain("\"cr\"", repairedRevision.ContentJson!, StringComparison.Ordinal);
                Assert.DoesNotContain("\"translatedEntityType\"", repairedRevision.ContentJson!, StringComparison.Ordinal);
                Assert.Equal(
                    1,
                    await db.SourceEntityRevisions.CountAsync(
                        value => value.SourceEntityId == entity.Id));

                var repairedRaceCanonicalId = await ReadBoundCanonicalEntityAsync(
                    db,
                    revision.Id);
                Assert.Equal(correctedRaceCanonicalId, repairedRaceCanonicalId);
                Assert.False(await HasSameHistoryRelationshipAsync(
                    db,
                    staleMonsterCanonicalId,
                    repairedRaceCanonicalId));
            }
            finally
            {
                await db.SourcePackages
                    .Where(value => value.Key == packageKey)
                    .ExecuteDeleteAsync();
            }
        }
    }

    private static async Task<Guid> ReadBoundCanonicalEntityAsync(
        RulesCoreDbContext db,
        Guid revisionId)
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
                WHERE binding.source_entity_revision_id = @revision_id;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@revision_id";
            parameter.Value = revisionId;
            command.Parameters.Add(parameter);
            return (Guid)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("The source revision is not canonically bound."));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<bool> HasSameHistoryRelationshipAsync(
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
                SELECT EXISTS (
                    SELECT 1
                    FROM canonical_entity_relationship
                    WHERE relationship_kind IN ('revision', 'rename')
                      AND ((from_canonical_entity_id = @left AND to_canonical_entity_id = @right)
                        OR (from_canonical_entity_id = @right AND to_canonical_entity_id = @left)));
                """;
            foreach (var (name, value) in new[] { ("@left", left), ("@right", right) })
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }
            return Convert.ToBoolean(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static SourceRepresentationArtifact Artifact(string path, string text) =>
        new(
            Path.GetFileName(path),
            Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal)),
            $"test:three-x-maintenance#{path}",
            SourceUri: $"https://example.invalid/{path}",
            MediaType: "text/plain");

    private static async Task<RulesCoreDbContext?> OpenDatabaseAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        return db;
    }
}
