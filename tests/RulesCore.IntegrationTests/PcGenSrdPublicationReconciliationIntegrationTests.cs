using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class PcGenSrdPublicationReconciliationIntegrationTests
{
    [Fact]
    public async Task KnownPcGenSrdIdentitiesReuseTheCorrectBuiltInPublicationsWithoutCollapsingDistinctRevisions()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var identities = new CanonicalPublicationIdentityService(db);
            var builtInThreeE = await identities.ResolveAsync(BuiltInSrd("SRD3", "3e"));
            var builtInThirtyFiveE = await identities.ResolveAsync(BuiltInSrd("SRD35", "3.5e"));
            var builtInFiveOne = await identities.ResolveAsync(BuiltInSrd("SRD51", "5e"));

            Assert.NotEqual(builtInThreeE.Id, builtInThirtyFiveE.Id);
            Assert.NotEqual(builtInThreeE.Id, builtInFiveOne.Id);
            Assert.NotEqual(builtInThirtyFiveE.Id, builtInFiveOne.Id);

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
                SOURCEDATE:2003-07
                """);
            var pcGenFiveZero = PcGenCampaign(
                "data/5e/wizards_of_the_coast/srd5/_system_reference_document_5.0.pcc",
                """
                CAMPAIGN:5.0 SRD
                GAMEMODE:5e
                PUBNAMELONG:Wizards of the Coast
                SOURCELONG:System Reference Document 5.0
                SOURCESHORT:SRD5
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

            var resolvedThreeE = await identities.ResolveAsync(ToEvidence(Assert.Single(pcGenThreeE.Publications!)));
            var resolvedThirtyFiveE = await identities.ResolveAsync(ToEvidence(Assert.Single(pcGenThirtyFiveE.Publications!)));
            var resolvedFiveZero = await identities.ResolveAsync(ToEvidence(Assert.Single(pcGenFiveZero.Publications!)));
            var resolvedAmbiguousThirtyFiveE = await identities.ResolveAsync(
                ToEvidence(Assert.Single(ambiguousThirtyFiveEGenericSrd.Publications!)));

            Assert.Equal(builtInThreeE.Id, resolvedThreeE.Id);
            Assert.Equal("alias:5etools-source-code", resolvedThreeE.MatchKind);
            Assert.Equal(builtInThirtyFiveE.Id, resolvedThirtyFiveE.Id);
            Assert.Equal("alias:5etools-source-code", resolvedThirtyFiveE.MatchKind);

            Assert.NotEqual(builtInFiveOne.Id, resolvedFiveZero.Id);
            Assert.NotEqual(builtInThreeE.Id, resolvedFiveZero.Id);
            Assert.NotEqual(builtInThirtyFiveE.Id, resolvedFiveZero.Id);

            Assert.NotEqual(builtInThreeE.Id, resolvedAmbiguousThirtyFiveE.Id);
            Assert.NotEqual(builtInThirtyFiveE.Id, resolvedAmbiguousThirtyFiveE.Id);
            Assert.Equal("3.5e", Assert.Single(pcGenThirtyFiveE.Publications!).GameEdition);
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

    private static CanonicalPublicationEvidence ToEvidence(NormalizedSourcePublication publication) =>
        new(
            publication.DisplayName,
            publication.Publisher,
            publication.GameEdition,
            publication.PublicationDate,
            publication.ExternalIdentifiers);

    private static NormalizedSourceRepresentation PcGenCampaign(string path, string text)
    {
        var representation = new PcGenSourceFormatAdapter().TryRead(new SourceRepresentationArtifact(
            Path.GetFileName(path),
            Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal)),
            $"test:pcgen-srd-reconciliation#{path}",
            SourceUri: $"https://raw.githubusercontent.com/PCGen/pcgen/master/{path}",
            MediaType: "text/plain"));
        Assert.NotNull(representation);
        return representation!;
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
}
