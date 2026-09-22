using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CharacterSupportConsumerIntegrationTests
{
    [Fact]
    public async Task SupportProjectionUsesEffectiveCampaignRulesAndDoesNotLeakInaccessibleSources()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..12];
        var publicPackageKey = $"character-support-public-{token}";
        var privatePackageKey = $"character-support-private-{token}";
        var actor = $"rules-lawyer-{token}";
        var campaignId = Guid.NewGuid();
        Guid publicPackageId = Guid.Empty;
        Guid privatePackageId = Guid.Empty;

        try
        {
            Guid conceptId;
            Guid sourceEntityId;
            Guid globalRevisionId;
            Guid campaignRevisionId;
            PublishedRulesetRevisionView globalPublication;

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var normalization = new SourceNormalizationService(db);
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();

                var imported = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(
                        publicPackageKey,
                        isPublic: true,
                        SupportJson(
                            "Global Short Recovery",
                            passiveValue: 13,
                            qualificationCategory: "weapon-group",
                            recoveryAmount: 2)));
                publicPackageId = imported.PackageId;
                var entity = Assert.Single(imported.Entities);
                sourceEntityId = entity.EntityId;
                globalRevisionId = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == sourceEntityId)
                    .Select(value => value.Id)
                    .SingleAsync();

                var accepted = await normalization.AcceptAsync(
                    sourceEntityId,
                    actor);
                Assert.NotNull(accepted);
                conceptId = accepted!.Concept.Id;

                await globalRules.SetDecisionAsync(
                    conceptId,
                    new SetGlobalRuleDecisionRequest(
                        globalRevisionId,
                        "Publish global Character support fixture."),
                    actor);
                globalPublication = await globalRules.PublishAsync(actor);

                var revised = await importer.Import5eToolsDocumentAsync(
                    SourceRequest(
                        publicPackageKey,
                        isPublic: true,
                        SupportJson(
                            "Campaign Recovery Override",
                            passiveValue: 21,
                            qualificationCategory: "campaign-training",
                            recoveryAmount: 5)));
                Assert.Equal(publicPackageId, revised.PackageId);
                Assert.Equal(sourceEntityId, Assert.Single(revised.Entities).EntityId);
                Assert.Equal(2, revised.Entities.Single().RevisionNumber);
                campaignRevisionId = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == sourceEntityId
                        && value.RevisionNumber == 2)
                    .Select(value => value.Id)
                    .SingleAsync();
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var campaignRules = scope.ServiceProvider.GetRequiredService<ICampaignRulesService>();
                await campaignRules.SelectBaselineAsync(
                    campaignId,
                    new SelectCampaignRulesetBaselineRequest(globalPublication.Id),
                    actor);
                await campaignRules.SetDecisionAsync(
                    campaignId,
                    conceptId,
                    new SetCampaignRuleDecisionRequest(
                        CampaignRuleDecisionKinds.SelectSource,
                        campaignRevisionId,
                        "Campaign selects the alternate recovery/training implementation."),
                    actor);
                await campaignRules.PublishAsync(campaignId, actor);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var mechanics = scope.ServiceProvider
                    .GetRequiredService<ICharacterMechanicsConsumerService>();

                var facts = new CharacterSupportProjectionRequest(
                    FactsBySupportKey:
                        new Dictionary<string, CharacterSupportInputValues>
                        {
                            ["qualification.fixture-training"] = new(
                                StringInputs: new Dictionary<string, string>
                                {
                                    ["state"] = "proficient"
                                })
                        },
                    CapabilityKeys: []);

                var global = await mechanics.ProjectGlobalSupportAsync(
                    facts,
                    userId: null);
                var globalRecovery = Assert.Single(
                    global.RecoveryProcedures,
                    value => value.ProcedureKey == "recovery.fixture");
                Assert.Equal("Global Short Recovery", globalRecovery.DisplayName);
                Assert.Equal(
                    CharacterRecoveryPresentationRoles.ShortRest,
                    globalRecovery.PresentationRole);
                Assert.Equal(publicPackageKey, Assert.Single(globalRecovery.SourceAttributions).PackageKey);

                var globalPassive = Assert.Single(
                    global.PassiveValues,
                    value => value.MechanicKey == "passive.fixture-awareness");
                Assert.Equal(CharacterSupportResolutionStates.Resolved, globalPassive.ResolutionState);
                Assert.Equal(13, globalPassive.Value);

                var globalQualification = Assert.Single(
                    global.Qualifications,
                    value => value.QualificationKey == "qualification.fixture-training");
                Assert.Equal("weapon-group", globalQualification.Category);
                Assert.Equal("proficient", globalQualification.State!.StringValue);

                var globalResolution = await mechanics.ResolveGlobalRecoveryAsync(
                    "recovery.fixture",
                    new CharacterRecoveryResolutionRequest(CapabilityKeys: []),
                    userId: null);
                Assert.NotNull(globalResolution);
                Assert.Equal(CharacterRecoveryResolutionStatuses.Resolved, globalResolution.Status);
                Assert.Equal(2, Assert.Single(globalResolution.Consequences).Amount);

                var campaign = await mechanics.ProjectCampaignSupportAsync(
                    campaignId,
                    facts,
                    actor);
                var campaignRecovery = Assert.Single(
                    campaign.RecoveryProcedures,
                    value => value.ProcedureKey == "recovery.fixture");
                Assert.Equal("Campaign Recovery Override", campaignRecovery.DisplayName);

                var campaignPassive = Assert.Single(
                    campaign.PassiveValues,
                    value => value.MechanicKey == "passive.fixture-awareness");
                Assert.Equal(21, campaignPassive.Value);

                var campaignQualification = Assert.Single(
                    campaign.Qualifications,
                    value => value.QualificationKey == "qualification.fixture-training");
                Assert.Equal("campaign-training", campaignQualification.Category);
                Assert.Equal("proficient", campaignQualification.State!.StringValue);

                var campaignResolution = await mechanics.ResolveCampaignRecoveryAsync(
                    campaignId,
                    "recovery.fixture",
                    new CharacterRecoveryResolutionRequest(CapabilityKeys: []),
                    actor);
                Assert.NotNull(campaignResolution);
                Assert.Equal(CharacterRecoveryResolutionStatuses.Resolved, campaignResolution.Status);
                Assert.Equal(5, Assert.Single(campaignResolution.Consequences).Amount);
                Assert.NotEqual(
                    globalResolution.Consequences.Single().Amount,
                    campaignResolution.Consequences.Single().Amount);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var normalization = new SourceNormalizationService(db);
                var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();

                var importedPrivate = await importer.Import5eToolsDocumentAsync(
                    new Import5eToolsDocumentRequest(
                        PackageKey: privatePackageKey,
                        PackageDisplayName: "Private Character Support Fixture",
                        Provider: "integration-test",
                        License: "test-only",
                        IsPublic: false,
                        WorkKey: $"private-character-support-{token}",
                        WorkDisplayName: "Private Character Support Fixture",
                        EditionKey: "5e",
                        EditionDisplayName: "5e",
                        Json: PrivateSupportJson,
                        GameEdition: "5e"));
                privatePackageId = importedPrivate.PackageId;
                var privateEntity = Assert.Single(importedPrivate.Entities);
                var acceptedPrivate = await normalization.AcceptAsync(
                    privateEntity.EntityId,
                    actor);
                Assert.NotNull(acceptedPrivate);
                var privateRevisionId = await db.SourceEntityRevisions
                    .Where(value => value.SourceEntityId == privateEntity.EntityId)
                    .Select(value => value.Id)
                    .SingleAsync();

                await globalRules.SetDecisionAsync(
                    acceptedPrivate!.Concept.Id,
                    new SetGlobalRuleDecisionRequest(
                        privateRevisionId,
                        "Publish restricted support fixture."),
                    actor);
                await globalRules.PublishAsync(actor);

                db.UserSourceGrants.Add(new UserSourceGrant
                {
                    Id = Guid.NewGuid(),
                    SourcePackageId = privatePackageId,
                    UserId = "licensed-player",
                    GrantedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var mechanics = scope.ServiceProvider
                    .GetRequiredService<ICharacterMechanicsConsumerService>();
                var anonymous = await mechanics.ProjectGlobalSupportAsync(
                    new CharacterSupportProjectionRequest(),
                    userId: null);
                Assert.DoesNotContain(
                    anonymous.RecoveryProcedures,
                    value => value.ProcedureKey == "recovery.private-secret");
                Assert.DoesNotContain(
                    anonymous.PassiveValues,
                    value => value.MechanicKey == "passive.private-secret");
                Assert.DoesNotContain(
                    anonymous.Qualifications,
                    value => value.QualificationKey == "qualification.private-secret");

                var licensed = await mechanics.ProjectGlobalSupportAsync(
                    new CharacterSupportProjectionRequest(),
                    "licensed-player");
                var restrictedRecovery = Assert.Single(
                    licensed.RecoveryProcedures,
                    value => value.ProcedureKey == "recovery.private-secret");
                Assert.Equal(
                    privatePackageKey,
                    Assert.Single(restrictedRecovery.SourceAttributions).PackageKey);

                var restricted = Assert.Single(
                    licensed.PassiveValues,
                    value => value.MechanicKey == "passive.private-secret");
                Assert.Equal(999, restricted.Value);
                Assert.Equal(
                    privatePackageKey,
                    Assert.Single(restricted.SourceAttributions).PackageKey);

                var restrictedQualification = Assert.Single(
                    licensed.Qualifications,
                    value => value.QualificationKey == "qualification.private-secret");
                Assert.Equal(
                    CharacterSupportResolutionStates.UnresolvedCharacterState,
                    restrictedQualification.ResolutionState);
                Assert.Equal(
                    privatePackageKey,
                    Assert.Single(restrictedQualification.SourceAttributions).PackageKey);
            }
        }
        finally
        {
            await using var cleanupScope = factory.Services.CreateAsyncScope();
            await CleanupAsync(
                cleanupScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>(),
                publicPackageId,
                privatePackageId);
        }
    }

    private static Import5eToolsDocumentRequest SourceRequest(
        string packageKey,
        bool isPublic,
        string json) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: "Character Support Fixture",
            Provider: "integration-test",
            License: "test-only",
            IsPublic: isPublic,
            WorkKey: "character-support-fixture",
            WorkDisplayName: "Character Support Fixture",
            EditionKey: "5e",
            EditionDisplayName: "5e",
            Json: json,
            GameEdition: "5e");

    private static string SupportJson(
        string recoveryName,
        int passiveValue,
        string qualificationCategory,
        int recoveryAmount) =>
        $$"""
        {
          "variantrule": [
            {
              "name": "Character Support Fixture",
              "source": "FIX",
              "entries": ["Integration fixture."],
              "_rulesCore": {
                "characterSupport": {
                  "recoveryProcedures": [
                    {
                      "key": "recovery.fixture",
                      "displayName": "{{recoveryName}}",
                      "presentationRole": "short-rest",
                      "effects": [
                        {
                          "key": "recover-stamina",
                          "targetKind": "resource",
                          "targetKey": "stamina",
                          "operation": "adjust",
                          "amount": {{recoveryAmount}}
                        }
                      ]
                    }
                  ],
                  "passiveValues": [
                    {
                      "key": "passive.fixture-awareness",
                      "displayName": "Fixture Awareness",
                      "evaluationKind": "sum",
                      "constant": {{passiveValue}},
                      "relatedConceptKey": "skill.fixture-awareness"
                    }
                  ],
                  "qualifications": [
                    {
                      "key": "qualification.fixture-training",
                      "displayName": "Fixture Training",
                      "category": "{{qualificationCategory}}",
                      "family": "fixture",
                      "stateInput": {
                        "key": "state",
                        "valueKind": "string",
                        "origin": "character-state",
                        "required": true
                      },
                      "associatedConceptKey": "training.fixture"
                    }
                  ]
                }
              }
            }
          ]
        }
        """;

    private const string PrivateSupportJson = """
        {
          "variantrule": [
            {
              "name": "Private Character Support Fixture",
              "source": "PRIVATE",
              "entries": ["Restricted integration fixture."],
              "_rulesCore": {
                "characterSupport": {
                  "recoveryProcedures": [
                    {
                      "key": "recovery.private-secret",
                      "displayName": "Private Recovery",
                      "effects": [
                        {
                          "key": "recover-private",
                          "targetKind": "resource",
                          "targetKey": "private-resource",
                          "operation": "adjust",
                          "amount": 1
                        }
                      ]
                    }
                  ],
                  "passiveValues": [
                    {
                      "key": "passive.private-secret",
                      "displayName": "Private Secret Value",
                      "evaluationKind": "sum",
                      "constant": 999
                    }
                  ],
                  "qualifications": [
                    {
                      "key": "qualification.private-secret",
                      "displayName": "Private Training",
                      "category": "private-training",
                      "stateInput": {
                        "key": "state",
                        "valueKind": "string",
                        "origin": "character-state",
                        "required": true
                      }
                    }
                  ]
                }
              }
            }
          ]
        }
        """;

    private static async Task CleanupAsync(
        RulesCoreDbContext db,
        params Guid[] packageIds)
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

        var ids = packageIds
            .Where(value => value != Guid.Empty)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var packages = await db.SourcePackages
            .Where(value => ids.Contains(value.Id))
            .ToArrayAsync();
        db.SourcePackages.RemoveRange(packages);
        await db.SaveChangesAsync();
    }
}
