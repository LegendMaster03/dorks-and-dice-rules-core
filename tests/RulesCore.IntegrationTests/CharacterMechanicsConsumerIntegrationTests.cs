using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CharacterMechanicsConsumerIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task ConsumerEndpointsExposeGenericMechanicsAndPreserveCampaignReadAuthority()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var campaignId = Guid.NewGuid();
        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["player-ticket"] = Context("player", campaignId, "Player"),
            ["outsider-ticket"] = Context("outsider", campaignId: null, campaignRole: null)
        });

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IToolHostAuthenticationClient>();
                services.AddSingleton<IToolHostAuthenticationClient>(authenticationClient);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using (var globalResponse = await client.GetAsync("/api/rules/mechanics?includeUnavailable=true"))
        {
            Assert.Equal(HttpStatusCode.OK, globalResponse.StatusCode);
            var catalog = await globalResponse.Content.ReadFromJsonAsync<CharacterMechanicsCatalogView>();
            Assert.NotNull(catalog);
            Assert.Contains(catalog.Mechanics, value => value.MechanicKey == "check.competency");
            Assert.Contains(catalog.Mechanics, value => value.MechanicKey == "save.fortitude");
            var harvesting = Assert.Single(
                catalog.Mechanics,
                value => value.MechanicKey == "check.harvesting.total");
            Assert.False(harvesting.IsApplicableUnderRuleset);
            var attribution = Assert.Single(harvesting.SourceAttributions);
            Assert.Equal("loot-tavern-free", attribution.PackageKey);
            Assert.Equal("Loot Tavern Free Releases", attribution.PackageDisplayName);
            Assert.Equal(KnownCharacterMechanics.LootTavernReferenceKey, attribution.WorkKey);
            Assert.Equal("Harvesting & Crafting Lite", attribution.WorkDisplayName);
            Assert.Equal("5e", attribution.GameEdition);
            Assert.Equal(new DateOnly(2024, 7, 3), attribution.PublicationDate);
            Assert.True(attribution.PresentationRequired);
            Assert.True(attribution.ReferenceLinkRequired);
        }

        using (var evaluationResponse = await client.PostAsJsonAsync(
                   "/api/rules/mechanics/check.competency/evaluate",
                   new CharacterMechanicEvaluationRequest(
                       IntegerInputs: new Dictionary<string, int>
                       {
                           ["d20Roll"] = 12,
                           ["abilityModifier"] = 3,
                           ["competencyModifier"] = 2,
                           ["targetDc"] = 17
                       },
                       StringInputs: new Dictionary<string, string>
                       {
                           ["abilityKey"] = "intelligence",
                           ["competencyKey"] = "skill.arcana"
                       })))
        {
            Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
            var evaluation = await evaluationResponse.Content.ReadFromJsonAsync<CharacterMechanicEvaluationView>();
            Assert.NotNull(evaluation);
            Assert.Equal(17, evaluation.Value);
            Assert.True(evaluation.MeetsTarget);
        }

        using (var anonymousCampaign = await client.GetAsync(
                   $"/api/campaigns/{campaignId}/rules/mechanics"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousCampaign.StatusCode);
        }

        using (var outsiderRequest = HostedRequest(
                   HttpMethod.Get,
                   $"/api/campaigns/{campaignId}/rules/mechanics",
                   "outsider-ticket"))
        using (var outsiderResponse = await client.SendAsync(outsiderRequest))
        {
            Assert.Equal(HttpStatusCode.NotFound, outsiderResponse.StatusCode);
        }

        using (var playerRequest = HostedRequest(
                   HttpMethod.Get,
                   $"/api/campaigns/{campaignId}/rules/mechanics?includeUnavailable=true",
                   "player-ticket"))
        using (var playerResponse = await client.SendAsync(playerRequest))
        {
            Assert.Equal(HttpStatusCode.OK, playerResponse.StatusCode);
            Assert.Equal("no-store", playerResponse.Headers.CacheControl?.ToString());
            var catalog = await playerResponse.Content.ReadFromJsonAsync<CharacterMechanicsCatalogView>();
            Assert.NotNull(catalog);
            Assert.Equal("campaign", catalog.Scope);
            Assert.Equal(campaignId, catalog.CampaignId);
            Assert.Contains(catalog.Mechanics, value => value.MechanicKey == "check.competency");
        }
    }


    [Fact]
    public async Task EffectiveThreeXPublicationMetadataAndCompositeCompetenciesFlowThroughConsumerContract()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-mechanics-3x-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var mechanics = scope.ServiceProvider.GetRequiredService<ICharacterMechanicsConsumerService>();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

            var imported = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                PackageKey: packageKey,
                PackageDisplayName: $"Character Mechanics 3.x {token}",
                Provider: "integration-test",
                License: "test-only",
                IsPublic: true,
                WorkKey: $"character-mechanics-work-{token}",
                WorkDisplayName: $"Character Mechanics 3.5e Work {token}",
                EditionKey: "3-5e",
                EditionDisplayName: "3.5e",
                Json: $"""
                    {
                      "skill": [
                        { "name": "Hide", "source": "CM{{token}}" },
                        { "name": "Move Silently", "source": "CM{{token}}" },
                        { "name": "Stealth", "source": "CM{{token}}" }
                      ]
                    }
                    """,
                GameEdition: "3.5e"));
            packageId = imported.PackageId;

            foreach (var (name, conceptKey) in new[]
                     {
                         ("Hide", "skill.hide"),
                         ("Move Silently", "skill.move-silently"),
                         ("Stealth", "skill.stealth")
                     })
            {
                var source = imported.Entities.Single(value => value.Name == name);
                var concept = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(conceptKey, "skill", name),
                    $"mechanics-test-{token}");
                await globalRules.BindSourceEntityAsync(
                    concept.Value.Id,
                    new BindRuleConceptSourceRequest(source.EntityId),
                    $"mechanics-test-{token}");

                var revisionId = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == source.EntityId)
                    .Select(value => value.Id)
                    .SingleAsync();
                await globalRules.SetDecisionAsync(
                    concept.Value.Id,
                    new SetGlobalRuleDecisionRequest(revisionId, "Character mechanics consumer fixture."),
                    $"mechanics-test-{token}");
            }

            await globalRules.PublishAsync($"mechanics-test-{token}");

            var catalog = await mechanics.GetGlobalAsync(userId: null);
            var fortitude = Assert.Single(
                catalog.Mechanics,
                value => value.MechanicKey == "save.fortitude");
            Assert.True(fortitude.IsApplicableUnderRuleset);

            var stealth = Assert.Single(
                catalog.Mechanics,
                value => value.MechanicKey == "competency.skill.stealth");
            Assert.Equal(CharacterMechanicEvaluationKinds.CompositeCompetency, stealth.EvaluationKind);
            var relationship = Assert.Single(
                stealth.Relationships,
                value => value.RelationshipKey == "skill-composite.stealth");
            Assert.True(relationship.CanResolve);
            Assert.Equal(
                MechanicalRelationshipResolutionKinds.DeriveParent,
                relationship.EffectiveResolutionKind);

            var attribution = Assert.Single(
                stealth.SourceAttributions,
                value => value.GameEdition == "3.5e");
            Assert.Equal(1, attribution.SourceRevisionNumber);
            Assert.Equal("integration-test", attribution.Provider);
            Assert.NotNull(attribution.WorkKey);
            Assert.NotNull(attribution.WorkDisplayName);

            var evaluation = await mechanics.EvaluateGlobalAsync(
                "competency.skill.stealth",
                new CharacterMechanicEvaluationRequest(
                    IntegerInputs: new Dictionary<string, int>
                    {
                        ["skill.hide"] = 9,
                        ["skill.move-silently"] = 3
                    },
                    Modifiers:
                    [
                        new CharacterMechanicModifierInput("skill.hide", 2),
                        new CharacterMechanicModifierInput("skill.stealth", 1)
                    ]),
                userId: null);
            Assert.NotNull(evaluation);
            Assert.Equal(8, evaluation.Value);
        }
        finally
        {
            await using var cleanupScope = factory.Services.CreateAsyncScope();
            await CleanupAsync(
                cleanupScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>(),
                packageId);
        }
    }

    private static HttpRequestMessage HostedRequest(HttpMethod method, string path, string ticket)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(ToolHostAuthenticationHeaders.Ticket, ticket);
        request.Headers.Add(ToolHostAuthenticationHeaders.IntrospectionPath, IntrospectionPath);
        return request;
    }


    private static async Task CleanupAsync(RulesCoreDbContext db, Guid packageId)
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

        if (packageId != Guid.Empty)
        {
            var package = await db.SourcePackages.SingleOrDefaultAsync(value => value.Id == packageId);
            if (package is not null)
            {
                db.SourcePackages.Remove(package);
                await db.SaveChangesAsync();
            }
        }
    }

    private static ToolHostAuthenticationContext Context(
        string userId,
        Guid? campaignId,
        string? campaignRole) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: RulesAuthority.DorksAndDiceMode,
            User: new ToolHostUserContext(userId, userId),
            GlobalRoles: [],
            Campaigns: campaignId is not null && campaignRole is not null
                ? [new ToolHostCampaignContext(campaignId.Value, "Mechanics Campaign", campaignRole)]
                : []);

    private sealed class FakeToolHostAuthenticationClient(
        IReadOnlyDictionary<string, ToolHostAuthenticationContext> contexts)
        : IToolHostAuthenticationClient
    {
        public Task<ToolHostAuthenticationContext?> RedeemAsync(
            string ticket,
            string introspectionPath,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(introspectionPath, IntrospectionPath, StringComparison.Ordinal))
            {
                return Task.FromResult<ToolHostAuthenticationContext?>(null);
            }

            contexts.TryGetValue(ticket, out var context);
            return Task.FromResult(context);
        }
    }
}
