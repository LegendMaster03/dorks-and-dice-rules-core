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
public sealed class WikiReferenceSpellAccessIntegrationTests
{
    [Fact]
    public async Task PrivateSpellLookupDoesNotEnrichPublicSpellUntilGranted()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..10];
            var spellName = $"Phase Four Bolt {token}";
            var importer = new ReconciledNormalizedSourceImportService(
                new NormalizedSourceImportService(db),
                db);

            var spellRepresentation = ReadFiveEToolsSpell(spellName);
            await importer.ImportAsync(new ImportNormalizedSourceRequest(
                $"spell-public-{token}",
                $"Spell Public {token}",
                "integration-test",
                null,
                true,
                spellRepresentation));

            var lookupRepresentation = ReadSpellLookup(spellName);
            var privateLookup = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                $"spell-lookup-private-{token}",
                $"Spell Lookup Private {token}",
                "integration-test",
                null,
                false,
                lookupRepresentation));

            var anonymous = await CatalogAsync(db, null, spellName);
            var anonymousReference = Assert.Single(
                anonymous.References.Where(value => value.DisplayName == spellName));
            Assert.DoesNotContain(
                anonymousReference.BrowserFields,
                value => value.Key == "spellList");

            await new SourceGrantService(db).GrantAsync("spell-access-reader", privateLookup.PackageId);

            var granted = await CatalogAsync(db, "spell-access-reader", spellName);
            var grantedReference = Assert.Single(
                granted.References.Where(value => value.DisplayName == spellName));
            var spellList = Assert.Single(
                grantedReference.BrowserFields,
                value => value.Key == "spellList");
            Assert.Equal("Bard, Sorcerer, Wizard", spellList.Value);
        }
    }

    private static NormalizedSourceRepresentation ReadFiveEToolsSpell(string spellName)
    {
        var json = JsonSerializer.Serialize(new
        {
            _meta = new
            {
                edition = "one",
                sources = new[]
                {
                    new
                    {
                        json = "XPHB",
                        full = "2024 Player's Handbook",
                        dateReleased = "2024-09-17"
                    }
                }
            },
            spell = new[]
            {
                new
                {
                    name = spellName,
                    source = "XPHB",
                    level = 3,
                    school = "V",
                    entries = new[] { "Spell mechanics." }
                }
            }
        });
        return new FiveEToolsCompanionSourceFormatAdapter().TryRead(
            new SourceRepresentationArtifact(
                "data/spells/spells-xphb.json",
                Encoding.UTF8.GetBytes(json),
                $"integration:spell:{spellName}",
                "https://raw.githubusercontent.com/5etools-mirror-3/5etools-src/main/data/spells/spells-xphb.json",
                "application/json"))
            ?? throw new InvalidOperationException("Spell fixture was not readable.");
    }

    private static NormalizedSourceRepresentation ReadSpellLookup(string spellName)
    {
        var access = new
        {
            @class = new Dictionary<string, object>
            {
                ["XPHB"] = new Dictionary<string, object>
                {
                    ["Sorcerer"] = true,
                    ["Wizard"] = true
                }
            },
            classVariant = new Dictionary<string, object>
            {
                ["XPHB"] = new Dictionary<string, object>
                {
                    ["Bard"] = new { definedInSources = new[] { "XPHB" } }
                }
            }
        };
        var source = new Dictionary<string, object>
        {
            ["xphb"] = new Dictionary<string, object>
            {
                [spellName.ToLowerInvariant()] = access
            }
        };
        var json = JsonSerializer.Serialize(source);
        return new FiveEToolsCompanionSourceFormatAdapter().TryRead(
            new SourceRepresentationArtifact(
                "data/generated/gendata-spell-source-lookup.json",
                Encoding.UTF8.GetBytes(json),
                $"integration:spell-lookup:{spellName}",
                "https://raw.githubusercontent.com/5etools-mirror-3/5etools-src/main/data/generated/gendata-spell-source-lookup.json",
                "application/json"))
            ?? throw new InvalidOperationException("Spell lookup fixture was not readable.");
    }

    private static async Task<WikiReferenceCatalogView> CatalogAsync(
        RulesCoreDbContext db,
        string? userId,
        string query)
    {
        var catalog = await new WikiReferenceCatalogService(db).GetGlobalCatalogAsync(
            userId,
            entityType: "spell",
            categoryMode: WikiReferenceCategoryModes.AnyVariation,
            query,
            sourceCode: null,
            packageKey: null,
            edition: null,
            limit: 100,
            offset: 0);
        catalog = await new WikiReferenceBrowserProjectionService(db).EnrichAsync(catalog);
        return await new WikiReferenceSpellAccessEnrichmentService(db).EnrichAsync(
            catalog,
            userId);
    }

    private static async Task<RulesCoreDbContext?> OpenDatabaseAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>()
                .UseNpgsql(connectionString)
                .Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        return db;
    }
}
