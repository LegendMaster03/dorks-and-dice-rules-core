using System.Net;
using System.Text;
using System.Text.Json;
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
using RulesCore.Infrastructure.Sources;

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

        using (var globalResponse = await client.GetAsync("/api/rules/mechanics"))
        {
            Assert.Equal(HttpStatusCode.OK, globalResponse.StatusCode);
            var catalog = await globalResponse.Content.ReadFromJsonAsync<CharacterMechanicsCatalogView>();
            Assert.NotNull(catalog);
            Assert.Contains(catalog.Mechanics, value => value.MechanicKey == "check.competency");
            Assert.Contains(catalog.Mechanics, value => value.MechanicKey == "save.fortitude");
            var harvesting = Assert.Single(
                catalog.Mechanics,
                value => value.MechanicKey == "check.harvesting.total");
            Assert.True(harvesting.IsAvailableUnderRuleset);
            Assert.Equal(
                CharacterMechanicApplicabilityKinds.ExternalPublicRules,
                harvesting.Applicability.Kind);
            var attribution = Assert.Single(harvesting.SourceAttributions);
            Assert.Null(attribution.PackageKey);
            Assert.Null(attribution.PackageDisplayName);
            Assert.Equal(KnownCharacterMechanics.LootTavernReferenceKey, attribution.WorkKey);
            Assert.Equal("Harvesting & Crafting Lite", attribution.WorkDisplayName);
            Assert.Equal("5e", attribution.GameEdition);
            Assert.Equal("public-release", attribution.ReleaseKind);
            Assert.Equal(new DateOnly(2024, 7, 3), attribution.PublicationDate);
            Assert.Equal(
                "https://www.patreon.com/LootTavern/posts/helianas-and-to-107406117",
                attribution.ReferenceUri);
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
                           ["competencyContribution"] = 2,
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

        using (var batchResponse = await client.PostAsJsonAsync(
                   "/api/rules/mechanics/evaluate",
                   new CharacterMechanicsBatchEvaluationRequest(
                   [
                       new CharacterMechanicBatchEvaluationItemRequest(
                           "check.competency",
                           new CharacterMechanicEvaluationRequest(
                               IntegerInputs: new Dictionary<string, int>
                               {
                                   ["d20Roll"] = 10,
                                   ["abilityModifier"] = 2,
                                   ["competencyContribution"] = 3
                               },
                               StringInputs: new Dictionary<string, string>
                               {
                                   ["abilityKey"] = "intelligence",
                                   ["competencyKey"] = "skill.arcana"
                               })),
                       new CharacterMechanicBatchEvaluationItemRequest(
                           "save.fortitude",
                           new CharacterMechanicEvaluationRequest(
                               IntegerInputs: new Dictionary<string, int>
                               {
                                   ["baseSave"] = 4,
                                   ["constitutionModifier"] = 2
                               },
                               CapabilityKeys: ["save.fortitude"]))
                   ])))
        {
            Assert.Equal(HttpStatusCode.OK, batchResponse.StatusCode);
            var batch = await batchResponse.Content
                .ReadFromJsonAsync<CharacterMechanicsBatchEvaluationView>();
            Assert.NotNull(batch);
            Assert.Equal("global", batch.Scope);
            Assert.Equal(2, batch.Evaluations.Count);
            Assert.Equal(15, batch.Evaluations[0].Evaluation!.Value);
            Assert.Equal(6, batch.Evaluations[1].Evaluation!.Value);
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

        using (var playerBatchRequest = HostedRequest(
                   HttpMethod.Post,
                   $"/api/campaigns/{campaignId}/rules/mechanics/evaluate",
                   "player-ticket"))
        {
            playerBatchRequest.Content = JsonContent.Create(
                new CharacterMechanicsBatchEvaluationRequest(
                [
                    new CharacterMechanicBatchEvaluationItemRequest(
                        "check.competency",
                        new CharacterMechanicEvaluationRequest(
                            IntegerInputs: new Dictionary<string, int>
                            {
                                ["d20Roll"] = 9,
                                ["abilityModifier"] = 2,
                                ["competencyContribution"] = 1
                            },
                            StringInputs: new Dictionary<string, string>
                            {
                                ["abilityKey"] = "wisdom",
                                ["competencyKey"] = "skill.survival"
                            }))
                ]));
            using var playerBatchResponse = await client.SendAsync(playerBatchRequest);
            Assert.Equal(HttpStatusCode.OK, playerBatchResponse.StatusCode);
            var batch = await playerBatchResponse.Content
                .ReadFromJsonAsync<CharacterMechanicsBatchEvaluationView>();
            Assert.NotNull(batch);
            Assert.Equal("campaign", batch.Scope);
            Assert.Equal(campaignId, batch.CampaignId);
            Assert.Equal(12, Assert.Single(batch.Evaluations).Evaluation!.Value);
        }
    }


    [Fact]
    public async Task PcGenCompetencyMetadataCompositeRulesAndCapabilityGatesFlowThroughConsumerContract()
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
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var mechanics = scope.ServiceProvider.GetRequiredService<ICharacterMechanicsConsumerService>();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

            var sourceShort = $"CM{token}";
            var fileName = $"data/35e/example/character_mechanics_skills_{token}.lst";
            var sourceText = string.Join('\n',
            [
                $"SOURCELONG:Character Mechanics 3.5e Work {token}\tSOURCESHORT:{sourceShort}",
                "Hide\tKEYSTAT:DEX\tUSEUNTRAINED:YES\tACHECK:YES",
                "Move Silently\tKEYSTAT:DEX\tUSEUNTRAINED:YES\tACHECK:YES",
                "Stealth\tKEYSTAT:DEX\tUSEUNTRAINED:YES\tACHECK:YES",
                "Knowledge (the planes)\tKEYSTAT:INT\tUSEUNTRAINED:NO\tACHECK:NO",
                "Craft (blacksmithing)\tKEYSTAT:INT\tUSEUNTRAINED:YES\tACHECK:NO",
                "Craft (alchemy)\tKEYSTAT:INT\tUSEUNTRAINED:YES\tACHECK:NO"
            ]);
            var representation = new PcGenSourceFormatAdapter().TryRead(
                new SourceRepresentationArtifact(
                    fileName,
                    Encoding.UTF8.GetBytes(sourceText),
                    $"integration:character-mechanics:{token}#{fileName}"))
                ?? throw new InvalidOperationException("PCGen character mechanics fixture was not readable.");

            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Character Mechanics 3.x {token}",
                    "integration-test",
                    "test-only",
                    true,
                    representation));
            packageId = imported.PackageId;

            foreach (var (name, conceptKey) in new[]
                     {
                         ("Hide", "skill.hide"),
                         ("Move Silently", "skill.move-silently"),
                         ("Stealth", "skill.stealth"),
                         ("Knowledge (the planes)", "skill.knowledge-the-planes"),
                         ("Craft (blacksmithing)", "skill.craft-blacksmithing"),
                         ("Alchemist's Supplies", "tool.alchemists-supplies")
                     })
            {
                var source = imported.Entities.Single(value => value.Name == name);
                var concept = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(conceptKey, source.EntityType, name),
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
            Assert.True(fortitude.IsAvailableUnderRuleset);
            Assert.Equal(
                CharacterMechanicApplicabilityKinds.CharacterCapability,
                fortitude.Applicability.Kind);
            Assert.Equal(
                new[] { "save.fortitude" },
                fortitude.Applicability.RequiredCapabilityKeys);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                mechanics.EvaluateGlobalAsync(
                    "save.fortitude",
                    new CharacterMechanicEvaluationRequest(
                        IntegerInputs: new Dictionary<string, int>
                        {
                            ["baseSave"] = 2,
                            ["constitutionModifier"] = 3
                        }),
                    userId: null));

            var fortitudeEvaluation = await mechanics.EvaluateGlobalAsync(
                "save.fortitude",
                new CharacterMechanicEvaluationRequest(
                    IntegerInputs: new Dictionary<string, int>
                    {
                        ["baseSave"] = 2,
                        ["constitutionModifier"] = 3
                    },
                    CapabilityKeys: ["save.fortitude"]),
                userId: null);
            Assert.NotNull(fortitudeEvaluation);
            Assert.Equal(5, fortitudeEvaluation.Value);

            var stealth = Assert.Single(
                catalog.Mechanics,
                value => value.MechanicKey == "competency.skill.stealth");
            Assert.Equal(CharacterMechanicEvaluationKinds.CompositeCompetency, stealth.EvaluationKind);
            Assert.NotNull(stealth.Competency);
            Assert.Equal(CharacterCompetencyKinds.Skill, stealth.Competency!.CompetencyKind);
            Assert.Equal("dexterity", stealth.Competency.GoverningAbilityKey);
            Assert.True(stealth.Competency.SupportsRanks);
            Assert.True(stealth.Competency.SupportsClassSkillState);
            Assert.True(stealth.Competency.SupportsTrainingState);
            Assert.False(stealth.Competency.TrainedOnly);
            Assert.True(stealth.Competency.ArmorCheckPenaltyApplies);
            Assert.Contains(stealth.Inputs, value => value.Key == "abilityContribution");
            Assert.Contains(stealth.Inputs, value => value.Key == "ranks");
            Assert.Contains(stealth.Inputs, value => value.Key == "classSkillState");
            Assert.Contains(stealth.Inputs, value => value.Key == "isTrained");
            Assert.Contains(stealth.Inputs, value => value.Key == "armorCheckPenaltyAdjustment");
            Assert.DoesNotContain(stealth.Inputs, value => value.Key == "value");

            var relationship = Assert.Single(
                stealth.Relationships,
                value => value.RelationshipKey == "skill-composite.stealth");
            Assert.True(relationship.CanResolve);
            Assert.Equal(
                MechanicalRelationshipResolutionKinds.DeriveParent,
                relationship.EffectiveResolutionKind);

            var specialized = Assert.Single(
                catalog.Mechanics,
                value => value.MechanicKey == "competency.skill.knowledge-the-planes");
            Assert.NotNull(specialized.Competency);
            Assert.Equal(
                CharacterCompetencyKinds.SpecializedSkill,
                specialized.Competency!.CompetencyKind);
            Assert.Equal("Knowledge", specialized.Competency.FamilyName);
            Assert.Equal("the planes", specialized.Competency.Specialty);
            Assert.Equal("intelligence", specialized.Competency.GoverningAbilityKey);
            Assert.True(specialized.Competency.TrainedOnly);
            Assert.False(specialized.Competency.ArmorCheckPenaltyApplies);

            var craft = Assert.Single(
                catalog.Mechanics,
                value => value.MechanicKey == "competency.skill.craft-blacksmithing");
            Assert.NotNull(craft.Competency);
            Assert.Equal(CharacterCompetencyKinds.SpecializedSkill, craft.Competency!.CompetencyKind);
            Assert.Equal("Craft", craft.Competency.FamilyName);
            Assert.Equal("blacksmithing", craft.Competency.Specialty);
            Assert.True(craft.Competency.SupportsRanks);

            var alchemyTools = Assert.Single(
                catalog.Mechanics,
                value => value.MechanicKey == "competency.tool.alchemists-supplies");
            Assert.NotNull(alchemyTools.Competency);
            Assert.Equal(CharacterCompetencyKinds.Tool, alchemyTools.Competency!.CompetencyKind);
            Assert.Equal("Craft", alchemyTools.Competency.FamilyName);
            Assert.Equal("alchemy", alchemyTools.Competency.Specialty);
            Assert.True(alchemyTools.Competency.SupportsRanks);
            Assert.True(alchemyTools.Competency.SupportsClassSkillState);

            var attribution = Assert.Single(
                stealth.SourceAttributions,
                value => value.GameEdition == "3.5e");
            Assert.Equal(1, attribution.SourceRevisionNumber);
            Assert.Equal("integration-test", attribution.Provider);
            Assert.NotNull(attribution.WorkKey);
            Assert.NotNull(attribution.WorkDisplayName);

            var hide = Assert.Single(
                catalog.Mechanics,
                value => value.MechanicKey == "competency.skill.hide");
            Assert.NotNull(hide.Competency);
            var hideProfile = Assert.Single(hide.Competency!.Profiles);
            Assert.Equal("ranked-skill", hideProfile.EvaluationProfileKey);
            Assert.True(hideProfile.CanEvaluate);
            Assert.Contains(hideProfile.Inputs, value => value.Key == "armorCheckPenaltyAdjustment");

            var hideEvaluation = await mechanics.EvaluateGlobalAsync(
                "competency.skill.hide",
                new CharacterMechanicEvaluationRequest(
                    IntegerInputs: new Dictionary<string, int>
                    {
                        ["abilityContribution"] = 4,
                        ["ranks"] = 6,
                        ["armorCheckPenaltyAdjustment"] = -2,
                        ["otherModifier"] = 1
                    },
                    CapabilityKeys: ["competency.skill-ranks"],
                    CompetencyProfileSourceEntityRevisionId: hideProfile.SourceEntityRevisionId),
                userId: null);
            Assert.NotNull(hideEvaluation);
            Assert.Equal(CharacterMechanicEvaluationKinds.CompetencyProfile, hideEvaluation.EvaluationKind);
            Assert.Equal(9, hideEvaluation.Value);
            Assert.Equal(
                hideProfile.SourceEntityRevisionId,
                hideEvaluation.CompetencyProfileSourceEntityRevisionId);

            var specializedProfile = Assert.Single(specialized.Competency.Profiles);
            Assert.True(specializedProfile.CanEvaluate);
            Assert.DoesNotContain(
                specializedProfile.Inputs,
                value => value.Key == "armorCheckPenaltyAdjustment");
            var untrainedSpecialized = await mechanics.EvaluateGlobalAsync(
                "competency.skill.knowledge-the-planes",
                new CharacterMechanicEvaluationRequest(
                    IntegerInputs: new Dictionary<string, int>
                    {
                        ["abilityContribution"] = 3,
                        ["ranks"] = 5,
                        ["armorCheckPenaltyAdjustment"] = -20,
                        ["otherModifier"] = 2
                    },
                    BooleanInputs: new Dictionary<string, bool>
                    {
                        ["isTrained"] = false
                    },
                    CapabilityKeys: ["competency.skill-ranks"],
                    CompetencyProfileSourceEntityRevisionId: specializedProfile.SourceEntityRevisionId),
                userId: null);
            Assert.NotNull(untrainedSpecialized);
            Assert.Equal(10, untrainedSpecialized.Value);
            Assert.False(untrainedSpecialized.RequirementsSatisfied);
            Assert.Equal(new[] { "isTrained" }, untrainedSpecialized.UnsatisfiedRequirementKeys);

            var trainedSpecialized = await mechanics.EvaluateGlobalAsync(
                "competency.skill.knowledge-the-planes",
                new CharacterMechanicEvaluationRequest(
                    IntegerInputs: new Dictionary<string, int>
                    {
                        ["abilityContribution"] = 3,
                        ["ranks"] = 5,
                        ["otherModifier"] = 2
                    },
                    BooleanInputs: new Dictionary<string, bool>
                    {
                        ["isTrained"] = true,
                        ["classSkillState"] = true
                    },
                    CapabilityKeys: ["competency.skill-ranks"],
                    CompetencyProfileSourceEntityRevisionId: specializedProfile.SourceEntityRevisionId),
                userId: null);
            Assert.NotNull(trainedSpecialized);
            Assert.Equal(10, trainedSpecialized.Value);
            Assert.True(trainedSpecialized.RequirementsSatisfied);

            var craftProfile = Assert.Single(craft.Competency.Profiles);
            var craftEvaluation = await mechanics.EvaluateGlobalAsync(
                "competency.skill.craft-blacksmithing",
                new CharacterMechanicEvaluationRequest(
                    IntegerInputs: new Dictionary<string, int>
                    {
                        ["abilityContribution"] = 2,
                        ["ranks"] = 4,
                        ["otherModifier"] = 1
                    },
                    CapabilityKeys: ["competency.skill-ranks"],
                    CompetencyProfileSourceEntityRevisionId: craftProfile.SourceEntityRevisionId),
                userId: null);
            Assert.NotNull(craftEvaluation);
            Assert.Equal(7, craftEvaluation.Value);

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

    [Fact]
    public async Task DirectEquivalentLaterSourceDoesNotEraseThreeXCompetencyProfile()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var actor = $"mechanics-direct-{token}";
        Guid fivePackageId = Guid.Empty;
        Guid threePackageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var importer = new NormalizedSourceImportService(db);
            var normalization = new RulesCore.Infrastructure.Rules.SourceNormalizationService(db);
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var mechanics = scope.ServiceProvider.GetRequiredService<ICharacterMechanicsConsumerService>();

            var fiveSource = $"D5{token}";
            var fiveRaw = JsonSerializer.Serialize(new
            {
                name = "Deception",
                source = fiveSource,
                entries = new[] { "Later-edition Deception fixture." }
            });
            var fiveImport = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                $"mechanics-deception-5e-{token}",
                $"Mechanics Deception 5e {token}",
                "integration-test",
                "test-only",
                true,
                new NormalizedSourceRepresentation(
                    FiveEToolsSourceFormatAdapter.Format,
                    new SourceRepresentationArtifact(
                        $"deception-{token}.json",
                        Encoding.UTF8.GetBytes(fiveRaw),
                        $"integration:mechanics-deception-5e:{token}"),
                    [new NormalizedSourceRecord(
                        "skill",
                        "Deception",
                        fiveSource,
                        $"skill|Deception|{fiveSource}",
                        fiveRaw,
                        PublicationLocalKey: fiveSource)],
                    [new NormalizedSourcePublication(
                        fiveSource,
                        $"Later Deception {token}",
                        "Integration Test Press",
                        "5e",
                        new DateOnly(2014, 8, 19))])));
            fivePackageId = fiveImport.PackageId;
            var fiveEntity = Assert.Single(fiveImport.Entities);
            var accepted = await normalization.AcceptAsync(fiveEntity.EntityId, actor);
            Assert.NotNull(accepted);
            Assert.Equal("skill.deception", accepted!.Concept.Key);

            var sourceShort = $"B35{token}";
            var pcgenFile = $"data/35e/example/direct_equivalence_skills_{token}.lst";
            var pcgenText = string.Join('\n',
            [
                $"SOURCELONG:Bluff 3.5e Fixture {token}\tSOURCESHORT:{sourceShort}",
                "Bluff\tKEYSTAT:CHA\tUSEUNTRAINED:YES\tACHECK:NO"
            ]);
            var pcgenRepresentation = new PcGenSourceFormatAdapter().TryRead(
                new SourceRepresentationArtifact(
                    pcgenFile,
                    Encoding.UTF8.GetBytes(pcgenText),
                    $"integration:mechanics-bluff-35:{token}#{pcgenFile}"))
                ?? throw new InvalidOperationException("PCGen Bluff fixture was not readable.");
            var threeImport = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                $"mechanics-bluff-35-{token}",
                $"Mechanics Bluff 3.5e {token}",
                "integration-test",
                "test-only",
                true,
                pcgenRepresentation));
            threePackageId = threeImport.PackageId;
            var bluff = Assert.Single(threeImport.Entities);
            Assert.Equal("skill", bluff.EntityType);
            Assert.Equal("Deception", bluff.Name);

            var fiveRevisionId = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == fiveEntity.EntityId)
                .Select(value => value.Id)
                .SingleAsync();
            await globalRules.SetDecisionAsync(
                accepted.Concept.Id,
                new SetGlobalRuleDecisionRequest(
                    fiveRevisionId,
                    "Select the later-edition presentation while retaining canonical competency profiles."),
                actor);
            await globalRules.PublishAsync(actor);

            var catalog = await mechanics.GetGlobalAsync(userId: null);
            var deception = Assert.Single(
                catalog.Mechanics,
                value => value.MechanicKey == "competency.skill.deception");
            Assert.NotNull(deception.Competency);
            Assert.False(deception.Competency!.SupportsRanks);
            Assert.False(deception.Competency.SupportsClassSkillState);
            Assert.DoesNotContain(deception.Inputs, value => value.Key == "ranks");
            Assert.Contains(deception.Inputs, value => value.Key == "trainingContribution");
            var laterProfile = Assert.Single(
                deception.Competency.Profiles,
                value => value.ProfileKey == "dnd-5x");
            Assert.True(laterProfile.CanEvaluate);
            Assert.False(laterProfile.SupportsRanks);
            Assert.DoesNotContain(laterProfile.Inputs, value => value.Key == "ranks");
            Assert.Equal(
                laterProfile.SourceEntityRevisionId,
                deception.Competency.DefaultProfileSourceEntityRevisionId);

            var threeProfile = Assert.Single(
                deception.Competency.Profiles,
                value => value.ProfileKey == "dnd-3x");
            Assert.Equal("3.5e", threeProfile.GameEdition);
            Assert.True(threeProfile.SupportsRanks);
            Assert.True(threeProfile.SupportsClassSkillState);
            Assert.Contains(
                "competency.skill-ranks",
                threeProfile.RequiredCapabilityKeys);

            var laterEvaluation = await mechanics.EvaluateGlobalAsync(
                "competency.skill.deception",
                new CharacterMechanicEvaluationRequest(
                    IntegerInputs: new Dictionary<string, int>
                    {
                        ["abilityContribution"] = 2,
                        ["trainingContribution"] = 3,
                        ["otherModifier"] = 1,
                        ["ranks"] = 99
                    }),
                userId: null);
            Assert.NotNull(laterEvaluation);
            Assert.Equal(6, laterEvaluation.Value);
            Assert.Equal(
                laterProfile.SourceEntityRevisionId,
                laterEvaluation.CompetencyProfileSourceEntityRevisionId);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                mechanics.EvaluateGlobalAsync(
                    "competency.skill.deception",
                    new CharacterMechanicEvaluationRequest(
                        IntegerInputs: new Dictionary<string, int>
                        {
                            ["abilityContribution"] = 4,
                            ["ranks"] = 5
                        },
                        CompetencyProfileSourceEntityRevisionId: threeProfile.SourceEntityRevisionId),
                    userId: null));

            var threeEvaluation = await mechanics.EvaluateGlobalAsync(
                "competency.skill.deception",
                new CharacterMechanicEvaluationRequest(
                    IntegerInputs: new Dictionary<string, int>
                    {
                        ["abilityContribution"] = 4,
                        ["ranks"] = 5,
                        ["otherModifier"] = 1
                    },
                    CapabilityKeys: ["competency.skill-ranks"],
                    CompetencyProfileSourceEntityRevisionId: threeProfile.SourceEntityRevisionId),
                userId: null);
            Assert.NotNull(threeEvaluation);
            Assert.Equal(10, threeEvaluation.Value);
            Assert.Equal(
                threeProfile.SourceEntityRevisionId,
                threeEvaluation.CompetencyProfileSourceEntityRevisionId);
        }
        finally
        {
            await using var cleanupScope = factory.Services.CreateAsyncScope();
            await CleanupAsync(
                cleanupScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>(),
                fivePackageId,
                threePackageId);
        }
    }

    private static HttpRequestMessage HostedRequest(HttpMethod method, string path, string ticket)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(ToolHostAuthenticationHeaders.Ticket, ticket);
        request.Headers.Add(ToolHostAuthenticationHeaders.IntrospectionPath, IntrospectionPath);
        return request;
    }


    private static async Task CleanupAsync(RulesCoreDbContext db, params Guid[] packageIds)
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

        var ids = packageIds.Where(value => value != Guid.Empty).Distinct().ToArray();
        if (ids.Length > 0)
        {
            var packages = await db.SourcePackages
                .Where(value => ids.Contains(value.Id))
                .ToArrayAsync();
            db.SourcePackages.RemoveRange(packages);
            await db.SaveChangesAsync();
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
