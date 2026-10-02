using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

public sealed record RuleConceptCoverageRepairResult(
    int HistoriesExamined,
    int HistoriesAlreadyCovered,
    int ConceptsCreated,
    int ExistingConceptsReused,
    int BindingsCreated,
    int KeyCollisionsResolved);

/// <summary>
/// Ensures every non-ignored canonical revision/rename history with Source Layer content has a
/// Rules Layer concept anchor. Existing concept-backed histories are left untouched; this repair
/// only fills gaps created by historical reconciliation or legacy normalization state.
/// </summary>
public sealed class RuleConceptCoverageRepairService(RulesCoreDbContext dbContext)
{
    private const string AdvisoryLockIdentity = "rules-core-rule-concept-coverage-repair-v1";

    public async Task<RuleConceptCoverageRepairResult> RepairAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = RequireActor(actorUserId);
        await GlobalSourceDispositionService.EnsureSchemaAsync(dbContext, cancellationToken);
        await RuleConceptRelationshipStore.EnsureSchemaAsync(dbContext, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await AcquireRepairLockAsync(cancellationToken);

        var sourceMappings = await ReadSourceCanonicalMappingsAsync(cancellationToken);
        if (sourceMappings.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return new RuleConceptCoverageRepairResult(0, 0, 0, 0, 0, 0);
        }

        var mappedSourceIds = sourceMappings.Select(value => value.SourceEntityId).Distinct().ToArray();
        var sourceEntities = await dbContext.SourceEntities
            .AsNoTracking()
            .Where(value => mappedSourceIds.Contains(value.Id))
            .Select(value => new SourceMetadata(
                value.Id,
                value.EntityType,
                value.Name,
                value.NativeIdentityJson,
                value.SourceCode ?? string.Empty))
            .ToArrayAsync(cancellationToken);
        var sourceById = sourceEntities.ToDictionary(value => value.Id);

        var historyEdges = await ReadHistoryEdgesAsync(cancellationToken);
        var bindings = await dbContext.RuleConceptSourceBindings
            .AsNoTracking()
            .Select(value => new BindingMetadata(
                value.RuleConceptId,
                value.CanonicalEntityId))
            .ToArrayAsync(cancellationToken);

        var union = new UnionFind(
            sourceMappings.Select(value => value.CanonicalEntityId)
                .Concat(historyEdges.SelectMany(value => new[] { value.FromId, value.ToId }))
                .Concat(bindings.Select(value => value.CanonicalEntityId)));
        foreach (var edge in historyEdges)
        {
            union.Union(edge.FromId, edge.ToId);
        }

        var sources = sourceMappings
            .Where(value => sourceById.ContainsKey(value.SourceEntityId))
            .Select(value => new SourceRow(
                value.CanonicalEntityId,
                sourceById[value.SourceEntityId]))
            .ToArray();
        var sourceGroups = sources
            .GroupBy(value => union.Find(value.CanonicalEntityId))
            .OrderBy(group => group.Key)
            .ToArray();
        var boundRoots = bindings
            .Select(value => union.Find(value.CanonicalEntityId))
            .ToHashSet();

        var existingConcepts = await dbContext.RuleConcepts
            .AsNoTracking()
            .Select(value => new ConceptMetadata(
                value.Id,
                value.Key,
                value.EntityType,
                value.DisplayName))
            .ToArrayAsync(cancellationToken);
        var conceptsByKey = existingConcepts
            .ToDictionary(value => value.Key, StringComparer.Ordinal);
        var boundConceptIds = bindings.Select(value => value.RuleConceptId).ToHashSet();

        var incomingHistoryIds = historyEdges.Select(value => value.ToId).ToHashSet();
        var canonicalIdsByRoot = union.Values
            .GroupBy(union.Find)
            .ToDictionary(group => group.Key, group => group.ToHashSet());

        var historiesExamined = 0;
        var historiesAlreadyCovered = 0;
        var conceptsCreated = 0;
        var existingConceptsReused = 0;
        var bindingsCreated = 0;
        var keyCollisionsResolved = 0;

        foreach (var group in sourceGroups)
        {
            historiesExamined += 1;
            if (boundRoots.Contains(group.Key))
            {
                historiesAlreadyCovered += 1;
                continue;
            }

            var componentIds = canonicalIdsByRoot.GetValueOrDefault(group.Key) ?? group
                .Select(value => value.CanonicalEntityId)
                .ToHashSet();
            var directedRoots = componentIds
                .Where(value => !incomingHistoryIds.Contains(value))
                .ToHashSet();
            var anchorCandidates = group
                .Where(value => directedRoots.Contains(value.CanonicalEntityId))
                .ToArray();
            if (anchorCandidates.Length == 0)
            {
                anchorCandidates = group.ToArray();
            }

            var anchor = anchorCandidates
                .OrderBy(value => value.CanonicalEntityId)
                .ThenBy(value => value.Source.SourceCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.Source.Id)
                .First();
            var normalizedType = RuleConceptEntityTypes.Normalize(anchor.Source.EntityType);
            var baseKey = SourceNormalizationService.BuildSuggestedConceptKey(
                anchor.Source.EntityType,
                anchor.Source.Name,
                anchor.Source.NativeIdentityJson);

            var concept = FindReusableConcept(
                baseKey,
                normalizedType,
                conceptsByKey,
                boundConceptIds);
            if (concept is null)
            {
                var key = baseKey;
                if (conceptsByKey.ContainsKey(key))
                {
                    key = BuildCollisionKey(baseKey, group.Key);
                    keyCollisionsResolved += 1;
                    concept = FindReusableConcept(
                        key,
                        normalizedType,
                        conceptsByKey,
                        boundConceptIds);
                }

                if (concept is null)
                {
                    if (conceptsByKey.TryGetValue(key, out var conflicting))
                    {
                        throw new InvalidOperationException(
                            $"Rule concept coverage repair can not safely use key '{key}' because it belongs to " +
                            $"concept '{conflicting.Id}' with incompatible or already-bound state.");
                    }

                    var entity = new RuleConcept
                    {
                        Id = Guid.NewGuid(),
                        Key = key,
                        EntityType = normalizedType,
                        DisplayName = anchor.Source.Name.Trim(),
                        CreatedByUserId = actor,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    dbContext.RuleConcepts.Add(entity);
                    concept = new ConceptMetadata(
                        entity.Id,
                        entity.Key,
                        entity.EntityType,
                        entity.DisplayName);
                    conceptsByKey.Add(entity.Key, concept);
                    conceptsCreated += 1;
                }
                else
                {
                    existingConceptsReused += 1;
                }
            }
            else
            {
                existingConceptsReused += 1;
            }

            dbContext.RuleConceptSourceBindings.Add(new RuleConceptSourceBinding
            {
                Id = Guid.NewGuid(),
                RuleConceptId = concept.Id,
                CanonicalEntityId = anchor.CanonicalEntityId,
                SourceEntityId = anchor.Source.Id,
                CreatedByUserId = actor,
                CreatedAt = DateTimeOffset.UtcNow
            });
            boundConceptIds.Add(concept.Id);
            boundRoots.Add(group.Key);
            bindingsCreated += 1;
        }

        if (bindingsCreated > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await RuleConceptRelationshipStore.SynchronizeSubclassParentsAsync(
                dbContext,
                actor,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new RuleConceptCoverageRepairResult(
            historiesExamined,
            historiesAlreadyCovered,
            conceptsCreated,
            existingConceptsReused,
            bindingsCreated,
            keyCollisionsResolved);
    }

    private static ConceptMetadata? FindReusableConcept(
        string key,
        string normalizedType,
        IReadOnlyDictionary<string, ConceptMetadata> conceptsByKey,
        IReadOnlySet<Guid> boundConceptIds)
    {
        if (!conceptsByKey.TryGetValue(key, out var concept)
            || boundConceptIds.Contains(concept.Id)
            || !string.Equals(
                RuleConceptEntityTypes.Normalize(concept.EntityType),
                normalizedType,
                StringComparison.Ordinal))
        {
            return null;
        }

        return concept;
    }

    private static string BuildCollisionKey(string baseKey, Guid componentId)
    {
        var suffix = $"-{componentId:N}";
        var available = Math.Max(1, 300 - suffix.Length);
        var prefix = baseKey.Length <= available
            ? baseKey
            : baseKey[..available].TrimEnd('.', '-');
        if (string.IsNullOrWhiteSpace(prefix)) prefix = "entity";
        return $"{prefix}{suffix}";
    }

    private async Task AcquireRepairLockAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@identity, 0));";
        AddParameter(command, "@identity", AdvisoryLockIdentity);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SourceCanonicalMapping>> ReadSourceCanonicalMappingsAsync(
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            WITH latest_revision AS (
                SELECT DISTINCT ON (revision.source_entity_id)
                    revision.source_entity_id,
                    revision.source_entity_revision_id
                FROM source_entity_revision revision
                ORDER BY revision.source_entity_id,
                         revision.revision_number DESC,
                         revision.imported_at DESC,
                         revision.source_entity_revision_id
            )
            SELECT latest.source_entity_id,
                   occurrence.canonical_entity_id
            FROM latest_revision latest
            JOIN source_entity source
              ON source.source_entity_id = latest.source_entity_id
            JOIN source_entity_occurrence_binding binding
              ON binding.source_entity_revision_id = latest.source_entity_revision_id
            JOIN canonical_source_occurrence occurrence
              ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
            WHERE occurrence.canonical_entity_id IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1
                  FROM global_source_disposition disposition
                  WHERE disposition.source_package_id = source.source_package_id
                    AND disposition.restored_at IS NULL)
            ORDER BY latest.source_entity_id;
            """;
        var rows = new List<SourceCanonicalMapping>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new SourceCanonicalMapping(reader.GetGuid(0), reader.GetGuid(1)));
        }
        return rows;
    }

    private async Task<IReadOnlyList<HistoryEdge>> ReadHistoryEdgesAsync(
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            SELECT from_canonical_entity_id,
                   to_canonical_entity_id
            FROM canonical_entity_relationship
            WHERE relationship_kind IN ('revision', 'rename')
            ORDER BY from_canonical_entity_id, to_canonical_entity_id;
            """;
        var rows = new List<HistoryEdge>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new HistoryEdge(reader.GetGuid(0), reader.GetGuid(1)));
        }
        return rows;
    }

    private static string RequireActor(string actorUserId)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            throw new ArgumentException("Actor user ID can not be blank.", nameof(actorUserId));
        }
        var actor = actorUserId.Trim();
        if (actor.Length > 200)
        {
            throw new ArgumentException("Actor user ID can not exceed 200 characters.", nameof(actorUserId));
        }
        return actor;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record SourceCanonicalMapping(Guid SourceEntityId, Guid CanonicalEntityId);
    private sealed record SourceMetadata(
        Guid Id,
        string EntityType,
        string Name,
        string? NativeIdentityJson,
        string SourceCode);
    private sealed record SourceRow(Guid CanonicalEntityId, SourceMetadata Source);
    private sealed record HistoryEdge(Guid FromId, Guid ToId);
    private sealed record BindingMetadata(Guid RuleConceptId, Guid CanonicalEntityId);
    private sealed record ConceptMetadata(Guid Id, string Key, string EntityType, string DisplayName);

    private sealed class UnionFind
    {
        private readonly Dictionary<Guid, Guid> parent;

        public UnionFind(IEnumerable<Guid> values)
        {
            parent = values.Distinct().ToDictionary(value => value);
        }

        public IEnumerable<Guid> Values => parent.Keys;

        public Guid Find(Guid value)
        {
            if (!parent.ContainsKey(value)) parent[value] = value;
            var current = value;
            while (parent[current] != current)
            {
                current = parent[current];
            }
            var root = current;
            current = value;
            while (parent[current] != current)
            {
                var next = parent[current];
                parent[current] = root;
                current = next;
            }
            return root;
        }

        public void Union(Guid left, Guid right)
        {
            var leftRoot = Find(left);
            var rightRoot = Find(right);
            if (leftRoot != rightRoot) parent[rightRoot] = leftRoot;
        }
    }
}
