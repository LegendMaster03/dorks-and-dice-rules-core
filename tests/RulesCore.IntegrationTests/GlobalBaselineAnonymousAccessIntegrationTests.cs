using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Infrastructure.Bootstrap;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class GlobalBaselineAnonymousAccessIntegrationTests
{
    private static readonly string[] BuiltInPackageKeys =
    [
        "wotc-srd-ogl",
        "wotc-srd-cc",
        "loot-tavern-free",
        "loot-tavern-licensed",
        "dorks-and-dice-baseline"
    ];

    [Fact]
    public async Task PublicGlobalBaselineIsReadableWithoutAuthentication()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            await ResetAsync(db);
            var bootstrapper = scope.ServiceProvider.GetRequiredService<IRulesCoreBaselineBootstrapper>();
            await bootstrapper.EnsureAsync();
        }

        try
        {
            using var client = factory.CreateClient();

            using (var sourcesResponse = await client.GetAsync("/api/sources"))
            {
                Assert.Equal(HttpStatusCode.OK, sourcesResponse.StatusCode);
                using var body = JsonDocument.Parse(await sourcesResponse.Content.ReadAsStringAsync());
                var keys = body.RootElement.EnumerateArray()
                    .Select(value => value.GetProperty("key").GetString())
                    .Where(value => value is not null)
                    .ToHashSet(StringComparer.Ordinal);

                Assert.Contains("wotc-srd-ogl", keys);
                Assert.Contains("wotc-srd-cc", keys);
                Assert.Contains("loot-tavern-free", keys);
                Assert.Contains("dorks-and-dice-baseline", keys);
                Assert.DoesNotContain("loot-tavern-licensed", keys);
            }

            using (var ruleResponse = await client.GetAsync("/api/rules/house.healing-potion-use"))
            {
                Assert.Equal(HttpStatusCode.OK, ruleResponse.StatusCode);
                using var body = JsonDocument.Parse(await ruleResponse.Content.ReadAsStringAsync());
                Assert.Equal(
                    "maximum possible healing",
                    body.RootElement.GetProperty("document")
                        .GetProperty("action")
                        .GetProperty("healing")
                        .GetString());
            }

            using (var catalogResponse = await client.GetAsync("/api/rules?limit=100"))
            {
                Assert.Equal(HttpStatusCode.OK, catalogResponse.StatusCode);
                using var body = JsonDocument.Parse(await catalogResponse.Content.ReadAsStringAsync());
                var serialized = body.RootElement.GetRawText();
                Assert.Contains("house.healing-potion-use", serialized, StringComparison.Ordinal);
                Assert.Contains("house.spell-preparation", serialized, StringComparison.Ordinal);
                Assert.Contains("house.cross-edition-additive-compatibility", serialized, StringComparison.Ordinal);
            }
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await ResetAsync(scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>());
        }
    }

    private static async Task ResetAsync(RulesCoreDbContext db)
    {
        await db.CampaignRulesetRevisionEntries.ExecuteDeleteAsync();
        await db.CampaignRulesetRevisions.ExecuteDeleteAsync();
        await db.CampaignRuleDecisions.ExecuteDeleteAsync();
        await db.CampaignRulesetSelections.ExecuteDeleteAsync();
        await db.RulesetRevisionEntries.ExecuteDeleteAsync();
        await db.RulesetRevisions.ExecuteDeleteAsync();
        await db.GlobalRuleDecisions.ExecuteDeleteAsync();
        await db.RuleConceptSourceBindings.ExecuteDeleteAsync();
        await db.RuleConcepts.ExecuteDeleteAsync();

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS source_edition_authority_reference (
                source_edition_authority_reference_id uuid NOT NULL,
                source_edition_id uuid NOT NULL,
                authority_kind varchar(80) NOT NULL,
                uri varchar(2000) NOT NULL,
                media_type varchar(200) NOT NULL,
                note varchar(2000) NULL,
                created_at timestamp with time zone NOT NULL,
                CONSTRAINT pk_source_edition_authority_reference PRIMARY KEY (source_edition_authority_reference_id),
                CONSTRAINT fk_source_edition_authority_reference_edition FOREIGN KEY (source_edition_id)
                    REFERENCES source_edition(source_edition_id) ON DELETE CASCADE);
            DELETE FROM hosted_source_definition
            WHERE definition_key IN ('builtin-wotc-srd-5-1', 'builtin-wotc-srd-5-2-1');
            """);

        db.ChangeTracker.Clear();
        var packages = await db.SourcePackages
            .Where(value => BuiltInPackageKeys.Contains(value.Key))
            .ToArrayAsync();
        db.SourcePackages.RemoveRange(packages);
        await db.SaveChangesAsync();
    }
}
