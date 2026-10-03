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
public sealed class CharacterStartingEquipmentSourceShapeIntegrationTests
{
    [Fact]
    public async Task ProjectionSupportsResolvedEquipmentCategoriesAndStartingGoldAlternative()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"character-starting-equipment-shapes-{token}";
        var sourceCode = $"SS{token}";
        var actor = $"character-starting-equipment-shapes-{token}";
        var classConceptKey = $"class.starting-equipment-shapes-{token}";
        var backgroundConceptKey = $"background.starting-equipment-shapes-{token}";
        var clubConceptKey = $"item.club-{token}";
        var shortbowConceptKey = $"item.shortbow-{token}";
        var wandConceptKey = $"item.arcane-wand-{token}";
        var holyConceptKey = $"item.holy-symbol-{token}";
        var luteConceptKey = $"item.lute-{token}";
        var toolsConceptKey = $"item.artisan-tools-{token}";
        var pouchConceptKey = $"item.pouch-{token}";
        Guid packageId = Guid.Empty;

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var globalRules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var projection = scope.ServiceProvider.GetRequiredService<ICharacterRulesProjectionService>();

            string Item(
                string name,
                string type,
                string? weaponCategory = null,
                string? scfType = null) =>
                JsonSerializer.Serialize(new
                {
                    name,
                    source = sourceCode,
                    type,
                    weaponCategory,
                    scfType
                });

            var classRaw = JsonSerializer.Serialize(new
            {
                name = "Starting Equipment Shape Class",
                source = sourceCode,
                hd = new { number = 1, faces = 8 },
                proficiency = new[] { "dex", "wis" },
                startingEquipment = new
                {
                    additionalFromBackground = true,
                    goldAlternative = "{@dice 2d4 × 10|2d4 × 10|Starting Gold}",
                    defaultData = new object[]
                    {
                        new
                        {
                            _ = new object[]
                            {
                                new { equipmentType = "weaponSimpleMelee" },
                                new { equipmentType = "focusSpellcastingArcane" },
                                new { equipmentTypes = new[] { "instrumentMusical", "toolArtisan" } }
                            }
                        }
                    }
                }
            });
            var backgroundRaw = JsonSerializer.Serialize(new
            {
                name = "Starting Equipment Shape Background",
                source = sourceCode,
                startingEquipment = new object[]
                {
                    new
                    {
                        _ = new object[]
                        {
                            new { item = $"Pouch|{sourceCode}" }
                        }
                    }
                }
            });

            var records = new[]
            {
                new NormalizedSourceRecord(
                    "class",
                    "Starting Equipment Shape Class",
                    sourceCode,
                    $"class|Starting Equipment Shape Class|{sourceCode}",
                    classRaw,
                    PublicationLocalKey: sourceCode),
                new NormalizedSourceRecord(
                    "background",
                    "Starting Equipment Shape Background",
                    sourceCode,
                    $"background|Starting Equipment Shape Background|{sourceCode}",
                    backgroundRaw,
                    PublicationLocalKey: sourceCode),
                new NormalizedSourceRecord(
                    "item",
                    "Club",
                    sourceCode,
                    $"item|Club|{sourceCode}",
                    Item("Club", "M", "simple"),
                    PublicationLocalKey: sourceCode),
                new NormalizedSourceRecord(
                    "item",
                    "Shortbow",
                    sourceCode,
                    $"item|Shortbow|{sourceCode}",
                    Item("Shortbow", "R", "simple"),
                    PublicationLocalKey: sourceCode),
                new NormalizedSourceRecord(
                    "item",
                    "Arcane Wand",
                    sourceCode,
                    $"item|Arcane Wand|{sourceCode}",
                    Item("Arcane Wand", "SCF", scfType: "arcane"),
                    PublicationLocalKey: sourceCode),
                new NormalizedSourceRecord(
                    "item",
                    "Holy Symbol",
                    sourceCode,
                    $"item|Holy Symbol|{sourceCode}",
                    Item("Holy Symbol", "SCF", scfType: "holy"),
                    PublicationLocalKey: sourceCode),
                new NormalizedSourceRecord(
                    "item",
                    "Lute",
                    sourceCode,
                    $"item|Lute|{sourceCode}",
                    Item("Lute", "INS"),
                    PublicationLocalKey: sourceCode),
                new NormalizedSourceRecord(
                    "item",
                    "Artisan Tools",
                    sourceCode,
                    $"item|Artisan Tools|{sourceCode}",
                    Item("Artisan Tools", "AT"),
                    PublicationLocalKey: sourceCode),
                new NormalizedSourceRecord(
                    "item",
                    "Pouch",
                    sourceCode,
                    $"item|Pouch|{sourceCode}",
                    Item("Pouch", "G"),
                    PublicationLocalKey: sourceCode)
            };

