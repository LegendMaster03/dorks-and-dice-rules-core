using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

internal static class SpeciesRelationshipSynchronizer
{
    public static async Task SynchronizeAsync(
        RulesCoreDbContext dbContext,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUserId);

        IDbContextTransaction? ownedTransaction = null;
        if (dbContext.Database.CurrentTransaction is null)
        {
            ownedTransaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
        }

        try
        {
            var bindings = await ReadBoundAncestrySourcesAsync(dbContext, cancellationToken);
            var species = bindings
                .Where(value => string.Equals(
                    RuleConceptEntityTypes.Normalize(value.EntityType),
                    RuleConceptEntityTypes.Species,
                    StringComparison.Ordinal))
                .ToArray();
            var desired = new HashSet<(Guid FromRuleConceptId, Guid ToRuleConceptId)>();

            foreach (var subspecies in bindings.Where(value => string.Equals(
                         RuleConceptEntityTypes.Normalize(value.EntityType),
                         RuleConceptEntityTypes.Subspecies,
                         StringComparison.Ordinal)))
            {
                if (!TryReadParentSpeciesIdentity(
                        subspecies.NativeIdentityJson,
                        out var speciesName,
                        out var speciesSource))
                {
                    continue;
                }

                var parents = species
                    .Where(candidate => string.Equals(
                        candidate.SourceEntityName,
                        speciesName,
                        StringComparison.OrdinalIgnoreCase))
                    .Where(candidate => speciesSource is null
                        || string.Equals(candidate.SourceCode, speciesSource, StringComparison.OrdinalIgnoreCase))
                    .Select(candidate => candidate.RuleConceptId)
                    .Distinct()
                    .ToArray();

                foreach (var parent in parents)
                {
                    if (parent != subspecies.RuleConceptId)
                    {
                        desired.Add((subspecies.RuleConceptId, parent));
                    }
                }
            }

            var existing = await ReadExistingAsync(dbContext, cancellationToken);
            foreach (var relationship in existing.Where(value => !desired.Contains(value)))
            {
                await DeleteAsync(dbContext, relationship, cancellationToken);
            }
            foreach (var relationship in desired)
            {
                await InsertAsync(dbContext, relationship, actorUserId.Trim(), cancellationToken);
            }

            if (ownedTransaction is not null)
            {
                await ownedTransaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
        finally
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.DisposeAsync();
            }
        }
    }

    internal static bool TryReadParentSpeciesIdentity(
        string nativeIdentityJson,
        out string speciesName,
        out string? speciesSource)
    {
        speciesName = string.Empty;
        speciesSource = null;
        if (string.IsNullOrWhiteSpace(nativeIdentityJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(nativeIdentityJson);
            if (!document.RootElement.TryGetProperty("raceName", out var name)
                || name.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(name.GetString()))
            {
                return false;
            }

            speciesName = name.GetString()!.Trim();
            if (document.RootElement.TryGetProperty("raceSource", out var source)
                && source.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(source.GetString()))
            {
                speciesSource = source.GetString()!.Trim();
            }
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task<BoundAncestrySource[]> ReadBoundAncestrySourcesAsync(
        RulesCoreDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var values = new List<BoundAncestrySource>();
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                WITH RECURSIVE concept_entities(rule_concept_id, canonical_entity_id) AS (
                    SELECT binding.rule_concept_id,
                           binding.canonical_entity_id
                    FROM rule_concept_source_binding binding
                    UNION
                    SELECT parent.rule_concept_id,
                           relationship.to_canonical_entity_id
                    FROM canonical_entity_relationship relationship
                    JOIN concept_entities parent
                      ON parent.canonical_entity_id = relationship.from_canonical_entity_id
                    WHERE relationship.relationship_kind = 'revision'
                ),
                bound_sources AS (
                    SELECT DISTINCT concept_entity.rule_concept_id,
                           source.source_entity_id
                    FROM concept_entities concept_entity
                    JOIN canonical_source_occurrence occurrence
                      ON occurrence.canonical_entity_id = concept_entity.canonical_entity_id
                    JOIN source_entity_occurrence_binding occurrence_binding
                      ON occurrence_binding.canonical_source_occurrence_id = occurrence.canonical_source_occurrence_id
                    JOIN source_entity_revision revision
                      ON revision.source_entity_revision_id = occurrence_binding.source_entity_revision_id
                    JOIN source_entity source
                      ON source.source_entity_id = revision.source_entity_id
                )
                SELECT bound_source.rule_concept_id,
                       concept.entity_type,
                       source.entity_name,
                       source.source_code,
                       source.native_identity_json::text
                FROM bound_sources bound_source
                JOIN rule_concept concept
                  ON concept.rule_concept_id = bound_source.rule_concept_id
                JOIN source_entity source
                  ON source.source_entity_id = bound_source.source_entity_id
                WHERE lower(concept.entity_type) IN ('race', 'species', 'subrace', 'subspecies');
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                values.Add(new BoundAncestrySource(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetString(4)));
            }
            return values.ToArray();
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<HashSet<(Guid FromRuleConceptId, Guid ToRuleConceptId)>> ReadExistingAsync(
        RulesCoreDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var result = new HashSet<(Guid FromRuleConceptId, Guid ToRuleConceptId)>();
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT from_rule_concept_id, to_rule_concept_id
                FROM rule_concept_relationship
                WHERE relationship_kind = @kind;
                """;
            AddParameter(command, "kind", RuleConceptRelationshipKinds.ParentSpecies);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add((reader.GetGuid(0), reader.GetGuid(1)));
            }
            return result;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static Task InsertAsync(
        RulesCoreDbContext dbContext,
        (Guid FromRuleConceptId, Guid ToRuleConceptId) relationship,
        string actorUserId,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO rule_concept_relationship (
                rule_concept_relationship_id,
                from_rule_concept_id,
                to_rule_concept_id,
                relationship_kind,
                created_by_user_id,
                created_at)
            VALUES (
                {Guid.NewGuid()},
                {relationship.FromRuleConceptId},
                {relationship.ToRuleConceptId},
                {RuleConceptRelationshipKinds.ParentSpecies},
                {actorUserId},
                {DateTimeOffset.UtcNow})
            ON CONFLICT (from_rule_concept_id, to_rule_concept_id, relationship_kind) DO NOTHING;
            """, cancellationToken);

    private static Task DeleteAsync(
        RulesCoreDbContext dbContext,
        (Guid FromRuleConceptId, Guid ToRuleConceptId) relationship,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM rule_concept_relationship
            WHERE from_rule_concept_id = {relationship.FromRuleConceptId}
              AND to_rule_concept_id = {relationship.ToRuleConceptId}
              AND relationship_kind = {RuleConceptRelationshipKinds.ParentSpecies};
            """, cancellationToken);

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record BoundAncestrySource(
        Guid RuleConceptId,
        string EntityType,
        string SourceEntityName,
        string? SourceCode,
        string NativeIdentityJson);
}
