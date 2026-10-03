using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CharacterStartingEquipmentProjectionIntegrationTests
{
    [Fact]
    public async Task ProjectionUsesResolvedStartingClassAndBackgroundAndExposesOnlyCharacterChoices()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-starting-equipment-{token}";
        var sourceCode = $"SE{token}";
        var actor = $"character-starting-equipment-{token}";
        var classConceptKey = $"class.starting-equipment-{token}";
        var secondaryClassConceptKey = $"class.secondary-equipment-{token}";
        var backgroundConceptKey = $"background.starting-equipment-{token}";
        var chainConceptKey = $"item.chain-mail-{token}";
        var rapierConceptKey = $"item.rapier-{token}";
        var longswordConceptKey = $"item.longsword-{token}";
        var pouchConceptKey = $"item.pouch-{token}";
        var secondaryItemConceptKey = $"item.secondary-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            string Item(string name, string type, string? weaponCategory = null) =>
                JsonSerializer.Serialize(new
                {
                    name,
                    source = sourceCode,
                    type,
                    weaponCategory
                });

            var classRaw = JsonSerializer.Serialize(new
            {
                name = "Starting Equipment Class",
                source = sourceCode,
                hd = new { number = 1, faces = 10 },
                proficiency = new[] { "str", "con" },
                startingEquipment = new
                {
                    additionalFromBackground = true,
                    defaultData = new object[]
                    {
                        new
                        {
                            a = new object[]
                            {
                                new { item = $"Chain Mail|{sourceCode}" },
                                new { equipmentType = "weaponMartial" },
                                new { value = 425 }
                            },
                            b = new object[]
                            {
                                new { item = $"Longsword|{sourceCode}" }
                            }
                        }
                    }
                }
            });
            var secondaryClassRaw = JsonSerializer.Serialize(new
            {
                name = "Secondary Equipment Class",
                source = sourceCode,
                hd = new { number = 1, faces = 8 },
                proficiency = new[] { "dex", "int" },
                startingEquipment = new
                {
                    defaultData = new object[]
                    {
                        new
                        {
                            _ = new object[]
                            {
                                new { item = $"Secondary Token|{sourceCode}" }
                            }
                        }
                    }
                }
            });
            var backgroundRaw = JsonSerializer.Serialize(new
            {
                name = "Starting Equipment Background",
                source = sourceCode,
                startingEquipment = new object[]
                {
                    new
                    {
                        _ = new object[]
                        {
                            new { item = $"Pouch|{sourceCode}" },
                            new { value = 100 }
                        }
                    }
                }
            });

            var records = new[]
            {
                new NormalizedSourceRecord(
                    "class",
                    "Starting Equipment Class",
                    sourceCode,
                    $"class|Starting Equipment Class|{sourceCode}",
                    classRaw,
                    PublicationLocalKey: sourceCode),
                new NormalizedSourceRecord(
                    "class",
                    "Secondary Equipment Class",
                    sourceCode,
                    $"class|Secondary Equipment Class|{sourceCode}",
                    secondaryClassRaw,
                    PublicationLocalKey: sourceCode),
                new NormalizedSourceRecord(
                    "background",
                    "Starting Equipment Background",
                    sourceCode,
                    $"background|Starting Equipment Background|{sourceCode}",
                    backgroundRaw,
                    PublicationLocalKey: sourceCode),
                new NormalizedSourceRecord(
                    "item",
                    "Chain Mail",
                    sourceCode,
                    $"item|Chain Mail|{sourceCode}",
                    Item("Chain Mail", "HA"),
                    PublicationLocalKey: sourceCode),
                new NormalizedSourceRecord(
                    "item",
                    "Rapier",
                    sourceCode,
                    $"item|Rapier|{sourceCode}",
                    Item("Rapier", "M", "martial"),
                    PublicationLocalKey: sourceCode),
                new NormalizedSourceRecord(
                    "item",
                    "Longsword",
                    sourceCode,
                    $"item|Longsword|{sourceCode}",
                    Item("Longsword", "M", "martial"),
                    PublicationLocalKey: sourceCode),
                new NormalizedSourceRecord(
                    "item",
                    "Pouch",
                    sourceCode,
                    $"item|Pouch|{sourceCode}",
                    Item("Pouch", "G"),
                    PublicationLocalKey: sourceCode),
                new NormalizedSourceRecord(
                    "item",
                    "Secondary Token",
                    sourceCode,
                    $"item|Secondary Token|{sourceCode}",
                    Item("Secondary Token", "G"),
                    PublicationLocalKey: sourceCode)
            };

            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Character Starting Equipment Fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    new NormalizedSourceRepresentation(
                        FiveEToolsSourceFormatAdapter.Format,
                        new SourceRepresentationArtifact(
                            $"starting-equipment-{token}.json",
                            Encoding.UTF8.GetBytes("{}"),
                            $"integration:character-starting-equipment:{token}"),
                        records,
                        [
                            new NormalizedSourcePublication(
                                sourceCode,
                                $"Character Starting Equipment Fixture {token}",
                                "Integration Test Press",
                                "5e",
                                new DateOnly(2014, 8, 19))
                        ])));
            packageId = imported.PackageId;

            var conceptKeys = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Starting Equipment Class"] = classConceptKey,
                ["Secondary Equipment Class"] = secondaryClassConceptKey,
                ["Starting Equipment Background"] = backgroundConceptKey,
                ["Chain Mail"] = chainConceptKey,
                ["Rapier"] = rapierConceptKey,
                ["Longsword"] = longswordConceptKey,
                ["Pouch"] = pouchConceptKey,
                ["Secondary Token"] = secondaryItemConceptKey
            };

            var revisionIds = await db.SourceEntityRevisions
                .Where(value => imported.Entities.Select(entity => entity.EntityId).Contains(value.SourceEntityId))
                .ToDictionaryAsync(value => value.SourceEntityId, value => value.Id);

            foreach (var entity in imported.Entities)
            {
                var concept = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(
                        conceptKeys[entity.Name],
                        entity.EntityType,
                        entity.Name),
                    actor);
                await globalRules.BindSourceEntityAsync(
                    concept.Value.Id,
                    new BindRuleConceptSourceRequest(entity.EntityId),
                    actor);
                await globalRules.SetDecisionAsync(
                    concept.Value.Id,
                    new SetGlobalRuleDecisionRequest(
                        revisionIds[entity.EntityId],
                        "Starting equipment projection fixture."),
                    actor);
            }
            await globalRules.PublishAsync(actor);

            CharacterRulesProjectionRequest Request(
                IReadOnlyList<CharacterRuntimeChoiceInput>? equipmentChoices = null) =>
                new(
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(classConceptKey, 1),
                        new CharacterAdvancementFactInput(secondaryClassConceptKey, 1)
                    ],
                    SelectedConcepts:
                    [
                        new CharacterSelectedConceptInput(backgroundConceptKey)
                    ],
                    Choices:
                    [
                        new CharacterRuntimeChoiceInput(
                            "advancement.starting-class",
                            classConceptKey),
                        .. equipmentChoices ?? []
                    ]);

            var unresolved = await projection.ResolveGlobalAsync(Request(), userId: null);

            Assert.DoesNotContain(
                unresolved.Grants,
                value => value.TargetKey == secondaryItemConceptKey);
            Assert.Contains(
                unresolved.Grants,
                value => value.Kind == "starting-equipment-item"
                    && value.TargetKey == pouchConceptKey
                    && value.SourceConceptKey == backgroundConceptKey);
            Assert.Single(
                unresolved.Grants,
                value => value.Kind == "starting-equipment-currency"
                    && value.TargetKey == "gp"
                    && value.SourceConceptKey == backgroundConceptKey);

            var packageChoice = Assert.Single(
                unresolved.Choices,
                value => value.Kind == "starting-equipment"
                    && value.SourceConceptKey == classConceptKey
                    && value.Options.Any(option => option.Value == "a")
                    && value.Options.Any(option => option.Value == "b"));
            Assert.Equal(CharacterResolutionStates.ChoiceRequired, packageChoice.State);
            Assert.DoesNotContain(
                unresolved.Choices,
                value => value.SourceConceptKey == secondaryClassConceptKey
                    && value.Kind == "starting-equipment");

            var packageSelected = await projection.ResolveGlobalAsync(
                Request(
                [
                    new CharacterRuntimeChoiceInput(packageChoice.ChoiceKey, "a")
                ]),
                userId: null);

            Assert.Contains(
                packageSelected.Grants,
                value => value.Kind == "starting-equipment-item"
                    && value.TargetKey == chainConceptKey
                    && value.SourceConceptKey == classConceptKey);
            Assert.Equal(4, packageSelected.Grants.Count(value =>
                value.Kind == "starting-equipment-currency"
                && value.TargetKey == "gp"
                && value.SourceConceptKey == classConceptKey));
            Assert.Equal(2, packageSelected.Grants.Count(value =>
                value.Kind == "starting-equipment-currency"
                && value.TargetKey == "sp"
                && value.SourceConceptKey == classConceptKey));
            Assert.Equal(5, packageSelected.Grants.Count(value =>
                value.Kind == "starting-equipment-currency"
                && value.TargetKey == "cp"
                && value.SourceConceptKey == classConceptKey));

            var weaponChoice = Assert.Single(
                packageSelected.Choices,
                value => value.Kind == "starting-equipment"
                    && value.SourceConceptKey == classConceptKey
                    && value.Options.Any(option => option.ConceptKey == rapierConceptKey)
                    && value.Options.Any(option => option.ConceptKey == longswordConceptKey));
            Assert.Equal(CharacterResolutionStates.ChoiceRequired, weaponChoice.State);

            var resolved = await projection.ResolveGlobalAsync(
                Request(
                [
                    new CharacterRuntimeChoiceInput(packageChoice.ChoiceKey, "a"),
                    new CharacterRuntimeChoiceInput(weaponChoice.ChoiceKey, rapierConceptKey)
                ]),
                userId: null);

            Assert.Equal(
                CharacterResolutionStates.Resolved,
                Assert.Single(
                    resolved.Choices,
                    value => value.ChoiceKey == packageChoice.ChoiceKey).State);
            Assert.Equal(
                CharacterResolutionStates.Resolved,
                Assert.Single(
                    resolved.Choices,
                    value => value.ChoiceKey == weaponChoice.ChoiceKey).State);
            Assert.Contains(
                resolved.Grants,
                value => value.Kind == "starting-equipment-item"
                    && value.TargetKey == rapierConceptKey
                    && value.SourceConceptKey == classConceptKey);
            Assert.DoesNotContain(
                resolved.Grants,
                value => value.TargetKey == secondaryItemConceptKey);
            Assert.DoesNotContain(
                resolved.Conflicts,
                value => value.ConflictKey.Contains(
                    "starting-equipment",
                    StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await using var cleanupScope = factory.Services.CreateAsyncScope();
            await CleanupAsync(
                cleanupScope.ServiceProvider.GetRequiredService<RulesCoreDbContext>(),
                packageId);
        }
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
