using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
                "Craft\tKEYSTAT:INT\tUSEUNTRAINED:YES\tACHECK:NO",
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

            // Simulate source revisions translated before family taxonomy metadata existed.
            // The consumer must recover current family semantics from preserved source identity
            // without requiring a destructive re-import of immutable source revisions.
            var legacyFamilyEntityIds = imported.Entities
                .Where(value => value.Name is "Craft" or "Craft (blacksmithing)" or "Craft (alchemy)")
                .Select(value => value.EntityId)
                .ToArray();
            var legacyFamilyRevisions = await db.SourceEntityRevisions
                .Where(value => legacyFamilyEntityIds.Contains(value.SourceEntityId))
                .ToArrayAsync();
            foreach (var revision in legacyFamilyRevisions)
            {
                var root = JsonNode.Parse(revision.ContentJson!)!.AsObject();
                var competency = root["_rulesCore"]!["competency"]!.AsObject();
                competency.Remove("familyName");
                competency.Remove("specialty");
                competency.Remove("isFamily");
                revision.ContentJson = root.ToJsonString();
            }
            await db.SaveChangesAsync();

            foreach (var (name, conceptKey) in new[]
                     {
                         ("Hide", "skill.hide"),
                         ("Move Silently", "skill.move-silently"),
                         ("Stealth", "skill.stealth"),
                         ("The planes", "skill.the-planes"),
                         ("Craft", "skill.craft"),
                         ("Craft (blacksmithing)", "skill.craft-blacksmithing"),
                         ("Craft (alchemy)", "skill.craft-alchemy")
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
                CharacterCompetencyKinds.Skill,
                specialized.Competency!.CompetencyKind);
            Assert.Null(specialized.Competency.FamilyName);
            Assert.Null(specialized.Competency.Specialty);
            Assert.Equal("intelligence", specialized.Competency.GoverningAbilityKey);
            Assert.True(specialized.Competency.TrainedOnly);
            Assert.False(specialized.Competency.ArmorCheckPenaltyApplies);

            var craftFamily = Assert.Single(
                catalog.Mechanics,
                value => value.MechanicKey == "competency.skill.craft");
            Assert.NotNull(craftFamily.Competency);
            Assert.Equal(CharacterCompetencyKinds.Skill, craftFamily.Competency!.CompetencyKind);
            Assert.Equal("Craft", craftFamily.Competency.FamilyName);
            Assert.Null(craftFamily.Competency.Specialty);
            Assert.True(craftFamily.Competency.IsFamily);

            var craft = Assert.Single(
                catalog.Mechanics,
                value => value.MechanicKey == "competency.skill.craft-blacksmithing");
            Assert.NotNull(craft.Competency);
            Assert.Equal(CharacterCompetencyKinds.SpecializedSkill, craft.Competency!.CompetencyKind);
            Assert.Equal("Craft", craft.Competency.FamilyName);
            Assert.Equal("blacksmithing", craft.Competency.Specialty);
            Assert.True(craft.Competency.SupportsRanks);

            var alchemy = Assert.Single(
                catalog.Mechanics,
                value => value.MechanicKey == "competency.skill.craft-alchemy");
            Assert.NotNull(alchemy.Competency);
            Assert.Equal(
                CharacterCompetencyKinds.SpecializedSkill,
                alchemy.Competency!.CompetencyKind);
            Assert.Equal("Craft", alchemy.Competency.FamilyName);
            Assert.Equal("alchemy", alchemy.Competency.Specialty);
            Assert.Equal("alchemy", alchemy.Competency.IdentityKey);
            Assert.Equal("Alchemy", alchemy.Competency.IdentityName);
            Assert.Equal(
                "competency.alchemy.training",
                alchemy.Competency.SharedTrainingKey);
            Assert.True(alchemy.Competency.SupportsRanks);
            Assert.True(alchemy.Competency.SupportsClassSkillState);
            var alchemySkillFacet = Assert.Single(alchemy.Competency.Facets);
            Assert.Equal("skill", alchemySkillFacet.FacetType);
            Assert.True(alchemySkillFacet.SupportsRanks);
            Assert.True(alchemySkillFacet.SupportsClassSkillState);
            Assert.Contains("competency.skill.craft-alchemy", alchemySkillFacet.MechanicKeys!);

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
    public async Task SharedCompetencyFacetsShareTrainingIdentityWithoutSharingNumericMechanics()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var actor = $"mechanics-facets-{token}";
        Guid skillPackageId = Guid.Empty;
        Guid toolPackageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var importer = new NormalizedSourceImportService(db);
            var normalization = new SourceNormalizationService(db);
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var mechanics = scope.ServiceProvider.GetRequiredService<ICharacterMechanicsConsumerService>();

            var toolSource = $"AT5{token}";
            var toolRaw = JsonSerializer.Serialize(new
            {
                name = "Alchemist's Supplies",
                source = toolSource,
                entries = new[] { "Later-edition alchemy tool fixture." }
            });
            var toolImport = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                $"mechanics-alchemy-tool-{token}",
                $"Mechanics Alchemy Tool {token}",
                "integration-test",
                "test-only",
                true,
                new NormalizedSourceRepresentation(
                    FiveEToolsSourceFormatAdapter.Format,
                    new SourceRepresentationArtifact(
                        $"alchemy-tool-{token}.json",
                        Encoding.UTF8.GetBytes(toolRaw),
                        $"integration:mechanics-alchemy-tool:{token}"),
                    [new NormalizedSourceRecord(
                        "tool",
                        "Alchemist's Supplies",
                        toolSource,
                        $"tool|Alchemist's Supplies|{toolSource}",
                        toolRaw,
                        PublicationLocalKey: toolSource)],
                    [new NormalizedSourcePublication(
                        toolSource,
                        $"Later Alchemy Tool {token}",
                        "Integration Test Press",
                        "5e",
                        new DateOnly(2014, 8, 19))])));
            toolPackageId = toolImport.PackageId;
            var toolEntity = Assert.Single(toolImport.Entities);
            var toolAccepted = await normalization.AcceptAsync(toolEntity.EntityId, actor);
            Assert.NotNull(toolAccepted);
            Assert.Equal("tool.alchemists-supplies", toolAccepted!.Concept.Key);

            var sourceShort = $"CA35{token}";
            var pcgenFile = $"data/35e/example/shared_alchemy_skill_{token}.lst";
            var pcgenText = string.Join('\n',
            [
                $"SOURCELONG:Craft Alchemy 3.5e Fixture {token}\tSOURCESHORT:{sourceShort}",
                "Craft (alchemy)\tKEYSTAT:INT\tUSEUNTRAINED:YES\tACHECK:NO"
            ]);
            var pcgenRepresentation = new PcGenSourceFormatAdapter().TryRead(
                new SourceRepresentationArtifact(
                    pcgenFile,
                    Encoding.UTF8.GetBytes(pcgenText),
                    $"integration:mechanics-craft-alchemy-35:{token}#{pcgenFile}"))
                ?? throw new InvalidOperationException(
                    "PCGen Craft (alchemy) fixture was not readable.");
            var skillImport = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                $"mechanics-craft-alchemy-35-{token}",
                $"Mechanics Craft Alchemy 3.5e {token}",
                "integration-test",
                "test-only",
                true,
                pcgenRepresentation));
            skillPackageId = skillImport.PackageId;
            var skillEntity = Assert.Single(skillImport.Entities);
            Assert.Equal("skill", skillEntity.EntityType);
            Assert.Equal("Craft (alchemy)", skillEntity.Name);
            var skillAccepted = await normalization.AcceptAsync(skillEntity.EntityId, actor);
            Assert.NotNull(skillAccepted);
            Assert.Equal("skill.craft-alchemy", skillAccepted!.Concept.Key);

            var revisionIds = await db.SourceEntityRevisions
                .Where(value =>
                    value.SourceEntityId == toolEntity.EntityId
                    || value.SourceEntityId == skillEntity.EntityId)
                .ToDictionaryAsync(value => value.SourceEntityId, value => value.Id);
            await globalRules.SetDecisionAsync(
                toolAccepted.Concept.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionIds[toolEntity.EntityId],
                    "Later tool facet fixture."),
                actor);
            await globalRules.SetDecisionAsync(
                skillAccepted.Concept.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionIds[skillEntity.EntityId],
                    "Historical ranked skill facet fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            var catalog = await mechanics.GetGlobalAsync(userId: null);
            var tool = Assert.Single(
                catalog.Mechanics,
                value => value.MechanicKey == "competency.tool.alchemists-supplies");
            var skill = Assert.Single(
                catalog.Mechanics,
                value => value.MechanicKey == "competency.skill.craft-alchemy");
            Assert.NotNull(tool.Competency);
            Assert.NotNull(skill.Competency);

            foreach (var competency in new[] { tool.Competency!, skill.Competency! })
            {
                Assert.Equal("alchemy", competency.IdentityKey);
                Assert.Equal("Alchemy", competency.IdentityName);
                Assert.Equal("competency.alchemy.training", competency.SharedTrainingKey);
                Assert.Equal(2, competency.Facets!.Count);

                var skillFacet = Assert.Single(
                    competency.Facets,
                    value => value.FacetType == "skill");
                Assert.True(skillFacet.SupportsRanks);
                Assert.True(skillFacet.SupportsClassSkillState);
                Assert.Contains(
                    "competency.skill.craft-alchemy",
                    skillFacet.MechanicKeys!);

                var toolFacet = Assert.Single(
                    competency.Facets,
                    value => value.FacetType == "tool");
                Assert.False(toolFacet.SupportsRanks);
                Assert.False(toolFacet.SupportsClassSkillState);
                Assert.True(toolFacet.SupportsTrainingState);
                Assert.Contains(
                    "competency.tool.alchemists-supplies",
                    toolFacet.MechanicKeys!);
            }

            var skillProfile = Assert.Single(skill.Competency!.Profiles);
            Assert.True(skillProfile.SupportsRanks);
            var skillEvaluation = await mechanics.EvaluateGlobalAsync(
                "competency.skill.craft-alchemy",
                new CharacterMechanicEvaluationRequest(
                    IntegerInputs: new Dictionary<string, int>
                    {
                        ["abilityContribution"] = 4,
                        ["ranks"] = 6,
                        ["otherModifier"] = 1
                    },
                    CapabilityKeys: ["competency.skill-ranks"],
                    CompetencyProfileSourceEntityRevisionId:
                        skillProfile.SourceEntityRevisionId),
                userId: null);
            Assert.NotNull(skillEvaluation);
            Assert.Equal(11, skillEvaluation.Value);

            var toolProfile = Assert.Single(tool.Competency!.Profiles);
            Assert.False(toolProfile.SupportsRanks);
            var toolEvaluation = await mechanics.EvaluateGlobalAsync(
                "competency.tool.alchemists-supplies",
                new CharacterMechanicEvaluationRequest(
                    IntegerInputs: new Dictionary<string, int>
                    {
                        ["abilityContribution"] = 4,
                        ["trainingContribution"] = 3,
                        ["otherModifier"] = 1,
                        ["ranks"] = 99
                    },
                    CompetencyProfileSourceEntityRevisionId:
                        toolProfile.SourceEntityRevisionId),
                userId: null);
            Assert.NotNull(toolEvaluation);
            Assert.Equal(8, toolEvaluation.Value);
            Assert.DoesNotContain(toolProfile.Inputs, value => value.Key == "ranks");
        }
        finally
        {
            await using var cleanupScope = factory.Services.CreateAsyncScope();
            await CleanupAsync(
                cleanupScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>(),
                skillPackageId,
                toolPackageId);
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
                        ["combat.grapple.size-modifier"] = 0,
                        ["defense.ac.size-modifier"] = 0,
                        ["defense.ac.armor-bonus"] = 4,
                        ["defense.ac.shield-bonus"] = 2,
                        ["defense.ac.natural-armor-bonus"] = 1,
                        ["defense.ac.deflection-bonus"] = 1,
                        ["defense.ac.dodge-contribution"] = 1,
                        ["defense.ac.contribution.insight"] = 2,
                        ["defense.ac.touch.contribution.circumstance"] = 1,
                        ["defense.ac.flat-footed.contribution.sacred"] = 3
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
            Assert.DoesNotContain(
                result.Mechanics,
                value => value.MechanicKey == "save.constitution");
            Assert.Equal(5, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "combat.grapple").NumericValue);
            var totalArmorClass = Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "defense.ac.total");
            Assert.Equal(24, totalArmorClass.NumericValue);
            Assert.Contains(
                totalArmorClass.Contributions,
                value => value.ContributionKey == "defense.ac.contribution.insight"
                    && value.NumericValue == 2);

            var touchArmorClass = Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "defense.ac.touch");
            Assert.Equal(16, touchArmorClass.NumericValue);
            Assert.Contains(
                touchArmorClass.Contributions,
                value => value.ContributionKey == "defense.ac.touch.contribution.circumstance"
                    && value.NumericValue == 1);

            var flatFootedArmorClass = Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "defense.ac.flat-footed");
            Assert.Equal(21, flatFootedArmorClass.NumericValue);
            Assert.Contains(
                flatFootedArmorClass.Contributions,
                value => value.ContributionKey == "defense.ac.flat-footed.contribution.sacred"
                    && value.NumericValue == 3);
            Assert.Contains(
                result.Capabilities,
                value => value.CapabilityKey == "defense.ac.touch");
            Assert.Contains(
                result.Capabilities,
                value => value.CapabilityKey == "defense.ac.flat-footed");
            Assert.Equal(4, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == $"advancement.{conceptKey}.skill-points-per-level").NumericValue);

            var hitDice = Assert.Single(
                result.Resources,
                value => value.ResourceKey == $"resource.hit-die.{conceptKey}");
            Assert.Equal(5, hitDice.MaximumValue);
            var maximumHp = Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "health.maximum-hp");
            Assert.Equal(CharacterResolutionStates.MissingCharacterInput, maximumHp.State);
            Assert.Contains(
                $"health.hit-point-gain.{conceptKey}.level-1",
                maximumHp.MissingCharacterInputs);
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
                hd = new { number = 1, faces = 8 },
                proficiency = new[] { "int", "wis" },
                spellcastingAbility = "int",
                classFeatures = new object[]
                {
                    $"Arcane Study|Example Mage|{sourceCode}|1",
                    new
                    {
                        classFeature = $"Focused Study|Example Mage|{sourceCode}|3"
                    },
                    $"Arcane Mastery|Example Mage|{sourceCode}|6"
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
                    ],
                    HitPointGains:
                    [
                        new CharacterHitPointGainInput(conceptKey, 1, 8),
                        new CharacterHitPointGainInput(conceptKey, 2, 5),
                        new CharacterHitPointGainInput(conceptKey, 3, 5),
                        new CharacterHitPointGainInput(conceptKey, 4, 5),
                        new CharacterHitPointGainInput(conceptKey, 5, 5)
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
            Assert.Equal(1, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "save.constitution").NumericValue);
            Assert.DoesNotContain(
                result.Mechanics,
                value => value.MechanicKey == "save.fortitude");
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
            var maximumHp = Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "health.maximum-hp");
            Assert.Equal(CharacterResolutionStates.Resolved, maximumHp.State);
            Assert.Equal(33, maximumHp.NumericValue);
            Assert.Equal(5, maximumHp.Contributions.Count);
            Assert.Contains(
                result.Capabilities,
                value => value.CapabilityKey == "spellcasting");
            var arcaneStudy = Assert.Single(
                result.Features,
                value => value.DisplayName == "Arcane Study"
                    && value.State == CharacterResolutionStates.Resolved);
            Assert.Equal("class", arcaneStudy.GrantingSourceKind);
            Assert.Equal(1, arcaneStudy.AcquisitionLevel);
            Assert.Equal(arcaneStudy.FeatureKey, arcaneStudy.OccurrenceKey);
            Assert.Equal(conceptKey, arcaneStudy.SourceConceptKey);

            var focusedStudy = Assert.Single(
                result.Features,
                value => value.DisplayName == "Focused Study"
                    && value.State == CharacterResolutionStates.Resolved);
            Assert.Equal("class", focusedStudy.GrantingSourceKind);
            Assert.Equal(3, focusedStudy.AcquisitionLevel);
            Assert.Equal(conceptKey, focusedStudy.SourceConceptKey);
            Assert.DoesNotContain(
                result.Features,
                value => value.DisplayName == "Arcane Mastery");
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
    public async Task CharacterProjectionProjectsTemporaryAbilityDeathSaveAndMissChanceSemantics()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-projection-state-support-{token}";
        var conceptKey = $"rule.character-state-support-{token}";
        var actor = $"character-state-support-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            var sourceCode = $"CSS{token}";
            var raw = JsonSerializer.Serialize(new
            {
                name = "Character State Support",
                source = sourceCode,
                preparedSpellRestriction = "removed",
                _rulesCore = new
                {
                    character = new
                    {
                        effects = new object[]
                        {
                            new
                            {
                                key = "effect.temporary-strength",
                                kind = "mechanic-contribution",
                                operation = "add",
                                target = "ability.strength.score",
                                value = 4,
                                condition = "condition.temporary-strength"
                            }
                        },
                        resources = new object[]
                        {
                            new
                            {
                                key = "resource.death-save.successes",
                                displayName = "Death Save Successes",
                                maximum = 3,
                                recoveryProcedure = "procedure.death-save.reset"
                            },
                            new
                            {
                                key = "resource.death-save.failures",
                                displayName = "Death Save Failures",
                                maximum = 3,
                                recoveryProcedure = "procedure.death-save.reset"
                            }
                        },
                        procedures = new object[]
                        {
                            new
                            {
                                key = "procedure.death-save.reset",
                                displayName = "Reset Death Saves",
                                presentationRole = "recovery",
                                effects = new object[]
                                {
                                    new
                                    {
                                        key = "effect.death-save.reset-successes",
                                        kind = "resource",
                                        operation = "set",
                                        target = "resource.death-save.successes",
                                        value = 0
                                    },
                                    new
                                    {
                                        key = "effect.death-save.reset-failures",
                                        kind = "resource",
                                        operation = "set",
                                        target = "resource.death-save.failures",
                                        value = 0
                                    }
                                }
                            }
                        },
                        passives = new object[]
                        {
                            new
                            {
                                key = "defense.miss-chance",
                                displayName = "Miss Chance",
                                value = 20,
                                unit = "percent"
                            }
                        }
                    }
                }
            });

            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Character State Support {token}",
                    "integration-test",
                    "test-only",
                    true,
                    new NormalizedSourceRepresentation(
                        FiveEToolsSourceFormatAdapter.Format,
                        new SourceRepresentationArtifact(
                            $"character-state-support-{token}.json",
                            Encoding.UTF8.GetBytes(raw),
                            $"integration:character-state-support:{token}"),
                        [
                            new NormalizedSourceRecord(
                                "rule",
                                "Character State Support",
                                sourceCode,
                                $"rule|Character State Support|{sourceCode}",
                                raw,
                                PublicationLocalKey: sourceCode)
                        ],
                        [
                            new NormalizedSourcePublication(
                                sourceCode,
                                $"Character State Support {token}",
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
                    "Character state support fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            CharacterRulesProjectionRequest Request(bool active) =>
                new(
                    BaseAbilityScores: new Dictionary<string, int>
                    {
                        ["strength"] = 10,
                        ["dexterity"] = 14,
                        ["constitution"] = 12,
                        ["intelligence"] = 10,
                        ["wisdom"] = 10,
                        ["charisma"] = 10
                    },
                    SelectedConcepts: [new CharacterSelectedConceptInput(conceptKey)],
                    CurrentResources: new Dictionary<string, int>
                    {
                        ["resource.death-save.successes"] = 1,
                        ["resource.death-save.failures"] = 2
                    },
                    ConditionKeys: active ? ["condition.temporary-strength"] : []);

            var inactive = await projection.ResolveGlobalAsync(Request(active: false), userId: null);
            Assert.Equal(10, Assert.Single(
                inactive.Mechanics,
                value => value.MechanicKey == "ability.strength.score").NumericValue);
            Assert.DoesNotContain(
                inactive.Mechanics,
                value => value.MechanicKey == "ability.strength.temporary-score");

            var result = await projection.ResolveGlobalAsync(Request(active: true), userId: null);

            Assert.Equal(10, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "ability.strength.base").NumericValue);
            Assert.Equal(10, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "ability.strength.ordinary-score").NumericValue);
            Assert.Equal(14, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "ability.strength.temporary-score").NumericValue);
            Assert.Equal(2, Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "ability.strength.temporary-modifier").NumericValue);
            var effectiveStrength = Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "ability.strength.score");
            Assert.Equal(14, effectiveStrength.NumericValue);
            Assert.Contains(
                effectiveStrength.Contributions,
                value => value.ContributionKey == "effect.temporary-strength"
                    && value.StateKind == "temporary"
                    && value.ConditionKey == "condition.temporary-strength");

            var successes = Assert.Single(
                result.Resources,
                value => value.ResourceKey == "resource.death-save.successes");
            Assert.Equal(1, successes.CurrentValue);
            Assert.Equal(3, successes.MaximumValue);
            Assert.Equal("procedure.death-save.reset", successes.RecoveryProcedureKey);
            var failures = Assert.Single(
                result.Resources,
                value => value.ResourceKey == "resource.death-save.failures");
            Assert.Equal(2, failures.CurrentValue);
            Assert.Equal(3, failures.MaximumValue);

            var reset = Assert.Single(
                result.Procedures,
                value => value.ProcedureKey == "procedure.death-save.reset");
            Assert.Equal(2, reset.Effects.Count);
            Assert.Contains(
                reset.Effects,
                value => value.TargetKey == "resource.death-save.successes"
                    && value.Operation == "set"
                    && value.NumericValue == 0);
            Assert.Contains(
                reset.Effects,
                value => value.TargetKey == "resource.death-save.failures"
                    && value.Operation == "set"
                    && value.NumericValue == 0);

            var missChance = Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "defense.miss-chance");
            Assert.Equal(20, missChance.NumericValue);
            Assert.Equal("percent", missChance.Unit);
            var preparation = Assert.Single(
                result.Mechanics,
                value => value.MechanicKey == "spellcasting.preparation-restriction");
            Assert.Equal("removed", preparation.TextValue);
            Assert.Equal(conceptKey, Assert.Single(preparation.Contributions).SourceConceptKey);
            Assert.DoesNotContain(
                Assert.Single(
                    result.Mechanics,
                    value => value.MechanicKey == "defense.ac.total").Contributions,
                value => value.ContributionKey == "defense.miss-chance");
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
    public async Task CharacterProjectionExposesSpellCastingMetadata()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-projection-spell-detail-{token}";
        var conceptKey = $"spell.example-ward-{token}";
        var actor = $"character-spell-detail-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            var sourceCode = $"SPD{token}";
            var raw = JsonSerializer.Serialize(new
            {
                name = "Example Ward",
                source = sourceCode,
                level = 3,
                school = "A",
                time = new object[]
                {
                    new { number = 1, unit = "action" }
                },
                range = new
                {
                    type = "point",
                    distance = new { type = "feet", amount = 60 }
                },
                components = new
                {
                    v = true,
                    s = true,
                    m = "a pearl worth 50 gp"
                },
                duration = new object[]
                {
                    new
                    {
                        type = "timed",
                        duration = new { type = "minute", amount = 1 },
                        concentration = true
                    }
                },
                meta = new { ritual = true }
            });

            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Character Spell Detail {token}",
                    "integration-test",
                    "test-only",
                    true,
                    new NormalizedSourceRepresentation(
                        FiveEToolsSourceFormatAdapter.Format,
                        new SourceRepresentationArtifact(
                            $"spell-detail-{token}.json",
                            Encoding.UTF8.GetBytes(raw),
                            $"integration:character-spell-detail:{token}"),
                        [
                            new NormalizedSourceRecord(
                                "spell",
                                "Example Ward",
                                sourceCode,
                                $"spell|Example Ward|{sourceCode}",
                                raw,
                                PublicationLocalKey: sourceCode)
                        ],
                        [
                            new NormalizedSourcePublication(
                                sourceCode,
                                $"Character Spell Detail {token}",
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
                    "Spell detail fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            var result = await projection.ResolveGlobalAsync(
                new CharacterRulesProjectionRequest(
                    KnownSpellConceptKeys: [conceptKey]),
                userId: null);

            var action = Assert.Single(
                result.Actions,
                value => value.ActionKey == $"action.spell.{conceptKey}");
            Assert.Equal(conceptKey, action.SourceConceptKey);
            Assert.Equal(3, action.SpellLevel);
            Assert.Equal("A", action.SpellSchool);
            Assert.Equal("1 action", action.CastingTime);
            Assert.Equal("60 feet", action.Range);
            Assert.Equal(new[] { "V", "S", "M" }, action.SpellComponents);
            Assert.Equal("a pearl worth 50 gp", action.MaterialComponent);
            Assert.Equal("1 minute", action.Duration);
            Assert.True(action.Ritual);
            Assert.True(action.Concentration);
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
                size = new[] { "S", "M" },
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

            var unresolvedRequest = new CharacterRulesProjectionRequest(
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

            var unresolvedSize = await projection.ResolveGlobalAsync(
                unresolvedRequest,
                userId: null);
            var sizeChoice = Assert.Single(
                unresolvedSize.Choices,
                value => value.Kind == "size-category");
            Assert.Equal(CharacterResolutionStates.ChoiceRequired, sizeChoice.State);
            Assert.Collection(
                sizeChoice.Options.OrderBy(value => value.DisplayName, StringComparer.Ordinal),
                value => Assert.Equal("Medium", value.DisplayName),
                value => Assert.Equal("Small", value.DisplayName));
            Assert.Equal(
                CharacterResolutionStates.ChoiceRequired,
                Assert.Single(
                    unresolvedSize.Mechanics,
                    value => value.MechanicKey == "character.size-category").State);

            var request = unresolvedRequest with
            {
                Choices =
                [
                    new CharacterRuntimeChoiceInput(
                        $"choice.{conceptKey}.size-category",
                        "Medium")
                ]
            };

            var global = await projection.ResolveGlobalAsync(request, userId: null);
            var globalWalk = Assert.Single(
                global.Movement,
                value => value.MovementKey == "movement.walk");
            Assert.Equal(30, globalWalk.Value);
            var size = Assert.Single(
                global.Mechanics,
                value => value.MechanicKey == "character.size-category");
            Assert.Equal("Medium", size.TextValue);
            var sizeContribution = Assert.Single(size.Contributions);
            Assert.Equal(conceptKey, sizeContribution.SourceConceptKey);
            Assert.Equal("Medium", sizeContribution.TextValue);
            var speciesFeature = Assert.Single(
                global.Features,
                value => value.FeatureKey == $"feature.{conceptKey}");
            Assert.Equal("race", speciesFeature.GrantingSourceKind);
            Assert.Equal(conceptKey, speciesFeature.SourceConceptKey);
            Assert.Equal(speciesFeature.FeatureKey, speciesFeature.OccurrenceKey);

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
                category = "armor",
                ac = 14,
                weight = 20.0m,
                property = new[] { "StealthDisadvantage" }
            });
            var shieldRaw = JsonSerializer.Serialize(new
            {
                name = "Shield",
                source = sourceCode,
                type = "S",
                category = "armor",
                ac = 2,
                weight = 6.0m,
                reqAttune = "by a shield specialist"
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

            var armorDefinition = Assert.Single(
                result.Equipment,
                value => value.ConceptKey == armorConceptKey);
            Assert.Equal("MA", armorDefinition.ItemType);
            Assert.Equal("medium-armor", armorDefinition.ArmorRole);
            Assert.Equal(20.0m, armorDefinition.Weight);
            Assert.Equal("lb", armorDefinition.WeightUnit);
            Assert.Contains("StealthDisadvantage", armorDefinition.PropertyKeys);

            var shieldDefinition = Assert.Single(
                result.Equipment,
                value => value.ConceptKey == shieldConceptKey);
            Assert.Equal("shield", shieldDefinition.ArmorRole);
            Assert.True(shieldDefinition.RequiresAttunement);
            Assert.Equal("by a shield specialist", shieldDefinition.AttunementRequirement);

            var carriedOnly = await projection.ResolveGlobalAsync(
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
                    ItemConceptKeys: [armorConceptKey]),
                userId: null);
            Assert.Single(
                carriedOnly.Equipment,
                value => value.ConceptKey == armorConceptKey);
            Assert.Equal(14, Assert.Single(
                carriedOnly.Mechanics,
                value => value.MechanicKey == "defense.ac.total").NumericValue);
            Assert.DoesNotContain(
                carriedOnly.Features,
                value => value.FeatureKey == $"feature.{armorConceptKey}");

            var unarmored = await projection.ResolveGlobalAsync(
                new CharacterRulesProjectionRequest(
                    BaseAbilityScores: new Dictionary<string, int>
                    {
                        ["strength"] = 10,
                        ["dexterity"] = 18,
                        ["constitution"] = 10,
                        ["intelligence"] = 10,
                        ["wisdom"] = 10,
                        ["charisma"] = 10
                    }),
                userId: null);
            Assert.Equal(14, Assert.Single(
                unarmored.Mechanics,
                value => value.MechanicKey == "defense.ac.total").NumericValue);
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
    public async Task CharacterProjectionResolvesFiveXWeaponAttackAndPreservesFinesseChoice()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-projection-weapon-{token}";
        var classConceptKey = $"class.weapon-user-{token}";
        var weaponConceptKey = $"item.rapier-{token}";
        var actor = $"character-weapon-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            var sourceCode = $"WPN{token}";
            var classRaw = JsonSerializer.Serialize(new
            {
                name = "Weapon User",
                source = sourceCode,
                hd = new { number = 1, faces = 10 },
                proficiency = new[] { "str", "con" },
                startingProficiencies = new
                {
                    weapons = new[] { "simple", "martial" }
                }
            });
            var weaponRaw = JsonSerializer.Serialize(new
            {
                name = "Rapier +1",
                source = sourceCode,
                type = "M",
                weaponCategory = "martial",
                property = new[] { "F" },
                dmg1 = "1d8",
                dmgType = "P",
                bonusWeapon = "+1"
            });
            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Character Weapon Fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    new NormalizedSourceRepresentation(
                        FiveEToolsSourceFormatAdapter.Format,
                        new SourceRepresentationArtifact(
                            $"weapon-fixture-{token}.json",
                            Encoding.UTF8.GetBytes("{}"),
                            $"integration:character-weapon:{token}"),
                        [
                            new NormalizedSourceRecord(
                                "class",
                                "Weapon User",
                                sourceCode,
                                $"class|Weapon User|{sourceCode}",
                                classRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "item",
                                "Rapier +1",
                                sourceCode,
                                $"item|Rapier +1|{sourceCode}",
                                weaponRaw,
                                PublicationLocalKey: sourceCode)
                        ],
                        [
                            new NormalizedSourcePublication(
                                sourceCode,
                                $"Character Weapon Fixture {token}",
                                "Integration Test Press",
                                "5e",
                                new DateOnly(2014, 8, 19))
                        ])));
            packageId = imported.PackageId;
            var byName = imported.Entities.ToDictionary(value => value.Name, StringComparer.Ordinal);
            var classSource = byName["Weapon User"];
            var weaponSource = byName["Rapier +1"];

            var classConcept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(
                    classConceptKey,
                    classSource.EntityType,
                    classSource.Name),
                actor);
            var weaponConcept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(
                    weaponConceptKey,
                    weaponSource.EntityType,
                    weaponSource.Name),
                actor);
            await globalRules.BindSourceEntityAsync(
                classConcept.Value.Id,
                new BindRuleConceptSourceRequest(classSource.EntityId),
                actor);
            await globalRules.BindSourceEntityAsync(
                weaponConcept.Value.Id,
                new BindRuleConceptSourceRequest(weaponSource.EntityId),
                actor);

            var revisionIds = await db.SourceEntityRevisions
                .Where(value =>
                    value.SourceEntityId == classSource.EntityId
                    || value.SourceEntityId == weaponSource.EntityId)
                .ToDictionaryAsync(value => value.SourceEntityId, value => value.Id);
            await globalRules.SetDecisionAsync(
                classConcept.Value.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionIds[classSource.EntityId],
                    "Weapon class fixture."),
                actor);
            await globalRules.SetDecisionAsync(
                weaponConcept.Value.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionIds[weaponSource.EntityId],
                    "Weapon item fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            CharacterRulesProjectionRequest Request(IReadOnlyList<CharacterRuntimeChoiceInput>? choices = null) =>
                new(
                    BaseAbilityScores: new Dictionary<string, int>
                    {
                        ["strength"] = 10,
                        ["dexterity"] = 18,
                        ["constitution"] = 12,
                        ["intelligence"] = 10,
                        ["wisdom"] = 10,
                        ["charisma"] = 10
                    },
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(classConceptKey, 5)
                    ],
                    EquippedItemConceptKeys: [weaponConceptKey],
                    Choices: choices);

            var unresolved = await projection.ResolveGlobalAsync(Request(), userId: null);
            var unresolvedAttack = Assert.Single(
                unresolved.Mechanics,
                value => value.MechanicKey == $"attack.{weaponConceptKey}");
            Assert.Equal(CharacterResolutionStates.ChoiceRequired, unresolvedAttack.State);
            Assert.Contains(
                $"weapon.{weaponConceptKey}.attack-ability",
                unresolvedAttack.RequiredChoices);

            var resolved = await projection.ResolveGlobalAsync(
                Request(
                [
                    new CharacterRuntimeChoiceInput(
                        $"weapon.{weaponConceptKey}.attack-ability",
                        "dexterity")
                ]),
                userId: null);

            var attack = Assert.Single(
                resolved.Mechanics,
                value => value.MechanicKey == $"attack.{weaponConceptKey}");
            Assert.Equal(CharacterResolutionStates.Resolved, attack.State);
            Assert.Equal(8, attack.NumericValue);
            Assert.Equal("dexterity", attack.TextValue);

            var damageModifier = Assert.Single(
                resolved.Mechanics,
                value => value.MechanicKey == $"damage.{weaponConceptKey}.modifier");
            Assert.Equal(5, damageModifier.NumericValue);

            var action = Assert.Single(
                resolved.Actions,
                value => value.ActionKey == $"action.attack.{weaponConceptKey}");
            Assert.Equal(CharacterResolutionStates.Resolved, action.State);
            Assert.Equal($"attack.{weaponConceptKey}", action.AttackMechanicKey);
            Assert.Equal("1d8 + 5", action.DamageExpression);
            Assert.Equal("P", action.DamageType);
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
    public async Task CharacterProjectionUsesPublishedResourceChoiceAndSingleClassSpellSlotTable()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-projection-spell-resource-{token}";
        var classConceptKey = $"class.slot-caster-{token}";
        var houseConceptKey = $"house.spellcasting-resource-choice-{token}";
        var actor = $"character-spell-resource-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            var sourceCode = $"SPR{token}";
            var classRaw = JsonSerializer.Serialize(new
            {
                name = "Slot Caster",
                source = sourceCode,
                hd = new { number = 1, faces = 8 },
                proficiency = new[] { "wis", "cha" },
                spellcastingAbility = "cha",
                casterProgression = "full",
                classTableGroups = new object[]
                {
                    new
                    {
                        title = "Spell Slots per Spell Level",
                        colLabels = new[] { "1st", "2nd", "3rd", "4th", "5th", "6th" },
                        rowsSpellProgression = new[]
                        {
                            new[] { 2, 0, 0, 0, 0, 0 },
                            new[] { 3, 0, 0, 0, 0, 0 },
                            new[] { 4, 2, 0, 0, 0, 0 },
                            new[] { 4, 3, 0, 0, 0, 0 },
                            new[] { 4, 3, 2, 0, 0, 0 },
                            new[] { 4, 3, 3, 0, 0, 0 },
                            new[] { 4, 3, 3, 1, 0, 0 },
                            new[] { 4, 3, 3, 2, 0, 0 },
                            new[] { 4, 3, 3, 3, 1, 0 },
                            new[] { 4, 3, 3, 3, 2, 0 },
                            new[] { 4, 3, 3, 3, 2, 1 }
                        }
                    }
                }
            });
            var houseRaw = JsonSerializer.Serialize(new
            {
                name = "Spellcasting Resource Choice",
                source = sourceCode,
                category = "spellcasting",
                casterChoosesResourceSystem = true,
                availableResourceSystems = new[] { "spell slots", "spell points" }
            });

            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Character Spell Resource Fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    new NormalizedSourceRepresentation(
                        FiveEToolsSourceFormatAdapter.Format,
                        new SourceRepresentationArtifact(
                            $"spell-resource-{token}.json",
                            Encoding.UTF8.GetBytes("{}"),
                            $"integration:character-spell-resource:{token}"),
                        [
                            new NormalizedSourceRecord(
                                "class",
                                "Slot Caster",
                                sourceCode,
                                $"class|Slot Caster|{sourceCode}",
                                classRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "houseRule",
                                "Spellcasting Resource Choice",
                                sourceCode,
                                $"houseRule|Spellcasting Resource Choice|{sourceCode}",
                                houseRaw,
                                PublicationLocalKey: sourceCode)
                        ],
                        [
                            new NormalizedSourcePublication(
                                sourceCode,
                                $"Character Spell Resource Fixture {token}",
                                "Integration Test Press",
                                "5e",
                                new DateOnly(2014, 8, 19))
                        ])));
            packageId = imported.PackageId;
            var byName = imported.Entities.ToDictionary(value => value.Name, StringComparer.Ordinal);
            var classSource = byName["Slot Caster"];
            var houseSource = byName["Spellcasting Resource Choice"];

            var classConcept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(
                    classConceptKey,
                    classSource.EntityType,
                    classSource.Name),
                actor);
            var houseConcept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(
                    houseConceptKey,
                    houseSource.EntityType,
                    houseSource.Name),
                actor);
            await globalRules.BindSourceEntityAsync(
                classConcept.Value.Id,
                new BindRuleConceptSourceRequest(classSource.EntityId),
                actor);
            await globalRules.BindSourceEntityAsync(
                houseConcept.Value.Id,
                new BindRuleConceptSourceRequest(houseSource.EntityId),
                actor);

            var revisionIds = await db.SourceEntityRevisions
                .Where(value =>
                    value.SourceEntityId == classSource.EntityId
                    || value.SourceEntityId == houseSource.EntityId)
                .ToDictionaryAsync(value => value.SourceEntityId, value => value.Id);
            await globalRules.SetDecisionAsync(
                classConcept.Value.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionIds[classSource.EntityId],
                    "Spell slot table fixture."),
                actor);
            await globalRules.SetDecisionAsync(
                houseConcept.Value.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionIds[houseSource.EntityId],
                    "Published resource choice fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            CharacterRulesProjectionRequest Request(
                string? system = null,
                int classLevel = 5) =>
                new(
                    BaseAbilityScores: new Dictionary<string, int>
                    {
                        ["strength"] = 10,
                        ["dexterity"] = 12,
                        ["constitution"] = 12,
                        ["intelligence"] = 10,
                        ["wisdom"] = 10,
                        ["charisma"] = 18
                    },
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(classConceptKey, classLevel)
                    ],
                    Choices: system is null
                        ? null
                        : [
                            new CharacterRuntimeChoiceInput(
                                "spellcasting.resource-system",
                                system)
                        ]);

            var choiceRequired = await projection.ResolveGlobalAsync(
                Request(),
                userId: null);
            var resourceChoice = Assert.Single(
                choiceRequired.Spellcasting,
                value => value.SpellcastingKey == "spellcasting.resource-choice");
            Assert.Equal(CharacterResolutionStates.ChoiceRequired, resourceChoice.State);
            Assert.Contains(
                "spellcasting.resource-system",
                resourceChoice.RequiredChoices);
            Assert.DoesNotContain(
                choiceRequired.Resources,
                value => value.ResourceKey.StartsWith("resource.spell-slot.", StringComparison.Ordinal));

            var slots = await projection.ResolveGlobalAsync(
                Request("spell slots"),
                userId: null);
            Assert.Equal(4, Assert.Single(
                slots.Resources,
                value => value.ResourceKey == "resource.spell-slot.1").MaximumValue);
            Assert.Equal(3, Assert.Single(
                slots.Resources,
                value => value.ResourceKey == "resource.spell-slot.2").MaximumValue);
            Assert.Equal(2, Assert.Single(
                slots.Resources,
                value => value.ResourceKey == "resource.spell-slot.3").MaximumValue);
            Assert.Equal(
                "spell-slots",
                Assert.Single(
                    slots.Spellcasting,
                    value => value.SpellcastingKey == $"spellcasting.{classConceptKey}")
                    .ResourceSystemKey);

            var points = await projection.ResolveGlobalAsync(
                Request("spell points"),
                userId: null);
            var spellPoints = Assert.Single(
                points.Resources,
                value => value.ResourceKey == "resource.spell-points");
            Assert.Equal(CharacterResolutionStates.Resolved, spellPoints.State);
            Assert.Equal(27, spellPoints.MaximumValue);
            Assert.Equal(3, Assert.Single(
                points.Mechanics,
                value => value.MechanicKey ==
                    "spellcasting.spell-points.maximum-slot-level").NumericValue);
            Assert.Equal(5, Assert.Single(
                points.Mechanics,
                value => value.MechanicKey ==
                    "spellcasting.spell-points.slot-cost.level-3").NumericValue);
            Assert.DoesNotContain(
                points.Resources,
                value => value.ResourceKey.StartsWith("resource.spell-slot.", StringComparison.Ordinal));
            Assert.Equal(
                "spell-points",
                Assert.Single(
                    points.Spellcasting,
                    value => value.SpellcastingKey == $"spellcasting.{classConceptKey}")
                    .ResourceSystemKey);

            var highLevelPoints = await projection.ResolveGlobalAsync(
                Request("spell points", classLevel: 11),
                userId: null);
            Assert.Equal(73, Assert.Single(
                highLevelPoints.Resources,
                value => value.ResourceKey == "resource.spell-points").MaximumValue);
            Assert.Equal(6, Assert.Single(
                highLevelPoints.Mechanics,
                value => value.MechanicKey ==
                    "spellcasting.spell-points.maximum-slot-level").NumericValue);
            Assert.Equal(9, Assert.Single(
                highLevelPoints.Mechanics,
                value => value.MechanicKey ==
                    "spellcasting.spell-points.slot-cost.level-6").NumericValue);
            Assert.Equal(1, Assert.Single(
                highLevelPoints.Mechanics,
                value => value.MechanicKey ==
                    "spellcasting.spell-points.slot-creation-limit.level-6").NumericValue);
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
    public async Task CharacterProjectionKeepsPactMagicSeparateFromStandardSpellSlots()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-projection-pact-magic-{token}";
        var pactConceptKey = $"class.pact-caster-{token}";
        var fullConceptKey = $"class.full-caster-{token}";
        var houseConceptKey = $"house.spellcasting-resource-choice-{token}";
        var actor = $"character-pact-magic-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            var sourceCode = $"PCM{token}";
            var pactRaw = JsonSerializer.Serialize(new
            {
                name = "Pact Caster",
                source = sourceCode,
                hd = new { number = 1, faces = 8 },
                proficiency = new[] { "wis", "cha" },
                spellcastingAbility = "cha",
                casterProgression = "pact",
                classTableGroups = new object[]
                {
                    new
                    {
                        colLabels = new[] { "Cantrips Known", "Spell Slots", "Slot Level" },
                        rows = new object[][]
                        {
                            [2, 1, "{@filter 1st|spells|level=1|class=Pact Caster}"],
                            [2, 2, "{@filter 1st|spells|level=1|class=Pact Caster}"],
                            [2, 2, "{@filter 2nd|spells|level=2|class=Pact Caster}"],
                            [3, 2, "{@filter 2nd|spells|level=2|class=Pact Caster}"],
                            [3, 2, "{@filter 3rd|spells|level=3|class=Pact Caster}"]
                        }
                    }
                }
            });
            var fullRaw = JsonSerializer.Serialize(new
            {
                name = "Full Caster",
                source = sourceCode,
                hd = new { number = 1, faces = 6 },
                proficiency = new[] { "int", "wis" },
                spellcastingAbility = "int",
                casterProgression = "full",
                classTableGroups = new object[]
                {
                    new
                    {
                        title = "Spell Slots per Spell Level",
                        colLabels = new[] { "1st", "2nd", "3rd" },
                        rowsSpellProgression = new[]
                        {
                            new[] { 2, 0, 0 },
                            new[] { 3, 0, 0 },
                            new[] { 4, 2, 0 }
                        }
                    }
                }
            });

            var houseRaw = JsonSerializer.Serialize(new
            {
                name = "Spellcasting Resource Choice",
                source = sourceCode,
                category = "spellcasting",
                casterChoosesResourceSystem = true,
                availableResourceSystems = new[] { "spell slots", "spell points" }
            });

            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Character Pact Magic Fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    new NormalizedSourceRepresentation(
                        FiveEToolsSourceFormatAdapter.Format,
                        new SourceRepresentationArtifact(
                            $"pact-magic-{token}.json",
                            Encoding.UTF8.GetBytes("{}"),
                            $"integration:character-pact-magic:{token}"),
                        [
                            new NormalizedSourceRecord(
                                "class",
                                "Pact Caster",
                                sourceCode,
                                $"class|Pact Caster|{sourceCode}",
                                pactRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "class",
                                "Full Caster",
                                sourceCode,
                                $"class|Full Caster|{sourceCode}",
                                fullRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "houseRule",
                                "Spellcasting Resource Choice",
                                sourceCode,
                                $"houseRule|Spellcasting Resource Choice|{sourceCode}",
                                houseRaw,
                                PublicationLocalKey: sourceCode)
                        ],
                        [
                            new NormalizedSourcePublication(
                                sourceCode,
                                $"Character Pact Magic Fixture {token}",
                                "Integration Test Press",
                                "5e",
                                new DateOnly(2014, 8, 19))
                        ])));
            packageId = imported.PackageId;
            var byName = imported.Entities.ToDictionary(value => value.Name, StringComparer.Ordinal);
            var pactSource = byName["Pact Caster"];
            var fullSource = byName["Full Caster"];
            var houseSource = byName["Spellcasting Resource Choice"];

            var pactConcept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(
                    pactConceptKey,
                    pactSource.EntityType,
                    pactSource.Name),
                actor);
            var fullConcept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(
                    fullConceptKey,
                    fullSource.EntityType,
                    fullSource.Name),
                actor);
            var houseConcept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(
                    houseConceptKey,
                    houseSource.EntityType,
                    houseSource.Name),
                actor);
            await globalRules.BindSourceEntityAsync(
                pactConcept.Value.Id,
                new BindRuleConceptSourceRequest(pactSource.EntityId),
                actor);
            await globalRules.BindSourceEntityAsync(
                fullConcept.Value.Id,
                new BindRuleConceptSourceRequest(fullSource.EntityId),
                actor);
            await globalRules.BindSourceEntityAsync(
                houseConcept.Value.Id,
                new BindRuleConceptSourceRequest(houseSource.EntityId),
                actor);

            var revisionIds = await db.SourceEntityRevisions
                .Where(value =>
                    value.SourceEntityId == pactSource.EntityId
                    || value.SourceEntityId == fullSource.EntityId
                    || value.SourceEntityId == houseSource.EntityId)
                .ToDictionaryAsync(value => value.SourceEntityId, value => value.Id);
            await globalRules.SetDecisionAsync(
                pactConcept.Value.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionIds[pactSource.EntityId],
                    "Pact Magic table fixture."),
                actor);
            await globalRules.SetDecisionAsync(
                fullConcept.Value.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionIds[fullSource.EntityId],
                    "Standard spell slot table fixture."),
                actor);
            await globalRules.SetDecisionAsync(
                houseConcept.Value.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionIds[houseSource.EntityId],
                    "Published resource choice fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            var result = await projection.ResolveGlobalAsync(
                new CharacterRulesProjectionRequest(
                    BaseAbilityScores: new Dictionary<string, int>
                    {
                        ["strength"] = 8,
                        ["dexterity"] = 12,
                        ["constitution"] = 14,
                        ["intelligence"] = 18,
                        ["wisdom"] = 12,
                        ["charisma"] = 18
                    },
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(pactConceptKey, 5),
                        new CharacterAdvancementFactInput(fullConceptKey, 3)
                    ],
                    Choices:
                    [
                        new CharacterRuntimeChoiceInput(
                            "advancement.starting-class",
                            fullConceptKey),
                        new CharacterRuntimeChoiceInput(
                            "spellcasting.resource-system",
                            "spell-points")
                    ]),
                userId: null);

            var pactResource = Assert.Single(
                result.Resources,
                value => value.ResourceKey ==
                    $"resource.pact-slot.{pactConceptKey}.level-3");
            Assert.Equal(CharacterResolutionStates.Resolved, pactResource.State);
            Assert.Equal(2, pactResource.MaximumValue);

            Assert.Equal(14, Assert.Single(
                result.Resources,
                value => value.ResourceKey == "resource.spell-points").MaximumValue);
            Assert.DoesNotContain(
                result.Resources,
                value => value.ResourceKey.StartsWith(
                    "resource.spell-slot.",
                    StringComparison.Ordinal));
            Assert.DoesNotContain(
                result.Conflicts,
                value => value.ConflictKey == "conflict.spellcasting.multiclass-slots");

            var pactSpellcasting = Assert.Single(
                result.Spellcasting,
                value => value.SpellcastingKey == $"spellcasting.{pactConceptKey}");
            Assert.Equal(CharacterResolutionStates.Resolved, pactSpellcasting.State);
            Assert.Equal("pact-magic", pactSpellcasting.ResourceSystemKey);
            Assert.Contains(
                result.Capabilities,
                value => value.CapabilityKey == "spellcasting.pact");

            var standardSpellcasting = Assert.Single(
                result.Spellcasting,
                value => value.SpellcastingKey == $"spellcasting.{fullConceptKey}");
            Assert.Equal("spell-points", standardSpellcasting.ResourceSystemKey);
            Assert.Contains(
                result.Capabilities,
                value => value.CapabilityKey == "spellcasting.standard");
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
    public async Task CharacterProjectionCombinesStandardCasterProgressionsByEffectiveLevel()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-projection-multiclass-slots-{token}";
        var fullConceptKey = $"class.full-slot-caster-{token}";
        var halfConceptKey = $"class.half-slot-caster-{token}";
        var actor = $"character-multiclass-slots-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            var sourceCode = $"MCS{token}";
            var fullRows = new[]
            {
                new[] { 2, 0, 0 },
                new[] { 3, 0, 0 },
                new[] { 4, 2, 0 },
                new[] { 4, 3, 0 },
                new[] { 4, 3, 2 }
            };
            var halfRows = new[]
            {
                new[] { 0, 0, 0 },
                new[] { 2, 0, 0 },
                new[] { 3, 0, 0 },
                new[] { 3, 0, 0 },
                new[] { 4, 2, 0 },
                new[] { 4, 2, 0 },
                new[] { 4, 3, 0 },
                new[] { 4, 3, 0 },
                new[] { 4, 3, 2 },
                new[] { 4, 3, 2 }
            };
            var fullRaw = JsonSerializer.Serialize(new
            {
                name = "Full Slot Caster",
                source = sourceCode,
                hd = new { number = 1, faces = 6 },
                proficiency = new[] { "int", "wis" },
                spellcastingAbility = "int",
                casterProgression = "full",
                classTableGroups = new object[]
                {
                    new
                    {
                        title = "Spell Slots per Spell Level",
                        colLabels = new[] { "1st", "2nd", "3rd" },
                        rowsSpellProgression = fullRows
                    }
                }
            });
            var halfRaw = JsonSerializer.Serialize(new
            {
                name = "Half Slot Caster",
                source = sourceCode,
                hd = new { number = 1, faces = 10 },
                proficiency = new[] { "wis", "cha" },
                spellcastingAbility = "wis",
                casterProgression = "1/2",
                classTableGroups = new object[]
                {
                    new
                    {
                        title = "Spell Slots per Spell Level",
                        colLabels = new[] { "1st", "2nd", "3rd" },
                        rowsSpellProgression = halfRows
                    }
                }
            });

            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Character Multiclass Slot Fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    new NormalizedSourceRepresentation(
                        FiveEToolsSourceFormatAdapter.Format,
                        new SourceRepresentationArtifact(
                            $"multiclass-slots-{token}.json",
                            Encoding.UTF8.GetBytes("{}"),
                            $"integration:character-multiclass-slots:{token}"),
                        [
                            new NormalizedSourceRecord(
                                "class",
                                "Full Slot Caster",
                                sourceCode,
                                $"class|Full Slot Caster|{sourceCode}",
                                fullRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "class",
                                "Half Slot Caster",
                                sourceCode,
                                $"class|Half Slot Caster|{sourceCode}",
                                halfRaw,
                                PublicationLocalKey: sourceCode)
                        ],
                        [
                            new NormalizedSourcePublication(
                                sourceCode,
                                $"Character Multiclass Slot Fixture {token}",
                                "Integration Test Press",
                                "5e",
                                new DateOnly(2014, 8, 19))
                        ])));
            packageId = imported.PackageId;
            var byName = imported.Entities.ToDictionary(value => value.Name, StringComparer.Ordinal);
            var fullSource = byName["Full Slot Caster"];
            var halfSource = byName["Half Slot Caster"];

            var fullConcept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(
                    fullConceptKey,
                    fullSource.EntityType,
                    fullSource.Name),
                actor);
            var halfConcept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(
                    halfConceptKey,
                    halfSource.EntityType,
                    halfSource.Name),
                actor);
            await globalRules.BindSourceEntityAsync(
                fullConcept.Value.Id,
                new BindRuleConceptSourceRequest(fullSource.EntityId),
                actor);
            await globalRules.BindSourceEntityAsync(
                halfConcept.Value.Id,
                new BindRuleConceptSourceRequest(halfSource.EntityId),
                actor);

            var revisionIds = await db.SourceEntityRevisions
                .Where(value =>
                    value.SourceEntityId == fullSource.EntityId
                    || value.SourceEntityId == halfSource.EntityId)
                .ToDictionaryAsync(value => value.SourceEntityId, value => value.Id);
            await globalRules.SetDecisionAsync(
                fullConcept.Value.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionIds[fullSource.EntityId],
                    "Full caster slot table fixture."),
                actor);
            await globalRules.SetDecisionAsync(
                halfConcept.Value.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionIds[halfSource.EntityId],
                    "Half caster slot table fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            var result = await projection.ResolveGlobalAsync(
                new CharacterRulesProjectionRequest(
                    BaseAbilityScores: new Dictionary<string, int>
                    {
                        ["strength"] = 10,
                        ["dexterity"] = 12,
                        ["constitution"] = 14,
                        ["intelligence"] = 18,
                        ["wisdom"] = 16,
                        ["charisma"] = 10
                    },
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(fullConceptKey, 3),
                        new CharacterAdvancementFactInput(halfConceptKey, 4)
                    ],
                    Choices:
                    [
                        new CharacterRuntimeChoiceInput(
                            "advancement.starting-class",
                            fullConceptKey)
                    ]),
                userId: null);

            Assert.Equal(4, Assert.Single(
                result.Resources,
                value => value.ResourceKey == "resource.spell-slot.1").MaximumValue);
            Assert.Equal(3, Assert.Single(
                result.Resources,
                value => value.ResourceKey == "resource.spell-slot.2").MaximumValue);
            Assert.Equal(2, Assert.Single(
                result.Resources,
                value => value.ResourceKey == "resource.spell-slot.3").MaximumValue);
            Assert.DoesNotContain(
                result.Conflicts,
                value => value.ConflictKey == "conflict.spellcasting.multiclass-slots");

            var firstLevel = Assert.Single(
                result.Resources,
                value => value.ResourceKey == "resource.spell-slot.1");
            Assert.Contains(
                firstLevel.MaximumContributions,
                value => value.ContributionKey ==
                    $"{fullConceptKey}.effective-caster-level"
                    && value.NumericValue == 3);
            Assert.Contains(
                firstLevel.MaximumContributions,
                value => value.ContributionKey ==
                    $"{halfConceptKey}.effective-caster-level"
                    && value.NumericValue == 2);
            Assert.Contains(
                firstLevel.MaximumContributions,
                value => value.TextValue == "effective-caster-level:5");
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
    public async Task CharacterProjectionResolvesNativeSubclassFeatureLevels()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-projection-subclass-{token}";
        var conceptKey = $"subclass.arcane-path-{token}";
        var actor = $"character-subclass-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            var sourceCode = $"SUB{token}";
            var raw = JsonSerializer.Serialize(new
            {
                name = "Arcane Path",
                source = sourceCode,
                className = "Example Mage",
                classSource = sourceCode,
                subclassFeatures = new object[]
                {
                    $"Path Initiate|Example Mage|{sourceCode}|Arcane Path|{sourceCode}|3",
                    new
                    {
                        subclassFeature =
                            $"Path Adept|Example Mage|{sourceCode}|Arcane Path|{sourceCode}|6"
                    },
                    $"Path Master|Example Mage|{sourceCode}|Arcane Path|{sourceCode}|10"
                }
            });
            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Character Subclass Fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    new NormalizedSourceRepresentation(
                        FiveEToolsSourceFormatAdapter.Format,
                        new SourceRepresentationArtifact(
                            $"subclass-{token}.json",
                            Encoding.UTF8.GetBytes(raw),
                            $"integration:character-subclass:{token}"),
                        [
                            new NormalizedSourceRecord(
                                "subclass",
                                "Arcane Path",
                                sourceCode,
                                $"subclass|Arcane Path|{sourceCode}",
                                raw,
                                PublicationLocalKey: sourceCode)
                        ],
                        [
                            new NormalizedSourcePublication(
                                sourceCode,
                                $"Character Subclass Fixture {token}",
                                "Integration Test Press",
                                "5e",
                                new DateOnly(2014, 8, 19))
                        ])));
            packageId = imported.PackageId;
            var source = Assert.Single(imported.Entities);

            var concept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(
                    conceptKey,
                    source.EntityType,
                    source.Name),
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
                    "Native subclass feature fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            var result = await projection.ResolveGlobalAsync(
                new CharacterRulesProjectionRequest(
                    BaseAbilityScores: new Dictionary<string, int>
                    {
                        ["strength"] = 10,
                        ["dexterity"] = 10,
                        ["constitution"] = 10,
                        ["intelligence"] = 16,
                        ["wisdom"] = 10,
                        ["charisma"] = 10
                    },
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(conceptKey, 7)
                    ]),
                userId: null);

            var pathInitiate = Assert.Single(
                result.Features,
                value => value.DisplayName == "Path Initiate"
                    && value.State == CharacterResolutionStates.Resolved);
            Assert.Equal("subclass", pathInitiate.GrantingSourceKind);
            Assert.Equal(3, pathInitiate.AcquisitionLevel);
            Assert.Equal(conceptKey, pathInitiate.SourceConceptKey);

            var pathAdept = Assert.Single(
                result.Features,
                value => value.DisplayName == "Path Adept"
                    && value.State == CharacterResolutionStates.Resolved);
            Assert.Equal("subclass", pathAdept.GrantingSourceKind);
            Assert.Equal(6, pathAdept.AcquisitionLevel);
            Assert.DoesNotContain(
                result.Features,
                value => value.DisplayName == "Path Master");
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
    public async Task CharacterProjectionExposesClassSkillChoiceOptionsAndAppliesSelectedTraining()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-projection-skill-choice-{token}";
        var classConceptKey = $"class.skill-choice-{token}";
        var arcanaConceptKey = $"skill.arcana-{token}";
        var historyConceptKey = $"skill.history-{token}";
        var actor = $"character-skill-choice-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            var sourceCode = $"SKL{token}";
            var classRaw = JsonSerializer.Serialize(new
            {
                name = "Scholar",
                source = sourceCode,
                hd = new { number = 1, faces = 8 },
                proficiency = new[] { "int", "wis" },
                startingProficiencies = new
                {
                    skills = new object[]
                    {
                        new
                        {
                            choose = new
                            {
                                from = new[] { "arcana", "history" },
                                count = 1
                            }
                        }
                    }
                }
            });
            var arcanaRaw = JsonSerializer.Serialize(new
            {
                name = "Arcana",
                source = sourceCode,
                ability = "int"
            });
            var historyRaw = JsonSerializer.Serialize(new
            {
                name = "History",
                source = sourceCode,
                ability = "int"
            });

            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Character Skill Choice Fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    new NormalizedSourceRepresentation(
                        FiveEToolsSourceFormatAdapter.Format,
                        new SourceRepresentationArtifact(
                            $"skill-choice-{token}.json",
                            Encoding.UTF8.GetBytes("{}"),
                            $"integration:character-skill-choice:{token}"),
                        [
                            new NormalizedSourceRecord(
                                "class",
                                "Scholar",
                                sourceCode,
                                $"class|Scholar|{sourceCode}",
                                classRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "skill",
                                "Arcana",
                                sourceCode,
                                $"skill|Arcana|{sourceCode}",
                                arcanaRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "skill",
                                "History",
                                sourceCode,
                                $"skill|History|{sourceCode}",
                                historyRaw,
                                PublicationLocalKey: sourceCode)
                        ],
                        [
                            new NormalizedSourcePublication(
                                sourceCode,
                                $"Character Skill Choice Fixture {token}",
                                "Integration Test Press",
                                "5e",
                                new DateOnly(2014, 8, 19))
                        ])));
            packageId = imported.PackageId;
            var byName = imported.Entities.ToDictionary(value => value.Name, StringComparer.Ordinal);
            var classSource = byName["Scholar"];
            var arcanaSource = byName["Arcana"];
            var historySource = byName["History"];

            var classConcept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(
                    classConceptKey,
                    classSource.EntityType,
                    classSource.Name),
                actor);
            var arcanaConcept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(
                    arcanaConceptKey,
                    arcanaSource.EntityType,
                    arcanaSource.Name),
                actor);
            var historyConcept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(
                    historyConceptKey,
                    historySource.EntityType,
                    historySource.Name),
                actor);

            foreach (var pair in new[]
                     {
                         (ConceptId: classConcept.Value.Id, EntityId: classSource.EntityId),
                         (ConceptId: arcanaConcept.Value.Id, EntityId: arcanaSource.EntityId),
                         (ConceptId: historyConcept.Value.Id, EntityId: historySource.EntityId)
                     })
            {
                await globalRules.BindSourceEntityAsync(
                    pair.ConceptId,
                    new BindRuleConceptSourceRequest(pair.EntityId),
                    actor);
            }

            var revisionIds = await db.SourceEntityRevisions
                .Where(value =>
                    value.SourceEntityId == classSource.EntityId
                    || value.SourceEntityId == arcanaSource.EntityId
                    || value.SourceEntityId == historySource.EntityId)
                .ToDictionaryAsync(value => value.SourceEntityId, value => value.Id);
            foreach (var pair in new[]
                     {
                         (ConceptId: classConcept.Value.Id, EntityId: classSource.EntityId),
                         (ConceptId: arcanaConcept.Value.Id, EntityId: arcanaSource.EntityId),
                         (ConceptId: historyConcept.Value.Id, EntityId: historySource.EntityId)
                     })
            {
                await globalRules.SetDecisionAsync(
                    pair.ConceptId,
                    new SetGlobalRuleDecisionRequest(
                        revisionIds[pair.EntityId],
                        "Class skill choice fixture."),
                    actor);
            }
            await globalRules.PublishAsync(actor);

            CharacterRulesProjectionRequest Request(
                IReadOnlyList<CharacterRuntimeChoiceInput>? choices = null) =>
                new(
                    BaseAbilityScores: new Dictionary<string, int>
                    {
                        ["strength"] = 10,
                        ["dexterity"] = 12,
                        ["constitution"] = 12,
                        ["intelligence"] = 18,
                        ["wisdom"] = 12,
                        ["charisma"] = 10
                    },
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(classConceptKey, 5)
                    ],
                    Choices: choices);

            var unresolved = await projection.ResolveGlobalAsync(
                Request(),
                userId: null);
            var choice = Assert.Single(
                unresolved.Choices,
                value => value.Kind == "skill-proficiency");
            Assert.Equal(CharacterResolutionStates.ChoiceRequired, choice.State);
            Assert.Equal(2, choice.Options.Count);
            var arcanaOption = Assert.Single(
                choice.Options,
                value => value.ConceptKey == arcanaConceptKey);
            Assert.Contains(
                choice.Options,
                value => value.ConceptKey == historyConceptKey);

            var selected = await projection.ResolveGlobalAsync(
                Request(
                [
                    new CharacterRuntimeChoiceInput(
                        choice.ChoiceKey,
                        arcanaOption.Value)
                ]),
                userId: null);

            var resolvedChoice = Assert.Single(
                selected.Choices,
                value => value.ChoiceKey == choice.ChoiceKey);
            Assert.Equal(CharacterResolutionStates.Resolved, resolvedChoice.State);
            Assert.Equal(arcanaOption.Value, resolvedChoice.SelectedValue);

            var arcana = Assert.Single(
                selected.Mechanics,
                value => value.MechanicKey == $"competency.{arcanaConceptKey}");
            Assert.Equal(CharacterResolutionStates.Resolved, arcana.State);
            Assert.Equal(7, arcana.NumericValue);
            Assert.Contains(
                selected.Qualifications,
                value => value.Category == "skills"
                    && value.DisplayName == "Arcana"
                    && value.IsQualified == true);
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
    public async Task CharacterProjectionAppliesSelectedFeatSkillProficiencies()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-projection-feat-skills-{token}";
        var classConceptKey = $"class.feat-skill-user-{token}";
        var featConceptKey = $"feat.skill-training-{token}";
        var arcanaConceptKey = $"skill.arcana-feat-{token}";
        var historyConceptKey = $"skill.history-feat-{token}";
        var actor = $"character-feat-skills-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            var sourceCode = $"FSP{token}";
            var classRaw = JsonSerializer.Serialize(new
            {
                name = "Feat Skill User",
                source = sourceCode,
                hd = new { number = 1, faces = 8 },
                proficiency = new[] { "int", "wis" }
            });
            var featRaw = JsonSerializer.Serialize(new
            {
                name = "Skill Training",
                source = sourceCode,
                skillProficiencies = new object[]
                {
                    new Dictionary<string, object> { ["history"] = true },
                    new
                    {
                        choose = new
                        {
                            from = new[] { "arcana" },
                            count = 1
                        }
                    }
                }
            });
            var arcanaRaw = JsonSerializer.Serialize(new
            {
                name = "Arcana",
                source = sourceCode,
                ability = "int"
            });
            var historyRaw = JsonSerializer.Serialize(new
            {
                name = "History",
                source = sourceCode,
                ability = "int"
            });

            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Feat Skill Projection Fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    new NormalizedSourceRepresentation(
                        FiveEToolsSourceFormatAdapter.Format,
                        new SourceRepresentationArtifact(
                            $"feat-skills-{token}.json",
                            Encoding.UTF8.GetBytes("{}"),
                            $"integration:character-feat-skills:{token}"),
                        [
                            new NormalizedSourceRecord(
                                "class",
                                "Feat Skill User",
                                sourceCode,
                                $"class|Feat Skill User|{sourceCode}",
                                classRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "feat",
                                "Skill Training",
                                sourceCode,
                                $"feat|Skill Training|{sourceCode}",
                                featRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "skill",
                                "Arcana",
                                sourceCode,
                                $"skill|Arcana|{sourceCode}",
                                arcanaRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "skill",
                                "History",
                                sourceCode,
                                $"skill|History|{sourceCode}",
                                historyRaw,
                                PublicationLocalKey: sourceCode)
                        ],
                        [
                            new NormalizedSourcePublication(
                                sourceCode,
                                $"Feat Skill Projection Fixture {token}",
                                "Integration Test Press",
                                "5e",
                                new DateOnly(2014, 8, 19))
                        ])));
            packageId = imported.PackageId;
            var byName = imported.Entities.ToDictionary(value => value.Name, StringComparer.Ordinal);
            var definitions = new[]
            {
                (Key: classConceptKey, Entity: byName["Feat Skill User"]),
                (Key: featConceptKey, Entity: byName["Skill Training"]),
                (Key: arcanaConceptKey, Entity: byName["Arcana"]),
                (Key: historyConceptKey, Entity: byName["History"])
            };
            var concepts = new Dictionary<string, RuleConceptView>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                var concept = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(
                        definition.Key,
                        definition.Entity.EntityType,
                        definition.Entity.Name),
                    actor);
                concepts[definition.Key] = concept.Value;
                await globalRules.BindSourceEntityAsync(
                    concept.Value.Id,
                    new BindRuleConceptSourceRequest(definition.Entity.EntityId),
                    actor);
            }

            var entityIds = definitions.Select(value => value.Entity.EntityId).ToArray();
            var revisionIds = await db.SourceEntityRevisions
                .Where(value => entityIds.Contains(value.SourceEntityId))
                .ToDictionaryAsync(value => value.SourceEntityId, value => value.Id);
            foreach (var definition in definitions)
            {
                await globalRules.SetDecisionAsync(
                    concepts[definition.Key].Id,
                    new SetGlobalRuleDecisionRequest(
                        revisionIds[definition.Entity.EntityId],
                        "Source-level skill proficiency fixture."),
                    actor);
            }
            await globalRules.PublishAsync(actor);

            CharacterRulesProjectionRequest Request(
                IReadOnlyList<CharacterRuntimeChoiceInput>? choices = null) =>
                new(
                    BaseAbilityScores: new Dictionary<string, int>
                    {
                        ["strength"] = 10,
                        ["dexterity"] = 12,
                        ["constitution"] = 12,
                        ["intelligence"] = 18,
                        ["wisdom"] = 12,
                        ["charisma"] = 10
                    },
                    SelectedConcepts:
                    [
                        new CharacterSelectedConceptInput(featConceptKey)
                    ],
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(classConceptKey, 5)
                    ],
                    Choices: choices);

            var unresolved = await projection.ResolveGlobalAsync(
                Request(),
                userId: null);
            var choice = Assert.Single(
                unresolved.Choices,
                value => value.SourceConceptKey == featConceptKey
                    && value.Kind == "skill-proficiency");
            Assert.Equal(CharacterResolutionStates.ChoiceRequired, choice.State);

            var history = Assert.Single(
                unresolved.Mechanics,
                value => value.MechanicKey == $"competency.{historyConceptKey}");
            Assert.Equal(CharacterResolutionStates.Resolved, history.State);
            Assert.Equal(7, history.NumericValue);

            var arcanaOption = Assert.Single(
                choice.Options,
                value => value.ConceptKey == arcanaConceptKey);
            var selected = await projection.ResolveGlobalAsync(
                Request(
                [
                    new CharacterRuntimeChoiceInput(
                        choice.ChoiceKey,
                        arcanaOption.Value)
                ]),
                userId: null);

            var arcana = Assert.Single(
                selected.Mechanics,
                value => value.MechanicKey == $"competency.{arcanaConceptKey}");
            Assert.Equal(CharacterResolutionStates.Resolved, arcana.State);
            Assert.Equal(7, arcana.NumericValue);
            Assert.Equal(
                CharacterResolutionStates.Resolved,
                Assert.Single(
                    selected.Choices,
                    value => value.ChoiceKey == choice.ChoiceKey).State);
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
    public async Task CharacterProjectionUsesEffectiveLanguageChoicesAndExcludesKnownLanguages()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-projection-languages-{token}";
        var backgroundConceptKey = $"background.languages-{token}";
        var commonConceptKey = $"language.common-{token}";
        var elvishConceptKey = $"language.elvish-{token}";
        var abyssalConceptKey = $"language.abyssal-{token}";
        var actor = $"character-languages-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            var sourceCode = $"LNG{token}";
            var backgroundRaw = JsonSerializer.Serialize(new
            {
                name = "Language Background",
                source = sourceCode,
                languageProficiencies = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["anyStandard"] = 1,
                        ["common"] = true,
                        ["anyExotic"] = 1
                    }
                }
            });
            var commonRaw = JsonSerializer.Serialize(new
            {
                name = "Common",
                source = sourceCode,
                type = "standard"
            });
            var elvishRaw = JsonSerializer.Serialize(new
            {
                name = "Elvish",
                source = sourceCode,
                type = "standard"
            });
            var abyssalRaw = JsonSerializer.Serialize(new
            {
                name = "Abyssal",
                source = sourceCode,
                type = "exotic"
            });

            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Character Language Fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    new NormalizedSourceRepresentation(
                        FiveEToolsSourceFormatAdapter.Format,
                        new SourceRepresentationArtifact(
                            $"languages-{token}.json",
                            Encoding.UTF8.GetBytes("{}"),
                            $"integration:character-languages:{token}"),
                        [
                            new NormalizedSourceRecord(
                                "background",
                                "Language Background",
                                sourceCode,
                                $"background|Language Background|{sourceCode}",
                                backgroundRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "language",
                                "Common",
                                sourceCode,
                                $"language|Common|{sourceCode}",
                                commonRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "language",
                                "Elvish",
                                sourceCode,
                                $"language|Elvish|{sourceCode}",
                                elvishRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "language",
                                "Abyssal",
                                sourceCode,
                                $"language|Abyssal|{sourceCode}",
                                abyssalRaw,
                                PublicationLocalKey: sourceCode)
                        ],
                        [
                            new NormalizedSourcePublication(
                                sourceCode,
                                $"Character Language Fixture {token}",
                                "Integration Test Press",
                                "5e",
                                new DateOnly(2014, 8, 19))
                        ])));
            packageId = imported.PackageId;
            var byName = imported.Entities.ToDictionary(value => value.Name, StringComparer.Ordinal);
            var definitions = new[]
            {
                (Key: backgroundConceptKey, Entity: byName["Language Background"]),
                (Key: commonConceptKey, Entity: byName["Common"]),
                (Key: elvishConceptKey, Entity: byName["Elvish"]),
                (Key: abyssalConceptKey, Entity: byName["Abyssal"])
            };
            var concepts = new Dictionary<string, RuleConceptView>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                var created = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(
                        definition.Key,
                        definition.Entity.EntityType,
                        definition.Entity.Name),
                    actor);
                concepts[definition.Key] = created.Value;
                await globalRules.BindSourceEntityAsync(
                    created.Value.Id,
                    new BindRuleConceptSourceRequest(definition.Entity.EntityId),
                    actor);
            }

            var entityIds = definitions.Select(value => value.Entity.EntityId).ToArray();
            var revisionIds = await db.SourceEntityRevisions
                .Where(value => entityIds.Contains(value.SourceEntityId))
                .ToDictionaryAsync(value => value.SourceEntityId, value => value.Id);
            foreach (var definition in definitions)
            {
                await globalRules.SetDecisionAsync(
                    concepts[definition.Key].Id,
                    new SetGlobalRuleDecisionRequest(
                        revisionIds[definition.Entity.EntityId],
                        "Language proficiency fixture."),
                    actor);
            }
            await globalRules.PublishAsync(actor);

            CharacterRulesProjectionRequest Request(
                IReadOnlyList<CharacterRuntimeChoiceInput>? choices = null) =>
                new(
                    SelectedConcepts:
                    [
                        new CharacterSelectedConceptInput(backgroundConceptKey)
                    ],
                    Choices: choices);

            var unresolved = await projection.ResolveGlobalAsync(
                Request(),
                userId: null);

            Assert.Contains(
                unresolved.Qualifications,
                value => value.QualificationKey == "qualification.languages.common"
                    && value.IsQualified == true
                    && value.State == CharacterResolutionStates.Resolved);

            var standardChoice = Assert.Single(
                unresolved.Choices,
                value => value.Kind == "language-proficiency"
                    && value.ChoiceKey.Contains(
                        "anystandard",
                        StringComparison.OrdinalIgnoreCase));
            Assert.Equal(CharacterResolutionStates.ChoiceRequired, standardChoice.State);
            var elvishOption = Assert.Single(standardChoice.Options);
            Assert.Equal(elvishConceptKey, elvishOption.ConceptKey);
            Assert.DoesNotContain(
                standardChoice.Options,
                value => value.ConceptKey == commonConceptKey);

            var exoticChoice = Assert.Single(
                unresolved.Choices,
                value => value.Kind == "language-proficiency"
                    && value.ChoiceKey.Contains(
                        "anyexotic",
                        StringComparison.OrdinalIgnoreCase));
            Assert.Equal(CharacterResolutionStates.ChoiceRequired, exoticChoice.State);
            var abyssalOption = Assert.Single(exoticChoice.Options);
            Assert.Equal(abyssalConceptKey, abyssalOption.ConceptKey);

            var selected = await projection.ResolveGlobalAsync(
                Request(
                [
                    new CharacterRuntimeChoiceInput(
                        standardChoice.ChoiceKey,
                        elvishOption.Value),
                    new CharacterRuntimeChoiceInput(
                        exoticChoice.ChoiceKey,
                        abyssalOption.Value)
                ]),
                userId: null);

            Assert.Contains(
                selected.Qualifications,
                value => value.QualificationKey == "qualification.languages.elvish"
                    && value.IsQualified == true);
            Assert.Contains(
                selected.Qualifications,
                value => value.QualificationKey == "qualification.languages.abyssal"
                    && value.IsQualified == true);
            Assert.Equal(
                CharacterResolutionStates.Resolved,
                Assert.Single(
                    selected.Choices,
                    value => value.ChoiceKey == standardChoice.ChoiceKey).State);
            Assert.Equal(
                CharacterResolutionStates.Resolved,
                Assert.Single(
                    selected.Choices,
                    value => value.ChoiceKey == exoticChoice.ChoiceKey).State);
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
    public async Task CharacterProjectionUsesStructuredWeaponArmorAndToolProficiencies()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-projection-structured-profs-{token}";
        var classConceptKey = $"class.structured-profs-{token}";
        var thievesConceptKey = $"tool.thieves-tools-{token}";
        var herbalismConceptKey = $"tool.herbalism-kit-{token}";
        var smithConceptKey = $"tool.smiths-tools-{token}";
        var lightWeaponConceptKey = $"item.martial-light-blade-{token}";
        var heavyWeaponConceptKey = $"item.martial-heavy-blade-{token}";
        var actor = $"character-structured-profs-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            var sourceCode = $"PRF{token}";
            var classRaw = JsonSerializer.Serialize(new
            {
                name = "Structured Proficiency Class",
                source = sourceCode,
                hd = new { number = 1, faces = 8 },
                proficiency = new[] { "dex", "wis" },
                startingProficiencies = new
                {
                    weaponProficiencies = new object[]
                    {
                        new
                        {
                            simple = true,
                            all = new { fromFilter = "type=martial weapon|property=light;finesse" }
                        }
                    },
                    armorProficiencies = new object[]
                    {
                        new { light = true, shield = true }
                    },
                    toolProficiencies = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["thieves' tools"] = true,
                            ["anyTool"] = 1,
                            ["anyArtisansTool"] = 1
                        }
                    }
                }
            });
            var thievesRaw = JsonSerializer.Serialize(new
            {
                name = "Thieves' Tools",
                source = sourceCode,
                type = "T",
                ability = "dex"
            });
            var herbalismRaw = JsonSerializer.Serialize(new
            {
                name = "Herbalism Kit",
                source = sourceCode,
                type = "T",
                ability = "wis"
            });
            var smithRaw = JsonSerializer.Serialize(new
            {
                name = "Smith's Tools",
                source = sourceCode,
                type = "AT",
                ability = "str"
            });
            var lightWeaponRaw = JsonSerializer.Serialize(new
            {
                name = "Martial Light Blade",
                source = sourceCode,
                type = "M",
                weaponCategory = "martial",
                property = new[] { "L" },
                dmg1 = "1d6",
                dmgType = "S"
            });
            var heavyWeaponRaw = JsonSerializer.Serialize(new
            {
                name = "Martial Heavy Blade",
                source = sourceCode,
                type = "M",
                weaponCategory = "martial",
                property = new[] { "H" },
                dmg1 = "1d10",
                dmgType = "S"
            });

            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Structured Proficiency Fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    new NormalizedSourceRepresentation(
                        FiveEToolsSourceFormatAdapter.Format,
                        new SourceRepresentationArtifact(
                            $"structured-profs-{token}.json",
                            Encoding.UTF8.GetBytes("{}"),
                            $"integration:structured-profs:{token}"),
                        [
                            new NormalizedSourceRecord(
                                "class",
                                "Structured Proficiency Class",
                                sourceCode,
                                $"class|Structured Proficiency Class|{sourceCode}",
                                classRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "tool",
                                "Thieves' Tools",
                                sourceCode,
                                $"tool|Thieves' Tools|{sourceCode}",
                                thievesRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "tool",
                                "Herbalism Kit",
                                sourceCode,
                                $"tool|Herbalism Kit|{sourceCode}",
                                herbalismRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "tool",
                                "Smith's Tools",
                                sourceCode,
                                $"tool|Smith's Tools|{sourceCode}",
                                smithRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "item",
                                "Martial Light Blade",
                                sourceCode,
                                $"item|Martial Light Blade|{sourceCode}",
                                lightWeaponRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "item",
                                "Martial Heavy Blade",
                                sourceCode,
                                $"item|Martial Heavy Blade|{sourceCode}",
                                heavyWeaponRaw,
                                PublicationLocalKey: sourceCode)
                        ],
                        [
                            new NormalizedSourcePublication(
                                sourceCode,
                                $"Structured Proficiency Fixture {token}",
                                "Integration Test Press",
                                "5e",
                                new DateOnly(2014, 8, 19))
                        ])));
            packageId = imported.PackageId;
            var byName = imported.Entities.ToDictionary(value => value.Name, StringComparer.Ordinal);
            var definitions = new[]
            {
                (Key: classConceptKey, Entity: byName["Structured Proficiency Class"]),
                (Key: thievesConceptKey, Entity: byName["Thieves' Tools"]),
                (Key: herbalismConceptKey, Entity: byName["Herbalism Kit"]),
                (Key: smithConceptKey, Entity: byName["Smith's Tools"]),
                (Key: lightWeaponConceptKey, Entity: byName["Martial Light Blade"]),
                (Key: heavyWeaponConceptKey, Entity: byName["Martial Heavy Blade"])
            };
            var concepts = new Dictionary<string, RuleConceptView>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                var created = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(
                        definition.Key,
                        definition.Entity.EntityType,
                        definition.Entity.Name),
                    actor);
                concepts[definition.Key] = created.Value;
                await globalRules.BindSourceEntityAsync(
                    created.Value.Id,
                    new BindRuleConceptSourceRequest(definition.Entity.EntityId),
                    actor);
            }

            var entityIds = definitions.Select(value => value.Entity.EntityId).ToArray();
            var revisionIds = await db.SourceEntityRevisions
                .Where(value => entityIds.Contains(value.SourceEntityId))
                .ToDictionaryAsync(value => value.SourceEntityId, value => value.Id);
            foreach (var definition in definitions)
            {
                await globalRules.SetDecisionAsync(
                    concepts[definition.Key].Id,
                    new SetGlobalRuleDecisionRequest(
                        revisionIds[definition.Entity.EntityId],
                        "Structured proficiency fixture."),
                    actor);
            }
            await globalRules.PublishAsync(actor);

            CharacterRulesProjectionRequest Request(
                IReadOnlyList<CharacterRuntimeChoiceInput>? choices = null) =>
                new(
                    BaseAbilityScores: new Dictionary<string, int>
                    {
                        ["strength"] = 10,
                        ["dexterity"] = 14,
                        ["constitution"] = 12,
                        ["intelligence"] = 10,
                        ["wisdom"] = 12,
                        ["charisma"] = 10
                    },
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(classConceptKey, 5)
                    ],
                    EquippedItemConceptKeys:
                    [
                        lightWeaponConceptKey
                    ],
                    Choices: choices);

            var unresolved = await projection.ResolveGlobalAsync(
                Request(),
                userId: null);

            Assert.Contains(
                unresolved.Qualifications,
                value => value.QualificationKey == "qualification.weapons.simple"
                    && value.IsQualified == true);
            Assert.Contains(
                unresolved.Qualifications,
                value => value.QualificationKey == "qualification.armor.light"
                    && value.IsQualified == true);
            Assert.Contains(
                unresolved.Qualifications,
                value => value.QualificationKey == "qualification.armor.shield"
                    && value.IsQualified == true);
            Assert.Contains(
                unresolved.Qualifications,
                value => value.QualificationKey == "qualification.weapons.martial-light-blade"
                    && value.IsQualified == true);
            Assert.DoesNotContain(
                unresolved.Qualifications,
                value => value.QualificationKey == "qualification.weapons.martial-heavy-blade");
            Assert.DoesNotContain(
                unresolved.Qualifications,
                value => value.QualificationKey.StartsWith(
                    "qualification.weapons.source-expression.",
                    StringComparison.Ordinal));

            var weaponAttack = Assert.Single(
                unresolved.Mechanics,
                value => value.MechanicKey == $"attack.{lightWeaponConceptKey}");
            Assert.Equal(CharacterResolutionStates.Resolved, weaponAttack.State);
            Assert.Equal(3, weaponAttack.NumericValue);

            var thieves = Assert.Single(
                unresolved.Mechanics,
                value => value.MechanicKey == $"competency.{thievesConceptKey}");
            Assert.Equal(CharacterResolutionStates.Resolved, thieves.State);
            Assert.Equal(5, thieves.NumericValue);

            var anyTool = Assert.Single(
                unresolved.Choices,
                value => value.Kind == "tool-proficiency"
                    && value.ChoiceKey.Contains("anytool", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(CharacterResolutionStates.ChoiceRequired, anyTool.State);
            Assert.Equal(3, anyTool.Options.Count);
            var herbalismOption = Assert.Single(
                anyTool.Options,
                value => value.ConceptKey == herbalismConceptKey);

            var artisan = Assert.Single(
                unresolved.Choices,
                value => value.Kind == "tool-proficiency"
                    && value.ChoiceKey.Contains(
                        "anyartisanstool",
                        StringComparison.OrdinalIgnoreCase));
            Assert.Equal(CharacterResolutionStates.ChoiceRequired, artisan.State);
            var smithOption = Assert.Single(
                artisan.Options,
                value => value.ConceptKey == smithConceptKey);

            var selected = await projection.ResolveGlobalAsync(
                Request(
                [
                    new CharacterRuntimeChoiceInput(
                        anyTool.ChoiceKey,
                        herbalismOption.Value),
                    new CharacterRuntimeChoiceInput(
                        artisan.ChoiceKey,
                        smithOption.Value)
                ]),
                userId: null);
            var herbalism = Assert.Single(
                selected.Mechanics,
                value => value.MechanicKey == $"competency.{herbalismConceptKey}");
            Assert.Equal(CharacterResolutionStates.Resolved, herbalism.State);
            Assert.Equal(4, herbalism.NumericValue);
            Assert.Contains(
                selected.Qualifications,
                value => value.Category == "tools"
                    && value.DisplayName == "Herbalism Kit"
                    && value.IsQualified == true);
            Assert.Contains(
                selected.Qualifications,
                value => value.Category == "tools"
                    && value.DisplayName == "Smith's Tools"
                    && value.IsQualified == true);
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
    public async Task CharacterProjectionKeepsAlternateWeightedAbilitySetsMutuallyExclusive()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-projection-ability-choice-{token}";
        var conceptKey = $"background.ability-choice-{token}";
        var actor = $"character-ability-choice-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            var sourceCode = $"ABI{token}";
            var raw = JsonSerializer.Serialize(new
            {
                name = "Flexible Background",
                source = sourceCode,
                ability = new object[]
                {
                    new
                    {
                        choose = new
                        {
                            weighted = new
                            {
                                from = new[] { "str", "dex", "con" },
                                weights = new[] { 2, 1 }
                            }
                        }
                    },
                    new
                    {
                        choose = new
                        {
                            weighted = new
                            {
                                from = new[] { "str", "dex", "con" },
                                weights = new[] { 1, 1, 1 }
                            }
                        }
                    }
                }
            });

            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Ability Choice Fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    new NormalizedSourceRepresentation(
                        FiveEToolsSourceFormatAdapter.Format,
                        new SourceRepresentationArtifact(
                            $"ability-choice-{token}.json",
                            Encoding.UTF8.GetBytes("{}"),
                            $"integration:ability-choice:{token}"),
                        [
                            new NormalizedSourceRecord(
                                "background",
                                "Flexible Background",
                                sourceCode,
                                $"background|Flexible Background|{sourceCode}",
                                raw,
                                PublicationLocalKey: sourceCode)
                        ],
                        [
                            new NormalizedSourcePublication(
                                sourceCode,
                                $"Ability Choice Fixture {token}",
                                "Integration Test Press",
                                "5e",
                                new DateOnly(2024, 9, 17))
                        ])));
            packageId = imported.PackageId;
            var source = Assert.Single(imported.Entities);
            var concept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(
                    conceptKey,
                    source.EntityType,
                    source.Name),
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
                    "Weighted ability choice fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            CharacterRulesProjectionRequest Request(
                IReadOnlyList<CharacterRuntimeChoiceInput>? choices = null) =>
                new(
                    BaseAbilityScores: new Dictionary<string, int>
                    {
                        ["strength"] = 10,
                        ["dexterity"] = 10,
                        ["constitution"] = 10,
                        ["intelligence"] = 10,
                        ["wisdom"] = 10,
                        ["charisma"] = 10
                    },
                    SelectedConcepts:
                    [
                        new CharacterSelectedConceptInput(conceptKey)
                    ],
                    Choices: choices);

            var noSet = await projection.ResolveGlobalAsync(
                Request(),
                userId: null);
            var setChoice = Assert.Single(
                noSet.Choices,
                value => value.Kind == "ability-score-set");
            Assert.Equal(CharacterResolutionStates.ChoiceRequired, setChoice.State);
            Assert.Equal(2, setChoice.Options.Count);
            Assert.Equal(
                CharacterResolutionStates.ChoiceRequired,
                Assert.Single(
                    noSet.Mechanics,
                    value => value.MechanicKey == "ability.strength.score").State);
            Assert.Equal(
                CharacterResolutionStates.Resolved,
                Assert.Single(
                    noSet.Mechanics,
                    value => value.MechanicKey == "ability.wisdom.score").State);

            var firstSetValue = setChoice.Options[0].Value;
            var setSelected = await projection.ResolveGlobalAsync(
                Request(
                [
                    new CharacterRuntimeChoiceInput(
                        setChoice.ChoiceKey,
                        firstSetValue)
                ]),
                userId: null);
            var abilityChoices = setSelected.Choices
                .Where(value => value.Kind == "ability-score")
                .OrderBy(value => value.ChoiceKey, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(2, abilityChoices.Length);
            Assert.Contains(abilityChoices, value => value.DisplayName.Contains("+2", StringComparison.Ordinal));
            Assert.Contains(abilityChoices, value => value.DisplayName.Contains("+1", StringComparison.Ordinal));

            var plusTwo = abilityChoices.Single(value =>
                value.DisplayName.Contains("+2", StringComparison.Ordinal));
            var plusOne = abilityChoices.Single(value =>
                value.DisplayName.Contains("+1", StringComparison.Ordinal));

            var resolved = await projection.ResolveGlobalAsync(
                Request(
                [
                    new CharacterRuntimeChoiceInput(
                        setChoice.ChoiceKey,
                        firstSetValue),
                    new CharacterRuntimeChoiceInput(
                        plusTwo.ChoiceKey,
                        "strength"),
                    new CharacterRuntimeChoiceInput(
                        plusOne.ChoiceKey,
                        "dexterity")
                ]),
                userId: null);

            Assert.Equal(12, Assert.Single(
                resolved.Mechanics,
                value => value.MechanicKey == "ability.strength.score").NumericValue);
            Assert.Equal(11, Assert.Single(
                resolved.Mechanics,
                value => value.MechanicKey == "ability.dexterity.score").NumericValue);
            Assert.Equal(10, Assert.Single(
                resolved.Mechanics,
                value => value.MechanicKey == "ability.constitution.score").NumericValue);
            Assert.DoesNotContain(
                resolved.Mechanics,
                value => value.MechanicKey.StartsWith("ability.", StringComparison.Ordinal)
                    && value.State == CharacterResolutionStates.ChoiceRequired);
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
    public async Task CharacterProjectionRequiresStartingClassAndUsesMulticlassProficiencies()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-projection-multiclass-{token}";
        var alphaConceptKey = $"class.alpha-{token}";
        var betaConceptKey = $"class.beta-{token}";
        var actor = $"character-multiclass-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            var sourceCode = $"MUL{token}";
            var alphaRaw = JsonSerializer.Serialize(new
            {
                name = "Alpha",
                source = sourceCode,
                hd = new { number = 1, faces = 10 },
                proficiency = new[] { "str", "con" },
                startingProficiencies = new
                {
                    armor = new[] { "heavy" }
                },
                multiclassing = new
                {
                    proficienciesGained = new
                    {
                        armor = new[] { "light" }
                    }
                }
            });
            var betaRaw = JsonSerializer.Serialize(new
            {
                name = "Beta",
                source = sourceCode,
                hd = new { number = 1, faces = 8 },
                proficiency = new[] { "dex", "int" },
                startingProficiencies = new
                {
                    armor = new[] { "medium" }
                },
                multiclassing = new
                {
                    proficienciesGained = new
                    {
                        armor = new[] { "shield" }
                    }
                }
            });

            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Character Multiclass Fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    new NormalizedSourceRepresentation(
                        FiveEToolsSourceFormatAdapter.Format,
                        new SourceRepresentationArtifact(
                            $"multiclass-{token}.json",
                            Encoding.UTF8.GetBytes("{}"),
                            $"integration:character-multiclass:{token}"),
                        [
                            new NormalizedSourceRecord(
                                "class",
                                "Alpha",
                                sourceCode,
                                $"class|Alpha|{sourceCode}",
                                alphaRaw,
                                PublicationLocalKey: sourceCode),
                            new NormalizedSourceRecord(
                                "class",
                                "Beta",
                                sourceCode,
                                $"class|Beta|{sourceCode}",
                                betaRaw,
                                PublicationLocalKey: sourceCode)
                        ],
                        [
                            new NormalizedSourcePublication(
                                sourceCode,
                                $"Character Multiclass Fixture {token}",
                                "Integration Test Press",
                                "5e",
                                new DateOnly(2014, 8, 19))
                        ])));
            packageId = imported.PackageId;
            var byName = imported.Entities.ToDictionary(value => value.Name, StringComparer.Ordinal);
            var alphaSource = byName["Alpha"];
            var betaSource = byName["Beta"];

            var alphaConcept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(
                    alphaConceptKey,
                    alphaSource.EntityType,
                    alphaSource.Name),
                actor);
            var betaConcept = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(
                    betaConceptKey,
                    betaSource.EntityType,
                    betaSource.Name),
                actor);
            await globalRules.BindSourceEntityAsync(
                alphaConcept.Value.Id,
                new BindRuleConceptSourceRequest(alphaSource.EntityId),
                actor);
            await globalRules.BindSourceEntityAsync(
                betaConcept.Value.Id,
                new BindRuleConceptSourceRequest(betaSource.EntityId),
                actor);

            var revisionIds = await db.SourceEntityRevisions
                .Where(value =>
                    value.SourceEntityId == alphaSource.EntityId
                    || value.SourceEntityId == betaSource.EntityId)
                .ToDictionaryAsync(value => value.SourceEntityId, value => value.Id);
            await globalRules.SetDecisionAsync(
                alphaConcept.Value.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionIds[alphaSource.EntityId],
                    "Alpha multiclass fixture."),
                actor);
            await globalRules.SetDecisionAsync(
                betaConcept.Value.Id,
                new SetGlobalRuleDecisionRequest(
                    revisionIds[betaSource.EntityId],
                    "Beta multiclass fixture."),
                actor);
            await globalRules.PublishAsync(actor);

            CharacterRulesProjectionRequest Request(
                IReadOnlyList<CharacterRuntimeChoiceInput>? choices = null) =>
                new(
                    BaseAbilityScores: new Dictionary<string, int>
                    {
                        ["strength"] = 14,
                        ["dexterity"] = 16,
                        ["constitution"] = 12,
                        ["intelligence"] = 14,
                        ["wisdom"] = 10,
                        ["charisma"] = 10
                    },
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(alphaConceptKey, 3),
                        new CharacterAdvancementFactInput(betaConceptKey, 2)
                    ],
                    Choices: choices);

            var unresolved = await projection.ResolveGlobalAsync(
                Request(),
                userId: null);
            var startingClass = Assert.Single(
                unresolved.Mechanics,
                value => value.MechanicKey == "advancement.starting-class");
            Assert.Equal(CharacterResolutionStates.ChoiceRequired, startingClass.State);
            Assert.Contains(
                "advancement.starting-class",
                startingClass.RequiredChoices);
            Assert.DoesNotContain(
                unresolved.Capabilities,
                value => value.CapabilityKey.StartsWith(
                    "save.",
                    StringComparison.OrdinalIgnoreCase)
                    && value.CapabilityKey.EndsWith(
                        ".proficient",
                        StringComparison.OrdinalIgnoreCase));

            var resolved = await projection.ResolveGlobalAsync(
                Request(
                [
                    new CharacterRuntimeChoiceInput(
                        "advancement.starting-class",
                        alphaConceptKey)
                ]),
                userId: null);

            Assert.Equal(3, Assert.Single(
                resolved.Mechanics,
                value => value.MechanicKey == "proficiency.standard").NumericValue);
            Assert.Equal(5, Assert.Single(
                resolved.Mechanics,
                value => value.MechanicKey == "save.strength").NumericValue);
            Assert.Equal(3, Assert.Single(
                resolved.Mechanics,
                value => value.MechanicKey == "save.dexterity").NumericValue);

            Assert.Contains(
                resolved.Capabilities,
                value => value.CapabilityKey == "save.strength.proficient");
            Assert.Contains(
                resolved.Capabilities,
                value => value.CapabilityKey == "save.constitution.proficient");
            Assert.DoesNotContain(
                resolved.Capabilities,
                value => value.CapabilityKey == "save.dexterity.proficient");
            Assert.DoesNotContain(
                resolved.Capabilities,
                value => value.CapabilityKey == "save.intelligence.proficient");

            Assert.Contains(
                resolved.Qualifications,
                value => value.QualificationKey == "qualification.armor.heavy");
            Assert.Contains(
                resolved.Qualifications,
                value => value.QualificationKey == "qualification.armor.shield");
            Assert.DoesNotContain(
                resolved.Qualifications,
                value => value.QualificationKey == "qualification.armor.medium");
            Assert.DoesNotContain(
                resolved.Qualifications,
                value => value.QualificationKey == "qualification.armor.light");
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
