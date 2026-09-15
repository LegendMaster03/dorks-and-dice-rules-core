using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class MechanicalRelationshipIntegrationTests
{
    [Fact]
    public async Task CompositeSkillsRemainDistinctAndResolveStructurallyWithRulesLawyerOverride()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var actor = $"composite-skill-{token}";
            var pcgenPackage = $"composite-pcgen-{token}";
            var umbrellaPackage = $"composite-umbrella-{token}";
            var importer = new NormalizedSourceImportService(db);
            var accepted = new List<AcceptedSourceNormalizationView>();

            try
            {
                var granularImport = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                    pcgenPackage,
                    $"Composite granular fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    PcGenRepresentation(token, ["Hide", "Move Silently", "Search", "Escape Artist"])));
                var umbrellaImport = await importer.ImportAsync(new ImportNormalizedSourceRequest(
                    umbrellaPackage,
                    $"Composite umbrella fixture {token}",
                    "integration-test",
                    "test-only",
                    true,
                    FiveESkillRepresentation(token, "Stealth")));

                var normalization = new SourceNormalizationService(db);
                foreach (var entity in granularImport.Entities.Concat(umbrellaImport.Entities))
                {
                    var result = await normalization.AcceptAsync(entity.EntityId, actor);
                    Assert.NotNull(result);
                    accepted.Add(result!);
                }

                var hide = accepted.Single(value => value.Concept.Key == "skill.hide");
                var moveSilently = accepted.Single(value => value.Concept.Key == "skill.move-silently");
                var stealth = accepted.Single(value => value.Concept.Key == "skill.stealth");
                var search = accepted.Single(value => value.Concept.Key == "skill.search");
                var escapeArtist = accepted.Single(value => value.Concept.Key == "skill.escape-artist");

                Assert.NotEqual(hide.Concept.Id, moveSilently.Concept.Id);
                Assert.NotEqual(hide.Concept.Id, stealth.Concept.Id);
                Assert.NotEqual(moveSilently.Concept.Id, stealth.Concept.Id);

                var hideEntity = granularImport.Entities.Single(value => value.Name == "Hide");
                var moveEntity = granularImport.Entities.Single(value => value.Name == "Move Silently");
                var stealthEntity = Assert.Single(umbrellaImport.Entities);
                Assert.NotEqual(
                    await ReadCanonicalEntityIdAsync(db, hideEntity.EntityId),
                    await ReadCanonicalEntityIdAsync(db, stealthEntity.EntityId));
                Assert.NotEqual(
                    await ReadCanonicalEntityIdAsync(db, moveEntity.EntityId),
                    await ReadCanonicalEntityIdAsync(db, stealthEntity.EntityId));

                var relationships = new MechanicalRelationshipService(db);
                var recommended = Assert.Single(await relationships.GetForConceptAsync(stealth.Concept.Id));

                Assert.Equal(MechanicalRelationshipResolutionKinds.DeriveParent, recommended.RecommendedResolutionKind);
                Assert.Equal(MechanicalRelationshipResolutionKinds.DeriveParent, recommended.EffectiveResolutionKind);
                Assert.False(recommended.IsOverridden);
                Assert.True(recommended.CanResolveStructurally);
                Assert.True(recommended.RequiresAdjudication);
                Assert.Empty(recommended.MissingConceptKeys);
                Assert.Null(recommended.LatestRuling);
                Assert.Equal("skill.stealth", recommended.Relationship.Parent.ConceptKey);
                Assert.Equal(
                    ["skill.hide", "skill.move-silently"],
                    recommended.Relationship.Components.Select(value => value.ConceptKey).ToArray());
                Assert.All(recommended.Relationship.Components, value => Assert.True(value.HasRuleBinding));
                Assert.True(recommended.Relationship.Parent.HasRuleBinding);

                var fromHide = Assert.Single(await relationships.GetForConceptAsync(hide.Concept.Id));
                Assert.Equal("component", fromHide.ConceptRole);
                Assert.Equal("skill-composite.stealth", fromHide.Relationship.Key);

                Assert.Empty(await relationships.GetForConceptAsync(search.Concept.Id));
                Assert.Empty(await relationships.GetForConceptAsync(escapeArtist.Concept.Id));

                var overridden = await relationships.SetRulingAsync(
                    "skill-composite.stealth",
                    new SetMechanicalRelationshipRulingRequest(
                        MechanicalRelationshipResolutionKinds.IndependentParent,
                        "Fixture override"),
                    actor);
                Assert.True(overridden.IsOverridden);
                Assert.Equal(MechanicalRelationshipResolutionKinds.IndependentParent, overridden.EffectiveResolutionKind);
                Assert.Equal(1, overridden.LatestRuling!.RulingNumber);

                var reloaded = Assert.Single(await relationships.GetForConceptAsync(stealth.Concept.Id));
                Assert.True(reloaded.IsOverridden);
                Assert.True(reloaded.RequiresAdjudication);
                Assert.Equal(MechanicalRelationshipResolutionKinds.IndependentParent, reloaded.EffectiveResolutionKind);
            }
            finally
            {
                await DeleteRelationshipRulingsAsync(db, actor);
                await DeleteAcceptedConceptsAsync(db, accepted);
                await DeletePackageAsync(db, pcgenPackage);
                await DeletePackageAsync(db, umbrellaPackage);
            }
        }
    }

    private static NormalizedSourceRepresentation PcGenRepresentation(
        string token,
        IReadOnlyList<string> names)
    {
        var sourceShort = $"CR{token}";
        var fileName = "data/35e/example/composite_skills.lst";
        var text = string.Join('\n',
            new[] { $"SOURCELONG:Composite Skills Fixture {token}\tSOURCESHORT:{sourceShort}" }
                .Concat(names.Select(name => $"{name}\tKEYSTAT:DEX")));
        return new PcGenSourceFormatAdapter().TryRead(new SourceRepresentationArtifact(
            fileName,
            Encoding.UTF8.GetBytes(text),
            $"integration:composite-skills:{token}#{fileName}"))
            ?? throw new InvalidOperationException("PCGen composite skill fixture was not readable.");
    }

    private static NormalizedSourceRepresentation FiveESkillRepresentation(string token, string name)
    {
        var source = $"CS{token}";
        var raw = JsonSerializer.Serialize(new
        {
            name,
            source,
            entries = new[] { $"Integration fixture for {name}." }
        });
        return new NormalizedSourceRepresentation(
            FiveEToolsSourceFormatAdapter.Format,
            new SourceRepresentationArtifact(
                $"skill-{token}.json",
                Encoding.UTF8.GetBytes(raw),
                $"integration:composite-umbrella:{token}"),
            [new NormalizedSourceRecord(
                "skill",
                name,
                source,
                $"skill|{name}|{source}",
                raw,
                PublicationLocalKey: source)],
            [new NormalizedSourcePublication(
                source,
                $"Composite Umbrella Fixture {token}",
                "Integration Test Press",
                "5e",
                new DateOnly(2014, 8, 19))]);
    }

    private static async Task<Guid> ReadCanonicalEntityIdAsync(RulesCoreDbContext db, Guid sourceEntityId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT occurrence.canonical_entity_id
                FROM source_entity_revision revision
                JOIN source_entity_occurrence_binding binding
                    ON binding.source_entity_revision_id = revision.source_entity_revision_id
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                WHERE revision.source_entity_id = @source_entity_id
                    AND occurrence.canonical_entity_id IS NOT NULL
                ORDER BY revision.revision_number DESC
                LIMIT 1;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            return (Guid)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Canonical entity was not resolved."));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task DeleteRelationshipRulingsAsync(RulesCoreDbContext db, string actor)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($$"""
            DELETE FROM rule_mechanical_relationship_ruling
            WHERE created_by_user_id = {{actor}};
            """);
    }

    private static async Task DeleteAcceptedConceptsAsync(
        RulesCoreDbContext db,
        IReadOnlyCollection<AcceptedSourceNormalizationView> accepted)
    {
        var bindingIds = accepted
            .Where(value => value.CreatedBinding)
            .Select(value => value.Binding.Id)
            .ToArray();
        if (bindingIds.Length > 0)
        {
            var bindings = await db.RuleConceptSourceBindings
                .Where(value => bindingIds.Contains(value.Id))
                .ToArrayAsync();
            db.RuleConceptSourceBindings.RemoveRange(bindings);
            await db.SaveChangesAsync();
        }

        var conceptIds = accepted
            .Where(value => value.CreatedConcept)
            .Select(value => value.Concept.Id)
            .ToArray();
        if (conceptIds.Length > 0)
        {
            var concepts = await db.RuleConcepts
                .Where(value => conceptIds.Contains(value.Id))
                .ToArrayAsync();
            db.RuleConcepts.RemoveRange(concepts);
            await db.SaveChangesAsync();
        }
    }

    private static async Task DeletePackageAsync(RulesCoreDbContext db, string packageKey)
    {
        var package = await db.SourcePackages.SingleOrDefaultAsync(value => value.Key == packageKey);
        if (package is null) return;
        db.SourcePackages.Remove(package);
        await db.SaveChangesAsync();
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

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
