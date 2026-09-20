using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class PcGenSrdPublicationReconciliationIntegrationTests
{
    private const string RulesCoreSourceCodeScheme = "rules-core-source-code";

    [Fact]
    public void ReviewedLegacySnapshotPublishesFormatNeutralSourceCodeIdentity()
    {
        const string json = """
            {
              "skill": [
                {
                  "name": "Balance",
                  "source": "SRD35",
                  "uniqueId": "skill-balance",
                  "documentUri": "https://example.invalid/srd35/skills/balance",
                  "body": "Key Ability: Dex"
                }
              ]
            }
            """;

        var representation = new LegacySrdSourceFormatAdapter().TryRead(
            new SourceRepresentationArtifact(
                "srd-3-5e.json",
                Encoding.UTF8.GetBytes(json),
                "test:reviewed-legacy-srd"));

        Assert.NotNull(representation);
        var publication = Assert.Single(representation!.Publications!);
        Assert.NotNull(publication.ExternalIdentifiers);
        Assert.Equal("SRD35", publication.ExternalIdentifiers!["5etools-source-code"]);
        Assert.Equal("SRD35", publication.ExternalIdentifiers[RulesCoreSourceCodeScheme]);
    }

    [Fact]
    public async Task OfficialPcGenSrdIdentitiesReuseCorrectBuiltInPublicationsWithoutCollapsingDistinctEditions()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var identities = new CanonicalPublicationIdentityService(db);
            var builtInThreeE = await identities.ResolveAsync(BuiltInSrd("SRD3", "3e"));
            var builtInThirtyFiveE = await identities.ResolveAsync(BuiltInSrd("SRD35", "3.5e"));
            var builtInFiveOne = await identities.ResolveAsync(BuiltInSrd("SRD51", "5e"));
            var importer = new NormalizedSourceImportService(db);

            var pcGenThreeE = PcGenCampaign(
                "data/3e/wizards_of_the_coast/srd/srd.pcc",
                """
                CAMPAIGN:3.0 SRD
                GAMEMODE:3e
                PUBNAMELONG:Wizards of the Coast
                SOURCELONG:System Reference Document
                SOURCESHORT:SRD
                SOURCEDATE:2000-01
                """);
            var pcGenThirtyFiveE = PcGenCampaign(
                "data/35e/wizards_of_the_coast/rsrd/rsrd.pcc",
                """
                CAMPAIGN:3.5 RSRD
                GAMEMODE:35e
                PUBNAMELONG:Wizards of the Coast
                SOURCELONG:Revised (v.3.5) System Reference Document
                SOURCESHORT:RSRD
                SOURCEDATE:2000-01
                """);
            var pcGenFiveZero = PcGenCampaign(
                "data/5e/wizards_of_the_coast/srd5/_system_reference_document_5.0.pcc",
                """
                CAMPAIGN:5.0 SRD
                GAMEMODE:5e
                PUBNAMELONG:Wizards of the Coast
                SOURCELONG:System Reference Document
                SOURCESHORT:SRD5
                SOURCEDATE:2016-01
                """);
            var ambiguousThirtyFiveEGenericSrd = PcGenCampaign(
                "data/35e/example/generic-srd.pcc",
                """
                CAMPAIGN:Generic 3.5 SRD Fixture
                GAMEMODE:35e
                PUBNAMELONG:Wizards of the Coast
                SOURCELONG:System Reference Document
                SOURCESHORT:SRD
                """);

            Assert.Equal("SRD", Assert.Single(pcGenThreeE.Records).SourceCode);
            Assert.Equal("RSRD", Assert.Single(pcGenThirtyFiveE.Records).SourceCode);
            Assert.Equal("SRD5", Assert.Single(pcGenFiveZero.Records).SourceCode);

            var importedThreeE = await importer.ImportAsync(Request($"pcgen-srd3-{token}", pcGenThreeE));
            var importedThirtyFiveE = await importer.ImportAsync(Request($"pcgen-srd35-{token}", pcGenThirtyFiveE));
            var importedFiveZero = await importer.ImportAsync(Request($"pcgen-srd5-{token}", pcGenFiveZero));
            var importedAmbiguous = await importer.ImportAsync(Request(
                $"pcgen-generic-srd35-{token}",
                ambiguousThirtyFiveEGenericSrd));

            Assert.Equal(builtInThreeE.Id, Assert.Single(importedThreeE.Publications).CanonicalPublicationId);
            Assert.Equal(builtInThirtyFiveE.Id, Assert.Single(importedThirtyFiveE.Publications).CanonicalPublicationId);

            var fiveZeroId = Assert.Single(importedFiveZero.Publications).CanonicalPublicationId;
            Assert.NotEqual(builtInFiveOne.Id, fiveZeroId);
            Assert.NotEqual(builtInThreeE.Id, fiveZeroId);
            Assert.NotEqual(builtInThirtyFiveE.Id, fiveZeroId);

            var ambiguousId = Assert.Single(importedAmbiguous.Publications).CanonicalPublicationId;
            Assert.NotEqual(builtInThreeE.Id, ambiguousId);
            Assert.NotEqual(builtInThirtyFiveE.Id, ambiguousId);

            var threeEAliases = await ReadAliasesAsync(db, builtInThreeE.Id);
            Assert.Contains(
                threeEAliases,
                value => value.Scheme == RulesCoreSourceCodeScheme && value.Value == "srd3");
            Assert.DoesNotContain(threeEAliases, value => value.Scheme == "pcgen-source-short");
        }
    }

    [Fact]
    public async Task LookalikePcGenSourceDoesNotReceiveReviewedSrdPublicationIdentity()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var identities = new CanonicalPublicationIdentityService(db);
            var builtInThreeE = await identities.ResolveAsync(BuiltInSrd("SRD3", "3e"));
            var importer = new NormalizedSourceImportService(db);
            var representation = PcGenCampaign(
                "data/3e/wizards_of_the_coast/srd/srd.pcc",
                """
                CAMPAIGN:3.0 SRD
                GAMEMODE:3e
                PUBNAMELONG:Wizards of the Coast
                SOURCELONG:System Reference Document
                SOURCESHORT:SRD
                SOURCEDATE:2000-01
                """,
                officialSource: false);

            var imported = await importer.ImportAsync(Request($"pcgen-lookalike-{token}", representation));

            Assert.NotEqual(
                builtInThreeE.Id,
                Assert.Single(imported.Publications).CanonicalPublicationId);
            var nativePublication = Assert.Single(representation.Publications!);
            Assert.True(nativePublication.ExternalIdentifiers!.ContainsKey("pcgen-source-short"));
            Assert.False(nativePublication.ExternalIdentifiers.ContainsKey(RulesCoreSourceCodeScheme));
        }
    }

    [Fact]
    public async Task ReimportRepairsStalePublicationAndOccurrenceLinksWithoutCreatingSourceRevision()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var identities = new CanonicalPublicationIdentityService(db);
            var builtInThreeE = await identities.ResolveAsync(BuiltInSrd("SRD3", "3e"));
            var importer = new NormalizedSourceImportService(db);
            var representation = PcGenPublishedRecord(
                "data/3e/wizards_of_the_coast/srd/srd.pcc",
                """
                CAMPAIGN:3.0 SRD
                GAMEMODE:3e
                PUBNAMELONG:Wizards of the Coast
                SOURCELONG:System Reference Document
                SOURCESHORT:SRD
                SOURCEDATE:2000-01
                """);
            var publication = Assert.Single(representation.Publications!);
            var legacyLikeRepresentation = representation with
            {
                Publications =
                [
                    publication with { ExternalIdentifiers = null }
                ]
            };
            var packageKey = $"pcgen-stale-srd3-{token}";

            var first = await importer.ImportAsync(Request(packageKey, legacyLikeRepresentation));
            var stalePublicationId = Assert.Single(first.Publications).CanonicalPublicationId;
            Assert.NotEqual(builtInThreeE.Id, stalePublicationId);
            var entityId = Assert.Single(first.Entities).EntityId;
            Assert.Equal(stalePublicationId, await ReadEntityPublicationAsync(db, entityId));
            Assert.Equal(stalePublicationId, await ReadRepresentationPublicationAsync(db, packageKey));

            var repaired = await importer.ImportAsync(Request(packageKey, representation));

            Assert.Empty(repaired.ReconciliationIssues);
            Assert.False(Assert.Single(repaired.Entities).CreatedRevision);
            Assert.Equal(builtInThreeE.Id, Assert.Single(repaired.Publications).CanonicalPublicationId);
            Assert.Equal(builtInThreeE.Id, await ReadEntityPublicationAsync(db, entityId));
            Assert.Equal(builtInThreeE.Id, await ReadRepresentationPublicationAsync(db, packageKey));
        }
    }

    private static CanonicalPublicationEvidence BuiltInSrd(string sourceCode, string gameEdition) =>
        new(
            sourceCode,
            GameEdition: gameEdition,
            Aliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["5etools-source-code"] = sourceCode
            });

    private static ImportNormalizedSourceRequest Request(
        string packageKey,
        NormalizedSourceRepresentation representation) =>
        new(
            packageKey,
            packageKey,
            "PCGen",
            License: null,
            IsPublic: false,
            representation);

    private static NormalizedSourceRepresentation PcGenCampaign(
        string path,
        string text,
        bool officialSource = true)
    {
        var sourceUri = officialSource
            ? $"https://raw.githubusercontent.com/PCGen/pcgen/master/{path}"
            : $"https://example.invalid/PCGen/pcgen/master/{path}";
        var representation = new PcGenSourceFormatAdapter().TryRead(new SourceRepresentationArtifact(
            Path.GetFileName(path),
            Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal)),
            $"test:pcgen-srd-reconciliation#{path}",
            SourceUri: sourceUri,
            MediaType: "text/plain"));
        Assert.NotNull(representation);
        return representation!;
    }

    private static NormalizedSourceRepresentation PcGenPublishedRecord(
        string campaignPath,
        string campaignText)
    {
        const string listFileName = "rules-core-reconciliation-fixture.lst";
        var separator = campaignPath.LastIndexOf('/');
        var directory = separator < 0 ? string.Empty : campaignPath[..separator];
        var listPath = string.IsNullOrEmpty(directory)
            ? listFileName
            : $"{directory}/{listFileName}";
        var campaignWithReference = $"{campaignText.TrimEnd()}\nSPELL:{listFileName}\n";
        var adapter = new PcGenSourceFormatAdapter();
        var representations = adapter.TryReadMany(
        [
            new SourceRepresentationArtifact(
                Path.GetFileName(campaignPath),
                Encoding.UTF8.GetBytes(campaignWithReference.Replace("\r\n", "\n", StringComparison.Ordinal)),
                $"test:pcgen-srd-reconciliation#{campaignPath}",
                SourceUri: $"https://raw.githubusercontent.com/PCGen/pcgen/master/{campaignPath}",
                MediaType: "text/plain"),
            new SourceRepresentationArtifact(
                listFileName,
                Encoding.UTF8.GetBytes("Arc Spark\tTYPE:Arcane\tSCHOOL:Evocation\tDESC:Publication reconciliation fixture.\n"),
                $"test:pcgen-srd-reconciliation#{listPath}",
                SourceUri: $"https://raw.githubusercontent.com/PCGen/pcgen/master/{listPath}",
                MediaType: "text/plain")
        ]);

        var representation = Assert.Single(
            representations,
            value => string.Equals(value.Artifact.FileName, listFileName, StringComparison.Ordinal));
        Assert.Single(representation.Publications!);
        Assert.Single(representation.Records);
        Assert.NotNull(Assert.Single(representation.Records).PublicationLocalKey);
        return representation;
    }

    private static async Task<Guid?> ReadEntityPublicationAsync(RulesCoreDbContext db, Guid sourceEntityId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT occurrence.canonical_publication_id
                FROM source_entity_revision revision
                JOIN source_entity_occurrence_binding binding
                    ON binding.source_entity_revision_id = revision.source_entity_revision_id
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                WHERE revision.source_entity_id = @source_entity_id
                ORDER BY revision.revision_number DESC
                LIMIT 1;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            var result = await command.ExecuteScalarAsync();
            return result is Guid id ? id : null;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<Guid?> ReadRepresentationPublicationAsync(
        RulesCoreDbContext db,
        string packageKey)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT link.canonical_publication_id
                FROM source_representation_publication link
                JOIN source_representation representation
                    ON representation.source_representation_id = link.source_representation_id
                JOIN source_package package
                    ON package.source_package_id = representation.source_package_id
                WHERE package.package_key = @package_key
                ORDER BY representation.imported_at DESC
                LIMIT 1;
                """;
            AddParameter(command, "@package_key", packageKey);
            var result = await command.ExecuteScalarAsync();
            return result is Guid id ? id : null;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<IReadOnlyList<(string Scheme, string Value)>> ReadAliasesAsync(
        RulesCoreDbContext db,
        Guid publicationId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT alias_scheme, alias_value
                FROM canonical_publication_alias
                WHERE canonical_publication_id = @publication_id
                ORDER BY alias_scheme, alias_value;
                """;
            AddParameter(command, "@publication_id", publicationId);
            var result = new List<(string Scheme, string Value)>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add((reader.GetString(0), reader.GetString(1)));
            }
            return result;
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
