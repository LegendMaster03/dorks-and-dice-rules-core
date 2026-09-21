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
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
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

            var helpers = Assert.Single(harvesting.ContributorGroups);
            Assert.Equal("helpers", helpers.Key);
            Assert.Equal("creatureSize", helpers.MaximumCountStringInputKey);
            Assert.Equal(0, helpers.MaximumCountByStringValue["Tiny"]);
            Assert.Equal(1, helpers.MaximumCountByStringValue["Small"]);
            Assert.Equal(2, helpers.MaximumCountByStringValue["Medium"]);
            Assert.Equal(4, helpers.MaximumCountByStringValue["Large"]);
            Assert.Equal(6, helpers.MaximumCountByStringValue["Huge"]);
            Assert.Equal(10, helpers.MaximumCountByStringValue["Gargantuan"]);
            Assert.False(helpers.StandardHelpActionApplies);
            Assert.Contains(
                helpers.ContributorInputs,
                value => value.Key == "proficiencyBonus"
                    && value.ValueKind == CharacterMechanicInputValueKinds.Integer);
            Assert.Contains(
                helpers.ContributorInputs,
                value => value.Key == "isProficient"
                    && value.ValueKind == CharacterMechanicInputValueKinds.Boolean);
            Assert.Contains(
                helpers.ContributorInputs,
                value => value.Key == "participatedForEntireDuration"
                    && value.ValueKind == CharacterMechanicInputValueKinds.Boolean);
            Assert.Contains(
                helpers.BooleanRequirements,
                value => value.InputKey == "participatedForEntireDuration"
                    && value.ExpectedValue);
            Assert.Contains(
                helpers.BooleanRequirements,
                value => value.InputKey == "isAssessmentParticipant"
                    && !value.ExpectedValue);
            Assert.Contains(
                helpers.BooleanRequirements,
                value => value.InputKey == "isCarvingParticipant"
                    && !value.ExpectedValue);
        }

        using (var harvestingResponse = await client.PostAsJsonAsync(
                   "/api/rules/mechanics/check.harvesting.total/evaluate",
                   new CharacterMechanicEvaluationRequest(
                       IntegerInputs: new Dictionary<string, int>
                       {
                           ["assessmentResult"] = 14,
                           ["carvingResult"] = 12
                       },
                       BooleanInputs: new Dictionary<string, bool>
                       {
                           ["sameActor"] = false,
                           ["helpAction"] = true
                       },
                       StringInputs: new Dictionary<string, string>
                       {
                           ["creatureSize"] = "Medium"
                       },
                       ContributorGroups:
                       [
                           new CharacterMechanicContributorGroupInput(
                               "helpers",
                               [
                                   new CharacterMechanicContributorInput(
                                       IntegerInputs: new Dictionary<string, int>
                                       {
                                           ["proficiencyBonus"] = 3
                                       },
                                       BooleanInputs: new Dictionary<string, bool>
                                       {
                                           ["isProficient"] = true,
                                           ["participatedForEntireDuration"] = true,
                                           ["isAssessmentParticipant"] = false,
                                           ["isCarvingParticipant"] = false
                                       }),
                                   new CharacterMechanicContributorInput(
                                       IntegerInputs: new Dictionary<string, int>
                                       {
                                           ["proficiencyBonus"] = 5
                                       },
                                       BooleanInputs: new Dictionary<string, bool>
                                       {
                                           ["isProficient"] = false,
                                           ["participatedForEntireDuration"] = true,
                                           ["isAssessmentParticipant"] = false,
                                           ["isCarvingParticipant"] = false
                                       })
                               ])
                       ])))
        {
            Assert.Equal(HttpStatusCode.OK, harvestingResponse.StatusCode);
            var evaluation = await harvestingResponse.Content
                .ReadFromJsonAsync<CharacterMechanicEvaluationView>();
            Assert.NotNull(evaluation);
            Assert.Equal(31, evaluation.Value);
            var helpers = Assert.Single(evaluation.ContributorGroups);
            Assert.Equal(2, helpers.ContributorCount);
            Assert.Equal(2, helpers.MaximumContributorCount);
            Assert.Equal(5, helpers.Value);
            Assert.Empty(evaluation.AppliedRollRules);
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
        var relationshipActor = $"mechanics-relationship-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var mechanics = scope.ServiceProvider.GetRequiredService<ICharacterMechanicsConsumerService>();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var mechanicalRelationships = new MechanicalRelationshipService(db);

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
                         ("The planes", "skill.the-planes"),
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
            Assert.Equal(
                new[] { "skill.hide", "skill.move-silently" },
                stealth.Inputs.Select(value => value.Key).ToArray());
            Assert.All(
                stealth.Inputs,
                value => Assert.Equal(
                    CharacterMechanicInputOrigins.Derived,
                    value.Origin));

            var relationship = Assert.Single(
                stealth.Relationships,
                value => value.RelationshipKey == "skill-composite.stealth");
            Assert.True(relationship.CanResolve);
            Assert.Equal(
                MechanicalRelationshipResolutionKinds.DeriveParent,
                relationship.EffectiveResolutionKind);

            var hide = Assert.Single(
                catalog.Mechanics,
                value => value.MechanicKey == "competency.skill.hide");
            Assert.NotNull(hide.Competency);
            var hideProfile = Assert.Single(hide.Competency!.Profiles);

            var alternateAbilityCheck = await mechanics.EvaluateGlobalAsync(
                "check.competency",
                new CharacterMechanicEvaluationRequest(
                    IntegerInputs: new Dictionary<string, int>
                    {
                        ["d20Roll"] = 10,
                        ["abilityModifier"] = 2
                    },
                    StringInputs: new Dictionary<string, string>
                    {
                        ["abilityKey"] = "wisdom"
                    },
                    Competency: new CharacterMechanicCompetencyInput(
                        "competency.skill.hide",
                        IntegerInputs: new Dictionary<string, int>
                        {
                            ["ranks"] = 6,
                            ["armorCheckPenaltyAdjustment"] = -2,
                            ["otherModifier"] = 1
                        },
                        CapabilityKeys: ["competency.skill-ranks"],
                        CompetencyProfileSourceEntityRevisionId: hideProfile.SourceEntityRevisionId)),
                userId: null);
            Assert.NotNull(alternateAbilityCheck);
            Assert.Equal(17, alternateAbilityCheck.Value);

            var assessment = await mechanics.EvaluateGlobalAsync(
                "check.harvesting.assessment",
                new CharacterMechanicEvaluationRequest(
                    IntegerInputs: new Dictionary<string, int>
                    {
                        ["d20Roll"] = 10,
                        ["intelligenceModifier"] = 3
                    },
                    Competency: new CharacterMechanicCompetencyInput(
                        "competency.skill.hide",
                        IntegerInputs: new Dictionary<string, int>
                        {
                            ["ranks"] = 6,
                            ["armorCheckPenaltyAdjustment"] = -2,
                            ["otherModifier"] = 1
                        },
                        CapabilityKeys: ["competency.skill-ranks"],
                        CompetencyProfileSourceEntityRevisionId: hideProfile.SourceEntityRevisionId)),
                userId: null);
            Assert.NotNull(assessment);
            Assert.Equal(18, assessment.Value);

            var carving = await mechanics.EvaluateGlobalAsync(
                "check.harvesting.carving",
                new CharacterMechanicEvaluationRequest(
                    IntegerInputs: new Dictionary<string, int>
                    {
                        ["d20Roll"] = 10,
                        ["dexterityModifier"] = 4
                    },
                    Competency: new CharacterMechanicCompetencyInput(
                        "competency.skill.hide",
                        IntegerInputs: new Dictionary<string, int>
                        {
                            ["ranks"] = 6,
                            ["armorCheckPenaltyAdjustment"] = -2,
                            ["otherModifier"] = 1
                        },
                        CapabilityKeys: ["competency.skill-ranks"],
                        CompetencyProfileSourceEntityRevisionId: hideProfile.SourceEntityRevisionId)),
                userId: null);
            Assert.NotNull(carving);
            Assert.Equal(19, carving.Value);

            var specialized = Assert.Single(
                catalog.Mechanics,
                value => value.MechanicKey == "competency.skill.the-planes");
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

            Assert.Equal("ranked-skill", hideProfile.EvaluationProfileKey);
            Assert.True(hideProfile.CanEvaluate);
            Assert.Contains(hideProfile.Inputs, value => value.Key == "armorCheckPenaltyAdjustment");
            Assert.Contains(
                hideProfile.Inputs,
                value => value.Key == "abilityContribution"
                    && value.ContributionRole == "ability");
            Assert.Contains(
                hideProfile.Inputs,
                value => value.Key == "ranks"
                    && value.ContributionRole == "competency");

            var moveSilently = Assert.Single(
                catalog.Mechanics,
                value => value.MechanicKey == "competency.skill.move-silently");
            Assert.NotNull(moveSilently.Competency);
            var moveSilentlyProfile = Assert.Single(moveSilently.Competency!.Profiles);
            Assert.Equal("ranked-skill", moveSilentlyProfile.EvaluationProfileKey);
            Assert.True(moveSilentlyProfile.CanEvaluate);
            Assert.Contains(
                moveSilentlyProfile.Inputs,
                value => value.Key == "armorCheckPenaltyAdjustment");

            var compositeCheck = await mechanics.EvaluateGlobalAsync(
                "check.competency",
                new CharacterMechanicEvaluationRequest(
                    IntegerInputs: new Dictionary<string, int>
                    {
                        ["d20Roll"] = 10,
                        ["abilityModifier"] = 2
                    },
                    StringInputs: new Dictionary<string, string>
                    {
                        ["abilityKey"] = "wisdom"
                    },
                    Competency: new CharacterMechanicCompetencyInput(
                        "competency.skill.stealth",
                        Components:
                        [
                            new CharacterMechanicCompetencyInput(
                                "competency.skill.hide",
                                IntegerInputs: new Dictionary<string, int>
                                {
                                    ["ranks"] = 6,
                                    ["armorCheckPenaltyAdjustment"] = -2,
                                    ["otherModifier"] = 1
                                },
                                CapabilityKeys: ["competency.skill-ranks"],
                                CompetencyProfileSourceEntityRevisionId: hideProfile.SourceEntityRevisionId),
                            new CharacterMechanicCompetencyInput(
                                "competency.skill.move-silently",
                                IntegerInputs: new Dictionary<string, int>
                                {
                                    ["ranks"] = 4,
                                    ["armorCheckPenaltyAdjustment"] = -1
                                },
                                CapabilityKeys: ["competency.skill-ranks"],
                                CompetencyProfileSourceEntityRevisionId: moveSilentlyProfile.SourceEntityRevisionId)
                        ],
                        Modifiers:
                        [
                            new CharacterMechanicModifierInput("skill.hide", 2),
                            new CharacterMechanicModifierInput("skill.stealth", 1)
                        ])),
                userId: null);
            Assert.NotNull(compositeCheck);
            Assert.Equal(18, compositeCheck.Value);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                mechanics.EvaluateGlobalAsync(
                    "check.competency",
                    new CharacterMechanicEvaluationRequest(
                        IntegerInputs: new Dictionary<string, int>
                        {
                            ["d20Roll"] = 10,
                            ["abilityModifier"] = 2
                        },
                        StringInputs: new Dictionary<string, string>
                        {
                            ["abilityKey"] = "wisdom"
                        },
                        Competency: new CharacterMechanicCompetencyInput(
                            "competency.skill.stealth",
                            Components:
                            [
                                new CharacterMechanicCompetencyInput(
                                    "competency.skill.hide",
                                    IntegerInputs: new Dictionary<string, int>
                                    {
                                        ["ranks"] = 6
                                    },
                                    CapabilityKeys: ["competency.skill-ranks"],
                                    CompetencyProfileSourceEntityRevisionId: hideProfile.SourceEntityRevisionId)
                            ])),
                    userId: null));

            await Assert.ThrowsAsync<ArgumentException>(() =>
                mechanics.EvaluateGlobalAsync(
                    "check.competency",
                    new CharacterMechanicEvaluationRequest(
                        IntegerInputs: new Dictionary<string, int>
                        {
                            ["d20Roll"] = 10,
                            ["abilityModifier"] = 2
                        },
                        StringInputs: new Dictionary<string, string>
                        {
                            ["abilityKey"] = "wisdom"
                        },
                        Competency: new CharacterMechanicCompetencyInput(
                            "competency.skill.stealth",
                            Components:
                            [
                                new CharacterMechanicCompetencyInput(
                                    "competency.skill.hide",
                                    IntegerInputs: new Dictionary<string, int>
                                    {
                                        ["ranks"] = 6
                                    },
                                    CapabilityKeys: ["competency.skill-ranks"],
                                    CompetencyProfileSourceEntityRevisionId: hideProfile.SourceEntityRevisionId),
                                new CharacterMechanicCompetencyInput(
                                    "competency.skill.move-silently",
                                    IntegerInputs: new Dictionary<string, int>
                                    {
                                        ["ranks"] = 4
                                    },
                                    CapabilityKeys: ["competency.skill-ranks"],
                                    CompetencyProfileSourceEntityRevisionId: moveSilentlyProfile.SourceEntityRevisionId),
                                new CharacterMechanicCompetencyInput(
                                    "competency.skill.unknown")
                            ])),
                    userId: null));

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
            Assert.NotNull(hideEvaluation.CompetencyBreakdown);
            Assert.Equal(4, hideEvaluation.CompetencyBreakdown!.AbilityContribution);
            Assert.Equal(5, hideEvaluation.CompetencyBreakdown.CompetencyContribution);
            Assert.Equal(
                hideProfile.SourceEntityRevisionId,
                hideEvaluation.CompetencyProfileSourceEntityRevisionId);

            var specializedProfile = Assert.Single(specialized.Competency.Profiles);
            Assert.True(specializedProfile.CanEvaluate);
            Assert.DoesNotContain(
                specializedProfile.Inputs,
                value => value.Key == "armorCheckPenaltyAdjustment");
            var untrainedSpecialized = await mechanics.EvaluateGlobalAsync(
                "competency.skill.the-planes",
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
                "competency.skill.the-planes",
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

            var overridden = await mechanicalRelationships.SetRulingAsync(
                "skill-composite.stealth",
                new SetMechanicalRelationshipRulingRequest(
                    MechanicalRelationshipResolutionKinds.IndependentParent,
                    "Character mechanics consumer independent-parent fixture."),
                relationshipActor);
            Assert.Equal(
                MechanicalRelationshipResolutionKinds.IndependentParent,
                overridden.EffectiveResolutionKind);

            var independentCatalog = await mechanics.GetGlobalAsync(userId: null);
            var independentStealth = Assert.Single(
                independentCatalog.Mechanics,
                value => value.MechanicKey == "competency.skill.stealth");
            Assert.Equal(
                CharacterMechanicEvaluationKinds.CompetencyProfile,
                independentStealth.EvaluationKind);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                mechanics.EvaluateGlobalAsync(
                    "check.competency",
                    new CharacterMechanicEvaluationRequest(
                        IntegerInputs: new Dictionary<string, int>
                        {
                            ["d20Roll"] = 10,
                            ["abilityModifier"] = 2
                        },
                        StringInputs: new Dictionary<string, string>
                        {
                            ["abilityKey"] = "wisdom"
                        },
                        Competency: new CharacterMechanicCompetencyInput(
                            "competency.skill.stealth",
                            Components:
                            [
                                new CharacterMechanicCompetencyInput(
                                    "competency.skill.hide",
                                    IntegerInputs: new Dictionary<string, int>
                                    {
                                        ["ranks"] = 6
                                    },
                                    CapabilityKeys: ["competency.skill-ranks"],
                                    CompetencyProfileSourceEntityRevisionId: hideProfile.SourceEntityRevisionId),
                                new CharacterMechanicCompetencyInput(
                                    "competency.skill.move-silently",
                                    IntegerInputs: new Dictionary<string, int>
                                    {
                                        ["ranks"] = 4
                                    },
                                    CapabilityKeys: ["competency.skill-ranks"],
                                    CompetencyProfileSourceEntityRevisionId: moveSilentlyProfile.SourceEntityRevisionId)
                            ])),
                    userId: null));

            var independentProfile = Assert.Single(independentStealth.Competency!.Profiles);
            var independentCheck = await mechanics.EvaluateGlobalAsync(
                "check.competency",
                new CharacterMechanicEvaluationRequest(
                    IntegerInputs: new Dictionary<string, int>
                    {
                        ["d20Roll"] = 10,
                        ["abilityModifier"] = 2
                    },
                    StringInputs: new Dictionary<string, string>
                    {
                        ["abilityKey"] = "wisdom"
                    },
                    Competency: new CharacterMechanicCompetencyInput(
                        "competency.skill.stealth",
                        IntegerInputs: new Dictionary<string, int>
                        {
                            ["ranks"] = 8,
                            ["armorCheckPenaltyAdjustment"] = -2
                        },
                        CapabilityKeys: ["competency.skill-ranks"],
                        CompetencyProfileSourceEntityRevisionId: independentProfile.SourceEntityRevisionId)),
                userId: null);
            Assert.NotNull(independentCheck);
            Assert.Equal(18, independentCheck.Value);
        }
        finally
        {
            await using var cleanupScope = factory.Services.CreateAsyncScope();
            var cleanupDb = cleanupScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            await DeleteRelationshipRulingsAsync(cleanupDb, relationshipActor);
            await CleanupAsync(cleanupDb, packageId);
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
            var threeRevisionId = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == bluff.EntityId)
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
            Assert.NotNull(deception.Provenance);
            Assert.Contains(
                deception.SourceAttributions,
                value => value.PackageKey == $"mechanics-deception-5e-{token}");
            Assert.Contains(
                deception.SourceAttributions,
                value => value.PackageKey == $"mechanics-bluff-35-{token}");
            Assert.Contains(
                deception.Provenance!.CanonicalConcept,
                value => value.PackageKey == $"mechanics-deception-5e-{token}");
            Assert.Contains(
                deception.Provenance.CanonicalConcept,
                value => value.PackageKey == $"mechanics-bluff-35-{token}");
            Assert.All(
                deception.Provenance.MechanicalProfile,
                value => Assert.Equal($"mechanics-deception-5e-{token}", value.PackageKey));
            Assert.All(
                deception.Provenance.EffectiveRule,
                value => Assert.Equal($"mechanics-deception-5e-{token}", value.PackageKey));
            Assert.False(deception.Competency!.SupportsRanks);
            Assert.False(deception.Competency.SupportsClassSkillState);
            Assert.DoesNotContain(deception.Inputs, value => value.Key == "ranks");
            Assert.Contains(deception.Inputs, value => value.Key == "trainingContribution");
            var laterProfile = Assert.Single(
                deception.Competency.Profiles,
                value => value.SourceEntityRevisionId == fiveRevisionId);
            Assert.Equal("dnd-5x", laterProfile.ProfileKey);
            Assert.True(laterProfile.CanEvaluate);
            Assert.False(laterProfile.SupportsRanks);
            Assert.DoesNotContain(laterProfile.Inputs, value => value.Key == "ranks");
            Assert.Equal(
                fiveRevisionId,
                deception.Competency.DefaultProfileSourceEntityRevisionId);

            var threeProfile = Assert.Single(
                deception.Competency.Profiles,
                value => value.SourceEntityRevisionId == threeRevisionId);
            Assert.Equal("dnd-3x", threeProfile.ProfileKey);
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
            Assert.NotNull(laterEvaluation.CompetencyBreakdown);
            Assert.Equal(2, laterEvaluation.CompetencyBreakdown!.AbilityContribution);
            Assert.Equal(4, laterEvaluation.CompetencyBreakdown.CompetencyContribution);
            Assert.Equal(
                laterProfile.SourceEntityRevisionId,
                laterEvaluation.CompetencyProfileSourceEntityRevisionId);

            var laterCheck = await mechanics.EvaluateGlobalAsync(
                "check.competency",
                new CharacterMechanicEvaluationRequest(
                    IntegerInputs: new Dictionary<string, int>
                    {
                        ["d20Roll"] = 10,
                        ["abilityModifier"] = 4
                    },
                    StringInputs: new Dictionary<string, string>
                    {
                        ["abilityKey"] = "intelligence"
                    },
                    Competency: new CharacterMechanicCompetencyInput(
                        "competency.skill.deception",
                        IntegerInputs: new Dictionary<string, int>
                        {
                            ["trainingContribution"] = 3,
                            ["otherModifier"] = 1,
                            ["ranks"] = 99
                        },
                        CompetencyProfileSourceEntityRevisionId: laterProfile.SourceEntityRevisionId)),
                userId: null);
            Assert.NotNull(laterCheck);
            Assert.Equal(18, laterCheck.Value);

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
            Assert.NotNull(threeEvaluation.CompetencyBreakdown);
            Assert.Equal(4, threeEvaluation.CompetencyBreakdown!.AbilityContribution);
            Assert.Equal(6, threeEvaluation.CompetencyBreakdown.CompetencyContribution);
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

    [Fact]
    public async Task CharacterProjectionResolvesThreeXClassBabSavesGrappleAndAdvancementFeatures()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-projection-class-{token}";
        var conceptKey = $"class.example-martial-{token}";
        var actor = $"character-projection-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            var campaign = new SourceRepresentationArtifact(
                "example.pcc",
                Encoding.UTF8.GetBytes("""
                    CAMPAIGN:Character Projection Class
                    GAMEMODE:35e
                    SOURCELONG:Character Projection Class
                    SOURCESHORT:CPC
                    CLASS:example_classes.lst
                    """),
                $"integration:character-projection:{token}#data/35e/example/example.pcc");
            var classes = new SourceRepresentationArtifact(
                "example_classes.lst",
                Encoding.UTF8.GetBytes(string.Join('\n',
                [
                    "CLASS:Example Martial\tHD:8\tTYPE:Base.PC\tBONUS:COMBAT|BASEAB|classlevel(\"APPLIEDAS=NONEPIC\")*3/4\tBONUS:SAVE|BASE.Fortitude,BASE.Will|classlevel(\"APPLIEDAS=NONEPIC\")/3\tBONUS:SAVE|BASE.Reflex|classlevel(\"APPLIEDAS=NONEPIC\")/2+2",
                    "CLASS:Example Martial\tSTARTSKILLPTS:4\tCSKILL:Climb|Jump|TYPE.Craft",
                    "1\tABILITY:Special Ability|AUTOMATIC|Opening Feature",
                    "2\tSAB:Second Feature"
                ])),
                $"integration:character-projection:{token}#data/35e/example/example_classes.lst");
            var representation = new PcGenSourceFormatAdapter()
                .TryReadMany([campaign, classes])
                .Single(value => value.Artifact.FileName == "example_classes.lst");
            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Character Projection Class {token}",
                    "integration-test",
                    "test-only",
                    true,
                    representation));
            packageId = imported.PackageId;
            var source = Assert.Single(imported.Entities);

            var concept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(conceptKey, source.EntityType, source.Name),
                actor);
            await globalRules.BindSourceEntityAsync(
                concept.Value.Id,
                new BindRuleConceptSourceRequest(source.EntityId),
                actor);
            var revisionId = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == source.EntityId)
                .Select(value => value.Id)
                .SingleAsync();
            await globalRules.SetDecisionAsync(
                concept.Value.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionId,
                    "Character projection 3.x class fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            var result = await projection.ResolveGlobalAsync(
                new CharacterRulesProjectionRequest(
                    BaseAbilityScores: new Dictionary<string, int>
                    {
                        ["strength"] = 14,
                        ["dexterity"] = 16,
                        ["constitution"] = 14,
                        ["intelligence"] = 10,
                        ["wisdom"] = 12,
                        ["charisma"] = 8
                    },
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(conceptKey, 5)
                    ],
                    IntegerFacts: new Dictionary<string, int>
                    {
                        ["combat.grapple.size-modifier"] = 0
                    }),
                userId: null);

            Assert.Equal(3, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "combat.base-attack-bonus").NumericValue);
            Assert.Equal(3, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "save.fortitude").NumericValue);
            Assert.Equal(7, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "save.reflex").NumericValue);
            Assert.Equal(2, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "save.will").NumericValue);
            Assert.Equal(5, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "combat.grapple").NumericValue);
            Assert.Equal(4, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == $"advancement.{conceptKey}.skill-points-per-level").NumericValue);

            var hitDice = Assert.Single(
                result.Resources,
                value => value.ResourceKey == $"resource.hit-die.{conceptKey}");
            Assert.Equal(5, hitDice.MaximumValue);
            Assert.Equal(CharacterResolutionStates.RollRequired, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "health.maximum-hp").State);
            Assert.Contains(
                result.Features,
                value => value.DisplayName == "Opening Feature"
                    && value.State == CharacterResolutionStates.Resolved);
            Assert.Contains(
                result.Features,
                value => value.DisplayName == "Second Feature"
                    && value.State == CharacterResolutionStates.Resolved);
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
    public async Task CharacterProjectionEvaluatesNormalizedFeatPrerequisitesWithoutRejectingSelectedConcept()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-projection-prerequisite-{token}";
        var conceptKey = $"feat.mighty-training-{token}";
        var actor = $"character-prerequisite-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            var fileName = $"data/35e/example/prerequisite_feats_{token}.lst";
            var sourceText = string.Join('\n',
            [
                $"SOURCELONG:Prerequisite Fixture {token}\tSOURCESHORT:PF{token}",
                "Mighty Training\tCATEGORY:FEAT\tPREMULT:1,[PRESTAT:1,STR=13]\tDESC:Requires exceptional strength."
            ]);
            var representation = new PcGenSourceFormatAdapter().TryRead(
                new SourceRepresentationArtifact(
                    fileName,
                    Encoding.UTF8.GetBytes(sourceText),
                    $"integration:character-prerequisite:{token}#{fileName}"))
                ?? throw new InvalidOperationException("PCGen prerequisite fixture was not readable.");

            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Character Prerequisite {token}",
                    "integration-test",
                    "test-only",
                    true,
                    representation));
            packageId = imported.PackageId;
            var source = Assert.Single(imported.Entities);
            Assert.Equal("feat", source.EntityType);

            var concept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(conceptKey, source.EntityType, source.Name),
                actor);
            await globalRules.BindSourceEntityAsync(
                concept.Value.Id,
                new BindRuleConceptSourceRequest(source.EntityId),
                actor);
            var revisionId = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == source.EntityId)
                .Select(value => value.Id)
                .SingleAsync();
            await globalRules.SetDecisionAsync(
                concept.Value.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionId,
                    "Character prerequisite fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            static CharacterRulesProjectionRequest Request(string key, int strength) =>
                new(
                    BaseAbilityScores: new Dictionary<string, int>
                    {
                        ["strength"] = strength,
                        ["dexterity"] = 10,
                        ["constitution"] = 10,
                        ["intelligence"] = 10,
                        ["wisdom"] = 10,
                        ["charisma"] = 10
                    },
                    SelectedConcepts: [new CharacterSelectedConceptInput(key)]);

            var eligible = await projection.ResolveGlobalAsync(
                Request(conceptKey, 14),
                userId: null);
            var eligiblePrerequisite = Assert.Single(
                eligible.Prerequisites,
                value => value.ConceptKey == conceptKey);
            Assert.True(eligiblePrerequisite.Satisfied);
            Assert.Equal(CharacterResolutionStates.Resolved, eligiblePrerequisite.State);
            Assert.True(Assert.Single(eligiblePrerequisite.Requirements).Satisfied);
            Assert.DoesNotContain(
                eligible.Conflicts,
                value => value.ConflictKey == $"conflict.prerequisite.{conceptKey}");

            var ineligible = await projection.ResolveGlobalAsync(
                Request(conceptKey, 12),
                userId: null);
            var ineligiblePrerequisite = Assert.Single(
                ineligible.Prerequisites,
                value => value.ConceptKey == conceptKey);
            Assert.False(ineligiblePrerequisite.Satisfied);
            Assert.Equal(CharacterResolutionStates.Resolved, ineligiblePrerequisite.State);
            Assert.False(Assert.Single(ineligiblePrerequisite.Requirements).Satisfied);
            Assert.Contains(
                ineligible.Conflicts,
                value => value.ConflictKey == $"conflict.prerequisite.{conceptKey}");
            Assert.Contains(
                ineligible.Features,
                value => value.FeatureKey == $"feature.{conceptKey}");
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
    public async Task CharacterProjectionResolvesRepresentativeFiveXClassMechanics()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-projection-5x-{token}";
        var conceptKey = $"class.example-mage-{token}";
        var actor = $"character-5x-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            var sourceCode = $"FIVE{token}";
            var raw = JsonSerializer.Serialize(new
            {
                name = "Example Mage",
                source = sourceCode,
                hd = new { number = 1, faces = 6 },
                proficiency = new[] { "int", "wis" },
                spellcastingAbility = "int",
                classFeatures = new object[]
                {
                    new { name = "Arcane Study", entries = new[] { "Representative feature." } }
                }
            });
            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Character 5.x Fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    new NormalizedSourceRepresentation(
                        FiveEToolsSourceFormatAdapter.Format,
                        new SourceRepresentationArtifact(
                            $"class-{token}.json",
                            Encoding.UTF8.GetBytes(raw),
                            $"integration:character-5x:{token}"),
                        [
                            new NormalizedSourceRecord(
                                "class",
                                "Example Mage",
                                sourceCode,
                                $"class|Example Mage|{sourceCode}",
                                raw,
                                PublicationLocalKey: sourceCode)
                        ],
                        [
                            new NormalizedSourcePublication(
                                sourceCode,
                                $"Character 5.x Fixture {token}",
                                "Integration Test Press",
                                "5e",
                                new DateOnly(2014, 8, 19))
                        ])));
            packageId = imported.PackageId;
            var source = Assert.Single(imported.Entities);

            var concept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(conceptKey, source.EntityType, source.Name),
                actor);
            await globalRules.BindSourceEntityAsync(
                concept.Value.Id,
                new BindRuleConceptSourceRequest(source.EntityId),
                actor);
            var revisionId = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == source.EntityId)
                .Select(value => value.Id)
                .SingleAsync();
            await globalRules.SetDecisionAsync(
                concept.Value.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionId,
                    "Representative 5.x Character projection fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            var result = await projection.ResolveGlobalAsync(
                new CharacterRulesProjectionRequest(
                    BaseAbilityScores: new Dictionary<string, int>
                    {
                        ["strength"] = 10,
                        ["dexterity"] = 14,
                        ["constitution"] = 12,
                        ["intelligence"] = 18,
                        ["wisdom"] = 12,
                        ["charisma"] = 8
                    },
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(conceptKey, 5)
                    ]),
                userId: null);

            Assert.Equal(3, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "proficiency.standard").NumericValue);
            Assert.Equal(2, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "combat.initiative").NumericValue);
            Assert.Equal(7, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "save.intelligence").NumericValue);
            Assert.Equal(4, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "save.wisdom").NumericValue);
            Assert.Equal(2, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "save.dexterity").NumericValue);
            Assert.Equal(15, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == $"spellcasting.{conceptKey}.save-dc").NumericValue);
            Assert.Equal(7, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == $"spellcasting.{conceptKey}.attack").NumericValue);

            var hitDice = Assert.Single(
                result.Resources,
                value => value.ResourceKey == $"resource.hit-die.{conceptKey}");
            Assert.Equal(5, hitDice.MaximumValue);
            Assert.Equal(CharacterResolutionStates.RollRequired, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "health.maximum-hp").State);
            Assert.Contains(
                result.Capabilities,
                value => value.CapabilityKey == "spellcasting");
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
    public async Task CharacterProjectionUsesEffectiveCampaignOverrideDocument()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-projection-campaign-{token}";
        var conceptKey = $"race.example-speed-{token}";
        var actor = $"character-campaign-{token}";
        var campaignId = Guid.NewGuid();
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var campaignRules = scope.ServiceProvider.GetRequiredService<ICampaignRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            var sourceCode = $"OVR{token}";
            var raw = JsonSerializer.Serialize(new
            {
                name = "Example Species",
                source = sourceCode,
                size = new[] { "M" },
                speed = 30
            });
            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Campaign Character Fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    new NormalizedSourceRepresentation(
                        FiveEToolsSourceFormatAdapter.Format,
                        new SourceRepresentationArtifact(
                            $"race-{token}.json",
                            Encoding.UTF8.GetBytes(raw),
                            $"integration:character-campaign:{token}"),
                        [
                            new NormalizedSourceRecord(
                                "race",
                                "Example Species",
                                sourceCode,
                                $"race|Example Species|{sourceCode}",
                                raw,
                                PublicationLocalKey: sourceCode)
                        ],
                        [
                            new NormalizedSourcePublication(
                                sourceCode,
                                $"Campaign Character Fixture {token}",
                                "Integration Test Press",
                                "5e",
                                new DateOnly(2014, 8, 19))
                        ])));
            packageId = imported.PackageId;
            var source = Assert.Single(imported.Entities);

            var concept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(conceptKey, source.EntityType, source.Name),
                actor);
            await globalRules.BindSourceEntityAsync(
                concept.Value.Id,
                new BindRuleConceptSourceRequest(source.EntityId),
                actor);
            var revisionId = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == source.EntityId)
                .Select(value => value.Id)
                .SingleAsync();
            await globalRules.SetDecisionAsync(
                concept.Value.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionId,
                    "Global movement fixture."),
                actor);
            var globalPublication = await globalRules.PublishAsync(actor);

            await campaignRules.SelectBaselineAsync(
                campaignId,
                new SelectCampaignRulesetBaselineRequest(globalPublication.Id),
                actor);
            using var patchDocument = JsonDocument.Parse("""{"speed":40}""");
            await campaignRules.SetDecisionAsync(
                campaignId,
                concept.Value.Id,
                new SetCampaignRuleDecisionRequest(
                    CampaignRuleDecisionKinds.JsonMergePatch,
                    SourceEntityRevisionId: null,
                    Note: "Campaign movement override.",
                    MergePatch: patchDocument.RootElement.Clone()),
                actor);
            await campaignRules.PublishAsync(campaignId, actor);

            var request = new CharacterRulesProjectionRequest(
                BaseAbilityScores: new Dictionary<string, int>
                {
                    ["strength"] = 10,
                    ["dexterity"] = 10,
                    ["constitution"] = 10,
                    ["intelligence"] = 10,
                    ["wisdom"] = 10,
                    ["charisma"] = 10
                },
                SelectedConcepts: [new CharacterSelectedConceptInput(conceptKey)]);

            var global = await projection.ResolveGlobalAsync(request, userId: null);
            var globalWalk = Assert.Single(
                global.Movement,
                value => value.MovementKey == "movement.walk");
            Assert.Equal(30, globalWalk.Value);

            var campaign = await projection.ResolveCampaignAsync(
                campaignId,
                request,
                userId: "campaign-reader");
            var campaignWalk = Assert.Single(
                campaign.Movement,
                value => value.MovementKey == "movement.walk");
            Assert.Equal(40, campaignWalk.Value);
            Assert.Equal("campaign", campaign.Scope);
            Assert.Equal(campaignId, campaign.CampaignId);
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
    public async Task CharacterProjectionResolvesRecognizedArmorAndShieldFormula()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-projection-armor-{token}";
        var armorConceptKey = $"item.breastplate-{token}";
        var shieldConceptKey = $"item.shield-{token}";
        var actor = $"character-armor-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            var sourceCode = $"ARM{token}";
            var armorRaw = JsonSerializer.Serialize(new
            {
                name = "Breastplate",
                source = sourceCode,
                type = "MA",
                ac = 14
            });
            var shieldRaw = JsonSerializer.Serialize(new
            {
                name = "Shield",
                source = sourceCode,
                type = "S",
                ac = 2
            });
            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Character Armor Fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    new NormalizedSourceRepresentation(
                        FiveEToolsSourceFormatAdapter.Format,
                        new SourceRepresentationArtifact(
                            $"items-{token}.json",
                            Encoding.UTF8.GetBytes("{}"),
                            $"integration:character-armor:{token}"),
                        [
                            new NormalizedSourceRecord(
                                "item",
                                "Breastplate",
                                sourceCode,
                                $"item|Breastplate|{sourceCode}",
                                armorRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "item",
                                "Shield",
                                sourceCode,
                                $"item|Shield|{sourceCode}",
                                shieldRaw,
                                PublicationLocalKey: sourceCode)
                        ],
                        [
                            new NormalizedSourcePublication(
                                sourceCode,
                                $"Character Armor Fixture {token}",
                                "Integration Test Press",
                                "5e",
                                new DateOnly(2014, 8, 19))
                        ])));
            packageId = imported.PackageId;

            var byName = imported.Entities.ToDictionary(value => value.Name, StringComparer.Ordinal);
            var armorSource = byName["Breastplate"];
            var shieldSource = byName["Shield"];
            var armorConcept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(armorConceptKey, "item", armorSource.Name),
                actor);
            var shieldConcept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(shieldConceptKey, "item", shieldSource.Name),
                actor);
            await globalRules.BindSourceEntityAsync(
                armorConcept.Value.Id,
                new BindRuleConceptSourceRequest(armorSource.EntityId),
                actor);
            await globalRules.BindSourceEntityAsync(
                shieldConcept.Value.Id,
                new BindRuleConceptSourceRequest(shieldSource.EntityId),
                actor);

            var revisionIds = await db.SourceEntityRevisions
                .Where(value =>
                    value.SourceEntityId == armorSource.EntityId
                    || value.SourceEntityId == shieldSource.EntityId)
                .ToDictionaryAsync(value => value.SourceEntityId, value => value.Id);
            await globalRules.SetDecisionAsync(
                armorConcept.Value.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionIds[armorSource.EntityId],
                    "Armor projection fixture."),
                actor);
            await globalRules.SetDecisionAsync(
                shieldConcept.Value.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionIds[shieldSource.EntityId],
                    "Shield projection fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            var result = await projection.ResolveGlobalAsync(
                new CharacterRulesProjectionRequest(
                    BaseAbilityScores: new Dictionary<string, int>
                    {
                        ["strength"] = 10,
                        ["dexterity"] = 18,
                        ["constitution"] = 10,
                        ["intelligence"] = 10,
                        ["wisdom"] = 10,
                        ["charisma"] = 10
                    },
                    EquippedItemConceptKeys: [armorConceptKey, shieldConceptKey]),
                userId: null);

            var armorClass = Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "defense.ac.total");
            Assert.Equal(CharacterResolutionStates.Resolved, armorClass.State);
            Assert.Equal(18, armorClass.NumericValue);
            Assert.Contains(
                armorClass.Contributions,
                value => value.Label == "Armor base"
                    && value.NumericValue == 14);
            Assert.Contains(
                armorClass.Contributions,
                value => value.Label == "Dexterity contribution"
                    && value.NumericValue == 2);
            Assert.Contains(
                armorClass.Contributions,
                value => value.Label == "Shield bonus"
                    && value.NumericValue == 2);
            Assert.Empty(
                result.Conflicts.Where(value =>
                    value.ConflictKey.StartsWith("conflict.defense.ac.", StringComparison.Ordinal)));
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


    private static Task DeleteRelationshipRulingsAsync(
        RulesCoreDbContext db,
        string actor) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM rule_mechanical_relationship_ruling
            WHERE created_by_user_id = {actor};
            """);

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