            var imported = await new NormalizedSourceImportService(db).ImportAsync(
                new ImportNormalizedSourceRequest(
                    packageKey,
                    $"Character Starting Equipment Shape Fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    new NormalizedSourceRepresentation(
                        FiveEToolsSourceFormatAdapter.Format,
                        new SourceRepresentationArtifact(
                            $"starting-equipment-shapes-{token}.json",
                            Encoding.UTF8.GetBytes("{}"),
                            $"integration:character-starting-equipment-shapes:{token}"),
                        records,
                        [
                            new NormalizedSourcePublication(
                                sourceCode,
                                $"Character Starting Equipment Shape Fixture {token}",
                                "Integration Test Press",
                                "5e",
                                new DateOnly(2014, 8, 19))
                        ])));
            packageId = imported.PackageId;

            var conceptKeys = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Starting Equipment Shape Class"] = classConceptKey,
                ["Starting Equipment Shape Background"] = backgroundConceptKey,
                ["Club"] = clubConceptKey,
                ["Shortbow"] = shortbowConceptKey,
                ["Arcane Wand"] = wandConceptKey,
                ["Holy Symbol"] = holyConceptKey,
                ["Lute"] = luteConceptKey,
                ["Artisan Tools"] = toolsConceptKey,
                ["Pouch"] = pouchConceptKey
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
                        "Starting equipment source-shape fixture."),
                    actor);
            }
            await globalRules.PublishAsync(actor);

            CharacterRulesProjectionRequest Request(
                IReadOnlyList<CharacterRuntimeChoiceInput>? choices = null,
                IReadOnlyList<CharacterRuntimeRollInput>? rolls = null) =>
                new(
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(classConceptKey, 1)
                    ],
                    SelectedConcepts:
                    [
                        new CharacterSelectedConceptInput(backgroundConceptKey)
                    ],
                    Choices: choices,
                    Rolls: rolls);

            var unresolved = await projection.ResolveGlobalAsync(Request(), userId: null);
            var methodChoice = Assert.Single(
                unresolved.Choices,
                value => value.Kind == "starting-equipment"
                    && value.SourceConceptKey == classConceptKey
                    && value.Options.Any(option => option.Value == "equipment")
                    && value.Options.Any(option => option.Value == "gold"));
            Assert.Equal(CharacterResolutionStates.ChoiceRequired, methodChoice.State);
            Assert.DoesNotContain(unresolved.Grants, value => value.TargetKey == pouchConceptKey);

            var equipment = await projection.ResolveGlobalAsync(
                Request(
                [
                    new CharacterRuntimeChoiceInput(methodChoice.ChoiceKey, "equipment")
                ]),
                userId: null);
            Assert.Contains(
                equipment.Grants,
                value => value.Kind == "starting-equipment-item"
                    && value.TargetKey == pouchConceptKey
                    && value.SourceConceptKey == backgroundConceptKey);

            var meleeChoice = Assert.Single(
                equipment.Choices,
                value => value.Kind == "starting-equipment"
                    && value.SourceConceptKey == classConceptKey
                    && value.Options.Any(option => option.ConceptKey == clubConceptKey));
            Assert.Contains(meleeChoice.Options, option => option.ConceptKey == clubConceptKey);
            Assert.DoesNotContain(meleeChoice.Options, option => option.ConceptKey == shortbowConceptKey);

            var focusChoice = Assert.Single(
                equipment.Choices,
                value => value.Kind == "starting-equipment"
                    && value.SourceConceptKey == classConceptKey
                    && value.Options.Any(option => option.ConceptKey == wandConceptKey));
            Assert.Contains(focusChoice.Options, option => option.ConceptKey == wandConceptKey);
            Assert.DoesNotContain(focusChoice.Options, option => option.ConceptKey == holyConceptKey);

            var toolChoice = Assert.Single(
                equipment.Choices,
                value => value.Kind == "starting-equipment"
                    && value.SourceConceptKey == classConceptKey
                    && value.Options.Any(option => option.ConceptKey == luteConceptKey)
                    && value.Options.Any(option => option.ConceptKey == toolsConceptKey));
            Assert.Contains(toolChoice.Options, option => option.ConceptKey == luteConceptKey);
            Assert.Contains(toolChoice.Options, option => option.ConceptKey == toolsConceptKey);

            var goldPending = await projection.ResolveGlobalAsync(
                Request(
                [
                    new CharacterRuntimeChoiceInput(methodChoice.ChoiceKey, "gold")
                ]),
                userId: null);
            Assert.DoesNotContain(goldPending.Grants, value => value.Kind == "starting-equipment-item");
            Assert.DoesNotContain(goldPending.Grants, value => value.TargetKey == pouchConceptKey);
            var goldMechanic = Assert.Single(
                goldPending.Mechanics,
                value => value.MechanicKey == $"starting-equipment.gold.{classConceptKey}");
            Assert.Equal(CharacterResolutionStates.RollRequired, goldMechanic.State);
            var rollKey = Assert.Single(goldMechanic.RequiredRolls);

            var goldResolved = await projection.ResolveGlobalAsync(
                Request(
                    [
                        new CharacterRuntimeChoiceInput(methodChoice.ChoiceKey, "gold")
                    ],
                    [
                        new CharacterRuntimeRollInput(rollKey, 5)
                    ]),
                userId: null);
            Assert.Equal(
                50,
                goldResolved.Grants.Count(value =>
                    value.Kind == "starting-equipment-currency"
                    && value.TargetKey == "gp"
                    && value.SourceConceptKey == classConceptKey));
            Assert.DoesNotContain(goldResolved.Grants, value => value.Kind == "starting-equipment-item");
            Assert.Equal(
                CharacterResolutionStates.Resolved,
                Assert.Single(
                    goldResolved.Mechanics,
                    value => value.MechanicKey == $"starting-equipment.gold.{classConceptKey}").State);
            Assert.DoesNotContain(
                goldResolved.Conflicts,
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
